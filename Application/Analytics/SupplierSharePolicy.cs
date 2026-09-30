namespace Application.Analytics;

public readonly record struct SupplierShareEvidence(
    decimal? Numerator,
    decimal Denominator,
    double? SharePct,
    string State,
    bool IsAvailable);

public static class SupplierSharePolicy
{
    public const string Basis = "positive_net_revenue";
    public const string NumeratorBasis = "positive_supplier_net_revenue";
    public const string DenominatorBasis = "positive_net_revenue_declared_population";
    public const string MeasuredState = "measured";
    public const string NonPositiveSupplierState = "unavailable_non_positive_supplier_net_revenue";
    public const string NonPositiveDenominatorState = "unavailable_non_positive_net_revenue";

    public static decimal ResolveDenominator(IEnumerable<decimal> revenues)
        => revenues.Sum(revenue => Math.Max(0m, revenue));

    public static SupplierShareEvidence Resolve(decimal revenue, decimal denominator)
    {
        if (denominator <= 0m)
        {
            return new SupplierShareEvidence(null, denominator, null, NonPositiveDenominatorState, false);
        }

        if (revenue <= 0m)
        {
            return new SupplierShareEvidence(null, denominator, null, NonPositiveSupplierState, false);
        }

        var sharePct = (double)(revenue / denominator * 100m);
        var isFinite = double.IsFinite(sharePct);
        return new SupplierShareEvidence(
            Math.Round(revenue, 2),
            Math.Round(denominator, 2),
            isFinite ? Math.Round(sharePct, 2) : null,
            isFinite ? MeasuredState : NonPositiveDenominatorState,
            isFinite);
    }
}
