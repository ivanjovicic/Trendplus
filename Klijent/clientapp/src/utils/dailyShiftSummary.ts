import type { DailySalesNumeric, DailySalesRow } from "../services/dailySalesStatsApi";

export type DailyShiftSummaryState = "complete" | "partial" | "missing" | "unavailable";

export type DailyShiftEvidenceState = "complete" | "partial" | "unavailable";

export type ShiftColumnAggregate = {
  sum: number | null;
  isPartial: boolean;
};

function finiteOrNull(value: DailySalesNumeric): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function isShiftValueAbsent(value: DailySalesNumeric): boolean {
  return finiteOrNull(value) === null;
}

export function classifyDailyShiftSummary(row: DailySalesRow): DailyShiftSummaryState {
  const total = finiteOrNull(row.totalItemsSold);
  const firstAbsent = isShiftValueAbsent(row.firstShiftTotalItems);
  const secondAbsent = isShiftValueAbsent(row.secondShiftTotalItems);

  if (total == null || total < 0) return "unavailable";

  if (total === 0) {
    if (firstAbsent && secondAbsent) return "unavailable";
    if (!firstAbsent && !secondAbsent) return "complete";
    return "partial";
  }

  if (firstAbsent && secondAbsent) return "missing";

  if (!firstAbsent && !secondAbsent) {
    const first = finiteOrNull(row.firstShiftTotalItems);
    const second = finiteOrNull(row.secondShiftTotalItems);
    if (first === 0 && second === 0) return "missing";
    return "complete";
  }

  return "partial";
}

export function toDailyShiftEvidenceState(row: DailySalesRow): DailyShiftEvidenceState {
  const state = classifyDailyShiftSummary(row);
  if (state === "missing" || state === "unavailable") return "unavailable";
  if (state === "partial") return "partial";
  return "complete";
}

export function hasMissingShiftSummary(row: DailySalesRow): boolean {
  return classifyDailyShiftSummary(row) === "missing";
}

export function hasPartialShiftSummary(row: DailySalesRow): boolean {
  return classifyDailyShiftSummary(row) === "partial";
}

export function hasIncompleteShiftEvidence(row: DailySalesRow): boolean {
  const state = classifyDailyShiftSummary(row);
  return state === "partial" || state === "missing";
}

export function resolveShiftDisplayValue(
  row: DailySalesRow,
  shift: "first" | "second",
  formatNumber: (value: DailySalesNumeric) => string,
): string {
  const state = classifyDailyShiftSummary(row);
  const value = shift === "first" ? row.firstShiftTotalItems : row.secondShiftTotalItems;

  if (state === "missing" || state === "unavailable") return formatNumber(null);
  if (state === "partial" && isShiftValueAbsent(value)) return formatNumber(null);
  return formatNumber(value);
}

export function resolveShiftExportValue(
  row: DailySalesRow,
  shift: "first" | "second",
  placeholder: string,
): string | number | null {
  const state = classifyDailyShiftSummary(row);
  const value = shift === "first" ? row.firstShiftTotalItems : row.secondShiftTotalItems;

  if (state === "missing" || state === "unavailable") return placeholder;
  if (state === "partial" && isShiftValueAbsent(value)) return placeholder;
  return finiteOrNull(value);
}

export function resolveShiftChartValue(
  row: DailySalesRow,
  shift: "first" | "second",
): number | null {
  const state = classifyDailyShiftSummary(row);
  const value = shift === "first" ? row.firstShiftTotalItems : row.secondShiftTotalItems;

  if (state === "missing" || state === "unavailable") return null;
  if (state === "partial" && isShiftValueAbsent(value)) return null;
  return finiteOrNull(value);
}

export function sumShiftColumn(
  rows: DailySalesRow[],
  key: "firstShiftTotalItems" | "secondShiftTotalItems",
): ShiftColumnAggregate {
  let total = 0;
  let hasValue = false;
  let isPartial = false;

  for (const row of rows) {
    const state = classifyDailyShiftSummary(row);
    if (state === "missing" || state === "unavailable") {
      if (state === "missing") isPartial = true;
      continue;
    }

    const value = finiteOrNull(row[key]);
    if (value != null) {
      total += value;
      hasValue = true;
      continue;
    }

    if (state === "partial") {
      isPartial = true;
    }
  }

  return {
    sum: hasValue ? total : null,
    isPartial,
  };
}

export function summarizeShiftItems(
  rows: DailySalesRow[],
  shift: "first" | "second",
): { value: number | null; state: DailyShiftEvidenceState } {
  if (rows.length === 0) return { value: null, state: "unavailable" };

  const key = shift === "first" ? "firstShiftTotalItems" : "secondShiftTotalItems";
  const aggregate = sumShiftColumn(rows, key);

  if (aggregate.sum == null) {
    return { value: null, state: "unavailable" };
  }

  if (aggregate.isPartial || rows.some((row) => classifyDailyShiftSummary(row) !== "complete")) {
    return { value: aggregate.sum, state: "partial" };
  }

  return { value: aggregate.sum, state: "complete" };
}

export function periodHasIncompleteShiftEvidence(rows: DailySalesRow[]): boolean {
  return rows.some((row) => hasIncompleteShiftEvidence(row));
}
