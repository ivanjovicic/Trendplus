import { apiUrl } from "../utils/apiUrl";
import { fetchWithTimeout } from "../utils/fetchWithTimeout";
import { API_COLD_START_TIMEOUT_MS } from "../utils/apiTimeouts";

export interface TransferLineInputDto {
  skuId: number;
  quantity: number;
  unit?: string;
}

export interface TransferItemDto {
  skuId: number;
  skuCode?: string;
  skuName?: string;
  quantity: number;
  reservedQuantity: number;
  processedQuantity: number;
  availableQuantity?: number;
  unit?: string;
}

export interface TransferCreateRequest {
  sourceId: number;
  destinationId: number;
  sourceType: "store" | "warehouse";
  destinationType: "store" | "warehouse";
  reserve: boolean;
  notes?: string;
  items: TransferLineInputDto[];
}

export interface TransferUpdateRequest {
  reserve: boolean;
  notes?: string;
  items: TransferLineInputDto[];
}

export interface TransferResponse {
  id: number;
  status: string;
  sourceId: number;
  destinationId: number;
  reserve: boolean;
  notes?: string;
  items: TransferItemDto[];
  createdAt: string;
  updatedAt: string;
  confirmedAt?: string;
  completedAt?: string;
  cancelledAt?: string;
  createdBy?: string;
  updatedBy?: string;
  totalQuantity: number;
  lineCount: number;
}

export interface TransferListItemProjection {
  id: number;
  status: string;
  sourceId: number;
  destinationId: number;
  reserve: boolean;
  notes?: string;
  createdBy?: string;
  updatedBy?: string;
  itemCount: number;
  totalQuantity: number;
  createdAt: string;
  updatedAt: string;
  completedAt?: string;
}

export interface TransferListResponse {
  items: TransferListItemProjection[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

async function parseApiError(response: Response, fallback: string): Promise<string> {
  try {
    const json = (await response.json()) as { error?: string; detail?: string; title?: string };
    return json.error ?? json.detail ?? json.title ?? fallback;
  } catch {
    const text = await response.text();
    return text || fallback;
  }
}

async function fetchJson<T>(path: string, init?: RequestInit, fallback = "Transfer request failed"): Promise<T> {
  const response = await fetchWithTimeout(apiUrl(path), init, API_COLD_START_TIMEOUT_MS);
  if (!response.ok) {
    throw new Error(await parseApiError(response, fallback));
  }
  return (await response.json()) as T;
}

export function createTransfer(req: TransferCreateRequest): Promise<TransferResponse> {
  return fetchJson<TransferResponse>("/api/transfers", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  }, "Kreiranje transfera nije uspelo");
}

export function updateTransfer(id: number, req: TransferUpdateRequest): Promise<TransferResponse> {
  return fetchJson<TransferResponse>(`/api/transfers/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  }, "Azuriranje transfera nije uspelo");
}

export function confirmTransfer(id: number): Promise<TransferResponse> {
  return fetchJson<TransferResponse>(`/api/transfers/${id}/confirm`, { method: "POST" }, "Potvrda transfera nije uspela");
}

export function completeTransfer(id: number): Promise<TransferResponse> {
  return fetchJson<TransferResponse>(`/api/transfers/${id}/complete`, { method: "POST" }, "Zavrsetak transfera nije uspeo");
}

export function cancelTransfer(id: number): Promise<TransferResponse> {
  return fetchJson<TransferResponse>(`/api/transfers/${id}/cancel`, { method: "POST" }, "Otkazivanje transfera nije uspelo");
}

export function getTransfer(id: number): Promise<TransferResponse> {
  return fetchJson<TransferResponse>(`/api/transfers/${id}`, undefined, "Ucitavanje transfera nije uspelo");
}

export function listTransfers(options?: {
  pageNumber?: number;
  pageSize?: number;
  status?: string;
  actor?: string;
  createdBy?: string;
  updatedBy?: string;
}): Promise<TransferListResponse> {
  const params = new URLSearchParams();
  if (options?.pageNumber) params.set("pageNumber", String(options.pageNumber));
  if (options?.pageSize) params.set("pageSize", String(options.pageSize));
  if (options?.status) params.set("status", options.status);
  if (options?.actor) params.set("actor", options.actor);
  if (options?.createdBy) params.set("createdBy", options.createdBy);
  if (options?.updatedBy) params.set("updatedBy", options.updatedBy);

  const query = params.toString();
  return fetchJson<TransferListResponse>(`/api/transfers${query ? `?${query}` : ""}`, undefined, "Ucitavanje transfer liste nije uspelo");
}
