using System.Globalization;
using System.Text;
using Npgsql;
using Xunit;
using Api.Services;

namespace Api.Tests;

public sealed class SupplierScorecardOracleTests : IClassFixture<PostgresContainerFixture>
{
    private static readonly DateOnly PureAnchor = new(2026, 10, 1);

    private readonly PostgresContainerFixture _fixture;

    public SupplierScorecardOracleTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // ------------------------------------------------------------------
    // Hand-computed golden values: prove the oracle itself before it is
    // compared with SQL.
    // ------------------------------------------------------------------

    [Fact]
    public void GoldenArticleSignals_MatchHandComputedValues()
    {
        var fixture = GoldenFixture.Build(PureAnchor).Fixture;
        var signals = SupplierScorecardOracle.ArticleSignals(fixture, SupplierScorecardOracleOptions.AllTime(PureAnchor))
            .ToDictionary(s => s.ArticleId);

        // A1011: 8 pre units at 100, line cost 40, 2 sold since markdown, stock 2.
        var a1011 = signals[1011];
        Assert.Equal(PureAnchor.AddDays(-40), a1011.FirstMarkdownDate);
        Assert.Equal(8m, a1011.PreQty);
        Assert.Equal(800m, a1011.PreRevenue);
        Assert.Equal(480m, a1011.PreMargin);
        Assert.Equal(4m, a1011.StockBeforeMarkdown);
        Assert.Equal(0.6667m, a1011.PreSellthrough);
        Assert.False(a1011.StockoutBeforeMarkdown);
        Assert.Equal("high", a1011.SignalQuality);

        // A1022: stock 3 + net sold since markdown (3 - 1) - customer-return move 1.
        Assert.Equal(4m, signals[1022].StockBeforeMarkdown);
        Assert.Equal(0.3333m, signals[1022].PreSellthrough);

        // A1041: the DUG receipt in the pre window is not retail turnover.
        Assert.Equal(2m, signals[1041].PreQty);
        Assert.Equal(240m, signals[1041].PreRevenue);

        // A1042: a post-markdown KOREKCIJA line still moved stock: 2 + (1 + 1) sold since markdown.
        Assert.Equal(4m, signals[1042].StockBeforeMarkdown);
        Assert.Equal(0.3333m, signals[1042].PreSellthrough);

        // A1050: no line, article or foreign cost -> margin equals revenue and no cost signal.
        Assert.Equal(240m, signals[1050].PreMargin);
        Assert.False(signals[1050].HasCostSignal);

        // A1051/A1052 are stockouts: stock proxy 0 and 2 against minimum stock 2.
        Assert.True(signals[1051].StockoutBeforeMarkdown);
        Assert.True(signals[1052].StockoutBeforeMarkdown);
        Assert.Equal(1m, signals[1051].PreSellthrough);

        // A1061 first markdown predates the 90d window; A1062 skips the earlier price increase.
        Assert.Equal(PureAnchor.AddDays(-120), signals[1061].FirstMarkdownDate);
        Assert.Equal(PureAnchor.AddDays(-50), signals[1062].FirstMarkdownDate);
        Assert.Equal(155m, signals[1062].PreRevenue);
        Assert.Equal(80m, signals[1062].PreMargin);
        Assert.Equal(25m, signals[1062].CurrentCost);
        Assert.False(signals[1062].HasDidSignal);

        var window90 = SupplierScorecardOracle.ArticleSignals(fixture, SupplierScorecardOracleOptions.Window(PureAnchor, 90))
            .Select(s => s.ArticleId)
            .ToHashSet();
        var window180 = SupplierScorecardOracle.ArticleSignals(fixture, SupplierScorecardOracleOptions.Window(PureAnchor, 180))
            .Select(s => s.ArticleId)
            .ToHashSet();
        Assert.DoesNotContain(1061, window90);
        Assert.Contains(1062, window90);
        Assert.Contains(1061, window180);
    }

    [Fact]
    public void GoldenSupplierInputsAndRecommendations_MatchHandComputedValues()
    {
        var fixture = GoldenFixture.Build(PureAnchor).Fixture;
        var scores = SupplierScorecardOracle.Score(fixture, SupplierScorecardOracleOptions.Window(PureAnchor, 90))
            .ToDictionary(s => s.Input.SupplierId);

        Assert.Equal(new[] { 101, 102, 103, 104, 105, 106 }, scores.Keys.Order());

        // Alfa: (8 + 6) / ((8 + 4) + (6 + 2)); pre margin (480 + 330) / (800 + 540).
        Assert.Equal(0.7m, scores[101].Input.FullpriceSellthrough);
        Assert.Equal(810m / 1340m, scores[101].Input.PreMarkdownMarginPct);
        Assert.Equal("complete", scores[101].EvidenceQualityStatus);

        // Gama: 3 returned / 12 gross sold units in the evidence window.
        Assert.Equal(0.25m, scores[103].Input.ReturnRate);
        Assert.Equal("REVIEW_QUALITY", scores[103].RecommendationCode);

        // Delta: 9 of 10 articles have cost evidence.
        Assert.Equal(0.9m, scores[104].Input.CostSignalCoverage);
        Assert.Equal("partial", scores[104].EvidenceQualityStatus);
        Assert.Equal("REVIEW_QUALITY", scores[104].RecommendationCode);

        // Epsilon: two of three articles stocked out, retail returns 0, evidence complete.
        Assert.True(scores[105].Input.StockoutBeforeMarkdownFlag);
        Assert.Equal(0m, scores[105].Input.ReturnRate);
        Assert.Equal("OOS_FALSE_NEGATIVE", scores[105].RecommendationCode);

        // Zeta: sale-time supplier is unknown, so the return-rate baseline is
        // missing and the return rank is neutral, not best.
        Assert.Null(scores[106].Input.ReturnRate);
        Assert.Equal("missing_sales_baseline", scores[106].ReturnRateMissingEvidenceReason);
        Assert.Equal(0.5m, scores[106].ReturnRateRank);
        Assert.Equal(1, scores[106].Input.ArticleCount);
        Assert.Equal("REVIEW_QUALITY", scores[106].RecommendationCode);

        // Beta: sale-time attribution keeps A1053's sale/return under Beta after
        // the supplier change: 2 returned / (9 + 5 + 2) gross sold units.
        Assert.Equal(0.125m, scores[102].Input.ReturnRate);
        Assert.Equal("REVIEW_QUALITY", scores[102].RecommendationCode);
    }

    // ------------------------------------------------------------------
    // Property / counterexample tests on the scoring layer.
    // ------------------------------------------------------------------

