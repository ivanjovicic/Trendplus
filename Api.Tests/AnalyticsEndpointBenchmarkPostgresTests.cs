using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using Application.Config;
using Infrastructure.Configuration;
using Infrastructure.DbContexts;
using Infrastructure.Services;
using Infrastructure.Services.Caching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Reproducible, non-gating HTTP baselines for the two remaining Operations performance gaps.
/// The test proves the handler and payload shape execute on disposable PostgreSQL, then records
/// warm samples. It intentionally has no latency pass/fail threshold.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AnalyticsEndpointBenchmarkPostgresTests : IClassFixture<PostgresContainerFixture>
{
    private const int SampleCount = 10;
    private const int ModerateSaleLineCount = 600;
    private const string ModerateFromDate = "2026-07-01";
    private const string ModerateToDate = "2026-07-07";
    private const string EmptyFromDate = "2026-12-01";
    private const string EmptyToDate = "2026-12-07";

    private readonly PostgresContainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AnalyticsEndpointBenchmarkPostgresTests(
        PostgresContainerFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [PostgresRequiredFact(DisplayName = "RQ647 records non-gating Supplier and ShoeType endpoint baselines")]
    public async Task SupplierAndShoeTypeEndpoints_ProduceReproducibleBaselineReport()
    {
        Assert.True(_fixture.IsAvailable, "RQ647 requires its disposable PostgreSQL Testcontainer.");
        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"rq647_{Guid.NewGuid():N}");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        await SupplierSalesStatsEndpointPostgresParityTests.CreateFixtureAsync(connectionString!);
        await AddModerateFixtureSalesAsync(connectionString!);
        var fixtureRows = await CountFixtureRowsAsync(connectionString!);
        Assert.True(
            fixtureRows["salesLines"] >= ModerateSaleLineCount,
            $"RQ647 moderate fixture requires at least {ModerateSaleLineCount} sale lines.");

        var registry = new OperationsAnalyticsIntegrityRegistry();
        SupplierSalesStatsEndpointPostgresParityTests.SetBoundSupplierEvidence(
            registry,
            OperationsAnalyticsIntegrityStates.Verified,
            "rq647-benchmark-fixture",
            blocksDecisionSignals: false,
            trigger: "rq647_fixture_verified",
            summary: "RQ647 uses the deterministic all-routes PostgreSQL fixture with a 600-sale-line moderate expansion.");

        var cache = new DisabledAnalyticsCacheService();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var commandRecorder = new BenchmarkDbCommandRecorder();
        await using var factory = new BenchmarkEndpointFactory(
            connectionString!,
            cache,
            registry,
            memoryCache,
            commandRecorder);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var cases = new[]
        {
            new BenchmarkCase(
                "supplier-moderate",
                "supplier",
                "moderate",
                $"/api/analytics/supplier-sales-stats?fromDate={ModerateFromDate}&toDate={ModerateToDate}&dataScope=all"),
            new BenchmarkCase(
                "supplier-empty",
                "supplier",
                "empty",
                $"/api/analytics/supplier-sales-stats?fromDate={EmptyFromDate}&toDate={EmptyToDate}&dataScope=all"),
            new BenchmarkCase(
                "shoetype-moderate",
                "shoe-type",
                "moderate",
                $"/api/analytics/shoe-type-sales-stats?fromDate={ModerateFromDate}&toDate={ModerateToDate}&dataScope=all"),
            new BenchmarkCase(
                "shoetype-empty",
                "shoe-type",
                "empty",
                $"/api/analytics/shoe-type-sales-stats?fromDate={EmptyFromDate}&toDate={EmptyToDate}&dataScope=all")
        };

        var reports = new List<BenchmarkCaseReport>(cases.Length);
        foreach (var benchmarkCase in cases)
            reports.Add(await MeasureCaseAsync(client, benchmarkCase, factory.ClearMemoryCache, commandRecorder));

        var report = new BenchmarkReport(
            "rq647-analytics-endpoint-baseline-v1",
            DateTime.UtcNow,
            "PostgreSQL Testcontainer; operations-analytics-all-routes-seed.sql plus deterministic 600-sale-line moderate expansion",
            "disabled (IAnalyticsCacheService and per-request IMemoryCache clear)",
            "Stopwatch.GetTimestamp via HttpClient; one unmeasured warmup; sequential concurrency=1; EF command interceptor records query count and duration",
            SampleCount,
            new BenchmarkEnvironment(
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription,
                Environment.ProcessorCount,
                1),
            fixtureRows,
            reports);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var reportPath = Environment.GetEnvironmentVariable("TRENDPLUS_BENCHMARK_REPORT_PATH");
        reportPath = string.IsNullOrWhiteSpace(reportPath)
            ? Path.Combine("TestResults", "rq647-analytics-endpoint-benchmark.json")
            : reportPath;
        var reportDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory))
            Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(reportPath, json);

        using var reportDocument = JsonDocument.Parse(json);
        Assert.Equal("rq647-analytics-endpoint-baseline-v1", reportDocument.RootElement.GetProperty("reportId").GetString());
        Assert.Equal(4, reportDocument.RootElement.GetProperty("cases").GetArrayLength());
        Assert.False(reportDocument.RootElement.GetProperty("environment").TryGetProperty("machineName", out _));
        Assert.True(
            reportDocument.RootElement.GetProperty("fixtureRows").GetProperty("salesLines").GetInt32() >= ModerateSaleLineCount);
        Assert.All(reports, result =>
        {
            Assert.Equal(SampleCount, result.SamplesMs.Count);
            Assert.True(result.StatusCodes.All(status => status == (int)HttpStatusCode.OK));
            Assert.True(result.ResultCount >= 0);
            Assert.True(result.P95Ms >= result.P50Ms);
        });

        _output.WriteLine("RQ647_BENCHMARK_REPORT_PATH={0}", reportPath);
        _output.WriteLine("RQ647_BENCHMARK_REPORT_JSON={0}", json);
    }

    private static async Task<BenchmarkCaseReport> MeasureCaseAsync(
        HttpClient client,
        BenchmarkCase benchmarkCase,
        Action clearMemoryCache,
        BenchmarkDbCommandRecorder commandRecorder)
    {
        // Warm the host and EF query pipeline before collecting samples. This keeps setup and
        // container startup out of the baseline while still proving the real HTTP route executes.
        var warmup = await ExecuteRequestAsync(client, benchmarkCase, clearMemoryCache, commandRecorder);
        Assert.Equal(0, warmup.StatusCode - (int)HttpStatusCode.OK);

        var baseline = await ExecuteRequestAsync(client, benchmarkCase, clearMemoryCache, commandRecorder);
        var samples = new List<double>(SampleCount);
        var commandCounts = new List<int>(SampleCount);
        var commandDurationsMs = new List<double>(SampleCount);
        var statusCodes = new List<int>(SampleCount + 1) { baseline.StatusCode };
        for (var index = 0; index < SampleCount; index++)
        {
            var sample = await ExecuteRequestAsync(client, benchmarkCase, clearMemoryCache, commandRecorder);
            samples.Add(sample.ElapsedMs);
            commandCounts.Add(sample.DbCommandCount);
            commandDurationsMs.Add(sample.DbCommandDurationMs);
            statusCodes.Add(sample.StatusCode);
        }

        var ordered = samples.OrderBy(value => value).ToArray();
        var p50 = Percentile(ordered, 0.50);
        var p95 = Percentile(ordered, 0.95);
        var commandDurationP95 = Percentile(commandDurationsMs.OrderBy(value => value).ToArray(), 0.95);
        var expectedModerate = benchmarkCase.Population == "moderate";
        if (expectedModerate)
            Assert.True(baseline.ResultCount > 0, $"{benchmarkCase.Name} must exercise populated data.");
        else
            Assert.Equal(0, baseline.ResultCount);

        return new BenchmarkCaseReport(
            benchmarkCase.Name,
            benchmarkCase.Endpoint,
            benchmarkCase.Population,
            benchmarkCase.RequestPath,
            true,
            baseline.ElapsedMs,
            samples,
            p50,
            p95,
            Math.Round(p50 - baseline.ElapsedMs, 3),
            Math.Round(p95 - baseline.ElapsedMs, 3),
            baseline.DbCommandCount,
            Math.Round(baseline.DbCommandDurationMs, 3),
            commandCounts,
            commandDurationP95,
            statusCodes,
            baseline.ResultCount);
    }

    private static async Task<MeasuredResponse> ExecuteRequestAsync(
        HttpClient client,
        BenchmarkCase benchmarkCase,
        Action clearMemoryCache,
        BenchmarkDbCommandRecorder commandRecorder)
    {
        clearMemoryCache();
        commandRecorder.Reset();
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync(benchmarkCase.RequestPath);
        stopwatch.Stop();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("meta", out var meta), $"{benchmarkCase.Name} has no analytics meta: {body}");
        Assert.True(meta.GetProperty("success").GetBoolean(), $"{benchmarkCase.Name} was not a successful response: {body}");
        var rowsProperty = benchmarkCase.Endpoint == "supplier" ? "suppliers" : "shoeTypes";
        Assert.True(root.TryGetProperty(rowsProperty, out var rows), $"{benchmarkCase.Name} has no {rowsProperty} array: {body}");
        Assert.Equal(JsonValueKind.Array, rows.ValueKind);
        return new MeasuredResponse(
            stopwatch.Elapsed.TotalMilliseconds,
            (int)response.StatusCode,
            rows.GetArrayLength(),
            commandRecorder.Snapshot());
    }

    private static async Task<IReadOnlyDictionary<string, int>> CountFixtureRowsAsync(string connectionString)
    {
        await using var db = new TrendplusDbContext(
            new DbContextOptionsBuilder<TrendplusDbContext>().UseNpgsql(connectionString).Options);
        return new Dictionary<string, int>
        {
            ["products"] = await db.Artikli.CountAsync(),
            ["suppliers"] = await db.Dobavljaci.CountAsync(),
            ["shoeTypes"] = await db.TipoviObuce.CountAsync(),
            ["salesHeaders"] = await db.ProdajaZaglavlja.CountAsync(),
            ["salesLines"] = await db.ProdajaStavke.CountAsync(),
            ["costSnapshots"] = await db.AnalyticsSaleLineCostSnapshots.CountAsync()
        };
    }

    private static async Task AddModerateFixtureSalesAsync(string connectionString)
    {
        const int idOffset = 647_000;
        const string sql = """
            INSERT INTO prodaja_zaglavlje
                (id, broj_racuna, datum_prodaje, id_objekat, korisnik_ime, data_origin)
            SELECT
                @idOffset + n,
                'RQ647-MODERATE-' || n,
                '2026-07-01T12:00:00Z'::timestamptz + ((n - 1) % 7) * interval '1 day',
                CASE WHEN n % 2 = 0 THEN 1 ELSE 2 END,
                'rq647',
                CASE WHEN n % 2 = 0 THEN 'existing' ELSE 'access' END
            FROM generate_series(1, @saleLineCount) AS n;

            INSERT INTO prodaja_stavke
                (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena,
                 supplier_id_at_sale, shoe_type_id_at_sale, attribution_basis)
            SELECT
                @idOffset + n,
                @idOffset + n,
                CASE WHEN n % 2 = 0 THEN 1 ELSE 2 END,
                1 + (n % 3),
                CASE WHEN n % 2 = 0 THEN 100 ELSE 130 END,
                CASE WHEN n % 2 = 0 THEN 50 ELSE 65 END,
                CASE WHEN n % 2 = 0 THEN 1 ELSE 2 END,
                CASE WHEN n % 2 = 0 THEN 1 ELSE 2 END,
                'sale_snapshot'
            FROM generate_series(1, @saleLineCount) AS n;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("idOffset", idOffset);
        command.Parameters.AddWithValue("saleLineCount", ModerateSaleLineCount);
        await command.ExecuteNonQueryAsync();
    }

    private static double Percentile(IReadOnlyList<double> ordered, double quantile)
    {
        var rank = Math.Max(1, (int)Math.Ceiling(quantile * ordered.Count));
        return Math.Round(ordered[rank - 1], 3);
    }

    private sealed record BenchmarkCase(string Name, string Endpoint, string Population, string RequestPath);

    private sealed record MeasuredResponse(
        double ElapsedMs,
        int StatusCode,
        int ResultCount,
        BenchmarkDbCommandSnapshot DbCommands)
    {
        public int DbCommandCount => DbCommands.Count;
        public double DbCommandDurationMs => DbCommands.DurationMs;
    }

    private sealed record BenchmarkCaseReport(
        string Name,
        string Endpoint,
        string Population,
        string RequestPath,
        bool WarmupExecuted,
        double BaselineMs,
        IReadOnlyList<double> SamplesMs,
        double P50Ms,
        double P95Ms,
        double DeltaP50VsBaselineMs,
        double DeltaP95VsBaselineMs,
        int BaselineDbCommandCount,
        double BaselineDbCommandDurationMs,
        IReadOnlyList<int> DbCommandCounts,
        double DbCommandDurationP95Ms,
        IReadOnlyList<int> StatusCodes,
        int ResultCount);

    private sealed record BenchmarkReport(
        string ReportId,
        DateTime GeneratedAtUtc,
        string Fixture,
        string CacheProvider,
        string Measurement,
        int SampleCount,
        BenchmarkEnvironment Environment,
        IReadOnlyDictionary<string, int> FixtureRows,
        IReadOnlyList<BenchmarkCaseReport> Cases);

    private sealed record BenchmarkEnvironment(
        string OsDescription,
        string Runtime,
        int LogicalProcessors,
        int Concurrency);

    private sealed record BenchmarkDbCommandSnapshot(int Count, double DurationMs);

    private sealed class BenchmarkDbCommandRecorder : DbCommandInterceptor
    {
        private int _count;
        private long _durationTicks;

        public void Reset()
        {
            Interlocked.Exchange(ref _count, 0);
            Interlocked.Exchange(ref _durationTicks, 0);
        }

        public BenchmarkDbCommandSnapshot Snapshot()
            => new(
                Volatile.Read(ref _count),
                Math.Round(TimeSpan.FromTicks(Interlocked.Read(ref _durationTicks)).TotalMilliseconds, 3));

        private void Record(CommandExecutedEventData eventData)
        {
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _durationTicks, eventData.Duration.Ticks);
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            Record(eventData);
            return result;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            Record(eventData);
            return ValueTask.FromResult(result);
        }

        public override object? ScalarExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result)
        {
            Record(eventData);
            return result;
        }

        public override ValueTask<object?> ScalarExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            object? result,
            CancellationToken cancellationToken = default)
        {
            Record(eventData);
            return ValueTask.FromResult(result);
        }

        public override int NonQueryExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result)
        {
            Record(eventData);
            return result;
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            Record(eventData);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class BenchmarkEndpointFactory : WebApplicationFactory<global::Program>
    {
        private readonly string _connectionString;
        private readonly IAnalyticsCacheService _cache;
        private readonly OperationsAnalyticsIntegrityRegistry _registry;
        private readonly MemoryCache _memoryCache;
        private readonly BenchmarkDbCommandRecorder _commandRecorder;

        public BenchmarkEndpointFactory(
            string connectionString,
            IAnalyticsCacheService cache,
            OperationsAnalyticsIntegrityRegistry registry,
            MemoryCache memoryCache,
            BenchmarkDbCommandRecorder commandRecorder)
        {
            _connectionString = connectionString;
            _cache = cache;
            _registry = registry;
            _memoryCache = memoryCache;
            _commandRecorder = commandRecorder;
        }

        public void ClearMemoryCache() => _memoryCache.Compact(1.0);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var settings = new Dictionary<string, string?>
            {
                ["Database:AutoMigrate"] = "false",
                ["StartupReadiness:GateApiTraffic"] = "false",
                ["PROCESS_TYPE"] = "web",
                ["Workers:Enabled"] = "false",
                ["Caching:Provider"] = "disabled",
                ["AnalyticsCache:Provider"] = "disabled",
                ["Analytics:UseSnapshotCost"] = "true",
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["ConnectionStrings:AnalyticsConnection"] = _connectionString,
                ["ConnectionStrings:OpenProductTrainingConnection"] = _connectionString
            };

            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<DbContextOptions<TrendplusDbContext>>();
                services.RemoveAll<TrendplusDbContext>();
                services.RemoveAll<IDbContextFactory<TrendplusDbContext>>();
                services.RemoveAll<ITrendplusDbContext>();
                services.AddDbContextFactory<TrendplusDbContext>(options => options
                    .UseNpgsql(_connectionString)
                    .AddInterceptors(_commandRecorder));
                services.AddScoped<TrendplusDbContext>(provider =>
                    provider.GetRequiredService<IDbContextFactory<TrendplusDbContext>>().CreateDbContext());
                services.AddScoped<ITrendplusDbContext>(provider => provider.GetRequiredService<TrendplusDbContext>());

                services.RemoveAll<DbContextOptions<AnalyticsDbContext>>();
                services.RemoveAll<AnalyticsDbContext>();
                services.RemoveAll<IAnalyticsDbContext>();
                services.AddDbContext<AnalyticsDbContext>(options => options
                    .UseNpgsql(_connectionString)
                    .AddInterceptors(_commandRecorder));
                services.AddScoped<IAnalyticsDbContext>(provider => provider.GetRequiredService<AnalyticsDbContext>());

                services.RemoveAll<IAnalyticsCacheService>();
                services.AddSingleton<IAnalyticsCacheService>(_cache);
                services.RemoveAll<IMemoryCache>();
                services.AddSingleton<IMemoryCache>(_memoryCache);
                services.RemoveAll<OperationsAnalyticsIntegrityRegistry>();
                services.AddSingleton(_registry);
                services.Configure<AnalyticsSnapshotOptions>(options => options.UseSnapshotCost = true);
                services.Configure<PerformanceLoggingOptions>(options => options.CaptureHttpRequests = false);
            });
        }
    }

}
