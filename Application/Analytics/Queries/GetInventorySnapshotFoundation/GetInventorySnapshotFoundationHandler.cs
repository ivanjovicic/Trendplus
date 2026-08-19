using System.Data;
using System.Data.Common;
using System.Globalization;
using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Application.Analytics.Queries.GetInventorySnapshotFoundation;

public sealed class GetInventorySnapshotFoundationHandler
    : IRequestHandler<GetInventorySnapshotFoundationQuery, InventorySnapshotFoundationListDto>
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<GetInventorySnapshotFoundationHandler> _logger;

    public GetInventorySnapshotFoundationHandler(
        IAnalyticsDbContext db,
        ILogger<GetInventorySnapshotFoundationHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<InventorySnapshotFoundationListDto> Handle(GetInventorySnapshotFoundationQuery request, CancellationToken ct)
    {
        var items = new List<InventorySnapshotFoundationItem>();
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var normalizedTop = Math.Clamp(request.Top, 1, 500);
        var asOfDate = request.SnapshotDate?.Date;

        await using var command = connection.CreateCommand();
        command.CommandText = """
            with bounds as (
                select coalesce(@snapshotDate::date, (
                    select max(snapshot_date)
                    from analytics_intel.mv_inventory_snapshot_foundation_v1_cache
                )) as as_of_date
            )
            select
                article_id,
                sku,
                product_name,
                snapshot_date,
                observed_at_utc,
                observed_stock_qty,
                reconstructed_stock_qty,
                stock_qty,
                snapshot_source_status,
                has_mixed_evidence,
                source_records,
                count(*) over() as total_matching_count
            from analytics_intel.mv_inventory_snapshot_foundation_v1_cache f
            join bounds b
              on b.as_of_date is not null
             and f.snapshot_date = b.as_of_date
            where (@articleId is null or article_id = @articleId)
            order by
                case snapshot_source_status
                    when 'observed' then 0
                    when 'mixed' then 1
                    when 'reconstructed' then 2
                    else 3
                end,
                article_id
            limit @top;
            """;

        AddParameter(command, "@snapshotDate", request.SnapshotDate?.Date);
        AddParameter(command, "@articleId", request.ArticleId);
        AddParameter(command, "@top", normalizedTop);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            var totalMatchingCount = 0;

            while (await reader.ReadAsync(ct))
            {
                if (totalMatchingCount == 0)
                {
                    totalMatchingCount = Convert.ToInt32(reader.GetInt64(11), CultureInfo.InvariantCulture);
                }

                items.Add(new InventorySnapshotFoundationItem(
                    ArticleId: reader.GetInt32(0),
                    Sku: reader.GetString(1),
                    ProductName: reader.GetString(2),
                    SnapshotDate: reader.GetDateTime(3),
                    ObservedAtUtc: reader.GetNullableDateTime(4),
                    ObservedStockQty: reader.GetNullableDecimal(5),
                    ReconstructedStockQty: reader.GetNullableDecimal(6),
                    StockQty: reader.GetNullableDecimal(7),
                    SnapshotSourceStatus: reader.GetString(8),
                    HasMixedEvidence: reader.GetBoolean(9),
                    SourceRecords: reader.GetInt32(10)));
            }

            var returnedCount = items.Count;

            if (!asOfDate.HasValue)
            {
                asOfDate = items.Count > 0
                    ? items[0].SnapshotDate.Date
                    : await TryGetLatestSnapshotDateAsync(connection, ct);
            }

            var hasNonObservedEvidence = items.Any(item =>
                !string.Equals(item.SnapshotSourceStatus, "observed", StringComparison.OrdinalIgnoreCase));

            return new InventorySnapshotFoundationListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                AsOfDate: asOfDate,
                TotalCount: returnedCount,
                ReturnedCount: returnedCount,
                TotalMatchingCount: totalMatchingCount,
                IsTruncated: totalMatchingCount > returnedCount,
                SnapshotAvailable: true,
                Warning: items.Count == 0
                    ? "Observed inventory snapshot foundation postoji, ali nema redova za trazeni datum."
                    : hasNonObservedEvidence
                        ? "Observed inventory snapshot foundation sadrzi reconstructed, mixed ili missing redove. Provenance je eksplicitna."
                        : null,
                Items: items);
        }
        catch (Exception ex) when (IsMissingRelation(ex))
        {
            _logger.LogWarning(ex, "analytics_intel.mv_inventory_snapshot_foundation_v1_cache is not available yet.");
            return new InventorySnapshotFoundationListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                AsOfDate: asOfDate,
                TotalCount: 0,
                ReturnedCount: 0,
                TotalMatchingCount: 0,
                IsTruncated: false,
                SnapshotAvailable: false,
                Warning: "Observed inventory snapshot foundation jos nije dostupan. Nightly rebuild verovatno jos nije pustio cache.",
                Items: []);
        }
    }

    private static void AddParameter(IDbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static async Task<DateTime?> TryGetLatestSnapshotDateAsync(DbConnection connection, CancellationToken ct)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                select max(snapshot_date)
                from analytics_intel.mv_inventory_snapshot_foundation_v1_cache;
                """;

            var value = await command.ExecuteScalarAsync(ct);
            if (value is null || value is DBNull)
            {
                return null;
            }

            var date = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            return date.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
                : date.ToUniversalTime();
        }
        catch (PostgresException ex) when (IsMissingRelation(ex))
        {
            return null;
        }
    }

    private static bool IsMissingRelation(Exception ex) =>
        ex is PostgresException pg && (pg.SqlState == "42P01" || pg.SqlState == "42703")
        || ex.InnerException is PostgresException innerPg && (innerPg.SqlState == "42P01" || innerPg.SqlState == "42703");
}

internal static class InventorySnapshotFoundationDbReaderExtensions
{
    public static DateTime? GetNullableDateTime(this IDataRecord record, int ordinal) =>
        record.IsDBNull(ordinal)
            ? null
            : NormalizeDateTime(Convert.ToDateTime(record.GetValue(ordinal), CultureInfo.InvariantCulture));

    public static decimal? GetNullableDecimal(this IDataRecord record, int ordinal) =>
        record.IsDBNull(ordinal)
            ? null
            : Convert.ToDecimal(record.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static DateTime NormalizeDateTime(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
}