    [Fact]
    public void FormulaVersionAndExplainabilityMetadata_AreStableAndCoverCurrentPolicy()
    {
        Assert.Equal(SupplierScorecardModelContract.FormulaVersion, SupplierScorecardOracle.FormulaVersion);
        Assert.Equal("supplier-scorecard-explainability-v1", SupplierScorecardModelContract.Current.ExplainabilityVersion);
        Assert.Contains(SupplierScorecardModelContract.Current.Components, item => item.Key == "demand.fullprice_sellthrough");
        Assert.Contains(SupplierScorecardModelContract.Current.Components, item => item.Key == "penalty.dead_stock");
        Assert.Contains(SupplierScorecardModelContract.Current.Components, item => item.Key == "penalty.unsold_stock_value");
        Assert.Contains(SupplierScorecardModelContract.Current.Components, item => item.Key == "score.final");
        Assert.Contains(SupplierScorecardModelContract.Current.Gates, item => item.Key == "margin_p80_clamp");
        Assert.Contains(
            SupplierScorecardModelContract.Current.OwnerDecisionPending,
            item => item.StartsWith("owner_decision_pending:", StringComparison.Ordinal));
    }

    [Fact]
    public void OracleContributions_ReproduceScore_ForAllTimeAndWindowedFixtures()
    {
        var fixture = GoldenFixture.Build(PureAnchor).Fixture;

        foreach (var options in new[]
        {
            SupplierScorecardOracleOptions.AllTime(PureAnchor),
            SupplierScorecardOracleOptions.Window(PureAnchor, 90),
            SupplierScorecardOracleOptions.Window(PureAnchor, 180)
        })
        {
            foreach (var score in SupplierScorecardOracle.Score(fixture, options))
            {
                var contributionSum = score.DemandScore
                    + score.MarginScore
                    - score.MarkdownPenalty
                    - score.InventoryPenalty
                    + score.SupplierQualityComponent;

                Assert.Equal(score.ScoreRaw, contributionSum);
                Assert.Equal(
                    Math.Min(100m, Math.Max(0m, score.ScoreRaw)),
                    score.SupplierQualityIndex);
            }
        }
    }

    [Fact]
    public void Score_IsMonotonicInFullpriceSellthrough_WithEverythingElseFixed()
    {
        var baseline = SyntheticInputs();
        decimal? previous = null;

        for (var sellthrough = 0m; sellthrough <= 1m; sellthrough += 0.05m)
        {
            var inputs = baseline.ToList();
            inputs[0] = inputs[0] with { FullpriceSellthrough = sellthrough };
            var score = SupplierScorecardOracle.Score(inputs)[0].SupplierQualityIndex;

            if (previous is not null)
            {
                Assert.True(score >= previous, $"Score dropped from {previous} to {score} at sell-through {sellthrough}.");
            }

            previous = score;
        }

        Assert.True(previous > SupplierScorecardOracle.Score(
            baseline.Select((i, index) => index == 0 ? i with { FullpriceSellthrough = 0m } : i).ToList())[0].SupplierQualityIndex);
    }

    [Fact]
    public void Score_ReportsClampingAtBothEnds()
    {
        var inputs = SyntheticInputs();
        inputs[0] = inputs[0] with
        {
            FullpriceSellthrough = 1m,
            FullpriceRevenueShare = 1m,
            PreMarkdownMarginPct = 0.9m,
            MarkdownRevenueShare = 0m,
            DeadStockRate = 0m,
            UnsoldStockValue = 0m,
            RepeatWinnerRate = 1m,
            CategoryFocusScore = 100m,
            ReturnRate = 0m
        };
        inputs[1] = inputs[1] with
        {
            FullpriceSellthrough = 0m,
            FullpriceRevenueShare = 0m,
            PreMarkdownMarginPct = -0.5m,
            MarkdownRevenueShare = 1m,
            DeadStockRate = 1m,
            UnsoldStockValue = 1_000_000m,
            RepeatWinnerRate = 0m,
            CategoryFocusScore = 0m,
            ReturnRate = 0.9m
        };

        var scores = SupplierScorecardOracle.Score(inputs);

        Assert.True(scores[0].ScoreRaw > 100);
        Assert.True(scores[0].ScoreClamped);
        Assert.Equal(100m, scores[0].SupplierQualityIndex);
        Assert.True(scores[1].ScoreRaw < 0);
        Assert.True(scores[1].ScoreClamped);
        Assert.Equal(0m, scores[1].SupplierQualityIndex);
    }

    [Fact]
    public void ConfidenceWeights_SumTo130Percent_AndAreClampedToOne_DocumentsN16()
    {
        var inputs = SyntheticInputs();
        inputs[0] = inputs[0] with
        {
            HadSalesShare = 1m,
            HighSignalShare = 1m,
            ArticleCount = 1_000,
            Units = 1_000_000m,
            PostSignalCoverage = 1m,
            DidSignalCoverage = 1m,
            CostSignalCoverage = 1m,
            ReturnRate = 0m
        };

        var score = SupplierScorecardOracle.Score(inputs)[0];

        Assert.Equal(1.3m, score.ConfidenceRaw);
        Assert.Equal(1m, score.ConfidenceScore);
    }

    [Fact]
    public void MissingReturnRate_IsNeutral_NotBest_AndCostsConfidence_RepairsN11()
    {
        var inputs = SyntheticInputs();
        inputs[0] = inputs[0] with { ReturnRate = null, GrossSoldUnitsInPeriod = 0m };
        inputs[1] = inputs[1] with { ReturnRate = 0m };

        var scores = SupplierScorecardOracle.Score(inputs);

        Assert.Equal(0.5m, scores[0].ReturnRateRank);
        Assert.Equal(0m, scores[1].ReturnRateRank);
        // Known rates rank only against each other: 0.00 < 0.01 < 0.08 < 0.10.
        Assert.Equal(
            new[] { 0m, 0.666666666666667m, 0.333333333333333m, 1m },
            scores.Skip(1).Select(s => s.ReturnRateRank).ToArray());
        Assert.Equal("partial", scores[0].EvidenceQualityStatus);
        Assert.Equal(
            SupplierScorecardOracle.Score(inputs.Select((i, index) => index == 0 ? i with { ReturnRate = 0m, GrossSoldUnitsInPeriod = 10m } : i).ToList())[0].ConfidenceRaw - 0.15m,
            scores[0].ConfidenceRaw);
    }

