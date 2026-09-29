using Application.Analytics;
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

    [Fact]
    public void Tier1ManifestHasUniqueSurfaceMetricOwnership()
    {
        var manifest = AnalyticsMetricEvidenceCoveragePolicy.Manifest;

        Assert.NotEmpty(manifest);
        Assert.Equal(
            manifest.Count,
            manifest.Select(spec => $"{spec.Surface}:{spec.MetricKey}").Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(manifest, spec => spec.Surface == "dashboard" && spec.MetricKey == "revenue");
        Assert.Contains(manifest, spec => spec.Surface == "analytics-actions" && spec.MetricKey == "counts");
        Assert.All(manifest, spec =>
        {
            Assert.False(string.IsNullOrWhiteSpace(spec.FormulaVersion));
            Assert.False(string.IsNullOrWhiteSpace(spec.CoverageOwner));
            Assert.False(string.IsNullOrWhiteSpace(spec.Limitation));
        });
    }

    [Fact]
    public void RuntimeEvidenceBindsContextCoverageAndLimitationWithoutTrustingPartialResults()
    {
        var context = AnalyticsContextFingerprintPolicy.Create(
            sourceDataset: "certified_sales_rows",
            sourceGeneration: "sales_header_origin_v1",
            formulaVersion: "dashboard_context_v1",
            materializerGeneration: "dashboard_bootstrap",
            rowLimitSemantics: "section_specific_declared_populations",
            requestedPeriodFromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            requestedPeriodToUtc: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            effectivePeriodFromUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            effectivePeriodToUtc: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            observedPeriodFromUtc: null,
            observedPeriodToUtc: null,
            requestedDataScope: "all",
            effectiveDataScope: "all",
            dataScopeSource: "dashboard_sections_declared",
            populationKey: "dashboard_sales_sections",
            populationFilters: null);
        var meta = new AnalyticsResponseMetaDto
        {
            Success = true,
            IsPartial = true,
            DataQualityStatus = "warning",
            Context = context
        };

        var enriched = AnalyticsMetricEvidenceCoveragePolicy.Enrich(
            "dashboard",
            meta,
            new Dictionary<string, AnalyticsMetricProvenanceDto>
            {
                ["revenue"] = new()
                {
                    Kind = AnalyticsMetricProvenanceKinds.AuthoritativeBackendAggregate,
                    Authority = AnalyticsMetricAuthority.Authoritative,
                    Actionability = AnalyticsMetricActionability.Actionable,
                    Unit = "RSD"
                }
            });

        Assert.Equal("partial", enriched["revenue"].Coverage);
        Assert.Equal(context.Fingerprint, enriched["revenue"].ContextFingerprint);
        Assert.Equal("dashboard_context_v1", enriched["revenue"].FormulaVersion);
        Assert.Equal(AnalyticsMetricActionability.Blocked, enriched["revenue"].Actionability);
        Assert.Contains("qualified", enriched["revenue"].Limitation!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unitsSold", enriched.Keys);
    }

    [Fact]
    public void MissingContextFailsClosedForRuntimeEvidence()
    {
        var enriched = AnalyticsMetricEvidenceCoveragePolicy.Enrich(
            "supplier",
            new AnalyticsResponseMetaDto { Success = false },
            new Dictionary<string, AnalyticsMetricProvenanceDto>
            {
                ["revenue"] = new()
                {
                    Kind = AnalyticsMetricProvenanceKinds.AuthoritativeBackendAggregate,
                    Authority = AnalyticsMetricAuthority.Authoritative,
                    Actionability = AnalyticsMetricActionability.Actionable
                }
            });

        Assert.Equal(AnalyticsMetricProvenanceKinds.Unknown, enriched["revenue"].Kind);
        Assert.Equal(AnalyticsMetricAuthority.Unknown, enriched["revenue"].Authority);
        Assert.Equal(AnalyticsMetricEvidenceCoveragePolicy.CoverageUnavailable, enriched["revenue"].Coverage);
        Assert.Null(enriched["revenue"].ContextFingerprint);
        Assert.Equal(AnalyticsMetricActionability.Blocked, enriched["revenue"].Actionability);
    }
}
