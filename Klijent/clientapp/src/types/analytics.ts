export interface SalesSummary {
  totalRevenue: number;
  totalTransactions: number;
  totalUnits: number;
  avgBasketValue: number;
  avgItemPrice: number;
}

export interface TopProduct {
  productId: number;
  productName: string;
  totalRevenue: number;
  totalUnits: number;
  velicina?: string | null;
  boja?: string | null;
}

export interface TopProductsResult {
  byRevenue: TopProduct[];
  byUnits: TopProduct[];
}

export interface InventoryStatus {
  totalSkuCount: number;
  totalOnHand: number;
  lowStockCount: number;
  outOfStockCount: number;
}

export interface DailySale {
  date: string;
  totalRevenue: number;
  transactionCount: number;
  totalUnits: number;
}

export interface CategoryData {
  kategorija: string;
  pol: string;
  totalRevenue: number;
  totalUnits: number;
  transactionCount: number;
}

export interface GenderData {
  pol: string;
  totalRevenue: number;
  totalUnits: number;
}

export interface SupplierData {
  dobavljacId?: number | null;
  dobavljacNaziv: string;
  totalRevenue: number;
  totalUnits: number;
  transactionCount: number;
}

export interface PaymentData {
  nacinPlacanja: string;
  totalRevenue: number;
  transactionCount: number;
}

export interface WeekdayData {
  dayOfWeek: number;
  dayName: string;
  totalRevenue: number;
  transactionCount: number;
}

export interface HourData {
  hour: number;
  totalRevenue: number;
  transactionCount: number;
}

export interface QuickInsights {
  bestDay?: string | null;
  bestDayRevenue: number;
  topProduct?: string | null;
  lowStockAlert: number;
}

export interface TransactionStats {
  avgItemsPerTransaction: number;
  avgTransactionValue: number;
  totalTransactions: number;
}

export interface CategoryTrendPoint {
  date: string;
  [key: string]: string | number;
}

export interface ReorderSuggestion {
  id: number;
  naziv: string;
  kolicina?: number | null;
  minimalnaKolicina?: number | null;
  kategorija?: string | null;
  nabavnaCena?: number | null;
}

export interface StoreOption {
  storeId: number;
  storeName: string;
  city?: string | null;
  region?: string | null;
}

export interface SupplierFilterOption {
  supplierId: number;
  supplierName: string;
}

export interface DashboardMetricCard {
  key: string;
  label: string;
  value: number;
  unit: string;
  trendPct?: number | null;
  status: "good" | "warning" | "critical" | "neutral" | string;
  subtitle: string;
}

export interface DashboardInsight {
  badge: string;
  description: string;
  color: "green" | "yellow" | "red" | "blue" | string;
}

export interface DashboardAction {
  priority: "P1" | "P2" | "P3" | string;
  title: string;
  recommendation: string;
}

export interface DashboardValidationItem {
  severity: "error" | "warning" | "info" | string;
  message: string;
}

export interface DashboardAdvancedSnapshot {
  generatedAtUtc: string;
  cards: DashboardMetricCard[];
  insights: DashboardInsight[];
  actions: DashboardAction[];
  validations: DashboardValidationItem[];
}

export interface DashboardValidationEndpoint {
  status: "good" | "warning" | "critical" | "info" | string;
  message: string;
  score?: number | null;
  totalSku?: number | null;
  affectedSku?: number | null;
  lastImport?: string | null;
  freshnessHours?: number | null;
  lostSalesEstimate?: number | null;
  negativeQtyCount?: number | null;
  totalRows?: number | null;
}

export interface TopProductAdvancedItem {
  productId: number;
  sku: string;
  productName: string;
  revenue: number;
  units: number;
  velocityUnitsPerDay: number;
  marginImpact?: number | null;
  stockStatus: "good" | "warning" | "critical" | "neutral" | string;
  trendPct?: number | null;
}

export interface TopProductsAdvancedResult {
  byRevenue: TopProductAdvancedItem[];
  byUnits: TopProductAdvancedItem[];
  byVelocity: TopProductAdvancedItem[];
  byMarginImpact: TopProductAdvancedItem[];
  marginAvailable: boolean;
  marginMessage?: string | null;
}

export interface AnalyticsDashboardBootstrap {
  summary: SalesSummary | null;
  inventory: InventoryStatus | null;
  dailySales: DailySale[];
  categoryData: CategoryData[];
  genderData: GenderData[];
  supplierData: SupplierData[];
  supplierOptions: SupplierFilterOption[];
  paymentData: PaymentData[];
  weekdayData: WeekdayData[];
  hourData: HourData[];
  quickInsights: QuickInsights | null;
  transactionStats: TransactionStats | null;
  advanced: DashboardAdvancedSnapshot | null;
  topAdvanced: TopProductsAdvancedResult | null;
  validationCompleteness: DashboardValidationEndpoint | null;
  validationFreshness: DashboardValidationEndpoint | null;
  validationLostSales: DashboardValidationEndpoint | null;
  errors: string[];
}

