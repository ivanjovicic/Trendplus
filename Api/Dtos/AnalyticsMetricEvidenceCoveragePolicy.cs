using System.Collections.ObjectModel;

namespace Trendplus2.Dtos;

public sealed record AnalyticsMetricEvidenceCoverageSpec(
    string Surface,
    string MetricKey,
    string DefaultKind,
    string DefaultAuthority,
    string DefaultActionability,
    string? Unit,
    string? Denominator,
    string FormulaVersion,
    string CoverageOwner,
    string Limitation);

/// <summary>
/// Runtime evidence manifest for the selected Tier-1 metric representatives.
/// Methodology text may describe these metrics, but it cannot create runtime evidence.
/// </summary>
public static class AnalyticsMetricEvidenceCoveragePolicy
{
    public const string CoverageComplete = "complete";
    public const string CoveragePartial = "partial";
    public const string CoverageEmpty = "empty";
    public const string CoverageUnavailable = "unavailable";

    public static IReadOnlyList<AnalyticsMetricEvidenceCoverageSpec> Manifest { get; } =
    [
        Spec("dashboard", "revenue", "RSD", "period_total_revenue", "dashboard_context_v1", "Dashboard backend aggregate; not accounting profit."),
        Spec("dashboard", "unitsSold", "units", "period_units", "dashboard_context_v1", "Dashboard aggregate is limited to the declared sales population."),
        Spec("dashboard", "counts", "items", "dashboard_sections_declared_populations", "dashboard_context_v1", "Section counts may use declared section populations."),
        Spec("product-decision-center", "counts", "items", "returned_rows_and_summary_counts", "product_decision_context_v1", "Top-row and ignored-row semantics remain explicit."),
        Spec("product-decision-center", "expectedImpactRsd", "RSD", "backend_expected_impact_when_allowed", "product_decision_context_v1", "Expected impact is unavailable when the backend blocks the recommendation."),
        Spec("product-decision-center", "confidence", "percent", "backend_recommendation_confidence", "product_decision_context_v1", "Confidence is a backend signal and is not a causal effect."),
        Spec("supplier", "revenue", "RSD", "period_total_net_sales_signed", "supplier_sales_context_v1", "Signed net-sales basis; returns remain part of the declared population."),
        Spec("supplier", "revenueShare", "percent", "positive_net_sales_share_declared_population", "supplier_sales_context_v1", "Share is only comparable inside the declared supplier population."),
        Spec("supplier", "margin", "RSD", "covered_cost_sales_rows", "supplier_sales_context_v1", "Partial or fallback cost evidence remains qualified."),
        Spec("supplier", "confidence", "percent", "backend_recommendation_gate", "supplier_sales_context_v1", "Recommendation confidence is blocked when the backend trust gate blocks actionability."),
        Spec("supplier", "reliability", "percent", "backend_recommendation_gate", "supplier_sales_context_v1", "Reliability is a trust signal, not a frontend score."),
        Spec("supplier", "counts", "items", "all_filtered_rows", "supplier_sales_context_v1", "Row count follows the declared filtered population."),
        Spec("shoe-type", "revenue", "RSD", "period_total_net_sales_signed", "shoe_type_sales_context_v1", "Signed net-sales basis; returns remain part of the declared population."),
        Spec("shoe-type", "revenueShare", "percent", "positive_net_sales_share_declared_population", "shoe_type_sales_context_v1", "Share is only comparable inside the declared shoe-type population."),
        Spec("shoe-type", "margin", "RSD", "covered_cost_sales_rows", "shoe_type_sales_context_v1", "Partial or fallback cost evidence remains qualified."),
        Spec("shoe-type", "confidence", "percent", "backend_recommendation_gate", "shoe_type_sales_context_v1", "Recommendation confidence is blocked when the backend trust gate blocks actionability."),
        Spec("shoe-type", "reliability", "percent", "backend_recommendation_gate", "shoe_type_sales_context_v1", "Reliability is a trust signal, not a frontend score."),
        Spec("shoe-type", "counts", "items", "all_filtered_rows", "shoe_type_sales_context_v1", "Row count follows the declared filtered population."),
        Spec("color", "decisionScore", "score", "backend_color_decision_score", "color_sales_context_v1", "Decision score is detail/transparency evidence, not a competing final CTA."),
        Spec("color", "revenueShare", "percent", "net_sales_signed_declared_population", "color_sales_context_v1", "Signed share may be negative or above 100%; unavailable is not zero."),
        Spec("color", "margin", "RSD", "covered_cost_sales_rows", "color_sales_context_v1", "Partial or fallback cost evidence remains qualified."),
        Spec("color", "confidence", "percent", "backend_recommendation_gate", "color_sales_context_v1", "Color is a supporting signal; backend status and reason remain authoritative."),
        Spec("color", "reliability", "percent", "backend_recommendation_gate", "color_sales_context_v1", "Color trust is a supporting signal, not an independent recommendation owner."),
        Spec("color", "counts", "items", "all_filtered_rows", "color_sales_context_v1", "Row count follows the declared filtered population."),
        Spec("color", "prePostNivelacijaRevenueImpactPct", "percent", "comparable_prepost_cohort", "color_sales_context_v1", "Impact requires the backend comparable cohort and remains unavailable otherwise."),
        Spec("color", "prePostNivelacijaUnitsImpactPct", "percent", "comparable_prepost_cohort", "color_sales_context_v1", "Impact requires the backend comparable cohort and remains unavailable otherwise."),
        Spec("color", "prePostComparableArticleCount", "articles", "comparable_prepost_cohort", "color_sales_context_v1", "Comparable article population is evidence, not an action score."),
        Spec("inventory", "stockAtRisk", "items", "declared_inventory_signal_population", "inventory_signal_context_v1", "Inventory signal is snapshot-backed and may be stale or unavailable."),
        Spec("inventory", "stockCoverDays", "days", "declared_inventory_signal_population", "inventory_signal_context_v1", "Coverage depends on the snapshot and its freshness state."),
        Spec("inventory", "inventoryTurnover", "ratio", "declared_inventory_signal_population", "inventory_signal_context_v1", "Turnover is a derived inventory signal, not a causal forecast."),
        Spec("inventory", "counts", "items", "declared_inventory_signal_population", "inventory_signal_context_v1", "Counts follow the snapshot population and row-limit contract."),
        Spec("data-quality", "revenue", "RSD", "data_quality_sales_denominator", "data_quality_health_context_v1", "Revenue is denominator evidence, not a sales recommendation."),
        Spec("data-quality", "missingCostRevenueShare", "percent", "data_quality_sales_denominator", "data_quality_health_context_v1", "Coverage ratio remains distinct from signed sales share."),
        Spec("data-quality", "unknownSupplierRevenueShare", "percent", "data_quality_sales_denominator", "data_quality_health_context_v1", "Unknown-supplier policy remains explicit in the denominator."),
        Spec("data-quality", "dataQualityStatus", "status", "data_quality_health_score", "data_quality_health_context_v1", "Status is backend-owned quality evidence and does not become a numeric KPI."),
        Spec("decision-board", "expectedImpactRsd", "RSD", "backend_source_card_expected_impact", "decision_board_context_v1", "Board composes source-owned impact; it does not recreate recommendation logic."),
        Spec("decision-board", "confidence", "percent", "backend_source_card_confidence", "decision_board_context_v1", "Confidence remains qualified by the source card's trust state."),
        Spec("decision-board", "reliability", "percent", "backend_source_card_reliability", "decision_board_context_v1", "Reliability is a source trust signal."),
        Spec("decision-board", "counts", "items", "board_sections_declared_populations", "decision_board_context_v1", "Board counts are composed from declared source populations."),
        Spec("analytics-actions", "counts", "items", "action_ledger_population", "analytics_actions_context_v1", "Action counts describe the ledger population, not sales volume."),
        Spec("analytics-actions", "dataQualityStatus", "status", "action_ledger_quality_fields", "analytics_actions_context_v1", "Action quality status is copied from backend action evidence."),
        Spec("supplier-report", "revenue", "RSD", "supplier_decision_rows", "supplier_decision_context_v1", "Report revenue follows the supplier decision population."),
        Spec("supplier-report", "margin", "RSD", "covered_cost_sales_rows", "supplier_decision_context_v1", "Report margin remains qualified by cost coverage."),
        Spec("supplier-report", "revenueShare", "percent", "positive_net_sales_share_declared_population", "supplier_decision_context_v1", "Share is not comparable across different report cohorts."),
        Spec("supplier-report", "confidence", "percent", "backend_recommendation_gate", "supplier_decision_context_v1", "Report confidence is backend evidence."),
        Spec("supplier-report", "reliability", "percent", "backend_recommendation_gate", "supplier_decision_context_v1", "Report reliability is backend trust evidence."),
        Spec("supplier-report", "counts", "items", "supplier_rows_with_summary_population", "supplier_decision_context_v1", "Report count follows the summary population."),
        Spec("pilot-intake", "dataQualityStatus", "status", "pilot_intake_quality_status", "pilot_intake_context_v1", "Readiness status is not an accuracy or recommendation score."),
        Spec("pilot-intake", "revenue", "RSD", "data_quality_sales_denominator", "pilot_intake_context_v1", "Pilot revenue is descriptive intake evidence."),
        Spec("pilot-intake", "counts", "items", "pilot_intake_declared_sections", "pilot_intake_context_v1", "Report counts retain section and empty-state semantics.")
    ];

