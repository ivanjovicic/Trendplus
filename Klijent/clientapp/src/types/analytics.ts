export interface SalesSummary {
  totalRevenue: number;
  totalTransactions: number;
  totalUnits: number;
  avgBasketValue: number;
  avgItemPrice: number;
}

export interface AnalyticsResponseMeta {
  success: boolean;
  warningCode?: string | null;
  warningMessage?: string | null;
  errorCode?: string | null;
  errorMessage?: string | null;
  emptyReason?: string | null;
  correlationId?: string | null;
  message?: string | null;
  generatedAtUtc?: string | null;
  lastRefreshAtUtc?: string | null;
  dataQualityStatus?: "good" | "warning" | "critical" | "insufficient_data" | string | null;
  isPartial?: boolean;
}

export type AnalyticsFreshnessStatus = "fresh" | "stale" | "critical" | "unknown";

export interface AnalyticsRefreshJobStatus {
  key: string;
  displayName: string;
  workerName: string;
  lastSuccessfulRefreshAtUtc?: string | null;
  lastAttemptAtUtc?: string | null;
  lastFailureAtUtc?: string | null;
  isRunning: boolean;
  lastErrorMessage?: string | null;
  currentStep?: string | null;
  refreshedObjects: string[];
  failedObjects: string[];
  durationSeconds?: number | null;
  dataFreshnessStatus: AnalyticsFreshnessStatus | string;
  statusReason?: string | null;
}

export interface AnalyticsRefreshRun {
  id: number;
  jobKey: string;
  jobName: string;
  status: "running" | "succeeded" | "failed" | "partial" | string;
  startedAtUtc: string;
  finishedAtUtc?: string | null;
  durationSeconds?: number | null;
  refreshedObjects: string[];
  failedObjects: string[];
  errorCode?: string | null;
  errorMessage?: string | null;
  correlationId?: string | null;
  triggeredBy: "nightly" | "manual" | "import" | "system" | string;
  processMode: "web" | "worker" | "unknown" | string;
  workerName?: string | null;
  createdAtUtc: string;
}

export interface AnalyticsRefreshStatus {
  lastSuccessfulRefreshAtUtc?: string | null;
  lastAttemptAtUtc?: string | null;
  lastFailureAtUtc?: string | null;
  isRunning: boolean;
  lastErrorMessage?: string | null;
  currentStep?: string | null;
  refreshedObjects: string[];
  failedObjects: string[];
  durationSeconds?: number | null;
  dataFreshnessStatus: AnalyticsFreshnessStatus | string;
  processMode: "web" | "worker" | "unknown" | string;
  processType: "web" | "worker" | string;
  workersEnabled: boolean;
  workerWarning?: string | null;
  workerProcessWarning?: string | null;
  cacheMode?: "redis" | "in-memory" | "disabled" | "unknown" | string;
  isDistributed?: boolean;
  lastAnalyticsCacheClearAtUtc?: string | null;
  lastReportCacheClearAtUtc?: string | null;
  cacheWarning?: string | null;
  generatedAtUtc: string;
  jobs: AnalyticsRefreshJobStatus[];
  recentRuns?: AnalyticsRefreshRun[];
}

export interface AnalyticsCacheStatus {
  provider: string;
  redisAvailable: boolean;
  redisEnabled: boolean;
  isShared: boolean;
  isDistributed: boolean;
  cacheMode: "redis" | "in-memory" | "disabled" | "unknown" | string;
  environment: string;
  cacheType: string;
  message: string;
  warning?: string | null;
  lastClearAtUtc?: string | null;
  lastClearFamily?: string | null;
  lastAnalyticsCacheClearAtUtc?: string | null;
  lastReportCacheClearAtUtc?: string | null;
  reportCacheVersion?: number;
  clearStateStorage?: string | null;
}

export interface AnalyticsCacheInvalidateResponse {
  success: boolean;
  message: string;
  lastClearAtUtc?: string | null;
  lastClearFamily?: string | null;
  lastAnalyticsCacheClearAtUtc?: string | null;
  lastReportCacheClearAtUtc?: string | null;
  reportCacheVersion?: number;
  isShared: boolean;
  warning?: string | null;
  storage: string;
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
  // TODO(backend-dto): add confidence/reliability/dataQualityStatus/statusReason to dashboard actions.
  confidencePct?: number | null;
  reliabilityPct?: number | null;
  recommendationAllowed?: boolean | null;
  dataQualityStatus?: "good" | "warning" | "critical" | "insufficient_data" | string | null;
  statusReason?: string | null;
  reasonCodes?: string[] | null;
}

