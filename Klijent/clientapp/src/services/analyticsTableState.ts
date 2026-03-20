import type {
  AnalyticsDetailField,
  AnalyticsDetailResponse,
  AnalyticsNamedValue,
  AnalyticsScalar,
  AnalyticsTableColumn,
  ResolvedAnalyticsTablePayload,
} from "../types/analyticsTable";

const PRINT_PREFIX = "analytics-print:";
const DETAIL_PREFIX = "analytics-detail:";
const PRINT_TTL_MS = 15 * 60 * 1000;

type StoredPrintPayload = {
  savedAtUtc: string;
  payload: ResolvedAnalyticsTablePayload;
};

function stringifyValue(value: AnalyticsScalar): string {
  if (value == null) return "";
  if (typeof value === "boolean") return value ? "Da" : "Ne";
  return String(value);
}

export function resolveAnalyticsTablePayload<Row>(input: {
  tableKey: string;
  tableTitle: string;
  columns: AnalyticsTableColumn<Row>[];
  rows: Row[];
  filters?: AnalyticsNamedValue[];
  metadata?: AnalyticsNamedValue[];
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
      value: stringifyValue(rawValue),
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

export function getPrintPayload(key: string | null): ResolvedAnalyticsTablePayload | null {
  if (!key) return null;
  const raw = localStorage.getItem(key);
  if (!raw) return null;

  try {
    const parsed = JSON.parse(raw) as ResolvedAnalyticsTablePayload | StoredPrintPayload;

    if ("payload" in parsed && "savedAtUtc" in parsed) {
      const ageMs = Date.now() - Date.parse(parsed.savedAtUtc);
      if (!Number.isFinite(ageMs) || ageMs > PRINT_TTL_MS) {
        localStorage.removeItem(key);
        return null;
      }

      return parsed.payload;
    }

    return parsed as ResolvedAnalyticsTablePayload;
  } catch {
    return null;
  }
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