    public static IReadOnlyDictionary<string, AnalyticsMetricProvenanceDto> Enrich(
        string surface,
        AnalyticsResponseMetaDto meta,
        IReadOnlyDictionary<string, AnalyticsMetricProvenanceDto>? existing = null)
    {
        var specs = Manifest
            .Where(spec => string.Equals(spec.Surface, surface, StringComparison.Ordinal))
            .ToDictionary(spec => spec.MetricKey, StringComparer.Ordinal);
        var result = new Dictionary<string, AnalyticsMetricProvenanceDto>(StringComparer.Ordinal);

        if (existing is not null)
        {
            foreach (var pair in existing)
            {
                result[pair.Key] = BuildEntry(pair.Value, specs.GetValueOrDefault(pair.Key), meta);
            }
        }

        foreach (var spec in specs.Values)
        {
            if (!result.ContainsKey(spec.MetricKey))
            {
                result[spec.MetricKey] = BuildEntry(null, spec, meta);
            }
        }

        return new ReadOnlyDictionary<string, AnalyticsMetricProvenanceDto>(result);
    }

    public static IReadOnlyList<AnalyticsMetricEvidenceCoverageSpec> ForSurface(string surface) =>
        Manifest.Where(spec => string.Equals(spec.Surface, surface, StringComparison.Ordinal)).ToArray();

