using System.Data;
using System.Data.Common;
using Application.Artikli.Common.Interfaces;
using Application.Analytics.Queries;
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
                forecast_7d,
                forecast_14d,
                forecast_28d,
                probability_of_oos_in_7d,
                overstock_risk,
                confidence_score,
                coalesce(explanation, 'snapshot') as explanation,
                count(*) over() as total_matching_count
            from analytics_inventory_forecast_snapshot
            where (@storeId is null or store_id = @storeId)
              and (@supplierId is null or supplier_id = @supplierId)
              and (@skuId is null or sku_id = @skuId)
              and (@sizeCode is null or size_code = @sizeCode)
            order by probability_of_oos_in_7d desc nulls last, confidence_score desc nulls last, sku_id, store_id
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
            var totalMatchingCount = 0;
            while (await reader.ReadAsync(ct))
            {
                totalMatchingCount = Convert.ToInt32(reader.GetInt64(10));
                items.Add(new InventoryForecastDto(
                    SkuId: reader.GetInt32(0),
                    StoreId: reader.GetInt32(1),
                    SizeCode: reader.GetString(2),
                    Forecast7d: reader.GetNullableDecimal(3),
                    Forecast14d: reader.GetNullableDecimal(4),
                    Forecast28d: reader.GetNullableDecimal(5),
                    ProbabilityOfOOSIn7d: reader.GetNullableDecimal(6),
                    OverstockRisk: reader.GetNullableDecimal(7),
                    ConfidenceScore: reader.GetNullableDecimal(8),
                    Explanation: reader.GetString(9)));
            }

            var returnedCount = items.Count;
            var provenance = InventoryForecastSnapshotProvenance.ForReadableUnprovenOwner();

            var hasMissingEvidence = items.Any(item =>
                item.Forecast7d is null
                || item.Forecast14d is null
                || item.Forecast28d is null
                || item.ProbabilityOfOOSIn7d is null
                || item.OverstockRisk is null
                || item.ConfidenceScore is null);

            var detailWarning = items.Count == 0
                ? "Forecast snapshot postoji, ali nema redova za trazene filtere."
                : hasMissingEvidence ? "Forecast snapshot sadrzi redove sa nepotpunom signalnom evidencijom." : null;

            return new InventoryForecastListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: returnedCount,
                ReturnedCount: returnedCount,
                TotalMatchingCount: totalMatchingCount,
                IsTruncated: totalMatchingCount > returnedCount,
                SnapshotAvailable: true,
                ProvenanceStatus: provenance.ProvenanceStatus,
                MaterializerOwner: provenance.MaterializerOwner,
                IsAuthoritativeForecast: provenance.IsAuthoritativeForecast,
                SnapshotFreshnessUtc: provenance.SnapshotFreshnessUtc,
                Warning: InventoryForecastSnapshotProvenance.ComposeWarning(provenance.ProvenanceStatus, detailWarning),
                Items: items);
        }
        catch (Exception ex) when (IsMissingRelation(ex))
        {
            _logger.LogWarning(ex, "analytics_inventory_forecast_snapshot relation is missing; fail-closed as missing_relation.");
            var provenance = InventoryForecastSnapshotProvenance.ForMissingRelation();
            return new InventoryForecastListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: 0,
                ReturnedCount: 0,
                TotalMatchingCount: 0,
                IsTruncated: false,
                SnapshotAvailable: false,
                ProvenanceStatus: provenance.ProvenanceStatus,
                MaterializerOwner: provenance.MaterializerOwner,
                IsAuthoritativeForecast: provenance.IsAuthoritativeForecast,
                SnapshotFreshnessUtc: provenance.SnapshotFreshnessUtc,
                Warning: InventoryForecastSnapshotProvenance.ComposeWarning(provenance.ProvenanceStatus, null),
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
