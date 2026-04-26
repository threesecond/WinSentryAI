namespace WinSentryAI.Models
{
    public enum ChatRole
    {
        User,
        Assistant
    }

    public record ChatMessage(ChatRole Role, string Content);
}
