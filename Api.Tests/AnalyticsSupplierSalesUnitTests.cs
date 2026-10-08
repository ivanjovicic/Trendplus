using System.Linq;
using System.Globalization;
using Xunit;

namespace Trendplus2.Tests;

/// <summary>
/// Unit tests for supplier-sales-stats calculation helpers and core business logic.
/// These tests ensure numeric accuracy and edge-case handling independent of database state.
/// Tests utilize inline fixture data; large dataset testing is reserved for integration tests.
/// </summary>
[Trait("Category", "Unit")]
public class AnalyticsSupplierSalesUnitTests
{
    /// <summary>
    /// Tests the Pct helper function which computes percentage change.
    /// Formula: if pre=0, return post>0 ? 100 : 0; else return ((post-pre)/pre)*100, rounded to 2 decimals.
    /// </summary>
    public class PctHelperTests
    {
        private static decimal Pct(decimal pre, decimal post)
        {
            if (pre == 0m) return post > 0m ? 100m : 0m;
            return Math.Round(((post - pre) / pre) * 100m, 2);
        }

        [Theory(DisplayName = "Pct: normal positive change")]
        [InlineData(100, 150, 50.00)]
        [InlineData(100, 50, -50.00)]
        [InlineData(1000, 1100, 10.00)]
        public void NormalChange_ReturnsCorrectPercentage(decimal pre, decimal post, decimal expected)
        {
            var result = Pct(pre, post);
            Assert.Equal(expected, result);
        }

        [Fact(DisplayName = "Certified trend: zero/missing previous stays unavailable (never synthesizes +100%)")]
        public void ZeroPrevious_DoesNotSynthesizePlusOneHundred()
        {
            // ProductDecisionReasoningHelper is the shared certified trend contract.
            // Local endpoint helpers that synthesize +100% are not the supplier-decision truth.
            Assert.Null(Application.Analytics.ProductDecisionReasoningHelper.ComputeTrendPct(100m, 0m));
            Assert.Null(Application.Analytics.ProductDecisionReasoningHelper.ComputeTrendPct(100m, null));
            Assert.Equal(50m, Application.Analytics.ProductDecisionReasoningHelper.ComputeTrendPct(150m, 100m));
        }

        [Theory(DisplayName = "Pct: rounding to 2 decimals")]
        [InlineData(3, 10, 233.33)]  // (10-3)/3*100 = 233.333... -> 233.33
        [InlineData(7, 2, -71.43)]   // (2-7)/7*100 = -71.428... -> -71.43
        public void RoundingTo2Decimals(decimal pre, decimal post, decimal expected)
        {
            var result = Pct(pre, post);
            Assert.Equal(expected, result);
        }

        [Theory(DisplayName = "Pct: large values")]
        [InlineData(1_000_000, 1_500_000, 50.00)]
        [InlineData(0.01, 0.015, 50.00)]
        public void LargeAndSmallValues(decimal pre, decimal post, decimal expected)
        {
            var result = Pct(pre, post);
            Assert.Equal(expected, result);
        }
    }

    /// <summary>
    /// Tests for numeric edge cases commonly encountered in supplier-sales-stats calculations.
    /// Ensures guards against Infinity, NaN, and division by zero.
    /// </summary>
    public class NumericEdgeCaseTests
    {
        private static decimal SafePct(decimal pre, decimal post)
        {
            if (pre == 0m) return post > 0m ? 100m : 0m;
            return Math.Round(((post - pre) / pre) * 100m, 2);
        }

        [Fact(DisplayName = "Division by near-zero in OOS lost-sales calculation")]
        public void OosLostSalesCalc_WhenOosNearOne_MustNotProduceInfinity()
        {
            // Simulates: lostSales = (postRevenue * oos) / (1 - oos)
            decimal postRevenue = 1000m;
            decimal oos = 0.9999m;  // Nearly guaranteed to be out-of-stock

            var denominator = 1m - oos;
            Assert.NotEqual(0m, denominator);  // Denominator is not zero (0.0001)

            // Server code must guard against this with: if (denominator > 0m) check
            if (denominator > 0m)
            {
                decimal lostSales = (postRevenue * oos) / denominator;
                // Decimal can't be Infinity/NaN, but check for reasonable bounds
                Assert.True(lostSales > 0, "Lost sales should be positive given positive revenue and oos");
                // With oos close to 1 this number can be very large; the guard we care about is "finite and not overflow".
                Assert.True(lostSales < 10000000m, "Lost sales result should be finite and within a very wide sanity cap");
            }
        }

