using System.Security.Cryptography;
using System.Text;

namespace Api.Services.DataSources;

public static class SourceMappingProfileId
{
    public static string Compute(
        string connectionId,
        string entity,
        string table,
        string? externalKeyColumn,
        string? cursorMode,
        IEnumerable<(string Target, string Source)> fields)
    {
        var builder = new StringBuilder();
        builder.Append(Normalize(connectionId));
        builder.Append('\n');
        builder.Append(Normalize(entity));
        builder.Append('\n');
        builder.Append(table.Trim());
        builder.Append('\n');
        builder.Append(Normalize(externalKeyColumn));
        builder.Append('\n');
        builder.Append(Normalize(cursorMode));
        foreach (var field in fields
            .Select(item => (Target: Normalize(item.Target), Source: item.Source.Trim()))
            .Where(item => item.Target.Length > 0 && item.Source.Length > 0)
            .OrderBy(item => item.Target, StringComparer.Ordinal)
            .ThenBy(item => item.Source, StringComparer.Ordinal))
        {
            builder.Append('\n');
            builder.Append(field.Target);
            builder.Append('=');
            builder.Append(field.Source);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}