    [Fact]
    public void InventoryPenalty_RanksAbsoluteStockValue_SoSupplierSizeIsExposed_DocumentsN15Policy()
    {
        var inputs = SyntheticInputs();
        inputs[0] = inputs[0] with { DeadStockRate = 0.2m, UnsoldStockValue = 10_000m };
        inputs[1] = inputs[1] with { DeadStockRate = 0.2m, UnsoldStockValue = 100_000m };

        var scores = SupplierScorecardOracle.Score(inputs);

        Assert.True(scores[1].InventoryPenalty > scores[0].InventoryPenalty);
    }

    [Fact]
    public void SingleSupplierCohort_UsesRankGuardOfOne()
    {
        var score = SupplierScorecardOracle.Score(SyntheticInputs().Take(1).ToList())[0];

        Assert.Equal(1m, score.FullpriceSellthroughRank);
        Assert.Equal(1m, score.MarkdownRevenueShareRank);
        Assert.Equal(1m, score.ReturnRateRank);
        Assert.Equal(100m, score.DemandScore);
        Assert.Equal(100m, score.MarginScore);
    }

    [Fact]
    public void AddingUnrelatedSupplier_ChangesExistingRelativeScore_DocumentsN14()
    {
        var builder = GoldenFixture.Build(PureAnchor);
        var options = SupplierScorecardOracleOptions.Window(PureAnchor, 90);
        var before = SupplierScorecardOracle.Score(builder.Fixture, options).Single(s => s.Input.SupplierId == 101);

        GoldenFixture.AddUnrelatedSupplier(builder);
        var after = SupplierScorecardOracle.Score(builder.Fixture, options).Single(s => s.Input.SupplierId == 101);

        // Alfa's own inputs are untouched, yet its relative score moves; the
        // 0-100 clamp can hide that move in the published index.
        Assert.Equal(before.Input, after.Input);
        Assert.True(before.ScoreClamped);
        Assert.NotEqual(before.ScoreRaw, after.ScoreRaw);
        Assert.NotEqual(before.DemandScore, after.DemandScore);
    }

    // ------------------------------------------------------------------
    // Real PostgreSQL: oracle vs 018/029 SQL on the golden fixture.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ScorecardViews_MatchIndependentOracle_ForGoldenFixture()
    {
        await using var db = await TryCreateScorecardDatabaseAsync("tp_supplier_oracle");
        if (db is null)
        {
            return;
        }

        var builder = GoldenFixture.Build(db.Anchor);
        await SeedAsync(db.Connection, builder.Fixture);
        await ApplyScorecardSqlAsync(db.Connection);

        var mismatches = new List<string>();

        var sqlArticles = await ReadArticleSignalsAsync(db.Connection, "vw_supplier_fullprice_signals");
        CompareArticles(
            "all_time",
            SupplierScorecardOracle.ArticleSignals(builder.Fixture, SupplierScorecardOracleOptions.AllTime(db.Anchor)),
            sqlArticles,
            mismatches);

        foreach (var (window, view) in new[] { (90, "vw_supplier_fullprice_signals_90d"), (180, "vw_supplier_fullprice_signals_180d") })
        {
            var expectedIds = SupplierScorecardOracle
                .ArticleSignals(builder.Fixture, SupplierScorecardOracleOptions.Window(db.Anchor, window))
                .Select(s => s.ArticleId)
                .Order()
                .ToArray();
            var actualIds = (await ReadArticleSignalsAsync(db.Connection, view)).Keys.Order().ToArray();
            if (!expectedIds.SequenceEqual(actualIds))
            {
                mismatches.Add($"{view}: expected articles [{string.Join(",", expectedIds)}], actual [{string.Join(",", actualIds)}]");
            }
        }

        foreach (var (options, mv) in ScoreCaches(db.Anchor))
        {
            CompareScores(
                mv,
                SupplierScorecardOracle.Score(builder.Fixture, options),
                await ReadScoresAsync(db.Connection, mv),
                mismatches);
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    [Fact]
    public async Task ScorecardInputRepairs_AreAppliedBySql_Rq521()
    {
        await using var db = await TryCreateScorecardDatabaseAsync("tp_supplier_oracle_repairs");
        if (db is null)
        {
            return;
        }

        var fixture = GoldenFixture.Build(db.Anchor).Fixture;
        await SeedAsync(db.Connection, fixture);
        await ApplyScorecardSqlAsync(db.Connection);

        var currentAllTime = SupplierScorecardOracleOptions.AllTime(db.Anchor);

        foreach (var mv in new[] { "mv_supplier_decision_score_cache", "mv_supplier_decision_score_cache_90d", "mv_supplier_decision_score_cache_180d" })
        {
            var scores = await ReadScoresAsync(db.Connection, mv);

            // N11: return rate uses gross sold units (3 / 12), not net units (3 / 9).
            Assert.Equal(0.25m, scores[103].ReturnRate);

            // N12: sale-time attribution keeps A1053's sale/return (sold under Beta
            // before the supplier change) in Beta: 2 / 16 crosses the 12% gate.
            Assert.Equal(0.125m, scores[102].ReturnRate);
            Assert.Equal("REVIEW_QUALITY", scores[102].RecommendationCode);

            // Missing return evidence stays visible and is not treated as zero.
            Assert.Null(scores[106].ReturnRate);
            Assert.Equal("missing_sales_baseline", scores[106].ReturnRateMissingEvidenceReason);
        }

        var sql90 = await ReadScoresAsync(db.Connection, "mv_supplier_decision_score_cache_90d");
        var sqlArticles = await ReadArticleSignalsAsync(db.Connection, "vw_supplier_fullprice_signals");

        // N13 is policy (RQ531), not an input bug: one missing cost out of ten still forces REVIEW_QUALITY.
        Assert.Equal(0.9m, sql90[104].CostSignalCoverage);
        Assert.Equal("partial", sql90[104].EvidenceQualityStatus);
        Assert.Equal("REVIEW_QUALITY", sql90[104].RecommendationCode);

        // N19: DUG/KOREKCIJA are not retail turnover in the full-price profile,
        // but a post-markdown KOREKCIJA line still counts as a physical stock movement.
        Assert.Equal(2m, sqlArticles[1041].PreQty);
        Assert.Equal(240m, sqlArticles[1041].PreRevenue);
        Assert.Equal(4m, sqlArticles[1042].StockBeforeMarkdown);

        // N20 (unchanged, unproven against real data): a customer return recorded both
        // as a negative sale line and as a "Povrat kupca" move is subtracted twice.
        var singleReturn = SupplierScorecardOracle
            .ArticleSignals(fixture, currentAllTime with { StockProxySubtractsCustomerReturnMoves = false })
            .Single(s => s.ArticleId == 1022);
        Assert.Equal(4m, sqlArticles[1022].StockBeforeMarkdown);
        Assert.Equal(5m, singleReturn.StockBeforeMarkdown);

        // N17: the first-ever markdown anchors the population, so an article whose
        // first markdown predates the window is excluded despite a later one.
        Assert.DoesNotContain(1061, (await ReadArticleSignalsAsync(db.Connection, "vw_supplier_fullprice_signals_90d")).Keys);
        Assert.Contains(1061, (await ReadArticleSignalsAsync(db.Connection, "vw_supplier_fullprice_signals_180d")).Keys);
    }

    [Fact]
    public async Task AddingUnrelatedSupplier_ChangesExistingSqlScore_DocumentsN14()
    {
        await using var db = await TryCreateScorecardDatabaseAsync("tp_supplier_oracle_relative");
        if (db is null)
        {
            return;
        }

        var builder = GoldenFixture.Build(db.Anchor);
        await SeedAsync(db.Connection, builder.Fixture);
        await ApplyScorecardSqlAsync(db.Connection);
        var options = SupplierScorecardOracleOptions.Window(db.Anchor, 90);
        var before = (await ReadScoresAsync(db.Connection, "mv_supplier_decision_score_cache_90d"))[101];

        var mark = builder.Mark();
        GoldenFixture.AddUnrelatedSupplier(builder);
        await SeedAsync(db.Connection, builder.Since(mark));
        await ExecuteAsync(db.Connection, "REFRESH MATERIALIZED VIEW mv_supplier_decision_score_cache_90d;");
        var afterRows = await ReadScoresAsync(db.Connection, "mv_supplier_decision_score_cache_90d");

        var mismatches = new List<string>();
        CompareScores("mv_supplier_decision_score_cache_90d+unrelated", SupplierScorecardOracle.Score(builder.Fixture, options), afterRows, mismatches);
        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));

        var after = afterRows[101];
        Assert.Equal(before.Revenue, after.Revenue);
        Assert.Equal(before.FullpriceSellthrough, after.FullpriceSellthrough);
        Assert.Equal(before.PreMarkdownMarginPct, after.PreMarkdownMarginPct);
        Assert.Equal(before.ReturnRate, after.ReturnRate);
        Assert.True(
            before.SupplierQualityIndex != after.SupplierQualityIndex
            || before.MarkdownDependencyScore != after.MarkdownDependencyScore
            || before.StockRiskScore != after.StockRiskScore
            || before.ConfidenceScore != after.ConfidenceScore,
            "Adding an unrelated supplier should move at least one relative Alfa output.");
    }

