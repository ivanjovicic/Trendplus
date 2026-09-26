using System.Text;

namespace Application.Analytics;

/// <summary>
/// Canonical identity policy for color analytics.
/// Display text and comparison identity are intentionally separate: the former
/// preserves the user's label, while the latter prevents casing/Unicode aliases
/// from creating duplicate rows, detail links or previous-period joins.
/// </summary>
public static class ColorIdentityPolicy
{
    public const string UnknownDisplayName = "Nepoznato";

    public static string DisplayName(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);
        return string.IsNullOrWhiteSpace(normalized)
            || string.Equals(normalized, UnknownDisplayName, StringComparison.OrdinalIgnoreCase)
            ? UnknownDisplayName
            : normalized;
    }

    public static string Key(string? value) => DisplayName(value).ToUpperInvariant();

    public static bool IsUnknown(string? value) =>
        string.Equals(Key(value), Key(UnknownDisplayName), StringComparison.Ordinal);
}
