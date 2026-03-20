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
