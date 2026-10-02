using System.Diagnostics;
using System.Text.Json;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Trendplus2.Endpoints;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

[Trait("Category", "Integration")]
public sealed class SupplierNivelacijaQueryPerformanceTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SupplierNivelacijaQueryPerformanceTests(PostgresContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task FirstNivelacijaQuery_BaselineAndArticleBoundedVariants_PreserveRequestedArticleResults()
    {
        if (!_fixture.IsAvailable)
        {
            return;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"rq487_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var commandLog = new List<string>();
        var options = new DbContextOptionsBuilder<TrendplusDbContext>()
            .UseNpgsql(connectionString)
            .LogTo(message =>
            {
                if (message.Contains("Executed DbCommand", StringComparison.Ordinal))
                {
                    commandLog.Add(message);
                }
            }, LogLevel.Information)
            .Options;
        await using var db = new TrendplusDbContext(options);
        await db.Database.EnsureCreatedAsync();

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand(
                """
                INSERT INTO "DnevnikPromena" ("Id", "TipPromene", "Datum", "Iznos", "ArtikalId", "IDObjekat", "DataOrigin")
                SELECT n,
                       'Nivelacija',
                       (DATE '2025-10-01' + ((n - 1) % 365)::integer)::timestamp,
                       0,
                       ((n - 1) % 12000)::integer + 1,
                       1,
                       'existing'
                FROM generate_series(1, 120000) AS n;
                """,
                connection);
            await seed.ExecuteNonQueryAsync();

            await using var seedArticles = new NpgsqlCommand(
                """
                INSERT INTO "Artikli" ("Id", "PLU", "Naziv", "IDObjekat", "Kolicina", "MinimalnaKolicina", "NabavnaCena", "NabavnaCenaDin", "ProdajnaCena", "UpdatedAt", "DataOrigin")
                SELECT n, 'RQ487-' || n::text, 'RQ487 article ' || n::text, 1, 5, 1, 50, 50, 100,
                       TIMESTAMP '2026-10-01 12:00:00', 'existing'
                FROM generate_series(1, 12000) AS n;

                INSERT INTO prodaja_zaglavlje (id, datum_prodaje, id_objekat, data_origin)
                SELECT (slot * 12000) + n,
                       CASE slot
                           WHEN 1 THEN TIMESTAMP '2026-09-15 12:00:00'
                           WHEN 2 THEN TIMESTAMP '2026-08-15 12:00:00'
                           WHEN 3 THEN TIMESTAMP '2026-06-15 12:00:00'
                           ELSE TIMESTAMP '2025-12-15 12:00:00'
                       END,
                       1,
                       'existing'
                FROM generate_series(1, 12000) AS n
                CROSS JOIN generate_series(1, 4) AS slot;

                INSERT INTO prodaja_stavke (id, id_prodaja, id_artikal, kolicina, cena, nabavna_cena)
                SELECT (slot * 12000) + n,
                       (slot * 12000) + n,
                       n,
                       2,
                       100,
                       50
                FROM generate_series(1, 12000) AS n
                CROSS JOIN generate_series(1, 4) AS slot;

                INSERT INTO analytics_cost_snapshot_batches (id, scope, status, created_at_utc, created_by, row_count)
                VALUES (1, 'rq487-fixture', 'active', TIMESTAMP '2026-10-01 12:00:00', 'RQ487 fixture', 120000);

                INSERT INTO analytics_sale_line_cost_snapshots
                    (id, batch_id, prodaja_stavka_id, resolved_unit_cost, cost_source, artikal_id)
                SELECT n, 1, n, 50, 1, ((n - 1) % 12000) + 1
                FROM generate_series(1, 120000) AS n;
                """,
                connection);
            await seedArticles.ExecuteNonQueryAsync();

            await using var analyze = new NpgsqlCommand("ANALYZE \"DnevnikPromena\";", connection);
            await analyze.ExecuteNonQueryAsync();
        }

        foreach (var days in new[] { 30, 90 })
        {
            commandLog.Clear();
            var to = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var from = to.AddDays(1 - days);
            var watch = Stopwatch.StartNew();
            var response = await CachedAnalyticsEndpoints.BuildProductDecisionCenterAsync(
                db,
                from,
                to,
                storeId: 1,
                supplierId: null,
                top: 50,
                dataScope: "all",
                CancellationToken.None);
            watch.Stop();
            Assert.Equal(50, response.TotalRows);
            Assert.Equal(12000, response.AnalyzedRows);
            _output.WriteLine(
                "RQ487 Product Decision baseline days={0}; articles=12000; saleLines=48000; analyzedRows={1}; returnedRows={2}; elapsedMs={3}; dbCommands={4}",
                days,
                response.AnalyzedRows,
                response.Rows.Count,
                watch.ElapsedMilliseconds,
                commandLog.Count);
            foreach (var command in commandLog)
            {
                _output.WriteLine("RQ487 Product Decision command: {0}", command[..Math.Min(command.Length, 220)].ReplaceLineEndings(" "));
            }
        }

        var articleIds = Enumerable.Range(1, 24).ToArray();
        var participatingSaleLineIds = Enumerable.Range(12001, 24).ToArray();
        foreach (var days in new[] { 30, 90 })
        {
            var endExclusive = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc).AddDays(1 - days);
            var baselineSql = $"""
                SELECT "ArtikalId", MIN("Datum") AS "PrvaDatum"
                FROM "DnevnikPromena"
                WHERE "TipPromene" IN ('Nivelacija', 'NivelacijaCena')
                  AND "ArtikalId" IS NOT NULL
                  AND "Datum" < TIMESTAMP '{endExclusive:yyyy-MM-dd HH:mm:ss}'
                  AND ("IDObjekat" IS NULL OR "IDObjekat" = 1)
                GROUP BY "ArtikalId"
                ORDER BY "ArtikalId";
                """;
            var boundedSql = $"""
                SELECT "ArtikalId", MIN("Datum") AS "PrvaDatum"
                FROM "DnevnikPromena"
                WHERE "TipPromene" IN ('Nivelacija', 'NivelacijaCena')
                  AND "ArtikalId" = ANY (ARRAY[{string.Join(',', articleIds)}]::integer[])
                  AND "Datum" < TIMESTAMP '{endExclusive:yyyy-MM-dd HH:mm:ss}'
                  AND ("IDObjekat" IS NULL OR "IDObjekat" = 1)
                GROUP BY "ArtikalId"
                ORDER BY "ArtikalId";
                """;

            var (baselineRows, baselineMs) = await ExecuteRowsAsync(connectionString, baselineSql);
            var boundedWatch = Stopwatch.StartNew();
            var boundedMap = await SupplierSalesStatsQuerySupport.LoadFirstNivelacijaByArticleAsync(
                db,
                articleIds,
                endExclusive,
                storeId: 1,
                CancellationToken.None);
            boundedWatch.Stop();
            var boundedRows = boundedMap.Select(pair => (ArticleId: pair.Key, FirstAt: pair.Value)).ToList();
            var boundedMs = boundedWatch.ElapsedMilliseconds;
            var baselineByArticle = baselineRows.ToDictionary(row => row.ArticleId, row => row.FirstAt);
            var expectedBounded = baselineByArticle
                .Where(pair => articleIds.Contains(pair.Key))
                .OrderBy(pair => pair.Key)
                .ToArray();
            Assert.Equal(expectedBounded, boundedRows.OrderBy(row => row.ArticleId)
                .Select(row => new KeyValuePair<int, DateTime>(row.ArticleId, row.FirstAt)));

            var (baselinePlan, baselinePlanMs) = await ExplainAsync(connectionString, baselineSql);
            var (boundedPlan, boundedPlanMs) = await ExplainAsync(connectionString, boundedSql);
            _output.WriteLine(
                "RQ487 baseline days={0}; fixtureRows=120000; matchingArticles={1}; baselineReturned={2}; boundedReturned={3}; baselineMs={4}; boundedMs={5}; baselineExplainMs={6}; boundedExplainMs={7}",
                days,
                articleIds.Length,
                baselineRows.Count,
                boundedRows.Count,
                baselineMs,
                boundedMs,
                baselinePlanMs,
                boundedPlanMs);
            _output.WriteLine("baseline EXPLAIN ({0}d): {1}", days, baselinePlan);
            _output.WriteLine("bounded EXPLAIN ({0}d): {1}", days, boundedPlan);

            var baselineSnapshotSql = """
                SELECT prodaja_stavka_id, resolved_unit_cost
                FROM analytics_sale_line_cost_snapshots
                WHERE batch_id = 1
                ORDER BY prodaja_stavka_id;
                """;
            var boundedSnapshotSql = $"""
                SELECT prodaja_stavka_id, resolved_unit_cost
                FROM analytics_sale_line_cost_snapshots
                WHERE batch_id = 1
                  AND prodaja_stavka_id = ANY (ARRAY[{string.Join(',', participatingSaleLineIds)}]::integer[])
                ORDER BY prodaja_stavka_id;
                """;
            var (baselineSnapshotRows, baselineSnapshotMs) = await ExecuteSnapshotCostsAsync(connectionString, baselineSnapshotSql);
            var boundedSnapshotWatch = Stopwatch.StartNew();
            var boundedSnapshotRows = await SupplierSalesStatsQuerySupport.LoadSnapshotCostsForSaleLinesAsync(
                db,
                activeBatchId: 1,
                participatingSaleLineIds,
                CancellationToken.None);
            boundedSnapshotWatch.Stop();
            var boundedSnapshotMs = boundedSnapshotWatch.ElapsedMilliseconds;
            var expectedSnapshotRows = baselineSnapshotRows
                .Where(pair => participatingSaleLineIds.Contains(pair.Key))
                .OrderBy(pair => pair.Key)
                .ToArray();
            Assert.Equal(expectedSnapshotRows, boundedSnapshotRows.OrderBy(pair => pair.Key));

            _output.WriteLine(
                "RQ487 snapshot baseline days={0}; activeBatchRows={1}; participatingSaleLines={2}; boundedRows={3}; baselineMs={4}; boundedMs={5}",
                days,
                baselineSnapshotRows.Count,
                participatingSaleLineIds.Length,
                boundedSnapshotRows.Count,
                baselineSnapshotMs,
                boundedSnapshotMs);
        }
    }

    private static async Task<(List<(int ArticleId, DateTime FirstAt)> Rows, long ElapsedMs)> ExecuteRowsAsync(
        string connectionString,
        string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var watch = Stopwatch.StartNew();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(int ArticleId, DateTime FirstAt)>();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetInt32(0), reader.GetDateTime(1)));
        }

        watch.Stop();
        return (rows, watch.ElapsedMilliseconds);
    }

    private static async Task<(Dictionary<int, decimal> Rows, long ElapsedMs)> ExecuteSnapshotCostsAsync(
        string connectionString,
        string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var watch = Stopwatch.StartNew();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new Dictionary<int, decimal>();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetInt32(0), reader.GetDecimal(1));
        }

        watch.Stop();
        return (rows, watch.ElapsedMilliseconds);
    }

    private static async Task<(string Summary, long ElapsedMs)> ExplainAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var watch = Stopwatch.StartNew();
        await using var command = new NpgsqlCommand($"EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) {sql}", connection);
        var json = (string)(await command.ExecuteScalarAsync() ?? "[]");
        watch.Stop();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement[0].GetProperty("Plan");
        var scan = FindSequentialScan(root);
        var summary = string.Join(
            "; ",
            $"root={root.GetProperty("Node Type").GetString()}",
            $"actualRows={root.GetProperty("Actual Rows").GetInt64()}",
            $"actualMs={root.GetProperty("Actual Total Time").GetDouble():F3}",
            $"sharedReadBlocks={root.GetProperty("Shared Read Blocks").GetInt64()}",
            scan.HasValue
                ? $"seqScanRows={scan.Value.Rows}; seqScanMs={scan.Value.Ms:F3}; filtered={scan.Value.Filtered}"
                : "seqScan=none");
        return (summary, watch.ElapsedMilliseconds);
    }

    private static (long Rows, double Ms, long Filtered)? FindSequentialScan(JsonElement node)
    {
        if (node.GetProperty("Node Type").GetString() == "Seq Scan")
        {
            return (
                node.GetProperty("Actual Rows").GetInt64(),
                node.GetProperty("Actual Total Time").GetDouble(),
                node.TryGetProperty("Rows Removed by Filter", out var filtered) ? filtered.GetInt64() : 0L);
        }

        if (!node.TryGetProperty("Plans", out var children))
        {
            return null;
        }

        foreach (var child in children.EnumerateArray())
        {
            var scan = FindSequentialScan(child);
            if (scan.HasValue)
            {
                return scan;
            }
        }

        return null;
    }
}
