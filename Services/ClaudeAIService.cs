using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using WinSentryAI.Models;

namespace WinSentryAI.Services
{
    public sealed class ClaudeAIService : IAIService
    {
        private const string SecretKey = "ClaudeApiKey";
        private const string Endpoint = "https://api.anthropic.com/v1/messages";

        private readonly IDatabaseService _db;
        private readonly ISettingsService _settings;
        private readonly HttpClient _http;

        public string ProviderId => "claude";
        public string ModelName => _settings.Get("AI", "ClaudeModel", "claude-3-7-sonnet-latest");

        public ClaudeAIService(IDatabaseService db, ISettingsService settings, HttpClient? http = null)
        {
            _db = db;
            _settings = settings;
            _http = http ?? SharedHttpClientProvider.Default;
        }

        public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
        {
            try
            {
                var key = await _db.GetSecretAsync(SecretKey);
                return !string.IsNullOrWhiteSpace(key);
            }
            catch (SecretDecryptionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to check Claude API key presence.");
                return false;
            }
        }

        public async Task<IReadOnlyList<string>> FetchModelsAsync(CancellationToken ct = default)
        {
            string? apiKey = await _db.GetSecretAsync(SecretKey);
            if (string.IsNullOrWhiteSpace(apiKey))
                return Array.Empty<string>();

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models?limit=1000");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            using var resp = await _http.SendAsync(request, ct);
            string body = await resp.Content.ReadAsStringAsync(ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return Array.Empty<string>();

            return data.EnumerateArray()
                .Select(m => m.TryGetProperty("id", out var id) ? id.GetString() : null)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<AIAnalysisResponse> AnalyzeEventAsync(AIAnalysisRequest request, CancellationToken ct = default)
        {
            string systemPrompt = PromptBuilder.BuildSystemPrompt(request.Language);
            var redactionContext = ShouldRedact() ? new SubstitutionContext() : null;
            string userMessage = PromptBuilder.BuildUserMessage(
                request.TriggerEvent, request.ContextLogs, request.SystemSnapshot, redactionContext);

            var messages = new List<ClaudeMessage> { new() { Role = "user", Content = userMessage } };
            var result = await ExecuteRequestAsync(systemPrompt, userMessage, messages, ct);
            return result with { RedactionMap = redactionContext?.Map ?? new Dictionary<string, string>() };
        }

        public async Task<string> SendChatAsync(string systemPrompt, IList<ChatMessage> chatHistory, CancellationToken ct = default)
        {
            var messages = chatHistory.Select(m => new ClaudeMessage 
            { 
                Role = m.Role == ChatRole.User ? "user" : "assistant", 
                Content = m.Content 
            }).ToList();

            var response = await ExecuteRequestAsync(systemPrompt, string.Empty, messages, ct);
            return response.IsSuccess ? (response.Response ?? string.Empty) : $"[Error] {response.ErrorMessage}";
        }

        private async Task<AIAnalysisResponse> ExecuteRequestAsync(string sys, string usr, List<ClaudeMessage> messages, CancellationToken ct)
        {
            string model = ModelName;
            string? apiKey;
            try { apiKey = await _db.GetSecretAsync(SecretKey); }
            catch (SecretDecryptionException)
            {
                return new(false, sys, usr, null,
                    "The saved Claude API key could not be decrypted by the current Windows account. Re-enter the API key in Settings -> AI.",
                    model);
            }
            catch (Exception ex) { return new(false, sys, usr, null, $"Secure storage error: {ex.Message}", model); }

            if (string.IsNullOrWhiteSpace(apiKey))
                return new(false, sys, usr, null, "[No Key] Claude API key not configured. Open Settings → AI to add your key.", model);

            var payload = new ClaudeRequest { Model = model, System = sys, Messages = messages.ToArray(), MaxTokens = 4096, Stream = false };
            int maxRetries = Math.Clamp(_settings.GetInt("ErrorHandling", "MaxRetryCount", 3), 1, 10);

            var attempt = await AIHttpRetryPolicy.ExecuteAsync(
                "Claude",
                maxRetries,
                token => SendRequestOnceAsync(sys, usr, model, apiKey, payload, token),
                ct);

            return attempt.Value ?? new(false, sys, usr, null, attempt.ErrorMessage ?? "Claude request failed.", model);
        }

        private async Task<AIHttpAttempt<AIAnalysisResponse>> SendRequestOnceAsync(
            string sys,
            string usr,
            string model,
            string apiKey,
            ClaudeRequest payload,
            CancellationToken ct)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = JsonContent.Create(payload);

                using var resp = await _http.SendAsync(request, ct);
                string body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    var error = AIHttpErrorClassifier.ClassifyHttpError("Claude", resp.StatusCode, body);
                    return AIHttpAttempt<AIAnalysisResponse>.Failure(error.Message, error.IsRetryable);
                }

                using var doc = JsonDocument.Parse(body);
                string text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
                return AIHttpAttempt<AIAnalysisResponse>.Success(new(true, sys, usr, text, null, model));
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                var error = AIHttpErrorClassifier.ClassifyException("Claude", new TaskCanceledException(), cancellationRequested: true);
                return AIHttpAttempt<AIAnalysisResponse>.Failure(error.Message, error.IsRetryable);
            }
            catch (TaskCanceledException ex)
            {
                Log.Warning(ex, "Claude API call timed out.");
                var error = AIHttpErrorClassifier.ClassifyException("Claude", ex, cancellationRequested: false);
                return AIHttpAttempt<AIAnalysisResponse>.Failure(error.Message, error.IsRetryable);
            }
            catch (HttpRequestException ex)
            {
                Log.Warning(ex, "Claude network request failed.");
                var error = AIHttpErrorClassifier.ClassifyException("Claude", ex, cancellationRequested: false);
                return AIHttpAttempt<AIAnalysisResponse>.Failure(error.Message, error.IsRetryable);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Claude API call failed.");
                var error = AIHttpErrorClassifier.ClassifyException("Claude", ex, cancellationRequested: false);
                return AIHttpAttempt<AIAnalysisResponse>.Failure(error.Message, error.IsRetryable);
            }
        }

        private bool ShouldRedact() =>
            _settings.GetBool("AI", "EnableRedaction", true);

        private class ClaudeRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("system")] public string? System { get; set; }
            [JsonPropertyName("messages")] public ClaudeMessage[] Messages { get; set; } = Array.Empty<ClaudeMessage>();
            [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
            [JsonPropertyName("stream")] public bool Stream { get; set; }
        }

        private class ClaudeMessage
        {
            [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
            [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
        }
    }
}
