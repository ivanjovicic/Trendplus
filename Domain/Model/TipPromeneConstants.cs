namespace Domain.Model;

/// <summary>
/// Canonical string constants for <see cref="DnevnikPromena.TipPromene"/> and
/// <see cref="InventoryMovementFact.TipPromene"/>.
/// <para>
/// Always use these constants instead of magic strings so that the database index
/// <c>IX_DnevnikPromena_TipPromene_Datum</c> can be used via exact equality queries
/// rather than slow ILIKE/Contains scans.
/// </para>
/// </summary>
public static class TipPromeneConstants
{
    // ── Price-change events ────────────────────────────────────────────────
    /// <summary>Batch price update imported from Access (nivelacija table).</summary>
    public const string Nivelacija     = "Nivelacija";

    /// <summary>Manual single-article price change entered via the web UI.</summary>
    public const string NivelacijaCena = "Nivelacija cena";

    // ── Inventory-movement events ──────────────────────────────────────────
    /// <summary>Goods received from a supplier.</summary>
    public const string UlazRobe    = "Ulaz robe";

    /// <summary>Customer return.</summary>
    public const string PovratKupca = "Povrat kupca";

    /// <summary>Inter-store transfer — outgoing side (source store).</summary>
    public const string PrenosIzlaz = "Prenos izlaz";

    /// <summary>Inter-store transfer — incoming side (destination store).</summary>
    public const string PrenosUlaz  = "Prenos ulaz";

    /// <summary>Point-of-service sale.</summary>
    public const string Prodaja     = "Prodaja";

    // ── Aggregated lookups (O(1) HashSet checks) ───────────────────────────

    /// <summary>All types that represent a price-adjustment (nivelacija) event.</summary>
    public static readonly IReadOnlySet<string> SvaNivelacijaTypes =
        new HashSet<string>(StringComparer.Ordinal) { Nivelacija, NivelacijaCena };

    /// <summary>All types that represent inward stock movement (positive quantity).</summary>
    public static readonly IReadOnlySet<string> UlazTypes =
        new HashSet<string>(StringComparer.Ordinal) { UlazRobe, PrenosUlaz, PovratKupca };

    /// <summary>All types that represent a sale or outward movement.</summary>
    public static readonly IReadOnlySet<string> ProdajaTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Prodaja, "sale", "prodato", "promet"
        };

    // ── Helper predicates ─────────────────────────────────────────────────

    public static bool IsNivelacija(string? tip) =>
        tip is not null && SvaNivelacijaTypes.Contains(tip);

    public static bool IsSale(string? tip) =>
        tip is not null && ProdajaTypes.Contains(tip);

    // ── SQL literals (use in raw Npgsql queries) ──────────────────────────

    /// <summary>
    /// SQL IN-list covering all nivelacija types for use with exact-match queries.
    /// Example: <c>WHERE "TipPromene" = ANY(@nivelacijaTypes)</c>
    /// </summary>
    public static readonly string[] NivelacijaSqlValues = [Nivelacija, NivelacijaCena];
}
