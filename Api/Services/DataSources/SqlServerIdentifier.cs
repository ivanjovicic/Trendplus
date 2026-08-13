namespace Api.Services.DataSources;

/// <summary>
/// SQL Server identifier quoting and table-name parsing. Identifiers are never concatenated unquoted.
/// </summary>
internal static class SqlServerIdentifier
{
    public const string DefaultSchema = "dbo";

    public static string Quote(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return "[]";

        return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    public static bool TryQuoteTable(string? table, out string quotedIdentifier, out string failureReason)
    {
        quotedIdentifier = string.Empty;
        if (!TryParseTable(table, out var schema, out var name, out failureReason))
            return false;

        quotedIdentifier = $"{Quote(schema)}.{Quote(name)}";
        return true;
    }

    public static bool TryParseTable(string? table, out string schema, out string name, out string failureReason)
    {
        schema = DefaultSchema;
        name = string.Empty;
        failureReason = string.Empty;

        if (string.IsNullOrWhiteSpace(table))
        {
            failureReason = "table name is empty";
            return false;
        }

        var trimmed = table.Trim();
        if (trimmed.IndexOfAny(['\0', '\r', '\n', ';']) >= 0)
        {
            failureReason = "table name contains prohibited characters";
            return false;
        }

        var parts = SplitIdentifierParts(trimmed);
        if (parts is null || parts.Count == 0)
        {
            failureReason = "table name is not a valid identifier";
            return false;
        }

        if (parts.Count == 1)
        {
            name = parts[0];
            if (string.IsNullOrWhiteSpace(name))
            {
                failureReason = "table name is empty";
                return false;
            }

            return true;
        }

        if (parts.Count == 2)
        {
            schema = parts[0];
            name = parts[1];
            if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(name))
            {
                failureReason = "table name is not a valid identifier";
                return false;
            }

            return true;
        }

        failureReason = "three-part and four-part names are not allowed";
        return false;
    }

    internal static IReadOnlyList<string>? SplitIdentifierParts(string value)
    {
        var parts = new List<string>();
        var i = 0;
        while (i < value.Length)
        {
            if (value[i] == '[')
            {
                var close = FindClosingBracket(value, i + 1);
                if (close < 0)
                    return null;

                parts.Add(UnescapeBrackets(value[(i + 1)..close]));
                i = close + 1;
                if (i >= value.Length)
                    break;

                if (value[i] != '.')
                    return null;

                i++;
                continue;
            }

            var dot = value.IndexOf('.', i);
            if (dot < 0)
            {
                parts.Add(value[i..]);
                break;
            }

            parts.Add(value[i..dot]);
            i = dot + 1;
        }

        return parts;
    }

    private static int FindClosingBracket(string value, int start)
    {
        for (var i = start; i < value.Length; i++)
        {
            if (value[i] != ']')
                continue;

            if (i + 1 < value.Length && value[i + 1] == ']')
            {
                i++;
                continue;
            }

            return i;
        }

        return -1;
    }

    private static string UnescapeBrackets(string value)
        => value.Replace("]]", "]", StringComparison.Ordinal);
}
