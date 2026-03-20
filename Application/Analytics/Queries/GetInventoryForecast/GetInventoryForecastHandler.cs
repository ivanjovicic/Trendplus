using System.Data;
using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Application.Analytics.Queries.GetInventoryForecast;

public sealed class GetInventoryForecastHandler
    : IRequestHandler<GetInventoryForecastQuery, InventoryForecastListDto>
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<GetInventoryForecastHandler> _logger;

    public GetInventoryForecastHandler(
        IAnalyticsDbContext db,
        ILogger<GetInventoryForecastHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<InventoryForecastListDto> Handle(GetInventoryForecastQuery request, CancellationToken ct)
    {
        var items = new List<InventoryForecastDto>();
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                sku_id,
                store_id,
                coalesce(size_code, 'UNKNOWN') as size_code,
                cast(coalesce(forecast_7d, 0) as numeric(18,2)) as forecast_7d,
                cast(coalesce(forecast_14d, 0) as numeric(18,2)) as forecast_14d,
                cast(coalesce(forecast_28d, 0) as numeric(18,2)) as forecast_28d,
                cast(coalesce(probability_of_oos_in_7d, 0) as numeric(18,4)) as probability_of_oos_in_7d,
                cast(coalesce(overstock_risk, 0) as numeric(18,4)) as overstock_risk,
                cast(coalesce(confidence_score, 0) as numeric(18,4)) as confidence_score,
                coalesce(explanation, 'snapshot') as explanation
            from analytics_inventory_forecast_snapshot
            where (@storeId is null or store_id = @storeId)
              and (@supplierId is null or supplier_id = @supplierId)
              and (@skuId is null or sku_id = @skuId)
              and (@sizeCode is null or size_code = @sizeCode)
            order by probability_of_oos_in_7d desc, confidence_score desc, sku_id, store_id
            limit @top;
            """;

        AddParameter(command, "@storeId", request.StoreId);
        AddParameter(command, "@supplierId", request.SupplierId);
        AddParameter(command, "@skuId", request.SkuId);
        AddParameter(command, "@sizeCode", request.SizeCode);
        AddParameter(command, "@top", Math.Clamp(request.Top, 1, 500));

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new InventoryForecastDto(
                    SkuId: reader.GetInt32(0),
                    StoreId: reader.GetInt32(1),
                    SizeCode: reader.GetString(2),
                    Forecast7d: reader.GetDecimal(3),
                    Forecast14d: reader.GetDecimal(4),
                    Forecast28d: reader.GetDecimal(5),
                    ProbabilityOfOOSIn7d: reader.GetDecimal(6),
                    OverstockRisk: reader.GetDecimal(7),
                    ConfidenceScore: reader.GetDecimal(8),
                    Explanation: reader.GetString(9)));
            }

            return new InventoryForecastListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: items.Count,
                SnapshotAvailable: true,
                Warning: items.Count == 0 ? "Forecast snapshot postoji, ali nema redova za trazene filtere." : null,
                Items: items);
        }
        catch (Exception ex) when (IsMissingRelation(ex))
        {
            _logger.LogWarning(ex, "analytics_inventory_forecast_snapshot is not available yet.");
            return new InventoryForecastListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: 0,
                SnapshotAvailable: false,
                Warning: "Forecast snapshot jos nije dostupan. Nightly recompute verovatno jos nije pustio tabelu.",
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
