import type { VendorSalesNivelacijaResponse } from "../services/vendorSalesNivelacijaApi";
import { comparablePrePostMetric } from "./prePostNivelacijaTrust";

export type PreviousPeriodComparisonState = "available" | "empty" | "failed";

export type PreviousPeriodComparisonResolution = {
  state: PreviousPeriodComparisonState;
  previousRevenue: number | null;
  warning: string | null;
  emptyBaselineNote: string | null;
};

export const SUPPLIER_PREVIOUS_PERIOD_EMPTY_NOTE =
  "Prethodni uporedivi period nema dovoljno podataka za poređenje.";

export const SUPPLIER_PREVIOUS_PERIOD_FAILURE_NOTE =
  "Uporedni prethodni period nije učitan. Trend i rast/pad su privremeno nedostupni.";

export function resolvePreviousPeriodComparison(
  previousResult: PromiseSettledResult<VendorSalesNivelacijaResponse>,
  formatError: (reason: unknown) => string,
): PreviousPeriodComparisonResolution {
  if (previousResult.status === "rejected") {
    return {
      state: "failed",
      previousRevenue: null,
      warning: formatError(previousResult.reason),
      emptyBaselineNote: null,
    };
  }

  const previousRevenue = comparablePrePostMetric(
    previousResult.value.totals.postRevenue,
    { hasComparableSalesWindow: previousResult.value.totals.hasComparableSalesWindow },
  );

  if (previousRevenue == null) {
    return {
      state: "empty",
      previousRevenue: null,
      warning: null,
      emptyBaselineNote: SUPPLIER_PREVIOUS_PERIOD_EMPTY_NOTE,
    };
  }

  return {
    state: "available",
    previousRevenue,
    warning: null,
    emptyBaselineNote: null,
  };
}

export function resolveSupplierPeriodGrowthPct(args: {
  previousPeriodState: PreviousPeriodComparisonState;
  previousRevenue: number | null;
  totalRevenue: number | null;
}): number | null {
  if (args.previousPeriodState !== "available") {
    return null;
  }

  if (args.previousRevenue == null || args.previousRevenue <= 0 || args.totalRevenue == null) {
    return null;
  }

  return ((args.totalRevenue - args.previousRevenue) / args.previousRevenue) * 100;
}