    [Fact]
    public async Task StaleWindowedCaches_AreRecreated_AndRepairedCoreViewsPassStartupReadiness()
    {
        await using var db = await TryCreateScorecardDatabaseAsync("tp_supplier_oracle_upgrade");
        if (db is null)
        {
            return;
        }

        var builder = GoldenFixture.Build(db.Anchor);
        await SeedAsync(db.Connection, builder.Fixture);
        await ApplyScorecardSqlAsync(db.Connection);

        // The startup readiness predicate must accept the repaired core views as
        // deparsed by PostgreSQL; otherwise every startup re-runs the core batches
        // and skips the cache build as "still missing after repair".
        var initializer = ReadRepoFile("Infrastructure/Seed/DatabaseInitializer.cs");
        var method = initializer.IndexOf("AreSupplierDecisionHubCoreViewsReadyAsync(string connectionString)", StringComparison.Ordinal);
        var sqlStart = initializer.IndexOf("\"\"\"", method, StringComparison.Ordinal) + 3;
        var sqlEnd = initializer.IndexOf("\"\"\"", sqlStart, StringComparison.Ordinal);
        await using (var readiness = new NpgsqlCommand(initializer[sqlStart..sqlEnd], db.Connection))
        {
            Assert.True((bool)(await readiness.ExecuteScalarAsync())!);
        }

        // A windowed cache built from a pre-RQ521 definition is replaced on the
        // next 029 run instead of being kept by CREATE ... IF NOT EXISTS.
        await ExecuteAsync(db.Connection, """
            DROP MATERIALIZED VIEW mv_supplier_decision_score_cache_90d;
            CREATE MATERIALIZED VIEW mv_supplier_decision_score_cache_90d AS SELECT 1 AS legacy_sold_units_in_period;
            """);
        foreach (var batch in ReadRepoFile("Database/Migrations/029_AddSupplierDecisionWindowedViews.sql")
                     .Split("-- SQL_BATCH_BREAK", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await ExecuteAsync(db.Connection, batch);
        }

        var mismatches = new List<string>();
        CompareScores(
            "mv_supplier_decision_score_cache_90d+recreated",
            SupplierScorecardOracle.Score(builder.Fixture, SupplierScorecardOracleOptions.Window(db.Anchor, 90)),
            await ReadScoresAsync(db.Connection, "mv_supplier_decision_score_cache_90d"),
            mismatches);
        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    // ------------------------------------------------------------------
    // Golden fixture
    // ------------------------------------------------------------------

    private sealed class FixtureBuilder(DateOnly anchor)
    {
        private int _receiptId;
        private long _eventId;

        public DateOnly Anchor { get; } = anchor;

        public OracleFixture Fixture { get; } = new();

        public void Supplier(int id, string name) => Fixture.Suppliers.Add(new OracleSupplier(id, name));

        public void Article(int id, int supplierId, string category, int stock, int minStock, decimal costDin, decimal costForeign = 0m) =>
            Fixture.Articles.Add(new OracleArticle(id, supplierId, category, stock, minStock, costDin, costForeign));

        public void Sale(int articleId, int dayOffset, int qty, decimal price, int? supplierAtSale, decimal? lineCost, string? receiptNumber = null)
        {
            _receiptId++;
            Fixture.Sales.Add(new OracleSaleLine(
                _receiptId,
                receiptNumber ?? $"R-{_receiptId}",
                Anchor.AddDays(dayOffset),
                articleId,
                qty,
                price,
                lineCost,
                supplierAtSale));
        }

        public void Move(int articleId, int dayOffset, string type, int qty) =>
            Fixture.Moves.Add(new OracleInventoryMove(articleId, Anchor.AddDays(dayOffset), type, qty));

        // Post-window qty/revenue on the event row mirror the fixture sales in
        // [event, event + 30); call after the article's sales are added.
        public void PriceEvent(int articleId, int vendorId, int dayOffset, decimal oldPrice, decimal newPrice, bool did = true, decimal coveragePre = 0.8m, decimal coveragePost = 0.8m)
        {
            _eventId++;
            var day = Anchor.AddDays(dayOffset);
            var post = Fixture.Sales.Where(s => s.ArticleId == articleId && s.Day >= day && s.Day < day.AddDays(30)).ToList();
            Fixture.Events.Add(new OraclePriceEvent(
                _eventId,
                articleId,
                vendorId,
                day,
                oldPrice,
                newPrice,
                coveragePre,
                coveragePost,
                false,
                post.Sum(s => s.Qty),
                post.Sum(s => s.Qty * s.Price)));
            if (did)
            {
                Fixture.Did.Add(new OracleDidRow(_eventId, 10m, 1m));
            }
        }

        public int[] Mark() =>
        [
            Fixture.Suppliers.Count, Fixture.Articles.Count, Fixture.Sales.Count,
            Fixture.Moves.Count, Fixture.Events.Count, Fixture.Did.Count
        ];

        public OracleFixture Since(int[] mark)
        {
            var delta = new OracleFixture();
            delta.Suppliers.AddRange(Fixture.Suppliers.Skip(mark[0]));
            delta.Articles.AddRange(Fixture.Articles.Skip(mark[1]));
            delta.Sales.AddRange(Fixture.Sales.Skip(mark[2]));
            delta.Moves.AddRange(Fixture.Moves.Skip(mark[3]));
            delta.Events.AddRange(Fixture.Events.Skip(mark[4]));
            delta.Did.AddRange(Fixture.Did.Skip(mark[5]));
            return delta;
        }
    }

    private static class GoldenFixture
    {
        public static FixtureBuilder Build(DateOnly anchor)
        {
            var b = new FixtureBuilder(anchor);

            // 101 Alfa: clean full-price winner, complete evidence.
            b.Supplier(101, "Alfa Winner");
            b.Article(1011, 101, "Patike", stock: 2, minStock: 1, costDin: 40m);
            foreach (var day in new[] { -60, -55, -50, -45 })
            {
                b.Sale(1011, day, 2, 100m, 101, 40m);
            }
            b.Sale(1011, -30, 2, 70m, 101, 40m);
            b.PriceEvent(1011, 101, -40, 100m, 70m);

            b.Article(1012, 101, "Patike", stock: 1, minStock: 1, costDin: 35m);
            b.Sale(1012, -58, 3, 90m, 101, 35m);
            b.Sale(1012, -48, 3, 90m, 101, 35m);
            b.Sale(1012, -25, 1, 60m, 101, 35m);
            b.PriceEvent(1012, 101, -38, 90m, 60m);

            // 102 Beta: markdown-dependent seasonal supplier; A1022 has a return
            // recorded both as a negative sale and as a customer-return move.
            b.Supplier(102, "Beta Markdown");
            b.Article(1021, 102, "Sandale", stock: 6, minStock: 1, costDin: 50m);
            b.Sale(1021, -55, 1, 100m, 102, 50m);
            b.Sale(1021, -28, 4, 60m, 102, 50m);
            b.Sale(1021, -20, 4, 60m, 102, 50m);
            b.PriceEvent(1021, 102, -35, 100m, 60m, coveragePre: 0.3m);

            b.Article(1022, 102, "Sandale", stock: 3, minStock: 1, costDin: 45m);
            b.Sale(1022, -50, 2, 100m, 102, 45m);
            b.Sale(1022, -25, 3, 70m, 102, 45m);
            b.Sale(1022, -10, -1, 70m, 102, 45m);
            b.Move(1022, -10, "Povrat kupca", 1);
            b.PriceEvent(1022, 102, -32, 100m, 70m);

            // 103 Gama: high retail returns.
            b.Supplier(103, "Gama Returns");
            b.Article(1031, 103, "Patike", stock: 4, minStock: 1, costDin: 30m);
            b.Sale(1031, -60, 5, 80m, 103, 30m);
            b.Sale(1031, -50, 5, 80m, 103, 30m);
            b.Sale(1031, -30, 2, 60m, 103, 30m);
            b.Sale(1031, -20, -3, 60m, 103, 30m);
            b.PriceEvent(1031, 103, -40, 80m, 60m);

            // 104 Delta: ten boots, one without any cost (90% cost coverage), one
            // DUG receipt in a pre-markdown window and one post-markdown KOREKCIJA.
            b.Supplier(104, "Delta Coverage");
            for (var id = 1041; id <= 1050; id++)
            {
                var hasCost = id != 1050;
                b.Article(id, 104, "Cizme", stock: 2, minStock: 1, costDin: hasCost ? 60m : 0m);
                b.Sale(id, -50, 2, 120m, 104, hasCost ? 60m : null);
                if (id == 1041)
                {
                    b.Sale(id, -45, 1, 120m, 104, 60m, receiptNumber: "DUG");
                }
                b.Sale(id, -25, 1, 90m, 104, hasCost ? 60m : null);
                if (id == 1042)
                {
                    b.Sale(id, -20, 1, 90m, 104, 60m, receiptNumber: " korekcija ");
                }
                b.PriceEvent(id, 104, -35, 120m, 90m);
            }

            // 105 Epsilon: stockouts before markdown; A1053 moved from Beta to
            // Epsilon, so its sale and return carry sale-time supplier 102.
            b.Supplier(105, "Epsilon Stockout");
            b.Article(1051, 105, "Patike", stock: 0, minStock: 2, costDin: 40m);
            b.Sale(1051, -50, 3, 90m, 105, 40m);
            b.Sale(1051, -45, 2, 90m, 105, 40m);
            b.PriceEvent(1051, 105, -40, 90m, 70m);

            b.Article(1052, 105, "Patike", stock: 1, minStock: 2, costDin: 40m);
            b.Sale(1052, -50, 2, 90m, 105, 40m);
            b.Sale(1052, -42, 2, 90m, 105, 40m);
            b.Sale(1052, -30, 1, 70m, 105, 40m);
            b.PriceEvent(1052, 105, -40, 90m, 70m);

            b.Article(1053, 105, "Patike", stock: 6, minStock: 1, costDin: 40m);
            b.Sale(1053, -52, 2, 80m, 102, 40m);
            b.Sale(1053, -5, -1, 80m, 102, 40m);
            b.PriceEvent(1053, 105, -38, 80m, 64m);

            // 106 Zeta: legacy sales without sale-time supplier; A1061's first
            // markdown predates 90d; A1062 has an earlier price increase, a
            // foreign-cost fallback and no DiD row.
            b.Supplier(106, "Zeta Legacy");
            b.Article(1061, 106, "Patike", stock: 3, minStock: 1, costDin: 30m);
            b.Sale(1061, -140, 2, 100m, null, 30m);
            b.Sale(1061, -130, 2, 100m, null, 30m);
            b.Sale(1061, -40, 1, 60m, null, 30m);
            b.PriceEvent(1061, 106, -120, 100m, 80m);
            b.PriceEvent(1061, 106, -30, 80m, 60m);

            b.Article(1062, 106, "Patike", stock: 5, minStock: 1, costDin: 0m, costForeign: 25m);
            b.Sale(1062, -70, 2, 50m, null, null);
            b.Sale(1062, -55, 1, 55m, null, null);
            b.Sale(1062, -40, 2, 40m, null, null);
            b.PriceEvent(1062, 106, -60, 50m, 55m);
            b.PriceEvent(1062, 106, -50, 55m, 40m, did: false);

            return b;
        }

        public static void AddUnrelatedSupplier(FixtureBuilder b)
        {
            b.Supplier(107, "Eta Unrelated");
            b.Article(1071, 107, "Patike", stock: 1, minStock: 1, costDin: 20m);
            b.Sale(1071, -50, 10, 50m, 107, 20m);
            b.Sale(1071, -20, 1, 40m, 107, 20m);
            b.PriceEvent(1071, 107, -40, 50m, 40m);
        }
    }

    private static List<OracleDecisionInput> SyntheticInputs()
    {
        OracleDecisionInput Input(int id, decimal sellthrough, decimal margin, decimal markdown, decimal dead, decimal value, decimal? returns, decimal focus, int articles, decimal units) =>
            new(
                id,
                $"S{id}",
                PureAnchor.AddDays(-60),
                PureAnchor,
                1000m,
                units,
                0.5m,
                sellthrough,
                margin,
                markdown,
                dead,
                value,
                1m,
                1m,
                1m,
                10m,
                0m,
                returns,
                focus,
                0.4m,
                articles,
                0.5m,
                0.3m,
                0.9m,
                false,
                0.1m);

        return
        [
            Input(1, 0.40m, 0.30m, 0.40m, 0.10m, 5_000m, 0.02m, 60m, 4, 40m),
            Input(2, 0.55m, 0.35m, 0.30m, 0.20m, 8_000m, 0.05m, 70m, 6, 60m),
            Input(3, 0.65m, 0.25m, 0.50m, 0.15m, 3_000m, 0.08m, 50m, 5, 30m),
            Input(4, 0.30m, 0.40m, 0.20m, 0.30m, 12_000m, 0.01m, 80m, 8, 90m),
            Input(5, 0.50m, 0.20m, 0.60m, 0.05m, 2_000m, 0.10m, 40m, 3, 20m)
        ];
    }

    private static decimal Round4(decimal value) => SupplierScorecardOracle.Round(value, 4);

    private static IEnumerable<(SupplierScorecardOracleOptions Options, string View)> ScoreCaches(DateOnly anchor) =>
    [
        (SupplierScorecardOracleOptions.AllTime(anchor), "mv_supplier_decision_score_cache"),
        (SupplierScorecardOracleOptions.Window(anchor, 90), "mv_supplier_decision_score_cache_90d"),
        (SupplierScorecardOracleOptions.Window(anchor, 180), "mv_supplier_decision_score_cache_180d")
    ];

    // ------------------------------------------------------------------
    // Comparison
    // ------------------------------------------------------------------

    private sealed record SqlArticleSignal(
        int SupplierId,
        DateOnly FirstMarkdownDate,
        decimal PreQty,
        decimal PreRevenue,
        decimal PreMargin,
        decimal PreSellthrough,
        decimal StockBeforeMarkdown,
        bool StockoutBeforeMarkdown,
        bool HadSalesBeforeMarkdown,
        string SignalQuality);

    private sealed record SqlScore(
        DateOnly PeriodFrom,
        DateOnly PeriodTo,
        decimal Revenue,
        decimal Units,
        decimal FullpriceRevenueShare,
        decimal FullpriceSellthrough,
        decimal PreMarkdownMarginPct,
        decimal MarkdownDependencyScore,
        decimal StockRiskScore,
        decimal? ReturnRate,
        decimal CategoryFocusScore,
        decimal RepeatWinnerRate,
        decimal PostSignalCoverage,
        decimal DidSignalCoverage,
        decimal CostSignalCoverage,
        string EvidenceQualityStatus,
        string? ReturnRateMissingEvidenceReason,
        decimal SupplierQualityIndex,
        string RecommendationCode,
        decimal ConfidenceScore);

    private static void CompareArticles(
        string label,
        IReadOnlyList<OracleArticleSignal> expected,
        IReadOnlyDictionary<int, SqlArticleSignal> actual,
        List<string> mismatches)
    {
        var expectedIds = expected.Select(e => e.ArticleId).Order().ToArray();
        var actualIds = actual.Keys.Order().ToArray();
        if (!expectedIds.SequenceEqual(actualIds))
        {
            mismatches.Add($"{label}: expected articles [{string.Join(",", expectedIds)}], actual [{string.Join(",", actualIds)}]");
            return;
        }

        foreach (var e in expected)
        {
            var a = actual[e.ArticleId];
            var context = $"{label} article {e.ArticleId}";
            Check(mismatches, context, "supplier_id", e.SupplierId, a.SupplierId);
            Check(mismatches, context, "first_markdown_date", e.FirstMarkdownDate, a.FirstMarkdownDate);
            Check(mismatches, context, "pre_qty_30d", e.PreQty, a.PreQty);
            Check(mismatches, context, "pre_revenue_30d", e.PreRevenue, a.PreRevenue);
            Check(mismatches, context, "pre_margin_30d", e.PreMargin, a.PreMargin);
            Check(mismatches, context, "pre_sellthrough_30d", e.PreSellthrough, a.PreSellthrough);
            Check(mismatches, context, "stock_before_markdown", e.StockBeforeMarkdown, a.StockBeforeMarkdown);
            Check(mismatches, context, "stockout_before_markdown_flag", e.StockoutBeforeMarkdown, a.StockoutBeforeMarkdown);
            Check(mismatches, context, "had_sales_before_markdown_flag", e.HadSalesBeforeMarkdown, a.HadSalesBeforeMarkdown);
            Check(mismatches, context, "signal_quality_flag", e.SignalQuality, a.SignalQuality);
        }
    }

    private static void CompareScores(
        string label,
        IReadOnlyList<OracleSupplierScore> expected,
        IReadOnlyDictionary<int, SqlScore> actual,
        List<string> mismatches)
    {
        var expectedIds = expected.Select(e => e.Input.SupplierId).Order().ToArray();
        var actualIds = actual.Keys.Order().ToArray();
        if (!expectedIds.SequenceEqual(actualIds))
        {
            mismatches.Add($"{label}: expected suppliers [{string.Join(",", expectedIds)}], actual [{string.Join(",", actualIds)}]");
            return;
        }

        foreach (var e in expected)
        {
            var a = actual[e.Input.SupplierId];
            var i = e.Input;
            var context = $"{label} supplier {i.SupplierId}";
            Check(mismatches, context, "period_from", i.PeriodFrom, a.PeriodFrom);
            Check(mismatches, context, "period_to", i.PeriodTo, a.PeriodTo);
            Check(mismatches, context, "revenue", i.Revenue, a.Revenue);
            Check(mismatches, context, "units", i.Units, a.Units);
            Check(mismatches, context, "fullprice_revenue_share", Round4(i.FullpriceRevenueShare), a.FullpriceRevenueShare);
            Check(mismatches, context, "fullprice_sellthrough", Round4(i.FullpriceSellthrough), a.FullpriceSellthrough);
            Check(mismatches, context, "pre_markdown_margin_pct", Round4(i.PreMarkdownMarginPct), a.PreMarkdownMarginPct);
            Check(mismatches, context, "markdown_dependency_score", e.MarkdownPenalty, a.MarkdownDependencyScore);
            Check(mismatches, context, "stock_risk_score", e.InventoryPenalty, a.StockRiskScore);
            Check(mismatches, context, "return_rate", i.ReturnRate is null ? (decimal?)null : Round4(i.ReturnRate.Value), a.ReturnRate);
            Check(mismatches, context, "category_focus_score", SupplierScorecardOracle.Round(i.CategoryFocusScore, 2), a.CategoryFocusScore);
            Check(mismatches, context, "repeat_winner_rate", Round4(i.RepeatWinnerRate), a.RepeatWinnerRate);
            Check(mismatches, context, "post_signal_coverage", i.PostSignalCoverage, a.PostSignalCoverage);
            Check(mismatches, context, "did_signal_coverage", i.DidSignalCoverage, a.DidSignalCoverage);
            Check(mismatches, context, "cost_signal_coverage", i.CostSignalCoverage, a.CostSignalCoverage);
            Check(mismatches, context, "evidence_quality_status", e.EvidenceQualityStatus, a.EvidenceQualityStatus);
            Check(mismatches, context, "return_rate_missing_evidence_reason", e.ReturnRateMissingEvidenceReason, a.ReturnRateMissingEvidenceReason);
            Check(mismatches, context, "supplier_quality_index", e.SupplierQualityIndex, a.SupplierQualityIndex);
            Check(mismatches, context, "recommendation_code", e.RecommendationCode, a.RecommendationCode);
            Check(mismatches, context, "confidence_score", e.ConfidenceScore, a.ConfidenceScore);
        }
    }

    private static void Check<T>(List<string> mismatches, string context, string field, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            mismatches.Add($"{context}: {field} oracle={expected} sql={actual}");
        }
    }

    // ------------------------------------------------------------------
    // PostgreSQL harness
    // ------------------------------------------------------------------

    private sealed class ScorecardDatabase(NpgsqlConnection connection, DateOnly anchor) : IAsyncDisposable
    {
        public NpgsqlConnection Connection { get; } = connection;

        public DateOnly Anchor { get; } = anchor;

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }

    private async Task<ScorecardDatabase?> TryCreateScorecardDatabaseAsync(string prefix)
    {
        if (!_fixture.IsAvailable)
        {
            return null;
        }

        var connectionString = await _fixture.TryCreateDatabaseConnectionStringAsync($"{prefix}_{Guid.NewGuid():N}");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE "Dobavljaci" ("Id" integer PRIMARY KEY, "Naziv" text);
            CREATE TABLE "Artikli" (
                "Id" integer PRIMARY KEY,
                "IDDobavljac" integer,
                "Kategorija" text,
                "Kolicina" integer,
                "MinimalnaKolicina" integer,
                "NabavnaCenaDin" numeric(18,2),
                "NabavnaCena" numeric(18,2)
            );
            CREATE TABLE prodaja_zaglavlje (id integer PRIMARY KEY, datum_prodaje timestamp NOT NULL, broj_racuna text);
            CREATE TABLE prodaja_stavke (
                id serial PRIMARY KEY,
                id_prodaja integer NOT NULL,
                id_artikal integer NOT NULL,
                kolicina integer NOT NULL,
                cena numeric(18,2) NOT NULL,
                nabavna_cena numeric(18,2),
                supplier_id_at_sale integer
            );
            CREATE TABLE "DnevnikPromena" (
                "Id" serial PRIMARY KEY,
                "ArtikalId" integer,
                "Datum" timestamp NOT NULL,
                "TipPromene" text NOT NULL,
                "Kolicina" integer
            );
            -- Fixture-controlled nivelacija seam (RQ527 owns the real view math).
            CREATE TABLE vw_vendor_sales_nivelacija (
                price_event_id bigint PRIMARY KEY,
                event_date date NOT NULL,
                vendor_id bigint,
                vendor_name text,
                article_id bigint NOT NULL,
                sku text,
                article_name text,
                category text,
                old_price numeric,
                new_price numeric,
                pre_qty numeric,
                post_qty numeric,
                pre_revenue numeric,
                post_revenue numeric,
                coverage_pre30 numeric,
                coverage_post30 numeric,
                is_low_signal boolean NOT NULL DEFAULT FALSE
            );
            CREATE TABLE vw_nivelacija_did (price_event_id bigint PRIMARY KEY, did_revenue numeric, did_qty numeric);
            """);

        var anchor = DateOnly.FromDateTime(await ScalarAsync<DateTime>(connection, "SELECT CURRENT_DATE::timestamp;"));
        return new ScorecardDatabase(connection, anchor);
    }

    private static async Task SeedAsync(NpgsqlConnection connection, OracleFixture fixture)
    {
        var sql = new StringBuilder();

        foreach (var s in fixture.Suppliers)
        {
            sql.AppendLine($"INSERT INTO \"Dobavljaci\" VALUES ({s.Id}, {Text(s.Name)});");
        }

        foreach (var a in fixture.Articles)
        {
            sql.AppendLine(
                $"INSERT INTO \"Artikli\" VALUES ({a.Id}, {a.SupplierId}, {Text(a.Category)}, {a.Stock}, {a.MinStock}, {Num(a.CostDin)}, {Num(a.CostForeign)});");
        }

        foreach (var line in fixture.Sales)
        {
            sql.AppendLine(
                $"INSERT INTO prodaja_zaglavlje VALUES ({line.ReceiptId}, {Timestamp(line.Day)}, {Text(line.ReceiptNumber)});");
            sql.AppendLine(
                "INSERT INTO prodaja_stavke (id_prodaja, id_artikal, kolicina, cena, nabavna_cena, supplier_id_at_sale) "
                + $"VALUES ({line.ReceiptId}, {line.ArticleId}, {line.Qty}, {Num(line.Price)}, {Num(line.LineCost)}, {Num(line.SupplierAtSale)});");
        }

        foreach (var move in fixture.Moves)
        {
            sql.AppendLine(
                $"INSERT INTO \"DnevnikPromena\" (\"ArtikalId\", \"Datum\", \"TipPromene\", \"Kolicina\") VALUES ({move.ArticleId}, {Timestamp(move.Day)}, {Text(move.Type)}, {move.Qty});");
        }

        foreach (var e in fixture.Events)
        {
            sql.AppendLine(
                "INSERT INTO vw_vendor_sales_nivelacija (price_event_id, event_date, vendor_id, article_id, old_price, new_price, pre_qty, post_qty, pre_revenue, post_revenue, coverage_pre30, coverage_post30, is_low_signal) "
                + $"VALUES ({e.EventId}, DATE '{e.Day:yyyy-MM-dd}', {e.VendorId}, {e.ArticleId}, {Num(e.OldPrice)}, {Num(e.NewPrice)}, 0, {Num(e.PostQty)}, 0, {Num(e.PostRevenue)}, {Num(e.CoveragePre)}, {Num(e.CoveragePost)}, {(e.IsLowSignal ? "TRUE" : "FALSE")});");
        }

        foreach (var d in fixture.Did)
        {
            sql.AppendLine($"INSERT INTO vw_nivelacija_did VALUES ({d.EventId}, {Num(d.DidRevenue)}, {Num(d.DidQty)});");
        }

        if (sql.Length > 0)
        {
            await ExecuteAsync(connection, sql.ToString());
        }
    }

    private static async Task ApplyScorecardSqlAsync(NpgsqlConnection connection)
    {
        foreach (var path in new[]
        {
            "Database/Migrations/018_AddSupplierDecisionHubViews.sql",
            "Database/Migrations/029_AddSupplierDecisionWindowedViews.sql"
        })
        {
            foreach (var batch in ReadRepoFile(path).Split("-- SQL_BATCH_BREAK", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await ExecuteAsync(connection, batch);
            }
        }
    }

    private static async Task<Dictionary<int, SqlArticleSignal>> ReadArticleSignalsAsync(NpgsqlConnection connection, string view)
    {
        var rows = new Dictionary<int, SqlArticleSignal>();
        await using var command = new NpgsqlCommand(
            $"""
            SELECT supplier_id, article_id, first_markdown_date, pre_qty_30d, pre_revenue_30d, pre_margin_30d,
                   pre_sellthrough_30d, stock_before_markdown, stockout_before_markdown_flag,
                   had_sales_before_markdown_flag, signal_quality_flag
            FROM {view};
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows[Convert.ToInt32(reader["article_id"], CultureInfo.InvariantCulture)] = new SqlArticleSignal(
                Convert.ToInt32(reader["supplier_id"], CultureInfo.InvariantCulture),
                DateOnly.FromDateTime(reader.GetFieldValue<DateTime>(reader.GetOrdinal("first_markdown_date"))),
                reader.GetFieldValue<decimal>(reader.GetOrdinal("pre_qty_30d")),
                reader.GetFieldValue<decimal>(reader.GetOrdinal("pre_revenue_30d")),
                reader.GetFieldValue<decimal>(reader.GetOrdinal("pre_margin_30d")),
                reader.GetFieldValue<decimal>(reader.GetOrdinal("pre_sellthrough_30d")),
                reader.GetFieldValue<decimal>(reader.GetOrdinal("stock_before_markdown")),
                reader.GetFieldValue<bool>(reader.GetOrdinal("stockout_before_markdown_flag")),
                reader.GetFieldValue<bool>(reader.GetOrdinal("had_sales_before_markdown_flag")),
                reader.GetFieldValue<string>(reader.GetOrdinal("signal_quality_flag")));
        }

        return rows;
    }

