import type { DailySalesNumeric, DailySalesRow, DailySalesShiftAssignmentStatus } from "../services/dailySalesStatsApi";

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

export function isTrustedShiftAssignment(
  status: DailySalesShiftAssignmentStatus | null | undefined,
): boolean {
  return status === "measured" || status === "partial";
}

export function classifyDailyShiftSummary(
  row: DailySalesRow,
  assignmentStatus?: DailySalesShiftAssignmentStatus | null,
): DailyShiftSummaryState {
  if (assignmentStatus === "no_time_fallback" || assignmentStatus === "unavailable") {
    return "unavailable";
  }

  const total = finiteOrNull(row.totalItemsSold);
  const firstAbsent = isShiftValueAbsent(row.firstShiftTotalItems);
  const secondAbsent = isShiftValueAbsent(row.secondShiftTotalItems);

  if (total == null) return "unavailable";

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
    return assignmentStatus === "partial" ? "partial" : "complete";
  }

  return "partial";
}

export function toDailyShiftEvidenceState(
  row: DailySalesRow,
  assignmentStatus?: DailySalesShiftAssignmentStatus | null,
): DailyShiftEvidenceState {
  const state = classifyDailyShiftSummary(row, assignmentStatus);
  if (state === "missing" || state === "unavailable") return "unavailable";
  if (state === "partial") return "partial";
  return "complete";
}

export function hasMissingShiftSummary(
  row: DailySalesRow,
  assignmentStatus?: DailySalesShiftAssignmentStatus | null,
): boolean {
  return classifyDailyShiftSummary(row, assignmentStatus) === "missing";
}

export function hasPartialShiftSummary(
  row: DailySalesRow,
  assignmentStatus?: DailySalesShiftAssignmentStatus | null,
): boolean {
  return classifyDailyShiftSummary(row, assignmentStatus) === "partial";
}

export function hasIncompleteShiftEvidence(
  row: DailySalesRow,
  assignmentStatus?: DailySalesShiftAssignmentStatus | null,
): boolean {
  const state = classifyDailyShiftSummary(row, assignmentStatus);
  return state === "partial" || state === "missing" || state === "unavailable";
}

export function resolveShiftDisplayValue(
  row: DailySalesRow,
  shift: "first" | "second",
  formatNumber: (value: DailySalesNumeric) => string,
  assignmentStatus?: DailySalesShiftAssignmentStatus | null,
): string {
  const state = classifyDailyShiftSummary(row, assignmentStatus);
  const value = shift === "first" ? row.firstShiftTotalItems : row.secondShiftTotalItems;

  if (state === "missing" || state === "unavailable") return formatNumber(null);
  if (state === "partial" && isShiftValueAbsent(value)) return formatNumber(null);
  return formatNumber(value);
}

export function resolveShiftExportValue(
  row: DailySalesRow,
  shift: "first" | "second",
  placeholder: string,
  assignmentStatus?: DailySalesShiftAssignmentStatus | null,
): string | number | null {
  const state = classifyDailyShiftSummary(row, assignmentStatus);
  const value = shift === "first" ? row.firstShiftTotalItems : row.secondShiftTotalItems;

  if (state === "missing" || state === "unavailable") return placeholder;
  if (state === "partial" && isShiftValueAbsent(value)) return placeholder;
  return finiteOrNull(value);
}

export function resolveShiftChartValue(
  row: DailySalesRow,
  shift: "first" | "second",
  assignmentStatus?: DailySalesShiftAssignmentStatus | null,
): number | null {
  const state = classifyDailyShiftSummary(row, assignmentStatus);
  const value = shift === "first" ? row.firstShiftTotalItems : row.secondShiftTotalItems;

  if (state === "missing" || state === "unavailable") return null;
  if (state === "partial" && isShiftValueAbsent(value)) return null;
  return finiteOrNull(value);
}

export function sumShiftColumn(
  rows: DailySalesRow[],
  key: "firstShiftTotalItems" | "secondShiftTotalItems",
  assignmentStatus?: DailySalesShiftAssignmentStatus | null,
): ShiftColumnAggregate {
  let total = 0;
  let hasValue = false;
  let isPartial = false;

  for (const row of rows) {
    const state = classifyDailyShiftSummary(row, assignmentStatus);
    if (state === "missing" || state === "unavailable") {
      isPartial = true;
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
  assignmentStatus?: DailySalesShiftAssignmentStatus | null,
): { value: number | null; state: DailyShiftEvidenceState } {
  if (rows.length === 0) return { value: null, state: "unavailable" };
  if (assignmentStatus === "no_time_fallback" || assignmentStatus === "unavailable") {
    return { value: null, state: "unavailable" };
  }

  const key = shift === "first" ? "firstShiftTotalItems" : "secondShiftTotalItems";
  const aggregate = sumShiftColumn(rows, key, assignmentStatus);

  if (aggregate.sum == null) {
    return { value: null, state: "unavailable" };
  }

  if (
    aggregate.isPartial
    || assignmentStatus === "partial"
    || rows.some((row) => classifyDailyShiftSummary(row, assignmentStatus) !== "complete")
  ) {
    return { value: aggregate.sum, state: "partial" };
  }

  return { value: aggregate.sum, state: "complete" };
}

export function periodHasIncompleteShiftEvidence(
  rows: DailySalesRow[],
  assignmentStatus?: DailySalesShiftAssignmentStatus | null,
): boolean {
  if (assignmentStatus === "no_time_fallback" || assignmentStatus === "unavailable") return true;
  return rows.some((row) => hasIncompleteShiftEvidence(row, assignmentStatus));
}
