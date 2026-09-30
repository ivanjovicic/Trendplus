using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Application.Analytics;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services.Caching;
using Microsoft.EntityFrameworkCore;
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

    public OperationsAnalyticsIntegrityService(
        TrendplusDbContext db,
        IAnalyticsCacheService cache,
        OperationsAnalyticsIntegrityRegistry registry,
        IOptions<OperationsAnalyticsIntegrityOptions> options,
        ILogger<OperationsAnalyticsIntegrityService> logger,
        IEnumerable<IOperationsAnalyticsIntegrityFamilyProbe>? familyProbes = null)
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

            var cacheDelta = await TryCompareCachedSupplierTotalsAsync(fromUtc, toUtc, dataScope, liveTotals, probeCt);
            if (cacheDelta is not null)
                deltas.Add(cacheDelta);

            var materialDrift = deltas.Any(delta =>
                Math.Abs(delta.RevenueDelta) > _options.RevenueToleranceRsd
                || delta.UnitsDelta != 0);

            var supplierFamily = OperationsAnalyticsIntegrityFamilies.SupplierShoeType;
            var supplierGeneration = _registry.GetGeneration(supplierFamily);
            var supplierSnapshot = CreateSnapshot(
                supplierFamily,
                materialDrift
                    ? OperationsAnalyticsIntegrityStates.DriftDetected
                    : OperationsAnalyticsIntegrityStates.Verified,
                BuildEvidenceId($"{probeTrigger}:{supplierFamily}"),
                probeTrigger,
                probeSummary ?? (materialDrift
                    ? "Bounded Supplier/Shoe Type probe detected unexplained quantity or revenue delta."
                    : "Bounded Supplier/Shoe Type probe reconciled live aggregates, raw-fact oracle and cache lane."),
                filters,
                checkedAt,
                BuildContextFingerprint(supplierFamily, supplierGeneration, filters),
                supplierGeneration,
                deltas,
                materialDrift,
                materialDrift ? null : checkedAt,
                oracleTotals.SaleLineCount);
            await StoreAsync(supplierSnapshot, filters, ct);

            var salesFamily = OperationsAnalyticsIntegrityFamilies.SalesDashboard;
            var salesGeneration = _registry.GetGeneration(salesFamily);
            var salesSnapshot = CreateSnapshot(
                salesFamily,
                materialDrift
                    ? OperationsAnalyticsIntegrityStates.DriftDetected
                    : OperationsAnalyticsIntegrityStates.Verified,
                BuildEvidenceId($"{probeTrigger}:{salesFamily}"),
                probeTrigger,
                probeSummary ?? (materialDrift
                    ? "Dashboard sales totals have a non-zero bounded reconciliation delta."
                    : "Dashboard sales totals reconciled to the independent raw-fact oracle."),
                filters,
                checkedAt,
                BuildContextFingerprint(salesFamily, salesGeneration, filters),
                salesGeneration,
                [BuildDelta("dashboard_sales_totals", liveTotals.Revenue, oracleTotals.TotalRevenue, liveTotals.Units, oracleTotals.TotalUnits)],
                materialDrift,
                materialDrift ? null : checkedAt,
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
                    null);
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
        var importedOnly = filters.DataScope == "imported";
        var existingOnly = filters.DataScope == "existing";

        var rows = await (
            from ps in _db.ProdajaStavke.AsNoTracking()
            join pz in _db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
            join a in _db.Artikli.AsNoTracking() on ps.IdArtikal equals a.Id
            where pz.DatumProdaje >= filters.FromUtc
                  && pz.DatumProdaje <= filters.ToUtc
                  && (!filters.StoreId.HasValue || pz.IDObjekat == filters.StoreId.Value)
                  && (!importedOnly || a.DataOrigin == "access")
                  && (!existingOnly || a.DataOrigin == "existing" || a.DataOrigin == null || a.DataOrigin == "")
            select new { ps.Kolicina, Revenue = ps.Kolicina * ps.Cena })
            .ToListAsync(ct);

        return (rows.Sum(x => x.Revenue), rows.Sum(x => x.Kolicina));
    }

    private async Task<OperationsAnalyticsIntegrityProbeDelta?> TryCompareCachedSupplierTotalsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string dataScope,
        (decimal Revenue, int Units) liveTotals,
        CancellationToken ct)
    {
        var cacheKey = AnalyticsCacheKeys.SupplierSalesStats(fromUtc, toUtc, storeId: null, sezonaId: null, dataScope, activeSnapshotBatchId: null);
        var cached = await _cache.GetAsync<AnalyticsJsonCachePayload>(cacheKey, ct);
        if (cached is null || string.IsNullOrWhiteSpace(cached.Json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(cached.Json);
            var totals = document.RootElement.GetProperty("totals");
            var cachedRevenue = totals.GetProperty("ukupanPromet").GetDecimal();
            var cachedUnits = totals.GetProperty("ukupnaKolicina").GetInt32();
            return BuildDelta("supplier_sales_cache_lane", cachedRevenue, liveTotals.Revenue, cachedUnits, liveTotals.Units);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Skipping cache-lane integrity comparison for key {CacheKey}.", cacheKey);
            return null;
        }
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
