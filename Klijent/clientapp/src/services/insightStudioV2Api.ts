import { makeUrl } from "./analyticsApi";

// ====================== TYPES ======================

export interface HeatmapCell {
  day: number;
  dayName: string;
  weekStart: string;
  revenue: number;
  units: number;
  transactions: number;
}

export interface DayAggregate {
  day: number;
  dayName: string;
  avgRevenue: number;
  avgUnits: number;
  totalRevenue: number;
  peakWeek: string;
}

export interface WeeklyHeatmap {
  cells: HeatmapCell[];
  byDay: DayAggregate[];
}

export interface BasketPair {
  categoryA: string;
  categoryB: string;
  coOccurrences: number;
  supportPct: number;
}

export interface BasketAffinity {
  pairs: BasketPair[];
  totalMultiItemTransactions: number;
}

export interface VelocityMarginItem {
  artikalId: number;
  naziv: string;
  kategorija: string;
  pol: string;
  totalRevenue: number;
  totalUnits: number;
  marginPct: number;
  velocity: number;
  quadrant: "STAR" | "NICHE_GEM" | "VOLUME_TRAP" | "DEAD_WEIGHT";
}

export interface VelocityMarginMatrix {
  items: VelocityMarginItem[];
  medianMargin: number;
  medianVelocity: number;
  quadrantCounts: {
    stars: number;
    nicheGems: number;
    volumeTraps: number;
    deadWeight: number;
  };
}

export interface LifecycleItem {
  artikalId: number;
  naziv: string;
  kategorija: string;
  pol: string;
  totalUnits: number;
  totalRevenue: number;
  firstHalfUnits: number;
  secondHalfUnits: number;
  trendPct: number;
  stage: "LAUNCH" | "GROWTH" | "MATURE" | "DECLINE";
  currentStock: number;
}

export interface LifecycleResult {
  items: LifecycleItem[];
  summary: {
    launch: number;
    growth: number;
    mature: number;
    decline: number;
  };
}

export interface DepletionForecast {
  artikalId: number;
  naziv: string;
  kategorija: string;
  currentStock: number;
  avgDailySales: number;
  daysUntilOOS: number;
  depletionDate: string;
  atRiskRevenue: number;
  marginPct: number;
  severity: "CRITICAL" | "WARNING" | "WATCH" | "OK";
}

export interface DepletionResult {
  forecasts: DepletionForecast[];
  totalAtRiskRevenue: number;
  criticalCount: number;
}

export interface MarginAlert {
  artikalId: number;
  naziv: string;
  kategorija: string;
  marginPct: number;
  priceDropPct: number;
  totalRevenue: number;
  totalUnits: number;
  nabavnaCena: number;
  prodajnaCena: number;
  alertType: "NEGATIVE_MARGIN" | "LOW_MARGIN" | "HEAVY_MARKDOWN";
  lostMargin: number;
}

export interface MarginAlertResult {
  alerts: MarginAlert[];
  summary: {
    negativeMarginCount: number;
    lowMarginCount: number;
    heavyMarkdownCount: number;
    totalLostMargin: number;
  };
}

export interface CategoryChange {
  kategorija: string;
  thisWeekRevenue: number;
  lastWeekRevenue: number;
  changePct: number;
}

export interface WeeklyChangelog {
  thisWeekRevenue: number;
  lastWeekRevenue: number;
  revenueChangePct: number;
  thisWeekUnits: number;
  lastWeekUnits: number;
  unitChangePct: number;
  thisWeekTransactions: number;
  lastWeekTransactions: number;
  categoryChanges: CategoryChange[];
  oosCount: number;
  priceChangesThisWeek: number;
}

export interface SupplierScoreV2 {
  dobavljacId: number | null;
  dobavljacNaziv: string;
  totalRevenue: number;
  totalUnits: number;
  marginPct: number;
  uniqueProducts: number;
  uniqueCategories: number;
  dependency: number;
  velocity: number;
  unsoldStock: number;
  returnRate: number;
  profitScore: number;
  velocityScore: number;
  diversityScore: number;
  reliabilityScore: number;
  compositeScore: number;
  tier: "GOLD" | "SILVER" | "BRONZE" | "AT_RISK";
}

