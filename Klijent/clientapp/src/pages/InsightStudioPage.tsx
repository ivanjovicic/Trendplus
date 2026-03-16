import { useCallback, useEffect, useMemo, useState, type CSSProperties } from "react";
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
import {
  buildLegacyReorderFallbackFromSignals,
  mergeAgingAsPrimary,
  mergeCategorySignalsAsPrimary,
  mergeDepletionAsPrimary,
  mergePriceSensitivityAsPrimary,
  mergeSmartReorderAsPrimary,
} from "../services/analyticsIntelligenceDerived";

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

const PERIOD_PRESETS = [
  { label: "7d", days: 7 },
  { label: "30d", days: 30 },
  { label: "90d", days: 90 },
  { label: "6m", days: 180 },
  { label: "1g", days: 365 },
];

const PAL = {
  blue: "#4F8EF7",
  green: "#4CAF82",
  yellow: "#F5C542",
  orange: "#F97316",
  red: "#E05C5C",
  purple: "#9B72CF",
  cyan: "#22D3EE",
  pink: "#F472B6",
  bg: "#0D0F14",
  card: "#161A23",
  cardHover: "#1E2332",
  border: "#2A3045",
  textPrimary: "#E8ECF4",
  textSecondary: "#8A95B0",
  textMuted: "#4A5270",
};

const DONUT_COLORS = [PAL.blue, PAL.yellow, PAL.green, PAL.purple, PAL.red, PAL.orange, PAL.cyan, PAL.pink];

const TIER_COLORS: Record<string, string> = { GOLD: "#F5C542", SILVER: "#8A95B0", BRONZE: "#F97316", AT_RISK: "#E05C5C" };
const TIER_LABELS: Record<string, string> = { GOLD: "Zlato", SILVER: "Srebro", BRONZE: "Bronza", AT_RISK: "Rizik" };

const QUAD_COLORS: Record<string, string> = { STAR: PAL.green, NICHE_GEM: PAL.purple, VOLUME_TRAP: PAL.yellow, DEAD_WEIGHT: PAL.red };
const QUAD_LABELS: Record<string, string> = { STAR: "⭐ Zvezda", NICHE_GEM: "💎 Niša", VOLUME_TRAP: "⚡ Volume Trap", DEAD_WEIGHT: "⚠ Mrtav Teg" };

const STAGE_COLORS: Record<string, string> = { LAUNCH: PAL.cyan, GROWTH: PAL.green, MATURE: PAL.yellow, DECLINE: PAL.red };
const STAGE_LABELS: Record<string, string> = { LAUNCH: "Lansiranje", GROWTH: "Rast", MATURE: "Zrelost", DECLINE: "Pad" };

const SEVERITY_COLORS: Record<string, string> = { CRITICAL: PAL.red, WARNING: PAL.orange, WATCH: PAL.yellow, OK: PAL.green };

const URGENCY_COLORS: Record<string, string> = { "KRITIČNO": PAL.red, HITNO: PAL.orange, "PREPORUČUJE SE": PAL.yellow, OK: PAL.green };

const AGING_COLORS: Record<string, string> = { Aktivno: PAL.green, Pazi: PAL.yellow, Upozorenje: PAL.orange, "Kritično": PAL.red };

const ABC_COLORS: Record<string, string> = { A: PAL.green, B: PAL.yellow, C: PAL.red };

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
    <span className={`ml-1 text-[11px] font-semibold ${up ? "text-[#4CAF82]" : "text-[#E05C5C]"}`}>
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
      className="group relative rounded-xl border border-[#2A3045] bg-[#161A23] p-4 flex flex-col gap-1 overflow-hidden transition hover:border-[#3A4565]"
      onMouseEnter={() => setShowTip(true)} onMouseLeave={() => setShowTip(false)}
    >
      <div className="absolute left-0 top-0 h-[3px] w-full opacity-80" style={{ background: `linear-gradient(90deg, ${accent}, transparent)` }} />
      <div className="flex items-center gap-1.5 text-[11px] text-[#8A95B0] uppercase tracking-wider">
        {icon && <span className="text-sm">{icon}</span>}
        {label}
        {tooltip && showTip && (
          <div className="absolute left-4 top-full z-20 mt-1 max-w-[220px] rounded-lg bg-[#1E2332] border border-[#2A3045] px-3 py-2 text-[11px] text-[#8A95B0] normal-case tracking-normal shadow-xl">
            {tooltip}
          </div>
        )}
      </div>
      <div className="text-xl font-bold text-[#E8ECF4]">{value}</div>
      {sub && (
        <div className="text-[11px] text-[#8A95B0]">
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
          <div className="h-4 flex-1 bg-[#2A3045] rounded" />
          <div className="h-4 w-20 bg-[#2A3045] rounded" />
          <div className="h-4 w-20 bg-[#2A3045] rounded" />
        </div>
      ))}
    </div>
  );
}

