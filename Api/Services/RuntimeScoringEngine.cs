using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Config;
using Api.Models;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Api.Services
{
    public interface IRuntimeScoringEngine
    {
        Task<RuntimeScoringEvaluateResponse> EvaluateAsync(
            RuntimeScoringEngineInput input,
            CancellationToken ct = default);
    }

    public sealed record RuntimeScoringEngineInput(
        string ImagePath,
        decimal? Cost,
        decimal? TargetPrice,
        string? Brand,
        string? Category,
        string? Market,
        int?    DobavljacId  = null,
        int?    TipObuceId   = null,
        int?    SezonaId     = null,
        string? Velicina     = null,
        string? Boja         = null,
        string? Materijal    = null);

    public sealed class RuntimeScoringEngine : IRuntimeScoringEngine
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IOpenProductTrainingSignalProvider _trainingSignals;
        private readonly AnalyticsDbContext _analyticsDb;
        private readonly ITrendplusDbContext _trendDb;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RuntimeScoringOptions _options;
        private readonly ILogger<RuntimeScoringEngine> _logger;

        public RuntimeScoringEngine(
            IEmbeddingService embeddingService,
            IOpenProductTrainingSignalProvider trainingSignals,
            AnalyticsDbContext analyticsDb,
            ITrendplusDbContext trendDb,
            IHttpClientFactory httpClientFactory,
            IOptions<RuntimeScoringOptions> options,
            ILogger<RuntimeScoringEngine> logger)
        {
            _embeddingService = embeddingService;
            _trainingSignals = trainingSignals;
            _analyticsDb = analyticsDb;
            _trendDb = trendDb;
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<RuntimeScoringEvaluateResponse> EvaluateAsync(
            RuntimeScoringEngineInput input,
            CancellationToken ct = default)
        {
            var market = NormalizeMarket(input.Market, _options.DefaultMarket);
            var brand = NormalizeText(input.Brand);
            var category = NormalizeText(input.Category);
            var brandLike = ToLike(brand);
            var categoryLike = ToLike(category);

            var training = await _trainingSignals.ResolveAsync(brand, category, input.TargetPrice, ct);
            var scraped = await LoadScraperSignalsAsync(brandLike, categoryLike, market, ct);
            var marketplace = await LoadMarketplaceFallbackAsync(brandLike, categoryLike, ct);

            float[]? embedding = null;
            List<SimilarProduct> similarProducts = new();
            try
            {
                embedding = await _embeddingService.GetEmbeddingAsync(input.ImagePath, ct);
                similarProducts = await _embeddingService.FindSimilarProductsAsync(
                    embedding,
                    threshold: 0.62f,
                    limit: 8,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding fallback in runtime scoring.");
            }

            var imageSimilarityScore = ComputeImageSimilarity(similarProducts);
            var sourceCoverageCount = scraped.SourceCount + marketplace.SourceCount;
            // 3 marketplace sources max + scraper; cap denominator at 6 for a fair coverage score
            var sourceCoverageScore = ClampScore((Math.Min(sourceCoverageCount, 6) / 6d) * 100d);

            // ── Local-data signals (from own sales / seasonal DB) ──────────────────
            var supplierScore  = await ComputeSupplierScoreAsync(input.DobavljacId);
            var shoeTypeScore  = await ComputeShoeTypeScoreAsync(input.TipObuceId);
            var seasonalScore  = await ComputeSeasonalScoreAsync(input.SezonaId);
            var sizeColorScore = await ComputeSizeColorScoreAsync(input.Velicina, input.Boja);
            var materialScore  = ComputeMaterialScore(input.Materijal);

            var baselinePrice = Median(new[] { scraped.MedianPrice, marketplace.AvgPrice, training.TypicalPrice }
                .Where(x => x.HasValue)
                .Select(x => x!.Value));

            var priceFitScore = ComputePriceFit(input.TargetPrice, baselinePrice);
            var marginScore = ComputeMargin(input.Cost, input.TargetPrice);

            var scrapedFinalScore = NormalizeScraperScore(scraped.AvgFinalScore);
            var marketplaceTrendScore = NormalizeMarketplaceTrendScore(marketplace.AvgTrendScore);
            var trainingPopularity = ClampScore((double)training.PopularityPriorScore);

            // Popularity should gracefully degrade when training signals are missing.
            // Blend training prior with scraper, otherwise use scraper + marketplace as proxy.
            var popularityScore = trainingPopularity > 0
                ? NormalizeScore(0.70 * trainingPopularity + 0.30 * scrapedFinalScore)
                : NormalizeScore(0.60 * scrapedFinalScore + 0.40 * marketplaceTrendScore);

            // Trend momentum: prefer scraper momentum; fallback to marketplace trend.
            var scrapedMomentumScore = NormalizeMomentum(scraped.AvgMomentum);
            var trendMomentum = scrapedMomentumScore > 0
                ? NormalizeScore(0.80 * scrapedMomentumScore + 0.20 * scrapedFinalScore)
                : marketplaceTrendScore;

            // Deal: scenario-specific (target vs baseline) with optional training prior.
            var dealFromBaseline = input.TargetPrice.HasValue && baselinePrice.HasValue
                ? ComputeDeal(input.TargetPrice.Value, baselinePrice.Value)
                : 0d;
            var dealFromTraining = ClampScore((double)training.DealScore);
            var dealScore = dealFromBaseline > 0 && dealFromTraining > 0
                ? NormalizeScore(0.70 * dealFromBaseline + 0.30 * dealFromTraining)
                : (dealFromBaseline > 0 ? dealFromBaseline : dealFromTraining);

            // Aggregate local demand score (only counted when at least one signal provided)
            var hasLocalSignals = input.DobavljacId.HasValue || input.TipObuceId.HasValue
                               || input.SezonaId.HasValue
                               || !string.IsNullOrWhiteSpace(input.Velicina)
                               || !string.IsNullOrWhiteSpace(input.Boja)
                               || !string.IsNullOrWhiteSpace(input.Materijal);

            var normalizedLocalDemandScore = hasLocalSignals
                ? NormalizeScore(0.28 * supplierScore + 0.30 * shoeTypeScore + 0.22 * seasonalScore
                   + 0.12 * sizeColorScore + 0.08 * materialScore)
                : 0d;

            var normalizedMarketDemandScore = NormalizeScore(
                0.40 * popularityScore +
                0.25 * trendMomentum +
                0.20 * sourceCoverageScore +
                0.15 * NormalizeScraperScore(scraped.AvgMarketScore));

            var sellProbabilityScore = hasLocalSignals
                ? NormalizeScore(0.30 * normalizedMarketDemandScore + 0.18 * priceFitScore + 0.14 * dealScore
                    + 0.15 * normalizedLocalDemandScore + 0.10 * imageSimilarityScore + 0.09 * marginScore
                    + 0.04 * sourceCoverageScore)
                : NormalizeScore(0.38 * normalizedMarketDemandScore + 0.20 * priceFitScore + 0.14 * dealScore
                    + 0.12 * imageSimilarityScore + 0.10 * marginScore
                    + 0.06 * sourceCoverageScore);

            var sellProbability = Clamp01(sellProbabilityScore / 100d);

            var localFinalScore = NormalizeScore(
                0.34 * sellProbabilityScore +
                0.18 * priceFitScore +
                0.16 * popularityScore +
                0.12 * dealScore +
                0.10 * marginScore +
                0.10 * trendMomentum);

            var pythonPayload = new
            {
                market,
                brand,
                category,
                cost = input.Cost,
                targetPrice = input.TargetPrice,
                baselinePrice,
                popularityScore,
                trendMomentum,
                dealScore,
                marginScore,
                priceFitScore,
                sourceCoverageScore,
                imageSimilarityScore,
                supplierScore,
                shoeTypeScore,
                seasonalScore,
                sizeColorScore,
                materialScore
            };

            var python = await TryPredictWithPythonAsync(pythonPayload, ct);
            if (python.SellProbability.HasValue)
            {
                // Python returns probability in [0,1]. Keep response semantics stable.
                sellProbability = Clamp01((sellProbability * 0.40) + (python.SellProbability.Value * 0.60));
            }

            var finalScore = python.FinalScore.HasValue
                ? NormalizeScore((localFinalScore * 0.4) + (python.FinalScore.Value * 0.6))
                : localFinalScore;

            var currency = scraped.Currency ?? marketplace.Currency ?? "EUR";
            var priceRange = BuildRange(baselinePrice, input.TargetPrice, currency);

            var effPriceFit  = Round2(python.PriceFitScore  ?? priceFitScore);
            var effPopularity = Round2(python.PopularityScore ?? popularityScore);
            var effDeal      = Round2(python.DealScore      ?? dealScore);
            var effMargin    = Round2(python.MarginScore    ?? marginScore);
            var effTrend     = Round2(python.TrendMomentum  ?? trendMomentum);

            var (verdict, verdictColor) = GetVerdict(finalScore);
            var scoreLabel   = GetScoreLabel(finalScore);
            var confidence   = ComputeConfidence(training.HasTrainingSignal, sourceCoverageCount, imageSimilarityScore, baselinePrice.HasValue);
            var pricePos     = GetPricePositioning(input.TargetPrice, baselinePrice);
            var insights     = BuildInsights(
                effPriceFit, effPopularity, effDeal, effMargin, effTrend,
                imageSimilarityScore, sourceCoverageCount, training.HasTrainingSignal, pricePos);

            return new RuntimeScoringEvaluateResponse
            {
                FinalScore = Round2(finalScore),
                SellProbabilityRS = Round4(sellProbability),
                PriceFitScore  = effPriceFit,
                PopularityScore = effPopularity,
                DealScore      = effDeal,
                MarginScore    = effMargin,
                TrendMomentum  = effTrend,
                RecommendedPriceRange = priceRange,
                MarketDemandScore = Round2(normalizedMarketDemandScore),
                ImageSimilarityScore = Round2(imageSimilarityScore),
                SourceCoverageScore = Round2(sourceCoverageScore),
                SourceCoverageCount = sourceCoverageCount,
                SupplierScore   = Round2(supplierScore),
                ShoeTypeScore   = Round2(shoeTypeScore),
                SeasonalScore   = Round2(seasonalScore),
                SizeColorScore  = Round2(sizeColorScore),
                MaterialScore   = Round2(materialScore),
                LocalDemandScore = Round2(normalizedLocalDemandScore),
                HasTrainingSignal = training.HasTrainingSignal,
                UsedPythonModel = python.Used,
                Market = market,
                Currency = currency,
                TypicalPrice = baselinePrice,
                Verdict = verdict,
                VerdictColor = verdictColor,
                ScoreLabel = scoreLabel,
                Confidence = Round2(confidence),
                PricePositioning = pricePos,
                Insights = insights,
                SimilarProducts = similarProducts
                    .OrderByDescending(x => x.Similarity)
                    .Take(5)
                    .Select(x => new RuntimeScoringSimilarProductDto
                    {
                        ProductId = x.ProductId,
                        ProductName = x.ProductName,
                        Similarity = x.Similarity,
                        ImageFileName = x.ImageFileName
                    })
                    .ToList()
            };
        }

        // ── Local-data scoring methods ─────────────────────────────────────────────

        /// <summary>Score how well this supplier sells (0-100) based on our own sales data.</summary>
        private async Task<double> ComputeSupplierScoreAsync(int? dobavljacId)
        {
            if (!dobavljacId.HasValue) return 0d;
            try
            {
                // Units sold for this supplier
                var supplierSold = await _trendDb.ProdajaStavke
                    .Join(_trendDb.Artikli,
                          ps => ps.IdArtikal,
                          a  => a.Id,
                          (ps, a) => new { ps.Kolicina, a.IDDobavljac })
                    .Where(x => x.IDDobavljac == dobavljacId.Value)
                    .SumAsync(x => (double)x.Kolicina);

                if (supplierSold <= 0) return 30d; // supplier exists but no purchases yet

                // Average supplier sold (across all suppliers that have sales)
                var avgSold = await _trendDb.ProdajaStavke
                    .Join(_trendDb.Artikli,
                          ps => ps.IdArtikal,
                          a  => a.Id,
                          (ps, a) => new { ps.Kolicina, a.IDDobavljac })
                    .Where(x => x.IDDobavljac.HasValue)
                    .GroupBy(x => x.IDDobavljac)
                    .Select(g => g.Sum(x => (double)x.Kolicina))
                    .AverageAsync();

                if (avgSold <= 0) return 50d;

                // Normalise: avg → 50, 2× avg → 90, 0.5× avg → 30
                var ratio = supplierSold / avgSold;
                var score = 50d + 40d * Math.Tanh(ratio - 1d);
                return ClampScore(score);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SupplierScore computation failed.");
                return 0d;
            }
        }

        /// <summary>Score how well this shoe type sells (0-100) based on our own sales data.</summary>
        private async Task<double> ComputeShoeTypeScoreAsync(int? tipObuceId)
        {
            if (!tipObuceId.HasValue) return 0d;
            try
            {
                var typeSold = await _trendDb.ProdajaStavke
                    .Join(_trendDb.Artikli,
                          ps => ps.IdArtikal,
                          a  => a.Id,
                          (ps, a) => new { ps.Kolicina, a.IDTipObuce })
                    .Where(x => x.IDTipObuce == tipObuceId.Value)
                    .SumAsync(x => (double)x.Kolicina);

                if (typeSold <= 0) return 30d;

                var avgTypeSold = await _trendDb.ProdajaStavke
                    .Join(_trendDb.Artikli,
                          ps => ps.IdArtikal,
                          a  => a.Id,
                          (ps, a) => new { ps.Kolicina, a.IDTipObuce })
                    .Where(x => x.IDTipObuce.HasValue)
                    .GroupBy(x => x.IDTipObuce)
                    .Select(g => g.Sum(x => (double)x.Kolicina))
                    .AverageAsync();

                if (avgTypeSold <= 0) return 50d;

                var ratio = typeSold / avgTypeSold;
                var score = 50d + 40d * Math.Tanh(ratio - 1d);
                return ClampScore(score);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ShoeTypeScore computation failed.");
                return 0d;
            }
        }

        /// <summary>Return 90 if the season is currently active, 55 if near, 20 if off-season.</summary>
        private async Task<double> ComputeSeasonalScoreAsync(int? sezonaId)
        {
            if (!sezonaId.HasValue) return 0d;
            try
            {
                var sezona = await _trendDb.Sezone.FindAsync(sezonaId.Value);
                if (sezona == null) return 40d;

                var now = DateTime.UtcNow;
                if (now >= sezona.DatumOd && now <= sezona.DatumDo) return 90d;

                // Near season: within 30 days before or after
                var daysUntilStart = (sezona.DatumOd - now).TotalDays;
                var daysSinceEnd   = (now - sezona.DatumDo).TotalDays;
                if ((daysUntilStart > 0 && daysUntilStart <= 30d) ||
                    (daysSinceEnd   > 0 && daysSinceEnd   <= 30d)) return 58d;

                return 20d;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SeasonalScore computation failed.");
                return 0d;
            }
        }

        /// <summary>Score popularity of a size/color combination based on own sales (ProdajaStavke).</summary>
        private async Task<double> ComputeSizeColorScoreAsync(string? velicina, string? boja)
        {
            var hasVel = !string.IsNullOrWhiteSpace(velicina);
            var hasBoj = !string.IsNullOrWhiteSpace(boja);
            if (!hasVel && !hasBoj) return 0d;
            try
            {
                // Units sold for products with this size / color
                var comboQuery = _trendDb.ProdajaStavke
                    .Join(_trendDb.Artikli,
                          ps => ps.IdArtikal,
                          a  => a.Id,
                          (ps, a) => new { ps.Kolicina, a.Velicina, a.Boja });

                if (hasVel) comboQuery = comboQuery.Where(x => x.Velicina == velicina);
                if (hasBoj) comboQuery = comboQuery.Where(x => x.Boja == boja);

                var comboSold = await comboQuery.SumAsync(x => (double)x.Kolicina);
                if (comboSold <= 0) return 35d;

                var totalSold = await _trendDb.ProdajaStavke.SumAsync(x => (double)x.Kolicina);
                if (totalSold <= 0) return 50d;

                var pct = comboSold / totalSold * 100d;
                // 5 % share → 50, 15 % share → 90
                return ClampScore(30d + pct * 4d);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SizeColorScore computation failed.");
                return 0d;
            }
        }

        /// <summary>Rule-based material quality score (0-100).</summary>
        private static double ComputeMaterialScore(string? materijal)
        {
            if (string.IsNullOrWhiteSpace(materijal)) return 0d;
            var m = materijal.Trim().ToLowerInvariant();
            return m switch
            {
                "koža"      or "koza"      or "leather"    => 88d,
                "nabuk"     or "nubuk"     or "suede"      => 78d,
                "tekstil"   or "textile"   or "fabric"     => 68d,
                "platno"    or "canvas"                    => 64d,
                "guma"      or "rubber"                    => 62d,
                "sintetika" or "synthetic" or "pu"         => 54d,
                "mesh"                                     => 70d,
                _                                          => 50d
            };
        }

        private async Task<(decimal AvgFinalScore, decimal AvgMomentum, decimal AvgMarketScore, int SourceCount, decimal? MedianPrice, string? Currency)> LoadScraperSignalsAsync(
            string? brandLike,
            string? categoryLike,
            string market,
            CancellationToken ct)
        {
            var cs = _analyticsDb.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(cs))
            {
                return default;
            }

            const string sql = @"
                WITH latest AS (SELECT MAX(run_id) run_id FROM runs),
                filtered AS (
                    SELECT i.item_id FROM items i
                    WHERE (@brand IS NULL OR i.brand ILIKE @brand)
                      AND (@category IS NULL OR i.category ILIKE @category)
                )
                SELECT
                    COALESCE(AVG(irs.final_score), 0),
                    COALESCE(AVG(irs.momentum_normalized), 0),
                    COALESCE(AVG(CASE WHEN ims.market = @market THEN ims.score END), 0),
                    COALESCE(COUNT(DISTINCT CASE WHEN s.market = @market THEN s.source_name END), 0),
                    (
                        SELECT percentile_cont(0.5) WITHIN GROUP (ORDER BY s2.price)
                        FROM item_sources s2 JOIN filtered f2 ON f2.item_id = s2.item_id
                        WHERE s2.price IS NOT NULL AND s2.market = @market
                    ),
                    (
                        SELECT s3.currency FROM item_sources s3 JOIN filtered f3 ON f3.item_id = s3.item_id
                        WHERE s3.market = @market AND s3.currency IS NOT NULL AND s3.currency <> ''
                        GROUP BY s3.currency ORDER BY COUNT(*) DESC LIMIT 1
                    )
                FROM latest l
                LEFT JOIN item_run_stats irs ON irs.run_id = l.run_id
                LEFT JOIN filtered f ON f.item_id = irs.item_id
                LEFT JOIN item_market_stats ims ON ims.run_id = irs.run_id AND ims.item_id = irs.item_id
                LEFT JOIN item_sources s ON s.item_id = irs.item_id
                WHERE f.item_id IS NOT NULL;";

            try
            {
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync(ct);
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("brand", (object?)brandLike ?? DBNull.Value);
                cmd.Parameters.AddWithValue("category", (object?)categoryLike ?? DBNull.Value);
                cmd.Parameters.AddWithValue("market", market);

                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct))
                {
                    return default;
                }

                var sources = r.IsDBNull(3) ? 0 : Convert.ToInt32(r.GetInt64(3), CultureInfo.InvariantCulture);
                return (
                    r.IsDBNull(0) ? 0m : r.GetDecimal(0),
                    r.IsDBNull(1) ? 0m : r.GetDecimal(1),
                    r.IsDBNull(2) ? 0m : r.GetDecimal(2),
                    sources,
                    r.IsDBNull(4) ? null : r.GetDecimal(4),
                    r.IsDBNull(5) ? null : r.GetString(5));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable || ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                _logger.LogWarning(ex, "Scraper scoring tables missing, skipping runtime scraper signals.");
                return default;
            }
        }

        private async Task<(double AvgTrendScore, decimal? AvgPrice, int SourceCount, string? Currency)> LoadMarketplaceFallbackAsync(
            string? brandLike,
            string? categoryLike,
            CancellationToken ct)
        {
            async Task<(int Count, double AvgTrend, decimal? AvgPrice, string? Currency)> AggregateAmazon()
            {
                var q = _analyticsDb.AmazonShoeProducts.AsNoTracking();
                if (!string.IsNullOrWhiteSpace(brandLike)) q = q.Where(x => x.Brand != null && EF.Functions.ILike(x.Brand, brandLike));
                if (!string.IsNullOrWhiteSpace(categoryLike)) q = q.Where(x => x.Category != null && EF.Functions.ILike(x.Category, categoryLike));
                var rows = await q
                    .OrderByDescending(x => x.LastSynced)
                    .Select(x => new { x.TrendScore, x.Price, x.Currency })
                    .Take(1000)
                    .ToListAsync(ct);
                if (rows.Count == 0) return default;
                return (rows.Count, rows.Average(x => (double)x.TrendScore), rows.Where(x => x.Price.HasValue).Select(x => x.Price!.Value).DefaultIfEmpty().Average(), rows.Where(x => !string.IsNullOrWhiteSpace(x.Currency)).Select(x => x.Currency).FirstOrDefault());
            }

            async Task<(int Count, double AvgTrend, decimal? AvgPrice, string? Currency)> AggregateEbay()
            {
                var q = _analyticsDb.EbayShoeProducts.AsNoTracking();
                if (!string.IsNullOrWhiteSpace(brandLike)) q = q.Where(x => x.Brand != null && EF.Functions.ILike(x.Brand, brandLike));
                if (!string.IsNullOrWhiteSpace(categoryLike)) q = q.Where(x => x.Category != null && EF.Functions.ILike(x.Category, categoryLike));
                var rows = await q
                    .OrderByDescending(x => x.LastSynced)
                    .Select(x => new { x.TrendScore, x.Price, x.Currency })
                    .Take(1000)
                    .ToListAsync(ct);
                if (rows.Count == 0) return default;
                return (rows.Count, rows.Average(x => (double)x.TrendScore), rows.Where(x => x.Price.HasValue).Select(x => x.Price!.Value).DefaultIfEmpty().Average(), rows.Where(x => !string.IsNullOrWhiteSpace(x.Currency)).Select(x => x.Currency).FirstOrDefault());
            }

            async Task<(int Count, double AvgTrend, decimal? AvgPrice, string? Currency)> AggregateGoogle()
            {
                var q = _analyticsDb.GoogleShoppingProducts.AsNoTracking();
                if (!string.IsNullOrWhiteSpace(brandLike)) q = q.Where(x => x.Brand != null && EF.Functions.ILike(x.Brand, brandLike));
                if (!string.IsNullOrWhiteSpace(categoryLike)) q = q.Where(x => x.Category != null && EF.Functions.ILike(x.Category, categoryLike));
                var rows = await q
                    .OrderByDescending(x => x.LastSynced)
                    .Select(x => new { x.TrendScore, x.Price, x.Currency })
                    .Take(1000)
                    .ToListAsync(ct);
                if (rows.Count == 0) return default;
                return (rows.Count, rows.Average(x => (double)x.TrendScore), rows.Where(x => x.Price.HasValue).Select(x => x.Price!.Value).DefaultIfEmpty().Average(), rows.Where(x => !string.IsNullOrWhiteSpace(x.Currency)).Select(x => x.Currency).FirstOrDefault());
            }

            try
            {
                var snapshots = new List<(int Count, double AvgTrend, decimal? AvgPrice, string? Currency)>
                {
                    await AggregateAmazon(),
                    await AggregateEbay(),
                    await AggregateGoogle()
                }.Where(x => x.Count > 0).ToList();

                if (snapshots.Count == 0) return default;
                var total = snapshots.Sum(x => x.Count);
                var avgTrend = snapshots.Sum(x => x.AvgTrend * x.Count) / Math.Max(total, 1);
                var avgPrice = snapshots.Where(x => x.AvgPrice.HasValue).ToList();
                decimal? weightedPrice = null;
                if (avgPrice.Count > 0)
                {
                    weightedPrice = avgPrice.Sum(x => x.AvgPrice!.Value * x.Count) / Math.Max(avgPrice.Sum(x => x.Count), 1);
                    weightedPrice = Math.Round(weightedPrice.Value, 2);
                }

                return (avgTrend, weightedPrice, snapshots.Count, snapshots.Select(x => x.Currency).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable || ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                _logger.LogWarning(ex, "Marketplace fallback tables missing.");
                return default;
            }
        }

        private async Task<(bool Used, double? SellProbability, double? FinalScore, double? PriceFitScore, double? PopularityScore, double? DealScore, double? MarginScore, double? TrendMomentum)> TryPredictWithPythonAsync(
            object payload,
            CancellationToken ct)
        {
            if (!_options.EnablePythonPredict)
            {
                return default;
            }

            try
            {
                var client = _httpClientFactory.CreateClient("PythonModel");
                var endpoint = string.IsNullOrWhiteSpace(_options.PredictPath) ? "/predict" : _options.PredictPath;
                using var response = await client.PostAsJsonAsync(endpoint, payload, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return default;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                var root = doc.RootElement;
                double? Read(params string[] names)
                {
                    foreach (var name in names)
                    {
                        if (!root.TryGetProperty(name, out var v)) continue;
                        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n)) return n;
                        if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out n)) return n;
                    }
                    return null;
                }

                var sell = Read("sellProbabilityRS", "sell_probability_rs", "sell_probability");
                if (sell.HasValue)
                {
                    sell = sell <= 1 ? Clamp01(sell.Value) : Clamp01(sell.Value / 100d);
                }

                double? Score(double? v) => v.HasValue ? ClampScore(v.Value <= 1 ? v.Value * 100d : v.Value) : null;
                return (
                    Used: true,
                    SellProbability: sell,
                    FinalScore: Score(Read("finalScore", "final_score")),
                    PriceFitScore: Score(Read("priceFitScore", "price_fit_score")),
                    PopularityScore: Score(Read("popularityScore", "popularity_score")),
                    DealScore: Score(Read("dealScore", "deal_score")),
                    MarginScore: Score(Read("marginScore", "margin_score")),
                    TrendMomentum: Score(Read("trendMomentum", "trend_momentum")));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Python runtime prediction failed; local scoring kept.");
                return default;
            }
        }

        private static string BuildRange(decimal? baseline, decimal? target, string currency)
        {
            decimal? center = baseline.HasValue && target.HasValue
                ? Math.Round((baseline.Value * 0.6m) + (target.Value * 0.4m), 2)
                : baseline ?? target;
            if (!center.HasValue || center.Value <= 0) return "N/A";
            var low = Math.Round(center.Value * 0.92m, 2);
            var high = Math.Round(center.Value * 1.08m, 2);
            return $"{currency} {low:F2} - {high:F2}";
        }

        private static double ComputePriceFit(decimal? target, decimal? baseline)
        {
            if (!target.HasValue || target.Value <= 0) return 50; // Fallback to average value
            if (!baseline.HasValue || baseline.Value <= 0) return 50;
            var deviation = Math.Abs((double)((target.Value - baseline.Value) / baseline.Value));
            return ClampScore(100d - (deviation * 150d)); // Reduced penalty for deviations
        }

        private static double ComputeMargin(decimal? cost, decimal? target)
        {
            if (!cost.HasValue || !target.HasValue || target.Value <= 0) return 50;
            if (cost.Value >= target.Value) return ClampScore(-50); // Penalize negative margins
            var margin = (double)((target.Value - cost.Value) / target.Value);
            return margin >= 0.70 ? 100 : ClampScore((margin / 0.70) * 100d);
        }

        private static double ComputeDeal(decimal current, decimal baseline)
        {
            if (baseline <= 0) return 50; // Fallback to average value
            var rel = (double)((baseline - current) / baseline);
            if (rel <= 0) return 0;
            if (rel >= 0.30) return 100;
            return ClampScore((rel / 0.30) * 100d);
        }

        private static decimal? Median(IEnumerable<decimal> values)
        {
            var arr = values.OrderBy(x => x).ToArray();
            if (arr.Length == 0) return null;
            var m = arr.Length / 2;
            return arr.Length % 2 == 0 ? Math.Round((arr[m - 1] + arr[m]) / 2m, 2) : arr[m];
        }

        private static double ComputeImageSimilarity(IReadOnlyCollection<SimilarProduct> items)
            // Return 0 when embedding unavailable — do not inflate score artificially
            => items.Count == 0 ? 0 : ClampScore(items.OrderByDescending(x => x.Similarity).Take(3).Average(x => x.Similarity) * 100d);

        private static double NormalizeMarketplaceTrendScore(double score)
        {
            if (score <= 0) return 0;

            // Auto-detect common score scales:
            // - [0..1]   already normalized
            // - [0..30]  ShoeScoring.Compute legacy range
            // - [0..100] percent-like range
            if (score <= 1.2) return ClampScore(score * 100d);
            if (score <= 40) return ClampScore((score / 30d) * 100d);
            return ClampScore(score);
        }
        private static double NormalizeScraperScore(decimal score) => score <= 0 ? 0 : ClampScore(score <= 1 ? (double)score * 100d : (double)score);
        private static double NormalizeMomentum(decimal score) => score <= 0 ? 0 : ClampScore(score <= 1.5m ? (double)score * 100d : (double)score);

        private static string NormalizeMarket(string? market, string fallback)
            => string.IsNullOrWhiteSpace(market) ? (string.IsNullOrWhiteSpace(fallback) ? "RS" : fallback.Trim().ToUpperInvariant()) : market.Trim().ToUpperInvariant();

        // ── Verdict / label / insight helpers ─────────────────────────────────

        private static (string Verdict, string Color) GetVerdict(double score) => score switch
        {
            >= 82 => ("Odlično za prodaju", "green"),
            >= 68 => ("Solidno", "blue"),
            >= 52 => ("Prosečno", "amber"),
            >= 36 => ("Rizično", "orange"),
            _ => ("Neisplativo", "red")
        };

        private static string GetScoreLabel(double score) => score switch
        {
            >= 82 => "Odlično",
            >= 68 => "Dobro",
            >= 52 => "Prosečno",
            >= 36 => "Rizično",
            _ => "Loše"
        };

        private static double ComputeConfidence(
            bool hasTraining, int sourceCoverage, double imageSim, bool hasBaseline)
        {
            double c = 0;
            if (hasTraining)   c += 35;
            c += Math.Min(sourceCoverage * 10, 35);
            if (imageSim > 50) c += 20;
            else if (imageSim > 20) c += 10;
            if (hasBaseline) c += 10;
            return Math.Min(c, 100);
        }

        private static string GetPricePositioning(decimal? target, decimal? baseline)
        {
            if (!target.HasValue || !baseline.HasValue || baseline.Value <= 0) return string.Empty;
            var ratio = (double)(target.Value / baseline.Value);
            if (ratio < 0.92) return "ispod_tržišta";
            if (ratio > 1.08) return "iznad_tržišta";
            return "u_rangu";
        }

        private static List<string> BuildInsights(
            double priceFit, double popularity, double deal, double margin, double trend,
            double imageSim, int sourceCoverage, bool hasTraining, string pricePos)
        {
            var list = new List<string>();

            // Price fit
            if (priceFit >= 80)
                list.Add("✅ Cena je u idealnom rangu za tržište");
            else if (priceFit < 45 && pricePos == "iznad_tržišta")
                list.Add("⚠️ Ciljna cena je iznad tržišnog proseka — razmotri sniženje");
            else if (priceFit < 45 && pricePos == "ispod_tržišta")
                list.Add("ℹ️ Cena je ispod tržišnog proseka — dobro za kupca, manja margina");
            else if (priceFit < 60)
                list.Add("ℹ️ Cena neznatno odstupa od tržišnog ranga");

            // Margin
            if (margin >= 80)
                list.Add("💰 Odlična margina — visok potencijal za profit");
            else if (margin < 30)
                list.Add("⚠️ Niska margina — razmotri korekciju nabavne ili prodajne cene");
            else if (margin < 50)
                list.Add("ℹ️ Margina je umerena — ima prostora za optimizaciju");

            // Popularity
            if (popularity >= 75)
                list.Add("🔥 Popularna kategorija — visoka potražnja na tržištu");
            else if (popularity < 30)
                list.Add("📉 Slaba popularnost — razmotri drugi asortiman");

            // Deal
            if (deal >= 75)
                list.Add("🏷️ Izvanredna cena u odnosu na tržišni standard");
            else if (deal >= 55)
                list.Add("✅ Konkurentna cena u odnosu na tržište");

            // Trend
            if (trend >= 72)
                list.Add("📈 Jak trend rast u ovom segmentu");

            // Training signal
            if (hasTraining)
                list.Add("✅ Trening signal potvrđuje tržišnu procenu");
            else
                list.Add("ℹ️ Nema obučenih signala — procena bazirana isključivo na tržišnim podacima");

            // Data coverage
            if (sourceCoverage == 0)
                list.Add("⚠️ Nema tržišnih podataka — rezultati su orijentacioni");
            else if (sourceCoverage >= 4)
                list.Add($"📊 Procena potvrdjena sa {sourceCoverage} tržišnih izvora");

            // Image similarity
            if (imageSim >= 70)
                list.Add("🖼️ Pronađeni vizuelno slični proizvodi — embedding procena pouzdana");
            else if (imageSim < 10)
                list.Add("ℹ️ Nema vizuelno sličnih proizvoda u bazi");

            return list;
        }

        private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        private static string? ToLike(string? value) => string.IsNullOrWhiteSpace(value) ? null : $"%{value}%";
        private static double NormalizeScore(double value) => ClampScore(value);
        private static double ClampScore(double value) => value < 0 ? 0 : (value > 100 ? 100 : value);
        private static double Clamp01(double value) => value < 0 ? 0 : (value > 1 ? 1 : value);
        private static double Round2(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
        private static double Round4(double value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
    }
}
