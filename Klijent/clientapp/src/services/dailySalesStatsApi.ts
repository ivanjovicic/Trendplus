import { fetchAnalyticsJson } from "./analyticsHttp";
import type { AnalyticsResponseMeta } from "../types/analytics";
import { dailySalesTableResponseSchema } from "../validation/analyticsResponseSchemas";

export type DailySalesNumeric = number | null | undefined;

export interface DailySalesSupplierHeader {
  supplierId: number | null;
  supplierName: string;
  isUnknown: boolean;
  totalQty: DailySalesNumeric;
  totalRevenue: DailySalesNumeric;
}

export interface DailySalesRow {
  date: string;
  firstShiftTotalItems: DailySalesNumeric;
  secondShiftTotalItems: DailySalesNumeric;
  totalRevenue: DailySalesNumeric;
  topSupplierCounts: DailySalesNumeric[];
  othersCount: DailySalesNumeric;
  totalItemsSold: DailySalesNumeric;
}

export interface DailySalesReceiptReconciliation {
  status: "verified" | "unavailable" | string;
  reasonCode?: string | null;
  matchedReceiptCount: DailySalesNumeric;
  unmatchedReceiptCount: DailySalesNumeric;
  unmatchedDnevnikReceiptCount: DailySalesNumeric;
  mismatchCount: DailySalesNumeric;
  mismatchAmount: DailySalesNumeric;
}

export interface DailySalesMetadata {
  totalDays: DailySalesNumeric;
  uniqueSuppliersInRange: DailySalesNumeric;
  unknownSupplierPct: DailySalesNumeric;
  unknownSupplierItems: DailySalesNumeric;
  offShiftItems: DailySalesNumeric;
  offShiftRevenue: DailySalesNumeric;
  totalItemsInRange: DailySalesNumeric;
  duplicateReceiptGroupCount: DailySalesNumeric;
  duplicateReceiptHeaderCount: DailySalesNumeric;
  receiptAmountMismatchCount: DailySalesNumeric;
  receiptAmountMismatchRevenue: DailySalesNumeric;
  receiptReconciliation?: DailySalesReceiptReconciliation;
  nonStandardReceiptCount: DailySalesNumeric;
  nonStandardReceiptRevenue: DailySalesNumeric;
  debtReceiptCount: DailySalesNumeric;
  debtReceiptRevenue: DailySalesNumeric;
  diagnosticsDataScope?: string | null;
  availabilityDataScope?: string | null;
  /** ISO 8601 date string — earliest sale date available in the selected scope/store. Null if no data. */
  minAvailableDate: string | null;
  /** ISO 8601 date string — latest sale date available in the selected scope/store. Null if no data. */
  maxAvailableDate: string | null;
  warnings?: string[];
}

export interface DailySalesTableResponse {
  requestedFrom: string;
  requestedTo: string;
  storeId: number | null;
  topN: number;
  dataScope: string;
  topSuppliers: DailySalesSupplierHeader[];
  topSuppliersOrder: string[];
  dateRows: DailySalesRow[];
  metadata: DailySalesMetadata;
  meta?: AnalyticsResponseMeta;
}

export interface DailySalesQuery {
  fromDate?: string | null;
  toDate?: string | null;
  storeId?: number | null;
  topN?: number | null;
  dataScope?: string | null;
  signal?: AbortSignal;
}

export async function getDailySalesStats(query: DailySalesQuery = {}): Promise<DailySalesTableResponse> {
  const params = new URLSearchParams();
  if (query.fromDate) params.set("fromDate", query.fromDate);
  if (query.toDate) params.set("toDate", query.toDate);
  if (query.storeId != null) params.set("storeId", String(query.storeId));
  if (query.topN != null) params.set("topN", String(query.topN));
  if (query.dataScope) params.set("dataScope", query.dataScope);

  return fetchAnalyticsJson<DailySalesTableResponse>(
    "/api/analytics/daily-sales",
    params,
    "Greška pri učitavanju dnevne prodaje po smenama i dobavljačima",
    { signal: query.signal, schema: dailySalesTableResponseSchema }
  );
}
