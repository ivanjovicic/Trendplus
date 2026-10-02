using System.Globalization;

namespace Api.Services;

public enum EntityKind
{
    Supplier,
    Store,
    ShoeType,
    Season
}

/// <summary>
/// Access "Random AutoNumber" assigns negative IDs to suppliers, shoe types, stores and possibly seasons.
/// Never treat <c>id &lt;= 0</c> as unknown: an entity reference is unknown only when it is NULL
/// (missing attribution) or has no master row (dangling reference).
/// </summary>
public static class EntityIdentity
{
    public static bool IsAssigned(int? id) => id.HasValue;

    /// <summary>Matches the frontend <c>formatEntityFallbackLabel</c> wording.</summary>
    public static string FallbackLabel(EntityKind kind, int? id)
    {
        var baseLabel = kind switch
        {
            EntityKind.Supplier => "Nepoznat dobavljač",
            EntityKind.Store => "Nepoznat objekat",
            EntityKind.ShoeType => "Nepoznat tip obuće",
            EntityKind.Season => "Nepoznata sezona",
            _ => "Nepoznata vrednost"
        };

        return id.HasValue
            ? $"{baseLabel} (ID {id.Value.ToString(CultureInfo.InvariantCulture)})"
            : baseLabel;
    }
}
