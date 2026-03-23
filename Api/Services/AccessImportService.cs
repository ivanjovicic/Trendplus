using System.Data;
using System.Data.Odbc;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Api.Models;
using Domain.Model;
using Domain.Model.Povracaj;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Services;

public interface IAccessImportService
{
    Task<AccessImportPreviewResponse> PreviewAsync(string accessFilePath, bool includeTemporaryTables = false, CancellationToken ct = default);
    Task<AccessImportRunResponse> ImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default);
    Task<List<AccessImportBatchDto>> GetRecentBatchesAsync(int take = 20, CancellationToken ct = default);
    Task<DeleteBatchResult> DeleteBatchAsync(long batchId, bool includeAnalytics = true, CancellationToken ct = default);
}

public sealed class AccessImportService : IAccessImportService
{
    private sealed class FieldAlias
    {
        public string TargetField { get; }
        public string[] Aliases { get; }

        public FieldAlias(string targetField, params string[] aliases)
        {
            TargetField = targetField;
            Aliases = aliases;
        }
    }

    // ── Table-name candidates (exact then contains, then column-signature fallback) ────────────────
    private static readonly string[] TipoviCandidates        = ["tipoviobuce", "tipobuce", "tipovi_obuce", "footweartypes", "tbltipobuce", "tbltipovi"];
    private static readonly string[] DobavljaciCandidates    = ["dobavljaci", "dobavljac", "suppliers", "tbldobavljaci", "tbldobavljac"];
    private static readonly string[] SezoneCandidates        = ["sezone", "sezona", "seasons", "tblsezone", "tblsezona", "godisnjedoba"];
    private static readonly string[] ArtikliCandidates       = ["artikli", "artikal", "proizvodi", "products", "tblartikal", "tblarticles", "sifarnik"];
    private static readonly string[] ProdajaCandidates       = ["prodaja_zaglavlje", "prodajazaglavlje", "prodaja", "racuni", "salesheader", "tblracuni", "tblprodaja", "tbldnevnikprodaje"];
    private static readonly string[] ProdajaStavkeCandidates = ["prodaja_stavke", "prodajastavke", "stavkeprodaje", "salelines", "tblstavkeracuna", "tblstavkeprodaje"];
    private static readonly string[] DnevnikPromenaCandidates = ["dnevnikpromjena", "dnevnikpromena", "dnevnik_promjena", "dnevnik_promena", "dnevnik", "log", "promena", "promjena", "events", "journal", "tbldnevnikpromena", "tbldnevnikpromjena", "tbldnevnik"];
    private static readonly string[] PovracajCandidates      = ["povracaj_zaglavlje", "povracajzaglavlje", "povracaj", "returns", "returnheader", "vracanje", "tblpovracaj", "tblzapisnikopovracaju", "tblzapisnik"];
    private static readonly string[] PovracajStavkeCandidates2 = ["povracaj_stavke", "povracajstavke", "stavkepovracaja", "returnlines", "returnstems", "tblstavkepovracaja", "tblstavkezapisnika"];
    // ── New movement-type candidates ─────────────────────────────────────────────────────────────
    private static readonly string[] NivelacijeCandidates    = ["nivelacije", "nivelacija", "priceupdate", "cenovneizmene", "tblnivelacije", "tblnivelacija", "nivelacijeartikala"];
    private static readonly string[] UnosRobeCandidates      = ["unosrobe", "unos_robe", "goodsreceipt", "prijem", "tblunosrobe", "tblprijemsrobe", "tblprijem", "kretanjezalihe"];
    private static readonly string[] PovratniceCandidates    = ["povratnice", "povratnica", "customerreturns", "vracajakupaca", "tblpovratnice", "tblpovratnica", "tblvracanjakupaca"];
    private static readonly string[] PrenosRobeCandidates    = ["prenosrobe", "prenos_robe", "transfer", "prebacivanje", "tblprenosrobe", "tblprenos", "medjuobjekatskirenos"];
    private static readonly string[] ObjekatCandidates       = ["objekti", "objekat", "stores", "poslovnice", "maloprodaja", "tblobjekat", "tblobjekti", "tblprodavnice", "organizacione"];
    private static readonly Dictionary<string, IReadOnlyList<FieldAlias>> PreviewFieldMappings =
        new Dictionary<string, IReadOnlyList<FieldAlias>>(StringComparer.OrdinalIgnoreCase)
        {
            ["artikli"] =
            [
                new FieldAlias("Id", "id", "idartikal", "productid"),
                new FieldAlias("Naziv", "naziv", "nazivartikal", "nazivarticle", "nazivproizvoda", "opis", "opisartikal", "opisproizvoda", "description", "proizvod", "name", "productname", "articlename", "itemname", "ime", "artikal", "article", "item", "roba"),
                new FieldAlias("PLU", "plu", "sku", "sifra", "sifraartikla", "barcode", "barkod", "kod", "code"),
                new FieldAlias("IDTipObuce", "idtipobuce", "tipobuceid", "footweartypeid"),
                new FieldAlias("IDDobavljac", "iddobavljac", "dobavljacid", "supplierid"),
                new FieldAlias("NabavnaCena", "nabavnacena", "purchaseprice", "cost"),
                new FieldAlias("ProdajnaCena", "prodajnacena", "saleprice", "price"),
                new FieldAlias("Kolicina", "kolicina", "kol", "qty", "quantity", "stock", "stanje", "lager", "zaliha", "stockqty"),
                new FieldAlias("IDSezona", "idsezona", "seasonid")
            ],
            ["dobavljaci"] =
            [
                new FieldAlias("Id", "id", "iddobavljac", "supplierid"),
                new FieldAlias("Naziv", "naziv", "dobavljac", "supplier", "name"),
                new FieldAlias("Adresa", "adresa", "address"),
                new FieldAlias("Telefon", "telefon", "phone", "brteldob", "brteldobav", "tel", "br_tel", "mobilni", "mobile")
            ],
            ["sezone"] =
            [
                new FieldAlias("Id", "id", "idsezona", "seasonid"),
                new FieldAlias("Naziv", "naziv", "sezona", "name"),
                new FieldAlias("DatumOd", "datumod", "od", "startdate"),
                new FieldAlias("DatumDo", "datumdo", "do", "enddate")
            ],
            ["prodaja_zaglavlje"] =
            [
                new FieldAlias("Id", "id", "idprodaja", "saleid", "iddnevnik"),
                new FieldAlias("BrojRacuna", "brojracuna", "brojkalkulacije", "invoice", "receiptnumber"),
                new FieldAlias("DatumProdaje", "datumprodaje", "datum", "saledate"),
                new FieldAlias("NacinPlacanja", "nacinplacanja", "paymenttype"),
                new FieldAlias("IDObjekat", "idobjekat", "storeid")
            ],
            ["prodaja_stavke"] =
            [
                new FieldAlias("IdProdaja", "idprodaja", "saleid", "idzaglavlje", "iddnevnik"),
                new FieldAlias("IdArtikal", "idartikal", "productid", "artiklid"),
                new FieldAlias("Kolicina", "kolicina", "qty", "quantity"),
                new FieldAlias("Cena", "prodajnacena", "cena", "unitprice", "price")
            ],
            ["dnevnik_promena"] =
            [
                new FieldAlias("Id", "id", "iddnevnik", "iddnevnikpromene", "iddnevnikpromena", "idlog", "logid", "seqno"),
                new FieldAlias("TipPromene", "tippromene", "vrstapromene", "tip", "type", "eventtype", "tipprocene", "promena",
                         "vrstaknjizenjem", "vrstaknjiz", "document", "doctype", "Unos"),
                new FieldAlias("Datum", "datum", "datumizmene", "eventdate"),
                new FieldAlias("Iznos", "iznospromene", "iznos", "amount", "total"),
                new FieldAlias("BrojRacuna/BrojKalkulacije", "brojkalkulacije", "brojracuna", "invoice", "documentno"),
                new FieldAlias("DobavljacId", "iddobavljac", "iddobavljaca", "dobavljacid", "supplierid"),
                new FieldAlias("ArtikalId", "idartikal", "artikalid", "productid"),
                new FieldAlias("Kolicina", "kolicina", "qty", "quantity"),
                new FieldAlias("StaraProdajnaCena", "staracena", "staraprodajnacena", "oldprice"),
                new FieldAlias("NovaProdajnaCena", "novacena", "novaprodajnacena", "newprice"),
                new FieldAlias("Komentar", "komentar", "comment", "napomena", "opis"),
                new FieldAlias("KorisnikIme", "korisnik", "korisnikime", "username", "operater")
            ],
            ["povracaj_zaglavlje"] =
            [
                new FieldAlias("Id", "id", "idpovracaj", "returnid"),
                new FieldAlias("IDDobavljac", "iddobavljac", "dobavljacid", "supplierid"),
                new FieldAlias("DatumPovracaja", "datumazapisnika", "datumpovracaja", "datum", "date"),
                new FieldAlias("BrojZapisnika", "brojzapisnika", "brozapisnika", "broj", "recordnumber", "returnno"),
                new FieldAlias("UkupanIznos", "ukupaniznos", "total", "iznos")
            ],
            ["povracaj_stavke"] =
            [
                new FieldAlias("IdPovracaj", "idpovracaj", "returnid", "idzaglavlje"),
                new FieldAlias("IdArtikal", "idartikal", "productid", "artiklid"),
                new FieldAlias("Kolicina", "kolicina", "qty", "quantity"),
                new FieldAlias("Cena", "cena", "unitprice", "price", "nabavnacena")
            ],
            ["nivelacije"] =
            [
                new FieldAlias("IDDnevnik",    "iddnevnik", "idlog", "seqno"),
                new FieldAlias("IDArtikal",    "idartikal", "productid"),
                new FieldAlias("Kolicina",     "kolicina", "qty"),
                new FieldAlias("StaraCena",    "staracena", "staraprodajnacena", "oldprice"),
                new FieldAlias("NovaCena",     "novacena", "novaprodajnacena", "newprice")
            ],
            ["unos_robe"] =
            [
                new FieldAlias("IDDnevnik",    "iddnevnik", "idlog"),
                new FieldAlias("IDArtikal",    "idartikal", "productid"),
                new FieldAlias("IDDobavljac",  "iddobavljac", "dobavljacid", "supplierid"),
                new FieldAlias("Kolicina",     "kolicina", "qty"),
                new FieldAlias("NabavnaCena",  "nabavnacena", "purchaseprice", "cena")
            ],
            ["povratnice"] =
            [
                new FieldAlias("IDDnevnik",    "iddnevnik", "idlog"),
                new FieldAlias("IDArtikal",    "idartikal", "productid"),
                new FieldAlias("Kolicina",     "kolicina", "qty"),
                new FieldAlias("Razlog",       "razlog", "reason"),
                new FieldAlias("Cena",         "cena", "prodajnacena", "unitprice")
            ],
            ["prenos_robe"] =
            [
                new FieldAlias("IDDnevnik",    "iddnevnik", "idlog"),
                new FieldAlias("IDArtikal",    "idartikal", "productid"),
                new FieldAlias("Kolicina",     "kolicina", "qty"),
                new FieldAlias("IDObjekatIz",  "idobjekatiz", "idobjekatizlaza", "fromstore"),
                new FieldAlias("IDObjekatU",   "idobjekatulaz", "idobjekatdolaz", "tostore")
            ],
            ["objekti"] =
            [
                new FieldAlias("ID",           "id", "idobjekat", "storeid"),
                new FieldAlias("NazivObjekta", "nazivobjekta", "naziv", "storename", "name", "poslovnica"),
                new FieldAlias("Adresa",       "adresa", "address"),
                new FieldAlias("Telefon",      "telefon", "phone", "tel"),
                new FieldAlias("Menedzer",     "menedzer", "manager", "rukovodilac")
            ]
        };

