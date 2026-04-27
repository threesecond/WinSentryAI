using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using WinSentryAI.Models;

namespace WinSentryAI.Services
{
    /// <summary>
    /// Google Gemini provider。
    /// Retry policy: 指數退避，只對暫時性錯誤重試，auth / bad-request 類立即失敗。
    /// API key 不會出現在 log 中。
    /// </summary>
    public sealed class GeminiAIService : IAIService
    {
        private const string DefaultModel = "gemini-2.0-flash";
        private const string SecretKey = "GeminiApiKey";
        private const string BaseEndpoint = "https://generativelanguage.googleapis.com/v1beta/models";

        private readonly IDatabaseService _db;
        private readonly ISettingsService _settings;
        private readonly HttpClient _http;

        public string ProviderId => "gemini";
        public string ModelName => _settings.Get("AI", "GeminiModel", DefaultModel);

        public GeminiAIService(IDatabaseService db, ISettingsService settings, HttpClient? http = null)
        {
            _db = db;
            _settings = settings;
            _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public async Task<bool> IsConfiguredAsync(CancellationToken ct = default)
        {
            try
            {
                var key = await _db.GetSecretAsync(SecretKey);
                return !string.IsNullOrWhiteSpace(key);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to check Gemini API key presence.");
                return false;
            }
        }

        public async Task<AIAnalysisResponse> AnalyzeEventAsync(AIAnalysisRequest request, CancellationToken ct = default)
        {
            string systemPrompt = PromptBuilder.BuildSystemPrompt(request.Language);
            var redactionContext = ShouldRedact() ? new SubstitutionContext() : null;
            string userMessage = PromptBuilder.BuildUserMessage(
                request.TriggerEvent, request.ContextLogs, request.SystemSnapshot, redactionContext);
            string model = ModelName;

            // 讀 API key
            string? apiKey;
            try
            {
                apiKey = await _db.GetSecretAsync(SecretKey);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to read Gemini API key from secure storage.");
                return Failure(systemPrompt, userMessage, model,
                    "Could not read Gemini API key from secure storage. This may indicate a Windows DPAPI error (e.g., different user account).");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Failure(systemPrompt, userMessage, model,
                    "[No Key] Gemini API key is not configured. Open Settings → AI to add your key.");
            }

            var payload = BuildPayload(systemPrompt, userMessage);

            // URL 組裝後不可進 log，只記 model 名稱
            string url = BuildUrl(model, apiKey);
            int maxRetries = Math.Clamp(_settings.GetInt("ErrorHandling", "MaxRetryCount", 3), 1, 10);

            var result = await ExecuteWithRetryAsync(systemPrompt, userMessage, model, url, payload, maxRetries, ct);
            return result with { RedactionMap = redactionContext?.Map ?? new Dictionary<string, string>() };
        }

        public async Task<string> SendChatAsync(string systemPrompt, IList<ChatMessage> chatHistory, CancellationToken ct = default)
        {
            string model = ModelName;
            string? apiKey;
            try
            {
                apiKey = await _db.GetSecretAsync(SecretKey);
            }
            catch (Exception ex)
            {
                return $"[Error] Failed to read Gemini API key: {ex.Message}";
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "[Error] Gemini API key is not configured.";
            }

            var payload = new GeminiRequest
            {
                SystemInstruction = new GeminiContent
                {
                    Parts = new[] { new GeminiPart { Text = systemPrompt } }
                },
                Contents = chatHistory.Select(m => new GeminiContent
                {
                    Role = m.Role == ChatRole.User ? "user" : "model",
                    Parts = new[] { new GeminiPart { Text = m.Content } }
                }).ToArray(),
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = 0.4,
                    MaxOutputTokens = 2048
                }
            };

            string url = BuildUrl(model, apiKey);

            try
            {
                using var resp = await _http.PostAsJsonAsync(url, payload, JsonOpts, ct);
                string body = await resp.Content.ReadAsStringAsync(ct);

                if (resp.IsSuccessStatusCode)
                {
                    return ExtractText(body) ?? "[Error] Gemini returned an empty response.";
                }

                return $"[Error] {ClassifyHttpError(resp.StatusCode, body)}";
            }
            catch (Exception ex)
            {
                return $"[Error] {ex.Message}";
            }
        }

        public async Task<IReadOnlyList<string>> FetchModelsAsync(CancellationToken ct = default)
        {
            string? apiKey;
            try { apiKey = await _db.GetSecretAsync(SecretKey); }
            catch { return Array.Empty<string>(); }

            if (string.IsNullOrWhiteSpace(apiKey))
                return Array.Empty<string>();

            try
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(apiKey)}";
                using var resp = await _http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return Array.Empty<string>();

                string body = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);

                if (!doc.RootElement.TryGetProperty("models", out var models))
                    return Array.Empty<string>();