    private static async Task<Dictionary<int, SqlScore>> ReadScoresAsync(NpgsqlConnection connection, string view)
    {
        var rows = new Dictionary<int, SqlScore>();
        await using var command = new NpgsqlCommand($"SELECT * FROM {view};", connection);
        await using var reader = await command.ExecuteReaderAsync();

        decimal Dec(string column) => reader.GetFieldValue<decimal>(reader.GetOrdinal(column));
        decimal? NullableDec(string column) =>
            reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetFieldValue<decimal>(reader.GetOrdinal(column));
        string? NullableText(string column) =>
            reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetFieldValue<string>(reader.GetOrdinal(column));
        DateOnly Date(string column) => DateOnly.FromDateTime(reader.GetFieldValue<DateTime>(reader.GetOrdinal(column)));

        while (await reader.ReadAsync())
        {
            rows[Convert.ToInt32(reader["supplier_id"], CultureInfo.InvariantCulture)] = new SqlScore(
                Date("period_from"),
                Date("period_to"),
                Dec("revenue"),
                Dec("units"),
                Dec("fullprice_revenue_share"),
                Dec("fullprice_sellthrough"),
                Dec("pre_markdown_margin_pct"),
                Dec("markdown_dependency_score"),
                Dec("stock_risk_score"),
                NullableDec("return_rate"),
                Dec("category_focus_score"),
                Dec("repeat_winner_rate"),
                Dec("post_signal_coverage"),
                Dec("did_signal_coverage"),
                Dec("cost_signal_coverage"),
                NullableText("evidence_quality_status") ?? string.Empty,
                NullableText("return_rate_missing_evidence_reason"),
                Dec("supplier_quality_index"),
                NullableText("recommendation_code") ?? string.Empty,
                Dec("confidence_score"));
        }

        return rows;
    }

    private static string Text(string? value) =>
        value is null ? "NULL" : $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string Num(decimal? value) =>
        value is null ? "NULL" : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string Num(int? value) =>
        value is null ? "NULL" : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string Timestamp(DateOnly day) => $"TIMESTAMP '{day:yyyy-MM-dd} 12:00:00'";

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Expected scalar result for SQL: {sql}"));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Trendplus2.sln")))
            {
                return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
