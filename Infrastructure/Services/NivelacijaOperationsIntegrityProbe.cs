using Application.Analytics;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Services;

/// <summary>
/// Bounded Nivelacija integrity probe over the canonical compatibility view.
/// It does not call vendor-sales-nivelacija endpoint aggregations.
/// </summary>
public sealed class NivelacijaOperationsIntegrityProbe : IOperationsAnalyticsIntegrityFamilyProbe
{
    private readonly TrendplusDbContext _db;

    public NivelacijaOperationsIntegrityProbe(TrendplusDbContext db)
    {
        _db = db;
    }

    public string Family => OperationsAnalyticsIntegrityFamilies.Nivelacija;

    public async Task<OperationsAnalyticsIntegrityProbeResult> ProbeAsync(
        OperationsAnalyticsIntegrityProbeRequest request)
    {
        var ct = request.CancellationToken;
        if (request.MaxRows <= 0)
            return OperationsAnalyticsIntegrityProbeResult.Degraded("Nivelacija probe row bound is invalid.");

        var rowLimit = Math.Min(request.MaxRows, Math.Max(1, request.Definition.MaxRows));
        var windowDays = (request.ToUtc - request.FromUtc).TotalDays;
        if (request.FromUtc >= request.ToUtc
            || windowDays > Math.Max(1, request.Definition.MaxWindowDays))
        {
            return OperationsAnalyticsIntegrityProbeResult.Degraded(
                "Nivelacija probe window is invalid or exceeds its configured bound.");
        }

        var connectionString = _db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return OperationsAnalyticsIntegrityProbeResult.Degraded(
                "Database connection string is unavailable for the Nivelacija integrity probe.");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        if (!await ViewIsQueryableAsync(connection, ct))
        {
            return OperationsAnalyticsIntegrityProbeResult.Unverified(
                "Canonical vw_vendor_sales_nivelacija is unavailable; Nivelacija integrity remains explicitly unverified.");
        }

        const string sql = """
            SELECT
                price_event_id,
                price_direction,
                post_window_complete,
                has_revenue_baseline,
                change_percent_revenue_semantic
            FROM vw_vendor_sales_nivelacija
            WHERE event_date >= @from::date
              AND event_date < @to::date
            ORDER BY event_date, price_event_id
            LIMIT @limit
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("from", request.FromUtc.Date);
        command.Parameters.AddWithValue("to", request.ToUtc.Date);
        command.Parameters.AddWithValue("limit", rowLimit + 1);

        var rows = new List<NivelacijaProbeRow>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                rows.Add(new NivelacijaProbeRow(
                    reader.GetInt64(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    !reader.IsDBNull(2) && reader.GetBoolean(2),
                    !reader.IsDBNull(3) && reader.GetBoolean(3),
                    reader.IsDBNull(4) ? null : reader.GetDecimal(4)));
            }
        }

        if (rows.Count > rowLimit)
        {
            return OperationsAnalyticsIntegrityProbeResult.Degraded(
                "Nivelacija canonical view row bound was exceeded during the bounded probe.");
        }

        if (rows.Count == 0)
        {
            return OperationsAnalyticsIntegrityProbeResult.Unverified(
                "No canonical Nivelacija events exist in the probe window; an empty window is not certified as verified.",
                0);
        }

        var deltas = new List<OperationsAnalyticsIntegrityProbeDelta>();
        foreach (var row in rows)
        {
            if (!IsKnownDirection(row.PriceDirection))
            {
                deltas.Add(new OperationsAnalyticsIntegrityProbeDelta(
                    $"event:{row.PriceEventId}:direction",
                    0m,
                    0m,
                    0m,
                    0,
                    0,
                    0));
                continue;
            }

            if (!row.PostWindowComplete
                && row.HasRevenueBaseline
                && row.ChangePercentRevenueSemantic.HasValue)
            {
                deltas.Add(new OperationsAnalyticsIntegrityProbeDelta(
                    $"event:{row.PriceEventId}:immature_baseline",
                    row.ChangePercentRevenueSemantic.Value,
                    0m,
                    row.ChangePercentRevenueSemantic.Value,
                    1,
                    0,
                    1));
            }
        }

        return deltas.Count > 0
            ? OperationsAnalyticsIntegrityProbeResult.DriftDetected(
                "Canonical Nivelacija view exposes immature or unknown-direction events as comparable revenue baselines.",
                deltas,
                rows.Count)
            : OperationsAnalyticsIntegrityProbeResult.Verified(
                "Bounded canonical Nivelacija events respect direction and mature-window baseline semantics in the probe window.",
                probeRowCount: rows.Count);
    }

    private static bool IsKnownDirection(string? direction)
        => direction is "markdown" or "markup" or "flat";

    private static async Task<bool> ViewIsQueryableAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_catalog.pg_class c
                JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relkind IN ('v', 'm')
                  AND n.nspname = current_schema()
                  AND c.relname = 'vw_vendor_sales_nivelacija'
            )
            AND EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'vw_vendor_sales_nivelacija'
                  AND column_name IN ('price_direction', 'post_window_complete')
            )
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(ct);
        return result is bool exists && exists;
    }

    private sealed record NivelacijaProbeRow(
        long PriceEventId,
        string? PriceDirection,
        bool PostWindowComplete,
        bool HasRevenueBaseline,
        decimal? ChangePercentRevenueSemantic);
}
