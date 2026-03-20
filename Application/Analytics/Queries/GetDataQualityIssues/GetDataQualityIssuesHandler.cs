using System.Data;
using Application.Artikli.Common.Interfaces;
using MediatR;
using Npgsql;

namespace Application.Analytics.Queries.GetDataQualityIssues;

public sealed class GetDataQualityIssuesHandler
    : IRequestHandler<GetDataQualityIssuesQuery, DataQualityIssueListDto>
{
    private readonly ITrendplusDbContext _db;

    public GetDataQualityIssuesHandler(ITrendplusDbContext db)
    {
        _db = db;
    }

    public async Task<DataQualityIssueListDto> Handle(GetDataQualityIssuesQuery request, CancellationToken cancellationToken)
    {
        var issueType = DataQualityIssueTypes.Normalize(request.Type);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = request.Query?.Trim() ?? string.Empty;
        var offset = (page - 1) * pageSize;
        var sortBy = NormalizeSortBy(request.SortBy);
        var sortDir = NormalizeSortDir(request.SortDir);

        var orderClause = BuildOrderClause(sortBy, sortDir);
        var sql = $"""
            WITH sales_30d AS (
                SELECT
                    ps.id_artikal AS artikal_id,
                    COALESCE(SUM(ps.kolicina * ps.cena), 0) AS sales_30d
                FROM prodaja_stavke ps
                JOIN prodaja_zaglavlje p ON p.id = ps.id_prodaja
                WHERE p.datum_prodaje >= @salesFromUtc
                GROUP BY ps.id_artikal
            ),
            quality_source AS (
                SELECT
                    a."PLU" AS sku,
                    a."Id" AS product_id,
                    NULLIF(BTRIM(a."Naziv"), '') AS product_name,
                    a."IDDobavljac" AS supplier_id,
                    NULLIF(BTRIM(d."Naziv"), '') AS supplier_name,
                    a."IDTipObuce" AS shoe_type_id,
                    NULLIF(BTRIM(t."Naziv"), '') AS shoe_type_name,
                    CASE
                        WHEN a."IDDobavljac" IS NULL OR d."Id" IS NULL THEN 'missingSupplier'
                        WHEN a."IDTipObuce" IS NULL OR t."Id" IS NULL THEN 'missingShoeType'
                        WHEN NULLIF(BTRIM(a."Naziv"), '') IS NULL
                             OR (a."IDDobavljac" IS NOT NULL AND NULLIF(BTRIM(d."Naziv"), '') IS NULL)
                             OR (a."IDTipObuce" IS NOT NULL AND NULLIF(BTRIM(t."Naziv"), '') IS NULL)
                            THEN 'invalidName'
                        ELSE 'ok'
                    END AS issue_type,
                    COALESCE(s.sales_30d, 0) AS sales_30d,
                    COALESCE(a."Kolicina", 0) AS stock,
                    a."UpdatedAt" AS last_updated
                FROM "Artikli" a
                LEFT JOIN "Dobavljaci" d ON a."IDDobavljac" = d."Id"
                LEFT JOIN "TipoviObuce" t ON a."IDTipObuce" = t."Id"
                LEFT JOIN sales_30d s ON s.artikal_id = a."Id"
            )
            SELECT
                sku,
                product_id,
                product_name,
                supplier_id,
                supplier_name,
                shoe_type_id,
                shoe_type_name,
                issue_type,
                sales_30d,
                stock,
                last_updated,
                COUNT(*) OVER() AS total_count
            FROM quality_source
            WHERE issue_type = @issueType
              AND (
                    @query = ''
                    OR COALESCE(sku, '') ILIKE @queryPattern
                    OR COALESCE(product_name, '') ILIKE @queryPattern
                    OR COALESCE(supplier_name, '') ILIKE @queryPattern
                    OR COALESCE(shoe_type_name, '') ILIKE @queryPattern
                  )
            ORDER BY {orderClause}
            LIMIT @pageSize
            OFFSET @offset;
            """;

        var connection = _db.GetDbConnection() as NpgsqlConnection
            ?? throw new InvalidOperationException("Data quality query requires an Npgsql connection.");

        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("salesFromUtc", DateTime.UtcNow.AddDays(-30));
            command.Parameters.AddWithValue("issueType", issueType);
            command.Parameters.AddWithValue("query", query);
            command.Parameters.AddWithValue("queryPattern", $"%{query}%");
            command.Parameters.AddWithValue("pageSize", pageSize);
            command.Parameters.AddWithValue("offset", offset);

            var items = new List<DataQualityIssueItemDto>();
            var total = 0;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (total == 0 && !reader.IsDBNull(11))
                {
                    total = reader.GetInt32(11);
                }

                items.Add(new DataQualityIssueItemDto(
                    Sku: reader.IsDBNull(0) ? null : reader.GetString(0),
                    ProductId: reader.GetInt32(1).ToString(),
                    Name: reader.IsDBNull(2) ? null : reader.GetString(2),
                    SupplierId: reader.IsDBNull(3) ? null : reader.GetInt32(3).ToString(),
                    SupplierName: reader.IsDBNull(4) ? null : reader.GetString(4),
                    ShoeTypeId: reader.IsDBNull(5) ? null : reader.GetInt32(5).ToString(),
                    ShoeTypeName: reader.IsDBNull(6) ? null : reader.GetString(6),
                    IssueType: reader.GetString(7),
                    Sales30d: reader.GetDecimal(8),
                    Stock: reader.GetInt32(9),
                    LastUpdated: reader.GetDateTime(10)));
            }

            return new DataQualityIssueListDto(page, pageSize, total, items);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            "lastUpdated" => "lastUpdated",
            "stock" => "stock",
            "name" => "name",
            _ => "sales30d"
        };
    }

    private static string NormalizeSortDir(string? sortDir)
        => string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

    private static string BuildOrderClause(string sortBy, string sortDir)
    {
        return sortBy switch
        {
            "lastUpdated" => $"""last_updated {sortDir}, sales_30d DESC, product_id ASC""",
            "stock" => $"""stock {sortDir}, sales_30d DESC, product_id ASC""",
            "name" => $"""product_name {sortDir} NULLS LAST, sales_30d DESC, product_id ASC""",
            _ => $"""sales_30d {sortDir}, last_updated DESC, product_id ASC"""
        };
    }
}
