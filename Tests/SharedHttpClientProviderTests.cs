using WinSentryAI.Services;

namespace WinSentryAI.Tests;

public class SharedHttpClientProviderTests
{
    [Fact]
    public void Default_returns_same_client_instance()
    {
        Assert.Same(SharedHttpClientProvider.Default, SharedHttpClientProvider.Default);
        Assert.Equal(TimeSpan.FromSeconds(60), SharedHttpClientProvider.Default.Timeout);
    }

    [Fact]
    public void ShortTimeout_returns_same_client_instance_with_short_timeout()
    {
        Assert.Same(SharedHttpClientProvider.ShortTimeout, SharedHttpClientProvider.ShortTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), SharedHttpClientProvider.ShortTimeout.Timeout);
    }
}
