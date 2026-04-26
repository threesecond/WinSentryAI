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
            string userMessage = PromptBuilder.BuildUserMessage(request.TriggerEvent, request.ContextLogs, request.SystemSnapshot);

            var messages = new List<ClaudeMessage> { new() { Role = "user", Content = userMessage } };
            return await ExecuteRequestAsync(systemPrompt, userMessage, messages, ct);
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
            catch (Exception ex) { return new(false, sys, usr, null, $"Secure storage error: {ex.Message}", model); }

            if (string.IsNullOrWhiteSpace(apiKey))
                return new(false, sys, usr, null, "[No Key] Claude API key not configured. Open Settings → AI to add your key.", model);

            var payload = new ClaudeRequest { Model = model, System = sys, Messages = messages.ToArray(), MaxTokens = 4096, Stream = false };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
                request.Headers.Add("x-api-key", apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = JsonContent.Create(payload);

                using var resp = await _http.SendAsync(request, ct);
                string body = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                    return new(false, sys, usr, null, $"Claude HTTP {(int)resp.StatusCode}: {body}", model);

                using var doc = JsonDocument.Parse(body);
                string text = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
                return new(true, sys, usr, text, null, model);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Claude API call failed.");
                return new(false, sys, usr, null, ex.Message, model);
            }
        }

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
