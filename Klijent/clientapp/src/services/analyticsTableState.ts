import type {
  AnalyticsDetailField,
  AnalyticsDetailResponse,
  AnalyticsNamedValue,
  AnalyticsScalar,
  AnalyticsTableColumn,
  ResolvedAnalyticsTablePayload,
} from "../types/analyticsTable";
import { fmtNumber, fmtPct, fmtRsd, formatDate, formatDateTime } from "../utils/analyticsFormatters";

const PRINT_PREFIX = "analytics-print:";
const DETAIL_PREFIX = "analytics-detail:";
/** Browser-stored print/report preview TTL (10 minutes). */
export const ANALYTICS_PRINT_TTL_MS = 10 * 60 * 1000;
const PRINT_TTL_MS = ANALYTICS_PRINT_TTL_MS;

type StoredPrintPayload = {
  savedAtUtc: string;
  payload: ResolvedAnalyticsTablePayload;
};

export type PrintPayloadSnapshot = {
  payload: ResolvedAnalyticsTablePayload;
  savedAtUtc: string;
  expiresAtUtc: string;
  ttlMs: number;
  ageMs: number;
};

function stringifyValue(value: AnalyticsScalar): string {
  if (value == null) return "";
  if (typeof value === "boolean") return value ? "Da" : "Ne";
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
  dataType: AnalyticsTableColumn<unknown>["dataType"],
): string {
  if (value == null) {
    return "";
  }

  if (typeof value === "boolean") {
    return value ? "Da" : "Ne";
  }

  switch (dataType) {
    case "currency": {
      const amount = toFiniteNumber(value);
      return amount == null ? stringifyValue(value) : fmtRsd(amount, 0, stringifyValue(value));
    }
    case "percent": {
      const pct = toFiniteNumber(value);
      return pct == null ? stringifyValue(value) : fmtPct(pct, 2);
    }
    case "number": {
      const num = toFiniteNumber(value);
      if (num == null) return stringifyValue(value);
      return fmtNumber(num, Number.isInteger(num) ? 0 : 2);
    }
    case "date":
      return formatDate(typeof value === "string" ? value : String(value), stringifyValue(value));
    case "datetime":
      return formatDateTime(typeof value === "string" ? value : String(value), stringifyValue(value));
    default:
      return stringifyValue(value);
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
      resolvedRow[column.key] = column.getValue
        ? column.getValue(row)
        : (row as Record<string, AnalyticsScalar>)[column.key];
    }

    return resolvedRow;
  });

  return {
    tableKey: input.tableKey,
    tableTitle: input.tableTitle,
    columns,
    rows,
    filters: input.filters ?? [],
    metadata: input.metadata ?? [],
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
      value: formatDetailFieldValue(rawValue, column.dataType),
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
      value: stringifyValue(item.value),
      dataType: "text",
      highlight: false,
    })),
  };
}

export function savePrintPayload(payload: ResolvedAnalyticsTablePayload): string {
  const key = `${PRINT_PREFIX}${crypto.randomUUID()}`;
  const stored: StoredPrintPayload = {
    savedAtUtc: new Date().toISOString(),
    payload,
  };
  const raw = JSON.stringify(stored);
  localStorage.setItem(key, raw);
  return key;
}

export function getPrintPayloadSnapshot(key: string | null): PrintPayloadSnapshot | null {
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

export function getPrintPayload(key: string | null): ResolvedAnalyticsTablePayload | null {
  return getPrintPayloadSnapshot(key)?.payload ?? null;
}

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
