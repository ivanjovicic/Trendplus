using System.Data;
using Application.Artikli.Common.Interfaces;
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
                coalesce(recommended_qty, 0) as recommended_qty,
                coalesce(urgency, 'normal') as urgency,
                cast(coalesce(confidence, 0) as numeric(18,4)) as confidence,
                coalesce(reason, 'snapshot') as reason,
                cast(coalesce(expected_saved_sales, 0) as numeric(18,2)) as expected_saved_sales,
                cast(coalesce(expected_capital_release, 0) as numeric(18,2)) as expected_capital_release
            from analytics_rebalance_suggestion_snapshot
            where (@fromStoreId is null or from_store_id = @fromStoreId)
              and (@toStoreId is null or to_store_id = @toStoreId)
              and (@supplierId is null or supplier_id = @supplierId)
              and (@urgency is null or urgency = @urgency)
            order by confidence desc, expected_saved_sales desc, recommended_qty desc
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
                    RecommendedQty: reader.GetInt32(4),
                    Urgency: reader.GetString(5),
                    Confidence: reader.GetDecimal(6),
                    Reason: reader.GetString(7),
                    ExpectedSavedSales: reader.GetDecimal(8),
                    ExpectedCapitalRelease: reader.GetDecimal(9)));
            }

            return new RebalanceSuggestionListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: items.Count,
                SnapshotAvailable: true,
                Warning: items.Count == 0 ? "Rebalance snapshot postoji, ali nema predloga za trazene filtere." : null,
                Items: items);
        }
        catch (Exception ex) when (IsMissingRelation(ex))
        {
            _logger.LogWarning(ex, "analytics_rebalance_suggestion_snapshot is not available yet.");
            return new RebalanceSuggestionListDto(
                GeneratedAtUtc: DateTime.UtcNow,
                TotalCount: 0,
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
