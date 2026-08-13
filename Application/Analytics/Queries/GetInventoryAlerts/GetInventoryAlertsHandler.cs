using System.Data;
using System.Data.Common;
using Application.Artikli.Common.Interfaces;
using Application.Analytics.Queries;
using MediatR;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Application.Analytics.Queries.GetInventoryAlerts;

public sealed class GetInventoryAlertsHandler
    : IRequestHandler<GetInventoryAlertsQuery, InventoryAlertListDto>
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<GetInventoryAlertsHandler> _logger;

    public GetInventoryAlertsHandler(
        IAnalyticsDbContext db,
        ILogger<GetInventoryAlertsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<InventoryAlertListDto> Handle(GetInventoryAlertsQuery request, CancellationToken ct)
    {
        var items = new List<InventoryAlertDto>();
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
        command.CommandText = """
            select
                coalesce(alert_type, 'unknown') as alert_type,
                sku_id,
                store_id,
                size_code,
                severity,
                coalesce(title, 'Alert') as title,
                coalesce(message, '') as message,
                confidence_score,
                count(*) over() as total_matching_count
            from analytics_inventory_alert_snapshot
            where (@storeId is null or store_id = @storeId)
              and (@supplierId is null or supplier_id = @supplierId)
              and (@severity is null or severity = @severity)
            order by
                case severity
                    when 'critical' then 0
                    when 'warning' then 1
                    when 'info' then 2
                    else 3
                end,
                confidence_score desc nulls last
            limit @top;
            """;

        AddParameter(command, "@storeId", request.StoreId);
        AddParameter(command, "@supplierId", request.SupplierId);
        AddParameter(command, "@severity", request.Severity);
        AddParameter(command, "@top", Math.Clamp(request.Top, 1, 500));

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            var totalMatchingCount = 0;
            while (await reader.ReadAsync(ct))
            {
                totalMatchingCount = Convert.ToInt32(reader.GetInt64(8));
                items.Add(new InventoryAlertDto(
                    AlertType: reader.GetString(0),
                    SkuId: reader.GetInt32(1),
                    StoreId: reader.GetInt32(2),
                    SizeCode: reader.IsDBNull(3) ? null : reader.GetString(3),
                    Severity: reader.GetNullableString(4),
                    Title: reader.GetString(5),
                    Message: reader.GetString(6),
                    ConfidenceScore: reader.GetNullableDecimal(7)));
            }

            var returnedCount = items.Count;

            var hasMissingEvidence = items.Any(item => item.Severity is null || item.ConfidenceScore is null);

            return new InventoryAlertListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: returnedCount,
                ReturnedCount: returnedCount,
                TotalMatchingCount: totalMatchingCount,
                IsTruncated: totalMatchingCount > returnedCount,
                SnapshotAvailable: true,
                Warning: items.Count == 0
                    ? "Inventory alert snapshot postoji, ali nema aktivnih alertova za trazene filtere."
                    : hasMissingEvidence ? "Inventory alert snapshot sadrzi redove sa nepotpunom signalnom evidencijom." : null,
                Items: items);
        }
        catch (Exception ex) when (IsMissingRelation(ex))
        {
            _logger.LogWarning(ex, "analytics_inventory_alert_snapshot is not available yet.");
            return new InventoryAlertListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: 0,
                ReturnedCount: 0,
                TotalMatchingCount: 0,
                IsTruncated: false,
                SnapshotAvailable: false,
                Warning: "Inventory alert snapshot jos nije dostupan.",
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
