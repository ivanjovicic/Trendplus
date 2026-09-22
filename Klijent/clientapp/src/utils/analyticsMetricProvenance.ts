import type {
  AnalyticsCriticalMetricKey,
  AnalyticsMetricActionability,
  AnalyticsMetricAuthority,
  AnalyticsMetricProvenance,
  AnalyticsMetricProvenanceKind,
  AnalyticsResponseMeta,
} from "../types/analytics";

const PROVENANCE_KINDS = new Set<AnalyticsMetricProvenanceKind>([
  "authoritative_backend_aggregate",
  "observed_row_value",
  "frontend_display_derivation",
  "modeled_estimated",
  "unknown",
]);

const AUTHORITIES = new Set<AnalyticsMetricAuthority>([
  "authoritative",
  "observed",
  "derived",
  "modeled",
  "unknown",
]);

const ACTIONABILITIES = new Set<AnalyticsMetricActionability>([
  "actionable",
  "informational",
  "blocked",
  "unknown",
]);

function isStringValue<T extends string>(value: unknown, allowed: Set<T>): value is T {
  return typeof value === "string" && allowed.has(value as T);
}

function normalizeOptionalText(value: unknown): string | null {
  return typeof value === "string" && value.trim() ? value.trim() : null;
}

/**
 * Returns only explicitly known provenance. Invalid or missing provenance is
 * deliberately treated as unavailable rather than trusted or zero-valued.
 */
export function readAnalyticsMetricProvenance(
  meta: Pick<AnalyticsResponseMeta, "metricProvenance"> | null | undefined,
  metric: AnalyticsCriticalMetricKey,
): AnalyticsMetricProvenance | null {
  const candidate = meta?.metricProvenance?.[metric];
  if (!candidate || typeof candidate !== "object") {
    return null;
  }

  if (
    !isStringValue(candidate.kind, PROVENANCE_KINDS)
    || !isStringValue(candidate.authority, AUTHORITIES)
    || !isStringValue(candidate.actionability, ACTIONABILITIES)
  ) {
    return null;
  }

  if (
    candidate.kind === "authoritative_backend_aggregate"
    && candidate.authority !== "authoritative"
  ) {
    return null;
  }

  if (
    candidate.kind === "frontend_display_derivation"
    && candidate.authority === "authoritative"
  ) {
    return null;
  }

  return {
    kind: candidate.kind,
    authority: candidate.authority,
    actionability: candidate.actionability,
    unit: normalizeOptionalText(candidate.unit),
    denominator: normalizeOptionalText(candidate.denominator),
  };
}

export function isAuthoritativeAnalyticsMetric(
  meta: Pick<AnalyticsResponseMeta, "metricProvenance"> | null | undefined,
  metric: AnalyticsCriticalMetricKey,
): boolean {
  return readAnalyticsMetricProvenance(meta, metric)?.kind === "authoritative_backend_aggregate";
}
