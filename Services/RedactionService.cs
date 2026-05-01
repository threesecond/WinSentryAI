using System.Text.RegularExpressions;

namespace WinSentryAI.Services
{
    public sealed class SubstitutionContext
    {
        private readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _counters = new(StringComparer.OrdinalIgnoreCase);

        public string GetOrAdd(string original, string prefix)
        {
            if (string.IsNullOrWhiteSpace(original)) return original;
            if (_map.TryGetValue(original, out var existing)) return existing;

            _counters.TryGetValue(prefix, out int count);
            _counters[prefix] = ++count;
            string placeholder = $"[{prefix}_{count}]";
            _map[original] = placeholder;
            return placeholder;
        }

        public IReadOnlyDictionary<string, string> Map => _map;
    }

    internal static partial class RedactionService
    {
        public static string RedactMessage(string message, SubstitutionContext context)
        {
            if (string.IsNullOrEmpty(message)) return message;

            string redacted = UserPathRegex().Replace(message, m =>
                m.Groups["prefix"].Value + context.GetOrAdd(m.Groups["user"].Value, "USER"));
            redacted = DomainUserRegex().Replace(redacted, m =>
                m.Groups["prefix"].Value + context.GetOrAdd(m.Groups["user"].Value, "USER"));
            redacted = EmailRegex().Replace(redacted, m => context.GetOrAdd(m.Value, "EMAIL"));
            redacted = ReplaceKnownUser(redacted, context);
            return redacted;
        }

        private static string ReplaceKnownUser(string message, SubstitutionContext context)
        {
            string userName = Environment.UserName;
            if (string.IsNullOrWhiteSpace(userName)) return message;

            string placeholder = context.GetOrAdd(userName, "USER");
            return message.Replace(userName, placeholder, StringComparison.OrdinalIgnoreCase);
        }

        [GeneratedRegex(@"(?<prefix>\b[^\[\\\s,:""']+\\)(?<user>[^\[\\,\s""'\]]+)", RegexOptions.Compiled)]
        private static partial Regex DomainUserRegex();

        [GeneratedRegex(@"(?<prefix>[Cc]:\\[Uu]sers\\)(?<user>[^\\,\s""'\]]+)", RegexOptions.Compiled)]
        private static partial Regex UserPathRegex();

        [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
        private static partial Regex EmailRegex();
    }
}
