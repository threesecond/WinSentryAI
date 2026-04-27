namespace WinSentryAI.Services
{
    public interface ITrayService : IDisposable
    {
        void Initialize();
        void SetAlert(bool isAlert);
        void ShowBalloon(string title, string message);
    }
}