export interface DashboardDecisionAction {
  actionKey?: string | null;
  sourceType?: AnalyticsActionSourceType | null;
  priority: "P1" | "P2" | "P3" | string;
  title: string;
  description?: string | null;
  reason: string;
  statusReason?: string | null;
  recommendationStatus?: string | null;
  expectedImpact?: string | null;
  impactEstimateRsd?: number | null;
  confidencePct?: number | null;
  reliabilityPct?: number | null;
  recommendationAllowed?: boolean | null;
  dataQualityStatus?: "good" | "warning" | "critical" | "insufficient_data" | string | null;
  actionUrl?: string | null;
  metadata?: Record<string, unknown> | null;
  link: string;
  linkLabel?: string | null;
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
  // TODO(backend-dto): add per-row margin quality tier / cost coverage and recommendation quality payload.
  marginQualityLabel?: string | null;
  marginQualityTier?: string | null;
  confidencePct?: number | null;
  reliabilityPct?: number | null;
  dataQualityStatus?: "good" | "warning" | "critical" | "insufficient_data" | string | null;
  statusReason?: string | null;
  reasonCodes?: string[] | null;
}

export interface TopProductsAdvancedResult {
  byRevenue: TopProductAdvancedItem[];
  byUnits: TopProductAdvancedItem[];
  byVelocity: TopProductAdvancedItem[];
  byMarginImpact: TopProductAdvancedItem[];
  marginAvailable: boolean;
  marginMessage?: string | null;
}

export type ProductDecisionRecommendationStatus =
  | "BOOST"
  | "REPLENISH"
  | "WATCH"
  | "MARKDOWN"
  | "DO_NOT_ORDER"
  | "FIX_DATA"
  | "INSUFFICIENT_DATA";

export interface ProductDecisionCenterItem {
  productId: number;
  recommendationId?: string | null;
  sourceType?: string | null;
  sourceKey?: string | null;
  recommendationType?: string | null;
  sku: string;
  productName: string;
  supplierId?: number | null;
  supplierName?: string | null;
  category?: string | null;
  tipObuce?: string | null;
  color?: string | null;
  size?: string | null;
  revenue: number;
  unitsSold: number;
  velocityUnitsPerDay: number;
  marginContribution: number;
  marginPct?: number | null;
  marginQualityLabel: string;
  marginCoveragePct: number;
  currentStock: number;
  minStock: number;
  stockGap: number;
  daysSinceLastSale?: number | null;
  trendPct?: number | null;
  lostSalesEstimate: number;
  slowStockCapital?: number | null;
  dataQualityStatus: string;
  confidenceLevel?: "high" | "medium" | "low" | "insufficient_data" | string | null;
  confidenceScore?: number | null;
  confidencePct: number;
  reliabilityPct: number;
  recommendationStatus: ProductDecisionRecommendationStatus;
  recommendationLabel: string;
  recommendationReason: string;
  reasonCodes: string[];
  warningCodes?: string[] | null;
  primaryDrivers?: string[] | null;
  expectedImpactRsd?: number | null;
  impactWindowDays?: number | null;
  riskIfIgnored?: string | null;
  explainabilityText?: string | null;
  inputFreshnessStatus?: "fresh" | "stale" | "critical" | "unknown" | string | null;
  recommendedAction: string;
}

export interface ProductDecisionCenterSummary {
  replenishCount: number;
  markdownCount: number;
  highPotentialCount: number;
  badDataCount: number;
  lostSalesEstimate: number;
  slowStockCapital: number;
}

export interface ProductDecisionCenterResponse {
  generatedAtUtc: string;
  periodFromUtc: string;
  periodToUtc: string;
  totalRows: number;
  analyzedRows?: number;
  ignoredRowsCount?: number;
  summary: ProductDecisionCenterSummary;
  rows: ProductDecisionCenterItem[];
  meta?: AnalyticsResponseMeta | null;
}

