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
  velicina?: string | null;  // Veli?ina cipela
  boja?: string | null;      // Boja cipela
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
