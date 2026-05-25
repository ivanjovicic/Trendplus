using System.Data;
using System.Data.Odbc;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Diagnostics;
using Api.Config;
using Api.Models;
using Api.Services.Access;
using Application.Common.Interfaces;
using Domain.Model;
using Domain.Model.Povracaj;
using Infrastructure.DbContexts;
using Infrastructure.Configuration;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
    public interface IAccessImportService
    {
        Task<AccessImportPreviewResponse> PreviewAsync(string accessFilePath, bool includeTemporaryTables = false, CancellationToken ct = default);
        Task<AccessImportRunResponse> ImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default);
        Task<AccessImportRunResponse> StartImportAsync(string accessFilePath, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, CancellationToken ct = default);
        Task<AccessImportRunResponse> RunExistingBatchAsync(long batchId, string accessFilePath, string sourceFileName, bool includeAnalytics, bool overwriteExisting, bool includeTemporaryTables = false, bool deleteWorkingFileAfterCompletion = false, CancellationToken ct = default);
        Task RefreshBatchStatusesAsync(long? batchId = null, CancellationToken ct = default);
        Task<List<AccessImportBatchDto>> GetRecentBatchStatusesAsync(int take = 20, CancellationToken ct = default);
        Task<List<AccessImportBatchDto>> GetRecentBatchesAsync(int take = 20, CancellationToken ct = default);
        Task<AccessImportBatchDto?> GetBatchAsync(long batchId, CancellationToken ct = default);
        Task<bool> RequestCancellationAsync(long batchId, CancellationToken ct = default);
        Task MarkBatchInterruptedAsync(long batchId, CancellationToken ct = default);
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
                new FieldAlias("Cena", "prodajnacena", "cena", "unitprice", "price"),
                new FieldAlias("NabavnaCena", "nabavnacenadin", "purchasepricersd", "nabavnacena", "purchaseprice", "cost", "nc")
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
    private static readonly string[] ArtikliIdAliases = ["id", "idartikal", "productid"];
    private static readonly string[] ArtikliNazivAliases =
    [
        "naziv", "nazivartikal", "nazivarticle", "nazivproizvoda",
        "opis", "opisartikal", "opisproizvoda", "description", "desc",
        "proizvod", "name", "productname", "articlename", "itemname", "ime",
        "artikal", "article", "item", "roba"
    ];
    private static readonly string[] ArtikliPluAliases = ["plu", "sku", "sifra", "sifraartikla", "barcode", "barkod", "kod", "code", "artikal"];
    private static readonly string[] ArtikliTipAliases = ["idtipobuce", "tipobuceid", "footweartypeid"];
    private static readonly string[] ArtikliDobavljacAliases = ["iddobavljac", "dobavljacid", "supplierid"];
    private static readonly string[] ArtikliNabavnaCenaAliases = ["nabavnacena", "purchaseprice", "cost"];
    private static readonly string[] ArtikliNabavnaCenaDinAliases = ["nabavnacenadin", "purchasepricersd"];
    private static readonly string[] ProdajaLineNabavnaCenaAliases =
        ["nabavnacenadin", "purchasepricersd", "nabavnacena", "purchaseprice", "cost", "nc"];
    private static readonly string[] ArtikliPrvaProdajnaCenaAliases = ["prvaprodajnacena", "firstsaleprice"];
    private static readonly string[] ArtikliProdajnaCenaAliases = ["prodajnacena", "saleprice", "price"];
    private static readonly string[] ArtikliVelicinaAliases = ["velicina", "size"];
    private static readonly string[] ArtikliBojaAliases = ["boja", "color"];
    private static readonly string[] ArtikliMaterijalAliases =
    [
        "materijal", "material", "materijal_gornjista", "gornjiste",
        "upper", "fabric", "sastav", "sastav_gornjista"
    ];
    private static readonly string[] ArtikliKolicinaAliases =
    [
        "kolicina", "kol", "qty", "quantity", "stock", "stanje", "stanjeartikla",
        "stanjeartikal", "lager", "zaliha", "zalihe", "raspolozivo", "inventar",
        "stockqty", "totalqty", "total_qty", "raspolozivokolicina"
    ];
    private static readonly string[] ArtikliMinimalnaKolicinaAliases = ["minimalnakolicina", "minimumqty", "minqty", "minstock"];
    private static readonly string[] ArtikliKomentarAliases = ["komentar", "comment", "napomena", "url"];
    private static readonly string[] ArtikliObjekatAliases = ["idobjekat", "storeid"];
    private static readonly string[] ArtikliSezonaAliases = ["idsezona", "seasonid"];
    private static readonly string[] ArtikliKategorijaAliases = ["kategorija", "category"];
    private static readonly string[] ArtikliPolAliases = ["pol", "gender"];
    private static readonly string[] ArtikliImagePathAliases = ["imagepath", "imageurl", "slika", "image"];
    private static readonly string[] DefaultCursorTimestampAliases = ["updatedat", "lastmodified", "datumizmene", "datumpromene", "modifiedat"];
    private static readonly IReadOnlyDictionary<string, string[]> IncrementalIdAliasesByTable =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["tipovi_obuce"] = ["id", "idtipobuce", "tipid"],
            ["dobavljaci"] = ["id", "iddobavljac", "supplierid"],
            ["sezone"] = ["id", "idsezona", "seasonid"],
            ["artikli"] = ArtikliIdAliases,
            ["dnevnik_promena"] = ["id", "iddnevnik", "iddnevnikpromene", "iddnevnikpromena", "idlog", "seqno"],
            ["prodaja_zaglavlje"] = ["id", "idprodaja", "saleid", "iddnevnik"],
            ["prodaja_stavke"] = ["id", "idstavka", "lineid"],
            ["povracaj_zaglavlje"] = ["id", "idpovracaj", "returnid"],
            ["povracaj_stavke"] = ["id", "idstavka", "lineid"],
            ["nivelacije"] = ["iddnevnik", "id", "idlog"],
            ["unos_robe"] = ["iddnevnik", "id", "idlog"],
            ["povratnice"] = ["iddnevnik", "id", "idpovratnice", "idlog"],
            ["prenos_robe"] = ["iddnevnik", "id", "brprenos", "idlog"]
        };

    private sealed class ActiveIncrementalTableScope
    {
        public required long BatchId { get; init; }
        public required string TableKey { get; init; }
        public required string SourceTableName { get; init; }
        public required string CursorMode { get; init; }
        public required string[] TimestampAliases { get; init; }
        public required string[] IdAliases { get; init; }
        public required bool ApplyFilter { get; init; }
        public required bool CommitOnSuccess { get; init; }
        public required int OverlapSeconds { get; init; }
        public required DateTime? CursorTimestampUtc { get; init; }
        public required long? CursorId { get; init; }
        public required long? CursorTieBreakerId { get; init; }
        public required int WriteBatchSize { get; init; }
        public required int LeaseDurationSeconds { get; init; }
        public required DateTime LastLeaseRenewedUtc { get; set; }
        public bool LeaseAcquired { get; set; }
        public long RowsScanned { get; set; }
        public long RowsFilteredOut { get; set; }
        public long RowsPassedFilter { get; set; }
        public long RowsAccepted { get; set; }
        public DateTime? MaxSeenTimestampUtc { get; set; }
        public long? MaxSeenId { get; set; }
        public long? MaxSeenTieBreakerId { get; set; }
        public bool MissingCursorAliasLogged { get; set; }
    }

    private sealed class IncrementalTableSnapshot
    {
        public string TableKey { get; init; } = string.Empty;
        public string SourceTableName { get; init; } = string.Empty;
        public string CursorMode { get; init; } = string.Empty;
        public bool AppliedFilter { get; init; }
        public bool CommittedCursor { get; init; }
        public DateTime? CursorBeforeTimestampUtc { get; init; }
        public long? CursorBeforeId { get; init; }
        public long? CursorBeforeTieBreakerId { get; init; }
        public DateTime? CursorAfterTimestampUtc { get; init; }
        public long? CursorAfterId { get; init; }
        public long? CursorAfterTieBreakerId { get; init; }
        public long RowsScanned { get; init; }
        public long RowsFilteredOut { get; init; }
        public long RowsPassedFilter { get; init; }
        public long RowsAccepted { get; init; }
        public string Status { get; init; } = "completed";
        public string? Error { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    private readonly TrendplusDbContext _trendDb;
    private readonly AnalyticsDbContext _analyticsDb;
    private readonly IAnalyticsCacheService? _analyticsCache;
    private readonly ILogger<AccessImportService> _logger;
    private readonly AccessImportOptions _options;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly IAccessImportJobQueue? _jobQueue;
    private readonly IAccessImportCursorRepository? _cursorRepository;
    private readonly IFileStorage? _fileStorage;
    private readonly string _storageProviderName;
    private readonly TimeSpan _storageUploadTimeout;
    private readonly string _incrementalLeaseOwner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    private ActiveIncrementalTableScope? _activeIncrementalScope;
    private readonly Dictionary<string, IncrementalTableSnapshot> _incrementalTableSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim BatchSchemaBootstrapLock = new(1, 1);
    private static volatile bool _batchSchemaBootstrapCompleted;

    // Populated by ImportTrendplus, consumed by SyncAnalyticsAsync for StoresDim upsert
    private Dictionary<int, (string Name, string? Address, string? Phone, string? Manager)> _importedStores = [];
    private readonly HashSet<int> _analyticsDeltaProductIds = [];
    private readonly HashSet<int> _analyticsDeltaSaleIds = [];
    private readonly HashSet<int> _analyticsDeltaMovementIds = [];
    private readonly HashSet<int> _analyticsDeltaSupplierIds = [];
    private readonly HashSet<int> _analyticsDeltaSeasonIds = [];
    private readonly HashSet<int> _analyticsDeltaTypeIds = [];
    private readonly HashSet<int> _analyticsDeltaStoreIds = [];

    // CLI fallback state
    private bool _useCliMode;
    private string? _cliFilePath;
    private int _pendingTrendWrites;
    private long? _activeBatchId;
    private AccessImportRunResponse? _activeBatchResult;
    private string? _activeBatchStep;
    private string? _activeBatchTable;
    private DateTime _lastBatchHeartbeatPersistedUtc;
    private DateTime _lastTrendFlushUtc;
    private int _trendFlushCount;
    private int _batchHeartbeatPersistCount;

    public AccessImportService(
        TrendplusDbContext trendDb,
        AnalyticsDbContext analyticsDb,
        ILogger<AccessImportService> logger,
        IOptions<AccessImportOptions>? options = null,
        IAnalyticsCacheService? analyticsCache = null,
        IServiceScopeFactory? serviceScopeFactory = null,
        IAccessImportJobQueue? jobQueue = null,
        IAccessImportCursorRepository? cursorRepository = null,
        IFileStorage? fileStorage = null,
        IOptions<StorageOptions>? storageOptions = null)
    {
        _trendDb = trendDb;
        _analyticsDb = analyticsDb;
        _logger = logger;
        _options = options?.Value ?? new AccessImportOptions();
        _analyticsCache = analyticsCache;
        _serviceScopeFactory = serviceScopeFactory;
        _jobQueue = jobQueue;
        _cursorRepository = cursorRepository;
        _fileStorage = fileStorage;
        var effectiveStorageOptions = storageOptions?.Value ?? new StorageOptions();
        _storageProviderName = NormalizeStorageProviderName(effectiveStorageOptions.Provider);
        _storageUploadTimeout = TimeSpan.FromSeconds(Math.Max(5, effectiveStorageOptions.UploadTimeoutSeconds));
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
        _lastTrendFlushUtc = DateTime.UtcNow;
        _trendFlushCount = 0;
        _batchHeartbeatPersistCount = 0;
        _incrementalTableSnapshots.Clear();
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
        _lastTrendFlushUtc = DateTime.MinValue;
        _trendFlushCount = 0;
        _batchHeartbeatPersistCount = 0;
        _incrementalTableSnapshots.Clear();
    }

    private void ResetAnalyticsDeltaTracking()
    {
        _analyticsDeltaProductIds.Clear();
        _analyticsDeltaSaleIds.Clear();
        _analyticsDeltaMovementIds.Clear();
        _analyticsDeltaSupplierIds.Clear();
        _analyticsDeltaSeasonIds.Clear();
        _analyticsDeltaTypeIds.Clear();
        _analyticsDeltaStoreIds.Clear();
    }

    private bool IsIncrementalFeatureEnabled()
        => _options.Incremental.Enabled;

    private bool IsIncrementalWriteMode()
        => IsIncrementalFeatureEnabled()
           && string.Equals(_options.Incremental.Mode, "incremental", StringComparison.OrdinalIgnoreCase);

    private bool IsIncrementalShadowMode()
        => IsIncrementalFeatureEnabled()
           && string.Equals(_options.Incremental.Mode, "shadow", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCursorMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return "id";

        var normalized = mode.Trim().ToLowerInvariant();
        return normalized switch
        {
            "timestamp" or "id" or "none" or "timestamp_then_id" or "id_or_composite" => normalized,
            _ => "id"
        };
    }

    internal static bool ShouldSkipLinkedTablesByDnevnikTrigger(
        bool incrementalWriteMode,
        bool dnevnikTablePresent,
        int dnevnikImportedDelta)
        => incrementalWriteMode
           && dnevnikTablePresent
           && dnevnikImportedDelta <= 0;

    private AccessIncrementalTableProfile? ResolveIncrementalProfile(string tableKey)
    {
        if (!IsIncrementalFeatureEnabled())
            return null;

        if (_options.Incremental.Profiles is null || _options.Incremental.Profiles.Count == 0)
            return null;

        var normalizedTableKey = Normalize(tableKey);
        return _options.Incremental.Profiles
            .FirstOrDefault(x =>
                x.Enabled &&
                Normalize(x.TableKey).Equals(normalizedTableKey, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] BuildNormalizedAliasArray(IEnumerable<string>? aliases, IReadOnlyList<string> fallbackAliases)
    {
        var selected = aliases is null
            ? fallbackAliases
            : aliases.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var normalized = selected
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return normalized.Length == 0
            ? fallbackAliases.Select(Normalize)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : normalized;
    }

    private static string[] ResolveIncrementalIdAliases(string tableKey, AccessIncrementalTableProfile profile)
    {
        var fallback = IncrementalIdAliasesByTable.TryGetValue(tableKey, out var aliases)
            ? aliases
            : ["id"];
        return BuildNormalizedAliasArray(profile.CursorIdAliases, fallback);
    }

    private static string[] ResolveIncrementalTimestampAliases(AccessIncrementalTableProfile profile)
        => BuildNormalizedAliasArray(profile.CursorTimestampAliases, DefaultCursorTimestampAliases);

    private static int ResolveIncrementalOverlapSeconds(AccessIncrementalTableProfile profile, AccessIncrementalOptions incrementalOptions)
    {
        var raw = profile.OverlapSeconds ?? incrementalOptions.DefaultOverlapSeconds;
        return Math.Clamp(raw, 0, 3600);
    }

    private int ResolveIncrementalWriteBatchSize(AccessIncrementalTableProfile profile)
    {
        var raw = profile.BatchSize ?? _options.DbSaveBatchSize;
        return Math.Max(1, raw);
    }

    private async Task<ActiveIncrementalTableScope?> BeginIncrementalTableScopeAsync(
        long batchId,
        string tableKey,
        string? tableName,
        CancellationToken ct)
    {
        if (!IsIncrementalFeatureEnabled())
            return null;

        if (_cursorRepository is null)
            return null;

        var profile = ResolveIncrementalProfile(tableKey);
        if (profile is null)
            return null;

        var normalizedMode = NormalizeCursorMode(profile.CursorMode);
        if (string.Equals(normalizedMode, "none", StringComparison.OrdinalIgnoreCase))
            return null;

        var cursor = await _cursorRepository.GetOrCreateAsync(tableKey, normalizedMode, ct);
        var leaseDurationSeconds = Math.Clamp(Math.Max(_options.HeartbeatIntervalSeconds, _options.StatusUpdateThrottleSeconds) * 3, 30, 600);
        var leaseAcquired = await _cursorRepository.TryAcquireLeaseAsync(
            tableKey,
            _incrementalLeaseOwner,
            TimeSpan.FromSeconds(leaseDurationSeconds),
            ct);

        if (!leaseAcquired)
        {
            throw new InvalidOperationException(
                $"Incremental import lease is busy for table '{tableKey}'. Retry after the current worker finishes.");
        }

        await _cursorRepository.MarkRunStartedAsync(tableKey, ct);

        return new ActiveIncrementalTableScope
        {
            BatchId = batchId,
            TableKey = tableKey,
            SourceTableName = tableName ?? tableKey,
            CursorMode = normalizedMode,
            TimestampAliases = ResolveIncrementalTimestampAliases(profile),
            IdAliases = ResolveIncrementalIdAliases(tableKey, profile),
            ApplyFilter = IsIncrementalWriteMode(),
            CommitOnSuccess = IsIncrementalWriteMode(),
            OverlapSeconds = ResolveIncrementalOverlapSeconds(profile, _options.Incremental),
            CursorTimestampUtc = cursor.CursorTimestampUtc,
            CursorId = cursor.CursorId,
            CursorTieBreakerId = cursor.CursorTieBreakerId,
            WriteBatchSize = ResolveIncrementalWriteBatchSize(profile),
            LeaseDurationSeconds = leaseDurationSeconds,
            LastLeaseRenewedUtc = DateTime.UtcNow,
            LeaseAcquired = true
        };
    }

    private async Task CompleteIncrementalTableScopeAsync(ActiveIncrementalTableScope scope, CancellationToken ct)
    {
        if (_cursorRepository is null)
            return;

        var hasCursorProgress =
            scope.MaxSeenTimestampUtc.HasValue ||
            scope.MaxSeenId.HasValue ||
            scope.MaxSeenTieBreakerId.HasValue;

        if (scope.CommitOnSuccess && hasCursorProgress)
        {
            var nextTimestamp = scope.MaxSeenTimestampUtc;
            var nextId = scope.MaxSeenId;
            var nextTieBreakerId = scope.MaxSeenTieBreakerId;

            await _cursorRepository.CommitCursorAsync(
                scope.TableKey,
                nextTimestamp,
                nextId,
                nextTieBreakerId,
                checked((int)Math.Min(int.MaxValue, Math.Max(0, scope.RowsScanned))),
                checked((int)Math.Min(int.MaxValue, Math.Max(0, scope.RowsAccepted))),
                nextTimestamp.HasValue
                    ? checked((int)Math.Min(int.MaxValue, Math.Max(0, (DateTime.UtcNow - nextTimestamp.Value).TotalSeconds)))
                    : null,
                scope.BatchId,
                ct);
        }

        await _cursorRepository.MarkRunCompletedAsync(scope.TableKey, ct);

        _logger.LogInformation(
            "Access incremental scope completed. BatchId: {BatchId}. TableKey: {TableKey}. SourceTable: {SourceTable}. Mode: {Mode}. ApplyFilter: {ApplyFilter}. CommitOnSuccess: {CommitOnSuccess}. RowsScanned: {RowsScanned}. RowsPassedFilter: {RowsPassedFilter}. RowsFilteredOut: {RowsFilteredOut}. RowsAccepted: {RowsAccepted}. NextCursorTimestampUtc: {NextCursorTimestampUtc}. NextCursorId: {NextCursorId}.",
            scope.BatchId,
            scope.TableKey,
            scope.SourceTableName,
            scope.CursorMode,
            scope.ApplyFilter,
            scope.CommitOnSuccess,
            scope.RowsScanned,
            scope.RowsPassedFilter,
            scope.RowsFilteredOut,
            scope.RowsAccepted,
            scope.MaxSeenTimestampUtc,
            scope.MaxSeenId);
    }

    private async Task MarkIncrementalTableScopeFailedAsync(ActiveIncrementalTableScope scope, Exception ex)
    {
        if (_cursorRepository is null)
            return;

        try
        {
            await _cursorRepository.MarkFailureAsync(scope.TableKey, ex.GetBaseException().Message, CancellationToken.None);
        }
        catch (Exception innerEx)
        {
            _logger.LogDebug(
                innerEx,
                "Access incremental scope failure marker could not be persisted. BatchId: {BatchId}. TableKey: {TableKey}.",
                scope.BatchId,
                scope.TableKey);
        }
    }

    private async Task ReleaseIncrementalTableScopeAsync(ActiveIncrementalTableScope scope)
    {
        if (_cursorRepository is null || !scope.LeaseAcquired)
            return;

        try
        {
            await _cursorRepository.ReleaseLeaseAsync(scope.TableKey, _incrementalLeaseOwner, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Access incremental scope lease release failed. BatchId: {BatchId}. TableKey: {TableKey}.",
                scope.BatchId,
                scope.TableKey);
        }
    }

    private void CaptureIncrementalSnapshot(ActiveIncrementalTableScope scope, string status, string? error = null)
    {
        var committedCursor =
            scope.CommitOnSuccess &&
            string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) &&
            (scope.MaxSeenTimestampUtc.HasValue || scope.MaxSeenId.HasValue || scope.MaxSeenTieBreakerId.HasValue);

        _incrementalTableSnapshots[scope.TableKey] = new IncrementalTableSnapshot
        {
            TableKey = scope.TableKey,
            SourceTableName = scope.SourceTableName,
            CursorMode = scope.CursorMode,
            AppliedFilter = scope.ApplyFilter,
            CommittedCursor = committedCursor,
            CursorBeforeTimestampUtc = scope.CursorTimestampUtc,
            CursorBeforeId = scope.CursorId,
            CursorBeforeTieBreakerId = scope.CursorTieBreakerId,
            CursorAfterTimestampUtc = scope.MaxSeenTimestampUtc,
            CursorAfterId = scope.MaxSeenId,
            CursorAfterTieBreakerId = scope.MaxSeenTieBreakerId,
            RowsScanned = scope.RowsScanned,
            RowsFilteredOut = scope.RowsFilteredOut,
            RowsPassedFilter = scope.RowsPassedFilter,
            RowsAccepted = scope.RowsAccepted,
            Status = status,
            Error = string.IsNullOrWhiteSpace(error) ? null : TrimToMaxLength(error, 1000),
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private string? BuildBatchCursorSnapshotJson()
    {
        if (_incrementalTableSnapshots.Count == 0)
            return null;

        var payload = new
        {
            mode = _options.Incremental.Mode,
            capturedAtUtc = DateTime.UtcNow,
            tables = _incrementalTableSnapshots.Values
                .OrderBy(x => x.TableKey, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        return JsonSerializer.Serialize(payload);
    }

    private static int? ReadCursorInt(AccessDataRow row, IReadOnlyList<string> normalizedAliases)
    {
        for (var i = 0; i < normalizedAliases.Count; i++)
        {
            var value = INormalized(row, normalizedAliases[i]);
            if (value.HasValue)
                return value;
        }

        return null;
    }

    private static DateTime? ReadCursorTimestamp(AccessDataRow row, IReadOnlyList<string> normalizedAliases)
    {
        for (var i = 0; i < normalizedAliases.Count; i++)
        {
            var value = ConvertToDate(GetNormalized(row, normalizedAliases[i]));
            if (value.HasValue)
                return value;
        }

        return null;
    }

    private void ApplyAccessSourceLineage(IAccessImportSourceLineage entity, string tableKey, AccessDataRow row)
    {
        var scope = _activeIncrementalScope;
        var useScopeAliases = scope is not null && scope.TableKey.Equals(tableKey, StringComparison.OrdinalIgnoreCase);
        var idAliases = useScopeAliases
            ? scope!.IdAliases
            : ResolveDefaultLineageIdAliases(tableKey);
        var timestampAliases = useScopeAliases
            ? scope!.TimestampAliases
            : ResolveDefaultLineageTimestampAliases();

        entity.SourceTableKey = Normalize(tableKey);
        entity.SourceRowId = ReadCursorInt(row, idAliases);
        entity.SourceUpdatedAtUtc = ReadCursorTimestamp(row, timestampAliases);
        entity.SourceHash = ComputeAccessRowHash(row);
        entity.SourceBatchId = _activeBatchId;
    }

    private bool ShouldSkipStaleOrUnchangedAccessOverwrite(
        IAccessImportSourceLineage existing,
        string tableKey,
        AccessDataRow row)
    {
        var incomingTimestamp = ReadCursorTimestamp(row, ResolveLineageTimestampAliasesForTable(tableKey));
        var incomingRowId = ReadCursorInt(row, ResolveLineageIdAliasesForTable(tableKey));

        if (incomingTimestamp.HasValue && existing.SourceUpdatedAtUtc.HasValue)
        {
            if (existing.SourceUpdatedAtUtc.Value > incomingTimestamp.Value)
            {
                _logger.LogInformation(
                    "Access import skipped stale overwrite. TableKey: {TableKey}. ExistingSourceTimestamp: {ExistingSourceTimestamp}. IncomingSourceTimestamp: {IncomingSourceTimestamp}. ExistingSourceRowId: {ExistingSourceRowId}. IncomingSourceRowId: {IncomingSourceRowId}.",
                    tableKey,
                    existing.SourceUpdatedAtUtc,
                    incomingTimestamp,
                    existing.SourceRowId,
                    incomingRowId);
                MarkStaleOverwriteSkipped();
                return true;
            }

            if (existing.SourceUpdatedAtUtc.Value == incomingTimestamp.Value &&
                existing.SourceRowId.HasValue &&
                incomingRowId.HasValue &&
                existing.SourceRowId.Value > incomingRowId.Value)
            {
                _logger.LogInformation(
                    "Access import skipped stale tie-breaker overwrite. TableKey: {TableKey}. SourceTimestamp: {SourceTimestamp}. ExistingSourceRowId: {ExistingSourceRowId}. IncomingSourceRowId: {IncomingSourceRowId}.",
                    tableKey,
                    incomingTimestamp,
                    existing.SourceRowId,
                    incomingRowId);
                MarkStaleOverwriteSkipped();
                return true;
            }
        }

        var incomingHash = ComputeAccessRowHash(row);
        return !string.IsNullOrWhiteSpace(existing.SourceHash) &&
               string.Equals(existing.SourceHash, incomingHash, StringComparison.Ordinal);
    }

    private void MarkStaleOverwriteSkipped()
    {
        if (_activeBatchResult is not null)
            _activeBatchResult.RowsSkippedStale++;
    }

    private sealed class AccessLineageSnapshot : IAccessImportSourceLineage
    {
        public string? SourceTableKey { get; set; }
        public long? SourceRowId { get; set; }
        public DateTime? SourceUpdatedAtUtc { get; set; }
        public string? SourceHash { get; set; }
        public long? SourceBatchId { get; set; }
    }

    private string[] ResolveLineageIdAliasesForTable(string tableKey)
    {
        var scope = _activeIncrementalScope;
        return scope is not null && scope.TableKey.Equals(tableKey, StringComparison.OrdinalIgnoreCase)
            ? scope.IdAliases
            : ResolveDefaultLineageIdAliases(tableKey);
    }

    private string[] ResolveLineageTimestampAliasesForTable(string tableKey)
    {
        var scope = _activeIncrementalScope;
        return scope is not null && scope.TableKey.Equals(tableKey, StringComparison.OrdinalIgnoreCase)
            ? scope.TimestampAliases
            : ResolveDefaultLineageTimestampAliases();
    }

    private static string[] ResolveDefaultLineageIdAliases(string tableKey)
    {
        var aliases = IncrementalIdAliasesByTable.TryGetValue(tableKey, out var configured)
            ? configured
            : ["id"];

        return aliases
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] ResolveDefaultLineageTimestampAliases()
        => DefaultCursorTimestampAliases
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string ComputeAccessRowHash(AccessDataRow row)
    {
        var builder = new StringBuilder();
        foreach (var item in row.ToDictionary().OrderBy(x => Normalize(x.Key), StringComparer.OrdinalIgnoreCase))
        {
            builder
                .Append(Normalize(item.Key))
                .Append('\u001f')
                .Append(NormalizeLineageHashValue(item.Value))
                .Append('\u001e');
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string NormalizeLineageHashValue(object? value)
    {
        if (value is null or DBNull)
            return string.Empty;

        return value switch
        {
            DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
            decimal d => d.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString()?.Trim() ?? string.Empty
        };
    }

    private bool ShouldIncludeRowForIncrementalScope(ActiveIncrementalTableScope scope, AccessDataRow row)
    {
        scope.RowsScanned++;
        if (!scope.ApplyFilter)
            return true;

        var effectiveCursorTimestamp = scope.CursorTimestampUtc?.AddSeconds(-scope.OverlapSeconds);
        var rowTimestamp = ReadCursorTimestamp(row, scope.TimestampAliases);
        var rowId = ReadCursorInt(row, scope.IdAliases);

        bool include;
        switch (scope.CursorMode)
        {
            case "timestamp":
                include = !effectiveCursorTimestamp.HasValue || !rowTimestamp.HasValue || rowTimestamp.Value >= effectiveCursorTimestamp.Value;
                break;
            case "timestamp_then_id":
                include = EvaluateTimestampThenIdCursor(scope, rowTimestamp, rowId, effectiveCursorTimestamp);
                break;
            case "id_or_composite":
            case "id":
            default:
                include = !scope.CursorId.HasValue || !rowId.HasValue || rowId.Value > scope.CursorId.Value;
                break;
        }

        if (include)
            scope.RowsPassedFilter++;
        else
            scope.RowsFilteredOut++;

        if (!rowId.HasValue && !rowTimestamp.HasValue && !scope.MissingCursorAliasLogged)
        {
            scope.MissingCursorAliasLogged = true;
            _logger.LogWarning(
                "Access incremental scope could not resolve cursor aliases for table {TableKey}. Falling back to pass-through rows for this step.",
                scope.TableKey);
        }

        return include;
    }

    private static bool EvaluateTimestampThenIdCursor(
        ActiveIncrementalTableScope scope,
        DateTime? rowTimestamp,
        int? rowId,
        DateTime? effectiveCursorTimestamp)
    {
        if (!effectiveCursorTimestamp.HasValue)
            return true;

        if (!rowTimestamp.HasValue)
        {
            if (!scope.CursorId.HasValue || !rowId.HasValue)
                return true;
            return rowId.Value > scope.CursorId.Value;
        }

        if (rowTimestamp.Value > effectiveCursorTimestamp.Value)
            return true;
        if (rowTimestamp.Value < effectiveCursorTimestamp.Value)
            return false;

        if (!scope.CursorTieBreakerId.HasValue || !rowId.HasValue)
            return true;

        return rowId.Value > scope.CursorTieBreakerId.Value;
    }

    private void TrackIncrementalAcceptedRow(string tableKey, AccessDataRow row)
    {
        var scope = _activeIncrementalScope;
        if (scope is null)
            return;
        if (!scope.TableKey.Equals(tableKey, StringComparison.OrdinalIgnoreCase))
            return;

        scope.RowsAccepted++;

        var rowTimestamp = ReadCursorTimestamp(row, scope.TimestampAliases);
        var rowId = ReadCursorInt(row, scope.IdAliases);
        if (rowId.HasValue)
            scope.MaxSeenId = !scope.MaxSeenId.HasValue ? rowId.Value : Math.Max(scope.MaxSeenId.Value, rowId.Value);

        if (!rowTimestamp.HasValue)
            return;

        if (!scope.MaxSeenTimestampUtc.HasValue || rowTimestamp.Value > scope.MaxSeenTimestampUtc.Value)
        {
            scope.MaxSeenTimestampUtc = rowTimestamp.Value;
            scope.MaxSeenTieBreakerId = rowId;
            return;
        }

        if (rowTimestamp.Value == scope.MaxSeenTimestampUtc.Value && rowId.HasValue)
        {
            scope.MaxSeenTieBreakerId = !scope.MaxSeenTieBreakerId.HasValue
                ? rowId.Value
                : Math.Max(scope.MaxSeenTieBreakerId.Value, rowId.Value);
        }
    }

    private async IAsyncEnumerable<AccessDataRow> ReadRowsForTableAsync(
        IAccessDataReaderSession session,
        string sourceTableName,
        string tableKey,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var scope = _activeIncrementalScope;
        var appliesToScope = scope is not null
            && scope.TableKey.Equals(tableKey, StringComparison.OrdinalIgnoreCase);
        var canPushDown = false;
        if (appliesToScope && scope!.ApplyFilter && session.SupportsPredicatePushdown)
        {
            var columns = await session.GetColumnsAsync(sourceTableName, ct);
            canPushDown = CanApplyAccessReadPushdown(
                scope.CursorMode,
                scope.CursorTimestampUtc,
                scope.CursorId,
                scope.CursorTieBreakerId,
                scope.TimestampAliases,
                scope.IdAliases,
                columns);

            if (!canPushDown)
            {
                _logger.LogInformation(
                    "Access incremental predicate pushdown is unavailable for this table. Falling back to in-memory filtering. TableKey: {TableKey}. SourceTableName: {SourceTableName}. Mode: {Mode}.",
                    tableKey,
                    sourceTableName,
                    scope.CursorMode);
            }
        }

        var readQuery = canPushDown ? BuildAccessReadQuery(scope!) : null;
        var leaseProbeCounter = 0;

        await foreach (var row in session.ReadRowsAsync(sourceTableName, readQuery, ct))
        {
            leaseProbeCounter++;
            if (appliesToScope && leaseProbeCounter >= 128)
            {
                leaseProbeCounter = 0;
                await TryRenewIncrementalLeaseAsync(scope!, ct);
            }

            if (appliesToScope && canPushDown)
            {
                scope!.RowsScanned++;
                scope.RowsPassedFilter++;
                yield return row;
                continue;
            }

            if (appliesToScope && !ShouldIncludeRowForIncrementalScope(scope!, row))
            {
                continue;
            }

            yield return row;
        }
    }

    private static AccessReadQuery BuildAccessReadQuery(ActiveIncrementalTableScope scope)
        => new()
        {
            CursorMode = scope.CursorMode,
            CursorTimestampUtc = scope.CursorTimestampUtc,
            CursorId = scope.CursorId,
            CursorTieBreakerId = scope.CursorTieBreakerId,
            OverlapSeconds = scope.OverlapSeconds,
            TimestampAliases = scope.TimestampAliases,
            IdAliases = scope.IdAliases
        };

    internal static bool CanApplyAccessReadPushdown(
        string cursorMode,
        DateTime? cursorTimestampUtc,
        long? cursorId,
        long? cursorTieBreakerId,
        IReadOnlyList<string> timestampAliases,
        IReadOnlyList<string> idAliases,
        IReadOnlyList<string> sourceColumns)
    {
        var normalizedMode = NormalizeCursorMode(cursorMode);
        if (string.Equals(normalizedMode, "none", StringComparison.OrdinalIgnoreCase))
            return false;

        var hasTimestampColumn = HasAnyAliasInColumns(sourceColumns, timestampAliases);
        var hasIdColumn = HasAnyAliasInColumns(sourceColumns, idAliases);

        return normalizedMode switch
        {
            "timestamp" => cursorTimestampUtc.HasValue && hasTimestampColumn,
            "timestamp_then_id" => (cursorTimestampUtc.HasValue && hasTimestampColumn) ||
                                   (cursorId.HasValue && hasIdColumn) ||
                                   (cursorTieBreakerId.HasValue && hasIdColumn),
            "id_or_composite" => cursorId.HasValue && hasIdColumn,
            "id" => cursorId.HasValue && hasIdColumn,
            _ => cursorId.HasValue && hasIdColumn
        };
    }

    private static bool HasAnyAliasInColumns(
        IReadOnlyList<string> sourceColumns,
        IReadOnlyList<string> normalizedAliases)
    {
        if (sourceColumns.Count == 0 || normalizedAliases.Count == 0)
            return false;

        var normalizedSourceColumns = new HashSet<string>(sourceColumns.Count, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < sourceColumns.Count; i++)
        {
            var normalized = Normalize(sourceColumns[i]);
            if (!string.IsNullOrWhiteSpace(normalized))
                normalizedSourceColumns.Add(normalized);
        }

        for (var i = 0; i < normalizedAliases.Count; i++)
        {
            var normalizedAlias = Normalize(normalizedAliases[i]);
            if (!string.IsNullOrWhiteSpace(normalizedAlias) && normalizedSourceColumns.Contains(normalizedAlias))
                return true;
        }

        return false;
    }

    private async Task TryRenewIncrementalLeaseAsync(ActiveIncrementalTableScope scope, CancellationToken ct)
    {
        if (_cursorRepository is null || !scope.LeaseAcquired)
            return;

        var renewIntervalSeconds = Math.Clamp(scope.LeaseDurationSeconds / 2, 10, 120);
        var now = DateTime.UtcNow;
        if (now - scope.LastLeaseRenewedUtc < TimeSpan.FromSeconds(renewIntervalSeconds))
            return;

        var renewed = await _cursorRepository.RenewLeaseAsync(
            scope.TableKey,
            _incrementalLeaseOwner,
            TimeSpan.FromSeconds(scope.LeaseDurationSeconds),
            ct);
        if (!renewed)
        {
            throw new InvalidOperationException(
                $"Incremental import lease was lost for table '{scope.TableKey}'. Aborting to preserve single-writer safety.");
        }

        scope.LastLeaseRenewedUtc = now;
    }

    private void TrackAnalyticsProductId(int productId)
    {
        if (productId > 0)
            _analyticsDeltaProductIds.Add(productId);
    }

    private void TrackAnalyticsSaleId(int saleId)
    {
        if (saleId > 0)
            _analyticsDeltaSaleIds.Add(saleId);
    }

    private void TrackAnalyticsMovementId(int movementId)
    {
        if (movementId > 0)
            _analyticsDeltaMovementIds.Add(movementId);
    }

    private void TrackAnalyticsSupplierId(int? supplierId)
    {
        if (supplierId is > 0)
            _analyticsDeltaSupplierIds.Add(supplierId.Value);
    }

    private void TrackAnalyticsSeasonId(int? seasonId)
    {
        if (seasonId is > 0)
            _analyticsDeltaSeasonIds.Add(seasonId.Value);
    }

    private void TrackAnalyticsTypeId(int? typeId)
    {
        if (typeId is > 0)
            _analyticsDeltaTypeIds.Add(typeId.Value);
    }

    private void TrackAnalyticsStoreId(int? storeId)
    {
        if (storeId is > 0)
            _analyticsDeltaStoreIds.Add(storeId.Value);
    }

    private static bool IsStandardReceiptNumber(string? brojRacuna)
    {
        if (string.IsNullOrWhiteSpace(brojRacuna))
            return false;

        foreach (var ch in brojRacuna.Trim())
        {
            if (!char.IsDigit(ch))
                return false;
        }

        return true;
    }

    private static bool IsDebtReceiptNumber(string? brojRacuna)
        => string.Equals(brojRacuna?.Trim(), "DUG", StringComparison.OrdinalIgnoreCase);

    private async Task AppendImportedSalesDiagnosticsAsync(AccessImportRunResponse result, CancellationToken ct)
    {
        var importedSaleIds = _analyticsDeltaSaleIds
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        if (importedSaleIds.Length == 0)
            return;

        var saleHeaders = await _trendDb.ProdajaZaglavlja
            .AsNoTracking()
            .Where(x => importedSaleIds.Contains(x.Id))
            .Select(x => new
            {
                SaleId = x.Id,
                SaleDate = x.DatumProdaje.Date,
                x.BrojRacuna,
                x.IDObjekat
            })
            .ToListAsync(ct);
        if (saleHeaders.Count == 0)
            return;

        var saleTypeCandidates = TipPromeneConstants.ProdajaTypes.ToArray();
        var lineTotals = await _trendDb.ProdajaStavke
            .AsNoTracking()
            .Where(x => importedSaleIds.Contains(x.IdProdaja))
            .GroupBy(x => x.IdProdaja)
            .Select(g => new
            {
                SaleId = g.Key,
                LineTotal = g.Sum(x => x.Kolicina * x.Cena)
            })
            .ToListAsync(ct);
        var lineTotalsBySaleId = lineTotals.ToDictionary(x => x.SaleId, x => x.LineTotal);

        var dnevnikTotals = await _trendDb.DnevnikPromena
            .AsNoTracking()
            .Where(x => importedSaleIds.Contains(x.Id) && saleTypeCandidates.Contains(x.TipPromene))
            .GroupBy(x => x.Id)
            .Select(g => new
            {
                SaleId = g.Key,
                DnevnikTotal = g.Sum(x => x.Iznos < 0 ? -x.Iznos : x.Iznos)
            })
            .ToListAsync(ct);
        var dnevnikTotalsBySaleId = dnevnikTotals.ToDictionary(x => x.SaleId, x => x.DnevnikTotal);

        var duplicateReceiptGroups = saleHeaders
            .Where(x => !string.IsNullOrWhiteSpace(x.BrojRacuna))
            .GroupBy(x => new
            {
                x.SaleDate,
                x.BrojRacuna,
                x.IDObjekat
            })
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                g.Key.SaleDate,
                g.Key.BrojRacuna,
                g.Key.IDObjekat,
                HeaderCount = g.Count()
            })
            .OrderByDescending(x => x.HeaderCount)
            .ThenBy(x => x.SaleDate)
            .ToList();

        if (duplicateReceiptGroups.Count > 0)
        {
            var sample = string.Join(
                ", ",
                duplicateReceiptGroups
                    .Take(3)
                    .Select(x => $"{x.BrojRacuna}/{x.IDObjekat ?? 0} ({x.HeaderCount}x)"));
            var suffix = duplicateReceiptGroups.Count > 3 ? " ..." : string.Empty;
            result.Warnings.Add(
                $"Import rekonsilijacija: detektovano je {duplicateReceiptGroups.Count} grupa dupliranih racuna u upravo uvezenoj prodaji. Primeri: {sample}{suffix}.");
        }

        var receiptDiagnostics = saleHeaders
            .Select(x => new
            {
                x.SaleId,
                x.SaleDate,
                x.BrojRacuna,
                x.IDObjekat,
                Revenue = lineTotalsBySaleId.TryGetValue(x.SaleId, out var lineTotal)
                    ? lineTotal
                    : dnevnikTotalsBySaleId.TryGetValue(x.SaleId, out var dnevnikTotal)
                        ? dnevnikTotal
                        : 0m
            })
            .ToList();

        var receiptAmountMismatches = receiptDiagnostics
            .Where(x => lineTotalsBySaleId.TryGetValue(x.SaleId, out var lineTotal)
                        && dnevnikTotalsBySaleId.TryGetValue(x.SaleId, out var dnevnikTotal)
                        && decimal.Abs(lineTotal - dnevnikTotal) > 0.01m)
            .Select(x => new
            {
                x.BrojRacuna,
                LineTotal = lineTotalsBySaleId[x.SaleId],
                DnevnikTotal = dnevnikTotalsBySaleId[x.SaleId],
                Difference = decimal.Abs(lineTotalsBySaleId[x.SaleId] - dnevnikTotalsBySaleId[x.SaleId])
            })
            .OrderByDescending(x => x.Difference)
            .ToList();

        if (receiptAmountMismatches.Count > 0)
        {
            var sample = string.Join(
                ", ",
                receiptAmountMismatches
                    .Take(3)
                    .Select(x => $"{x.BrojRacuna ?? "(bez broja)"} ({x.LineTotal:0.##} vs {x.DnevnikTotal:0.##})"));
            var suffix = receiptAmountMismatches.Count > 3 ? " ..." : string.Empty;
            result.Warnings.Add(
                $"Import rekonsilijacija: {receiptAmountMismatches.Count} racuna ima mismatch izmedju dnevnika i stavki. Primeri: {sample}{suffix}.");
        }

        var nonStandardReceipts = receiptDiagnostics
            .Where(x => !IsStandardReceiptNumber(x.BrojRacuna))
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.SaleDate)
            .ToList();
        if (nonStandardReceipts.Count > 0)
        {
            var sample = string.Join(
                ", ",
                nonStandardReceipts
                    .Take(3)
                    .Select(x => $"{(string.IsNullOrWhiteSpace(x.BrojRacuna) ? "(prazno)" : x.BrojRacuna)}/{x.IDObjekat ?? 0}"));
            var suffix = nonStandardReceipts.Count > 3 ? " ..." : string.Empty;
            result.Warnings.Add(
                $"Import quality: {nonStandardReceipts.Count} prodajnih dokumenata ima nestandardni broj racuna. Njihov promet je {decimal.Round(nonStandardReceipts.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero):0.##} RSD. Primeri: {sample}{suffix}.");
        }

        var debtReceipts = nonStandardReceipts
            .Where(x => IsDebtReceiptNumber(x.BrojRacuna))
            .ToList();
        if (debtReceipts.Count > 0)
        {
            result.Warnings.Add(
                $"Import quality: dokument DUG pojavljuje se {debtReceipts.Count} put(a) sa ukupno {decimal.Round(debtReceipts.Sum(x => x.Revenue), 2, MidpointRounding.AwayFromZero):0.##} RSD.");
        }
    }

    private TimeSpan GetBatchHeartbeatPersistInterval()
        => TimeSpan.FromSeconds(Math.Max(1, Math.Max(_options.HeartbeatIntervalSeconds, _options.StatusUpdateThrottleSeconds)));

    private TimeSpan GetTrendFlushInterval()
        => TimeSpan.FromSeconds(Math.Clamp(_options.StatusUpdateThrottleSeconds, 2, 5));

    private int GetActiveTrendWriteBatchSize()
        => _activeIncrementalScope?.WriteBatchSize is > 0
            ? _activeIncrementalScope.WriteBatchSize
            : Math.Max(1, _options.DbSaveBatchSize);

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
            batch.RowsSkippedStale = _activeBatchResult.RowsSkippedStale;
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
            Interlocked.Increment(ref _batchHeartbeatPersistCount);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
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

    private async Task RunBatchHeartbeatLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(GetBatchHeartbeatPersistInterval());
        while (true)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(ct))
                    break;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await PersistBatchProgressAsync("heartbeat", force: false, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (IsTransientDatabaseTimeout(ex))
            {
                _logger.LogDebug(
                    ex,
                    "Access import heartbeat loop skipped one persistence cycle due to transient database issue.");
            }
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

    internal static bool SourceColumnsContainProdajaLineNabavnaCena(IReadOnlyList<string> sourceColumns)
        => HasAnyAliasInColumns(sourceColumns, ProdajaLineNabavnaCenaAliases);

    public async Task<AccessImportRunResponse> ImportAsync(
        string accessFilePath,
        bool includeAnalytics,
        bool overwriteExisting,
        bool includeTemporaryTables = false,
        CancellationToken ct = default)
    {
        var (batch, _) = await CreateImportBatchAsync(
            sourceFilePath: accessFilePath,
            sourceStorageKey: null,
            sourceStorageProvider: null,
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
            throw new FileNotFoundException("ACCDB fajl nije pronađen.", accessFilePath);

        if (_jobQueue is null)
            throw new InvalidOperationException("Access import background job queue is not configured.");

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

        var preparedSource = await PrepareQueuedSourceAsync(accessFilePath, Path.GetFileName(accessFilePath), ct);
        AccessImportRunResponse result;
        try
        {
            (_, result) = await CreateImportBatchAsync(
                sourceFilePath: preparedSource.SourceFilePath,
                sourceStorageKey: preparedSource.SourceStorageKey,
                sourceStorageProvider: preparedSource.SourceStorageProvider,
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

            if (preparedSource.UploadedToStorage)
                await DeleteQueuedStorageSourceBestEffortAsync(preparedSource.SourceStorageKey);

            throw;
        }
        catch
        {
            if (preparedSource.UploadedToStorage)
                await DeleteQueuedStorageSourceBestEffortAsync(preparedSource.SourceStorageKey);

            if (!string.IsNullOrWhiteSpace(preparedSource.SourceFilePath))
                TryDeleteFile(preparedSource.SourceFilePath, "batch-create-failed-cleanup", Path.GetFileName(accessFilePath), 0, "working-copy");
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
        string? sourceFilePath,
        string? sourceStorageKey,
        string? sourceStorageProvider,
        string sourceFileName,
        bool includeAnalytics,
        bool overwriteExisting,
        bool includeTemporaryTables,
        CancellationToken ct)
    {
        EnsurePlatformSupport();
        var hasLocalSource = !string.IsNullOrWhiteSpace(sourceFilePath);
        var hasStorageSource = !string.IsNullOrWhiteSpace(sourceStorageKey);
        if (!hasLocalSource && !hasStorageSource)
            throw new InvalidOperationException("Access import source is missing. Provide local source path or storage key.");

        if (hasLocalSource && !File.Exists(sourceFilePath!))
            throw new FileNotFoundException("ACCDB fajl nije pronađen.", sourceFilePath);

        var now = DateTime.UtcNow;
        await EnsureDataImportBatchesTableIfEnabledAsync(ct);

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
            SourceFileName = string.IsNullOrWhiteSpace(sourceFileName)
                ? (hasLocalSource ? Path.GetFileName(sourceFilePath!) : "access-import-source")
                : sourceFileName,
            SourceFilePath = sourceFilePath,
            SourceStorageKey = sourceStorageKey,
            SourceStorageProvider = sourceStorageProvider,
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
            ImportStrategy = IsIncrementalWriteMode()
                ? "incremental"
                : IsIncrementalShadowMode()
                    ? "shadow"
                    : "full",
            ProgressPercent = 0,
            RowsRead = 0,
            RowsAccepted = 0,
            RowsWritten = 0,
            RetryCount = 0,
            CancellationRequested = false,
            IsIncremental = IsIncrementalFeatureEnabled(),
            CursorSnapshot = IsIncrementalFeatureEnabled()
                ? JsonSerializer.Serialize(new
                {
                    mode = _options.Incremental.Mode,
                    createdAtUtc = now
                })
                : null,
            CursorBeforeJson = IsIncrementalFeatureEnabled()
                ? JsonSerializer.Serialize(new
                {
                    mode = _options.Incremental.Mode,
                    createdAtUtc = now
                })
                : null
        };

        _trendDb.DataImportBatches.Add(batch);
        try
        {
            await _trendDb.SaveChangesAsync(ct);
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UndefinedTable ||
            ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            throw new InvalidOperationException(
                "Access import batch schema is not ready. Apply Trendplus migrations before starting imports.",
                ex);
        }

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
            throw new FileNotFoundException("ACCDB fajl nije pronađen.", accessFilePath);

        var batch = await _trendDb.DataImportBatches.FirstOrDefaultAsync(x => x.Id == batchId, ct)
            ?? throw new InvalidOperationException($"Batch {batchId} nije pronađen.");

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
        if (string.IsNullOrWhiteSpace(batch.SourceFilePath) && string.IsNullOrWhiteSpace(batch.SourceStorageKey))
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
        ResetAnalyticsDeltaTracking();
        _importedStores = [];
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
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeatTask = RunBatchHeartbeatLoopAsync(heartbeatCts.Token);

        try
        {
            await RetriableDbContextTransaction.ExecuteAsync(_trendDb, async transactionCt =>
            {
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
                    await PersistBatchProgressAsync("batch-start", force: true, transactionCt);
                    await ImportTrendplusAsync(session, overwriteExisting, includeTemporaryTables, result, transactionCt);
                    await FlushTrendWritesAsync(force: true, transactionCt);
                    await ResetTrendplusSequencesAsync(transactionCt);

                    if (includeAnalytics)
                        await SyncAnalyticsAsync(result, transactionCt);

                    if (_analyticsCache is not null || _serviceScopeFactory is not null)
                    {
                        try
                        {
                            await InvalidateAnalyticsCacheAsync(transactionCt);
                        }
                        catch (Exception cacheEx)
                        {
                            _logger.LogWarning(cacheEx, "Analytics cache invalidation failed after Access import. BatchId: {BatchId}.", batch.Id);
                        }
                    }

                    result.Status = "completed";
                    result.CompletedAtUtc = DateTime.UtcNow;
                    if (_trendDb.Entry(batch).State == EntityState.Detached)
                    {
                        var trackedBatch = await _trendDb.DataImportBatches.FirstOrDefaultAsync(x => x.Id == batch.Id, transactionCt);
                        if (trackedBatch is not null)
                            batch = trackedBatch;
                    }
                    batch.Status = "completed";
                    batch.CompletedAtUtc = result.CompletedAtUtc;
                    batch.LastHeartbeatUtc = result.CompletedAtUtc;
                    batch.CurrentStep = null;
                    batch.CurrentTable = null;
                    batch.ProgressPercent = 100;
                    UpdateBatchDurationSeconds(batch, result.CompletedAtUtc ?? DateTime.UtcNow);
                    ApplyBatchMetricsFromResult(batch, result);
                    var completedCursorSnapshot = BuildBatchCursorSnapshotJson();
                    batch.CursorSnapshot = completedCursorSnapshot;
                    batch.CursorAfterJson = completedCursorSnapshot;
                    batch.SummaryJson = JsonSerializer.Serialize(result);
                    await _trendDb.SaveChangesAsync(transactionCt);
                    _logger.LogInformation(
                        "Access import completed. BatchId: {BatchId}. SourceFileName: {SourceFileName}. Status: {Status}. IncludeAnalytics: {IncludeAnalytics}. TrendFlushes: {TrendFlushes}. HeartbeatPersists: {HeartbeatPersists}.",
                        batch.Id,
                        batch.SourceFileName,
                        result.Status,
                        includeAnalytics,
                        _trendFlushCount,
                        _batchHeartbeatPersistCount);
                }
                finally
                {
                    _trendDb.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetectChanges;
                }
            }, _logger, "AccessImportBatchExecution", ct);

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
            UpdateBatchDurationSeconds(batch, result.CompletedAtUtc ?? DateTime.UtcNow);
            ApplyBatchMetricsFromResult(batch, result);
            batch.ErrorMessage = "Cancellation requested by user.";
            batch.ErrorDetailsJson = JsonSerializer.Serialize(new
            {
                type = ex.GetType().FullName,
                message = ex.Message
            });
            var cancelledCursorSnapshot = BuildBatchCursorSnapshotJson();
            batch.CursorSnapshot = cancelledCursorSnapshot;
            batch.CursorAfterJson = cancelledCursorSnapshot;
            batch.SummaryJson = JsonSerializer.Serialize(result);
            foreach (var entry in _trendDb.ChangeTracker.Entries().Where(e => !ReferenceEquals(e.Entity, batch)).ToList())
                entry.State = EntityState.Detached;
            if (_trendDb.Entry(batch).State == EntityState.Detached)
                _trendDb.DataImportBatches.Attach(batch);
            _trendDb.Entry(batch).State = EntityState.Modified;
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
            UpdateBatchDurationSeconds(batch, result.CompletedAtUtc ?? DateTime.UtcNow);
            ApplyBatchMetricsFromResult(batch, result, minTotalErrors: 1);
            batch.RetryCount = Math.Max(0, batch.RetryCount) + 1;
            batch.ErrorMessage = ex.GetBaseException().Message;
            batch.ErrorDetailsJson = JsonSerializer.Serialize(new
            {
                type = ex.GetType().FullName,
                baseType = ex.GetBaseException().GetType().FullName,
                message = ex.GetBaseException().Message
            });
            var failedCursorSnapshot = BuildBatchCursorSnapshotJson();
            batch.CursorSnapshot = failedCursorSnapshot;
            batch.CursorAfterJson = failedCursorSnapshot;
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
            heartbeatCts.Cancel();
            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown/cancellation.
            }
            catch (Exception heartbeatEx)
            {
                _logger.LogDebug(heartbeatEx, "Access import heartbeat loop stopped with an exception.");
            }

            try
            {
                await EnsureBatchTerminalStateAsync(batch.Id, result, CancellationToken.None);
            }
            catch (Exception terminalStateEx)
            {
                _logger.LogWarning(
                    terminalStateEx,
                    "Failed terminal-state safety persistence for Access import batch. BatchId: {BatchId}.",
                    batch.Id);
            }

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

    public async Task<List<AccessImportBatchDto>> GetRecentBatchStatusesAsync(int take = 20, CancellationToken ct = default)
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
                    SummaryJson = null,
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
            _logger.LogWarning(
                ex,
                "Access import lightweight batches query hit legacy schema (missing columns). BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Falling back to compatibility projection.",
                0L,
                "DataImportBatches",
                "list-batches-lightweight");

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
                    SummaryJson = null,
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
            _logger.LogWarning(
                ex,
                "Access import lightweight batches table query is missing. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Returning empty list as compatibility fallback.",
                0L,
                "DataImportBatches",
                "list-batches-lightweight");
            return [];
        }
        catch (Exception ex) when (IsTransientDatabaseTimeout(ex))
        {
            _logger.LogWarning(
                ex,
                "Access import lightweight batches query hit a transient timeout/connectivity issue. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}. Returning empty list.",
                0L,
                "DataImportBatches",
                "list-batches-lightweight");
            return [];
        }
    }

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
                    SummaryJson = null,
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
                    SummaryJson = null,
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

        await EnsureDataImportBatchesTableIfEnabledAsync(ct);

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

        int affected;
        try
        {
            var sw = Stopwatch.StartNew();
            affected = await _trendDb.Database.ExecuteSqlRawAsync(
                sql,
                new object[] { batchId, now, "Cancellation requested by user." },
                ct);
            sw.Stop();
            try { Infrastructure.Logging.SqlCommandLoggingHelper.LogSqlExecution("access-import", "ExecuteSqlRaw", sql, null, sw.ElapsedMilliseconds, true, affected, null, Application.Logging.RequestLogContext.Current.RequestId, Application.Logging.RequestLogContext.Current.TraceId); } catch { }
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UndefinedTable ||
            ex.SqlState == PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogWarning(
                ex,
                "Access import cancellation request skipped because DataImportBatches schema is missing/incompatible. BatchId: {BatchId}.",
                batchId);
            return false;
        }

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

    private async Task EnsureBatchTerminalStateAsync(long batchId, AccessImportRunResponse result, CancellationToken ct)
    {
        if (batchId <= 0)
            return;

        var terminalStatus = Normalize(result.Status) switch
        {
            "completed" => "completed",
            "cancelled" => "cancelled",
            "interrupted" => "interrupted",
            _ => "failed"
        };

        await using var scope = _serviceScopeFactory?.CreateAsyncScope();
        var db = scope?.ServiceProvider.GetService<TrendplusDbContext>() ?? _trendDb;
        var batch = await db.DataImportBatches.FirstOrDefaultAsync(x => x.Id == batchId, ct);
        if (batch is null)
            return;

        if (!string.Equals(batch.Status, "running", StringComparison.OrdinalIgnoreCase) || batch.CompletedAtUtc is not null)
            return;

        var now = result.CompletedAtUtc ?? DateTime.UtcNow;
        batch.Status = terminalStatus;
        batch.CompletedAtUtc = now;
        batch.LastHeartbeatUtc = now;
        batch.CurrentStep = terminalStatus;
        batch.CurrentTable = null;
        batch.ProgressPercent = terminalStatus == "completed"
            ? 100
            : Math.Clamp(batch.ProgressPercent, 0, 99);
        UpdateBatchDurationSeconds(batch, now);
        ApplyBatchMetricsFromResult(batch, result, minTotalErrors: terminalStatus == "completed" ? 0 : 1);
        if (!string.Equals(terminalStatus, "completed", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(batch.ErrorMessage))
        {
            batch.ErrorMessage = "Import did not finish cleanly and was finalized by terminal-state safety.";
        }
        if (string.IsNullOrWhiteSpace(batch.SummaryJson))
            batch.SummaryJson = JsonSerializer.Serialize(result);
        if (string.IsNullOrWhiteSpace(batch.CursorSnapshot))
            batch.CursorSnapshot = BuildBatchCursorSnapshotJson();

        await db.SaveChangesAsync(ct);
        _logger.LogWarning(
            "Access import batch terminal-state safety updated a stale running batch. BatchId: {BatchId}. FinalStatus: {FinalStatus}.",
            batchId,
            terminalStatus);
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

    internal static bool IsPendingBatchStale(DateTime queuedAtUtc, DateTime? lastHeartbeatUtc, DateTime utcNow, int staleAfterMinutes)
    {
        var safeWindowMinutes = Math.Max(1, staleAfterMinutes);
        var referenceTime = lastHeartbeatUtc ?? queuedAtUtc;
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
                .Where(x => x.Status == "running" && x.CompletedAtUtc == null);

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
                      AND "Status" = 'running'
                      AND "CompletedAtUtc" IS NULL;
                    """;

                var sw = Stopwatch.StartNew();
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
                sw.Stop();
                try { Infrastructure.Logging.SqlCommandLoggingHelper.LogSqlExecution("access-import", "ExecuteSqlRaw", sql, null, sw.ElapsedMilliseconds, true, null, null, Application.Logging.RequestLogContext.Current.RequestId, Application.Logging.RequestLogContext.Current.TraceId); } catch { }

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
        var summaryDeleted = 0;

        var trendArchiveEnabled = true;
        var analyticsArchiveEnabled = true;

        async Task ArchiveTrendAsync(string tableName, string sql, params object[] parameters)
        {
            if (!trendArchiveEnabled)
                return;

            trendArchiveEnabled = await TryArchiveInsertCompatAsync(
                () => _trendDb.Database.ExecuteSqlRawAsync(sql, parameters),
                tableName,
                "delete-batch-archive",
                batchId);
        }

        async Task ArchiveAnalyticsAsync(string tableName, string sql, params object[] parameters)
        {
            if (!analyticsArchiveEnabled)
                return;

            analyticsArchiveEnabled = await TryArchiveInsertCompatAsync(
                () => _analyticsDb.Database.ExecuteSqlRawAsync(sql, parameters),
                tableName,
                "delete-batch-analytics-archive",
                batchId);
        }

        // Delete transactional / master data
        // Stavke must be deleted before zaglavlja (FK constraint), filtered via parent
        // Archive prv: povracaj_stavke rows that belong to access-origin parents
        await ArchiveTrendAsync("povracaj_stavke", @"
            INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
            SELECT @p0, 'povracaj_stavke', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
            FROM povracaj_stavke t
            WHERE t.id_povracaj IN (SELECT id FROM povracaj_zaglavlje WHERE data_origin = 'access')
        ", batchId);
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
        await ArchiveTrendAsync("povracaj_zaglavlje", @"
            INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
            SELECT @p0, 'povracaj_zaglavlje', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
            FROM povracaj_zaglavlje t
            WHERE t.data_origin = 'access'
        ", batchId);
        var pvDeleted2 = await ExecuteDeleteCompatAsync(
            () => _trendDb.PovracajZaglavlja.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "povracaj_zaglavlje",
            "delete-batch",
            batchId);
        await ArchiveTrendAsync("DnevnikPromena", @"
            INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
            SELECT @p0, 'DnevnikPromena', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
            FROM ""DnevnikPromena"" t
            WHERE t.""DataOrigin"" = 'access'
        ", batchId);
        var dnDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.DnevnikPromena.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "DnevnikPromena",
            "delete-batch",
            batchId);
        await ArchiveTrendAsync("prodaja_stavke", @"
            INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
            SELECT @p0, 'prodaja_stavke', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
            FROM prodaja_stavke t
            WHERE t.id_prodaja IN (SELECT id FROM prodaja_zaglavlje WHERE data_origin = 'access')
        ", batchId);
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
        await ArchiveTrendAsync("prodaja_zaglavlje", @"
            INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
            SELECT @p0, 'prodaja_zaglavlje', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
            FROM prodaja_zaglavlje t
            WHERE t.data_origin = 'access'
        ", batchId);
        var pvDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.ProdajaZaglavlja.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "prodaja_zaglavlje",
            "delete-batch",
            batchId);
        await ArchiveTrendAsync("Artikli", @"
            INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
            SELECT @p0, 'Artikli', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
            FROM ""Artikli"" t
            WHERE t.""DataOrigin"" = 'access'
        ", batchId);
        var arDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.Artikli.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "Artikli",
            "delete-batch",
            batchId);
        await ArchiveTrendAsync("Sezone", @"
            INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
            SELECT @p0, 'Sezone', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
            FROM ""Sezone"" t
            WHERE t.""DataOrigin"" = 'access'
        ", batchId);
        var seDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.Sezone.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "Sezone",
            "delete-batch",
            batchId);
        await ArchiveTrendAsync("Dobavljaci", @"
            INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
            SELECT @p0, 'Dobavljaci', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
            FROM ""Dobavljaci"" t
            WHERE t.""DataOrigin"" = 'access'
        ", batchId);
        var doDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.Dobavljaci.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "Dobavljaci",
            "delete-batch",
            batchId);
        await ArchiveTrendAsync("TipoviObuce", @"
            INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
            SELECT @p0, 'TipoviObuce', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
            FROM ""TipoviObuce"" t
            WHERE t.""DataOrigin"" = 'access'
        ", batchId);
        var tiDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.TipoviObuce.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "TipoviObuce",
            "delete-batch",
            batchId);

        if (includeAnalytics)
        {
            var accessStoreIds = await LoadAccessStoreIdsCompatAsync(batchId, ct);

            // Collect distinct sale dates BEFORE deleting SalesFacts (needed for summary table cleanup)
            var accessSaleDates = await _analyticsDb.SalesFacts
                .Where(x => x.DataOrigin == "access")
                .Select(x => x.SaleTimestampUtc.Date)
                .Distinct()
                .ToArrayAsync(ct);

            // Delete analytics data imported from Access (DataOrigin="access")
            // Note: per-batch FK does not exist in analytics tables, so this removes all Access-origin rows.
            await ArchiveAnalyticsAsync("SalesFacts", @"
                INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
                SELECT @p0, 'SalesFacts', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
                FROM ""SalesFacts"" t
                WHERE t.""DataOrigin"" = 'access'
            ", batchId);
            sfDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.SalesFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "SalesFacts",
                "delete-batch-analytics",
                batchId);
            await ArchiveAnalyticsAsync("SalesLineFacts", @"
                INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
                SELECT @p0, 'SalesLineFacts', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
                FROM ""SalesLineFacts"" t
                WHERE t.""DataOrigin"" = 'access'
            ", batchId);
            slfDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.SalesLineFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "SalesLineFacts",
                "delete-batch-analytics",
                batchId);
            await ArchiveAnalyticsAsync("ProductsDim", @"
                INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
                SELECT @p0, 'ProductsDim', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
                FROM ""ProductsDim"" t
                WHERE t.""DataOrigin"" = 'access'
            ", batchId);
            pdDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.ProductsDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "ProductsDim",
                "delete-batch-analytics",
                batchId);
            await ArchiveAnalyticsAsync("InventoryMovementFacts", @"
                INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
                SELECT @p0, 'InventoryMovementFacts', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
                FROM ""InventoryMovementFacts"" t
                WHERE t.""DataOrigin"" = 'access'
            ", batchId);
            imDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.InventoryMovementFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "InventoryMovementFacts",
                "delete-batch-analytics",
                batchId);
            await ArchiveAnalyticsAsync("SuppliersDim", @"
                INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
                SELECT @p0, 'SuppliersDim', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
                FROM ""SuppliersDim"" t
                WHERE t.""DataOrigin"" = 'access'
            ", batchId);
            suppDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.SuppliersDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "SuppliersDim",
                "delete-batch-analytics",
                batchId);
            await ArchiveAnalyticsAsync("SeasonsDim", @"
                INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
                SELECT @p0, 'SeasonsDim', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
                FROM ""SeasonsDim"" t
                WHERE t.""DataOrigin"" = 'access'
            ", batchId);
            seasDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.SeasonsDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "SeasonsDim",
                "delete-batch-analytics",
                batchId);
            await ArchiveAnalyticsAsync("FootwearTypesDim", @"
                INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
                SELECT @p0, 'FootwearTypesDim', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
                FROM ""FootwearTypesDim"" t
                WHERE t.""DataOrigin"" = 'access'
            ", batchId);
            typeDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.FootwearTypesDim.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "FootwearTypesDim",
                "delete-batch-analytics",
                batchId);
            await ArchiveAnalyticsAsync("StoresDim", @"
                INSERT INTO deleted_rows_archive(batch_id, table_name, primary_key, row_json, deleted_at, deleted_by, reason)
                SELECT @p0, 'StoresDim', jsonb_build_object('id', t.id), to_jsonb(t), NOW(), current_user, 'delete-batch'
                                FROM ""StoresDim"" t
                                WHERE (t.""DataOrigin"" = 'access' OR t.""StoreId"" = ANY(@p1))
                                    AND NOT EXISTS (SELECT 1 FROM ""SalesFacts"" sf WHERE sf.""StoreId"" = t.""StoreId"")
            ", batchId, accessStoreIds.ToArray());
            storeDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.StoresDim
                    .Where(x => (x.DataOrigin == "access" || accessStoreIds.Contains(x.StoreId))
                                && !_analyticsDb.SalesFacts.Any(sf => sf.StoreId == x.StoreId))
                    .ExecuteDeleteAsync(ct),
                "StoresDim",
                "delete-batch-analytics",
                batchId);

            // Delete pre-aggregated summary rows for dates affected by this batch.
            // These tables have no DataOrigin column — cleaned up by date.
            if (accessSaleDates.Length > 0)
            {
                var summaryDateArray = accessSaleDates.Select(DateOnly.FromDateTime).ToArray();
                foreach (var tbl in new[] { "AnalyticsDailySummary", "AnalyticsCategorySummary",
                                            "AnalyticsSupplierSummary", "AnalyticsGenderSummary",
                                            "AnalyticsTopProducts" })
                {
                    var capturedTbl = tbl;
                    summaryDeleted += await ExecuteDeleteCompatAsync(
                        () => _analyticsDb.Database.ExecuteSqlRawAsync(
                            $"DELETE FROM \"{capturedTbl}\" WHERE \"Date\" = ANY(@p0)",
                            new NpgsqlParameter { ParameterName = "p0", Value = summaryDateArray,
                                                  NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Date }),
                        capturedTbl,
                        "delete-batch-analytics-summary",
                        batchId);
                }
            }
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
        if (includeAnalytics && (_analyticsCache is not null || _serviceScopeFactory is not null))
        {
            cacheInvalidated = await InvalidateAnalyticsCacheAsync(ct);
        }

        _logger.LogInformation(
            "Deleted access-import batch {BatchId}: artikli={Ar}, prodaja={Pv}/{Sv}, dnevnik={Dn}, povracaj={Pv2}/{PvS}, sezone={Se}, dobavljaci={Do}, tipovi={Ti}, analytics={IncludeAnalytics} pd={Pd}/sf={Sf}/slf={Slf}/im={Im}/sup={Sup}/seas={Seas}/types={Types}/stores={Stores}/summary={Summary}, cacheInvalidated={CacheInvalidated}. TableName: {TableName}. Operation: {Operation}.",
            batchId, arDeleted, pvDeleted, svDeleted, dnDeleted, pvDeleted2, pvStavkeDeleted, seDeleted, doDeleted, tiDeleted, includeAnalytics, pdDeleted, sfDeleted, slfDeleted, imDeleted, suppDeleted, seasDeleted, typeDeleted, storeDeleted, summaryDeleted, cacheInvalidated, "all", "delete-batch");

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
            SummaryRowsDeleted = summaryDeleted,
            CacheInvalidated = cacheInvalidated
        };
    }

    private async Task<bool> InvalidateAnalyticsCacheAsync(CancellationToken ct)
    {
        if (_serviceScopeFactory is not null)
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var cacheAdmin = scope.ServiceProvider.GetService<AnalyticsCacheAdminService>();
                if (cacheAdmin is not null)
                {
                    await cacheAdmin.ClearAsync("all", ct);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Analytics cache admin invalidation failed. Falling back to raw prefix invalidation.");
            }
        }

        if (_analyticsCache is null)
        {
            return false;
        }

        await _analyticsCache.RemoveByPrefixAsync(AnalyticsCacheKeys.Prefix, ct);
        return true;
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

    private async Task<bool> TryArchiveInsertCompatAsync(Func<Task> archiveAction, string tableName, string operation, long batchId)
    {
        try
        {
            await archiveAction();
            return true;
        }
        catch (PostgresException ex) when (IsLegacySchemaArtifact(ex))
        {
            _logger.LogWarning(
                ex,
                "Skipping archive write for legacy schema artifact. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}.",
                batchId,
                tableName,
                operation);
            return false;
        }
        catch (PostgresException ex) when (IsArchiveStorageLimitExceeded(ex))
        {
            _logger.LogWarning(
                ex,
                "Skipping archive write because PostgreSQL storage quota is full. BatchId: {BatchId}. TableName: {TableName}. Operation: {Operation}.",
                batchId,
                tableName,
                operation);
            return false;
        }
    }

    private static bool IsArchiveStorageLimitExceeded(PostgresException ex)
        => string.Equals(ex.SqlState, "53100", StringComparison.Ordinal);

    private static bool IsLegacySchemaArtifact(PostgresException ex)
        => ex.SqlState is PostgresErrorCodes.UndefinedColumn or PostgresErrorCodes.UndefinedTable;

    private bool ShouldResetAccessSalesSnapshot(bool overwriteExisting)
        => overwriteExisting && !IsIncrementalWriteMode();

    private async Task ResetAccessSalesSnapshotAsync(AccessImportRunResponse result, CancellationToken ct)
    {
        var batchId = result.BatchId;
        var analyticsSalesLineFactsDeleted = 0;
        var analyticsSalesFactsDeleted = 0;

        if (result.IncludeAnalytics)
        {
            analyticsSalesLineFactsDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.SalesLineFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "SalesLineFacts",
                "pre-import-reset",
                batchId);
            analyticsSalesFactsDeleted = await ExecuteDeleteCompatAsync(
                () => _analyticsDb.SalesFacts.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
                "SalesFacts",
                "pre-import-reset",
                batchId);
        }

        var trendSalesLinesDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.ProdajaStavke
                .Where(s => _trendDb.ProdajaZaglavlja
                    .Where(z => z.DataOrigin == "access")
                    .Select(z => z.Id)
                    .Contains(s.IdProdaja))
                .ExecuteDeleteAsync(ct),
            "prodaja_stavke",
            "pre-import-reset",
            batchId);
        var trendSalesHeadersDeleted = await ExecuteDeleteCompatAsync(
            () => _trendDb.ProdajaZaglavlja.Where(x => x.DataOrigin == "access").ExecuteDeleteAsync(ct),
            "prodaja_zaglavlje",
            "pre-import-reset",
            batchId);

        var totalDeleted =
            analyticsSalesLineFactsDeleted +
            analyticsSalesFactsDeleted +
            trendSalesLinesDeleted +
            trendSalesHeadersDeleted;

        if (totalDeleted <= 0)
            return;

        result.Warnings.Add(
            $"Pre punog importa obrisan je prethodni Access snapshot prodaje: zaglavlja={trendSalesHeadersDeleted}, stavke={trendSalesLinesDeleted}, analytics facts={analyticsSalesFactsDeleted}, analytics lines={analyticsSalesLineFactsDeleted}.");
        _logger.LogInformation(
            "Access sales snapshot reset completed before full import. BatchId: {BatchId}. SalesHeadersDeleted: {SalesHeadersDeleted}. SalesLinesDeleted: {SalesLinesDeleted}. SalesFactsDeleted: {SalesFactsDeleted}. SalesLineFactsDeleted: {SalesLineFactsDeleted}.",
            batchId,
            trendSalesHeadersDeleted,
            trendSalesLinesDeleted,
            analyticsSalesFactsDeleted,
            analyticsSalesLineFactsDeleted);
    }

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
            throw new InvalidOperationException("Nije pronađena tabela za artikle u ACCDB fajlu.");

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

        var dnevnikImportedBefore = result.DnevnikInserted + result.DnevnikUpdated;
        if (dnevnik is not null)
            await RunImportStepAsync("import", "dnevnik_promena", dnevnik, result, async innerCt =>
            {
                await ImportDnevnikPromenaAsync(session, dnevnik, overwriteExisting, result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);
        var dnevnikImportedDelta = (result.DnevnikInserted + result.DnevnikUpdated) - dnevnikImportedBefore;
        var skipLinkedByDnevnikTrigger = ShouldSkipLinkedTablesByDnevnikTrigger(
            IsIncrementalWriteMode(),
            dnevnik is not null,
            dnevnikImportedDelta);

        if (skipLinkedByDnevnikTrigger)
        {
            _logger.LogInformation(
                "Access import skipped linked sales/returns tables because dnevnik_promena produced no incremental delta. DnevnikImportedDelta: {DnevnikImportedDelta}. BatchId: {BatchId}.",
                dnevnikImportedDelta,
                result.BatchId);
            result.Warnings.Add(
                "Preskocen je import tabela prodaja/povracaj jer u dnevnik_promena nema novih izmena za incremental batch.");
        }

        if (!skipLinkedByDnevnikTrigger &&
            ShouldResetAccessSalesSnapshot(overwriteExisting) &&
            (prodaja is not null || prodajaStavke is not null || dnevnik is not null))
        {
            await RunImportStepAsync("reset-snapshot", "prodaja", prodaja ?? prodajaStavke ?? dnevnik ?? "prodaja", result, async innerCt =>
            {
                await ResetAccessSalesSnapshotAsync(result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);
        }

        var importedProdajaFromLineTable = false;
        var synthesizedProdajaFromDnevnik = false;
        var saleLineCostColumnsKnown = false;
        var saleLineCostColumnsPresent = false;
        string? saleLineTableForCostDiscovery = null;
        if (!skipLinkedByDnevnikTrigger && prodaja is not null && await IsProdajaLineTableAsync(session, prodaja, ct))
        {
            importedProdajaFromLineTable = true;
            saleLineTableForCostDiscovery = prodaja;
            result.Warnings.Add($"Tabela '{prodaja}' prepoznata je kao tabela stavki prodaje (IDDnevnik/IDArtikal). Uvozim prodaju kroz vezu sa DnevnikPromena.");
            await RunImportStepAsync("import-line-table", "prodaja_zaglavlje", prodaja, result, async innerCt =>
            {
                await ImportProdajaFromLineTableAsync(session, prodaja, overwriteExisting, result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);
        }
        else if (!skipLinkedByDnevnikTrigger)
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
            {
                saleLineTableForCostDiscovery = prodajaStavke;
                await RunImportStepAsync("import", "prodaja_stavke", prodajaStavke, result, async innerCt =>
                {
                    await ImportProdajaStavkeAsync(session, prodajaStavke, prodaja, overwriteExisting, result, innerCt);
                    await FlushTrendWritesAsync(force: true, innerCt);
                }, ct);
            }
        }

        if (!skipLinkedByDnevnikTrigger && povracaj is not null)
            await RunImportStepAsync("import", "povracaj_zaglavlje", povracaj, result, async innerCt =>
            {
                await ImportPovracajAsync(session, povracaj, overwriteExisting, result, innerCt);
                await FlushTrendWritesAsync(force: true, innerCt);
            }, ct);

        if (!skipLinkedByDnevnikTrigger && povracajStavke is not null)
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

        if (!skipLinkedByDnevnikTrigger && !string.IsNullOrWhiteSpace(saleLineTableForCostDiscovery))
        {
            var saleLineColumns = await session.GetColumnsAsync(saleLineTableForCostDiscovery, ct);
            saleLineCostColumnsKnown = true;
            saleLineCostColumnsPresent = SourceColumnsContainProdajaLineNabavnaCena(saleLineColumns);

            if (saleLineCostColumnsPresent)
            {
                _logger.LogInformation(
                    "Access prodaja line source contains purchase-price columns. TableName={TableName}. Approximation fallback backfill will be skipped.",
                    saleLineTableForCostDiscovery);
            }
        }

        if (!skipLinkedByDnevnikTrigger)
        {
            if (saleLineCostColumnsKnown && !saleLineCostColumnsPresent)
            {
                await ApplyApproximateProdajaStavkeNabavnaCenaBackfillAsync(result, ct);
            }
            else if (!saleLineCostColumnsKnown)
            {
                result.Warnings.Add(
                    "Nije potvrdeno da li source prodajne stavke sadrze nabavnu cenu po stavci; fallback backfill iz Artikli master podataka nije automatski primenjen.");
            }
        }

        await AppendImportedSalesDiagnosticsAsync(result, ct);

        if (!skipLinkedByDnevnikTrigger &&
            prodaja is null &&
            dnevnik is not null &&
            !importedProdajaFromLineTable &&
            !synthesizedProdajaFromDnevnik)
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
        ActiveIncrementalTableScope? incrementalScope = null;

        try
        {
            incrementalScope = await BeginIncrementalTableScopeAsync(result.BatchId, tableKey, tableName, ct);
            _activeIncrementalScope = incrementalScope;

            _logger.LogInformation(
                "Access import step started. Step: {Step}. TableKey: {TableKey}. TableName: {TableName}. StartedAtUtc: {StartedAtUtc}. IncrementalFilter: {IncrementalFilter}. IncrementalCommit: {IncrementalCommit}.",
                step,
                tableKey,
                tableName ?? "<none>",
                started,
                incrementalScope?.ApplyFilter ?? false,
                incrementalScope?.CommitOnSuccess ?? false);
            await PersistBatchProgressAsync("step-start", force: true, ct);

            await action(ct);
            await EnsureBatchNotCancelledAsync(result.BatchId, ct);

            if (incrementalScope is not null)
            {
                await CompleteIncrementalTableScopeAsync(incrementalScope, ct);
                CaptureIncrementalSnapshot(incrementalScope, "completed");
            }

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
                GetActiveTrendWriteBatchSize());
            await PersistBatchProgressAsync("step-complete", force: true, ct);
        }
        catch (Exception ex)
        {
            if (incrementalScope is not null)
            {
                await MarkIncrementalTableScopeFailedAsync(incrementalScope, ex);
                CaptureIncrementalSnapshot(incrementalScope, "failed", ex.GetBaseException().Message);
            }
            throw;
        }
        finally
        {
            if (incrementalScope is not null)
                await ReleaseIncrementalTableScopeAsync(incrementalScope);
            _activeIncrementalScope = null;
        }
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
        await foreach (var row in ReadRowsForTableAsync(session, table, "tipovi_obuce", ct))
        {
            MarkSourceRow(result, "tipovi_obuce");
            var naziv = S(row, "naziv", "tip", "tipobuce", "name");
            if (string.IsNullOrWhiteSpace(naziv)) continue;
            var id = I(row, "id", "idtipobuce", "tipid");
            if (!id.HasValue) continue;
            MarkAccepted(result, "tipovi_obuce");
            TrackIncrementalAcceptedRow("tipovi_obuce", row);
            TrackAnalyticsTypeId(id.Value);

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
        await foreach (var row in ReadRowsForTableAsync(session, table, "dobavljaci", ct))
        {
            MarkSourceRow(result, "dobavljaci");
            var naziv = S(row, "naziv", "dobavljac", "supplier", "name");
            if (string.IsNullOrWhiteSpace(naziv)) continue;
            var id = I(row, "id", "iddobavljac", "supplierid");
            if (!id.HasValue) continue;
            MarkAccepted(result, "dobavljaci");
            TrackIncrementalAcceptedRow("dobavljaci", row);
            TrackAnalyticsSupplierId(id.Value);

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
        await foreach (var row in ReadRowsForTableAsync(session, table, "sezone", ct))
        {
            MarkSourceRow(result, "sezone");
            var naziv = S(row, "naziv", "sezona", "name");
            if (string.IsNullOrWhiteSpace(naziv)) continue;
            var id = I(row, "id", "idsezona", "seasonid");
            if (!id.HasValue) continue;
            MarkAccepted(result, "sezone");
            TrackIncrementalAcceptedRow("sezone", row);
            TrackAnalyticsSeasonId(id.Value);

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
        await foreach (var row in ReadRowsForTableAsync(session, table, "objekti", ct))
        {
            MarkSourceRow(result, "objekti");
            var id = I(row, "id", "idobjekat", "storeid", "idobjekta", "poslovnicaid");
            if (!id.HasValue) continue;
            MarkAccepted(result, "objekti");
            TrackIncrementalAcceptedRow("objekti", row);
            var naziv = S(row, "nazivobjekta", "naziv", "storename", "name", "poslovnica",
                          "ime", "opisobjekta") ?? $"Objekat {id.Value}";
            _importedStores[id.Value] = (
                Name: naziv,
                Address: S(row, "adresa", "address", "ulica"),
                Phone: S(row, "telefon", "phone", "tel", "mobilni"),
                Manager: S(row, "menedzer", "manager", "rukovodilac", "vodja", "direktorfiliajle"));
            result.ObjekatInserted++;
            TrackAnalyticsStoreId(id.Value);
        }
    }

    private async Task ImportArtikliAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var preloadSw = System.Diagnostics.Stopwatch.StartNew();
        var existingRows = await _trendDb.Artikli
            .AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.SourceTableKey,
                x.SourceRowId,
                x.SourceUpdatedAtUtc,
                x.SourceHash,
                x.SourceBatchId
            })
            .ToListAsync(ct);
        var existingIds = existingRows.Select(x => x.Id).ToHashSet();
        var existingLineageById = existingRows.ToDictionary(
            x => x.Id,
            x => new AccessLineageSnapshot
            {
                SourceTableKey = x.SourceTableKey,
                SourceRowId = x.SourceRowId,
                SourceUpdatedAtUtc = x.SourceUpdatedAtUtc,
                SourceHash = x.SourceHash,
                SourceBatchId = x.SourceBatchId
            });
        preloadSw.Stop();

        _logger.LogInformation(
            "Access import artikli existing-id preload completed. ExistingCount: {ExistingCount}. DurationMs: {DurationMs}. TableName: {TableName}. Operation: {Operation}.",
            existingIds.Count,
            preloadSw.ElapsedMilliseconds,
            table,
            "artikli-id-preload");

        var usedIds = existingIds.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        var trackedCurrentBatch = new Dictionary<int, Artikli>(Math.Max(32, _options.DbSaveBatchSize));
        var modifiedCurrentBatch = new HashSet<int>();
        ArtikliAliasMap? aliasMap = null;
        var artikliSw = Stopwatch.StartNew();
        var sourceRows = 0;
        var acceptedRows = 0;
        var trackedReuseHits = 0;
        var flushCount = 0;
        var insertedBeforeLoop = result.ArtikliInserted;
        var updatedBeforeLoop = result.ArtikliUpdated;

        await foreach (var row in ReadRowsForTableAsync(session, table, "artikli", ct))
        {
            sourceRows++;
            MarkSourceRow(result, "artikli");
            if (aliasMap is null)
            {
                aliasMap = BuildArtikliAliasMap(row.Columns);
                _logger.LogInformation(
                    "Access import artikli alias map initialized. TableName: {TableName}. Columns: {ColumnCount}. IdAlias: {IdAlias}. NazivAlias: {NazivAlias}. Operation: {Operation}.",
                    table,
                    row.Columns.Count,
                    aliasMap.IdAlias ?? "<missing>",
                    aliasMap.NazivAlias ?? "<missing>",
                    "artikli-alias-map");
            }

            var naziv = SNormalized(row, aliasMap.NazivAlias);
            if (string.IsNullOrWhiteSpace(naziv)) continue;
            MarkAccepted(result, "artikli");
            TrackIncrementalAcceptedRow("artikli", row);
            acceptedRows++;
            var id = INormalized(row, aliasMap.IdAlias);

            Artikli? e = null;
            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && trackedCurrentBatch.TryGetValue(sourceId, out var tracked))
            {
                e = tracked;
                trackedReuseHits++;
            }

            var isInsert = false;
            if (e is null)
            {
                var assignedId = sourceId;
                if (assignedId > 0 && existingIds.Contains(assignedId))
                {
                    if (!overwriteExisting)
                        continue;

                    if (existingLineageById.TryGetValue(assignedId, out var existingLineage) &&
                        ShouldSkipStaleOrUnchangedAccessOverwrite(existingLineage, "artikli", row))
                    {
                        continue;
                    }

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
                    existingLineageById[assignedId] = new AccessLineageSnapshot();
                    result.ArtikliInserted++;
                    isInsert = true;
                }
            }
            else
            {
                if (!overwriteExisting)
                    continue;

                if (ShouldSkipStaleOrUnchangedAccessOverwrite(e, "artikli", row))
                    continue;

                result.ArtikliUpdated++;
            }

            ApplyArtikliValues(e, row, naziv!, aliasMap);
            ApplyAccessSourceLineage(e, "artikli", row);
            existingLineageById[e.Id] = new AccessLineageSnapshot
            {
                SourceTableKey = e.SourceTableKey,
                SourceRowId = e.SourceRowId,
                SourceUpdatedAtUtc = e.SourceUpdatedAtUtc,
                SourceHash = e.SourceHash,
                SourceBatchId = e.SourceBatchId
            };
            TrackAnalyticsProductId(e.Id);
            TrackAnalyticsTypeId(e.IDTipObuce);
            TrackAnalyticsSupplierId(e.IDDobavljac);
            TrackAnalyticsSeasonId(e.IDSezona);
            TrackAnalyticsStoreId(e.IDObjekat);

            if (!isInsert && modifiedCurrentBatch.Add(e.Id))
                _trendDb.Artikli.Update(e);

            TrackTrendWrite();
            var shouldClearTrackedBatch = _pendingTrendWrites >= GetActiveTrendWriteBatchSize();
            await FlushTrendWritesAsync(force: false, ct);
            if (shouldClearTrackedBatch)
            {
                flushCount++;
                trackedCurrentBatch.Clear();
                modifiedCurrentBatch.Clear();
            }

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

        var rowsPerSecond = acceptedRows / Math.Max(1d, artikliSw.Elapsed.TotalSeconds);
        _logger.LogInformation(
            "Access import artikli completed. SourceRows: {SourceRows}. AcceptedRows: {AcceptedRows}. InsertedDelta: {InsertedDelta}. UpdatedDelta: {UpdatedDelta}. ExistingCount: {ExistingCount}. TrackedReuseHits: {TrackedReuseHits}. FlushCount: {FlushCount}. RowsPerSecond: {RowsPerSecond}. DurationMs: {DurationMs}. TableName: {TableName}. Operation: {Operation}.",
            sourceRows,
            acceptedRows,
            result.ArtikliInserted - insertedBeforeLoop,
            result.ArtikliUpdated - updatedBeforeLoop,
            existingIds.Count,
            trackedReuseHits,
            flushCount,
            rowsPerSecond,
            artikliSw.ElapsedMilliseconds,
            table,
            "artikli-complete");
    }

    private sealed record ArtikliAliasMap(
        string? IdAlias,
        string? NazivAlias,
        string? PluAlias,
        string? TipAlias,
        string? DobavljacAlias,
        string? NabavnaCenaAlias,
        string? NabavnaCenaDinAlias,
        string? PrvaProdajnaCenaAlias,
        string? ProdajnaCenaAlias,
        string? VelicinaAlias,
        string? BojaAlias,
        string? MaterijalAlias,
        string? KolicinaAlias,
        string? MinimalnaKolicinaAlias,
        string? KomentarAlias,
        string? ObjekatAlias,
        string? SezonaAlias,
        string? KategorijaAlias,
        string? PolAlias,
        string? ImagePathAlias);

    private static ArtikliAliasMap BuildArtikliAliasMap(IReadOnlyList<string> columns)
    {
        var normalizedColumns = new HashSet<string>(columns.Count, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++)
        {
            var normalized = Normalize(columns[i]);
            if (!string.IsNullOrWhiteSpace(normalized))
                normalizedColumns.Add(normalized);
        }

        return new ArtikliAliasMap(
            IdAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliIdAliases),
            NazivAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliNazivAliases),
            PluAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliPluAliases),
            TipAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliTipAliases),
            DobavljacAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliDobavljacAliases),
            NabavnaCenaAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliNabavnaCenaAliases),
            NabavnaCenaDinAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliNabavnaCenaDinAliases),
            PrvaProdajnaCenaAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliPrvaProdajnaCenaAliases),
            ProdajnaCenaAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliProdajnaCenaAliases),
            VelicinaAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliVelicinaAliases),
            BojaAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliBojaAliases),
            MaterijalAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliMaterijalAliases),
            KolicinaAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliKolicinaAliases),
            MinimalnaKolicinaAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliMinimalnaKolicinaAliases),
            KomentarAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliKomentarAliases),
            ObjekatAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliObjekatAliases),
            SezonaAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliSezonaAliases),
            KategorijaAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliKategorijaAliases),
            PolAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliPolAliases),
            ImagePathAlias: ResolveNormalizedAlias(normalizedColumns, ArtikliImagePathAliases));
    }

    private static string? ResolveNormalizedAlias(HashSet<string> normalizedColumns, IReadOnlyList<string> aliases)
    {
        for (var i = 0; i < aliases.Count; i++)
        {
            var normalized = Normalize(aliases[i]);
            if (!string.IsNullOrWhiteSpace(normalized) && normalizedColumns.Contains(normalized))
                return normalized;
        }

        return null;
    }

    private static void ApplyArtikliValues(Artikli entity, AccessDataRow row, string naziv, ArtikliAliasMap aliases)
    {
        entity.PLU = SNormalized(row, aliases.PluAlias);
        entity.Naziv = naziv;
        entity.IDTipObuce = INormalized(row, aliases.TipAlias);
        entity.IDDobavljac = INormalized(row, aliases.DobavljacAlias);
        entity.NabavnaCena = DNormalized(row, aliases.NabavnaCenaAlias);
        entity.NabavnaCenaDin = DNormalized(row, aliases.NabavnaCenaDinAlias);
        entity.PrvaProdajnaCena = DNormalized(row, aliases.PrvaProdajnaCenaAlias);
        entity.ProdajnaCena = DNormalized(row, aliases.ProdajnaCenaAlias);
        entity.Velicina = SNormalized(row, aliases.VelicinaAlias);
        entity.Boja = SNormalized(row, aliases.BojaAlias);
        entity.Materijal = SNormalized(row, aliases.MaterijalAlias);
        entity.Kolicina = INormalized(row, aliases.KolicinaAlias);
        entity.MinimalnaKolicina = INormalized(row, aliases.MinimalnaKolicinaAlias);
        entity.Komentar = SNormalized(row, aliases.KomentarAlias);
        entity.IDObjekat = INormalized(row, aliases.ObjekatAlias);
        entity.IDSezona = INormalized(row, aliases.SezonaAlias);
        entity.Kategorija = SNormalized(row, aliases.KategorijaAlias);
        entity.Pol = SNormalized(row, aliases.PolAlias);
        entity.ImagePath = SNormalized(row, aliases.ImagePathAlias);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.DataOrigin = "access";
    }

    private async Task ImportProdajaAsync(IAccessDataReaderSession session, string table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var existing = ToFirstDictionary(_trendDb.ProdajaZaglavlja.AsNoTracking().ToList(), x => x.Id);
        await foreach (var row in ReadRowsForTableAsync(session, table, "prodaja_zaglavlje", ct))
        {
            MarkSourceRow(result, "prodaja_zaglavlje");
            var id = I(row, "id", "idprodaja", "saleid", "iddnevnik");
            if (!id.HasValue) continue;
            MarkAccepted(result, "prodaja_zaglavlje");
            TrackIncrementalAcceptedRow("prodaja_zaglavlje", row);
            TrackAnalyticsSaleId(id.Value);

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
                if (ShouldSkipStaleOrUnchangedAccessOverwrite(e, "prodaja_zaglavlje", row))
                    continue;

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

            ApplyAccessSourceLineage(e, "prodaja_zaglavlje", row);
            TrackAnalyticsStoreId(e.IDObjekat);

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportProdajaStavkeAsync(IAccessDataReaderSession session, string table, string? parentTable, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        var existing = ToFirstDictionary(_trendDb.ProdajaStavke.AsNoTracking().ToList(), x => x.Id);
        var usedIds = existing.Keys.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;

        // Snapshot existing composite-key counts so we can preserve legitimate duplicate source rows
        // while still avoiding re-inserting occurrences that are already present in PostgreSQL.
        var existingCompositeKeys = new Dictionary<(int, int, decimal), int>();
        foreach (var e in existing.Values)
        {
            var ck = (e.IdProdaja, e.IdArtikal, e.Cena);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }
        var consumedExistingCompositeKeys = new Dictionary<(int, int, decimal), int>();
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
        await foreach (var row in ReadRowsForTableAsync(session, table, "prodaja_stavke", ct))
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
            TrackIncrementalAcceptedRow("prodaja_stavke", row);
            TrackAnalyticsSaleId(idProdaja.Value);
            TrackAnalyticsProductId(idArtikal.Value);

            var id = I(row, "id", "idstavka", "lineid");
            var qty = I(row, "kolicina", "qty", "quantity") ?? 0;
            var cena = D(row, "cena", "unitprice", "price") ?? 0m;
            var nabavnaCena = ResolveProdajaLineNabavnaCena(row);

            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && existing.TryGetValue(sourceId, out var e))
            {
                if (!overwriteExisting) continue;
                if (ShouldSkipStaleOrUnchangedAccessOverwrite(e, "prodaja_stavke", row))
                    continue;

                e.IdProdaja = idProdaja.Value;
                e.IdArtikal = idArtikal.Value;
                e.Kolicina = qty;
                e.Cena = cena;
                if (nabavnaCena.HasValue)
                    e.NabavnaCena = nabavnaCena.Value;
                ApplyAccessSourceLineage(e, "prodaja_stavke", row);
                _trendDb.ProdajaStavke.Update(e);
                result.ProdajaStavkeUpdated++;
                TrackTrendWrite();
            }
            else
            {
                var compositeKey = (idProdaja.Value, idArtikal.Value, cena);
                existingCompositeKeys.TryGetValue(compositeKey, out var existingCompositeCount);
                consumedExistingCompositeKeys.TryGetValue(compositeKey, out var consumedExistingCompositeCount);
                if (consumedExistingCompositeCount < existingCompositeCount)
                {
                    consumedExistingCompositeKeys[compositeKey] = consumedExistingCompositeCount + 1;
                    continue;
                }

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
                    Cena = cena,
                    NabavnaCena = nabavnaCena
                };
                ApplyAccessSourceLineage(newLine, "prodaja_stavke", row);
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
        // Composite-key multiset to detect duplicate inserts when Access rows lack a stable id.
        var existingCompositeKeys = new Dictionary<(string, int, DateTime, decimal), int>();
        foreach (var v in existing.Values)
        {
            var ck = (v.TipPromene ?? "", v.ArtikalId ?? 0, v.Datum, v.Iznos);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }
        var supplierByKey = _trendDb.Dobavljaci
            .AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.Naziv))
            .AsEnumerable()
            .GroupBy(x => NormalizeLookup(x.Naziv), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        await foreach (var row in ReadRowsForTableAsync(session, table, "dnevnik_promena", ct))
        {
            MarkSourceRow(result, "dnevnik_promena");
            var id = I(row, "id", "iddnevnik", "iddnevnikpromene", "iddnevnikpromena", "iddnevprom", "idlog", "logid", "seqno");
            var tip = S(row, "tippromene", "vrstapromene", "vrsta", "tip", "type", "eventtype", "tipprocene", "promena",
                         "vrstaknjizenjem", "vrstaknjiz", "document", "doctype") ?? "Unos";
            var datum = DT(row, "datum", "datumizmene", "datumdokumenta", "datumprocene", "date", "eventdate", "datumpromena") ?? DateTime.UtcNow;
            var iznos = D(row, "iznos", "cena", "prodajnacena", "saleprice", "amount", "total", "vrednost", "ukupno",
                          "novacena", "novaprodajnacena", "iznospromene") ?? 0m;
            var artikalId = I(row, "idartikal", "idartikal", "artikalid", "artiklid", "productid", "idproizvoda",
                            "artikal", "sifra", "sifraartikla", "kodartikla");

            DnevnikPromena? e = null;
            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && dbExistingIds.Contains(sourceId) && existing.TryGetValue(sourceId, out var found))
                e = found;

            var isInsert = false;
            if (e is null)
            {
                // Composite-key dedup: skip if an identical (TipPromene, ArtikalId, Datum, Iznos) row already exists.
                var compositeKey = (tip, artikalId ?? 0, datum, iznos);
                if (existingCompositeKeys.TryGetValue(compositeKey, out var ckCount) && ckCount > 0)
                {
                    existingCompositeKeys[compositeKey] = ckCount - 1;
                    continue;
                }

                var assignedId = sourceId;
                if (assignedId <= 0 || usedIds.Contains(assignedId))
                    assignedId = AllocateNextId(usedIds, ref nextGeneratedId);
                else
                    usedIds.Add(assignedId);

                e = new DnevnikPromena { Id = assignedId, DataOrigin = "access" };
                _trendDb.DnevnikPromena.Add(e);
                existing[assignedId] = e;
                result.DnevnikInserted++;
                existingCompositeKeys[compositeKey] = existingCompositeKeys.GetValueOrDefault(compositeKey) + 1;
                isInsert = true;
            }
            else if (overwriteExisting)
            {
                if (ShouldSkipStaleOrUnchangedAccessOverwrite(e, "dnevnik_promena", row))
                    continue;

                result.DnevnikUpdated++;
            }
            else
            {
                continue;
            }
            MarkAccepted(result, "dnevnik_promena");
            TrackIncrementalAcceptedRow("dnevnik_promena", row);

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
            e.ArtikalId = artikalId;
            e.StaraProdajnaCena = D(row, "staracena", "stara", "staraprodajnacena", "cenabefore", "oldprice", "cenabefore");
            e.NovaProdajnaCena = D(row, "novacena", "nova", "novaprodajnacena", "cenaafter", "newprice");
            e.Komentar = komentar;
            e.KorisnikIme = S(row, "korisnik", "korisnikime", "user", "username", "operater", "radnik", "ime",
                              "operator", "prodavac", "cashier");
            e.IDObjekat = I(row, "idobjekat", "storeid", "idobjekta", "objekatid", "idposlovnice", "prodavnicaid");
            e.RedniBroj = I(row, "rednibr", "rednibrojartikla", "rbrartikla", "rbr", "rbroj",
                             "sek", "seqno", "seq", "linebr", "redni");
            e.DataOrigin = "access";
            ApplyAccessSourceLineage(e, "dnevnik_promena", row);
            TrackAnalyticsMovementId(e.Id);
            TrackAnalyticsProductId(e.ArtikalId ?? 0);
            TrackAnalyticsSupplierId(e.DobavljacId);
            TrackAnalyticsStoreId(e.IDObjekat);

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
        var pendingHeadersByBusinessKey = new Dictionary<string, Domain.Model.Prodaja.ProdajaZaglavlje>(StringComparer.OrdinalIgnoreCase);
        var pendingHeaderIdsByBusinessKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var updatedHeaderIds = new HashSet<int>();
        var missingDnevnikHeaderRowCount = 0;
        var missingDnevnikHeaderIds = new HashSet<int>();
        var flushProbeCounter = 0;

        await foreach (var row in ReadRowsForTableAsync(session, table, "prodaja_stavke", ct))
        {
            var sourceSaleId = I(row, "iddnevnik", "idprodaja", "saleid", "iddnevnikpromene", "iddnevnikpromena");
            var idArtikal = I(row, "idartikal", "artikalid", "artiklid", "productid");
            if (!sourceSaleId.HasValue || sourceSaleId.Value <= 0 || !idArtikal.HasValue || idArtikal.Value <= 0)
                continue;

            MarkSourceRow(result, "prodaja_zaglavlje");
            MarkSourceRow(result, "prodaja_stavke");

            dnevnikById.TryGetValue(sourceSaleId.Value, out var dnevnik);
            var rowSaleDate = DT(row, "datumprodaje", "datum", "saledate");
            if (dnevnik is null && !rowSaleDate.HasValue)
            {
                missingDnevnikHeaderRowCount++;
                missingDnevnikHeaderIds.Add(sourceSaleId.Value);
                continue;
            }

            MarkAccepted(result, "prodaja_zaglavlje");
            TrackIncrementalAcceptedRow("prodaja_zaglavlje", row);
            MarkAccepted(result, "prodaja_stavke");
            TrackIncrementalAcceptedRow("prodaja_stavke", row);
            TrackAnalyticsSaleId(sourceSaleId.Value);
            TrackAnalyticsProductId(idArtikal.Value);

            var qty = I(row, "kolicina", "qty", "quantity") ?? 1;
            if (qty <= 0)
                qty = 1;

            var cena = D(row, "prodajnacena", "cena", "unitprice", "price") ?? 0m;
            var nabavnaCena = ResolveProdajaLineNabavnaCena(row);
            var idObjekat = I(row, "idobjekat", "storeid");
            var brojRacunaFromRow = S(row, "brojracuna", "brojkalkulacije", "invoice", "receiptnumber");

            if (!existingZaglavlja.TryGetValue(sourceSaleId.Value, out var zaglavlje))
            {
                var brojRacuna = dnevnik?.BrojRacuna ?? brojRacunaFromRow;
                var businessKey = BuildProdajaHeaderBusinessKey(sourceSaleId.Value, brojRacuna);

                if (!pendingHeadersByBusinessKey.TryGetValue(businessKey, out zaglavlje))
                {
                    zaglavlje = new Domain.Model.Prodaja.ProdajaZaglavlje
                    {
                        Id = sourceSaleId.Value,
                        BrojRacuna = brojRacuna,
                        DatumProdaje = dnevnik?.Datum ?? rowSaleDate!.Value,
                        NacinPlacanja = S(row, "nacinplacanja", "paymenttype"),
                        IDObjekat = idObjekat,
                        DataOrigin = "access"
                    };
                    _trendDb.ProdajaZaglavlja.Add(zaglavlje);
                    pendingHeadersByBusinessKey[businessKey] = zaglavlje;
                    pendingHeaderIdsByBusinessKey[businessKey] = zaglavlje.Id;
                    existingZaglavlja[zaglavlje.Id] = zaglavlje;
                    if (!string.IsNullOrWhiteSpace(zaglavlje.BrojRacuna))
                        existingBrojevi[zaglavlje.BrojRacuna] = zaglavlje;
                    result.ProdajaInserted++;
                    TrackTrendWrite();
                }
            }
            else if (overwriteExisting)
            {
                if (string.IsNullOrWhiteSpace(zaglavlje.BrojRacuna))
                    zaglavlje.BrojRacuna = brojRacunaFromRow;
                if (zaglavlje.IDObjekat is null && idObjekat.HasValue)
                    zaglavlje.IDObjekat = idObjekat.Value;
                zaglavlje.DataOrigin = "access";
                if (updatedHeaderIds.Add(zaglavlje.Id))
                {
                    if (_trendDb.Entry(zaglavlje).State == EntityState.Detached)
                        _trendDb.ProdajaZaglavlja.Update(zaglavlje);
                    result.ProdajaUpdated++;
                    TrackTrendWrite();
                }
            }

            ApplyAccessSourceLineage(zaglavlje, "prodaja_zaglavlje", row);
            TrackAnalyticsStoreId(zaglavlje.IDObjekat);

            var lineKey = BuildProdajaLineKey(zaglavlje.Id, idArtikal.Value, qty, cena);
            existingLineCounts.TryGetValue(lineKey, out var existingCountForKey);
            consumedExistingLineCounts.TryGetValue(lineKey, out var consumedCountForKey);

            if (consumedCountForKey < existingCountForKey)
            {
                consumedExistingLineCounts[lineKey] = consumedCountForKey + 1;
                continue;
            }

            var line = new Domain.Model.Prodaja.ProdajaStavka
            {
                Id = ++maxStavkaId,
                IdProdaja = zaglavlje.Id,
                IdArtikal = idArtikal.Value,
                Kolicina = qty,
                Cena = cena,
                NabavnaCena = nabavnaCena
            };
            ApplyAccessSourceLineage(line, "prodaja_stavke", row);
            if (_trendDb.Entry(zaglavlje).State == EntityState.Added)
                line.Prodaja = zaglavlje;

            _trendDb.ProdajaStavke.Add(line);
            result.ProdajaStavkeInserted++;
            TrackTrendWrite();

            flushProbeCounter++;
            if (flushProbeCounter >= 128)
            {
                flushProbeCounter = 0;
                await FlushTrendWritesAsync(force: false, ct);

                // Keep explicit business-key -> persisted id mapping up to date after each flush.
                foreach (var (businessKey, header) in pendingHeadersByBusinessKey)
                    pendingHeaderIdsByBusinessKey[businessKey] = header.Id;
            }
        }

        foreach (var (businessKey, header) in pendingHeadersByBusinessKey)
            pendingHeaderIdsByBusinessKey[businessKey] = header.Id;

        if (pendingHeaderIdsByBusinessKey.Count > 0)
        {
            _logger.LogDebug(
                "Access import prodaja header-id map refreshed after buffered ingest. HeaderKeys: {HeaderKeys}.",
                pendingHeaderIdsByBusinessKey.Count);
        }

        if (missingDnevnikHeaderRowCount > 0)
        {
            var sample = string.Join(", ", missingDnevnikHeaderIds.Take(10));
            var suffix = missingDnevnikHeaderIds.Count > 10 ? " ..." : string.Empty;
            result.Warnings.Add(
                $"Tabela '{table}' ima {missingDnevnikHeaderRowCount} stavki prodaje bez odgovarajuceg reda u DnevnikPromena i bez validnog datuma. Preskoceno je {missingDnevnikHeaderIds.Count} racuna kako bi se izbeglo upisivanje datuma importa kao datuma prodaje. Primeri IDDnevnik: {sample}{suffix}.");
        }
    }

    private async Task ApplyApproximateProdajaStavkeNabavnaCenaBackfillAsync(AccessImportRunResponse result, CancellationToken ct)
    {
        var unresolvedBefore = await (
            from ps in _trendDb.ProdajaStavke
            join pz in _trendDb.ProdajaZaglavlja on ps.IdProdaja equals pz.Id
            where pz.DataOrigin == "access" && ps.NabavnaCena == null
            select ps.Id
        ).CountAsync(ct);

        if (unresolvedBefore == 0)
            return;

        var fromDin = await (
            from ps in _trendDb.ProdajaStavke
            join pz in _trendDb.ProdajaZaglavlja on ps.IdProdaja equals pz.Id
            join a in _trendDb.Artikli on ps.IdArtikal equals a.Id
            where pz.DataOrigin == "access"
                  && ps.NabavnaCena == null
                  && a.NabavnaCenaDin != null
            select ps.Id
        ).CountAsync(ct);

        var fromLegacy = await (
            from ps in _trendDb.ProdajaStavke
            join pz in _trendDb.ProdajaZaglavlja on ps.IdProdaja equals pz.Id
            join a in _trendDb.Artikli on ps.IdArtikal equals a.Id
            where pz.DataOrigin == "access"
                  && ps.NabavnaCena == null
                  && a.NabavnaCenaDin == null
                  && a.NabavnaCena != null
            select ps.Id
        ).CountAsync(ct);

        var updated = await _trendDb.Database.ExecuteSqlRawAsync(
            """
            UPDATE prodaja_stavke ps
            SET nabavna_cena = COALESCE(a."NabavnaCenaDin", a."NabavnaCena")
            FROM prodaja_zaglavlje pz, "Artikli" a
            WHERE ps.id_prodaja = pz.id
              AND a."Id" = ps.id_artikal
              AND pz.data_origin = 'access'
              AND ps.nabavna_cena IS NULL
              AND COALESCE(a."NabavnaCenaDin", a."NabavnaCena") IS NOT NULL;
            """,
            Array.Empty<object>(),
            ct);

        var unresolvedAfter = await (
            from ps in _trendDb.ProdajaStavke
            join pz in _trendDb.ProdajaZaglavlja on ps.IdProdaja equals pz.Id
            where pz.DataOrigin == "access" && ps.NabavnaCena == null
            select ps.Id
        ).CountAsync(ct);

        var approximationWarning =
            "ProdajaStavke.NabavnaCena fallback backfill je uradjen iz Artikli master podataka (COALESCE(NabavnaCenaDin, NabavnaCena)); ovo je aproksimacija, nije istorijska nabavna cena sa trenutka prodaje.";

        if (!result.Warnings.Any(x => x.Equals(approximationWarning, StringComparison.Ordinal)))
            result.Warnings.Add(approximationWarning);

        _logger.LogInformation(
            "Access sale-line cost fallback applied (approximation). Updated={Updated}. CandidatesDin={CandidatesDin}. CandidatesLegacy={CandidatesLegacy}. RemainingNullBefore={Before}. RemainingNullAfter={After}.",
            updated,
            fromDin,
            fromLegacy,
            unresolvedBefore,
            unresolvedAfter);
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

        await foreach (var row in ReadRowsForTableAsync(session, table, "povracaj_zaglavlje", ct))
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
                if (ShouldSkipStaleOrUnchangedAccessOverwrite(e, "povracaj_zaglavlje", row))
                    continue;

                result.PovracajUpdated++;
            }
            else
            {
                continue;
            }

            MarkAccepted(result, "povracaj_zaglavlje");
            TrackIncrementalAcceptedRow("povracaj_zaglavlje", row);

            e.BrojZapisnika = broj;
            e.IDDobavljac = idDobavljac;
            e.DatumPovracaja = datum;
            e.RazlogPovracaja = S(row, "razlog", "reason", "razlogpovracaja");
            e.Status = S(row, "status") ?? "Kreiran";
            e.UkupanIznos = D(row, "ukupaniznos", "total", "iznos") ?? 0m;
            e.Komentar = S(row, "komentar", "comment", "napomena");
            e.KreatorKorisnik = S(row, "korisnik", "kreirao", "user", "username", "operater");
            e.DataOrigin = "access";
            ApplyAccessSourceLineage(e, "povracaj_zaglavlje", row);

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

        // Composite-key multiset to detect duplicate inserts when Access rows lack a stable id.
        var existingCompositeKeys = new Dictionary<(int, int, decimal), int>();
        foreach (var v in existing.Values)
        {
            var ck = (v.IdPovracaj, v.IdArtikal, v.Cena);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }

        await foreach (var row in ReadRowsForTableAsync(session, table, "povracaj_stavke", ct))
        {
            MarkSourceRow(result, "povracaj_stavke");

            var idPovracaj = I(row, "idpovracaj", "returnid", "idzaglavlje");
            var idArtikal = I(row, "idartikal", "productid", "artiklid");
            if (!idPovracaj.HasValue || !idArtikal.HasValue || !povracajIds.Contains(idPovracaj.Value))
                continue;

            MarkAccepted(result, "povracaj_stavke");
            TrackIncrementalAcceptedRow("povracaj_stavke", row);

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
                if (ShouldSkipStaleOrUnchangedAccessOverwrite(e, "povracaj_stavke", row))
                    continue;

                e.IdPovracaj = idPovracaj.Value;
                e.IdArtikal = idArtikal.Value;
                e.Kolicina = qty;
                e.Cena = cena;
                e.Razlog = razlog;
                e.StanjeArtikla = stanje;
                ApplyAccessSourceLineage(e, "povracaj_stavke", row);
                _trendDb.PovracajStavke.Update(e);
                result.PovracajStavkeUpdated++;
                TrackTrendWrite();
            }
            else
            {
                // Composite-key dedup: skip if an identical (IdPovracaj, IdArtikal, Cena) row already exists.
                var compositeKey = (idPovracaj.Value, idArtikal.Value, cena);
                if (existingCompositeKeys.TryGetValue(compositeKey, out var ckCount) && ckCount > 0)
                {
                    existingCompositeKeys[compositeKey] = ckCount - 1;
                    continue;
                }

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
                ApplyAccessSourceLineage(newLine, "povracaj_stavke", row);
                _trendDb.PovracajStavke.Add(newLine);
                existing[newLine.Id] = newLine;
                existingCompositeKeys[compositeKey] = existingCompositeKeys.GetValueOrDefault(compositeKey) + 1;
                result.PovracajStavkeInserted++;
                TrackTrendWrite();
            }

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private sealed record NivelacijaSourceSnapshot(DateTime Datum, int? IDObjekat, int? DobavljacId);

    private async Task<Dictionary<int, NivelacijaSourceSnapshot>> LoadNivelacijaSourceSnapshotsAsync(
        IAccessDataReaderSession session,
        CancellationToken ct)
    {
        var tables = await session.GetTablesAsync(includeTemporaryTables: true, ct);
        var dnevnikTable = await FindTableAsync(
            session,
            tables,
            DnevnikPromenaCandidates,
            sigRequired: ["iddnevnik", "datum"],
            ct: ct);

        if (dnevnikTable is null)
            return new Dictionary<int, NivelacijaSourceSnapshot>();

        var snapshots = new Dictionary<int, NivelacijaSourceSnapshot>();
        await foreach (var row in ReadRowsForTableAsync(session, dnevnikTable, "nivelacija_source_dnevnik", ct))
        {
            var sourceId = I(row, "id", "iddnevnik", "iddnevnikpromene", "iddnevnikpromena", "iddnevprom", "idlog", "logid", "seqno");
            var datum = DT(row, "datum", "datumizmene", "datumdokumenta", "datumprocene", "date", "eventdate", "datumpromena");
            if (!sourceId.HasValue || !datum.HasValue)
                continue;

            snapshots[sourceId.Value] = new NivelacijaSourceSnapshot(
                datum.Value,
                I(row, "idobjekat", "storeid", "idobjekta", "objekatid", "idposlovnice", "prodavnicaid"),
                I(row, "iddobavljac", "dobavljacid", "supplierid", "idd", "iddob"));
        }

        return snapshots;
    }

    private Dictionary<int, NivelacijaSourceSnapshot> LoadNivelacijaSourceSnapshots(OdbcConnection conn)
    {
        var tables = GetUserTables(conn, includeTemporaryTables: true);
        var dnevnikTable = FindTable(conn, tables, DnevnikPromenaCandidates, sigRequired: ["iddnevnik", "datum"]);
        if (dnevnikTable is null)
            return new Dictionary<int, NivelacijaSourceSnapshot>();

        var snapshots = new Dictionary<int, NivelacijaSourceSnapshot>();
        foreach (var row in ReadRows(conn, dnevnikTable))
        {
            var sourceId = I(row, "id", "iddnevnik", "iddnevnikpromene", "iddnevnikpromena", "iddnevprom", "idlog", "logid", "seqno");
            var datum = DT(row, "datum", "datumizmene", "datumdokumenta", "datumprocene", "date", "eventdate", "datumpromena");
            if (!sourceId.HasValue || !datum.HasValue)
                continue;

            snapshots[sourceId.Value] = new NivelacijaSourceSnapshot(
                datum.Value,
                I(row, "idobjekat", "storeid", "idobjekta", "objekatid", "idposlovnice", "prodavnicaid"),
                I(row, "iddobavljac", "dobavljacid", "supplierid", "idd", "iddob"));
        }

        return snapshots;
    }

    private Dictionary<int, int?> LoadArtikalSupplierLookup()
    {
        var supplierByArticleId = _trendDb.Artikli.Local
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().IDDobavljac);

        foreach (var article in _trendDb.Artikli.AsNoTracking())
        {
            if (!supplierByArticleId.ContainsKey(article.Id))
                supplierByArticleId[article.Id] = article.IDDobavljac;
        }

        return supplierByArticleId;
    }

    private async Task ImportNivelacijeAsync(IAccessDataReaderSession session, string? table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        if (table is null)
            return;

        var dnevnikById = _trendDb.DnevnikPromena.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToDictionary(x => x.Id, x => x);
        Dictionary<int, NivelacijaSourceSnapshot>? sourceSnapshotsById = null;
        Dictionary<int, int?>? supplierByArticleId = null;

        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;

        // Composite-key multiset to detect duplicate inserts on re-import.
        var existingCompositeKeys = new Dictionary<(int, DateTime, decimal), int>();
        foreach (var d in dnevnikById.Values.Where(x => x.TipPromene == TipPromeneConstants.Nivelacija))
        {
            var ck = (d.ArtikalId ?? 0, d.Datum, d.Iznos);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }

        await foreach (var row in ReadRowsForTableAsync(session, table, "nivelacije", ct))
        {
            MarkSourceRow(result, "nivelacije");

            var idArtikal = I(row, "idartikal", "artikalid", "productid", "id_artikal");
            if (!idArtikal.HasValue)
                continue;

            var novaCena = D(row, "novacena", "novaprodajnacena", "newprice", "cena");
            if (!novaCena.HasValue)
                continue;

            MarkAccepted(result, "nivelacije");
            TrackIncrementalAcceptedRow("nivelacije", row);

            var staraCena = D(row, "staracena", "staraprodajnacena", "oldprice");
            var kolicina = I(row, "kolicina", "qty", "quantity") ?? 1;
            var iznos = Math.Abs((novaCena.Value - (staraCena ?? 0m)) * kolicina);
            var srcId = I(row, "iddnevnik", "id", "idlog") ?? 0;

            dnevnikById.TryGetValue(srcId, out var sourceDnevnik);
            var eventDate = DT(row, "datum", "datumnivelacije", "date")
                ?? sourceDnevnik?.Datum;
            var storeId = I(row, "idobjekat", "storeid", "idobjekta")
                ?? sourceDnevnik?.IDObjekat;
            var supplierId = I(row, "iddobavljac", "dobavljacid", "supplierid")
                ?? sourceDnevnik?.DobavljacId;

            // The source dnevnik scan is expensive on large MDB files, so only pay for it
            // when the current nivelacija row is missing context we cannot infer otherwise.
            if ((!eventDate.HasValue || !storeId.HasValue || !supplierId.HasValue) && srcId > 0)
            {
                sourceSnapshotsById ??= await LoadNivelacijaSourceSnapshotsAsync(session, ct);
                if (sourceSnapshotsById.TryGetValue(srcId, out var sourceSnapshot))
                {
                    eventDate ??= sourceSnapshot.Datum;
                    storeId ??= sourceSnapshot.IDObjekat;
                    supplierId ??= sourceSnapshot.DobavljacId;
                }
            }

            if (!supplierId.HasValue)
            {
                supplierByArticleId ??= LoadArtikalSupplierLookup();
                supplierByArticleId.TryGetValue(idArtikal.Value, out supplierId);
            }

            eventDate ??= DateTime.UtcNow;

            // Composite-key dedup: skip if an identical (ArtikalId, Datum, Iznos) row already exists.
            var compositeKey = (idArtikal.Value, eventDate.Value, iznos);
            if (existingCompositeKeys.TryGetValue(compositeKey, out var ckCount) && ckCount > 0)
            {
                existingCompositeKeys[compositeKey] = ckCount - 1;
                continue;
            }

            var assignedId = srcId > 0 && !usedIds.Contains(srcId)
                ? srcId
                : AllocateNextId(usedIds, ref next);

            usedIds.Add(assignedId);
            existingCompositeKeys[compositeKey] = existingCompositeKeys.GetValueOrDefault(compositeKey) + 1;
            var movement = new DnevnikPromena
            {
                Id = assignedId,
                TipPromene = TipPromeneConstants.Nivelacija,
                Datum = eventDate.Value,
                ArtikalId = idArtikal.Value,
                Kolicina = kolicina,
                StaraProdajnaCena = staraCena,
                NovaProdajnaCena = novaCena,
                Iznos = iznos,
                IDObjekat = storeId,
                RedniBroj = I(row, "rednibr", "rbr", "seqno"),
                BrojRacuna = S(row, "brdokumenta", "iddnevnik"),
                DobavljacId = supplierId,
                DataOrigin = "access"
            };
            ApplyAccessSourceLineage(movement, "nivelacije", row);
            _trendDb.DnevnikPromena.Add(movement);
            result.NivelacijeInserted++;
            TrackTrendWrite();
            TrackAnalyticsMovementId(assignedId);
            TrackAnalyticsProductId(idArtikal.Value);
            TrackAnalyticsSupplierId(supplierId);
            TrackAnalyticsStoreId(storeId);

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportUnosRobeAsync(IAccessDataReaderSession session, string? table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        if (table is null)
            return;

        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;

        // Composite-key multiset to detect duplicate inserts on re-import.
        var existingCompositeKeys = new Dictionary<(int, DateTime, decimal), int>();
        foreach (var d in _trendDb.DnevnikPromena.AsNoTracking()
            .Where(x => x.TipPromene == TipPromeneConstants.UlazRobe))
        {
            var ck = (d.ArtikalId ?? 0, d.Datum, d.Iznos);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }

        await foreach (var row in ReadRowsForTableAsync(session, table, "unos_robe", ct))
        {
            MarkSourceRow(result, "unos_robe");

            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue)
                continue;

            MarkAccepted(result, "unos_robe");
            TrackIncrementalAcceptedRow("unos_robe", row);

            var kolicina = I(row, "kolicina", "qty", "quantity") ?? 1;
            var nabavnaCena = D(row, "nabavnacena", "purchaseprice", "cena", "nc") ?? 0m;
            var datum = DT(row, "datum", "datumunosarobe", "datumulaza", "date") ?? DateTime.UtcNow;
            var iznos = nabavnaCena * kolicina;
            var srcId = I(row, "iddnevnik", "id", "idlog") ?? 0;

            // Composite-key dedup: skip if an identical (ArtikalId, Datum, Iznos) row already exists.
            var compositeKey = (idArtikal.Value, datum, iznos);
            if (existingCompositeKeys.TryGetValue(compositeKey, out var ckCount) && ckCount > 0)
            {
                existingCompositeKeys[compositeKey] = ckCount - 1;
                continue;
            }

            var assignedId = srcId > 0 && !usedIds.Contains(srcId)
                ? srcId
                : AllocateNextId(usedIds, ref next);

            usedIds.Add(assignedId);
            existingCompositeKeys[compositeKey] = existingCompositeKeys.GetValueOrDefault(compositeKey) + 1;
            var movement = new DnevnikPromena
            {
                Id = assignedId,
                TipPromene = TipPromeneConstants.UlazRobe,
                Datum = datum,
                ArtikalId = idArtikal.Value,
                Kolicina = kolicina,
                NovaProdajnaCena = nabavnaCena,
                Iznos = iznos,
                DobavljacId = I(row, "iddobavljac", "dobavljacid", "supplierid"),
                IDObjekat = I(row, "idobjekat", "storeid"),
                RedniBroj = I(row, "rednibr", "rbr", "seqno"),
                BrojRacuna = S(row, "brdokumenta", "iddnevnik"),
                DataOrigin = "access"
            };
            ApplyAccessSourceLineage(movement, "unos_robe", row);
            _trendDb.DnevnikPromena.Add(movement);
            result.UnosRobeInserted++;
            TrackTrendWrite();
            TrackAnalyticsMovementId(assignedId);
            TrackAnalyticsProductId(idArtikal.Value);
            TrackAnalyticsSupplierId(I(row, "iddobavljac", "dobavljacid", "supplierid"));
            TrackAnalyticsStoreId(I(row, "idobjekat", "storeid"));

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportPovratniceAsync(IAccessDataReaderSession session, string? table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        if (table is null)
            return;

        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;

        // Composite-key multiset to detect duplicate inserts on re-import.
        var existingCompositeKeys = new Dictionary<(int, DateTime, decimal), int>();
        foreach (var d in _trendDb.DnevnikPromena.AsNoTracking()
            .Where(x => x.TipPromene == TipPromeneConstants.PovratKupca))
        {
            var ck = (d.ArtikalId ?? 0, d.Datum, d.Iznos);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }

        await foreach (var row in ReadRowsForTableAsync(session, table, "povratnice", ct))
        {
            MarkSourceRow(result, "povratnice");

            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue)
                continue;

            MarkAccepted(result, "povratnice");
            TrackIncrementalAcceptedRow("povratnice", row);

            var kolicina = I(row, "kolicina", "qty", "quantity") ?? 1;
            var cena = D(row, "cena", "prodajnacena", "unitprice", "pc") ?? 0m;
            var datum = DT(row, "datum", "datumpovratnice", "date") ?? DateTime.UtcNow;
            var iznos = cena * kolicina;
            var srcId = I(row, "iddnevnik", "id", "idpovratnice", "idlog") ?? 0;

            // Composite-key dedup: skip if an identical (ArtikalId, Datum, Iznos) row already exists.
            var compositeKey = (idArtikal.Value, datum, iznos);
            if (existingCompositeKeys.TryGetValue(compositeKey, out var ckCount) && ckCount > 0)
            {
                existingCompositeKeys[compositeKey] = ckCount - 1;
                continue;
            }

            var assignedId = srcId > 0 && !usedIds.Contains(srcId)
                ? srcId
                : AllocateNextId(usedIds, ref next);

            usedIds.Add(assignedId);
            existingCompositeKeys[compositeKey] = existingCompositeKeys.GetValueOrDefault(compositeKey) + 1;
            var movement = new DnevnikPromena
            {
                Id = assignedId,
                TipPromene = TipPromeneConstants.PovratKupca,
                Datum = datum,
                ArtikalId = idArtikal.Value,
                Kolicina = kolicina,
                NovaProdajnaCena = cena,
                Iznos = iznos,
                IDObjekat = I(row, "idobjekat", "storeid"),
                RedniBroj = I(row, "rednibr", "rbr"),
                Komentar = S(row, "razlog", "reason", "napomena"),
                DataOrigin = "access"
            };
            ApplyAccessSourceLineage(movement, "povratnice", row);
            _trendDb.DnevnikPromena.Add(movement);
            result.PovratnicaInserted++;
            TrackTrendWrite();
            TrackAnalyticsMovementId(assignedId);
            TrackAnalyticsProductId(idArtikal.Value);
            TrackAnalyticsStoreId(I(row, "idobjekat", "storeid"));

            await FlushTrendWritesAsync(force: false, ct);
        }
    }

    private async Task ImportPrenosRobeAsync(IAccessDataReaderSession session, string? table, bool overwriteExisting, AccessImportRunResponse result, CancellationToken ct)
    {
        if (table is null)
            return;

        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;

        // Composite-key multiset to detect duplicate transfer entries on re-import.
        // Key includes TipPromene to distinguish izlaz/ulaz pairs.
        var existingCompositeKeys = new Dictionary<(string, int, DateTime, decimal), int>();
        foreach (var d in _trendDb.DnevnikPromena.AsNoTracking()
            .Where(x => x.TipPromene == TipPromeneConstants.PrenosIzlaz || x.TipPromene == TipPromeneConstants.PrenosUlaz))
        {
            var ck = (d.TipPromene ?? "", d.ArtikalId ?? 0, d.Datum, d.Iznos);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }

        await foreach (var row in ReadRowsForTableAsync(session, table, "prenos_robe", ct))
        {
            MarkSourceRow(result, "prenos_robe");

            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue)
                continue;

            MarkAccepted(result, "prenos_robe");
            TrackIncrementalAcceptedRow("prenos_robe", row);

            var kolicina = I(row, "kolicina", "qty", "quantity") ?? 1;
            var datum = DT(row, "datum", "datumprenos", "datumtransfera", "date") ?? DateTime.UtcNow;
            var cena = D(row, "cena", "nabavnacena", "prodajnacena") ?? 0m;
            var iznos = cena * kolicina;
            var idIz = I(row, "idobjekatiz", "idobjekatizlaza", "fromstore", "idobjekat");
            var idU = I(row, "idobjekatulaz", "idobjekatdolaz", "tostore", "idobjekatodredista");
            var brDok = S(row, "iddnevnik", "brdokumenta", "brprenos");

            // Composite-key dedup for izlaz entry.
            var ckIzlaz = (TipPromeneConstants.PrenosIzlaz, idArtikal.Value, datum, iznos);
            if (existingCompositeKeys.TryGetValue(ckIzlaz, out var ckOutCount) && ckOutCount > 0)
            {
                // Both izlaz and ulaz already exist — skip the whole pair.
                existingCompositeKeys[ckIzlaz] = ckOutCount - 1;
                var ckUlaz = (TipPromeneConstants.PrenosUlaz, idArtikal.Value, datum, iznos);
                if (existingCompositeKeys.TryGetValue(ckUlaz, out var ckInCount) && ckInCount > 0)
                    existingCompositeKeys[ckUlaz] = ckInCount - 1;
                continue;
            }

            var idOut = AllocateNextId(usedIds, ref next);
            existingCompositeKeys[ckIzlaz] = existingCompositeKeys.GetValueOrDefault(ckIzlaz) + 1;
            var movementOut = new DnevnikPromena
            {
                Id = idOut,
                TipPromene = TipPromeneConstants.PrenosIzlaz,
                Datum = datum,
                ArtikalId = idArtikal.Value,
                Kolicina = -kolicina,
                NovaProdajnaCena = cena,
                Iznos = iznos,
                IDObjekat = idIz,
                BrojRacuna = brDok,
                DataOrigin = "access"
            };
            ApplyAccessSourceLineage(movementOut, "prenos_robe", row);
            _trendDb.DnevnikPromena.Add(movementOut);
            result.PrenosRobeInserted++;
            TrackTrendWrite();
            TrackAnalyticsMovementId(idOut);
            TrackAnalyticsProductId(idArtikal.Value);
            TrackAnalyticsStoreId(idIz);

            var idIn = AllocateNextId(usedIds, ref next);
            var ckUlazNew = (TipPromeneConstants.PrenosUlaz, idArtikal.Value, datum, iznos);
            existingCompositeKeys[ckUlazNew] = existingCompositeKeys.GetValueOrDefault(ckUlazNew) + 1;
            var movementIn = new DnevnikPromena
            {
                Id = idIn,
                TipPromene = TipPromeneConstants.PrenosUlaz,
                Datum = datum,
                ArtikalId = idArtikal.Value,
                Kolicina = kolicina,
                NovaProdajnaCena = cena,
                Iznos = iznos,
                IDObjekat = idU,
                BrojRacuna = brDok,
                DataOrigin = "access"
            };
            ApplyAccessSourceLineage(movementIn, "prenos_robe", row);
            _trendDb.DnevnikPromena.Add(movementIn);
            result.PrenosRobeInserted++;
            TrackTrendWrite();
            TrackAnalyticsMovementId(idIn);
            TrackAnalyticsProductId(idArtikal.Value);
            TrackAnalyticsStoreId(idU);

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
            if (!id.HasValue) continue;
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
            if (!id.HasValue) continue;
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
            if (!id.HasValue) continue;
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
            if (!id.HasValue) continue;
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

        // Build a composite-key multiset to detect duplicate inserts when Access rows lack a stable id.
        var existingCompositeKeys = new Dictionary<(int, int, decimal), int>();
        foreach (var e in existing.Values)
        {
            var ck = (e.IdProdaja, e.IdArtikal, e.Cena);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }

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
                // Composite-key dedup: skip if an identical (idProdaja, idArtikal, cena) row already exists.
                var compositeKey = (idProdaja.Value, idArtikal.Value, cena);
                if (existingCompositeKeys.TryGetValue(compositeKey, out var ckCount) && ckCount > 0)
                {
                    existingCompositeKeys[compositeKey] = ckCount - 1;
                    continue;
                }

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
                existingCompositeKeys[compositeKey] = existingCompositeKeys.GetValueOrDefault(compositeKey) + 1;
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

            var line = new Domain.Model.Prodaja.ProdajaStavka
            {
                Id = ++maxStavkaId,
                IdProdaja = zaglavlje.Id,
                IdArtikal = idArtikal.Value,
                Kolicina = qty,
                Cena = cena
            };
            if (_trendDb.Entry(zaglavlje).State == EntityState.Added)
                line.Prodaja = zaglavlje;

            _trendDb.ProdajaStavke.Add(line);
            result.ProdajaStavkeInserted++;
        }
    }

    private static string BuildProdajaLineKey(int idProdaja, int idArtikal, int qty, decimal cena)
        => $"{idProdaja}|{idArtikal}|{qty}|{cena.ToString(CultureInfo.InvariantCulture)}";

    private static string BuildProdajaHeaderBusinessKey(int sourceSaleId, string? brojRacuna)
    {
        var normalizedBroj = string.IsNullOrWhiteSpace(brojRacuna)
            ? string.Empty
            : Normalize(brojRacuna);
        return $"{sourceSaleId}|{normalizedBroj}";
    }

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

        // Composite-key dedup: track existing stavke to avoid re-inserting on repeated imports
        var existingCompositeKeys = new Dictionary<(int, int, decimal), int>();
        foreach (var es in _trendDb.ProdajaStavke.AsNoTracking())
        {
            var ck = (es.IdProdaja, es.IdArtikal, es.Cena);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }
        var consumedExistingCompositeKeys = new Dictionary<(int, int, decimal), int>();

        var insertedProdaja = 0;
        var updatedProdaja = 0;
        var insertedStavke = 0;
        var skippedDuplicateStavke = 0;
        var flushProbeCounter = 0;

        foreach (var grp in groups)
        {
            var first = grp.First();
            TrackAnalyticsStoreId(first.IDObjekat);

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

                // Composite-key dedup: skip if this (IdProdaja, IdArtikal, Cena) already exists
                var compositeKey = (zaglavlje.Id, d.ArtikalId!.Value, stavkaCena);
                existingCompositeKeys.TryGetValue(compositeKey, out var existingCount);
                consumedExistingCompositeKeys.TryGetValue(compositeKey, out var consumedCount);
                if (consumedCount < existingCount)
                {
                    consumedExistingCompositeKeys[compositeKey] = consumedCount + 1;
                    skippedDuplicateStavke++;
                    continue;
                }

                var stavka = new Domain.Model.Prodaja.ProdajaStavka
                {
                    Id = ++maxStavkaId,
                    IdProdaja = zaglavlje.Id,
                    IdArtikal = d.ArtikalId!.Value,
                    Kolicina = stavkaQty,
                    Cena = stavkaCena
                };
                if (_trendDb.Entry(zaglavlje).State == EntityState.Added)
                    stavka.Prodaja = zaglavlje;

                _trendDb.ProdajaStavke.Add(stavka);
                result.ProdajaStavkeInserted++;
                insertedStavke++;
                TrackTrendWrite();
                TrackAnalyticsProductId(d.ArtikalId.Value);
                flushProbeCounter++;
                if (flushProbeCounter >= 128)
                {
                    flushProbeCounter = 0;
                    await FlushTrendWritesAsync(force: false, ct);
                }
            }

            TrackAnalyticsSaleId(zaglavlje.Id);
        }

        var summary = $"Sintetizovano {insertedProdaja} prodaja i {insertedStavke} stavki iz DnevnikPromena (nije pronadjena posebna tabela prodaje).";
        if (updatedProdaja > 0)
            summary += $" Azurirano zaglavlja: {updatedProdaja}.";
        if (skippedDuplicateStavke > 0)
            summary += $" Preskoceno dupliranih stavki: {skippedDuplicateStavke}.";
        result.Warnings.Add(summary);
        _logger.LogInformation(
            "Access import synthesized prodaja from dnevnik. ProdajaInserted: {ProdajaInserted}. ProdajaUpdated: {ProdajaUpdated}. ProdajaStavkeInserted: {ProdajaStavkeInserted}. SkippedDuplicateStavke: {SkippedDuplicateStavke}.",
            insertedProdaja,
            updatedProdaja,
            insertedStavke,
            skippedDuplicateStavke);
    }

    private void ImportPovracajStavke(OdbcConnection conn, string table, bool overwriteExisting, AccessImportRunResponse result)
    {
        var existing = ToFirstDictionary(_trendDb.PovracajStavke, x => x.Id);
        var usedIds = existing.Keys.ToHashSet();
        var nextGeneratedId = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        var povracajIds = _trendDb.PovracajZaglavlja.Select(x => x.Id).ToHashSet();

        // Composite-key multiset to detect duplicate inserts when Access rows lack a stable id.
        var existingCompositeKeys = new Dictionary<(int, int, decimal), int>();
        foreach (var v in existing.Values)
        {
            var ck = (v.IdPovracaj, v.IdArtikal, v.Cena);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }

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
                // Composite-key dedup: skip if an identical (IdPovracaj, IdArtikal, Cena) row already exists.
                var compositeKey = (idPovracaj.Value, idArtikal.Value, cena);
                if (existingCompositeKeys.TryGetValue(compositeKey, out var ckCount) && ckCount > 0)
                {
                    existingCompositeKeys[compositeKey] = ckCount - 1;
                    continue;
                }

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
                existingCompositeKeys[compositeKey] = existingCompositeKeys.GetValueOrDefault(compositeKey) + 1;
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
        // Composite-key multiset to detect duplicate inserts when Access rows lack a stable id.
        var existingCompositeKeys = new Dictionary<(string, int, DateTime, decimal), int>();
        foreach (var v in existing.Values)
        {
            var ck = (v.TipPromene ?? "", v.ArtikalId ?? 0, v.Datum, v.Iznos);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }
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
            var artikalId = I(row, "idartikal", "idartikal", "artikalid", "artiklid", "productid", "idproizvoda",
                            "artikal", "sifra", "sifraartikla", "kodartikla");

            DnevnikPromena? e = null;
            var sourceId = id.GetValueOrDefault();
            if (sourceId > 0 && dbExistingIds.Contains(sourceId) && existing.TryGetValue(sourceId, out var found))
                e = found;

            if (e is null)
            {
                // Composite-key dedup: skip if an identical (TipPromene, ArtikalId, Datum, Iznos) row already exists.
                var compositeKey = (tip, artikalId ?? 0, datum, iznos);
                if (existingCompositeKeys.TryGetValue(compositeKey, out var ckCount) && ckCount > 0)
                {
                    existingCompositeKeys[compositeKey] = ckCount - 1;
                    continue;
                }

                var assignedId = sourceId;
                if (assignedId <= 0 || usedIds.Contains(assignedId))
                    assignedId = AllocateNextId(usedIds, ref nextGeneratedId);
                else
                    usedIds.Add(assignedId);

                e = new DnevnikPromena { Id = assignedId, DataOrigin = "access" };
                _trendDb.DnevnikPromena.Add(e);
                existing[assignedId] = e;
                existingCompositeKeys[compositeKey] = existingCompositeKeys.GetValueOrDefault(compositeKey) + 1;
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
            e.ArtikalId = artikalId;
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
            if (!id.HasValue) continue;
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
        Dictionary<int, NivelacijaSourceSnapshot>? sourceSnapshotsById = null;
        Dictionary<int, int?>? supplierByArticleId = null;

        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;

        // Composite-key multiset to detect duplicate inserts on re-import.
        var existingCompositeKeys = new Dictionary<(int, DateTime, decimal), int>();
        foreach (var d in dnevnikById.Values.Where(x => x.TipPromene == TipPromeneConstants.Nivelacija))
        {
            var ck = (d.ArtikalId ?? 0, d.Datum, d.Iznos);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }

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
                ?? sourceDnevnik?.Datum;
            var storeId = I(row, "idobjekat", "storeid", "idobjekta")
                ?? sourceDnevnik?.IDObjekat;
            var supplierId = I(row, "iddobavljac", "dobavljacid", "supplierid")
                ?? sourceDnevnik?.DobavljacId;

            if ((!eventDate.HasValue || !storeId.HasValue || !supplierId.HasValue) && srcId > 0)
            {
                sourceSnapshotsById ??= LoadNivelacijaSourceSnapshots(conn);
                if (sourceSnapshotsById.TryGetValue(srcId, out var sourceSnapshot))
                {
                    eventDate ??= sourceSnapshot.Datum;
                    storeId ??= sourceSnapshot.IDObjekat;
                    supplierId ??= sourceSnapshot.DobavljacId;
                }
            }

            if (!supplierId.HasValue)
            {
                supplierByArticleId ??= LoadArtikalSupplierLookup();
                supplierByArticleId.TryGetValue(idArtikal.Value, out supplierId);
            }

            eventDate ??= DateTime.UtcNow;

            // Composite-key dedup: skip if an identical (ArtikalId, Datum, Iznos) row already exists.
            var compositeKey = (idArtikal.Value, eventDate.Value, iznos);
            if (existingCompositeKeys.TryGetValue(compositeKey, out var ckCount) && ckCount > 0)
            {
                existingCompositeKeys[compositeKey] = ckCount - 1;
                continue;
            }

            var assignedId = (srcId > 0 && !usedIds.Contains(srcId))
                ? srcId : AllocateNextId(usedIds, ref next);
            usedIds.Add(assignedId);
            existingCompositeKeys[compositeKey] = existingCompositeKeys.GetValueOrDefault(compositeKey) + 1;
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = assignedId,
                TipPromene = TipPromeneConstants.Nivelacija,
                Datum = eventDate.Value,
                ArtikalId = idArtikal.Value,
                Kolicina = kolicina,
                StaraProdajnaCena = staraCena,
                NovaProdajnaCena = novaCena,
                Iznos = iznos,
                IDObjekat = storeId,
                RedniBroj = I(row, "rednibr", "rbr", "seqno"),
                BrojRacuna = S(row, "brdokumenta", "iddnevnik"),
                DobavljacId = supplierId,
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
        // Composite-key multiset to detect duplicate inserts on re-import.
        var existingCompositeKeys = new Dictionary<(int, DateTime, decimal), int>();
        foreach (var d in _trendDb.DnevnikPromena.AsNoTracking()
            .Where(x => x.TipPromene == TipPromeneConstants.UlazRobe))
        {
            var ck = (d.ArtikalId ?? 0, d.Datum, d.Iznos);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }
        foreach (var row in ReadRows(conn, table))
        {
            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue) continue;
            MarkAccepted(result, "unos_robe");
            var kolicina     = I(row, "kolicina", "qty", "quantity") ?? 1;
            var nabavnaCena  = D(row, "nabavnacena", "purchaseprice", "cena", "nc") ?? 0m;
            var datum        = DT(row, "datum", "datumunosarobe", "datumulaza", "date") ?? DateTime.UtcNow;
            var iznos        = nabavnaCena * kolicina;
            var srcId        = I(row, "iddnevnik", "id", "idlog") ?? 0;

            // Composite-key dedup: skip if an identical (ArtikalId, Datum, Iznos) row already exists.
            var compositeKey = (idArtikal.Value, datum, iznos);
            if (existingCompositeKeys.TryGetValue(compositeKey, out var ckCount) && ckCount > 0)
            {
                existingCompositeKeys[compositeKey] = ckCount - 1;
                continue;
            }

            var assignedId   = (srcId > 0 && !usedIds.Contains(srcId))
                ? srcId : AllocateNextId(usedIds, ref next);
            usedIds.Add(assignedId);
            existingCompositeKeys[compositeKey] = existingCompositeKeys.GetValueOrDefault(compositeKey) + 1;
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = assignedId,
                TipPromene = TipPromeneConstants.UlazRobe,
                Datum = datum,
                ArtikalId = idArtikal.Value,
                Kolicina = kolicina,
                NovaProdajnaCena = nabavnaCena,
                Iznos = iznos,
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
        // Composite-key multiset to detect duplicate inserts on re-import.
        var existingCompositeKeys = new Dictionary<(int, DateTime, decimal), int>();
        foreach (var d in _trendDb.DnevnikPromena.AsNoTracking()
            .Where(x => x.TipPromene == TipPromeneConstants.PovratKupca))
        {
            var ck = (d.ArtikalId ?? 0, d.Datum, d.Iznos);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }
        foreach (var row in ReadRows(conn, table))
        {
            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue) continue;
            MarkAccepted(result, "povratnice");
            var kolicina  = I(row, "kolicina", "qty", "quantity") ?? 1;
            var cena      = D(row, "cena", "prodajnacena", "unitprice", "pc") ?? 0m;
            var datum     = DT(row, "datum", "datumpovratnice", "date") ?? DateTime.UtcNow;
            var iznos     = cena * kolicina;
            var srcId     = I(row, "iddnevnik", "id", "idpovratnice", "idlog") ?? 0;

            // Composite-key dedup: skip if an identical (ArtikalId, Datum, Iznos) row already exists.
            var compositeKey = (idArtikal.Value, datum, iznos);
            if (existingCompositeKeys.TryGetValue(compositeKey, out var ckCount) && ckCount > 0)
            {
                existingCompositeKeys[compositeKey] = ckCount - 1;
                continue;
            }

            var assignedId = (srcId > 0 && !usedIds.Contains(srcId))
                ? srcId : AllocateNextId(usedIds, ref next);
            usedIds.Add(assignedId);
            existingCompositeKeys[compositeKey] = existingCompositeKeys.GetValueOrDefault(compositeKey) + 1;
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = assignedId,
                TipPromene = TipPromeneConstants.PovratKupca,
                Datum = datum,
                ArtikalId = idArtikal.Value,
                Kolicina = kolicina,
                NovaProdajnaCena = cena,
                Iznos = iznos,
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
        // Each transfer row → TWO DnevnikPromena entries: izlaz from source + ulaz to destination
        if (table is null) return;
        var usedIds = GetDnevnikPromenaUsedIds();
        var next = usedIds.Count == 0 ? 1 : usedIds.Max() + 1;
        // Composite-key multiset to detect duplicate transfer entries on re-import.
        var existingCompositeKeys = new Dictionary<(string, int, DateTime, decimal), int>();
        foreach (var d in _trendDb.DnevnikPromena.AsNoTracking()
            .Where(x => x.TipPromene == TipPromeneConstants.PrenosIzlaz || x.TipPromene == TipPromeneConstants.PrenosUlaz))
        {
            var ck = (d.TipPromene ?? "", d.ArtikalId ?? 0, d.Datum, d.Iznos);
            existingCompositeKeys[ck] = existingCompositeKeys.GetValueOrDefault(ck) + 1;
        }
        foreach (var row in ReadRows(conn, table))
        {
            var idArtikal = I(row, "idartikal", "artikalid", "productid");
            if (!idArtikal.HasValue) continue;
            MarkAccepted(result, "prenos_robe");
            var kolicina = I(row, "kolicina", "qty", "quantity") ?? 1;
            var datum    = DT(row, "datum", "datumprenos", "datumtransfera", "date") ?? DateTime.UtcNow;
            var cena     = D(row, "cena", "nabavnacena", "prodajnacena") ?? 0m;
            var iznos    = cena * kolicina;
            var idIz     = I(row, "idobjekatiz", "idobjekatizlaza", "fromstore", "idobjekat");
            var idU      = I(row, "idobjekatulaz", "idobjekatdolaz", "tostore", "idobjekatodredista");
            var brDok    = S(row, "iddnevnik", "brdokumenta", "brprenos");

            // Composite-key dedup for izlaz entry.
            var ckIzlaz = (TipPromeneConstants.PrenosIzlaz, idArtikal.Value, datum, iznos);
            if (existingCompositeKeys.TryGetValue(ckIzlaz, out var ckOutCount) && ckOutCount > 0)
            {
                existingCompositeKeys[ckIzlaz] = ckOutCount - 1;
                var ckUlaz = (TipPromeneConstants.PrenosUlaz, idArtikal.Value, datum, iznos);
                if (existingCompositeKeys.TryGetValue(ckUlaz, out var ckInCount) && ckInCount > 0)
                    existingCompositeKeys[ckUlaz] = ckInCount - 1;
                continue;
            }

            // Prenos izlaz (source store)
            var idOut = AllocateNextId(usedIds, ref next);
            existingCompositeKeys[ckIzlaz] = existingCompositeKeys.GetValueOrDefault(ckIzlaz) + 1;
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = idOut, TipPromene = TipPromeneConstants.PrenosIzlaz, Datum = datum,
                ArtikalId = idArtikal.Value, Kolicina = -kolicina,
                NovaProdajnaCena = cena, Iznos = iznos,
                IDObjekat = idIz, BrojRacuna = brDok, DataOrigin = "access"
            });
            // Prenos ulaz (destination store)
            var idIn = AllocateNextId(usedIds, ref next);
            var ckUlazNew = (TipPromeneConstants.PrenosUlaz, idArtikal.Value, datum, iznos);
            existingCompositeKeys[ckUlazNew] = existingCompositeKeys.GetValueOrDefault(ckUlazNew) + 1;
            _trendDb.DnevnikPromena.Add(new DnevnikPromena
            {
                Id = idIn, TipPromene = TipPromeneConstants.PrenosUlaz, Datum = datum,
                ArtikalId = idArtikal.Value, Kolicina = kolicina,
                NovaProdajnaCena = cena, Iznos = iznos,
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
        if (!_options.EnableFastWritePath)
        {
            _logger.LogWarning(
                "AccessImport:EnableFastWritePath is disabled, but analytics sync requires fast bulk path in this build. Proceeding with fast path.");
        }

        if (!await TrySyncAnalyticsFastAsync(result, ct))
        {
            throw new InvalidOperationException(
                "Analytics sync did not execute because there was no prepared batch delta payload.");
        }
    }

    private sealed record ProductsDimSyncRow(
        int ProductId,
        string? Plu,
        string ProductName,
        string Category,
        string SubCategory,
        string Brand,
        string? Velicina,
        string? Boja,
        string? Materijal,
        int? FootwearTypeId,
        int? SupplierId,
        int? SeasonId,
        decimal? PurchasePrice,
        decimal? PurchasePriceRsd,
        decimal? FirstSalePrice,
        decimal? SalePrice,
        bool IsActive,
        DateTime Timestamp,
        int? Kolicina,
        int? MinimalnaKolicina,
        string DataOrigin);

    private sealed record StoreDimSyncRow(
        int StoreId,
        string StoreName,
        string? City,
        string Region,
        string? Telefon,
        string? Menedzer,
        string DataOrigin);

    private sealed record SalesFactSyncRow(
        int SaleId,
        string BrojRacuna,
        DateTime SaleTimestampUtc,
        int StoreId,
        string PaymentType,
        decimal TotalAmount,
        int TotalUnits,
        int TotalLines,
        string DataOrigin);

    private sealed record SaleHeaderSyncRow(
        int Id,
        string? BrojRacuna,
        DateTime DatumProdaje,
        int? IDObjekat,
        string? NacinPlacanja);

    private sealed record SaleLineSourceSyncRow(
        int IdProdaja,
        int IdArtikal,
        int Kolicina,
        decimal Cena,
        decimal? NabavnaCena);

    private sealed record SalesLineFactSyncRow(
        int SaleId,
        int ProductId,
        int Qty,
        decimal UnitPrice,
        decimal LineTotal,
        decimal? NabavnaCena,
        string DataOrigin);

    private sealed record SupplierDimSyncRow(
        int SupplierId,
        string Naziv,
        string? Adresa,
        string? Telefon,
        string? Napomena,
        string DataOrigin,
        DateTime UpdatedAt);

    private sealed record SeasonDimSyncRow(
        int SeasonId,
        string Naziv,
        DateTime DatumOd,
        DateTime DatumDo,
        string DataOrigin,
        DateTime UpdatedAt);

    private sealed record FootwearTypeDimSyncRow(
        int TypeId,
        string Naziv,
        string DataOrigin,
        DateTime UpdatedAt);

    private sealed record InventoryMovementSyncRow(
        int SourceId,
        string TipPromene,
        DateTime Datum,
        int? ArtikalId,
        int? Kolicina,
        decimal? StaraProdajnaCena,
        decimal? NovaProdajnaCena,
        decimal Iznos,
        int? StoreId,
        int? DobavljacId,
        string? BrojDokumenta,
        string? KorisnikIme,
        string DataOrigin);

    private string GetAnalyticsConnectionString()
    {
        var connectionString = _analyticsDb.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = _analyticsDb.Database.GetDbConnection().ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Analytics DB connection string is not configured.");
        return connectionString;
    }

    private static async Task ExecuteAnalyticsNonQueryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        CancellationToken ct,
        ILogger logger)
    {
        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            sw.Stop();
            try
            {
                Infrastructure.Logging.SqlCommandLoggingHelper.LogSqlExecution(
                    dbSource: "analytics",
                    commandKind: "ExecuteNonQuery",
                    sql: sql,
                    parameters: cmd.Parameters,
                    durationMs: sw.ElapsedMilliseconds,
                    succeeded: true,
                    rowsAffected: rows,
                    exception: null,
                    requestId: Application.Logging.RequestLogContext.Current.RequestId,
                    traceId: Application.Logging.RequestLogContext.Current.TraceId);
            }
            catch { }
        }
        catch (PostgresException ex)
        {
            sw.Stop();
            try
            {
                Infrastructure.Logging.SqlCommandLoggingHelper.LogSqlExecution(
                    dbSource: "analytics",
                    commandKind: "ExecuteNonQuery",
                    sql: sql,
                    parameters: cmd.Parameters,
                    durationMs: sw.ElapsedMilliseconds,
                    succeeded: false,
                    rowsAffected: null,
                    exception: ex,
                    requestId: Application.Logging.RequestLogContext.Current.RequestId,
                    traceId: Application.Logging.RequestLogContext.Current.TraceId);
            }
            catch { }

            logger.LogError(ex, "SQL FAILED: {Sql}", sql);
            throw;
        }
    }

    private static async Task<bool> HasProductsDimProductIdUniqueIndexAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_index i
                JOIN pg_class t ON t.oid = i.indrelid
                JOIN pg_namespace ns ON ns.oid = t.relnamespace
                JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(i.indkey)
                WHERE t.relname = 'ProductsDim'
                  AND ns.nspname = ANY (current_schemas(false))
                  AND i.indisunique
                  AND i.indisvalid
                  AND i.indpred IS NULL
                  AND i.indnkeyatts = 1
                  AND a.attname = 'ProductId'
            );
            """;

        await using var cmd = new NpgsqlCommand(sql, connection, transaction);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    private static void WriteNullableInt(NpgsqlBinaryImporter importer, int? value)
    {
        if (value.HasValue)
            importer.Write(value.Value, NpgsqlDbType.Integer);
        else
            importer.WriteNull();
    }

    private static void WriteNullableDecimal(NpgsqlBinaryImporter importer, decimal? value)
    {
        if (value.HasValue)
            importer.Write(value.Value, NpgsqlDbType.Numeric);
        else
            importer.WriteNull();
    }

    private static void WriteNullableString(NpgsqlBinaryImporter importer, string? value)
    {
        if (value is null)
            importer.WriteNull();
        else
            importer.Write(value, NpgsqlDbType.Text);
    }

    private sealed record AnalyticsFastPayload(
        IReadOnlyList<ProductsDimSyncRow> Products,
        IReadOnlyList<StoreDimSyncRow> Stores,
        IReadOnlyList<SalesFactSyncRow> SalesFacts,
        IReadOnlyList<SalesLineFactSyncRow> SalesLineFacts,
        IReadOnlyList<SupplierDimSyncRow> Suppliers,
        IReadOnlyList<SeasonDimSyncRow> Seasons,
        IReadOnlyList<FootwearTypeDimSyncRow> Types,
        IReadOnlyList<InventoryMovementSyncRow> Movements,
        IReadOnlyCollection<int> SaleIds,
        IReadOnlyCollection<int> ExistingProductIds,
        IReadOnlyCollection<int> ExistingStoreIds,
        IReadOnlyCollection<int> ExistingSaleIds)
    {
        public bool IsEmpty =>
            Products.Count == 0 &&
            Stores.Count == 0 &&
            SalesFacts.Count == 0 &&
            SalesLineFacts.Count == 0 &&
            Suppliers.Count == 0 &&
            Seasons.Count == 0 &&
            Types.Count == 0 &&
            Movements.Count == 0 &&
            SaleIds.Count == 0;
    }

    private async Task<bool> TrySyncAnalyticsFastAsync(AccessImportRunResponse result, CancellationToken ct)
    {
        var productIds = _analyticsDeltaProductIds.Where(x => x > 0).Distinct().ToArray();
        var saleIds = _analyticsDeltaSaleIds.Where(x => x > 0).Distinct().ToArray();
        var movementIds = _analyticsDeltaMovementIds.Where(x => x > 0).Distinct().ToArray();
        var supplierIds = _analyticsDeltaSupplierIds.Where(x => x > 0).Distinct().ToArray();
        var seasonIds = _analyticsDeltaSeasonIds.Where(x => x > 0).Distinct().ToArray();
        var typeIds = _analyticsDeltaTypeIds.Where(x => x > 0).Distinct().ToArray();
        var storeIds = _analyticsDeltaStoreIds.Where(x => x > 0).Distinct().ToArray();

        if (productIds.Length == 0 &&
            saleIds.Length == 0 &&
            movementIds.Length == 0 &&
            supplierIds.Length == 0 &&
            seasonIds.Length == 0 &&
            typeIds.Length == 0 &&
            storeIds.Length == 0 &&
            _importedStores.Count == 0)
        {
            _logger.LogInformation(
                "Access analytics fast sync skipped because the current batch produced no analytics delta.");
            return true;
        }

        try
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation(
                "Access analytics fast sync started. Products: {Products}. Sales: {Sales}. Movements: {Movements}. Suppliers: {Suppliers}. Seasons: {Seasons}. Types: {Types}. Stores: {Stores}.",
                productIds.Length,
                saleIds.Length,
                movementIds.Length,
                supplierIds.Length,
                seasonIds.Length,
                typeIds.Length,
                Math.Max(storeIds.Length, _importedStores.Count));

            var payload = await BuildAnalyticsFastPayloadAsync(
                productIds,
                saleIds,
                movementIds,
                supplierIds,
                seasonIds,
                typeIds,
                storeIds,
                ct);
            if (payload.IsEmpty)
                return true;

            await ApplyAnalyticsFastPayloadAsync(payload, result, ct);

            sw.Stop();
            _logger.LogInformation(
                "Access analytics fast sync completed. DurationMs: {DurationMs}. Products: {Products}. Sales: {Sales}. SalesLines: {SalesLines}. Movements: {Movements}.",
                sw.ElapsedMilliseconds,
                payload.Products.Count,
                payload.SalesFacts.Count,
                payload.SalesLineFacts.Count,
                payload.Movements.Count);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Access analytics fast sync failed.");
            throw;
        }
    }

    private async Task<AnalyticsFastPayload> BuildAnalyticsFastPayloadAsync(
        int[] productIds,
        int[] saleIds,
        int[] movementIds,
        int[] supplierIds,
        int[] seasonIds,
        int[] typeIds,
        int[] storeIds,
        CancellationToken ct)
    {
        var products = productIds.Length == 0
            ? new List<ProductsDimSyncRow>()
            : await _trendDb.Artikli
                .AsNoTracking()
                .Where(x => productIds.Contains(x.Id))
                .Select(x => new ProductsDimSyncRow(
                    x.Id,
                    x.PLU,
                    x.Naziv ?? string.Empty,
                    x.Kategorija ?? string.Empty,
                    x.Pol ?? string.Empty,
                    string.Empty,
                    x.Velicina,
                    x.Boja,
                    x.Materijal,
                    x.IDTipObuce,
                    x.IDDobavljac,
                    x.IDSezona,
                    x.NabavnaCena,
                    x.NabavnaCenaDin,
                    x.PrvaProdajnaCena,
                    x.ProdajnaCena,
                    true,
                    DateTime.UtcNow,
                    x.Kolicina,
                    x.MinimalnaKolicina,
                    "access"))
                .ToListAsync(ct);

        var sales = saleIds.Length == 0
            ? new List<SaleHeaderSyncRow>()
            : await _trendDb.ProdajaZaglavlja
                .AsNoTracking()
                .Where(x => saleIds.Contains(x.Id))
                .Select(x => new SaleHeaderSyncRow(
                    x.Id,
                    x.BrojRacuna,
                    x.DatumProdaje,
                    x.IDObjekat,
                    x.NacinPlacanja))
                .ToListAsync(ct);

        var salesLinesRaw = saleIds.Length == 0
            ? new List<SaleLineSourceSyncRow>()
            : await _trendDb.ProdajaStavke
                .AsNoTracking()
                .Where(x => saleIds.Contains(x.IdProdaja))
                .Select(x => new SaleLineSourceSyncRow(
                    x.IdProdaja,
                    x.IdArtikal,
                    x.Kolicina,
                    x.Cena,
                    x.NabavnaCena))
                .ToListAsync(ct);

        var salesLineFacts = salesLinesRaw
            .Select(x => new SalesLineFactSyncRow(
                x.IdProdaja,
                x.IdArtikal,
                x.Kolicina,
                x.Cena,
                x.Kolicina * x.Cena,
                x.NabavnaCena,
                "access"))
            .ToList();

        var salesLineBySale = salesLineFacts
            .GroupBy(x => x.SaleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var salesFacts = new List<SalesFactSyncRow>(sales.Count);
        foreach (var sale in sales)
        {
            salesLineBySale.TryGetValue(sale.Id, out var linesForSale);
            var lines = linesForSale ?? new List<SalesLineFactSyncRow>();
            var storeId = sale.IDObjekat ?? 1;
            TrackAnalyticsStoreId(storeId);
            salesFacts.Add(new SalesFactSyncRow(
                sale.Id,
                sale.BrojRacuna ?? string.Empty,
                DateTime.SpecifyKind(sale.DatumProdaje, DateTimeKind.Utc),
                storeId,
                sale.NacinPlacanja ?? string.Empty,
                lines.Sum(x => x.LineTotal),
                lines.Sum(x => x.Qty),
                lines.Count,
                "access"));
        }

        var effectiveStoreIds = _analyticsDeltaStoreIds
            .Where(x => x > 0)
            .Concat(storeIds)
            .Concat(salesFacts.Select(x => x.StoreId))
            .Distinct()
            .ToArray();
        var stores = effectiveStoreIds
            .Select(storeId =>
            {
                _importedStores.TryGetValue(storeId, out var storeData);
                return new StoreDimSyncRow(
                    storeId,
                    storeData.Name ?? $"Objekat {storeId}",
                    storeData.Address ?? "N/A",
                    "N/A",
                    storeData.Phone,
                    storeData.Manager,
                    "access");
            })
            .ToList();

        var suppliers = supplierIds.Length == 0
            ? new List<SupplierDimSyncRow>()
            : await _trendDb.Dobavljaci
                .AsNoTracking()
                .Where(x => supplierIds.Contains(x.Id))
                .Select(x => new SupplierDimSyncRow(
                    x.Id,
                    x.Naziv ?? string.Empty,
                    x.Adresa,
                    x.Telefon,
                    x.Napomena,
                    string.IsNullOrWhiteSpace(x.DataOrigin) ? "access" : x.DataOrigin,
                    DateTime.UtcNow))
                .ToListAsync(ct);

        var seasons = seasonIds.Length == 0
            ? new List<SeasonDimSyncRow>()
            : await _trendDb.Sezone
                .AsNoTracking()
                .Where(x => seasonIds.Contains(x.Id))
                .Select(x => new SeasonDimSyncRow(
                    x.Id,
                    x.Naziv ?? string.Empty,
                    DateTime.SpecifyKind(x.DatumOd, DateTimeKind.Utc),
                    DateTime.SpecifyKind(x.DatumDo, DateTimeKind.Utc),
                    string.IsNullOrWhiteSpace(x.DataOrigin) ? "access" : x.DataOrigin,
                    DateTime.UtcNow))
                .ToListAsync(ct);

        var types = typeIds.Length == 0
            ? new List<FootwearTypeDimSyncRow>()
            : await _trendDb.TipoviObuce
                .AsNoTracking()
                .Where(x => typeIds.Contains(x.Id))
                .Select(x => new FootwearTypeDimSyncRow(
                    x.Id,
                    x.Naziv ?? string.Empty,
                    string.IsNullOrWhiteSpace(x.DataOrigin) ? "access" : x.DataOrigin,
                    DateTime.UtcNow))
                .ToListAsync(ct);

        var movements = movementIds.Length == 0
            ? new List<InventoryMovementSyncRow>()
            : await _trendDb.DnevnikPromena
                .AsNoTracking()
                .Where(x => movementIds.Contains(x.Id))
                .Select(x => new InventoryMovementSyncRow(
                    x.Id,
                    x.TipPromene ?? string.Empty,
                    DateTime.SpecifyKind(x.Datum, DateTimeKind.Utc),
                    x.ArtikalId,
                    x.Kolicina,
                    x.StaraProdajnaCena,
                    x.NovaProdajnaCena,
                    x.Iznos,
                    x.IDObjekat,
                    x.DobavljacId,
                    x.BrojRacuna,
                    x.KorisnikIme,
                    "access"))
                .ToListAsync(ct);

        var productIdList = products.Select(x => x.ProductId).Distinct().ToArray();
        var storeIdList = stores.Select(x => x.StoreId).Distinct().ToArray();
        var saleIdList = salesFacts.Select(x => x.SaleId).Distinct().ToArray();

        var existingProductIds = productIdList.Length == 0
            ? new HashSet<int>()
            : (await _analyticsDb.ProductsDim.AsNoTracking()
                .Where(x => productIdList.Contains(x.ProductId))
                .Select(x => x.ProductId)
                .ToListAsync(ct))
            .ToHashSet();
        var existingStoreIds = storeIdList.Length == 0
            ? new HashSet<int>()
            : (await _analyticsDb.StoresDim.AsNoTracking()
                .Where(x => storeIdList.Contains(x.StoreId))
                .Select(x => x.StoreId)
                .ToListAsync(ct))
            .ToHashSet();
        var existingSaleIds = saleIdList.Length == 0
            ? new HashSet<int>()
            : (await _analyticsDb.SalesFacts.AsNoTracking()
                .Where(x => saleIdList.Contains(x.SaleId))
                .Select(x => x.SaleId)
                .ToListAsync(ct))
            .ToHashSet();

        return new AnalyticsFastPayload(
            products,
            stores,
            salesFacts,
            salesLineFacts,
            suppliers,
            seasons,
            types,
            movements,
            saleIdList,
            existingProductIds,
            existingStoreIds,
            existingSaleIds);
    }

    private async Task ApplyAnalyticsFastPayloadAsync(
        AnalyticsFastPayload payload,
        AccessImportRunResponse result,
        CancellationToken ct)
    {
        var analyticsConnectionString = GetAnalyticsConnectionString();
        await using var connection = new NpgsqlConnection(analyticsConnectionString);
        await connection.OpenAsync(ct);

        // Execute each analytics step in its own transaction to isolate failures.
        async Task RunStepAsync(Func<NpgsqlTransaction, Task> step, string stepName)
        {
            await using var stepTx = await connection.BeginTransactionAsync(ct);
            try
            {
                await step(stepTx);
                await stepTx.CommitAsync(ct);
            }
            catch (PostgresException ex)
            {
                _logger.LogError(ex, "Analytics step '{Step}' failed. Rolling back step.", stepName);
                try { await stepTx.RollbackAsync(ct); } catch { }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analytics step '{Step}' failed with exception. Rolling back step.", stepName);
                try { await stepTx.RollbackAsync(ct); } catch { }
                throw;
            }
        }

        if (payload.Stores.Count > 0)
            await RunStepAsync(tx => UpsertStoresDimBulkAsync(connection, tx, payload.Stores, ct), "UpsertStoresDimBulkAsync");
        if (payload.Products.Count > 0)
            await RunStepAsync(tx => UpsertProductsDimBulkAsync(connection, tx, payload.Products, ct), "UpsertProductsDimBulkAsync");
        if (payload.SalesFacts.Count > 0)
            await RunStepAsync(tx => UpsertSalesFactsBulkAsync(connection, tx, payload.SalesFacts, ct), "UpsertSalesFactsBulkAsync");
        if (payload.SaleIds.Count > 0)
            await RunStepAsync(tx => ReplaceSalesLineFactsBulkAsync(connection, tx, payload.SaleIds, payload.SalesLineFacts, ct), "ReplaceSalesLineFactsBulkAsync");
        if (payload.Suppliers.Count > 0)
            await RunStepAsync(tx => UpsertSuppliersDimBulkAsync(connection, tx, payload.Suppliers, ct), "UpsertSuppliersDimBulkAsync");
        if (payload.Seasons.Count > 0)
            await RunStepAsync(tx => UpsertSeasonsDimBulkAsync(connection, tx, payload.Seasons, ct), "UpsertSeasonsDimBulkAsync");
        if (payload.Types.Count > 0)
            await RunStepAsync(tx => UpsertFootwearTypesDimBulkAsync(connection, tx, payload.Types, ct), "UpsertFootwearTypesDimBulkAsync");
        if (payload.Movements.Count > 0)
            await RunStepAsync(tx => UpsertInventoryMovementsBulkAsync(connection, tx, payload.Movements, ct), "UpsertInventoryMovementsBulkAsync");

        var productIdSet = payload.Products.Select(x => x.ProductId).ToHashSet();
        result.ProductsDimInserted += productIdSet.Count(x => !payload.ExistingProductIds.Contains(x));
        result.ProductsDimUpdated += productIdSet.Count(x => payload.ExistingProductIds.Contains(x));

        var saleIdSet = payload.SalesFacts.Select(x => x.SaleId).ToHashSet();
        result.SalesFactsInserted += saleIdSet.Count(x => !payload.ExistingSaleIds.Contains(x));
        result.SalesFactsUpdated += saleIdSet.Count(x => payload.ExistingSaleIds.Contains(x));
        result.SalesLineFactsInserted = payload.SalesLineFacts.Count;

        var storeIdSet = payload.Stores.Select(x => x.StoreId).ToHashSet();
        result.StoresInserted += storeIdSet.Count(x => !payload.ExistingStoreIds.Contains(x));
        result.StoresUpdated += storeIdSet.Count(x => payload.ExistingStoreIds.Contains(x));
    }

    private async Task UpsertProductsDimBulkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<ProductsDimSyncRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        const string createTempSql = """
            CREATE TEMP TABLE temp_products_dim (
                "ProductId" integer NOT NULL,
                "PLU" text NULL,
                "ProductName" text NOT NULL,
                "Category" text NOT NULL,
                "SubCategory" text NOT NULL,
                "Brand" text NOT NULL,
                "Velicina" text NULL,
                "Boja" text NULL,
                "Materijal" text NULL,
                "FootwearTypeId" integer NULL,
                "SupplierId" integer NULL,
                "SeasonId" integer NULL,
                "PurchasePrice" numeric(18,2) NULL,
                "PurchasePriceRsd" numeric(18,2) NULL,
                "FirstSalePrice" numeric(18,2) NULL,
                "SalePrice" numeric(18,2) NULL,
                "IsActive" boolean NOT NULL,
                "Timestamp" timestamp with time zone NOT NULL,
                "Kolicina" integer NULL,
                "MinimalnaKolicina" integer NULL,
                "DataOrigin" text NOT NULL
            ) ON COMMIT DROP;
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, createTempSql, ct, _logger);

        using (var importer = connection.BeginBinaryImport("""
            COPY temp_products_dim (
                "ProductId","PLU","ProductName","Category","SubCategory","Brand","Velicina","Boja","Materijal",
                "FootwearTypeId","SupplierId","SeasonId","PurchasePrice","PurchasePriceRsd","FirstSalePrice","SalePrice",
                "IsActive","Timestamp","Kolicina","MinimalnaKolicina","DataOrigin")
            FROM STDIN (FORMAT BINARY)
            """))
        {
            foreach (var row in rows)
            {
                importer.StartRow();
                importer.Write(row.ProductId, NpgsqlDbType.Integer);
                WriteNullableString(importer, row.Plu);
                importer.Write(row.ProductName, NpgsqlDbType.Text);
                importer.Write(row.Category, NpgsqlDbType.Text);
                importer.Write(row.SubCategory, NpgsqlDbType.Text);
                importer.Write(row.Brand, NpgsqlDbType.Text);
                WriteNullableString(importer, row.Velicina);
                WriteNullableString(importer, row.Boja);
                WriteNullableString(importer, row.Materijal);
                WriteNullableInt(importer, row.FootwearTypeId);
                WriteNullableInt(importer, row.SupplierId);
                WriteNullableInt(importer, row.SeasonId);
                WriteNullableDecimal(importer, row.PurchasePrice);
                WriteNullableDecimal(importer, row.PurchasePriceRsd);
                WriteNullableDecimal(importer, row.FirstSalePrice);
                WriteNullableDecimal(importer, row.SalePrice);
                importer.Write(row.IsActive, NpgsqlDbType.Boolean);
                importer.Write(row.Timestamp, NpgsqlDbType.TimestampTz);
                WriteNullableInt(importer, row.Kolicina);
                WriteNullableInt(importer, row.MinimalnaKolicina);
                importer.Write(row.DataOrigin, NpgsqlDbType.Text);
            }

            importer.Complete();
        }

        const string mergeSql = """
            WITH source_rows AS (
                SELECT DISTINCT ON ("ProductId")
                    "ProductId","PLU","ProductName","Category","SubCategory","Brand","Velicina","Boja","Materijal",
                    "FootwearTypeId","SupplierId","SeasonId","PurchasePrice","PurchasePriceRsd","FirstSalePrice","SalePrice",
                    "IsActive","Timestamp","Kolicina","MinimalnaKolicina","DataOrigin"
                FROM temp_products_dim
                ORDER BY "ProductId", "Timestamp" DESC
            )
            INSERT INTO "ProductsDim" (
                "ProductId","PLU","ProductName","Category","SubCategory","Brand","Velicina","Boja","Materijal",
                "FootwearTypeId","SupplierId","SeasonId","PurchasePrice","PurchasePriceRsd","FirstSalePrice","SalePrice",
                "IsActive","Timestamp","Kolicina","MinimalnaKolicina","DataOrigin")
            SELECT
                "ProductId","PLU","ProductName","Category","SubCategory","Brand","Velicina","Boja","Materijal",
                "FootwearTypeId","SupplierId","SeasonId","PurchasePrice","PurchasePriceRsd","FirstSalePrice","SalePrice",
                "IsActive","Timestamp","Kolicina","MinimalnaKolicina","DataOrigin"
            FROM source_rows
            ON CONFLICT ("ProductId") DO UPDATE
            SET
                "PLU" = EXCLUDED."PLU",
                "ProductName" = EXCLUDED."ProductName",
                "Category" = EXCLUDED."Category",
                "SubCategory" = EXCLUDED."SubCategory",
                "Brand" = EXCLUDED."Brand",
                "Velicina" = EXCLUDED."Velicina",
                "Boja" = EXCLUDED."Boja",
                "Materijal" = EXCLUDED."Materijal",
                "FootwearTypeId" = EXCLUDED."FootwearTypeId",
                "SupplierId" = EXCLUDED."SupplierId",
                "SeasonId" = EXCLUDED."SeasonId",
                "PurchasePrice" = EXCLUDED."PurchasePrice",
                "PurchasePriceRsd" = EXCLUDED."PurchasePriceRsd",
                "FirstSalePrice" = EXCLUDED."FirstSalePrice",
                "SalePrice" = EXCLUDED."SalePrice",
                "IsActive" = EXCLUDED."IsActive",
                "Timestamp" = EXCLUDED."Timestamp",
                "Kolicina" = EXCLUDED."Kolicina",
                "MinimalnaKolicina" = EXCLUDED."MinimalnaKolicina",
                "DataOrigin" = EXCLUDED."DataOrigin";
            """;
        const string mergeSqlLegacy = """
            WITH source_rows AS (
                SELECT DISTINCT ON ("ProductId")
                    "ProductId","PLU","ProductName","Category","SubCategory","Brand","Velicina","Boja","Materijal",
                    "FootwearTypeId","SupplierId","SeasonId","PurchasePrice","PurchasePriceRsd","FirstSalePrice","SalePrice",
                    "IsActive","Timestamp","Kolicina","MinimalnaKolicina","DataOrigin"
                FROM temp_products_dim
                ORDER BY "ProductId", "Timestamp" DESC
            ),
            updated AS (
                UPDATE "ProductsDim" AS pd
                SET
                    "PLU" = src."PLU",
                    "ProductName" = src."ProductName",
                    "Category" = src."Category",
                    "SubCategory" = src."SubCategory",
                    "Brand" = src."Brand",
                    "Velicina" = src."Velicina",
                    "Boja" = src."Boja",
                    "Materijal" = src."Materijal",
                    "FootwearTypeId" = src."FootwearTypeId",
                    "SupplierId" = src."SupplierId",
                    "SeasonId" = src."SeasonId",
                    "PurchasePrice" = src."PurchasePrice",
                    "PurchasePriceRsd" = src."PurchasePriceRsd",
                    "FirstSalePrice" = src."FirstSalePrice",
                    "SalePrice" = src."SalePrice",
                    "IsActive" = src."IsActive",
                    "Timestamp" = src."Timestamp",
                    "Kolicina" = src."Kolicina",
                    "MinimalnaKolicina" = src."MinimalnaKolicina",
                    "DataOrigin" = src."DataOrigin"
                FROM source_rows AS src
                WHERE pd."ProductId" = src."ProductId"
                RETURNING pd."ProductId"
            )
            INSERT INTO "ProductsDim" (
                "ProductId","PLU","ProductName","Category","SubCategory","Brand","Velicina","Boja","Materijal",
                "FootwearTypeId","SupplierId","SeasonId","PurchasePrice","PurchasePriceRsd","FirstSalePrice","SalePrice",
                "IsActive","Timestamp","Kolicina","MinimalnaKolicina","DataOrigin")
            SELECT
                src."ProductId",src."PLU",src."ProductName",src."Category",src."SubCategory",src."Brand",src."Velicina",src."Boja",src."Materijal",
                src."FootwearTypeId",src."SupplierId",src."SeasonId",src."PurchasePrice",src."PurchasePriceRsd",src."FirstSalePrice",src."SalePrice",
                src."IsActive",src."Timestamp",src."Kolicina",src."MinimalnaKolicina",src."DataOrigin"
            FROM source_rows AS src
            WHERE NOT EXISTS (
                SELECT 1
                FROM "ProductsDim" AS pd
                WHERE pd."ProductId" = src."ProductId");
            """;
        if (await HasProductsDimProductIdUniqueIndexAsync(connection, transaction, ct))
        {
            await ExecuteAnalyticsNonQueryAsync(connection, transaction, mergeSql, ct, _logger);
        }
        else
        {
            _logger.LogWarning(
                "ProductsDim upsert fell back to legacy merge because the analytics database lacks a unique constraint on ProductId.");
            await ExecuteAnalyticsNonQueryAsync(connection, transaction, mergeSqlLegacy, ct, _logger);
        }
    }

    private async Task UpsertStoresDimBulkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<StoreDimSyncRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        const string createTempSql = """
            CREATE TEMP TABLE temp_stores_dim (
                "StoreId" integer NOT NULL,
                "StoreName" text NOT NULL,
                "City" text NULL,
                "Region" text NOT NULL,
                "Telefon" text NULL,
                "Menedzer" text NULL,
                "DataOrigin" text NOT NULL
            ) ON COMMIT DROP;
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, createTempSql, ct, _logger);

        using (var importer = connection.BeginBinaryImport("""
            COPY temp_stores_dim ("StoreId","StoreName","City","Region","Telefon","Menedzer","DataOrigin")
            FROM STDIN (FORMAT BINARY)
            """))
        {
            foreach (var row in rows)
            {
                importer.StartRow();
                importer.Write(row.StoreId, NpgsqlDbType.Integer);
                importer.Write(row.StoreName, NpgsqlDbType.Text);
                WriteNullableString(importer, row.City);
                importer.Write(row.Region, NpgsqlDbType.Text);
                WriteNullableString(importer, row.Telefon);
                WriteNullableString(importer, row.Menedzer);
                importer.Write(row.DataOrigin, NpgsqlDbType.Text);
            }

            importer.Complete();
        }

        const string mergeSql = """
            INSERT INTO "StoresDim" ("StoreId","StoreName","City","Region","Telefon","Menedzer","DataOrigin")
            SELECT "StoreId","StoreName","City","Region","Telefon","Menedzer","DataOrigin"
            FROM temp_stores_dim
            ON CONFLICT ("StoreId") DO UPDATE
            SET
                "StoreName" = EXCLUDED."StoreName",
                "City" = EXCLUDED."City",
                "Region" = EXCLUDED."Region",
                "Telefon" = EXCLUDED."Telefon",
                "Menedzer" = EXCLUDED."Menedzer",
                "DataOrigin" = EXCLUDED."DataOrigin";
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, mergeSql, ct, _logger);
    }

    private async Task UpsertSalesFactsBulkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<SalesFactSyncRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        const string createTempSql = """
            CREATE TEMP TABLE temp_sales_facts (
                "SaleId" integer NOT NULL,
                "BrojRacuna" text NOT NULL,
                "SaleTimestampUtc" timestamp with time zone NOT NULL,
                "StoreId" integer NOT NULL,
                "PaymentType" text NOT NULL,
                "TotalAmount" numeric(18,2) NOT NULL,
                "TotalUnits" integer NOT NULL,
                "TotalLines" integer NOT NULL,
                "DataOrigin" text NOT NULL
            ) ON COMMIT DROP;
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, createTempSql, ct, _logger);

        using (var importer = connection.BeginBinaryImport("""
            COPY temp_sales_facts (
                "SaleId","BrojRacuna","SaleTimestampUtc","StoreId","PaymentType","TotalAmount","TotalUnits","TotalLines","DataOrigin")
            FROM STDIN (FORMAT BINARY)
            """))
        {
            foreach (var row in rows)
            {
                importer.StartRow();
                importer.Write(row.SaleId, NpgsqlDbType.Integer);
                importer.Write(row.BrojRacuna, NpgsqlDbType.Text);
                importer.Write(row.SaleTimestampUtc, NpgsqlDbType.TimestampTz);
                importer.Write(row.StoreId, NpgsqlDbType.Integer);
                importer.Write(row.PaymentType, NpgsqlDbType.Text);
                importer.Write(row.TotalAmount, NpgsqlDbType.Numeric);
                importer.Write(row.TotalUnits, NpgsqlDbType.Integer);
                importer.Write(row.TotalLines, NpgsqlDbType.Integer);
                importer.Write(row.DataOrigin, NpgsqlDbType.Text);
            }

            importer.Complete();
        }

        const string mergeSql = """
            INSERT INTO "SalesFacts" (
                "SaleId","BrojRacuna","SaleTimestampUtc","StoreId","PaymentType","TotalAmount","TotalUnits","TotalLines","DataOrigin")
            SELECT
                "SaleId","BrojRacuna","SaleTimestampUtc","StoreId","PaymentType","TotalAmount","TotalUnits","TotalLines","DataOrigin"
            FROM temp_sales_facts
            ON CONFLICT ("SaleId") DO UPDATE
            SET
                "BrojRacuna" = EXCLUDED."BrojRacuna",
                "SaleTimestampUtc" = EXCLUDED."SaleTimestampUtc",
                "StoreId" = EXCLUDED."StoreId",
                "PaymentType" = EXCLUDED."PaymentType",
                "TotalAmount" = EXCLUDED."TotalAmount",
                "TotalUnits" = EXCLUDED."TotalUnits",
                "TotalLines" = EXCLUDED."TotalLines",
                "DataOrigin" = EXCLUDED."DataOrigin";
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, mergeSql, ct, _logger);
    }

    private async Task ReplaceSalesLineFactsBulkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyCollection<int> saleIds,
        IReadOnlyList<SalesLineFactSyncRow> rows,
        CancellationToken ct)
    {
        if (saleIds.Count == 0)
            return;

        await using (var deleteCmd = new NpgsqlCommand(
                         """DELETE FROM "SalesLineFacts" WHERE "SaleId" = ANY(@saleIds);""",
                         connection,
                         transaction))
        {
            deleteCmd.Parameters.Add("saleIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = saleIds.ToArray();
            await deleteCmd.ExecuteNonQueryAsync(ct);
        }

        if (rows.Count == 0)
            return;

        const string createTempSql = """
            CREATE TEMP TABLE temp_sales_line_facts (
                "SaleId" integer NOT NULL,
                "ProductId" integer NOT NULL,
                "Qty" integer NOT NULL,
                "UnitPrice" numeric(18,2) NOT NULL,
                "LineTotal" numeric(18,2) NOT NULL,
                "NabavnaCena" numeric(18,2) NULL,
                "DataOrigin" text NOT NULL
            ) ON COMMIT DROP;
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, createTempSql, ct, _logger);

        using (var importer = connection.BeginBinaryImport("""
            COPY temp_sales_line_facts ("SaleId","ProductId","Qty","UnitPrice","LineTotal","NabavnaCena","DataOrigin")
            FROM STDIN (FORMAT BINARY)
            """))
        {
            foreach (var row in rows)
            {
                importer.StartRow();
                importer.Write(row.SaleId, NpgsqlDbType.Integer);
                importer.Write(row.ProductId, NpgsqlDbType.Integer);
                importer.Write(row.Qty, NpgsqlDbType.Integer);
                importer.Write(row.UnitPrice, NpgsqlDbType.Numeric);
                importer.Write(row.LineTotal, NpgsqlDbType.Numeric);
                WriteNullableDecimal(importer, row.NabavnaCena);
                importer.Write(row.DataOrigin, NpgsqlDbType.Text);
            }

            importer.Complete();
        }

        const string insertSql = """
            INSERT INTO "SalesLineFacts" ("SaleId","ProductId","Qty","UnitPrice","LineTotal","NabavnaCena","DataOrigin")
            SELECT "SaleId","ProductId","Qty","UnitPrice","LineTotal","NabavnaCena","DataOrigin"
            FROM temp_sales_line_facts;
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, insertSql, ct, _logger);
    }

    private async Task UpsertSuppliersDimBulkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<SupplierDimSyncRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        const string createTempSql = """
            CREATE TEMP TABLE temp_suppliers_dim (
                "SupplierId" integer NOT NULL,
                "Naziv" text NOT NULL,
                "Adresa" text NULL,
                "Telefon" text NULL,
                "Napomena" text NULL,
                "DataOrigin" text NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL
            ) ON COMMIT DROP;
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, createTempSql, ct, _logger);

        using (var importer = connection.BeginBinaryImport("""
            COPY temp_suppliers_dim ("SupplierId","Naziv","Adresa","Telefon","Napomena","DataOrigin","UpdatedAt")
            FROM STDIN (FORMAT BINARY)
            """))
        {
            foreach (var row in rows)
            {
                importer.StartRow();
                importer.Write(row.SupplierId, NpgsqlDbType.Integer);
                importer.Write(row.Naziv, NpgsqlDbType.Text);
                WriteNullableString(importer, row.Adresa);
                WriteNullableString(importer, row.Telefon);
                WriteNullableString(importer, row.Napomena);
                importer.Write(row.DataOrigin, NpgsqlDbType.Text);
                importer.Write(row.UpdatedAt, NpgsqlDbType.TimestampTz);
            }

            importer.Complete();
        }

        const string mergeSql = """
            INSERT INTO "SuppliersDim" ("SupplierId","Naziv","Adresa","Telefon","Napomena","DataOrigin","UpdatedAt")
            SELECT "SupplierId","Naziv","Adresa","Telefon","Napomena","DataOrigin","UpdatedAt"
            FROM temp_suppliers_dim
            ON CONFLICT ("SupplierId") DO UPDATE
            SET
                "Naziv" = EXCLUDED."Naziv",
                "Adresa" = EXCLUDED."Adresa",
                "Telefon" = EXCLUDED."Telefon",
                "Napomena" = EXCLUDED."Napomena",
                "DataOrigin" = EXCLUDED."DataOrigin",
                "UpdatedAt" = EXCLUDED."UpdatedAt";
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, mergeSql, ct, _logger);
    }

    private async Task UpsertSeasonsDimBulkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<SeasonDimSyncRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        const string createTempSql = """
            CREATE TEMP TABLE temp_seasons_dim (
                "SeasonId" integer NOT NULL,
                "Naziv" text NOT NULL,
                "DatumOd" timestamp with time zone NOT NULL,
                "DatumDo" timestamp with time zone NOT NULL,
                "DataOrigin" text NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL
            ) ON COMMIT DROP;
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, createTempSql, ct, _logger);

        using (var importer = connection.BeginBinaryImport("""
            COPY temp_seasons_dim ("SeasonId","Naziv","DatumOd","DatumDo","DataOrigin","UpdatedAt")
            FROM STDIN (FORMAT BINARY)
            """))
        {
            foreach (var row in rows)
            {
                importer.StartRow();
                importer.Write(row.SeasonId, NpgsqlDbType.Integer);
                importer.Write(row.Naziv, NpgsqlDbType.Text);
                importer.Write(row.DatumOd, NpgsqlDbType.TimestampTz);
                importer.Write(row.DatumDo, NpgsqlDbType.TimestampTz);
                importer.Write(row.DataOrigin, NpgsqlDbType.Text);
                importer.Write(row.UpdatedAt, NpgsqlDbType.TimestampTz);
            }

            importer.Complete();
        }

        const string mergeSql = """
            INSERT INTO "SeasonsDim" ("SeasonId","Naziv","DatumOd","DatumDo","DataOrigin","UpdatedAt")
            SELECT "SeasonId","Naziv","DatumOd","DatumDo","DataOrigin","UpdatedAt"
            FROM temp_seasons_dim
            ON CONFLICT ("SeasonId") DO UPDATE
            SET
                "Naziv" = EXCLUDED."Naziv",
                "DatumOd" = EXCLUDED."DatumOd",
                "DatumDo" = EXCLUDED."DatumDo",
                "DataOrigin" = EXCLUDED."DataOrigin",
                "UpdatedAt" = EXCLUDED."UpdatedAt";
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, mergeSql, ct, _logger);
    }

    private async Task UpsertFootwearTypesDimBulkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<FootwearTypeDimSyncRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        const string createTempSql = """
            CREATE TEMP TABLE temp_footwear_types_dim (
                "TypeId" integer NOT NULL,
                "Naziv" text NOT NULL,
                "DataOrigin" text NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL
            ) ON COMMIT DROP;
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, createTempSql, ct, _logger);

        using (var importer = connection.BeginBinaryImport("""
            COPY temp_footwear_types_dim ("TypeId","Naziv","DataOrigin","UpdatedAt")
            FROM STDIN (FORMAT BINARY)
            """))
        {
            foreach (var row in rows)
            {
                importer.StartRow();
                importer.Write(row.TypeId, NpgsqlDbType.Integer);
                importer.Write(row.Naziv, NpgsqlDbType.Text);
                importer.Write(row.DataOrigin, NpgsqlDbType.Text);
                importer.Write(row.UpdatedAt, NpgsqlDbType.TimestampTz);
            }

            importer.Complete();
        }

        const string mergeSql = """
            INSERT INTO "FootwearTypesDim" ("TypeId","Naziv","DataOrigin","UpdatedAt")
            SELECT "TypeId","Naziv","DataOrigin","UpdatedAt"
            FROM temp_footwear_types_dim
            ON CONFLICT ("TypeId") DO UPDATE
            SET
                "Naziv" = EXCLUDED."Naziv",
                "DataOrigin" = EXCLUDED."DataOrigin",
                "UpdatedAt" = EXCLUDED."UpdatedAt";
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, mergeSql, ct, _logger);
    }

    private async Task UpsertInventoryMovementsBulkAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<InventoryMovementSyncRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
            return;

        const string createTempSql = """
            CREATE TEMP TABLE temp_inventory_movement_facts (
                "SourceId" integer NOT NULL,
                "TipPromene" text NOT NULL,
                "Datum" timestamp with time zone NOT NULL,
                "ArtikalId" integer NULL,
                "Kolicina" integer NULL,
                "StaraProdajnaCena" numeric(18,2) NULL,
                "NovaProdajnaCena" numeric(18,2) NULL,
                "Iznos" numeric(18,2) NOT NULL,
                "StoreId" integer NULL,
                "DobavljacId" integer NULL,
                "BrojDokumenta" text NULL,
                "KorisnikIme" text NULL,
                "DataOrigin" text NOT NULL
            ) ON COMMIT DROP;
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, createTempSql, ct, _logger);

        using (var importer = connection.BeginBinaryImport("""
            COPY temp_inventory_movement_facts (
                "SourceId","TipPromene","Datum","ArtikalId","Kolicina","StaraProdajnaCena","NovaProdajnaCena",
                "Iznos","StoreId","DobavljacId","BrojDokumenta","KorisnikIme","DataOrigin")
            FROM STDIN (FORMAT BINARY)
            """))
        {
            foreach (var row in rows)
            {
                importer.StartRow();
                importer.Write(row.SourceId, NpgsqlDbType.Integer);
                importer.Write(row.TipPromene, NpgsqlDbType.Text);
                importer.Write(row.Datum, NpgsqlDbType.TimestampTz);
                WriteNullableInt(importer, row.ArtikalId);
                WriteNullableInt(importer, row.Kolicina);
                WriteNullableDecimal(importer, row.StaraProdajnaCena);
                WriteNullableDecimal(importer, row.NovaProdajnaCena);
                importer.Write(row.Iznos, NpgsqlDbType.Numeric);
                WriteNullableInt(importer, row.StoreId);
                WriteNullableInt(importer, row.DobavljacId);
                WriteNullableString(importer, row.BrojDokumenta);
                WriteNullableString(importer, row.KorisnikIme);
                importer.Write(row.DataOrigin, NpgsqlDbType.Text);
            }

            importer.Complete();
        }

        const string mergeSql = """
            INSERT INTO "InventoryMovementFacts" (
                "SourceId","TipPromene","Datum","ArtikalId","Kolicina","StaraProdajnaCena","NovaProdajnaCena",
                "Iznos","StoreId","DobavljacId","BrojDokumenta","KorisnikIme","DataOrigin")
            SELECT
                "SourceId","TipPromene","Datum","ArtikalId","Kolicina","StaraProdajnaCena","NovaProdajnaCena",
                "Iznos","StoreId","DobavljacId","BrojDokumenta","KorisnikIme","DataOrigin"
            FROM temp_inventory_movement_facts
            ON CONFLICT ("SourceId", "DataOrigin") DO UPDATE
            SET
                "TipPromene" = EXCLUDED."TipPromene",
                "Datum" = EXCLUDED."Datum",
                "ArtikalId" = EXCLUDED."ArtikalId",
                "Kolicina" = EXCLUDED."Kolicina",
                "StaraProdajnaCena" = EXCLUDED."StaraProdajnaCena",
                "NovaProdajnaCena" = EXCLUDED."NovaProdajnaCena",
                "Iznos" = EXCLUDED."Iznos",
                "StoreId" = EXCLUDED."StoreId",
                "DobavljacId" = EXCLUDED."DobavljacId",
                "BrojDokumenta" = EXCLUDED."BrojDokumenta",
                "KorisnikIme" = EXCLUDED."KorisnikIme";
            """;
        await ExecuteAnalyticsNonQueryAsync(connection, transaction, mergeSql, ct, _logger);
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

    private async Task EnsureDataImportBatchesTableIfEnabledAsync(CancellationToken ct)
    {
        if (!_options.EnableRuntimeBatchSchemaBootstrap || _batchSchemaBootstrapCompleted)
            return;

        await BatchSchemaBootstrapLock.WaitAsync(ct);
        try
        {
            if (_batchSchemaBootstrapCompleted)
                return;

            await EnsureDataImportBatchesTableAsync(ct);
            _batchSchemaBootstrapCompleted = true;
        }
        finally
        {
            BatchSchemaBootstrapLock.Release();
        }
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
                "SourceStorageKey" character varying(1024),
                "SourceStorageProvider" character varying(32),
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
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "SourceStorageKey" character varying(1024);
            ALTER TABLE IF EXISTS "DataImportBatches" ADD COLUMN IF NOT EXISTS "SourceStorageProvider" character varying(32);
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
        if (row is null || schema is null || schema.Columns.Count == 0)
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
    /// - If TABLE_TYPE column exists → must equal "TABLE"
    /// - If TABLE_TYPE column missing → assume it's a user table (fail-open)
    /// 
    /// INTERNAL: Exposed for testing.
    /// </summary>
    internal static bool CheckIsUserTable(DataRow row, DataTable schema)
    {
        if (schema is null || schema.Columns.Count == 0)
        {
            // Empty schema → assume it's a user table (fail-open)
            return true;
        }

        // Locate TABLE_TYPE column case-insensitively and use ordinal access; fail-open when missing
        var tableTypeCol = schema.Columns.Cast<DataColumn>()
            .FirstOrDefault(c => string.Equals(c.ColumnName, "TABLE_TYPE", StringComparison.OrdinalIgnoreCase));

        if (tableTypeCol is null)
        {
            // Missing column → assume it's a user table (fail-open, safe default)
            return true;
        }

        try
        {
            var value = row[tableTypeCol.Ordinal];
            if (value is null or DBNull)
                return true; // Null → assume user table

            var typeStr = value.ToString() ?? string.Empty;
            return string.Equals(typeStr, "TABLE", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CheckIsUserTable] Exception: {ex.GetType().Name}: {ex.Message}");
            // On any error → assume it's a user table
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
                        DatumProdaje = DT(row, "datumprodaje", "datum", "saledate") ?? DateTime.MinValue,
                        NacinPlacanja = S(row, "nacinplacanja", "paymenttype"),
                        IDObjekat = I(row, "idobjekat", "storeid") ?? 0,
                        KorisnikIme = S(row, "korisnikime", "korisnik", "username", "operater", "kasir"),
                        DataOrigin = "access"
                    };

                    if (e.DatumProdaje == DateTime.MinValue)
                        continue;

                    _trendDb.ProdajaZaglavlja.Add(e);
                    result.ProdajaInserted++;
                    insertedIds.Add(id.Value);
                    toInsert.Remove(id.Value);
                    TrackTrendWrite();
                    TrackAnalyticsSaleId(e.Id);
                    TrackAnalyticsStoreId(e.IDObjekat);

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
    private sealed record QueuedSourceMetadata(
        string? SourceFilePath,
        string? SourceStorageKey,
        string? SourceStorageProvider,
        bool UploadedToStorage);

    private static string NormalizeStorageProviderName(string? provider)
    {
        return string.IsNullOrWhiteSpace(provider)
            ? "local"
            : provider.Trim().ToLowerInvariant();
    }

    private string BuildAccessSourceStorageKey(string sourceFileName)
    {
        var extension = Path.GetExtension(sourceFileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".accdb";

        var safeName = Path.GetFileNameWithoutExtension(sourceFileName);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "access-source";

        return $"access-import/sources/{DateTime.UtcNow:yyyy/MM/dd}/{safeName}_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    }

    private async Task<QueuedSourceMetadata> PrepareQueuedSourceAsync(
        string accessFilePath,
        string sourceFileName,
        CancellationToken ct)
    {
        var fileSizeBytes = new FileInfo(accessFilePath).Length;
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Access import source preparation started. SourceFileName: {SourceFileName}. FileSizeBytes: {FileSizeBytes}. StorageProvider: {StorageProvider}.",
            sourceFileName,
            fileSizeBytes,
            _storageProviderName);

        if (_fileStorage is null || string.Equals(_storageProviderName, "local", StringComparison.Ordinal))
        {
            var workingCopy = await CreateBackgroundWorkingCopyAsync(accessFilePath, ct);
            stopwatch.Stop();
            _logger.LogInformation(
                "Access import source preparation completed with local staging. SourceFileName: {SourceFileName}. FileSizeBytes: {FileSizeBytes}. ElapsedMs: {ElapsedMs}.",
                sourceFileName,
                fileSizeBytes,
                stopwatch.ElapsedMilliseconds);
            return new QueuedSourceMetadata(
                SourceFilePath: workingCopy,
                SourceStorageKey: null,
                SourceStorageProvider: null,
                UploadedToStorage: false);
        }

        var storageKey = BuildAccessSourceStorageKey(sourceFileName);
        await using var source = new FileStream(
            accessFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        using var uploadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        uploadCts.CancelAfter(_storageUploadTimeout);

        try
        {
            await _fileStorage.UploadAsync(storageKey, source, uploadCts.Token);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested && uploadCts.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "Access import source upload timed out. SourceFileName: {SourceFileName}. FileSizeBytes: {FileSizeBytes}. StorageProvider: {StorageProvider}. ElapsedMs: {ElapsedMs}. TimeoutSeconds: {TimeoutSeconds}.",
                sourceFileName,
                fileSizeBytes,
                _storageProviderName,
                stopwatch.ElapsedMilliseconds,
                _storageUploadTimeout.TotalSeconds);
            throw new TimeoutException(
                $"Access import source upload timed out after {_storageUploadTimeout.TotalSeconds:0} seconds.",
                ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "Access import source preparation failed. SourceFileName: {SourceFileName}. FileSizeBytes: {FileSizeBytes}. StorageProvider: {StorageProvider}. ElapsedMs: {ElapsedMs}.",
                sourceFileName,
                fileSizeBytes,
                _storageProviderName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "Access import source uploaded to durable storage. SourceFileName: {SourceFileName}. StorageProvider: {StorageProvider}. StorageKey: {StorageKey}. FileSizeBytes: {FileSizeBytes}. ElapsedMs: {ElapsedMs}.",
            sourceFileName,
            _storageProviderName,
            storageKey,
            fileSizeBytes,
            stopwatch.ElapsedMilliseconds);

        return new QueuedSourceMetadata(
            SourceFilePath: null,
            SourceStorageKey: storageKey,
            SourceStorageProvider: _storageProviderName,
            UploadedToStorage: true);
    }

    private async Task DeleteQueuedStorageSourceBestEffortAsync(string? storageKey)
    {
        if (_fileStorage is null || string.IsNullOrWhiteSpace(storageKey))
            return;

        try
        {
            await _fileStorage.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (Exception cleanupEx)
        {
            _logger.LogWarning(
                cleanupEx,
                "Failed to clean up storage source after batch-create failure. StorageProvider: {StorageProvider}. StorageKey: {StorageKey}.",
                _storageProviderName,
                storageKey);
        }
    }

    private async Task<string> CreateBackgroundWorkingCopyAsync(string accessFilePath, CancellationToken ct)
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

        await using var source = new FileStream(
            accessFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        await using var destination = new FileStream(
            tmpPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await source.CopyToAsync(destination, ct);
        await destination.FlushAsync(ct);
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

    private static void UpdateBatchDurationSeconds(DataImportBatch batch, DateTime completedAtUtc)
        => batch.DurationSeconds = (int)Math.Max(0, Math.Round((completedAtUtc - batch.StartedAtUtc).TotalSeconds));

    private static void ApplyBatchMetricsFromResult(DataImportBatch batch, AccessImportRunResponse result, int minTotalErrors = 0)
    {
        batch.RowsRead = CountSourceRows(result);
        batch.RowsAccepted = CountAcceptedRows(result);
        batch.RowsWritten = CountImportedRows(result) + CountUpdatedRows(result);
        batch.TotalImported = CountImportedRows(result);
        batch.TotalUpdated = CountUpdatedRows(result);
        batch.TotalErrors = Math.Max(minTotalErrors, result.Warnings.Count);
        batch.ProcessedRowCount = batch.RowsWritten;
        batch.SkippedRowCount = Math.Max(0, batch.RowsRead - batch.RowsAccepted);
        batch.RowsInserted = batch.TotalImported;
        batch.RowsUpdated = batch.TotalUpdated;
        batch.RowsUnchanged = Math.Max(0, batch.RowsAccepted - batch.RowsWritten);
        batch.RowsSkippedStale = result.RowsSkippedStale;
    }

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

        var activeBatchSize = GetActiveTrendWriteBatchSize();
        if (!force)
        {
            var batchThresholdReached = _pendingTrendWrites >= activeBatchSize;
            var timeThresholdReached = DateTime.UtcNow - _lastTrendFlushUtc >= GetTrendFlushInterval();
            if (!batchThresholdReached && !timeThresholdReached)
                return;
        }

        if (_activeBatchId.HasValue)
            await EnsureBatchNotCancelledAsync(_activeBatchId.Value, ct);

        var writesToFlush = _pendingTrendWrites;
        _trendDb.ChangeTracker.DetectChanges();
        await GuardProdajaStavkeForeignKeysBeforeFlushAsync(ct);
        await _trendDb.SaveChangesAsync(ct);
        _trendDb.ChangeTracker.Clear();
        _pendingTrendWrites = 0;
        _lastTrendFlushUtc = DateTime.UtcNow;
        Interlocked.Increment(ref _trendFlushCount);
        _logger.LogInformation(
            "Access import DB flush completed. Step: {Step}. RowsWritten: {RowsWritten}. BatchSize: {BatchSize}. Force: {Force}.",
            "db-flush",
            writesToFlush,
            activeBatchSize,
            force);
    }

    private async Task GuardProdajaStavkeForeignKeysBeforeFlushAsync(CancellationToken ct)
    {
        var lineEntries = _trendDb.ChangeTracker
            .Entries<Domain.Model.Prodaja.ProdajaStavka>()
            .Where(e => (e.State == EntityState.Added || e.State == EntityState.Modified) && e.Entity.IdProdaja > 0)
            .ToList();

        if (lineEntries.Count == 0)
            return;

        var trackedNewParentIds = _trendDb.ChangeTracker
            .Entries<Domain.Model.Prodaja.ProdajaZaglavlje>()
            .Where(e => e.State == EntityState.Added && e.Entity.Id > 0)
            .Select(e => e.Entity.Id)
            .ToHashSet();

        var parentIdsToVerify = lineEntries
            .Select(e => e.Entity.IdProdaja)
            .Where(id => !trackedNewParentIds.Contains(id))
            .Distinct()
            .ToArray();

        if (parentIdsToVerify.Length == 0)
            return;

        var persistedParentIds = new HashSet<int>();
        foreach (var chunk in parentIdsToVerify.Chunk(500))
        {
            var ids = await _trendDb.ProdajaZaglavlja
                .AsNoTracking()
                .Where(x => chunk.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(ct);
            persistedParentIds.UnionWith(ids);
        }

        var missingParentIds = parentIdsToVerify
            .Where(id => !persistedParentIds.Contains(id))
            .ToHashSet();

        if (missingParentIds.Count == 0)
            return;

        var invalidEntries = lineEntries
            .Where(e => missingParentIds.Contains(e.Entity.IdProdaja))
            .ToList();

        var sample = string.Join(
            ", ",
            invalidEntries
                .Take(ForeignKeyWarningSampleLimit)
                .Select(e => $"stavka={e.Entity.Id}, id_prodaja={e.Entity.IdProdaja}"));

        var sampleMissingParentIds = string.Join(
            ", ",
            missingParentIds.Take(ForeignKeyWarningSampleLimit));

        if (!_options.SkipInvalidForeignKeys)
        {
            throw new InvalidOperationException(
                $"prodaja_stavke FK validation failed before flush: missing prodaja_zaglavlje parents count={missingParentIds.Count}; samples={sample}");
        }

        foreach (var entry in invalidEntries)
        {
            if (entry.State == EntityState.Added)
                entry.State = EntityState.Detached;
            else
                entry.State = EntityState.Unchanged;
        }

        var message =
            $"Access import skipped {invalidEntries.Count} pending prodaja_stavke rows before DB flush because parent prodaja_zaglavlje rows are missing. Samples: {sample}.";
        _logger.LogWarning(
            "Access import skipped pending prodaja_stavke rows with missing parents before DB flush. BatchId: {BatchId}. MissingParentCount: {MissingParentCount}. SkippedLineCount: {SkippedLineCount}. SampleMissingParentIds: {SampleMissingParentIds}. SkipInvalidForeignKeys: {SkipInvalidForeignKeys}.",
            _activeBatchId,
            missingParentIds.Count,
            invalidEntries.Count,
            sampleMissingParentIds,
            _options.SkipInvalidForeignKeys);

        var orphanRatioPct = lineEntries.Count > 0
            ? invalidEntries.Count * 100 / lineEntries.Count
            : 0;
        if (orphanRatioPct > 5)
        {
            _logger.LogError(
                "Access import high orphan rate in prodaja_stavke: {SkippedLineCount} of {TotalPendingStavke} pending rows skipped due to missing parents ({OrphanRatioPct}%). BatchId: {BatchId}. SampleMissingParentIds: {SampleMissingParentIds}. SkipInvalidForeignKeys: {SkipInvalidForeignKeys}.",
                invalidEntries.Count,
                lineEntries.Count,
                orphanRatioPct,
                _activeBatchId,
                sampleMissingParentIds,
                _options.SkipInvalidForeignKeys);
        }

        if (_activeBatchResult is not null)
            _activeBatchResult.Warnings.Add(message);

        if (_activeBatchId is long batchId)
        {
            AddAccessImportLogEntry(
                batchId,
                "prodaja_stavke",
                0,
                "warning",
                TrimToMaxLength(message, 2000));
        }
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

    private static object? GetNormalized(AccessDataRow row, string? normalizedAlias)
    {
        if (string.IsNullOrWhiteSpace(normalizedAlias))
            return null;

        return row.TryGetValueNormalized(normalizedAlias, out var value) ? value : null;
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

    private static string? SNormalized(AccessDataRow row, string? normalizedAlias)
    {
        var v = GetNormalized(row, normalizedAlias);
        var s = v is null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static int? I(AccessDataRow row, params string[] aliases) => ConvertToInt(Get(row, aliases));
    private static decimal? D(AccessDataRow row, params string[] aliases) => ConvertToDecimal(Get(row, aliases));
    private static DateTime? DT(AccessDataRow row, params string[] aliases) => ConvertToDate(Get(row, aliases));
    private static int? INormalized(AccessDataRow row, string? normalizedAlias) => ConvertToInt(GetNormalized(row, normalizedAlias));
    private static decimal? DNormalized(AccessDataRow row, string? normalizedAlias) => ConvertToDecimal(GetNormalized(row, normalizedAlias));

    internal static decimal? ResolveProdajaLineNabavnaCena(AccessDataRow row)
    {
        // Prefer explicit RSD field when available; fallback to legacy purchase-price aliases.
        var rsd = D(row, "nabavnacenadin", "purchasepricersd");
        if (rsd is > 0)
            return rsd.Value;

        var legacy = D(row, "nabavnacena", "purchaseprice", "cost", "nc");
        return legacy is > 0 ? legacy.Value : null;
    }

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