function CardSkeleton() {
  return <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-4 animate-pulse"><div className="h-3 w-24 bg-[#2A3045] rounded mb-3" /><div className="h-6 w-32 bg-[#2A3045] rounded mb-2" /><div className="h-3 w-20 bg-[#2A3045] rounded" /></div>;
}

function Badge({ label, color }: { label: string; color: string }) {
  return <span className="rounded px-1.5 py-0.5 text-[10px] font-bold" style={{ background: `${color}22`, color }}>{label}</span>;
}

function ScoreBar({ label, score, max = 100 }: { label: string; score: number; max?: number }) {
  const pct = Math.min(100, Math.max(0, (score / max) * 100));
  const color = pct >= 70 ? PAL.green : pct >= 40 ? PAL.yellow : PAL.red;
  return (
    <div>
      <div className="flex justify-between text-[11px] text-[#8A95B0] mb-0.5">
        <span>{label}</span>
        <span style={{ color }}>{score.toFixed(0)}/{max}</span>
      </div>
      <div className="h-1.5 rounded-full bg-[#2A3045] overflow-hidden">
        <div className="h-full rounded-full transition-all duration-500" style={{ width: `${pct}%`, background: color }} />
      </div>
    </div>
  );
}

function MiniStat({ label, value, color }: { label: string; value: string | number; color?: string }) {
  return (
    <div className="rounded-lg bg-[#1E2332] px-3 py-2">
      <div className="text-[10px] text-[#8A95B0] uppercase">{label}</div>
      <div className="font-semibold text-sm" style={{ color: color ?? PAL.textPrimary }}>{value}</div>
    </div>
  );
}

function SectionHeader({ title, subtitle }: { title: string; subtitle?: string }) {
  return (
    <div className="mb-4">
      <h3 className="text-sm font-semibold text-[#c9d3e4]">{title}</h3>
      {subtitle && <p className="text-[10px] text-[#8A95B0] mt-0.5">{subtitle}</p>}
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

const tooltipStyle: CSSProperties = { background: "#1E2332", border: "1px solid #2A3045", borderRadius: 8, fontSize: 12 };

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
        <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-5">
          <div className="flex items-center gap-2 mb-4">
            <span className="text-base">🔔</span>
            <h3 className="text-sm font-bold text-[#c9d3e4]">Šta se promenilo ove nedelje?</h3>
          </div>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div className="rounded-lg bg-[#1E2332] px-3 py-2">
              <div className="text-[10px] text-[#8A95B0] uppercase">Prihod ove nedelje</div>
              <div className="text-sm font-bold text-[#E8ECF4]">{fmtRsd(changelog.thisWeekRevenue)}</div>
              <div className="text-[11px]">{changeBadge(changelog.revenueChangePct)}</div>
            </div>
            <div className="rounded-lg bg-[#1E2332] px-3 py-2">
              <div className="text-[10px] text-[#8A95B0] uppercase">Prodato kom.</div>
              <div className="text-sm font-bold text-[#E8ECF4]">{fmtNum(changelog.thisWeekUnits)}</div>
              <div className="text-[11px]">{changeBadge(changelog.unitChangePct)}</div>
            </div>
            <div className="rounded-lg bg-[#1E2332] px-3 py-2">
              <div className="text-[10px] text-[#8A95B0] uppercase">Transakcije</div>
              <div className="text-sm font-bold text-[#E8ECF4]">{fmtNum(changelog.thisWeekTransactions)}</div>
            </div>
            <div className="rounded-lg bg-[#1E2332] px-3 py-2">
              <div className="text-[10px] text-[#8A95B0] uppercase">OOS / Promene cena</div>
              <div className="text-sm font-bold" style={{ color: changelog.oosCount > 5 ? PAL.red : PAL.textPrimary }}>{changelog.oosCount} / {changelog.priceChangesThisWeek}</div>
            </div>
          </div>

          {/* Category movement */}
          {changelog.categoryChanges.length > 0 && (
            <div className="mt-4">
              <div className="text-[11px] text-[#8A95B0] uppercase mb-2">Promene po kategorijama (nedelja-na-nedelju)</div>
              <div className="flex flex-wrap gap-2">
                {changelog.categoryChanges.slice(0, 8).map((c) => (
                  <div
                    key={c.kategorija}
                    className="rounded-lg border border-[#2A3045] bg-[#0D0F14] px-3 py-1.5 text-xs"
                  >
                    <span className="text-[#c9d3e4] font-medium">{c.kategorija}</span>
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
        <div className="rounded-xl border border-[#E05C5C]/20 bg-[#E05C5C]/5 p-5">
          <div className="flex items-center gap-2 mb-3">
            <span className="text-base">⚠️</span>
            <h3 className="text-sm font-bold text-[#E05C5C]">
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
              <div key={a.artikalId} className="flex items-center gap-3 rounded-lg bg-[#161A23] border border-[#2A3045] px-3 py-2 text-xs">
                <Badge label={a.alertType === "NEGATIVE_MARGIN" ? "NEG" : a.alertType === "LOW_MARGIN" ? "LOW" : "MD"} color={a.alertType === "NEGATIVE_MARGIN" ? PAL.red : PAL.orange} />
                <span className="flex-1 text-[#E8ECF4] truncate">{a.naziv}</span>
                <span className="text-[#8A95B0]">{a.kategorija}</span>
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
      <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-5">
        <div className="flex items-center gap-2 mb-3">
          <span className="text-base">🧭</span>
          <h3 className="text-sm font-bold text-[#c9d3e4]">Preporučene akcije</h3>
        </div>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
          {kpi && kpi.oosCount > 3 && (
            <div className="rounded-lg border border-[#E05C5C]/20 bg-[#E05C5C]/5 px-3 py-2 text-xs text-[#E05C5C]">
              🔴 <strong>{kpi.oosCount}</strong> artikala bez zalihe — proveri tab "Nabavka 2.0"
            </div>
          )}
          {marginAlerts && marginAlerts.summary.negativeMarginCount > 0 && (
            <div className="rounded-lg border border-[#F97316]/20 bg-[#F97316]/5 px-3 py-2 text-xs text-[#F97316]">
              🟠 <strong>{marginAlerts.summary.negativeMarginCount}</strong> artikala pod negativnom maržom
            </div>
          )}
          {changelog && changelog.revenueChangePct < -15 && (
            <div className="rounded-lg border border-[#F5C542]/20 bg-[#F5C542]/5 px-3 py-2 text-xs text-[#F5C542]">
              🟡 Prihod pao <strong>{Math.abs(changelog.revenueChangePct).toFixed(0)}%</strong> nedelja-na-nedelju
            </div>
          )}
          {changelog && changelog.revenueChangePct >= 10 && (
            <div className="rounded-lg border border-[#4CAF82]/20 bg-[#4CAF82]/5 px-3 py-2 text-xs text-[#4CAF82]">
              🟢 Rast prihoda +{changelog.revenueChangePct.toFixed(0)}% ove nedelje! 
            </div>
          )}
          <div className="rounded-lg border border-[#4F8EF7]/20 bg-[#4F8EF7]/5 px-3 py-2 text-xs text-[#4F8EF7]">
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
}: {
  v2Data: SupplierScoreV2[];
  v1Data: SupplierScore[];
  loading: boolean;
}) {
  const [selected, setSelected] = useState<SupplierScoreV2 | null>(null);
  const data = v2Data.length > 0 ? v2Data : [];

  if (loading) return <Skeleton rows={8} />;
  if (!data.length && !v1Data.length)
    return <p className="text-[#8A95B0] text-sm">Nema podataka o dobavljačima za izabrani period.</p>;

  // Fallback to v1 if v2 failed
  if (!data.length && v1Data.length) {
    return <SupplierTabV1 data={v1Data} loading={false} />;
  }

  const displayed = selected ?? data[0];

  return (
    <div className="space-y-5">
      <SectionHeader title="Dobavljači 2.0 — Rangiranje i Scorecard" subtitle="Kompozitni skor: Profitabilnost × Velocity × Diverzifikacija × Pouzdanost" />

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-5">
        {/* Leaderboard */}
        <div className="lg:col-span-3 overflow-x-auto rounded-xl border border-[#2A3045]">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
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
                  onClick={() => setSelected(s)}
                  className={`cursor-pointer border-b border-[#2A3045] transition hover:bg-[#1E2332] ${
                    displayed?.dobavljacId === s.dobavljacId ? "bg-[#1f2940] ring-1 ring-inset ring-[#32579e]" : ""
                  }`}
                >
                  <td className="px-3 py-2 text-[#8A95B0]">{i + 1}</td>
                  <td className="px-3 py-2 font-medium text-[#E8ECF4]">{s.dobavljacNaziv}</td>
                  <td className="px-3 py-2 text-right text-[#E8ECF4]">{fmtRsd(s.totalRevenue)}</td>
                  <td className="px-3 py-2 text-right" style={{ color: s.marginPct >= 30 ? PAL.green : s.marginPct >= 15 ? PAL.yellow : PAL.red }}>{fmtPct(s.marginPct)}</td>
                  <td className="px-3 py-2 text-right text-[#4F8EF7]">{s.velocity.toFixed(2)}</td>
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
          <div className="lg:col-span-2 rounded-xl border border-[#2A3045] bg-[#161A23] p-4 space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-bold text-[#c9d3e4]">{displayed.dobavljacNaziv}</h3>
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
            <div className="rounded-lg bg-[#1E2332] px-3 py-2 flex items-center justify-between">
              <span className="text-xs text-[#8A95B0]">Kompozitni Skor</span>
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
                  <CartesianGrid stroke="#2A3045" vertical={false} />
                  <XAxis dataKey="name" tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false} />
                  <YAxis domain={[0, 100]} tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false} />
                  <Tooltip contentStyle={tooltipStyle} itemStyle={{ color: "#E8ECF4" }} />
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
function SupplierTabV1({ data, loading }: { data: SupplierScore[]; loading: boolean }) {
  const [selected, setSelected] = useState<SupplierScore | null>(null);
  if (loading) return <Skeleton rows={8} />;
  if (!data.length) return <p className="text-[#8A95B0] text-sm">Nema podataka.</p>;
  const displayed = selected ?? data[0];
  return (
    <div className="grid grid-cols-1 gap-5 lg:grid-cols-5">
      <div className="lg:col-span-3 overflow-x-auto rounded-xl border border-[#2A3045]">
        <table className="w-full text-sm">
          <thead><tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
            <th className="px-3 py-2 text-left">#</th><th className="px-3 py-2 text-left">Dobavljač</th>
            <th className="px-3 py-2 text-right">Prihod</th><th className="px-3 py-2 text-right">Marža%</th>
            <th className="px-3 py-2 text-center">Risk</th><th className="px-3 py-2 text-right">Skor</th>
          </tr></thead>
          <tbody>
            {data.map((s, i) => (
              <tr key={s.dobavljacId ?? i} onClick={() => setSelected(s)}
                className={`cursor-pointer border-b border-[#2A3045] transition hover:bg-[#1E2332] ${displayed.dobavljacId === s.dobavljacId ? "bg-[#1f2940]" : ""}`}>
                <td className="px-3 py-2 text-[#8A95B0]">{i + 1}</td>
                <td className="px-3 py-2 text-[#E8ECF4]">{s.dobavljacNaziv}</td>
                <td className="px-3 py-2 text-right">{fmtRsd(s.totalRevenue)}</td>
                <td className="px-3 py-2 text-right text-[#4CAF82]">{fmtPct(s.marginPct)}</td>
                <td className="px-3 py-2 text-center"><Badge label={s.riskLevel} color={s.riskLevel === "LOW" ? PAL.green : s.riskLevel === "MED" ? PAL.yellow : PAL.red} /></td>
                <td className="px-3 py-2 text-right font-bold text-[#4F8EF7]">{s.compositeScore.toFixed(0)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="lg:col-span-2 rounded-xl border border-[#2A3045] bg-[#161A23] p-4 space-y-3">
        <h3 className="text-sm font-bold text-[#c9d3e4]">{displayed.dobavljacNaziv}</h3>
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
}: {
  byCategory: CategoryStat[];
  byGender: GenderStat[];
  priceSensitivity: PriceSensitivity | null;
  basketAffinity: BasketAffinity | null;
  loading: boolean;
}) {
  const [subTab, setSubTab] = useState<"kategorije" | "pol" | "cene" | "korpa">("kategorije");

  if (loading) return <Skeleton rows={6} />;
  if (!byCategory.length) return <p className="text-[#8A95B0] text-sm">Nema podataka.</p>;

  const subTabs = [
    { key: "kategorije" as const, label: "Po Tipu Obuće" },
    { key: "pol" as const, label: "Po Polu" },
    { key: "cene" as const, label: "Cenovna Osetljivost" },
    { key: "korpa" as const, label: "Basket Afinitet" },
  ];

  return (
    <div className="space-y-5">
      <SectionHeader title="Kategorije & Segmentacija" subtitle="Prihodi, marže, velocity i cross-sell analitika" />
      <div className="rounded-xl border border-[#4F8EF7]/20 bg-[#4F8EF7]/5 px-4 py-3 text-xs text-[#8A95B0]">
        Primarni read model za cenovne i category signale sada dolazi iz <span className="font-semibold text-[#7ea5ff]">analytics_intel</span>.
        Basket afinitet i raspodela po polu ostaju na legacy advanced sloju kao dopuna.
      </div>
      <div className="flex gap-2 flex-wrap">
        {subTabs.map(t => (
          <button key={t.key} onClick={() => setSubTab(t.key)}
            className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subTab === t.key ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0] hover:text-[#c9d3e4]"}`}>
            {t.label}
          </button>
        ))}
      </div>

      {subTab === "kategorije" && (
        <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
          <div>
            <h4 className="mb-2 text-xs font-semibold text-[#c9d3e4]">Prihod po kategoriji</h4>
            <div className="h-[260px]">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={byCategory.slice(0, 8)} layout="vertical" barSize={18}>
                  <CartesianGrid stroke="#2A3045" horizontal={false} />
                  <XAxis type="number" tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false} tickFormatter={(v) => fmtRsd(Number(v))} />
                  <YAxis type="category" dataKey="kategorija" tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false} width={80} />
                  <Tooltip contentStyle={tooltipStyle} formatter={(v: number | string | undefined) => [fmtRsd(Number(v ?? 0)), "Prihod"]} />
                  <Bar dataKey="totalRevenue" fill={PAL.blue} radius={[0, 4, 4, 0]}>
                    {byCategory.slice(0, 8).map((_, i) => <Cell key={i} fill={DONUT_COLORS[i % DONUT_COLORS.length]} />)}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>
          <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
            <table className="w-full text-xs">
              <thead><tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
                <th className="px-3 py-2 text-left">Kategorija</th>
                <th className="px-3 py-2 text-right">Udeo%</th>
                <th className="px-3 py-2 text-right">Marža%</th>
                <th className="px-3 py-2 text-right">Lift</th>
                <th className="px-3 py-2 text-right">Velocity</th>
                <th className="px-3 py-2 text-right">SKU</th>
              </tr></thead>
              <tbody>
                {byCategory.map((cat, i) => (
                  <tr key={i} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                    <td className="px-3 py-2 font-medium text-[#E8ECF4]">{cat.kategorija}</td>
                    <td className="px-3 py-2 text-right text-[#8A95B0]">{fmtPct(cat.revShare)}</td>
                    <td className="px-3 py-2 text-right text-[#4CAF82]">{fmtPct(cat.marginPct)}</td>
                    <td className="px-3 py-2 text-right" style={{ color: cat.profitLift >= 0 ? PAL.green : PAL.red }}>{cat.profitLift >= 0 ? "+" : ""}{fmtPct(cat.profitLift)}</td>
                    <td className="px-3 py-2 text-right text-[#4F8EF7]">{cat.velocity.toFixed(3)}</td>
                    <td className="px-3 py-2 text-right text-[#8A95B0]">{cat.uniqueSKU}</td>
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
                <Legend formatter={(v) => <span style={{ color: "#8A95B0", fontSize: 12 }}>{v}</span>} />
              </PieChart>
            </ResponsiveContainer>
          </div>
          <div className="overflow-x-auto rounded-xl border border-[#2A3045] self-start">
            <table className="w-full text-xs">
              <thead><tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
                <th className="px-3 py-2 text-left">Pol</th><th className="px-3 py-2 text-right">Prihod</th>
                <th className="px-3 py-2 text-right">Udeo%</th><th className="px-3 py-2 text-right">Kom</th>
              </tr></thead>
              <tbody>
                {byGender.map((g, i) => (
                  <tr key={i} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                    <td className="px-3 py-2 text-[#E8ECF4] flex items-center gap-2">
                      <span className="w-2.5 h-2.5 rounded-full inline-block" style={{ background: DONUT_COLORS[i % DONUT_COLORS.length] }} />{g.pol}
                    </td>
                    <td className="px-3 py-2 text-right">{fmtRsd(g.totalRevenue)}</td>
                    <td className="px-3 py-2 text-right text-[#4F8EF7] font-semibold">{fmtPct(g.revShare)}</td>
                    <td className="px-3 py-2 text-right text-[#8A95B0]">{fmtNum(g.totalUnits)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {subTab === "cene" && priceSensitivity && (
        <div className="space-y-4">
          <h4 className="text-xs font-semibold text-[#c9d3e4]">Cenovna osetljivost — po cenovnim opsezima</h4>
          <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
            <table className="w-full text-xs">
              <thead><tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
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
                  <tr key={i} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                    <td className="px-3 py-2 font-medium text-[#E8ECF4]">{b.priceBand}</td>
                    <td className="px-3 py-2 text-right text-[#8A95B0]">{b.skuCount}</td>
                    <td className="px-3 py-2 text-right text-[#E8ECF4]">{fmtNum(b.totalUnits)}</td>
                    <td className="px-3 py-2 text-right text-[#4F8EF7]">{b.avgVelocityPerSku.toFixed(1)}</td>
                    <td className="px-3 py-2 text-right text-[#8A95B0]">{fmtRsd(b.avgPrice)}</td>
                    <td className="px-3 py-2 text-right" style={{ color: b.avgMarginPct >= 20 ? PAL.green : b.avgMarginPct >= 10 ? PAL.yellow : PAL.red }}>{fmtPct(b.avgMarginPct)}</td>
                    <td className="px-3 py-2 text-right text-[#8A95B0]">{fmtNum(b.totalStock)}</td>
                    <td className="px-3 py-2 text-right text-[#8A95B0]">{b.markdownCount}</td>
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
            <h4 className="text-xs font-semibold text-[#c9d3e4]">Basket Afinitet — koji se tipovi prodaju zajedno?</h4>
            <span className="text-[10px] text-[#8A95B0]">({basketAffinity.totalMultiItemTransactions} multi-item transakcija)</span>
          </div>
          {basketAffinity.pairs.length > 0 ? (
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
              {basketAffinity.pairs.slice(0, 12).map((p, i) => (
                <div key={i} className="rounded-lg border border-[#2A3045] bg-[#161A23] px-3 py-2 flex items-center gap-3">
                  <span className="text-xl">🤝</span>
                  <div className="flex-1">
                    <div className="text-xs text-[#E8ECF4]">{p.categoryA} + {p.categoryB}</div>
                    <div className="text-[10px] text-[#8A95B0]">{p.coOccurrences}× zajedno · {fmtPct(p.supportPct)} support</div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-[#8A95B0] text-sm">Nema dovoljno multi-item transakcija.</p>
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
    return <p className="text-[#8A95B0] text-sm">Nema podataka za velocity-margin matricu.</p>;

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
        <div className="rounded-xl border border-[#4CAF82]/20 bg-[#4CAF82]/5 px-3 py-2 cursor-pointer" onClick={() => setQuadFilter(quadFilter === "STAR" ? "ALL" : "STAR")}>
          <div className="text-[10px] uppercase text-[#4CAF82]">⭐ Zvezde</div>
          <div className="text-xl font-bold text-[#4CAF82]">{quadrantCounts.stars}</div>
          <div className="text-[10px] text-[#8A95B0]">Visoka vel. + visoka marža</div>
        </div>
        <div className="rounded-xl border border-[#9B72CF]/20 bg-[#9B72CF]/5 px-3 py-2 cursor-pointer" onClick={() => setQuadFilter(quadFilter === "NICHE_GEM" ? "ALL" : "NICHE_GEM")}>
          <div className="text-[10px] uppercase text-[#9B72CF]">💎 Niša Dragulje</div>
          <div className="text-xl font-bold text-[#9B72CF]">{quadrantCounts.nicheGems}</div>
          <div className="text-[10px] text-[#8A95B0]">Niska vel. + visoka marža</div>
        </div>
        <div className="rounded-xl border border-[#F5C542]/20 bg-[#F5C542]/5 px-3 py-2 cursor-pointer" onClick={() => setQuadFilter(quadFilter === "VOLUME_TRAP" ? "ALL" : "VOLUME_TRAP")}>
          <div className="text-[10px] uppercase text-[#F5C542]">⚡ Volume Trap</div>
          <div className="text-xl font-bold text-[#F5C542]">{quadrantCounts.volumeTraps}</div>
          <div className="text-[10px] text-[#8A95B0]">Visoka vel. + niska marža</div>
        </div>
        <div className="rounded-xl border border-[#E05C5C]/20 bg-[#E05C5C]/5 px-3 py-2 cursor-pointer" onClick={() => setQuadFilter(quadFilter === "DEAD_WEIGHT" ? "ALL" : "DEAD_WEIGHT")}>
          <div className="text-[10px] uppercase text-[#E05C5C]">⚠ Mrtav Teg</div>
          <div className="text-xl font-bold text-[#E05C5C]">{quadrantCounts.deadWeight}</div>
          <div className="text-[10px] text-[#8A95B0]">Niska vel. + niska marža</div>
        </div>
      </div>

      {/* Scatter chart */}
      <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-4">
        <div className="h-[350px]">
          <ResponsiveContainer width="100%" height="100%">
            <ScatterChart margin={{ top: 10, right: 20, bottom: 20, left: 10 }}>
              <CartesianGrid stroke="#2A3045" />
              <XAxis type="number" dataKey="x" name="Velocity" tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false}
                label={{ value: "Velocity →", position: "insideBottom", offset: -10, fill: "#8A95B0", fontSize: 11 }} />
              <YAxis type="number" dataKey="y" name="Marža%" tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false}
                label={{ value: "Marža% →", angle: -90, position: "insideLeft", fill: "#8A95B0", fontSize: 11 }} />
              <ZAxis type="number" dataKey="z" range={[30, 400]} name="Prihod" />
              <ReferenceLine y={medianMargin} stroke={PAL.yellow} strokeDasharray="4 4" label={{ value: `Median marža ${medianMargin.toFixed(0)}%`, fill: PAL.yellow, fontSize: 9 }} />
              <ReferenceLine x={medianVelocity} stroke={PAL.yellow} strokeDasharray="4 4" label={{ value: `Median vel ${medianVelocity.toFixed(2)}`, fill: PAL.yellow, fontSize: 9 }} />
              <Tooltip contentStyle={tooltipStyle} formatter={(v: number | undefined) => [typeof v === "number" ? v.toFixed(2) : String(v ?? "")]}
                content={({ payload }) => {
                  if (!payload?.length) return null;
                  const d = payload[0].payload;
                  return (
                    <div className="bg-[#1E2332] border border-[#2A3045] rounded-lg px-3 py-2 text-xs shadow-xl">
                      <div className="font-semibold text-[#E8ECF4]">{d.name}</div>
                      <div className="text-[#8A95B0]">{d.kat}</div>
                      <div className="text-[#4F8EF7]">Vel: {d.x.toFixed(3)} · Marža: {d.y.toFixed(1)}%</div>
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
            <div key={q} className="rounded-xl border border-[#2A3045] bg-[#161A23] p-3">
              <div className="flex items-center gap-2 mb-2">
                <Badge label={QUAD_LABELS[q]} color={QUAD_COLORS[q]} />
                <span className="text-[10px] text-[#8A95B0]">Top 5</span>
              </div>
              <div className="space-y-1">
                {qItems.map((it, i) => (
                  <div key={it.artikalId} className="flex items-center justify-between text-xs py-0.5">
                    <span className="text-[#8A95B0] w-4">{i + 1}.</span>
                    <span className="flex-1 text-[#E8ECF4] truncate">{it.naziv}</span>
                    <span className="text-[#8A95B0] ml-2">V:{it.velocity.toFixed(2)}</span>
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

  const zColorClass = !data ? "text-[#8A95B0]" : data.isExtremeOutlier ? "text-[#E05C5C]" : data.isOutlier ? "text-[#F5C542]" : "text-[#4CAF82]";

  return (
    <div className="space-y-5">
      <SectionHeader title="Analiza Dana & Tjedna Potražnja" subtitle="Z-score detekcija anomalija i heatmap nedeljne aktivnosti" />
      <div className="rounded-xl border border-[#E05C5C]/20 bg-[#E05C5C]/5 px-4 py-3 text-xs text-[#8A95B0]">
        Primarni prikaz koristi intelligence inventory signal layer. Legacy depletion forecast ostaje fallback ako signal cache nije spreman.
      </div>
      <div className="flex gap-2">
        <button onClick={() => setSubView("analiza")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "analiza" ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0]"}`}>📊 Dnevna Analiza</button>
        <button onClick={() => setSubView("heatmap")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "heatmap" ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0]"}`}>🔥 Heatmap</button>
      </div>

      {subView === "analiza" && (
        <>
          <div className="flex items-center gap-3 flex-wrap">
            <label className="text-sm text-[#8A95B0]">Dan:</label>
            <input type="date" value={selectedDate} onChange={e => onDateChange(e.target.value)}
              className="rounded-lg border border-[#2A3045] bg-[#1E2332] px-3 py-1.5 text-sm text-[#E8ECF4] focus:border-[#4F8EF7] focus:outline-none" />
            {loading && <span className="text-xs text-[#8A95B0] animate-pulse">Učitavanje…</span>}
          </div>
          {data && (
            <>
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                <MiniStat label="Prihod tog dana" value={fmtRsd(data.targetRevenue)} />
                <MiniStat label="Prosek perioda" value={fmtRsd(data.meanRevenue)} />
                <MiniStat label="Z-Score" value={data.zScore.toFixed(2)} color={data.isExtremeOutlier ? PAL.red : data.isOutlier ? PAL.yellow : PAL.green} />
                <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-3">
                  <div className="text-[10px] uppercase text-[#8A95B0]">Outlier?</div>
                  <div className={`text-sm font-bold ${zColorClass}`}>{data.isExtremeOutlier ? "⚡ Ekstremni" : data.isOutlier ? "⚠ Da" : "✓ Ne"}</div>
                </div>
              </div>
              <div className="h-[200px]">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={data.dailyData}>
                    <CartesianGrid stroke="#2A3045" />
                    <XAxis dataKey="date" tick={{ fill: "#8A95B0", fontSize: 9 }} axisLine={false} tickLine={false} interval={Math.floor(data.dailyData.length / 10)} />
                    <YAxis tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false} tickFormatter={v => fmtRsd(v)} width={70} />
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
                  <h4 className="mb-2 text-xs font-semibold text-[#c9d3e4]">Top 5 artikala — {data.analysisDate}</h4>
                  <div className="space-y-1.5">
                    {data.top5Articles.map((a, i) => (
                      <div key={a.artikalId} className="flex items-center gap-3 rounded-lg bg-[#161A23] border border-[#2A3045] px-3 py-2">
                        <span className="text-[#4F8EF7] font-bold w-5 text-center">{i + 1}</span>
                        <div className="flex-1 min-w-0"><div className="text-sm text-[#E8ECF4] truncate">{a.naziv}</div><div className="text-[10px] text-[#8A95B0]">{a.kategorija}</div></div>
                        <div className="text-right"><div className="text-sm font-semibold text-[#4CAF82]">{fmtRsd(a.revenue)}</div><div className="text-[10px] text-[#8A95B0]">{a.units} kom</div></div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </>
          )}
          {!data && !loading && <p className="text-[#8A95B0] text-sm">Izaberite dan za analizu.</p>}
        </>
      )}

      {subView === "heatmap" && (
        <>
          {heatmapLoading ? <Skeleton rows={7} /> : heatmap ? (
            <div className="space-y-4">
              <h4 className="text-xs font-semibold text-[#c9d3e4]">Prosečan prihod po danu u nedelji</h4>
              <div className="grid grid-cols-7 gap-2">
                {heatmap.byDay.map(d => {
                  const maxRev = Math.max(...heatmap.byDay.map(x => x.avgRevenue));
                  const intensity = maxRev > 0 ? d.avgRevenue / maxRev : 0;
                  return (
                    <div key={d.day} className="rounded-lg border border-[#2A3045] p-3 text-center transition hover:scale-105"
                      style={{ background: `rgba(79, 142, 247, ${0.05 + intensity * 0.4})` }}>
                      <div className="text-xs font-bold text-[#c9d3e4]">{d.dayName}</div>
                      <div className="text-sm font-bold text-[#E8ECF4] mt-1">{fmtRsd(d.avgRevenue)}</div>
                      <div className="text-[10px] text-[#8A95B0]">{d.avgUnits.toFixed(0)} kom</div>
                    </div>
                  );
                })}
              </div>
              <h4 className="text-xs font-semibold text-[#c9d3e4] mt-4">Trend po danima</h4>
              <div className="h-[180px]">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={heatmap.byDay} barSize={36}>
                    <CartesianGrid stroke="#2A3045" vertical={false} />
                    <XAxis dataKey="dayName" tick={{ fill: "#8A95B0", fontSize: 11 }} axisLine={false} tickLine={false} />
                    <YAxis tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false} tickFormatter={v => fmtRsd(v)} />
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
          ) : <p className="text-[#8A95B0] text-sm">Nema podataka za heatmapu.</p>}
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
}: {
  abcData: AbcItem[]; abcLoading: boolean;
  lifecycle: LifecycleResult | null; lifecycleLoading: boolean;
}) {
  const [subView, setSubView] = useState<"abc" | "lifecycle">("abc");
  const [showAll, setShowAll] = useState(false);

  return (
    <div className="space-y-5">
      <SectionHeader title="ABC Klasifikacija & Životni Ciklus Proizvoda" subtitle="Pareto analiza prihoda i klasifikacija faze životnog ciklusa" />
      <div className="flex gap-2">
        <button onClick={() => setSubView("abc")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "abc" ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0]"}`}>📊 ABC</button>
        <button onClick={() => setSubView("lifecycle")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "lifecycle" ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0]"}`}>🔄 Životni Ciklus</button>
      </div>

      {subView === "abc" && (
        abcLoading ? <Skeleton rows={8} /> : !abcData.length ? <p className="text-[#8A95B0] text-sm">Nema podataka.</p> : (
          <AbcContent data={abcData} showAll={showAll} setShowAll={setShowAll} />
        )
      )}

      {subView === "lifecycle" && (
        lifecycleLoading ? <Skeleton rows={8} /> : !lifecycle ? <p className="text-[#8A95B0] text-sm">Nema podataka životnog ciklusa.</p> : (
          <LifecycleContent data={lifecycle} />
        )
      )}
    </div>
  );
}

function AbcContent({ data, showAll, setShowAll }: { data: AbcItem[]; showAll: boolean; setShowAll: (v: boolean) => void }) {
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
        <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-4">
          <h4 className="mb-2 text-xs font-semibold text-[#c9d3e4]">ABC Distribucija</h4>
          <div className="h-[180px]">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie data={donutData} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={75} innerRadius={40} paddingAngle={3}>
                  {donutData.map((_, i) => <Cell key={i} fill={[PAL.green, PAL.yellow, PAL.red][i]} />)}
                </Pie>
                <Tooltip contentStyle={tooltipStyle} formatter={(v: number | string | undefined) => [fmtRsd(Number(v ?? 0)), "Prihod"]} />
                <Legend formatter={v => <span style={{ color: "#8A95B0", fontSize: 11 }}>{v}</span>} />
              </PieChart>
            </ResponsiveContainer>
          </div>
          <div className="grid grid-cols-3 gap-2 mt-2">
            {donutData.map((d, i) => (
              <div key={i} className="text-center">
                <div className="text-xs font-bold" style={{ color: [PAL.green, PAL.yellow, PAL.red][i] }}>{d.count} SKU</div>
                <div className="text-[10px] text-[#8A95B0]">{total > 0 ? ((d.value / total) * 100).toFixed(0) : 0}% prih.</div>
              </div>
            ))}
          </div>
        </div>
        <div className="lg:col-span-2 space-y-2">
          <div className="rounded-xl border border-[#2A3045] bg-[#1E2332] px-4 py-3 text-sm text-[#8A95B0]">
            <span className="font-semibold text-[#4CAF82]">Klasa A</span> — top artikli (~70% prihoda). Prioritet u nabavci, nikad out-of-stock.
          </div>
          <div className="rounded-xl border border-[#2A3045] bg-[#1E2332] px-4 py-3 text-sm text-[#8A95B0]">
            <span className="font-semibold text-[#F5C542]">Klasa B</span> — srednji artikli (70-90% kum.). Pratiti trendove i pravovremeno dopunjavati.
          </div>
          <div className="rounded-xl border border-[#2A3045] bg-[#1E2332] px-4 py-3 text-sm text-[#8A95B0]">
            <span className="font-semibold text-[#E05C5C]">Klasa C</span> — dugi rep. Razmotriti likvidaciju ili specijalne promocije.
          </div>
        </div>
      </div>
      <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
        <table className="w-full text-xs">
          <thead><tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
            <th className="px-3 py-2 text-left">Artikal</th><th className="px-3 py-2 text-left">Kat.</th>
            <th className="px-3 py-2 text-right">Prihod</th><th className="px-3 py-2 text-right">Udeo%</th>
            <th className="px-3 py-2 text-right">Kum.%</th><th className="px-3 py-2 text-right">Kom</th>
            <th className="px-3 py-2 text-center">Klasa</th>
          </tr></thead>
          <tbody>
            {displayed.map(item => (
              <tr key={item.artikalId} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                <td className="px-3 py-2 text-[#E8ECF4] max-w-[160px] truncate">{item.naziv}</td>
                <td className="px-3 py-2 text-[#8A95B0]">{item.kategorija}</td>
                <td className="px-3 py-2 text-right">{fmtRsd(item.totalRevenue)}</td>
                <td className="px-3 py-2 text-right text-[#8A95B0]">{item.revPct.toFixed(1)}%</td>
                <td className="px-3 py-2 text-right text-[#8A95B0]">{item.cumulativePct.toFixed(1)}%</td>
                <td className="px-3 py-2 text-right text-[#8A95B0]">{item.totalUnits}</td>
                <td className="px-3 py-2 text-center"><Badge label={item.abcClass} color={ABC_COLORS[item.abcClass]} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {data.length > 15 && (
        <button onClick={() => setShowAll(!showAll)} className="text-xs text-[#4F8EF7] hover:underline">
          {showAll ? "Prikaži manje" : `Prikaži svih ${data.length} →`}
        </button>
      )}
    </div>
  );
}

function LifecycleContent({ data }: { data: LifecycleResult }) {
  const stages = ["LAUNCH", "GROWTH", "MATURE", "DECLINE"] as const;
  return (
    <div className="space-y-5">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {stages.map(s => (
          <div key={s} className="rounded-xl border px-3 py-2" style={{ borderColor: `${STAGE_COLORS[s]}33`, background: `${STAGE_COLORS[s]}08` }}>
            <div className="text-[10px] uppercase" style={{ color: STAGE_COLORS[s] }}>{STAGE_LABELS[s]}</div>
            <div className="text-xl font-bold" style={{ color: STAGE_COLORS[s] }}>{data.summary[s.toLowerCase() as keyof typeof data.summary]}</div>
            <div className="text-[10px] text-[#8A95B0]">artikala</div>
          </div>
        ))}
      </div>
      <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
        <table className="w-full text-xs">
          <thead><tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
            <th className="px-3 py-2 text-left">Artikal</th><th className="px-3 py-2 text-left">Kat.</th>
            <th className="px-3 py-2 text-right">Prodato</th><th className="px-3 py-2 text-right">Trend</th>
            <th className="px-3 py-2 text-right">Zaliha</th><th className="px-3 py-2 text-center">Faza</th>
          </tr></thead>
          <tbody>
            {data.items.slice(0, 30).map(it => (
              <tr key={it.artikalId} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                <td className="px-3 py-2 text-[#E8ECF4] max-w-[160px] truncate">{it.naziv}</td>
                <td className="px-3 py-2 text-[#8A95B0]">{it.kategorija}</td>
                <td className="px-3 py-2 text-right">{it.totalUnits}</td>
                <td className="px-3 py-2 text-right" style={{ color: it.trendPct >= 0 ? PAL.green : PAL.red }}>{it.trendPct >= 0 ? "+" : ""}{it.trendPct.toFixed(0)}%</td>
                <td className="px-3 py-2 text-right text-[#8A95B0]">{it.currentStock}</td>
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
}: {
  agingItems: AgingItem[]; agingLoading: boolean;
  agingSummary?: { totalSKU: number; critical: number; warning: number; watch: number; active: number; criticalStockValue: number };
  depletion: DepletionResult | null; depletionLoading: boolean;
}) {
  const [subView, setSubView] = useState<"aging" | "depletion">("aging");
  const [filter, setFilter] = useState("Sve");
  const [showAll, setShowAll] = useState(false);

  return (
    <div className="space-y-5">
      <SectionHeader title="Stanje Zaliha & Prognoza Iscrpljenja" subtitle="Aging analiza + automatski forecast datuma OOS-a" />
      <div className="flex gap-2">
        <button onClick={() => setSubView("aging")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "aging" ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0]"}`}>📦 Aging</button>
        <button onClick={() => setSubView("depletion")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "depletion" ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0]"}`}>📉 Deplecija</button>
      </div>

      {subView === "aging" && (
        agingLoading ? <Skeleton rows={8} /> : !agingItems.length ? <p className="text-[#8A95B0] text-sm">Nema zaliha.</p> : (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              {[
                { name: "Kritično (>90d)", value: agingSummary?.critical ?? 0, fill: PAL.red },
                { name: "Upozorenje (>60d)", value: agingSummary?.warning ?? 0, fill: PAL.orange },
                { name: "Pazi (>30d)", value: agingSummary?.watch ?? 0, fill: PAL.yellow },
                { name: "Aktivno (<30d)", value: agingSummary?.active ?? 0, fill: PAL.green },
              ].map(b => (
                <div key={b.name} className="rounded-xl border border-[#2A3045] bg-[#161A23] p-3">
                  <div className="text-[10px] text-[#8A95B0] uppercase">{b.name}</div>
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
                  className={`rounded-lg px-3 py-1 text-xs font-medium transition ${filter === c ? "ring-1 ring-current" : "text-[#8A95B0]"}`}
                  style={filter === c && c !== "Sve" ? { color: AGING_COLORS[c], background: `${AGING_COLORS[c]}18` } : undefined}>
                  {c}
                </button>
              ))}
            </div>
            <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
              <table className="w-full text-xs">
                <thead><tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
                  <th className="px-3 py-2 text-left">Artikal</th><th className="px-3 py-2 text-left">Kat.</th>
                  <th className="px-3 py-2 text-right">Zaliha</th><th className="px-3 py-2 text-right">Posl. prod.</th>
                  <th className="px-3 py-2 text-right">Dana</th><th className="px-3 py-2 text-center">Status</th>
                </tr></thead>
                <tbody>
                  {(() => {
                    const f = filter === "Sve" ? agingItems : agingItems.filter(x => x.agingCategory === filter);
                    return (showAll ? f : f.slice(0, 20)).map(item => (
                      <tr key={item.id} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                        <td className="px-3 py-2 text-[#E8ECF4] max-w-[160px] truncate">{item.naziv}</td>
                        <td className="px-3 py-2 text-[#8A95B0]">{item.kategorija}</td>
                        <td className="px-3 py-2 text-right">{item.kolicina}</td>
                        <td className="px-3 py-2 text-right text-[#8A95B0]">{item.lastSaleDate}</td>
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
                <button onClick={() => setShowAll(!showAll)} className="text-xs text-[#4F8EF7] hover:underline">
                  {showAll ? "Manje" : `Svih ${f.length} →`}
                </button>
              );
            })()}
          </div>
        )
      )}

      {subView === "depletion" && (
        depletionLoading ? <Skeleton rows={8} /> : !depletion ? <p className="text-[#8A95B0] text-sm">Nema podataka deplecije.</p> : (
          <div className="space-y-4">
            <div className="grid grid-cols-3 gap-3">
              <MiniStat label="Kritičan OOS" value={depletion.criticalCount} color={PAL.red} />
              <MiniStat label="Ukupno forecastova" value={depletion.forecasts.length} />
              <MiniStat label="At-Risk prihod" value={fmtRsd(depletion.totalAtRiskRevenue)} color={PAL.orange} />
            </div>
            {depletion.criticalCount > 0 && (
              <AlertBanner severity="danger">⚡ {depletion.criticalCount} artikala ce biti OOS u narednih 7 dana! At-risk prihod: {fmtRsd(depletion.totalAtRiskRevenue)}</AlertBanner>
            )}
            <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
              <table className="w-full text-xs">
                <thead><tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
                  <th className="px-3 py-2 text-left">Artikal</th><th className="px-3 py-2 text-left">Kat.</th>
                  <th className="px-3 py-2 text-right">Zaliha</th><th className="px-3 py-2 text-right">Avg/dan</th>
                  <th className="px-3 py-2 text-right">Dana do OOS</th><th className="px-3 py-2 text-right">Datum OOS</th>
                  <th className="px-3 py-2 text-right">At-Risk</th><th className="px-3 py-2 text-center">Sev.</th>
                </tr></thead>
                <tbody>
                  {depletion.forecasts.slice(0, 30).map(f => (
                    <tr key={f.artikalId} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                      <td className="px-3 py-2 text-[#E8ECF4] max-w-[140px] truncate">{f.naziv}</td>
                      <td className="px-3 py-2 text-[#8A95B0]">{f.kategorija}</td>
                      <td className="px-3 py-2 text-right">{f.currentStock}</td>
                      <td className="px-3 py-2 text-right text-[#8A95B0]">{f.avgDailySales.toFixed(2)}</td>
                      <td className="px-3 py-2 text-right font-bold" style={{ color: SEVERITY_COLORS[f.severity] }}>{f.daysUntilOOS > 365 ? "∞" : `${f.daysUntilOOS}d`}</td>
                      <td className="px-3 py-2 text-right text-[#8A95B0]">{f.daysUntilOOS > 365 ? "—" : f.depletionDate}</td>
                      <td className="px-3 py-2 text-right text-[#F97316]">{fmtRsd(f.atRiskRevenue)}</td>
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
}: {
  smartData: SmartReorderResult | null; smartLoading: boolean;
  v1Items: ReorderItem[]; v1Loading: boolean;
  v1Summary?: { criticalCount: number; urgentCount: number; recommendedCount: number; totalReorderValue: number };
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

      <div className="rounded-xl border border-[#4CAF82]/20 bg-[#4CAF82]/5 px-4 py-3 text-xs text-[#8A95B0]">
        Reorder prioriteti sada se prvenstveno izvode iz demand, inventory, price i trend intelligence signala, a legacy plan ostaje rezervni fallback.
      </div>

      {/* Summary KPIs */}
      {summary && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-6">
          <div className="rounded-xl border border-[#E05C5C]/30 bg-[#E05C5C]/10 p-3">
            <div className="text-[10px] text-[#E05C5C] uppercase">Kritično</div>
            <div className="text-2xl font-bold text-[#E05C5C]">{summary.criticalCount}</div>
          </div>
          <div className="rounded-xl border border-[#F97316]/30 bg-[#F97316]/10 p-3">
            <div className="text-[10px] text-[#F97316] uppercase">Hitno</div>
            <div className="text-2xl font-bold text-[#F97316]">{summary.urgentCount}</div>
          </div>
          <div className="rounded-xl border border-[#F5C542]/30 bg-[#F5C542]/10 p-3">
            <div className="text-[10px] text-[#F5C542] uppercase">Preporučuje se</div>
            <div className="text-2xl font-bold text-[#F5C542]">{summary.recommendedCount}</div>
          </div>
          <div className="rounded-xl border border-[#4F8EF7]/30 bg-[#4F8EF7]/10 p-3">
            <div className="text-[10px] text-[#4F8EF7] uppercase">Trošak nabavke</div>
            <div className="text-lg font-bold text-[#4F8EF7]">{fmtRsd("totalReorderCost" in summary ? (summary as {totalReorderCost: number}).totalReorderCost : ("totalReorderValue" in summary ? (summary as {totalReorderValue: number}).totalReorderValue : 0))}</div>
          </div>
          {useSmart && (
            <>
              <div className="rounded-xl border border-[#4CAF82]/30 bg-[#4CAF82]/10 p-3">
                <div className="text-[10px] text-[#4CAF82] uppercase">Očekivani prihod</div>
                <div className="text-lg font-bold text-[#4CAF82]">{fmtRsd(smartData!.summary.expectedRevenueFromReorder)}</div>
              </div>
              <div className="rounded-xl border border-[#9B72CF]/30 bg-[#9B72CF]/10 p-3">
                <div className="text-[10px] text-[#9B72CF] uppercase">Očekivani profit</div>
                <div className="text-lg font-bold text-[#9B72CF]">{fmtRsd(smartData!.summary.expectedProfitFromReorder)}</div>
              </div>
            </>
          )}
        </div>
      )}

      {/* Sub navigation for smart reorder */}
      {useSmart && (
        <div className="flex gap-2">
          <button onClick={() => setSubView("artikli")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "artikli" ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0]"}`}>📋 Po artiklima</button>
          <button onClick={() => setSubView("kategorije")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "kategorije" ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0]"}`}>📊 Po kategorijama</button>
          <button onClick={() => setSubView("dobavljaci")} className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${subView === "dobavljaci" ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0]"}`}>🏭 Po dobavljačima</button>
        </div>
      )}

      {subView === "artikli" && (
        <>
          <div className="flex gap-2 flex-wrap">
            {urgencies.map(u => (
              <button key={u} onClick={() => { setUrgencyFilter(u); setShowAll(false); }}
                className={`rounded-lg px-3 py-1 text-xs font-medium transition ${urgencyFilter === u ? "ring-1 ring-current" : "text-[#8A95B0]"}`}
                style={urgencyFilter === u && u !== "Sve" ? { color: URGENCY_COLORS[u], background: `${URGENCY_COLORS[u]}18` } : undefined}>
                {u}
              </button>
            ))}
          </div>
          <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
            <table className="w-full text-xs">
              <thead><tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
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
                  <tr key={item.artikalId} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                    <td className="px-3 py-2 text-[#E8ECF4] max-w-[130px] truncate">{item.naziv}</td>
                    <td className="px-2 py-2 text-[#8A95B0]">{item.kategorija}</td>
                    <td className="px-2 py-2 text-[#8A95B0]">{item.dobavljacNaziv}</td>
                    <td className="px-2 py-2 text-right">{item.currentStock}</td>
                    <td className="px-2 py-2 text-right text-[#8A95B0]">{item.avgDailySales.toFixed(2)}</td>
                    <td className="px-2 py-2 text-right font-semibold" style={{ color: item.doh < 7 ? PAL.red : item.doh < 14 ? PAL.orange : item.doh < 30 ? PAL.yellow : PAL.green }}>
                      {item.doh > 900 ? "∞" : `${item.doh.toFixed(0)}d`}
                    </td>
                    <td className="px-2 py-2 text-right font-bold" style={{ color: item.needsReorder ? PAL.yellow : PAL.textMuted }}>
                      {item.needsReorder ? `+${item.recommendedQty}` : "—"}
                    </td>
                    {useSmart && (() => { const s = item as unknown as { marginPct?: number }; return <td className="px-2 py-2 text-right" style={{ color: s.marginPct !== undefined && s.marginPct >= 20 ? PAL.green : PAL.orange }}>{s.marginPct !== undefined ? fmtPct(s.marginPct) : "—"}</td>; })()}
                    {useSmart && (() => { const s = item as unknown as { reorderProbability?: number }; return <td className="px-2 py-2 text-right text-[#4F8EF7]">{s.reorderProbability !== undefined ? fmtPct(s.reorderProbability) : "—"}</td>; })()}
                    <td className="px-2 py-2 text-center"><Badge label={item.urgency} color={URGENCY_COLORS[item.urgency]} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {filteredReorder.length > 20 && (
            <button onClick={() => setShowAll(!showAll)} className="text-xs text-[#4F8EF7] hover:underline">
              {showAll ? "Manje" : `Svih ${filteredReorder.length} →`}
            </button>
          )}
        </>
      )}

      {subView === "kategorije" && useSmart && (
        <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
          <table className="w-full text-xs">
            <thead><tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
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
                <tr key={i} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                  <td className="px-3 py-2 font-medium text-[#E8ECF4]">{c.kategorija}</td>
                  <td className="px-3 py-2 text-right text-[#8A95B0]">{c.totalItems}</td>
                  <td className="px-3 py-2 text-right" style={{ color: c.criticalCount > 0 ? PAL.red : PAL.textSecondary }}>{c.criticalCount}</td>
                  <td className="px-3 py-2 text-right" style={{ color: c.urgentCount > 0 ? PAL.orange : PAL.textSecondary }}>{c.urgentCount}</td>
                  <td className="px-3 py-2 text-right text-[#4F8EF7]">{fmtRsd(c.totalReorderCost)}</td>
                  <td className="px-3 py-2 text-right text-[#4CAF82]">{fmtRsd(c.expectedRevenue)}</td>
                  <td className="px-3 py-2 text-right" style={{ color: c.avgMargin >= 20 ? PAL.green : PAL.yellow }}>{fmtPct(c.avgMargin)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {subView === "dobavljaci" && useSmart && (
        <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
          <table className="w-full text-xs">
            <thead><tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
              <th className="px-3 py-2 text-left">Dobavljač</th>
              <th className="px-3 py-2 text-right">Artikala</th>
              <th className="px-3 py-2 text-right">Kritično</th>
              <th className="px-3 py-2 text-right">Trošak nabavke</th>
              <th className="px-3 py-2 text-right">Avg prob. reordering</th>
            </tr></thead>
            <tbody>
              {smartData!.bySupplierPlan.map((s, i) => (
                <tr key={i} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                  <td className="px-3 py-2 font-medium text-[#E8ECF4]">{s.dobavljac}</td>
                  <td className="px-3 py-2 text-right text-[#8A95B0]">{s.totalItems}</td>
                  <td className="px-3 py-2 text-right" style={{ color: s.criticalCount > 0 ? PAL.red : PAL.textSecondary }}>{s.criticalCount}</td>
                  <td className="px-3 py-2 text-right text-[#4F8EF7]">{fmtRsd(s.totalReorderCost)}</td>
                  <td className="px-3 py-2 text-right text-[#4CAF82]">{fmtPct(s.avgReorderProbability)}</td>
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
  const [activeTab, setActiveTab] = useState<TabKey>("pregled");
  const [periodDays, setPeriodDays] = useState(30);

  const toDate = toDateStr(new Date());
  const fromDate = toDateStr(daysAgo(periodDays));

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
          <h1 className="text-xl font-bold text-white">Insight Studio</h1>
          <span className="rounded bg-gradient-to-r from-[#4F8EF7]/20 to-[#9B72CF]/20 px-2 py-0.5 text-[10px] font-semibold text-[#7ea5ff] uppercase tracking-wider">
            Analitika 2 — Pro
          </span>
        </div>
        <p className="text-[11px] text-[#8A95B0]">
          Napredna analiza profitabilnosti, V×M matrica, lifecycle, deplecija i reorder 2.0
        </p>
      </div>

      {/* ── Period Filter ──────────────────────────────────────── */}
      <div className="flex items-center gap-2 flex-wrap rounded-xl border border-[#2A3045] bg-[#161A23] px-4 py-2.5">
        <span className="text-xs text-[#8A95B0] mr-1">Period:</span>
        {PERIOD_PRESETS.map(p => (
          <button key={p.days} onClick={() => handlePeriodChange(p.days)}
            className={`rounded-lg px-3 py-1 text-xs font-medium transition ${periodDays === p.days ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0] hover:text-[#c9d3e4] hover:bg-[#20222a]"}`}>
            {p.label}
          </button>
        ))}
        <span className="ml-auto text-[11px] text-[#4A5270]">{fromDate} → {toDate}</span>
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
      <div className="flex gap-1 flex-wrap rounded-xl border border-[#2A3045] bg-[#161A23] p-1.5 overflow-x-auto">
        {TABS.map(t => (
          <button key={t.key} onClick={() => handleTabChange(t.key)}
            className={`flex items-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium transition whitespace-nowrap ${
              activeTab === t.key ? "bg-[#1f2940] text-[#d8e5ff] ring-1 ring-[#32579e]" : "text-[#8A95B0] hover:text-[#c9d3e4] hover:bg-[#20222a]"
            }`}>
            <span>{t.icon}</span><span>{t.label}</span>
          </button>
        ))}
      </div>

      {/* ── Tab Content ──────────────────────────────────────────── */}
      <div className="rounded-xl border border-[#2A3045] bg-[#0D0F14] p-5">
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
          <SupplierTab v2Data={supplierV2} v1Data={suppliers} loading={supplierV2Loading && supplierLoading} />
        )}
        {activeTab === "kategorije" && (
          <CategoryTab
            byCategory={catData?.byCategory ?? []} byGender={catData?.byGender ?? []}
            priceSensitivity={priceSensitivity} basketAffinity={basketAffinity}
            loading={catLoading}
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
          <AbcLifecycleTab abcData={abcData} abcLoading={abcLoading} lifecycle={lifecycle} lifecycleLoading={lifecycleLoading} />
        )}
        {activeTab === "zalihe" && (
          <StockTab agingItems={agingItems} agingLoading={agingLoading} agingSummary={agingSummary}
            depletion={depletion} depletionLoading={depletionLoading} />
        )}
        {activeTab === "nabavka" && (
          <ReorderTab2 smartData={smartReorder} smartLoading={smartReorderLoading}
            v1Items={reorderItems} v1Loading={reorderLoading} v1Summary={reorderSummary} />
        )}
      </div>
    </div>
  );
}
