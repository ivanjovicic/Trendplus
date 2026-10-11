using Api.Services;
using Xunit;

namespace Api.Tests;

public sealed class AccessStoreDayCoverageEvidenceTests
{
    [Fact]
    public void DuplicateOrSkippedSourceRows_CannotCertifyCompleteCoverage()
    {
        Assert.False(AccessStoreDayCoverageEvidence.IsCompleteSourceScan(
            sourceSnapshotComplete: true,
            sourceHeaderRows: 2,
            acceptedHeaderRows: 2,
            persistedHeaderRows: 1,
            sourceLineRows: 3,
            acceptedLineRows: 3,
            persistedLineRows: 3,
            unknownDateRows: 0));
        Assert.False(AccessStoreDayCoverageEvidence.IsCompleteSourceScan(
            sourceSnapshotComplete: true,
            sourceHeaderRows: 2,
            acceptedHeaderRows: 1,
            persistedHeaderRows: 1,
            sourceLineRows: 3,
            acceptedLineRows: 3,
            persistedLineRows: 3,
            unknownDateRows: 0));
        Assert.False(AccessStoreDayCoverageEvidence.IsCompleteSourceScan(
            sourceSnapshotComplete: true,
            sourceHeaderRows: 2,
            acceptedHeaderRows: 2,
            persistedHeaderRows: 2,
            sourceLineRows: 3,
            acceptedLineRows: 3,
            persistedLineRows: 3,
            unknownDateRows: 1));
    }

    [Fact]
    public void UnknownStoreIdentity_PreventsEmptyDayCertification()
    {
        var evidence = AccessStoreDayCoverageEvidence.Build(
        [
            new AccessStoreDayCounts(1, new DateOnly(2026, 3, 28), 2, 3),
            new AccessStoreDayCounts(1, new DateOnly(2026, 3, 30), 1, 1),
            new AccessStoreDayCounts(2, new DateOnly(2026, 3, 29), 1, 2),
            new AccessStoreDayCounts(null, new DateOnly(2026, 3, 29), 1, 1)
        ],
        sourceDayComplete: true);

        Assert.All(evidence.Where(row => row.StoreId.HasValue), row => Assert.Equal("partial", row.Status));
        Assert.Contains(evidence, row => row.StoreId is null &&
            row.Status == "unverified" && row.ReasonCode == "missing_store_identity");
        Assert.DoesNotContain(evidence, row => row.Status == "verified_empty");
    }

    [Fact]
    public void CompleteMappedStores_ProveEmptyDatesOnlyInsideTheirObservedRanges()
    {
        var evidence = AccessStoreDayCoverageEvidence.Build(
        [
            new AccessStoreDayCounts(1, new DateOnly(2026, 3, 28), 2, 3),
            new AccessStoreDayCounts(1, new DateOnly(2026, 3, 30), 1, 1),
            new AccessStoreDayCounts(2, new DateOnly(2026, 3, 29), 1, 2)
        ],
        sourceDayComplete: true);

        Assert.Contains(evidence, row => row.StoreId == 1 && row.BusinessDate == new DateOnly(2026, 3, 29) && row.Status == "verified_empty");
        Assert.Contains(evidence, row => row.StoreId == 1 && row.Status == "observed_sales");
        Assert.Contains(evidence, row => row.StoreId == 2 && row.Status == "observed_sales");
        Assert.DoesNotContain(evidence, row => row.StoreId == 2 && row.Status == "verified_empty");
        Assert.DoesNotContain(evidence, row => row.BusinessDate == new DateOnly(2026, 3, 27));
    }

    [Fact]
    public void UnmappedStore_DoesNotContributeVerifiedCoverage()
    {
        var evidence = AccessStoreDayCoverageEvidence.Build(
        [new AccessStoreDayCounts(99, new DateOnly(2026, 10, 25), 1, 1, StoreMapped: false)],
        sourceDayComplete: true);

        var row = Assert.Single(evidence);
        Assert.Equal(99, row.StoreId);
        Assert.Equal("unverified", row.Status);
        Assert.Equal("unmapped_store_id", row.ReasonCode);
    }

    [Fact]
    public void IncompleteOrFailedSourceScan_NeverPromotesObservedDayToComplete()
    {
        var evidence = AccessStoreDayCoverageEvidence.Build(
        [
            new AccessStoreDayCounts(1, new DateOnly(2026, 10, 24), 1, 2),
            new AccessStoreDayCounts(1, new DateOnly(2026, 10, 26), 1, 1)
        ],
        sourceDayComplete: false);

        Assert.All(evidence, row => Assert.Equal("partial", row.Status));
        Assert.DoesNotContain(evidence, row => row.Status == "verified_empty");
        Assert.Equal(3, evidence.Sum(row => row.SaleLineCount)); // signed returns stay source rows
    }
}
