namespace EventTicketing.Api.Common;

/// <summary>
/// Builds <c>ILIKE</c> patterns from user text. Case folding happens in PostgreSQL on both sides, so the result does not
/// depend on the culture of the API process (e.g. Turkish <c>"I".ToLower()</c> is <c>"ı"</c>).
/// </summary>
public static class LikePattern
{
    /// <summary>Pass as the escape character of <c>EF.Functions.ILike</c>; without it Npgsql emits <c>ESCAPE ''</c>.</summary>
    public const string EscapeCharacter = @"\";

    /// <summary>Matches <paramref name="text"/> exactly, ignoring case.</summary>
    public static string Exact(string text) => Escape(text.Trim());

    /// <summary>Matches values that contain <paramref name="text"/>, ignoring case.</summary>
    public static string Contains(string text) => $"%{Escape(text.Trim())}%";

    private static string Escape(string text) =>
        text.Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);
}
