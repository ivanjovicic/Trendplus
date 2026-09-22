using System.Collections.Generic;
using Trendplus2.Dtos;
using Xunit;

namespace Api.Tests;

public sealed class AnalyticsMetricProvenanceContractTests
{
    [Fact]
    public void ProvenanceVocabulary_CoversCriticalMetricKinds()
    {
        Assert.Equal("authoritative_backend_aggregate", AnalyticsMetricProvenanceKinds.AuthoritativeBackendAggregate);
        Assert.Equal("observed_row_value", AnalyticsMetricProvenanceKinds.ObservedRowValue);
        Assert.Equal("frontend_display_derivation", AnalyticsMetricProvenanceKinds.FrontendDisplayDerivation);
        Assert.Equal("modeled_estimated", AnalyticsMetricProvenanceKinds.ModeledEstimated);
        Assert.Equal("unknown", AnalyticsMetricProvenanceKinds.Unknown);
    }

    [Fact]
    public void MetaFactory_PreservesMetricProvenanceWithUnitAndDenominator()
    {
        var provenance = new Dictionary<string, AnalyticsMetricProvenanceDto>
        {
            ["revenueShare"] = new()
            {
                Kind = AnalyticsMetricProvenanceKinds.AuthoritativeBackendAggregate,
                Authority = AnalyticsMetricAuthority.Authoritative,
                Actionability = AnalyticsMetricActionability.Informational,
                Unit = "ratio",
                Denominator = "period_total_revenue"
            },
            ["margin"] = new()
            {
                Kind = AnalyticsMetricProvenanceKinds.ObservedRowValue,
                Authority = AnalyticsMetricAuthority.Observed,
                Actionability = AnalyticsMetricActionability.Informational,
                Unit = "RSD"
            },
            ["confidence"] = new()
            {
                Kind = AnalyticsMetricProvenanceKinds.ModeledEstimated,
                Authority = AnalyticsMetricAuthority.Modeled,
                Actionability = AnalyticsMetricActionability.Actionable,
                Unit = "ratio",
                Denominator = "eligible_signal_count"
            },
            ["reliability"] = new()
            {
                Kind = AnalyticsMetricProvenanceKinds.Unknown,
                Authority = AnalyticsMetricAuthority.Unknown,
                Actionability = AnalyticsMetricActionability.Blocked
            },
            ["counts"] = new()
            {
                Kind = AnalyticsMetricProvenanceKinds.FrontendDisplayDerivation,
                Authority = AnalyticsMetricAuthority.Derived,
                Actionability = AnalyticsMetricActionability.Informational,
                Unit = "items"
            }
        };

        var meta = AnalyticsResponseMetaFactory.Success(metricProvenance: provenance);

        Assert.True(meta.Success);
        Assert.NotNull(meta.MetricProvenance);
        Assert.Equal("period_total_revenue", meta.MetricProvenance!["revenueShare"].Denominator);
        Assert.Equal("RSD", meta.MetricProvenance["margin"].Unit);
        Assert.Equal(AnalyticsMetricActionability.Blocked, meta.MetricProvenance["reliability"].Actionability);
    }

    [Fact]
    public void ExistingMetaContract_RemainsBackwardCompatibleWhenProvenanceIsOmitted()
    {
        var meta = AnalyticsResponseMetaFactory.Success();

        Assert.True(meta.Success);
        Assert.Null(meta.MetricProvenance);
    }
}
