import type { DailyAnalysis, ReorderPlan, SupplierScore } from "../services/insightStudioApi";
import type { SmartReorderResult } from "../services/insightStudioV2Api";
import { fmtPct, fmtRsd, fmtSignedPct } from "../utils/analyticsFormatters";

export function presentDailyAnalysisMetrics(data: DailyAnalysis) {
  const hasTargetEvidence = data.targetDataStatus !== "missing";
  const zScore = data.zScore;
  return {
    hasTargetEvidence,
    targetRevenueLabel: hasTargetEvidence ? fmtRsd(data.targetRevenue) : "N/D",
    targetUnitsLabel: hasTargetEvidence && data.targetUnits != null ? String(data.targetUnits) : "N/D",
    zScoreLabel: zScore != null && Number.isFinite(zScore) ? zScore.toFixed(2) : "N/D",
    outlierSummary: data.outlierLabel
      ?? (hasTargetEvidence ? "—" : "Nema podataka za ciljni dan"),
    showOutlierBadge: hasTargetEvidence && zScore != null && Number.isFinite(zScore),
  };
}

export function presentSupplierMarginBenchmark(
  supplier: Pick<SupplierScore, "marginPct" | "systemBenchmarkAvailable" | "systemMarginPct">,
) {
  if (supplier.systemBenchmarkAvailable === false) {
    return {
      marginLabel: fmtPct(supplier.marginPct),
      benchmarkNote: "Sistemski benchmark marže nije dostupan; profit skor ne koristi lažni 35% referent.",
    };
  }

  return {
    marginLabel: fmtPct(supplier.marginPct),
    benchmarkNote: supplier.systemMarginPct != null
      ? `Sistemska marža: ${fmtPct(supplier.systemMarginPct)}`
      : null,
  };
}

export function presentSignedInsightPercent(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) return "N/D";
  return value >= 0 ? `+${fmtPct(value)}` : fmtSignedPct(value);
}

export function presentReorderV1Summary(summary: ReorderPlan["summary"]) {
  const potentialRevenue = summary.potentialRevenueRsd ?? summary.totalReorderValue;
  return {
    potentialRevenueLabel: potentialRevenue == null || !Number.isFinite(potentialRevenue)
      ? "N/D"
      : fmtRsd(potentialRevenue),
    procurementCostLabel: summary.estimatedProcurementCostRsd != null
      ? fmtRsd(summary.estimatedProcurementCostRsd)
      : "N/D",
    costCoverageLabel: summary.costCoveragePct != null ? fmtPct(summary.costCoveragePct) : "N/D",
    basisNote: summary.reorderValueBasis === "potential_revenue_at_selling_price"
      ? "Vrednost nabavke prikazuje potencijalni prihod po prodajnoj ceni, ne novčani trošak nabavke."
      : null,
  };
}

export function presentSmartReorderProfit(summary: SmartReorderResult["summary"]) {
  const profitReliable = summary.expectedProfitFromReorder != null
    && Number.isFinite(summary.expectedProfitFromReorder);
  return {
    profitLabel: profitReliable ? fmtRsd(summary.expectedProfitFromReorder) : "N/D",
    profitNote: profitReliable
      ? null
      : "Profit nije pouzdan jer trošak ili cena nisu dostupni za sve preporučene količine.",
  };
}

export function isEstimatedCategoryRevenue(
  row: { estimated?: boolean; revenueBasis?: string },
): boolean {
  return row.estimated === true || row.revenueBasis === "estimated_velocity_price";
}
