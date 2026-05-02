using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace WinSentryAI.Services
{
    public static class AIHttpErrorClassifier
    {
        public static AIHttpError ClassifyHttpError(string providerName, HttpStatusCode code, string body)
        {
            string? apiMessage = ExtractApiError(body);
            string detail = apiMessage ?? $"HTTP {(int)code}";
            bool retryable = IsRetryableStatusCode(code);

            string message = code switch
            {
                HttpStatusCode.Unauthorized =>
                    $"[Invalid Key] Authentication failed (401). Your {providerName} API key is invalid or expired. Please update it in Settings -> AI. Detail: {detail}",

                HttpStatusCode.Forbidden =>
                    $"[Access Denied] Permission denied (403). Your {providerName} key may not have access to the selected model. Detail: {detail}",

                (HttpStatusCode)429 =>
                    $"[Quota / Rate Limit] Too many requests (429). You may have exceeded your {providerName} quota or hit a rate limit. Wait a moment before retrying. Detail: {detail}",

                HttpStatusCode.BadRequest =>
                    $"[Bad Request] The request was rejected by {providerName} (400). This may be a model, endpoint, or prompt formatting issue. Detail: {detail}",

                HttpStatusCode.NotFound =>
                    $"[Not Found] Model or endpoint not found (404). Check that the model name in Settings is correct. Detail: {detail}",

                HttpStatusCode.InternalServerError =>
                    $"[Server Error] {providerName} returned an internal server error (500). Will retry if retries remain. Detail: {detail}",

                HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout =>
                    $"[Server Unavailable] {providerName} is temporarily unavailable ({(int)code}). Will retry if retries remain. Detail: {detail}",

                _ => $"[HTTP {(int)code}] {providerName} request failed. Detail: {detail}"
            };

            return new AIHttpError(message, retryable);
        }

        public static AIHttpError ClassifyException(string providerName, Exception exception, bool cancellationRequested)
        {
            if (exception is TaskCanceledException && cancellationRequested)
                return new AIHttpError("[Cancelled] Analysis was cancelled.", false);

            if (exception is TaskCanceledException)
                return new AIHttpError($"[Timeout] {providerName} request timed out. Check your internet connection.", true);

            if (exception is HttpRequestException)
                return new AIHttpError($"[Network Error] {providerName} network request failed: {exception.Message}", true);

            return new AIHttpError($"[Unexpected Error] {exception.GetType().Name}: {exception.Message}", false);
        }

        public static bool IsRetryableStatusCode(HttpStatusCode code) => code switch
        {
            (HttpStatusCode)429 => true,
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            _ => false
        };

        private static string? ExtractApiError(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("error", out var err))
                    return null;

                if (err.ValueKind == JsonValueKind.String)
                    return err.GetString();

                if (err.ValueKind != JsonValueKind.Object)
                    return null;

                string status = ReadString(err, "status")
                    ?? ReadString(err, "type")
                    ?? ReadString(err, "code")
                    ?? string.Empty;
                string message = ReadString(err, "message") ?? string.Empty;

                if (string.IsNullOrWhiteSpace(status))
                    return string.IsNullOrWhiteSpace(message) ? null : message;

                return string.IsNullOrWhiteSpace(message) ? status : $"{status}: {message}";
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
                return null;

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : property.ToString();
        }
    }

    public sealed record AIHttpError(string Message, bool IsRetryable);
}
