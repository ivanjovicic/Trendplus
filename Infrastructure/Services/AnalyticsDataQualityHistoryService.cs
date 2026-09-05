using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace Infrastructure.Services;

public sealed class AnalyticsDataQualityHistoryService
{
    private static bool _schemaEnsured;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);

    private readonly TrendplusDbContext _db;

    public AnalyticsDataQualityHistoryService(TrendplusDbContext db)
    {
        _db = db;
    }

    public async Task SaveSnapshotAsync(AnalyticsDataQualityHealthSnapshot snapshot, string? dataScope, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);

        const string sql = """
            INSERT INTO analytics_data_quality_history (
                snapshot_date_utc,
                captured_at_utc,
                lookback_days,
                orphan_article_count,
                missing_cost_revenue,
                missing_cost_revenue_share_pct,
                unknown_supplier_revenue,
                unknown_supplier_revenue_share_pct,
                data_scope
            )
            VALUES (
                @snapshotDateUtc,
                @capturedAtUtc,
                @lookbackDays,
                @orphanArticleCount,
                @missingCostRevenue,
                @missingCostRevenueSharePct,
                @unknownSupplierRevenue,
                @unknownSupplierRevenueSharePct,
                @dataScope
            )
            ON CONFLICT (snapshot_date_utc, data_scope, lookback_days)
            DO UPDATE SET
                captured_at_utc = EXCLUDED.captured_at_utc,
                orphan_article_count = EXCLUDED.orphan_article_count,
                missing_cost_revenue = EXCLUDED.missing_cost_revenue,
                missing_cost_revenue_share_pct = EXCLUDED.missing_cost_revenue_share_pct,
                unknown_supplier_revenue = EXCLUDED.unknown_supplier_revenue,
                unknown_supplier_revenue_share_pct = EXCLUDED.unknown_supplier_revenue_share_pct;
            """;

        var parameters = new object[]
        {
            new NpgsqlParameter("snapshotDateUtc", snapshot.GeneratedAtUtc.Date),
            new NpgsqlParameter("capturedAtUtc", snapshot.GeneratedAtUtc),
            new NpgsqlParameter("lookbackDays", snapshot.LookbackDays),
            new NpgsqlParameter("orphanArticleCount", snapshot.OrphanArticleCount),
            new NpgsqlParameter("missingCostRevenue", snapshot.MissingCostRevenue),
            new NpgsqlParameter("missingCostRevenueSharePct", snapshot.MissingCostRevenueSharePct is { } missingCostShare ? missingCostShare : DBNull.Value),
            new NpgsqlParameter("unknownSupplierRevenue", snapshot.UnknownSupplierRevenue),
            new NpgsqlParameter("unknownSupplierRevenueSharePct", snapshot.UnknownSupplierRevenueSharePct is { } unknownSupplierShare ? unknownSupplierShare : DBNull.Value),
            new NpgsqlParameter("dataScope", NormalizeDataScope(dataScope))
        };

        await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }

    public async Task<IReadOnlyList<DataQualityTrendPointDto>> GetTrendAsync(int days, string? dataScope, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);

        const string sql = """
            SELECT
                snapshot_date_utc,
                missing_cost_revenue_share_pct,
                unknown_supplier_revenue_share_pct,
                orphan_article_count
            FROM analytics_data_quality_history
            WHERE snapshot_date_utc >= @fromDateUtc
              AND data_scope = @dataScope
            ORDER BY snapshot_date_utc ASC;
            """;

        var connection = _db.Database.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Trend query requires an Npgsql connection.");

        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.CommandTimeout = 30;
            command.Parameters.AddWithValue("fromDateUtc", DateTime.UtcNow.Date.AddDays(-(Math.Max(2, days) - 1)));
            command.Parameters.AddWithValue("dataScope", NormalizeDataScope(dataScope));

            var points = new List<DataQualityTrendPointDto>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                points.Add(new DataQualityTrendPointDto(
                    reader.GetDateTime(0),
                    reader.IsDBNull(1) ? null : reader.GetDouble(1),
                    reader.IsDBNull(2) ? null : reader.GetDouble(2),
                    reader.IsDBNull(3) ? 0 : reader.GetInt32(3)));
            }

            return points;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaEnsured)
        {
            return;
        }

        await SchemaLock.WaitAsync(ct);
        try
        {
            if (_schemaEnsured)
            {
                return;
            }

            const string sql = """
                CREATE TABLE IF NOT EXISTS analytics_data_quality_history (
                    id BIGSERIAL PRIMARY KEY,
                    snapshot_date_utc DATE NOT NULL,
                    captured_at_utc TIMESTAMPTZ NOT NULL,
                    lookback_days INTEGER NOT NULL,
                    orphan_article_count INTEGER NOT NULL DEFAULT 0,
                    missing_cost_revenue NUMERIC(18, 2) NOT NULL DEFAULT 0,
                    missing_cost_revenue_share_pct DOUBLE PRECISION NULL,
                    unknown_supplier_revenue NUMERIC(18, 2) NOT NULL DEFAULT 0,
                    unknown_supplier_revenue_share_pct DOUBLE PRECISION NULL,
                    data_scope VARCHAR(20) NOT NULL DEFAULT 'all'
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ux_analytics_data_quality_history_snapshot
                    ON analytics_data_quality_history (snapshot_date_utc, data_scope, lookback_days);

                CREATE INDEX IF NOT EXISTS ix_analytics_data_quality_history_scope_date
                    ON analytics_data_quality_history (data_scope, snapshot_date_utc DESC);

                ALTER TABLE analytics_data_quality_history
                    ALTER COLUMN missing_cost_revenue_share_pct DROP NOT NULL;
                ALTER TABLE analytics_data_quality_history
                    ALTER COLUMN unknown_supplier_revenue_share_pct DROP NOT NULL;
                """;

            await _db.Database.ExecuteSqlRawAsync(sql, ct);
            _schemaEnsured = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    private static string NormalizeDataScope(string? dataScope)
    {
        var normalized = (dataScope ?? "all").Trim().ToLowerInvariant();
        return normalized is "existing" or "imported" ? normalized : "all";
    }
}

public sealed record DataQualityTrendPointDto(
    DateTime Date,
    double? MissingCostRevenueSharePct,
    double? UnknownSupplierRevenueSharePct,
    int OrphanArticleCount);