export interface SmartReorderItem {
  artikalId: number;
  naziv: string;
  kategorija: string;
  pol: string;
  dobavljacNaziv: string;
  currentStock: number;
  totalSold: number;
  avgDailySales: number;
  doh: number;
  rop: number;
  needsReorder: boolean;
  recommendedQty: number;
  urgency: "KRITIČNO" | "HITNO" | "PREPORUČUJE SE" | "OK";
  marginPct: number;
  reorderCost: number;
  expectedRevenue: number;
  expectedProfit: number;
  reorderProbability: number;
  prodajnaCena: number | null;
}

export interface CategoryPlan {
  kategorija: string;
  totalItems: number;
  criticalCount: number;
  urgentCount: number;
  totalReorderCost: number;
  expectedRevenue: number;
  avgMargin: number;
}

export interface SupplierPlan {
  dobavljac: string;
  totalItems: number;
  criticalCount: number;
  totalReorderCost: number;
  avgReorderProbability: number;
}

export interface SmartReorderResult {
  items: SmartReorderItem[];
  byCategoryPlan: CategoryPlan[];
  bySupplierPlan: SupplierPlan[];
  summary: {
    criticalCount: number;
    urgentCount: number;
    recommendedCount: number;
    totalReorderCost: number;
    expectedRevenueFromReorder: number;
    expectedProfitFromReorder: number;
  };
}

export interface PriceBand {
  priceBand: string;
  skuCount: number;
  totalUnits: number;
  avgVelocityPerSku: number;
  avgPrice: number;
  avgMarginPct: number;
  totalStock: number;
  markdownCount: number;
  elasticity: string;
}

export interface PriceSensitivity {
  bands: PriceBand[];
}

// ====================== API FUNCTIONS ======================

function buildDateParams(fromDate?: string, toDate?: string): URLSearchParams {
  const params = new URLSearchParams();
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);
  return params;
}

export async function getWeeklyHeatmap(fromDate?: string, toDate?: string): Promise<WeeklyHeatmap> {
  const res = await fetch(makeUrl("/api/analytics/advanced/v2/weekly-heatmap", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška weekly heatmap");
  return res.json();
}

export async function getBasketAffinity(fromDate?: string, toDate?: string): Promise<BasketAffinity> {
  const res = await fetch(makeUrl("/api/analytics/advanced/v2/basket-affinity", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška basket affinity");
  return res.json();
}

export async function getVelocityMarginMatrix(fromDate?: string, toDate?: string): Promise<VelocityMarginMatrix> {
  const res = await fetch(makeUrl("/api/analytics/advanced/v2/velocity-margin-matrix", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška velocity-margin matrix");
  return res.json();
}

export async function getProductLifecycle(fromDate?: string, toDate?: string): Promise<LifecycleResult> {
  const res = await fetch(makeUrl("/api/analytics/advanced/v2/product-lifecycle", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška product lifecycle");
  return res.json();
}

export async function getStockDepletionForecast(fromDate?: string, toDate?: string): Promise<DepletionResult> {
  const res = await fetch(makeUrl("/api/analytics/advanced/v2/stock-depletion-forecast", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška stock depletion");
  return res.json();
}

export async function getMarginAlerts(fromDate?: string, toDate?: string): Promise<MarginAlertResult> {
  const res = await fetch(makeUrl("/api/analytics/advanced/v2/margin-alerts", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška margin alerts");
  return res.json();
}

export async function getWeeklyChangelog(): Promise<WeeklyChangelog> {
  const res = await fetch(makeUrl("/api/analytics/advanced/v2/weekly-changelog"));
  if (!res.ok) throw new Error("Greška weekly changelog");
  return res.json();
}

export async function getSupplierScoringV2(fromDate?: string, toDate?: string): Promise<SupplierScoreV2[]> {
  const res = await fetch(makeUrl("/api/analytics/advanced/v2/supplier-scoring-v2", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška supplier scoring v2");
  return res.json();
}

export async function getSmartReorder(fromDate?: string, toDate?: string): Promise<SmartReorderResult> {
  const res = await fetch(makeUrl("/api/analytics/advanced/v2/smart-reorder", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška smart reorder");
  return res.json();
}

export async function getPriceSensitivity(): Promise<PriceSensitivity> {
  const res = await fetch(makeUrl("/api/analytics/advanced/v2/price-sensitivity"));
  if (!res.ok) throw new Error("Greška price sensitivity");
  return res.json();
}
