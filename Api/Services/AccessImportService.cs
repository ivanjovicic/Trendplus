using System.Data;
using System.Data.Odbc;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Api.Models;
using Domain.Model;
using Domain.Model.Povracaj;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public interface IAccessImportService
{
    Task<AccessImportPreviewResponse> PreviewAsync(string accessFilePath, CancellationToken ct = default);
    Task<AccessImportRunResponse> ImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, CancellationToken ct = default);
    Task<List<AccessImportBatchDto>> GetRecentBatchesAsync(int take = 20, CancellationToken ct = default);
    Task<DeleteBatchResult> DeleteBatchAsync(long batchId, CancellationToken ct = default);
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

    private static readonly string[] TipoviCandidates = ["tipoviobuce", "tipobuce", "tipovi_obuce", "footweartypes"];
    private static readonly string[] DobavljaciCandidates = ["dobavljaci", "dobavljac", "suppliers"];
    private static readonly string[] SezoneCandidates = ["sezone", "sezona", "seasons"];
    private static readonly string[] ArtikliCandidates = ["artikli", "artikal", "proizvodi", "products"];
    private static readonly string[] ProdajaCandidates = ["prodaja_zaglavlje", "prodajazaglavlje", "prodaja", "racuni", "salesheader"];
    private static readonly string[] ProdajaStavkeCandidates = ["prodaja_stavke", "prodajastavke", "stavkeprodaje", "salelines"];
    private static readonly string[] DnevnikPromenaCandidates = ["dnevnikpromjena", "dnevnikpromena", "dnevnik_promjena", "dnevnik_promena", "dnevnik", "log", "promena", "promjena", "events", "journal"];
    private static readonly string[] PovracajCandidates = ["povracaj_zaglavlje", "povracajzaglavlje", "povracaj", "returns", "returnheader", "vracanje"];
    private static readonly string[] PovracajStavkeCandidates2 = ["povracaj_stavke", "povracajstavke", "stavkepovracaja", "returnlines", "returnstems"];
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<FieldAlias>> PreviewFieldMappings =
        new Dictionary<string, IReadOnlyList<FieldAlias>>(StringComparer.OrdinalIgnoreCase)
        {
            ["artikli"] =
            [
                new FieldAlias("Id", "id", "idartikal", "productid"),
                new FieldAlias("Naziv", "naziv", "nazivartikal", "artikal", "name", "productname"),
                new FieldAlias("PLU", "plu", "sku", "sifra", "barcode", "barkod"),
                new FieldAlias("IDTipObuce", "idtipobuce", "tipobuceid"),
                new FieldAlias("IDDobavljac", "iddobavljac", "dobavljacid"),
                new FieldAlias("NabavnaCena", "nabavnacena", "purchaseprice", "cost"),
                new FieldAlias("ProdajnaCena", "prodajnacena", "saleprice", "price"),
                new FieldAlias("Kolicina", "kolicina", "qty", "quantity"),
                new FieldAlias("IDSezona", "idsezona", "seasonid")
            ],
            ["dobavljaci"] =
            [
                new FieldAlias("Id", "id", "iddobavljac", "supplierid"),
                new FieldAlias("Naziv", "naziv", "dobavljac", "supplier", "name"),
                new FieldAlias("Adresa", "adresa", "address"),
                new FieldAlias("Telefon", "telefon", "phone")
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
                new FieldAlias("TipPromene", "tippromene", "vrstapromene", "tip", "type"),
                new FieldAlias("Datum", "datum", "datumizmene", "eventdate"),
                new FieldAlias("Iznos", "iznospromene", "iznos", "amount", "total"),
                new FieldAlias("BrojRacuna/BrojKalkulacije", "brojkalkulacije", "brojracuna", "invoice", "documentno"),
                new FieldAlias("DobavljacId", "iddobavljac", "iddobavljaca", "dobavljacid", "supplierid"),
                new FieldAlias("ArtikalId", "idartikal", "artikalid", "productid"),
                new FieldAlias("Kolicina", "kolicina", "qty", "quantity"),
                new FieldAlias("StaraProdajnaCena", "staracena", "staraprodajnacena", "oldprice"),
                new FieldAlias("NovaProdajnaCena", "novacena", "novaprodajnacena", "newprice"),
                new FieldAlias("Komentar", "komentar", "napomena", "comment", "opis"),
                new FieldAlias("KorisnikIme", "korisnik", "korisnikime", "username", "operater")
            ],
            ["povracaj_zaglavlje"] =
            [
                new FieldAlias("Id", "id", "idpovracaj", "returnid"),
                new FieldAlias("IDDobavljac", "iddobavljac", "dobavljacid", "supplierid"),
                new FieldAlias("DatumPovracaja", "datumazapisnika", "datumpovracaja", "datum", "date"),
                new FieldAlias("BrojZapisnika", "brozapisnika", "broj", "recordnumber", "returnno"),
                new FieldAlias("UkupanIznos", "ukupaniznos", "total", "iznos")
            ],
            ["povracaj_stavke"] =
            [
                new FieldAlias("IdPovracaj", "idpovracaj", "returnid", "idzaglavlje"),
                new FieldAlias("IdArtikal", "idartikal", "productid", "artiklid"),
                new FieldAlias("Kolicina", "kolicina", "qty", "quantity"),
                new FieldAlias("Cena", "cena", "unitprice", "price", "nabavnacena")
            ]
        };

    private readonly TrendplusDbContext _trendDb;
    private readonly AnalyticsDbContext _analyticsDb;
    private readonly ILogger<AccessImportService> _logger;

    public AccessImportService(TrendplusDbContext trendDb, AnalyticsDbContext analyticsDb, ILogger<AccessImportService> logger)
    {
        _trendDb = trendDb;
        _analyticsDb = analyticsDb;
        _logger = logger;
    }

    public async Task<AccessImportPreviewResponse> PreviewAsync(string accessFilePath, CancellationToken ct = default)
    {
        EnsurePlatformSupport();
        if (!File.Exists(accessFilePath))
            throw new FileNotFoundException("ACCDB fajl nije pronađen.", accessFilePath);

        return await Task.Run(() =>
        {
            using var conn = CreateOdbcConnection(accessFilePath);
            conn.Open();
            var tables = GetUserTables(conn);

            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["tipovi_obuce"] = FindTable(tables, TipoviCandidates),
                ["dobavljaci"] = FindTable(tables, DobavljaciCandidates),
                ["sezone"] = FindTable(tables, SezoneCandidates),
                ["artikli"] = FindTable(tables, ArtikliCandidates),
                ["prodaja_zaglavlje"] = FindTable(tables, ProdajaCandidates),
                ["prodaja_stavke"] = FindTable(tables, ProdajaStavkeCandidates),
                ["dnevnik_promena"] = FindTable(tables, DnevnikPromenaCandidates),
                ["povracaj_zaglavlje"] = FindTable(tables, PovracajCandidates),
                ["povracaj_stavke"] = FindTable(tables, PovracajStavkeCandidates2),
            };

            var response = new AccessImportPreviewResponse
            {
                SourceFileName = Path.GetFileName(accessFilePath),
                CanImport = map["artikli"] is not null,
                AvailableTables = tables.OrderBy(x => x).ToList(),
                Tables = new List<AccessImportTablePreview>()
            };

            foreach (var entry in map)
            {
                var tablePreview = BuildTablePreview(conn, entry.Key, entry.Value);
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

            if (map["prodaja_zaglavlje"] is null && map["dnevnik_promena"] is not null)
                response.Warnings.Add("Nije pronađena tabela prodaje — prodaja će biti sintetizovana iz DnevnikPromena (tip='Prodaja').");

            if (map["prodaja_stavke"] is null && map["prodaja_zaglavlje"] is not null)
                response.Warnings.Add("Nije pronađena tabela stavki prodaje — zaglavlja bez stavki biće uvezena bez linija.");

            return response;
        }, ct);
    }

    private static AccessImportTablePreview BuildTablePreview(OdbcConnection conn, string key, string? tableName)
    {
        var preview = new AccessImportTablePreview
        {
            Key = key,
            TableName = tableName,
            RowCount = tableName is null ? 0 : RowCount(conn, tableName)
        };

        if (string.IsNullOrWhiteSpace(tableName))
            return preview;

        preview.AccessColumns = ReadColumnNames(conn, tableName);
        preview.FieldMappings = BuildFieldMappingsPreview(key, preview.AccessColumns);
        return preview;
    }

    private static List<string> ReadColumnNames(OdbcConnection conn, string table)
    {
        var columns = new List<string>();
        try
        {
            using var cmd = new OdbcCommand($"SELECT * FROM `{table}` WHERE 1=0", conn);
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

        try
        {
            await Task.Run(() => ImportTrendplus(accessFilePath, overwriteExisting, result), ct);
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
    }

    public async Task<List<AccessImportBatchDto>> GetRecentBatchesAsync(int take = 20, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
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
                ErrorMessage = x.ErrorMessage
            })
            .ToListAsync(ct);
    }

    public async Task<DeleteBatchResult> DeleteBatchAsync(long batchId, CancellationToken ct = default)
    {
        var batch = await _trendDb.DataImportBatches.FindAsync([batchId], ct);
        if (batch is null)
            return new DeleteBatchResult { Found = false };

        // Delete analytics data imported by this batch (DataOrigin="access").
        // Because individual records don't carry a per-batch FK we clean all
        // access-origin rows in one go — safe when Access is the sole external source.
        var sfDeleted  = await _analyticsDb.SalesFacts  .Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
        var slfDeleted = await _analyticsDb.SalesLineFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);
        var pdDeleted  = await _analyticsDb.ProductsDim .Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct);

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

        _trendDb.DataImportBatches.Remove(batch);
        await _trendDb.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Deleted access-import batch {BatchId}: artikli={Ar}, prodaja={Pv}/{Sv}, dnevnik={Dn}, povracaj={Pv2}/{PvS}, sezone={Se}, dobavljaci={Do}, tipovi={Ti}, analytics pd={Pd}/sf={Sf}/slf={Slf}",
            batchId, arDeleted, pvDeleted, svDeleted, dnDeleted, pvDeleted2, pvStavkeDeleted, seDeleted, doDeleted, tiDeleted, pdDeleted, sfDeleted, slfDeleted);

        return new DeleteBatchResult
        {
            Found          = true,
            BatchId        = batchId,
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
            SalesLineFactsDeleted = slfDeleted
        };
    }

    private void ImportTrendplus(string accessFilePath, bool overwriteExisting, AccessImportRunResponse result)
    {
        using var conn = CreateOdbcConnection(accessFilePath);
        conn.Open();

        var tables = GetUserTables(conn);
        var tipovi = FindTable(tables, TipoviCandidates);
        var dobavljaci = FindTable(tables, DobavljaciCandidates);
        var sezone = FindTable(tables, SezoneCandidates);
        var artikli = FindTable(tables, ArtikliCandidates);
        var prodaja = FindTable(tables, ProdajaCandidates);
        var prodajaStavke = FindTable(tables, ProdajaStavkeCandidates);
        var dnevnik = FindTable(tables, DnevnikPromenaCandidates);
        var povracaj = FindTable(tables, PovracajCandidates);
        var povracajStavke = FindTable(tables, PovracajStavkeCandidates2);

        if (artikli is null)
            throw new InvalidOperationException("Nije pronađena tabela za artikle u ACCDB fajlu.");

        if (tipovi is not null) ImportTipovi(conn, tipovi, overwriteExisting, result);
        if (dobavljaci is not null) ImportDobavljaci(conn, dobavljaci, overwriteExisting, result);
        if (sezone is not null) ImportSezone(conn, sezone, overwriteExisting, result);
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

        // If Access DB has no dedicated prodaja tables but tracks sales in DnevnikPromena,
        // synthesize ProdajaZaglavlje + ProdajaStavke from "Prodaja" type journal entries.
        if (prodaja is null && dnevnik is not null && !importedProdajaFromLineTable)
            SynthesizeProdajaFromDnevnik(overwriteExisting, result);
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

            if (!existing.TryGetValue(id.Value, out var e))
            {
                e = new Dobavljac
                {
                    Id = id.Value,
                    Naziv = naziv,
                    Adresa = S(row, "adresa", "address"),
                    Telefon = S(row, "telefon", "phone"),
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
                e.Telefon = S(row, "telefon", "phone");
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

            var datumOd = DT(row, "datumod", "od", "startdate") ?? DateTime.UtcNow.Date;
            var datumDo = DT(row, "datumdo", "do", "enddate") ?? datumOd.AddMonths(6);

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

            if (!existing.TryGetValue(id.Value, out var e))
            {
                e = new Domain.Model.Prodaja.ProdajaZaglavlje
                {
                    Id = id.Value,
                    BrojRacuna = S(row, "brojracuna", "invoice", "receiptnumber"),
                    DatumProdaje = DT(row, "datumprodaje", "datum", "saledate") ?? DateTime.UtcNow,
                    NacinPlacanja = S(row, "nacinplacanja", "paymenttype"),
                    IDObjekat = I(row, "idobjekat", "storeid"),
                    DataOrigin = "access"
                };
                _trendDb.ProdajaZaglavlja.Add(e);
                existing[e.Id] = e;
                result.ProdajaInserted++;
            }
            else if (overwriteExisting)
            {
                e.BrojRacuna = S(row, "brojracuna", "invoice", "receiptnumber");
                e.DatumProdaje = DT(row, "datumprodaje", "datum", "saledate") ?? DateTime.UtcNow;
                e.NacinPlacanja = S(row, "nacinplacanja", "paymenttype");
                e.IDObjekat = I(row, "idobjekat", "storeid");
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
            using var cmd = new OdbcCommand($"SELECT * FROM `{table}` WHERE 1=0", conn);
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
        var dnevnikById = _trendDb.DnevnikPromena.Local
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First());
        foreach (var d in _trendDb.DnevnikPromena.AsNoTracking())
        {
            if (!dnevnikById.ContainsKey(d.Id))
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
        static bool IsSaleType(string tip) =>
            tip.Contains("prodaj", StringComparison.OrdinalIgnoreCase) ||
            tip.Contains("sale",   StringComparison.OrdinalIgnoreCase)  ||
            tip.Contains("prodato",StringComparison.OrdinalIgnoreCase)  ||
            tip.Contains("promet", StringComparison.OrdinalIgnoreCase);

        var saleEntries = _trendDb.DnevnikPromena.Local
            .Where(d => d.DataOrigin == "access" && IsSaleType(d.TipPromene))
            .ToList();

        if (saleEntries.Count == 0) return;

        var existingZaglavlja = _trendDb.ProdajaZaglavlja.ToDictionary(x => x.Id);
        var existingBrojevi   = _trendDb.ProdajaZaglavlja
            .Where(x => x.BrojRacuna != null)
            .ToDictionary(x => x.BrojRacuna!, StringComparer.OrdinalIgnoreCase);

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

            e.TipPromene = tip;
            e.Datum = datum;
            e.Iznos = iznos;
            e.Kolicina = I(row, "kolicina", "kol", "qty", "quantity", "kolicinaproizvoda");
            e.BrojRacuna = S(row, "brojracuna", "brracuna", "brrach", "brojfakture", "brfakture", "dokument",
                              "brdokumenta", "brojdokumenta", "racun", "invoice", "receiptnumber", "documentno",
                              "brnaloga", "brojnaloga", "nalog", "brojkalkulacije");

            var komentar = S(row, "komentar", "comment", "napomena", "opis", "beleska", "info", "memo");
            var dobavljacId = I(row, "iddobavljac", "iddobavljaca", "dobavljacid", "supplierid", "idd", "iddob");
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
            e.DataOrigin = "access";
        }
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

    private static int? ResolveSupplierIdByName(string? supplierName, IReadOnlyDictionary<string, int> supplierByKey)
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
        var existingByBroj = _trendDb.PovracajZaglavlja.Where(x => x.BrojZapisnika != null)
            .ToDictionary(x => x.BrojZapisnika!, StringComparer.OrdinalIgnoreCase);
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

        foreach (var s in importedSales)
        {
            var storeId = s.IDObjekat ?? 1;
            if (!existingStores.ContainsKey(storeId))
            {
                _analyticsDb.StoresDim.Add(new StoresDim { StoreId = storeId, StoreName = $"Store {storeId}", City = "N/A", Region = "N/A" });
                result.StoresInserted++;
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
                DataOrigin = "access"
            }))
            .ToList();

        if (newLines.Count > 0)
        {
            await _analyticsDb.SalesLineFacts.AddRangeAsync(newLines, ct);
            result.SalesLineFactsInserted = newLines.Count;
        }

        await _analyticsDb.SaveChangesAsync(ct);
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
            ? $"Driver={{Microsoft Access Driver (*.mdb, *.accdb)}};Dbq={accessFilePath};"
            : $"Driver=MDBTools;Database={accessFilePath};";   // mdbtools ODBC on Linux/macOS
        return new OdbcConnection(cs);
    }

    private static List<string> GetUserTables(OdbcConnection conn)
    {
        // Do NOT pass type restriction — the Access ODBC driver only supports 3 restriction
        // columns (catalog/schema/name) and throws if a 4th is provided.
        // Filter TABLE_TYPE in memory instead.
        var schema = conn.GetSchema("Tables");
        return schema.Rows.Cast<DataRow>()
            .Where(r => string.Equals(Convert.ToString(r["TABLE_TYPE"]), "TABLE", StringComparison.OrdinalIgnoreCase))
            .Select(r => Convert.ToString(r["TABLE_NAME"]) ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int RowCount(OdbcConnection conn, string table)
    {
        try
        {
            using var cmd = new OdbcCommand($"SELECT COUNT(*) FROM `{table}`", conn);
            return ConvertToInt(cmd.ExecuteScalar()) ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static IEnumerable<Dictionary<string, object?>> ReadRows(OdbcConnection conn, string table)
    {
        using var cmd = new OdbcCommand($"SELECT * FROM `{table}`", conn);
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

    private static string? FindTable(IEnumerable<string> tables, IEnumerable<string> candidates)
    {
        var normalized = tables.Select(t => new { Original = t, Key = Normalize(t) }).ToList();
        foreach (var c in candidates.Select(Normalize))
        {
            var exact = normalized.FirstOrDefault(x => x.Key == c);
            if (exact is not null) return exact.Original;
        }

        foreach (var c in candidates.Select(Normalize))
        {
            var contains = normalized.FirstOrDefault(x => x.Key.Contains(c, StringComparison.Ordinal));
            if (contains is not null) return contains.Original;
        }

        return null;
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        Span<char> buffer = stackalloc char[s.Length];
        var j = 0;
        foreach (var c in s)
            if (char.IsLetterOrDigit(c))
                buffer[j++] = char.ToLowerInvariant(c);
        return new string(buffer[..j]);
    }

    private static string NormalizeLookup(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        var normalized = s.Normalize(NormalizationForm.FormD);
        var chars = new List<char>(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                chars.Add(char.ToLowerInvariant(c));
        }
        return chars.Count == 0 ? string.Empty : new string(chars.ToArray());
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