export type DataQualityIssueType = "missingSupplier" | "missingShoeType" | "invalidName";
export type DataQualitySortBy = "sales30d" | "lastUpdated" | "stock" | "name";
export type DataQualitySortDir = "asc" | "desc";

export interface DataQualityIssueItem {
  sku?: string | null;
  productId: string;
  name?: string | null;
  supplierId?: string | null;
  supplierName?: string | null;
  shoeTypeId?: string | null;
  shoeTypeName?: string | null;
  issueType: DataQualityIssueType;
  sales30d: number;
  stock: number;
  lastUpdated: string;
}

export interface DataQualityIssueListResult {
  page: number;
  pageSize: number;
  total: number;
  items: DataQualityIssueItem[];
}

export interface InventoryBalance {
  totalSku: number;
  totalOnHand: number;
  lowStockCount: number;
  outOfStockCount: number;
  estimatedInventoryValue?: number | null;
}

export interface InventoryListItem {
  id: number;
  naziv: string;
  plu?: string | null;
  kolicina?: number | null;
  minimalnaKolicina?: number | null;
  nabavnaCena?: number | null;
  estimatedValue?: number | null;
  idObjekat?: number | null;
  idDobavljac?: number | null;
  velicina?: string | null;
  velicinaGroup?: string | null;
}

