import { makeUrl } from "./analyticsApi";
import type { AnalyticsResponseMeta } from "../types/analytics";
import { validateAnalyticsResponse } from "../validation/analyticsResponseValidation";
import { decisionPulseResponseSchema } from "../validation/analyticsResponseSchemas";

export type DecisionPulseItem = {
  id: string;
  sourceType: string;
  sourceKey: string;
  title: string;
  whySummary: string;
  reasonCodes: string[];
  recommendationStatus: string;
  recommendationLabel: string;
  dataQualityStatus: string;
  inputFreshnessStatus: string;
  deepLink: string;
  generatedAtUtc: string | null;
  tenantScope: string;
};

export type DecisionPulseResponse = {
  generatedAtUtc: string;
  periodFromUtc: string | null;
  periodToUtc: string | null;
  tenantScope: string;
  suppressedCount: number;
  items: DecisionPulseItem[];
  meta: AnalyticsResponseMeta;
};

export async function getDecisionPulse(options?: {
  fromDate?: string;
  toDate?: string;
  storeId?: number;
  supplierId?: number;
  dataScope?: string;
}): Promise<DecisionPulseResponse> {
  const params = new URLSearchParams();
  if (options?.fromDate) params.set("fromDate", options.fromDate);
  if (options?.toDate) params.set("toDate", options.toDate);
  if (options?.storeId != null) params.set("storeId", String(options.storeId));
  if (options?.supplierId != null) params.set("supplierId", String(options.supplierId));
  if (options?.dataScope) params.set("dataScope", options.dataScope);

  const response = await fetch(makeUrl("/api/analytics/decision-pulse", params));
  if (!response.ok) {
    throw new Error(`Decision Pulse HTTP ${response.status}`);
  }

  return validateAnalyticsResponse(
    await response.json(),
    decisionPulseResponseSchema,
    "Decision Pulse",
  ) as DecisionPulseResponse;
}
