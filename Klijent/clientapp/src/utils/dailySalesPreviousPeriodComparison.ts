import type { DailySalesRow, DailySalesTableResponse } from "../services/dailySalesStatsApi";
import { fmtSignedPct } from "./analyticsFormatters";
import type { PreviousPeriodComparisonState } from "./supplierPreviousPeriodComparison";

export type DailySalesPreviousPeriodResolution = {
  state: PreviousPeriodComparisonState;
  previousData: DailySalesTableResponse | null;
  warning: string | null;
  emptyBaselineNote: string | null;
};

export const DAILY_SALES_PREVIOUS_PERIOD_EMPTY_NOTE =
  "Prethodni uporedivi period nema dovoljno podataka za poređenje.";

export const DAILY_SALES_PREVIOUS_PERIOD_FAILURE_NOTE =
  "Uporedni prethodni period nije učitan. PoP kartice su privremeno nedostupne.";

function finiteOrNull(value: number | null | undefined): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function hasPreviousPeriodBaseline(rows: DailySalesRow[]): boolean {
  if (rows.length === 0) return false;
  return rows.some((row) => finiteOrNull(row.totalRevenue) != null || finiteOrNull(row.totalItemsSold) != null);
}

export function resolveDailySalesPreviousPeriodComparison(
  previousResult: PromiseSettledResult<DailySalesTableResponse>,
  formatError: (reason: unknown) => string,
): DailySalesPreviousPeriodResolution {
  if (previousResult.status === "rejected") {
    return {
      state: "failed",
      previousData: null,
      warning: formatError(previousResult.reason),
      emptyBaselineNote: null,
    };
  }

  const previousData = previousResult.value;
  if (!hasPreviousPeriodBaseline(previousData.dateRows ?? [])) {
    return {
      state: "empty",
      previousData,
      warning: null,
      emptyBaselineNote: DAILY_SALES_PREVIOUS_PERIOD_EMPTY_NOTE,
    };
  }

  return {
    state: "available",
    previousData,
    warning: null,
    emptyBaselineNote: null,
  };
}

export function formatDailySalesComparisonDelta(
  deltaPct: number | null,
  currentValue: number | null,
  previousValue: number | null,
  previousPeriodState: PreviousPeriodComparisonState,
): string {
  if (previousPeriodState === "failed") {
    return "Nedostupno";
  }

  if (deltaPct == null) {
    if (finiteOrNull(previousValue) === 0 && (finiteOrNull(currentValue) ?? 0) > 0) {
      return "Nova baza";
    }
    return "N/A";
  }

  return fmtSignedPct(deltaPct, 1);
}
