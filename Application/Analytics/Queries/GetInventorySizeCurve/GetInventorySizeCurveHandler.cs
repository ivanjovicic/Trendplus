using System.Data;
using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Application.Analytics.Queries.GetInventorySizeCurve;

public sealed class GetInventorySizeCurveHandler
    : IRequestHandler<GetInventorySizeCurveQuery, InventorySizeCurveListDto>
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<GetInventorySizeCurveHandler> _logger;

    public GetInventorySizeCurveHandler(
        IAnalyticsDbContext db,
        ILogger<GetInventorySizeCurveHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<InventorySizeCurveListDto> Handle(GetInventorySizeCurveQuery request, CancellationToken ct)
    {
        var items = new List<InventorySizeCurveDto>();
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
                cast(coalesce(actual_size_share, 0) as numeric(18,4)) as actual_size_share,
                cast(coalesce(ideal_size_share, 0) as numeric(18,4)) as ideal_size_share,
                cast(coalesce(deviation_pct, 0) as numeric(18,4)) as deviation_pct,
                coalesce(is_core_size_missing, false) as is_core_size_missing,
                coalesce(is_dead_size, false) as is_dead_size,
                coalesce(broken_run, false) as broken_run,
                cast(coalesce(curve_confidence, 0) as numeric(18,4)) as curve_confidence,
                coalesce(reason_codes, '') as reason_codes
            from analytics_size_curve_snapshot
            where (@storeId is null or store_id = @storeId)
              and (@supplierId is null or supplier_id = @supplierId)
              and (@skuId is null or sku_id = @skuId)
            order by broken_run desc, is_core_size_missing desc, abs(deviation_pct) desc
            limit @top;
            """;

        AddParameter(command, "@storeId", request.StoreId);
        AddParameter(command, "@supplierId", request.SupplierId);
        AddParameter(command, "@skuId", request.SkuId);
        AddParameter(command, "@top", Math.Clamp(request.Top, 1, 500));

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var reasonsRaw = reader.GetString(10);
                var reasons = string.IsNullOrWhiteSpace(reasonsRaw)
                    ? Array.Empty<string>()
                    : reasonsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                items.Add(new InventorySizeCurveDto(
                    SkuId: reader.GetInt32(0),
                    StoreId: reader.GetInt32(1),
                    SizeCode: reader.GetString(2),
                    ActualSizeShare: reader.GetDecimal(3),
                    IdealSizeShare: reader.GetDecimal(4),
                    DeviationPct: reader.GetDecimal(5),
                    IsCoreSizeMissing: reader.GetBoolean(6),
                    IsDeadSize: reader.GetBoolean(7),
                    BrokenRun: reader.GetBoolean(8),
                    CurveConfidence: reader.GetDecimal(9),
                    ReasonCodes: reasons));
            }

            return new InventorySizeCurveListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: items.Count,
                SnapshotAvailable: true,
                Warning: items.Count == 0 ? "Size curve snapshot postoji, ali nema redova za trazene filtere." : null,
                Items: items);
        }
        catch (Exception ex) when (IsMissingRelation(ex))
        {
            _logger.LogWarning(ex, "analytics_size_curve_snapshot is not available yet.");
            return new InventorySizeCurveListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: 0,
                SnapshotAvailable: false,
                Warning: "Size curve snapshot jos nije dostupan.",
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
