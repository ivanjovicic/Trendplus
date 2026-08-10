using System.Data;
using System.Data.Common;
using Application.Artikli.Common.Interfaces;
using Application.Analytics.Queries;
using MediatR;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Application.Analytics.Queries.GetRebalanceSuggestions;

public sealed class GetRebalanceSuggestionsHandler
    : IRequestHandler<GetRebalanceSuggestionsQuery, RebalanceSuggestionListDto>
{
    private readonly IAnalyticsDbContext _db;
    private readonly ILogger<GetRebalanceSuggestionsHandler> _logger;

    public GetRebalanceSuggestionsHandler(
        IAnalyticsDbContext db,
        ILogger<GetRebalanceSuggestionsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RebalanceSuggestionListDto> Handle(GetRebalanceSuggestionsQuery request, CancellationToken ct)
    {
        var items = new List<RebalanceSuggestionDto>();
        var connection = _db.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 60;
        command.CommandText = """
            select
                from_store_id,
                to_store_id,
                sku_id,
                coalesce(size_code, 'UNKNOWN') as size_code,
                recommended_qty,
                urgency,
                confidence,
                coalesce(reason, 'snapshot') as reason,
                expected_saved_sales,
                expected_capital_release,
                count(*) over() as total_matching_count
            from analytics_rebalance_suggestion_snapshot
            where (@fromStoreId is null or from_store_id = @fromStoreId)
              and (@toStoreId is null or to_store_id = @toStoreId)
              and (@supplierId is null or supplier_id = @supplierId)
              and (@urgency is null or urgency = @urgency)
            order by confidence desc nulls last, expected_saved_sales desc nulls last, recommended_qty desc nulls last
            limit @top;
            """;

        AddParameter(command, "@fromStoreId", request.FromStoreId);
        AddParameter(command, "@toStoreId", request.ToStoreId);
        AddParameter(command, "@supplierId", request.SupplierId);
        AddParameter(command, "@urgency", request.Urgency);
        AddParameter(command, "@top", Math.Clamp(request.Top, 1, 500));

        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new RebalanceSuggestionDto(
                    FromStoreId: reader.GetInt32(0),
                    ToStoreId: reader.GetInt32(1),
                    SkuId: reader.GetInt32(2),
                    SizeCode: reader.GetString(3),
                    RecommendedQty: reader.GetNullableInt32(4),
                    Urgency: reader.GetNullableString(5),
                    Confidence: reader.GetNullableDecimal(6),
                    Reason: reader.GetString(7),
                    ExpectedSavedSales: reader.GetNullableDecimal(8),
                    ExpectedCapitalRelease: reader.GetNullableDecimal(9)));
            }

            var totalMatchingCount = items.Count == 0
                ? 0
                : Convert.ToInt32(reader.GetInt64(10));
            var returnedCount = items.Count;

            var hasMissingEvidence = items.Any(item =>
                item.RecommendedQty is null
                || item.Urgency is null
                || item.Confidence is null
                || item.ExpectedSavedSales is null
                || item.ExpectedCapitalRelease is null);

            return new RebalanceSuggestionListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: returnedCount,
                ReturnedCount: returnedCount,
                TotalMatchingCount: totalMatchingCount,
                IsTruncated: totalMatchingCount > returnedCount,
                SnapshotAvailable: true,
                Warning: items.Count == 0
                    ? "Rebalance snapshot postoji, ali nema predloga za trazene filtere."
                    : hasMissingEvidence ? "Rebalance snapshot sadrzi redove sa nepotpunom signalnom evidencijom." : null,
                Items: items);
        }
        catch (Exception ex) when (IsMissingRelation(ex))
        {
            _logger.LogWarning(ex, "analytics_rebalance_suggestion_snapshot is not available yet.");
            return new RebalanceSuggestionListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: 0,
                ReturnedCount: 0,
                TotalMatchingCount: 0,
                IsTruncated: false,
                SnapshotAvailable: false,
                Warning: "Rebalance snapshot jos nije dostupan.",
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
