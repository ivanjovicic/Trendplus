using System.Globalization;
using System.Text.Json;
using Api.Config;
using Api.Models;
using Api.Services.Access;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Api.Services;

public interface INivelacijaRepairService
{
    Task<NivelacijaRepairPreflightDto> RunPreflightAsync(string? explicitSourceFilePath = null, CancellationToken ct = default);
    Task<IReadOnlyList<NivelacijaRepairIssueDto>> ScanIssuesAsync(string? explicitSourceFilePath = null, CancellationToken ct = default);
    Task<NivelacijaRepairPlanDto> GenerateRepairPlanAsync(string? explicitSourceFilePath = null, int maxRowsToModify = 10_000, CancellationToken ct = default);
    Task<long> WriteDryRunAuditAsync(string requestedBy, NivelacijaRepairPlanDto plan, CancellationToken ct = default);
    Task<NivelacijaRepairExecutionResultDto> ExecuteRepairAsync(string? explicitSourceFilePath, string requestedBy, int maxRowsToModify = 10_000, CancellationToken ct = default);
}

public sealed class NivelacijaRepairService : INivelacijaRepairService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly string[] NivelacijeCandidates = ["nivelacije", "nivelacija", "priceupdate", "cenovneizmene", "tblnivelacije", "tblnivelacija", "nivelacijeartikala"];
    private static readonly string[] DnevnikPromenaCandidates = ["dnevnikpromjena", "dnevnikpromena", "dnevnik_promjena", "dnevnik_promena", "dnevnik", "log", "promena", "promjena", "events", "journal", "tbldnevnikpromena", "tbldnevnikpromjena", "tbldnevnik"];
    private static readonly string[] ArtikliCandidates = ["artikli", "artikal", "proizvodi", "products", "tblartikal", "tblarticles", "sifarnik"];
    private static readonly string[] RootAccessFileCandidates = ["TRENDPLUS.accdb", "TRENDPLUS.mdb", "trendplus.accdb", "trendplus.mdb", "Trend plus.mdb", "Trend plus.accdb"];

    private const int DefaultMaxRowsThreshold = 10_000;
    private const int AbsoluteMaxRowsThreshold = 50_000;

    private readonly TrendplusDbContext _db;
    private readonly AccessImportOptions _accessOptions;
    private readonly ILogger<NivelacijaRepairService> _logger;

    public NivelacijaRepairService(
        TrendplusDbContext db,
        IOptions<AccessImportOptions> accessOptions,
        ILogger<NivelacijaRepairService> logger)
    {
        _db = db;
        _accessOptions = accessOptions.Value;
        _logger = logger;
    }

    public async Task<NivelacijaRepairPreflightDto> RunPreflightAsync(string? explicitSourceFilePath = null, CancellationToken ct = default)
    {
        var sourceFilePath = await ResolveSourceFilePathAsync(explicitSourceFilePath, ct);
        await using var sessionHandle = await OpenAccessSessionAsync(sourceFilePath, ct);
        var requiredObjects = await CheckRequiredDatabaseObjectsAsync(ct);
        var accessTables = await ResolveAccessTablesAsync(sessionHandle.Session, ct);

        return new NivelacijaRepairPreflightDto
        {
            ResolvedSourceFilePath = sourceFilePath,
            DatabaseReachable = requiredObjects.Values.All(static value => value),
            DefaultMaxRowsThreshold = DefaultMaxRowsThreshold,
            RequiredObjects = requiredObjects,
            AccessTables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["nivelacije"] = accessTables.NivelacijeTable ?? string.Empty,
                ["dnevnikPromena"] = accessTables.DnevnikTable ?? string.Empty,
                ["artikli"] = accessTables.ArtikliTable ?? string.Empty,
            },
            Warnings = accessTables.Warnings,
        };
    }

    public async Task<IReadOnlyList<NivelacijaRepairIssueDto>> ScanIssuesAsync(string? explicitSourceFilePath = null, CancellationToken ct = default)
    {
        var analysis = await AnalyzeAsync(explicitSourceFilePath, DefaultMaxRowsThreshold, ct);
        return analysis.Plan.DetectedIssues;
    }

    public async Task<NivelacijaRepairPlanDto> GenerateRepairPlanAsync(string? explicitSourceFilePath = null, int maxRowsToModify = 10_000, CancellationToken ct = default)
    {
        var analysis = await AnalyzeAsync(explicitSourceFilePath, maxRowsToModify, ct);
        return analysis.Plan;
    }

    public async Task<long> WriteDryRunAuditAsync(string requestedBy, NivelacijaRepairPlanDto plan, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await EnsureAuditSchemaAsync(connection, transaction: null, ct);
        var summaryJson = BuildAuditSummaryJson(
            dryRun: true,
            plan,
            fixedRows: 0,
            skippedRows: plan.ProposedFixes.Count,
            remainingIssues: plan.DetectedIssues.Count,
            verification: plan.Verification);

        return await InsertAuditRecordAsync(
            connection,
            transaction: null,
            requestedBy,
            dryRun: true,
            detectedIssues: plan.DetectedIssues.Count,
            fixedRows: 0,
            summaryJson,
            ct);
    }

    public async Task<NivelacijaRepairExecutionResultDto> ExecuteRepairAsync(string? explicitSourceFilePath, string requestedBy, int maxRowsToModify = 10_000, CancellationToken ct = default)
    {
        var analysis = await AnalyzeAsync(explicitSourceFilePath, maxRowsToModify, ct);
        var plan = analysis.Plan;

        if (!plan.EstimatedImpact.CanExecute)
        {
            throw new InvalidOperationException(
                $"Repair plan exceeds the configured safety threshold ({plan.EstimatedImpact.ProposedFixesCount}/{plan.EstimatedImpact.MaxRowsThreshold}).");
        }

        await using var connection = await OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await EnsureAuditSchemaAsync(connection, transaction, ct);

        var fixedRows = 0;
        var skippedRows = 0;
        var remainingIssues = 0;
        NivelacijaRepairVerificationDto verificationAfter;

        if (plan.ProposedFixes.Count > 0)
        {
            await CreateTempRepairTableAsync(connection, transaction, ct);
            await BulkLoadRepairFixesAsync(connection, transaction, plan.ProposedFixes, ct);
            fixedRows = await ExecuteRepairUpdateAsync(connection, transaction, ct);
            skippedRows = Math.Max(0, plan.ProposedFixes.Count - fixedRows);
            remainingIssues = await CountRemainingTempMismatchesAsync(connection, transaction, ct);
        }
        else
        {
            skippedRows = plan.DetectedIssues.Count;
        }

        verificationAfter = await CollectVerificationAsync(connection, transaction, analysis.AccessLineage, ct);
        var summaryJson = BuildAuditSummaryJson(
            dryRun: false,
            plan,
            fixedRows,
            skippedRows,
            remainingIssues,
            verificationAfter);

        var auditId = await InsertAuditRecordAsync(
            connection,
            transaction,
            requestedBy,
            dryRun: false,
            detectedIssues: plan.DetectedIssues.Count,
            fixedRows,
            summaryJson,
            ct);

        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "Nivelacija repair completed. AuditId: {AuditId}. FixedRows: {FixedRows}. SkippedRows: {SkippedRows}. RemainingIssues: {RemainingIssues}.",
            auditId,
            fixedRows,
            skippedRows,
            remainingIssues);

        return new NivelacijaRepairExecutionResultDto
        {
            SourceFilePath = plan.SourceFilePath,
            AuditId = auditId,
            FixedRows = fixedRows,
            SkippedRows = skippedRows,
            RemainingIssuesAfterRepair = remainingIssues,
            EstimatedImpact = plan.EstimatedImpact,
            Verification = verificationAfter,
        };
    }

    private async Task<AnalysisResult> AnalyzeAsync(string? explicitSourceFilePath, int maxRowsToModify, CancellationToken ct)
    {
        var sourceFilePath = await ResolveSourceFilePathAsync(explicitSourceFilePath, ct);
        await using var sessionHandle = await OpenAccessSessionAsync(sourceFilePath, ct);
        var accessLineage = await BuildAccessLineageAsync(sessionHandle.Session, ct);

        await using var connection = await OpenConnectionAsync(ct);
        var requiredObjects = await CheckRequiredDatabaseObjectsAsync(connection, transaction: null, ct);
        var missingObjects = requiredObjects.Where(static item => !item.Value).Select(static item => item.Key).ToArray();
        if (missingObjects.Length > 0)
        {
            throw new InvalidOperationException($"Repair preflight failed because required objects are missing: {string.Join(", ", missingObjects)}.");
        }

        var candidateRows = await LoadCandidateRowsAsync(connection, transaction: null, ct);
        var issues = new List<NivelacijaRepairIssueDto>();
        var fixes = new List<NivelacijaRepairFixDto>();
        var updatedDateRows = 0;
        var updatedStoreRows = 0;
        var updatedVendorRows = 0;
        var missingSourceMappings = 0;

        foreach (var row in candidateRows)
        {
            if (!TryParseSourceHeaderId(row.SourceHeaderRaw, out var sourceHeaderId))
            {
                missingSourceMappings++;
                issues.Add(BuildMissingMappingIssue(row, detectedIssueType: "missing_source_header_id"));
                continue;
            }

            if (!accessLineage.LineageByKey.TryGetValue((row.ArticleId, sourceHeaderId), out var accessEntry))
            {
                missingSourceMappings++;
                issues.Add(BuildMissingMappingIssue(row, detectedIssueType: "missing_access_lineage", sourceHeaderId));
                continue;
            }

            var targetEventDate = accessEntry.EventDate ?? row.EventDate;
            var targetStoreId = accessEntry.StoreId ?? row.CurrentStoreId;
            var targetVendorId = accessEntry.VendorId ?? row.CurrentVendorId;

            var changedFields = new List<string>(capacity: 3);
            if (targetEventDate != row.EventDate)
            {
                updatedDateRows++;
                changedFields.Add("event_date");
            }

            if (targetStoreId != row.CurrentStoreId)
            {
                updatedStoreRows++;
                changedFields.Add("store");
            }

            if (targetVendorId != row.CurrentVendorId)
            {
                updatedVendorRows++;
                changedFields.Add("vendor");
            }

            if (changedFields.Count == 0)
                continue;

            fixes.Add(new NivelacijaRepairFixDto
            {
                PriceEventId = row.PriceEventId,
                ArticleId = row.ArticleId,
                SourceHeaderId = sourceHeaderId,
                CurrentEventDate = row.EventDate,
                TargetEventDate = targetEventDate,
                CurrentStoreId = row.CurrentStoreId,
                TargetStoreId = targetStoreId,
                CurrentVendorId = row.CurrentVendorId,
                TargetVendorId = targetVendorId,
                FieldsChanged = changedFields,
            });

            issues.Add(new NivelacijaRepairIssueDto
            {
                PriceEventId = row.PriceEventId,
                ArticleId = row.ArticleId,
                SourceHeaderId = sourceHeaderId,
                VendorId = row.CurrentVendorId,
                EventDate = row.EventDate,
                OldPrice = row.OldPrice,
                NewPrice = row.NewPrice,
                CalculatedPreSales = row.CalculatedPreSales,
                CalculatedPostSales = row.CalculatedPostSales,
                DetectedIssueType = BuildDetectedIssueType(changedFields),
                CurrentStoreId = row.CurrentStoreId,
                CurrentVendorId = row.CurrentVendorId,
                ProposedEventDate = targetEventDate,
                ProposedStoreId = targetStoreId,
                ProposedVendorId = targetVendorId,
                Fixable = true,
            });
        }

        var threshold = NormalizeThreshold(maxRowsToModify);
        var verification = await CollectVerificationAsync(connection, transaction: null, accessLineage, ct);
        var plan = new NivelacijaRepairPlanDto
        {
            SourceFilePath = sourceFilePath,
            GeneratedAtUtc = DateTime.UtcNow,
            DetectedIssues = issues,
            ProposedFixes = fixes,
            EstimatedImpact = new NivelacijaRepairEstimatedImpactDto
            {
                CandidateRowsScanned = candidateRows.Count,
                DetectedIssuesCount = issues.Count,
                ProposedFixesCount = fixes.Count,
                MissingSourceMappings = missingSourceMappings,
                UpdatedDateRows = updatedDateRows,
                UpdatedStoreRows = updatedStoreRows,
                UpdatedVendorRows = updatedVendorRows,
                MaxRowsThreshold = threshold,
                ExceedsThreshold = fixes.Count > threshold,
                CanExecute = fixes.Count <= threshold,
            },
            Verification = verification,
        };

        return new AnalysisResult(plan, accessLineage);
    }

    private async Task<Dictionary<string, bool>> CheckRequiredDatabaseObjectsAsync(CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        return await CheckRequiredDatabaseObjectsAsync(connection, transaction: null, ct);
    }

    private static async Task<Dictionary<string, bool>> CheckRequiredDatabaseObjectsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken ct)
    {
        const string sql = @"
            SELECT
                to_regclass('public.""DnevnikPromena""') IS NOT NULL AS dnevnik_promena,
                to_regclass('public.vw_sales_pre_nivelacija') IS NOT NULL AS vw_sales_pre_nivelacija,
                to_regclass('public.vw_sales_post_nivelacija') IS NOT NULL AS vw_sales_post_nivelacija,
                to_regclass('public.vw_vendor_sales_nivelacija') IS NOT NULL AS vw_vendor_sales_nivelacija,
                to_regclass('public.prodaja_zaglavlje') IS NOT NULL AS prodaja_zaglavlje,
                to_regclass('public.prodaja_stavke') IS NOT NULL AS prodaja_stavke;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["DnevnikPromena"] = reader.GetBoolean(reader.GetOrdinal("dnevnik_promena")),
            ["vw_sales_pre_nivelacija"] = reader.GetBoolean(reader.GetOrdinal("vw_sales_pre_nivelacija")),
            ["vw_sales_post_nivelacija"] = reader.GetBoolean(reader.GetOrdinal("vw_sales_post_nivelacija")),
            ["vw_vendor_sales_nivelacija"] = reader.GetBoolean(reader.GetOrdinal("vw_vendor_sales_nivelacija")),
            ["prodaja_zaglavlje"] = reader.GetBoolean(reader.GetOrdinal("prodaja_zaglavlje")),
            ["prodaja_stavke"] = reader.GetBoolean(reader.GetOrdinal("prodaja_stavke")),
        };
    }

    private async Task<AccessTablesResult> ResolveAccessTablesAsync(IAccessDataReaderSession session, CancellationToken ct)
    {
        var tables = await session.GetTablesAsync(includeTemporaryTables: true, ct);
        var nivelacijeTable = await FindTableAsync(session, tables, NivelacijeCandidates, sigRequired: ["idartikal", "iddnevnik", "novacena"], ct: ct);
        var dnevnikTable = await FindTableAsync(session, tables, DnevnikPromenaCandidates, sigRequired: ["iddnevnik", "datum"], ct: ct);
        var artikliTable = await FindTableAsync(session, tables, ArtikliCandidates, sigRequired: ["idartikal"], ct: ct, sigBonus: ["iddobavljac"]);

        var warnings = new List<string>();
        if (artikliTable is null)
            warnings.Add("Access artikli table was not found; vendor fallback will use Postgres Artikli.");

        if (nivelacijeTable is null || dnevnikTable is null)
        {
            throw new InvalidOperationException("Access source is missing tblNivelacije or tblDnevnikPromena equivalent, so the repair plan cannot be built.");
        }

        return new AccessTablesResult(nivelacijeTable, dnevnikTable, artikliTable, warnings);
    }

    private async Task<AccessLineageResult> BuildAccessLineageAsync(IAccessDataReaderSession session, CancellationToken ct)
    {
        var tables = await ResolveAccessTablesAsync(session, ct);
        var sourceSnapshots = await LoadSourceSnapshotsAsync(session, tables.DnevnikTable!, ct);
        var supplierByArticleId = await LoadSupplierLookupAsync(tables.ArtikliTable, session, ct);
        var lineages = new Dictionary<(int ArticleId, int SourceHeaderId), AccessLineageEntry>();
        var sourceHeaderIds = new HashSet<int>();
        var articleDateCounts = new Dictionary<(int ArticleId, DateOnly EventDate), int>();
        var totalLineRows = 0;

        await foreach (var row in session.ReadRowsAsync(tables.NivelacijeTable!, ct))
        {
            totalLineRows++;

            var articleId = ReadInt(row, "idartikal", "artikalid", "productid", "id_artikal");
            var sourceHeaderId = ReadInt(row, "iddnevnik", "id", "idlog");
            if (!articleId.HasValue || !sourceHeaderId.HasValue)
                continue;

            sourceHeaderIds.Add(sourceHeaderId.Value);
            sourceSnapshots.TryGetValue(sourceHeaderId.Value, out var sourceSnapshot);
            supplierByArticleId.TryGetValue(articleId.Value, out var articleSupplierId);

            var eventDate = ReadDateOnly(row, "datum", "datumnivelacije", "date") ?? sourceSnapshot?.EventDate;
            var storeId = ReadInt(row, "idobjekat", "storeid", "idobjekta") ?? sourceSnapshot?.StoreId;
            var vendorId = ReadInt(row, "iddobavljac", "dobavljacid", "supplierid") ?? sourceSnapshot?.VendorId ?? articleSupplierId;

            lineages[(articleId.Value, sourceHeaderId.Value)] = new AccessLineageEntry(eventDate, storeId, vendorId);

            if (eventDate.HasValue)
            {
                var groupingKey = (articleId.Value, eventDate.Value);
                articleDateCounts[groupingKey] = articleDateCounts.GetValueOrDefault(groupingKey) + 1;
            }
        }

        return new AccessLineageResult(
            lineages,
            totalLineRows,
            sourceHeaderIds.Count,
            articleDateCounts.Values.Count(static count => count > 1));
    }

    private async Task<Dictionary<int, AccessHeaderSnapshot>> LoadSourceSnapshotsAsync(
        IAccessDataReaderSession session,
        string dnevnikTable,
        CancellationToken ct)
    {
        var snapshots = new Dictionary<int, AccessHeaderSnapshot>();

        await foreach (var row in session.ReadRowsAsync(dnevnikTable, ct))
        {
            var sourceId = ReadInt(row, "id", "iddnevnik", "iddnevnikpromene", "iddnevnikpromena", "iddnevprom", "idlog", "logid", "seqno");
            var eventDate = ReadDateOnly(row, "datum", "datumizmene", "datumdokumenta", "datumprocene", "date", "eventdate", "datumpromena");
            if (!sourceId.HasValue || !eventDate.HasValue)
                continue;

            snapshots[sourceId.Value] = new AccessHeaderSnapshot(
                eventDate,
                ReadInt(row, "idobjekat", "storeid", "idobjekta", "objekatid", "idposlovnice", "prodavnicaid"),
                ReadInt(row, "iddobavljac", "dobavljacid", "supplierid", "idd", "iddob"));
        }

        return snapshots;
    }

    private async Task<Dictionary<int, int?>> LoadSupplierLookupAsync(string? artikliTable, IAccessDataReaderSession session, CancellationToken ct)
    {
        var supplierByArticleId = await LoadPostgresSupplierLookupAsync(ct);
        if (string.IsNullOrWhiteSpace(artikliTable))
            return supplierByArticleId;

        await foreach (var row in session.ReadRowsAsync(artikliTable, ct))
        {
            var articleId = ReadInt(row, "id", "idartikal", "productid");
            if (!articleId.HasValue)
                continue;

            supplierByArticleId[articleId.Value] = ReadInt(row, "iddobavljac", "dobavljacid", "supplierid");
        }

        return supplierByArticleId;
    }

    private async Task<Dictionary<int, int?>> LoadPostgresSupplierLookupAsync(CancellationToken ct)
    {
        return await _db.Artikli
            .AsNoTracking()
            .ToDictionaryAsync(article => article.Id, article => article.IDDobavljac, ct);
    }

    private static async Task<string?> FindTableAsync(
        IAccessDataReaderSession session,
        IReadOnlyList<string> tables,
        string[] candidates,
        string[]? sigRequired,
        CancellationToken ct,
        string[]? sigBonus = null)
    {
        var normalized = tables.Select(table => new { Original = table, Key = AccessImportService.Normalize(table) }).ToList();

        foreach (var candidate in candidates)
        {
            var key = AccessImportService.Normalize(candidate);
            var exact = normalized.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
            if (exact is not null)
                return exact.Original;
        }

        foreach (var candidate in candidates)
        {
            var key = AccessImportService.Normalize(candidate);
            var contains = normalized.FirstOrDefault(item => item.Key.Contains(key, StringComparison.Ordinal));
            if (contains is not null)
                return contains.Original;
        }

        if (sigRequired is null || sigRequired.Length == 0)
            return null;

        string? bestTable = null;
        var bestScore = -1;
        var requiredKeys = sigRequired.Select(AccessImportService.Normalize).ToArray();
        var bonusKeys = sigBonus?.Select(AccessImportService.Normalize).ToArray() ?? [];

        foreach (var table in tables)
        {
            var columns = await session.GetColumnsAsync(table, ct);
            var normalizedColumns = columns
                .Select(AccessImportService.Normalize)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);

            if (!requiredKeys.All(normalizedColumns.Contains))
                continue;

            var score = bonusKeys.Count(normalizedColumns.Contains);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestTable = table;
        }

        return bestTable;
    }

    private async Task<List<CandidateRow>> LoadCandidateRowsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken ct)
    {
        const string sql = @"
            SELECT
                d.""Id"",
                d.""ArtikalId"",
                d.""BrojRacuna"",
                d.""Datum""::date AS event_date,
                d.""IDObjekat"",
                d.""DobavljacId"",
                d.""StaraProdajnaCena"",
                d.""NovaProdajnaCena"",
                v.pre_revenue,
                v.post_revenue
            FROM ""DnevnikPromena"" AS d
            LEFT JOIN ""vw_vendor_sales_nivelacija"" AS v
              ON v.price_event_id = d.""Id""
            WHERE d.""DataOrigin"" = 'access'
              AND d.""TipPromene"" = 'Nivelacija'
              AND d.""ArtikalId"" IS NOT NULL
            ORDER BY d.""Id"";";

        var rows = new List<CandidateRow>();
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var idOrdinal = reader.GetOrdinal("Id");
        var articleIdOrdinal = reader.GetOrdinal("ArtikalId");
        var brojRacunaOrdinal = reader.GetOrdinal("BrojRacuna");
        var eventDateOrdinal = reader.GetOrdinal("event_date");
        var storeOrdinal = reader.GetOrdinal("IDObjekat");
        var vendorOrdinal = reader.GetOrdinal("DobavljacId");
        var oldPriceOrdinal = reader.GetOrdinal("StaraProdajnaCena");
        var newPriceOrdinal = reader.GetOrdinal("NovaProdajnaCena");
        var preRevenueOrdinal = reader.GetOrdinal("pre_revenue");
        var postRevenueOrdinal = reader.GetOrdinal("post_revenue");

        while (await reader.ReadAsync(ct))
        {
            rows.Add(new CandidateRow(
                reader.GetInt32(idOrdinal),
                reader.GetInt32(articleIdOrdinal),
                reader.IsDBNull(brojRacunaOrdinal) ? null : reader.GetString(brojRacunaOrdinal),
                DateOnly.FromDateTime(reader.GetDateTime(eventDateOrdinal)),
                reader.IsDBNull(storeOrdinal) ? null : reader.GetInt32(storeOrdinal),
                reader.IsDBNull(vendorOrdinal) ? null : reader.GetInt32(vendorOrdinal),
                reader.IsDBNull(oldPriceOrdinal) ? null : reader.GetDecimal(oldPriceOrdinal),
                reader.IsDBNull(newPriceOrdinal) ? null : reader.GetDecimal(newPriceOrdinal),
                reader.IsDBNull(preRevenueOrdinal) ? null : reader.GetDecimal(preRevenueOrdinal),
                reader.IsDBNull(postRevenueOrdinal) ? null : reader.GetDecimal(postRevenueOrdinal)));
        }

        return rows;
    }

    private async Task<NivelacijaRepairVerificationDto> CollectVerificationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        AccessLineageResult accessLineage,
        CancellationToken ct)
    {
        const string aggregateSql = @"
            SELECT
                (SELECT COUNT(*) FROM ""DnevnikPromena""
                 WHERE ""DataOrigin"" = 'access'
                   AND ""TipPromene"" = 'Nivelacija'
                   AND ""ArtikalId"" IS NOT NULL) AS imported_line_rows,
                (SELECT COUNT(*) FROM ""DnevnikPromena""
                 WHERE ""DataOrigin"" = 'access'
                   AND ""TipPromene"" IN ('Nivelacija', 'Nivelacija cena')
                   AND ""ArtikalId"" IS NULL) AS imported_header_rows,
                (SELECT COUNT(*) FROM ""vw_sales_pre_nivelacija"") AS pre_rows,
                (SELECT COUNT(*) FROM ""vw_sales_post_nivelacija"") AS post_rows,
                (SELECT COUNT(*) FROM ""vw_vendor_sales_nivelacija"") AS vendor_rows,
                (SELECT COUNT(DISTINCT price_event_id) FROM ""vw_vendor_sales_nivelacija"") AS vendor_distinct_events,
                (SELECT COUNT(DISTINCT ""BrojRacuna"") FROM ""DnevnikPromena""
                 WHERE ""DataOrigin"" = 'access'
                   AND ""TipPromene"" = 'Nivelacija'
                   AND ""ArtikalId"" IS NOT NULL
                   AND ""BrojRacuna"" ~ '^-?[0-9]+$') AS imported_distinct_source_headers,
                (SELECT COALESCE(SUM(pre_qty), 0) FROM ""vw_sales_pre_nivelacija"") AS pre_qty_sum,
                (SELECT COALESCE(SUM(pre_revenue), 0) FROM ""vw_sales_pre_nivelacija"") AS pre_revenue_sum,
                (SELECT COALESCE(SUM(post_qty), 0) FROM ""vw_sales_post_nivelacija"") AS post_qty_sum,
                (SELECT COALESCE(SUM(post_revenue), 0) FROM ""vw_sales_post_nivelacija"") AS post_revenue_sum,
                (SELECT COALESCE(SUM(pre_qty), 0) FROM ""vw_vendor_sales_nivelacija"") AS vendor_pre_qty_sum,
                (SELECT COALESCE(SUM(pre_revenue), 0) FROM ""vw_vendor_sales_nivelacija"") AS vendor_pre_revenue_sum,
                (SELECT COALESCE(SUM(post_qty), 0) FROM ""vw_vendor_sales_nivelacija"") AS vendor_post_qty_sum,
                (SELECT COALESCE(SUM(post_revenue), 0) FROM ""vw_vendor_sales_nivelacija"") AS vendor_post_revenue_sum;";

        const string edgeCaseSql = @"
            SELECT
                (SELECT COUNT(*) FROM (
                    SELECT ""ArtikalId"", ""Datum""::date, COALESCE(""Iznos"", 0), COUNT(*)
                    FROM ""DnevnikPromena""
                    WHERE ""DataOrigin"" = 'access'
                      AND ""TipPromene"" = 'Nivelacija'
                      AND ""ArtikalId"" IS NOT NULL
                    GROUP BY 1, 2, 3
                    HAVING COUNT(*) > 1
                ) duplicates) AS imported_duplicate_groups,
                (SELECT COUNT(*) FROM (
                    SELECT event_date::date, COALESCE(vendor_id, -1), article_id, old_price, new_price, COUNT(*)
                    FROM ""vw_vendor_sales_nivelacija""
                    GROUP BY 1, 2, 3, 4, 5
                    HAVING COUNT(*) > 1
                ) duplicates) AS view_duplicate_groups,
                (SELECT COUNT(*) FROM ""vw_vendor_sales_nivelacija""
                 WHERE COALESCE(pre_qty, 0) = 0 OR COALESCE(post_qty, 0) = 0) AS zero_sales_period_rows,
                (SELECT COUNT(*) FROM ""vw_vendor_sales_nivelacija""
                 WHERE COALESCE(pre_qty, 0) = 0
                   AND COALESCE(post_qty, 0) = 0
                   AND COALESCE(pre_revenue, 0) = 0
                   AND COALESCE(post_revenue, 0) = 0) AS inactive_rows,
                (SELECT COUNT(*) FROM (
                    SELECT article_id, event_date::date, COUNT(*)
                    FROM ""vw_vendor_sales_nivelacija""
                    GROUP BY 1, 2
                    HAVING COUNT(*) > 1
                ) multi_change) AS multiple_changes_same_day_rows,
                to_regclass('public.vw_stock_red_zone') IS NOT NULL AS has_oos_view;";

        var aggregate = new NivelacijaRepairAggregateVerificationDto
        {
            AccessLineRows = accessLineage.TotalLineRows,
            AccessDistinctEvents = accessLineage.DistinctSourceHeaderCount,
        };

        await using (var aggregateCommand = new NpgsqlCommand(aggregateSql, connection, transaction))
        await using (var aggregateReader = await aggregateCommand.ExecuteReaderAsync(ct))
        {
            await aggregateReader.ReadAsync(ct);
            aggregate.ImportedLineRows = ReadInt32(aggregateReader, "imported_line_rows");
            aggregate.ImportedHeaderRows = ReadInt32(aggregateReader, "imported_header_rows");
            aggregate.PreRows = ReadInt32(aggregateReader, "pre_rows");
            aggregate.PostRows = ReadInt32(aggregateReader, "post_rows");
            aggregate.VendorRows = ReadInt32(aggregateReader, "vendor_rows");
            aggregate.VendorDistinctEvents = ReadInt32(aggregateReader, "vendor_distinct_events");
            aggregate.ImportedDistinctSourceHeaders = ReadInt32(aggregateReader, "imported_distinct_source_headers");
            aggregate.PreQtySum = ReadDecimal(aggregateReader, "pre_qty_sum");
            aggregate.PreRevenueSum = ReadDecimal(aggregateReader, "pre_revenue_sum");
            aggregate.PostQtySum = ReadDecimal(aggregateReader, "post_qty_sum");
            aggregate.PostRevenueSum = ReadDecimal(aggregateReader, "post_revenue_sum");
            aggregate.VendorPreQtySum = ReadDecimal(aggregateReader, "vendor_pre_qty_sum");
            aggregate.VendorPreRevenueSum = ReadDecimal(aggregateReader, "vendor_pre_revenue_sum");
            aggregate.VendorPostQtySum = ReadDecimal(aggregateReader, "vendor_post_qty_sum");
            aggregate.VendorPostRevenueSum = ReadDecimal(aggregateReader, "vendor_post_revenue_sum");
        }

        aggregate.AccessLinesMatchVendorRows = aggregate.AccessLineRows == aggregate.VendorRows;
        aggregate.PreQtyMatchesVendorQty = aggregate.PreQtySum == aggregate.VendorPreQtySum;
        aggregate.PreRevenueMatchesVendorRevenue = aggregate.PreRevenueSum == aggregate.VendorPreRevenueSum;
        aggregate.PostQtyMatchesVendorQty = aggregate.PostQtySum == aggregate.VendorPostQtySum;
        aggregate.PostRevenueMatchesVendorRevenue = aggregate.PostRevenueSum == aggregate.VendorPostRevenueSum;
        aggregate.AccessEventsMatchImportedSourceHeaders = aggregate.AccessDistinctEvents == aggregate.ImportedDistinctSourceHeaders;

        var edgeCases = new NivelacijaRepairEdgeCaseVerificationDto
        {
            AccessMultipleChangesSameDayRows = accessLineage.MultipleChangesSameDayRows,
        };

        await using (var edgeCommand = new NpgsqlCommand(edgeCaseSql, connection, transaction))
        await using (var edgeReader = await edgeCommand.ExecuteReaderAsync(ct))
        {
            await edgeReader.ReadAsync(ct);
            edgeCases.ImportedDuplicateGroups = ReadInt32(edgeReader, "imported_duplicate_groups");
            edgeCases.ViewDuplicateGroups = ReadInt32(edgeReader, "view_duplicate_groups");
            edgeCases.ZeroSalesPeriodRows = ReadInt32(edgeReader, "zero_sales_period_rows");
            edgeCases.InactiveRows = ReadInt32(edgeReader, "inactive_rows");
            edgeCases.MultipleChangesSameDayRows = ReadInt32(edgeReader, "multiple_changes_same_day_rows");

            var hasOosView = edgeReader.GetBoolean(edgeReader.GetOrdinal("has_oos_view"));
            if (hasOosView)
            {
                edgeCases.OutOfStockCheckStatus = "available";
            }
            else
            {
                edgeCases.OutOfStockCheckStatus = "skipped_missing_vw_stock_red_zone";
            }
        }

        if (string.Equals(edgeCases.OutOfStockCheckStatus, "available", StringComparison.Ordinal))
        {
            const string oosSql = @"
                SELECT COUNT(*) AS out_of_stock_event_rows
                FROM (
                    SELECT v.article_id, v.event_date::date
                    FROM ""vw_vendor_sales_nivelacija"" AS v
                    JOIN ""vw_stock_red_zone"" AS s
                      ON UPPER(TRIM(COALESCE(s.sku, ''))) = UPPER(TRIM(COALESCE(v.sku, '')))
                    WHERE s.is_oos
                    GROUP BY 1, 2
                ) AS oos_rows;";

            await using var oosCommand = new NpgsqlCommand(oosSql, connection, transaction);
            var result = await oosCommand.ExecuteScalarAsync(ct);
            edgeCases.OutOfStockEventRows = ConvertToInt(result);
        }

        return new NivelacijaRepairVerificationDto
        {
            Aggregate = aggregate,
            EdgeCases = edgeCases,
        };
    }

    private static async Task CreateTempRepairTableAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        const string sql = @"
            CREATE TEMP TABLE tmp_nivelacija_lineage_fix (
                datum date NOT NULL,
                idobjekat integer,
                dobavljacid integer,
                id integer NOT NULL
            ) ON COMMIT DROP;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task BulkLoadRepairFixesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<NivelacijaRepairFixDto> fixes,
        CancellationToken ct)
    {
        await using var importer = connection.BeginBinaryImport(
            "COPY tmp_nivelacija_lineage_fix (datum, idobjekat, dobavljacid, id) FROM STDIN (FORMAT BINARY)");

        foreach (var fix in fixes)
        {
            await importer.StartRowAsync(ct);
            importer.Write(fix.TargetEventDate, NpgsqlDbType.Date);
            WriteNullableInt(importer, fix.TargetStoreId);
            WriteNullableInt(importer, fix.TargetVendorId);
            importer.Write(fix.PriceEventId, NpgsqlDbType.Integer);
        }

        await importer.CompleteAsync(ct);
    }

    private static async Task<int> ExecuteRepairUpdateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        const string sql = @"
            UPDATE ""DnevnikPromena"" AS target
            SET ""Datum"" = source.datum,
                ""IDObjekat"" = source.idobjekat,
                ""DobavljacId"" = source.dobavljacid
            FROM tmp_nivelacija_lineage_fix AS source
            WHERE target.""Id"" = source.id
              AND (
                    target.""Datum""::date IS DISTINCT FROM source.datum
                 OR target.""IDObjekat"" IS DISTINCT FROM source.idobjekat
                 OR target.""DobavljacId"" IS DISTINCT FROM source.dobavljacid
              );";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<int> CountRemainingTempMismatchesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken ct)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM tmp_nivelacija_lineage_fix AS source
            JOIN ""DnevnikPromena"" AS target
              ON target.""Id"" = source.id
            WHERE target.""Datum""::date IS DISTINCT FROM source.datum
               OR target.""IDObjekat"" IS DISTINCT FROM source.idobjekat
               OR target.""DobavljacId"" IS DISTINCT FROM source.dobavljacid;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var result = await command.ExecuteScalarAsync(ct);
        return ConvertToInt(result) ?? 0;
    }

    private static async Task EnsureAuditSchemaAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken ct)
    {
        const string sql = @"
            CREATE TABLE IF NOT EXISTS public.nivelacija_repair_audit (
                id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""timestamp"" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ""user"" character varying(200),
                dry_run boolean NOT NULL,
                detected_issues integer NOT NULL DEFAULT 0,
                fixed_rows integer NOT NULL DEFAULT 0,
                repair_summary_json jsonb NOT NULL DEFAULT '{}'::jsonb
            );

            CREATE INDEX IF NOT EXISTS ix_nivelacija_repair_audit_timestamp
                ON public.nivelacija_repair_audit (""timestamp"" DESC);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long> InsertAuditRecordAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string requestedBy,
        bool dryRun,
        int detectedIssues,
        int fixedRows,
        string summaryJson,
        CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO public.nivelacija_repair_audit (
                ""user"",
                dry_run,
                detected_issues,
                fixed_rows,
                repair_summary_json)
            VALUES (@user, @dryRun, @detectedIssues, @fixedRows, @summaryJson::jsonb)
            RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("user", NpgsqlDbType.Varchar, string.IsNullOrWhiteSpace(requestedBy) ? "unknown" : requestedBy);
        command.Parameters.AddWithValue("dryRun", NpgsqlDbType.Boolean, dryRun);
        command.Parameters.AddWithValue("detectedIssues", NpgsqlDbType.Integer, detectedIssues);
        command.Parameters.AddWithValue("fixedRows", NpgsqlDbType.Integer, fixedRows);
        command.Parameters.AddWithValue("summaryJson", NpgsqlDbType.Jsonb, summaryJson);

        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }

    private static string BuildAuditSummaryJson(
        bool dryRun,
        NivelacijaRepairPlanDto plan,
        int fixedRows,
        int skippedRows,
        int remainingIssues,
        NivelacijaRepairVerificationDto verification)
    {
        var payload = new
        {
            dryRun,
            plan.SourceFilePath,
            plan.GeneratedAtUtc,
            plan.EstimatedImpact,
            verification,
            fixedRows,
            skippedRows,
            remainingIssues,
            sampleIssues = plan.DetectedIssues.Take(25).ToArray(),
            sampleFixes = plan.ProposedFixes.Take(25).ToArray(),
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connectionString = _db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Trendplus connection string is not configured.");
        }

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private async Task<string> ResolveSourceFilePathAsync(string? explicitSourceFilePath, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(explicitSourceFilePath))
        {
            if (!File.Exists(explicitSourceFilePath))
                throw new FileNotFoundException("Configured Access source file was not found.", explicitSourceFilePath);

            return explicitSourceFilePath;
        }

        var recentBatchPath = await _db.DataImportBatches
            .AsNoTracking()
            .Where(batch => batch.SourceSystem == "access" && batch.SourceFilePath != null && batch.SourceFilePath != string.Empty)
            .OrderByDescending(batch => batch.CompletedAtUtc ?? batch.StartedAtUtc)
            .Select(batch => batch.SourceFilePath)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(recentBatchPath) && File.Exists(recentBatchPath))
            return recentBatchPath;

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        var depth = 0;
        while (directory is not null && depth < 8)
        {
            foreach (var candidateName in RootAccessFileCandidates)
            {
                var candidatePath = Path.Combine(directory.FullName, candidateName);
                if (File.Exists(candidatePath))
                    return candidatePath;
            }

            directory = directory.Parent;
            depth++;
        }

        throw new FileNotFoundException("Access source file was not found. Provide sourceFilePath or place TRENDPLUS.mdb/TRENDPLUS.accdb in the repository root.");
    }

    private Task<RepairAccessSession> OpenAccessSessionAsync(string sourceFilePath, CancellationToken ct)
    {
        var effectivePath = MaybeCreateSnapshotCopy(sourceFilePath, out var deleteAfter);
        IAccessDataReaderSession session = OperatingSystem.IsWindows()
            ? new WindowsAccessSession(effectivePath, _accessOptions, _logger)
            : new MdbToolsCliSession(effectivePath, _accessOptions, _logger);

        return Task.FromResult(new RepairAccessSession(session, effectivePath, deleteAfter, _logger));
    }

    private static string MaybeCreateSnapshotCopy(string sourceFilePath, out bool deleteAfter)
    {
        deleteAfter = false;
        var lockFilePath = GetAccessLockFilePath(sourceFilePath);
        if (lockFilePath is null || !File.Exists(lockFilePath))
            return sourceFilePath;

        var tempDirectory = Path.Combine(Path.GetTempPath(), "trendplus-nivelacija-repair");
        Directory.CreateDirectory(tempDirectory);
        var extension = Path.GetExtension(sourceFilePath);
        var baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
        var tempPath = Path.Combine(tempDirectory, $"{baseName}-snapshot-{Guid.NewGuid():N}{extension}");
        File.Copy(sourceFilePath, tempPath, overwrite: true);
        deleteAfter = true;
        return tempPath;
    }

    private static string? GetAccessLockFilePath(string sourceFilePath)
    {
        var directory = Path.GetDirectoryName(sourceFilePath);
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        var baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
        var extension = Path.GetExtension(sourceFilePath);
        if (string.Equals(extension, ".accdb", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(directory, $"{baseName}.laccdb");
        if (string.Equals(extension, ".mdb", StringComparison.OrdinalIgnoreCase))
            return Path.Combine(directory, $"{baseName}.ldb");

        return null;
    }

    private static NivelacijaRepairIssueDto BuildMissingMappingIssue(CandidateRow row, string detectedIssueType, int? sourceHeaderId = null)
    {
        return new NivelacijaRepairIssueDto
        {
            PriceEventId = row.PriceEventId,
            ArticleId = row.ArticleId,
            SourceHeaderId = sourceHeaderId,
            VendorId = row.CurrentVendorId,
            EventDate = row.EventDate,
            OldPrice = row.OldPrice,
            NewPrice = row.NewPrice,
            CalculatedPreSales = row.CalculatedPreSales,
            CalculatedPostSales = row.CalculatedPostSales,
            DetectedIssueType = detectedIssueType,
            CurrentStoreId = row.CurrentStoreId,
            CurrentVendorId = row.CurrentVendorId,
            Fixable = false,
        };
    }

    private static string BuildDetectedIssueType(IReadOnlyList<string> changedFields)
    {
        return string.Join("|", changedFields.Select(static field => field switch
        {
            "event_date" => "event_date_mismatch",
            "store" => "store_mismatch",
            "vendor" => "vendor_mismatch",
            _ => field,
        }));
    }

    private static int NormalizeThreshold(int requestedThreshold)
    {
        if (requestedThreshold <= 0)
            return DefaultMaxRowsThreshold;

        return Math.Clamp(requestedThreshold, 1, AbsoluteMaxRowsThreshold);
    }

    private static bool TryParseSourceHeaderId(string? rawValue, out int sourceHeaderId)
    {
        sourceHeaderId = default;
        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        return int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out sourceHeaderId);
    }

    private static int ReadInt32(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static decimal ReadDecimal(NpgsqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
            return 0m;

        return Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static int? ReadInt(AccessDataRow row, params string[] aliases)
        => ReadValue(row, ConvertToInt, aliases);

    private static DateOnly? ReadDateOnly(AccessDataRow row, params string[] aliases)
        => ReadValue(row, ConvertToDateOnly, aliases);

    private static T? ReadValue<T>(AccessDataRow row, Func<object?, T?> converter, params string[] aliases)
        where T : struct
    {
        foreach (var alias in aliases)
        {
            var normalized = AccessImportService.Normalize(alias);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!row.TryGetValueNormalized(normalized, out var value))
                continue;

            var converted = converter(value);
            if (converted.HasValue)
                return converted.Value;
        }

        return null;
    }

    private static int? ConvertToInt(object? value)
    {
        if (value is null or DBNull)
            return null;

        switch (value)
        {
            case int intValue:
                return intValue;
            case long longValue:
                return longValue > int.MaxValue || longValue < int.MinValue ? null : (int)longValue;
            case short shortValue:
                return shortValue;
            case decimal decimalValue:
                return decimalValue > int.MaxValue || decimalValue < int.MinValue ? null : decimal.ToInt32(decimalValue);
            case double doubleValue:
                return doubleValue > int.MaxValue || doubleValue < int.MinValue ? null : Convert.ToInt32(doubleValue, CultureInfo.InvariantCulture);
            case float floatValue:
                return floatValue > int.MaxValue || floatValue < int.MinValue ? null : Convert.ToInt32(floatValue, CultureInfo.InvariantCulture);
            case string stringValue:
                if (int.TryParse(stringValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
                    return parsedInt;
                if (decimal.TryParse(stringValue.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedDecimal))
                    return parsedDecimal > int.MaxValue || parsedDecimal < int.MinValue ? null : decimal.ToInt32(parsedDecimal);
                return null;
            default:
                try
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return null;
                }
        }
    }

    private static DateOnly? ConvertToDateOnly(object? value)
    {
        if (value is null or DBNull)
            return null;

        return value switch
        {
            DateOnly dateOnly => dateOnly,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            string stringValue when DateOnly.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedDateOnly) => parsedDateOnly,
            string stringValue when DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedDateTimeInvariant) => DateOnly.FromDateTime(parsedDateTimeInvariant),
            string stringValue when DateTime.TryParse(stringValue, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedDateTimeCurrent) => DateOnly.FromDateTime(parsedDateTimeCurrent),
            _ => null,
        };
    }

    private static void WriteNullableInt(NpgsqlBinaryImporter importer, int? value)
    {
        if (value.HasValue)
        {
            importer.Write(value.Value, NpgsqlDbType.Integer);
            return;
        }

        importer.WriteNull();
    }

    private sealed record AccessTablesResult(
        string? NivelacijeTable,
        string? DnevnikTable,
        string? ArtikliTable,
        List<string> Warnings);

    private sealed record AccessHeaderSnapshot(
        DateOnly? EventDate,
        int? StoreId,
        int? VendorId);

    private sealed record AccessLineageEntry(
        DateOnly? EventDate,
        int? StoreId,
        int? VendorId);

    private sealed record AccessLineageResult(
        Dictionary<(int ArticleId, int SourceHeaderId), AccessLineageEntry> LineageByKey,
        int TotalLineRows,
        int DistinctSourceHeaderCount,
        int MultipleChangesSameDayRows);

    private sealed record CandidateRow(
        int PriceEventId,
        int ArticleId,
        string? SourceHeaderRaw,
        DateOnly EventDate,
        int? CurrentStoreId,
        int? CurrentVendorId,
        decimal? OldPrice,
        decimal? NewPrice,
        decimal? CalculatedPreSales,
        decimal? CalculatedPostSales);

    private sealed record AnalysisResult(
        NivelacijaRepairPlanDto Plan,
        AccessLineageResult AccessLineage);

    private sealed class RepairAccessSession : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly bool _deleteAfter;

        public RepairAccessSession(IAccessDataReaderSession session, string effectivePath, bool deleteAfter, ILogger logger)
        {
            Session = session;
            EffectivePath = effectivePath;
            _deleteAfter = deleteAfter;
            _logger = logger;
        }

        public IAccessDataReaderSession Session { get; }

        public string EffectivePath { get; }

        public async ValueTask DisposeAsync()
        {
            await Session.DisposeAsync();
            if (!_deleteAfter || !File.Exists(EffectivePath))
                return;

            try
            {
                File.Delete(EffectivePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary Access snapshot used by NivelacijaRepairService. Path: {Path}.", EffectivePath);
            }
        }
    }
}