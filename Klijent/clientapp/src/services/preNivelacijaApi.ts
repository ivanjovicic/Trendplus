import type { PreNivelacijaPriorityResponse } from "../types/preNivelacija";
import { getSafeAnalyticsErrorMessage } from "../utils/analyticsErrorMessages";
import { assertAnalyticsMetaSuccess } from "../utils/analyticsResponseMeta";
import { normalizeDataScope } from "../utils/dataScope";

import { makeUrl } from "./analyticsApi";

export interface PreNivelacijaQuery {
  supplierId?: number;
  seasonId?: number;
  footwearTypeId?: number;
  stockMin?: number;
  stockMax?: number;
  noSaleDaysMin?: number;
  minScore?: number;
  marginFloor?: number;
  page?: number;
  pageSize?: number;
  dataScope?: string | null;
}

const PRE_NIVELACIJA_ERROR_FALLBACK =
  "Pre-nivelacija prioriteti trenutno nisu dostupni. Proverite status osvežavanja i pokušajte ponovo.";

export class PreNivelacijaApiError extends Error {
  readonly errorCode: string | null;
  readonly correlationId: string | null;

  constructor(message: string, errorCode?: string | null, correlationId?: string | null) {
    super(message);
    this.name = "PreNivelacijaApiError";
    this.errorCode = errorCode ?? null;
    this.correlationId = correlationId ?? null;
  }
}

type ErrorPayload = {
  detail?: unknown;
  title?: unknown;
  message?: unknown;
  error?: unknown;
  errorCode?: unknown;
  correlationId?: unknown;
};

function asText(value: unknown): string | null {
  return typeof value === "string" && value.trim() ? value.trim() : null;
}

function parseErrorBody(body: string, contentType: string): ErrorPayload | null {
  if (!body || !contentType.toLocaleLowerCase().includes("json")) return null;
  try {
    const parsed = JSON.parse(body) as unknown;
    return parsed && typeof parsed === "object" ? parsed as ErrorPayload : null;
  } catch {
    return null;
  }
}

function toSafeFetchError(body: string, contentType: string, status: number): PreNivelacijaApiError {
  const payload = parseErrorBody(body, contentType);
  const errorCode = asText(payload?.errorCode);
  const correlationId = asText(payload?.correlationId);
  const candidate = asText(payload?.detail) ?? asText(payload?.message) ?? asText(payload?.title) ?? asText(payload?.error) ?? body;
  const isHtml = contentType.toLocaleLowerCase().includes("text/html") || /<[^>]+>/.test(candidate);
  const message = isHtml
    ? PRE_NIVELACIJA_ERROR_FALLBACK
    : getSafeAnalyticsErrorMessage(candidate, errorCode, PRE_NIVELACIJA_ERROR_FALLBACK);
  return new PreNivelacijaApiError(message || `${PRE_NIVELACIJA_ERROR_FALLBACK} (HTTP ${status})`, errorCode, correlationId);
}

export async function getPreNivelacijaPrioriteti(query: PreNivelacijaQuery): Promise<PreNivelacijaPriorityResponse> {
  const params = new URLSearchParams();
  if (query.supplierId != null) params.set("supplierId", String(query.supplierId));
  if (query.seasonId != null) params.set("seasonId", String(query.seasonId));
  if (query.footwearTypeId != null) params.set("footwearTypeId", String(query.footwearTypeId));
  if (query.stockMin != null) params.set("stockMin", String(query.stockMin));
  if (query.stockMax != null) params.set("stockMax", String(query.stockMax));
  if (query.noSaleDaysMin != null) params.set("noSaleDaysMin", String(query.noSaleDaysMin));
  if (query.minScore != null) params.set("minScore", String(query.minScore));
  if (query.marginFloor != null) params.set("marginFloor", String(query.marginFloor));
  params.set("page", String(query.page ?? 1));
  params.set("pageSize", String(query.pageSize ?? 20));
  if (query.dataScope != null && query.dataScope !== "") {
    params.set("dataScope", normalizeDataScope(query.dataScope));
  }

  let res: Response;
  try {
    res = await fetch(makeUrl(`/api/analytics/pre-nivelacija-prioriteti`, params));
  } catch {
    throw new PreNivelacijaApiError(PRE_NIVELACIJA_ERROR_FALLBACK);
  }
  if (!res.ok) {
    const contentType = res.headers.get("content-type") ?? "";
    const body = await res.text().catch(() => "");
    throw toSafeFetchError(body.trim(), contentType, res.status);
  }

  const payload = (await res.json()) as PreNivelacijaPriorityResponse;
  return assertAnalyticsMetaSuccess(
    payload,
    (response) => response.meta,
    "Pre-nivelacija prioriteti trenutno nisu dostupni."
  );
}
