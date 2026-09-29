using Application.Analytics;
using Xunit;

namespace Api.Tests;

[Trait("Category", "Unit")]
public sealed class AnalyticsContextFingerprintTests
{
    private static AnalyticsContextDescriptor Create(
        IReadOnlyDictionary<string, string?>? filters = null,
        string sourceGeneration = "sales_header_origin_v1",
        string resultState = AnalyticsContextFingerprintPolicy.StateAvailable,
        DateTime? requestedFromUtc = null)
        => AnalyticsContextFingerprintPolicy.Create(
            sourceDataset: "certified_sales_rows",
            sourceGeneration: sourceGeneration,
            formulaVersion: "sales_context_v1",
            materializerGeneration: "live_query",
            rowLimitSemantics: "all_filtered_rows",
            requestedPeriodFromUtc: requestedFromUtc ?? new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            requestedPeriodToUtc: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            effectivePeriodFromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            effectivePeriodToUtc: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            observedPeriodFromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            observedPeriodToUtc: new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            requestedDataScope: "all",
            effectiveDataScope: "all",
            dataScopeSource: "sale_header_data_origin",
            populationKey: "certified_retail_sales",
            populationFilters: filters ?? new Dictionary<string, string?>
            {
                ["storeId"] = "7",
                ["supplierId"] = null
            },
            resultState: resultState);

    [Fact]
    public void FingerprintIsStableWhenFilterInsertionOrderChanges()
    {
        var first = Create(new Dictionary<string, string?>
        {
            ["supplierId"] = null,
            ["storeId"] = "7"
        });
        var second = Create(new Dictionary<string, string?>
        {
            ["storeId"] = "7",
            ["supplierId"] = null
        });

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(AnalyticsContextFingerprintPolicy.StateAvailable, first.State);
        Assert.Matches("^sha256:[0-9a-f]{64}$", first.Fingerprint!);
    }

    [Fact]
    public void FingerprintChangesWhenPeriodOrSourceGenerationChanges()
    {
        var baseline = Create();
        var differentPeriod = Create(requestedFromUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        var differentSource = Create(sourceGeneration: "sales_header_origin_v2");

        Assert.NotEqual(baseline.Fingerprint, differentPeriod.Fingerprint);
        Assert.NotEqual(baseline.Fingerprint, differentSource.Fingerprint);
    }

    [Fact]
    public void UnspecifiedDateInputsAreInterpretedAsUtcForDeterministicIdentity()
    {
        var utc = Create(requestedFromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var unspecified = Create(requestedFromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified));

        Assert.Equal(utc.Fingerprint, unspecified.Fingerprint);
        Assert.Equal(DateTimeKind.Utc, unspecified.RequestedPeriodFromUtc!.Value.Kind);
    }

    [Fact]
    public void EmptyStateKeepsTheSameContextIdentityWithoutPretendingToBeUnavailable()
    {
        var available = Create();
        var empty = Create(resultState: AnalyticsContextFingerprintPolicy.StateEmpty);

        Assert.Equal(available.Fingerprint, empty.Fingerprint);
        Assert.Equal(AnalyticsContextFingerprintPolicy.StateEmpty, empty.State);
        Assert.Null(empty.UnavailableReason);
    }

    [Fact]
    public void MissingGenerationFailsClosedWithoutAComparableFingerprint()
    {
        var descriptor = AnalyticsContextFingerprintPolicy.Create(
            sourceDataset: "certified_sales_rows",
            sourceGeneration: null,
            formulaVersion: "sales_context_v1",
            materializerGeneration: "live_query",
            rowLimitSemantics: "all_filtered_rows",
            requestedPeriodFromUtc: null,
            requestedPeriodToUtc: null,
            effectivePeriodFromUtc: null,
            effectivePeriodToUtc: null,
            observedPeriodFromUtc: null,
            observedPeriodToUtc: null,
            requestedDataScope: "all",
            effectiveDataScope: "all",
            dataScopeSource: "sale_header_data_origin",
            populationKey: "certified_retail_sales",
            populationFilters: null);

        Assert.Equal(AnalyticsContextFingerprintPolicy.StateUnavailable, descriptor.State);
        Assert.Null(descriptor.Fingerprint);
        Assert.Contains("sourceGeneration", descriptor.UnavailableReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ReconciliationProducesReadOnlyOutputOnlyForComparableContexts()
    {
        var context = Create();
        var results = AnalyticsContextReconciliation.Compare(
        [
            new AnalyticsReconciliationCase(
                "/api/dashboard", "/api/supplier", "revenue",
                context, context, 540m, 540m),
            new AnalyticsReconciliationCase(
                "/api/dashboard", "/api/supplier", "revenue",
                context, Create(sourceGeneration: "sales_header_origin_v2"), 540m, 540m),
            new AnalyticsReconciliationCase(
                "/api/dashboard", "/api/supplier", "revenue",
                context, context, 540m, 541m),
            new AnalyticsReconciliationCase(
                "/api/dashboard", "/api/supplier", "revenue",
                AnalyticsContextFingerprintPolicy.Create(
                    sourceDataset: "certified_sales_rows",
                    sourceGeneration: null,
                    formulaVersion: "sales_context_v1",
                    materializerGeneration: "live_query",
                    rowLimitSemantics: "all_filtered_rows",
                    requestedPeriodFromUtc: null,
                    requestedPeriodToUtc: null,
                    effectivePeriodFromUtc: null,
                    effectivePeriodToUtc: null,
                    observedPeriodFromUtc: null,
                    observedPeriodToUtc: null,
                    requestedDataScope: "all",
                    effectiveDataScope: "all",
                    dataScopeSource: "sale_header_data_origin",
                    populationKey: "certified_retail_sales",
                    populationFilters: null),
                context,
                540m,
                540m)
        ]);

        Assert.Equal(AnalyticsContextReconciliation.Reconciled, results[0].Classification);
        Assert.Equal(0m, results[0].Delta);
        Assert.Equal(context.Fingerprint, results[0].ContextFingerprint);
        Assert.Equal(AnalyticsContextReconciliation.NonComparableContext, results[1].Classification);
        Assert.Null(results[1].Delta);
        Assert.Equal(AnalyticsContextReconciliation.UnexplainedDelta, results[2].Classification);
        Assert.Equal(1m, results[2].Delta);
        Assert.Equal(AnalyticsContextReconciliation.UnavailableContext, results[3].Classification);
        Assert.Contains("not evaluated", results[3].Explanation, StringComparison.Ordinal);
    }
}
