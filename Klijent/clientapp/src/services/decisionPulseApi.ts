import { makeUrl } from "./analyticsApi";
import type { AnalyticsResponseMeta } from "../types/analytics";
import { validateAnalyticsResponse } from "../validation/analyticsResponseValidation";
import { decisionPulseResponseSchema } from "../validation/analyticsResponseSchemas";
import { ensureExportAdminKey } from "./exportApi";

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
  asOfUtc?: string | null;
  evidenceBasis?: string;
  expectedImpactRsd?: number | null;
  priorityEvidence?: string | null;
};

export type DecisionPulseResponse = {
  generatedAtUtc: string;
  periodFromUtc: string | null;
  periodToUtc: string | null;
  tenantScope: string;
  suppressedCount: number;
  items: DecisionPulseItem[];
  meta: AnalyticsResponseMeta;
  currentness?: "current" | "latest_known" | string;
  asOfUtc?: string | null;
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

export type DecisionPulseDisposition = "accepted" | "deferred" | "rejected" | "ignored";

export async function getDecisionPulseDispositions(
  items: DecisionPulseItem[],
): Promise<Record<string, DecisionPulseDisposition>> {
  if (items.length === 0) return {};
  const response = await fetch(makeUrl("/api/analytics/actions/status"), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      items: items.map((item) => ({ sourceType: item.sourceType, sourceKey: item.id })),
    }),
  });
  if (!response.ok) throw new Error(`Učitavanje odluka nije uspelo (HTTP ${response.status}).`);
  const result = await response.json() as {
    items?: Array<{ sourceKey: string; status?: string | null }>;
  };
  const allowed = new Set<DecisionPulseDisposition>(["accepted", "deferred", "rejected", "ignored"]);
  return Object.fromEntries((result.items ?? []).flatMap((entry) =>
    entry.status && allowed.has(entry.status as DecisionPulseDisposition)
      ? [[entry.sourceKey, entry.status as DecisionPulseDisposition]]
      : [],
  ));
}

export async function recordDecisionPulseDisposition(
  item: DecisionPulseItem,
  disposition: DecisionPulseDisposition,
  context?: { fromDate?: string; toDate?: string; storeId?: number; supplierId?: number; dataScope?: string },
): Promise<void> {
  const adminKey = ensureExportAdminKey("evidentiranje vlasničke odluke");
  if (!adminKey) throw new Error("Evidentiranje je otkazano.");

  const priority = item.priorityEvidence?.toUpperCase();
  const priorityValue = priority === "P1" || priority === "P2" || priority === "P3" ? priority : "UNRANKED";
  const upsertResponse = await fetch(makeUrl("/api/analytics/actions"), {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Admin-Key": adminKey },
    body: JSON.stringify({
      sourceType: item.sourceType,
      sourceKey: item.id,
      title: item.title,
      description: item.whySummary,
      recommendationStatus: item.recommendationStatus,
      priority: priorityValue,
      expectedImpactRsd: item.expectedImpactRsd ?? null,
      dataQualityStatus: item.dataQualityStatus,
      actionUrl: item.deepLink,
      sourceRecommendationId: item.id,
      recommendationType: item.recommendationStatus,
      decisionReason: item.whySummary,
      recommendedAction: item.recommendationLabel,
      generatedAtUtc: item.generatedAtUtc,
      inputFreshnessStatus: item.inputFreshnessStatus,
      metadataJson: JSON.stringify({
        source: "owner_decision_digest",
        fromDate: context?.fromDate,
        toDate: context?.toDate,
        asOfUtc: item.asOfUtc ?? null,
        evidenceBasis: item.evidenceBasis ?? "source_latest_known",
        disposition,
        storeId: context?.storeId,
        supplierId: context?.supplierId,
        dataScope: context?.dataScope,
        priorityEvidence: item.priorityEvidence ?? null,
      }),
      explainabilityText: item.whySummary,
      reasonCodes: item.reasonCodes,
    }),
  });
  if (!upsertResponse.ok) throw new Error(`Evidentiranje nije uspelo (HTTP ${upsertResponse.status}).`);
  const upserted = await upsertResponse.json() as { item?: { id?: number } };
  const id = upserted.item?.id;
  if (!id) throw new Error("Evidentiranje nije vratilo identifikator akcije.");

  const statusResponse = await fetch(makeUrl(`/api/analytics/actions/${id}/status`), {
    method: "PATCH",
    headers: { "Content-Type": "application/json", "X-Admin-Key": adminKey },
    body: JSON.stringify({
      status: disposition,
      note: `Vlasnička odluka evidentirana kroz pregled odluka: ${disposition}. Ishod se ovim ne meri.`,
    }),
  });
  if (!statusResponse.ok) throw new Error(`Evidentiranje statusa nije uspelo (HTTP ${statusResponse.status}).`);
}
