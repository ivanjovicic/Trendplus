import { useCallback, useEffect, useMemo, useState, type CSSProperties } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ReferenceLine,
  ResponsiveContainer,
  Scatter,
  ScatterChart,
  Tooltip,
  XAxis,
  YAxis,
  ZAxis,
} from "recharts";
import {
  getAbcClassification,
  getAgingStock,
  getCategoryIntelligence,
  getDailyAnalysis,
  getKpiSnapshot,
  getReorderPlan,
  getSupplierScorecard,
  type AbcItem,
  type AgingItem,
  type CategoryStat,
  type DailyAnalysis,
  type GenderStat,
  type KpiSnapshot,
  type ReorderItem,
  type SupplierScore,
} from "../services/insightStudioApi";
import {
  getWeeklyHeatmap,
  getBasketAffinity,
  getVelocityMarginMatrix,
  getProductLifecycle,
  getStockDepletionForecast,
  getMarginAlerts,
  getWeeklyChangelog,
  getSupplierScoringV2,
  getSmartReorder,
  getPriceSensitivity,
  type WeeklyHeatmap,
  type BasketAffinity,
  type VelocityMarginMatrix,
  type VelocityMarginItem,
  type LifecycleResult,
  type DepletionResult,
  type MarginAlertResult,
  type WeeklyChangelog,
  type SupplierScoreV2,
  type SmartReorderResult,
  type PriceSensitivity,
} from "../services/insightStudioV2Api";
import IntelligenceSnapshotPanel from "../components/dashboard/IntelligenceSnapshotPanel";
import InfoTip from "../components/ui/InfoTip";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import {
  getDemandSignals,
  getDemandSignalsSample,
  getInventoryRiskSignals,
  getInventoryRiskSignalsSample,
  getPriceIntelligence,
  getPriceIntelligenceSample,
  getTrendMomentum,
  getTrendMomentumSample,
  type DemandSignalItem,
  type InventoryRiskSignalItem,
  type PriceIntelligenceItem,
  type TrendMomentumItem,
} from "../services/analyticsIntelligenceApi";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import {
  buildLegacyReorderFallbackFromSignals,
  mergeAgingAsPrimary,
  mergeCategorySignalsAsPrimary,
  mergeDepletionAsPrimary,
  mergePriceSensitivityAsPrimary,
  mergeSmartReorderAsPrimary,
} from "../services/analyticsIntelligenceDerived";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";

// ══════════════════════════════════════════════════════════════════
// TYPES & CONSTANTS
// ══════════════════════════════════════════════════════════════════

type TabKey = "pregled" | "dobavljaci" | "kategorije" | "matrica" | "dnevna" | "abc" | "zalihe" | "nabavka";

const TABS: { key: TabKey; label: string; icon: string }[] = [
  { key: "pregled", label: "Pregled", icon: "🎯" },
  { key: "dobavljaci", label: "Dobavljači", icon: "🏭" },
  { key: "kategorije", label: "Kategorije", icon: "👟" },
  { key: "matrica", label: "Matrica V×M", icon: "📐" },
  { key: "dnevna", label: "Dnevna", icon: "📅" },
  { key: "abc", label: "ABC / Životni Ciklus", icon: "📊" },
  { key: "zalihe", label: "Zalihe & Deplecija", icon: "📦" },
  { key: "nabavka", label: "Nabavka 2.0", icon: "🛒" },
];

const TAB_TIPS: Record<TabKey, string> = {
  pregled: "Kratki pregled KPI i upozorenja za brzu akciju.",
  dobavljaci: "Ocena dobavljača, skorovi i rizici.",
  kategorije: "Analiza po kategorijama i osvetljavanje profitabilnosti.",
  matrica: "V×M matrica za identifikaciju zvezda i zamki volumena.",
  dnevna: "Dnevna analiza prodaje i promene.",
  abc: "ABC klasifikacija i lifecycle za SKU.",
  zalihe: "Pregled zaliha i predviđanje deplecije.",
  nabavka: "Predlozi prioriteta nabavke i reorder planovi.",
};

const PERIOD_PRESETS = [
  { label: "7d", days: 7 },
  { label: "30d", days: 30 },
  { label: "90d", days: 90 },
  { label: "6m", days: 180 },
  { label: "1g", days: 365 },
];

const PAL = {
  blue: "var(--c-4f8ef7, var(--theme-color-4f8ef7, #4F8EF7))",
  green: "var(--c-4caf82, var(--theme-color-4caf82, #4CAF82))",
  yellow: "var(--c-f5c542, var(--theme-color-f5c542, #F5C542))",
  orange: "var(--c-f97316, var(--theme-color-f97316, #F97316))",
  red: "var(--c-e05c5c, var(--theme-color-e05c5c, #E05C5C))",
  purple: "var(--c-9b72cf, var(--theme-color-9b72cf, #9B72CF))",
  cyan: "var(--c-22d3ee, var(--theme-color-22d3ee, #22D3EE))",
  pink: "var(--c-f472b6, var(--theme-color-f472b6, #F472B6))",
  bg: "var(--c-0d0f14, var(--theme-color-0d0f14, #0D0F14))",
  card: "var(--c-161a23, var(--theme-color-161a23, #161A23))",
  cardHover: "var(--c-1e2332, var(--theme-color-1e2332, #1E2332))",
  border: "var(--c-2a3045, var(--theme-color-2a3045, #2A3045))",
  textPrimary: "var(--c-e8ecf4, var(--theme-color-e8ecf4, #E8ECF4))",
  textSecondary: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))",
  textMuted: "var(--c-4a5270, var(--theme-color-4a5270, #4A5270))",
};

const DONUT_COLORS = [PAL.blue, PAL.yellow, PAL.green, PAL.purple, PAL.red, PAL.orange, PAL.cyan, PAL.pink];

const TIER_COLORS: Record<string, string> = { GOLD: "var(--c-f5c542, var(--theme-color-f5c542, #F5C542))", SILVER: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", BRONZE: "var(--c-f97316, var(--theme-color-f97316, #F97316))", AT_RISK: "var(--c-e05c5c, var(--theme-color-e05c5c, #E05C5C))" };
const TIER_LABELS: Record<string, string> = { GOLD: "Zlato", SILVER: "Srebro", BRONZE: "Bronza", AT_RISK: "Rizik" };

const QUAD_COLORS: Record<string, string> = { STAR: PAL.green, NICHE_GEM: PAL.purple, VOLUME_TRAP: PAL.yellow, DEAD_WEIGHT: PAL.red };
const QUAD_LABELS: Record<string, string> = { STAR: "⭐ Zvezda", NICHE_GEM: "💎 Niša", VOLUME_TRAP: "⚡ Volume Trap", DEAD_WEIGHT: "⚠ Mrtav Teg" };

const STAGE_COLORS: Record<string, string> = { LAUNCH: PAL.cyan, GROWTH: PAL.green, MATURE: PAL.yellow, DECLINE: PAL.red };
const STAGE_LABELS: Record<string, string> = { LAUNCH: "Lansiranje", GROWTH: "Rast", MATURE: "Zrelost", DECLINE: "Pad" };

const SEVERITY_COLORS: Record<string, string> = { CRITICAL: PAL.red, WARNING: PAL.orange, WATCH: PAL.yellow, OK: PAL.green };

const URGENCY_COLORS: Record<string, string> = { "KRITIČNO": PAL.red, HITNO: PAL.orange, "PREPORUČUJE SE": PAL.yellow, OK: PAL.green };

const AGING_COLORS: Record<string, string> = { Aktivno: PAL.green, Pazi: PAL.yellow, Upozorenje: PAL.orange, "Kritično": PAL.red };

const ABC_COLORS: Record<string, string> = { A: PAL.green, B: PAL.yellow, C: PAL.red };

type InsightAnalyticsContext = {
  filters: AnalyticsNamedValue[];
  metadata: AnalyticsNamedValue[];
  openSnapshotDetail: <Row>(
    table: string,
    recordId: string,
    title: string,
    subtitle: string,
    columns: AnalyticsTableColumn<Row>[],
    row: Row
  ) => void;
};

const supplierV2Columns: AnalyticsTableColumn<SupplierScoreV2>[] = [
  { key: "dobavljacId", header: "Dobavljac ID", dataType: "number" },
  { key: "dobavljacNaziv", header: "Dobavljac", dataType: "text" },
  { key: "totalRevenue", header: "Prihod", dataType: "currency" },
  { key: "marginPct", header: "Marza %", dataType: "percent" },
  { key: "velocity", header: "Velocity", dataType: "number" },
  { key: "unsoldStock", header: "Neprodato", dataType: "number" },
  { key: "tier", header: "Tier", dataType: "text" },
  { key: "compositeScore", header: "Score", dataType: "number" },
];

const supplierV1Columns: AnalyticsTableColumn<SupplierScore>[] = [
  { key: "dobavljacId", header: "Dobavljac ID", dataType: "number" },
  { key: "dobavljacNaziv", header: "Dobavljac", dataType: "text" },
  { key: "totalRevenue", header: "Prihod", dataType: "currency" },
  { key: "marginPct", header: "Marza %", dataType: "percent" },
  { key: "riskLevel", header: "Risk", dataType: "text" },
  { key: "compositeScore", header: "Score", dataType: "number" },
];

const categoryColumns: AnalyticsTableColumn<CategoryStat>[] = [
  { key: "kategorija", header: "Kategorija", dataType: "text" },
  { key: "totalRevenue", header: "Prihod", dataType: "currency" },
  { key: "revShare", header: "Udeo %", dataType: "percent" },
  { key: "marginPct", header: "Marza %", dataType: "percent" },
  { key: "profitLift", header: "Lift", dataType: "percent" },
  { key: "velocity", header: "Velocity", dataType: "number" },
  { key: "uniqueSKU", header: "SKU", dataType: "number" },
];

const genderColumns: AnalyticsTableColumn<GenderStat>[] = [
  { key: "pol", header: "Pol", dataType: "text" },
  { key: "totalRevenue", header: "Prihod", dataType: "currency" },
  { key: "revShare", header: "Udeo %", dataType: "percent" },
  { key: "totalUnits", header: "Kom", dataType: "number" },
];

const priceSensitivityColumns: AnalyticsTableColumn<PriceSensitivity["bands"][number]>[] = [
  { key: "priceBand", header: "Opseg", dataType: "text" },
  { key: "skuCount", header: "SKU", dataType: "number" },
  { key: "totalUnits", header: "Prodato", dataType: "number" },
  { key: "avgVelocityPerSku", header: "Vel/SKU", dataType: "number" },
  { key: "avgPrice", header: "Avg cena", dataType: "currency" },
  { key: "avgMarginPct", header: "Avg marza %", dataType: "percent" },
  { key: "totalStock", header: "Zaliha", dataType: "number" },
  { key: "markdownCount", header: "Niv.", dataType: "number" },
  { key: "elasticity", header: "Elasticnost", dataType: "text" },
];

const abcColumns: AnalyticsTableColumn<AbcItem>[] = [
  { key: "artikalId", header: "Artikal ID", dataType: "number" },
  { key: "naziv", header: "Artikal", dataType: "text" },
  { key: "kategorija", header: "Kategorija", dataType: "text" },
  { key: "totalRevenue", header: "Prihod", dataType: "currency" },
  { key: "revPct", header: "Udeo %", dataType: "percent" },
  { key: "cumulativePct", header: "Kum. %", dataType: "percent" },
  { key: "totalUnits", header: "Kom", dataType: "number" },
  { key: "abcClass", header: "Klasa", dataType: "text" },
];

const lifecycleColumns: AnalyticsTableColumn<LifecycleResult["items"][number]>[] = [
  { key: "artikalId", header: "Artikal ID", dataType: "number" },
  { key: "naziv", header: "Artikal", dataType: "text" },
  { key: "kategorija", header: "Kategorija", dataType: "text" },
  { key: "totalUnits", header: "Prodato", dataType: "number" },
  { key: "trendPct", header: "Trend %", dataType: "percent" },
  { key: "currentStock", header: "Zaliha", dataType: "number" },
  { key: "stage", header: "Faza", dataType: "text" },
];

const agingColumns: AnalyticsTableColumn<AgingItem>[] = [
  { key: "id", header: "Artikal ID", dataType: "number" },
  { key: "naziv", header: "Artikal", dataType: "text" },
  { key: "kategorija", header: "Kategorija", dataType: "text" },
  { key: "kolicina", header: "Zaliha", dataType: "number" },
  { key: "lastSaleDate", header: "Posl. prod.", dataType: "date" },
  { key: "daysWithoutSale", header: "Dana", dataType: "number" },
  { key: "agingCategory", header: "Status", dataType: "text" },
];

const depletionColumns: AnalyticsTableColumn<DepletionResult["forecasts"][number]>[] = [
  { key: "artikalId", header: "Artikal ID", dataType: "number" },
  { key: "naziv", header: "Artikal", dataType: "text" },
  { key: "kategorija", header: "Kategorija", dataType: "text" },
  { key: "currentStock", header: "Zaliha", dataType: "number" },
  { key: "avgDailySales", header: "Avg/dan", dataType: "number" },
  { key: "daysUntilOOS", header: "Dana do OOS", dataType: "number" },
  { key: "depletionDate", header: "Datum OOS", dataType: "date" },
  { key: "atRiskRevenue", header: "At-risk", dataType: "currency" },
  { key: "severity", header: "Sev.", dataType: "text" },
];

const reorderItemColumns: AnalyticsTableColumn<SmartReorderResult["items"][number] | ReorderItem>[] = [
  { key: "artikalId", header: "Artikal ID", dataType: "number" },
  { key: "naziv", header: "Artikal", dataType: "text" },
  { key: "kategorija", header: "Kategorija", dataType: "text" },
  { key: "dobavljacNaziv", header: "Dobavljac", dataType: "text" },
  { key: "currentStock", header: "Zaliha", dataType: "number" },
  { key: "avgDailySales", header: "V/dan", dataType: "number" },
  { key: "doh", header: "DOH", dataType: "number" },
  { key: "recommendedQty", header: "Preporuka", dataType: "number" },
  { key: "urgency", header: "Hitnost", dataType: "text" },
];

const reorderCategoryColumns: AnalyticsTableColumn<SmartReorderResult["byCategoryPlan"][number]>[] = [
  { key: "kategorija", header: "Kategorija", dataType: "text" },
  { key: "totalItems", header: "Artikala", dataType: "number" },
  { key: "criticalCount", header: "Kriticno", dataType: "number" },
  { key: "urgentCount", header: "Hitno", dataType: "number" },
  { key: "totalReorderCost", header: "Trosak nabavke", dataType: "currency" },
  { key: "expectedRevenue", header: "Ocekivani prihod", dataType: "currency" },
  { key: "avgMargin", header: "Avg marza", dataType: "percent" },
];

const reorderSupplierColumns: AnalyticsTableColumn<SmartReorderResult["bySupplierPlan"][number]>[] = [
  { key: "dobavljac", header: "Dobavljac", dataType: "text" },
  { key: "totalItems", header: "Artikala", dataType: "number" },
  { key: "criticalCount", header: "Kriticno", dataType: "number" },
  { key: "totalReorderCost", header: "Trosak nabavke", dataType: "currency" },
  { key: "avgReorderProbability", header: "Avg prob. reordering", dataType: "percent" },
];

// ══════════════════════════════════════════════════════════════════
// HELPERS
// ══════════════════════════════════════════════════════════════════

const toDateStr = (d: Date) => d.toISOString().slice(0, 10);
const daysAgo = (n: number) => { const d = new Date(); d.setDate(d.getDate() - n); return d; };
const fmtRsd = (v: number) => { if (Math.abs(v) >= 1e6) return `${(v / 1e6).toFixed(1)}M`; if (Math.abs(v) >= 1e3) return `${(v / 1e3).toFixed(0)}k`; return v.toLocaleString("sr-RS"); };
const fmtPct = (v: number, d = 1) => `${v.toFixed(d)}%`;
const fmtNum = (v: number) => v.toLocaleString("sr-RS");

function changeBadge(change: number, suffix = "%") {
  const up = change >= 0;
  return (
    <span className={`ml-1 text-[11px] font-semibold ${up ? "text-success" : "text-error"}`}>
      {up ? "▲" : "▼"} {Math.abs(change).toFixed(1)}{suffix}
    </span>
  );
}

// ══════════════════════════════════════════════════════════════════
// REUSABLE COMPONENTS
// ══════════════════════════════════════════════════════════════════

function KpiCard({
  label, value, sub, change, accent = PAL.blue, sparkline, icon, tooltip,
}: {
  label: string; value: string; sub?: string; change?: number; accent?: string;
  sparkline?: { date: string; revenue: number }[]; icon?: string; tooltip?: string;
}) {
  const [showTip, setShowTip] = useState(false);
  return (
    <div
      className="group relative rounded-xl border border-border bg-surface p-4 flex flex-col gap-1 overflow-hidden transition hover:border-border-hover"
      onMouseEnter={() => setShowTip(true)} onMouseLeave={() => setShowTip(false)}
    >
      <div className="absolute left-0 top-0 h-[3px] w-full opacity-80" style={{ background: `linear-gradient(90deg, ${accent}, transparent)` }} />
      <div className="flex items-center gap-1.5 text-[11px] text-muted uppercase tracking-wider">
        {icon && <span className="text-sm">{icon}</span>}
        {label}
        {tooltip && showTip && (
          <div className="absolute left-4 top-full z-20 mt-1 max-w-[220px] rounded-lg bg-surface-darker border border-border px-3 py-2 text-[11px] text-muted normal-case tracking-normal shadow-xl">
            {tooltip}
          </div>
        )}
      </div>
      <div className="text-xl font-bold text-foreground">{value}</div>
      {sub && (
        <div className="text-[11px] text-muted">
          {sub}
          {change !== undefined && changeBadge(change)}
        </div>
      )}
      {sparkline && sparkline.length > 2 && (
        <div className="mt-1 h-10">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={sparkline}>
              <defs>
                <linearGradient id={`spark-${label}`} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor={accent} stopOpacity={0.3} />
                  <stop offset="100%" stopColor={accent} stopOpacity={0} />
                </linearGradient>
              </defs>
              <Area type="monotone" dataKey="revenue" stroke={accent} strokeWidth={1.5} fill={`url(#spark-${label})`} dot={false} />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}

function Skeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className="space-y-3 animate-pulse">
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className="flex gap-3">
          <div className="h-4 flex-1 bg-border rounded" />
          <div className="h-4 w-20 bg-border rounded" />
          <div className="h-4 w-20 bg-border rounded" />
        </div>
      ))}
    </div>
  );
}

function CardSkeleton() {
  return <div className="rounded-xl border border-border bg-surface p-4 animate-pulse"><div className="h-3 w-24 bg-border rounded mb-3" /><div className="h-6 w-32 bg-border rounded mb-2" /><div className="h-3 w-20 bg-border rounded" /></div>;
}

function Badge({ label, color }: { label: string; color: string }) {
  return <span className="rounded px-1.5 py-0.5 text-[10px] font-bold" style={{ background: `${color}22`, color }}>{label}</span>;
}

function ScoreBar({ label, score, max = 100 }: { label: string; score: number; max?: number }) {
  const pct = Math.min(100, Math.max(0, (score / max) * 100));
  const color = pct >= 70 ? PAL.green : pct >= 40 ? PAL.yellow : PAL.red;
  return (
    <div>
      <div className="flex justify-between text-[11px] text-[var(--text-primary)] mb-0.5">
        <span>{label}</span>
        <span style={{ color }}>{score.toFixed(0)}/{max}</span>
      </div>
      <div className="h-1.5 rounded-full bg-[var(--surface-elevated)] overflow-hidden">
        <div className="h-full rounded-full transition-all duration-500" style={{ width: `${pct}%`, background: color }} />
      </div>
    </div>
  );
}

function MiniStat({ label, value, color }: { label: string; value: string | number; color?: string }) {
  return (
    <div className="rounded-lg bg-surface-darker px-3 py-2">
      <div className="text-[10px] text-muted uppercase">{label}</div>
      <div className="font-semibold text-sm" style={{ color: color ?? PAL.textPrimary }}>{value}</div>
    </div>
  );
}

function SectionHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div className="mb-4">
      <h3 className="text-sm font-semibold text-contrast">{title}</h3>
      {subtitle && <p className="text-[10px] text-muted mt-0.5">{subtitle}</p>}
    </div>
  );
}