export interface DecisionBoardMetric {
  label: string;
  value: string;
  tone: "good" | "warning" | "critical" | "neutral" | "insufficient" | string;
  note?: string | null;
}

export interface DecisionBoardSourceState {
  sourceKey: string;
  displayName: string;
  status: "good" | "warning" | "critical" | "insufficient_data" | "fresh" | "stale" | "unknown" | string;
  generatedAtUtc?: string | null;
  warningCodes: string[];
  message?: string | null;
  sourceLink?: string | null;
}

export interface DecisionBoardCard {
  id: string;
  kind: "product" | "inventory" | "supplier" | "blocker" | "action" | "outcome" | string;
  sectionKey: string;
  sourceModule: string;
  sourceType?: string | null;
  sourceKey?: string | null;
  title: string;
  summary?: string | null;
  confidenceLevel: "high" | "medium" | "low" | "insufficient_data" | string;
  confidenceScore?: number | null;
  reliabilityPct?: number | null;
  expectedImpactRsd?: number | null;
  measuredImpactRsd?: number | null;
  realizationRatio?: number | null;
  riskIfIgnored: string;
  recommendedNextAction: string;
  actionHref: string;
  alreadyInAction: boolean;
  alreadyClosed: boolean;
  warningCodes: string[];
  dataQualityStatus: "good" | "warning" | "critical" | "insufficient_data" | "unknown" | string;
  generatedAtUtc?: string | null;
  priorityScore: number;
  impactScore: number;
}

export interface DecisionBoardSection {
  key: string;
  title: string;
  description: string;
  sourceLink: string;
  emptyMessage: string;
  warnings: string[];
  cards: DecisionBoardCard[];
}

export interface DecisionBoardAggregateResponse {
  generatedAtUtc: string;
  periodFromUtc?: string | null;
  periodToUtc?: string | null;
  lastRefreshAtUtc?: string | null;
  overallDataQualityStatus: "good" | "warning" | "critical" | "insufficient_data" | "unknown" | string;
  recommendationNote: string;
  warnings: string[];
  metrics: DecisionBoardMetric[];
  sourceStates: DecisionBoardSourceState[];
  sections: DecisionBoardSection[];
  meta?: AnalyticsResponseMeta | null;
}

export interface ExecutiveTopSupplier {
  supplierId?: number | null;
  supplierName: string;
  revenue: number;
  marginContribution: number;
  link: string;
}

export interface ExecutiveTopMarginItem {
  key: string;
  label: string;
  itemType: "product" | "category" | string;
  productId?: number | null;
  supplierId?: number | null;
  supplierName?: string | null;
  revenue: number;
  marginContribution: number;
  marginPct?: number | null;
  dataQualityStatus: "good" | "warning" | "critical" | "insufficient_data" | "unknown" | string;
  confidencePct?: number | null;
  link: string;
}

export interface ExecutiveNegativeSignal {
  signalType: string;
  title: string;
  description: string;
  priority: "P1" | "P2" | "P3" | string;
  impactEstimateRsd?: number | null;
  confidencePct?: number | null;
  dataQualityStatus: "good" | "warning" | "critical" | "insufficient_data" | "unknown" | string;
  recommendationStatus?: string | null;
  recommendationReason?: string | null;
  productId?: number | null;
  sku?: string | null;
  productName?: string | null;
  supplierName?: string | null;
  link: string;
}

export interface ExecutiveDataQualitySummary {
  missingSupplierCount: number;
  missingCostCount: number;
  insufficientSignalCount: number;
  ignoredRowsCount: number;
  zeroRevenueRowsCount?: number;
  freshnessStatus: string;
}

export interface ExecutiveDashboardSnapshot {
  totalMarginContributionRsd: number;
  inventoryDangerValueRsd: number;
  topSuppliers: ExecutiveTopSupplier[];
  topMarginProducts: ExecutiveTopMarginItem[];
  topMarginCategories: ExecutiveTopMarginItem[];
  negativeSignals: ExecutiveNegativeSignal[];
  dataQualitySummary: ExecutiveDataQualitySummary;
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
  decisionActions: DashboardDecisionAction[];
  executive?: ExecutiveDashboardSnapshot | null;
  errors: string[];
  meta?: AnalyticsResponseMeta | null;
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
  meta?: AnalyticsResponseMeta | null;
}