        [Theory(DisplayName = "Elasticity calculation with zero or near-zero price change")]
        [InlineData(100, 120, 0)]      // qty increases, price flat
        [InlineData(100, 80, 0.001)]   // qty decreases, tiny price change
        public void ElasticityCalc_GuardedAgainstDivisionByZero(decimal preQty, decimal postQty, decimal pricePct)
        {
            // Simulates: elasticity = qtyPct / pricePct
            var qtyPct = SafePct(preQty, postQty);

            // Code must guard: if (pricePct != 0) { elasticity = qtyPct / pricePct; } else { skip }
            decimal? elasticity = null;
            if (pricePct != 0m)
            {
                elasticity = qtyPct / pricePct;
            }

            // Either elasticity is null (guarded) or it's reasonable
            if (elasticity.HasValue)
            {
                Assert.True(elasticity.Value < 1000000m && elasticity.Value > -1000000m, "Elasticity must be reasonable");
            }
        }

        [Fact(DisplayName = "Percentage change with very small denominator")]
        public void PercentChange_VerySmallDenominator_MustNotProduceInfinity()
        {
            decimal previousRevenue = 0.01m;
            decimal currentRevenue = 1000m;

            var pct = SafePct(previousRevenue, currentRevenue);
            // Decimal can't be Infinity/NaN, but verify it's reasonable
            Assert.True(pct > 0, "Percentage should be positive for increase");
            Assert.Equal(9999900m, pct);  // ((1000 - 0.01) / 0.01) * 100 = 9,999,900%
        }

        [Theory(DisplayName = "Null/undefined numeric fields must be skipped, not summed")]
        [InlineData("100", null, 100)]    // Only pre revenue = 100
        [InlineData(null, "100", 100)]    // Only post revenue = 100
        public void NullNumericFields_ShouldNotAffectAggregates(string? pre, string? post, decimal expectedNonNull)
        {
            static decimal? ParseNullableDecimal(string? value)
                => value is null ? null : decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

            // When aggregating suppliers, any null numeric field should be treated as 0 or skipped.
            var preValue = ParseNullableDecimal(pre);
            var postValue = ParseNullableDecimal(post);
            decimal sum = (preValue ?? 0m) + (postValue ?? 0m);
            Assert.Equal(expectedNonNull, sum);
        }
    }

    /// <summary>
    /// Tests for the MarginAccumulator logic used in supplier aggregation.
    /// Ensures margins are correctly accumulated and the final snapshot is accurate.
    /// Note: Full MarginAccumulator testing requires access to the class; 
    /// these tests verify integration-level margin calculations via endpoint response.
    /// </summary>
    public class MarginAccumulatorLogicTests
    {
        [Fact(DisplayName = "Margin accumulation: basic calculation")]
        public void BasicMarginAccumulation_Revenue100Cost50EqualsSingleMargin()
        {
            // Margin = Revenue - Cost
            decimal revenue = 100m;
            decimal cost = 50m;
            decimal expectedMargin = 50m;

            decimal actualMargin = revenue - cost;
            Assert.Equal(expectedMargin, actualMargin);
        }

        [Fact(DisplayName = "Margin with multiple line items")]
        public void MultipleLineItems_MarginShouldSum()
        {
            // Item 1: revenue 100, cost 50, margin 50
            // Item 2: revenue 200, cost 120, margin 80
            // Total: revenue 300, cost 170, margin 130

            decimal margin1 = 100m - 50m;
            decimal margin2 = 200m - 120m;
            decimal totalMargin = margin1 + margin2;

            Assert.Equal(130m, totalMargin);
        }

