using System.Linq.Expressions;
using Domain.Model.Analytics;

namespace Application.Analytics;

/// <summary>
/// Positive-only quarantine marker for synthetic Actions fixtures. Operational
/// actions may have incomplete evidence and unusual titles without being fixtures.
/// </summary>
public static class AnalyticsActionFixturePolicy
{
    private static readonly Expression<Func<AnalyticsActionItem, bool>> SmokeFixtureExpression = item =>
        item.SourceKey != null
        && (item.SourceKey.ToLower() == "smoke"
            || item.SourceKey.ToLower().StartsWith("smoke:")
            || item.SourceKey.ToLower().EndsWith(":smoke")
            || item.SourceKey.ToLower().Contains(":smoke:"));

    private static readonly Expression<Func<AnalyticsActionItem, bool>> OperationalActionExpression =
        Expression.Lambda<Func<AnalyticsActionItem, bool>>(
            Expression.Not(SmokeFixtureExpression.Body),
            SmokeFixtureExpression.Parameters);

    public static Expression<Func<AnalyticsActionItem, bool>> SmokeFixturePredicate => SmokeFixtureExpression;

    public static IQueryable<AnalyticsActionItem> ExcludeKnownSmokeFixtures(
        this IQueryable<AnalyticsActionItem> query)
        => query.Where(OperationalActionExpression);

    public static bool IsSmokeFixtureSourceKey(string? sourceKey)
        => sourceKey?.Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, "smoke", StringComparison.OrdinalIgnoreCase)) == true;
}
