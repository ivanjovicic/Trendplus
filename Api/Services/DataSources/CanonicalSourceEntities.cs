namespace Api.Services.DataSources;

/// <summary>
/// Canonical Trendplus entities for mapping preview. Aliases are deterministic catalogs, never auto-applied.
/// </summary>
internal static class CanonicalSourceEntities
{
    internal sealed record Entity(
        string Key,
        IReadOnlyList<string> RequiredTargets,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Aliases);

    private static readonly IReadOnlyDictionary<string, Entity> Entities =
        new Dictionary<string, Entity>(StringComparer.OrdinalIgnoreCase)
        {
            ["artikli"] = new(
                "artikli",
                ["Id", "Naziv"],
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Id"] = ["id", "idartikal", "productid"],
                    ["Naziv"] = ["naziv", "name", "productname"]
                }),
            ["prodaja_zaglavlje"] = new(
                "prodaja_zaglavlje",
                ["Id", "DatumProdaje"],
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Id"] = ["id", "idprodaja", "saleid"],
                    ["DatumProdaje"] = ["datumprodaje", "datum", "saledate"]
                }),
            ["prodaja_stavke"] = new(
                "prodaja_stavke",
                ["IdProdaja", "IdArtikal", "Kolicina", "Cena"],
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["IdProdaja"] = ["idprodaja", "saleid"],
                    ["IdArtikal"] = ["idartikal", "productid"],
                    ["Kolicina"] = ["kolicina", "qty", "quantity"],
                    ["Cena"] = ["cena", "price", "saleprice"]
                })
        };

    public static bool TryGet(string? entity, out Entity definition)
    {
        definition = null!;
        if (string.IsNullOrWhiteSpace(entity))
            return false;

        return Entities.TryGetValue(entity.Trim(), out definition!);
    }

    public static IReadOnlyList<string> Keys => Entities.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray();
}
