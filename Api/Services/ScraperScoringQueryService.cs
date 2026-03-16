using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Api.Models;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.Services
{
    public interface IScraperScoringQueryService
    {
        Task<CanonicalItemsResponse> GetItemsAsync(
            string? brand,
            string? category,
            string? color,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<TrendingResponse> GetTrendingAsync(
            string? category,
            string? brand,
            string? market,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<MomentumResponse> GetMomentumAsync(
            string? category,
            string? market,
            decimal threshold,
            string? sortBy,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<MarketsResponse> GetMarketsAsync(
            string market,
            string? category,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<DebugScoreResponse?> GetDebugScoreAsync(
            long itemId,
            CancellationToken ct = default);
    }

    public sealed class ScraperScoringQueryService : IScraperScoringQueryService
    {
        private readonly AnalyticsDbContext _analyticsDb;
        private readonly ILogger<ScraperScoringQueryService> _logger;

        public ScraperScoringQueryService(
            AnalyticsDbContext analyticsDb,
            ILogger<ScraperScoringQueryService> logger)
        {
            _analyticsDb = analyticsDb;
            _logger = logger;
        }

        public async Task<CanonicalItemsResponse> GetItemsAsync(
            string? brand,
            string? category,
            string? color,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var normalizedPage = Math.Max(page, 1);
            var normalizedPageSize = Math.Max(pageSize, 1);
            var offset = (normalizedPage - 1) * normalizedPageSize;

            await using var connection = await OpenConnectionAsync(ct);
            var (latestRunId, _) = await GetLatestRunAsync(connection, ct);

            var brandLike = NormalizeLike(brand);
            var categoryLike = NormalizeLike(category);
            var colorLike = NormalizeLike(color);
            var normalizedSearch = NormalizeText(search);

            const string countSql = @"
                SELECT COUNT(*)
                FROM items i
                WHERE (@brand IS NULL OR i.brand ILIKE @brand)
                  AND (@category IS NULL OR i.category ILIKE @category)
                  AND (@color IS NULL OR i.color ILIKE @color)
                  AND (
                        @search IS NULL
                        OR to_tsvector('simple', COALESCE(i.name, '') || ' ' || COALESCE(i.brand, ''))
                           @@ websearch_to_tsquery('simple', @search)
                  );";

            await using var countCommand = new NpgsqlCommand(countSql, connection);
            AddItemsFilters(countCommand, brandLike, categoryLike, colorLike, normalizedSearch);
            // var totalRaw = await countCommand.ExecuteScalarAsync(ct);
            // var total = totalRaw is null || totalRaw == DBNull.Value ? 0L : Convert.ToInt64(totalRaw, CultureInfo.InvariantCulture);

            const string dataSql = @"
                SELECT
                    i.item_id,
                    COALESCE(i.name, '') AS name,
                    COALESCE(i.brand, '') AS brand,
                    i.color,
                    i.category,
                    irs.final_score AS latest_score,
                    img.image_url,
                    COUNT(*) OVER() AS total_count
                FROM items i
                LEFT JOIN item_run_stats irs
                    ON irs.item_id = i.item_id
                   AND irs.run_id = @run_id
                LEFT JOIN LATERAL (
                    SELECT ii.image_url
                    FROM item_images ii
                    WHERE ii.item_id = i.item_id
                    ORDER BY ii.created_at DESC
                    LIMIT 1
                ) img ON TRUE
                WHERE (@brand IS NULL OR i.brand ILIKE @brand)
                  AND (@category IS NULL OR i.category ILIKE @category)
                  AND (@color IS NULL OR i.color ILIKE @color)
                  AND (
                        @search IS NULL
                        OR to_tsvector('simple', COALESCE(i.name, '') || ' ' || COALESCE(i.brand, ''))
                           @@ websearch_to_tsquery('simple', @search)
                  )
                ORDER BY irs.final_score DESC NULLS LAST, i.name ASC NULLS LAST, i.item_id ASC
                LIMIT @limit OFFSET @offset;";

            await using var dataCommand = new NpgsqlCommand(dataSql, connection);
            dataCommand.Parameters.AddWithValue("run_id", latestRunId > 0 ? latestRunId : (object)DBNull.Value);
            AddItemsFilters(dataCommand, brandLike, categoryLike, colorLike, normalizedSearch);
            dataCommand.Parameters.AddWithValue("limit", normalizedPageSize);
            dataCommand.Parameters.AddWithValue("offset", offset);

            var items = new List<CanonicalItemDto>(normalizedPageSize);
            long total = 0;
            await using var reader = await dataCommand.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (total == 0) total = reader.GetInt64(7);
                items.Add(new CanonicalItemDto
                {
                    ItemId = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Brand = reader.GetString(2),
                    Color = ReadNullableString(reader, 3),
                    Category = ReadNullableString(reader, 4),
                    LatestScore = ReadNullableDecimal(reader, 5),
                    ImgUrl = ReadNullableString(reader, 6)
                });
            }

            return new CanonicalItemsResponse
            {
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                Total = total,
                Items = items
            };
        }

        public async Task<TrendingResponse> GetTrendingAsync(
            string? category,
            string? brand,
            string? market,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var normalizedPage = Math.Max(page, 1);
            var normalizedPageSize = Math.Max(pageSize, 1);
            var offset = (normalizedPage - 1) * normalizedPageSize;

            await using var connection = await OpenConnectionAsync(ct);
            var (latestRunId, generatedAt) = await GetLatestRunAsync(connection, ct);
            if (latestRunId <= 0)
            {
                return new TrendingResponse
                {
                    RunId = 0,
                    GeneratedAt = null,
                    Page = normalizedPage,
                    PageSize = normalizedPageSize,
                    Total = 0,
                    Items = new List<TrendingItemDto>()
                };
            }

            var categoryLike = NormalizeLike(category);
            var brandLike = NormalizeLike(brand);
            var marketExact = NormalizeMarket(market);

            const string countSql = @"
                SELECT COUNT(*)
                FROM item_run_stats irs
                JOIN items i ON i.item_id = irs.item_id
                WHERE irs.run_id = @run_id
                  AND (@category IS NULL OR i.category ILIKE @category)
                  AND (@brand IS NULL OR i.brand ILIKE @brand)
                  AND (
                        @market IS NULL
                        OR EXISTS (
                            SELECT 1
                            FROM item_market_stats ims
                            WHERE ims.run_id = irs.run_id
                              AND ims.item_id = irs.item_id
                              AND ims.market = @market
                        )
                  );";

            await using var countCommand = new NpgsqlCommand(countSql, connection);
            AddTrendingFilters(countCommand, latestRunId, categoryLike, brandLike, marketExact);
            var totalRaw = await countCommand.ExecuteScalarAsync(ct);
            var total = totalRaw is null || totalRaw == DBNull.Value ? 0L : Convert.ToInt64(totalRaw, CultureInfo.InvariantCulture);

            const string dataSql = @"
                SELECT
                    irs.item_id,
                    COALESCE(irs.final_score, 0) AS final_score,
                    COALESCE(irs.base_score, 0) AS base_score,
                    COALESCE(irs.momentum_normalized, 0) AS momentum,
                    COALESCE(irs.momentum_raw, 0) AS momentum_raw,
                    irs.rank,
                    COALESCE(i.brand, '') AS brand,
                    COALESCE(i.name, '') AS name,
                    img.image_url
                FROM item_run_stats irs
                JOIN items i ON i.item_id = irs.item_id
                LEFT JOIN LATERAL (
                    SELECT ii.image_url
                    FROM item_images ii
                    WHERE ii.item_id = irs.item_id
                    ORDER BY ii.created_at DESC
                    LIMIT 1
                ) img ON TRUE
                WHERE irs.run_id = @run_id
                  AND (@category IS NULL OR i.category ILIKE @category)
                  AND (@brand IS NULL OR i.brand ILIKE @brand)
                  AND (
                        @market IS NULL
                        OR EXISTS (
                            SELECT 1
                            FROM item_market_stats ims
                            WHERE ims.run_id = irs.run_id
                              AND ims.item_id = irs.item_id
                              AND ims.market = @market
                        )
                  )
                ORDER BY irs.final_score DESC NULLS LAST, irs.rank ASC NULLS LAST, irs.item_id ASC
                LIMIT @limit OFFSET @offset;";

            await using var dataCommand = new NpgsqlCommand(dataSql, connection);
            AddTrendingFilters(dataCommand, latestRunId, categoryLike, brandLike, marketExact);
            dataCommand.Parameters.AddWithValue("limit", normalizedPageSize);
            dataCommand.Parameters.AddWithValue("offset", offset);

            var items = new List<TrendingItemDto>(normalizedPageSize);
            await using var reader = await dataCommand.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new TrendingItemDto
                {
                    ItemId = reader.GetInt64(0),
                    FinalScore = reader.GetDecimal(1),
                    BaseScore = reader.GetDecimal(2),
                    Momentum = reader.GetDecimal(3),
                    MomentumRaw = reader.GetDecimal(4),
                    Rank = ReadNullableInt(reader, 5),
                    Brand = reader.GetString(6),
                    Name = reader.GetString(7),
                    ImgUrl = ReadNullableString(reader, 8)
                });
            }

            return new TrendingResponse
            {
                RunId = latestRunId,
                GeneratedAt = generatedAt,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                Total = total,
                Items = items
            };
        }

        public async Task<MomentumResponse> GetMomentumAsync(
            string? category,
            string? market,
            decimal threshold,
            string? sortBy,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var normalizedPage = Math.Max(page, 1);
            var normalizedPageSize = Math.Max(pageSize, 1);
            var offset = (normalizedPage - 1) * normalizedPageSize;

            await using var connection = await OpenConnectionAsync(ct);
            var (latestRunId, generatedAt) = await GetLatestRunAsync(connection, ct);
            if (latestRunId <= 0)
            {
                return new MomentumResponse
                {
                    RunId = 0,
                    GeneratedAt = null,
                    Page = normalizedPage,
                    PageSize = normalizedPageSize,
                    Total = 0,
                    Items = new List<MomentumItemDto>()
                };
            }

            var categoryLike = NormalizeLike(category);
            var marketExact = NormalizeMarket(market);
            var useRawSort = string.Equals(sortBy, "raw", StringComparison.OrdinalIgnoreCase);
            var sortMetric = useRawSort ? "COALESCE(irs.momentum_raw, 0)" : "COALESCE(irs.momentum_normalized, 0)";

            var countSql = $@"
                SELECT COUNT(*)
                FROM item_run_stats irs
                JOIN items i ON i.item_id = irs.item_id
                WHERE irs.run_id = @run_id
                  AND (@category IS NULL OR i.category ILIKE @category)
                  AND (
                        @market IS NULL
                        OR EXISTS (
                            SELECT 1
                            FROM item_market_stats ims
                            WHERE ims.run_id = irs.run_id
                              AND ims.item_id = irs.item_id
                              AND ims.market = @market
                        )
                  )
                  AND {sortMetric} >= @threshold;";

            await using var countCommand = new NpgsqlCommand(countSql, connection);
            AddMomentumFilters(countCommand, latestRunId, categoryLike, marketExact, threshold);
            var totalRaw = await countCommand.ExecuteScalarAsync(ct);
            var total = totalRaw is null || totalRaw == DBNull.Value ? 0L : Convert.ToInt64(totalRaw, CultureInfo.InvariantCulture);

            var dataSql = $@"
                SELECT
                    irs.item_id,
                    COALESCE(i.name, '') AS name,
                    COALESCE(i.brand, '') AS brand,
                    COALESCE(irs.momentum_normalized, 0) AS momentum,
                    COALESCE(irs.momentum_raw, 0) AS momentum_raw,
                    COALESCE(irs.base_score, 0) AS base_score,
                    COALESCE(irs.final_score, 0) AS final_score,
                    img.image_url
                FROM item_run_stats irs
                JOIN items i ON i.item_id = irs.item_id
                LEFT JOIN LATERAL (
                    SELECT ii.image_url
                    FROM item_images ii
                    WHERE ii.item_id = irs.item_id
                    ORDER BY ii.created_at DESC
                    LIMIT 1
                ) img ON TRUE
                WHERE irs.run_id = @run_id
                  AND (@category IS NULL OR i.category ILIKE @category)
                  AND (
                        @market IS NULL
                        OR EXISTS (
                            SELECT 1
                            FROM item_market_stats ims
                            WHERE ims.run_id = irs.run_id
                              AND ims.item_id = irs.item_id
                              AND ims.market = @market
                        )
                  )
                  AND {sortMetric} >= @threshold
                ORDER BY {sortMetric} DESC, irs.final_score DESC NULLS LAST, irs.item_id ASC
                LIMIT @limit OFFSET @offset;";

            await using var dataCommand = new NpgsqlCommand(dataSql, connection);
            AddMomentumFilters(dataCommand, latestRunId, categoryLike, marketExact, threshold);
            dataCommand.Parameters.AddWithValue("limit", normalizedPageSize);
            dataCommand.Parameters.AddWithValue("offset", offset);

            var items = new List<MomentumItemDto>(normalizedPageSize);
            await using var reader = await dataCommand.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new MomentumItemDto
                {
                    ItemId = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    Brand = reader.GetString(2),
                    Momentum = reader.GetDecimal(3),
                    MomentumRaw = reader.GetDecimal(4),
                    BaseScore = reader.GetDecimal(5),
                    FinalScore = reader.GetDecimal(6),
                    ImgUrl = ReadNullableString(reader, 7)
                });
            }

            return new MomentumResponse
            {
                RunId = latestRunId,
                GeneratedAt = generatedAt,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                Total = total,
                Items = items
            };
        }

        public async Task<MarketsResponse> GetMarketsAsync(
            string market,
            string? category,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var normalizedMarket = NormalizeMarket(market) ?? "DE";
            var normalizedPage = Math.Max(page, 1);
            var normalizedPageSize = Math.Max(pageSize, 1);
            var offset = (normalizedPage - 1) * normalizedPageSize;
            var categoryLike = NormalizeLike(category);

            await using var connection = await OpenConnectionAsync(ct);
            var (latestRunId, generatedAt) = await GetLatestRunAsync(connection, ct);
            if (latestRunId <= 0)
            {
                return new MarketsResponse
                {
                    RunId = 0,
                    GeneratedAt = null,
                    Market = normalizedMarket,
                    Currency = "EUR",
                    Page = normalizedPage,
                    PageSize = normalizedPageSize,
                    Total = 0,
                    TopItems = new List<MarketTopItemDto>()
                };
            }

            const string currencySql = @"
                SELECT s.currency
                FROM item_sources s
                WHERE s.market = @market
                  AND s.currency IS NOT NULL
                  AND s.currency <> ''
                GROUP BY s.currency
                ORDER BY COUNT(*) DESC
                LIMIT 1;";

            string currency = "EUR";
            await using (var currencyCommand = new NpgsqlCommand(currencySql, connection))
            {
                currencyCommand.Parameters.AddWithValue("market", normalizedMarket);
                var currencyRaw = await currencyCommand.ExecuteScalarAsync(ct);
                if (currencyRaw != null && currencyRaw != DBNull.Value)
                {
                    currency = Convert.ToString(currencyRaw, CultureInfo.InvariantCulture) ?? "EUR";
                }
            }

            const string countSql = @"
                SELECT COUNT(*)
                FROM item_market_stats ims
                JOIN items i ON i.item_id = ims.item_id
                WHERE ims.run_id = @run_id
                  AND ims.market = @market
                  AND (@category IS NULL OR i.category ILIKE @category);";

            await using var countCommand = new NpgsqlCommand(countSql, connection);
            countCommand.Parameters.AddWithValue("run_id", latestRunId);
            countCommand.Parameters.AddWithValue("market", normalizedMarket);
            countCommand.Parameters.AddWithValue("category", (object?)categoryLike ?? DBNull.Value);
            var totalRaw = await countCommand.ExecuteScalarAsync(ct);
            var total = totalRaw is null || totalRaw == DBNull.Value ? 0L : Convert.ToInt64(totalRaw, CultureInfo.InvariantCulture);

            const string dataSql = @"
                SELECT
                    ims.item_id,
                    COALESCE(irs.final_score, 0) AS final_score,
                    ims.rank AS market_rank,
                    COALESCE(ims.score, 0) AS market_score,
                    COALESCE(i.brand, '') AS brand,
                    COALESCE(i.name, '') AS name,
                    img.image_url
                FROM item_market_stats ims
                JOIN items i ON i.item_id = ims.item_id
                LEFT JOIN item_run_stats irs
                    ON irs.run_id = ims.run_id
                   AND irs.item_id = ims.item_id
                LEFT JOIN LATERAL (
                    SELECT ii.image_url
                    FROM item_images ii
                    WHERE ii.item_id = ims.item_id
                    ORDER BY ii.created_at DESC
                    LIMIT 1
                ) img ON TRUE
                WHERE ims.run_id = @run_id
                  AND ims.market = @market
                  AND (@category IS NULL OR i.category ILIKE @category)
                ORDER BY ims.score DESC NULLS LAST, ims.rank ASC NULLS LAST, ims.item_id ASC
                LIMIT @limit OFFSET @offset;";

            await using var dataCommand = new NpgsqlCommand(dataSql, connection);
            dataCommand.Parameters.AddWithValue("run_id", latestRunId);
            dataCommand.Parameters.AddWithValue("market", normalizedMarket);
            dataCommand.Parameters.AddWithValue("category", (object?)categoryLike ?? DBNull.Value);
            dataCommand.Parameters.AddWithValue("limit", normalizedPageSize);
            dataCommand.Parameters.AddWithValue("offset", offset);

            var topItems = new List<MarketTopItemDto>(normalizedPageSize);
            await using var reader = await dataCommand.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                topItems.Add(new MarketTopItemDto
                {
                    ItemId = reader.GetInt64(0),
                    FinalScore = reader.GetDecimal(1),
                    MarketRank = ReadNullableInt(reader, 2),
                    MarketScore = reader.GetDecimal(3),
                    Brand = reader.GetString(4),
                    Name = reader.GetString(5),
                    ImgUrl = ReadNullableString(reader, 6)
                });
            }

            return new MarketsResponse
            {
                RunId = latestRunId,
                GeneratedAt = generatedAt,
                Market = normalizedMarket,
                Currency = currency,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                Total = total,
                TopItems = topItems
            };
        }

        public async Task<DebugScoreResponse?> GetDebugScoreAsync(
            long itemId,
            CancellationToken ct = default)
        {
            await using var connection = await OpenConnectionAsync(ct);
            var (latestRunId, _) = await GetLatestRunAsync(connection, ct);
            if (latestRunId <= 0)
            {
                return null;
            }

            const string itemSql = @"
                SELECT
                    irs.stat_id,
                    irs.run_id,
                    COALESCE(irs.base_score, 0) AS base_score,
                    COALESCE(irs.final_score, 0) AS final_score,
                    COALESCE(irs.momentum_normalized, 0) AS momentum,
                    COALESCE(irs.momentum_raw, 0) AS momentum_raw,
                    COALESCE(i.name, '') AS name,
                    COALESCE(i.brand, '') AS brand
                FROM item_run_stats irs
                JOIN items i ON i.item_id = irs.item_id
                WHERE irs.run_id = @run_id
                  AND irs.item_id = @item_id
                LIMIT 1;";

            await using var itemCommand = new NpgsqlCommand(itemSql, connection);
            itemCommand.Parameters.AddWithValue("run_id", latestRunId);
            itemCommand.Parameters.AddWithValue("item_id", itemId);

            long statId;
            DebugScoreResponse response;

            await using (var itemReader = await itemCommand.ExecuteReaderAsync(ct))
            {
                if (!await itemReader.ReadAsync(ct))
                {
                    return null;
                }

                statId = itemReader.GetInt64(0);
                response = new DebugScoreResponse
                {
                    ItemId = itemId,
                    RunId = itemReader.GetInt64(1),
                    BaseScore = itemReader.GetDecimal(2),
                    FinalScore = itemReader.GetDecimal(3),
                    Momentum = itemReader.GetDecimal(4),
                    MomentumRaw = itemReader.GetDecimal(5),
                    Name = itemReader.GetString(6),
                    Brand = itemReader.GetString(7)
                };
            }

            const string componentsSql = @"
                SELECT
                    component_name,
                    COALESCE(component_value, 0) AS component_value,
                    COALESCE(weight, 1) AS weight
                FROM score_components
                WHERE stat_id = @stat_id
                ORDER BY component_name ASC;";

            await using (var componentCommand = new NpgsqlCommand(componentsSql, connection))
            {
                componentCommand.Parameters.AddWithValue("stat_id", statId);
                await using var componentReader = await componentCommand.ExecuteReaderAsync(ct);
                while (await componentReader.ReadAsync(ct))
                {
                    response.Components.Add(new ScoreComponentDto
                    {
                        Name = componentReader.GetString(0),
                        Value = componentReader.GetDecimal(1),
                        Weight = componentReader.GetDecimal(2)
                    });
                }
            }

            const string sourcesSql = @"
                SELECT
                    COALESCE(source_name, '') AS source_name,
                    COALESCE(market, '') AS market,
                    price,
                    currency,
                    COALESCE(availability, TRUE) AS availability,
                    product_url
                FROM item_sources
                WHERE item_id = @item_id
                ORDER BY source_name ASC, market ASC, last_seen DESC;";

            await using (var sourceCommand = new NpgsqlCommand(sourcesSql, connection))
            {
                sourceCommand.Parameters.AddWithValue("item_id", itemId);
                await using var sourceReader = await sourceCommand.ExecuteReaderAsync(ct);
                while (await sourceReader.ReadAsync(ct))
                {
                    response.Sources.Add(new ItemSourceDto
                    {
                        Source = sourceReader.GetString(0),
                        Market = sourceReader.GetString(1),
                        Price = ReadNullableDecimal(sourceReader, 2),
                        Currency = ReadNullableString(sourceReader, 3),
                        Available = sourceReader.GetBoolean(4),
                        ProductUrl = ReadNullableString(sourceReader, 5)
                    });
                }
            }

            return response;
        }

        private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct)
        {
            var connectionString = _analyticsDb.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Analytics connection string is not configured.");
            }

            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            return connection;
        }

        private static async Task<(long RunId, DateTime? GeneratedAt)> GetLatestRunAsync(
            NpgsqlConnection connection,
            CancellationToken ct)
        {
            const string sql = @"
                SELECT run_id, COALESCE(finished_at, started_at, created_at) AS generated_at
                FROM runs
                ORDER BY run_id DESC
                LIMIT 1;";

            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return (0, null);
            }

            var runId = reader.GetInt64(0);
            var generatedAt = ReadNullableUtcDateTime(reader, 1);
            return (runId, generatedAt);
        }

        private static void AddItemsFilters(
            NpgsqlCommand command,
            string? brandLike,
            string? categoryLike,
            string? colorLike,
            string? search)
        {
            command.Parameters.AddWithValue("brand", (object?)brandLike ?? DBNull.Value);
            command.Parameters.AddWithValue("category", (object?)categoryLike ?? DBNull.Value);
            command.Parameters.AddWithValue("color", (object?)colorLike ?? DBNull.Value);
            command.Parameters.AddWithValue("search", (object?)search ?? DBNull.Value);
        }

        private static void AddTrendingFilters(
            NpgsqlCommand command,
            long runId,
            string? categoryLike,
            string? brandLike,
            string? market)
        {
            command.Parameters.AddWithValue("run_id", runId);
            command.Parameters.AddWithValue("category", (object?)categoryLike ?? DBNull.Value);
            command.Parameters.AddWithValue("brand", (object?)brandLike ?? DBNull.Value);
            command.Parameters.AddWithValue("market", (object?)market ?? DBNull.Value);
        }

        private static void AddMomentumFilters(
            NpgsqlCommand command,
            long runId,
            string? categoryLike,
            string? market,
            decimal threshold)
        {
            command.Parameters.AddWithValue("run_id", runId);
            command.Parameters.AddWithValue("category", (object?)categoryLike ?? DBNull.Value);
            command.Parameters.AddWithValue("market", (object?)market ?? DBNull.Value);
            command.Parameters.AddWithValue("threshold", threshold);
        }

        private static string? NormalizeText(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? NormalizeLike(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : $"%{value.Trim()}%";

        private static string? NormalizeMarket(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

        private static string? ReadNullableString(DbDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

        private static decimal? ReadNullableDecimal(DbDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

        private static int? ReadNullableInt(DbDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

        private static DateTime? ReadNullableUtcDateTime(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            var value = reader.GetDateTime(ordinal);
            if (value.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(value, DateTimeKind.Utc);
            }

            return value.ToUniversalTime();
        }
    }
}
