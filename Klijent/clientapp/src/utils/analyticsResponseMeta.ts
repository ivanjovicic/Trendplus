import type { AnalyticsResponseMeta } from "../types/analytics";

const EMPTY_REASON_MESSAGES: Record<string, string> = {
  no_data_in_period: "Nema podataka za izabrani period.",
  insufficient_data: "Nema dovoljno podataka za pouzdanu analizu.",
  no_open_issues: "Nema otvorenih problema za izabrane filtere.",
  no_top_offenders: "Nema top offender zapisa za izabrani tip problema.",
  no_sales_in_period: "Nema prodaje u izabranom periodu.",
  no_import: "Nema import batch-a za izabrani period.",
};

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

export function isAnalyticsMetaInsufficient(meta?: AnalyticsResponseMeta | null): boolean {
  if (!meta) return false;
  if (meta.success !== true) return false;
  return meta.dataQualityStatus === "insufficient_data";
}

export function shouldShowAnalyticsEmptyState(
  meta: AnalyticsResponseMeta | null | undefined,
  rowCount?: number | null
): boolean {
  if (isAnalyticsMetaError(meta)) return false;
  if (!meta || meta.success !== true) return false;
  if (meta.emptyReason) return true;
  return rowCount === 0 && meta.dataQualityStatus === "insufficient_data";
}

export function getAnalyticsMetaMessage(meta?: AnalyticsResponseMeta | null): string | null {
  if (!meta) return null;

  const errorMessage = meta.errorMessage?.trim();
  if (errorMessage) return errorMessage;

  const warningMessage = meta.warningMessage?.trim();
  if (warningMessage) return warningMessage;

  const message = meta.message?.trim();
  if (message) return message;

  const emptyReason = meta.emptyReason?.trim();
  if (emptyReason) {
    return EMPTY_REASON_MESSAGES[emptyReason] ?? "Nema podataka za izabrani opseg.";
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

  const detail = getAnalyticsMetaMessage(meta) || "Podaci trenutno nisu dostupni.";
  const suffixParts: string[] = [];
  if (meta?.errorCode) {
    suffixParts.push(`sifra: ${meta.errorCode}`);
  }
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
