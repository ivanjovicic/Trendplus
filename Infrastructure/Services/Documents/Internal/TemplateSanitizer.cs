using System.Text.RegularExpressions;

namespace Infrastructure.Services.Documents.Internal;

internal static partial class TemplateSanitizer
{
    private static readonly string[] ForbiddenFragments =
    {
        "<script",
        "javascript:",
        "onerror=",
        "onclick=",
        "onload=",
        "<iframe"
    };

    [GeneratedRegex(@"\s+on[a-z]+\s*=\s*(['""]).*?\1", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineEventRegex();

    public static string Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sanitized = InlineEventRegex().Replace(input, string.Empty);
        foreach (var fragment in ForbiddenFragments)
        {
            if (sanitized.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Template contains forbidden content: {fragment}");
            }
        }

        return sanitized;
    }
}
