using System.Data;
using System.Data.Common;
using Application.Artikli.Common.Interfaces;
using Application.Analytics.Queries;
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
                actual_size_share,
                ideal_size_share,
                deviation_pct,
                is_core_size_missing,
                is_dead_size,
                broken_run,
                curve_confidence,
                coalesce(reason_codes, '') as reason_codes,
                count(*) over() as total_matching_count
            from analytics_size_curve_snapshot
            where (@storeId is null or store_id = @storeId)
              and (@supplierId is null or supplier_id = @supplierId)
              and (@skuId is null or sku_id = @skuId)
            order by broken_run desc nulls last, is_core_size_missing desc nulls last, abs(deviation_pct) desc nulls last
            limit @top;
            """;

        AddParameter(command, "@storeId", request.StoreId);
        AddParameter(command, "@supplierId", request.SupplierId);
        AddParameter(command, "@skuId", request.SkuId);
        AddParameter(command, "@top", Math.Clamp(request.Top, 1, 500));

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            var totalMatchingCount = 0;
            while (await reader.ReadAsync(ct))
            {
                totalMatchingCount = Convert.ToInt32(reader.GetInt64(11));
                var reasonsRaw = reader.GetString(10);
                var reasons = string.IsNullOrWhiteSpace(reasonsRaw)
                    ? Array.Empty<string>()
                    : reasonsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                items.Add(new InventorySizeCurveDto(
                    SkuId: reader.GetInt32(0),
                    StoreId: reader.GetInt32(1),
                    SizeCode: reader.GetString(2),
                    ActualSizeShare: reader.GetNullableDecimal(3),
                    IdealSizeShare: reader.GetNullableDecimal(4),
                    DeviationPct: reader.GetNullableDecimal(5),
                    IsCoreSizeMissing: reader.GetNullableBoolean(6),
                    IsDeadSize: reader.GetNullableBoolean(7),
                    BrokenRun: reader.GetNullableBoolean(8),
                    CurveConfidence: reader.GetNullableDecimal(9),
                    EvidenceStatus: reader.IsDBNull(6) || reader.IsDBNull(7) || reader.IsDBNull(8) || reader.IsDBNull(3) || reader.IsDBNull(4) || reader.IsDBNull(5) || reader.IsDBNull(9)
                        ? "missing"
                        : "complete",
                    ReasonCodes: reasons));
            }

            var returnedCount = items.Count;

            var hasMissingEvidence = items.Any(item =>
                item.ActualSizeShare is null
                || item.IdealSizeShare is null
                || item.DeviationPct is null
                || item.IsCoreSizeMissing is null
                || item.IsDeadSize is null
                || item.BrokenRun is null
                || item.CurveConfidence is null);

            return new InventorySizeCurveListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: returnedCount,
                ReturnedCount: returnedCount,
                TotalMatchingCount: totalMatchingCount,
                IsTruncated: totalMatchingCount > returnedCount,
                SnapshotAvailable: true,
                Warning: items.Count == 0
                    ? "Size curve snapshot postoji, ali nema redova za trazene filtere."
                    : hasMissingEvidence ? "Size curve snapshot sadrzi redove sa nepotpunom signalnom evidencijom." : null,
                Items: items);
        }
        catch (Exception ex) when (IsMissingRelation(ex))
        {
            _logger.LogWarning(ex, "analytics_size_curve_snapshot is not available yet.");
            return new InventorySizeCurveListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: 0,
                ReturnedCount: 0,
                TotalMatchingCount: 0,
                IsTruncated: false,
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
