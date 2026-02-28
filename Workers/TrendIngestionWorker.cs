using Application.Analytics.Services;
using Application.Artikli.Common.Interfaces;
using Domain.Model.Analytics;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers;

/// <summary>
/// Daily background worker that:
///   1. Calls the Python trend_engine FastAPI  GET /generate-trends
///   2. Upserts results into TrendProductSnapshots
///   3. Computes momentum (today vs yesterday) → TrendProductMomentums
///   4. Computes per-market TrendplusIndex       → TrendplusIndexRecords
///
/// Schedule: runs once per day at TrendIngestionOptions.RunAtHourUtc (UTC).
/// </summary>
public sealed class TrendIngestionWorker : BackgroundService
{
    private const string WorkerName = "TrendIngestionWorker";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<TrendIngestionWorker> _logger;
    private readonly WorkerHealthService _healthService;
    private readonly WorkerRuntimeControlService _controlService;
    private readonly TrendIngestionOptions _options;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public TrendIngestionWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpFactory,
        ILogger<TrendIngestionWorker> logger,
        WorkerHealthService healthService,
        WorkerRuntimeControlService controlService,
        IOptions<TrendIngestionOptions> options)
    {
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _logger = logger;
        _healthService = healthService;
        _controlService = controlService;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("📈 {Worker} starting.", WorkerName);
        _healthService.ReportRunning(WorkerName, "Starting up…");

        // Give the app time to fully boot before first check
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        DateOnly? lastRunDate = null;
        var pauseCheckInterval = TimeSpan.FromSeconds(10);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_options.Enabled || !_controlService.IsEnabled)
            {
                _healthService.ReportStopped(WorkerName, "Disabled.");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { break; }
                continue;
            }

            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);
            var shouldRun = nowUtc.Hour >= _options.RunAtHourUtc
                            && lastRunDate != todayUtc;

            if (!shouldRun)
            {
                try { await Task.Delay(pauseCheckInterval, stoppingToken); } catch (OperationCanceledException) { break; }
                continue;
            }

            lastRunDate = todayUtc;
            _logger.LogInformation("📈 {Worker} — starting daily ingestion for {Date}", WorkerName, todayUtc);

            try
            {
                await RunIngestionAsync(todayUtc, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "📈 {Worker} — ingestion failed.", WorkerName);
                _healthService.ReportError(WorkerName, ex);
            }

            // Wait until next check cycle
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("📈 {Worker} stopped.", WorkerName);
    }

    // ──────────────────────────────────────────────────────────────────────────

    private async Task RunIngestionAsync(DateOnly date, CancellationToken ct)
    {
        _healthService.ReportRunning(WorkerName, $"Calling Python API for {date}…");

        // ── Step 1: call Python trend_engine FastAPI ──────────────────────────
        var items = await FetchTrendItemsAsync(ct);
        if (items.Count == 0)
        {
            _logger.LogWarning("📈 {Worker} — Python API returned 0 items, skipping DB writes.", WorkerName);
            _healthService.ReportHealthy(WorkerName, "Python API returned 0 items.");
            return;
        }

        _logger.LogInformation("📈 {Worker} — received {Count} trend items.", WorkerName, items.Count);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAnalyticsDbContext>();

        // ── Step 2: upsert TrendProductSnapshots ─────────────────────────────
        _healthService.ReportRunning(WorkerName, "Writing snapshots…");
        await UpsertSnapshotsAsync(db, date, items, ct);

        // ── Step 3: compute + write momentum ─────────────────────────────────
        _healthService.ReportRunning(WorkerName, "Computing momentum…");
        await UpsertMomentumAsync(db, date, ct);

        // ── Step 4: compute + write Trendplus Index per market ────────────────
        _healthService.ReportRunning(WorkerName, "Computing Trendplus Index…");
        await UpsertTrendIndexAsync(db, date, ct);

        _logger.LogInformation("📈 {Worker} — ingestion complete for {Date}.", WorkerName, date);
        _healthService.ReportHealthy(WorkerName, $"Ingestion OK for {date}. {items.Count} products.");
    }

    // ── Python API call ───────────────────────────────────────────────────────

    private async Task<List<TrendItemDto>> FetchTrendItemsAsync(CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("TrendEngine");

        var marketsQuery = string.Join("&", _options.Markets.Select(m => $"markets={Uri.EscapeDataString(m)}"));
        var url = $"/generate-trends?pages={_options.Pages}&{marketsQuery}&top={_options.Top}";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.PythonCallTimeoutSeconds));

        try
        {
            var response = await client.GetAsync(url, cts.Token);
            response.EnsureSuccessStatusCode();

            var envelope = await response.Content.ReadFromJsonAsync<TrendEnvelopeDto>(_jsonOpts, ct);
            return envelope?.Items ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "📈 {Worker} — Python API unreachable at {Url}; skipping run.", WorkerName, _options.PythonApiBaseUrl + url);
            return [];
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("📈 {Worker} — Python API call timed out after {Timeout}s.", WorkerName, _options.PythonCallTimeoutSeconds);
            return [];
        }
    }

    // ── Snapshots ─────────────────────────────────────────────────────────────

    private static async Task UpsertSnapshotsAsync(
        IAnalyticsDbContext db,
        DateOnly date,
        List<TrendItemDto> items,
        CancellationToken ct)
    {
        // Delete today's existing snapshots (idempotent re-run)
        var existing = await db.TrendProductSnapshots
            .Where(s => s.SnapshotDate == date)
            .ToListAsync(ct);

        if (existing.Count > 0)
            db.TrendProductSnapshots.RemoveRange(existing);

        var snapshots = items.Select((item, idx) => new TrendProductSnapshot
        {
            SnapshotDate   = date,
            CanonicalKey   = item.CanonicalKey,
            ProductName    = item.Name,
            Brand          = item.Brand,
            Category       = null,          // not returned by current Python API
            Market         = item.Markets.Count > 0 ? string.Join(",", item.Markets) : null,
            Score          = item.FinalScore,
            RankGlobal     = item.Rank > 0 ? item.Rank : idx + 1,
            SocialScore    = null,
            SourceCount    = item.TotalOccurrences,
            UniqueSources  = item.UniqueSources,
            CreatedAt      = DateTimeOffset.UtcNow,
        }).ToList();

        await db.TrendProductSnapshots.AddRangeAsync(snapshots, ct);
        await db.SaveChangesAsync(ct);
    }

    // ── Momentum ──────────────────────────────────────────────────────────────

    private static async Task UpsertMomentumAsync(
        IAnalyticsDbContext db,
        DateOnly today,
        CancellationToken ct)
    {
        var yesterday = today.AddDays(-1);

        var todaySnaps = await db.TrendProductSnapshots
            .Where(s => s.SnapshotDate == today)
            .ToListAsync(ct);

        var yesterdayByKey = await db.TrendProductSnapshots
            .Where(s => s.SnapshotDate == yesterday)
            .ToDictionaryAsync(s => s.CanonicalKey, ct);

        // Delete today's existing momentum rows (idempotent)
        var existingMomentum = await db.TrendProductMomentums
            .Where(m => m.SnapshotDate == today)
            .ToListAsync(ct);

        if (existingMomentum.Count > 0)
            db.TrendProductMomentums.RemoveRange(existingMomentum);

        var momentums = new List<TrendProductMomentum>(todaySnaps.Count);
        foreach (var snap in todaySnaps)
        {
            TrendProductMomentum m;
            if (yesterdayByKey.TryGetValue(snap.CanonicalKey, out var prev))
            {
                var score = TrendScoringService.ComputeMomentum(
                    snap.Score, prev.Score,
                    snap.RankGlobal, prev.RankGlobal);

                m = new TrendProductMomentum
                {
                    SnapshotDate  = today,
                    CanonicalKey  = snap.CanonicalKey,
                    MomentumScore = score,
                    ScoreDelta    = snap.Score - prev.Score,
                    RankDelta     = prev.RankGlobal - snap.RankGlobal, // positive = climbed
                    IsNewEntry    = false,
                    CreatedAt     = DateTimeOffset.UtcNow,
                };
            }
            else
            {
                // Brand-new entry — treat as maximum upward momentum
                m = new TrendProductMomentum
                {
                    SnapshotDate  = today,
                    CanonicalKey  = snap.CanonicalKey,
                    MomentumScore = 1.0,
                    ScoreDelta    = snap.Score,
                    RankDelta     = 0,
                    IsNewEntry    = true,
                    CreatedAt     = DateTimeOffset.UtcNow,
                };
            }

            momentums.Add(m);
        }

        await db.TrendProductMomentums.AddRangeAsync(momentums, ct);
        await db.SaveChangesAsync(ct);
    }

    // ── Trendplus Index ───────────────────────────────────────────────────────

    private static async Task UpsertTrendIndexAsync(
        IAnalyticsDbContext db,
        DateOnly today,
        CancellationToken ct)
    {
        var snaps = await db.TrendProductSnapshots
            .Where(s => s.SnapshotDate == today)
            .ToListAsync(ct);

        var momentumByKey = await db.TrendProductMomentums
            .Where(m => m.SnapshotDate == today)
            .ToDictionaryAsync(m => m.CanonicalKey, ct);

        // Delete today's existing index records (idempotent)
        var existing = await db.TrendplusIndexRecords
            .Where(r => r.SnapshotDate == today)
            .ToListAsync(ct);

        if (existing.Count > 0)
            db.TrendplusIndexRecords.RemoveRange(existing);

        var records = new List<TrendplusIndexRecord>();

        // Global index
        records.Add(BuildIndexRecord(today, "global", "all", snaps, momentumByKey));

        // Per-market index
        var byMarket = snaps
            .Where(s => !string.IsNullOrEmpty(s.Market))
            .SelectMany(s => s.Market!.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => (Market: m.Trim(), Snap: s)))
            .GroupBy(x => x.Market);

        foreach (var marketGroup in byMarket)
        {
            var marketSnaps = marketGroup.Select(x => x.Snap).ToList();
            records.Add(BuildIndexRecord(today, "market", marketGroup.Key, marketSnaps, momentumByKey));
        }

        // Per-brand index (only brands with ≥5 products)
        var byBrand = snaps
            .GroupBy(s => s.Brand.ToLowerInvariant())
            .Where(g => g.Count() >= 5);

        foreach (var brandGroup in byBrand)
        {
            records.Add(BuildIndexRecord(today, "brand", brandGroup.Key, brandGroup.ToList(), momentumByKey));
        }

        await db.TrendplusIndexRecords.AddRangeAsync(records, ct);
        await db.SaveChangesAsync(ct);
    }

    private static TrendplusIndexRecord BuildIndexRecord(
        DateOnly date,
        string scopeType,
        string scopeValue,
        List<TrendProductSnapshot> snaps,
        Dictionary<string, TrendProductMomentum> momentumByKey)
    {
        var scores = snaps.Select(s => s.Score).ToList();
        var momentums = snaps.Select(s =>
            momentumByKey.TryGetValue(s.CanonicalKey, out var m) ? m.MomentumScore : 0.0).ToList();

        var avgSocial = snaps
            .Where(s => s.SocialScore.HasValue)
            .Select(s => s.SocialScore!.Value / 100.0)
            .DefaultIfEmpty(0.0)
            .Average();

        var (indexVal, baseVal, momentumVal, socialVal) =
            TrendScoringService.ComputeExtendedTrendIndex(scores, momentums, avgSocial);

        return new TrendplusIndexRecord
        {
            SnapshotDate       = date,
            ScopeType          = scopeType,
            ScopeValue         = scopeValue,
            IndexValue         = Math.Round(indexVal,    2),
            BaseComponent      = Math.Round(baseVal,     2),
            MomentumComponent  = Math.Round(momentumVal, 2),
            SocialComponent    = Math.Round(socialVal,   2),
            CreatedAt          = DateTimeOffset.UtcNow,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DTOs for Python API response deserialization
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class TrendEnvelopeDto
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("items")]
        public List<TrendItemDto> Items { get; set; } = [];
    }

    private sealed class TrendItemDto
    {
        [JsonPropertyName("canonical_key")]
        public string CanonicalKey { get; set; } = string.Empty;

        [JsonPropertyName("brand")]
        public string Brand { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("markets")]
        public List<string> Markets { get; set; } = [];

        [JsonPropertyName("sources")]
        public List<string> Sources { get; set; } = [];

        [JsonPropertyName("total_occurrences")]
        public int TotalOccurrences { get; set; }

        [JsonPropertyName("unique_sources")]
        public int UniqueSources { get; set; }

        [JsonPropertyName("unique_markets")]
        public int UniqueMarkets { get; set; }

        [JsonPropertyName("base_score")]
        public double BaseScore { get; set; }

        [JsonPropertyName("final_score")]
        public double FinalScore { get; set; }

        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("source_counts")]
        public Dictionary<string, int> SourceCounts { get; set; } = [];

        [JsonPropertyName("market_counts")]
        public Dictionary<string, int> MarketCounts { get; set; } = [];
    }
}