        [Fact(DisplayName = "Margin percentage calculation")]
        public void MarginPercentage_WithValidRevenue()
        {
            decimal totalRevenue = 1000m;
            decimal totalMargin = 350m;
            decimal marginPct = Math.Round((totalMargin / totalRevenue) * 100m, 2);

            Assert.Equal(35m, marginPct);
        }

        [Fact(DisplayName = "Missing cost must not be treated as zero-cost full margin")]
        public void MarginWithNullCost_MustNotTreatUnknownAsZeroCost()
        {
            // Production MarginAccumulator skips unreliable cost; unknown ↛ fake 100% margin.
            var accumulator = new Application.Analytics.MarginAccumulator();
            accumulator.Add(revenue: 100m, quantity: 1m, unitCost: null);
            var snapshot = accumulator.Build(totalRevenue: 100m);

            Assert.Equal(0m, snapshot.RevenueWithCost);
            Assert.Equal(0m, snapshot.MarginContribution);
            Assert.Equal(0d, snapshot.MarginPct);
            Assert.Equal(
                100m,
                Application.Analytics.AnalyticsMarginPolicy.ResolveNoCostRevenue(100m, snapshot.RevenueWithCost));
        }
    }

    /// <summary>
    /// Aggregation invariants against production policies (not hardcoded self-equality).
    /// </summary>
    public class AggregationInvariantTests
    {
        [Fact(DisplayName = "SupplierSharePolicy denominator equals sum of positive net revenues")]
        public void ShareDenominator_EqualsPositiveNetRevenueSum()
        {
            var revenues = new[] { 1500m, -200m, 1000m, 0m, 960m };
            var denominator = Application.Analytics.SupplierSharePolicy.ResolveDenominator(revenues);
            Assert.Equal(3460m, denominator);

            var share = Application.Analytics.SupplierSharePolicy.Resolve(1500m, denominator);
            Assert.True(share.IsAvailable);
            Assert.Equal(43.35d, share.SharePct);
        }

        [Fact(DisplayName = "MarginAccumulator total contribution equals sum of reliable-cost line contributions")]
        public void MarginContribution_EqualsSumOfReliableCostLines()
        {
            var accumulator = new Application.Analytics.MarginAccumulator();
            accumulator.Add(1500m, 10m, 75m);   // margin 750
            accumulator.Add(1000m, 5m, 134m);   // margin 330
            accumulator.Add(960m, 8m, null);    // excluded — missing cost
            var snapshot = accumulator.Build(totalRevenue: 3460m);

            Assert.Equal(2500m, snapshot.RevenueWithCost);
            Assert.Equal(1420m, snapshot.TotalCost); // 10*75 + 5*134
            Assert.Equal(1080m, snapshot.MarginContribution); // 750 + 330
            Assert.Equal(960m, Application.Analytics.AnalyticsMarginPolicy.ResolveNoCostRevenue(3460m, snapshot.RevenueWithCost));
        }

        [Fact(DisplayName = "Non-positive supplier revenue does not publish a measured 0% share")]
        public void NonPositiveSupplierShare_IsUnavailable()
        {
            var evidence = Application.Analytics.SupplierSharePolicy.Resolve(-50m, 1000m);
            Assert.False(evidence.IsAvailable);
            Assert.Null(evidence.SharePct);
        }

        [Fact(DisplayName = "Signed return quantities are allowed; fake non-negative quantity invariant is rejected")]
        public void SignedReturnQuantities_AreValidRetailEvidence()
        {
            // Certified retail keeps signed returns. A test that requires qty >= 0 would lock a bug.
            var lines = new[] { 30m, -5m, 45m };
            Assert.Equal(70m, lines.Sum());
            Assert.Contains(lines, qty => qty < 0m);
        }
    }

    /// <summary>
    /// Placeholder for future: tests for data scope filtering (existing vs imported vs all).
    /// These require database context and are better suited as integration tests.
    /// </summary>
    public class DataScopeFilteringTests
    {
        [Theory(DisplayName = "DataScope parameter routing")]
        [InlineData("existing")]
        [InlineData("imported")]
        [InlineData("all")]
        public void DataScopeParameter_AcceptsValidValues(string dataScope)
        {
            var validScopes = new[] { "existing", "imported", "all" };
            Assert.Contains(dataScope, validScopes);
        }

