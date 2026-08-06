namespace Api.Services.Startup;

/// <summary>
/// Stable public codes for anonymous dependency health checks.
/// Full exception text stays in server logs only.
/// </summary>
public static class DependencyHealthPublicErrors
{
    public const string MissingConnectionString = "missing_connection_string";
    public const string Timeout = "timeout";
    public const string RequestAborted = "request_aborted";
    public const string Unavailable = "unavailable";

    public static string ForMissingConnectionString() => MissingConnectionString;

    public static string ForCanceled(bool requestAborted) =>
        requestAborted ? RequestAborted : Timeout;

    public static string ForUnexpectedFailure() => Unavailable;

    /// <summary>
    /// Returns true when a probe error string is safe to emit on a public health surface.
    /// </summary>
    public static bool IsPublicSafeCode(string? code) =>
        string.Equals(code, MissingConnectionString, StringComparison.Ordinal) ||
        string.Equals(code, Timeout, StringComparison.Ordinal) ||
        string.Equals(code, RequestAborted, StringComparison.Ordinal) ||
        string.Equals(code, Unavailable, StringComparison.Ordinal);
}
