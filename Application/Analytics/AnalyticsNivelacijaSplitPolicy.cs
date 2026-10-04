namespace Application.Analytics;

public readonly record struct NivelacijaSplitSnapshot(
    decimal PreRevenue,
    int PreQuantity,
    decimal PostRevenue,
    int PostQuantity,
    decimal RevenueWithSplit,
    decimal ComparableRevenueWithSplit,
    decimal ComparablePreRevenue,
    decimal ComparablePostRevenue,
    int ArticleCountWithNivelacija,
    int ComparableArticleCount,
    int ComparablePreQuantity,
    int ComparablePostQuantity,
    double? RevenueCoveragePct,
    double? ComparableRevenueCoveragePct,
    double? RevenueImpactPct,
    double? UnitsImpactPct,
    bool HasComparableSignal,
    string? SignalNote);

public readonly record struct NivelacijaComparableSignal(
    double? RevenueImpactPct,
    double? UnitsImpactPct,
    string? SignalNote);

public static class AnalyticsNivelacijaSplitPolicy
{
    public const int MinimumComparablePreQuantity = 5;
    public const double MinimumComparableCoveragePct = 15d;
    public const int MaximumComparableWindowDays = 30;

    public static NivelacijaSplitSnapshot Build<T>(
        IEnumerable<T> rows,
        IReadOnlyDictionary<int, DateTime> nivelacijaDateByArticle,
        Func<T, int> articleIdSelector,
        Func<T, DateTime> saleDateSelector,
        Func<T, decimal> revenueSelector,
        Func<T, int> quantitySelector,
        DateTime? periodFrom = null,
        DateTime? periodToExclusive = null)
    {
        var materializedRows = rows as IReadOnlyCollection<T> ?? rows.ToList();
        var totalRevenue = materializedRows.Sum(revenueSelector);
        var periodStart = (periodFrom ?? (materializedRows.Count > 0
            ? materializedRows.Min(saleDateSelector).Date
            : DateTime.MinValue)).Date;
        var periodEndExclusive = (periodToExclusive ?? (materializedRows.Count > 0
            ? materializedRows.Max(saleDateSelector).Date.AddDays(1)
            : DateTime.MinValue)).Date;

        decimal preRevenue = 0m;
        decimal postRevenue = 0m;
        decimal comparablePreRevenue = 0m;
        decimal comparablePostRevenue = 0m;
        decimal revenueWithSplit = 0m;
        int preQuantity = 0;
        int postQuantity = 0;
        int comparablePreQuantity = 0;
        int comparablePostQuantity = 0;
        int articleCountWithNivelacija = 0;
        int comparableArticleCount = 0;
        var hasEventBeforePeriod = false;

        foreach (var articleGroup in materializedRows.GroupBy(articleIdSelector))
        {
            if (!nivelacijaDateByArticle.TryGetValue(articleGroup.Key, out var selectedNivelacijaDate))
            {
                continue;
            }

            var eventDate = selectedNivelacijaDate.Date;
            var eventInPeriod = eventDate >= periodStart && eventDate < periodEndExclusive;
            hasEventBeforePeriod |= eventDate < periodStart;

            // Keep the two observed sides the same calendar length. The selected
            // period bounds cap each side, while 30 days matches the canonical
            // Pre/Post event window. This is a descriptive change, not a causal effect.
            var preDays = eventInPeriod
                ? Math.Min(MaximumComparableWindowDays, Math.Max(0, (eventDate - periodStart).Days))
                : 0;
            var postDays = eventInPeriod
                ? Math.Min(MaximumComparableWindowDays, Math.Max(0, (periodEndExclusive - eventDate).Days))
                : 0;
            var equalWindowDays = Math.Min(preDays, postDays);
            var preWindowStart = eventDate.AddDays(-equalWindowDays);
            var postWindowEnd = eventDate.AddDays(equalWindowDays);

            articleCountWithNivelacija++;

            decimal articlePreRevenue = 0m;
            decimal articlePostRevenue = 0m;
            decimal articleWindowPreRevenue = 0m;
            decimal articleWindowPostRevenue = 0m;
            int articlePreQuantity = 0;
            int articlePostQuantity = 0;
            int articleWindowPreQuantity = 0;
            int articleWindowPostQuantity = 0;
            decimal articleRevenueWithSplit = 0m;

            foreach (var row in articleGroup)
            {
                var revenue = revenueSelector(row);
                var quantity = quantitySelector(row);
                articleRevenueWithSplit += revenue;

                var saleDate = saleDateSelector(row).Date;
                if (saleDate < eventDate)
                {
                    articlePreRevenue += revenue;
                    articlePreQuantity += quantity;
                }
                else
                {
                    articlePostRevenue += revenue;
                    articlePostQuantity += quantity;
                }

                if (saleDate >= preWindowStart && saleDate < eventDate)
                {
                    articleWindowPreRevenue += revenue;
                    articleWindowPreQuantity += quantity;
                }
                else if (saleDate >= eventDate && saleDate < postWindowEnd)
                {
                    articleWindowPostRevenue += revenue;
                    articleWindowPostQuantity += quantity;
                }
            }

            revenueWithSplit += articleRevenueWithSplit;
            preRevenue += articlePreRevenue;
            postRevenue += articlePostRevenue;
            preQuantity += articlePreQuantity;
            postQuantity += articlePostQuantity;

            if (equalWindowDays == 0
                || articleWindowPreRevenue <= 0m
                || articleWindowPostRevenue <= 0m
                || articleWindowPreQuantity <= 0
                || articleWindowPostQuantity <= 0)
            {
                continue;
            }

            comparableArticleCount++;
            comparablePreRevenue += articleWindowPreRevenue;
            comparablePostRevenue += articleWindowPostRevenue;
            comparablePreQuantity += articleWindowPreQuantity;
            comparablePostQuantity += articleWindowPostQuantity;
        }

        var revenueCoveragePct = totalRevenue > 0m
            ? Math.Round((double)(revenueWithSplit / totalRevenue * 100m), 2)
            : (double?)null;

        var comparableRevenueCoveragePct = totalRevenue > 0m
            ? Math.Round((double)((comparablePreRevenue + comparablePostRevenue) / totalRevenue * 100m), 2)
            : (double?)null;

        var comparableSignal = EvaluateComparableSignal(
            comparablePreRevenue,
            comparablePostRevenue,
            comparablePreQuantity,
            comparablePostQuantity,
            comparableArticleCount,
            totalRevenue);
        if (comparableArticleCount == 0 && hasEventBeforePeriod)
        {
            comparableSignal = comparableSignal with
            {
                SignalNote = "Poslednja nivelacija je pre izabranog perioda; nema uporedive pre-baze u ovom periodu."
            };
        }
        else if (comparableArticleCount == 0 && articleCountWithNivelacija > 0)
        {
            comparableSignal = comparableSignal with
            {
                SignalNote = "Nema dovoljno dana za jednako dug pre/post prozor u izabranom periodu."
            };
        }

        return new NivelacijaSplitSnapshot(
            PreRevenue: Math.Round(preRevenue, 2),
            PreQuantity: preQuantity,
            PostRevenue: Math.Round(postRevenue, 2),
            PostQuantity: postQuantity,
            RevenueWithSplit: Math.Round(revenueWithSplit, 2),
            ComparableRevenueWithSplit: Math.Round(comparablePreRevenue + comparablePostRevenue, 2),
            ComparablePreRevenue: Math.Round(comparablePreRevenue, 2),
            ComparablePostRevenue: Math.Round(comparablePostRevenue, 2),
            ArticleCountWithNivelacija: articleCountWithNivelacija,
            ComparableArticleCount: comparableArticleCount,
            ComparablePreQuantity: comparablePreQuantity,
            ComparablePostQuantity: comparablePostQuantity,
            RevenueCoveragePct: revenueCoveragePct,
            ComparableRevenueCoveragePct: comparableRevenueCoveragePct,
            RevenueImpactPct: comparableSignal.RevenueImpactPct,
            UnitsImpactPct: comparableSignal.UnitsImpactPct,
            HasComparableSignal: comparableSignal.RevenueImpactPct.HasValue && comparableSignal.UnitsImpactPct.HasValue,
            SignalNote: comparableSignal.SignalNote);
    }

