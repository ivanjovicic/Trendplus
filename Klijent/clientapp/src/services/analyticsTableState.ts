import type {
  AnalyticsDetailField,
  AnalyticsDetailResponse,
  AnalyticsDataType,
  AnalyticsNamedValue,
  AnalyticsScalar,
  AnalyticsTableColumn,
  ResolvedAnalyticsTablePayload,
} from "../types/analyticsTable";
import { fmtNumber, fmtPct, fmtRsd, formatDate, formatDateTime } from "../utils/analyticsFormatters";

const BROWSER_PREVIEW_PREFIX = "analytics-preview:";
const DETAIL_PREFIX = "analytics-detail:";
/** Browser-stored print/report preview TTL (10 minutes). Never use as durable report storage. */
export const ANALYTICS_BROWSER_PREVIEW_TTL_MS = 10 * 60 * 1000;
/** @deprecated Use ANALYTICS_BROWSER_PREVIEW_TTL_MS. */
export const ANALYTICS_PRINT_TTL_MS = ANALYTICS_BROWSER_PREVIEW_TTL_MS;
const PRINT_TTL_MS = ANALYTICS_BROWSER_PREVIEW_TTL_MS;

type StoredPrintPayload = {
  savedAtUtc: string;
  payload: ResolvedAnalyticsTablePayload;
};

export type BrowserPreviewSnapshot = {
  payload: ResolvedAnalyticsTablePayload;
  savedAtUtc: string;
  expiresAtUtc: string;
  ttlMs: number;
  ageMs: number;
};

const NON_FINITE_TOKEN = /^(?:[+-]?infinity|nan)$/i;

function isNumericDataType(dataType?: AnalyticsDataType | string): boolean {
  return dataType === "number" || dataType === "currency" || dataType === "percent";
}

function normalizeAnalyticsScalar(value: AnalyticsScalar, dataType?: AnalyticsDataType | string): AnalyticsScalar {
  if (value == null) return null;
  if (typeof value === "number") return Number.isFinite(value) ? value : null;
  if (typeof value === "string" && NON_FINITE_TOKEN.test(value.trim())) return null;
  if (isNumericDataType(dataType)) return toFiniteNumber(value);
  return value;
}

function stringifyValue(value: AnalyticsScalar, fallback = "N/A"): string {
  if (value == null) return fallback;
  if (typeof value === "boolean") return value ? "Da" : "Ne";
  if (typeof value === "number" && !Number.isFinite(value)) return fallback;
  if (typeof value === "string" && NON_FINITE_TOKEN.test(value.trim())) return fallback;
  return String(value);
}

function toFiniteNumber(value: AnalyticsScalar): number | null {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === "string" && value.trim()) {
    const normalized = value.trim().replace(/\s/g, "").replace(",", ".");
    const parsed = Number(normalized);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }

  return null;
}

/**
 * Formats detail snapshot display values to match table formatters.
 * Percent columns expect percent units (35 = 35%), never silent ratio→percent conversion.
 */
export function formatDetailFieldValue(
  value: AnalyticsScalar,
  dataType: AnalyticsDataType | undefined,
): string {
  return formatAnalyticsCellValue(value, dataType);
}

export function formatAnalyticsCellValue(
  value: AnalyticsScalar,
  dataType?: AnalyticsDataType | string,
  fallback = "N/A",
): string {
  const normalized = normalizeAnalyticsScalar(value, dataType);
  if (normalized == null) return fallback;
  if (typeof normalized === "boolean") return normalized ? "Da" : "Ne";

  switch (dataType) {
    case "currency": {
      return fmtRsd(normalized as number, 0, fallback);
    }
    case "percent": {
      return fmtPct(normalized as number, 2, fallback);
    }
    case "number": {
      const num = normalized as number;
      return fmtNumber(num, Number.isInteger(num) ? 0 : 2, fallback);
    }
    case "date": {
      if (typeof normalized !== "string" || Number.isNaN(Date.parse(normalized))) return fallback;
      return formatDate(normalized, fallback);
    }
    case "datetime": {
      if (typeof normalized !== "string" || Number.isNaN(Date.parse(normalized))) return fallback;
      return formatDateTime(normalized, fallback);
    }
    default:
      return stringifyValue(normalized, fallback);
  }
}

