using System.Net.Http;

namespace WinSentryAI.Services
{
    public static class SharedHttpClientProvider
    {
        private static readonly Lazy<HttpClient> DefaultClient = new(() => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        });

        private static readonly Lazy<HttpClient> ShortTimeoutClient = new(() => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        });

        public static HttpClient Default => DefaultClient.Value;

        public static HttpClient ShortTimeout => ShortTimeoutClient.Value;
    }
}
