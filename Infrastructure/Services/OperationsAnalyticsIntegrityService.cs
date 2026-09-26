using System.Text.Json;
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
    Task<OperationsAnalyticsIntegritySnapshot> RunBoundedProbeAsync(CancellationToken ct = default);
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

    public async Task<OperationsAnalyticsIntegritySnapshot> RunBoundedProbeAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            var disabled = OperationsAnalyticsIntegritySnapshot.Degraded(
                BuildEvidenceId("disabled"),
                "disabled",
                "Operations integrity probes are disabled in configuration.");
            _registry.Set(disabled);
            return disabled;
        }

        var evidenceId = BuildEvidenceId("probe");
        var checkedAt = DateTime.UtcNow;
        var (fromUtc, toUtc) = ResolveProbeWindow(checkedAt);
        var dataScope = OperationsAnalyticsRawFactOracle.NormalizeDataScope(_options.DefaultDataScope);
        var filters = new OperationsAnalyticsRawFactOracle.Filters(fromUtc, toUtc, null, dataScope);

        try
        {
            var connectionString = _db.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return Store(OperationsAnalyticsIntegritySnapshot.Degraded(
                    evidenceId,
                    "missing_connection",
                    "Database connection string is unavailable for integrity probes."));
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);

            var oracleTotals = await OperationsAnalyticsRawFactOracle.QueryTotalsAsync(connection, filters, ct);
            var liveTotals = await ReadLiveAggregatedTotalsAsync(filters, ct);
            var deltas = new List<OperationsAnalyticsIntegrityProbeDelta>
            {
                BuildDelta("supplier_shoe_live_aggregate", liveTotals.Revenue, oracleTotals.TotalRevenue, liveTotals.Units, oracleTotals.TotalUnits)
            };

            var cacheDelta = await TryCompareCachedSupplierTotalsAsync(fromUtc, toUtc, dataScope, liveTotals, ct);
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
                    "bounded_probe",
                    "Bounded Supplier/Shoe Type probe detected unexplained quantity or revenue delta.",
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
                    "bounded_probe",
                    "Bounded Supplier/Shoe Type probe reconciled live aggregates, raw-fact oracle and cache lane.",
                    deltas,
                    BlocksDecisionSignals: false);
            }

            return Store(next);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Operations analytics integrity probe failed.");
            return Store(OperationsAnalyticsIntegritySnapshot.Degraded(
                evidenceId,
                "probe_failure",
                "Operations integrity probe failed; decision signals remain fail-closed until a successful check."));
        }
    }

    private OperationsAnalyticsIntegritySnapshot Store(OperationsAnalyticsIntegritySnapshot snapshot)
    {
        _registry.Set(snapshot);
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
