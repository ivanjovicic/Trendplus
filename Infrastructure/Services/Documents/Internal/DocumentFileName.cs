using System.Text;

namespace Infrastructure.Services.Documents.Internal;

internal static class DocumentFileName
{
    public static string Build(string tableTitle, string format, DateTime nowUtc)
    {
        var safeTitle = Slugify(tableTitle);
        var extension = format.ToLowerInvariant() switch
        {
            "xlsx" => "xlsx",
            "pdf" => "pdf",
            "html" => "html",
            _ => "csv"
        };

        return $"{safeTitle}-{nowUtc:yyyyMMdd-HHmmss}.{extension}";
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "document";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else if (builder.Length == 0 || builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-').Length == 0 ? "document" : builder.ToString().Trim('-');
    }
}