export function resolveAnalyticsTablePayload<Row>(input: {
  tableKey: string;
  tableTitle: string;
  columns: AnalyticsTableColumn<Row>[];
  rows: Row[];
  filters?: AnalyticsNamedValue[];
  metadata?: AnalyticsNamedValue[];
  methodologyMetricKeys?: string[];
  locale?: string;
  documentType?: string;
  templateName?: string;
  templateVersion?: number;
}): ResolvedAnalyticsTablePayload {
  const columns = input.columns.map((column) => ({
    key: column.key,
    header: column.header,
    dataType: column.dataType,
    formatHint: column.formatHint,
  }));

  const rows = input.rows.map((row) => {
    const resolvedRow: Record<string, AnalyticsScalar> = {};
    for (const column of input.columns) {
      const rawValue = column.getValue
        ? column.getValue(row)
        : (row as Record<string, AnalyticsScalar>)[column.key];
      resolvedRow[column.key] = normalizeAnalyticsScalar(rawValue, column.dataType);
    }

    return resolvedRow;
  });

  return {
    tableKey: input.tableKey,
    tableTitle: input.tableTitle,
    columns,
    rows,
    filters: (input.filters ?? []).map((item) => ({
      ...item,
      value: normalizeAnalyticsScalar(item.value),
    })),
    metadata: (input.metadata ?? []).map((item) => ({
      ...item,
      value: normalizeAnalyticsScalar(item.value),
    })),
    methodologyMetricKeys: input.methodologyMetricKeys,
    locale: input.locale,
    documentType: input.documentType,
    templateName: input.templateName,
    templateVersion: input.templateVersion,
  };
}

export function buildAnalyticsDetailSnapshot<Row>(input: {
  table: string;
  recordId: string;
  title: string;
  subtitle?: string | null;
  columns: AnalyticsTableColumn<Row>[];
  row: Row;
  metadata?: AnalyticsNamedValue[];
}): AnalyticsDetailResponse {
  const fields: AnalyticsDetailField[] = input.columns.map((column) => {
    const rawValue = column.getValue
      ? column.getValue(input.row)
      : (input.row as Record<string, AnalyticsScalar>)[column.key];

    return {
      key: column.key,
      label: column.detailLabel ?? column.header,
      value: formatAnalyticsCellValue(rawValue, column.dataType),
      dataType: column.dataType,
      highlight: column.dataType === "currency" || column.dataType === "percent",
    };
  });

  return {
    table: input.table,
    recordId: input.recordId,
    title: input.title,
    subtitle: input.subtitle,
    fields,
    metadata: (input.metadata ?? []).map((item) => ({
      key: item.key,
      label: item.label,
      value: formatAnalyticsCellValue(item.value, "text"),
      dataType: "text",
      highlight: false,
    })),
  };
}

export function saveBrowserPreviewPayload(payload: ResolvedAnalyticsTablePayload): string {
  const key = `${BROWSER_PREVIEW_PREFIX}${crypto.randomUUID()}`;
  const stored: StoredPrintPayload = {
    savedAtUtc: new Date().toISOString(),
    payload,
  };
  const raw = JSON.stringify(stored);
  localStorage.setItem(key, raw);
  return key;
}

export function getBrowserPreviewSnapshot(key: string | null): BrowserPreviewSnapshot | null {
  if (!key) return null;
  const raw = localStorage.getItem(key);
  if (!raw) return null;

  try {
    const parsed = JSON.parse(raw) as ResolvedAnalyticsTablePayload | StoredPrintPayload;
    const now = Date.now();

    if ("payload" in parsed && "savedAtUtc" in parsed) {
      const savedAtMs = Date.parse(parsed.savedAtUtc);
      const ageMs = now - savedAtMs;
      if (!Number.isFinite(ageMs) || ageMs > PRINT_TTL_MS) {
        localStorage.removeItem(key);
        return null;
      }

      return {
        payload: parsed.payload,
        savedAtUtc: parsed.savedAtUtc,
        expiresAtUtc: new Date(savedAtMs + PRINT_TTL_MS).toISOString(),
        ttlMs: PRINT_TTL_MS,
        ageMs,
      };
    }

    // Legacy unwrapped payload: treat as freshly readable but without durable provenance.
    const savedAtUtc = new Date(now).toISOString();
    return {
      payload: parsed as ResolvedAnalyticsTablePayload,
      savedAtUtc,
      expiresAtUtc: new Date(now + PRINT_TTL_MS).toISOString(),
      ttlMs: PRINT_TTL_MS,
      ageMs: 0,
    };
  } catch {
    return null;
  }
}

export function getBrowserPreviewPayload(key: string | null): ResolvedAnalyticsTablePayload | null {
  return getBrowserPreviewSnapshot(key)?.payload ?? null;
}

/** @deprecated Use saveBrowserPreviewPayload. Kept for generic print routes. */
export const savePrintPayload = saveBrowserPreviewPayload;
/** @deprecated Use getBrowserPreviewSnapshot. Kept for generic print routes. */
export const getPrintPayloadSnapshot = getBrowserPreviewSnapshot;
/** @deprecated Use getBrowserPreviewPayload. Kept for generic print routes. */
export const getPrintPayload = getBrowserPreviewPayload;

export function saveAnalyticsDetailSnapshot(snapshot: AnalyticsDetailResponse): void {
  sessionStorage.setItem(`${DETAIL_PREFIX}${snapshot.table}:${snapshot.recordId}`, JSON.stringify(snapshot));
}

export function getAnalyticsDetailSnapshot(table: string, recordId: string): AnalyticsDetailResponse | null {
  const raw = sessionStorage.getItem(`${DETAIL_PREFIX}${table}:${recordId}`);
  if (!raw) return null;

  try {
    return JSON.parse(raw) as AnalyticsDetailResponse;
  } catch {
    return null;
  }
}