function AlertBanner({ severity, children }: { severity: "info" | "warning" | "danger"; children: React.ReactNode }) {
  const map = { info: PAL.blue, warning: PAL.orange, danger: PAL.red };
  const c = map[severity];
  return (
    <div className="rounded-xl border px-4 py-3 text-sm" style={{ borderColor: `${c}33`, background: `${c}10`, color: c }}>
      {children}
    </div>
  );
}

const tooltipStyle: CSSProperties = { background: "var(--c-1e2332, var(--theme-color-1e2332, #1E2332))", border: "1px solid var(--c-2a3045, var(--theme-color-2a3045, #2A3045))", borderRadius: 8, fontSize: 12 };

// ══════════════════════════════════════════════════════════════════
// TAB 1: PREGLED (Command Center)
// ══════════════════════════════════════════════════════════════════

function OverviewTab({
  kpi,
  changelog,
  marginAlerts,
  loading,
  intelligenceDemand,
  intelligenceInventory,
  intelligencePrice,
  intelligenceTrend,
  intelligenceAsOfDate,
  intelligenceLoading,
  intelligenceError,
}: {
  kpi: KpiSnapshot | null;
  changelog: WeeklyChangelog | null;
  marginAlerts: MarginAlertResult | null;
  loading: boolean;
  intelligenceDemand: DemandSignalItem[];
  intelligenceInventory: InventoryRiskSignalItem[];
  intelligencePrice: PriceIntelligenceItem[];
  intelligenceTrend: TrendMomentumItem[];
  intelligenceAsOfDate: string | null;
  intelligenceLoading: boolean;
  intelligenceError: string | null;
}) {
  if (loading) return <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">{Array.from({ length: 8 }).map((_, i) => <CardSkeleton key={i} />)}</div>;

  return (
    <div className="space-y-6">
      {/* Weekly Changelog Alert Strip */}
      {changelog && (
        <div className="rounded-xl border border-border bg-surface p-5">
          <div className="flex items-center gap-2 mb-4">
            <span className="text-base">🔔</span>
            <h3 className="text-sm font-bold text-contrast">Šta se promenilo ove nedelje?</h3>
          </div>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div className="rounded-lg bg-surface-darker px-3 py-2">
              <div className="text-[10px] text-muted uppercase">Prihod ove nedelje</div>
              <div className="text-sm font-bold text-foreground">{fmtRsd(changelog.thisWeekRevenue)}</div>
              <div className="text-[11px]">{changeBadge(changelog.revenueChangePct)}</div>
            </div>
            <div className="rounded-lg bg-surface-darker px-3 py-2">
              <div className="text-[10px] text-muted uppercase">Prodato kom.</div>
              <div className="text-sm font-bold text-foreground">{fmtNum(changelog.thisWeekUnits)}</div>
              <div className="text-[11px]">{changeBadge(changelog.unitChangePct)}</div>
            </div>
            <div className="rounded-lg bg-surface-darker px-3 py-2">
              <div className="text-[10px] text-muted uppercase">Transakcije</div>
              <div className="text-sm font-bold text-foreground">{fmtNum(changelog.thisWeekTransactions)}</div>
            </div>
            <div className="rounded-lg bg-surface-darker px-3 py-2">
              <div className="text-[10px] text-muted uppercase">OOS / Promene cena</div>
              <div className="text-sm font-bold" style={{ color: changelog.oosCount > 5 ? PAL.red : PAL.textPrimary }}>{changelog.oosCount} / {changelog.priceChangesThisWeek}</div>
            </div>
          </div>

          {/* Category movement */}
          {changelog.categoryChanges.length > 0 && (
            <div className="mt-4">
              <div className="text-[11px] text-muted uppercase mb-2">Promene po kategorijama (nedelja-na-nedelju)</div>
              <div className="flex flex-wrap gap-2">
                {changelog.categoryChanges.slice(0, 8).map((c) => (
                  <div
                    key={c.kategorija}
                    className="rounded-lg border border-border bg-[var(--surface-elevated)] px-3 py-1.5 text-xs"
                  >
                    <span className="text-contrast font-medium">{c.kategorija}</span>
                    {changeBadge(c.changePct)}
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Margin Pressure Alerts */}
      {marginAlerts && marginAlerts.alerts.length > 0 && (
        <div className="rounded-xl border border-error/20 bg-error/5 p-5">
          <div className="flex items-center gap-2 mb-3">
            <span className="text-base">⚠️</span>
            <h3 className="text-sm font-bold text-error">
              Margin Pressure — {marginAlerts.summary.negativeMarginCount + marginAlerts.summary.lowMarginCount} artikala ugroženo
            </h3>
          </div>
          <div className="grid grid-cols-3 gap-3 mb-3">
            <MiniStat label="Negativna marža" value={marginAlerts.summary.negativeMarginCount} color={PAL.red} />
            <MiniStat label="Niska marža (<10%)" value={marginAlerts.summary.lowMarginCount} color={PAL.orange} />
            <MiniStat label="Ukupno izgubljena marža" value={fmtRsd(marginAlerts.summary.totalLostMargin)} color={PAL.red} />
          </div>
          <div className="space-y-1.5 max-h-[200px] overflow-y-auto">
            {marginAlerts.alerts.slice(0, 8).map((a) => (
              <div key={a.artikalId} className="flex items-center gap-3 rounded-lg bg-surface border border-border px-3 py-2 text-xs">
                <Badge label={a.alertType === "NEGATIVE_MARGIN" ? "NEG" : a.alertType === "LOW_MARGIN" ? "LOW" : "MD"} color={a.alertType === "NEGATIVE_MARGIN" ? PAL.red : PAL.orange} />
                <span className="flex-1 text-foreground truncate">{a.naziv}</span>
                <span className="text-muted">{a.kategorija}</span>
                <span style={{ color: a.marginPct < 0 ? PAL.red : PAL.orange }}>{fmtPct(a.marginPct)}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* KPI Overview Grid */}
      {kpi && (
        <div>
          <SectionHeader title="KPI Snapshot" subtitle="Ključni pokazatelji za izabrani period" />
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
            <KpiCard label="Ukupan prihod" value={fmtRsd(kpi.revenue)} sub="vs. preth. period" change={kpi.revenueChange} accent={PAL.blue} sparkline={kpi.sparkline} icon="💰" tooltip="Ukupna prodaja za izabrani period" />
            <KpiCard label="Bruto marža" value={fmtPct(kpi.marginPct)} sub="Procenjena profitabilnost" accent={PAL.green} icon="📈" tooltip="(Prodajna - Nabavna) / Prodajna × 100" />
            <KpiCard label="Prodato kom." value={fmtNum(kpi.units)} sub="vs. preth. period" change={kpi.unitsChange} accent={PAL.purple} icon="👟" />
            <KpiCard label="Transakcije" value={fmtNum(kpi.transactions)} sub={`Avg. ${fmtRsd(kpi.transactions > 0 ? kpi.revenue / kpi.transactions : 0)}/tr`} accent={PAL.yellow} icon="🧾" tooltip="Prosečna vrednost transakcije" />
            <KpiCard label="OOS / Malo" value={`${kpi.oosCount} / ${kpi.lowStockCount}`} sub="SKU bez zaliha / ispod min" accent={kpi.oosCount > 10 ? PAL.red : PAL.orange} icon="⚠️" tooltip="Artikli bez zaliha i artikli ispod minimalne količine" />
          </div>
        </div>
      )}

      <IntelligenceSnapshotPanel
        demand={intelligenceDemand}
        inventory={intelligenceInventory}
        price={intelligencePrice}
        trend={intelligenceTrend}
        asOfDate={intelligenceAsOfDate}
        loading={intelligenceLoading}
        error={intelligenceError}
      />

      {/* Quick Recommended Actions */}
      <div className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-5">
        <div className="flex items-center gap-2 mb-3">
          <span className="text-base">🧭</span>
          <h3 className="text-sm font-bold text-[var(--text-primary)]">Preporučene akcije</h3>
        </div>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
          {kpi && kpi.oosCount > 3 && (
            <div className="rounded-lg border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-3 py-2 text-xs text-[var(--text-primary)]">
              🔴 <strong>{kpi.oosCount}</strong> artikala bez zalihe — proveri tab "Nabavka 2.0"
            </div>
          )}
          {marginAlerts && marginAlerts.summary.negativeMarginCount > 0 && (
            <div className="rounded-lg border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-3 py-2 text-xs text-[var(--text-primary)]">
              🟠 <strong>{marginAlerts.summary.negativeMarginCount}</strong> artikala pod negativnom maržom
            </div>
          )}
          {changelog && changelog.revenueChangePct < -15 && (
            <div className="rounded-lg border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-3 py-2 text-xs text-[var(--text-primary)]">
              🟡 Prihod pao <strong>{Math.abs(changelog.revenueChangePct).toFixed(0)}%</strong> nedelja-na-nedelju
            </div>
          )}
          {changelog && changelog.revenueChangePct >= 10 && (
            <div className="rounded-lg border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-3 py-2 text-xs text-[var(--text-primary)]">
              🟢 Rast prihoda +{changelog.revenueChangePct.toFixed(0)}% ove nedelje! 
            </div>
          )}
          <div className="rounded-lg border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-3 py-2 text-xs text-[var(--text-primary)]">
            💡 Koristi "Matrica V×M" za identifikaciju ★ zvezda i mrtvih tegova
          </div>
        </div>
      </div>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════
// TAB 2: DOBAVLJAČI 2.0
// ══════════════════════════════════════════════════════════════════

function SupplierTab({
  v2Data,
  v1Data,
  loading,
  analyticsContext,
}: {
  v2Data: SupplierScoreV2[];
  v1Data: SupplierScore[];
  loading: boolean;
  analyticsContext: InsightAnalyticsContext;
}) {
  const [selected, setSelected] = useState<SupplierScoreV2 | null>(null);
  const data = v2Data.length > 0 ? v2Data : [];

  if (loading) return <Skeleton rows={8} />;
  if (!data.length && !v1Data.length)
    return <p className="text-[var(--text-primary)] text-sm">Nema podataka o dobavljačima za izabrani period.</p>;

  // Fallback to v1 if v2 failed
  if (!data.length && v1Data.length) {
    return <SupplierTabV1 data={v1Data} loading={false} analyticsContext={analyticsContext} />;
  }

  const displayed = selected ?? data[0];

  return (
    <div className="space-y-5">
      <SectionHeader title="Dobavljači 2.0 — Rangiranje i Scorecard" subtitle="Kompozitni skor: Profitabilnost × Velocity × Diverzifikacija × Pouzdanost" />

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-5">
        {/* Leaderboard */}
        <div className="lg:col-span-3 overflow-x-auto rounded-xl border border-[var(--border-default)]">
          <div className="mb-3 p-3">
            <AnalyticsTableToolbar
              tableKey="insight-supplier-scorecard-v2"
              tableTitle="Insight Studio - dobavljaci 2.0"
              columns={supplierV2Columns}
              rows={data}
              filters={analyticsContext.filters}
              metadata={analyticsContext.metadata}
              defaultOrientation="landscape"
            />
          </div>
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
                <th className="px-3 py-2 text-left">#</th>
                <th className="px-3 py-2 text-left">Dobavljač</th>
                <th className="px-3 py-2 text-right">Prihod</th>
                <th className="px-3 py-2 text-right">Marža%</th>
                <th className="px-3 py-2 text-right">Velocity</th>
                <th className="px-3 py-2 text-right">Neprodato</th>
                <th className="px-3 py-2 text-center">Tier</th>
                <th className="px-3 py-2 text-right">Score</th>
              </tr>
            </thead>
            <tbody>
              {data.map((s, i) => (
                <tr
                  key={s.dobavljacId ?? i}
                  onClick={() => {
                    setSelected(s);
                    analyticsContext.openSnapshotDetail(
                      "insight-supplier-scorecard-v2",
                      String(s.dobavljacId ?? s.dobavljacNaziv),
                      s.dobavljacNaziv,
                      "Insight Studio - dobavljaci 2.0",
                      supplierV2Columns,
                      s
                    );
                  }}
                  className={`cursor-pointer border-b border-[var(--border-default)] transition hover:bg-[var(--surface-light)] ${
                    displayed?.dobavljacId === s.dobavljacId ? "bg-[var(--surface-elevated)] ring-1 ring-inset ring-[var(--theme-color-32579e, #32579e)]" : ""
                  }`}
                >
                  <td className="px-3 py-2 text-[var(--text-primary)]">{i + 1}</td>
                  <td className="px-3 py-2 font-medium text-[var(--text-primary)]">{s.dobavljacNaziv}</td>
                  <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtRsd(s.totalRevenue)}</td>
                  <td className="px-3 py-2 text-right" style={{ color: s.marginPct >= 30 ? PAL.green : s.marginPct >= 15 ? PAL.yellow : PAL.red }}>{fmtPct(s.marginPct)}</td>
                  <td className="px-3 py-2 text-right text-[var(--text-primary)]">{s.velocity.toFixed(2)}</td>
                  <td className="px-3 py-2 text-right" style={{ color: s.unsoldStock > 50 ? PAL.orange : PAL.textSecondary }}>{s.unsoldStock}</td>
                  <td className="px-3 py-2 text-center"><Badge label={TIER_LABELS[s.tier] ?? s.tier} color={TIER_COLORS[s.tier] ?? PAL.textSecondary} /></td>
                  <td className="px-3 py-2 text-right font-bold" style={{ color: s.compositeScore >= 70 ? PAL.green : s.compositeScore >= 40 ? PAL.yellow : PAL.red }}>{s.compositeScore.toFixed(0)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Scorecard Detail */}
        {displayed && (
          <div className="lg:col-span-2 rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4 space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-bold text-[var(--text-primary)]">{displayed.dobavljacNaziv}</h3>
              <Badge label={TIER_LABELS[displayed.tier] ?? displayed.tier} color={TIER_COLORS[displayed.tier] ?? PAL.textSecondary} />
            </div>
            <div className="grid grid-cols-2 gap-2">
              <MiniStat label="Prihod" value={fmtRsd(displayed.totalRevenue)} />
              <MiniStat label="Marža" value={fmtPct(displayed.marginPct)} color={displayed.marginPct >= 20 ? PAL.green : PAL.orange} />
              <MiniStat label="Velocity" value={displayed.velocity.toFixed(2)} color={PAL.blue} />
              <MiniStat label="Kategorije" value={displayed.uniqueCategories} />
              <MiniStat label="Neprodato kom" value={displayed.unsoldStock} color={displayed.unsoldStock > 50 ? PAL.orange : PAL.textPrimary} />
              <MiniStat label="Zavisnost" value={fmtPct(displayed.dependency)} color={displayed.dependency > 30 ? PAL.red : PAL.textSecondary} />
            </div>
            <div className="space-y-2.5">
              <ScoreBar label="Profitabilnost" score={displayed.profitScore} />
              <ScoreBar label="Velocity / Obrt" score={displayed.velocityScore} />
              <ScoreBar label="Diverzifikacija" score={displayed.diversityScore} />
              <ScoreBar label="Pouzdanost" score={displayed.reliabilityScore} />
            </div>
            <div className="rounded-lg bg-[var(--surface-elevated)] px-3 py-2 flex items-center justify-between">
              <span className="text-xs text-[var(--text-primary)]">Kompozitni Skor</span>
              <span className="text-lg font-bold" style={{ color: displayed.compositeScore >= 70 ? PAL.green : displayed.compositeScore >= 40 ? PAL.yellow : PAL.red }}>
                {displayed.compositeScore.toFixed(1)}
              </span>
            </div>
            <div className="h-[130px]">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={[
                  { name: "Profit", v: displayed.profitScore },
                  { name: "Velocity", v: displayed.velocityScore },
                  { name: "Diverz", v: displayed.diversityScore },
                  { name: "Pouzdanost", v: displayed.reliabilityScore },
                ]} barSize={24}>
                  <CartesianGrid stroke="var(--c-2a3045, var(--theme-color-2a3045, #2A3045))" vertical={false} />
                  <XAxis dataKey="name" tick={{ fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 10 }} axisLine={false} tickLine={false} />
                  <YAxis domain={[0, 100]} tick={{ fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 10 }} axisLine={false} tickLine={false} />
                  <Tooltip contentStyle={tooltipStyle} itemStyle={{ color: "var(--c-e8ecf4, var(--theme-color-e8ecf4, #E8ECF4))" }} />
                  <Bar dataKey="v" fill={PAL.blue} radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

// Fallback V1 supplier tab
function SupplierTabV1({ data, loading, analyticsContext }: { data: SupplierScore[]; loading: boolean; analyticsContext: InsightAnalyticsContext }) {
  const [selected, setSelected] = useState<SupplierScore | null>(null);
  if (loading) return <Skeleton rows={8} />;
  if (!data.length) return <p className="text-[var(--text-primary)] text-sm">Nema podataka.</p>;
  const displayed = selected ?? data[0];
  return (
    <div className="grid grid-cols-1 gap-5 lg:grid-cols-5">
      <div className="lg:col-span-3 overflow-x-auto rounded-xl border border-[var(--border-default)]">
        <div className="mb-3 p-3">
          <AnalyticsTableToolbar
            tableKey="insight-supplier-scorecard-v1"
            tableTitle="Insight Studio - dobavljaci"
            columns={supplierV1Columns}
            rows={data}
            filters={analyticsContext.filters}
            metadata={analyticsContext.metadata}
            defaultOrientation="landscape"
          />
        </div>
        <table className="w-full text-sm">
          <thead><tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
            <th className="px-3 py-2 text-left">#</th><th className="px-3 py-2 text-left">Dobavljač</th>
            <th className="px-3 py-2 text-right">Prihod</th><th className="px-3 py-2 text-right">Marža%</th>
            <th className="px-3 py-2 text-center">Risk</th><th className="px-3 py-2 text-right">Skor</th>
          </tr></thead>
          <tbody>
            {data.map((s, i) => (
              <tr key={s.dobavljacId ?? i} onClick={() => {
                setSelected(s);
                analyticsContext.openSnapshotDetail(
                  "insight-supplier-scorecard-v1",
                  String(s.dobavljacId ?? s.dobavljacNaziv),
                  s.dobavljacNaziv,
                  "Insight Studio - dobavljaci",
                  supplierV1Columns,
                  s
                );
              }}
                className={`cursor-pointer border-b border-[var(--border-default)] transition hover:bg-[var(--surface-light)] ${displayed.dobavljacId === s.dobavljacId ? "bg-[var(--surface-elevated)]" : ""}`}>
                <td className="px-3 py-2 text-[var(--text-primary)]">{i + 1}</td>
                <td className="px-3 py-2 text-[var(--text-primary)]">{s.dobavljacNaziv}</td>
                <td className="px-3 py-2 text-right">{fmtRsd(s.totalRevenue)}</td>
                <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtPct(s.marginPct)}</td>
                <td className="px-3 py-2 text-center"><Badge label={s.riskLevel} color={s.riskLevel === "LOW" ? PAL.green : s.riskLevel === "MED" ? PAL.yellow : PAL.red} /></td>
                <td className="px-3 py-2 text-right font-bold text-[var(--text-primary)]">{s.compositeScore.toFixed(0)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="lg:col-span-2 rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4 space-y-3">
        <h3 className="text-sm font-bold text-[var(--text-primary)]">{displayed.dobavljacNaziv}</h3>
        <div className="grid grid-cols-2 gap-2">
          <MiniStat label="Prihod" value={fmtRsd(displayed.totalRevenue)} />
          <MiniStat label="Marža" value={fmtPct(displayed.marginPct)} color={PAL.green} />
        </div>
        <ScoreBar label="Profitabilnost" score={displayed.profitScore} />
        <ScoreBar label="Diverzifikacija" score={displayed.diversityScore} />
        <ScoreBar label="Niska zavisnost" score={displayed.dependencyScore} />
      </div>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════
// TAB 3: KATEGORIJE (Enhanced)
// ══════════════════════════════════════════════════════════════════

function CategoryTab({
  byCategory,
  byGender,
  priceSensitivity,
  basketAffinity,
  loading,
  analyticsContext,
}: {
  byCategory: CategoryStat[];
  byGender: GenderStat[];
  priceSensitivity: PriceSensitivity | null;
  basketAffinity: BasketAffinity | null;
  loading: boolean;
  analyticsContext: InsightAnalyticsContext;
}) {
  const [subTab, setSubTab] = useState<"kategorije" | "pol" | "cene" | "korpa">("kategorije");

  if (loading) return <Skeleton rows={6} />;
  if (!byCategory.length) return <p className="text-[var(--text-primary)] text-sm">Nema podataka.</p>;

  const subTabs = [
    { key: "kategorije" as const, label: "Po Tipu Obuće" },
    { key: "pol" as const, label: "Po Polu" },
    { key: "cene" as const, label: "Cenovna Osetljivost" },
    { key: "korpa" as const, label: "Basket Afinitet" },
  ];

  return (
    <div className="space-y-5">
      <SectionHeader title="Kategorije & Segmentacija" subtitle="Prihodi, marže, velocity i cross-sell analitika" />
      <div className="rounded-xl border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-4 py-3 text-xs text-[var(--text-primary)]">
        Primarni read model za cenovne i category signale sada dolazi iz <span className="font-semibold text-[var(--text-primary)]">analytics_intel</span>.
        Basket afinitet i raspodela po polu ostaju na legacy advanced sloju kao dopuna.
      </div>
      <div className="flex gap-2 flex-wrap">
        {subTabs.map(t => (
          <button key={t.key} onClick={() => setSubTab(t.key)}
            className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subTab === t.key ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)] hover:text-[var(--text-primary)]"}`}>
            {t.label}
          </button>
        ))}
      </div>

      {subTab === "kategorije" && (
        <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
          <div>
            <h4 className="mb-2 text-xs font-semibold text-[var(--text-primary)]">Prihod po kategoriji</h4>
            <div className="h-[260px]">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={byCategory.slice(0, 8)} layout="vertical" barSize={18}>
                  <CartesianGrid stroke="var(--c-2a3045, var(--theme-color-2a3045, #2A3045))" horizontal={false} />
                  <XAxis type="number" tick={{ fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 10 }} axisLine={false} tickLine={false} tickFormatter={(v) => fmtRsd(Number(v))} />
                  <YAxis type="category" dataKey="kategorija" tick={{ fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 10 }} axisLine={false} tickLine={false} width={80} />
                  <Tooltip contentStyle={tooltipStyle} formatter={(v: number | string | undefined) => [fmtRsd(Number(v ?? 0)), "Prihod"]} />
                  <Bar dataKey="totalRevenue" fill={PAL.blue} radius={[0, 4, 4, 0]}>
                    {byCategory.slice(0, 8).map((_, i) => <Cell key={i} fill={DONUT_COLORS[i % DONUT_COLORS.length]} />)}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>
          <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
            <div className="mb-3 p-3">
              <AnalyticsTableToolbar
                tableKey="insight-category-performance"
                tableTitle="Insight Studio - kategorije"
                columns={categoryColumns}
                rows={byCategory}
                filters={analyticsContext.filters}
                metadata={analyticsContext.metadata}
                defaultOrientation="landscape"
              />
            </div>
            <table className="w-full text-xs">
              <thead><tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
                <th className="px-3 py-2 text-left">Kategorija</th>
                <th className="px-3 py-2 text-right">Udeo%</th>
                <th className="px-3 py-2 text-right">Marža%</th>
                <th className="px-3 py-2 text-right">Lift</th>
                <th className="px-3 py-2 text-right">Velocity</th>
                <th className="px-3 py-2 text-right">SKU</th>
              </tr></thead>
              <tbody>
                {byCategory.map((cat, i) => (
                  <tr
                    key={i}
                    className="cursor-pointer border-b border-[var(--border-default)] hover:bg-[var(--surface-light)] transition"
                    onClick={() => analyticsContext.openSnapshotDetail("insight-category-performance", cat.kategorija, cat.kategorija, "Insight Studio - kategorije", categoryColumns, cat)}
                  >
                    <td className="px-3 py-2 font-medium text-[var(--text-primary)]">{cat.kategorija}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtPct(cat.revShare)}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtPct(cat.marginPct)}</td>
                    <td className="px-3 py-2 text-right" style={{ color: cat.profitLift >= 0 ? PAL.green : PAL.red }}>{cat.profitLift >= 0 ? "+" : ""}{fmtPct(cat.profitLift)}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)]">{cat.velocity.toFixed(3)}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)]">{cat.uniqueSKU}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {subTab === "pol" && (
        <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
          <div className="h-[220px]">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie data={byGender as unknown as Record<string, unknown>[]} dataKey="totalRevenue" nameKey="pol"
                  cx="50%" cy="50%" outerRadius={85} innerRadius={45} paddingAngle={3}>
                  {byGender.map((_, i) => <Cell key={i} fill={DONUT_COLORS[i % DONUT_COLORS.length]} />)}
                </Pie>
                <Tooltip contentStyle={tooltipStyle} formatter={(v: number | string | undefined) => [fmtRsd(Number(v ?? 0)), "Prihod"]} />
                <Legend formatter={(v) => <span style={{ color: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 12 }}>{v}</span>} />
              </PieChart>
            </ResponsiveContainer>
          </div>
          <div className="overflow-x-auto rounded-xl border border-[var(--border-default)] self-start">
            <div className="mb-3 p-3">
              <AnalyticsTableToolbar
                tableKey="insight-gender-breakdown"
                tableTitle="Insight Studio - po polu"
                columns={genderColumns}
                rows={byGender}
                filters={analyticsContext.filters}
                metadata={analyticsContext.metadata}
                defaultOrientation="portrait"
              />
            </div>
            <table className="w-full text-xs">
              <thead><tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
                <th className="px-3 py-2 text-left">Pol</th><th className="px-3 py-2 text-right">Prihod</th>
                <th className="px-3 py-2 text-right">Udeo%</th><th className="px-3 py-2 text-right">Kom</th>
              </tr></thead>
              <tbody>
                {byGender.map((g, i) => (
                  <tr
                    key={i}
                    className="cursor-pointer border-b border-[var(--border-default)] hover:bg-[var(--surface-light)] transition"
                    onClick={() => analyticsContext.openSnapshotDetail("insight-gender-breakdown", g.pol, g.pol, "Insight Studio - po polu", genderColumns, g)}
                  >
                    <td className="px-3 py-2 text-[var(--text-primary)] flex items-center gap-2">
                      <span className="w-2.5 h-2.5 rounded-full inline-block" style={{ background: DONUT_COLORS[i % DONUT_COLORS.length] }} />{g.pol}
                    </td>
                    <td className="px-3 py-2 text-right">{fmtRsd(g.totalRevenue)}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)] font-semibold">{fmtPct(g.revShare)}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtNum(g.totalUnits)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {subTab === "cene" && priceSensitivity && (
        <div className="space-y-4">
          <h4 className="text-xs font-semibold text-[var(--text-primary)]">Cenovna osetljivost — po cenovnim opsezima</h4>
          <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
            <div className="mb-3 p-3">
              <AnalyticsTableToolbar
                tableKey="insight-price-sensitivity"
                tableTitle="Insight Studio - cenovna osetljivost"
                columns={priceSensitivityColumns}
                rows={priceSensitivity.bands}
                filters={analyticsContext.filters}
                metadata={analyticsContext.metadata}
                defaultOrientation="landscape"
              />
            </div>
            <table className="w-full text-xs">
              <thead><tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
                <th className="px-3 py-2 text-left">Opseg</th>
                <th className="px-3 py-2 text-right">SKU</th>
                <th className="px-3 py-2 text-right">Prodato</th>
                <th className="px-3 py-2 text-right">Vel/SKU</th>
                <th className="px-3 py-2 text-right">Avg Cena</th>
                <th className="px-3 py-2 text-right">Avg Marža%</th>
                <th className="px-3 py-2 text-right">Zaliha</th>
                <th className="px-3 py-2 text-right">Niv.</th>
                <th className="px-3 py-2 text-center">Elastičnost</th>
              </tr></thead>
              <tbody>
                {priceSensitivity.bands.map((b, i) => (
                  <tr
                    key={i}
                    className="cursor-pointer border-b border-[var(--border-default)] hover:bg-[var(--surface-light)] transition"
                    onClick={() => analyticsContext.openSnapshotDetail("insight-price-sensitivity", b.priceBand, b.priceBand, "Insight Studio - cenovna osetljivost", priceSensitivityColumns, b)}
                  >
                    <td className="px-3 py-2 font-medium text-[var(--text-primary)]">{b.priceBand}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)]">{b.skuCount}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtNum(b.totalUnits)}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)]">{b.avgVelocityPerSku.toFixed(1)}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtRsd(b.avgPrice)}</td>
                    <td className="px-3 py-2 text-right" style={{ color: b.avgMarginPct >= 20 ? PAL.green : b.avgMarginPct >= 10 ? PAL.yellow : PAL.red }}>{fmtPct(b.avgMarginPct)}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtNum(b.totalStock)}</td>
                    <td className="px-3 py-2 text-right text-[var(--text-primary)]">{b.markdownCount}</td>
                    <td className="px-3 py-2 text-center"><Badge label={b.elasticity} color={b.elasticity === "Elastic" ? PAL.green : b.elasticity === "Inelastic" ? PAL.red : PAL.yellow} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {subTab === "korpa" && basketAffinity && (
        <div className="space-y-4">
          <div className="flex items-center gap-2">
            <h4 className="text-xs font-semibold text-[var(--text-primary)]">Basket Afinitet — koji se tipovi prodaju zajedno?</h4>
            <span className="text-[10px] text-[var(--text-primary)]">({basketAffinity.totalMultiItemTransactions} multi-item transakcija)</span>
          </div>
          {basketAffinity.pairs.length > 0 ? (
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
              {basketAffinity.pairs.slice(0, 12).map((p, i) => (
                <div key={i} className="rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 flex items-center gap-3">
                  <span className="text-xl">🤝</span>
                  <div className="flex-1">
                    <div className="text-xs text-[var(--text-primary)]">{p.categoryA} + {p.categoryB}</div>
                    <div className="text-[10px] text-[var(--text-primary)]">{p.coOccurrences}× zajedno · {fmtPct(p.supportPct)} support</div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-[var(--text-primary)] text-sm">Nema dovoljno multi-item transakcija.</p>
          )}
        </div>
      )}
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════
// TAB 4: MATRICA V×M (Velocity × Margin)
// ══════════════════════════════════════════════════════════════════

function MatrixTab({
  matrixData,
  loading,
}: {
  matrixData: VelocityMarginMatrix | null;
  loading: boolean;
}) {
  const [quadFilter, setQuadFilter] = useState<string>("ALL");

  if (loading) return <Skeleton rows={6} />;
  if (!matrixData || !matrixData.items.length)
    return <p className="text-[var(--text-primary)] text-sm">Nema podataka za velocity-margin matricu.</p>;

  const { items, medianMargin, medianVelocity, quadrantCounts } = matrixData;
  const filtered = quadFilter === "ALL" ? items : items.filter(x => x.quadrant === quadFilter);

  const scatterData = filtered.map(it => ({
    x: it.velocity,
    y: it.marginPct,
    z: it.totalRevenue,
    name: it.naziv,
    quad: it.quadrant,
    kat: it.kategorija,
  }));

  return (
    <div className="space-y-5">
      <SectionHeader title="Velocity × Margin Matrica" subtitle="Svaki artikal pozicioniran po brzini prodaje i profitabilnosti" />

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <div className="rounded-xl border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-3 py-2 cursor-pointer" onClick={() => setQuadFilter(quadFilter === "STAR" ? "ALL" : "STAR")}>
          <div className="text-[10px] uppercase text-[var(--text-primary)]">⭐ Zvezde</div>
          <div className="text-xl font-bold text-[var(--text-primary)]">{quadrantCounts.stars}</div>
          <div className="text-[10px] text-[var(--text-primary)]">Visoka vel. + visoka marža</div>
        </div>
        <div className="rounded-xl border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-3 py-2 cursor-pointer" onClick={() => setQuadFilter(quadFilter === "NICHE_GEM" ? "ALL" : "NICHE_GEM")}>
          <div className="text-[10px] uppercase text-[var(--text-primary)]">💎 Niša Dragulje</div>
          <div className="text-xl font-bold text-[var(--text-primary)]">{quadrantCounts.nicheGems}</div>
          <div className="text-[10px] text-[var(--text-primary)]">Niska vel. + visoka marža</div>
        </div>
        <div className="rounded-xl border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-3 py-2 cursor-pointer" onClick={() => setQuadFilter(quadFilter === "VOLUME_TRAP" ? "ALL" : "VOLUME_TRAP")}>
          <div className="text-[10px] uppercase text-[var(--text-primary)]">⚡ Volume Trap</div>
          <div className="text-xl font-bold text-[var(--text-primary)]">{quadrantCounts.volumeTraps}</div>
          <div className="text-[10px] text-[var(--text-primary)]">Visoka vel. + niska marža</div>
        </div>
        <div className="rounded-xl border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-3 py-2 cursor-pointer" onClick={() => setQuadFilter(quadFilter === "DEAD_WEIGHT" ? "ALL" : "DEAD_WEIGHT")}>
          <div className="text-[10px] uppercase text-[var(--text-primary)]">⚠ Mrtav Teg</div>
          <div className="text-xl font-bold text-[var(--text-primary)]">{quadrantCounts.deadWeight}</div>
          <div className="text-[10px] text-[var(--text-primary)]">Niska vel. + niska marža</div>
        </div>
      </div>

      {/* Scatter chart */}
      <div className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
        <div className="h-[350px]">
          <ResponsiveContainer width="100%" height="100%">
            <ScatterChart margin={{ top: 10, right: 20, bottom: 20, left: 10 }}>
              <CartesianGrid stroke="var(--c-2a3045, var(--theme-color-2a3045, #2A3045))" />
              <XAxis type="number" dataKey="x" name="Velocity" tick={{ fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 10 }} axisLine={false}
                label={{ value: "Velocity →", position: "insideBottom", offset: -10, fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 11 }} />
              <YAxis type="number" dataKey="y" name="Marža%" tick={{ fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 10 }} axisLine={false}
                label={{ value: "Marža% →", angle: -90, position: "insideLeft", fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 11 }} />
              <ZAxis type="number" dataKey="z" range={[30, 400]} name="Prihod" />
              <ReferenceLine y={medianMargin} stroke={PAL.yellow} strokeDasharray="4 4" label={{ value: `Median marža ${medianMargin.toFixed(0)}%`, fill: PAL.yellow, fontSize: 9 }} />
              <ReferenceLine x={medianVelocity} stroke={PAL.yellow} strokeDasharray="4 4" label={{ value: `Median vel ${medianVelocity.toFixed(2)}`, fill: PAL.yellow, fontSize: 9 }} />
              <Tooltip contentStyle={tooltipStyle} formatter={(v: number | undefined) => [typeof v === "number" ? v.toFixed(2) : String(v ?? "")]}
                content={({ payload }) => {
                  if (!payload?.length) return null;
                  const d = payload[0].payload;
                  return (
                    <div className="bg-[var(--surface-elevated)] border border-[var(--border-default)] rounded-lg px-3 py-2 text-xs shadow-xl">
                      <div className="font-semibold text-[var(--text-primary)]">{d.name}</div>
                      <div className="text-[var(--text-primary)]">{d.kat}</div>
                      <div className="text-[var(--text-primary)]">Vel: {d.x.toFixed(3)} · Marža: {d.y.toFixed(1)}%</div>
                      <Badge label={QUAD_LABELS[d.quad] ?? d.quad} color={QUAD_COLORS[d.quad] ?? PAL.textSecondary} />
                    </div>
                  );
                }}
              />
              <Scatter data={scatterData}>
                {scatterData.map((d, i) => <Cell key={i} fill={QUAD_COLORS[d.quad] ?? PAL.textSecondary} fillOpacity={0.75} />)}
              </Scatter>
            </ScatterChart>
          </ResponsiveContainer>
        </div>
      </div>

      {/* Top in each quadrant */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {(["STAR", "NICHE_GEM", "VOLUME_TRAP", "DEAD_WEIGHT"] as const).map(q => {
          const qItems = items.filter(x => x.quadrant === q).slice(0, 5);
          if (!qItems.length) return null;
          return (
            <div key={q} className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-3">
              <div className="flex items-center gap-2 mb-2">
                <Badge label={QUAD_LABELS[q]} color={QUAD_COLORS[q]} />
                <span className="text-[10px] text-[var(--text-primary)]">Top 5</span>
              </div>
              <div className="space-y-1">
                {qItems.map((it, i) => (
                  <div key={it.artikalId} className="flex items-center justify-between text-xs py-0.5">
                    <span className="text-[var(--text-primary)] w-4">{i + 1}.</span>
                    <span className="flex-1 text-[var(--text-primary)] truncate">{it.naziv}</span>
                    <span className="text-[var(--text-primary)] ml-2">V:{it.velocity.toFixed(2)}</span>
                    <span className="ml-2" style={{ color: it.marginPct >= 20 ? PAL.green : PAL.orange }}>{fmtPct(it.marginPct)}</span>
                  </div>
                ))}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════
// TAB 5: DNEVNA + HEATMAP
// ══════════════════════════════════════════════════════════════════

function DailyTab({
  data, loading, onDateChange, selectedDate,
  heatmap, heatmapLoading,
}: {
  data: DailyAnalysis | null; loading: boolean;
  onDateChange: (d: string) => void; selectedDate: string;
  heatmap: WeeklyHeatmap | null; heatmapLoading: boolean;
}) {
  const [subView, setSubView] = useState<"analiza" | "heatmap">("analiza");

  const zColorClass = !data ? "text-[var(--text-primary)]" : data.isExtremeOutlier ? "text-[var(--text-primary)]" : data.isOutlier ? "text-[var(--text-primary)]" : "text-[var(--text-primary)]";

  return (
    <div className="space-y-5">
      <SectionHeader title="Analiza Dana & Tjedna Potražnja" subtitle="Z-score detekcija anomalija i heatmap nedeljne aktivnosti" />
      <div className="rounded-xl border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-4 py-3 text-xs text-[var(--text-primary)]">
        Primarni prikaz koristi intelligence inventory signal layer. Legacy depletion forecast ostaje fallback ako signal cache nije spreman.
      </div>
      <div className="flex gap-2">
        <button onClick={() => setSubView("analiza")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "analiza" ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)]"}`}>📊 Dnevna Analiza</button>
        <button onClick={() => setSubView("heatmap")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "heatmap" ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)]"}`}>🔥 Heatmap</button>
      </div>

      {subView === "analiza" && (
        <>
          <div className="flex items-center gap-3 flex-wrap">
            <label className="text-sm text-[var(--text-primary)]">Dan:</label>
            <input type="date" value={selectedDate} onChange={e => onDateChange(e.target.value)}
              className="rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-1.5 text-sm text-[var(--text-primary)] focus:border-[var(--border-default)] focus:outline-none" />
            {loading && <span className="text-xs text-[var(--text-primary)] animate-pulse">Učitavanje…</span>}
          </div>
          {data && (
            <>
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                <MiniStat label="Prihod tog dana" value={fmtRsd(data.targetRevenue)} />
                <MiniStat label="Prosek perioda" value={fmtRsd(data.meanRevenue)} />
                <MiniStat label="Z-Score" value={data.zScore.toFixed(2)} color={data.isExtremeOutlier ? PAL.red : data.isOutlier ? PAL.yellow : PAL.green} />
                <div className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-3">
                  <div className="text-[10px] uppercase text-[var(--text-primary)]">Outlier?</div>
                  <div className={`text-sm font-bold ${zColorClass}`}>{data.isExtremeOutlier ? "⚡ Ekstremni" : data.isOutlier ? "⚠ Da" : "✓ Ne"}</div>
                </div>
              </div>
              <div className="h-[200px]">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={data.dailyData}>
                    <CartesianGrid stroke="var(--c-2a3045, var(--theme-color-2a3045, #2A3045))" />
                    <XAxis dataKey="date" tick={{ fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 9 }} axisLine={false} tickLine={false} interval={Math.floor(data.dailyData.length / 10)} />
                    <YAxis tick={{ fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 10 }} axisLine={false} tickLine={false} tickFormatter={v => fmtRsd(v)} width={70} />
                    <Tooltip contentStyle={tooltipStyle} formatter={(v: number | string | undefined) => [fmtRsd(Number(v ?? 0)), "Prihod"]} />
                    <ReferenceLine y={data.meanRevenue} stroke={PAL.yellow} strokeDasharray="4 4" label={{ value: "Prosek", fill: PAL.yellow, fontSize: 10 }} />
                    <Line type="monotone" dataKey="revenue" stroke={PAL.blue} strokeWidth={1.5}
                      dot={(props: Record<string, unknown>) => {
                        const pl = props.payload as Record<string, unknown>;
                        if (pl?.isTarget) return <circle key={props.key as string} cx={props.cx as number} cy={props.cy as number} r={5} fill={PAL.red} stroke={PAL.red} />;
                        return <></>;
                      }} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
              {data.top5Articles.length > 0 && (
                <div>
                  <h4 className="mb-2 text-xs font-semibold text-[var(--text-primary)]">Top 5 artikala — {data.analysisDate}</h4>
                  <div className="space-y-1.5">
                    {data.top5Articles.map((a, i) => (
                      <div key={a.artikalId} className="flex items-center gap-3 rounded-lg bg-[var(--surface-elevated)] border border-[var(--border-default)] px-3 py-2">
                        <span className="text-[var(--text-primary)] font-bold w-5 text-center">{i + 1}</span>
                        <div className="flex-1 min-w-0"><div className="text-sm text-[var(--text-primary)] truncate">{a.naziv}</div><div className="text-[10px] text-[var(--text-primary)]">{a.kategorija}</div></div>
                        <div className="text-right"><div className="text-sm font-semibold text-[var(--text-primary)]">{fmtRsd(a.revenue)}</div><div className="text-[10px] text-[var(--text-primary)]">{a.units} kom</div></div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </>
          )}
          {!data && !loading && <p className="text-[var(--text-primary)] text-sm">Izaberite dan za analizu.</p>}
        </>
      )}

      {subView === "heatmap" && (
        <>
          {heatmapLoading ? <Skeleton rows={7} /> : heatmap ? (
            <div className="space-y-4">
              <h4 className="text-xs font-semibold text-[var(--text-primary)]">Prosečan prihod po danu u nedelji</h4>
              <div className="grid grid-cols-7 gap-2">
                {heatmap.byDay.map(d => {
                  const maxRev = Math.max(...heatmap.byDay.map(x => x.avgRevenue));
                  const intensity = maxRev > 0 ? d.avgRevenue / maxRev : 0;
                  return (
                    <div key={d.day} className="rounded-lg border border-[var(--border-default)] p-3 text-center transition hover:scale-105"
                      style={{ background: `rgba(79, 142, 247, ${0.05 + intensity * 0.4})` }}>
                      <div className="text-xs font-bold text-[var(--text-primary)]">{d.dayName}</div>
                      <div className="text-sm font-bold text-[var(--text-primary)] mt-1">{fmtRsd(d.avgRevenue)}</div>
                      <div className="text-[10px] text-[var(--text-primary)]">{d.avgUnits.toFixed(0)} kom</div>
                    </div>
                  );
                })}
              </div>
              <h4 className="text-xs font-semibold text-[var(--text-primary)] mt-4">Trend po danima</h4>
              <div className="h-[180px]">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={heatmap.byDay} barSize={36}>
                    <CartesianGrid stroke="var(--c-2a3045, var(--theme-color-2a3045, #2A3045))" vertical={false} />
                    <XAxis dataKey="dayName" tick={{ fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 11 }} axisLine={false} tickLine={false} />
                    <YAxis tick={{ fill: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 10 }} axisLine={false} tickLine={false} tickFormatter={v => fmtRsd(v)} />
                    <Tooltip contentStyle={tooltipStyle} formatter={(v: number | string | undefined) => [fmtRsd(Number(v ?? 0)), "Avg Prihod"]} />
                    <Bar dataKey="avgRevenue" fill={PAL.blue} radius={[4, 4, 0, 0]}>
                      {heatmap.byDay.map((d, i) => {
                        const maxR = Math.max(...heatmap.byDay.map(x => x.avgRevenue));
                        const int2 = maxR > 0 ? d.avgRevenue / maxR : 0;
                        return <Cell key={i} fill={int2 > 0.7 ? PAL.green : int2 > 0.4 ? PAL.blue : PAL.textMuted} />;
                      })}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>
          ) : <p className="text-[var(--text-primary)] text-sm">Nema podataka za heatmapu.</p>}
        </>
      )}
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════
// TAB 6: ABC + ŽIVOTNI CIKLUS
// ══════════════════════════════════════════════════════════════════

function AbcLifecycleTab({
  abcData, abcLoading,
  lifecycle, lifecycleLoading,
  analyticsContext,
}: {
  abcData: AbcItem[]; abcLoading: boolean;
  lifecycle: LifecycleResult | null; lifecycleLoading: boolean;
  analyticsContext: InsightAnalyticsContext;
}) {
  const [subView, setSubView] = useState<"abc" | "lifecycle">("abc");
  const [showAll, setShowAll] = useState(false);

  return (
    <div className="space-y-5">
      <SectionHeader title="ABC Klasifikacija & Životni Ciklus Proizvoda" subtitle="Pareto analiza prihoda i klasifikacija faze životnog ciklusa" />
      <div className="flex gap-2">
        <button onClick={() => setSubView("abc")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "abc" ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)]"}`}>📊 ABC</button>
        <button onClick={() => setSubView("lifecycle")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "lifecycle" ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)]"}`}>🔄 Životni Ciklus</button>
      </div>

      {subView === "abc" && (
        abcLoading ? <Skeleton rows={8} /> : !abcData.length ? <p className="text-[var(--text-primary)] text-sm">Nema podataka.</p> : (
          <AbcContent data={abcData} showAll={showAll} setShowAll={setShowAll} analyticsContext={analyticsContext} />
        )
      )}

      {subView === "lifecycle" && (
        lifecycleLoading ? <Skeleton rows={8} /> : !lifecycle ? <p className="text-[var(--text-primary)] text-sm">Nema podataka životnog ciklusa.</p> : (
          <LifecycleContent data={lifecycle} analyticsContext={analyticsContext} />
        )
      )}
    </div>
  );
}

function AbcContent({ data, showAll, setShowAll, analyticsContext }: { data: AbcItem[]; showAll: boolean; setShowAll: (v: boolean) => void; analyticsContext: InsightAnalyticsContext }) {
  const revenueA = data.filter(x => x.abcClass === "A").reduce((s, x) => s + x.totalRevenue, 0);
  const revenueB = data.filter(x => x.abcClass === "B").reduce((s, x) => s + x.totalRevenue, 0);
  const revenueC = data.filter(x => x.abcClass === "C").reduce((s, x) => s + x.totalRevenue, 0);
  const total = revenueA + revenueB + revenueC;
  const donutData = [
    { name: "Klasa A", value: revenueA, count: data.filter(x => x.abcClass === "A").length },
    { name: "Klasa B", value: revenueB, count: data.filter(x => x.abcClass === "B").length },
    { name: "Klasa C", value: revenueC, count: data.filter(x => x.abcClass === "C").length },
  ];
  const displayed = showAll ? data : data.slice(0, 15);

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 gap-5 lg:grid-cols-3">
        <div className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4">
          <h4 className="mb-2 text-xs font-semibold text-[var(--text-primary)]">ABC Distribucija</h4>
          <div className="h-[180px]">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie data={donutData} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={75} innerRadius={40} paddingAngle={3}>
                  {donutData.map((_, i) => <Cell key={i} fill={[PAL.green, PAL.yellow, PAL.red][i]} />)}
                </Pie>
                <Tooltip contentStyle={tooltipStyle} formatter={(v: number | string | undefined) => [fmtRsd(Number(v ?? 0)), "Prihod"]} />
                <Legend formatter={v => <span style={{ color: "var(--c-8a95b0, var(--theme-color-8a95b0, #8A95B0))", fontSize: 11 }}>{v}</span>} />
              </PieChart>
            </ResponsiveContainer>
          </div>
          <div className="grid grid-cols-3 gap-2 mt-2">
            {donutData.map((d, i) => (
              <div key={i} className="text-center">
                <div className="text-xs font-bold" style={{ color: [PAL.green, PAL.yellow, PAL.red][i] }}>{d.count} SKU</div>
                <div className="text-[10px] text-[var(--text-primary)]">{total > 0 ? ((d.value / total) * 100).toFixed(0) : 0}% prih.</div>
              </div>
            ))}
          </div>
        </div>
        <div className="lg:col-span-2 space-y-2">
          <div className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-3 text-sm text-[var(--text-primary)]">
            <span className="font-semibold text-[var(--text-primary)]">Klasa A</span> — top artikli (~70% prihoda). Prioritet u nabavci, nikad out-of-stock.
          </div>
          <div className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-3 text-sm text-[var(--text-primary)]">
            <span className="font-semibold text-[var(--text-primary)]">Klasa B</span> — srednji artikli (70-90% kum.). Pratiti trendove i pravovremeno dopunjavati.
          </div>
          <div className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-3 text-sm text-[var(--text-primary)]">
            <span className="font-semibold text-[var(--text-primary)]">Klasa C</span> — dugi rep. Razmotriti likvidaciju ili specijalne promocije.
          </div>
        </div>
      </div>
      <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
        <div className="mb-3 p-3">
          <AnalyticsTableToolbar
            tableKey="insight-abc-classification"
            tableTitle="Insight Studio - ABC klasifikacija"
            columns={abcColumns}
            rows={displayed}
            filters={analyticsContext.filters}
            metadata={analyticsContext.metadata}
            defaultOrientation="landscape"
          />
        </div>
        <table className="w-full text-xs">
          <thead><tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
            <th className="px-3 py-2 text-left">Artikal</th><th className="px-3 py-2 text-left">Kat.</th>
            <th className="px-3 py-2 text-right">Prihod</th><th className="px-3 py-2 text-right">Udeo%</th>
            <th className="px-3 py-2 text-right">Kum.%</th><th className="px-3 py-2 text-right">Kom</th>
            <th className="px-3 py-2 text-center">Klasa</th>
          </tr></thead>
          <tbody>
            {displayed.map(item => (
              <tr
                key={item.artikalId}
                className="cursor-pointer border-b border-[var(--border-default)] hover:bg-[var(--surface-light)] transition"
                onClick={() => analyticsContext.openSnapshotDetail("insight-abc-classification", String(item.artikalId), item.naziv, "Insight Studio - ABC klasifikacija", abcColumns, item)}
              >
                <td className="px-3 py-2 text-[var(--text-primary)] max-w-[160px] truncate">{item.naziv}</td>
                <td className="px-3 py-2 text-[var(--text-primary)]">{item.kategorija}</td>
                <td className="px-3 py-2 text-right">{fmtRsd(item.totalRevenue)}</td>
                <td className="px-3 py-2 text-right text-[var(--text-primary)]">{item.revPct.toFixed(1)}%</td>
                <td className="px-3 py-2 text-right text-[var(--text-primary)]">{item.cumulativePct.toFixed(1)}%</td>
                <td className="px-3 py-2 text-right text-[var(--text-primary)]">{item.totalUnits}</td>
                <td className="px-3 py-2 text-center"><Badge label={item.abcClass} color={ABC_COLORS[item.abcClass]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {data.length > 15 && (
        <button onClick={() => setShowAll(!showAll)} className="text-xs text-[var(--text-primary)] hover:underline">
          {showAll ? "Prikaži manje" : `Prikaži svih ${data.length} →`}
        </button>
      )}
    </div>
  );
}

function LifecycleContent({ data, analyticsContext }: { data: LifecycleResult; analyticsContext: InsightAnalyticsContext }) {
  const stages = ["LAUNCH", "GROWTH", "MATURE", "DECLINE"] as const;
  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {stages.map(s => (
          <div key={s} className="rounded-xl border px-3 py-2" style={{ borderColor: `${STAGE_COLORS[s]}33`, background: `${STAGE_COLORS[s]}08` }}>
            <div className="text-[10px] uppercase" style={{ color: STAGE_COLORS[s] }}>{STAGE_LABELS[s]}</div>
            <div className="text-xl font-bold" style={{ color: STAGE_COLORS[s] }}>{data.summary[s.toLowerCase() as keyof typeof data.summary]}</div>
            <div className="text-[10px] text-[var(--text-primary)]">artikala</div>
          </div>
        ))}
      </div>
      <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
        <div className="mb-3 p-3">
          <AnalyticsTableToolbar
            tableKey="insight-lifecycle"
            tableTitle="Insight Studio - zivotni ciklus"
            columns={lifecycleColumns}
            rows={data.items.slice(0, 30)}
            filters={analyticsContext.filters}
            metadata={analyticsContext.metadata}
            defaultOrientation="landscape"
          />
        </div>
        <table className="w-full text-xs">
          <thead><tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
            <th className="px-3 py-2 text-left">Artikal</th><th className="px-3 py-2 text-left">Kat.</th>
            <th className="px-3 py-2 text-right">Prodato</th><th className="px-3 py-2 text-right">Trend</th>
            <th className="px-3 py-2 text-right">Zaliha</th><th className="px-3 py-2 text-center">Faza</th>
          </tr></thead>
          <tbody>
            {data.items.slice(0, 30).map(it => (
              <tr
                key={it.artikalId}
                className="cursor-pointer border-b border-[var(--border-default)] hover:bg-[var(--surface-light)] transition"
                onClick={() => analyticsContext.openSnapshotDetail("insight-lifecycle", String(it.artikalId), it.naziv, "Insight Studio - zivotni ciklus", lifecycleColumns, it)}
              >
                <td className="px-3 py-2 text-[var(--text-primary)] max-w-[160px] truncate">{it.naziv}</td>
                <td className="px-3 py-2 text-[var(--text-primary)]">{it.kategorija}</td>
                <td className="px-3 py-2 text-right">{it.totalUnits}</td>
                <td className="px-3 py-2 text-right" style={{ color: it.trendPct >= 0 ? PAL.green : PAL.red }}>{it.trendPct >= 0 ? "+" : ""}{it.trendPct.toFixed(0)}%</td>
                <td className="px-3 py-2 text-right text-[var(--text-primary)]">{it.currentStock}</td>
                <td className="px-3 py-2 text-center"><Badge label={STAGE_LABELS[it.stage]} color={STAGE_COLORS[it.stage]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════
// TAB 7: ZALIHE & DEPLECIJA
// ══════════════════════════════════════════════════════════════════

function StockTab({
  agingItems, agingLoading, agingSummary,
  depletion, depletionLoading,
  analyticsContext,
}: {
  agingItems: AgingItem[]; agingLoading: boolean;
  agingSummary?: { totalSKU: number; critical: number; warning: number; watch: number; active: number; criticalStockValue: number };
  depletion: DepletionResult | null; depletionLoading: boolean;
  analyticsContext: InsightAnalyticsContext;
}) {
  const [subView, setSubView] = useState<"aging" | "depletion">("aging");
  const [filter, setFilter] = useState("Sve");
  const [showAll, setShowAll] = useState(false);

  return (
    <div className="space-y-5">
      <SectionHeader title="Stanje Zaliha & Prognoza Iscrpljenja" subtitle="Aging analiza + automatski forecast datuma OOS-a" />
      <div className="flex gap-2">
        <button onClick={() => setSubView("aging")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "aging" ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)]"}`}>📦 Aging</button>
        <button onClick={() => setSubView("depletion")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "depletion" ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)]"}`}>📉 Deplecija</button>
      </div>

      {subView === "aging" && (
        agingLoading ? <Skeleton rows={8} /> : !agingItems.length ? <p className="text-[var(--text-primary)] text-sm">Nema zaliha.</p> : (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              {[
                { name: "Kritično (>90d)", value: agingSummary?.critical ?? 0, fill: PAL.red },
                { name: "Upozorenje (>60d)", value: agingSummary?.warning ?? 0, fill: PAL.orange },
                { name: "Pazi (>30d)", value: agingSummary?.watch ?? 0, fill: PAL.yellow },
                { name: "Aktivno (<30d)", value: agingSummary?.active ?? 0, fill: PAL.green },
              ].map(b => (
                <div key={b.name} className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-3">
                  <div className="text-[10px] text-[var(--text-primary)] uppercase">{b.name}</div>
                  <div className="text-2xl font-bold" style={{ color: b.fill }}>{b.value}</div>
                </div>
              ))}
            </div>
            {agingSummary && agingSummary.criticalStockValue > 0 && (
              <AlertBanner severity="danger">⚠️ Kritična zaliha (&gt;90d bez prodaje): <strong>{fmtRsd(agingSummary.criticalStockValue)}</strong></AlertBanner>
            )}
            <div className="flex gap-2 flex-wrap">
              {["Sve", "Kritično", "Upozorenje", "Pazi", "Aktivno"].map(c => (
                <button key={c} onClick={() => { setFilter(c); setShowAll(false); }}
                  className={`rounded-lg px-3 py-1 text-xs font-medium transition ${filter === c ? "ring-1 ring-current" : "text-[var(--text-primary)]"}`}
                  style={filter === c && c !== "Sve" ? { color: AGING_COLORS[c], background: `${AGING_COLORS[c]}18` } : undefined}>
                  {c}
                </button>
              ))}
            </div>
            <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
              <div className="mb-3 p-3">
                <AnalyticsTableToolbar
                  tableKey="insight-aging-stock"
                  tableTitle="Insight Studio - aging stock"
                  columns={agingColumns}
                  rows={filter === "Sve" ? agingItems : agingItems.filter(x => x.agingCategory === filter)}
                  filters={analyticsContext.filters}
                  metadata={analyticsContext.metadata}
                  defaultOrientation="landscape"
                />
              </div>
              <table className="w-full text-xs">
                <thead><tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
                  <th className="px-3 py-2 text-left">Artikal</th><th className="px-3 py-2 text-left">Kat.</th>
                  <th className="px-3 py-2 text-right">Zaliha</th><th className="px-3 py-2 text-right">Posl. prod.</th>
                  <th className="px-3 py-2 text-right">Dana</th><th className="px-3 py-2 text-center">Status</th>
                </tr></thead>
                <tbody>
                  {(() => {
                    const f = filter === "Sve" ? agingItems : agingItems.filter(x => x.agingCategory === filter);
                    return (showAll ? f : f.slice(0, 20)).map(item => (
                      <tr
                        key={item.id}
                        className="cursor-pointer border-b border-[var(--border-default)] hover:bg-[var(--surface-light)] transition"
                        onClick={() => analyticsContext.openSnapshotDetail("insight-aging-stock", String(item.id), item.naziv, "Insight Studio - aging stock", agingColumns, item)}
                      >
                        <td className="px-3 py-2 text-[var(--text-primary)] max-w-[160px] truncate">{item.naziv}</td>
                        <td className="px-3 py-2 text-[var(--text-primary)]">{item.kategorija}</td>
                        <td className="px-3 py-2 text-right">{item.kolicina}</td>
                        <td className="px-3 py-2 text-right text-[var(--text-primary)]">{item.lastSaleDate}</td>
                        <td className="px-3 py-2 text-right font-semibold" style={{ color: AGING_COLORS[item.agingCategory] }}>{item.daysWithoutSale}d</td>
                        <td className="px-3 py-2 text-center"><Badge label={item.agingCategory} color={AGING_COLORS[item.agingCategory]} /></td>
                      </tr>
                    ));
                  })()}
                </tbody>
              </table>
            </div>
            {(() => {
              const f = filter === "Sve" ? agingItems : agingItems.filter(x => x.agingCategory === filter);
              return f.length > 20 && (
                <button onClick={() => setShowAll(!showAll)} className="text-xs text-[var(--text-primary)] hover:underline">
                  {showAll ? "Manje" : `Svih ${f.length} →`}
                </button>
              );
            })()}
          </div>
        )
      )}

      {subView === "depletion" && (
        depletionLoading ? <Skeleton rows={8} /> : !depletion ? <p className="text-[var(--text-primary)] text-sm">Nema podataka deplecije.</p> : (
          <div className="space-y-4">
            <div className="grid grid-cols-3 gap-3">
              <MiniStat label="Kritičan OOS" value={depletion.criticalCount} color={PAL.red} />
              <MiniStat label="Ukupno forecastova" value={depletion.forecasts.length} />
              <MiniStat label="At-Risk prihod" value={fmtRsd(depletion.totalAtRiskRevenue)} color={PAL.orange} />
            </div>
            {depletion.criticalCount > 0 && (
              <AlertBanner severity="danger">⚡ {depletion.criticalCount} artikala ce biti OOS u narednih 7 dana! At-risk prihod: {fmtRsd(depletion.totalAtRiskRevenue)}</AlertBanner>
            )}
            <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
              <div className="mb-3 p-3">
                <AnalyticsTableToolbar
                  tableKey="insight-stock-depletion"
                  tableTitle="Insight Studio - stock depletion"
                  columns={depletionColumns}
                  rows={depletion.forecasts.slice(0, 30)}
                  filters={analyticsContext.filters}
                  metadata={analyticsContext.metadata}
                  defaultOrientation="landscape"
                />
              </div>
              <table className="w-full text-xs">
                <thead><tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
                  <th className="px-3 py-2 text-left">Artikal</th><th className="px-3 py-2 text-left">Kat.</th>
                  <th className="px-3 py-2 text-right">Zaliha</th><th className="px-3 py-2 text-right">Avg/dan</th>
                  <th className="px-3 py-2 text-right">Dana do OOS</th><th className="px-3 py-2 text-right">Datum OOS</th>
                  <th className="px-3 py-2 text-right">At-Risk</th><th className="px-3 py-2 text-center">Sev.</th>
                </tr></thead>
                <tbody>
                  {depletion.forecasts.slice(0, 30).map(f => (
                    <tr
                      key={f.artikalId}
                      className="cursor-pointer border-b border-[var(--border-default)] hover:bg-[var(--surface-light)] transition"
                      onClick={() => analyticsContext.openSnapshotDetail("insight-stock-depletion", String(f.artikalId), f.naziv, "Insight Studio - stock depletion", depletionColumns, f)}
                    >
                      <td className="px-3 py-2 text-[var(--text-primary)] max-w-[140px] truncate">{f.naziv}</td>
                      <td className="px-3 py-2 text-[var(--text-primary)]">{f.kategorija}</td>
                      <td className="px-3 py-2 text-right">{f.currentStock}</td>
                      <td className="px-3 py-2 text-right text-[var(--text-primary)]">{f.avgDailySales.toFixed(2)}</td>
                      <td className="px-3 py-2 text-right font-bold" style={{ color: SEVERITY_COLORS[f.severity] }}>{f.daysUntilOOS > 365 ? "∞" : `${f.daysUntilOOS}d`}</td>
                      <td className="px-3 py-2 text-right text-[var(--text-primary)]">{f.daysUntilOOS > 365 ? "—" : f.depletionDate}</td>
                      <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtRsd(f.atRiskRevenue)}</td>
                      <td className="px-3 py-2 text-center"><Badge label={f.severity} color={SEVERITY_COLORS[f.severity]} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )
      )}
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════
// TAB 8: NABAVKA 2.0 (Smart Reorder)
// ══════════════════════════════════════════════════════════════════

function ReorderTab2({
  smartData, smartLoading,
  v1Items, v1Loading, v1Summary,
  analyticsContext,
}: {
  smartData: SmartReorderResult | null; smartLoading: boolean;
  v1Items: ReorderItem[]; v1Loading: boolean;
  v1Summary?: { criticalCount: number; urgentCount: number; recommendedCount: number; totalReorderValue: number };
  analyticsContext: InsightAnalyticsContext;
}) {
  const [urgencyFilter, setUrgencyFilter] = useState("Sve");
  const [showAll, setShowAll] = useState(false);
  const [subView, setSubView] = useState<"artikli" | "kategorije" | "dobavljaci">("artikli");

  const loading = smartLoading || v1Loading;
  if (loading) return <Skeleton rows={10} />;

  // Use smart data if available, else fallback to v1
  const useSmart = smartData && smartData.items.length > 0;

  const items = useSmart ? smartData!.items : v1Items;
  const summary = useSmart ? smartData!.summary : v1Summary;

  const urgencies = ["Sve", "KRITIČNO", "HITNO", "PREPORUČUJE SE", "OK"];
  const filtered = urgencyFilter === "Sve" ? items : items.filter(x => x.urgency === urgencyFilter);
  const filteredReorder = filtered.filter(x => urgencyFilter !== "OK" ? true : x.needsReorder);
  const displayed = showAll ? filteredReorder : filteredReorder.slice(0, 20);

  return (
    <div className="space-y-5">
      <SectionHeader title="Nabavka 2.0 — Smart Reorder Engine" subtitle="Prioritizacija nabavke sa ROI projekcijom, verovatnoćom reordera i margin forecast-om" />

      <div className="rounded-xl border border-[var(--border-default)]/20 bg-[var(--surface-elevated)]/5 px-4 py-3 text-xs text-[var(--text-primary)]">
        Reorder prioriteti sada se prvenstveno izvode iz demand, inventory, price i trend intelligence signala, a legacy plan ostaje rezervni fallback.
      </div>

      {/* Summary KPIs */}
      {summary && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-6">
          <div className="rounded-xl border border-[var(--border-default)]/30 bg-[var(--surface-elevated)]/10 p-3">
            <div className="text-[10px] text-[var(--text-primary)] uppercase">Kritično</div>
            <div className="text-2xl font-bold text-[var(--text-primary)]">{summary.criticalCount}</div>
          </div>
          <div className="rounded-xl border border-[var(--border-default)]/30 bg-[var(--surface-elevated)]/10 p-3">
            <div className="text-[10px] text-[var(--text-primary)] uppercase">Hitno</div>
            <div className="text-2xl font-bold text-[var(--text-primary)]">{summary.urgentCount}</div>
          </div>
          <div className="rounded-xl border border-[var(--border-default)]/30 bg-[var(--surface-elevated)]/10 p-3">
            <div className="text-[10px] text-[var(--text-primary)] uppercase">Preporučuje se</div>
            <div className="text-2xl font-bold text-[var(--text-primary)]">{summary.recommendedCount}</div>
          </div>
          <div className="rounded-xl border border-[var(--border-default)]/30 bg-[var(--surface-elevated)]/10 p-3">
            <div className="text-[10px] text-[var(--text-primary)] uppercase">Trošak nabavke</div>
            <div className="text-lg font-bold text-[var(--text-primary)]">{fmtRsd("totalReorderCost" in summary ? (summary as {totalReorderCost: number}).totalReorderCost : ("totalReorderValue" in summary ? (summary as {totalReorderValue: number}).totalReorderValue : 0))}</div>
          </div>
          {useSmart && (
            <>
              <div className="rounded-xl border border-[var(--border-default)]/30 bg-[var(--surface-elevated)]/10 p-3">
                <div className="text-[10px] text-[var(--text-primary)] uppercase">Očekivani prihod</div>
                <div className="text-lg font-bold text-[var(--text-primary)]">{fmtRsd(smartData!.summary.expectedRevenueFromReorder)}</div>
              </div>
              <div className="rounded-xl border border-[var(--border-default)]/30 bg-[var(--surface-elevated)]/10 p-3">
                <div className="text-[10px] text-[var(--text-primary)] uppercase">Očekivani profit</div>
                <div className="text-lg font-bold text-[var(--text-primary)]">{fmtRsd(smartData!.summary.expectedProfitFromReorder)}</div>
              </div>
            </>
          )}
        </div>
      )}

      {/* Sub navigation for smart reorder */}
      {useSmart && (
        <div className="flex gap-2">
          <button onClick={() => setSubView("artikli")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "artikli" ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)]"}`}>📋 Po artiklima</button>
          <button onClick={() => setSubView("kategorije")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "kategorije" ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)]"}`}>📊 Po kategorijama</button>
          <button onClick={() => setSubView("dobavljaci")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "dobavljaci" ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)]"}`}>🏭 Po dobavljačima</button>
        </div>
      )}

      {subView === "artikli" && (
        <>
          <div className="flex gap-2 flex-wrap">
            {urgencies.map(u => (
              <button key={u} onClick={() => { setUrgencyFilter(u); setShowAll(false); }}
                className={`rounded-lg px-3 py-1 text-xs font-medium transition ${urgencyFilter === u ? "ring-1 ring-current" : "text-[var(--text-primary)]"}`}
                style={urgencyFilter === u && u !== "Sve" ? { color: URGENCY_COLORS[u], background: `${URGENCY_COLORS[u]}18` } : undefined}>
                {u}
              </button>
            ))}
          </div>
          <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
            <div className="mb-3 p-3">
              <AnalyticsTableToolbar
                tableKey="insight-smart-reorder-items"
                tableTitle="Insight Studio - smart reorder po artiklima"
                columns={reorderItemColumns}
                rows={displayed}
                filters={analyticsContext.filters}
                metadata={analyticsContext.metadata}
                defaultOrientation="landscape"
              />
            </div>
            <table className="w-full text-xs">
              <thead><tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
                <th className="px-3 py-2 text-left">Artikal</th>
                <th className="px-2 py-2 text-left">Kat.</th>
                <th className="px-2 py-2 text-left">Dobavljač</th>
                <th className="px-2 py-2 text-right">Zaliha</th>
                <th className="px-2 py-2 text-right">V/dan</th>
                <th className="px-2 py-2 text-right">DOH</th>
                <th className="px-2 py-2 text-right">Preporuka</th>
                {useSmart && <th className="px-2 py-2 text-right">Marža%</th>}
                {useSmart && <th className="px-2 py-2 text-right">Prob%</th>}
                <th className="px-2 py-2 text-center">Hitnost</th>
              </tr></thead>
              <tbody>
                {displayed.map(item => (
                  <tr
                    key={item.artikalId}
                    className="cursor-pointer border-b border-[var(--border-default)] hover:bg-[var(--surface-light)] transition"
                    onClick={() => analyticsContext.openSnapshotDetail("insight-smart-reorder-items", String(item.artikalId), item.naziv, "Insight Studio - smart reorder po artiklima", reorderItemColumns, item)}
                  >
                    <td className="px-3 py-2 text-[var(--text-primary)] max-w-[130px] truncate">{item.naziv}</td>
                    <td className="px-2 py-2 text-[var(--text-primary)]">{item.kategorija}</td>
                    <td className="px-2 py-2 text-[var(--text-primary)]">{item.dobavljacNaziv}</td>
                    <td className="px-2 py-2 text-right">{item.currentStock}</td>
                    <td className="px-2 py-2 text-right text-[var(--text-primary)]">{item.avgDailySales.toFixed(2)}</td>
                    <td className="px-2 py-2 text-right font-semibold" style={{ color: item.doh < 7 ? PAL.red : item.doh < 14 ? PAL.orange : item.doh < 30 ? PAL.yellow : PAL.green }}>
                      {item.doh > 900 ? "∞" : `${item.doh.toFixed(0)}d`}
                    </td>
                    <td className="px-2 py-2 text-right font-bold" style={{ color: item.needsReorder ? PAL.yellow : PAL.textMuted }}>
                      {item.needsReorder ? `+${item.recommendedQty}` : "—"}
                    </td>
                    {useSmart && (() => { const s = item as unknown as { marginPct?: number }; return <td className="px-2 py-2 text-right" style={{ color: s.marginPct !== undefined && s.marginPct >= 20 ? PAL.green : PAL.orange }}>{s.marginPct !== undefined ? fmtPct(s.marginPct) : "—"}</td>; })()}
                    {useSmart && (() => { const s = item as unknown as { reorderProbability?: number }; return <td className="px-2 py-2 text-right text-[var(--text-primary)]">{s.reorderProbability !== undefined ? fmtPct(s.reorderProbability) : "—"}</td>; })()}
                    <td className="px-2 py-2 text-center"><Badge label={item.urgency} color={URGENCY_COLORS[item.urgency]} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {filteredReorder.length > 20 && (
            <button onClick={() => setShowAll(!showAll)} className="text-xs text-[var(--text-primary)] hover:underline">
              {showAll ? "Manje" : `Svih ${filteredReorder.length} →`}
            </button>
          )}
        </>
      )}

      {subView === "kategorije" && useSmart && (
        <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
          <div className="mb-3 p-3">
            <AnalyticsTableToolbar
              tableKey="insight-smart-reorder-categories"
              tableTitle="Insight Studio - smart reorder po kategorijama"
              columns={reorderCategoryColumns}
              rows={smartData!.byCategoryPlan}
              filters={analyticsContext.filters}
              metadata={analyticsContext.metadata}
              defaultOrientation="landscape"
            />
          </div>
          <table className="w-full text-xs">
            <thead><tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
              <th className="px-3 py-2 text-left">Kategorija</th>
              <th className="px-3 py-2 text-right">Artikala</th>
              <th className="px-3 py-2 text-right">Kritično</th>
              <th className="px-3 py-2 text-right">Hitno</th>
              <th className="px-3 py-2 text-right">Trošak nabavke</th>
              <th className="px-3 py-2 text-right">Oč. prihod</th>
              <th className="px-3 py-2 text-right">Avg marža</th>
            </tr></thead>
            <tbody>
              {smartData!.byCategoryPlan.map((c, i) => (
                <tr
                  key={i}
                  className="cursor-pointer border-b border-[var(--border-default)] hover:bg-[var(--surface-light)] transition"
                  onClick={() => analyticsContext.openSnapshotDetail("insight-smart-reorder-categories", c.kategorija, c.kategorija, "Insight Studio - smart reorder po kategorijama", reorderCategoryColumns, c)}
                >
                  <td className="px-3 py-2 font-medium text-[var(--text-primary)]">{c.kategorija}</td>
                  <td className="px-3 py-2 text-right text-[var(--text-primary)]">{c.totalItems}</td>
                  <td className="px-3 py-2 text-right" style={{ color: c.criticalCount > 0 ? PAL.red : PAL.textSecondary }}>{c.criticalCount}</td>
                  <td className="px-3 py-2 text-right" style={{ color: c.urgentCount > 0 ? PAL.orange : PAL.textSecondary }}>{c.urgentCount}</td>
                  <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtRsd(c.totalReorderCost)}</td>
                  <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtRsd(c.expectedRevenue)}</td>
                  <td className="px-3 py-2 text-right" style={{ color: c.avgMargin >= 20 ? PAL.green : PAL.yellow }}>{fmtPct(c.avgMargin)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {subView === "dobavljaci" && useSmart && (
        <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
          <div className="mb-3 p-3">
            <AnalyticsTableToolbar
              tableKey="insight-smart-reorder-suppliers"
              tableTitle="Insight Studio - smart reorder po dobavljacima"
              columns={reorderSupplierColumns}
              rows={smartData!.bySupplierPlan}
              filters={analyticsContext.filters}
              metadata={analyticsContext.metadata}
              defaultOrientation="landscape"
            />
          </div>
          <table className="w-full text-xs">
            <thead><tr className="border-b border-[var(--border-default)] bg-[var(--surface-elevated)] text-[10px] uppercase tracking-wider text-[var(--text-primary)]">
              <th className="px-3 py-2 text-left">Dobavljač</th>
              <th className="px-3 py-2 text-right">Artikala</th>
              <th className="px-3 py-2 text-right">Kritično</th>
              <th className="px-3 py-2 text-right">Trošak nabavke</th>
              <th className="px-3 py-2 text-right">Avg prob. reordering</th>
            </tr></thead>
            <tbody>
              {smartData!.bySupplierPlan.map((s, i) => (
                <tr
                  key={i}
                  className="cursor-pointer border-b border-[var(--border-default)] hover:bg-[var(--surface-light)] transition"
                  onClick={() => analyticsContext.openSnapshotDetail("insight-smart-reorder-suppliers", s.dobavljac, s.dobavljac, "Insight Studio - smart reorder po dobavljacima", reorderSupplierColumns, s)}
                >
                  <td className="px-3 py-2 font-medium text-[var(--text-primary)]">{s.dobavljac}</td>
                  <td className="px-3 py-2 text-right text-[var(--text-primary)]">{s.totalItems}</td>
                  <td className="px-3 py-2 text-right" style={{ color: s.criticalCount > 0 ? PAL.red : PAL.textSecondary }}>{s.criticalCount}</td>
                  <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtRsd(s.totalReorderCost)}</td>
                  <td className="px-3 py-2 text-right text-[var(--text-primary)]">{fmtPct(s.avgReorderProbability)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════
// MAIN PAGE
// ══════════════════════════════════════════════════════════════════

export default function InsightStudioPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [activeTab, setActiveTab] = useState<TabKey>("pregled");
  const [periodDays, setPeriodDays] = useState(30);

  const toDate = toDateStr(new Date());
  const fromDate = toDateStr(daysAgo(periodDays));

  const analyticsFilters = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "fromDate", label: "Od datuma", value: fromDate },
    { key: "toDate", label: "Do datuma", value: toDate },
    { key: "periodDays", label: "Period (dana)", value: periodDays },
    { key: "activeTab", label: "Aktivni tab", value: activeTab },
  ], [activeTab, fromDate, periodDays, toDate]);

  // ── V1 State ──
  const [kpi, setKpi] = useState<KpiSnapshot | null>(null);
  const [kpiLoading, setKpiLoading] = useState(false);
  const [kpiError, setKpiError] = useState<string | null>(null);
  const [suppliers, setSuppliers] = useState<SupplierScore[]>([]);
  const [supplierLoading, setSupplierLoading] = useState(false);
  const [catData, setCatData] = useState<{ byCategory: CategoryStat[]; byGender: GenderStat[] } | null>(null);
  const [catLoading, setCatLoading] = useState(false);
  const [dailyDate, setDailyDate] = useState(toDateStr(daysAgo(1)));
  const [daily, setDaily] = useState<DailyAnalysis | null>(null);
  const [dailyLoading, setDailyLoading] = useState(false);
  const [abcData, setAbcData] = useState<AbcItem[]>([]);
  const [abcLoading, setAbcLoading] = useState(false);
  const [agingItems, setAgingItems] = useState<AgingItem[]>([]);
  const [agingSummary, setAgingSummary] = useState<{ totalSKU: number; critical: number; warning: number; watch: number; active: number; criticalStockValue: number } | undefined>();
  const [agingLoading, setAgingLoading] = useState(false);
  const [reorderItems, setReorderItems] = useState<ReorderItem[]>([]);
  const [reorderSummary, setReorderSummary] = useState<{ criticalCount: number; urgentCount: number; recommendedCount: number; totalReorderValue: number } | undefined>();
  const [reorderLoading, setReorderLoading] = useState(false);

  // ── V2 State ──
  const [changelog, setChangelog] = useState<WeeklyChangelog | null>(null);
  const [changelogLoading, setChangelogLoading] = useState(false);
  const [marginAlerts, setMarginAlerts] = useState<MarginAlertResult | null>(null);
  const [marginAlertsLoading, setMarginAlertsLoading] = useState(false);
  const [intelligenceDemand, setIntelligenceDemand] = useState<DemandSignalItem[]>([]);
  const [intelligenceInventory, setIntelligenceInventory] = useState<InventoryRiskSignalItem[]>([]);
  const [intelligencePrice, setIntelligencePrice] = useState<PriceIntelligenceItem[]>([]);
  const [intelligenceTrend, setIntelligenceTrend] = useState<TrendMomentumItem[]>([]);
  const [intelligenceAsOfDate, setIntelligenceAsOfDate] = useState<string | null>(null);
  const [intelligenceLoading, setIntelligenceLoading] = useState(false);
  const [intelligenceError, setIntelligenceError] = useState<string | null>(null);
  const [supplierV2, setSupplierV2] = useState<SupplierScoreV2[]>([]);
  const [supplierV2Loading, setSupplierV2Loading] = useState(false);
  const [heatmap, setHeatmap] = useState<WeeklyHeatmap | null>(null);
  const [heatmapLoading, setHeatmapLoading] = useState(false);
  const [matrix, setMatrix] = useState<VelocityMarginMatrix | null>(null);
  const [matrixLoading, setMatrixLoading] = useState(false);
  const [lifecycle, setLifecycle] = useState<LifecycleResult | null>(null);
  const [lifecycleLoading, setLifecycleLoading] = useState(false);
  const [depletion, setDepletion] = useState<DepletionResult | null>(null);
  const [depletionLoading, setDepletionLoading] = useState(false);
  const [smartReorder, setSmartReorder] = useState<SmartReorderResult | null>(null);
  const [smartReorderLoading, setSmartReorderLoading] = useState(false);
  const [priceSensitivity, setPriceSensitivity] = useState<PriceSensitivity | null>(null);
  const [basketAffinity, setBasketAffinity] = useState<BasketAffinity | null>(null);

  const analyticsMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "dailyDate", label: "Dnevna analiza", value: dailyDate },
    { key: "supplierCountV2", label: "Dobavljaci v2", value: supplierV2.length },
    { key: "supplierCountV1", label: "Dobavljaci v1", value: suppliers.length },
    { key: "abcCount", label: "ABC artikli", value: abcData.length },
    { key: "agingCount", label: "Aging artikli", value: agingItems.length },
  ], [abcData.length, agingItems.length, dailyDate, supplierV2.length, suppliers.length]);

  const openSnapshotDetail = useCallback(<Row,>(
    table: string,
    recordId: string,
    title: string,
    subtitle: string,
    columns: AnalyticsTableColumn<Row>[],
    row: Row
  ) => {
    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table,
        recordId,
        title,
        subtitle,
        columns,
        row,
        metadata: [...analyticsFilters, ...analyticsMetadata],
      })
    );

    navigate(`/analitika/${table}/${encodeURIComponent(recordId)}`, {
      state: { backgroundLocation: location },
    });
  }, [analyticsFilters, analyticsMetadata, location, navigate]);

  const analyticsContext = useMemo<InsightAnalyticsContext>(() => ({
    filters: analyticsFilters,
    metadata: analyticsMetadata,
    openSnapshotDetail,
  }), [analyticsFilters, analyticsMetadata, openSnapshotDetail]);

  // ── Load KPI + overview data ──
  const loadKpi = useCallback(async () => {
    setKpiLoading(true);
    setKpiError(null);
    try {
      const data = await getKpiSnapshot(fromDate, toDate);
      setKpi(data);
    } catch (e: unknown) {
      setKpiError(e instanceof Error ? e.message : "Greška");
    } finally {
      setKpiLoading(false);
    }
  }, [fromDate, toDate]);

  useEffect(() => { loadKpi(); }, [loadKpi]);

  // Tab data loading tracker
  const [loadedTabs] = useState<Set<TabKey>>(() => new Set<TabKey>());

  const loadTabData = useCallback(async (tab: TabKey) => {
    if (loadedTabs.has(tab)) return;
    loadedTabs.add(tab);

    if (tab === "pregled") {
      setChangelogLoading(true);
      setMarginAlertsLoading(true);
      setIntelligenceLoading(true);
      setIntelligenceError(null);
      getWeeklyChangelog().then(d => setChangelog(d)).catch(() => {}).finally(() => setChangelogLoading(false));
      getMarginAlerts(fromDate, toDate).then(d => setMarginAlerts(d)).catch(() => {}).finally(() => setMarginAlertsLoading(false));
      Promise.allSettled([
        getDemandSignals({ date: toDate, historyDays: 7, page: 1, pageSize: 5, sortBy: "demandAcceleration", sortDir: "desc" }),
        getInventoryRiskSignals({ date: toDate, historyDays: 1, page: 1, pageSize: 5, onlyAtRisk: true, sortBy: "deadStockRisk", sortDir: "desc" }),
        getPriceIntelligence({ page: 1, pageSize: 5, sortBy: "marginPct", sortDir: "desc" }),
        getTrendMomentum({ page: 1, pageSize: 5, sortBy: "externalTrendScore", sortDir: "desc" }),
      ])
        .then(([demandResult, inventoryResult, priceResult, trendResult]) => {
          setIntelligenceDemand(demandResult.status === "fulfilled" ? demandResult.value.items : []);
          setIntelligenceInventory(inventoryResult.status === "fulfilled" ? inventoryResult.value.items : []);
          setIntelligencePrice(priceResult.status === "fulfilled" ? priceResult.value.items : []);
          setIntelligenceTrend(trendResult.status === "fulfilled" ? trendResult.value.items : []);

          const snapshotDate =
            (demandResult.status === "fulfilled" ? demandResult.value.asOfDate : null)
            ?? (inventoryResult.status === "fulfilled" ? inventoryResult.value.asOfDate : null)
            ?? (priceResult.status === "fulfilled" ? priceResult.value.asOfDate : null)
            ?? (trendResult.status === "fulfilled" ? trendResult.value.asOfDate : null)
            ?? null;

          setIntelligenceAsOfDate(snapshotDate);

          const failedCount = [demandResult, inventoryResult, priceResult, trendResult]
            .filter(result => result.status === "rejected")
            .length;

          if (failedCount === 4) {
            setIntelligenceError("Nijedan intelligence signal nije trenutno dostupan.");
          } else if (failedCount > 0) {
            setIntelligenceError("Deo intelligence signala nije mogao da se ucita, prikazujem dostupne snapshot podatke.");
          } else {
            setIntelligenceError(null);
          }
        })
        .finally(() => setIntelligenceLoading(false));
    }
    if (tab === "dobavljaci") {
      setSupplierV2Loading(true);
      setSupplierLoading(true);
      getSupplierScoringV2(fromDate, toDate).then(d => setSupplierV2(d)).catch(() => {}).finally(() => setSupplierV2Loading(false));
      getSupplierScorecard(fromDate, toDate).then(d => setSuppliers(d)).catch(() => {}).finally(() => setSupplierLoading(false));
    }
    if (tab === "kategorije") {
      setCatLoading(true);
      Promise.allSettled([
        getCategoryIntelligence(fromDate, toDate),
        getPriceSensitivity(),
        getBasketAffinity(fromDate, toDate),
        getPriceIntelligenceSample({ sortBy: "priceDate", sortDir: "desc" }),
        getInventoryRiskSignalsSample({ date: toDate, historyDays: 1, sortBy: "deadStockRisk", sortDir: "desc" }),
        getDemandSignalsSample({ date: toDate, historyDays: 1, sortBy: "salesVelocity", sortDir: "desc" }),
      ])
        .then(([legacyCategoryResult, legacyPriceResult, basketResult, priceResult, inventoryResult, demandResult]) => {
          const legacyCategory = legacyCategoryResult.status === "fulfilled" ? legacyCategoryResult.value : null;
          const legacyPrice = legacyPriceResult.status === "fulfilled" ? legacyPriceResult.value : null;
          const priceItems = priceResult.status === "fulfilled" ? priceResult.value.items : [];
          const inventoryItems = inventoryResult.status === "fulfilled" ? inventoryResult.value.items : [];
          const demandItems = demandResult.status === "fulfilled" ? demandResult.value.items : [];

          setCatData(mergeCategorySignalsAsPrimary(legacyCategory, priceItems, inventoryItems, demandItems));
          setPriceSensitivity(mergePriceSensitivityAsPrimary(legacyPrice, priceItems, inventoryItems));
          setBasketAffinity(basketResult.status === "fulfilled" ? basketResult.value : null);
        })
        .finally(() => setCatLoading(false));
    }
    if (tab === "matrica") {
      setMatrixLoading(true);
      getVelocityMarginMatrix(fromDate, toDate).then(d => setMatrix(d)).catch(() => {}).finally(() => setMatrixLoading(false));
    }
    if (tab === "dnevna") {
      setDailyLoading(true);
      setHeatmapLoading(true);
      getDailyAnalysis(dailyDate, toDateStr(daysAgo(60)), toDate).then(d => setDaily(d)).catch(() => {}).finally(() => setDailyLoading(false));
      getWeeklyHeatmap(fromDate, toDate).then(d => setHeatmap(d)).catch(() => {}).finally(() => setHeatmapLoading(false));
    }
    if (tab === "abc") {
      setAbcLoading(true);
      setLifecycleLoading(true);
      getAbcClassification(fromDate, toDate).then(d => setAbcData(d.items)).catch(() => {}).finally(() => setAbcLoading(false));
      getProductLifecycle(fromDate, toDate).then(d => setLifecycle(d)).catch(() => {}).finally(() => setLifecycleLoading(false));
    }
    if (tab === "zalihe") {
      setAgingLoading(true);
      setDepletionLoading(true);
      Promise.allSettled([
        getAgingStock(),
        getStockDepletionForecast(fromDate, toDate),
        getInventoryRiskSignalsSample({ date: toDate, historyDays: 1, sortBy: "deadStockRisk", sortDir: "desc" }),
        getInventoryRiskSignalsSample({ date: toDate, historyDays: 1, sortBy: "daysOfCover", sortDir: "asc" }),
        getDemandSignalsSample({ date: toDate, historyDays: 1, sortBy: "daysSinceLastSale", sortDir: "desc" }),
        getPriceIntelligenceSample({ sortBy: "priceDate", sortDir: "desc" }),
      ])
        .then(([legacyAgingResult, legacyDepletionResult, agingInventoryResult, depletionInventoryResult, demandResult, priceResult]) => {
          const legacyAging = legacyAgingResult.status === "fulfilled" ? legacyAgingResult.value : null;
          const legacyDepletion = legacyDepletionResult.status === "fulfilled" ? legacyDepletionResult.value : null;
          const agingInventoryItems = agingInventoryResult.status === "fulfilled" ? agingInventoryResult.value.items : [];
          const depletionInventoryItems = depletionInventoryResult.status === "fulfilled" ? depletionInventoryResult.value.items : [];
          const demandItems = demandResult.status === "fulfilled" ? demandResult.value.items : [];
          const priceItems = priceResult.status === "fulfilled" ? priceResult.value.items : [];
          const asOfDate = depletionInventoryResult.status === "fulfilled" ? depletionInventoryResult.value.asOfDate : null;

          const mergedAging = mergeAgingAsPrimary(legacyAging, agingInventoryItems, demandItems, priceItems);
          const mergedDepletion = mergeDepletionAsPrimary(legacyDepletion, depletionInventoryItems, priceItems, asOfDate);

          setAgingItems(mergedAging?.items ?? []);
          setAgingSummary(mergedAging?.summary);
          setDepletion(mergedDepletion);
        })
        .finally(() => {
          setAgingLoading(false);
          setDepletionLoading(false);
        });
    }
    if (tab === "nabavka") {
      setSmartReorderLoading(true);
      setReorderLoading(true);
      Promise.allSettled([
        getSmartReorder(fromDate, toDate),
        getReorderPlan(fromDate, toDate),
        getInventoryRiskSignalsSample({ date: toDate, historyDays: 1, sortBy: "daysOfCover", sortDir: "asc" }),
        getDemandSignalsSample({ date: toDate, historyDays: 1, sortBy: "salesVelocity", sortDir: "desc" }),
        getPriceIntelligenceSample({ sortBy: "priceDate", sortDir: "desc" }),
        getTrendMomentumSample({ sortBy: "externalTrendScore", sortDir: "desc" }),
      ])
        .then(([legacySmartResult, legacyPlanResult, inventoryResult, demandResult, priceResult, trendResult]) => {
          const legacySmart = legacySmartResult.status === "fulfilled" ? legacySmartResult.value : null;
          const legacyPlan = legacyPlanResult.status === "fulfilled" ? legacyPlanResult.value : null;
          const inventoryItems = inventoryResult.status === "fulfilled" ? inventoryResult.value.items : [];
          const demandItems = demandResult.status === "fulfilled" ? demandResult.value.items : [];
          const priceItems = priceResult.status === "fulfilled" ? priceResult.value.items : [];
          const trendItems = trendResult.status === "fulfilled" ? trendResult.value.items : [];

          const mergedSmart = mergeSmartReorderAsPrimary(legacySmart, inventoryItems, demandItems, priceItems, trendItems);
          const fallbackPlan = buildLegacyReorderFallbackFromSignals(mergedSmart);

          setSmartReorder(mergedSmart);
          setReorderItems(fallbackPlan?.items ?? legacyPlan?.items ?? []);
          setReorderSummary(fallbackPlan?.summary ?? legacyPlan?.summary);
        })
        .finally(() => {
          setSmartReorderLoading(false);
          setReorderLoading(false);
        });
    }
  }, [fromDate, toDate, dailyDate, loadedTabs]);

  // Load initial tab
  useEffect(() => { loadTabData("pregled"); }, [loadTabData]);

  const handleTabChange = (tab: TabKey) => { setActiveTab(tab); loadTabData(tab); };

  const handleDailyDateChange = async (d: string) => {
    setDailyDate(d);
    setDailyLoading(true);
    try { setDaily(await getDailyAnalysis(d, toDateStr(daysAgo(60)), toDate)); }
    catch { /* silent */ }
    finally { setDailyLoading(false); }
  };

  const handlePeriodChange = (days: number) => {
    setPeriodDays(days);
    loadedTabs.clear();
  };

  return (
    <div className="space-y-5 pb-10">
      {/* ── Page Header ───────────────────────────────────────── */}
      <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-2">
          <span className="text-xl">🔬</span>
          <h1 className="text-xl font-bold text-white">Insight Studio <InfoTip text="Napredna analitika: matrice, lifecycle, rizici i predlozi za nabavku." /></h1>
          <span className="rounded bg-gradient-to-r from-[var(--surface-elevated)]/20 to-[var(--surface-elevated-dark)]/20 px-2 py-0.5 text-[10px] font-semibold text-[var(--text-primary)] uppercase tracking-wider">
            Analitika 2 — Pro
          </span>
        </div>
        <p className="text-[11px] text-[var(--text-primary)]">
          Napredna analiza profitabilnosti, V×M matrica, lifecycle, deplecija i reorder 2.0
        </p>
      </div>

      {/* ── Period Filter ──────────────────────────────────────── */}
      <div className="flex items-center gap-2 flex-wrap rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-2.5">
        <span className="text-xs text-[var(--text-primary)] mr-1">Period:</span>
        {PERIOD_PRESETS.map(p => (
          <button key={p.days} onClick={() => handlePeriodChange(p.days)}
            className={`rounded-lg px-3 py-1 text-xs font-medium transition ${periodDays === p.days ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)] hover:text-[var(--text-primary)] hover:bg-[var(--surface-light)]"}`}>
            {p.label}
          </button>
        ))}
        <span className="ml-auto text-[11px] text-[var(--text-primary)]">{fromDate} → {toDate}</span>
      </div>

      {/* ── KPI Command Row (always visible) ──────────────────── */}
      {kpiError ? (
        <AlertBanner severity="danger">Greška KPI: {kpiError}</AlertBanner>
      ) : kpiLoading || !kpi ? (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          {Array.from({ length: 5 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          <KpiCard label="Ukupan prihod" value={fmtRsd(kpi.revenue)} sub="vs. preth." change={kpi.revenueChange} accent={PAL.blue} sparkline={kpi.sparkline} icon="💰" tooltip="Ukupna prodaja za period" />
          <KpiCard label="Bruto marža" value={fmtPct(kpi.marginPct)} sub="Procenjena" accent={PAL.green} icon="📈" tooltip="(Prodajna - Nabavna) / Prodajna × 100" />
          <KpiCard label="Prodato kom." value={fmtNum(kpi.units)} sub="vs. preth." change={kpi.unitsChange} accent={PAL.purple} icon="👟" />
          <KpiCard label="Transakcije" value={fmtNum(kpi.transactions)} sub={`Avg ${fmtRsd(kpi.transactions > 0 ? kpi.revenue / kpi.transactions : 0)}`} accent={PAL.yellow} icon="🧾" />
          <KpiCard label="OOS / Malo" value={`${kpi.oosCount} / ${kpi.lowStockCount}`} sub="Bez zaliha / ispod min" accent={kpi.oosCount > 10 ? PAL.red : PAL.orange} icon="⚠️" />
        </div>
      )}

      {/* ── Tab Navigation ──────────────────────────────────────── */}
      <div className="flex gap-1 flex-wrap rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-1.5 overflow-x-auto">
        {TABS.map(t => (
          <button key={t.key} onClick={() => handleTabChange(t.key)}
            className={`flex items-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium transition whitespace-nowrap ${
              activeTab === t.key ? "bg-[var(--surface-elevated)] text-[var(--text-primary)] ring-1 ring-[var(--theme-color-32579e, #32579e)]" : "text-[var(--text-primary)] hover:text-[var(--text-primary)] hover:bg-[var(--surface-light)]"
            }`}>
            <span>{t.icon}</span>
            <span>{t.label}</span>
            {TAB_TIPS[t.key] ? <InfoTip text={TAB_TIPS[t.key]} /> : null}
          </button>
        ))}
      </div>

      {/* ── Tab Content ──────────────────────────────────────────── */}
      <div className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-5">
        {activeTab === "pregled" && (
          <OverviewTab
            kpi={kpi}
            changelog={changelog}
            marginAlerts={marginAlerts}
            loading={changelogLoading || marginAlertsLoading}
            intelligenceDemand={intelligenceDemand}
            intelligenceInventory={intelligenceInventory}
            intelligencePrice={intelligencePrice}
            intelligenceTrend={intelligenceTrend}
            intelligenceAsOfDate={intelligenceAsOfDate}
            intelligenceLoading={intelligenceLoading}
            intelligenceError={intelligenceError}
          />
        )}
        {activeTab === "dobavljaci" && (
          <SupplierTab v2Data={supplierV2} v1Data={suppliers} loading={supplierV2Loading && supplierLoading} analyticsContext={analyticsContext} />
        )}
        {activeTab === "kategorije" && (
          <CategoryTab
            byCategory={catData?.byCategory ?? []} byGender={catData?.byGender ?? []}
            priceSensitivity={priceSensitivity} basketAffinity={basketAffinity}
            loading={catLoading}
            analyticsContext={analyticsContext}
          />
        )}
        {activeTab === "matrica" && (
          <MatrixTab matrixData={matrix} loading={matrixLoading} />
        )}
        {activeTab === "dnevna" && (
          <DailyTab data={daily} loading={dailyLoading} onDateChange={handleDailyDateChange}
            selectedDate={dailyDate} heatmap={heatmap} heatmapLoading={heatmapLoading} />
        )}
        {activeTab === "abc" && (
          <AbcLifecycleTab abcData={abcData} abcLoading={abcLoading} lifecycle={lifecycle} lifecycleLoading={lifecycleLoading} analyticsContext={analyticsContext} />
        )}
        {activeTab === "zalihe" && (
          <StockTab agingItems={agingItems} agingLoading={agingLoading} agingSummary={agingSummary}
            depletion={depletion} depletionLoading={depletionLoading} analyticsContext={analyticsContext} />
        )}
        {activeTab === "nabavka" && (
          <ReorderTab2 smartData={smartReorder} smartLoading={smartReorderLoading}
            v1Items={reorderItems} v1Loading={reorderLoading} v1Summary={reorderSummary} analyticsContext={analyticsContext} />
        )}
      </div>
    </div>
  );
}