    // Minimal required fields to consider a table "import-safe" (preview diagnostics only).
    // Import itself still validates and can fallback (e.g. synthesize sales from DnevnikPromena).
    private static readonly Dictionary<string, string[]> PreviewRequiredFields =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["artikli"] = ["Id", "Naziv"],
            ["prodaja_zaglavlje"] = ["Id", "DatumProdaje"],
            ["prodaja_stavke"] = ["IdProdaja", "IdArtikal", "Kolicina", "Cena"],
            ["dnevnik_promena"] = ["Id", "TipPromene", "Datum"],
        };

    private readonly TrendplusDbContext _trendDb;
    private readonly AnalyticsDbContext _analyticsDb;
    private readonly IAnalyticsCacheService? _analyticsCache;
    private readonly ILogger<AccessImportService> _logger;
    // Populated by ImportTrendplus, consumed by SyncAnalyticsAsync for StoresDim upsert
    private Dictionary<int, (string Name, string? Address, string? Phone, string? Manager)> _importedStores = [];

    public AccessImportService(
        TrendplusDbContext trendDb,
        AnalyticsDbContext analyticsDb,
        ILogger<AccessImportService> logger,
        IAnalyticsCacheService? analyticsCache = null)
    {
        _trendDb = trendDb;
        _analyticsDb = analyticsDb;
        _logger = logger;
        _analyticsCache = analyticsCache;
    }

    public async Task<AccessImportPreviewResponse> PreviewAsync(string accessFilePath, bool includeTemporaryTables = false, CancellationToken ct = default)
    {
        EnsurePlatformSupport();
        if (!File.Exists(accessFilePath))
            throw new FileNotFoundException("ACCDB fajl nije pronađen.", accessFilePath);

        return await Task.Run(() =>
        {
            var snapshot = CreateSnapshotIfLocked(accessFilePath);
            try
            {
                using var conn = CreateOdbcConnection(snapshot.FilePath);
                conn.Open();
                var tables = GetUserTables(conn, includeTemporaryTables);
                var tableRowCounts = tables.ToDictionary(t => t, t => RowCount(conn, t), StringComparer.OrdinalIgnoreCase);

                var map = new Dictionary<string, TableMatch>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tipovi_obuce"]    = FindTableDetailed(conn, tables, TipoviCandidates),
                    ["dobavljaci"]      = FindTableDetailed(conn, tables, DobavljaciCandidates),
                    ["sezone"]         = FindTableDetailed(conn, tables, SezoneCandidates,
                                             sigRequired: ["idsezona", "naziv"]),
                    ["artikli"]        = FindTableDetailed(conn, tables, ArtikliCandidates,
                                             sigRequired: ["idartikal", "naziv"],
                                             sigBonus:    ["nabavnacena", "prodajnacena", "plu"]),
                    ["prodaja_zaglavlje"] = FindTableDetailed(conn, tables, ProdajaCandidates),
                    ["prodaja_stavke"]  = FindTableDetailed(conn, tables, ProdajaStavkeCandidates),
                    ["dnevnik_promena"] = FindTableDetailed(conn, tables, DnevnikPromenaCandidates,
                                             sigRequired: ["iddnevnik", "datum"]),
                    ["povracaj_zaglavlje"] = FindTableDetailed(conn, tables, PovracajCandidates),
                    ["povracaj_stavke"] = FindTableDetailed(conn, tables, PovracajStavkeCandidates2),
                    ["nivelacije"]      = FindTableDetailed(conn, tables, NivelacijeCandidates,
                                             sigRequired: ["idartikal", "novacena"]),
                    ["unos_robe"]       = FindTableDetailed(conn, tables, UnosRobeCandidates,
                                             sigRequired: ["idartikal", "kolicina", "iddobavljac"]),
                    ["povratnice"]      = FindTableDetailed(conn, tables, PovratniceCandidates,
                                             sigRequired: ["idartikal", "kolicina"],
                                             sigBonus:    ["razlog", "idpovratnice"]),
                    ["prenos_robe"]     = FindTableDetailed(conn, tables, PrenosRobeCandidates,
                                             sigRequired: ["idartikal", "kolicina"],
                                             sigBonus:    ["idobjekatiz", "idobjekatulaz", "idobjekat"]),
                    ["objekti"]         = FindTableDetailed(conn, tables, ObjekatCandidates,
                                             sigRequired: ["idobjekat", "nazivobjekta"]),
                };

                var response = new AccessImportPreviewResponse
                {
                    SourceFileName = Path.GetFileName(accessFilePath),
                    CanImport = map["artikli"].TableName is not null,
                    AvailableTables = tables.OrderBy(x => x).ToList(),
                    TotalAccessTables = tables.Count,
                    AccessTablesWithRows = tableRowCounts.Values.Count(x => x > 0),
                    TotalAccessRows = tableRowCounts.Values.Sum(),
                    Tables = new List<AccessImportTablePreview>()
                };
                if (!string.IsNullOrWhiteSpace(snapshot.Warning))
                    response.Warnings.Add(snapshot.Warning);

                foreach (var entry in map)
                {
                    var tablePreview = BuildTablePreview(conn, entry.Key, entry.Value, tableRowCounts);
                    response.Tables.Add(tablePreview);

                    if (entry.Key.Equals("prodaja_zaglavlje", StringComparison.OrdinalIgnoreCase)
                        && tablePreview.Found
                        && IsProdajaLineTableByColumns(tablePreview.AccessColumns))
                    {
                        response.Warnings.Add($"Tabela '{tablePreview.TableName}' izgleda kao stavke prodaje (IDDnevnik/IDArtikal/Kolicina/ProdajnaCena), ne kao zaglavlje.");
                    }
                }

                if (!response.CanImport)
                    response.Warnings.Add("Nije pronađena tabela za artikle (obavezna).");

                // Preview diagnostics for required fields (improves "canImport" accuracy and helps mapping/debugging).
                foreach (var req in PreviewRequiredFields)
                {
                    var tablePreview = response.Tables.FirstOrDefault(t =>
                        t.Key.Equals(req.Key, StringComparison.OrdinalIgnoreCase));

                    if (tablePreview is null || !tablePreview.Found)
                        continue;

                    var missing = FindMissingPreviewFields(tablePreview, req.Value);
                    tablePreview.RequiredFieldsMissing = missing;
                    if (missing.Count > 0)
                    {
                        response.Warnings.Add($"Tabela '{tablePreview.TableName}' ({tablePreview.Key}) nema obavezna polja: {string.Join(", ", missing)}.");

                        // Hard-block only when Artikli cannot be identified reliably.
                        if (req.Key.Equals("artikli", StringComparison.OrdinalIgnoreCase))
                            response.CanImport = false;
                    }

                    // Best-effort sample validation (nulls/duplicates) to catch mapping issues early.
                    TryAddSampleDataWarnings(conn, tablePreview, req.Value, response.Warnings);
                }

                foreach (var tablePreview in response.Tables.Where(t => t.Found && t.HasRows))
                {
                    if (tablePreview.TotalMappings > 0 && tablePreview.MatchedMappings == 0)
                    {
                        response.Warnings.Add($"Tabela '{tablePreview.TableName}' ima podatke ({tablePreview.RowCount} redova), ali nijedno kljucno polje nije mapirano za '{tablePreview.Key}'.");
                    }
                }

                response.MappedAccessTables = response.Tables.Count(t => t.Found);
                response.MappedAccessTablesWithRows = response.Tables.Count(t => t.Found && t.HasRows);
                response.MappedAccessRows = response.Tables.Where(t => t.Found).Sum(t => t.RowCount);
                response.RowCoveragePercent = response.TotalAccessRows == 0
                    ? 100d
                    : Math.Round(response.MappedAccessRows * 100d / response.TotalAccessRows, 2);

                var mappedTableKeys = map.Values
                    .Where(x => !string.IsNullOrWhiteSpace(x.TableName))
                    .Select(x => Normalize(x.TableName))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                response.UnmappedAccessTablesWithRows = tables
                    .Where(t => !mappedTableKeys.Contains(Normalize(t)))
                    .Where(t => tableRowCounts.TryGetValue(t, out var rowCount) && rowCount > 0)
                    .OrderByDescending(t => tableRowCounts[t])
                    .ToList();

                if (response.UnmappedAccessTablesWithRows.Count > 0)
                {
                    var sample = string.Join(", ", response.UnmappedAccessTablesWithRows.Take(10));
                    var hidden = response.UnmappedAccessTablesWithRows.Count - Math.Min(10, response.UnmappedAccessTablesWithRows.Count);
                    var suffix = hidden > 0 ? $" (+{hidden} dodatnih)" : string.Empty;
                    response.Warnings.Add($"Postoje Access tabele sa podacima koje nisu mapirane: {sample}{suffix}. Proveri da li ih treba ukljuciti u import.");
                }

                if (map["prodaja_zaglavlje"].TableName is null && map["dnevnik_promena"].TableName is not null)
                    response.Warnings.Add("Nije pronađena tabela prodaje — prodaja će biti sintetizovana iz DnevnikPromena (tip='Prodaja').");

                if (map["prodaja_stavke"].TableName is null && map["prodaja_zaglavlje"].TableName is not null)
                    response.Warnings.Add("Nije pronađena tabela stavki prodaje — zaglavlja bez stavki biće uvezena bez linija.");

                var foundMovements = new List<string>();
                foreach (var k in new[] { "nivelacije", "unos_robe", "povratnice", "prenos_robe" })
                {
                    if (map.TryGetValue(k, out var tm) && tm.TableName is not null)
                        foundMovements.Add(tm.TableName!);
                }
                if (foundMovements.Count > 0)
                    response.Warnings.Add($"Pronađene tabele kretanja zaliha: {string.Join(", ", foundMovements)}.");

                return response;
            }
            finally
            {
                if (snapshot.IsSnapshot)
                    TryDeleteFile(snapshot.FilePath);
            }
        }, ct);
    }

    private static AccessImportTablePreview BuildTablePreview(
        OdbcConnection conn,
        string key,
        TableMatch tableMatch,
        Dictionary<string, int> tableRowCounts)
    {
        var tableName = tableMatch.TableName;
        var preview = new AccessImportTablePreview
        {
            Key = key,
            TableName = tableName,
            MatchStrategy = tableMatch.Strategy,
            RowCount = tableName is null
                ? 0
                : (tableRowCounts.TryGetValue(tableName, out var rowCount) ? rowCount : RowCount(conn, tableName))
        };

        if (string.IsNullOrWhiteSpace(tableName))
            return preview;

        preview.AccessColumns = ReadColumnNames(conn, tableName);
        preview.FieldMappings = BuildFieldMappingsPreview(key, preview.AccessColumns);
        preview.TotalMappings = preview.FieldMappings.Count;
        preview.MatchedMappings = preview.FieldMappings.Count(m =>
            !string.IsNullOrWhiteSpace(m.SourceColumn) &&
            m.Status.Equals("matched", StringComparison.OrdinalIgnoreCase));
        preview.MappingCoveragePercent = preview.TotalMappings == 0
            ? 100d
            : Math.Round(preview.MatchedMappings * 100d / preview.TotalMappings, 2);

        var mappedSourceColumns = preview.FieldMappings
            .Where(m => !string.IsNullOrWhiteSpace(m.SourceColumn))
            .Select(m => Normalize(m.SourceColumn))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        preview.UnmappedAccessColumns = preview.AccessColumns
            .Where(column => !mappedSourceColumns.Contains(Normalize(column)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return preview;
    }

    private static List<string> ReadColumnNames(OdbcConnection conn, string table)
    {
        var columns = new List<string>();
        try
        {
            using var cmd = new OdbcCommand($"SELECT * FROM {QuoteAccessIdentifier(table)} WHERE 1=0", conn);
            using var r = cmd.ExecuteReader();
            if (r is null) return columns;

            for (var i = 0; i < r.FieldCount; i++)
                columns.Add(r.GetName(i));
        }
        catch
        {
            // Best-effort preview. Import path itself performs stronger validation/parsing.
        }
        return columns;
    }

    private static List<AccessImportFieldMappingPreview> BuildFieldMappingsPreview(string key, IReadOnlyList<string> columns)
    {
        if (!PreviewFieldMappings.TryGetValue(key, out var fieldAliases) || fieldAliases.Count == 0)
            return new List<AccessImportFieldMappingPreview>();

        var normalizedColumns = columns
            .Select(c => new { Original = c, Normalized = Normalize(c) })
            .GroupBy(x => x.Normalized, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Original, StringComparer.OrdinalIgnoreCase);

        var output = new List<AccessImportFieldMappingPreview>(fieldAliases.Count);
        foreach (var field in fieldAliases)
        {
            string? matchedColumn = null;
            foreach (var alias in field.Aliases)
            {
                if (normalizedColumns.TryGetValue(Normalize(alias), out matchedColumn))
                    break;
            }

            output.Add(new AccessImportFieldMappingPreview
            {
                TargetField = field.TargetField,
                SourceColumn = matchedColumn,
                Status = matchedColumn is null ? "missing" : "matched"
            });
        }

        return output;
    }

    private static List<string> FindMissingPreviewFields(AccessImportTablePreview tablePreview, string[] requiredTargets)
    {
        if (requiredTargets.Length == 0)
            return new List<string>();

        // If mappings couldn't be computed (ODBC preview failed), treat all required fields as missing.
        if (tablePreview.FieldMappings.Count == 0)
            return requiredTargets.ToList();

        var missing = new List<string>();
        foreach (var field in requiredTargets)
        {
            var mapping = tablePreview.FieldMappings.FirstOrDefault(m =>
                m.TargetField.Equals(field, StringComparison.OrdinalIgnoreCase));

            if (mapping is null ||
                string.IsNullOrWhiteSpace(mapping.SourceColumn) ||
                mapping.Status.Equals("missing", StringComparison.OrdinalIgnoreCase))
            {
                missing.Add(field);
            }
        }

        return missing;
    }

    private static bool IsProdajaLineTableByColumns(IReadOnlyCollection<string> columns)
    {
        var normalized = columns.Select(Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return normalized.Contains("iddnevnik")
            && normalized.Contains("idartikal")
            && normalized.Contains("kolicina")
            && (normalized.Contains("prodajnacena") || normalized.Contains("cena"))
            && !normalized.Contains("datumprodaje")
            && !normalized.Contains("brojracuna");
    }

    public async Task<AccessImportRunResponse> ImportAsync(
        string accessFilePath,
        bool includeAnalytics,
        bool overwriteExisting,
        bool includeTemporaryTables = false,
        CancellationToken ct = default)
    {
        EnsurePlatformSupport();
        if (!File.Exists(accessFilePath))
            throw new FileNotFoundException("ACCDB fajl nije pronađen.", accessFilePath);

        var started = DateTime.UtcNow;
        var batch = new DataImportBatch
        {
            SourceSystem = "access",
            SourceFileName = Path.GetFileName(accessFilePath),
            StartedAtUtc = started,
            Status = "running"
        };

        _trendDb.DataImportBatches.Add(batch);
        await _trendDb.SaveChangesAsync(ct);

        var result = new AccessImportRunResponse
        {
            BatchId = batch.Id,
            SourceFileName = batch.SourceFileName,
            IncludeAnalytics = includeAnalytics,
            StartedAtUtc = started
        };

        var snapshot = CreateSnapshotIfLocked(accessFilePath);
        if (!string.IsNullOrWhiteSpace(snapshot.Warning))
            result.Warnings.Add(snapshot.Warning);

        try
        {
            await Task.Run(() => ImportTrendplus(snapshot.FilePath, overwriteExisting, includeTemporaryTables, result), ct);
            await _trendDb.SaveChangesAsync(ct);
            await ResetTrendplusSequencesAsync(ct);

            if (includeAnalytics)
                await SyncAnalyticsAsync(result, ct);

            result.Status = "completed";
            result.CompletedAtUtc = DateTime.UtcNow;
            batch.Status = "completed";
            batch.CompletedAtUtc = result.CompletedAtUtc;
            batch.SummaryJson = JsonSerializer.Serialize(result);
            await _trendDb.SaveChangesAsync(ct);

            return result;
        }
        catch (Exception ex)
        {
            result.Status = "failed";
            result.CompletedAtUtc = DateTime.UtcNow;
            result.Warnings.Add(ex.GetBaseException().Message);

            batch.Status = "failed";
            batch.CompletedAtUtc = result.CompletedAtUtc;
            batch.ErrorMessage = ex.GetBaseException().Message;
            batch.SummaryJson = JsonSerializer.Serialize(result);

            try
            {
                foreach (var entry in _trendDb.ChangeTracker.Entries().Where(e => !ReferenceEquals(e.Entity, batch)).ToList())
                    entry.State = EntityState.Detached;

                if (_trendDb.Entry(batch).State == EntityState.Detached)
                    _trendDb.DataImportBatches.Attach(batch);

                _trendDb.Entry(batch).State = EntityState.Modified;
                await _trendDb.SaveChangesAsync(ct);
            }
            catch (Exception saveFailedEx)
            {
                _logger.LogWarning(saveFailedEx, "Failed to persist failed Access import batch status for batch {BatchId}.", batch.Id);
            }

            _logger.LogError(ex, "Access import failed.");
            throw;
        }
        finally
        {
            if (snapshot.IsSnapshot)
                TryDeleteFile(snapshot.FilePath);
        }
    }

    private sealed record TableMatch(string? TableName, string Strategy);

    public async Task<List<AccessImportBatchDto>> GetRecentBatchesAsync(int take = 20, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        try
        {
            return await _trendDb.DataImportBatches
                .AsNoTracking()
                .OrderByDescending(x => x.StartedAtUtc)
                .Take(take)
                .Select(x => new AccessImportBatchDto
                {
                    Id = x.Id,
                    SourceSystem = x.SourceSystem,
                    SourceFileName = x.SourceFileName,
                    StartedAtUtc = x.StartedAtUtc,
                    CompletedAtUtc = x.CompletedAtUtc,
                    Status = x.Status,
                    SummaryJson = x.SummaryJson,
                    ErrorMessage = x.ErrorMessage,
                    DurationSeconds = x.DurationSeconds,
                    TotalImported = x.TotalImported,
                    TotalUpdated = x.TotalUpdated,
                    TotalErrors = x.TotalErrors,
                    DataOrigin = x.DataOrigin
                })
                .ToListAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            // Legacy DB compatibility: pre-015/016 schemas may miss batch metrics/DataOrigin columns.
            _logger.LogWarning(
                ex,
                "Access import batches query hit legacy schema (missing columns). Falling back to compatibility projection.");

            return await _trendDb.DataImportBatches
                .AsNoTracking()
                .OrderByDescending(x => x.StartedAtUtc)
                .Take(take)
                .Select(x => new AccessImportBatchDto
                {
                    Id = x.Id,
                    SourceSystem = x.SourceSystem,
                    SourceFileName = x.SourceFileName,
                    StartedAtUtc = x.StartedAtUtc,
                    CompletedAtUtc = x.CompletedAtUtc,
                    Status = x.Status,
                    SummaryJson = x.SummaryJson,
                    ErrorMessage = x.ErrorMessage,
                    DurationSeconds = null,
                    TotalImported = 0,
                    TotalUpdated = 0,
                    TotalErrors = 0,
                    DataOrigin = "access"
                })
                .ToListAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // Legacy DB compatibility: some environments may not have DataImportBatches table yet.
            _logger.LogWarning(
                ex,
                "Access import batches table is missing. Returning empty list as compatibility fallback.");
            return [];
        }
    }

    public async Task<DeleteBatchResult> DeleteBatchAsync(long batchId, bool includeAnalytics = true, CancellationToken ct = default)
    {
        var batch = await _trendDb.DataImportBatches.FindAsync([batchId], ct);
        if (batch is null)
            return new DeleteBatchResult { Found = false };

        var sfDeleted = 0;
        var slfDeleted = 0;
        var pdDeleted = 0;
        var imDeleted = 0;
        var suppDeleted = 0;
        var seasDeleted = 0;
        var typeDeleted = 0;
        var storeDeleted = 0;

        // Delete transactional / master data
        // Stavke must be deleted before zaglavlja (FK constraint), filtered via parent
        var pvStavkeDeleted = await _trendDb.PovracajStavke
            .Where(s => _trendDb.PovracajZaglavlja
                .Where(z => z.DataOrigin == "access")
                .Select(z => z.Id)
                .Contains(s.IdPovracaj))
            .ExecuteDeleteAsync(ct);
        var pvDeleted2 = await _trendDb.PovracajZaglavlja.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
        var dnDeleted  = await _trendDb.DnevnikPromena   .Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
        var svDeleted  = await _trendDb.ProdajaStavke
            .Where(s => _trendDb.ProdajaZaglavlja
                .Where(z => z.DataOrigin == "access")
                .Select(z => z.Id)
                .Contains(s.IdProdaja))
            .ExecuteDeleteAsync(ct);
        var pvDeleted  = await _trendDb.ProdajaZaglavlja.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
        var arDeleted  = await _trendDb.Artikli         .Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
        var seDeleted  = await _trendDb.Sezone          .Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
        var doDeleted  = await _trendDb.Dobavljaci      .Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
        var tiDeleted  = await _trendDb.TipoviObuce     .Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);

        if (includeAnalytics)
        {
            var accessStoreIds = await _analyticsDb.SalesFacts
                .Where(x => x.DataOrigin == "access")
                .Select(x => x.StoreId)
                .Distinct()
                .ToListAsync(ct);

            // Delete analytics data imported from Access (DataOrigin="access")
            // Note: per-batch FK does not exist in analytics tables, so this removes all Access-origin rows.
            sfDeleted = await _analyticsDb.SalesFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
            slfDeleted = await _analyticsDb.SalesLineFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
            pdDeleted = await _analyticsDb.ProductsDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
            imDeleted = await _analyticsDb.InventoryMovementFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
            suppDeleted = await _analyticsDb.SuppliersDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
            seasDeleted = await _analyticsDb.SeasonsDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
            typeDeleted = await _analyticsDb.FootwearTypesDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
            storeDeleted = await _analyticsDb.StoresDim
                .Where(x => (x.DataOrigin == "access" || accessStoreIds.Contains(x.StoreId))
                            && !_analyticsDb.SalesFacts.Any(sf => sf.StoreId == x.StoreId))
                .ExecuteDeleteAsync(ct);
        }

        _trendDb.DataImportBatches.Remove(batch);
        await _trendDb.SaveChangesAsync(ct);

        var cacheInvalidated = false;
        if (includeAnalytics && _analyticsCache is not null)
        {
            await _analyticsCache.RemoveByPrefixAsync(AnalyticsCacheKeys.Prefix, ct);
            cacheInvalidated = true;
        }

        _logger.LogInformation(
            "Deleted access-import batch {BatchId}: artikli={Ar}, prodaja={Pv}/{Sv}, dnevnik={Dn}, povracaj={Pv2}/{PvS}, sezone={Se}, dobavljaci={Do}, tipovi={Ti}, analytics={IncludeAnalytics} pd={Pd}/sf={Sf}/slf={Slf}/im={Im}/sup={Sup}/seas={Seas}/types={Types}/stores={Stores}, cacheInvalidated={CacheInvalidated}",
            batchId, arDeleted, pvDeleted, svDeleted, dnDeleted, pvDeleted2, pvStavkeDeleted, seDeleted, doDeleted, tiDeleted, includeAnalytics, pdDeleted, sfDeleted, slfDeleted, imDeleted, suppDeleted, seasDeleted, typeDeleted, storeDeleted, cacheInvalidated);

        return new DeleteBatchResult
        {
            Found          = true,
            BatchId        = batchId,
            IncludeAnalytics = includeAnalytics,
            ArtikliDeleted = arDeleted,
            SezoneDeleted  = seDeleted,
            TipoviDeleted  = tiDeleted,
            DobavljaciDeleted = doDeleted,
            ProdajaDeleted = pvDeleted,
            StavkeDeleted  = svDeleted,
            DnevnikDeleted = dnDeleted,
            PovracajDeleted = pvDeleted2,
            PovracajStavkeDeleted = pvStavkeDeleted,
            ProductsDimDeleted   = pdDeleted,
            SalesFactsDeleted    = sfDeleted,
            SalesLineFactsDeleted = slfDeleted,
            InventoryMovementsDeleted = imDeleted,
            SuppliersDimDeleted = suppDeleted,
            SeasonsDimDeleted = seasDeleted,
            FootwearTypesDimDeleted = typeDeleted,
            StoresDimDeleted = storeDeleted,
            CacheInvalidated = cacheInvalidated
        };
    }

    private void ImportTrendplus(string accessFilePath, bool overwriteExisting, bool includeTemporaryTables, AccessImportRunResponse result)
    {
        using var conn = CreateOdbcConnection(accessFilePath);
        conn.Open();

        var tables = GetUserTables(conn, includeTemporaryTables);
        var tipovi        = FindTable(conn, tables, TipoviCandidates);
        var dobavljaci    = FindTable(conn, tables, DobavljaciCandidates);
        var sezone        = FindTable(conn, tables, SezoneCandidates,   sigRequired: ["idsezona", "naziv"]);
        var artikli       = FindTable(conn, tables, ArtikliCandidates,  sigRequired: ["idartikal", "naziv"], sigBonus: ["nabavnacena", "prodajnacena", "plu"]);
        var prodaja       = FindTable(conn, tables, ProdajaCandidates);
        var prodajaStavke = FindTable(conn, tables, ProdajaStavkeCandidates);
        var dnevnik       = FindTable(conn, tables, DnevnikPromenaCandidates, sigRequired: ["iddnevnik", "datum"]);
        var povracaj      = FindTable(conn, tables, PovracajCandidates);
        var povracajStavke = FindTable(conn, tables, PovracajStavkeCandidates2);
        // New movement types
        var nivelacije    = FindTable(conn, tables, NivelacijeCandidates,  sigRequired: ["idartikal", "novacena"]);
        var unosRobe      = FindTable(conn, tables, UnosRobeCandidates,    sigRequired: ["idartikal", "kolicina", "iddobavljac"]);
        var povratnice    = FindTable(conn, tables, PovratniceCandidates,  sigRequired: ["idartikal", "kolicina"], sigBonus: ["razlog", "idpovratnice"]);
        var prenosRobe    = FindTable(conn, tables, PrenosRobeCandidates,  sigRequired: ["idartikal", "kolicina"], sigBonus: ["idobjekatiz", "idobjekatulaz", "idobjekat"]);
        var objekti       = FindTable(conn, tables, ObjekatCandidates,     sigRequired: ["idobjekat", "nazivobjekta"]);
        var sourceRowsByTable = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        void AddSourceRowCount(string key, string? tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                return;
            sourceRowsByTable[key] = RowCount(conn, tableName);
        }

        AddSourceRowCount("tipovi_obuce", tipovi);
        AddSourceRowCount("dobavljaci", dobavljaci);
        AddSourceRowCount("sezone", sezone);
        AddSourceRowCount("objekti", objekti);
        AddSourceRowCount("artikli", artikli);
        AddSourceRowCount("dnevnik_promena", dnevnik);
        AddSourceRowCount("prodaja_zaglavlje", prodaja);
        AddSourceRowCount("prodaja_stavke", prodajaStavke);
        AddSourceRowCount("povracaj_zaglavlje", povracaj);
        AddSourceRowCount("povracaj_stavke", povracajStavke);
        AddSourceRowCount("nivelacije", nivelacije);
        AddSourceRowCount("unos_robe", unosRobe);
        AddSourceRowCount("povratnice", povratnice);
        AddSourceRowCount("prenos_robe", prenosRobe);

        if (artikli is null)
            throw new InvalidOperationException("Nije pronađena tabela za artikle u ACCDB fajlu.");

        if (tipovi is not null) ImportTipovi(conn, tipovi, overwriteExisting, result);
        if (dobavljaci is not null) ImportDobavljaci(conn, dobavljaci, overwriteExisting, result);
        if (sezone is not null) ImportSezone(conn, sezone, overwriteExisting, result);
        if (objekti is not null) ImportObjekti(conn, objekti, overwriteExisting, result);
        ImportArtikli(conn, artikli, overwriteExisting, result);
        if (dnevnik is not null) ImportDnevnikPromena(conn, dnevnik, overwriteExisting, result);

        var importedProdajaFromLineTable = false;
        if (prodaja is not null && IsProdajaLineTable(conn, prodaja))
        {
            importedProdajaFromLineTable = true;
            result.Warnings.Add($"Tabela '{prodaja}' prepoznata je kao tabela stavki prodaje (IDDnevnik/IDArtikal). Uvozim prodaju kroz vezu sa DnevnikPromena.");
            ImportProdajaFromLineTable(conn, prodaja, overwriteExisting, result);
        }
        else
        {
            if (prodaja is not null) ImportProdaja(conn, prodaja, overwriteExisting, result);
            if (prodajaStavke is not null) ImportProdajaStavke(conn, prodajaStavke, overwriteExisting, result);
        }

        if (povracaj is not null) ImportPovracaj(conn, povracaj, overwriteExisting, result);
        if (povracajStavke is not null) ImportPovracajStavke(conn, povracajStavke, overwriteExisting, result);

        // Movement types — all map to DnevnikPromena with different TipPromene values
        if (nivelacije is not null)  ImportNivelacije(conn, nivelacije, overwriteExisting, result);
        if (unosRobe is not null)    ImportUnosRobe(conn, unosRobe, overwriteExisting, result);
        if (povratnice is not null)  ImportPovratnice(conn, povratnice, overwriteExisting, result);
        if (prenosRobe is not null)  ImportPrenosRobe(conn, prenosRobe, overwriteExisting, result);

        // If Access DB has no dedicated prodaja tables but tracks sales in DnevnikPromena,
        // synthesize ProdajaZaglavlje + ProdajaStavke from "Prodaja" type journal entries.
        if (prodaja is null && dnevnik is not null && !importedProdajaFromLineTable)
            SynthesizeProdajaFromDnevnik(overwriteExisting, result);

        var importedRowsByTable = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["tipovi_obuce"] = result.TipoviInserted + result.TipoviUpdated,
            ["dobavljaci"] = result.DobavljaciInserted + result.DobavljaciUpdated,
            ["sezone"] = result.SezoneInserted + result.SezoneUpdated,
            ["objekti"] = result.ObjekatInserted + result.ObjekatUpdated,
            ["artikli"] = result.ArtikliInserted + result.ArtikliUpdated,
            ["dnevnik_promena"] = result.DnevnikInserted + result.DnevnikUpdated,
            ["prodaja_zaglavlje"] = result.ProdajaInserted + result.ProdajaUpdated,
            ["prodaja_stavke"] = result.ProdajaStavkeInserted + result.ProdajaStavkeUpdated,
            ["povracaj_zaglavlje"] = result.PovracajInserted + result.PovracajUpdated,
            ["povracaj_stavke"] = result.PovracajStavkeInserted + result.PovracajStavkeUpdated,
            ["nivelacije"] = result.NivelacijeInserted,
            ["unos_robe"] = result.UnosRobeInserted,
            ["povratnice"] = result.PovratnicaInserted,
            ["prenos_robe"] = result.PrenosRobeInserted,
        };

        result.SourceRowsByTable = sourceRowsByTable;
        result.ImportedRowsByTable = importedRowsByTable;
        FinalizeCoverageMetrics(
            result,
            sourceRowsByTable,
            importedRowsByTable,
            groupedProdajaHeaders: importedProdajaFromLineTable || (prodaja is null && dnevnik is not null),
            expandedPrenosRobe: prenosRobe is not null);

        foreach (var (key, sourceRows) in sourceRowsByTable.Where(x => x.Value > 0))
        {
            importedRowsByTable.TryGetValue(key, out var importedRows);
            if (importedRows == 0)
            {
                result.Warnings.Add($"[coverage] Tabela '{key}' ima {sourceRows} redova u Access bazi, ali 0 upisanih/azuriranih redova. Proveri mapiranje i quality podataka.");
            }
        }
    }

    private void ImportTipovi(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = _trendDb.TipoviObuce.ToDictionary(x => x.Id);
        foreach (var row in ReadRows(conn, table))
        {
            var naziv = S(row, "naziv", "tip", "tipobuce", "name");
            if (string.IsNullOrWhiteSpace(naziv)) continue;
            var id = I(row, "id", "idtipobuce", "tipid");
            if (!id.HasValue || id.Value <= 0) continue;
            MarkAccepted(result, "tipovi_obuce");

            if (!existing.TryGetValue(id.Value, out var e))
            {
                e = new TipObuce { Id = id.Value, Naziv = naziv!, DataOrigin = "access" };
                _trendDb.TipoviObuce.Add(e);
                existing[e.Id] = e;
                result.TipoviInserted++;
            }
            else if (overwriteExisting)
            {
                e.Naziv = naziv!;
                e.DataOrigin = "access";
                result.TipoviUpdated++;
            }
        }
    }

    private void ImportDobavljaci(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = _trendDb.Dobavljaci.ToDictionary(x => x.Id);
        foreach (var row in ReadRows(conn, table))
        {
            var naziv = S(row, "naziv", "dobavljac", "supplier", "name");
            if (string.IsNullOrWhiteSpace(naziv)) continue;
            var id = I(row, "id", "iddobavljac", "supplierid");
            if (!id.HasValue || id.Value <= 0) continue;
            MarkAccepted(result, "dobavljaci");

            if (!existing.TryGetValue(id.Value, out var e))
            {
                e = new Dobavljac
                {
                    Id = id.Value,
                    Naziv = naziv,
                    Adresa = S(row, "adresa", "address"),
                    Telefon = S(row, "telefon", "phone", "brteldob", "brteldobav", "tel", "br_tel", "mobilni", "mobile"),
                    Napomena = S(row, "napomena", "note"),
                    DataOrigin = "access"
                };
                _trendDb.Dobavljaci.Add(e);
                existing[e.Id] = e;
                result.DobavljaciInserted++;
            }
            else if (overwriteExisting)
            {
                e.Naziv = naziv;
                e.Adresa = S(row, "adresa", "address");
                e.Telefon = S(row, "telefon", "phone", "brteldob", "brteldobav", "tel", "br_tel", "mobilni", "mobile");
                e.Napomena = S(row, "napomena", "note");
                e.DataOrigin = "access";
                result.DobavljaciUpdated++;
            }
        }
    }

    private void ImportSezone(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = _trendDb.Sezone.ToDictionary(x => x.Id);
        foreach (var row in ReadRows(conn, table))
        {
            var naziv = S(row, "naziv", "sezona", "name");
            if (string.IsNullOrWhiteSpace(naziv)) continue;
            var id = I(row, "id", "idsezona", "seasonid");
            if (!id.HasValue || id.Value <= 0) continue;
            MarkAccepted(result, "sezone");

            var datumOd = DT(row, "datumod", "od", "startdate", "sezonapocetak", "sezonaod", "pocetak") ?? DateTime.UtcNow.Date;
            var datumDo = DT(row, "datumdo", "do", "enddate", "sezonakraj", "sezonadokraj", "kraj") ?? datumOd.AddMonths(6);

            if (!existing.TryGetValue(id.Value, out var e))
            {
                e = new Sezona
                {
                    Id = id.Value,
                    Naziv = naziv!,
                    DatumOd = datumOd,
                    DatumDo = datumDo,
                    DataOrigin = "access"
                };
                _trendDb.Sezone.Add(e);
                existing[e.Id] = e;
                result.SezoneInserted++;
            }
            else if (overwriteExisting)
            {
                e.Naziv = naziv!;
                e.DatumOd = datumOd;
                e.DatumDo = datumDo;
                e.DataOrigin = "access";
                result.SezoneUpdated++;
            }
        }
    }

    private void ImportArtikli(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = _trendDb.Artikli.ToDictionary(x => x.Id);
        var usedIds = existing.Keys.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        foreach (var row in ReadRows(conn, table))
        {
            // Try specific name candidates first; fall back to "artikal" last because
            // some Access POS DBs store the descriptive name exactly in that column.
            var naziv = S(row,
                "naziv", "nazivartikal", "nazivarticle", "nazivproizvoda",
                "opis", "opisartikal", "opisproizvoda", "description", "desc",
                "proizvod", "name", "productname", "articlename", "itemname", "ime",
                "artikal", "article", "item", "roba"); // last-resort columns
            if (string.IsNullOrWhiteSpace(naziv)) continue;
            MarkAccepted(result, "artikli");
            var id = I(row, "id", "idartikal", "productid");

            Artikli? e = null;
            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && existing.TryGetValue(sourceId, out var found))
                e = found;

            if (e is null)
            {
                var assignedId = sourceId;
                if (assignedId <= 0 || usedIds.Contains(assignedId))
                    assignedId = AllocateNextId(usedIds, ref nextGeneratedId);
                else
                    usedIds.Add(assignedId);

                e = new Artikli { Id = assignedId };
                _trendDb.Artikli.Add(e);
                existing[assignedId] = e;
                result.ArtikliInserted++;
            }
            else if (overwriteExisting)
            {
                result.ArtikliUpdated++;
            }
            else
            {
                continue;
            }

            e.PLU = S(row, "plu", "sku", "sifra", "sifraartikla", "barcode", "barkod", "kod", "code", "artikal");
            e.Naziv = naziv!;
            e.IDTipObuce = I(row, "idtipobuce", "tipobuceid", "footweartypeid");
            e.IDDobavljac = I(row, "iddobavljac", "dobavljacid", "supplierid");
            e.NabavnaCena = D(row, "nabavnacena", "purchaseprice", "cost");
            e.NabavnaCenaDin = D(row, "nabavnacenadin", "purchasepricersd");
            e.PrvaProdajnaCena = D(row, "prvaprodajnacena", "firstsaleprice");
            e.ProdajnaCena = D(row, "prodajnacena", "saleprice", "price");
            e.Velicina = S(row, "velicina", "size");
            e.Boja = S(row, "boja", "color");
            e.Materijal = S(row, "materijal", "material", "materijal_gornjista", "gornjiste",
                               "upper", "fabric", "sastav", "sastav_gornjista");
            e.Kolicina = I(row, "kolicina", "kol", "qty", "quantity", "stock", "stanje", "stanjeartikla",
                              "stanjeartikal", "lager", "zaliha", "zalihe", "raspolozivo", "inventar",
                              "stockqty", "totalqty", "total_qty", "raspolozivokolicina");
            e.MinimalnaKolicina = I(row, "minimalnakolicina", "minimumqty", "minqty", "minstock");
            e.Komentar = S(row, "komentar", "comment", "napomena", "url");
            e.IDObjekat = I(row, "idobjekat", "storeid");
            e.IDSezona = I(row, "idsezona", "seasonid");
            e.Kategorija = S(row, "kategorija", "category");
            e.Pol = S(row, "pol", "gender");
            e.ImagePath = S(row, "imagepath", "imageurl", "slika", "image");
            e.UpdatedAt = DateTime.UtcNow;
            e.DataOrigin = "access";
        }
    }

    private void ImportProdaja(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = _trendDb.ProdajaZaglavlja.ToDictionary(x => x.Id);
        foreach (var row in ReadRows(conn, table))
        {
            var id = I(row, "id", "idprodaja", "saleid");
            if (!id.HasValue || id.Value <= 0) continue;
            MarkAccepted(result, "prodaja_zaglavlje");

            if (!existing.TryGetValue(id.Value, out var e))
            {
                e = new Domain.Model.Prodaja.ProdajaZaglavlje
                {
                    Id = id.Value,
                    BrojRacuna = S(row, "brojracuna", "brojkalkulacije", "invoice", "receiptnumber"),
                    DatumProdaje = DT(row, "datumprodaje", "datum", "saledate") ?? DateTime.UtcNow,
                    NacinPlacanja = S(row, "nacinplacanja", "paymenttype"),
                    IDObjekat = I(row, "idobjekat", "storeid"),
                    KorisnikIme = S(row, "korisnikime", "korisnik", "username", "operater", "kasir"),
                    DataOrigin = "access"
                };
                _trendDb.ProdajaZaglavlja.Add(e);
                existing[e.Id] = e;
                result.ProdajaInserted++;
            }
            else if (overwriteExisting)
            {
                e.BrojRacuna = S(row, "brojracuna", "brojkalkulacije", "invoice", "receiptnumber");
                e.DatumProdaje = DT(row, "datumprodaje", "datum", "saledate") ?? DateTime.UtcNow;
                e.NacinPlacanja = S(row, "nacinplacanja", "paymenttype");
                e.IDObjekat = I(row, "idobjekat", "storeid");
                e.KorisnikIme = S(row, "korisnikime", "korisnik", "username", "operater", "kasir");
                e.DataOrigin = "access";
                result.ProdajaUpdated++;
            }
        }
    }

    private void ImportProdajaStavke(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = _trendDb.ProdajaStavke.ToDictionary(x => x.Id);
        var usedIds = existing.Keys.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        var saleIds = _trendDb.ProdajaZaglavlja.Select(x => x.Id).ToHashSet();

        foreach (var row in ReadRows(conn, table))
        {
            var idProdaja = I(row, "idprodaja", "saleid", "idzaglavlje");
            var idArtikal = I(row, "idartikal", "productid", "artiklid");
            if (!idProdaja.HasValue || !idArtikal.HasValue || !saleIds.Contains(idProdaja.Value)) continue;
            MarkAccepted(result, "prodaja_stavke");

            var id = I(row, "id", "idstavka", "lineid");
            var qty = I(row, "kolicina", "qty", "quantity") ?? 0;
            var cena = D(row, "cena", "unitprice", "price") ?? 0m;

            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && existing.TryGetValue(sourceId, out var e))
            {
                if (!overwriteExisting) continue;
                e.IdProdaja = idProdaja.Value;
                e.IdArtikal = idArtikal.Value;
                e.Kolicina = qty;
                e.Cena = cena;
                result.ProdajaStavkeUpdated++;
            }
            else
            {
                var assignedId = sourceId;
                if (assignedId <= 0 || usedIds.Contains(assignedId))
                    assignedId = AllocateNextId(usedIds, ref nextGeneratedId);
                else
                    usedIds.Add(assignedId);

                var newLine = new Domain.Model.Prodaja.ProdajaStavka
                {
                    Id = assignedId,
                    IdProdaja = idProdaja.Value,
                    IdArtikal = idArtikal.Value,
                    Kolicina = qty,
                    Cena = cena
                };
                _trendDb.ProdajaStavke.Add(newLine);
                existing[newLine.Id] = newLine;
                result.ProdajaStavkeInserted++;
            }
        }
    }

    private static bool IsProdajaLineTable(OdbcConnection conn, string table)
    {
        try
        {
            using var cmd = new OdbcCommand($"SELECT * FROM {QuoteAccessIdentifier(table)} WHERE 1=0", conn);
            using var r = cmd.ExecuteReader();
            if (r is null) return false;

            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < r.FieldCount; i++)
                cols.Add(Normalize(r.GetName(i)));

            return cols.Contains("iddnevnik")
                && cols.Contains("idartikal")
                && cols.Contains("kolicina")
                && (cols.Contains("prodajnacena") || cols.Contains("cena"))
                && !cols.Contains("datumprodaje")
                && !cols.Contains("brojracuna");
        }
        catch
        {
            return false;
        }
    }

    private void ImportProdajaFromLineTable(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existingZaglavlja = _trendDb.ProdajaZaglavlja.ToDictionary(x => x.Id);
        var existingBrojevi = BuildDuplicateTolerantLookup(
            _trendDb.ProdajaZaglavlja
                .AsNoTracking()
                .Where(x => x.BrojRacuna != null)
                .OrderBy(x => x.Id)
                .ToList(),
            x => x.BrojRacuna,
            out var duplicateBrojevi);
        AddDuplicateKeyWarning(
            result.Warnings,
            duplicateBrojevi,
            "Postoje duplikati broja racuna u postojecoj tabeli prodaje. Import ce koristiti prvi pronadjeni zapis za svaki broj racuna");
        var dnevnikById = _trendDb.DnevnikPromena.Local
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First());
        foreach (var d in _trendDb.DnevnikPromena.AsNoTracking())
        {
            if (!dnevnikById.TryGetValue(d.Id, out _))
                dnevnikById[d.Id] = d;
        }

        var maxStavkaId = _trendDb.ProdajaStavke.Any()
            ? _trendDb.ProdajaStavke.Max(x => x.Id)
            : 0;

        var existingLineCounts = _trendDb.ProdajaStavke
            .AsNoTracking()
            .GroupBy(x => new { x.IdProdaja, x.IdArtikal, x.Kolicina, x.Cena })
            .Select(g => new
            {
                g.Key.IdProdaja,
                g.Key.IdArtikal,
                g.Key.Kolicina,
                g.Key.Cena,
                Count = g.Count()
            })
            .AsEnumerable()
            .ToDictionary(
                x => BuildProdajaLineKey(x.IdProdaja, x.IdArtikal, x.Kolicina, x.Cena),
                x => x.Count,
                StringComparer.OrdinalIgnoreCase);

        var consumedExistingLineCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in ReadRows(conn, table))
        {
            var sourceSaleId = I(row, "iddnevnik", "idprodaja", "saleid", "iddnevnikpromene", "iddnevnikpromena");
            var idArtikal = I(row, "idartikal", "artikalid", "artiklid", "productid");
            if (!sourceSaleId.HasValue || sourceSaleId.Value <= 0 || !idArtikal.HasValue || idArtikal.Value <= 0)
                continue;
            MarkAccepted(result, "prodaja_zaglavlje");

            var qty = I(row, "kolicina", "qty", "quantity") ?? 1;
            if (qty <= 0) qty = 1;

            var cena = D(row, "prodajnacena", "cena", "unitprice", "price") ?? 0m;
            var idObjekat = I(row, "idobjekat", "storeid");

            if (!existingZaglavlja.TryGetValue(sourceSaleId.Value, out var zaglavlje))
            {
                dnevnikById.TryGetValue(sourceSaleId.Value, out var dnevnik);
                zaglavlje = new Domain.Model.Prodaja.ProdajaZaglavlje
                {
                    Id = sourceSaleId.Value,
                    BrojRacuna = dnevnik?.BrojRacuna ?? S(row, "brojracuna", "brojkalkulacije", "invoice", "receiptnumber"),
                    DatumProdaje = dnevnik?.Datum ?? DT(row, "datumprodaje", "datum", "saledate") ?? DateTime.UtcNow,
                    NacinPlacanja = S(row, "nacinplacanja", "paymenttype"),
                    IDObjekat = idObjekat,
                    DataOrigin = "access"
                };
                _trendDb.ProdajaZaglavlja.Add(zaglavlje);
                existingZaglavlja[zaglavlje.Id] = zaglavlje;
                if (zaglavlje.BrojRacuna != null)
                    existingBrojevi[zaglavlje.BrojRacuna] = zaglavlje;
                result.ProdajaInserted++;
            }
            else if (overwriteExisting)
            {
                if (string.IsNullOrWhiteSpace(zaglavlje.BrojRacuna))
                    zaglavlje.BrojRacuna = S(row, "brojracuna", "brojkalkulacije", "invoice", "receiptnumber");
                if (zaglavlje.IDObjekat is null && idObjekat.HasValue)
                    zaglavlje.IDObjekat = idObjekat.Value;
                zaglavlje.DataOrigin = "access";
                result.ProdajaUpdated++;
            }

            var lineKey = BuildProdajaLineKey(zaglavlje.Id, idArtikal.Value, qty, cena);
            existingLineCounts.TryGetValue(lineKey, out var existingCountForKey);
            consumedExistingLineCounts.TryGetValue(lineKey, out var consumedCountForKey);

            // Preserve duplicate source rows, but avoid duplicating already imported occurrences.
            if (consumedCountForKey < existingCountForKey)
            {
                consumedExistingLineCounts[lineKey] = consumedCountForKey + 1;
                continue;
            }

            _trendDb.ProdajaStavke.Add(new Domain.Model.Prodaja.ProdajaStavka
            {
                Id = ++maxStavkaId,
                IdProdaja = zaglavlje.Id,
                IdArtikal = idArtikal.Value,
                Kolicina = qty,
                Cena = cena
            });
            result.ProdajaStavkeInserted++;
        }
    }

    private static string BuildProdajaLineKey(int idProdaja, int idArtikal, int qty, decimal cena)
        => $"{idProdaja}|{idArtikal}|{qty}|{cena.ToString(CultureInfo.InvariantCulture)}";

    private void SynthesizeProdajaFromDnevnik(bool overwriteExisting, AccessImportRunResponse result)
    {
        // Collect all DnevnikPromena entries that represent a sale (already in DbContext, not yet saved).
        static bool IsSaleType(string tip) => TipPromeneConstants.IsSale(tip);

        var saleEntries = _trendDb.DnevnikPromena.Local
            .Where(d => d.DataOrigin == "access" && IsSaleType(d.TipPromene))
            .ToList();

        if (saleEntries.Count == 0) return;

        var existingZaglavlja = _trendDb.ProdajaZaglavlja.ToDictionary(x => x.Id);
        var existingBrojevi = BuildDuplicateTolerantLookup(
            _trendDb.ProdajaZaglavlja
                .AsNoTracking()
                .Where(x => x.BrojRacuna != null)
                .OrderBy(x => x.Id)
                .ToList(),
            x => x.BrojRacuna,
            out var duplicateBrojevi);
        AddDuplicateKeyWarning(
            result.Warnings,
            duplicateBrojevi,
            "Postoje duplikati broja racuna u postojecoj tabeli prodaje. Sintetizacija prodaje ce koristiti prvi pronadjeni zapis za svaki broj racuna");

        // Group entries by BrojRacuna (or by Id when no receipt number) to form one zaglavlje per sale.
        var groups = saleEntries
            .GroupBy(d => string.IsNullOrWhiteSpace(d.BrojRacuna)
                ? $"DN-{d.Id}"  // fallback key when no receipt number
                : d.BrojRacuna!)
            .ToList();

        // Running offset to avoid PK collisions with existing rows.
        int maxId = existingZaglavlja.Count > 0 ? existingZaglavlja.Keys.Max() : 0;
        int maxStavkaId = _trendDb.ProdajaStavke.Any() ? _trendDb.ProdajaStavke.Select(x => x.Id).Max() : 0;

        foreach (var grp in groups)
        {  
            var first = grp.First();

            // Re-use zaglavlje if it already exists (by ID or BrojRacuna).
            if (!existingZaglavlja.TryGetValue(first.Id, out var zaglavlje) &&
                !existingBrojevi.TryGetValue(grp.Key, out zaglavlje))
            {
                zaglavlje = new Domain.Model.Prodaja.ProdajaZaglavlje
                {
                    Id           = ++maxId,
                    BrojRacuna   = string.IsNullOrWhiteSpace(first.BrojRacuna) ? null : first.BrojRacuna,
                    DatumProdaje = first.Datum,
                    NacinPlacanja = null,
                    IDObjekat    = null,
                    DataOrigin   = "access"
                };
                _trendDb.ProdajaZaglavlja.Add(zaglavlje);
                existingZaglavlja[zaglavlje.Id] = zaglavlje;
                if (zaglavlje.BrojRacuna != null)
                    existingBrojevi[zaglavlje.BrojRacuna] = zaglavlje;
                result.ProdajaInserted++;
            }

            // Add a stavka for every journal entry in this group that has an article referenced.
            foreach (var d in grp.Where(d => d.ArtikalId.HasValue && d.ArtikalId.Value > 0))
            {
                var stavkaCena = (d.NovaProdajnaCena ?? d.StaraProdajnaCena ?? (d.Iznos > 0 ? d.Iznos : null)) ?? 0m;
                var stavkaQty  = (d.Kolicina.HasValue && d.Kolicina.Value > 0) ? d.Kolicina.Value : 1;
                _trendDb.ProdajaStavke.Add(new Domain.Model.Prodaja.ProdajaStavka
                {
                    Id         = ++maxStavkaId,
                    IdProdaja  = zaglavlje.Id,
                    IdArtikal  = d.ArtikalId!.Value,
                    Kolicina   = stavkaQty,
                    Cena       = stavkaCena
                });
                result.ProdajaStavkeInserted++;
            }
        }

        result.Warnings.Add($"Sintetizovano {result.ProdajaInserted} prodaja i {result.ProdajaStavkeInserted} stavki iz DnevnikPromena (nije pronađena posebna tabela prodaje).");
    }

    private void ImportPovracajStavke(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = _trendDb.PovracajStavke.ToDictionary(x => x.Id);
        var usedIds = existing.Keys.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        var povracajIds = _trendDb.PovracajZaglavlja.Select(x => x.Id).ToHashSet();

        foreach (var row in ReadRows(conn, table))
        {
            var idPovracaj = I(row, "idpovracaj", "returnid", "idzaglavlje");
            var idArtikal = I(row, "idartikal", "productid", "artiklid");
            if (!idPovracaj.HasValue || !idArtikal.HasValue || !povracajIds.Contains(idPovracaj.Value)) continue;
            MarkAccepted(result, "povracaj_stavke");

            var id = I(row, "id", "idstavka", "lineid");
            var qty = I(row, "kolicina", "qty", "quantity") ?? 1;
            var cena = D(row, "cena", "unitprice", "price", "nabavnacena", "purchaseprice") ?? 0m;
            var razlog = S(row, "razlog", "reason");
            var stanje = S(row, "stanjeartikal", "stanjearticle", "condition", "status");

            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && existing.TryGetValue(sourceId, out var e))
            {
                if (!overwriteExisting) continue;
                e.IdPovracaj = idPovracaj.Value;
                e.IdArtikal = idArtikal.Value;
                e.Kolicina = qty;
                e.Cena = cena;
                e.Razlog = razlog;
                e.StanjeArtikla = stanje;
                result.PovracajStavkeUpdated++;
            }
            else
            {
                var assignedId = sourceId;
                if (assignedId <= 0 || usedIds.Contains(assignedId))
                    assignedId = AllocateNextId(usedIds, ref nextGeneratedId);
                else
                    usedIds.Add(assignedId);

                var newLine = new PovracajStavka
                {
                    Id = assignedId,
                    IdPovracaj = idPovracaj.Value,
                    IdArtikal = idArtikal.Value,
                    Kolicina = qty,
                    Cena = cena,
                    Razlog = razlog,
                    StanjeArtikla = stanje
                };
                _trendDb.PovracajStavke.Add(newLine);
                existing[newLine.Id] = newLine;
                result.PovracajStavkeInserted++;
            }
        }
    }

    private void ImportDnevnikPromena(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = _trendDb.DnevnikPromena.ToDictionary(x => x.Id);
        var dbExistingIds = existing.Keys.ToHashSet();
        var usedIds = existing.Keys.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        var supplierByKey = _trendDb.Dobavljaci
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.Naziv))
            .AsEnumerable()
            .GroupBy(x => NormalizeLookup(x.Naziv), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        foreach (var row in ReadRows(conn, table))
        {
            var id = I(row, "id", "iddnevnik", "iddnevnikpromene", "iddnevnikpromena", "iddnevprom", "idlog", "logid", "seqno");
            var tip = S(row, "tippromene", "vrstapromene", "vrsta", "tip", "type", "eventtype", "tipprocene", "promena",
                         "vrstaknjizenjem", "vrstaknjiz", "document", "doctype") ?? "Unos";
            var datum = DT(row, "datum", "datumizmene", "datumdokumenta", "datumprocene", "date", "eventdate", "datumpromena") ?? DateTime.UtcNow;
            var iznos = D(row, "iznos", "cena", "prodajnacena", "saleprice", "amount", "total", "vrednost", "ukupno",
                          "novacena", "novaprodajnacena", "iznospromene") ?? 0m;

            DnevnikPromena? e = null;
            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && dbExistingIds.Contains(sourceId) && existing.TryGetValue(sourceId, out var found))
                e = found;

            if (e is null)
            {
                var assignedId = sourceId;
                if (assignedId <= 0 || usedIds.Contains(assignedId))
                    assignedId = AllocateNextId(usedIds, ref nextGeneratedId);
                else
                    usedIds.Add(assignedId);

                e = new DnevnikPromena { Id = assignedId, DataOrigin = "access" };
                _trendDb.DnevnikPromena.Add(e);
                existing[assignedId] = e;
                result.DnevnikInserted++;
            }
            else if (overwriteExisting)
            {
                result.DnevnikUpdated++;
            }
            else
            {
                continue;
            }
            MarkAccepted(result, "dnevnik_promena");

            e.TipPromene = tip;
            e.Datum = datum;
            e.Iznos = iznos;
            e.Kolicina = I(row, "kolicina", "kol", "qty", "quantity", "kolicinaproizvoda");
            e.BrojRacuna = S(row, "brojracuna", "brracuna", "brrach", "brojfakture", "brfakture", "dokument",
                              "brdokumenta", "brojdokumenta", "racun", "invoice", "receiptnumber", "documentno",
                              "brnaloga", "brojnaloga", "nalog", "brojkalkulacije");

            var komentar = S(row, "komentar", "comment", "napomena", "opis", "beleska", "info", "memo");
            var dobavljacId = I(row, "iddobavljac", "dobavljacid", "supplierid", "idd", "iddob");
            if (!dobavljacId.HasValue)
            {
                var dobavljacNaziv = S(row, "dobavljac", "dobavljacnaziv", "supplier", "suppliername", "nazivdobavljaca");
                dobavljacId = ResolveSupplierIdByName(dobavljacNaziv, supplierByKey);
            }

            if (!dobavljacId.HasValue)
            {
                var extractedSupplier = ExtractSupplierNameFromComment(komentar);
                dobavljacId = ResolveSupplierIdByName(extractedSupplier, supplierByKey);
            }

            e.DobavljacId = dobavljacId;
            e.ArtikalId = I(row, "idartikal", "idartikal", "artikalid", "artiklid", "productid", "idproizvoda",
                            "artikal", "sifra", "sifraartikla", "kodartikla");
            e.StaraProdajnaCena = D(row, "staracena", "stara", "staraprodajnacena", "cenabefore", "oldprice", "cenabefore");
            e.NovaProdajnaCena = D(row, "novacena", "nova", "novaprodajnacena", "cenaafter", "newprice");
            e.Komentar = komentar;
            e.KorisnikIme = S(row, "korisnik", "korisnikime", "user", "username", "operater", "radnik", "ime",
                              "operator", "prodavac", "cashier");
            e.IDObjekat = I(row, "idobjekat", "storeid", "idobjekta", "objekatid", "idposlovnice", "prodavnicaid");
            e.RedniBroj = I(row, "rednibr", "rednibrojartikla", "rbrartikla", "rbr", "rbroj",
                             "sek", "seqno", "seq", "linebr", "redni");
            e.DataOrigin = "access";
        }
    }

    private void ImportObjekti(OdbcConnection conn, string? table, bool overwriteExisting, AccessImportRunResponse result)
    {
        if (table is null) return;
        foreach (var row in ReadRows(conn, table))
        {
            var id = I(row, "id", "idobjekat", "storeid", "idobjekta", "poslovnicaid");
            if (!id.HasValue || id.Value <= 0) continue;
            MarkAccepted(result, "objekti");
            var naziv = S(row, "nazivobjekta", "naziv", "storename", "name", "poslovnica",
                          "ime", "opisobjekta") ?? $"Objekat {id.Value}";
            _importedStores[id.Value] = (
                Name:    naziv,
                Address: S(row, "adresa", "address", "ulica"),
                Phone:   S(row, "telefon", "phone", "tel", "mobilni"),
                Manager: S(row, "menedzer", "manager", "rukovodilac", "vodja", "direktorfiliajle"));
            result.ObjekatInserted++;
        }
    }

    private void ImportNivelacije(OdbcConnection conn, string? table, bool overwriteExisting, AccessImportRunResponse result)
    {
        if (table is null) return;

        // tblNivelacije in legacy Access schema does not contain a date column.
        // Every row points to tblDnevnikPromena via IDDnevnik, so we must inherit
        // the original event date from that source row to preserve historical analytics.
        var dnevnikById = _trendDb.DnevnikPromena.Local
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First());
        foreach (var d in _trendDb.DnevnikPromena.AsNoTracking())
        {
            if (!dnevnikById.TryGetValue(d.Id, out _))
                dnevnikById[d.Id] = d;
        }

        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        foreach (var row in ReadRows(conn, table))
        {
            var idArtikal = I(row, "idartikal", "artikalid", "productid", "id_artikal");
            if (!idArtikal.HasValue) continue;
            var novaCena = D(row, "novacena", "novaprodajnacena", "newprice", "cena");
            if (!novaCena.HasValue) continue;
            MarkAccepted(result, "nivelacije");
            var staraCena = D(row, "staracena", "staraprodajnacena", "oldprice");
            var kolicina  = I(row, "kolicina", "qty", "quantity") ?? 1;
            var iznos     = Math.Abs((novaCena.Value - (staraCena ?? 0m)) * kolicina);
            var srcId     = I(row, "iddnevnik", "id", "idlog") ?? 0;

            dnevnikById.TryGetValue(srcId, out var sourceDnevnik);
            var eventDate = DT(row, "datum", "datumnivelacije", "date")
                ?? sourceDnevnik?.Datum
                ?? DateTime.UtcNow;

            var assignedId = (srcId > 0 && !usedIds.Contains(srcId))
                ? srcId : AllocateNextId(usedIds, ref next);
            usedIds.Add(assignedId);
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = assignedId,
                TipPromene = TipPromeneConstants.Nivelacija,
                Datum = eventDate,
                ArtikalId = idArtikal.Value,
                Kolicina = kolicina,
                StaraProdajnaCena = staraCena,
                NovaProdajnaCena = novaCena,
                Iznos = iznos,
                IDObjekat = I(row, "idobjekat", "storeid", "idobjekta") ?? sourceDnevnik?.IDObjekat,
                RedniBroj = I(row, "rednibr", "rbr", "seqno"),
                BrojRacuna = S(row, "brdokumenta", "iddnevnik"),
                DobavljacId = I(row, "iddobavljac", "dobavljacid", "supplierid") ?? sourceDnevnik?.DobavljacId,
                DataOrigin = "access"
            });
            result.NivelacijeInserted++;
        }
        _trendDb.SaveChanges();
    }

    private void ImportUnosRobe(OdbcConnection conn, string? table, bool overwriteExisting, AccessImportRunResponse result)
    {
        if (table is null) return;
        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        foreach (var row in ReadRows(conn, table))
        {
            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue) continue;
            MarkAccepted(result, "unos_robe");
            var kolicina     = I(row, "kolicina", "qty", "quantity") ?? 1;
            var nabavnaCena  = D(row, "nabavnacena", "purchaseprice", "cena", "nc") ?? 0m;
            var srcId        = I(row, "iddnevnik", "id", "idlog") ?? 0;
            var assignedId   = (srcId > 0 && !usedIds.Contains(srcId))
                ? srcId : AllocateNextId(usedIds, ref next);
            usedIds.Add(assignedId);
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = assignedId,
                TipPromene = TipPromeneConstants.UlazRobe,
                Datum = DT(row, "datum", "datumunosarobe", "datumulaza", "date") ?? DateTime.UtcNow,
                ArtikalId = idArtikal.Value,
                Kolicina = kolicina,
                NovaProdajnaCena = nabavnaCena,
                Iznos = nabavnaCena * kolicina,
                DobavljacId = I(row, "iddobavljac", "dobavljacid", "supplierid"),
                IDObjekat = I(row, "idobjekat", "storeid"),
                RedniBroj = I(row, "rednibr", "rbr", "seqno"),
                BrojRacuna = S(row, "brdokumenta", "iddnevnik"),
                DataOrigin = "access"
            });
            result.UnosRobeInserted++;
        }
        _trendDb.SaveChanges();
    }

    private void ImportPovratnice(OdbcConnection conn, string? table, bool overwriteExisting, AccessImportRunResponse result)
    {
        if (table is null) return;
        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        foreach (var row in ReadRows(conn, table))
        {
            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue) continue;
            MarkAccepted(result, "povratnice");
            var kolicina  = I(row, "kolicina", "qty", "quantity") ?? 1;
            var cena      = D(row, "cena", "prodajnacena", "unitprice", "pc") ?? 0m;
            var srcId     = I(row, "iddnevnik", "id", "idpovratnice", "idlog") ?? 0;
            var assignedId = (srcId > 0 && !usedIds.Contains(srcId))
                ? srcId : AllocateNextId(usedIds, ref next);
            usedIds.Add(assignedId);
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = assignedId,
                TipPromene = TipPromeneConstants.PovratKupca,
                Datum = DT(row, "datum", "datumpovratnice", "date") ?? DateTime.UtcNow,
                ArtikalId = idArtikal.Value,
                Kolicina = kolicina,
                NovaProdajnaCena = cena,
                Iznos = cena * kolicina,
                IDObjekat = I(row, "idobjekat", "storeid"),
                RedniBroj = I(row, "rednibr", "rbr"),
                Komentar = S(row, "razlog", "reason", "napomena"),
                DataOrigin = "access"
            });
            result.PovratnicaInserted++;
        }
        _trendDb.SaveChanges();
    }

    private void ImportPrenosRobe(OdbcConnection conn, string? table, bool overwriteExisting, AccessImportRunResponse result)
    {
        // Each transfer row → TWO DnevnikPromena entries: izlaz from source + ulaz to destination
        if (table is null) return;
        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        foreach (var row in ReadRows(conn, table))
        {
            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue) continue;
            MarkAccepted(result, "prenos_robe");
            var kolicina = I(row, "kolicina", "qty", "quantity") ?? 1;
            var datum    = DT(row, "datum", "datumprenos", "datumtransfera", "date") ?? DateTime.UtcNow;
            var cena     = D(row, "cena", "nabavnacena", "prodajnacena") ?? 0m;
            var idIz     = I(row, "idobjekatiz", "idobjekatizlaza", "fromstore", "idobjekat");
            var idU      = I(row, "idobjekatulaz", "idobjekatdolaz", "tostore", "idobjekatodredista");
            var brDok    = S(row, "iddnevnik", "brdokumenta", "brprenos");
            // Prenos izlaz (source store)
            var idOut = AllocateNextId(usedIds, ref next);
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = idOut, TipPromene = TipPromeneConstants.PrenosIzlaz, Datum = datum,
                ArtikalId = idArtikal.Value, Kolicina = -kolicina,
                NovaProdajnaCena = cena, Iznos = cena * kolicina,
                IDObjekat = idIz, BrojRacuna = brDok, DataOrigin = "access"
            });
            // Prenos ulaz (destination store)
            var idIn = AllocateNextId(usedIds, ref next);
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = idIn, TipPromene = TipPromeneConstants.PrenosUlaz, Datum = datum,
                ArtikalId = idArtikal.Value, Kolicina = kolicina,
                NovaProdajnaCena = cena, Iznos = cena * kolicina,
                IDObjekat = idU, BrojRacuna = brDok, DataOrigin = "access"
            });
            result.PrenosRobeInserted += 2;
        }
        _trendDb.SaveChanges();
    }

    /// <summary>
    /// Returns the set of IDs already in use for DnevnikPromena by combining
    /// persisted DB rows with any entities currently in the EF change tracker
    /// (Added / Unchanged / Modified).  This prevents identity-conflict exceptions
    /// when multiple import methods write to the same table within a single
    /// DbContext lifetime before SaveChanges is called.
    /// </summary>
    private HashSet<int> GetDnevnikPromenaUsedIds()
    {
        var ids = _trendDb.DnevnikPromena.Select(x => x.Id).ToHashSet();
        foreach (var entry in _trendDb.ChangeTracker.Entries<DnevnikPromena>())
            ids.Add(entry.Entity.Id);
        return ids;
    }

    private static int AllocateNextId(HashSet<int> usedIds, ref int nextGeneratedId)
    {
        if (nextGeneratedId <= 0)
            nextGeneratedId = 1;

        while (usedIds.Contains(nextGeneratedId))
            nextGeneratedId++;

        var assignedId = nextGeneratedId;
        usedIds.Add(assignedId);
        nextGeneratedId++;
        return assignedId;
    }

    private static int? ResolveSupplierIdByName(string? supplierName, Dictionary<string, int> supplierByKey)
    {
        if (string.IsNullOrWhiteSpace(supplierName))
            return null;

        var key = NormalizeLookup(supplierName);
        return supplierByKey.TryGetValue(key, out var id) ? id : null;
    }

    private static string? ExtractSupplierNameFromComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return null;

        var text = comment.Trim();
        if (string.IsNullOrEmpty(text))
            return null;

        var markers = new[]
        {
            "nabavka iz ",
            "nabavka kod ",
            "povracaj robe u ",
            "povraćaj robe u ",
            "povracaj u ",
            "povraćaj u ",
            "isporuka od "
        };

        foreach (var marker in markers)
        {
            var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var candidate = text[(idx + marker.Length)..].Trim(' ', '.', ',', ';', '/', '\\', '-');
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }

    private void ImportPovracaj(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = _trendDb.PovracajZaglavlja.ToDictionary(x => x.Id);
        var usedIds = existing.Keys.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        var existingByBroj = BuildDuplicateTolerantLookup(
            _trendDb.PovracajZaglavlja
                .AsNoTracking()
                .Where(x => x.BrojZapisnika != null)
                .OrderBy(x => x.Id)
                .ToList(),
            x => x.BrojZapisnika,
            out var duplicateBrojevi);
        AddDuplicateKeyWarning(
            result.Warnings,
            duplicateBrojevi,
            "Postoje duplikati broja zapisnika u postojecoj tabeli povracaja. Import ce koristiti prvi pronadjeni zapis za svaki broj zapisnika");
        int seq = 0;

        foreach (var row in ReadRows(conn, table))
        {
            var id = I(row, "id", "idpovracaj", "returnid");
            var idDobavljac = I(row, "iddobavljac", "dobavljacid", "supplierid") ?? 0;
            var datum = DT(row, "datumazapisnika", "datumpovracaja", "datum", "date") ?? DateTime.UtcNow;
            var broj = S(row, "brozapisnika", "bројзаписника", "broj", "recordnumber", "returnno")
                       ?? $"ZP-{datum:yyyyMMdd}-{++seq:D4}";

            PovracajZaglavlje? e = null;
            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && existing.TryGetValue(sourceId, out var foundById))
                e = foundById;
            else if (existingByBroj.TryGetValue(broj, out var foundByBroj))
                e = foundByBroj;

            if (e is null)
            {
                var assignedId = sourceId;
                if (assignedId <= 0 || usedIds.Contains(assignedId))
                    assignedId = AllocateNextId(usedIds, ref nextGeneratedId);
                else
                    usedIds.Add(assignedId);

                e = new PovracajZaglavlje
                {
                    Id = assignedId,
                    BrojZapisnika = broj,
                    IDDobavljac = idDobavljac,
                    DatumPovracaja = datum,
                    DatumKreiranja = datum,
                    Status = "Kreiran",
                    DataOrigin = "access"
                };
                _trendDb.PovracajZaglavlja.Add(e);
                existing[e.Id] = e;
                existingByBroj[broj] = e;
                result.PovracajInserted++;
            }
            else if (overwriteExisting)
            {
                result.PovracajUpdated++;
            }
            else
            {
                continue;
            }
            MarkAccepted(result, "povracaj_zaglavlje");

            e.BrojZapisnika = broj;
            e.IDDobavljac = idDobavljac;
            e.DatumPovracaja = datum;
            e.RazlogPovracaja = S(row, "razlog", "reason", "razlogpovracaja");
            e.Status = S(row, "status") ?? "Kreiran";
            e.UkupanIznos = D(row, "ukupaniznos", "total", "iznos") ?? 0m;
            e.Komentar = S(row, "komentar", "comment", "napomena");
            e.KreatorKorisnik = S(row, "korisnik", "kreirao", "user", "username", "operater");
            e.DataOrigin = "access";
        }
    }

    private async Task SyncAnalyticsAsync(AccessImportRunResponse result, CancellationToken ct)
    {
        var importedProducts = await _trendDb.Artikli.AsNoTracking().Where(x => x.DataOrigin == "access").ToListAsync(ct);
        var productIds = importedProducts.Select(x => x.Id).ToArray();
        var existingDims = await _analyticsDb.ProductsDim.Where(x => productIds.Contains(x.ProductId)).ToDictionaryAsync(x => x.ProductId, ct);

        foreach (var p in importedProducts)
        {
            if (existingDims.TryGetValue(p.Id, out var dim))
            {
                dim.ProductName = p.Naziv;
                dim.Category = p.Kategorija ?? string.Empty;
                dim.SubCategory = p.Pol ?? string.Empty;
                dim.Velicina = p.Velicina;
                dim.Boja = p.Boja;
                dim.Materijal = p.Materijal;
                dim.FootwearTypeId = p.IDTipObuce;
                dim.SupplierId = p.IDDobavljac;
                dim.SeasonId = p.IDSezona;
                dim.PurchasePrice = p.NabavnaCena;
                dim.PurchasePriceRsd = p.NabavnaCenaDin;
                dim.FirstSalePrice = p.PrvaProdajnaCena;
                dim.SalePrice = p.ProdajnaCena;
                dim.Kolicina = p.Kolicina;
                dim.Timestamp = DateTime.UtcNow;
                dim.IsActive = true;
                dim.DataOrigin = "access";
                result.ProductsDimUpdated++;
            }
            else
            {
                _analyticsDb.ProductsDim.Add(new ProductsDim
                {
                    ProductId = p.Id,
                    ProductName = p.Naziv,
                    Category = p.Kategorija ?? string.Empty,
                    SubCategory = p.Pol ?? string.Empty,
                    Brand = string.Empty,
                    Velicina = p.Velicina,
                    Boja = p.Boja,
                    Materijal = p.Materijal,
                    FootwearTypeId = p.IDTipObuce,
                    SupplierId = p.IDDobavljac,
                    SeasonId = p.IDSezona,
                    PurchasePrice = p.NabavnaCena,
                    PurchasePriceRsd = p.NabavnaCenaDin,
                    FirstSalePrice = p.PrvaProdajnaCena,
                    SalePrice = p.ProdajnaCena,
                    IsActive = true,
                    Timestamp = DateTime.UtcNow,
                    Kolicina = p.Kolicina,
                    DataOrigin = "access"
                });
                result.ProductsDimInserted++;
            }
        }

        var importedSales = await _trendDb.ProdajaZaglavlja.AsNoTracking().Where(x => x.DataOrigin == "access").Include(x => x.Stavke).ToListAsync(ct);
        var saleIds = importedSales.Select(x => x.Id).ToArray();
        var existingFacts = await _analyticsDb.SalesFacts.Where(x => saleIds.Contains(x.SaleId)).ToDictionaryAsync(x => x.SaleId, ct);
        var existingStores = await _analyticsDb.StoresDim.ToDictionaryAsync(x => x.StoreId, ct);

        // Seed stores discovered by ImportObjekti (even if they have no sales yet)
        foreach (var (storeId, storeData) in _importedStores)
        {
            if (!existingStores.TryGetValue(storeId, out var e))
            {
                var newStore = new StoresDim
                {
                    StoreId   = storeId,
                    StoreName = storeData.Name    ?? $"Objekat {storeId}",
                    City      = storeData.Address ?? "N/A",
                    Region    = "N/A",
                    DataOrigin = "access"
                };
                _analyticsDb.StoresDim.Add(newStore);
                existingStores[storeId] = newStore;
                result.StoresInserted++;
            }
            else
            {
                e.StoreName = storeData.Name    ?? e.StoreName;
                e.City      = storeData.Address ?? e.City;
                e.DataOrigin = "access";
                result.StoresUpdated++;
            }
        }

        foreach (var s in importedSales)
        {
            var storeId = s.IDObjekat ?? 1;
            if (!existingStores.TryGetValue(storeId, out var existingStore))
            {
                _importedStores.TryGetValue(storeId, out var storeData);
                var newStore = new StoresDim
                {
                    StoreId   = storeId,
                    StoreName = storeData.Name    ?? $"Objekat {storeId}",
                    City      = storeData.Address ?? "N/A",
                    Region    = "N/A",
                    DataOrigin = "access"
                };
                _analyticsDb.StoresDim.Add(newStore);
                existingStores[storeId] = newStore;
                result.StoresInserted++;
            }
            else
            {
                existingStore.DataOrigin = "access";
            }

            var total = s.Stavke.Sum(x => x.Kolicina * x.Cena);
            var units = s.Stavke.Sum(x => x.Kolicina);
            if (existingFacts.TryGetValue(s.Id, out var fact))
            {
                fact.BrojRacuna = s.BrojRacuna ?? string.Empty;
                fact.SaleTimestampUtc = DateTime.SpecifyKind(s.DatumProdaje, DateTimeKind.Utc);
                fact.StoreId = storeId;
                fact.PaymentType = s.NacinPlacanja ?? string.Empty;
                fact.TotalAmount = total;
                fact.TotalUnits = units;
                fact.TotalLines = s.Stavke.Count;
                fact.DataOrigin = "access";
                result.SalesFactsUpdated++;
            }
            else
            {
                _analyticsDb.SalesFacts.Add(new SalesFact
                {
                    SaleId = s.Id,
                    BrojRacuna = s.BrojRacuna ?? string.Empty,
                    SaleTimestampUtc = DateTime.SpecifyKind(s.DatumProdaje, DateTimeKind.Utc),
                    StoreId = storeId,
                    PaymentType = s.NacinPlacanja ?? string.Empty,
                    TotalAmount = total,
                    TotalUnits = units,
                    TotalLines = s.Stavke.Count,
                    DataOrigin = "access"
                });
                result.SalesFactsInserted++;
            }
        }

        var oldLines = await _analyticsDb.SalesLineFacts.Where(x => saleIds.Contains(x.SaleId)).ToListAsync(ct);
        _analyticsDb.SalesLineFacts.RemoveRange(oldLines);

        var newLines = importedSales
            .SelectMany(s => s.Stavke.Select(l => new SalesLineFact
            {
                SaleId = s.Id,
                ProductId = l.IdArtikal,
                Qty = l.Kolicina,
                UnitPrice = l.Cena,
                LineTotal = l.Kolicina * l.Cena,
                NabavnaCena = l.NabavnaCena,
                DataOrigin = "access"
            }))
            .ToList();

        if (newLines.Count > 0)
        {
            await _analyticsDb.SalesLineFacts.AddRangeAsync(newLines, ct);
            result.SalesLineFactsInserted = newLines.Count;
        }

        // ── Suppliers ──────────────────────────────────────────────────────────────
        var importedSuppliers = await _trendDb.Dobavljaci.AsNoTracking().Where(x => x.DataOrigin == "access").ToListAsync(ct);

        // ── Seasons ────────────────────────────────────────────────────────────────
        var importedSeasons = await _trendDb.Sezone.AsNoTracking().Where(x => x.DataOrigin == "access").ToListAsync(ct);

        // ── Footwear types ─────────────────────────────────────────────────────────
        var importedTypes = await _trendDb.TipoviObuce.AsNoTracking().Where(x => x.DataOrigin == "access").ToListAsync(ct);

        // ── Inventory Movements ────────────────────────────────────────────────────
        var importedMovements = await _trendDb.DnevnikPromena.AsNoTracking().Where(x => x.DataOrigin == "access").ToListAsync(ct);
        await _analyticsDb.SaveChangesAsync(ct);
        await UpsertSuppliersDimAsync(importedSuppliers, ct);
        await UpsertSeasonsDimAsync(importedSeasons, ct);
        await UpsertFootwearTypesDimAsync(importedTypes, ct);
        await UpsertInventoryMovementsAsync(importedMovements, ct);
    }

    private async Task UpsertSuppliersDimAsync(IEnumerable<Dobavljac> suppliers, CancellationToken ct)
    {
        var processedSupplierIds = new HashSet<int>();

        foreach (var supplier in suppliers.OrderBy(x => x.Id))
        {
            if (!processedSupplierIds.Add(supplier.Id))
                continue;

            await _analyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "SuppliersDim" ("SupplierId", "Naziv", "Adresa", "Telefon", "Napomena", "DataOrigin", "UpdatedAt")
                VALUES ({supplier.Id}, {supplier.Naziv ?? string.Empty}, {supplier.Adresa}, {supplier.Telefon}, {supplier.Napomena}, {supplier.DataOrigin}, {DateTime.UtcNow})
                ON CONFLICT ("SupplierId") DO UPDATE
                SET "Naziv" = EXCLUDED."Naziv",
                    "Adresa" = EXCLUDED."Adresa",
                    "Telefon" = EXCLUDED."Telefon",
                    "Napomena" = EXCLUDED."Napomena",
                    "DataOrigin" = EXCLUDED."DataOrigin",
                    "UpdatedAt" = EXCLUDED."UpdatedAt";
                """, ct);
        }
    }

    private async Task UpsertSeasonsDimAsync(IEnumerable<Sezona> seasons, CancellationToken ct)
    {
        var processedSeasonIds = new HashSet<int>();

        foreach (var season in seasons.OrderBy(x => x.Id))
        {
            if (!processedSeasonIds.Add(season.Id))
                continue;

            var datumOd = DateTime.SpecifyKind(season.DatumOd, DateTimeKind.Utc);
            var datumDo = DateTime.SpecifyKind(season.DatumDo, DateTimeKind.Utc);

            await _analyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "SeasonsDim" ("SeasonId", "Naziv", "DatumOd", "DatumDo", "DataOrigin", "UpdatedAt")
                VALUES ({season.Id}, {season.Naziv}, {datumOd}, {datumDo}, {season.DataOrigin}, {DateTime.UtcNow})
                ON CONFLICT ("SeasonId") DO UPDATE
                SET "Naziv" = EXCLUDED."Naziv",
                    "DatumOd" = EXCLUDED."DatumOd",
                    "DatumDo" = EXCLUDED."DatumDo",
                    "DataOrigin" = EXCLUDED."DataOrigin",
                    "UpdatedAt" = EXCLUDED."UpdatedAt";
                """, ct);
        }
    }

    private async Task UpsertFootwearTypesDimAsync(IEnumerable<TipObuce> types, CancellationToken ct)
    {
        var processedTypeIds = new HashSet<int>();

        foreach (var type in types.OrderBy(x => x.Id))
        {
            if (!processedTypeIds.Add(type.Id))
                continue;

            await _analyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "FootwearTypesDim" ("TypeId", "Naziv", "DataOrigin", "UpdatedAt")
                VALUES ({type.Id}, {type.Naziv}, {type.DataOrigin}, {DateTime.UtcNow})
                ON CONFLICT ("TypeId") DO UPDATE
                SET "Naziv" = EXCLUDED."Naziv",
                    "DataOrigin" = EXCLUDED."DataOrigin",
                    "UpdatedAt" = EXCLUDED."UpdatedAt";
                """, ct);
        }
    }

    private async Task UpsertInventoryMovementsAsync(IEnumerable<DnevnikPromena> movements, CancellationToken ct)
    {
        var processedMovementIds = new HashSet<int>();

        foreach (var movement in movements.OrderBy(x => x.Id))
        {
            if (!processedMovementIds.Add(movement.Id))
                continue;

            var datum = DateTime.SpecifyKind(movement.Datum, DateTimeKind.Utc);

            await _analyticsDb.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "InventoryMovementFacts" ("SourceId", "TipPromene", "Datum", "ArtikalId", "Kolicina", "StaraProdajnaCena", "NovaProdajnaCena", "Iznos", "StoreId", "DobavljacId", "BrojDokumenta", "KorisnikIme", "DataOrigin")
                VALUES ({movement.Id}, {movement.TipPromene}, {datum}, {movement.ArtikalId}, {movement.Kolicina}, {movement.StaraProdajnaCena}, {movement.NovaProdajnaCena}, {movement.Iznos}, {movement.IDObjekat}, {movement.DobavljacId}, {movement.BrojRacuna}, {movement.KorisnikIme}, {"access"})
                ON CONFLICT ("SourceId", "DataOrigin") DO UPDATE
                SET "TipPromene" = EXCLUDED."TipPromene",
                    "Datum" = EXCLUDED."Datum",
                    "ArtikalId" = EXCLUDED."ArtikalId",
                    "Kolicina" = EXCLUDED."Kolicina",
                    "StaraProdajnaCena" = EXCLUDED."StaraProdajnaCena",
                    "NovaProdajnaCena" = EXCLUDED."NovaProdajnaCena",
                    "Iznos" = EXCLUDED."Iznos",
                    "StoreId" = EXCLUDED."StoreId",
                    "DobavljacId" = EXCLUDED."DobavljacId",
                    "BrojDokumenta" = EXCLUDED."BrojDokumenta",
                    "KorisnikIme" = EXCLUDED."KorisnikIme",
                    "DataOrigin" = EXCLUDED."DataOrigin";
                """, ct);
        }
    }

    private async Task ResetTrendplusSequencesAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT setval(pg_get_serial_sequence('"Artikli"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Artikli"), 1), true);
            SELECT setval(pg_get_serial_sequence('"Dobavljaci"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Dobavljaci"), 1), true);
            SELECT setval(pg_get_serial_sequence('"Sezone"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Sezone"), 1), true);
            SELECT setval(pg_get_serial_sequence('"TipoviObuce"', 'Id'), COALESCE((SELECT MAX("Id") FROM "TipoviObuce"), 1), true);
            SELECT setval(pg_get_serial_sequence('prodaja_zaglavlje', 'id'), COALESCE((SELECT MAX(id) FROM prodaja_zaglavlje), 1), true);
            SELECT setval(pg_get_serial_sequence('prodaja_stavke', 'id'), COALESCE((SELECT MAX(id) FROM prodaja_stavke), 1), true);
            SELECT setval(pg_get_serial_sequence('"DnevnikPromena"', 'Id'), COALESCE((SELECT MAX("Id") FROM "DnevnikPromena"), 1), true);
            SELECT setval(pg_get_serial_sequence('povracaj_zaglavlje', 'id'), COALESCE((SELECT MAX(id) FROM povracaj_zaglavlje), 1), true);
            SELECT setval(pg_get_serial_sequence('povracaj_stavke', 'id'), COALESCE((SELECT MAX(id) FROM povracaj_stavke), 1), true);
            """;
        await _trendDb.Database.ExecuteSqlRawAsync(sql, ct);
    }

    /// <summary>
    /// Creates a cross-platform ODBC connection to an Access database.
    /// Windows  : uses the built-in "Microsoft Access Driver (*.mdb, *.accdb)" ODBC driver (no ACE/Office needed).
    /// Linux/Mac: uses the open-source MDBTools ODBC driver — add to Dockerfile:
    ///   RUN apt-get update &amp;&amp; apt-get install -y mdbtools odbc-mdbtools
    /// </summary>
    private static OdbcConnection CreateOdbcConnection(string accessFilePath)
    {
        string cs = OperatingSystem.IsWindows()
            ? $"Driver={{Microsoft Access Driver (*.mdb, *.accdb)}};Dbq={accessFilePath};ReadOnly=1;"
            : $"Driver=MDBTools;Database={accessFilePath};";   // mdbtools ODBC on Linux/macOS
        return new OdbcConnection(cs);
    }

    private static List<string> GetUserTables(OdbcConnection conn, bool includeTemporaryTables = false)
    {
        var schema = conn.GetSchema("Tables");
        return schema.Rows.Cast<DataRow>()
            .Where(r => string.Equals(Convert.ToString(r["TABLE_TYPE"], CultureInfo.InvariantCulture), "TABLE", StringComparison.OrdinalIgnoreCase))
            .Select(r => Convert.ToString(r["TABLE_NAME"], CultureInfo.InvariantCulture) ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x)
             && !x.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)
             && (includeTemporaryTables || !Normalize(x).Contains("privremena", StringComparison.Ordinal)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int RowCount(OdbcConnection conn, string table)
    {
        try
        {
            using var cmd = new OdbcCommand($"SELECT COUNT(*) FROM {QuoteAccessIdentifier(table)}", conn);
            return ConvertToInt(cmd.ExecuteScalar()) ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static IEnumerable<Dictionary<string, object?>> ReadRows(OdbcConnection conn, string table)
    {
        using var cmd = new OdbcCommand($"SELECT * FROM {QuoteAccessIdentifier(table)}", conn);
        using var r = cmd.ExecuteReader();
        if (r is null) yield break;

        var names = Enumerable.Range(0, r.FieldCount).Select(i => (idx: i, name: Normalize(r.GetName(i)))).ToList();
        while (r.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (idx, name) in names)
                row[name] = r.IsDBNull(idx) ? null : r.GetValue(idx);
            yield return row;
        }
    }

    private static string? FindTable(OdbcConnection conn, IReadOnlyList<string> tables, string[] candidates, string[]? sigRequired = null, string[]? sigBonus = null)
        => FindTableDetailed(conn, tables, candidates, sigRequired, sigBonus).TableName;

    private static TableMatch FindTableDetailed(
        OdbcConnection conn,
        IReadOnlyList<string> tables,
        string[] candidates,
        string[]? sigRequired = null,
        string[]? sigBonus = null)
    {
        var normalized = tables.Select(t => new { Original = t, Key = Normalize(t) }).ToList();

        // 1. Exact name match (normalized)
        foreach (var candidate in candidates)
        {
            var key = Normalize(candidate);
            var exact = normalized.FirstOrDefault(x => x.Key == key);
            if (exact is not null)
                return new TableMatch(exact.Original, "exact");
        }

        // 2. Contains match (normalized)
        foreach (var candidate in candidates)
        {
            var key = Normalize(candidate);
            var contains = normalized.FirstOrDefault(x => x.Key.Contains(key, StringComparison.Ordinal));
            if (contains is not null)
                return new TableMatch(contains.Original, "contains");
        }

        // 3. Column-signature fallback (required + best bonus score)
        if (sigRequired?.Length > 0)
        {
            string? bestTable = null;
            var bestScore = -1;
            var requiredKeys = sigRequired.Select(Normalize).ToArray();
            var bonusKeys = sigBonus?.Select(Normalize).ToArray();

            foreach (var table in tables)
            {
                var cols = ReadColumnNamesNormalized(conn, table);
                if (!requiredKeys.All(cols.Contains))
                    continue;

                var score = bonusKeys?.Count(cols.Contains) ?? 0;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTable = table;
                }
            }

            if (bestTable is not null)
                return new TableMatch(bestTable, "signature");
        }

        return new TableMatch(null, "none");
    }

    private static HashSet<string> ReadColumnNamesNormalized(OdbcConnection conn, string table)
    {
        var cols = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            using var cmd = new OdbcCommand($"SELECT * FROM {QuoteAccessIdentifier(table)} WHERE 1=0", conn);
            using var r = cmd.ExecuteReader();
            if (r is null) return cols;
            for (var i = 0; i < r.FieldCount; i++)
                cols.Add(Normalize(r.GetName(i)));
        }
        catch { /* table unreadable – skip */ }
        return cols;
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var normalized = s.Normalize(NormalizationForm.FormD);
        Span<char> buffer = stackalloc char[normalized.Length];
        var j = 0;
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                buffer[j++] = char.ToLowerInvariant(c);
        }

        return j == 0 ? string.Empty : new string(buffer[..j]);
    }

    private static string NormalizeLookup(string? s)
    {
        return Normalize(s);
    }

    private static string QuoteAccessIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return "[]";
        return $"[{identifier.Replace("]", "]]")}]";
    }

    private sealed record AccessFileSnapshot(string FilePath, bool IsSnapshot, string? Warning);

    private static AccessFileSnapshot CreateSnapshotIfLocked(string accessFilePath)
    {
        var lockFilePath = TryGetAccessLockFilePath(accessFilePath);
        if (lockFilePath is null)
            return new AccessFileSnapshot(accessFilePath, false, null);

        try
        {
            var tmpDir = Path.Combine(Path.GetTempPath(), "trendplus_access_snapshots");
            Directory.CreateDirectory(tmpDir);

            var ext = Path.GetExtension(accessFilePath);
            var baseName = Path.GetFileNameWithoutExtension(accessFilePath);
            var tmpName = $"{baseName}_snapshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
            var tmpPath = Path.Combine(tmpDir, tmpName);

            File.Copy(accessFilePath, tmpPath, overwrite: true);

            return new AccessFileSnapshot(
                FilePath: tmpPath,
                IsSnapshot: true,
                Warning: $"Access baza deluje otvorena (pronađen lock fajl '{Path.GetFileName(lockFilePath)}'). Koristi se snapshot kopija '{tmpName}'.");
        }
        catch (Exception ex)
        {
            return new AccessFileSnapshot(
                FilePath: accessFilePath,
                IsSnapshot: false,
                Warning: $"Access baza deluje otvorena (pronađen lock fajl '{Path.GetFileName(lockFilePath)}'). Snapshot kopija nije uspela ({ex.GetType().Name}). Preporuka: zatvori Access pre importa.");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }

    private static void TryAddSampleDataWarnings(
        OdbcConnection conn,
        AccessImportTablePreview tablePreview,
        IReadOnlyCollection<string> requiredTargets,
        List<string> warnings)
    {
        if (tablePreview is null || !tablePreview.Found || string.IsNullOrWhiteSpace(tablePreview.TableName))
            return;

        try
        {
            const int sampleTake = 1000;

            var mappings = requiredTargets
                .Select(t =>
                {
                    var source = tablePreview.FieldMappings
                        .FirstOrDefault(m => m.TargetField.Equals(t, StringComparison.OrdinalIgnoreCase))
                        ?.SourceColumn;
                    return (Target: t, Source: source);
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Source))
                .ToList();

            if (mappings.Count == 0)
                return;

            var selectCols = string.Join(", ", mappings.Select(m => QuoteAccessIdentifier(m.Source!)));
            var sql = $"SELECT TOP {sampleTake} {selectCols} FROM {QuoteAccessIdentifier(tablePreview.TableName)}";

            using var cmd = new OdbcCommand(sql, conn);
            using var r = cmd.ExecuteReader();
            if (r is null)
                return;

            var nullCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var nonPositiveCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var dupCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var idSets = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var rows = 0;

            while (r.Read())
            {
                rows++;
                for (var i = 0; i < mappings.Count; i++)
                {
                    var target = mappings[i].Target;
                    var value = r.IsDBNull(i) ? null : r.GetValue(i);

                    if (value is null || value is DBNull)
                    {
                        nullCount[target] = nullCount.TryGetValue(target, out var n) ? n + 1 : 1;
                        continue;
                    }

                    // Quick numeric sanity checks for a few known targets.
                    if (target.Equals("Kolicina", StringComparison.OrdinalIgnoreCase) ||
                        target.Equals("Cena", StringComparison.OrdinalIgnoreCase) ||
                        target.Equals("NabavnaCena", StringComparison.OrdinalIgnoreCase) ||
                        target.Equals("ProdajnaCena", StringComparison.OrdinalIgnoreCase))
                    {
                        var d = ConvertToDecimal(value);
                        if (!d.HasValue || d.Value <= 0)
                            nonPositiveCount[target] = nonPositiveCount.TryGetValue(target, out var np) ? np + 1 : 1;
                    }

                    // Duplicate checks only for true table identifiers (avoid noise on line tables).
                    var checkUniqId =
                        target.Equals("Id", StringComparison.OrdinalIgnoreCase)
                        && (tablePreview.Key.Equals("artikli", StringComparison.OrdinalIgnoreCase)
                            || tablePreview.Key.Equals("prodaja_zaglavlje", StringComparison.OrdinalIgnoreCase)
                            || tablePreview.Key.Equals("dnevnik_promena", StringComparison.OrdinalIgnoreCase));

                    if (checkUniqId)
                    {
                        var s = Convert.ToString(value, CultureInfo.InvariantCulture);
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            if (!idSets.TryGetValue(target, out var set))
                            {
                                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                idSets[target] = set;
                            }

                            if (!set.Add(s))
                                dupCount[target] = dupCount.TryGetValue(target, out var dups) ? dups + 1 : 1;
                        }
                    }
                }
            }

            if (rows == 0)
                return;

            // Only warn when something looks suspicious (avoid noise).
            static void AddIf(string key, Dictionary<string, int> map, int threshold, string msgPrefix, List<string> w, string tableName, int sampleN)
            {
                if (map.TryGetValue(key, out var n) && n >= threshold)
                    w.Add($"{msgPrefix} Tabela '{tableName}': {n}/{sampleN} redova u uzorku ima problem sa poljem '{key}'.");
            }

            var tn = tablePreview.TableName!;
            AddIf("Id", nullCount, 1, "⚠", warnings, tn, rows);
            AddIf("Naziv", nullCount, 1, "⚠", warnings, tn, rows);
            AddIf("Datum", nullCount, 1, "⚠", warnings, tn, rows);
            AddIf("DatumProdaje", nullCount, 1, "⚠", warnings, tn, rows);
            AddIf("TipPromene", nullCount, 1, "⚠", warnings, tn, rows);
            AddIf("IdProdaja", nullCount, 1, "⚠", warnings, tn, rows);
            AddIf("IdArtikal", nullCount, 1, "⚠", warnings, tn, rows);
            AddIf("Kolicina", nullCount, 1, "⚠", warnings, tn, rows);
            AddIf("Cena", nullCount, 1, "⚠", warnings, tn, rows);

            foreach (var (k, v) in dupCount.Where(x => x.Value > 0))
            {
                warnings.Add($"⚠ Tabela '{tn}': duplikati u uzorku za '{k}' = {v} (od {rows} redova).");
            }

            foreach (var (k, v) in nonPositiveCount.Where(x => x.Value > 0))
            {
                warnings.Add($"⚠ Tabela '{tn}': {v}/{rows} redova u uzorku ima '{k}' <= 0 ili nije broj.");
            }
        }
        catch
        {
            // table might be unreadable; keep preview resilient
        }
    }

    private static string? TryGetAccessLockFilePath(string accessFilePath)
    {
        if (string.IsNullOrWhiteSpace(accessFilePath))
            return null;

        var dir = Path.GetDirectoryName(accessFilePath);
        var name = Path.GetFileNameWithoutExtension(accessFilePath);
        var ext = Path.GetExtension(accessFilePath);

        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ext))
            return null;

        var lockExt = ext.Equals(".accdb", StringComparison.OrdinalIgnoreCase)
            ? ".laccdb"
            : ext.Equals(".mdb", StringComparison.OrdinalIgnoreCase)
                ? ".ldb"
                : null;

        if (lockExt is null)
            return null;

        var lockPath = Path.Combine(dir, name + lockExt);
        return File.Exists(lockPath) ? lockPath : null;
    }

    private static Dictionary<string, T> BuildDuplicateTolerantLookup<T>(
        IEnumerable<T> source,
        Func<T, string?> keySelector,
        out IReadOnlyList<string> duplicateKeys)
    {
        var map = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in source)
        {
            var key = keySelector(item);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!map.TryAdd(key, item))
                duplicates.Add(key);
        }

        duplicateKeys = duplicates.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        return map;
    }

    private static void AddDuplicateKeyWarning(
        ICollection<string> warnings,
        IReadOnlyList<string> duplicateKeys,
        string messagePrefix)
    {
        if (duplicateKeys.Count == 0)
            return;

        var preview = string.Join(", ", duplicateKeys.Take(5));
        var remaining = duplicateKeys.Count - Math.Min(5, duplicateKeys.Count);
        var suffix = remaining > 0 ? $" (+{remaining} dodatnih)" : string.Empty;
        warnings.Add($"{messagePrefix}: {preview}{suffix}.");
    }

    private static AccessImportCoverageMetric GetCoverageMetric(AccessImportRunResponse result, string key)
    {
        if (!result.CoverageByTable.TryGetValue(key, out var metric))
        {
            metric = new AccessImportCoverageMetric();
            result.CoverageByTable[key] = metric;
        }

        return metric;
    }

    private static void MarkAccepted(AccessImportRunResponse result, string key, int count = 1)
        => GetCoverageMetric(result, key).AcceptedRows += count;

    private static void FinalizeCoverageMetrics(
        AccessImportRunResponse result,
        IReadOnlyDictionary<string, int> sourceRowsByTable,
        IReadOnlyDictionary<string, int> targetWritesByTable,
        bool groupedProdajaHeaders,
        bool expandedPrenosRobe)
    {
        var keys = sourceRowsByTable.Keys
            .Concat(targetWritesByTable.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            var metric = GetCoverageMetric(result, key);
            metric.SourceRows = sourceRowsByTable.TryGetValue(key, out var sourceRows) ? sourceRows : 0;
            metric.TargetWrites = targetWritesByTable.TryGetValue(key, out var targetWrites) ? targetWrites : 0;
            metric.SkippedRows = Math.Max(0, metric.SourceRows - metric.AcceptedRows);
            metric.CoveragePercent = metric.SourceRows <= 0
                ? 100d
                : Math.Round(metric.AcceptedRows * 100d / metric.SourceRows, 2);

            metric.TransformationType = "direct";
            metric.MergedRows = 0;
            metric.ExpandedTargetRows = 0;

            if (expandedPrenosRobe && key.Equals("prenos_robe", StringComparison.OrdinalIgnoreCase))
            {
                metric.TransformationType = "expanded";
                metric.ExpandedTargetRows = Math.Max(0, metric.TargetWrites - metric.AcceptedRows);
                continue;
            }

            if (groupedProdajaHeaders && key.Equals("prodaja_zaglavlje", StringComparison.OrdinalIgnoreCase))
            {
                metric.TransformationType = "grouped";
                metric.MergedRows = Math.Max(0, metric.AcceptedRows - metric.TargetWrites);
            }
        }
    }

    private static object? Get(Dictionary<string, object?> row, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (row.TryGetValue(Normalize(alias), out var value))
                return value;
        }
        return null;
    }

    private static string? S(Dictionary<string, object?> row, params string[] aliases)
    {
        var v = Get(row, aliases);
        var s = v is null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static int? I(Dictionary<string, object?> row, params string[] aliases) => ConvertToInt(Get(row, aliases));
    private static decimal? D(Dictionary<string, object?> row, params string[] aliases) => ConvertToDecimal(Get(row, aliases));
    private static DateTime? DT(Dictionary<string, object?> row, params string[] aliases) => ConvertToDate(Get(row, aliases));

    private static int? ConvertToInt(object? v)
    {
        if (v is null) return null;
        if (v is int i) return i;
        if (v is long l && l is >= int.MinValue and <= int.MaxValue) return (int)l;
        if (v is decimal d && d is >= int.MinValue and <= int.MaxValue) return (int)d;
        var s = Convert.ToString(v, CultureInfo.InvariantCulture);
        return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static decimal? ConvertToDecimal(object? v)
    {
        if (v is null) return null;
        if (v is decimal d) return d;
        if (v is double dd) return Convert.ToDecimal(dd, CultureInfo.InvariantCulture);
        var s = Convert.ToString(v, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static DateTime? ConvertToDate(object? v)
    {
        if (v is null) return null;
        if (v is DateTime dt) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        var s = Convert.ToString(v, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        return DateTime.TryParse(s, CultureInfo.GetCultureInfo("sr-Latn-RS"), DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;
    }

    private static void EnsurePlatformSupport()
    {
        // No platform restriction — ODBC works on Windows, Linux, macOS, and Docker.
        // Windows:  Microsoft Access Driver (*.mdb, *.accdb) — ships with Windows by default.
        // Linux:    Install mdbtools + odbc-mdbtools (apt-get install -y mdbtools odbc-mdbtools).
    }
}
