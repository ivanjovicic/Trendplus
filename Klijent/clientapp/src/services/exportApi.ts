import type { ResolvedAnalyticsTablePayload } from "../types/analyticsTable";
import { apiUrl } from "../utils/apiUrl";
export const SYNC_ROW_LIMIT = 5000;

export type ExportFormat = "pdf" | "xlsx" | "csv";
export type ExportOrientation = "portrait" | "landscape";

export interface ExportOptions {
  format: ExportFormat;
  orientation: ExportOrientation;
  includeFiltersAndMetadata: boolean;
  preview?: boolean;
  forceAsync?: boolean;
}

export interface DocumentOperationResponse {
  documentId: string;
  batchId?: string | null;
  status: string;
  isAsync: boolean;
  fileName?: string | null;
  mimeType?: string | null;
  sizeBytes?: number | null;
  createdAtUtc: string;
  completedAtUtc?: string | null;
  expiresAtUtc?: string | null;
  statusUrl?: string | null;
  downloadUrl?: string | null;
  printUrl?: string | null;
}

export interface DocumentStatusResponse extends DocumentOperationResponse {
  format?: string | null;
  tableKey?: string | null;
  tableTitle?: string | null;
  rowCount?: number;
  errorMessage?: string | null;
  startedAtUtc?: string | null;
}

function parseError(body: unknown, status: number): string {
  if (body && typeof body === "object") {
    const candidate = body as { detail?: string; title?: string; message?: string };
    return candidate.detail ?? candidate.message ?? candidate.title ?? `HTTP ${status}`;
  }

  return `HTTP ${status}`;
}

function buildRequest(payload: ResolvedAnalyticsTablePayload, options: ExportOptions) {
  return {
    format: options.format,
    orientation: options.orientation,
    includeFiltersAndMetadata: options.includeFiltersAndMetadata,
    preview: options.preview ?? false,
    forceAsync: options.forceAsync ?? false,
    locale: payload.locale ?? "sr-RS",
    templateName: payload.templateName ?? "analytics-table-default",
    templateVersion: payload.templateVersion,
    documentType: payload.documentType ?? "analytics-table-report",
    tableKey: payload.tableKey,
    tableTitle: payload.tableTitle,
    columns: payload.columns.map((column) => ({
      key: column.key,
      header: column.header,
      dataType: column.dataType,
      formatHint: column.formatHint,
    })),
    rows: payload.rows,
    filters: payload.filters,
    metadata: payload.metadata,
  };
}

export function resolveApiUrl(path: string): string {
  if (!path) return apiUrl("/");
  if (path.startsWith("http://") || path.startsWith("https://")) return path;
  return apiUrl(path);
}

export async function generateExport(
  payload: ResolvedAnalyticsTablePayload,
  options: ExportOptions
): Promise<DocumentOperationResponse> {
  const response = await fetch(apiUrl("/api/documents/generate"), {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(buildRequest(payload, options)),
  });

  const body = (await response.json().catch(() => null)) as DocumentOperationResponse | { detail?: string; title?: string; message?: string } | null;
  if (!response.ok || !body || !("documentId" in body)) {
    throw new Error(parseError(body, response.status));
  }

  return body;
}

export async function requestPrintPreview(
  payload: ResolvedAnalyticsTablePayload,
  options: Omit<ExportOptions, "format"> & { format?: ExportFormat }
): Promise<DocumentOperationResponse> {
  const response = await fetch(apiUrl("/api/documents/print-preview"), {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(buildRequest(payload, {
      format: options.format ?? "pdf",
      orientation: options.orientation,
      includeFiltersAndMetadata: options.includeFiltersAndMetadata,
      preview: true,
      forceAsync: false,
    })),
  });

  const body = (await response.json().catch(() => null)) as DocumentOperationResponse | { detail?: string; title?: string; message?: string } | null;
  if (!response.ok || !body || !("documentId" in body)) {
    throw new Error(parseError(body, response.status));
  }

  return body;
}

export async function getExportStatus(documentId: string): Promise<DocumentStatusResponse> {
  const response = await fetch(apiUrl(`/api/exports/${documentId}/status`));
  const body = (await response.json().catch(() => null)) as DocumentStatusResponse | { detail?: string; title?: string; message?: string } | null;

  if (!response.ok || !body || !("documentId" in body)) {
    throw new Error(parseError(body, response.status));
  }

  return body;
}

export async function listExports(take = 50): Promise<DocumentStatusResponse[]> {
  const response = await fetch(apiUrl(`/api/exports?take=${take}`));
  const body = (await response.json().catch(() => null)) as DocumentStatusResponse[] | { detail?: string; title?: string; message?: string } | null;

  if (!response.ok || !Array.isArray(body)) {
    throw new Error(parseError(body, response.status));
  }

  return body;
}

export async function waitForExport(documentId: string, timeoutMs = 120_000): Promise<DocumentStatusResponse> {
  const startedAt = Date.now();

  while (Date.now() - startedAt < timeoutMs) {
    const status = await getExportStatus(documentId);
    if (status.status === "completed") {
      return status;
    }

    if (status.status === "failed" || status.status === "poisoned") {
      throw new Error(status.errorMessage ?? "Export nije uspesno zavrsen.");
    }

    await new Promise((resolve) => window.setTimeout(resolve, 1500));
  }

  throw new Error("Export jos nije spreman. Proverite listu izvoza za status.");
}

export function downloadExport(downloadUrl: string, fileName?: string | null): void {
  const link = document.createElement("a");
  link.href = resolveApiUrl(downloadUrl);
  if (fileName) {
    link.download = fileName;
  }
  link.rel = "noopener";
  document.body.appendChild(link);
  link.click();
  link.remove();
}
