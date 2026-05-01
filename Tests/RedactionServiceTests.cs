using WinSentryAI.Services;

namespace WinSentryAI.Tests;

public class RedactionServiceTests
{
    [Fact]
    public void RedactMessage_replaces_user_paths_domain_users_and_email_consistently()
    {
        var context = new SubstitutionContext();
        string input = @"C:\Users\Alice\app.log failed for CONTOSO\Alice; contact alice@example.com. C:\Users\Alice\dump.dmp";

        string redacted = RedactionService.RedactMessage(input, context);

        Assert.DoesNotContain(@"C:\Users\Alice", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alice@example.com", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[USER_1]", redacted);
        Assert.Contains("[EMAIL_1]", redacted);
        Assert.Equal("[USER_1]", context.Map["Alice"]);
        Assert.Equal("[EMAIL_1]", context.Map["alice@example.com"]);
    }
}
