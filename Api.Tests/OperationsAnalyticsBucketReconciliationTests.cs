using Infrastructure.Services;
using Xunit;

namespace Api.Tests;

public sealed class OperationsAnalyticsBucketReconciliationTests
{
    [Fact]
    public void Compare_DetectsSwappedBucketsWhenGrandTotalsAreIdentical()
    {
        var expected = new[]
        {
            new OperationsAnalyticsBucketReconciliation.BucketValue("id:10", 40m, 4),
            new OperationsAnalyticsBucketReconciliation.BucketValue("id:20", 60m, 6)
        };
        var actual = new[]
        {
            new OperationsAnalyticsBucketReconciliation.BucketValue("id:10", 60m, 6),
            new OperationsAnalyticsBucketReconciliation.BucketValue("id:20", 40m, 4)
        };

        Assert.Equal(expected.Sum(bucket => bucket.Revenue), actual.Sum(bucket => bucket.Revenue));
        Assert.Equal(expected.Sum(bucket => bucket.Units), actual.Sum(bucket => bucket.Units));

        var deltas = OperationsAnalyticsBucketReconciliation.Compare("supplier", expected, actual);

        Assert.Equal(2, deltas.Count);
        Assert.Contains(deltas, delta => delta.Dimension == "supplier:id:10" && delta.RevenueDelta == 20m && delta.UnitsDelta == 2);
        Assert.Contains(deltas, delta => delta.Dimension == "supplier:id:20" && delta.RevenueDelta == -20m && delta.UnitsDelta == -2);
    }

    [Fact]
    public void Compare_KeepsSignedUnknownAndNegativeIdentityBucketsSeparate()
    {
        var expected = new[]
        {
            new OperationsAnalyticsBucketReconciliation.BucketValue("unknown", -10m, -1),
            new OperationsAnalyticsBucketReconciliation.BucketValue("id:-1", 10m, 1)
        };
        var actual = new[]
        {
            new OperationsAnalyticsBucketReconciliation.BucketValue("unknown", 10m, 1),
            new OperationsAnalyticsBucketReconciliation.BucketValue("id:-1", -10m, -1)
        };

        Assert.Equal(0m, expected.Sum(bucket => bucket.Revenue));
        Assert.Equal(0m, actual.Sum(bucket => bucket.Revenue));

        var deltas = OperationsAnalyticsBucketReconciliation.Compare("supplier", expected, actual);

        Assert.Equal(2, deltas.Count);
        Assert.Contains(deltas, delta => delta.Dimension == "supplier:unknown");
        Assert.Contains(deltas, delta => delta.Dimension == "supplier:id:-1");
    }
}
