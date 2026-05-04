using Serilog;

namespace WinSentryAI.Services
{
    public static class AIHttpRetryPolicy
    {
        public static async Task<AIHttpAttempt<T>> ExecuteAsync<T>(
            string providerName,
            int maxRetries,
            Func<CancellationToken, Task<AIHttpAttempt<T>>> operation,
            CancellationToken ct,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
        {
            maxRetries = Math.Clamp(maxRetries, 1, 10);
            delayAsync ??= Task.Delay;
            AIHttpAttempt<T>? lastAttempt = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    Log.Information("{Provider} retry {Attempt}/{Max} after {Delay}s.",
                        providerName, attempt, maxRetries - 1, delay.TotalSeconds);
                    await delayAsync(delay, ct);
                }

                lastAttempt = await operation(ct);
                if (lastAttempt.IsSuccess)
                    return lastAttempt;

                if (!lastAttempt.IsRetryable || ct.IsCancellationRequested)
                    return lastAttempt;

                if (attempt < maxRetries - 1)
                {
                    Log.Warning("{Provider} attempt {Attempt} failed (retryable): {Error}",
                        providerName, attempt + 1, lastAttempt.ErrorMessage);
                }
            }

            return lastAttempt ?? AIHttpAttempt<T>.Failure("Request failed before it could be sent.", false);
        }
    }

    public sealed record AIHttpAttempt<T>(T? Value, string? ErrorMessage, bool IsRetryable)
    {
        public bool IsSuccess => ErrorMessage == null;

        public static AIHttpAttempt<T> Success(T value) => new(value, null, false);

        public static AIHttpAttempt<T> Failure(string errorMessage, bool isRetryable) =>
            new(default, errorMessage, isRetryable);
    }
}
