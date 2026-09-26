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
    Task MarkUnverifiedAsync(string trigger, string summary, CancellationToken ct = default);
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

    public OperationsAnalyticsIntegrityService(
        TrendplusDbContext db,
        IAnalyticsCacheService cache,
        OperationsAnalyticsIntegrityRegistry registry,
        IOptions<OperationsAnalyticsIntegrityOptions> options,
        ILogger<OperationsAnalyticsIntegrityService> logger)
    {
        _db = db;
        _cache = cache;
        _registry = registry;
        _options = options.Value;
        _logger = logger;
    }

    public void MarkUnverified(string trigger, string summary)
    {
        _registry.MarkUnverified(trigger, summary);
        _logger.LogInformation(
            "Operations analytics integrity marked unverified. Trigger={Trigger}",
            trigger);
    }

    public async Task MarkUnverifiedAsync(string trigger, string summary, CancellationToken ct = default)
    {
        var checkedAt = DateTime.UtcNow;
        var (fromUtc, toUtc) = ResolveProbeWindow(checkedAt);
        var filters = new OperationsAnalyticsRawFactOracle.Filters(
            fromUtc,
            toUtc,
            null,
            OperationsAnalyticsRawFactOracle.NormalizeDataScope(_options.DefaultDataScope));
        var snapshot = OperationsAnalyticsIntegritySnapshot.Unverified(
            BuildEvidenceId(trigger),
            trigger,
            summary);

        await StoreAsync(snapshot, filters, ct);
        _logger.LogInformation(
            "Operations analytics integrity unverified transition persisted. Trigger={Trigger}",
            trigger);
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
        {
            var disabled = OperationsAnalyticsIntegritySnapshot.Degraded(
                BuildEvidenceId(probeTrigger),
                probeTrigger,
                probeSummary ?? "Operations integrity probes are disabled in configuration.");
            return await StoreAsync(disabled, filters, ct);
        }

        var evidenceId = BuildEvidenceId(probeTrigger);

        try
        {
            var connectionString = _db.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return await StoreAsync(OperationsAnalyticsIntegritySnapshot.Degraded(
                    evidenceId,
                    probeTrigger,
                    probeSummary ?? "Database connection string is unavailable for integrity probes."),
                    filters,
                    ct);
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(probeCt);

            var oracleTotals = await OperationsAnalyticsRawFactOracle.QueryTotalsAsync(connection, filters, probeCt);
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

            OperationsAnalyticsIntegritySnapshot next;
            if (materialDrift)
            {
                next = new OperationsAnalyticsIntegritySnapshot(
                    OperationsAnalyticsIntegrityStates.DriftDetected,
                    evidenceId,
                    checkedAt,
                    null,
                    probeTrigger,
                    probeSummary ?? "Bounded Supplier/Shoe Type probe detected unexplained quantity or revenue delta.",
                    deltas,
                    BlocksDecisionSignals: true);
            }
            else
            {
                next = new OperationsAnalyticsIntegritySnapshot(
                    OperationsAnalyticsIntegrityStates.Verified,
                    evidenceId,
                    checkedAt,
                    checkedAt,
                    probeTrigger,
                    probeSummary ?? "Bounded Supplier/Shoe Type probe reconciled live aggregates, raw-fact oracle and cache lane.",
                    deltas,
                    BlocksDecisionSignals: false);
            }

            return await StoreAsync(next, filters, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Operations analytics integrity probe timed out after {TimeoutSeconds} seconds. Trigger={Trigger}",
                Math.Max(1, _options.ProbeTimeoutSeconds),
                probeTrigger);
            return await StoreAsync(OperationsAnalyticsIntegritySnapshot.Degraded(
                evidenceId,
                probeTrigger,
                probeSummary ?? "Operations integrity probe timed out; decision signals remain fail-closed until a successful check."),
                filters,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Operations analytics integrity probe failed.");
            return await StoreAsync(OperationsAnalyticsIntegritySnapshot.Degraded(
                evidenceId,
                probeTrigger,
                probeSummary ?? "Operations integrity probe failed; decision signals remain fail-closed until a successful check."),
                filters,
                ct);
        }
    }

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
        => OperationsAnalyticsIntegrityProbeWindow.Resolve(checkedAtUtc, _options.ProbeLookbackDays);

    private static string BuildEvidenceId(string trigger)
        => $"{trigger}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
}
