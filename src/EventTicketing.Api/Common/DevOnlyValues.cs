namespace EventTicketing.Api.Common;

/// <summary><c>dev-only-</c> prefixed values are local fakes and are rejected in Production (ISKELET §5.14).</summary>
public static class DevOnlyValues
{
    public const string RejectionMessage = "dev-only- values are for local use and are not allowed in Production.";
    private const string Prefix = "dev-only-";

    public static bool Contains(string? value) => value?.Contains(Prefix, StringComparison.OrdinalIgnoreCase) == true;
}
