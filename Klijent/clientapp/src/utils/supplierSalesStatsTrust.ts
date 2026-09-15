import type { AnalyticsResponseMeta } from "../types/analytics";
import { getAnalyticsDataFreshnessStatus } from "./analyticsResponseMeta";

export type SupplierSalesStatsTrustProjection = {
  lastRefreshAt: string | null;
  dataFreshnessStatus: "fresh" | "stale" | "critical" | "unknown";
  isPartial: boolean;
};

export function buildSupplierSalesStatsTrustProjection(
  meta?: Pick<AnalyticsResponseMeta, "success" | "emptyReason" | "isPartial" | "lastRefreshAtUtc"> | null,
): SupplierSalesStatsTrustProjection {
  const lastRefreshAt = typeof meta?.lastRefreshAtUtc === "string" && meta.lastRefreshAtUtc.trim()
    ? meta.lastRefreshAtUtc
    : null;

  return {
    lastRefreshAt,
    dataFreshnessStatus: getAnalyticsDataFreshnessStatus(meta),
    isPartial: meta?.isPartial === true,
  };
}
