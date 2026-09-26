namespace Application.Analytics;

/// <summary>
/// Canonical Shoe Type identity (RQ457 / SST-ACCURACY-1.0).
/// Unknown means <c>tipObuceId == null</c> only; display names never define identity.
/// </summary>
public static class ShoeTypeIdentityPolicy
{
    public const string UnknownDisplayName = "Nepoznato";
    public const string UnknownDetailId = "unknown-nepoznato";

    public static bool IsUnknownId(int? tipObuceId) => !tipObuceId.HasValue;

    public static string DisplayName(int? tipObuceId, string? masterName)
    {
        if (IsUnknownId(tipObuceId))
        {
            return UnknownDisplayName;
        }

        return string.IsNullOrWhiteSpace(masterName)
            ? string.Empty
            : masterName.Trim();
    }

    public static string BucketKey(int? tipObuceId)
        => tipObuceId.HasValue ? $"id:{tipObuceId.Value}" : "unknown";

    public static int? TryParseBucketKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || string.Equals(key, "unknown", StringComparison.Ordinal))
        {
            return null;
        }

        if (key.StartsWith("id:", StringComparison.Ordinal)
            && int.TryParse(key.AsSpan(3), out var id))
        {
            return id;
        }

        return null;
    }

    public static bool IsUnknownDetailId(string? id)
        => string.Equals(id, UnknownDetailId, StringComparison.OrdinalIgnoreCase)
           || string.Equals(id, "unknown", StringComparison.OrdinalIgnoreCase);
}