    private static AnalyticsMetricProvenanceDto BuildEntry(
        AnalyticsMetricProvenanceDto? existing,
        AnalyticsMetricEvidenceCoverageSpec? spec,
        AnalyticsResponseMetaDto meta)
    {
        var isUnavailable = !meta.Success || meta.Context?.Fingerprint is null;
        var coverage = isUnavailable
            ? CoverageUnavailable
            : meta.EmptyReason is not null
                ? CoverageEmpty
                : meta.IsPartial
                    ? CoveragePartial
                    : CoverageComplete;
        var actionability = existing?.Actionability ?? spec?.DefaultActionability ?? AnalyticsMetricActionability.Unknown;
        if (isUnavailable || meta.IsPartial || meta.EmptyReason is not null || IsInsufficient(meta.DataQualityStatus))
        {
            actionability = actionability == AnalyticsMetricActionability.Actionable
                ? AnalyticsMetricActionability.Blocked
                : actionability;
        }

        var limitation = spec?.Limitation ?? "Metric evidence is not registered for this surface.";
        if (isUnavailable)
        {
            limitation = $"{limitation} Context fingerprint or successful source evidence is unavailable; do not compare or act.";
        }
        else if (coverage is CoveragePartial or CoverageEmpty)
        {
            limitation = $"{limitation} Runtime result is {coverage}; preserve the qualified state.";
        }

        return new AnalyticsMetricProvenanceDto
        {
            Kind = isUnavailable
                ? AnalyticsMetricProvenanceKinds.Unknown
                : existing?.Kind ?? spec?.DefaultKind ?? AnalyticsMetricProvenanceKinds.Unknown,
            Authority = isUnavailable
                ? AnalyticsMetricAuthority.Unknown
                : existing?.Authority ?? spec?.DefaultAuthority ?? AnalyticsMetricAuthority.Unknown,
            Actionability = actionability,
            Unit = existing?.Unit ?? spec?.Unit,
            Denominator = existing?.Denominator ?? spec?.Denominator,
            ContextFingerprint = meta.Context?.Fingerprint,
            FormulaVersion = spec?.FormulaVersion,
            Coverage = coverage,
            Limitation = limitation
        };
    }

    private static bool IsInsufficient(string? status) =>
        string.Equals(status, "critical", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "insufficient_data", StringComparison.OrdinalIgnoreCase);

    private static AnalyticsMetricEvidenceCoverageSpec Spec(
        string surface,
        string metricKey,
        string unit,
        string denominator,
        string formulaVersion,
        string limitation) =>
        new(
            surface,
            metricKey,
            AnalyticsMetricProvenanceKinds.AuthoritativeBackendAggregate,
            AnalyticsMetricAuthority.Authoritative,
            AnalyticsMetricActionability.Informational,
            unit,
            denominator,
            formulaVersion,
            "Analytics Reliability / Metric Evidence",
            limitation);
}
