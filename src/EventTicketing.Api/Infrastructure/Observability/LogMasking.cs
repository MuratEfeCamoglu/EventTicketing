namespace EventTicketing.Api.Infrastructure.Observability;

/// <summary>Full e-mail addresses never reach the logs (AC-18.4).</summary>
public static class LogMasking
{
    /// <summary><c>ayse@example.com</c> → <c>a***@example.com</c>.</summary>
    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