export interface AnalyticsDataQualityHealth {
  generatedAt: string;
  lookbackDays: number;
  windowFrom: string;
  windowTo: string;
  orphanArticleCount: number;
  totalRevenue: number;
  missingCostRevenue: number;
  missingCostRevenueSharePct: number | null;
  unknownSupplierRevenue: number;
  unknownSupplierRevenueSharePct: number | null;
  score: number;
  scoreStatus: "excellent" | "good" | "warning" | "critical";
  scoreSummary: string;
  thresholds: {
    orphanArticleCount: number;
    missingCostRevenueSharePct: number;
    unknownSupplierRevenueSharePct: number;
  };
  meta?: AnalyticsResponseMeta | null;
}

export interface DataQualityTopOffenderItem {
  sku?: string | null;
  productId: string;
  name?: string | null;
  supplierName?: string | null;
  shoeTypeName?: string | null;
  sales30d: number;
  revenueImpactRsd: number;
  revenueImpactPct: number;
  actionUrl?: string | null;
}

export interface DataQualityTopOffendersResult {
  issueType: DataQualityIssueType;
  limit: number;
  count: number;
  items: DataQualityTopOffenderItem[];
  meta?: AnalyticsResponseMeta | null;
}

export interface DataQualityTrendPoint {
  date: string;
  missingCostRevenueSharePct: number;
  unknownSupplierRevenueSharePct: number;
  orphanArticleCount: number;
}

export interface DataQualityTrendResult {
  days: number;
  dataScope: string;
  points: DataQualityTrendPoint[];
  meta?: AnalyticsResponseMeta | null;
}

export type PilotDataQualityReadinessStatus = "excellent" | "good" | "warning" | "critical" | string;

export interface PilotDataQualityIntakeLoadedData {
  articlesCount: number;
  saleItemsCount: number;
  receiptsCount: number;
  suppliersCount: number;
  storesCount: number;
  firstSaleDate: string | null;
  lastSaleDate: string | null;
}

export interface PilotDataQualityIntakeIssueItem {
  key: string;
  label: string;
  severity: "critical" | "warning" | string;
  count: number;
  impact: string;
}

export interface PilotDataQualityIntakeIssues {
  missingSupplierCount: number;
  missingCostCount: number;
  missingCategoryCount: number;
  missingColorCount?: number;
  missingSizeCount?: number;
  saleWithoutArticleCount: number;
  zeroOrNegativePriceCount: number;
  duplicateSkuCount?: number;
  missingSupplierNameCount: number;
}

export interface PilotDataQualityIntakeImpactItem {
  key: string;
  label: string;
  value: string;
  description: string;
}

export interface PilotDataQualityIntakeImpact {
  revenueWithoutCostPercent: number;
  articlesWithoutSupplierPercent: number;
  recommendationsBlockedCount: number;
  ignoredRowsCount: number;
  insufficientSignalCount: number;
}

export interface PilotDataQualityIntakeActionItem {
  priority: string;
  title: string;
  reason: string;
  nextStep: string;
}

export interface PilotDataQualityIntakeActions {
  items: PilotDataQualityIntakeActionItem[];
}

export interface PilotDataQualityIntakeReport {
  generatedAtUtc: string;
  periodFromUtc?: string | null;
  periodToUtc?: string | null;
  dataScope: string;
  storeId?: string | null;
  supplierId?: string | null;
  lastImportAtUtc?: string | null;
  lastRefreshAtUtc?: string | null;
  readinessStatus: PilotDataQualityReadinessStatus;
  readinessLabel: string;
  readinessScore: number;
  loadedData: PilotDataQualityIntakeLoadedData;
  issues: PilotDataQualityIntakeIssues;
  impact: PilotDataQualityIntakeImpact;
  recommendedActions: string[];
  meta?: AnalyticsResponseMeta | null;
}

export interface DurableReportRow {
  section: string;
  item: string;
  value: string;
  secondary?: string | null;
  note?: string | null;
}

export interface DurableReportSection {
  key: string;
  title?: string | null;
  rowCount: number;
  description?: string | null;
  emptyMessage?: string | null;
  columns?: DurableReportColumn[];
  rows?: Record<string, unknown>[];
}

