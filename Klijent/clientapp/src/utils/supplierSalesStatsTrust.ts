import type { AnalyticsResponseMeta } from "../types/analytics";
import { getAnalyticsDataFreshnessStatus } from "./analyticsResponseMeta";

export type SupplierSalesStatsTrustProjection = {
  lastRefreshAt: string | null;
  dataFreshnessStatus: "fresh" | "stale" | "critical" | "unknown";
  isPartial: boolean;
};

function parseAuthoritativeRefreshAt(
  meta?: Pick<AnalyticsResponseMeta, "success" | "lastRefreshAtUtc"> | null,
): string | null {
  if (meta?.success !== true) return null;
  const lastRefreshAtUtc = typeof meta.lastRefreshAtUtc === "string" ? meta.lastRefreshAtUtc.trim() : "";
  if (!lastRefreshAtUtc || !Number.isFinite(Date.parse(lastRefreshAtUtc))) return null;
  return lastRefreshAtUtc;
}

export function buildSupplierSalesStatsTrustProjection(
  meta?: Pick<AnalyticsResponseMeta, "success" | "emptyReason" | "isPartial" | "lastRefreshAtUtc"> | null,
): SupplierSalesStatsTrustProjection {
  return {
    lastRefreshAt: parseAuthoritativeRefreshAt(meta),
    dataFreshnessStatus: getAnalyticsDataFreshnessStatus(meta),
    isPartial: meta?.success === true && meta.isPartial === true,
  };
}