                var result = new List<string>();
                foreach (var m in models.EnumerateArray())
                {
                    if (!m.TryGetProperty("name", out var nameEl)) continue;
                    string name = nameEl.GetString() ?? string.Empty;

                    bool supportsGenerate = false;
                    if (m.TryGetProperty("supportedGenerationMethods", out var methods))
                        foreach (var method in methods.EnumerateArray())
                            if (method.GetString() == "generateContent") { supportsGenerate = true; break; }

                    if (supportsGenerate && name.StartsWith("models/"))
                        result.Add(name["models/".Length..]);
                }

                result.Sort();
                return result;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to fetch Gemini model list.");
                return Array.Empty<string>();
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Retry loop
        // ──────────────────────────────────────────────────────────────────────

        private async Task<AIAnalysisResponse> ExecuteWithRetryAsync(
            string systemPrompt, string userMessage, string model,
            string url, GeminiRequest payload,
            int maxRetries, CancellationToken ct)
        {
            string? lastError = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    int delaySec = (int)Math.Pow(2, attempt - 1); // 1s, 2s, 4s …
                    Log.Information("Gemini retry {Attempt}/{Max} after {Delay}s.", attempt, maxRetries - 1, delaySec);
                    try { await Task.Delay(TimeSpan.FromSeconds(delaySec), ct); }
                    catch (TaskCanceledException) { break; }
                }

                var result = await TrySendOnceAsync(systemPrompt, userMessage, model, url, payload, ct);

                if (result.IsSuccess)
                    return result.Response!;

                lastError = result.ErrorMessage;

                if (!result.IsRetryable || ct.IsCancellationRequested)
                    break;

