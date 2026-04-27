using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using WinSentryAI.Models;

namespace WinSentryAI.Services
{
    public sealed class OpenAIAIService : IAIService
    {
        private const string SecretKey = "OpenAiApiKey";
        private const string Endpoint = "https://api.openai.com/v1/chat/completions";

        private readonly IDatabaseService _db;
        private readonly ISettingsService _settings;
        private readonly HttpClient _http;

        public string ProviderId => "openai";
        public string ModelName => _settings.Get("AI", "OpenAiModel", "gpt-4o");

        public OpenAIAIService(IDatabaseService db, ISettingsService settings, HttpClient? http = null)
        {
            _db = db;
            _settings = settings;
            _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
        {
            var key = await _db.GetSecretAsync(SecretKey);
            return !string.IsNullOrWhiteSpace(key);
        }

        public async Task<AIAnalysisResponse> AnalyzeEventAsync(AIAnalysisRequest request, CancellationToken ct = default)
        {
            string systemPrompt = PromptBuilder.BuildSystemPrompt(request.Language);
            var redactionContext = ShouldRedact() ? new SubstitutionContext() : null;
            string userMessage = PromptBuilder.BuildUserMessage(
                request.TriggerEvent, request.ContextLogs, request.SystemSnapshot, redactionContext);

            var messages = new List<OpenAiMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userMessage }
            };

            var result = await ExecuteRequestAsync(systemPrompt, userMessage, messages, ct);
            return result with { RedactionMap = redactionContext?.Map ?? new Dictionary<string, string>() };
        }

        public async Task<string> SendChatAsync(string systemPrompt, IList<ChatMessage> chatHistory, CancellationToken ct = default)
        {
            var messages = new List<OpenAiMessage> { new() { Role = "system", Content = systemPrompt } };
            messages.AddRange(chatHistory.Select(m => new OpenAiMessage 
            { 
                Role = m.Role == ChatRole.User ? "user" : "assistant", 
                Content = m.Content 
            }));

            var response = await ExecuteRequestAsync(systemPrompt, string.Empty, messages, ct);
            return response.IsSuccess ? (response.Response ?? string.Empty) : $"[Error] {response.ErrorMessage}";
        }

        private async Task<AIAnalysisResponse> ExecuteRequestAsync(string sys, string usr, List<OpenAiMessage> messages, CancellationToken ct)
        {
            string model = ModelName;
            string? apiKey;
            try { apiKey = await _db.GetSecretAsync(SecretKey); }
            catch (Exception ex) { return new(false, sys, usr, null, $"Secure storage error: {ex.Message}", model); }

            if (string.IsNullOrWhiteSpace(apiKey))
                return new(false, sys, usr, null, "[No Key] OpenAI API key not configured. Open Settings → AI to add your key.", model);

            var payload = new OpenAiRequest { Model = model, Messages = messages.ToArray(), Stream = false };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = JsonContent.Create(payload);

                using var resp = await _http.SendAsync(request, ct);
                string body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                    return new(false, sys, usr, null, $"OpenAI HTTP {(int)resp.StatusCode}: {body}", model);

                using var doc = JsonDocument.Parse(body);
                string text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                return new(true, sys, usr, text, null, model);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "OpenAI API call failed.");
                return new(false, sys, usr, null, ex.Message, model);
            }
        }

        private bool ShouldRedact() =>
            _settings.GetBool("AI", "EnableRedaction", true);

        private class OpenAiRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("messages")] public OpenAiMessage[] Messages { get; set; } = Array.Empty<OpenAiMessage>();
            [JsonPropertyName("stream")] public bool Stream { get; set; }
        }

        private class OpenAiMessage
        {
            [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
            [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
        }
    }
}
