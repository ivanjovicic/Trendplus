using System.Data;
using System.Data.Odbc;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using Api.Config;
using Api.Models;
using Api.Services.Access;
using Domain.Model;
using Domain.Model.Povracaj;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
    public interface IAccessImportService
    {
        Task<AccessImportPreviewResponse> PreviewAsync(string accessFilePath, bool includeTemporaryTables = false, CancellationToken ct = default);
        Task<AccessImportRunResponse> ImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default);
        Task<AccessImportRunResponse> StartImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default);
        Task<AccessImportRunResponse> RunExistingBatchAsync(long batchId, string accessFilePath, string sourceFileName, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, bool deleteWorkingFileAfterCompletion = false, CancellationToken ct = default);
        Task RefreshBatchStatusesAsync(long? batchId = null, CancellationToken ct = default);
        Task<List<AccessImportBatchDto>> GetRecentBatchesAsync(int take = 20, CancellationToken ct = default);
        Task<AccessImportBatchDto?> GetBatchAsync(long batchId, CancellationToken ct = default);
        Task<bool> RequestCancellationAsync(long batchId, CancellationToken ct = default);
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

    // â”€â”€ Table-name candidates (exact then contains, then column-signature fallback) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static readonly string[] TipoviCandidates        = ["tipoviobuce", "tipobuce", "tipovi_obuce", "footweartypes", "tbltipobuce", "tbltipovi"];
    private static readonly string[] DobavljaciCandidates    = ["dobavljaci", "dobavljac", "suppliers", "tbldobavljaci", "tbldobavljac"];
    private static readonly string[] SezoneCandidates        = ["sezone", "sezona", "seasons", "tblsezone", "tblsezona", "godisnjedoba"];
    private static readonly string[] ArtikliCandidates       = ["artikli", "artikal", "proizvodi", "products", "tblartikal", "tblarticles", "sifarnik"];
    private static readonly string[] ProdajaCandidates       = ["prodaja_zaglavlje", "prodajazaglavlje", "prodaja", "racuni", "salesheader", "tblracuni", "tblprodaja", "tbldnevnikprodaje"];
    private static readonly string[] ProdajaStavkeCandidates = ["prodaja_stavke", "prodajastavke", "stavkeprodaje", "salelines", "tblstavkeracuna", "tblstavkeprodaje"];
    private static readonly string[] DnevnikPromenaCandidates = ["dnevnikpromjena", "dnevnikpromena", "dnevnik_promjena", "dnevnik_promena", "dnevnik", "log", "promena", "promjena", "events", "journal", "tbldnevnikpromena", "tbldnevnikpromjena", "tbldnevnik"];
    private static readonly string[] PovracajCandidates      = ["povracaj_zaglavlje", "povracajzaglavlje", "povracaj", "returns", "returnheader", "vracanje", "tblpovracaj", "tblzapisnikopovracaju", "tblzapisnik"];
    private static readonly string[] PovracajStavkeCandidates2 = ["povracaj_stavke", "povracajstavke", "stavkepovracaja", "returnlines", "returnstems", "tblstavkepovracaja", "tblstavkezapisnika"];
    // â”€â”€ New movement-type candidates â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    private static readonly ConcurrentDictionary<string, string> NormalizedStringCache =
        new(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, Dictionary<string, string>> PreviewAliasToTargetByKey =
        BuildPreviewAliasToTargetByKey();
    private const int SnapshotDeleteMaxAttempts = 3;
    private static readonly TimeSpan SnapshotDeleteRetryDelay = TimeSpan.FromMilliseconds(250);
    private const int ForeignKeyWarningSampleLimit = 5;
    private const int ArtikliProgressLogInterval = 250;
    private static readonly string[] ProgressTableOrder =
    [
        "tipovi_obuce",
        "dobavljaci",
        "sezone",
        "objekti",
        "artikli",
        "dnevnik_promena",
        "prodaja_zaglavlje",
        "prodaja_stavke",
        "povracaj_zaglavlje",
        "povracaj_stavke",
        "nivelacije",
        "unos_robe",
        "povratnice",
        "prenos_robe"
    ];

    private readonly TrendplusDbContext _trendDb;
    private readonly AnalyticsDbContext _analyticsDb;
    private readonly IAnalyticsCacheService? _analyticsCache;
    private readonly ILogger<AccessImportService> _logger;
    private readonly AccessImportOptions _options;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly IAccessImportJobQueue? _jobQueue;

    // Populated by ImportTrendplus, consumed by SyncAnalyticsAsync for StoresDim upsert
    private Dictionary<int, (string Name, string? Address, string? Phone, string? Manager)> _importedStores = [];

    // CLI fallback state
    private bool _useCliMode;
    private string? _cliFilePath;
    private int _pendingTrendWrites;
    private long? _activeBatchId;
    private AccessImportRunResponse? _activeBatchResult;
    private string? _activeBatchStep;
    private string? _activeBatchTable;
    private DateTime _lastBatchHeartbeatPersistedUtc;

    public AccessImportService(
        TrendplusDbContext trendDb,
        AnalyticsDbContext analyticsDb,
        ILogger<AccessImportService> logger,
        IOptions<AccessImportOptions>? options = null,
        IAnalyticsCacheService? analyticsCache = null,
        IServiceScopeFactory? serviceScopeFactory = null,
        IAccessImportJobQueue? jobQueue = null)
    {
        _trendDb = trendDb;
        _analyticsDb = analyticsDb;
        _logger = logger;
        _options = options?.Value ?? new AccessImportOptions();
        _analyticsCache = analyticsCache;
        _serviceScopeFactory = serviceScopeFactory;
        _jobQueue = jobQueue;
    }

    private IAccessDataReaderSession CreateReadSession(string accessFilePath)
    {
        return OperatingSystem.IsWindows()
            ? new WindowsAccessSession(accessFilePath, _options, _logger)
            : new MdbToolsCliSession(accessFilePath, _options, _logger);
    }

    private void ResetAccessReadMode(string accessFilePath)
    {
        _useCliMode = false;
        _cliFilePath = File.Exists(accessFilePath) ? accessFilePath : null;
    }

    private void InitializeBatchProgressContext(long batchId, AccessImportRunResponse result)
    {
        _activeBatchId = batchId;
        _activeBatchResult = result;
        _activeBatchStep = "queued";
        _activeBatchTable = "all";
        _lastBatchHeartbeatPersistedUtc = DateTime.MinValue;
    }

    private void SetBatchProgressContext(string? step, string? table)
    {
        _activeBatchStep = step;
        _activeBatchTable = table;
    }

    private void ClearBatchProgressContext()
    {
        _activeBatchId = null;
        _activeBatchResult = null;
        _activeBatchStep = null;
        _activeBatchTable = null;
        _lastBatchHeartbeatPersistedUtc = DateTime.MinValue;
    }

    private TimeSpan GetBatchHeartbeatPersistInterval()
        => TimeSpan.FromSeconds(Math.Max(1, Math.Max(_options.HeartbeatIntervalSeconds, _options.StatusUpdateThrottleSeconds)));

    private async Task PersistBatchProgressAsync(string reason, bool force, CancellationToken ct)
    {
        if (_activeBatchId is null || _activeBatchResult is null || _serviceScopeFactory is null)
            return;

        var now = DateTime.UtcNow;
        if (!force && now - _lastBatchHeartbeatPersistedUtc < GetBatchHeartbeatPersistInterval())
            return;

        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TrendplusDbContext>();
            var batch = await db.DataImportBatches.FirstOrDefaultAsync(x => x.Id == _activeBatchId.Value, ct);
            if (batch is null)
                return;

            if (!string.Equals(batch.Status, "running", StringComparison.OrdinalIgnoreCase) || batch.CompletedAtUtc is not null)
                return;

            batch.LastHeartbeatUtc = now;
            batch.CurrentStep = string.IsNullOrWhiteSpace(_activeBatchStep) ? null : TrimToMaxLength(_activeBatchStep, 64);
            batch.CurrentTable = string.IsNullOrWhiteSpace(_activeBatchTable) ? null : TrimToMaxLength(_activeBatchTable, 300);
            batch.DurationSeconds = (int)Math.Max(0, Math.Round((now - batch.StartedAtUtc).TotalSeconds));
            batch.RowsRead = CountSourceRows(_activeBatchResult);
            batch.RowsAccepted = CountAcceptedRows(_activeBatchResult);
            batch.RowsWritten = CountImportedRows(_activeBatchResult) + CountUpdatedRows(_activeBatchResult);
            batch.ProgressPercent = ComputeProgressPercent(
                status: batch.Status,
                currentStep: batch.CurrentStep,
                currentTable: batch.CurrentTable,
                result: _activeBatchResult);
            batch.TotalImported = CountImportedRows(_activeBatchResult);
            batch.TotalUpdated = CountUpdatedRows(_activeBatchResult);
            batch.TotalErrors = _activeBatchResult.Warnings.Count;
            batch.SummaryJson = JsonSerializer.Serialize(_activeBatchResult);
            await db.SaveChangesAsync(ct);

            _lastBatchHeartbeatPersistedUtc = now;
            _logger.LogDebug(
                "Access import batch heartbeat persisted. BatchId: {BatchId}. Step: {Step}. TableName: {TableName}. Reason: {Reason}. Imported: {Imported}. Updated: {Updated}. Errors: {Errors}.",
                batch.Id,
                batch.CurrentStep ?? "<none>",
                batch.CurrentTable ?? "<none>",
                reason,
                batch.TotalImported,
                batch.TotalUpdated,
                batch.TotalErrors);
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UndefinedTable ||
            ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogDebug(
                ex,
                "Skipping Access import heartbeat persistence because batch progress columns are not fully available yet. BatchId: {BatchId}.",
                _activeBatchId.Value);
        }
        catch (Exception ex) when (IsTransientDatabaseTimeout(ex))
        {
            _logger.LogWarning(
                ex,
                "Access import heartbeat persistence hit a transient database issue. BatchId: {BatchId}. Step: {Step}. TableName: {TableName}. Reason: {Reason}.",
                _activeBatchId.Value,
                _activeBatchStep ?? "<none>",
                _activeBatchTable ?? "<none>",
                reason);
        }
    }

    private bool TryEnableCliMode(string operation, Exception? ex = null)
    {
        if (_useCliMode)
            return !string.IsNullOrWhiteSpace(_cliFilePath) && File.Exists(_cliFilePath);

        if (OperatingSystem.IsWindows() || !IsMdbToolsCliAvailable())
            return false;

        if (string.IsNullOrWhiteSpace(_cliFilePath) || !File.Exists(_cliFilePath))
            return false;

        _useCliMode = true;
        if (ex is null)
        {
            _logger.LogWarning(
                "Falling back to MDBTools CLI. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. FilePath: {FilePath}.",
                0L,
                "all",
                operation,
                _cliFilePath);
        }
        else
        {
            _logger.LogWarning(
                ex,
                "Falling back to MDBTools CLI. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. FilePath: {FilePath}.",
                0L,
                "all",
                operation,
                _cliFilePath);
        }

        return true;
    }

    private void OpenAccessConnectionOrEnableCli(OdbcConnection conn, string operation)
    {
        if (_useCliMode)
            return;

        try
        {
            conn.Open();
        }
        catch (Exception ex) when (TryEnableCliMode($"{operation}:open", ex))
        {
            // CLI mode is active; downstream reads will use MDBTools commands instead.
        }
    }

    private static List<string> FilterVisibleAccessTables(IEnumerable<string> tables, bool includeTemporaryTables)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filtered = new List<string>();

        foreach (var tableName in tables)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                continue;

            if (tableName.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!includeTemporaryTables && Normalize(tableName).Contains("privremena", StringComparison.Ordinal))
                continue;

            if (seen.Add(tableName))
                filtered.Add(tableName);
        }

        return filtered;
    }

    private List<string> GetCliUserTables(bool includeTemporaryTables)
    {
        if (!_useCliMode || string.IsNullOrWhiteSpace(_cliFilePath))
            return [];

        return FilterVisibleAccessTables(MdbCliGetTables(_cliFilePath), includeTemporaryTables);
    }

    private static IReadOnlyDictionary<string, Dictionary<string, string>> BuildPreviewAliasToTargetByKey()
    {
        var output = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tableKey, mappings) in PreviewFieldMappings)
        {
            var aliasToTarget = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mapping in mappings)
            {
                foreach (var alias in mapping.Aliases)
                {
                    var normalizedAlias = Normalize(alias);
                    if (string.IsNullOrWhiteSpace(normalizedAlias))
                        continue;

                    aliasToTarget.TryAdd(normalizedAlias, mapping.TargetField);
                }
            }

            output[tableKey] = aliasToTarget;
        }

        return output;
    }

    private IReadOnlyDictionary<string, int> GetTableRowCounts(
        OdbcConnection conn,
        IEnumerable<string> tables)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
            counts[table] = RowCount(conn, table);

        return counts;
    }

    private static AccessImportPreviewResponse CreatePreviewFailureResponse(string sourceFileName, Exception ex)
    {
        return new AccessImportPreviewResponse
        {
            SourceFileName = sourceFileName,
            CanImport = false,
            Tables = [],
            AvailableTables = [],
            Warnings = [$"Preview failed: {ex.GetBaseException().Message}"]
        };
    }

    public async Task<AccessImportPreviewResponse> PreviewAsync(string accessFilePath, bool includeTemporaryTables = false, CancellationToken ct = default)
    {
        var sourceFileName = Path.GetFileName(accessFilePath ?? string.Empty);
        var operationId = Guid.NewGuid().ToString("N");
        using var previewScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["BatchId"] = 0L,
            ["TableName"] = "all",
            ["Operation"] = "preview",
            ["OperationId"] = operationId
        });

        try
        {
            ct.ThrowIfCancellationRequested();
            EnsurePlatformSupport();
            if (!File.Exists(accessFilePath))
                throw new FileNotFoundException("ACCDB file was not found.", accessFilePath);

            var snapshot = CreateSnapshotIfLocked(accessFilePath);
            try
            {
                await using var session = CreateReadSession(snapshot.FilePath);
                using var sessionScope = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["AccessReadMode"] = session.Mode
                });
                _logger.LogInformation(
                    "Access preview started. SourceFileName: {SourceFileName}. Mode: {Mode}. IncludeTemporaryTables: {IncludeTemporaryTables}.",
                    sourceFileName,
                    session.Mode,
                    includeTemporaryTables);

                var tables = (await session.GetTablesAsync(includeTemporaryTables, ct))
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var map = new Dictionary<string, TableMatch>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tipovi_obuce"] = await FindTableDetailedAsync(session, tables, TipoviCandidates, ct: ct),
                    ["dobavljaci"] = await FindTableDetailedAsync(session, tables, DobavljaciCandidates, ct: ct),
                    ["sezone"] = await FindTableDetailedAsync(session, tables, SezoneCandidates,
                        sigRequired: ["idsezona", "naziv"]),
                    ["artikli"] = await FindTableDetailedAsync(session, tables, ArtikliCandidates,
                        sigRequired: ["idartikal", "naziv"],
                        sigBonus: ["nabavnacena", "prodajnacena", "plu"], ct: ct),
                    ["prodaja_zaglavlje"] = await FindTableDetailedAsync(session, tables, ProdajaCandidates, ct: ct),
                    ["prodaja_stavke"] = await FindTableDetailedAsync(session, tables, ProdajaStavkeCandidates, ct: ct),
                    ["dnevnik_promena"] = await FindTableDetailedAsync(session, tables, DnevnikPromenaCandidates,
                        sigRequired: ["iddnevnik", "datum"]),
                    ["povracaj_zaglavlje"] = await FindTableDetailedAsync(session, tables, PovracajCandidates, ct: ct),
                    ["povracaj_stavke"] = await FindTableDetailedAsync(session, tables, PovracajStavkeCandidates2, ct: ct),
                    ["nivelacije"] = await FindTableDetailedAsync(session, tables, NivelacijeCandidates,
                        sigRequired: ["idartikal", "novacena"], ct: ct),
                    ["unos_robe"] = await FindTableDetailedAsync(session, tables, UnosRobeCandidates,
                        sigRequired: ["idartikal", "kolicina", "iddobavljac"], ct: ct),
                    ["povratnice"] = await FindTableDetailedAsync(session, tables, PovratniceCandidates,
                        sigRequired: ["idartikal", "kolicina"],
                        sigBonus: ["razlog", "idpovratnice"], ct: ct),
                    ["prenos_robe"] = await FindTableDetailedAsync(session, tables, PrenosRobeCandidates,
                        sigRequired: ["idartikal", "kolicina"],
                        sigBonus: ["idobjekatiz", "idobjekatulaz", "idobjekat"], ct: ct),
                    ["objekti"] = await FindTableDetailedAsync(session, tables, ObjekatCandidates,
                        sigRequired: ["idobjekat", "nazivobjekta"], ct: ct),
                };

                var response = new AccessImportPreviewResponse
                {
                    SourceFileName = sourceFileName,
                    CanImport = map["artikli"].TableName is not null,
                    AvailableTables = tables,
                    TotalAccessTables = tables.Count,
                    Tables = []
                };

                if (!string.IsNullOrWhiteSpace(snapshot.Warning))
                    response.Warnings.Add(snapshot.Warning);

                var previewBuilds = new Dictionary<string, PreviewTableBuildResult>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in map)
                {
                    var build = await BuildTablePreviewAsync(session, entry.Key, entry.Value, ct);
                    previewBuilds[entry.Key] = build;
                    response.Tables.Add(build.Preview);

                    var tablePreview = build.Preview;
                    if (entry.Key.Equals("prodaja_zaglavlje", StringComparison.OrdinalIgnoreCase)
                        && tablePreview.Found
                        && IsProdajaLineTableByColumns(tablePreview.AccessColumns))
                    {
                        response.Warnings.Add($"Tabela '{tablePreview.TableName}' izgleda kao stavke prodaje (IDDnevnik/IDArtikal/Kolicina/ProdajnaCena), ne kao zaglavlje.");
                    }
                }

                if (!response.CanImport)
                    response.Warnings.Add("Nije pronadjena tabela za artikle (obavezna).");

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

                        if (req.Key.Equals("artikli", StringComparison.OrdinalIgnoreCase))
                            response.CanImport = false;
                    }

                    if (previewBuilds.TryGetValue(req.Key, out var build))
                        TryAddSampleDataWarnings(build.SampleRows, tablePreview, req.Value, response.Warnings);
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
                response.TotalAccessRows = response.Tables.Sum(t => t.RowCount);
                response.AccessTablesWithRows = response.Tables.Count(t => t.HasRows);
                response.RowCoveragePercent = response.TotalAccessRows == 0
                    ? 100d
                    : Math.Round(response.MappedAccessRows * 100d / response.TotalAccessRows, 2);

                var mappedTableKeys = map.Values
                    .Where(x => !string.IsNullOrWhiteSpace(x.TableName))
                    .Select(x => Normalize(x.TableName))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                response.UnmappedAccessTablesWithRows = [];

                if (response.UnmappedAccessTablesWithRows.Count > 0)
                {
                    var sample = string.Join(", ", response.UnmappedAccessTablesWithRows.Take(10));
                    var hidden = response.UnmappedAccessTablesWithRows.Count - Math.Min(10, response.UnmappedAccessTablesWithRows.Count);
                    var suffix = hidden > 0 ? $" (+{hidden} dodatnih)" : string.Empty;
                    response.Warnings.Add($"Postoje Access tabele sa podacima koje nisu mapirane: {sample}{suffix}. Proveri da li ih treba ukljuciti u import.");
                }

                if (map["prodaja_zaglavlje"].TableName is null && map["dnevnik_promena"].TableName is not null)
                    response.Warnings.Add("Nije pronadjena tabela prodaje - prodaja ce biti sintetizovana iz DnevnikPromena (tip='Prodaja').");

                if (map["prodaja_stavke"].TableName is null && map["prodaja_zaglavlje"].TableName is not null)
                    response.Warnings.Add("Nije pronadjena tabela stavki prodaje - zaglavlja bez stavki bice uvezena bez linija.");

                var foundMovements = new List<string>();
                foreach (var k in new[] { "nivelacije", "unos_robe", "povratnice", "prenos_robe" })
                {
                    if (map.TryGetValue(k, out var tm) && tm.TableName is not null)
                        foundMovements.Add(tm.TableName);
                }
                if (foundMovements.Count > 0)
                    response.Warnings.Add($"Pronadjene tabele kretanja zaliha: {string.Join(", ", foundMovements)}.");

                _logger.LogInformation(
                    "Access preview completed. SourceFileName: {SourceFileName}. Mode: {Mode}. DurationTables: {TotalAccessTables}. MappedTables: {MappedAccessTables}. CanImport: {CanImport}.",
                    sourceFileName,
                    session.Mode,
                    response.TotalAccessTables,
                    response.MappedAccessTables,
                    response.CanImport);

                return response;
            }
            finally
            {
                if (snapshot.IsSnapshot)
                    TryDeleteFile(snapshot.FilePath, "preview-cleanup", sourceFileName);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Preview failed for file {SourceFileName}. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Returning fail-soft response.",
                sourceFileName,
                0L,
                "all",
                "preview");
            return CreatePreviewFailureResponse(sourceFileName, ex);
        }
    }

    private sealed record PreviewTableBuildResult(
        AccessImportTablePreview Preview,
        IReadOnlyList<AccessDataRow> SampleRows);

    private async Task<PreviewTableBuildResult> BuildTablePreviewAsync(
        IAccessDataReaderSession session,
        string key,
        TableMatch tableMatch,
        CancellationToken ct)
    {
        var tableName = tableMatch.TableName;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var preview = new AccessImportTablePreview
        {
            Key = key,
            TableName = tableName,
            MatchStrategy = tableMatch.Strategy,
            RowCountMode = "unknown"
        };

        if (string.IsNullOrWhiteSpace(tableName))
            return new PreviewTableBuildResult(preview, Array.Empty<AccessDataRow>());

        var columns = tableMatch.Columns ?? await session.GetColumnsAsync(tableName, ct);
        preview.AccessColumns = columns.ToList();
        preview.FieldMappings = BuildFieldMappingsPreview(key, preview.AccessColumns);
        preview.TotalMappings = preview.FieldMappings.Count;
        preview.MatchedMappings = preview.FieldMappings.Count(m =>
            !string.IsNullOrWhiteSpace(m.SourceColumn) &&
            m.Status.Equals("matched", StringComparison.OrdinalIgnoreCase));
        preview.MappingCoveragePercent = preview.TotalMappings == 0
            ? 100d
            : Math.Round(preview.MatchedMappings * 100d / preview.TotalMappings, 2);

        var rowCountResult = await session.TryGetExactRowCountAsync(tableName, ct);
        IReadOnlyList<AccessDataRow> sampleRows = Array.Empty<AccessDataRow>();
        if (rowCountResult.IsExact)
        {
            preview.RowCount = rowCountResult.Count;
            preview.RowCountMode = rowCountResult.Mode;
        }
        else
        {
            var sample = await ReadSampleRowsAsync(session, tableName, _options.PreviewSampleTake, ct);
            sampleRows = sample.Rows;
            preview.RowCount = sample.Rows.Count;
            preview.RowCountMode = sample.IsComplete ? "exact" : "sampled";
        }

        var mappedSourceColumns = preview.FieldMappings
            .Where(m => !string.IsNullOrWhiteSpace(m.SourceColumn))
            .Select(m => Normalize(m.SourceColumn))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        preview.UnmappedAccessColumns = preview.AccessColumns
            .Where(column => !mappedSourceColumns.Contains(Normalize(column)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        sw.Stop();
        _logger.LogInformation(
            "Access preview table analyzed. Step: {Step}. TableKey: {TableKey}. TableName: {TableName}. DurationMs: {DurationMs}. RowCount: {RowCount}. RowCountMode: {RowCountMode}. MatchedMappings: {MatchedMappings}.",
            "preview-table",
            key,
            tableName,
            sw.ElapsedMilliseconds,
            preview.RowCount,
            preview.RowCountMode,
            preview.MatchedMappings);

        return new PreviewTableBuildResult(preview, sampleRows);
    }

    private async Task<(List<AccessDataRow> Rows, bool IsComplete)> ReadSampleRowsAsync(
        IAccessDataReaderSession session,
        string table,
        int take,
        CancellationToken ct)
    {
        if (take <= 0)
            return ([], true);

        var rows = new List<AccessDataRow>(take);
        await using var enumerator = session.ReadRowsAsync(table, ct).GetAsyncEnumerator(ct);

        while (rows.Count < take && await enumerator.MoveNextAsync())
            rows.Add(enumerator.Current);

        var isComplete = rows.Count < take || !await enumerator.MoveNextAsync();
        return (rows, isComplete);
    }

    private AccessImportTablePreview BuildTablePreview(
        OdbcConnection conn,
        string key,
        TableMatch tableMatch,
        IReadOnlyDictionary<string, int> tableRowCounts)
    {
        var tableName = tableMatch.TableName;
        var preview = new AccessImportTablePreview
        {
            Key = key,
            TableName = tableName,
            MatchStrategy = tableMatch.Strategy,
            RowCount = tableName is null
                ? 0
                : (tableRowCounts.TryGetValue(tableName, out var rowCount) ? rowCount : 0)
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

    private List<string> ReadColumnNames(OdbcConnection conn, string table)
    {
        var columns = new List<string>();
        if (_useCliMode && _cliFilePath is not null)
        {
            try
            {
                return MdbCliGetColumnsRaw(_cliFilePath, table);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReadColumnNames] CLI failed for {table}: {ex.Message}");
                return columns;
            }
        }

        if (!TryGetQuotedTableIdentifier(table, out var quotedTable, out var failureReason))
        {
            System.Diagnostics.Debug.WriteLine($"[ReadColumnNames] Invalid table name '{table}': {failureReason}");
            return columns;
        }

        try
        {
            using var cmd = new OdbcCommand($"SELECT * FROM {quotedTable} WHERE 1=0", conn);
            using var r = cmd.ExecuteReader();
            
            if (r is null || r.FieldCount == 0)
                return columns;

            for (var i = 0; i < r.FieldCount; i++)
            {
                try
                {
                    var name = r.GetName(i);
                    if (!string.IsNullOrWhiteSpace(name))
                        columns.Add(name);
                }
                catch (Exception fex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReadColumnNames] Error reading column {i}: {fex.Message}");
                    // Skip problematic column, continue with others
                }
            }
        }
        catch (Exception ex)
        {
            // Best-effort preview. Import path performs stronger validation.
            System.Diagnostics.Debug.WriteLine($"[ReadColumnNames] Exception reading {table}: {ex.GetType().Name}: {ex.Message}");
        }
        return columns;
    }

    private static List<AccessImportFieldMappingPreview> BuildFieldMappingsPreview(string key, IReadOnlyList<string> columns)
    {
        if (!PreviewFieldMappings.TryGetValue(key, out var fieldAliases) || fieldAliases.Count == 0)
            return new List<AccessImportFieldMappingPreview>();

        PreviewAliasToTargetByKey.TryGetValue(key, out var aliasToTarget);
        aliasToTarget ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var matchedByTarget = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            var normalized = Normalize(column);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!aliasToTarget.TryGetValue(normalized, out var targetField))
                continue;

            matchedByTarget.TryAdd(targetField, column);
        }

        var output = new List<AccessImportFieldMappingPreview>(fieldAliases.Count);
        foreach (var field in fieldAliases)
        {
            matchedByTarget.TryGetValue(field.TargetField, out var matchedColumn);

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
        var (batch, _) = await CreateImportBatchAsync(
            sourceFilePath: accessFilePath,
            sourceFileName: Path.GetFileName(accessFilePath),
            includeAnalytics: includeAnalytics,
            overwriteExisting: overwriteExisting,
            includeTemporaryTables: includeTemporaryTables,
            ct: ct);
        return await ExecuteImportBatchAsync(
            batch.Id,
            accessFilePath,
            batch.SourceFileName,
            includeAnalytics,
            overwriteExisting,
            includeTemporaryTables,
            deleteWorkingFileAfterCompletion: false,
            ct);
    }

    public async Task<AccessImportRunResponse> StartImportAsync(
        string accessFilePath,
        bool includeAnalytics,
        bool overwriteExisting,
        bool includeTemporaryTables = false,
        CancellationToken ct = default)
    {
        EnsurePlatformSupport();
        if (!File.Exists(accessFilePath))
            throw new FileNotFoundException("ACCDB fajl nije pronaÄ‘en.", accessFilePath);

        if (_jobQueue is null)
            throw new InvalidOperationException("Access import background job queue is not configured.");

        await EnsureDataImportBatchesTableAsync(ct);
        await RecoverStaleRunningBatchesAsync(batchId: null, ct);

        if (_options.PreventConcurrentRuns)
        {
            var activeBatch = await GetActiveRunningBatchAsync(ct);
            if (activeBatch is not null)
            {
                var runningResponse = await BuildExistingRunningBatchResponseAsync(activeBatch, includeAnalytics, ct);
                if (runningResponse is not null)
                    return runningResponse;
            }
        }

        var workingCopy = CreateBackgroundWorkingCopy(accessFilePath);
        AccessImportRunResponse result;
        try
        {
            (_, result) = await CreateImportBatchAsync(
                sourceFilePath: workingCopy,
                sourceFileName: Path.GetFileName(accessFilePath),
                includeAnalytics: includeAnalytics,
                overwriteExisting: overwriteExisting,
                includeTemporaryTables: includeTemporaryTables,
                ct: ct);

            _logger.LogInformation(
                "Access import batch queued. BatchId: {BatchId}. SourceFileName: {SourceFileName}. IncludeAnalytics: {IncludeAnalytics}. OverwriteExisting: {OverwriteExisting}. IncludeTemporaryTables: {IncludeTemporaryTables}.",
                result.BatchId,
                Path.GetFileName(accessFilePath),
                includeAnalytics,
                overwriteExisting,
                includeTemporaryTables);
        }
        catch (InvalidOperationException) when (_options.PreventConcurrentRuns)
        {
            var activeBatch = await GetActiveRunningBatchAsync(ct);
            if (activeBatch is not null)
            {
                var runningResponse = await BuildExistingRunningBatchResponseAsync(activeBatch, includeAnalytics, ct);
                if (runningResponse is not null)
                    return runningResponse;
            }

            throw;
        }
        catch
        {
            TryDeleteFile(workingCopy, "batch-create-failed-cleanup", Path.GetFileName(accessFilePath), 0, "working-copy");
            throw;
        }

        // NOTE: enqueue is intentionally not performed synchronously here.
        // The batch is persisted as 'pending' and will be picked up by the background worker.
        // Performing enqueue synchronously from the HTTP request caused client-visible
        // failures when the queue temporarily errored (race condition). Manual or worker
        // recovery should be used to enqueue if needed.

        return result;
    }

    private async Task<AccessImportRunResponse?> BuildExistingRunningBatchResponseAsync(
        RunningBatchSnapshot activeBatch,
        bool includeAnalytics,
        CancellationToken ct)
    {
        var batch = await _trendDb.DataImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == activeBatch.Id, ct);

        if (batch is null)
            return null;

        AccessImportRunResponse response;
        if (!string.IsNullOrWhiteSpace(batch.SummaryJson))
        {
            try
            {
                response = JsonSerializer.Deserialize<AccessImportRunResponse>(batch.SummaryJson) ?? new AccessImportRunResponse();
            }
            catch
            {
                response = new AccessImportRunResponse();
            }
        }
        else
        {
            response = new AccessImportRunResponse();
        }

        response.BatchId = batch.Id;
        response.Status = string.IsNullOrWhiteSpace(batch.Status) ? "running" : batch.Status;
        response.SourceFileName = batch.SourceFileName;
        response.IncludeAnalytics = includeAnalytics;
        response.StartedAtUtc = batch.StartedAtUtc;
        response.CompletedAtUtc = null;

        var warning =
            $"Access import batch {batch.Id} is already {response.Status} since {batch.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC for file '{batch.SourceFileName}'.";
        if (!response.Warnings.Contains(warning, StringComparer.Ordinal))
            response.Warnings.Add(warning);

        if (batch.LastHeartbeatUtc.HasValue || !string.IsNullOrWhiteSpace(batch.CurrentStep) || !string.IsNullOrWhiteSpace(batch.CurrentTable))
        {
            var heartbeatValue = batch.LastHeartbeatUtc.HasValue
                ? batch.LastHeartbeatUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"
                : "n/a";
            var progressWarning =
                $"Current progress: step '{batch.CurrentStep ?? "unknown"}', table '{batch.CurrentTable ?? "unknown"}', last heartbeat {heartbeatValue}.";
            if (!response.Warnings.Contains(progressWarning, StringComparer.Ordinal))
                response.Warnings.Add(progressWarning);
        }

        _logger.LogInformation(
            "Reusing existing running Access import batch instead of creating a new one. BatchId: {BatchId}. SourceFileName: {SourceFileName}. StartedAtUtc: {StartedAtUtc}.",
            batch.Id,
            batch.SourceFileName,
            batch.StartedAtUtc);

        return response;
    }

    public Task<AccessImportRunResponse> RunExistingBatchAsync(
        long batchId,
        string accessFilePath,
        string sourceFileName,
        bool includeAnalytics,
        bool overwriteExisting,
        bool includeTemporaryTables = false,
        bool deleteWorkingFileAfterCompletion = false,
        CancellationToken ct = default)
        => ExecuteImportBatchAsync(
            batchId,
            accessFilePath,
            sourceFileName,
            includeAnalytics,
            overwriteExisting,
            includeTemporaryTables,
            deleteWorkingFileAfterCompletion,
            ct);

    private async Task<(DataImportBatch Batch, AccessImportRunResponse Result)> CreateImportBatchAsync(
        string sourceFilePath,
        string sourceFileName,
        bool includeAnalytics,
        bool overwriteExisting,
        bool includeTemporaryTables,
        CancellationToken ct)
    {
        EnsurePlatformSupport();
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException("ACCDB fajl nije pronaÄ‘en.", sourceFilePath);

        var now = DateTime.UtcNow;
        await EnsureDataImportBatchesTableAsync(ct);
        await RecoverStaleRunningBatchesAsync(batchId: null, ct);

        if (_options.PreventConcurrentRuns)
        {
            var activeBatch = await GetActiveRunningBatchAsync(ct);
            if (activeBatch is not null)
            {
                throw new InvalidOperationException(
                    $"Access import batch {activeBatch.Id} is already running since {activeBatch.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC for file '{activeBatch.SourceFileName}'.");
            }
        }

        var batch = new DataImportBatch
        {
            SourceSystem = "access",
            SourceFileName = string.IsNullOrWhiteSpace(sourceFileName) ? Path.GetFileName(sourceFilePath) : sourceFileName,
            SourceFilePath = sourceFilePath,
            QueuedAtUtc = now,
            StartedAtUtc = now,
            LastHeartbeatUtc = now,
            CurrentStep = "queued",
            CurrentTable = "all",
            Status = "pending",
            IncludeAnalytics = includeAnalytics,
            OverwriteExisting = overwriteExisting,
            IncludeTemporaryTables = includeTemporaryTables,
            SkipInvalidForeignKeys = _options.SkipInvalidForeignKeys,
            ImportMode = "auto",
            ProgressPercent = 0,
            RowsRead = 0,
            RowsAccepted = 0,
            RowsWritten = 0,
            RetryCount = 0,
            CancellationRequested = false
        };

        _trendDb.DataImportBatches.Add(batch);
        await _trendDb.SaveChangesAsync(ct);

        var result = new AccessImportRunResponse
        {
            BatchId = batch.Id,
            Status = "pending",
            SourceFileName = batch.SourceFileName,
            IncludeAnalytics = includeAnalytics,
            StartedAtUtc = batch.StartedAtUtc
        };

        return (batch, result);
    }

    private async Task<AccessImportRunResponse> ExecuteImportBatchAsync(
        long batchId,
        string accessFilePath,
        string sourceFileName,
        bool includeAnalytics,
        bool overwriteExisting,
        bool includeTemporaryTables,
        bool deleteWorkingFileAfterCompletion,
        CancellationToken ct)
    {
        EnsurePlatformSupport();
        if (!File.Exists(accessFilePath))
            throw new FileNotFoundException("ACCDB fajl nije pronaÄ‘en.", accessFilePath);

        var batch = await _trendDb.DataImportBatches.FirstOrDefaultAsync(x => x.Id == batchId, ct)
            ?? throw new InvalidOperationException($"Batch {batchId} nije pronaÄ‘en.");

        if (batch.CancellationRequested &&
            (string.Equals(batch.Status, "pending", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(batch.Status, "running", StringComparison.OrdinalIgnoreCase)))
        {
            var cancelledAt = DateTime.UtcNow;
            batch.Status = "cancelled";
            batch.CompletedAtUtc = cancelledAt;
            batch.LastHeartbeatUtc = cancelledAt;
            batch.CurrentStep = "cancelled";
            batch.CurrentTable = null;
            batch.ProgressPercent = 100;
            batch.ErrorMessage = "Cancellation requested by user.";
            await _trendDb.SaveChangesAsync(ct);

            return new AccessImportRunResponse
            {
                BatchId = batch.Id,
                Status = "cancelled",
                SourceFileName = batch.SourceFileName,
                IncludeAnalytics = includeAnalytics,
                StartedAtUtc = batch.StartedAtUtc,
                CompletedAtUtc = cancelledAt,
                Warnings = { "Import cancelled before execution started." }
            };
        }

        includeAnalytics = batch.IncludeAnalytics;
        overwriteExisting = batch.OverwriteExisting;
        includeTemporaryTables = batch.IncludeTemporaryTables;
        if (string.IsNullOrWhiteSpace(batch.SourceFilePath))
            batch.SourceFilePath = accessFilePath;
        batch.SourceFileName = string.IsNullOrWhiteSpace(sourceFileName) ? batch.SourceFileName : sourceFileName;

        var result = new AccessImportRunResponse
        {
            BatchId = batch.Id,
            Status = batch.Status,
            SourceFileName = batch.SourceFileName,
            IncludeAnalytics = includeAnalytics,
            StartedAtUtc = batch.StartedAtUtc
        };
        InitializeBatchProgressContext(batch.Id, result);
        SetBatchProgressContext("starting", "all");

        var operationId = Guid.NewGuid().ToString("N");
        var snapshot = _options.EnableSnapshotCopy
            ? CreateSnapshotIfLocked(accessFilePath)
            : new AccessFileSnapshot(accessFilePath, false, null);
        if (!string.IsNullOrWhiteSpace(snapshot.Warning))
            result.Warnings.Add(snapshot.Warning);

        using var importScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["BatchId"] = batch.Id,
            ["TableName"] = "all",
            ["Operation"] = "access-import",
            ["OperationId"] = operationId
        });

        try
        {
            await using var tx = await _trendDb.Database.BeginTransactionAsync(ct);
            var originalAutoDetectChanges = _trendDb.ChangeTracker.AutoDetectChangesEnabled;
            try
            {
                var startedAt = DateTime.UtcNow;
                batch.Status = "running";
                batch.StartedAtUtc = startedAt;
                batch.ErrorMessage = null;
                batch.ErrorDetailsJson = null;
                batch.CompletedAtUtc = null;
                batch.SummaryJson = null;
                batch.LastHeartbeatUtc = startedAt;
                batch.CurrentStep = "starting";
                batch.CurrentTable = "all";
                batch.ProgressPercent = 2;
                batch.RowsRead = 0;
                batch.RowsAccepted = 0;
                batch.RowsWritten = 0;

                _trendDb.ChangeTracker.AutoDetectChangesEnabled = false;
                _pendingTrendWrites = 0;
                await using var session = CreateReadSession(snapshot.FilePath);
                using var sessionScope = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["AccessReadMode"] = session.Mode
                });
                _logger.LogInformation(
                    "Access import started. BatchId: {BatchId}. SourceFileName: {SourceFileName}. Mode: {Mode}. IncludeAnalytics: {IncludeAnalytics}. OverwriteExisting: {OverwriteExisting}. IncludeTemporaryTables: {IncludeTemporaryTables}.",
                    batch.Id,
                    batch.SourceFileName,
                    session.Mode,
                    includeAnalytics,
                    overwriteExisting,
                    includeTemporaryTables);
                await PersistBatchProgressAsync("batch-start", force: true, ct);
                await ImportTrendplusAsync(session, overwriteExisting, includeTemporaryTables, result, ct);
                await FlushTrendWritesAsync(force: true, ct);
                await ResetTrendplusSequencesAsync(ct);

                if (includeAnalytics)
                    await SyncAnalyticsAsync(result, ct);

                result.Status = "completed";
                result.CompletedAtUtc = DateTime.UtcNow;
                batch.Status = "completed";
                batch.CompletedAtUtc = result.CompletedAtUtc;
                batch.LastHeartbeatUtc = result.CompletedAtUtc;
                batch.CurrentStep = null;
                batch.CurrentTable = null;
                batch.ProgressPercent = 100;
                batch.DurationSeconds = (int)Math.Max(0, Math.Round(((result.CompletedAtUtc ?? DateTime.UtcNow) - batch.StartedAtUtc).TotalSeconds));
                batch.RowsRead = CountSourceRows(result);
                batch.RowsAccepted = CountAcceptedRows(result);
                batch.RowsWritten = CountImportedRows(result) + CountUpdatedRows(result);
                batch.TotalImported = CountImportedRows(result);
                batch.TotalUpdated = CountUpdatedRows(result);
                batch.TotalErrors = result.Warnings.Count;
                batch.SummaryJson = JsonSerializer.Serialize(result);
                await _trendDb.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                _logger.LogInformation(
                    "Access import completed. BatchId: {BatchId}. SourceFileName: {SourceFileName}. Status: {Status}. IncludeAnalytics: {IncludeAnalytics}.",
                    batch.Id,
                    batch.SourceFileName,
                    result.Status,
                    includeAnalytics);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
            finally
            {
                _trendDb.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetectChanges;
            }

            return result;
        }
        catch (AccessImportCancellationRequestedException ex)
        {
            result.Status = "cancelled";
            result.CompletedAtUtc = DateTime.UtcNow;
            result.Warnings.Add(ex.Message);

            batch.Status = "cancelled";
            batch.CompletedAtUtc = result.CompletedAtUtc;
            batch.LastHeartbeatUtc = result.CompletedAtUtc;
            batch.CurrentStep = "cancelled";
            batch.CurrentTable = null;
            batch.ProgressPercent = 100;
            batch.DurationSeconds = (int)Math.Max(0, Math.Round(((result.CompletedAtUtc ?? DateTime.UtcNow) - batch.StartedAtUtc).TotalSeconds));
            batch.RowsRead = CountSourceRows(result);
            batch.RowsAccepted = CountAcceptedRows(result);
            batch.RowsWritten = CountImportedRows(result) + CountUpdatedRows(result);
            batch.TotalImported = CountImportedRows(result);
            batch.TotalUpdated = CountUpdatedRows(result);
            batch.TotalErrors = result.Warnings.Count;
            batch.ErrorMessage = "Cancellation requested by user.";
            batch.ErrorDetailsJson = JsonSerializer.Serialize(new
            {
                type = ex.GetType().FullName,
                message = ex.Message
            });
            batch.SummaryJson = JsonSerializer.Serialize(result);

            await _trendDb.SaveChangesAsync(ct);
            _logger.LogWarning(
                ex,
                "Access import cancelled. BatchId: {BatchId}.",
                batch.Id);
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Access import interrupted by cancellation. Marking batch {BatchId} as interrupted.",
                batch.Id);

            try
            {
                // Persist interrupted status even if the incoming cancellation token is cancelled
                await MarkBatchInterruptedAsync(batch.Id, CancellationToken.None);
            }
            catch (Exception markEx)
            {
                _logger.LogWarning(
                    markEx,
                    "Failed to persist interrupted status for Access import batch. BatchId: {BatchId}.",
                    batch.Id);
            }

            throw;
        }
        catch (Exception ex)
        {
            result.Status = "failed";
            result.CompletedAtUtc = DateTime.UtcNow;
            result.Warnings.Add(ex.GetBaseException().Message);

            batch.Status = "failed";
            batch.CompletedAtUtc = result.CompletedAtUtc;
            batch.LastHeartbeatUtc = result.CompletedAtUtc;
            batch.CurrentStep = string.IsNullOrWhiteSpace(_activeBatchStep) ? null : TrimToMaxLength(_activeBatchStep, 64);
            batch.CurrentTable = string.IsNullOrWhiteSpace(_activeBatchTable) ? null : TrimToMaxLength(_activeBatchTable, 300);
            batch.ProgressPercent = 100;
            batch.DurationSeconds = (int)Math.Max(0, Math.Round(((result.CompletedAtUtc ?? DateTime.UtcNow) - batch.StartedAtUtc).TotalSeconds));
            batch.RowsRead = CountSourceRows(result);
            batch.RowsAccepted = CountAcceptedRows(result);
            batch.RowsWritten = CountImportedRows(result) + CountUpdatedRows(result);
            batch.TotalImported = CountImportedRows(result);
            batch.TotalUpdated = CountUpdatedRows(result);
            batch.TotalErrors = Math.Max(1, result.Warnings.Count);
            batch.RetryCount = Math.Max(0, batch.RetryCount) + 1;
            batch.ErrorMessage = ex.GetBaseException().Message;
            batch.ErrorDetailsJson = JsonSerializer.Serialize(new
            {
                type = ex.GetType().FullName,
                baseType = ex.GetBaseException().GetType().FullName,
                message = ex.GetBaseException().Message
            });
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
                _logger.LogWarning(
                    saveFailedEx,
                    "Failed to persist failed Access import batch status. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}.",
                    batch.Id,
                    "DataImportBatches",
                    "mark-failed");
            }

            _logger.LogError(
                ex,
                "Access import failed. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}.",
                batch.Id,
                "all",
                "import");
            throw;
        }
        finally
        {
            ClearBatchProgressContext();

            if (snapshot.IsSnapshot)
                TryDeleteFile(snapshot.FilePath, "import-cleanup", batch.SourceFileName, batch.Id, "snapshot");

            if (deleteWorkingFileAfterCompletion)
                TryDeleteFile(accessFilePath, "background-import-cleanup", batch.SourceFileName, batch.Id, "working-copy");
        }
    }

    private sealed record TableMatch(string? TableName, string Strategy, IReadOnlyList<string>? Columns = null);

    private sealed record RunningBatchSnapshot(long Id, string SourceFileName, string Status, DateTime StartedAtUtc, DateTime? LastHeartbeatUtc);
    private sealed class AccessImportCancellationRequestedException : OperationCanceledException
    {
        public long BatchId { get; }

        public AccessImportCancellationRequestedException(long batchId)
            : base($"Access import batch {batchId} cancellation was requested.")
        {
            BatchId = batchId;
        }
    }

    public Task RefreshBatchStatusesAsync(long? batchId = null, CancellationToken ct = default)
        => RecoverStaleRunningBatchesAsync(batchId, ct);

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
                    QueuedAtUtc = x.QueuedAtUtc,
                    StartedAtUtc = x.StartedAtUtc,
                    CompletedAtUtc = x.CompletedAtUtc,
                    LastHeartbeatUtc = x.LastHeartbeatUtc,
                    Status = x.Status,
                    CurrentStep = x.CurrentStep,
                    CurrentTable = x.CurrentTable,
                    ProgressPercent = x.ProgressPercent,
                    RowsRead = x.RowsRead,
                    RowsAccepted = x.RowsAccepted,
                    RowsWritten = x.RowsWritten,
                    CancellationRequested = x.CancellationRequested,
                    CancellationRequestedAtUtc = x.CancellationRequestedAtUtc,
                    RetryCount = x.RetryCount,
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
                "Access import batches query hit legacy schema (missing columns). BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Falling back to compatibility projection.",
                0L,
                "DataImportBatches",
                "list-batches");

            return await _trendDb.DataImportBatches
                .AsNoTracking()
                .OrderByDescending(x => x.StartedAtUtc)
                .Take(take)
                .Select(x => new AccessImportBatchDto
                {
                    Id = x.Id,
                    SourceSystem = x.SourceSystem,
                    SourceFileName = x.SourceFileName,
                    QueuedAtUtc = x.StartedAtUtc,
                    StartedAtUtc = x.StartedAtUtc,
                    CompletedAtUtc = x.CompletedAtUtc,
                    LastHeartbeatUtc = null,
                    Status = x.Status,
                    CurrentStep = null,
                    CurrentTable = null,
                    ProgressPercent = 0,
                    RowsRead = 0,
                    RowsAccepted = 0,
                    RowsWritten = 0,
                    CancellationRequested = false,
                    CancellationRequestedAtUtc = null,
                    RetryCount = 0,
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
                "Access import batches table is missing. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Returning empty list as compatibility fallback.",
                0L,
                "DataImportBatches",
                "list-batches");
            return [];
        }
        catch (Exception ex) when (IsTransientDatabaseTimeout(ex))
        {
            _logger.LogWarning(
                ex,
                "Access import batches query hit a transient timeout/connectivity issue. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Returning empty list.",
                0L,
                "DataImportBatches",
                "list-batches");
            return [];
        }
    }

    public async Task<AccessImportBatchDto?> GetBatchAsync(long batchId, CancellationToken ct = default)
    {
        if (batchId <= 0)
            return null;

        try
        {
            return await _trendDb.DataImportBatches
                .AsNoTracking()
                .Where(x => x.Id == batchId)
                .Select(x => new AccessImportBatchDto
                {
                    Id = x.Id,
                    SourceSystem = x.SourceSystem,
                    SourceFileName = x.SourceFileName,
                    QueuedAtUtc = x.QueuedAtUtc,
                    StartedAtUtc = x.StartedAtUtc,
                    CompletedAtUtc = x.CompletedAtUtc,
                    LastHeartbeatUtc = x.LastHeartbeatUtc,
                    Status = x.Status,
                    CurrentStep = x.CurrentStep,
                    CurrentTable = x.CurrentTable,
                    ProgressPercent = x.ProgressPercent,
                    RowsRead = x.RowsRead,
                    RowsAccepted = x.RowsAccepted,
                    RowsWritten = x.RowsWritten,
                    CancellationRequested = x.CancellationRequested,
                    CancellationRequestedAtUtc = x.CancellationRequestedAtUtc,
                    RetryCount = x.RetryCount,
                    SummaryJson = x.SummaryJson,
                    ErrorMessage = x.ErrorMessage,
                    DurationSeconds = x.DurationSeconds,
                    TotalImported = x.TotalImported,
                    TotalUpdated = x.TotalUpdated,
                    TotalErrors = x.TotalErrors,
                    DataOrigin = x.DataOrigin
                })
                .FirstOrDefaultAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            // Legacy DB compatibility: projection may miss newer metrics columns.
            _logger.LogWarning(
                ex,
                "Access import batch lookup hit legacy schema (missing columns). BatchId: {BatchId}.",
                batchId);

            return await _trendDb.DataImportBatches
                .AsNoTracking()
                .Where(x => x.Id == batchId)
                .Select(x => new AccessImportBatchDto
                {
                    Id = x.Id,
                    SourceSystem = x.SourceSystem,
                    SourceFileName = x.SourceFileName,
                    QueuedAtUtc = x.StartedAtUtc,
                    StartedAtUtc = x.StartedAtUtc,
                    CompletedAtUtc = x.CompletedAtUtc,
                    LastHeartbeatUtc = null,
                    Status = x.Status,
                    CurrentStep = null,
                    CurrentTable = null,
                    ProgressPercent = 0,
                    RowsRead = 0,
                    RowsAccepted = 0,
                    RowsWritten = 0,
                    CancellationRequested = false,
                    CancellationRequestedAtUtc = null,
                    RetryCount = 0,
                    SummaryJson = x.SummaryJson,
                    ErrorMessage = x.ErrorMessage,
                    DurationSeconds = null,
                    TotalImported = 0,
                    TotalUpdated = 0,
                    TotalErrors = 0,
                    DataOrigin = "access"
                })
                .FirstOrDefaultAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // No DataImportBatches table yet.
            _logger.LogWarning(
                ex,
                "Access import batch lookup could not find DataImportBatches table. BatchId: {BatchId}.",
                batchId);
            return null;
        }
        catch (Exception ex) when (IsTransientDatabaseTimeout(ex))
        {
            _logger.LogWarning(
                ex,
                "Access import batch lookup hit a transient database issue. BatchId: {BatchId}.",
                batchId);
            return null;
        }
    }

    public async Task<bool> RequestCancellationAsync(long batchId, CancellationToken ct = default)
    {
        if (batchId <= 0)
            return false;

        await EnsureDataImportBatchesTableAsync(ct);

        var now = DateTime.UtcNow;
        const string sql = """
            UPDATE "DataImportBatches"
            SET "CancellationRequested" = TRUE,
                "CancellationRequestedAtUtc" = COALESCE("CancellationRequestedAtUtc", @p1),
                "LastHeartbeatUtc" = COALESCE("LastHeartbeatUtc", @p1),
                "Status" = CASE
                    WHEN "Status" = 'pending' THEN 'cancelled'
                    ELSE "Status"
                END,
                "CompletedAtUtc" = CASE
                    WHEN "Status" = 'pending' THEN @p1
                    ELSE "CompletedAtUtc"
                END,
                "ProgressPercent" = CASE
                    WHEN "Status" = 'pending' THEN 100
                    ELSE "ProgressPercent"
                END,
                "ErrorMessage" = CASE
                    WHEN "Status" = 'pending' AND COALESCE(NULLIF("ErrorMessage", ''), '') = '' THEN @p2
                    ELSE "ErrorMessage"
                END
            WHERE "Id" = @p0
              AND "Status" IN ('pending', 'running');
            """;

        var affected = await _trendDb.Database.ExecuteSqlRawAsync(
            sql,
            new object[] { batchId, now, "Cancellation requested by user." },
            ct);

        if (affected > 0)
        {
            _logger.LogInformation(
                "Access import cancellation requested. BatchId: {BatchId}.",
                batchId);
        }

        return affected > 0;
    }

    public async Task MarkBatchInterruptedAsync(long batchId, CancellationToken ct = default)
    {
        if (batchId <= 0)
            return;

        try
        {
            var batch = await _trendDb.DataImportBatches.FindAsync(new object[] { batchId }, ct);
            if (batch is null)
                return;

            var now = DateTime.UtcNow;
            batch.Status = "interrupted";
            batch.CurrentStep = "stopped";
            batch.CurrentTable = null;
            batch.CompletedAtUtc = now;
            batch.LastHeartbeatUtc = now;
            batch.ProgressPercent = 100;
            batch.DurationSeconds = (int)Math.Max(0, Math.Round((now - batch.StartedAtUtc).TotalSeconds));
            if (string.IsNullOrWhiteSpace(batch.ErrorMessage))
                batch.ErrorMessage = "Import interrupted during worker shutdown.";
            if (string.IsNullOrWhiteSpace(batch.ErrorDetailsJson))
            {
                batch.ErrorDetailsJson = JsonSerializer.Serialize(new
                {
                    type = typeof(OperationCanceledException).FullName,
                    message = "Access import worker shutdown interrupted batch execution."
                });
            }
            batch.TotalErrors = Math.Max(1, batch.TotalErrors);

            await _trendDb.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Marked Access import batch as interrupted. BatchId: {BatchId}. StartedAtUtc: {StartedAtUtc}. CompletedAtUtc: {CompletedAtUtc}.",
                batchId,
                batch.StartedAtUtc,
                batch.CompletedAtUtc);
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UndefinedTable ||
            ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogDebug(
                ex,
                "Skipping mark-interrupted because DataImportBatches compatibility columns are not available yet. BatchId: {BatchId}.",
                batchId);
        }
    }

    private static bool IsTransientDatabaseTimeout(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException)
                return true;

            if (current is NpgsqlException npgsqlException &&
                npgsqlException is not PostgresException)
                return true;
        }

        return false;
    }

    private async Task EnsureBatchNotCancelledAsync(long batchId, CancellationToken ct)
    {
        if (batchId <= 0)
            return;

        var isCancelled = await _trendDb.DataImportBatches
            .AsNoTracking()
            .Where(x => x.Id == batchId)
            .Select(x => x.CancellationRequested)
            .FirstOrDefaultAsync(ct);

        if (isCancelled)
            throw new AccessImportCancellationRequestedException(batchId);
    }

    internal static bool IsRunningBatchStale(DateTime startedAtUtc, DateTime utcNow, int staleAfterMinutes)
        => IsRunningBatchStale(startedAtUtc, lastHeartbeatUtc: null, utcNow, staleAfterMinutes);

    internal static bool IsRunningBatchStale(DateTime startedAtUtc, DateTime? lastHeartbeatUtc, DateTime utcNow, int staleAfterMinutes)
    {
        var safeWindowMinutes = Math.Max(15, staleAfterMinutes);
        var referenceTime = lastHeartbeatUtc ?? startedAtUtc;
        return referenceTime <= utcNow.AddMinutes(-safeWindowMinutes);
    }

    private int GetRunningBatchStaleMinutes()
        => Math.Max(15, _options.RunningBatchStaleMinutes);

    private static string BuildStaleBatchErrorMessage(int staleAfterMinutes)
        => $"Access import batch was marked as failed after exceeding the stale recovery window of {staleAfterMinutes} minutes. The background worker likely stopped before completion.";

    private async Task RecoverStaleRunningBatchesAsync(long? batchId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var staleAfterMinutes = GetRunningBatchStaleMinutes();

        try
        {
            var staleQuery = _trendDb.DataImportBatches
                .AsNoTracking()
                .Where(x => (x.Status == "running" || x.Status == "pending") && x.CompletedAtUtc == null);

            if (batchId.HasValue)
                staleQuery = staleQuery.Where(x => x.Id == batchId.Value);

            var staleCandidates = await staleQuery
                .Where(x => (x.LastHeartbeatUtc ?? x.StartedAtUtc) <= now.AddMinutes(-staleAfterMinutes))
                .Select(x => new RunningBatchSnapshot(x.Id, x.SourceFileName, x.Status, x.StartedAtUtc, x.LastHeartbeatUtc))
                .ToListAsync(ct);

            if (staleCandidates.Count == 0)
                return;

            foreach (var staleBatch in staleCandidates)
            {
                var errorMessage = BuildStaleBatchErrorMessage(staleAfterMinutes);
                var result = new AccessImportRunResponse
                {
                    BatchId = staleBatch.Id,
                    Status = "failed",
                    SourceFileName = staleBatch.SourceFileName,
                    StartedAtUtc = staleBatch.StartedAtUtc,
                    CompletedAtUtc = now
                };
                result.Warnings.Add(errorMessage);

                const string sql = """
                    UPDATE "DataImportBatches"
                    SET "Status" = @p0,
                        "CompletedAtUtc" = @p1,
                        "LastHeartbeatUtc" = @p1,
                        "ErrorMessage" = COALESCE(NULLIF("ErrorMessage", ''), @p2),
                        "SummaryJson" = COALESCE("SummaryJson", @p3)
                    WHERE "Id" = @p4
                      AND "Status" IN ('running', 'pending')
                      AND "CompletedAtUtc" IS NULL;
                    """;

                await _trendDb.Database.ExecuteSqlRawAsync(
                    sql,
                    new object[]
                    {
                        "failed",
                        now,
                        errorMessage,
                        JsonSerializer.Serialize(result),
                        staleBatch.Id
                    },
                    cancellationToken: ct);

                _logger.LogWarning(
                    "Recovered stale Access import batch. BatchId: {BatchId}. SourceFileName: {SourceFileName}. StartedAtUtc: {StartedAtUtc}. LastHeartbeatUtc: {LastHeartbeatUtc}. RecoveryWindowMinutes: {RecoveryWindowMinutes}.",
                    staleBatch.Id,
                    staleBatch.SourceFileName,
                    staleBatch.StartedAtUtc,
                    staleBatch.LastHeartbeatUtc,
                    staleAfterMinutes);
            }
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UndefinedTable ||
            ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogDebug(
                ex,
                "Skipping Access import stale batch recovery because DataImportBatches compatibility columns are not fully available yet.");
        }
    }

    private async Task<RunningBatchSnapshot?> GetActiveRunningBatchAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var staleAfterMinutes = GetRunningBatchStaleMinutes();

        try
        {
            return await _trendDb.DataImportBatches
                .AsNoTracking()
                .Where(x => (x.Status == "running" || x.Status == "pending") &&
                            x.CompletedAtUtc == null &&
                            (x.LastHeartbeatUtc ?? x.StartedAtUtc) > now.AddMinutes(-staleAfterMinutes))
                .OrderByDescending(x => x.StartedAtUtc)
                .Select(x => new RunningBatchSnapshot(x.Id, x.SourceFileName, x.Status, x.StartedAtUtc, x.LastHeartbeatUtc))
                .FirstOrDefaultAsync(ct);
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UndefinedTable ||
            ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogDebug(
                ex,
                "Skipping Access import active batch check because DataImportBatches compatibility columns are not fully available yet.");
            return null;
        }
    }

    private sealed record DeleteBatchHeader(
        long Id,
        string SourceFileName);

    public async Task<DeleteBatchResult> DeleteBatchAsync(long batchId, bool includeAnalytics = true, CancellationToken ct = default)
    {
        var batch = await GetDeleteBatchHeaderAsync(batchId, ct);
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
        var pvStavkeDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.PovracajStavke
                .Where(s => _trendDb.PovracajZaglavlja
                    .Where(z => z.DataOrigin == "access")
                    .Select(z => z.Id)
                    .Contains(s.IdPovracaj))
                .ExecuteDeleteAsync(ct),
            "povracaj_stavke",
            "delete-batch",
            batchId);
        var pvDeleted2 = await ExecuteDeleteCompatAsync(
            () => _trendDb.PovracajZaglavlja.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "povracaj_zaglavlje",
            "delete-batch",
            batchId);
        var dnDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.DnevnikPromena.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "DnevnikPromena",
            "delete-batch",
            batchId);
        var svDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.ProdajaStavke
                .Where(s => _trendDb.ProdajaZaglavlja
                    .Where(z => z.DataOrigin == "access")
                    .Select(z => z.Id)
                    .Contains(s.IdProdaja))
                .ExecuteDeleteAsync(ct),
            "prodaja_stavke",
            "delete-batch",
            batchId);
        var pvDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.ProdajaZaglavlja.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "prodaja_zaglavlje",
            "delete-batch",
            batchId);
        var arDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.Artikli.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "Artikli",
            "delete-batch",
            batchId);
        var seDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.Sezone.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "Sezone",
            "delete-batch",
            batchId);
        var doDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.Dobavljaci.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "Dobavljaci",
            "delete-batch",
            batchId);
        var tiDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.TipoviObuce.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "TipoviObuce",
            "delete-batch",
            batchId);

        if (includeAnalytics)
        {
            var accessStoreIds = await LoadAccessStoreIdsCompatAsync(batchId, ct);

            // Delete analytics data imported from Access (DataOrigin="access")
            // Note: per-batch FK does not exist in analytics tables, so this removes all Access-origin rows.
            sfDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.SalesFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "SalesFacts",
                "delete-batch-analytics",
                batchId);
            slfDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.SalesLineFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "SalesLineFacts",
                "delete-batch-analytics",
                batchId);
            pdDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.ProductsDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "ProductsDim",
                "delete-batch-analytics",
                batchId);
            imDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.InventoryMovementFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "InventoryMovementFacts",
                "delete-batch-analytics",
                batchId);
            suppDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.SuppliersDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "SuppliersDim",
                "delete-batch-analytics",
                batchId);
            seasDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.SeasonsDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "SeasonsDim",
                "delete-batch-analytics",
                batchId);
            typeDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.FootwearTypesDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "FootwearTypesDim",
                "delete-batch-analytics",
                batchId);
            storeDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.StoresDim
                    .Where(x => (x.DataOrigin == "access" || accessStoreIds.Contains(x.StoreId))
                                && !_analyticsDb.SalesFacts.Any(sf => sf.StoreId == x.StoreId))
                    .ExecuteDeleteAsync(ct),
                "StoresDim",
                "delete-batch-analytics",
                batchId);
        }

        await ExecuteDeleteCompatAsync(
            () => _trendDb.AccessImportLogs.Where(x => x.BatchId == batchId).ExecuteDeleteAsync(ct),
            "AccessImportLog",
            "delete-batch-logs",
            batchId);
        await ExecuteDeleteCompatAsync(
            () => _trendDb.DataImportBatches.Where(x => x.Id == batchId).ExecuteDeleteAsync(ct),
            "DataImportBatches",
            "delete-batch-record",
            batchId);

        var cacheInvalidated = false;
        if (includeAnalytics && _analyticsCache is not null)
        {
            await _analyticsCache.RemoveByPrefixAsync(AnalyticsCacheKeys.Prefix, ct);
            cacheInvalidated = true;
        }

        _logger.LogInformation(
            "Deleted access-import batch {BatchId}: artikli={Ar}, prodaja={Pv}/{Sv}, dnevnik={Dn}, povracaj={Pv2}/{PvS}, sezone={Se}, dobavljaci={Do}, tipovi={Ti}, analytics={IncludeAnalytics} pd={Pd}/sf={Sf}/slf={Slf}/im={Im}/sup={Sup}/seas={Seas}/types={Types}/stores={Stores}, cacheInvalidated={CacheInvalidated}. TableName: {TableName}. Operation: {Operation}.",
            batchId, arDeleted, pvDeleted, svDeleted, dnDeleted, pvDeleted2, pvStavkeDeleted, seDeleted, doDeleted, tiDeleted, includeAnalytics, pdDeleted, sfDeleted, slfDeleted, imDeleted, suppDeleted, seasDeleted, typeDeleted, storeDeleted, cacheInvalidated, "all", "delete-batch");

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

    private async Task<DeleteBatchHeader?> GetDeleteBatchHeaderAsync(long batchId, CancellationToken ct)
    {
        try
        {
            return await _trendDb.DataImportBatches
                .AsNoTracking()
                .Where(x => x.Id == batchId)
                .Select(x => new DeleteBatchHeader(x.Id, x.SourceFileName))
                .FirstOrDefaultAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogWarning(
                ex,
                "Delete batch lookup hit legacy schema (missing columns). BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Falling back to compatibility projection.",
                batchId,
                "DataImportBatches",
                "delete-batch-lookup");

            return await _trendDb.DataImportBatches
                .AsNoTracking()
                .Where(x => x.Id == batchId)
                .Select(x => new DeleteBatchHeader(x.Id, x.SourceFileName))
                .FirstOrDefaultAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogWarning(
                ex,
                "Delete batch lookup could not find DataImportBatches table. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}.",
                batchId,
                "DataImportBatches",
                "delete-batch-lookup");
            return null;
        }
    }

    private async Task<List<int>> LoadAccessStoreIdsCompatAsync(long batchId, CancellationToken ct)
    {
        try
        {
            return await _analyticsDb.SalesFacts
                .Where(x => x.DataOrigin == "access")
                .Select(x => x.StoreId)
                .Distinct()
                .ToListAsync(ct);
        }
        catch (PostgresException ex) when (IsLegacySchemaArtifact(ex))
        {
            _logger.LogWarning(
                ex,
                "Access analytics store lookup skipped because legacy schema is missing tables or columns. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}.",
                batchId,
                "SalesFacts",
                "delete-batch-analytics");
            return [];
        }
    }

    private async Task<int> ExecuteDeleteCompatAsync(Func<Task<int>> deleteAction, string tableName, string operation, long batchId)
    {
        try
        {
            return await deleteAction();
        }
        catch (PostgresException ex) when (IsLegacySchemaArtifact(ex))
        {
            _logger.LogWarning(
                ex,
                "Skipping delete for legacy schema artifact. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}.",
                batchId,
                tableName,
                operation);
            return 0;
        }
    }

    private static bool IsLegacySchemaArtifact(PostgresException ex)
        => ex.SqlState is PostgresErrorCodes.UndefinedColumn or PostgresErrorCodes.UndefinedTable;

    private async Task ImportTrendplusAsync(
        IAccessDataReaderSession session,
        bool overwriteExisting,
        bool includeTemporaryTables,
        AccessImportRunResponse result,
        CancellationToken ct)
    {
        var tables = await session.GetTablesAsync(includeTemporaryTables, ct);
        var tipovi        = await FindTableAsync(session, tables, TipoviCandidates, ct: ct);
        var dobavljaci    = await FindTableAsync(session, tables, DobavljaciCandidates, ct: ct);
        var sezone        = await FindTableAsync(session, tables, SezoneCandidates, sigRequired: ["idsezona", "naziv"], ct: ct);
        var artikli       = await FindTableAsync(session, tables, ArtikliCandidates, sigRequired: ["idartikal", "naziv"], sigBonus: ["nabavnacena", "prodajnacena", "plu"], ct: ct);
        var prodaja       = await FindTableAsync(session, tables, ProdajaCandidates, ct: ct);
        var prodajaStavke = await FindTableAsync(session, tables, ProdajaStavkeCandidates, ct: ct);
        var dnevnik       = await FindTableAsync(session, tables, DnevnikPromenaCandidates, sigRequired: ["iddnevnik", "datum"], ct: ct);
        var povracaj      = await FindTableAsync(session, tables, PovracajCandidates, ct: ct);
        var povracajStavke = await FindTableAsync(session, tables, PovracajStavkeCandidates2, ct: ct);
        var nivelacije    = await FindTableAsync(session, tables, NivelacijeCandidates, sigRequired: ["idartikal", "novacena"], ct: ct);
        var unosRobe      = await FindTableAsync(session, tables, UnosRobeCandidates, sigRequired: ["idartikal", "kolicina", "iddobavljac"], ct: ct);
        var povratnice    = await FindTableAsync(session, tables, PovratniceCandidates, sigRequired: ["idartikal", "kolicina"], sigBonus: ["razlog", "idpovratnice"], ct: ct);
        var prenosRobe    = await FindTableAsync(session, tables, PrenosRobeCandidates, sigRequired: ["idartikal", "kolicina"], sigBonus: ["idobjekatiz", "idobjekatulaz", "idobjekat"], ct: ct);
        var objekti       = await FindTableAsync(session, tables, ObjekatCandidates, sigRequired: ["idobjekat", "nazivobjekta"], ct: ct);

        if (artikli is null)
            throw new InvalidOperationException("Nije pronaÄ‘ena tabela za artikle u ACCDB fajlu.");

        if (tipovi is not null)
            await RunImportStepAsync("import", "tipovi_obuce", tipovi, result, async innerCt =>
            {
                await ImportTipoviAsync(session, tipovi, overwriteExisting, result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);

        if (dobavljaci is not null)
            await RunImportStepAsync("import", "dobavljaci", dobavljaci, result, async innerCt =>
            {
                await ImportDobavljaciAsync(session, dobavljaci, overwriteExisting, result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);

        if (sezone is not null)
            await RunImportStepAsync("import", "sezone", sezone, result, async innerCt =>
            {
                await ImportSezoneAsync(session, sezone, overwriteExisting, result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);

        if (objekti is not null)
            await RunImportStepAsync("import", "objekti", objekti, result, innerCt =>
                ImportObjektiAsync(session, objekti, overwriteExisting, result, innerCt), ct);

        await RunImportStepAsync("import", "artikli", artikli, result, async innerCt =>
        {
            await ImportArtikliAsync(session, artikli, overwriteExisting, result, innerCt);
            await FlushTrendWritesAsync(force: true, innerCt);
        }, ct);

        if (dnevnik is not null)
            await RunImportStepAsync("import", "dnevnik_promena", dnevnik, result, async innerCt =>
            {
                await ImportDnevnikPromenaAsync(session, dnevnik, overwriteExisting, result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);

        var importedProdajaFromLineTable = false;
        var synthesizedProdajaFromDnevnik = false;
        if (prodaja is not null && await IsProdajaLineTableAsync(session, prodaja, ct))
        {
            importedProdajaFromLineTable = true;
            result.Warnings.Add($"Tabela '{prodaja}' prepoznata je kao tabela stavki prodaje (IDDnevnik/IDArtikal). Uvozim prodaju kroz vezu sa DnevnikPromena.");
            await RunImportStepAsync("import-line-table", "prodaja_zaglavlje", prodaja, result, async innerCt =>
            {
                await ImportProdajaFromLineTableAsync(session, prodaja, overwriteExisting, result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);
        }
        else
        {
            if (prodaja is not null)
                await RunImportStepAsync("import", "prodaja_zaglavlje", prodaja, result, async innerCt =>
                {
                    await ImportProdajaAsync(session, prodaja, overwriteExisting, result, innerCt);
                    await FlushTrendWritesAsync(force: true, innerCt);
                }, ct);
            else if (dnevnik is not null)
            {
                synthesizedProdajaFromDnevnik = true;
                await RunImportStepAsync("synthesize", "prodaja_zaglavlje", dnevnik, result, async innerCt =>
                {
                    await SynthesizeProdajaFromDnevnikAsync(overwriteExisting, result, innerCt);
                    await FlushTrendWritesAsync(force: true, innerCt);
                }, ct);
            }

                if (prodajaStavke is not null)
                await RunImportStepAsync("import", "prodaja_stavke", prodajaStavke, result, async innerCt =>
                {
                    await ImportProdajaStavkeAsync(session, prodajaStavke, prodaja, overwriteExisting, result, innerCt);
                    await FlushTrendWritesAsync(force: true, innerCt);
                }, ct);
        }

        if (povracaj is not null)
            await RunImportStepAsync("import", "povracaj_zaglavlje", povracaj, result, async innerCt =>
            {
                await ImportPovracajAsync(session, povracaj, overwriteExisting, result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);

        if (povracajStavke is not null)
            await RunImportStepAsync("import", "povracaj_stavke", povracajStavke, result, async innerCt =>
            {
                await ImportPovracajStavkeAsync(session, povracajStavke, overwriteExisting, result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);

        if (nivelacije is not null)
            await RunImportStepAsync("import", "nivelacije", nivelacije, result, innerCt =>
                ImportNivelacijeAsync(session, nivelacije, overwriteExisting, result, innerCt), ct);
        if (unosRobe is not null)
            await RunImportStepAsync("import", "unos_robe", unosRobe, result, innerCt =>
                ImportUnosRobeAsync(session, unosRobe, overwriteExisting, result, innerCt), ct);
        if (povratnice is not null)
            await RunImportStepAsync("import", "povratnice", povratnice, result, innerCt =>
                ImportPovratniceAsync(session, povratnice, overwriteExisting, result, innerCt), ct);
        if (prenosRobe is not null)
            await RunImportStepAsync("import", "prenos_robe", prenosRobe, result, innerCt =>
                ImportPrenosRobeAsync(session, prenosRobe, overwriteExisting, result, innerCt), ct);
        await FlushTrendWritesAsync(force: true, ct);

        if (prodaja is null && dnevnik is not null && !importedProdajaFromLineTable && !synthesizedProdajaFromDnevnik)
            await RunImportStepAsync("synthesize", "prodaja_zaglavlje", dnevnik, result, async innerCt =>
            {
                await SynthesizeProdajaFromDnevnikAsync(overwriteExisting, result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);

        var sourceRowsByTable = result.CoverageByTable
            .Where(x => x.Value.SourceRows > 0)
            .ToDictionary(x => x.Key, x => x.Value.SourceRows, StringComparer.OrdinalIgnoreCase);

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
                result.Warnings.Add($"[coverage] Tabela '{key}' ima {sourceRows} redova u Access bazi, ali 0 upisanih/azuriranih redova. Proveri mapiranje i quality podataka.");
        }
    }

    private async Task RunImportStepAsync(
        string step,
        string tableKey,
        string? tableName,
        AccessImportRunResponse result,
        Func<CancellationToken, Task> action,
        CancellationToken ct)
    {
        await EnsureBatchNotCancelledAsync(result.BatchId, ct);

        var beforeMetric = result.CoverageByTable.TryGetValue(tableKey, out var existingMetric)
            ? new AccessImportCoverageMetric
            {
                SourceRows = existingMetric.SourceRows,
                AcceptedRows = existingMetric.AcceptedRows,
                TargetWrites = existingMetric.TargetWrites
            }
            : new AccessImportCoverageMetric();
        var beforeImported = GetImportedRowCount(result, tableKey);
        var started = DateTime.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        SetBatchProgressContext(step, tableName ?? tableKey);

        _logger.LogInformation(
            "Access import step started. Step: {Step}. TableKey: {TableKey}. TableName: {TableName}. StartedAtUtc: {StartedAtUtc}.",
            step,
            tableKey,
            tableName ?? "<none>",
            started);
        await PersistBatchProgressAsync("step-start", force: true, ct);

        await action(ct);
        await EnsureBatchNotCancelledAsync(result.BatchId, ct);

        sw.Stop();
        var afterMetric = result.CoverageByTable.TryGetValue(tableKey, out var metric)
            ? metric
            : new AccessImportCoverageMetric();
        var afterImported = GetImportedRowCount(result, tableKey);

        _logger.LogInformation(
            "Access import step completed. Step: {Step}. TableKey: {TableKey}. TableName: {TableName}. DurationMs: {DurationMs}. SourceRows: {SourceRows}. AcceptedRows: {AcceptedRows}. RowsWritten: {RowsWritten}. BatchSize: {BatchSize}.",
            step,
            tableKey,
            tableName ?? "<none>",
            sw.ElapsedMilliseconds,
            Math.Max(0, afterMetric.SourceRows - beforeMetric.SourceRows),
            Math.Max(0, afterMetric.AcceptedRows - beforeMetric.AcceptedRows),
            Math.Max(0, afterImported - beforeImported),
            _options.DbSaveBatchSize);
        await PersistBatchProgressAsync("step-complete", force: true, ct);
    }

    private static int GetImportedRowCount(AccessImportRunResponse result, string key)
        => key.ToLowerInvariant() switch
        {
            "tipovi_obuce" => result.TipoviInserted + result.TipoviUpdated,
            "dobavljaci" => result.DobavljaciInserted + result.DobavljaciUpdated,
            "sezone" => result.SezoneInserted + result.SezoneUpdated,
            "objekti" => result.ObjekatInserted + result.ObjekatUpdated,
            "artikli" => result.ArtikliInserted + result.ArtikliUpdated,
            "dnevnik_promena" => result.DnevnikInserted + result.DnevnikUpdated,
            "prodaja_zaglavlje" => result.ProdajaInserted + result.ProdajaUpdated,
            "prodaja_stavke" => result.ProdajaStavkeInserted + result.ProdajaStavkeUpdated,
            "povracaj_zaglavlje" => result.PovracajInserted + result.PovracajUpdated,
            "povracaj_stavke" => result.PovracajStavkeInserted + result.PovracajStavkeUpdated,
            "nivelacije" => result.NivelacijeInserted,
            "unos_robe" => result.UnosRobeInserted,
            "povratnice" => result.PovratnicaInserted,
            "prenos_robe" => result.PrenosRobeInserted,
            _ => 0
        };

    private async Task ImportTipoviAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var existing = ToFirstDictionary(_trendDb.TipoviObuce.AsNoTracking().ToList(), x => x.Id);
        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "tipovi_obuce");
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
                TrackTrendWrite();
            }
            else if (overwriteExisting)
            {
                e.Naziv = naziv!;
                e.DataOrigin = "access";
                _trendDb.TipoviObuce.Update(e);
                result.TipoviUpdated++;
                TrackTrendWrite();
            }

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportDobavljaciAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var existing = ToFirstDictionary(_trendDb.Dobavljaci.AsNoTracking().ToList(), x => x.Id);
        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "dobavljaci");
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
                TrackTrendWrite();
            }
            else if (overwriteExisting)
            {
                e.Naziv = naziv;
                e.Adresa = S(row, "adresa", "address");
                e.Telefon = S(row, "telefon", "phone", "brteldob", "brteldobav", "tel", "br_tel", "mobilni", "mobile");
                e.Napomena = S(row, "napomena", "note");
                e.DataOrigin = "access";
                _trendDb.Dobavljaci.Update(e);
                result.DobavljaciUpdated++;
                TrackTrendWrite();
            }

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportSezoneAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var existing = ToFirstDictionary(_trendDb.Sezone.AsNoTracking().ToList(), x => x.Id);
        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "sezone");
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
                TrackTrendWrite();
            }
            else if (overwriteExisting)
            {
                e.Naziv = naziv!;
                e.DatumOd = datumOd;
                e.DatumDo = datumDo;
                e.DataOrigin = "access";
                _trendDb.Sezone.Update(e);
                result.SezoneUpdated++;
                TrackTrendWrite();
            }

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportObjektiAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "objekti");
            var id = I(row, "id", "idobjekat", "storeid", "idobjekta", "poslovnicaid");
            if (!id.HasValue || id.Value <= 0) continue;
            MarkAccepted(result, "objekti");
            var naziv = S(row, "nazivobjekta", "naziv", "storename", "name", "poslovnica",
                          "ime", "opisobjekta") ?? $"Objekat {id.Value}";
            _importedStores[id.Value] = (
                Name: naziv,
                Address: S(row, "adresa", "address", "ulica"),
                Phone: S(row, "telefon", "phone", "tel", "mobilni"),
                Manager: S(row, "menedzer", "manager", "rukovodilac", "vodja", "direktorfiliajle"));
            result.ObjekatInserted++;
        }
    }

    private async Task ImportArtikliAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var preloadSw = System.Diagnostics.Stopwatch.StartNew();
        var existingIds = (await _trendDb.Artikli
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(ct))
            .ToHashSet();
        preloadSw.Stop();

        _logger.LogInformation(
            "Access import artikli existing-id preload completed. ExistingCount: {ExistingCount}. DurationMs: {DurationMs}. TableName: {TableName}. Operation: {Operation}.",
            existingIds.Count,
            preloadSw.ElapsedMilliseconds,
            table,
            "artikli-id-preload");

        var usedIds = existingIds.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        var trackedCurrentBatch = new Dictionary<int, Artikli>();
        var artikliSw = Stopwatch.StartNew();
        var sourceRows = 0;
        var acceptedRows = 0;
        var insertedBeforeLoop = result.ArtikliInserted;
        var updatedBeforeLoop = result.ArtikliUpdated;

        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            sourceRows++;
            MarkSourceRow(result, "artikli");
            var naziv = S(row,
                "naziv", "nazivartikal", "nazivarticle", "nazivproizvoda",
                "opis", "opisartikal", "opisproizvoda", "description", "desc",
                "proizvod", "name", "productname", "articlename", "itemname", "ime",
                "artikal", "article", "item", "roba");
            if (string.IsNullOrWhiteSpace(naziv)) continue;
            MarkAccepted(result, "artikli");
            acceptedRows++;
            var id = I(row, "id", "idartikal", "productid");

            Artikli? e = null;
            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && trackedCurrentBatch.TryGetValue(sourceId, out var tracked))
                e = tracked;

            var isInsert = false;
            if (e is null)
            {
                var assignedId = sourceId;
                if (assignedId > 0 && existingIds.Contains(assignedId))
                {
                    if (!overwriteExisting)
                        continue;

                    e = new Artikli { Id = assignedId };
                    _trendDb.Artikli.Attach(e);
                    trackedCurrentBatch[assignedId] = e;
                    result.ArtikliUpdated++;
                }
                else
                {
                    if (assignedId <= 0 || usedIds.Contains(assignedId))
                        assignedId = AllocateNextId(usedIds, ref nextGeneratedId);
                    else
                        usedIds.Add(assignedId);

                    e = new Artikli { Id = assignedId };
                    _trendDb.Artikli.Add(e);
                    trackedCurrentBatch[assignedId] = e;
                    existingIds.Add(assignedId);
                    result.ArtikliInserted++;
                    isInsert = true;
                }
            }
            else
            {
                if (!overwriteExisting)
                    continue;

                result.ArtikliUpdated++;
            }

            ApplyArtikliValues(e, row, naziv!);

            if (!isInsert)
                _trendDb.Artikli.Update(e);

            TrackTrendWrite();
            var shouldClearTrackedBatch = _pendingTrendWrites >= Math.Max(1, _options.DbSaveBatchSize);
            await FlushTrendWritesAsync(force: false, ct);
            if (shouldClearTrackedBatch)
                trackedCurrentBatch.Clear();

            if (acceptedRows == 1 || acceptedRows % ArtikliProgressLogInterval == 0)
            {
                _logger.LogInformation(
                    "Access import artikli progress. SourceRows: {SourceRows}. AcceptedRows: {AcceptedRows}. InsertedDelta: {InsertedDelta}. UpdatedDelta: {UpdatedDelta}. PendingTrendWrites: {PendingTrendWrites}. TrackedBatchSize: {TrackedBatchSize}. TableName: {TableName}. ElapsedMs: {ElapsedMs}. Operation: {Operation}.",
                    sourceRows,
                    acceptedRows,
                    result.ArtikliInserted - insertedBeforeLoop,
                    result.ArtikliUpdated - updatedBeforeLoop,
                    _pendingTrendWrites,
                    trackedCurrentBatch.Count,
                    table,
                    artikliSw.ElapsedMilliseconds,
                    "artikli-progress");
                await PersistBatchProgressAsync("artikli-progress", force: false, ct);
            }
        }

        _logger.LogInformation(
            "Access import artikli completed. SourceRows: {SourceRows}. AcceptedRows: {AcceptedRows}. InsertedDelta: {InsertedDelta}. UpdatedDelta: {UpdatedDelta}. ExistingCount: {ExistingCount}. DurationMs: {DurationMs}. TableName: {TableName}. Operation: {Operation}.",
            sourceRows,
            acceptedRows,
            result.ArtikliInserted - insertedBeforeLoop,
            result.ArtikliUpdated - updatedBeforeLoop,
            existingIds.Count,
            artikliSw.ElapsedMilliseconds,
            table,
            "artikli-complete");
    }

    private static void ApplyArtikliValues(Artikli entity, AccessDataRow row, string naziv)
    {
        entity.PLU = S(row, "plu", "sku", "sifra", "sifraartikla", "barcode", "barkod", "kod", "code", "artikal");
        entity.Naziv = naziv;
        entity.IDTipObuce = I(row, "idtipobuce", "tipobuceid", "footweartypeid");
        entity.IDDobavljac = I(row, "iddobavljac", "dobavljacid", "supplierid");
        entity.NabavnaCena = D(row, "nabavnacena", "purchaseprice", "cost");
        entity.NabavnaCenaDin = D(row, "nabavnacenadin", "purchasepricersd");
        entity.PrvaProdajnaCena = D(row, "prvaprodajnacena", "firstsaleprice");
        entity.ProdajnaCena = D(row, "prodajnacena", "saleprice", "price");
        entity.Velicina = S(row, "velicina", "size");
        entity.Boja = S(row, "boja", "color");
        entity.Materijal = S(row, "materijal", "material", "materijal_gornjista", "gornjiste",
            "upper", "fabric", "sastav", "sastav_gornjista");
        entity.Kolicina = I(row, "kolicina", "kol", "qty", "quantity", "stock", "stanje", "stanjeartikla",
            "stanjeartikal", "lager", "zaliha", "zalihe", "raspolozivo", "inventar",
            "stockqty", "totalqty", "total_qty", "raspolozivokolicina");
        entity.MinimalnaKolicina = I(row, "minimalnakolicina", "minimumqty", "minqty", "minstock");
        entity.Komentar = S(row, "komentar", "comment", "napomena", "url");
        entity.IDObjekat = I(row, "idobjekat", "storeid");
        entity.IDSezona = I(row, "idsezona", "seasonid");
        entity.Kategorija = S(row, "kategorija", "category");
        entity.Pol = S(row, "pol", "gender");
        entity.ImagePath = S(row, "imagepath", "imageurl", "slika", "image");
        entity.UpdatedAt = DateTime.UtcNow;
        entity.DataOrigin = "access";
    }

    private async Task ImportProdajaAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var existing = ToFirstDictionary(_trendDb.ProdajaZaglavlja.AsNoTracking().ToList(), x => x.Id);
        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "prodaja_zaglavlje");
            var id = I(row, "id", "idprodaja", "saleid", "iddnevnik");
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
                TrackTrendWrite();
            }
            else if (overwriteExisting)
            {
                e.BrojRacuna = S(row, "brojracuna", "brojkalkulacije", "invoice", "receiptnumber");
                e.DatumProdaje = DT(row, "datumprodaje", "datum", "saledate") ?? DateTime.UtcNow;
                e.NacinPlacanja = S(row, "nacinplacanja", "paymenttype");
                e.IDObjekat = I(row, "idobjekat", "storeid");
                e.KorisnikIme = S(row, "korisnikime", "korisnik", "username", "operater", "kasir");
                e.DataOrigin = "access";
                _trendDb.ProdajaZaglavlja.Update(e);
                result.ProdajaUpdated++;
                TrackTrendWrite();
            }

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportProdajaStavkeAsync(IAccessDataReaderSession session, string table, string? parentTable, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var existing = ToFirstDictionary(_trendDb.ProdajaStavke.AsNoTracking().ToList(), x => x.Id);
        var usedIds = existing.Keys.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
            var saleIds = (await _trendDb.ProdajaZaglavlja
                    .AsNoTracking()
                    .Select(x => x.Id)
                    .ToListAsync(ct))
                .ToHashSet();
            // Include any prodaja_zaglavlje entities that were added to the current DbContext
            // but not yet flushed to the database (pending parents created earlier in this import).
            var pendingFromTracker = _trendDb.ChangeTracker
                .Entries<Domain.Model.Prodaja.ProdajaZaglavlje>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity.Id)
                .ToHashSet();
            if (pendingFromTracker.Count > 0)
                saleIds.UnionWith(pendingFromTracker);
        // Pre-scan Access parent IDs if parentTable provided
        HashSet<int> accessSaleIds = new();
        if (!string.IsNullOrWhiteSpace(parentTable))
        {
            accessSaleIds = await ReadIdsFromAccessTableAsync(session, parentTable, ct);
            var missingSaleIds = accessSaleIds.Except(saleIds).ToList();
            if (missingSaleIds.Count > 0)
            {
                if (_options.AutoInsertMissingParents)
                {
                    _logger.LogInformation("Auto-inserting {Count} missing prodaja_zaglavlje rows before importing prodaja_stavke.", missingSaleIds.Count);
                    var insertedIds = await EnsureProdajaZaglavljeExistsAsync(missingSaleIds, session, overwriteExisting, result, ct);
                    // Prefer to merge newly inserted ids returned by the helper so we don't need an extra round-trip.
                    if (insertedIds is not null && insertedIds.Count > 0)
                        saleIds.UnionWith(insertedIds);
                    else
                        saleIds = (await _trendDb.ProdajaZaglavlja.AsNoTracking().Select(x => x.Id).ToListAsync(ct)).ToHashSet();
                }
                else if (!_options.SkipInvalidForeignKeys)
                {
                    var sample = string.Join(",", missingSaleIds.Take(20));
                    throw new InvalidOperationException($"prodaja_stavke FK violation: missing parent prodaja ids count={missingSaleIds.Count}, sample={sample}");
                }
                else
                {
                    _logger.LogWarning("Found {Count} prodaja parent ids present in Access but missing in DB; will skip orphan lines (SkipInvalidForeignKeys=true).", missingSaleIds.Count);
                }
            }
        }
        var orphanRows = 0;
        var orphanSamples = new List<string>(ForeignKeyWarningSampleLimit);
        var rowIndex = 0;

        var sw = Stopwatch.StartNew();
        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            rowIndex++;
            MarkSourceRow(result, "prodaja_stavke");
            var idProdaja = I(row, "idprodaja", "saleid", "idzaglavlje", "iddnevnik");
            var idArtikal = I(row, "idartikal", "productid", "artiklid");
            if (!idProdaja.HasValue || !idArtikal.HasValue)
                continue;

            if (!saleIds.Contains(idProdaja.Value))
            {
                orphanRows++;
                var sourceRowJson = SerializeRowForDiagnostics(row);
                var logMessage = TrimToMaxLength(
                    $"FK-skip: prodaja_stavke row skipped because id_prodaja={idProdaja.Value} does not exist in prodaja_zaglavlje.",
                    2000);
                if (orphanSamples.Count < ForeignKeyWarningSampleLimit)
                    orphanSamples.Add($"row {rowIndex}: id_prodaja={idProdaja.Value}");

                _logger.LogWarning(
                    "Access import detected orphan prodaja_stavke row. TableName: {TableName}. RowIndex: {RowIndex}. InvalidIdProdaja: {InvalidIdProdaja}. SkipInvalidForeignKeys: {SkipInvalidForeignKeys}. SourceRowJson: {SourceRowJson}.",
                    table,
                    rowIndex,
                    idProdaja.Value,
                    _options.SkipInvalidForeignKeys,
                    sourceRowJson);

                AddAccessImportLogEntry(
                    result.BatchId,
                    table,
                    rowIndex,
                    "warning",
                    logMessage,
                    sourceRowJson);
                TrackTrendWrite();

                if (!_options.SkipInvalidForeignKeys)
                {
                    throw new InvalidOperationException(
                        $"Tabela '{table}' sadrzi prodaja_stavke red sa nepostojecim id_prodaja={idProdaja.Value} na redu {rowIndex}. SourceRow={sourceRowJson}");
                }

                await FlushTrendWritesAsync(force: false, ct);
                continue;
            }

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
                _trendDb.ProdajaStavke.Update(e);
                result.ProdajaStavkeUpdated++;
                TrackTrendWrite();
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
                TrackTrendWrite();
            }

            await FlushTrendWritesAsync(force: false, ct);
        }

        sw.Stop();
        _logger.LogInformation("Import prodaja_stavke finished. Elapsed={Elapsed}s TotalRows={TotalRows} OrphanRows={OrphanRows} Rate={Rate:F1} rows/s", sw.Elapsed.TotalSeconds, rowIndex, orphanRows, rowIndex / Math.Max(1, sw.Elapsed.TotalSeconds));

        if (orphanRows > 0)
        {
            var sampleSuffix = orphanSamples.Count > 0
                ? $" Primeri: {string.Join("; ", orphanSamples)}."
                : string.Empty;
            var summary = _options.SkipInvalidForeignKeys
                ? $"Tabela '{table}' ima {orphanRows} prodaja_stavke redova sa nepostojecim id_prodaja. Redovi su preskoceni zbog AccessImport:SkipInvalidForeignKeys=true.{sampleSuffix}"
                : $"Tabela '{table}' ima {orphanRows} prodaja_stavke redova sa nepostojecim id_prodaja. Import je zaustavljen zbog AccessImport:SkipInvalidForeignKeys=false.{sampleSuffix}";

            result.Warnings.Add(summary);
            AddAccessImportLogEntry(
                result.BatchId,
                table,
                0,
                "warning",
                TrimToMaxLength(summary, 2000));
            TrackTrendWrite();
            _logger.LogWarning(
                "Access import prodaja_stavke FK validation summary. TableName: {TableName}. OrphanRows: {OrphanRows}. SkipInvalidForeignKeys: {SkipInvalidForeignKeys}. Samples: {Samples}.",
                table,
                orphanRows,
                _options.SkipInvalidForeignKeys,
                string.Join("; ", orphanSamples));
        }
    }

    private async Task<bool> IsProdajaLineTableAsync(IAccessDataReaderSession session, string table, CancellationToken ct)
    {
        try
        {
            var cols = await session.GetColumnsAsync(table, ct);
            return IsProdajaLineTableByColumns(cols);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IsProdajaLineTableAsync] Exception for table '{table}': {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private async Task ImportDnevnikPromenaAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var existing = ToFirstDictionary(_trendDb.DnevnikPromena.AsNoTracking().ToList(), x => x.Id);
        var dbExistingIds = existing.Keys.ToHashSet();
        var usedIds = existing.Keys.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        var supplierByKey = _trendDb.Dobavljaci
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.Naziv))
            .AsEnumerable()
            .GroupBy(x => NormalizeLookup(x.Naziv), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "dnevnik_promena");
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

            var isInsert = false;
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
                isInsert = true;
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

            if (!isInsert)
                _trendDb.DnevnikPromena.Update(e);

            TrackTrendWrite();
            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportProdajaFromLineTableAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var existingZaglavlja = ToFirstDictionary(_trendDb.ProdajaZaglavlja.AsNoTracking().ToList(), x => x.Id);
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

        var dnevnikById = _trendDb.DnevnikPromena.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToDictionary(x => x.Id, x => x);

        var maxStavkaId = _trendDb.ProdajaStavke.AsNoTracking().Any()
            ? _trendDb.ProdajaStavke.AsNoTracking().Max(x => x.Id)
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

        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "prodaja_zaglavlje");
            MarkSourceRow(result, "prodaja_stavke");

            var sourceSaleId = I(row, "iddnevnik", "idprodaja", "saleid", "iddnevnikpromene", "iddnevnikpromena");
            var idArtikal = I(row, "idartikal", "artikalid", "artiklid", "productid");
            if (!sourceSaleId.HasValue || sourceSaleId.Value <= 0 || !idArtikal.HasValue || idArtikal.Value <= 0)
                continue;

            MarkAccepted(result, "prodaja_zaglavlje");
            MarkAccepted(result, "prodaja_stavke");

            var qty = I(row, "kolicina", "qty", "quantity") ?? 1;
            if (qty <= 0)
                qty = 1;

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
                if (!string.IsNullOrWhiteSpace(zaglavlje.BrojRacuna))
                    existingBrojevi[zaglavlje.BrojRacuna] = zaglavlje;
                result.ProdajaInserted++;
                TrackTrendWrite();

                // Persist the newly created parent before inserting child rows while AutoDetectChanges is disabled.
                await FlushTrendWritesAsync(force: true, ct);
            }
            else if (overwriteExisting)
            {
                if (string.IsNullOrWhiteSpace(zaglavlje.BrojRacuna))
                    zaglavlje.BrojRacuna = S(row, "brojracuna", "brojkalkulacije", "invoice", "receiptnumber");
                if (zaglavlje.IDObjekat is null && idObjekat.HasValue)
                    zaglavlje.IDObjekat = idObjekat.Value;
                zaglavlje.DataOrigin = "access";
                _trendDb.ProdajaZaglavlja.Update(zaglavlje);
                result.ProdajaUpdated++;
                TrackTrendWrite();
            }

            var lineKey = BuildProdajaLineKey(zaglavlje.Id, idArtikal.Value, qty, cena);
            existingLineCounts.TryGetValue(lineKey, out var existingCountForKey);
            consumedExistingLineCounts.TryGetValue(lineKey, out var consumedCountForKey);

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
            TrackTrendWrite();

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportPovracajAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var existing = ToFirstDictionary(_trendDb.PovracajZaglavlja.AsNoTracking().ToList(), x => x.Id);
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
        var seq = 0;

        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "povracaj_zaglavlje");

            var id = I(row, "id", "idpovracaj", "returnid");
            var idDobavljac = I(row, "iddobavljac", "dobavljacid", "supplierid") ?? 0;
            var datum = DT(row, "datumazapisnika", "datumpovracaja", "datum", "date") ?? DateTime.UtcNow;
            var broj = S(row, "brojzapisnika", "brozapisnika", "broj", "recordnumber", "returnno")
                ?? $"ZP-{datum:yyyyMMdd}-{++seq:D4}";

            PovracajZaglavlje? e = null;
            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && existing.TryGetValue(sourceId, out var foundById))
                e = foundById;
            else if (existingByBroj.TryGetValue(broj, out var foundByBroj))
                e = foundByBroj;

            var isInsert = false;
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
                isInsert = true;
                TrackTrendWrite();
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

            if (!isInsert)
                _trendDb.PovracajZaglavlja.Update(e);

            TrackTrendWrite();
            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportPovracajStavkeAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var existing = ToFirstDictionary(_trendDb.PovracajStavke.AsNoTracking().ToList(), x => x.Id);
        var usedIds = existing.Keys.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        var povracajIds = _trendDb.PovracajZaglavlja.AsNoTracking().Select(x => x.Id).ToHashSet();

        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "povracaj_stavke");

            var idPovracaj = I(row, "idpovracaj", "returnid", "idzaglavlje");
            var idArtikal = I(row, "idartikal", "productid", "artiklid");
            if (!idPovracaj.HasValue || !idArtikal.HasValue || !povracajIds.Contains(idPovracaj.Value))
                continue;

            MarkAccepted(result, "povracaj_stavke");

            var id = I(row, "id", "idstavka", "lineid");
            var qty = I(row, "kolicina", "qty", "quantity") ?? 1;
            var cena = D(row, "cena", "unitprice", "price", "nabavnacena", "purchaseprice") ?? 0m;
            var razlog = S(row, "razlog", "reason");
            var stanje = S(row, "stanjeartikal", "stanjearticle", "condition", "status");

            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && existing.TryGetValue(sourceId, out var e))
            {
                if (!overwriteExisting)
                    continue;

                e.IdPovracaj = idPovracaj.Value;
                e.IdArtikal = idArtikal.Value;
                e.Kolicina = qty;
                e.Cena = cena;
                e.Razlog = razlog;
                e.StanjeArtikla = stanje;
                _trendDb.PovracajStavke.Update(e);
                result.PovracajStavkeUpdated++;
                TrackTrendWrite();
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
                TrackTrendWrite();
            }

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportNivelacijeAsync(IAccessDataReaderSession session, string? table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        if (table is null)
            return;

        var dnevnikById = _trendDb.DnevnikPromena.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToDictionary(x => x.Id, x => x);

        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;

        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "nivelacije");

            var idArtikal = I(row, "idartikal", "artikalid", "productid", "id_artikal");
            if (!idArtikal.HasValue)
                continue;

            var novaCena = D(row, "novacena", "novaprodajnacena", "newprice", "cena");
            if (!novaCena.HasValue)
                continue;

            MarkAccepted(result, "nivelacije");

            var staraCena = D(row, "staracena", "staraprodajnacena", "oldprice");
            var kolicina = I(row, "kolicina", "qty", "quantity") ?? 1;
            var iznos = Math.Abs((novaCena.Value - (staraCena ?? 0m)) * kolicina);
            var srcId = I(row, "iddnevnik", "id", "idlog") ?? 0;

            dnevnikById.TryGetValue(srcId, out var sourceDnevnik);
            var eventDate = DT(row, "datum", "datumnivelacije", "date")
                ?? sourceDnevnik?.Datum
                ?? DateTime.UtcNow;

            var assignedId = srcId > 0 && !usedIds.Contains(srcId)
                ? srcId
                : AllocateNextId(usedIds, ref next);

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
            TrackTrendWrite();

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportUnosRobeAsync(IAccessDataReaderSession session, string? table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        if (table is null)
            return;

        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;

        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "unos_robe");

            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue)
                continue;

            MarkAccepted(result, "unos_robe");

            var kolicina = I(row, "kolicina", "qty", "quantity") ?? 1;
            var nabavnaCena = D(row, "nabavnacena", "purchaseprice", "cena", "nc") ?? 0m;
            var srcId = I(row, "iddnevnik", "id", "idlog") ?? 0;
            var assignedId = srcId > 0 && !usedIds.Contains(srcId)
                ? srcId
                : AllocateNextId(usedIds, ref next);

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
            TrackTrendWrite();

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportPovratniceAsync(IAccessDataReaderSession session, string? table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        if (table is null)
            return;

        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;

        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "povratnice");

            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue)
                continue;

            MarkAccepted(result, "povratnice");

            var kolicina = I(row, "kolicina", "qty", "quantity") ?? 1;
            var cena = D(row, "cena", "prodajnacena", "unitprice", "pc") ?? 0m;
            var srcId = I(row, "iddnevnik", "id", "idpovratnice", "idlog") ?? 0;
            var assignedId = srcId > 0 && !usedIds.Contains(srcId)
                ? srcId
                : AllocateNextId(usedIds, ref next);

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
            TrackTrendWrite();

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportPrenosRobeAsync(IAccessDataReaderSession session, string? table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        if (table is null)
            return;

        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;

        await foreach (var row in session.ReadRowsAsync(table, ct))
        {
            MarkSourceRow(result, "prenos_robe");

            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue)
                continue;

            MarkAccepted(result, "prenos_robe");

            var kolicina = I(row, "kolicina", "qty", "quantity") ?? 1;
            var datum = DT(row, "datum", "datumprenos", "datumtransfera", "date") ?? DateTime.UtcNow;
            var cena = D(row, "cena", "nabavnacena", "prodajnacena") ?? 0m;
            var idIz = I(row, "idobjekatiz", "idobjekatizlaza", "fromstore", "idobjekat");
            var idU = I(row, "idobjekatulaz", "idobjekatdolaz", "tostore", "idobjekatodredista");
            var brDok = S(row, "iddnevnik", "brdokumenta", "brprenos");

            var idOut = AllocateNextId(usedIds, ref next);
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = idOut,
                TipPromene = TipPromeneConstants.PrenosIzlaz,
                Datum = datum,
                ArtikalId = idArtikal.Value,
                Kolicina = -kolicina,
                NovaProdajnaCena = cena,
                Iznos = cena * kolicina,
                IDObjekat = idIz,
                BrojRacuna = brDok,
                DataOrigin = "access"
            });
            result.PrenosRobeInserted++;
            TrackTrendWrite();

            var idIn = AllocateNextId(usedIds, ref next);
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = idIn,
                TipPromene = TipPromeneConstants.PrenosUlaz,
                Datum = datum,
                ArtikalId = idArtikal.Value,
                Kolicina = kolicina,
                NovaProdajnaCena = cena,
                Iznos = cena * kolicina,
                IDObjekat = idU,
                BrojRacuna = brDok,
                DataOrigin = "access"
            });
            result.PrenosRobeInserted++;
            TrackTrendWrite();

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private void ImportTipovi(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = ToFirstDictionary(_trendDb.TipoviObuce, x => x.Id);
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
        var existing = ToFirstDictionary(_trendDb.Dobavljaci, x => x.Id);
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
        var existing = ToFirstDictionary(_trendDb.Sezone, x => x.Id);
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
        var existing = ToFirstDictionary(_trendDb.Artikli, x => x.Id);
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
        var existing = ToFirstDictionary(_trendDb.ProdajaZaglavlja, x => x.Id);
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
        var existing = ToFirstDictionary(_trendDb.ProdajaStavke, x => x.Id);
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

    private bool IsProdajaLineTable(OdbcConnection conn, string table)
    {
        try
        {
            var cols = ReadColumnNamesNormalized(conn, table);
            return cols.Contains("iddnevnik")
                && cols.Contains("idartikal")
                && cols.Contains("kolicina")
                && (cols.Contains("prodajnacena") || cols.Contains("cena"))
                && !cols.Contains("datumprodaje")
                && !cols.Contains("brojracuna");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IsProdajaLineTable] Exception for table '{table}': {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private void ImportProdajaFromLineTable(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existingZaglavlja = ToFirstDictionary(_trendDb.ProdajaZaglavlja, x => x.Id);
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

    private async Task SynthesizeProdajaFromDnevnikAsync(bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        static bool IsSaleType(string tip) => TipPromeneConstants.IsSale(tip);

        var saleEntries = await _trendDb.DnevnikPromena
            .AsNoTracking()
            .Where(d => d.DataOrigin == "access")
            .OrderBy(d => d.Id)
            .ToListAsync(ct);
        saleEntries = saleEntries
            .Where(d => IsSaleType(d.TipPromene))
            .ToList();

        if (saleEntries.Count == 0)
            return;

        var existingZaglavlja = ToFirstDictionary(_trendDb.ProdajaZaglavlja.AsNoTracking().ToList(), x => x.Id);
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

        var groups = saleEntries
            .GroupBy(d => string.IsNullOrWhiteSpace(d.BrojRacuna)
                ? $"DN-{d.Id}"
                : d.BrojRacuna!)
            .ToList();

        int maxId = existingZaglavlja.Count > 0 ? existingZaglavlja.Keys.Max() : 0;
        int maxStavkaId = await _trendDb.ProdajaStavke.AsNoTracking().AnyAsync(ct)
            ? await _trendDb.ProdajaStavke.AsNoTracking().MaxAsync(x => x.Id, ct)
            : 0;
        var insertedProdaja = 0;
        var updatedProdaja = 0;
        var insertedStavke = 0;

        foreach (var grp in groups)
        {
            var first = grp.First();

            if (!existingZaglavlja.TryGetValue(first.Id, out var zaglavlje) &&
                !existingBrojevi.TryGetValue(grp.Key, out zaglavlje))
            {
                var assignedId = first.Id > 0 && !existingZaglavlja.ContainsKey(first.Id)
                    ? first.Id
                    : maxId + 1;
                if (assignedId > maxId)
                    maxId = assignedId;

                zaglavlje = new Domain.Model.Prodaja.ProdajaZaglavlje
                {
                    Id = assignedId,
                    BrojRacuna = string.IsNullOrWhiteSpace(first.BrojRacuna) ? null : first.BrojRacuna,
                    DatumProdaje = first.Datum,
                    NacinPlacanja = null,
                    IDObjekat = first.IDObjekat,
                    KorisnikIme = first.KorisnikIme,
                    DataOrigin = "access"
                };
                _trendDb.ProdajaZaglavlja.Add(zaglavlje);
                existingZaglavlja[zaglavlje.Id] = zaglavlje;
                if (zaglavlje.BrojRacuna != null)
                    existingBrojevi[zaglavlje.BrojRacuna] = zaglavlje;
                result.ProdajaInserted++;
                insertedProdaja++;
                TrackTrendWrite();

                // Persist the synthesized parent before dependent stavke are queued in the same import run.
                await FlushTrendWritesAsync(force: true, ct);
            }
            else if (overwriteExisting)
            {
                var shouldUpdate = false;
                if (string.IsNullOrWhiteSpace(zaglavlje.BrojRacuna) && !string.IsNullOrWhiteSpace(first.BrojRacuna))
                {
                    zaglavlje.BrojRacuna = first.BrojRacuna;
                    shouldUpdate = true;
                }
                if (zaglavlje.IDObjekat is null && first.IDObjekat.HasValue)
                {
                    zaglavlje.IDObjekat = first.IDObjekat.Value;
                    shouldUpdate = true;
                }
                if (string.IsNullOrWhiteSpace(zaglavlje.KorisnikIme) && !string.IsNullOrWhiteSpace(first.KorisnikIme))
                {
                    zaglavlje.KorisnikIme = first.KorisnikIme;
                    shouldUpdate = true;
                }
                if (!string.Equals(zaglavlje.DataOrigin, "access", StringComparison.OrdinalIgnoreCase))
                {
                    zaglavlje.DataOrigin = "access";
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                {
                    _trendDb.ProdajaZaglavlja.Update(zaglavlje);
                    result.ProdajaUpdated++;
                    updatedProdaja++;
                    TrackTrendWrite();
                }
            }

            foreach (var d in grp.Where(d => d.ArtikalId.HasValue && d.ArtikalId.Value > 0))
            {
                var stavkaCena = (d.NovaProdajnaCena ?? d.StaraProdajnaCena ?? (d.Iznos > 0 ? d.Iznos : null)) ?? 0m;
                var stavkaQty = (d.Kolicina.HasValue && d.Kolicina.Value > 0) ? d.Kolicina.Value : 1;
                _trendDb.ProdajaStavke.Add(new Domain.Model.Prodaja.ProdajaStavka
                {
                    Id = ++maxStavkaId,
                    IdProdaja = zaglavlje.Id,
                    IdArtikal = d.ArtikalId!.Value,
                    Kolicina = stavkaQty,
                    Cena = stavkaCena
                });
                result.ProdajaStavkeInserted++;
                insertedStavke++;
                TrackTrendWrite();
                await FlushTrendWritesAsync(force: false, ct);
            }
        }

        var summary = $"Sintetizovano {insertedProdaja} prodaja i {insertedStavke} stavki iz DnevnikPromena (nije pronadjena posebna tabela prodaje).";
        if (updatedProdaja > 0)
            summary += $" Azurirano zaglavlja: {updatedProdaja}.";
        result.Warnings.Add(summary);
        _logger.LogInformation(
            "Access import synthesized prodaja from dnevnik. ProdajaInserted: {ProdajaInserted}. ProdajaUpdated: {ProdajaUpdated}. ProdajaStavkeInserted: {ProdajaStavkeInserted}.",
            insertedProdaja,
            updatedProdaja,
            insertedStavke);
    }

    private void ImportPovracajStavke(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = ToFirstDictionary(_trendDb.PovracajStavke, x => x.Id);
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
        var existing = ToFirstDictionary(_trendDb.DnevnikPromena, x => x.Id);
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
    }

    private void ImportPrenosRobe(OdbcConnection conn, string? table, bool overwriteExisting, AccessImportRunResponse result)
    {
        // Each transfer row â†’ TWO DnevnikPromena entries: izlaz from source + ulaz to destination
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
            "povraÄ‡aj robe u ",
            "povracaj u ",
            "povraÄ‡aj u ",
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
        var existing = ToFirstDictionary(_trendDb.PovracajZaglavlja, x => x.Id);
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
            var broj = S(row, "brozapisnika", "bÑ€Ð¾Ñ˜Ð·Ð°Ð¿Ð¸ÑÐ½Ð¸ÐºÐ°", "broj", "recordnumber", "returnno")
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

        // â”€â”€ Suppliers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var importedSuppliers = await _trendDb.Dobavljaci.AsNoTracking().Where(x => x.DataOrigin == "access").ToListAsync(ct);

        // â”€â”€ Seasons â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var importedSeasons = await _trendDb.Sezone.AsNoTracking().Where(x => x.DataOrigin == "access").ToListAsync(ct);

        // â”€â”€ Footwear types â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var importedTypes = await _trendDb.TipoviObuce.AsNoTracking().Where(x => x.DataOrigin == "access").ToListAsync(ct);

        // â”€â”€ Inventory Movements â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
    /// Ensures "DataImportBatches" table exists. Handles first deploy to a fresh DB
    /// where DatabaseInitializer may have failed due to a Neon cold-start.
    /// </summary>
    private async Task EnsureDataImportBatchesTableAsync(CancellationToken ct)
    {
        var providerName = _trendDb.Database.ProviderName ?? string.Empty;
        if (providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            // In-memory provider used by tests: skip relational DDL/DDL checks
            return;
        }

        const string createSql = """
            CREATE TABLE IF NOT EXISTS "DataImportBatches" (
                "Id"              bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                "SourceSystem"    character varying(64)   NOT NULL DEFAULT 'access',
                "SourceFileName"  character varying(300)  NOT NULL DEFAULT '',
                "SourceFilePath"  character varying(800),
                "QueuedAtUtc"     timestamp with time zone NOT NULL DEFAULT NOW(),
                "StartedAtUtc"    timestamp with time zone NOT NULL DEFAULT NOW(),
                "CompletedAtUtc"  timestamp with time zone,
                "LastHeartbeatUtc" timestamp with time zone,
                "Status"          character varying(32)   NOT NULL DEFAULT 'pending',
                "CurrentStep"     character varying(64),
                "CurrentTable"    character varying(300),
                "SummaryJson"     text,
                "ErrorMessage"    character varying(4000),
                "ErrorDetailsJson" text,
                "RequestedBy"     character varying(200),
                "ImportMode"      character varying(16)   NOT NULL DEFAULT 'auto',
                "IncludeAnalytics" boolean NOT NULL DEFAULT TRUE,
                "OverwriteExisting" boolean NOT NULL DEFAULT TRUE,
                "IncludeTemporaryTables" boolean NOT NULL DEFAULT FALSE,
                "SkipInvalidForeignKeys" boolean NOT NULL DEFAULT TRUE,
                "CancellationRequested" boolean NOT NULL DEFAULT FALSE,
                "CancellationRequestedAtUtc" timestamp with time zone,
                "RetryCount"      integer NOT NULL DEFAULT 0,
                "ProgressPercent" integer NOT NULL DEFAULT 0,
                "RowsRead"        integer NOT NULL DEFAULT 0,
                "RowsAccepted"    integer NOT NULL DEFAULT 0,
                "RowsWritten"     integer NOT NULL DEFAULT 0,
                "DurationSeconds" integer,
                "TotalImported"   integer NOT NULL DEFAULT 0,
                "TotalUpdated"    integer NOT NULL DEFAULT 0,
                "TotalErrors"     integer NOT NULL DEFAULT 0,
                "DataOrigin"      character varying(32) NOT NULL DEFAULT 'access'
            );
            """;
        try
        {
            await _trendDb.Database.ExecuteSqlRawAsync(createSql, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message?.Contains("Relational-specific methods", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Non-relational provider (e.g. InMemory) - skip DDL
            _logger.LogDebug(ex, "Skipping DataImportBatches CREATE because database provider is not relational.");
            return;
        }
        const string alterSql = """
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "LastHeartbeatUtc" timestamp with time zone;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "CurrentStep" character varying(64);
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "CurrentTable" character varying(300);
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "SourceFilePath" character varying(800);
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "QueuedAtUtc" timestamp with time zone NOT NULL DEFAULT NOW();
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "ErrorDetailsJson" text;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "RequestedBy" character varying(200);
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "ImportMode" character varying(16) NOT NULL DEFAULT 'auto';
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "IncludeAnalytics" boolean NOT NULL DEFAULT TRUE;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "OverwriteExisting" boolean NOT NULL DEFAULT TRUE;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "IncludeTemporaryTables" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "SkipInvalidForeignKeys" boolean NOT NULL DEFAULT TRUE;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "CancellationRequested" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "CancellationRequestedAtUtc" timestamp with time zone;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "RetryCount" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "ProgressPercent" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "RowsRead" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "RowsAccepted" integer NOT NULL DEFAULT 0;
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "RowsWritten" integer NOT NULL DEFAULT 0;
            CREATE INDEX IF NOT EXISTS "IX_DataImportBatches_QueuedAtUtc" ON "DataImportBatches" ("QueuedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_DataImportBatches_LastHeartbeatUtc" ON "DataImportBatches" ("LastHeartbeatUtc");
            CREATE INDEX IF NOT EXISTS "IX_DataImportBatches_CancellationRequested" ON "DataImportBatches" ("CancellationRequested");
            """;
        try
        {
            await _trendDb.Database.ExecuteSqlRawAsync(alterSql, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message?.Contains("Relational-specific methods", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Non-relational provider (e.g. InMemory) - skip DDL
            _logger.LogDebug(ex, "Skipping DataImportBatches ALTER because database provider is not relational.");
            return;
        }
    }

    /// <summary>
    /// Creates a cross-platform ODBC connection to an Access database.
    /// Windows  : uses the built-in "Microsoft Access Driver (*.mdb, *.accdb)" ODBC driver (no ACE/Office needed).
    /// Linux/Mac: uses the open-source MDBTools ODBC driver.
    ///   Docker install: apt-get update && apt-get install -y unixodbc libodbc2 mdbtools odbc-mdbtools
    /// </summary>
    private static OdbcConnection CreateOdbcConnection(string accessFilePath)
    {
        var cs = BuildAccessOdbcConnectionString(
            accessFilePath,
            OperatingSystem.IsWindows(),
            FindMdbToolsDriver());

        return new OdbcConnection(cs);
    }

    internal static string BuildAccessOdbcConnectionString(string accessFilePath, bool isWindows, string? driverPath = null)
    {
        if (isWindows)
        {
            return $"Driver={{Microsoft Access Driver (*.mdb, *.accdb)}};Dbq={accessFilePath};ReadOnly=1;";
        }

        // MDBTools expects DBQ for the database file path. Using Database= causes
        // unixODBC to reject the connection string with "Could not find DSN nor DBQ".
        var driverToken = !string.IsNullOrWhiteSpace(driverPath)
            ? driverPath
            : "{MDBTools}";

        return $"Driver={driverToken};DBQ={accessFilePath};";
    }

    private static string? FindMdbToolsDriver()
    {
        // Common locations for libmdbodbc.so across Debian/Ubuntu variants
        string[] candidates = [
            "/usr/lib/x86_64-linux-gnu/odbc/libmdbodbc.so",
            "/usr/lib/aarch64-linux-gnu/odbc/libmdbodbc.so",
            "/usr/lib/odbc/libmdbodbc.so",
            "/usr/lib/libmdbodbc.so"
        ];
        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }
        // Last resort: search /usr
        try
        {
            foreach (var f in Directory.EnumerateFiles("/usr", "libmdbodbc*.so*", SearchOption.AllDirectories))
                return f;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FindMdbToolsDriver] Search failed: {ex.GetType().Name}: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Safely resolves a table name from a DataRow schema row.
    /// Handles provider-specific schema variations (missing TABLE_NAME, renamed columns, etc).
    /// 
    /// Resolution order:
    /// 1. TABLE_NAME column (standard OLEDB)
    /// 2. TABLE column (ODBC fallback)
    /// 3. Any column containing "TABLE" (case-insensitive)
    /// 4. Any column containing "NAME" (case-insensitive)
    /// 5. First available column
    /// 6. null (if no columns available)
    /// 
    /// INTERNAL: Exposed for testing.
    /// </summary>
    internal static string? ResolveTableName(DataRow row, DataTable schema)
    {
        if (row is null || schema?.Columns.Count == 0)
            return null;

        try
        {
            // 1. Try TABLE_NAME (standard OLEDB) using case-insensitive column lookup and ordinal access
            var tableNameCol = schema.Columns.Cast<DataColumn>()
                .FirstOrDefault(c => string.Equals(c.ColumnName, "TABLE_NAME", StringComparison.OrdinalIgnoreCase));
            if (tableNameCol is not null)
            {
                var value = row[tableNameCol.Ordinal];
                var name = value is not DBNull ? value?.ToString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                    return name.Trim();
            }

            // 2. Try TABLE (ODBC fallback)
            var tableCol = schema.Columns.Cast<DataColumn>()
                .FirstOrDefault(c => string.Equals(c.ColumnName, "TABLE", StringComparison.OrdinalIgnoreCase));
            if (tableCol is not null)
            {
                var value = row[tableCol.Ordinal];
                var name = value is not DBNull ? value?.ToString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                    return name.Trim();
            }

            // 3. Any best-effort column containing "TABLE" or "NAME" (provider-specific schemas).
            var candidateColumns = schema.Columns.Cast<DataColumn>()
                .Where(c =>
                    c.ColumnName.Contains("TABLE", StringComparison.OrdinalIgnoreCase) ||
                    c.ColumnName.Contains("NAME", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c =>
                {
                    var col = c.ColumnName;
                    var hasName = col.Contains("NAME", StringComparison.OrdinalIgnoreCase);
                    var hasTable = col.Contains("TABLE", StringComparison.OrdinalIgnoreCase);
                    var isMetadata = col.Contains("TYPE", StringComparison.OrdinalIgnoreCase)
                        || col.Contains("SCHEMA", StringComparison.OrdinalIgnoreCase)
                        || col.Contains("CAT", StringComparison.OrdinalIgnoreCase)
                        || col.Contains("CATALOG", StringComparison.OrdinalIgnoreCase);

                    if (hasTable && hasName) return 0;
                    if (hasName && !isMetadata) return 1;
                    if (hasTable && !isMetadata) return 2;
                    if (hasName) return 3;
                    return 4;
                })
                .ToList();

            foreach (var candidateColumn in candidateColumns)
            {
                var value = row[candidateColumn.Ordinal];
                var name = value is not DBNull ? value?.ToString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                    return name.Trim();
            }

            // 5. Fallback to first column
            if (schema.Columns.Count > 0)
            {
                var value = row[0];
                var name = value is not DBNull ? value?.ToString() : null;
                if (!string.IsNullOrWhiteSpace(name))
                    return name.Trim();
            }
        }
        catch (Exception ex)
        {
            // Gracefully handle any Access/ODBC provider quirks
            System.Diagnostics.Debug.WriteLine($"[ResolveTableName] Exception: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Safely checks if a row represents a user table (not a system/temporary table).
    /// Handles missing or misnamed TABLE_TYPE column across ODBC/OLEDB providers.
    /// 
    /// Rules:
    /// - If TABLE_TYPE column exists â†’ must equal "TABLE"
    /// - If TABLE_TYPE column missing â†’ assume it's a user table (fail-open)
    /// 
    /// INTERNAL: Exposed for testing.
    /// </summary>
    internal static bool CheckIsUserTable(DataRow row, DataTable schema)
    {
        if (schema?.Columns.Count == 0)
        {
            // Empty schema â†’ assume it's a user table (fail-open)
            return true;
        }

        // Locate TABLE_TYPE column case-insensitively and use ordinal access; fail-open when missing
        var tableTypeCol = schema.Columns.Cast<DataColumn>()
            .FirstOrDefault(c => string.Equals(c.ColumnName, "TABLE_TYPE", StringComparison.OrdinalIgnoreCase));

        if (tableTypeCol is null)
        {
            // Missing column â†’ assume it's a user table (fail-open, safe default)
            return true;
        }

        try
        {
            var value = row[tableTypeCol.Ordinal];
            if (value is null or DBNull)
                return true; // Null â†’ assume user table

            var typeStr = value.ToString() ?? string.Empty;
            return string.Equals(typeStr, "TABLE", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CheckIsUserTable] Exception: {ex.GetType().Name}: {ex.Message}");
            // On any error â†’ assume it's a user table
            return true;
        }
    }

    /// <summary>
    /// Safely retrieves user table names from Access database via ODBC/OLEDB.
    /// 
    /// This method is defensive against provider-specific schema variations:
    /// - Handles missing TABLE_NAME/TABLE_TYPE columns
    /// - Falls back to alternative column names (TABLE, NAME, etc)
    /// - Logs provider type and any schema issues for diagnostics
    /// - Never throws; returns empty list on schema errors
    /// </summary>
    private List<string> GetUserTables(OdbcConnection conn, bool includeTemporaryTables = false)
    {
        if (_useCliMode)
            return GetCliUserTables(includeTemporaryTables);

        try
        {
            var schema = conn.GetSchema("Tables");
            var provider = conn.GetType().Name;

            if (schema is null || schema.Rows.Count == 0)
            {
                _logger.LogWarning(
                    "Access schema returned no rows. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Provider: {Provider}.",
                    0L,
                    "TablesSchema",
                    "get-user-tables",
                    provider);

                if (TryEnableCliMode("get-user-tables:empty-schema"))
                    return GetCliUserTables(includeTemporaryTables);

                return [];
            }

            var hasTableName = schema.Columns.Contains("TABLE_NAME");
            var hasTableType = schema.Columns.Contains("TABLE_TYPE");
            if (!hasTableName || !hasTableType)
            {
                _logger.LogWarning(
                    "Non-standard schema detected. Provider: {Provider}. Columns: {Columns}. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}.",
                    provider,
                    string.Join(",", schema.Columns.Cast<DataColumn>().Select(c => c.ColumnName)),
                    0L,
                    "TablesSchema",
                    "get-user-tables");
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in schema.Rows)
            {
                var isUserTable = CheckIsUserTable(row, schema);
                if (!isUserTable)
                    continue;

                var tableName = ResolveTableName(row, schema);
                if (string.IsNullOrWhiteSpace(tableName))
                    continue;

                if (tableName.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!includeTemporaryTables && Normalize(tableName).Contains("privremena", StringComparison.Ordinal))
                    continue;

                if (seen.Add(tableName))
                    result.Add(tableName);
            }

            _logger.LogInformation(
                "Resolved Access user tables. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Provider: {Provider}. Count: {Count}.",
                0L,
                "TablesSchema",
                "get-user-tables",
                provider,
                result.Count);

            if (result.Count == 0 && TryEnableCliMode("get-user-tables:no-user-tables"))
                return GetCliUserTables(includeTemporaryTables);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to read Access schema tables. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}.",
                0L,
                "TablesSchema",
                "get-user-tables");

            if (TryEnableCliMode("get-user-tables:exception", ex))
                return GetCliUserTables(includeTemporaryTables);

            return [];
        }
    }

    private int RowCount(OdbcConnection conn, string table)
    {
        if (_useCliMode && _cliFilePath is not null)
        {
            try { return MdbCliRowCount(_cliFilePath, table); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MDBTools CLI row count failed for {Table}", table);
                return 0;
            }
        }
        else
        {
            if (!TryGetQuotedTableIdentifier(table, out var quotedTable, out var failureReason))
            {
                _logger.LogWarning(
                    "Invalid table name for row count. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Reason: {Reason}.",
                    0L,
                    table ?? "unknown",
                    "row-count",
                    failureReason);
                return 0;
            }

            try
            {
                using var cmd = new OdbcCommand($"SELECT COUNT(*) FROM {quotedTable}", conn);
                var result = cmd.ExecuteScalar();
                // Handle various return types from different ODBC providers
                return result switch
                {
                    null or DBNull => 0,
                    int i => i,
                    long l => l > int.MaxValue ? int.MaxValue : (int)l,
                    decimal d => d > int.MaxValue ? int.MaxValue : (int)d,
                    _ => ConvertToInt(result) ?? 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Row count query failed. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}.",
                    0L,
                    table ?? "unknown",
                    "row-count");
                return 0;
            }
        }
    }

    private IEnumerable<Dictionary<string, object?>> ReadRows(OdbcConnection conn, string table)
    {
        if (_useCliMode && _cliFilePath is not null)
        {
            foreach (var row in MdbCliReadRows(_cliFilePath, table))
                yield return row;
            yield break;
        }
        else
        {
            if (!TryGetQuotedTableIdentifier(table, out var quotedTable, out var failureReason))
            {
                _logger.LogWarning(
                    "Invalid table name for row read. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Reason: {Reason}.",
                    0L,
                    table ?? "unknown",
                    "read-rows",
                    failureReason);
                yield break;
            }

            using var cmd = new OdbcCommand($"SELECT * FROM {quotedTable}", conn);
            using var r = cmd.ExecuteReader(CommandBehavior.SequentialAccess);
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
    }

    private async Task<string?> FindTableAsync(
        IAccessDataReaderSession session,
        IReadOnlyList<string> tables,
        string[] candidates,
        string[]? sigRequired = null,
        string[]? sigBonus = null,
        CancellationToken ct = default)
        => (await FindTableDetailedAsync(session, tables, candidates, sigRequired, sigBonus, ct)).TableName;

    private async Task<TableMatch> FindTableDetailedAsync(
        IAccessDataReaderSession session,
        IReadOnlyList<string> tables,
        string[] candidates,
        string[]? sigRequired = null,
        string[]? sigBonus = null,
        CancellationToken ct = default)
    {
        var normalized = tables.Select(t => new { Original = t, Key = Normalize(t) }).ToList();

        foreach (var candidate in candidates)
        {
            var key = Normalize(candidate);
            var exact = normalized.FirstOrDefault(x => x.Key == key);
            if (exact is not null)
                return new TableMatch(exact.Original, "exact");
        }

        foreach (var candidate in candidates)
        {
            var key = Normalize(candidate);
            var contains = normalized.FirstOrDefault(x => x.Key.Contains(key, StringComparison.Ordinal));
            if (contains is not null)
                return new TableMatch(contains.Original, "contains");
        }

        if (sigRequired?.Length > 0)
        {
            var semaphore = new SemaphoreSlim(Math.Max(1, _options.MaxMetadataParallelism));
            try
            {
                var requiredKeys = sigRequired.Select(Normalize).ToArray();
                var bonusKeys = sigBonus?.Select(Normalize).ToArray();

                var matches = await Task.WhenAll(tables.Select(async table =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var columns = await session.GetColumnsAsync(table, ct);
                        var normalizedColumns = columns
                            .Select(Normalize)
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToHashSet(StringComparer.Ordinal);

                        if (!requiredKeys.All(normalizedColumns.Contains))
                            return (Table: (string?)null, Score: -1, Columns: (IReadOnlyList<string>?)null);

                        var score = bonusKeys?.Count(normalizedColumns.Contains) ?? 0;
                        return (Table: (string?)table, Score: score, Columns: (IReadOnlyList<string>?)columns);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));

                var best = matches
                    .Where(x => x.Table is not null)
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if (best.Table is not null)
                    return new TableMatch(best.Table, "signature", best.Columns);
            }
            finally
            {
                semaphore.Dispose();
            }
        }

        return new TableMatch(null, "none");
    }

    private string? FindTable(OdbcConnection conn, IReadOnlyList<string> tables, string[] candidates, string[]? sigRequired = null, string[]? sigBonus = null)
        => FindTableDetailed(conn, tables, candidates, sigRequired, sigBonus).TableName;

    private TableMatch FindTableDetailed(
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

    private HashSet<string> ReadColumnNamesNormalized(OdbcConnection conn, string table)
    {
        return ReadColumnNames(conn, table)
            .Select(Normalize)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<HashSet<int>> ReadIdsFromAccessTableAsync(IAccessDataReaderSession session, string table, CancellationToken ct)
    {
        var ids = new HashSet<int>();
        try
        {
            await foreach (var row in session.ReadRowsAsync(table, ct))
            {
                var id = I(row, "id", "idprodaja", "saleid", "iddnevnik");
                if (id.HasValue && id.Value > 0)
                    ids.Add(id.Value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to pre-scan Access table {Table} for ids.", table);
        }
        return ids;
    }

    private async Task<HashSet<int>> EnsureProdajaZaglavljeExistsAsync(IEnumerable<int> missingIds, IAccessDataReaderSession session, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var toInsert = new HashSet<int>(missingIds);
        if (toInsert.Count == 0) return new HashSet<int>();

        var insertedIds = new HashSet<int>();
        try
        {
            var tables = await session.GetTablesAsync(false, ct);
            foreach (var t in tables)
            {
                if (toInsert.Count == 0) break;
                await foreach (var row in session.ReadRowsAsync(t, ct))
                {
                    var id = I(row, "id", "idprodaja", "saleid", "iddnevnik");
                    if (!id.HasValue) continue;
                    if (!toInsert.Contains(id.Value)) continue;

                    var e = new Domain.Model.Prodaja.ProdajaZaglavlje
                    {
                        Id = id.Value,
                        BrojRacuna = S(row, "brojracuna", "brojkalkulacije", "invoice", "receiptnumber"),
                        DatumProdaje = DT(row, "datumprodaje", "datum", "saledate") ?? DateTime.UtcNow,
                        NacinPlacanja = S(row, "nacinplacanja", "paymenttype"),
                        IDObjekat = I(row, "idobjekat", "storeid") ?? 0,
                        KorisnikIme = S(row, "korisnikime", "korisnik", "username", "operater", "kasir"),
                        DataOrigin = "access"
                    };

                    _trendDb.ProdajaZaglavlja.Add(e);
                    result.ProdajaInserted++;
                    insertedIds.Add(id.Value);
                    toInsert.Remove(id.Value);
                    TrackTrendWrite();

                    if (toInsert.Count == 0) break;
                }
                await FlushTrendWritesAsync(force: false, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while attempting to auto-insert missing prodaja_zaglavlje rows.");
        }

        if (insertedIds.Count > 0)
        {
            // Make sure parents exist in PostgreSQL before prodaja_stavke import continues.
            await FlushTrendWritesAsync(force: true, ct);
        }

        return insertedIds;
    }

    internal static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return string.Empty;

        return NormalizedStringCache.GetOrAdd(s, static key => NormalizeCore(key));
    }

    private static string NormalizeCore(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
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

    internal static string QuoteAccessIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return "[]";
        return $"[{identifier.Replace("]", "]]")}]";
    }

    internal static bool TryGetQuotedTableIdentifier(string? tableName, out string quotedIdentifier, out string failureReason)
    {
        quotedIdentifier = string.Empty;
        failureReason = string.Empty;

        if (string.IsNullOrWhiteSpace(tableName))
        {
            failureReason = "table name is empty";
            return false;
        }

        var trimmed = tableName.Trim();
        if (trimmed.IndexOfAny(['\0', '\r', '\n', ';']) >= 0)
        {
            failureReason = "table name contains prohibited characters";
            return false;
        }

        quotedIdentifier = QuoteAccessIdentifier(trimmed);
        return true;
    }

    private sealed record AccessFileSnapshot(string FilePath, bool IsSnapshot, string? Warning);

    private string CreateBackgroundWorkingCopy(string accessFilePath)
    {
        var storageRoot = string.IsNullOrWhiteSpace(_options.StorageRoot)
            ? Path.Combine(Path.GetTempPath(), "trendplus_access_jobs")
            : _options.StorageRoot;

        var tmpDir = Path.IsPathRooted(storageRoot)
            ? storageRoot
            : Path.Combine(Path.GetTempPath(), storageRoot);

        Directory.CreateDirectory(tmpDir);

        var ext = Path.GetExtension(accessFilePath);
        var baseName = Path.GetFileNameWithoutExtension(accessFilePath);
        var tmpName = $"{baseName}_job_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
        var tmpPath = Path.Combine(tmpDir, tmpName);

        File.Copy(accessFilePath, tmpPath, overwrite: true);
        return tmpPath;
    }

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
                Warning: $"Access baza deluje otvorena (pronaÄ‘en lock fajl '{Path.GetFileName(lockFilePath)}'). Koristi se snapshot kopija '{tmpName}'.");
        }
        catch (Exception ex)
        {
            return new AccessFileSnapshot(
                FilePath: accessFilePath,
                IsSnapshot: false,
                Warning: $"Access baza deluje otvorena (pronaÄ‘en lock fajl '{Path.GetFileName(lockFilePath)}'). Snapshot kopija nije uspela ({ex.GetType().Name}). Preporuka: zatvori Access pre importa.");
        }
    }

    private void TryDeleteFile(
        string path,
        string operation,
        string sourceFileName,
        long batchId = 0,
        string tableName = "snapshot")
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        for (var attempt = 1; attempt <= SnapshotDeleteMaxAttempts; attempt++)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                return;
            }
            catch (Exception ex)
            {
                if (attempt >= SnapshotDeleteMaxAttempts)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to delete snapshot file after retries. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. SourceFileName: {SourceFileName}. SnapshotPath: {SnapshotPath}. Attempts: {Attempts}.",
                        batchId,
                        tableName,
                        operation,
                        sourceFileName,
                        path,
                        attempt);
                    return;
                }

                Thread.Sleep(SnapshotDeleteRetryDelay);
            }
        }
    }

    private void TryAddSampleDataWarnings(
        IReadOnlyList<AccessDataRow> sampleRows,
        AccessImportTablePreview tablePreview,
        IReadOnlyCollection<string> requiredTargets,
        List<string> warnings)
    {
        if (tablePreview is null || !tablePreview.Found || string.IsNullOrWhiteSpace(tablePreview.TableName))
            return;

        try
        {
            var mappings = requiredTargets
                .Select(t =>
                {
                    var source = tablePreview.FieldMappings
                        .FirstOrDefault(m => m.TargetField.Equals(t, StringComparison.OrdinalIgnoreCase))
                        ?.SourceColumn;
                    return (Target: t, SourceKey: Normalize(source));
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.SourceKey))
                .ToList();

            if (mappings.Count == 0)
                return;

            var nullCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var nonPositiveCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var dupCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var idSets = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var rows = 0;

            foreach (var row in sampleRows)
            {
                rows++;
                foreach (var mapping in mappings)
                {
                    var target = mapping.Target;
                    row.TryGetValue(mapping.SourceKey, out var value);

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
            AddIf("Id", nullCount, 1, "âš ", warnings, tn, rows);
            AddIf("Naziv", nullCount, 1, "âš ", warnings, tn, rows);
            AddIf("Datum", nullCount, 1, "âš ", warnings, tn, rows);
            AddIf("DatumProdaje", nullCount, 1, "âš ", warnings, tn, rows);
            AddIf("TipPromene", nullCount, 1, "âš ", warnings, tn, rows);
            AddIf("IdProdaja", nullCount, 1, "âš ", warnings, tn, rows);
            AddIf("IdArtikal", nullCount, 1, "âš ", warnings, tn, rows);
            AddIf("Kolicina", nullCount, 1, "âš ", warnings, tn, rows);
            AddIf("Cena", nullCount, 1, "âš ", warnings, tn, rows);

            foreach (var (k, v) in dupCount.Where(x => x.Value > 0))
            {
                warnings.Add($"âš  Tabela '{tn}': duplikati u uzorku za '{k}' = {v} (od {rows} redova).");
            }

            foreach (var (k, v) in nonPositiveCount.Where(x => x.Value > 0))
            {
                warnings.Add($"âš  Tabela '{tn}': {v}/{rows} redova u uzorku ima '{k}' <= 0 ili nije broj.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Sample data analysis failed. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}.",
                0L,
                tablePreview.TableName ?? "unknown",
                "preview-sample-analysis");
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

    internal static Dictionary<TKey, TValue> ToFirstDictionary<TKey, TValue>(
        IEnumerable<TValue> source,
        Func<TValue, TKey> keySelector,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        var output = comparer is null
            ? new Dictionary<TKey, TValue>()
            : new Dictionary<TKey, TValue>(comparer);

        foreach (var item in source)
        {
            var key = keySelector(item);
            output.TryAdd(key, item);
        }

        return output;
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

    private static void MarkSourceRow(AccessImportRunResponse result, string key, int count = 1)
        => GetCoverageMetric(result, key).SourceRows += count;

    private void TrackTrendWrite()
    {
        _pendingTrendWrites++;
    }

    private static int CountImportedRows(AccessImportRunResponse result)
        => result.TipoviInserted
         + result.DobavljaciInserted
         + result.SezoneInserted
         + result.ArtikliInserted
         + result.ProdajaInserted
         + result.ProdajaStavkeInserted
         + result.DnevnikInserted
         + result.PovracajInserted
         + result.PovracajStavkeInserted
         + result.NivelacijeInserted
         + result.UnosRobeInserted
         + result.PovratnicaInserted
         + result.PrenosRobeInserted
         + result.ObjekatInserted
         + result.ProductsDimInserted
         + result.SalesFactsInserted
         + result.SalesLineFactsInserted
         + result.StoresInserted;

    private static int CountUpdatedRows(AccessImportRunResponse result)
        => result.TipoviUpdated
         + result.DobavljaciUpdated
         + result.SezoneUpdated
         + result.ArtikliUpdated
         + result.ProdajaUpdated
         + result.ProdajaStavkeUpdated
         + result.DnevnikUpdated
         + result.PovracajUpdated
         + result.PovracajStavkeUpdated
         + result.ObjekatUpdated
         + result.ProductsDimUpdated
         + result.SalesFactsUpdated
         + result.StoresUpdated;

    private static int CountSourceRows(AccessImportRunResponse result)
        => result.CoverageByTable.Values.Sum(x => Math.Max(0, x.SourceRows));

    private static int CountAcceptedRows(AccessImportRunResponse result)
        => result.CoverageByTable.Values.Sum(x => Math.Max(0, x.AcceptedRows));

    private static int ComputeProgressPercent(
        string? status,
        string? currentStep,
        string? currentTable,
        AccessImportRunResponse result)
    {
        if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase))
            return 100;

        if (string.Equals(currentStep, "queued", StringComparison.OrdinalIgnoreCase))
            return 1;

        if (string.Equals(currentStep, "starting", StringComparison.OrdinalIgnoreCase))
            return 3;

        if (!string.Equals(currentStep, "import", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(currentStep, "sync-analytics", StringComparison.OrdinalIgnoreCase))
                return 92;

            return Math.Clamp(CountImportedRows(result) > 0 ? 35 : 10, 1, 95);
        }

        if (string.IsNullOrWhiteSpace(currentTable))
            return 12;

        var normalizedTable = Normalize(currentTable);
        var tableIndex = Array.FindIndex(ProgressTableOrder, key =>
            key.Equals(normalizedTable, StringComparison.OrdinalIgnoreCase));

        if (tableIndex < 0)
            return Math.Clamp(CountImportedRows(result) > 0 ? 45 : 15, 1, 95);

        var progressFromTable = 10 + (int)Math.Round(((tableIndex + 1) / (double)ProgressTableOrder.Length) * 78.0);
        return Math.Clamp(progressFromTable, 1, 95);
    }

    private async Task FlushTrendWritesAsync(bool force, CancellationToken ct)
    {
        if (_pendingTrendWrites <= 0)
            return;

        if (!force && _pendingTrendWrites < Math.Max(1, _options.DbSaveBatchSize))
            return;

        if (_activeBatchId.HasValue)
            await EnsureBatchNotCancelledAsync(_activeBatchId.Value, ct);

        var writesToFlush = _pendingTrendWrites;
        await _trendDb.SaveChangesAsync(ct);
        _trendDb.ChangeTracker.Clear();
        _pendingTrendWrites = 0;
        _logger.LogInformation(
            "Access import DB flush completed. Step: {Step}. RowsWritten: {RowsWritten}. BatchSize: {BatchSize}. Force: {Force}.",
            "db-flush",
            writesToFlush,
            _options.DbSaveBatchSize,
            force);
        await PersistBatchProgressAsync("db-flush", force: true, ct);
    }

    private static string SerializeRowForDiagnostics(AccessDataRow row)
    {
        var payload = row.ToDictionary()
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value is DBNull ? null : kvp.Value,
                StringComparer.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(payload);
        const int maxLength = 4000;
        return json.Length <= maxLength
            ? json
            : json[..maxLength] + "...(truncated)";
    }

    private void AddAccessImportLogEntry(long batchId, string tableName, int rowIndex, string severity, string message, string? sourceRowJson = null)
    {
        if (batchId <= 0)
            return;

        _trendDb.AccessImportLogs.Add(new AccessImportLog
        {
            BatchId = batchId,
            TableName = TrimToMaxLength(tableName, 128),
            RowIndex = Math.Max(0, rowIndex),
            Severity = TrimToMaxLength(string.IsNullOrWhiteSpace(severity) ? "info" : severity.Trim(), 16),
            Message = TrimToMaxLength(message, 2000),
            SourceRowJson = sourceRowJson
        });
    }

    private static string TrimToMaxLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
            return trimmed;

        return trimmed[..Math.Max(0, maxLength - 14)] + "...(truncated)";
    }

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

    private static object? Get(AccessDataRow row, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (row.TryGetValue(alias, out var value))
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

    private static string? S(AccessDataRow row, params string[] aliases)
    {
        var v = Get(row, aliases);
        var s = v is null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static int? I(AccessDataRow row, params string[] aliases) => ConvertToInt(Get(row, aliases));
    private static decimal? D(AccessDataRow row, params string[] aliases) => ConvertToDecimal(Get(row, aliases));
    private static DateTime? DT(AccessDataRow row, params string[] aliases) => ConvertToDate(Get(row, aliases));

    internal static int? ConvertToInt(object? v)
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
        // No platform restriction - ODBC works on Windows, Linux, macOS, and Docker.
        // Falls back to mdb-tables / mdb-export CLI if ODBC driver is broken on Linux.
    }

    // ======================================================================
    // MDBTools CLI fallback - used when the ODBC driver fails on Linux/Docker
    // ======================================================================

    private static bool IsMdbToolsCliAvailable()
    {
        if (OperatingSystem.IsWindows()) return false;
        try
        {
            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "mdb-tables",
                Arguments = "--help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            proc.Start();
            proc.WaitForExit(3000);
            return true;
        }
        catch { return false; }
    }

    private static string RunMdbCli(string command, string args, int timeoutMs = 30000)
    {
        using var proc = new System.Diagnostics.Process();
        proc.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        proc.Start();
        var output = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(timeoutMs);
        if (proc.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
            throw new InvalidOperationException($"{command} failed: {stderr}");
        return output;
    }

    private static List<string> MdbCliGetTables(string filePath)
    {
        var output = RunMdbCli("mdb-tables", $"-1 \"{filePath}\"");
        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !t.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static int MdbCliRowCount(string filePath, string tableName)
    {
        try
        {
            var csv = RunMdbCli("mdb-export", $"-H \"{filePath}\" \"{tableName}\"");
            return csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        }
        catch { return 0; }
    }

    private static List<string> MdbCliGetColumnsRaw(string filePath, string tableName)
    {
        var cols = new List<string>();
        try
        {
            var csv = RunMdbCli("mdb-export", $"\"{filePath}\" \"{tableName}\"");
            var header = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (header is null)
                return cols;

            foreach (var col in ParseCsvLine(header))
            {
                var clean = col.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(clean))
                    cols.Add(clean);
            }
        }
        catch
        {
        }

        return cols;
    }

    private static HashSet<string> MdbCliGetColumns(string filePath, string tableName)
    {
        var cols = new HashSet<string>(StringComparer.Ordinal);
        foreach (var col in MdbCliGetColumnsRaw(filePath, tableName))
            cols.Add(Normalize(col));
        return cols;
    }

    private static IEnumerable<Dictionary<string, object?>> MdbCliReadRows(string filePath, string tableName)
    {
        string csv;
        try
        {
            csv = RunMdbCli("mdb-export", $"\"{filePath}\" \"{tableName}\"", timeoutMs: 60000);
        }
        catch { yield break; }

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) yield break;

        var headers = ParseCsvLine(lines[0]);
        var normalizedHeaders = headers.Select(h => Normalize(h.Trim().Trim('"'))).ToArray();

        for (var i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < normalizedHeaders.Length && j < values.Length; j++)
            {
                var val = values[j].Trim().Trim('"');
                row[normalizedHeaders[j]] = string.IsNullOrEmpty(val) ? null : (object)val;
            }
            yield return row;
        }
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuote = false;
        var current = new StringBuilder();

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuote = !inQuote;
                current.Append(ch);
            }
            else if (ch == ',' && !inQuote)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