    public static NivelacijaComparableSignal EvaluateComparableSignal(
        decimal comparablePreRevenue,
        decimal comparablePostRevenue,
        int comparablePreQuantity,
        int comparablePostQuantity,
        int comparableArticleCount,
        decimal totalRevenue)
    {
        var comparableRevenueCoveragePct = totalRevenue > 0m
            ? Math.Round((double)((comparablePreRevenue + comparablePostRevenue) / totalRevenue * 100m), 2)
            : (double?)null;

        string? signalNote = null;
        if (comparableArticleCount == 0)
        {
            signalNote = "Nema artikala sa prodajom i pre i posle prve nivelacije.";
        }
        else if (comparablePreQuantity < MinimumComparablePreQuantity)
        {
            signalNote = $"Pre-baza je premala za pouzdan pre/post signal ({comparablePreQuantity} kom pre nivelacije).";
        }
        else if ((comparableRevenueCoveragePct ?? 0d) < MinimumComparableCoveragePct)
        {
            signalNote = $"Uporediv pre/post signal pokriva samo {comparableRevenueCoveragePct:0.##}% prometa.";
        }

        var revenueImpactPct = signalNote is null && comparablePreRevenue > 0m
            ? Math.Round((double)((comparablePostRevenue - comparablePreRevenue) / comparablePreRevenue * 100m), 2)
            : (double?)null;

        var unitsImpactPct = signalNote is null && comparablePreQuantity > 0
            ? Math.Round((comparablePostQuantity - comparablePreQuantity) / (double)comparablePreQuantity * 100d, 2)
            : (double?)null;

        return new NivelacijaComparableSignal(revenueImpactPct, unitsImpactPct, signalNote);
    }
}
