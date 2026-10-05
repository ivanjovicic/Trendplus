import { makeUrl } from "./analyticsApi";

// ====================== TYPES ======================

export interface InsightStudioTrustMeta {
  success: boolean;
  emptyReason?: string | null;
  dataQualityStatus?: string | null;
  warningCode?: string | null;
  warningMessage?: string | null;
}

export interface KpiSnapshot {
  revenue: number;
  revenueChange: number;
  units: number;
  unitsChange: number;
  transactions: number;
  marginPct: number;
  marginDataCoveragePct: number | null;
  revenueWithCost: number | null;
  oosCount: number;
  lowStockCount: number;
  sparkline: { date: string; revenue: number }[];
}

export interface SupplierScore {
  dobavljacId: number | null;
  dobavljacNaziv: string;
  totalRevenue: number;
  totalUnits: number;
  marginPct: number;
  marginDataCoveragePct?: number | null;
  revenueWithCost?: number | null;
  systemBenchmarkAvailable?: boolean;
  systemMarginPct?: number | null;
  uniqueProducts: number;
  uniqueCategories: number;
  dependencyRatio: number;
  profitScore: number;
  diversityScore: number;
  dependencyScore: number;
  compositeScore: number;
  riskLevel: "LOW" | "MED" | "HIGH";
}

export interface AbcItem {
  artikalId: number;
  naziv: string;
  kategorija: string;
  pol: string;
  totalRevenue: number;
  totalUnits: number;
  revPct: number;
  cumulativePct: number;
  abcClass: "A" | "B" | "C";
}

export interface AbcResult {
  items: AbcItem[];
  summary: {
    countA: number;
    countB: number;
    countC: number;
    revenueA: number;
    revenueB: number;
    revenueC: number;
  };
  meta?: InsightStudioTrustMeta;
}

export interface AgingItem {
  id: number;
  naziv: string;
  kategorija: string;
  pol: string;
  kolicina: number;
  stockValue: number | null;
  marginDataAvailable?: boolean;
  dobavljacNaziv: string;
  neverSold?: boolean;
  agingEvidenceStatus?: "never_sold" | "last_sale" | string;
  lastSaleDate: string | null;
  daysWithoutSale: number;
  agingCategory: "Aktivno" | "Pazi" | "Upozorenje" | "Kritično";
}

export interface AgingResult {
  items: AgingItem[];
  summary: {
    totalSKU: number;
    critical: number;
    warning: number;
    watch: number;
    active: number;
    criticalStockValue: number;
  };
}

export interface DailyAnalysis {
  analysisDate: string;
  targetDataStatus?: "present" | "missing";
  targetRevenue: number | null;
  targetUnits: number | null;
  meanRevenue: number;
  baselineSampleSize?: number;
  baselineWarning?: string | null;
  zScore: number | null;
  isOutlier: boolean;
  isExtremeOutlier: boolean;
  outlierLabel?: string;
  dailyData: { date: string; revenue: number; units: number; isTarget: boolean }[];
  top5Articles: {
    artikalId: number;
    naziv: string;
    kategorija: string;
    units: number;
    revenue: number;
  }[];
}

export type CategoryRevenueBasis = "booked_sales" | "estimated_velocity_price";

export interface CategoryStat {
  kategorija: string;
  totalRevenue: number;
  totalUnits: number;
  marginPct: number;
  marginDataCoveragePct?: number | null;
  revenueWithCost?: number | null;
  profitLift: number | null;
  /** Revenue share in percent units (25 = 25%). Not a 0–1 ratio. */
  revShare: number | null;
  velocity: number;
  velocityDenominatorBasis?: string;
  revenueBasis?: CategoryRevenueBasis;
  estimated?: boolean;
  uniqueSKU: number;
}

export interface GenderStat {
  pol: string;
  totalRevenue: number;
  totalUnits: number;
  /** Revenue share in percent units (25 = 25%). Not a 0–1 ratio. */
  revShare: number;
}

export interface CategoryIntelligence {
  byCategory: CategoryStat[];
  byGender: GenderStat[];
  velocityDenominatorBasis?: string;
  systemBenchmarkAvailable?: boolean;
  systemMarginPct?: number | null;
}

export interface ReorderItem {
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
  prodajnaCena: number | null;
  potentialRevenueRsd?: number;
  estimatedProcurementCostRsd?: number | null;
  costCoveragePct?: number;
}

export interface ReorderPlan {
  items: ReorderItem[];
  summary: {
    criticalCount: number;
    urgentCount: number;
    recommendedCount: number;
    potentialRevenueRsd?: number;
    estimatedProcurementCostRsd?: number | null;
    costCoveragePct?: number;
    reorderValueBasis?: string;
    totalReorderValue: number;
  };
}

// ====================== API FUNCTIONS ======================

function buildDateParams(fromDate?: string, toDate?: string): URLSearchParams {
  const params = new URLSearchParams();
  if (fromDate) params.append("fromDate", fromDate);
  if (toDate) params.append("toDate", toDate);
  return params;
}

export async function getKpiSnapshot(fromDate?: string, toDate?: string): Promise<KpiSnapshot> {
  const res = await fetch(makeUrl("/api/analytics/advanced/kpi-snapshot", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška pri učitavanju KPI snapshot");
  return res.json();
}

export async function getSupplierScorecard(fromDate?: string, toDate?: string): Promise<SupplierScore[]> {
  const res = await fetch(makeUrl("/api/analytics/advanced/supplier-scorecard", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška pri učitavanju supplier scorecard");
  return res.json();
}

export async function getAbcClassification(fromDate?: string, toDate?: string): Promise<AbcResult> {
  const res = await fetch(makeUrl("/api/analytics/advanced/abc-classification", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška pri učitavanju ABC klasifikacije");
  return res.json();
}

export async function getAgingStock(): Promise<AgingResult> {
  const res = await fetch(makeUrl("/api/analytics/advanced/aging-stock"));
  if (!res.ok) throw new Error("Greška pri učitavanju aging stock");
  return res.json();
}

export async function getDailyAnalysis(
  analysisDate?: string,
  fromDate?: string,
  toDate?: string
): Promise<DailyAnalysis> {
  const params = buildDateParams(fromDate, toDate);
  if (analysisDate) params.append("analysisDate", analysisDate);
  const res = await fetch(makeUrl("/api/analytics/advanced/daily-analysis", params));
  if (!res.ok) throw new Error("Greška pri učitavanju dnevne analize");
  return res.json();
}

export async function getCategoryIntelligence(fromDate?: string, toDate?: string): Promise<CategoryIntelligence> {
  const res = await fetch(makeUrl("/api/analytics/advanced/category-intelligence", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška pri učitavanju category intelligence");
  return res.json();
}

export async function getReorderPlan(fromDate?: string, toDate?: string): Promise<ReorderPlan> {
  const res = await fetch(makeUrl("/api/analytics/advanced/reorder-plan", buildDateParams(fromDate, toDate)));
  if (!res.ok) throw new Error("Greška pri učitavanju plana nabavke");
  return res.json();
}
