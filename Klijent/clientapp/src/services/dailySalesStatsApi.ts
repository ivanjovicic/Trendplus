import { fetchAnalyticsJson } from "./analyticsHttp";
import type { AnalyticsResponseMeta } from "../types/analytics";

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
  nonStandardReceiptCount: DailySalesNumeric;
  nonStandardReceiptRevenue: DailySalesNumeric;
  debtReceiptCount: DailySalesNumeric;
  debtReceiptRevenue: DailySalesNumeric;
  /** ISO 8601 date string — earliest sale date available in the whole dataset. Null if no data. */
  minAvailableDate: string | null;
  /** ISO 8601 date string — latest sale date available in the whole dataset. Null if no data. */
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
    "Greska pri ucitavanju dnevne prodaje po smenama i dobavljacima",
    { signal: query.signal }
  );
}
