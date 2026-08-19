using System.Data;
using System.Data.Common;
using Application.Analytics.Inventory;
using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Application.Analytics.Queries.GetObservedInventoryDailySnapshot;

public sealed class GetObservedInventoryDailySnapshotHandler
    : IRequestHandler<GetObservedInventoryDailySnapshotQuery, ObservedInventoryDailySnapshotListDto>
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<GetObservedInventoryDailySnapshotHandler> _logger;

    public GetObservedInventoryDailySnapshotHandler(
        IAnalyticsDbContext db,
        ILogger<GetObservedInventoryDailySnapshotHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ObservedInventoryDailySnapshotListDto> Handle(
        GetObservedInventoryDailySnapshotQuery request,
        CancellationToken ct)
    {
        var items = new List<ObservedInventoryDailySnapshotDto>();
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                article_id,
                store_id,
                date,
                observed_qty,
                reconstructed_qty,
                stock_qty,
                provenance,
                captured_at_utc,
                source_system,
                count(*) over() as total_matching_count
            from analytics_intel.vw_inventory_daily_stock_v1
            where (@articleId is null or article_id = @articleId)
              and (@storeId is null or store_id = @storeId)
              and (@fromDate is null or date >= @fromDate)
              and (@toDate is null or date < @toDate)
            order by date desc, article_id, store_id
            limit @top;
            """;

        AddParameter(command, "@articleId", request.ArticleId);
        AddParameter(command, "@storeId", request.StoreId);
        AddParameter(command, "@fromDate", request.FromDate?.Date);
        AddParameter(command, "@toDate", request.ToDate?.Date);
        AddParameter(command, "@top", Math.Clamp(request.Top, 1, 500));

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            var totalMatchingCount = 0;
            while (await reader.ReadAsync(ct))
            {
                totalMatchingCount = Convert.ToInt32(reader.GetInt64(9));
                items.Add(ObservedInventoryDailySnapshotMapper.Map(
                    articleId: reader.GetInt32(0),
                    storeId: reader.GetInt32(1),
                    date: reader.GetDateTime(2),
                    observedQty: reader.GetNullableDecimal(3),
                    reconstructedQty: reader.GetNullableDecimal(4),
                    stockQty: reader.GetNullableDecimal(5),
                    provenance: reader.GetNullableString(6),
                    capturedAtUtc: reader.GetNullableDateTime(7),
                    sourceSystem: reader.GetNullableString(8)));
            }

            var returnedCount = items.Count;
            var hasNonObserved = items.Any(item =>
                !InventoryDailyStockProvenance.IsObservedAuthoritative(item.Provenance));

            return new ObservedInventoryDailySnapshotListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: returnedCount,
                ReturnedCount: returnedCount,
                TotalMatchingCount: totalMatchingCount,
                IsTruncated: totalMatchingCount > returnedCount,
                SnapshotAvailable: true,
                Warning: items.Count == 0
                    ? "Observed daily snapshot postoji, ali nema redova za trazene filtere."
                    : hasNonObserved
                        ? "Neki dani nisu observed stock; reconstructed, mixed ili missing evidencija ostaje oznacena u provenance."
                        : null,
                Items: items);
        }
        catch (Exception ex) when (IsMissingRelation(ex))
        {
            _logger.LogWarning(ex, "analytics_intel.vw_inventory_daily_stock_v1 is not available yet.");
            return new ObservedInventoryDailySnapshotListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: 0,
                ReturnedCount: 0,
                TotalMatchingCount: 0,
                IsTruncated: false,
                SnapshotAvailable: false,
                Warning: "Observed daily snapshot jos nije dostupan. Foundation tabela/view verovatno jos nije primenjena.",
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

    private static bool IsMissingRelation(Exception ex) =>
        ex is PostgresException pg && pg.SqlState == "42P01"
        || ex.InnerException is PostgresException innerPg && innerPg.SqlState == "42P01";
}
