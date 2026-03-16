using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Linq;
using Api.Config;
using Api.Models;
using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Domain.Model.OpenProductTraining;
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
        string? ImagePath,
        decimal? Cost,
        decimal? TargetPrice,
        string? Brand,
        string? Category,
        string? Market,
        int?    ArtikalId    = null,
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
        private readonly ISellProbabilityRsOnnxScorer _onnxScorer;
        private readonly AnalyticsDbContext _analyticsDb;
        private readonly OpenProductTrainingDbContext _openTrainingDb;
        private readonly ITrendplusDbContext _trendDb;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IEnterpriseScoringModelProvider _enterpriseModelProvider;
        private readonly RuntimeScoringOptions _options;
        private readonly ILogger<RuntimeScoringEngine> _logger;
        private readonly EnterpriseScoringLinearModel _enterpriseFallbackModel;

        private sealed record FeatureStoreSnapshot(long ProductId, int? LocalProductId, IReadOnlyDictionary<string, float> Features);

        public RuntimeScoringEngine(
            IEmbeddingService embeddingService,
            IOpenProductTrainingSignalProvider trainingSignals,
            ISellProbabilityRsOnnxScorer onnxScorer,
            AnalyticsDbContext analyticsDb,
            OpenProductTrainingDbContext openTrainingDb,
            ITrendplusDbContext trendDb,
            IHttpClientFactory httpClientFactory,
            IEnterpriseScoringModelProvider enterpriseModelProvider,
            IOptionsSnapshot<RuntimeScoringOptions> options,
            ILogger<RuntimeScoringEngine> logger)
        {
            _embeddingService = embeddingService;
            _trainingSignals = trainingSignals;
            _onnxScorer = onnxScorer;
            _analyticsDb = analyticsDb;
            _openTrainingDb = openTrainingDb;
            _trendDb = trendDb;
            _httpClientFactory = httpClientFactory;
            _enterpriseModelProvider = enterpriseModelProvider;
            _options = options.Value;
            _logger = logger;
            _enterpriseFallbackModel = new EnterpriseScoringLinearModel(_options.Enterprise);
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

            var trainingTask = _trainingSignals.ResolveAsync(brand, category, input.TargetPrice, ct);
            var scrapedTask = LoadScraperSignalsAsync(brandLike, categoryLike, market, ct);
            var marketplaceTask = LoadMarketplaceFallbackAsync(brandLike, categoryLike, ct);
            var shopifyTask = LoadShopifySignalsAsync(brandLike, categoryLike, input.TargetPrice, ct);

            await Task.WhenAll(trainingTask, scrapedTask, marketplaceTask, shopifyTask);

            var training = await trainingTask;
            var scraped = await scrapedTask;
            var marketplace = await marketplaceTask;
            var shopify = await shopifyTask;

            float[]? embedding = null;
            List<SimilarProduct> similarProducts = new();
            if (!string.IsNullOrWhiteSpace(input.ImagePath))
            {
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
            }

            var enterpriseRuntimeModel = _options.Enterprise.Enabled
                ? await ResolveEnterpriseRuntimeModelAsync(ct)
                : (Model: (EnterpriseScoringLinearModel?)null, Version: (int?)null, ModelType: (string?)null, FromDatabase: false, Tuning: (RuntimeScoringTuningOptions?)null);
            var activeTuning = enterpriseRuntimeModel.Tuning ?? _options.Tuning;

            var imageSimilarityScore = ComputeImageSimilarity(similarProducts);
            var coverageItemsPerUnit = Math.Max(1, activeTuning.MarketplaceCoverageItemsPerUnit);
            var coverageMaxUnits = Math.Max(0, activeTuning.MarketplaceCoverageMaxUnits);
            var marketplaceCoverageUnits = Math.Min(marketplace.SourceCount / coverageItemsPerUnit, coverageMaxUnits);
            var shopifyCoverageUnits = Math.Min(shopify.MatchCount / coverageItemsPerUnit, coverageMaxUnits);
            var sourceCoverageCount = scraped.SourceCount + marketplaceCoverageUnits + shopifyCoverageUnits;
            // capped coverage buckets: scraper sources + marketplace depth buckets
            var coverageNormalizationUnits = Math.Max(1, activeTuning.SourceCoverageNormalizationMaxUnits);
            var sourceCoverageScore = ClampScore((Math.Min(sourceCoverageCount, coverageNormalizationUnits) / (double)coverageNormalizationUnits) * 100d);

            // ── Local-data signals (from own sales / seasonal DB) ──────────────────
            var supplierScore  = await ComputeSupplierScoreAsync(input.DobavljacId);
            var shoeTypeScore  = await ComputeShoeTypeScoreAsync(input.TipObuceId);
            var seasonalScore  = await ComputeSeasonalScoreAsync(input.SezonaId);
            var sizeColorScore = await ComputeSizeColorScoreAsync(input.Velicina, input.Boja);
            var materialScore  = ComputeMaterialScore(input.Materijal);

            var baselinePrice = Median(new[] { scraped.MedianPrice, marketplace.AvgPrice, training.TypicalPrice, shopify.MedianPrice }
                .Where(x => x.HasValue)
                .Select(x => x!.Value));

            var priceFitScore = ComputePriceFit(input.TargetPrice, baselinePrice, activeTuning);
            var marginScore = ComputeMargin(input.Cost, input.TargetPrice);

            var scrapedFinalScore = NormalizeScraperScore(scraped.AvgFinalScore);
            var marketplaceTrendScore = NormalizeMarketplaceTrendScore(marketplace.AvgTrendScore);
            var trainingPopularity = ClampScore((double)training.PopularityPriorScore);

            // Shopify cross-market price intelligence
            var shopifyPriceSignalScore = shopify.HasSignal
                ? ComputePriceFit(input.TargetPrice, shopify.MedianPrice, activeTuning)
                : 0d;

            // Popularity should gracefully degrade when training signals are missing.
            // Blend training prior with scraper; boost with Shopify breadth when available.
            var popularityScore = trainingPopularity > 0
                ? NormalizeScore(0.65 * trainingPopularity + 0.25 * scrapedFinalScore + 0.10 * (shopify.HasSignal ? shopifyPriceSignalScore : scrapedFinalScore))
                : shopify.HasSignal
                    ? NormalizeScore(0.50 * scrapedFinalScore + 0.30 * marketplaceTrendScore + 0.20 * shopifyPriceSignalScore)
                    : NormalizeScore(0.60 * scrapedFinalScore + 0.40 * marketplaceTrendScore);

            // Trend momentum: prefer scraper momentum; fallback to marketplace trend.
            var scrapedMomentumScore = NormalizeMomentum(scraped.AvgMomentum);
            var trendMomentum = scrapedMomentumScore > 0
                ? NormalizeScore(0.80 * scrapedMomentumScore + 0.20 * scrapedFinalScore)
                : marketplaceTrendScore;

            // Deal: scenario-specific (target vs baseline) with optional training prior.
            var dealFromBaseline = input.TargetPrice.HasValue && baselinePrice.HasValue
                ? ComputeDeal(input.TargetPrice.Value, baselinePrice.Value, activeTuning)
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

            var heuristicSellProbability = Clamp01(sellProbabilityScore / 100d);
            var canonicalFeatures = BuildEnterpriseCanonicalFeatures(
                priceFitScore,
                marginScore,
                popularityScore,
                trendMomentum,
                sourceCoverageScore,
                normalizedLocalDemandScore,
                imageSimilarityScore,
                dealScore,
                supplierScore,
                seasonalScore);
            var enterprise = enterpriseRuntimeModel.Model?.Compute(canonicalFeatures);

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

            var localProductId = await ResolveLocalProductIdAsync(input, ct);
            var featureStoreSnapshot = await TryLoadFeatureStoreSnapshotAsync(
                localProductId,
                brandLike,
                categoryLike,
                input.TargetPrice,
                ct);

            // Prefer ONNX in-process inference (fast) and keep Python as fallback (optional).
            SellProbabilityRsModelResult? onnx = null;
            try
            {
                var onnxFeatures = BuildSellProbabilityRsOnnxFeatures(
                    price: input.TargetPrice ?? baselinePrice ?? training.TypicalPrice,
                    training: training,
                    momentumProxy: scraped.AvgMomentum,
                    hasImageEmbedding: embedding is not null,
                    brand: brand,
                    featureStoreFeatures: featureStoreSnapshot?.Features);

                onnx = await _onnxScorer.TryPredictAsync(onnxFeatures, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ONNX sell_probability_rs prediction failed; keeping local scoring.");
            }

            var python = default((bool Used, double? SellProbability, double? FinalScore, double? PriceFitScore, double? PopularityScore, double? DealScore, double? MarginScore, double? TrendMomentum));
            if (onnx is null)
            {
                python = await TryPredictWithPythonAsync(pythonPayload, ct);
            }

            var externalModelProbability = onnx?.Prediction ?? python.SellProbability;
            var sellProbability = ResolveFinalSellProbability(
                heuristicSellProbability,
                externalModelProbability,
                enterprise?.FinalProbability,
                usedOnnxModel: onnx is not null,
                hasFeatureStoreFeatures: featureStoreSnapshot is not null);

            var currency = scraped.Currency ?? marketplace.Currency ?? "EUR";
            var priceRange = BuildRange(baselinePrice, input.TargetPrice, currency);
            var pricePos = GetPricePositioning(input.TargetPrice, baselinePrice);

            sellProbability = ApplyBusinessProbabilityOverlay(
                sellProbability,
                pricePositioning: pricePos,
                sourceCoverageCount: sourceCoverageCount,
                marginScore: marginScore,
                hasTrainingSignal: training.HasTrainingSignal,
                dealScore: dealScore);

            // Re-anchor score to the final probability so FinalScore stays consistent with SellProbabilityRS.
            sellProbabilityScore = NormalizeScore(sellProbability * 100d);

            var localFinalScore = NormalizeScore(
                0.55 * sellProbabilityScore +
                0.15 * priceFitScore +
                0.10 * marginScore +
                0.10 * trendMomentum +
                0.10 * popularityScore);

            var finalScore = python.FinalScore.HasValue
                ? NormalizeScore((localFinalScore * 0.4) + (python.FinalScore.Value * 0.6))
                : localFinalScore;

            var effPriceFit  = Round2(python.PriceFitScore  ?? priceFitScore);
            var effPopularity = Round2(python.PopularityScore ?? popularityScore);
            var effDeal      = Round2(python.DealScore      ?? dealScore);
            var effMargin    = Round2(python.MarginScore    ?? marginScore);
            var effTrend     = Round2(python.TrendMomentum  ?? trendMomentum);

            var (verdict, verdictColor) = GetVerdict(finalScore);
            var scoreLabel   = GetScoreLabel(finalScore);
            var confidence   = ComputeConfidence(training.HasTrainingSignal, sourceCoverageCount, imageSimilarityScore, baselinePrice.HasValue, activeTuning);
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
                ShopifyPriceSignalScore = Round2(shopifyPriceSignalScore),
                ShopifyMatchCount = shopify.MatchCount,
                ShopifyMedianPrice = shopify.MedianPrice,
                HasShopifySignal = shopify.HasSignal,
                UsedPythonModel = python.Used,
                UsedOnnxModel = onnx is not null,
                OnnxModelType = onnx?.ModelType,
                OnnxModelVersion = onnx?.Version,
                OnnxRawSellProbability = onnx?.RawPrediction,
                OnnxSellProbability = onnx?.Prediction,
                UsedEnterpriseModel = enterprise is not null,
                EnterpriseLinearScore = enterprise?.LinearScore,
                EnterpriseRawProbability = enterprise?.RawProbability,
                EnterpriseSoftmaxProbability = enterprise?.SoftmaxProbability,
                EnterpriseCalibratedProbability = enterprise?.CalibratedProbability,
                EnterpriseFinalProbability = enterprise?.FinalProbability,
                EnterpriseModelType = enterpriseRuntimeModel.ModelType,
                EnterpriseModelVersion = enterpriseRuntimeModel.Version,
                EnterpriseModelFromDatabase = enterpriseRuntimeModel.FromDatabase,
                ExternalModelProbability = externalModelProbability,
                UsedFeatureStoreFeatures = featureStoreSnapshot is not null,
                FeatureStoreLocalProductId = featureStoreSnapshot?.LocalProductId,
                FeatureStoreTrainingProductId = featureStoreSnapshot?.ProductId,
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
                    COALESCE((
                        SELECT COUNT(DISTINCT s.source_name)
                        FROM (
                            SELECT DISTINCT item_id, source_name, market
                            FROM item_sources
                        ) s
                        JOIN filtered f2 ON f2.item_id = s.item_id
                        WHERE s.market = @market
                    ), 0),
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
                var amazonTask = AggregateAmazon();
                var ebayTask = AggregateEbay();
                var googleTask = AggregateGoogle();
                await Task.WhenAll(amazonTask, ebayTask, googleTask);

                var snapshots = new List<(int Count, double AvgTrend, decimal? AvgPrice, string? Currency)>
                {
                    amazonTask.Result,
                    ebayTask.Result,
                    googleTask.Result
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

                return (avgTrend, weightedPrice, total, snapshots.Select(x => x.Currency).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable || ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                _logger.LogWarning(ex, "Marketplace fallback tables missing.");
                return default;
            }
        }

        // ── Shopify cross-market intelligence ─────────────────────────────────
        /// <summary>
        /// Queries Shopify-sourced training products by brand/category to provide
        /// additional cross-market price signals and product availability depth.
        /// This enriches baseline price, source coverage, and popularity scoring.
        /// </summary>
        private async Task<(decimal? MedianPrice, int MatchCount, bool HasSignal)> LoadShopifySignalsAsync(
            string? brandLike,
            string? categoryLike,
            decimal? targetPrice,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(brandLike) && string.IsNullOrWhiteSpace(categoryLike))
                return default;

            try
            {
                // Shopify datasets have SourceType = 'shopify'
                var shopifyDatasetIds = await _openTrainingDb.Datasets
                    .AsNoTracking()
                    .Where(d => d.SourceType == "shopify")
                    .Select(d => d.Id)
                    .ToListAsync(ct);

                if (shopifyDatasetIds.Count == 0)
                    return default;

                var q = _openTrainingDb.Products
                    .AsNoTracking()
                    .Where(p => shopifyDatasetIds.Contains(p.DatasetId) && p.Price.HasValue && p.Price > 0);

                if (!string.IsNullOrWhiteSpace(brandLike))
                    q = q.Where(p => p.Brand != null && EF.Functions.ILike(p.Brand.Name, brandLike));

                if (!string.IsNullOrWhiteSpace(categoryLike))
                    q = q.Where(p => p.Category != null && EF.Functions.ILike(p.Category.Name, categoryLike));

                var prices = await q
                    .OrderByDescending(p => p.UpdatedAt)
                    .Select(p => p.Price!.Value)
                    .Take(500)
                    .ToListAsync(ct);

                if (prices.Count == 0)
                    return default;

                var sorted = prices.OrderBy(x => x).ToList();
                var mid = sorted.Count / 2;
                var medianPrice = sorted.Count % 2 == 0
                    ? Math.Round((sorted[mid - 1] + sorted[mid]) / 2m, 2)
                    : sorted[mid];

                return (medianPrice, prices.Count, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Shopify signals loading failed; scoring continues without Shopify data.");
                return default;
            }
        }

        private async Task<int?> ResolveLocalProductIdAsync(RuntimeScoringEngineInput input, CancellationToken ct)
        {
            if (input.ArtikalId.HasValue)
                return input.ArtikalId.Value;

            var hasFilter =
                input.DobavljacId.HasValue ||
                input.TipObuceId.HasValue ||
                input.SezonaId.HasValue ||
                !string.IsNullOrWhiteSpace(input.Category) ||
                !string.IsNullOrWhiteSpace(input.Velicina) ||
                !string.IsNullOrWhiteSpace(input.Boja) ||
                !string.IsNullOrWhiteSpace(input.Materijal);

            if (!hasFilter)
                return null;

            var query = _trendDb.Artikli.AsNoTracking();

            if (input.DobavljacId.HasValue)
                query = query.Where(a => a.IDDobavljac == input.DobavljacId.Value);
            if (input.TipObuceId.HasValue)
                query = query.Where(a => a.IDTipObuce == input.TipObuceId.Value);
            if (input.SezonaId.HasValue)
                query = query.Where(a => a.IDSezona == input.SezonaId.Value);

            var categoryLike = ToLike(NormalizeText(input.Category));
            if (!string.IsNullOrWhiteSpace(categoryLike))
                query = query.Where(a => a.Kategorija != null && EF.Functions.ILike(a.Kategorija, categoryLike));

            var sizeLike = ToLike(NormalizeText(input.Velicina));
            if (!string.IsNullOrWhiteSpace(sizeLike))
                query = query.Where(a => a.Velicina != null && EF.Functions.ILike(a.Velicina, sizeLike));

            var colorLike = ToLike(NormalizeText(input.Boja));
            if (!string.IsNullOrWhiteSpace(colorLike))
                query = query.Where(a => a.Boja != null && EF.Functions.ILike(a.Boja, colorLike));

            var materialLike = ToLike(NormalizeText(input.Materijal));
            if (!string.IsNullOrWhiteSpace(materialLike))
                query = query.Where(a => a.Materijal != null && EF.Functions.ILike(a.Materijal, materialLike));

            var candidates = await query
                .OrderByDescending(a => a.UpdatedAt)
                .Select(a => new { a.Id, a.ProdajnaCena, a.UpdatedAt })
                .Take(200)
                .ToListAsync(ct);

            if (candidates.Count == 0)
                return null;

            if (input.TargetPrice.HasValue)
            {
                var target = input.TargetPrice.Value;
                var bestByPrice = candidates
                    .Where(c => c.ProdajnaCena.HasValue && c.ProdajnaCena.Value > 0)
                    .OrderBy(c => Math.Abs((double)(c.ProdajnaCena!.Value - target)))
                    .ThenByDescending(c => c.UpdatedAt)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefault();

                if (bestByPrice.HasValue)
                    return bestByPrice.Value;
            }

            return candidates
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => (int?)c.Id)
                .FirstOrDefault();
        }

        private async Task<FeatureStoreSnapshot?> TryLoadFeatureStoreSnapshotAsync(
            int? localProductId,
            string? brandLike,
            string? categoryLike,
            decimal? targetPrice,
            CancellationToken ct)
        {
            var connectionString = _openTrainingDb.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
                return null;

            const string selectColumns = """
                SELECT
                    product_id,
                    local_product_id,
                    price,
                    avg_rating,
                    review_count,
                    sentiment_score,
                    review_velocity_30d_proxy,
                    volatility_7d,
                    volatility_30d,
                    volatility_90d,
                    momentum_7d,
                    momentum_30d,
                    momentum_90d,
                    discount_freq_30d,
                    discount_freq_90d,
                    typical_change_rate_30d,
                    popularity_prior,
                    deal_score_prior,
                    typical_price_prior,
                    priors_level,
                    has_image_embedding,
                    image_cluster_id,
                    rs_sold_qty_30d,
                    rs_inflow_qty_30d,
                    sell_through_velocity_30d,
                    supply_demand_ratio_30d,
                    median_days_to_sale_proxy,
                    price_elasticity_90d
                FROM vw_feature_store
                """;

            const string sqlByLocalProduct = selectColumns + """
                WHERE local_product_id = @localProductId
                ORDER BY updated_at DESC NULLS LAST, product_id DESC
                LIMIT 1;
                """;

            const string sqlByBrandCategory = selectColumns + """
                WHERE (@brand IS NULL OR brand ILIKE @brand)
                  AND (@category IS NULL OR category ILIKE @category)
                ORDER BY
                    CASE
                        WHEN @targetPrice IS NULL OR price IS NULL OR price <= 0 THEN 999999.0
                        ELSE ABS(price - @targetPrice)
                    END,
                    updated_at DESC NULLS LAST,
                    product_id DESC
                LIMIT 1;
                """;

            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync(ct);

                if (localProductId.HasValue)
                {
                    await using var byLocal = new NpgsqlCommand(sqlByLocalProduct, conn);
                    byLocal.Parameters.AddWithValue("localProductId", localProductId.Value);
                    await using var localReader = await byLocal.ExecuteReaderAsync(ct);
                    if (await localReader.ReadAsync(ct))
                        return ReadFeatureStoreSnapshot(localReader);
                }

                if (string.IsNullOrWhiteSpace(brandLike) && string.IsNullOrWhiteSpace(categoryLike))
                    return null;

                await using var byBrand = new NpgsqlCommand(sqlByBrandCategory, conn);
                byBrand.Parameters.AddWithValue("brand", (object?)brandLike ?? DBNull.Value);
                byBrand.Parameters.AddWithValue("category", (object?)categoryLike ?? DBNull.Value);
                byBrand.Parameters.AddWithValue("targetPrice", (object?)targetPrice ?? DBNull.Value);
                await using var brandReader = await byBrand.ExecuteReaderAsync(ct);
                if (await brandReader.ReadAsync(ct))
                    return ReadFeatureStoreSnapshot(brandReader);

                return null;
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable || ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                _logger.LogDebug(ex, "Feature store view unavailable. Continuing with heuristic runtime features.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load feature store snapshot for ONNX runtime scoring.");
                return null;
            }
        }

        private static FeatureStoreSnapshot ReadFeatureStoreSnapshot(NpgsqlDataReader reader)
        {
            var productIdOrd = reader.GetOrdinal("product_id");
            var localProductIdOrd = reader.GetOrdinal("local_product_id");

            var productId = reader.IsDBNull(productIdOrd)
                ? 0L
                : Convert.ToInt64(reader.GetValue(productIdOrd), CultureInfo.InvariantCulture);

            int? localProductId = null;
            if (!reader.IsDBNull(localProductIdOrd))
                localProductId = Convert.ToInt32(reader.GetValue(localProductIdOrd), CultureInfo.InvariantCulture);

            float ReadFloat(string name)
            {
                var ord = reader.GetOrdinal(name);
                if (reader.IsDBNull(ord))
                    return 0f;

                var value = reader.GetValue(ord);
                return value switch
                {
                    float f => f,
                    double d => (float)d,
                    decimal m => (float)m,
                    int i => i,
                    long l => l,
                    short s => s,
                    bool b => b ? 1f : 0f,
                    _ => float.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : 0f
                };
            }

            var priorsLevelOrd = reader.GetOrdinal("priors_level");
            var priorsLevelRaw = reader.IsDBNull(priorsLevelOrd) ? null : Convert.ToString(reader.GetValue(priorsLevelOrd), CultureInfo.InvariantCulture);

            var features = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["price"] = ReadFloat("price"),
                ["avg_rating"] = ReadFloat("avg_rating"),
                ["review_count"] = ReadFloat("review_count"),
                ["sentiment_score"] = ReadFloat("sentiment_score"),
                ["review_velocity_30d_proxy"] = ReadFloat("review_velocity_30d_proxy"),
                ["volatility_7d"] = ReadFloat("volatility_7d"),
                ["volatility_30d"] = ReadFloat("volatility_30d"),
                ["volatility_90d"] = ReadFloat("volatility_90d"),
                ["momentum_7d"] = ReadFloat("momentum_7d"),
                ["momentum_30d"] = ReadFloat("momentum_30d"),
                ["momentum_90d"] = ReadFloat("momentum_90d"),
                ["discount_freq_30d"] = ReadFloat("discount_freq_30d"),
                ["discount_freq_90d"] = ReadFloat("discount_freq_90d"),
                ["typical_change_rate_30d"] = ReadFloat("typical_change_rate_30d"),
                ["popularity_prior"] = ReadFloat("popularity_prior"),
                ["deal_score_prior"] = ReadFloat("deal_score_prior"),
                ["typical_price_prior"] = ReadFloat("typical_price_prior"),
                ["priors_level"] = PriorsLevelToCode(priorsLevelRaw),
                ["has_image_embedding"] = ReadFloat("has_image_embedding"),
                ["image_cluster_id"] = ReadFloat("image_cluster_id"),
                ["rs_sold_qty_30d"] = ReadFloat("rs_sold_qty_30d"),
                ["rs_inflow_qty_30d"] = ReadFloat("rs_inflow_qty_30d"),
                ["sell_through_velocity_30d"] = ReadFloat("sell_through_velocity_30d"),
                ["supply_demand_ratio_30d"] = ReadFloat("supply_demand_ratio_30d"),
                ["median_days_to_sale_proxy"] = ReadFloat("median_days_to_sale_proxy"),
                ["price_elasticity_90d"] = ReadFloat("price_elasticity_90d"),
            };

            return new FeatureStoreSnapshot(productId, localProductId, features);
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

        private async Task<(EnterpriseScoringLinearModel? Model, int? Version, string? ModelType, bool FromDatabase, RuntimeScoringTuningOptions? Tuning)> ResolveEnterpriseRuntimeModelAsync(CancellationToken ct)
        {
            if (!_options.Enterprise.Enabled)
                return (null, null, null, false, null);

            if (!_options.Enterprise.PreferModelVersionParameters)
                return (_enterpriseFallbackModel, null, _options.Enterprise.ModelType, false, null);

            try
            {
                var dbModel = await _enterpriseModelProvider.TryGetActiveAsync(ct);
                if (dbModel is null || dbModel.FeatureWeights.Count == 0)
                    return (_enterpriseFallbackModel, null, _options.Enterprise.ModelType, false, null);

                var model = new EnterpriseScoringLinearModel(_options.Enterprise, dbModel);
                return (model, dbModel.Version, dbModel.ModelType, true, dbModel.RuntimeTuning);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed loading enterprise model_version parameters; using config fallback.");
                return (_enterpriseFallbackModel, null, _options.Enterprise.ModelType, false, null);
            }
        }

        private static IReadOnlyDictionary<string, double> BuildEnterpriseCanonicalFeatures(
            double priceFitScore,
            double marginScore,
            double popularityScore,
            double trendMomentum,
            double sourceCoverageScore,
            double localDemandScore,
            double imageSimilarityScore,
            double dealScore,
            double supplierScore,
            double seasonScore)
            => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["price_fit"] = Clamp01(priceFitScore / 100d),
                ["margin"] = Clamp01(marginScore / 100d),
                ["popularity"] = Clamp01(popularityScore / 100d),
                ["trend_momentum"] = Clamp01(trendMomentum / 100d),
                ["source_coverage"] = Clamp01(sourceCoverageScore / 100d),
                ["local_demand"] = Clamp01(localDemandScore / 100d),
                ["image_similarity"] = Clamp01(imageSimilarityScore / 100d),
                ["deal_score"] = Clamp01(dealScore / 100d),
                ["supplier_score"] = Clamp01(supplierScore / 100d),
                ["season_score"] = Clamp01(seasonScore / 100d),
            };

        private double ResolveFinalSellProbability(
            double heuristicProbability,
            double? externalModelProbability,
            double? enterpriseProbability,
            bool usedOnnxModel,
            bool hasFeatureStoreFeatures)
        {
            heuristicProbability = Clamp01(heuristicProbability);
            var external = externalModelProbability.HasValue ? Clamp01(externalModelProbability.Value) : (double?)null;
            var enterprise = enterpriseProbability.HasValue ? Clamp01(enterpriseProbability.Value) : (double?)null;

            if (!_options.Enterprise.Enabled)
            {
                if (!external.HasValue)
                    return heuristicProbability;

                if (usedOnnxModel)
                {
                    var confidenceWeight = hasFeatureStoreFeatures ? 0.70 : 0.50;
                    return BlendProbabilities(
                        (heuristicProbability, 1d - confidenceWeight),
                        (external.Value, confidenceWeight));
                }

                return BlendProbabilities(
                    (heuristicProbability, 0.40),
                    (external.Value, 0.60));
            }

            if (external.HasValue)
            {
                var baseExternalWeight = Clamp01(_options.Enterprise.ExternalModelWeight);
                if (usedOnnxModel)
                {
                    var confidenceWeight = hasFeatureStoreFeatures ? 0.70 : 0.50;
                    // keep configurable weight, but ensure ONNX confidence rule has minimum influence
                    baseExternalWeight = Math.Max(baseExternalWeight, confidenceWeight);
                }

                var remainingWeight = Math.Max(0d, 1d - baseExternalWeight);
                var baseHeuristicWeight = Math.Max(0d, _options.Enterprise.HeuristicWeightWithExternalModel);
                var baseEnterpriseWeight = Math.Max(0d, _options.Enterprise.EnterpriseWeightWithExternalModel);
                var baseRemainder = baseHeuristicWeight + baseEnterpriseWeight;

                var heuristicWeight = baseRemainder > 0d
                    ? remainingWeight * (baseHeuristicWeight / baseRemainder)
                    : remainingWeight * 0.5;
                var enterpriseWeight = baseRemainder > 0d
                    ? remainingWeight * (baseEnterpriseWeight / baseRemainder)
                    : remainingWeight * 0.5;

                return BlendProbabilities(
                    (heuristicProbability, heuristicWeight),
                    (external.Value, baseExternalWeight),
                    (enterprise ?? heuristicProbability, enterpriseWeight));
            }

            return BlendProbabilities(
                (heuristicProbability, _options.Enterprise.HeuristicWeightWithoutExternalModel),
                (enterprise ?? heuristicProbability, _options.Enterprise.EnterpriseWeightWithoutExternalModel));
        }

        private static double ApplyBusinessProbabilityOverlay(
            double probability,
            string pricePositioning,
            int sourceCoverageCount,
            double marginScore,
            bool hasTrainingSignal,
            double dealScore)
        {
            var adjusted = Clamp01(probability);

            if (sourceCoverageCount <= 0 && !hasTrainingSignal)
                adjusted *= 0.88;

            if (marginScore < 15)
                adjusted *= 0.82;

            if (pricePositioning == "iznad_tržišta" && dealScore < 20)
                adjusted *= 0.92;

            if (hasTrainingSignal && sourceCoverageCount >= 2 && marginScore >= 40 && dealScore >= 35)
                adjusted = Math.Min(1d, adjusted * 1.05);

            return Clamp01(adjusted);
        }

        private static double BlendProbabilities(params (double Value, double Weight)[] parts)
        {
            if (parts.Length == 0)
                return 0d;

            var sumWeights = parts.Sum(x => x.Weight > 0 ? x.Weight : 0d);
            if (sumWeights <= 0d)
                return Clamp01(parts[0].Value);

            var weighted = parts.Sum(x =>
            {
                var weight = x.Weight > 0 ? x.Weight : 0d;
                return Clamp01(x.Value) * weight;
            });

            return Clamp01(weighted / sumWeights);
        }

        private static IReadOnlyDictionary<string, float> BuildSellProbabilityRsOnnxFeatures(
            decimal? price,
            RuntimeScoringSignals training,
            decimal momentumProxy,
            bool hasImageEmbedding,
            string? brand,
            IReadOnlyDictionary<string, float>? featureStoreFeatures = null)
        {
            static float F(decimal? v) => v.HasValue ? (float)v.Value : 0f;

            static float MomentumAsReturn(decimal v)
            {
                if (v == 0) return 0f;
                var m = (double)v;
                if (m > 1.5) m /= 100.0; // heuristics: detect percent-like scale
                m = Math.Clamp(m, -1.0, 1.0);
                return (float)m;
            }

            var priorsLevel = training.HasTrainingSignal
                ? (string.IsNullOrWhiteSpace(brand) ? 2f : 3f)
                : 0f;

            var features = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["price"] = F(price),
                ["avg_rating"] = 0f,
                ["review_count"] = 0f,
                ["sentiment_score"] = 0f,
                ["review_velocity_30d_proxy"] = 0f,
                ["volatility_7d"] = 0f,
                ["volatility_30d"] = 0f,
                ["volatility_90d"] = 0f,
                ["momentum_7d"] = 0f,
                ["momentum_30d"] = MomentumAsReturn(momentumProxy),
                ["momentum_90d"] = 0f,
                ["discount_freq_30d"] = 0f,
                ["discount_freq_90d"] = 0f,
                ["typical_change_rate_30d"] = 0f,
                ["popularity_prior"] = F(training.PopularityPriorScore),
                ["deal_score_prior"] = F(training.DealScore),
                ["typical_price_prior"] = F(training.TypicalPrice),
                ["priors_level"] = priorsLevel,
                ["has_image_embedding"] = hasImageEmbedding ? 1f : 0f,
                ["image_cluster_id"] = -1f,
                ["rs_sold_qty_30d"] = 0f,
                ["rs_inflow_qty_30d"] = 0f,
                ["sell_through_velocity_30d"] = 0f,
                ["supply_demand_ratio_30d"] = 0f,
                ["median_days_to_sale_proxy"] = 0f,
                ["price_elasticity_90d"] = 0f,
            };

            if (featureStoreFeatures is not null)
            {
                foreach (var (key, value) in featureStoreFeatures)
                {
                    if (!float.IsNaN(value) && !float.IsInfinity(value))
                        features[key] = value;
                }
            }

            // Runtime request/market signals have precedence when available.
            if (price.HasValue && price.Value > 0)
                features["price"] = F(price);
            if (training.PopularityPriorScore > 0)
                features["popularity_prior"] = F(training.PopularityPriorScore);
            if (training.DealScore > 0)
                features["deal_score_prior"] = F(training.DealScore);
            if (training.TypicalPrice.HasValue && training.TypicalPrice.Value > 0)
                features["typical_price_prior"] = F(training.TypicalPrice);
            if (training.HasTrainingSignal)
                features["priors_level"] = Math.Max(features["priors_level"], priorsLevel);

            if (Math.Abs(features["momentum_30d"]) < 1e-6f)
                features["momentum_30d"] = MomentumAsReturn(momentumProxy);

            if (hasImageEmbedding)
                features["has_image_embedding"] = Math.Max(features["has_image_embedding"], 1f);

            return features;
        }

        private static float PriorsLevelToCode(string? priorsLevel)
        {
            if (string.IsNullOrWhiteSpace(priorsLevel))
                return 0f;

            return priorsLevel.Trim().ToLowerInvariant() switch
            {
                "brand_category" => 3f,
                "category" => 2f,
                "brand" => 1f,
                _ => 0f
            };
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

        private static double ComputePriceFit(decimal? target, decimal? baseline, RuntimeScoringTuningOptions tuning)
        {
            if (!target.HasValue || target.Value <= 0) return 50; // Fallback to average value
            if (!baseline.HasValue || baseline.Value <= 0) return 50;

            var deviation = Math.Abs((double)((target.Value - baseline.Value) / baseline.Value));

            // Smooth exponential decay avoids sudden drops for small deviations.
            var decay = Math.Clamp(tuning.PriceFitExponentialDecay, 0.05, 50d);
            var score = 100d * Math.Exp(-decay * deviation);
            return ClampScore(score);
        }

        private static double ComputeMargin(decimal? cost, decimal? target)
        {
            if (!cost.HasValue || !target.HasValue || target.Value <= 0) return 50;

            if (cost.Value >= target.Value)
                return 5; // explicit catastrophic margin signal, not generic zero

            var margin = (double)((target.Value - cost.Value) / target.Value);

            const double optimalMargin = 0.60;
            return ClampScore((margin / optimalMargin) * 100d);
        }

        private static double ComputeDeal(decimal current, decimal baseline, RuntimeScoringTuningOptions tuning)
        {
            if (baseline <= 0) return 50; // Fallback to average value
            var rel = (double)((baseline - current) / baseline);
            if (rel <= 0) return 0;

            // Smooth saturation without hard threshold.
            var multiplier = Math.Clamp(tuning.DealTanhMultiplier, 0.1, 100d);
            return ClampScore(100d * Math.Tanh(rel * multiplier));
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
        private static double NormalizeMomentum(decimal score)
        {
            if (score == 0) return 0;

            var m = (double)score;
            if (m > 1.5) m /= 100.0; // detect percent-like scale
            m = Math.Clamp(m, -1.0, 1.0);
            return ClampScore((m + 1.0) * 50.0); // maps [-1..1] -> [0..100]
        }

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
            bool hasTraining, int sourceCoverage, double imageSim, bool hasBaseline, RuntimeScoringTuningOptions tuning)
        {
            var baseConfidence = Math.Clamp(tuning.ConfidenceBase, 0d, 100d);
            var trainingBonus = Math.Max(0d, tuning.ConfidenceTrainingBonus);
            var perSource = Math.Max(0d, tuning.ConfidencePerSource);
            var sourceCap = Math.Max(0d, tuning.ConfidenceSourceCap);
            var imageDivisor = Math.Max(0.001d, tuning.ConfidenceImageDivisor);
            var imageCap = Math.Max(0d, tuning.ConfidenceImageCap);
            var baselineBonus = Math.Max(0d, tuning.ConfidenceBaselineBonus);
            var confidenceCap = Math.Clamp(tuning.ConfidenceCap, 1d, 100d);

            double c = baseConfidence; // floor confidence, never claim certainty from sparse signals
            if (hasTraining) c += trainingBonus;
            c += Math.Min(sourceCoverage * perSource, sourceCap);
            c += Math.Min(imageSim / imageDivisor, imageCap);
            if (hasBaseline) c += baselineBonus;
            return Math.Min(c, confidenceCap);
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
                list.Add($"📊 Procena potvrđena sa {sourceCoverage} tržišnih izvora (uključujući Shopify)");

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