                if (attempt < maxRetries - 1)
                    Log.Warning("Gemini attempt {Attempt} failed (retryable): {Error}", attempt + 1, result.ErrorMessage);
            }

            return Failure(systemPrompt, userMessage, model, lastError ?? "Analysis failed after retries.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // 單次 HTTP 嘗試
        // ──────────────────────────────────────────────────────────────────────

        private async Task<AttemptResult> TrySendOnceAsync(
            string systemPrompt, string userMessage, string model,
            string url, GeminiRequest payload, CancellationToken ct)
        {
            try
            {
                using var resp = await _http.PostAsJsonAsync(url, payload, JsonOpts, ct);
                string body = await resp.Content.ReadAsStringAsync(ct);

                if (resp.IsSuccessStatusCode)
                {
                    string? text = ExtractText(body);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return AttemptResult.Permanent(
                            "[Empty Response] Gemini returned an empty response. The model may have refused to answer or the response was filtered.");
                    }

                    return AttemptResult.Success(new AIAnalysisResponse(
                        IsSuccess: true,
                        SystemPrompt: systemPrompt,
                        UserMessage: userMessage,
                        Response: text,
                        ErrorMessage: null,
                        ModelName: model));
                }

                // 非 2xx — 分類錯誤
                string classifiedError = ClassifyHttpError(resp.StatusCode, body);
                bool retryable = IsRetryableStatusCode(resp.StatusCode);

                Log.Warning("Gemini HTTP {Status} — retryable={Retryable}: {Summary}",
                    (int)resp.StatusCode, retryable, SanitizeSummary(classifiedError));

                return retryable
                    ? AttemptResult.Retryable(classifiedError)
                    : AttemptResult.Permanent(classifiedError);
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                // 使用者取消 — 不重試
                return AttemptResult.Permanent("[Cancelled] Analysis was cancelled.");
            }
            catch (TaskCanceledException ex)
            {
                // HttpClient.Timeout 觸發 — 可重試
                Log.Warning(ex, "Gemini request timed out.");
                return AttemptResult.Retryable("[Timeout] Gemini request timed out. Check your internet connection.");
            }
            catch (HttpRequestException ex)
            {
                Log.Warning(ex, "Network error calling Gemini.");
                return AttemptResult.Retryable($"[Network Error] {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error calling Gemini.");
                return AttemptResult.Permanent($"[Unexpected Error] {ex.GetType().Name}: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // 錯誤分類
        // ──────────────────────────────────────────────────────────────────────

        private static string ClassifyHttpError(HttpStatusCode code, string body)
        {
            string? apiMessage = ExtractApiError(body);
            string baseMsg = apiMessage ?? $"HTTP {(int)code}";

            return code switch
            {
                HttpStatusCode.Unauthorized =>
                    $"[Invalid Key] Authentication failed (401). Your Gemini API key is invalid or expired. Please update it in Settings → AI. Detail: {baseMsg}",

                HttpStatusCode.Forbidden =>
                    $"[Access Denied] Permission denied (403). Your key may not have access to model '{ExtractModelFromError(body)}'. Detail: {baseMsg}",

                (HttpStatusCode)429 =>
                    $"[Quota / Rate Limit] Too many requests (429). You may have exceeded your Gemini free tier quota or hit a rate limit. Wait a moment before retrying. Detail: {baseMsg}",

                HttpStatusCode.BadRequest =>
                    $"[Bad Request] The request was rejected by Gemini (400). This is likely a prompt formatting issue. Detail: {baseMsg}",

                HttpStatusCode.NotFound =>
                    $"[Not Found] Model or endpoint not found (404). Check that the model name in Settings is correct. Detail: {baseMsg}",

                HttpStatusCode.InternalServerError =>
                    $"[Server Error] Gemini internal server error (500). Will retry. Detail: {baseMsg}",

                HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout =>
                    $"[Server Unavailable] Gemini service temporarily unavailable ({(int)code}). Will retry. Detail: {baseMsg}",

                _ => $"[HTTP {(int)code}] {baseMsg}"
            };
        }

        private static bool IsRetryableStatusCode(HttpStatusCode code) => code switch
        {
            (HttpStatusCode)429 => true,               // quota / rate limit — backoff 後重試
            HttpStatusCode.InternalServerError => true, // 500
            HttpStatusCode.BadGateway => true,          // 502
            HttpStatusCode.ServiceUnavailable => true,  // 503
            HttpStatusCode.GatewayTimeout => true,      // 504
            _ => false
        };

        // ──────────────────────────────────────────────────────────────────────
        // 輔助方法
        // ──────────────────────────────────────────────────────────────────────

        private static string BuildUrl(string model, string apiKey) =>
            $"{BaseEndpoint}/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        private static GeminiRequest BuildPayload(string systemPrompt, string userMessage) =>
            new()
            {
                SystemInstruction = new GeminiContent
                {
                    Parts = new[] { new GeminiPart { Text = systemPrompt } }
                },
                Contents = new[]
                {
                    new GeminiContent
                    {
                        Role = "user",
                        Parts = new[] { new GeminiPart { Text = userMessage } }
                    }
                },
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = 0.4,
                    MaxOutputTokens = 2048
                }
            };

        private static AIAnalysisResponse Failure(string sys, string usr, string model, string err) =>
            new(false, sys, usr, null, err, model);

        private bool ShouldRedact() =>
            _settings.GetBool("AI", "EnableRedaction", true);

        // 日誌用：去掉 key prefix 前的括號標籤（已無敏感資訊）
        private static string SanitizeSummary(string msg)
        {
            int idx = msg.IndexOf(']');
            return idx >= 0 ? msg[..(idx + 1)] : msg[..Math.Min(80, msg.Length)];
        }

        private static string? ExtractText(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                    return null;
                var first = candidates[0];
                if (!first.TryGetProperty("content", out var content)) return null;
                if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0) return null;
                var sb = new System.Text.StringBuilder();
                foreach (var p in parts.EnumerateArray())
                    if (p.TryGetProperty("text", out var t)) sb.Append(t.GetString());
                return sb.ToString();
            }
            catch { return null; }
        }

        private static string? ExtractApiError(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err))
                {
                    string status = err.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty;
                    string msg = err.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty;
                    return string.IsNullOrEmpty(status) ? msg : $"{status}: {msg}";
                }
            }
            catch { }
            return null;
        }

        private static string? ExtractModelFromError(string body)
        {
            // 回傳 model name 若 error.message 裡有，否則 null
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err) &&
                    err.TryGetProperty("message", out var m))
                    return m.GetString();
            }
            catch { }
            return null;
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // ──────────────────────────────────────────────────────────────────────
        // 內部 DTO
        // ──────────────────────────────────────────────────────────────────────

        private readonly struct AttemptResult
        {
            public bool IsSuccess { get; }
            public bool IsRetryable { get; }
            public string? ErrorMessage { get; }
            public AIAnalysisResponse? Response { get; }

            private AttemptResult(bool success, bool retryable, string? error, AIAnalysisResponse? response)
            {
                IsSuccess = success;
                IsRetryable = retryable;
                ErrorMessage = error;
                Response = response;
            }

            public static AttemptResult Success(AIAnalysisResponse r) => new(true, false, null, r);
            public static AttemptResult Retryable(string err) => new(false, true, err, null);
            public static AttemptResult Permanent(string err) => new(false, false, err, null);
        }

        private sealed class GeminiRequest
        {
            [JsonPropertyName("systemInstruction")] public GeminiContent? SystemInstruction { get; set; }
            [JsonPropertyName("contents")] public GeminiContent[]? Contents { get; set; }
            [JsonPropertyName("generationConfig")] public GeminiGenerationConfig? GenerationConfig { get; set; }
        }

        private sealed class GeminiContent
        {
            [JsonPropertyName("role")] public string? Role { get; set; }
            [JsonPropertyName("parts")] public GeminiPart[]? Parts { get; set; }
        }

        private sealed class GeminiPart
        {
            [JsonPropertyName("text")] public string? Text { get; set; }
        }

        private sealed class GeminiGenerationConfig
        {
            [JsonPropertyName("temperature")] public double? Temperature { get; set; }
            [JsonPropertyName("maxOutputTokens")] public int? MaxOutputTokens { get; set; }
        }
    }
}
