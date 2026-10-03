using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Application.Analytics;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Infrastructure.Services;

public interface IOperationsAnalyticsIntegrityService
{
    void MarkUnverified(string trigger, string summary);
    Task MarkUnverifiedAsync(string trigger, string summary, string? family = null, CancellationToken ct = default);
    Task<OperationsAnalyticsIntegritySnapshot> RunBoundedProbeAsync(
        CancellationToken ct = default,
        string? trigger = null,
        string? summary = null);
}

public sealed class OperationsAnalyticsIntegrityService : IOperationsAnalyticsIntegrityService
{
    private readonly TrendplusDbContext _db;
    private readonly IAnalyticsCacheService _cache;
    private readonly OperationsAnalyticsIntegrityRegistry _registry;
    private readonly OperationsAnalyticsIntegrityOptions _options;
    private readonly ILogger<OperationsAnalyticsIntegrityService> _logger;
    private readonly IReadOnlyDictionary<string, IOperationsAnalyticsIntegrityFamilyProbe> _familyProbes;
    private readonly TimeZoneInfo _dailySalesTimeZone;

    public OperationsAnalyticsIntegrityService(
        TrendplusDbContext db,
        IAnalyticsCacheService cache,
        OperationsAnalyticsIntegrityRegistry registry,
        IOptions<OperationsAnalyticsIntegrityOptions> options,
        ILogger<OperationsAnalyticsIntegrityService> logger,
        IEnumerable<IOperationsAnalyticsIntegrityFamilyProbe>? familyProbes = null,
        IConfiguration? configuration = null)
    {
        _db = db;
        _cache = cache;
        _registry = registry;
        _options = options.Value;
        _logger = logger;
        _familyProbes = (familyProbes ?? Array.Empty<IOperationsAnalyticsIntegrityFamilyProbe>())
            .Where(probe => !string.IsNullOrWhiteSpace(probe.Family))
            .GroupBy(probe => probe.Family.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        _dailySalesTimeZone = ResolveDailySalesTimeZone(configuration?["DailySales:TimeZoneId"]);
    }

    public void MarkUnverified(string trigger, string summary)
    {
        _registry.MarkUnverified(trigger, summary);
        _logger.LogInformation(
            "Operations analytics integrity marked unverified. Trigger={Trigger}",
            trigger);
    }

    public async Task MarkUnverifiedAsync(
        string trigger,
        string summary,
        string? family = null,
        CancellationToken ct = default)
    {
        var checkedAt = DateTime.UtcNow;
        var (fromUtc, toUtc) = ResolveProbeWindow(checkedAt);
        var filters = new OperationsAnalyticsRawFactOracle.Filters(
            fromUtc,
            toUtc,
            null,
            OperationsAnalyticsRawFactOracle.NormalizeDataScope(_options.DefaultDataScope));
        var affectedFamilies = OperationsAnalyticsIntegrityFamilies.ResolveAffected(family);
        foreach (var affectedFamily in affectedFamilies)
        {
            var generation = _registry.GetGeneration(affectedFamily);
            var snapshot = CreateSnapshot(
                affectedFamily,
                OperationsAnalyticsIntegrityStates.Unverified,
                BuildEvidenceId($"{trigger}:{affectedFamily}"),
                trigger,
                summary,
                filters,
                checkedAt,
                contextFingerprint: BuildContextFingerprint(affectedFamily, generation, filters),
                sourceGenerationForContext: generation,
                deltas: Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
                blocksDecisionSignals: false);
            await StoreAsync(snapshot, filters, ct);
        }
        _logger.LogInformation(
            "Operations analytics integrity unverified transition persisted. Trigger={Trigger} Family={Family}",
            trigger,
            family ?? "all");
    }

    public async Task<OperationsAnalyticsIntegritySnapshot> RunBoundedProbeAsync(
        CancellationToken ct = default,
        string? trigger = null,
        string? summary = null)
    {
        var checkedAt = DateTime.UtcNow;
        var (fromUtc, toUtc) = ResolveProbeWindow(checkedAt);
        var dataScope = OperationsAnalyticsRawFactOracle.NormalizeDataScope(_options.DefaultDataScope);
        var filters = new OperationsAnalyticsRawFactOracle.Filters(fromUtc, toUtc, null, dataScope);
        var probeTrigger = string.IsNullOrWhiteSpace(trigger) ? "bounded_probe" : trigger.Trim();
        var probeSummary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.ProbeTimeoutSeconds)));
        var probeCt = timeoutCts.Token;

        if (!_options.Enabled)
            return await StoreFamilyStatesAsync(
                filters,
                probeTrigger,
                probeSummary ?? "Operations integrity probes are disabled in configuration.",
                OperationsAnalyticsIntegrityStates.Degraded,
                ct);

        try
        {
            var connectionString = _db.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
                return await StoreFamilyStatesAsync(
                    filters,
                    probeTrigger,
                    probeSummary ?? "Database connection string is unavailable for integrity probes.",
                    OperationsAnalyticsIntegrityStates.Degraded,
                    ct);

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(probeCt);

            var oracleTotals = await OperationsAnalyticsRawFactOracle.QueryTotalsAsync(connection, filters, probeCt);
            if (oracleTotals.SaleLineCount > _options.MaxProbeRows)
            {
                return await StoreFamilyStatesAsync(
                    filters,
                    probeTrigger,
                    $"Probe row bound exceeded ({oracleTotals.SaleLineCount} > {_options.MaxProbeRows}); no family is certified.",
                    OperationsAnalyticsIntegrityStates.Degraded,
                    ct);
            }

            var liveTotals = await ReadLiveAggregatedTotalsAsync(filters, probeCt);
            var deltas = new List<OperationsAnalyticsIntegrityProbeDelta>
            {
                BuildDelta("supplier_shoe_live_aggregate", liveTotals.Revenue, oracleTotals.TotalRevenue, liveTotals.Units, oracleTotals.TotalUnits)
            };

            var supplierCacheComparison = await TryCompareCachedSupplierAsync(connection, filters, liveTotals, probeCt);
            if (supplierCacheComparison.AggregateDelta is not null)
                deltas.Add(supplierCacheComparison.AggregateDelta);
            deltas.AddRange(supplierCacheComparison.BucketDeltas);
            var shoeTypeBucketComparison = await TryCompareCachedIdBucketsAsync(
                connection,
                filters,
                AnalyticsCacheKeys.ShoeTypeSalesStats(fromUtc, toUtc, null, null, dataScope, activeSnapshotBatchId: null),
                "shoeTypes",
                "tipObuceId",
                "shoe_type_bucket",
                OperationsAnalyticsRawFactOracle.QueryShoeTypeBucketsAsync,
                probeCt);
            var colorBucketComparison = await TryCompareCachedColorBucketsAsync(connection, filters, fromUtc, toUtc, dataScope, probeCt);
            var dailyBucketComparison = await TryCompareCachedDailyBucketsAsync(connection, filters, probeCt);
            deltas.AddRange(shoeTypeBucketComparison.Deltas);
            deltas.AddRange(colorBucketComparison.Deltas);
            deltas.AddRange(dailyBucketComparison.Deltas);
            var allBucketDimensionsCompared = supplierCacheComparison.BucketsCompared
                                             && shoeTypeBucketComparison.Compared
                                             && colorBucketComparison.Compared
                                             && dailyBucketComparison.Compared;

            var supplierDrift = deltas.Any(delta =>
                !delta.Dimension.StartsWith("daily_", StringComparison.Ordinal)
                && (
                Math.Abs(delta.RevenueDelta) > _options.RevenueToleranceRsd
                || delta.UnitsDelta != 0));
            var dashboardDeltas = new[]
                {
                    BuildDelta("dashboard_sales_totals", liveTotals.Revenue, oracleTotals.TotalRevenue, liveTotals.Units, oracleTotals.TotalUnits)
                }
                .Concat(deltas.Where(delta => delta.Dimension.StartsWith("daily_", StringComparison.Ordinal)))
                .ToArray();
            var dashboardDrift = dashboardDeltas.Any(delta =>
                    Math.Abs(delta.RevenueDelta) > _options.RevenueToleranceRsd
                    || delta.UnitsDelta != 0);

            var supplierFamily = OperationsAnalyticsIntegrityFamilies.SupplierShoeType;
            var supplierGeneration = _registry.GetGeneration(supplierFamily);
            var supplierStatus = supplierDrift
                ? OperationsAnalyticsIntegrityStates.DriftDetected
                : allBucketDimensionsCompared
                    ? OperationsAnalyticsIntegrityStates.Verified
                    : OperationsAnalyticsIntegrityStates.Unverified;
            var supplierSnapshot = CreateSnapshot(
                supplierFamily,
                supplierStatus,
                BuildEvidenceId($"{probeTrigger}:{supplierFamily}"),
                probeTrigger,
                probeSummary ?? (supplierDrift
                    ? "Bounded Supplier/Shoe Type probe detected unexplained aggregate or supplier-bucket drift."
                    : allBucketDimensionsCompared
                        ? "Bounded Operations probe reconciled supplier, shoe-type, color and daily day/shift/store buckets to the raw-fact oracle."
                        : "Aggregate totals reconciled, but one or more supplier, shoe-type, color or daily bucket caches were unavailable; bucket integrity remains unverified."),
                filters,
                checkedAt,
                BuildContextFingerprint(supplierFamily, supplierGeneration, filters),
                supplierGeneration,
                deltas,
                supplierDrift,
                supplierStatus == OperationsAnalyticsIntegrityStates.Verified ? checkedAt : null,
                oracleTotals.SaleLineCount);
            await StoreAsync(supplierSnapshot, filters, ct);

            var salesFamily = OperationsAnalyticsIntegrityFamilies.SalesDashboard;
            var salesGeneration = _registry.GetGeneration(salesFamily);
            var salesSnapshot = CreateSnapshot(
                salesFamily,
                dashboardDrift
                    ? OperationsAnalyticsIntegrityStates.DriftDetected
                    : OperationsAnalyticsIntegrityStates.Verified,
                BuildEvidenceId($"{probeTrigger}:{salesFamily}"),
                probeTrigger,
                probeSummary ?? (dashboardDrift
                    ? "Dashboard sales totals have a non-zero bounded reconciliation delta."
                    : "Dashboard sales totals reconciled to the independent raw-fact oracle."),
                filters,
                checkedAt,
                BuildContextFingerprint(salesFamily, salesGeneration, filters),
                salesGeneration,
                dashboardDeltas,
                dashboardDrift,
                dashboardDrift ? null : checkedAt,
                oracleTotals.SaleLineCount);
            await StoreAsync(salesSnapshot, filters, ct);

            foreach (var definition in OperationsAnalyticsIntegrityFamilies.Enrolled.Where(
                         definition => !string.Equals(definition.Family, supplierFamily, StringComparison.Ordinal)
                                      && !string.Equals(definition.Family, salesFamily, StringComparison.Ordinal)))
            {
                var generation = _registry.GetGeneration(definition.Family);
                var result = await RunFamilyProbeAsync(definition, generation, filters, probeTrigger, probeCt);
                var snapshot = CreateSnapshot(
                    definition.Family,
                    result.Status,
                    BuildEvidenceId($"{probeTrigger}:{definition.Family}"),
                    probeTrigger,
                    result.Summary,
                    filters,
                    checkedAt,
                    BuildContextFingerprint(definition.Family, generation, filters),
                    generation,
                    result.Deltas,
                    result.BlocksDecisionSignals,
                    result.Status == OperationsAnalyticsIntegrityStates.Verified ? checkedAt : null,
                    result.ProbeRowCount);
                await StoreAsync(snapshot, filters, ct);
            }

            return supplierSnapshot;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Operations analytics integrity probe timed out after {TimeoutSeconds} seconds. Trigger={Trigger}",
                Math.Max(1, _options.ProbeTimeoutSeconds),
                probeTrigger);
            return await StoreFamilyStatesAsync(
                filters,
                probeTrigger,
                probeSummary ?? "Operations integrity probe timed out; decision signals remain fail-closed until a successful check.",
                OperationsAnalyticsIntegrityStates.Degraded,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Operations analytics integrity probe failed.");
            return await StoreFamilyStatesAsync(
                filters,
                probeTrigger,
                probeSummary ?? "Operations integrity probe failed; decision signals remain fail-closed until a successful check.",
                OperationsAnalyticsIntegrityStates.Degraded,
                ct);
        }
    }

    private async Task<OperationsAnalyticsIntegritySnapshot> StoreFamilyStatesAsync(
        OperationsAnalyticsRawFactOracle.Filters filters,
        string trigger,
        string summary,
        string status,
        CancellationToken ct)
    {
        OperationsAnalyticsIntegritySnapshot? supplierSnapshot = null;
        var checkedAt = DateTime.UtcNow;

        foreach (var definition in OperationsAnalyticsIntegrityFamilies.Enrolled)
        {
            var generation = _registry.GetGeneration(definition.Family);
            var snapshot = CreateSnapshot(
                definition.Family,
                status,
                BuildEvidenceId($"{trigger}:{definition.Family}"),
                trigger,
                summary,
                filters,
                checkedAt,
                BuildContextFingerprint(definition.Family, generation, filters),
                generation,
                Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
                blocksDecisionSignals: false,
                lastVerifiedAtUtc: null,
                probeRowCount: null);
            await StoreAsync(snapshot, filters, ct);

            if (string.Equals(definition.Family, OperationsAnalyticsIntegrityFamilies.SupplierShoeType, StringComparison.Ordinal))
                supplierSnapshot = snapshot;
        }

        return supplierSnapshot!;
    }

    private async Task<OperationsAnalyticsIntegrityProbeResult> RunFamilyProbeAsync(
        OperationsAnalyticsIntegrityFamilyDefinition definition,
        string generation,
        OperationsAnalyticsRawFactOracle.Filters filters,
        string trigger,
        CancellationToken ct)
    {
        if (!_familyProbes.TryGetValue(definition.Family, out var probe))
        {
            return OperationsAnalyticsIntegrityProbeResult.Unverified(
                $"No independently derived bounded probe is registered for {definition.Family}; state remains explicitly unverified.");
        }

        var request = new OperationsAnalyticsIntegrityProbeRequest(
            definition,
            BuildContextFingerprint(definition.Family, generation, filters),
            generation,
            filters.FromUtc,
            filters.ToUtc,
            filters.DataScope,
            trigger,
            Math.Min(definition.MaxRows, _options.MaxProbeRows),
            ct);

        try
        {
            return await probe.ProbeAsync(request);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return OperationsAnalyticsIntegrityProbeResult.Degraded(
                $"{definition.Family} bounded probe exceeded its runtime/window budget.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Integrity family probe failed. Family={Family}", definition.Family);
            return OperationsAnalyticsIntegrityProbeResult.Degraded(
                $"{definition.Family} bounded probe dependency failed; no verification was recorded.");
        }
    }

    private OperationsAnalyticsIntegritySnapshot CreateSnapshot(
        string family,
        string status,
        string evidenceId,
        string trigger,
        string summary,
        OperationsAnalyticsRawFactOracle.Filters filters,
        DateTime checkedAtUtc,
        string contextFingerprint,
        string sourceGenerationForContext,
        IReadOnlyList<OperationsAnalyticsIntegrityProbeDelta> deltas,
        bool blocksDecisionSignals,
        DateTime? lastVerifiedAtUtc = null,
        int? probeRowCount = null)
    {
        var snapshot = new OperationsAnalyticsIntegritySnapshot(
            status,
            evidenceId,
            checkedAtUtc,
            lastVerifiedAtUtc,
            trigger,
            summary,
            deltas,
            blocksDecisionSignals)
        {
            Family = family,
            ContextFingerprint = contextFingerprint,
            SourceGeneration = sourceGenerationForContext,
            ProbeWindowFromUtc = filters.FromUtc,
            ProbeWindowToUtc = filters.ToUtc,
            ProbeRowCount = probeRowCount
        };
        return snapshot;
    }

    private static string BuildContextFingerprint(
        string family,
        string sourceGeneration,
        OperationsAnalyticsRawFactOracle.Filters filters)
        => AnalyticsContextFingerprintPolicy.Create(
            sourceDataset: $"integrity:{family}",
            sourceGeneration: sourceGeneration,
            formulaVersion: "analytics_integrity_probe_v2",
            materializerGeneration: "bounded-independent-probe",
            rowLimitSemantics: $"max_rows:{OperationsAnalyticsIntegrityFamilies.DefinitionFor(family).MaxRows}",
            requestedPeriodFromUtc: filters.FromUtc,
            requestedPeriodToUtc: filters.ToUtc,
            effectivePeriodFromUtc: filters.FromUtc,
            effectivePeriodToUtc: filters.ToUtc,
            observedPeriodFromUtc: filters.FromUtc,
            observedPeriodToUtc: filters.ToUtc,
            requestedDataScope: filters.DataScope,
            effectiveDataScope: filters.DataScope,
            dataScopeSource: "operations_integrity_probe",
            populationKey: family,
            populationFilters: new Dictionary<string, string?>
            {
                ["store_id"] = filters.StoreId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["family"] = family
            },
            cacheGeneration: null,
            resultState: AnalyticsContextFingerprintPolicy.StateAvailable).Fingerprint!;

    private async Task<OperationsAnalyticsIntegritySnapshot> StoreAsync(
        OperationsAnalyticsIntegritySnapshot snapshot,
        OperationsAnalyticsRawFactOracle.Filters filters,
        CancellationToken ct)
    {
        _registry.Set(snapshot);

        try
        {
            var connectionString = _db.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
                return snapshot;

            var alreadyPersisted = await _db.OperationsAnalyticsIntegrityEvidence
                .AsNoTracking()
                .AnyAsync(row => row.EvidenceId == snapshot.EvidenceId, ct);
            if (alreadyPersisted)
                return snapshot;

            var appliedMigrations = await _db.Database.GetAppliedMigrationsAsync(ct);
            var primaryDelta = snapshot.Deltas.FirstOrDefault();
            var databaseFingerprint = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(connectionString)))
                .ToLowerInvariant();

            _db.OperationsAnalyticsIntegrityEvidence.Add(new Domain.Model.Analytics.OperationsAnalyticsIntegrityEvidenceRecord
            {
                EvidenceId = snapshot.EvidenceId,
                Family = snapshot.Family,
                Status = snapshot.Status,
                CheckedAtUtc = snapshot.CheckedAtUtc,
                LastVerifiedAtUtc = snapshot.LastVerifiedAtUtc,
                Trigger = snapshot.Trigger,
                Summary = snapshot.Summary,
                FailureClassification = snapshot.Status == OperationsAnalyticsIntegrityStates.Verified ? null : snapshot.Trigger,
                TenantScope = Environment.GetEnvironmentVariable("TRENDPLUS_TENANT_SCOPE") ?? "dedicated",
                StoreId = filters.StoreId,
                DataScope = filters.DataScope,
                RequestedFromUtc = filters.FromUtc,
                RequestedToUtc = filters.ToUtc,
                EffectiveFromUtc = filters.FromUtc,
                EffectiveToUtc = filters.ToUtc,
                DatabaseFingerprint = databaseFingerprint,
                AppCommit = Environment.GetEnvironmentVariable("TRENDPLUS_APP_COMMIT") ?? "unknown",
                SchemaVersion = appliedMigrations.LastOrDefault() ?? "unknown",
                ContractVersion = "SST-ACCURACY-1.0",
                ContextFingerprint = snapshot.ContextFingerprint,
                SourceGeneration = snapshot.SourceGeneration,
                FixtureVersion = "supplier-shoetype-adversarial-golden-2026-09-26",
                CacheVersion = "supplier-v5;shoe-v4;color-v5;data-window-v2",
                EndpointOrLiveRevenue = primaryDelta?.EndpointOrLiveRevenue,
                OracleRevenue = primaryDelta?.OracleRevenue,
                RevenueDelta = primaryDelta?.RevenueDelta,
                EndpointOrLiveUnits = primaryDelta?.EndpointOrLiveUnits,
                OracleUnits = primaryDelta?.OracleUnits,
                UnitsDelta = primaryDelta?.UnitsDelta,
                DeltasJson = JsonSerializer.Serialize(snapshot.Deltas),
                CoverageJson = JsonSerializer.Serialize(new
                {
                    family = snapshot.Family,
                    contextFingerprint = snapshot.ContextFingerprint,
                    sourceGeneration = snapshot.SourceGeneration,
                    probeWindowFromUtc = snapshot.ProbeWindowFromUtc,
                    probeWindowToUtc = snapshot.ProbeWindowToUtc,
                    probeRowCount = snapshot.ProbeRowCount,
                    unknownAttribution = "not_collected_by_bounded_probe",
                    attributionCoverage = "not_collected_by_bounded_probe",
                    costCoverage = "not_collected_by_bounded_probe"
                }),
                BlocksDecisionSignals = snapshot.BlocksDecisionSignals,
                CreatedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Operations analytics integrity evidence could not be persisted for {EvidenceId}.", snapshot.EvidenceId);
        }

        return snapshot;
    }

    private async Task<(decimal Revenue, int Units)> ReadLiveAggregatedTotalsAsync(
        OperationsAnalyticsRawFactOracle.Filters filters,
        CancellationToken ct)
    {
        var rows = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            where pz.DatumProdaje >= filters.FromUtc
                  && pz.DatumProdaje < filters.ToUtc
                  && (!filters.StoreId.HasValue || pz.IDObjekat == filters.StoreId.Value)
                  && (pz.BrojRacuna == null || pz.BrojRacuna.Trim().ToUpper() != "DUG")
                  && (pz.BrojRacuna == null || pz.BrojRacuna.Trim().ToUpper() != "KOREKCIJA")
                  && (filters.DataScope != "imported" || pz.DataOrigin == "access")
                  && (filters.DataScope != "existing" || pz.DataOrigin == "existing" || pz.DataOrigin == null || pz.DataOrigin == "")
            select new { ps.Kolicina, Revenue = ps.Kolicina * ps.Cena })
            .ToListAsync(ct);

        return (rows.Sum(x => x.Revenue), rows.Sum(x => x.Kolicina));
    }

    private async Task<SupplierCacheComparison> TryCompareCachedSupplierAsync(
        NpgsqlConnection connection,
        OperationsAnalyticsRawFactOracle.Filters filters,
        (decimal Revenue, int Units) liveTotals,
        CancellationToken ct)
    {
        var cacheKey = AnalyticsCacheKeys.SupplierSalesStats(
            filters.FromUtc,
            filters.ToUtc,
            storeId: null,
            sezonaId: null,
            filters.DataScope,
            activeSnapshotBatchId: null,
            integrityEvidenceId: _registry.Current.EvidenceId);
        var cached = await _cache.GetAsync<AnalyticsJsonCachePayload>(cacheKey, ct);
        if (cached is null || string.IsNullOrWhiteSpace(cached.Json))
            return new SupplierCacheComparison(null, Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(), false);

        try
        {
            using var document = JsonDocument.Parse(cached.Json);
            var root = document.RootElement;
            var totals = root.GetProperty("totals");
            var aggregateDelta = BuildDelta(
                "supplier_sales_cache_lane",
                totals.GetProperty("ukupanPromet").GetDecimal(),
                liveTotals.Revenue,
                totals.GetProperty("ukupnaKolicina").GetInt32(),
                liveTotals.Units);
            if (!root.TryGetProperty("suppliers", out var supplierRows)
                || supplierRows.ValueKind != JsonValueKind.Array)
            {
                return new SupplierCacheComparison(
                    aggregateDelta,
                    Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
                    false);
            }

            var actualBuckets = supplierRows.EnumerateArray()
                .Select(row =>
                {
                    var isUnknown = row.TryGetProperty("isUnknown", out var unknownValue)
                        && unknownValue.ValueKind == JsonValueKind.True;
                    var id = row.TryGetProperty("dobavljacId", out var idValue)
                        && idValue.ValueKind == JsonValueKind.Number
                            ? idValue.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : null;
                    var key = isUnknown || id is null ? "unknown" : "id:" + id;
                    return new OperationsAnalyticsBucketReconciliation.BucketValue(
                        key,
                        row.GetProperty("ukupanPromet").GetDecimal(),
                        row.GetProperty("ukupnaKolicina").GetInt32());
                })
                .ToArray();
            var expectedBuckets = await OperationsAnalyticsRawFactOracle.QuerySupplierBucketsAsync(connection, filters, ct);
            var expectedValues = expectedBuckets.Select(bucket => new OperationsAnalyticsBucketReconciliation.BucketValue(
                bucket.DimensionId.HasValue
                    ? "id:" + bucket.DimensionId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : "unknown",
                bucket.Revenue,
                bucket.Units));
            var bucketDeltas = OperationsAnalyticsBucketReconciliation.Compare(
                "supplier_bucket",
                expectedValues,
                actualBuckets,
                _options.RevenueToleranceRsd);
            return new SupplierCacheComparison(aggregateDelta, bucketDeltas, true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Skipping cache-lane integrity comparison for key {CacheKey}.", cacheKey);
            return new SupplierCacheComparison(null, Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(), false);
        }
    }

    private sealed record SupplierCacheComparison(
        OperationsAnalyticsIntegrityProbeDelta? AggregateDelta,
        IReadOnlyList<OperationsAnalyticsIntegrityProbeDelta> BucketDeltas,
        bool BucketsCompared);

    private async Task<BucketCacheComparison> TryCompareCachedIdBucketsAsync(
        NpgsqlConnection connection,
        OperationsAnalyticsRawFactOracle.Filters filters,
        string cacheKey,
        string rowsProperty,
        string idProperty,
        string dimension,
        Func<NpgsqlConnection, OperationsAnalyticsRawFactOracle.Filters, CancellationToken, Task<IReadOnlyList<OperationsAnalyticsRawFactOracle.Bucket>>> queryOracle,
        CancellationToken ct)
    {
        var cached = await _cache.GetAsync<object>(cacheKey, ct);
        if (cached is null)
            return BucketCacheComparison.Unavailable;

        try
        {
            var root = JsonSerializer.SerializeToElement(cached);
            if (!TryGetPropertyIgnoreCase(root, rowsProperty, out var rows)
                || rows.ValueKind != JsonValueKind.Array)
                return BucketCacheComparison.Unavailable;

            var actual = rows.EnumerateArray()
                .Select(row =>
                {
                    var id = TryGetPropertyIgnoreCase(row, idProperty, out var idValue)
                             && idValue.ValueKind == JsonValueKind.Number
                        ? idValue.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : null;
                    return new OperationsAnalyticsBucketReconciliation.BucketValue(
                        id is null ? "unknown" : "id:" + id,
                        GetDecimal(row, "ukupanPromet"),
                        GetInt32(row, "ukupnaKolicina"));
                })
                .ToArray();
            var expected = await queryOracle(connection, filters, ct);
            var expectedValues = expected.Select(bucket => new OperationsAnalyticsBucketReconciliation.BucketValue(
                bucket.DimensionId.HasValue
                    ? "id:" + bucket.DimensionId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : "unknown",
                bucket.Revenue,
                bucket.Units));
            return new BucketCacheComparison(
                OperationsAnalyticsBucketReconciliation.Compare(dimension, expectedValues, actual, _options.RevenueToleranceRsd),
                true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Skipping {Dimension} bucket comparison for cache key {CacheKey}.", dimension, cacheKey);
            return BucketCacheComparison.Unavailable;
        }
    }

    private async Task<BucketCacheComparison> TryCompareCachedColorBucketsAsync(
        NpgsqlConnection connection,
        OperationsAnalyticsRawFactOracle.Filters filters,
        DateTime fromUtc,
        DateTime toUtc,
        string dataScope,
        CancellationToken ct)
    {
        var cacheKey = AnalyticsCacheKeys.ColorSalesStats(
            fromUtc,
            toUtc,
            storeId: null,
            sezonaId: null,
            dataScope);
        var cached = await _cache.GetAsync<object>(cacheKey, ct);
        if (cached is null)
            return BucketCacheComparison.Unavailable;

        try
        {
            var cacheEntry = JsonSerializer.SerializeToElement(cached);
            if (!TryGetPropertyIgnoreCase(cacheEntry, "jsonPayload", out var payload)
                || payload.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(payload.GetString()))
                return BucketCacheComparison.Unavailable;
            using var document = JsonDocument.Parse(payload.GetString()!);
            var root = document.RootElement;
            if (!TryGetPropertyIgnoreCase(root, "colors", out var rows)
                || rows.ValueKind != JsonValueKind.Array)
                return BucketCacheComparison.Unavailable;

            var actual = rows.EnumerateArray()
                .Select(row => new OperationsAnalyticsBucketReconciliation.BucketValue(
                    Application.Analytics.ColorIdentityPolicy.Key(
                        TryGetPropertyIgnoreCase(row, "boja", out var color) && color.ValueKind == JsonValueKind.String
                            ? color.GetString()
                            : null),
                    GetDecimal(row, "ukupanPromet"),
                    GetInt32(row, "ukupnaKolicina")))
                .ToArray();
            var expected = await OperationsAnalyticsRawFactOracle.QueryColorBucketsAsync(connection, filters, ct);
            var expectedValues = expected.Select(bucket => new OperationsAnalyticsBucketReconciliation.BucketValue(
                bucket.DimensionKey,
                bucket.Revenue,
                bucket.Units));
            return new BucketCacheComparison(
                OperationsAnalyticsBucketReconciliation.Compare("color_bucket", expectedValues, actual, _options.RevenueToleranceRsd),
                true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Skipping color bucket comparison for cache key {CacheKey}.", cacheKey);
            return BucketCacheComparison.Unavailable;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static decimal GetDecimal(JsonElement element, string property)
    {
        if (!TryGetPropertyIgnoreCase(element, property, out var value) || value.ValueKind != JsonValueKind.Number)
            throw new InvalidOperationException($"Cached integrity bucket is missing numeric field '{property}'.");
        return value.GetDecimal();
    }

    private static int GetInt32(JsonElement element, string property)
    {
        if (!TryGetPropertyIgnoreCase(element, property, out var value) || !value.TryGetInt32(out var result))
            throw new InvalidOperationException($"Cached integrity bucket is missing integer field '{property}'.");
        return result;
    }

    private sealed record BucketCacheComparison(
        IReadOnlyList<OperationsAnalyticsIntegrityProbeDelta> Deltas,
        bool Compared)
    {
        public static BucketCacheComparison Unavailable { get; } = new(
            Array.Empty<OperationsAnalyticsIntegrityProbeDelta>(),
            false);
    }

    private async Task<BucketCacheComparison> TryCompareCachedDailyBucketsAsync(
        NpgsqlConnection connection,
        OperationsAnalyticsRawFactOracle.Filters filters,
        CancellationToken ct)
    {
        var oracleRows = await OperationsAnalyticsRawFactOracle.QueryDailyBucketsAsync(connection, filters, ct);
        var stores = oracleRows.Select(row => row.StoreId).Distinct().ToArray();
        if (stores.Length > 100)
            return BucketCacheComparison.Unavailable;

        var fromDate = filters.FromUtc;
        var inclusiveToDate = filters.ToUtc.AddDays(-1);
        var allStoreCacheKey = AnalyticsCacheKeys.DailySales(
            fromDate,
            inclusiveToDate,
            storeId: null,
            supplierId: null,
            filters.DataScope,
            topN: 15);
        var deltas = new List<OperationsAnalyticsIntegrityProbeDelta>();
        var compared = await CompareDailyCacheAsync(
            allStoreCacheKey,
            oracleRows,
            "all",
            deltas,
            ct);
        if (!compared)
            return new BucketCacheComparison(deltas, false);

        if (stores.Any(storeId => !storeId.HasValue))
            return new BucketCacheComparison(deltas, false);

        foreach (var storeId in stores.Where(storeId => storeId.HasValue).Select(storeId => storeId!.Value))
        {
            var cacheKey = AnalyticsCacheKeys.DailySales(
                fromDate,
                inclusiveToDate,
                storeId,
                supplierId: null,
                filters.DataScope,
                topN: 15);
            var storeRows = oracleRows.Where(row => row.StoreId == storeId).ToArray();
            if (!await CompareDailyCacheAsync(
                    cacheKey,
                    storeRows,
                    storeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    deltas,
                    ct))
                return new BucketCacheComparison(deltas, false);
        }

        return new BucketCacheComparison(deltas, true);
    }

    private async Task<bool> CompareDailyCacheAsync(
        string cacheKey,
        IReadOnlyList<OperationsAnalyticsRawFactOracle.DailyBucket> oracleRows,
        string storeFilter,
        List<OperationsAnalyticsIntegrityProbeDelta> deltas,
        CancellationToken ct)
    {
        var cached = await _cache.GetAsync<object>(cacheKey, ct);
        if (cached is null)
            return false;

        try
        {
            var root = JsonSerializer.SerializeToElement(cached);
            if (!TryGetPropertyIgnoreCase(root, "dateRows", out var rows)
                || rows.ValueKind != JsonValueKind.Array
                || !TryGetPropertyIgnoreCase(root, "metadata", out var metadata)
                || !TryGetPropertyIgnoreCase(metadata, "shiftAssignmentStatus", out var shiftStatusValue))
                return false;
            var shiftStatus = shiftStatusValue.GetString();
            var useNoTimeFallback = string.Equals(shiftStatus, "no_time_fallback", StringComparison.Ordinal);
            var knownShiftRows = oracleRows.Select(row => new
                {
                    Row = row,
                    Basis = ResolveDailyTimestampBasis(row.SourceTimestampBasis, row.DataOrigin)
                })
                .ToArray();
            var noTimestampProof = knownShiftRows.Any(item => item.Basis is null);
            var hasClassifiedShift = knownShiftRows.Any(item =>
            {
                if (item.Basis is null)
                    return false;
                var local = ResolveDailyLocalTimestamp(item.Row, item.Basis);
                return ResolveDailyShift(local.Hour) != 0;
            });
            var hasAnyKnownRows = knownShiftRows.Any(item => item.Basis is not null && item.Row.Units != 0);
            var expectedNoTimeFallback = !hasClassifiedShift && hasAnyKnownRows && !noTimestampProof;
            if (expectedNoTimeFallback != useNoTimeFallback)
                return false;

            var expectedDays = BuildExpectedDailyDays(oracleRows);
            var actualRows = rows.EnumerateArray().ToArray();
            var actualByDate = actualRows.ToDictionary(
                row => GetDate(row, "date"),
                row => row);
            var expectedTotals = expectedDays.Select(day => new OperationsAnalyticsBucketReconciliation.BucketValue(
                day.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                day.Revenue,
                day.Units));
            var actualTotals = actualRows.Select(row => new OperationsAnalyticsBucketReconciliation.BucketValue(
                GetDate(row, "date").ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                GetDecimal(row, "totalRevenue"),
                GetInt32(row, "totalItemsSold")));
            deltas.AddRange(OperationsAnalyticsBucketReconciliation.Compare(
                "daily_day_store:" + storeFilter,
                expectedTotals,
                actualTotals,
                _options.RevenueToleranceRsd));

            if (useNoTimeFallback)
                return false;

            foreach (var expected in expectedDays.Where(day => !day.HasUnknownTimestampBasis))
            {
                if (!actualByDate.TryGetValue(expected.Date, out var actual))
                    return false;
                if (!TryGetPropertyIgnoreCase(actual, "firstShiftTotalItems", out var firstShift)
                    || !TryGetPropertyIgnoreCase(actual, "secondShiftTotalItems", out var secondShift))
                    return false;
                if (firstShift.ValueKind == JsonValueKind.Null || secondShift.ValueKind == JsonValueKind.Null)
                    return false;

                deltas.AddRange(OperationsAnalyticsBucketReconciliation.Compare(
                    "daily_shift_store:" + storeFilter + ":first:" + expected.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    [new OperationsAnalyticsBucketReconciliation.BucketValue("shift:1", 0m, expected.FirstShiftUnits)],
                    [new OperationsAnalyticsBucketReconciliation.BucketValue("shift:1", 0m, firstShift.GetInt32())]));
                deltas.AddRange(OperationsAnalyticsBucketReconciliation.Compare(
                    "daily_shift_store:" + storeFilter + ":second:" + expected.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    [new OperationsAnalyticsBucketReconciliation.BucketValue("shift:2", 0m, expected.SecondShiftUnits)],
                    [new OperationsAnalyticsBucketReconciliation.BucketValue("shift:2", 0m, secondShift.GetInt32())]));
            }

            return !noTimestampProof;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Skipping daily day/shift/store comparison for cache key {CacheKey}.", cacheKey);
            return false;
        }
    }

    private IReadOnlyList<ExpectedDailyDay> BuildExpectedDailyDays(
        IReadOnlyList<OperationsAnalyticsRawFactOracle.DailyBucket> rows)
    {
        var days = new Dictionary<DateTime, ExpectedDailyDay>();
        foreach (var row in rows)
        {
            var basis = ResolveDailyTimestampBasis(row.SourceTimestampBasis, row.DataOrigin);
            var localTimestamp = basis is null
                ? DateTime.SpecifyKind(row.SaleDate.Date.AddHours(row.HourOfDay), DateTimeKind.Unspecified)
                : ResolveDailyLocalTimestamp(row, basis);
            var dayKey = DateTime.SpecifyKind(localTimestamp.Date, DateTimeKind.Utc);
            if (!days.TryGetValue(dayKey, out var day))
            {
                day = new ExpectedDailyDay(dayKey);
                days.Add(dayKey, day);
            }

            day.Revenue += row.Revenue;
            day.Units += row.Units;
            if (basis is null)
            {
                day.HasUnknownTimestampBasis = true;
                continue;
            }

            switch (ResolveDailyShift(localTimestamp.Hour))
            {
                case 1:
                    day.FirstShiftUnits += row.Units;
                    break;
                case 2:
                    day.SecondShiftUnits += row.Units;
                    break;
            }
        }

        return days.Values.ToArray();
    }

    private DateTime ResolveDailyLocalTimestamp(
        OperationsAnalyticsRawFactOracle.DailyBucket row,
        string basis)
    {
        var rawTimestamp = DateTime.SpecifyKind(
            row.SaleDate.Date.AddHours(row.HourOfDay),
            DateTimeKind.Unspecified);
        if (string.Equals(basis, "legacy_access_wall_clock", StringComparison.Ordinal))
            return rawTimestamp;

        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(rawTimestamp, DateTimeKind.Utc),
            _dailySalesTimeZone);
    }

    private static string? ResolveDailyTimestampBasis(string? explicitBasis, string? dataOrigin)
    {
        if (string.Equals(explicitBasis, "utc_instant", StringComparison.Ordinal)
            || string.Equals(explicitBasis, "legacy_access_wall_clock", StringComparison.Ordinal))
            return explicitBasis;
        if (string.Equals(dataOrigin, "access", StringComparison.OrdinalIgnoreCase))
            return "legacy_access_wall_clock";
        if (string.Equals(dataOrigin, "existing", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(dataOrigin))
            return "utc_instant";
        return null;
    }

    private static int ResolveDailyShift(int hour)
        => hour is >= 6 and < 14 ? 1 : hour is >= 14 and < 22 ? 2 : 0;

    private static TimeZoneInfo ResolveDailySalesTimeZone(string? configuredId)
    {
        if (string.IsNullOrWhiteSpace(configuredId))
            return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(configuredId.Trim());
        }
        catch (TimeZoneNotFoundException) when (string.Equals(configuredId.Trim(), "Europe/Belgrade", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static DateTime GetDate(JsonElement element, string property)
    {
        if (!TryGetPropertyIgnoreCase(element, property, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Cached daily bucket is missing date field '{property}'.");
        return value.GetDateTime().Date;
    }

    private sealed class ExpectedDailyDay(DateTime date)
    {
        public DateTime Date { get; } = date;
        public decimal Revenue { get; set; }
        public int Units { get; set; }
        public int FirstShiftUnits { get; set; }
        public int SecondShiftUnits { get; set; }
        public bool HasUnknownTimestampBasis { get; set; }
    }

    private static OperationsAnalyticsIntegrityProbeDelta BuildDelta(
        string dimension,
        decimal leftRevenue,
        decimal rightRevenue,
        int leftUnits,
        int rightUnits)
    {
        return new OperationsAnalyticsIntegrityProbeDelta(
            dimension,
            leftRevenue,
            rightRevenue,
            leftRevenue - rightRevenue,
            leftUnits,
            rightUnits,
            leftUnits - rightUnits);
    }

    private (DateTime FromUtc, DateTime ToUtc) ResolveProbeWindow(DateTime checkedAtUtc)
        => OperationsAnalyticsIntegrityProbeWindow.Resolve(
            checkedAtUtc,
            Math.Min(
                Math.Max(1, _options.ProbeLookbackDays),
                Math.Max(1, _options.MaxProbeWindowDays)));

    private static string BuildEvidenceId(string trigger)
        => $"{trigger}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
}
