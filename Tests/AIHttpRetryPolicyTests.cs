using WinSentryAI.Services;

namespace WinSentryAI.Tests;

public class AIHttpRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_retries_retryable_failure_then_returns_success()
    {
        var calls = 0;
        var delays = new List<TimeSpan>();

        var result = await AIHttpRetryPolicy.ExecuteAsync(
            "OpenAI",
            maxRetries: 3,
            _ =>
            {
                calls++;
                return Task.FromResult(calls == 1
                    ? AIHttpAttempt<string>.Failure("[Server Error] temporary failure", isRetryable: true)
                    : AIHttpAttempt<string>.Success("ok"));
            },
            CancellationToken.None,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
        Assert.Equal(2, calls);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromSeconds(1), delays[0]);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_retry_permanent_failure()
    {
        var calls = 0;

        var result = await AIHttpRetryPolicy.ExecuteAsync(
            "Claude",
            maxRetries: 3,
            _ =>
            {
                calls++;
                return Task.FromResult(AIHttpAttempt<string>.Failure("[Invalid Key] bad key", isRetryable: false));
            },
            CancellationToken.None,
            (_, _) => throw new InvalidOperationException("Permanent failures should not delay."));

        Assert.False(result.IsSuccess);
        Assert.Equal("[Invalid Key] bad key", result.ErrorMessage);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_returns_last_retryable_failure_after_max_retries()
    {
        var calls = 0;

        var result = await AIHttpRetryPolicy.ExecuteAsync(
            "Ollama",
            maxRetries: 2,
            _ =>
            {
                calls++;
                return Task.FromResult(AIHttpAttempt<string>.Failure($"[Timeout] attempt {calls}", isRetryable: true));
            },
            CancellationToken.None,
            (_, _) => Task.CompletedTask);

        Assert.False(result.IsSuccess);
        Assert.Equal("[Timeout] attempt 2", result.ErrorMessage);
        Assert.Equal(2, calls);
    }
}