export interface DurableReportColumn {
  key: string;
  label: string;
  dataType?: string;
}

export interface DurableReportKpi {
  key: string;
  label: string;
  value: unknown;
  unit?: string | null;
  tone?: string | null;
  note?: string | null;
}

export interface DurableReportAction {
  title: string;
  description: string;
  href: string;
  priority?: string;
}

export interface DurableReportMethodology {
  summary: string;
  notes: string[];
  sourceHints?: string[];
  metricKeys?: string[];
}

export interface DurableReportPayloadColumn {
  key: string;
  header: string;
  dataType?: string;
}

export interface DurableReportNamedValue {
  key: string;
  label: string;
  value: string;
}

export interface DurableResolvedReportPayload {
  tableKey: string;
  tableTitle: string;
  documentType: string;
  templateName: string;
  locale: string;
  columns: DurableReportPayloadColumn[];
  rows: DurableReportRow[];
  filters: DurableReportNamedValue[];
  metadata: DurableReportNamedValue[];
  templateVersion?: number;
}

export interface DurableReportPeriod {
  fromUtc: string;
  toUtc: string;
  label: string;
}

export interface ReportPeriod extends DurableReportPeriod {}

export interface ReportSection extends DurableReportSection {}

export interface ReportKpi {
  key: string;
  label: string;
  value: string;
  unit?: string | null;
  note?: string | null;
}

export interface ReportAction {
  priority: "P1" | "P2" | "P3" | string;
  title: string;
  reason?: string | null;
  nextStep?: string | null;
  href?: string | null;
}

export interface ReportMethodology {
  summary: string;
  notes?: string[];
}

export interface AnalyticsReportResponse {
  reportId: string;
  stableQueryUrl: string;
  reportTitle?: string;
  reportType?: string;
  generatedAtUtc: string;
  periodFrom?: string;
  periodTo?: string;
  period: ReportPeriod;
  lastRefreshAtUtc?: string | null;
  dataQualityStatus: string;
  recommendationAllowed?: boolean;
  usedFallback?: boolean;
  warnings?: string[];
  methodology: string;
  rows: DurableReportRow[];
  sections: ReportSection[];
  payload: DurableResolvedReportPayload;
  meta?: AnalyticsResponseMeta | null;
}

export interface SupplierDecisionDurableReport {
  reportId: string;
  stableQueryUrl: string;
  title?: string;
  type?: string;
  reportTitle?: string;
  reportType?: string;
  generatedAtUtc: string;
  periodFrom?: string;
  periodTo?: string;
  period: DurableReportPeriod;
  lastRefreshAtUtc?: string | null;
  dataFreshnessStatus?: string | null;
  dataQualityStatus: string;
  recommendationAllowed: boolean;
  usedFallback: boolean;
  fallbackReason?: string | null;
  warnings?: string[];
  kpis?: DurableReportKpi[];
  recommendedActions?: DurableReportAction[];
  methodology: DurableReportMethodology | string;
  methodologySummary?: string | null;
  rows: DurableReportRow[];
  sections: DurableReportSection[];
  payload: DurableResolvedReportPayload;
  meta?: AnalyticsResponseMeta | null;
}

export interface PilotIntakeDurableReport {
  reportId: string;
  stableQueryUrl: string;
  title?: string;
  type?: string;
  reportTitle?: string;
  reportType?: string;
  generatedAtUtc: string;
  periodFrom?: string;
  periodTo?: string;
  period: DurableReportPeriod;
  lastRefreshAtUtc?: string | null;
  dataFreshnessStatus?: string | null;
  dataQualityStatus: string;
  recommendationAllowed?: boolean;
  usedFallback?: boolean;
  fallbackReason?: string | null;
  warnings?: string[];
  kpis?: DurableReportKpi[];
  recommendedActions?: DurableReportAction[];
  methodology: DurableReportMethodology | string;
  methodologySummary?: string | null;
  rows: DurableReportRow[];
  sections: DurableReportSection[];
  payload: DurableResolvedReportPayload;
  meta?: AnalyticsResponseMeta | null;
}

