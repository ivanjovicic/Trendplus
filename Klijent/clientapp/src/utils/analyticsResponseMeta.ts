import type { AnalyticsResponseMeta } from "../types/analytics";

import {
  ANALYTICS_ERROR_FALLBACK_MESSAGE,
  getSafeAnalyticsErrorMessage,
} from "./analyticsErrorMessages";

const EMPTY_REASON_MESSAGES: Record<string, string> = {
  no_data_in_period: "Nema podataka za izabrani period.",
  insufficient_data: "Nema dovoljno podataka za pouzdanu analizu.",
  no_open_issues: "Nema otvorenih problema za izabrane filtere.",
  no_top_offenders: "Nema top offender zapisa za izabrani tip problema.",
  no_sales_in_period: "Nema prodaje u izabranom periodu.",
  no_import: "Nema import batch-a za izabrani period.",
  no_intake_evidence: "Nema dovoljno učitanih artikala ili import redova za readiness procenu.",
};

export function getAnalyticsDataFreshnessStatus(
  meta?: Pick<AnalyticsResponseMeta, "success" | "emptyReason" | "isPartial" | "lastRefreshAtUtc"> | null,
): "fresh" | "stale" | "critical" | "unknown" {
  if (!meta || meta.success !== true) {
    return "unknown";
  }

  if (typeof meta.emptyReason === "string" && meta.emptyReason.trim()) {
    return "unknown";
  }

  if (meta.isPartial === true) {
    return "stale";
  }

  const lastRefreshAtUtc = typeof meta.lastRefreshAtUtc === "string" ? meta.lastRefreshAtUtc.trim() : "";
  return lastRefreshAtUtc && Number.isFinite(Date.parse(lastRefreshAtUtc)) ? "fresh" : "unknown";
}

export function getAnalyticsEmptyReasonMessage(emptyReason?: string | null): string | null {
  if (typeof emptyReason !== "string") {
    return null;
  }

  const normalized = emptyReason.trim();
  if (!normalized) {
    return "Nije specificirano";
  }

  const key = normalized.toLowerCase().replace(/[\s-]+/g, "_");
  return EMPTY_REASON_MESSAGES[key] ?? "Nema podataka za izabrani opseg.";
}

export class AnalyticsMetaError extends Error {
  readonly errorCode?: string | null;
  readonly correlationId?: string | null;
  readonly context?: string | null;
  readonly meta?: AnalyticsResponseMeta | null;

  constructor(
    message: string,
    options?: {
      errorCode?: string | null;
      correlationId?: string | null;
      context?: string | null;
      meta?: AnalyticsResponseMeta | null;
    }
  ) {
    super(message);
    this.name = "AnalyticsMetaError";
    this.errorCode = options?.errorCode ?? null;
    this.correlationId = options?.correlationId ?? null;
    this.context = options?.context ?? null;
    this.meta = options?.meta ?? null;
  }
}

export function isAnalyticsMetaError(meta?: AnalyticsResponseMeta | null): boolean {
  if (!meta) return false;
  return meta.success === false || Boolean(meta.errorCode);
}

export function isAnalyticsMetaWarning(meta?: AnalyticsResponseMeta | null): boolean {
  if (!meta) return false;
  return Boolean(meta.warningCode) || meta.isPartial === true;
}

export function isAnalyticsMetaEmpty(meta?: AnalyticsResponseMeta | null): boolean {
  if (!meta) return false;
  if (meta.success !== true) return false;
  return Boolean(meta.emptyReason);
}

export function hasAnalyticsMetaEmptyReason(meta?: AnalyticsResponseMeta | null): boolean {
  if (!meta) return false;
  if (meta.success !== true) return false;
  return Boolean(meta.emptyReason);
}

export function isAnalyticsMetaInsufficient(meta?: AnalyticsResponseMeta | null): boolean {
  if (!meta) return false;
  if (meta.success !== true) return false;
  return meta.dataQualityStatus === "insufficient_data";
}

export function shouldShowAnalyticsEmptyState(
  meta: AnalyticsResponseMeta | null | undefined,
  rowCount?: number | null,
  options?: {
    allowEmptyReasonWithRows?: boolean;
  }
): boolean {
  const allowEmptyReasonWithRows = options?.allowEmptyReasonWithRows ?? false;
  if (isAnalyticsMetaError(meta)) return false;
  if (!meta || meta.success !== true) return false;
  if (hasAnalyticsMetaEmptyReason(meta)) {
    if (rowCount == null) return true;
    if (rowCount === 0) return true;
    return allowEmptyReasonWithRows;
  }
  return rowCount === 0 && meta.dataQualityStatus === "insufficient_data";
}

export function getAnalyticsMetaMessage(meta?: AnalyticsResponseMeta | null): string | null {
  if (!meta) return null;

  const errorMessage = typeof meta.errorMessage === "string" ? meta.errorMessage.trim() : null;
  if (errorMessage) {
    return getSafeAnalyticsErrorMessage(errorMessage, meta.errorCode);
  }

  const warningMessage = typeof meta.warningMessage === "string" ? meta.warningMessage.trim() : null;
  if (warningMessage) {
    return getSafeAnalyticsErrorMessage(warningMessage, meta.warningCode);
  }

  const message = typeof meta.message === "string" ? meta.message.trim() : null;
  if (message) {
    return getSafeAnalyticsErrorMessage(message, meta.errorCode ?? meta.warningCode);
  }

  const emptyReason = typeof meta.emptyReason === "string" ? meta.emptyReason.trim() : null;
  if (emptyReason) {
    return getAnalyticsEmptyReasonMessage(emptyReason);
  }

  return null;
}

export function assertAnalyticsMetaSuccess<T>(
  response: T,
  getMeta: (response: T) => AnalyticsResponseMeta | undefined | null,
  context: string
): T {
  const meta = getMeta(response);
  if (!isAnalyticsMetaError(meta)) {
    return response;
  }

  const detail = getSafeAnalyticsErrorMessage(
    getAnalyticsMetaMessage(meta),
    meta?.errorCode,
    ANALYTICS_ERROR_FALLBACK_MESSAGE,
  );
  const suffixParts: string[] = [];
  if (meta?.correlationId) {
    suffixParts.push(`correlation: ${meta.correlationId}`);
  }
  const suffix = suffixParts.length > 0 ? ` (${suffixParts.join(", ")})` : "";
  throw new AnalyticsMetaError(`${detail}${suffix}`, {
    errorCode: meta?.errorCode ?? null,
    correlationId: meta?.correlationId ?? null,
    context,
    meta,
  });
}

