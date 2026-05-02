using System.Net;
using System.Net.Http;
using WinSentryAI.Services;

namespace WinSentryAI.Tests;

public class AIHttpErrorClassifierTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "[Invalid Key]", false)]
    [InlineData(HttpStatusCode.Forbidden, "[Access Denied]", false)]
    [InlineData(HttpStatusCode.NotFound, "[Not Found]", false)]
    [InlineData((HttpStatusCode)429, "[Quota / Rate Limit]", true)]
    [InlineData(HttpStatusCode.InternalServerError, "[Server Error]", true)]
    [InlineData(HttpStatusCode.BadGateway, "[Server Unavailable]", true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "[Server Unavailable]", true)]
    [InlineData(HttpStatusCode.GatewayTimeout, "[Server Unavailable]", true)]
    public void ClassifyHttpError_maps_common_status_codes(HttpStatusCode statusCode, string expectedPrefix, bool retryable)
    {
        var error = AIHttpErrorClassifier.ClassifyHttpError(
            "OpenAI",
            statusCode,
            """{"error":{"message":"sample detail","type":"invalid_request_error"}}""");

        Assert.StartsWith(expectedPrefix, error.Message);
        Assert.Equal(retryable, error.IsRetryable);
        Assert.Contains("sample detail", error.Message);
    }

    [Fact]
    public void ClassifyHttpError_reads_gemini_status_and_message()
    {
        var error = AIHttpErrorClassifier.ClassifyHttpError(
            "Gemini",
            HttpStatusCode.BadRequest,
            """{"error":{"status":"INVALID_ARGUMENT","message":"bad prompt"}}""");

        Assert.False(error.IsRetryable);
        Assert.Contains("INVALID_ARGUMENT: bad prompt", error.Message);
    }

    [Fact]
    public void ClassifyException_marks_timeout_and_network_as_retryable()
    {
        var timeout = AIHttpErrorClassifier.ClassifyException("Claude", new TaskCanceledException(), cancellationRequested: false);
        var network = AIHttpErrorClassifier.ClassifyException("Claude", new HttpRequestException("dns failed"), cancellationRequested: false);

        Assert.True(timeout.IsRetryable);
        Assert.StartsWith("[Timeout]", timeout.Message);
        Assert.True(network.IsRetryable);
        Assert.StartsWith("[Network Error]", network.Message);
    }

    [Fact]
    public void ClassifyException_marks_user_cancellation_as_permanent()
    {
        var error = AIHttpErrorClassifier.ClassifyException("Ollama", new TaskCanceledException(), cancellationRequested: true);

        Assert.False(error.IsRetryable);
        Assert.StartsWith("[Cancelled]", error.Message);
    }
}
