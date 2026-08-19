import { fmtPctFromRatio } from "./analyticsFormatters";
import type {
  AnalyticsActionOutcomeSummaryResponse,
  RecommendationMeasurementStatistics,
} from "../types/analytics";

export const MEASUREMENT_RATE_UNAVAILABLE = "Nije dostupno";
export const MEASUREMENT_EMPTY_REASON_NO_ROWS = "no_rows";

export const MEASUREMENT_WARNING_LABELS: Record<string, string> = {
  small_sample: "Premali uzorak izdatih preporuka",
  small_measured_sample: "Premalo izmerenih ishoda",
  outcome_coverage_low: "Merenje ne pokriva dovoljno izvršenih akcija",
  rejected_actions_present: "U kohorti ima odbijenih akcija; to nije negativan ishod",
};

export type MeasurementStatisticsView =
  | { kind: "loading" }
  | {
      kind: "error";
      code: "load_failed" | "meta_unsuccessful" | "missing_statistics" | "projection_failed";
      message: string;
    }
  | { kind: "empty"; emptyReason: string }
  | { kind: "ready"; stats: RecommendationMeasurementStatistics };

export function formatMeasurementRate(rate: number | null | undefined): string {
  return fmtPctFromRatio(rate, 0, MEASUREMENT_RATE_UNAVAILABLE);
}

export function formatMeasurementWarning(code: string): string {
  return MEASUREMENT_WARNING_LABELS[code] ?? code;
}

export function csvCell(value: string | number | null | undefined): string {
  if (value == null) {
    return "";
  }

  const text = String(value);
  if (/[",\n\r]/.test(text)) {
    return `"${text.replaceAll("\"", "\"\"")}"`;
  }

  return text;
}

export function buildMeasurementStatisticsExportCsv(
  stats: RecommendationMeasurementStatistics,
): string {
  const headers = [
    "issuedCount",
    "acceptedCount",
    "rejectedCount",
    "ignoredCount",
    "executedCount",
    "measuredCount",
    "notMeasuredCount",
    "successCount",
    "neutralCount",
    "negativeCount",
    "pendingCount",
    "acceptanceRate",
    "rejectionRate",
    "ignoredRate",
    "executionRate",
    "measurementCoverageRate",
    "notMeasuredShare",
    "positiveOutcomeRate",
    "neutralOutcomeRate",
    "negativeOutcomeRate",
    "warningCodes",
    "emptyReason",
  ];

  const values = [
    stats.issuedCount,
    stats.acceptedCount,
    stats.rejectedCount,
    stats.ignoredCount,
    stats.executedCount,
    stats.measuredCount,
    stats.notMeasuredCount,
    stats.successCount,
    stats.neutralCount,
    stats.negativeCount,
    stats.pendingCount,
    stats.acceptanceRate,
    stats.rejectionRate,
    stats.ignoredRate,
    stats.executionRate,
    stats.measurementCoverageRate,
    stats.notMeasuredShare,
    stats.positiveOutcomeRate,
    stats.neutralOutcomeRate,
    stats.negativeOutcomeRate,
    stats.warningCodes.join("|"),
    stats.emptyReason ?? "",
  ];

  return `${headers.join(",")}\n${values.map(csvCell).join(",")}\n`;
}

export function canExportMeasurementStatistics(
  view: MeasurementStatisticsView,
): view is { kind: "ready"; stats: RecommendationMeasurementStatistics } {
  return view.kind === "ready";
}

export function downloadMeasurementStatisticsCsv(
  filename: string,
  csv: string,
): void {
  const trimmed = csv.trim();
  if (!trimmed) {
    throw new Error("Izvoz statistike merenja trenutno nije dostupan.");
  }

  const blob = new Blob([`\uFEFF${csv}`], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

export function resolveMeasurementStatisticsView(input: {
  loading: boolean;
  loadError?: string | null;
  summary: AnalyticsActionOutcomeSummaryResponse | null;
}): MeasurementStatisticsView {
  if (input.loading) {
    return { kind: "loading" };
  }

  if (input.loadError) {
    return {
      kind: "error",
      code: "load_failed",
      message: input.loadError,
    };
  }

  if (!input.summary) {
    return {
      kind: "error",
      code: "load_failed",
      message: "Sažetak merenja trenutno nije dostupan.",
    };
  }

  if (input.summary.meta.success === false) {
    return {
      kind: "error",
      code: "meta_unsuccessful",
      message: "Projekcija statistike merenja nije uspela. Stope nisu prikazane.",
    };
  }

  const stats = input.summary.measurementStatistics;
  if (!stats) {
    return {
      kind: "error",
      code: "missing_statistics",
      message: "Polje measurementStatistics nedostaje. Stope se ne izračunavaju iz totals.",
    };
  }

  if (stats.success === false) {
    return {
      kind: "error",
      code: "projection_failed",
      message: "Projekcija statistike merenja nije uspela. Stope nisu prikazane.",
    };
  }

  if (stats.emptyReason === MEASUREMENT_EMPTY_REASON_NO_ROWS) {
    return { kind: "empty", emptyReason: stats.emptyReason };
  }

  return { kind: "ready", stats };
}