export interface InventoryPagedResponse {
  items: InventoryListItem[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
}

export interface InventoryHistoryItem {
  movementId: number;
  tipPromene: string;
  datum: string;
  kolicina?: number | null;
  iznos: number;
  brojDokumenta?: string | null;
  korisnikIme?: string | null;
  dataOrigin?: string | null;
  storeId?: number | null;
  storeName?: string | null;
  supplierId?: number | null;
  supplierName?: string | null;
  staraCena?: number | null;
  novaCena?: number | null;
  komentar?: string | null;
}

export interface InventoryItemDetail {
  id: number;
  plu?: string | null;
  naziv: string;
  kolicina?: number | null;
  minimalnaKolicina?: number | null;
  nabavnaCena?: number | null;
  estimatedValue: number;
  storeId?: number | null;
  storeName?: string | null;
  supplierId?: number | null;
  supplierName?: string | null;
  kategorija?: string | null;
  pol?: string | null;
  materijal?: string | null;
  updatedAt: string;
  lastMovementAt?: string | null;
  movementCount30d: number;
  daysSinceMovement: number;
  agingBucket: string;
  agingLabel: string;
  abcClass: string;
  history: InventoryHistoryItem[];
}

export interface InventoryAgingBucket {
  bucketKey: string;
  label: string;
  itemCount: number;
  totalUnits: number;
  estimatedValue: number;
}

export interface InventoryAbcBucket {
  bucketKey: string;
  label: string;
  itemCount: number;
  estimatedValue: number;
  valueSharePct: number;
}

export interface InventoryInsightItem {
  id: number;
  plu?: string | null;
  naziv: string;
  supplierName?: string | null;
  storeName?: string | null;
  quantity: number;
  minimum: number;
  reorderGap: number;
  estimatedValue: number;
  daysSinceMovement: number;
  agingBucket: string;
  agingLabel: string;
  abcClass: string;
  stockState: string;
}

export interface InventoryInsights {
  totalItems: number;
  totalEstimatedValue: number;
  aging: InventoryAgingBucket[];
  abc: InventoryAbcBucket[];
  topAgedItems: InventoryInsightItem[];
  topCapitalLockedItems: InventoryInsightItem[];
}

// ── Inventory Forecast ────────────────────────────────────────────────────────

export interface ForecastRowDto {
  skuId: number;
  storeId: number;
  sizeCode: string;
  forecast7d: number;
  forecast14d: number;
  forecast28d: number;
  probabilityOfOOSIn7d: number;  // 0–1
  overstockRisk: number;         // 0–1
  confidenceScore: number;       // 0–1
  explanation: string;
}

export interface ForecastDto {
  generatedAtUtc: string;
  totalCount: number;
  snapshotAvailable: boolean;
  warning?: string | null;
  items: ForecastRowDto[];
}

// ── Size Curve ────────────────────────────────────────────────────────────────

export interface SizeCurvePointDto {
  skuId: number;
  storeId: number;
  sizeCode: string;
  actualSizeShare: number;   // percentage 0–100
  idealSizeShare: number;    // percentage 0–100
  deviationPct: number;      // actualSizeShare - idealSizeShare
  isCoreSizeMissing: boolean;
  isDeadSize: boolean;
  brokenRun: boolean;
  curveConfidence: number;   // 0–1
  reasonCodes: string[];
}

export interface SizeCurveDto {
  generatedAtUtc: string;
  totalCount: number;
  snapshotAvailable: boolean;
  warning?: string | null;
  items: SizeCurvePointDto[];
}

// ── Rebalancing ───────────────────────────────────────────────────────────────

export interface RebalanceSuggestionDto {
  fromStoreId: number;
  toStoreId: number;
  skuId: number;
  sizeCode: string;
  recommendedQty: number;
  urgency: string;              // 'urgent' | 'recommended' | 'optional'
  confidence: number;           // 0–1
  reason: string;
  expectedSavedSales: number;   // RSD
  expectedCapitalRelease: number;
}

export interface RebalanceListDto {
  generatedAtUtc: string;
  totalCount: number;
  snapshotAvailable: boolean;
  warning?: string | null;
  items: RebalanceSuggestionDto[];
}

// ── Inventory Alerts ─────────────────────────────────────────────────────────

export interface InventoryAlertDto {
  alertType: string;
  skuId: number;
  storeId: number;
  sizeCode?: string | null;
  severity: string;   // 'critical' | 'warning' | 'info'
  title: string;
  message: string;
  confidenceScore: number;
}

export interface InventoryAlertListDto {
  generatedAtUtc: string;
  totalCount: number;
  snapshotAvailable: boolean;
  warning?: string | null;
  items: InventoryAlertDto[];
}

export interface InventoryStoreComparisonItem {
  storeId: number;
  storeName: string;
  totalSku: number;
  totalOnHand: number;
  lowStockCount: number;
  outOfStockCount: number;
  criticalCount: number;
  stale90PlusCount: number;
  estimatedValue: number;
  avgUnitsPerSku: number;
  healthySharePct: number;
}

export interface InventoryStoreComparisonFocus {
  skuKey: string;
  label: string;
  storeCoverage: number;
  impactedStores: string[];
}

export interface InventoryStoreComparison {
  generatedAtUtc: string;
  stores: InventoryStoreComparisonItem[];
  sharedRisks: InventoryStoreComparisonFocus[];
  summary: string;
}

export interface InventoryActionSuggestion {
  suggestionKey: string;
  actionType: string;
  priority: string;
  label: string;
  reason: string;
  status: string;
  artikalId: number;
  plu?: string | null;
  naziv: string;
  fromStoreName?: string | null;
  toStoreName?: string | null;
  suggestedQty: number;
  estimatedValue: number;
  daysSinceMovement: number;
  note?: string | null;
  updatedAtUtc?: string | null;
}

export interface InventoryActionWorkflow {
  generatedAtUtc: string;
  pendingCount: number;
  approvedCount: number;
  deferredCount: number;
  closedCount: number;
  items: InventoryActionSuggestion[];
}

export interface InventoryActionDecisionInput {
  actionType: string;
  status: "pending" | "approved" | "deferred" | "closed";
  note?: string;
}

export interface InventoryReportSchedule {
  id: number;
  name: string;
  isEnabled: boolean;
  frequency: "daily" | "weekly" | string;
  dayOfWeek?: number | null;
  runAtLocalTime: string;
  timeZoneId: string;
  format: "pdf" | "xlsx" | "csv" | string;
  orientation: "portrait" | "landscape" | string;
  includeFiltersAndMetadata: boolean;
  recipientsCsv: string;
  subject?: string | null;
  search?: string | null;
  storeId?: number | null;
  supplierId?: number | null;
  sortBy?: string | null;
  lastRunAtUtc?: string | null;
  lastRunStatus?: string | null;
  lastError?: string | null;
  lastDocumentId?: string | null;
}

export interface InventoryReportScheduleInput {
  name: string;
  isEnabled: boolean;
  frequency: "daily" | "weekly";
  dayOfWeek?: number | null;
  runAtLocalTime: string;
  timeZoneId: string;
  format: "pdf" | "xlsx" | "csv";
  orientation: "portrait" | "landscape";
  includeFiltersAndMetadata: boolean;
  recipientsCsv: string;
  subject?: string;
  search?: string;
  storeId?: number | null;
  supplierId?: number | null;
  sortBy?: string | null;
}

export interface InventoryScheduleRunResponse {
  success: boolean;
  status: string;
  message: string;
  documentId?: string | null;
  executedAtUtc: string;
}