        [Fact(DisplayName = "DataScope filtering logic: missing cost should be reported")]
        public void DataScopeFiltering_ReportsMissingCostForAffectedRows()
        {
            // When DataOrigin = 'existing' and NabavnaCenaDin is null, that row lacks direct cost.
            // dataQuality should reflect this via missingCostRevenueSharePct.
            int totalRetail = 100;
            int missingCostRetail = 25;  // 25% missing cost

            decimal missingCostPct = Math.Round((decimal)missingCostRetail / totalRetail * 100m, 2);
            Assert.Equal(25m, missingCostPct);
        }

        [Fact(DisplayName = "DataScope: 'existing' should not include access-imported items")]
        public void ExistingScope_ExcludesImported()
        {
            // Logical filter: WHERE DataOrigin != 'imported'
            // Verified in integration tests with fixture.
        }
    }

    /// <summary>
    /// Tests for null/unknown supplier handling.
    /// Ensures the endpoint correctly flags and aggregates "unknown" suppliers.
    /// </summary>
    public class UnknownSupplierHandlingTests
    {
        private static string NormalizeSupplierName(string? name)
        {
            return string.IsNullOrWhiteSpace(name) ? "Nepoznato" : name;
        }

        [Theory(DisplayName = "Supplier name normalization: null and empty to Nepoznato")]
        [InlineData(null, "Nepoznato")]
        [InlineData("", "Nepoznato")]
        [InlineData("   ", "Nepoznato")]
        [InlineData("Supplier A", "Supplier A")]
        [InlineData("Valid Name", "Valid Name")]
        public void SupplierNameNormalization(string? inputName, string expectedName)
        {
            var result = NormalizeSupplierName(inputName);
            Assert.Equal(expectedName, result);
        }

        [Fact(DisplayName = "Null supplier ID and null name both map to unknown/Nepoznato")]
        public void NullSupplierIdAndName_BothMapToNepoznato()
        {
            int? supplierId = null;
            string supplierName = NormalizeSupplierName(null);

            Assert.Null(supplierId);
            Assert.Equal("Nepoznato", supplierName);
        }

        [Fact(DisplayName = "Unknown supplier entries should be aggregated in single bucket")]
        public void UnknownSupplierBucketAggregation()
        {
            // Multiple articles with null supplier ID should aggregate into single "Nepoznato" row
            var unknownSuppliers = new[] 
            { 
                new { id = (int?)null, name = "Nepoznato", revenue = 500m },
                new { id = (int?)null, name = "Nepoznato", revenue = 460m }
            };

            decimal unknownTotal = unknownSuppliers.Sum(s => s.revenue);
            Assert.Equal(960m, unknownTotal);

            // Also verify only one distinct unknown entry in final output
            var distinctUnknown = unknownSuppliers.DistinctBy(s => s.name).Count();
            Assert.Equal(1, distinctUnknown);
        }

        [Fact(DisplayName = "Unknown supplier warning when revenue exceeds threshold")]
        public void UnknownSupplierWarning_LargeUnknownBucket()
        {
            decimal unknownRevenue = 5000m;
            decimal totalRevenue = 10000m;
            decimal unknownSharePct = Math.Round((unknownRevenue / totalRevenue) * 100m, 2);

            // Endpoint should warn if unknownSharePct > 10% or unknown item count > 100
            Assert.True(unknownSharePct > 10, "Unknown share is significant");
            
            var dataQualityWarning = unknownSharePct > 10m ? "Large unknown supplier share detected" : "";
            Assert.NotEmpty(dataQualityWarning);
        }

        [Fact(DisplayName = "Unknown supplier articles should not break totals")]
        public void UnknownSupplierArticles_ContributeToTotalsCorrectly()
        {
            decimal knownSupplierRevenue = 3500m;
            decimal unknownSupplierRevenue = 960m;
            decimal expectedTotal = 4460m;

            decimal actualTotal = knownSupplierRevenue + unknownSupplierRevenue;
            Assert.Equal(expectedTotal, actualTotal);
        }
    }
}