export interface InventoryBalance {
  totalSku: number;
  totalOnHand: number;
  lowStockCount: number;
  outOfStockCount: number;
  estimatedInventoryValue?: number | null;
  meta?: AnalyticsResponseMeta | null;
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
  stockCoverDays?: number | null;
  stockCoverStatus?: string | null;
  stockCoverStatusLabel?: string | null;
  sellThroughRatio?: number | null;
  sellThroughStatus?: string | null;
  sellThroughStatusLabel?: string | null;
  signalConfidencePct?: number | null;
  recommendationAllowed?: boolean | null;
  reasonCodes?: string[] | null;
  dataQualityStatus?: string | null;
}

export interface InventoryPagedResponse {
  items: InventoryListItem[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  meta?: AnalyticsResponseMeta | null;
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
  meta?: AnalyticsResponseMeta | null;
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
  meta?: AnalyticsResponseMeta | null;
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
  meta?: AnalyticsResponseMeta | null;
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

// ── Analytics Action Queue ─────────────────────────────────────────────────

export type AnalyticsActionSourceType =
  | "dashboard"
  | "product"
  | "supplier"
  | "inventory"
  | "nivelacija"
  | "data_quality";

export type AnalyticsActionStatus =
  | "new"
  | "accepted"
  | "deferred"
  | "rejected"
  | "done";

export type AnalyticsActionPriority = "P1" | "P2" | "P3";

export type AnalyticsActionDataQualityStatus =
  | "good"
  | "warning"
  | "critical"
  | "insufficient_data";

export type AnalyticsActionLegacyDataQualityStatus = "fair" | "poor";
export type AnalyticsActionAnyDataQualityStatus =
  | AnalyticsActionDataQualityStatus
  | AnalyticsActionLegacyDataQualityStatus;

export interface AnalyticsActionNote {
  id: number;
  actionItemId: number;
  statusFrom: AnalyticsActionStatus;
  statusTo: AnalyticsActionStatus;
  note?: string | null;
  createdAtUtc: string;
  createdByUserId?: string | null;
  createdByUserName?: string | null;
}

export interface AnalyticsActionCreationSnapshot {
  sourceRecommendationId?: string | null;
  recommendationType?: string | null;
  expectedImpactBasis?: string | null;
  impactWindowDays?: number | null;
  confidenceLevel?: "high" | "medium" | "low" | "insufficient_data" | string | null;
  warningCodes: string[];
  primaryDrivers: string[];
  decisionReason?: string | null;
  recommendedAction?: string | null;
  generatedAtUtc?: string | null;
  inputFreshnessStatus?: "fresh" | "stale" | "critical" | "unknown" | string | null;
}

export interface AnalyticsActionResolutionSnapshot {
  measuredWindowDays?: number | null;
  evidenceSource?: string | null;
  evidenceReference?: string | null;
  resolutionNote?: string | null;
}

export interface AnalyticsActionLedgerSnapshot {
  schemaVersion: number;
  creationSnapshot?: AnalyticsActionCreationSnapshot | null;
  resolutionSnapshot?: AnalyticsActionResolutionSnapshot | null;
}

export interface AnalyticsActionItem {
  id: number;
  sourceType: AnalyticsActionSourceType;
  sourceKey: string;
  sourceId?: number | null;
  title: string;
  description?: string | null;
  recommendationStatus?: string | null;
  priority: AnalyticsActionPriority;
  impactEstimateRsd?: number | null;
  dueAtUtc?: string | null;
  expectedImpactRsd?: number | null;
  measuredImpactRsd?: number | null;
  outcomeStatus?: "pending" | "success" | "neutral" | "negative" | "not_measured" | string | null;
  outcomeMeasuredAtUtc?: string | null;
  outcomeNotes?: string | null;
  confidencePct?: number | null;
  reliabilityPct?: number | null;
  dataQualityStatus?: AnalyticsActionAnyDataQualityStatus | null;
  status: AnalyticsActionStatus;
  actionUrl?: string | null;
  metadataJson?: string | null;
  ledgerSnapshot?: AnalyticsActionLedgerSnapshot | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  resolvedAtUtc?: string | null;
  createdByUserId?: string | null;
  updatedByUserId?: string | null;
  updatedByUserName?: string | null;
  notes?: AnalyticsActionNote[];
}

export interface AnalyticsActionListResponse {
  items: AnalyticsActionItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AnalyticsActionCounts {
  new: number;
  accepted: number;
  deferred: number;
  rejected: number;
  done: number;
  p1Open: number;
}

export interface AnalyticsActionUpsertInput {
  sourceType: AnalyticsActionSourceType;
  sourceKey: string;
  sourceId?: number | null;
  title: string;
  description?: string | null;
  recommendationStatus?: string | null;
  priority: AnalyticsActionPriority;
  impactEstimateRsd?: number | null;
  dueAtUtc?: string | null;
  expectedImpactRsd?: number | null;
  confidencePct?: number | null;
  reliabilityPct?: number | null;
  dataQualityStatus?: AnalyticsActionDataQualityStatus | null;
  actionUrl?: string | null;
  metadataJson?: string | null;
}

export interface AnalyticsActionStatusUpdateInput {
  status: AnalyticsActionStatus;
  note?: string | null;
}

export interface AnalyticsActionOutcomeUpdateInput {
  outcomeStatus: "pending" | "success" | "neutral" | "negative" | "not_measured";
  measuredImpactRsd?: number | null;
  outcomeMeasuredAtUtc?: string | null;
  outcomeNotes?: string | null;
}

export interface AnalyticsActionOutcomeSummaryFilters {
  createdFrom?: string;
  createdTo?: string;
  resolvedFrom?: string;
  resolvedTo?: string;
  measuredFrom?: string;
  measuredTo?: string;
  sourceType?: AnalyticsActionSourceType;
  priority?: AnalyticsActionPriority;
  dataQualityStatus?: AnalyticsActionDataQualityStatus;
}

export interface AnalyticsActionOutcomeSummaryMeta {
  success: boolean;
  periodMode: string;
  createdFrom?: string | null;
  createdTo?: string | null;
  resolvedFrom?: string | null;
  resolvedTo?: string | null;
  measuredFrom?: string | null;
  measuredTo?: string | null;
  generatedAtUtc: string;
  sampleSize: number;
  measuredSampleSize: number;
  warnings: string[];
  emptyReason?: string | null;
}

export interface AnalyticsActionOutcomeSummaryTotals {
  createdCount: number;
  closedCount: number;
  openCount: number;
  measuredCount: number;
  pendingOutcomeCount: number;
  successCount: number;
  neutralCount: number;
  negativeCount: number;
  notMeasuredCount: number;
  outcomeCoverageRate?: number | null;
  positiveOutcomeRate?: number | null;
  negativeOutcomeRate?: number | null;
}

export interface AnalyticsActionOutcomeSummaryImpact {
  expectedImpactRsd?: number | null;
  measuredImpactRsd?: number | null;
  realizationRatio?: number | null;
  measuredImpactSampleCount: number;
}

export interface AnalyticsActionOutcomeSummaryBucket {
  key: string;
  label: string;
  totalCount: number;
  closedCount: number;
  measuredCount: number;
  pendingOutcomeCount: number;
  successCount: number;
  neutralCount: number;
  negativeCount: number;
  notMeasuredCount: number;
  expectedImpactRsd?: number | null;
  measuredImpactRsd?: number | null;
  outcomeCoverageRate?: number | null;
  positiveOutcomeRate?: number | null;
  negativeOutcomeRate?: number | null;
  realizationRatio?: number | null;
  measuredImpactSampleCount: number;
  warningCodes: string[];
}

export interface AnalyticsActionOutcomeSummaryResponse {
  meta: AnalyticsActionOutcomeSummaryMeta;
  totals: AnalyticsActionOutcomeSummaryTotals;
  impact: AnalyticsActionOutcomeSummaryImpact;
  bySourceType: AnalyticsActionOutcomeSummaryBucket[];
  byPriority: AnalyticsActionOutcomeSummaryBucket[];
  byOutcomeStatus: AnalyticsActionOutcomeSummaryBucket[];
  byDataQuality: AnalyticsActionOutcomeSummaryBucket[];
  byConfidenceBucket: AnalyticsActionOutcomeSummaryBucket[];
  byReliabilityBucket: AnalyticsActionOutcomeSummaryBucket[];
}

export interface AnalyticsActionFilters {
  status?: AnalyticsActionStatus;
  priority?: AnalyticsActionPriority;
  sourceType?: AnalyticsActionSourceType;
  dataQualityStatus?: AnalyticsActionDataQualityStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}
