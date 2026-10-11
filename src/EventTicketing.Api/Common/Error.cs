namespace EventTicketing.Api.Common;

/// <summary>Expected failure. <see cref="Code"/> is a value from ISKELET §5.12; extra fields go to the ProblemDetails body.</summary>
public sealed record Error(string Code, string Message, IReadOnlyDictionary<string, object?>? Extensions = null);
