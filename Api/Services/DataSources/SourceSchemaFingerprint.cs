using System.Security.Cryptography;
using System.Text;

namespace Api.Services.DataSources;

internal static class SourceSchemaFingerprint
{
    public static string Compute(string provider, string table, IReadOnlyList<string> columns)
    {
        var ordered = columns
            .Where(column => !string.IsNullOrWhiteSpace(column))
            .Select(column => column.Trim())
            .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
            .ThenBy(column => column, StringComparer.Ordinal);

        var builder = new StringBuilder();
        builder.Append(provider.Trim().ToLowerInvariant());
        builder.Append('\n');
        builder.Append(table.Trim());
        foreach (var column in ordered)
        {
            builder.Append('\n');
            builder.Append(column);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
