namespace Infrastructure.Services.Storage;

internal static class StorageKeyNormalizer
{
    public static string Normalize(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Storage key must not be empty.", nameof(key));
        }

        var normalized = key.Replace('\\', '/').Trim();
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new ArgumentException("Storage key must contain at least one valid path segment.", nameof(key));
        }

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i].Trim();
            if (segment is "." or "..")
            {
                throw new ArgumentException("Storage key must not contain relative traversal segments.", nameof(key));
            }

            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException($"Storage key segment '{segment}' contains invalid path characters.", nameof(key));
            }

            segments[i] = segment;
        }

        return string.Join('/', segments);
    }
}
