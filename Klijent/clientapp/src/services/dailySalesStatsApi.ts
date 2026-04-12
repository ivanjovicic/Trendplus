import { fetchAnalyticsJson } from "./analyticsHttp";

export interface DailySalesSupplierHeader {
  supplierId: number | null;
  supplierName: string;
  isUnknown: boolean;
  totalQty: number;
  totalRevenue: number;
}

export interface DailySalesRow {
  date: string;
  firstShiftTotalItems: number;
  secondShiftTotalItems: number;
  totalRevenue: number;
  topSupplierCounts: number[];
  othersCount: number;
  totalItemsSold: number;
}

export interface DailySalesMetadata {
  totalDays: number;
  uniqueSuppliersInRange: number;
  unknownSupplierPct: number;
  unknownSupplierItems: number;
  offShiftItems: number;
  offShiftRevenue: number;
  totalItemsInRange: number;
  duplicateReceiptGroupCount: number;
  duplicateReceiptHeaderCount: number;
  receiptAmountMismatchCount: number;
  receiptAmountMismatchRevenue: number;
  nonStandardReceiptCount: number;
  nonStandardReceiptRevenue: number;
  debtReceiptCount: number;
  debtReceiptRevenue: number;
  /** ISO 8601 date string — earliest sale date available in the whole dataset. Null if no data. */
  minAvailableDate: string | null;
  /** ISO 8601 date string — latest sale date available in the whole dataset. Null if no data. */
  maxAvailableDate: string | null;
  warnings: string[];
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
