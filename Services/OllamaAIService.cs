using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using WinSentryAI.Models;

namespace WinSentryAI.Services
{
    public sealed class OllamaAIService : IAIService
    {
        private readonly ISettingsService _settings;
        private readonly HttpClient _http;

        public string ProviderId => "ollama";
        public string ModelName => _settings.Get("AI", "OllamaModel", "llama3");
        private string Endpoint => _settings.Get("AI", "OllamaEndpoint", "http://localhost:11434");

        public OllamaAIService(ISettingsService settings, HttpClient? http = null)
        {
            _settings = settings;
            _http = http ?? SharedHttpClientProvider.Default;
        }

        public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.GetAsync($"{Endpoint}/api/tags", ct);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to connect to Ollama at {Endpoint}", Endpoint);
                return false;
            }
        }

        public async Task<AIAnalysisResponse> AnalyzeEventAsync(AIAnalysisRequest request, CancellationToken ct = default)
        {
            string systemPrompt = PromptBuilder.BuildSystemPrompt(request.Language);
            string userMessage = PromptBuilder.BuildUserMessage(
                request.TriggerEvent, request.ContextLogs, request.SystemSnapshot);
            
            var messages = new List<OllamaMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userMessage }
            };

            var payload = new OllamaChatRequest
            {
                Model = ModelName,
                Messages = messages.ToArray(),
                Stream = false
            };

            int maxRetries = Math.Clamp(_settings.GetInt("ErrorHandling", "MaxRetryCount", 3), 1, 10);
            var attempt = await AIHttpRetryPolicy.ExecuteAsync(
                "Ollama",
                maxRetries,
                token => SendRequestOnceAsync(systemPrompt, userMessage, payload, token),
                ct);

            return attempt.Value ?? new AIAnalysisResponse(false, systemPrompt, userMessage, null,
                attempt.ErrorMessage ?? "Ollama request failed.",
                ModelName);
        }

        private async Task<AIHttpAttempt<AIAnalysisResponse>> SendRequestOnceAsync(
            string systemPrompt,
            string userMessage,
            OllamaChatRequest payload,
            CancellationToken ct)
        {
            try
            {
                using var resp = await _http.PostAsJsonAsync($"{Endpoint}/api/chat", payload, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    string err = await resp.Content.ReadAsStringAsync(ct);
                    var error = AIHttpErrorClassifier.ClassifyHttpError("Ollama", resp.StatusCode, err);
                    return AIHttpAttempt<AIAnalysisResponse>.Failure(error.Message, error.IsRetryable);
                }

                var result = await resp.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct);
                return AIHttpAttempt<AIAnalysisResponse>.Success(
                    new AIAnalysisResponse(true, systemPrompt, userMessage, result?.Message?.Content, null, ModelName));
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                var error = AIHttpErrorClassifier.ClassifyException("Ollama", new TaskCanceledException(), cancellationRequested: true);
                return AIHttpAttempt<AIAnalysisResponse>.Failure(error.Message, error.IsRetryable);
            }
            catch (TaskCanceledException ex)
            {
                var error = AIHttpErrorClassifier.ClassifyException("Ollama", ex, cancellationRequested: false);
                return AIHttpAttempt<AIAnalysisResponse>.Failure(error.Message, error.IsRetryable);
            }
            catch (HttpRequestException ex)
            {
                var error = AIHttpErrorClassifier.ClassifyException("Ollama", ex, cancellationRequested: false);
                return AIHttpAttempt<AIAnalysisResponse>.Failure(error.Message, error.IsRetryable);
            }
            catch (Exception ex)
            {
                var error = AIHttpErrorClassifier.ClassifyException("Ollama", ex, cancellationRequested: false);
                return AIHttpAttempt<AIAnalysisResponse>.Failure(error.Message, error.IsRetryable);
            }
        }

        public async Task<string> SendChatAsync(string systemPrompt, IList<ChatMessage> chatHistory, CancellationToken ct = default)
        {
            var messages = new List<OllamaMessage> { new() { Role = "system", Content = systemPrompt } };
            messages.AddRange(chatHistory.Select(m => new OllamaMessage 
            { 
                Role = m.Role == ChatRole.User ? "user" : "assistant", 
                Content = m.Content 
            }));

            var payload = new OllamaChatRequest
            {
                Model = ModelName,
                Messages = messages.ToArray(),
                Stream = false
            };

            int maxRetries = Math.Clamp(_settings.GetInt("ErrorHandling", "MaxRetryCount", 3), 1, 10);
            var attempt = await AIHttpRetryPolicy.ExecuteAsync(
                "Ollama",
                maxRetries,
                SendChatOnceAsync,
                ct);

            return attempt.Value ?? $"[Error] {attempt.ErrorMessage ?? "Ollama request failed."}";

            async Task<AIHttpAttempt<string>> SendChatOnceAsync(CancellationToken token)
            {
            try
            {
                    using var resp = await _http.PostAsJsonAsync($"{Endpoint}/api/chat", payload, token);
                if (!resp.IsSuccessStatusCode)
                {
                        string err = await resp.Content.ReadAsStringAsync(token);
                        var error = AIHttpErrorClassifier.ClassifyHttpError("Ollama", resp.StatusCode, err);
                        return AIHttpAttempt<string>.Failure(error.Message, error.IsRetryable);
                }

                    var result = await resp.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: token);
                    return AIHttpAttempt<string>.Success(result?.Message?.Content ?? "[Error] Empty response from Ollama.");
                }
                catch (TaskCanceledException) when (token.IsCancellationRequested)
                {
                    var error = AIHttpErrorClassifier.ClassifyException("Ollama", new TaskCanceledException(), cancellationRequested: true);
                    return AIHttpAttempt<string>.Failure(error.Message, error.IsRetryable);
                }
                catch (TaskCanceledException ex)
                {
                    var error = AIHttpErrorClassifier.ClassifyException("Ollama", ex, cancellationRequested: false);
                    return AIHttpAttempt<string>.Failure(error.Message, error.IsRetryable);
                }
                catch (HttpRequestException ex)
                {
                    var error = AIHttpErrorClassifier.ClassifyException("Ollama", ex, cancellationRequested: false);
                    return AIHttpAttempt<string>.Failure(error.Message, error.IsRetryable);
                }
                catch (Exception ex)
                {
                    var error = AIHttpErrorClassifier.ClassifyException("Ollama", ex, cancellationRequested: false);
                    return AIHttpAttempt<string>.Failure(error.Message, error.IsRetryable);
                }
            }
        }

        // 內部 DTO
        private sealed class OllamaChatRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("messages")] public OllamaMessage[] Messages { get; set; } = Array.Empty<OllamaMessage>();
            [JsonPropertyName("stream")] public bool Stream { get; set; }
        }

        private sealed class OllamaMessage
        {
            [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
            [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
        }

        private sealed class OllamaChatResponse
        {
            [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
        }
    }
}
