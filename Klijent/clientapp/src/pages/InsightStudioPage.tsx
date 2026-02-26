import { useCallback, useEffect, useState } from "react";
import {
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
  Tooltip,
  XAxis,
  YAxis,
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

// ====================== CONSTANTS ======================

type TabKey = "dobavljaci" | "kategorije" | "dnevna" | "abc" | "zalihe" | "planiranje";

const TABS: { key: TabKey; label: string; icon: string }[] = [
  { key: "dobavljaci", label: "Dobavljači", icon: "🏭" },
  { key: "kategorije", label: "Kategorije", icon: "👟" },
  { key: "dnevna", label: "Analiza Dana", icon: "📅" },
  { key: "abc", label: "ABC Klasa", icon: "📊" },
  { key: "zalihe", label: "Stanje Zaliha", icon: "📦" },
  { key: "planiranje", label: "Planiranje", icon: "🛒" },
];

const PERIOD_PRESETS = [
  { label: "7 dana", days: 7 },
  { label: "30 dana", days: 30 },
  { label: "90 dana", days: 90 },
  { label: "6 meseci", days: 180 },
];

const DONUT_COLORS = ["#4F8EF7", "#F5C542", "#4CAF82", "#9B72CF", "#E05C5C", "#F97316", "#22D3EE"];

const ABC_COLORS: Record<string, string> = {
  A: "#4CAF82",
  B: "#F5C542",
  C: "#E05C5C",
};

const AGING_COLORS: Record<string, string> = {
  Aktivno: "#4CAF82",
  Pazi: "#F5C542",
  Upozorenje: "#F97316",
  Kritično: "#E05C5C",
};

const RISK_COLORS: Record<string, string> = {
  LOW: "#4CAF82",
  MED: "#F5C542",
  HIGH: "#E05C5C",
};

const URGENCY_COLORS: Record<string, string> = {
  KRITIČNO: "#E05C5C",
  HITNO: "#F97316",
  "PREPORUČUJE SE": "#F5C542",
  OK: "#4CAF82",
};

// ====================== HELPERS ======================

function toDateStr(d: Date): string {
  return d.toISOString().slice(0, 10);
}

function daysAgo(n: number): Date {
  const d = new Date();
  d.setDate(d.getDate() - n);
  return d;
}

function fmtRsd(v: number): string {
  if (v >= 1_000_000) return `${(v / 1_000_000).toFixed(1)}M RSD`;
  if (v >= 1_000) return `${(v / 1_000).toFixed(0)}k RSD`;
  return `${v.toLocaleString("sr-RS")} RSD`;
}

function fmtPct(v: number, decimals = 1): string {
  return `${v.toFixed(decimals)}%`;
}

function fmtNum(v: number): string {
  return v.toLocaleString("sr-RS");
}

function changeBadge(change: number) {
  const isUp = change >= 0;
  return (
    <span
      className={`ml-1 text-xs font-semibold ${isUp ? "text-[#4CAF82]" : "text-[#E05C5C]"}`}
    >
      {isUp ? "↑" : "↓"} {Math.abs(change).toFixed(1)}%
    </span>
  );
}

// ====================== KPI CARD ======================

interface KpiCardProps {
  label: string;
  value: string;
  sub?: string;
  change?: number;
  accent?: string;
  sparkline?: { date: string; revenue: number }[];
  icon?: string;
}

function KpiCard({ label, value, sub, change, accent = "#4F8EF7", sparkline, icon }: KpiCardProps) {
  return (
    <div className="relative rounded-xl border border-[#2A3045] bg-[#161A23] p-4 flex flex-col gap-1 overflow-hidden">
      <div
        className="absolute left-0 top-0 h-[3px] w-full"
        style={{ background: accent }}
      />
      <div className="flex items-center gap-1.5 text-[11px] text-[#8A95B0] uppercase tracking-wider">
        {icon && <span>{icon}</span>}
        {label}
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
            <LineChart data={sparkline}>
              <Line
                type="monotone"
                dataKey="revenue"
                stroke={accent}
                strokeWidth={1.5}
                dot={false}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}

// ====================== LOADING SKELETON ======================

function CardSkeleton() {
  return (
    <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-4 animate-pulse">
      <div className="h-3 w-24 bg-[#2A3045] rounded mb-3" />
      <div className="h-6 w-32 bg-[#2A3045] rounded mb-2" />
      <div className="h-3 w-20 bg-[#2A3045] rounded" />
    </div>
  );
}

function TableSkeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className="space-y-2 animate-pulse">
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

// ====================== SUPPLIER TAB ======================

function SupplierTab({
  data,
  loading,
}: {
  data: SupplierScore[];
  loading: boolean;
}) {
  const [selected, setSelected] = useState<SupplierScore | null>(null);

  if (loading) return <TableSkeleton rows={8} />;
  if (!data.length)
    return <p className="text-[#8A95B0] text-sm">Nema podataka o dobavljačima za izabrani period.</p>;

  const displayed = selected ?? data[0];

  function ScoreBar({ label, score }: { label: string; score: number }) {
    const pct = Math.min(100, Math.max(0, score));
    const color = pct >= 70 ? "#4CAF82" : pct >= 40 ? "#F5C542" : "#E05C5C";
    return (
      <div>
        <div className="flex justify-between text-[11px] text-[#8A95B0] mb-0.5">
          <span>{label}</span>
          <span style={{ color }}>{pct.toFixed(0)}/100</span>
        </div>
        <div className="h-1.5 rounded-full bg-[#2A3045] overflow-hidden">
          <div className="h-full rounded-full" style={{ width: `${pct}%`, background: color }} />
        </div>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-5 lg:grid-cols-5">
      {/* Leaderboard */}
      <div className="lg:col-span-3">
        <h3 className="mb-3 text-sm font-semibold text-[#c9d3e4]">Leaderboard — Dobavljači</h3>
        <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
                <th className="px-3 py-2 text-left">#</th>
                <th className="px-3 py-2 text-left">Dobavljač</th>
                <th className="px-3 py-2 text-right">Prihod</th>
                <th className="px-3 py-2 text-right">Marža%</th>
                <th className="px-3 py-2 text-right">Zavisnost</th>
                <th className="px-3 py-2 text-center">Risk</th>
                <th className="px-3 py-2 text-right">Skor</th>
              </tr>
            </thead>
            <tbody>
              {data.map((s, i) => (
                <tr
                  key={s.dobavljacId ?? i}
                  onClick={() => setSelected(s)}
                  className={`cursor-pointer border-b border-[#2A3045] transition hover:bg-[#1E2332] ${
                    displayed.dobavljacId === s.dobavljacId ? "bg-[#1f2940] ring-1 ring-inset ring-[#32579e]" : ""
                  }`}
                >
                  <td className="px-3 py-2 text-[#8A95B0]">{i + 1}</td>
                  <td className="px-3 py-2 font-medium text-[#E8ECF4]">{s.dobavljacNaziv}</td>
                  <td className="px-3 py-2 text-right text-[#E8ECF4]">{fmtRsd(s.totalRevenue)}</td>
                  <td className="px-3 py-2 text-right text-[#4CAF82]">{fmtPct(s.marginPct)}</td>
                  <td
                    className="px-3 py-2 text-right"
                    style={{ color: s.dependencyRatio > 30 ? "#E05C5C" : s.dependencyRatio > 15 ? "#F5C542" : "#8A95B0" }}
                  >
                    {fmtPct(s.dependencyRatio)}
                  </td>
                  <td className="px-3 py-2 text-center">
                    <span
                      className="rounded px-1.5 py-0.5 text-[10px] font-semibold"
                      style={{
                        background: `${RISK_COLORS[s.riskLevel]}22`,
                        color: RISK_COLORS[s.riskLevel],
                      }}
                    >
                      {s.riskLevel}
                    </span>
                  </td>
                  <td className="px-3 py-2 text-right font-semibold" style={{ color: "#4F8EF7" }}>
                    {s.compositeScore.toFixed(0)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Scorecard */}
      <div className="lg:col-span-2">
        <h3 className="mb-3 text-sm font-semibold text-[#c9d3e4]">
          Scorecard — {displayed.dobavljacNaziv}
        </h3>
        <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-4 space-y-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="rounded-lg bg-[#1E2332] px-3 py-2">
              <div className="text-[10px] text-[#8A95B0] uppercase">Prihod</div>
              <div className="font-semibold text-[#E8ECF4] text-sm">{fmtRsd(displayed.totalRevenue)}</div>
            </div>
            <div className="rounded-lg bg-[#1E2332] px-3 py-2">
              <div className="text-[10px] text-[#8A95B0] uppercase">Marža</div>
              <div className="font-semibold text-[#4CAF82] text-sm">{fmtPct(displayed.marginPct)}</div>
            </div>
            <div className="rounded-lg bg-[#1E2332] px-3 py-2">
              <div className="text-[10px] text-[#8A95B0] uppercase">Zavisnost</div>
              <div
                className="font-semibold text-sm"
                style={{ color: RISK_COLORS[displayed.riskLevel] }}
              >
                {fmtPct(displayed.dependencyRatio)}
              </div>
            </div>
            <div className="rounded-lg bg-[#1E2332] px-3 py-2">
              <div className="text-[10px] text-[#8A95B0] uppercase">Kategorije</div>
              <div className="font-semibold text-[#E8ECF4] text-sm">{displayed.uniqueCategories}</div>
            </div>
          </div>
          <div className="space-y-3">
            <ScoreBar label="Profitabilnost" score={displayed.profitScore} />
            <ScoreBar label="Diverzifikacija" score={displayed.diversityScore} />
            <ScoreBar label="Niska zavisnost" score={displayed.dependencyScore} />
          </div>
          <div className="rounded-lg bg-[#1E2332] px-3 py-2 flex items-center justify-between">
            <span className="text-xs text-[#8A95B0]">Kompozitni Skor</span>
            <span
              className="text-lg font-bold"
              style={{
                color:
                  displayed.compositeScore >= 70
                    ? "#4CAF82"
                    : displayed.compositeScore >= 40
                    ? "#F5C542"
                    : "#E05C5C",
              }}
            >
              {displayed.compositeScore.toFixed(1)}
            </span>
          </div>
          <div className="h-[130px]">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart
                data={[
                  { name: "Profit", value: displayed.profitScore },
                  { name: "Diverz", value: displayed.diversityScore },
                  { name: "Zavis", value: displayed.dependencyScore },
                ]}
                barSize={28}
              >
                <CartesianGrid stroke="#2A3045" vertical={false} />
                <XAxis dataKey="name" tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false} />
                <YAxis domain={[0, 100]} tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false} />
                <Tooltip
                  contentStyle={{ background: "#1E2332", border: "1px solid #2A3045", borderRadius: 8, fontSize: 12 }}
                  itemStyle={{ color: "#E8ECF4" }}
                />
                <Bar dataKey="value" fill="#4F8EF7" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      </div>
    </div>
  );
}

// ====================== CATEGORY TAB ======================

function CategoryTab({
  byCategory,
  byGender,
  loading,
}: {
  byCategory: CategoryStat[];
  byGender: GenderStat[];
  loading: boolean;
}) {
  const [subTab, setSubTab] = useState<"kategorije" | "pol">("kategorije");

  if (loading) return <TableSkeleton rows={6} />;
  if (!byCategory.length)
    return <p className="text-[#8A95B0] text-sm">Nema podataka za izabrani period.</p>;

  return (
    <div className="space-y-5">
      {/* Sub-tabs */}
      <div className="flex gap-2">
        {(["kategorije", "pol"] as const).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => setSubTab(t)}
            className={`rounded-lg px-3 py-1.5 text-xs font-medium transition ${
              subTab === t
                ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]"
                : "text-[#8A95B0] hover:text-[#c9d3e4]"
            }`}
          >
            {t === "kategorije" ? "Po Tipu Obuće" : "Po Polu"}
          </button>
        ))}
      </div>

      {subTab === "kategorije" && (
        <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
          {/* Bar chart */}
          <div>
            <h3 className="mb-3 text-sm font-semibold text-[#c9d3e4]">Prihod po Kategoriji</h3>
            <div className="h-[260px]">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={byCategory.slice(0, 8)} layout="vertical" barSize={18}>
                  <CartesianGrid stroke="#2A3045" horizontal={false} />
                  <XAxis type="number" tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false}
                    tickFormatter={(v) => fmtRsd(Number(v))} />
                  <YAxis type="category" dataKey="kategorija" tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false} width={80} />
                  <Tooltip
                    contentStyle={{ background: "#1E2332", border: "1px solid #2A3045", borderRadius: 8, fontSize: 12 }}
                    formatter={(v: number | string | undefined) => [fmtRsd(Number(v ?? 0)), "Prihod"]}
                  />
                  <Bar dataKey="totalRevenue" fill="#4F8EF7" radius={[0, 4, 4, 0]}>
                    {byCategory.slice(0, 8).map((entry, i) => (
                      <Cell key={i} fill={DONUT_COLORS[i % DONUT_COLORS.length]} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>

          {/* Category Velocity Table */}
          <div>
            <h3 className="mb-3 text-sm font-semibold text-[#c9d3e4]">Category Intelligence</h3>
            <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
              <table className="w-full text-xs">
                <thead>
                  <tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
                    <th className="px-3 py-2 text-left">Kategorija</th>
                    <th className="px-3 py-2 text-right">Udeo%</th>
                    <th className="px-3 py-2 text-right">Marža%</th>
                    <th className="px-3 py-2 text-right">Profit Lift</th>
                    <th className="px-3 py-2 text-right">Velocity</th>
                  </tr>
                </thead>
                <tbody>
                  {byCategory.map((cat, i) => (
                    <tr key={i} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                      <td className="px-3 py-2 font-medium text-[#E8ECF4]">{cat.kategorija}</td>
                      <td className="px-3 py-2 text-right text-[#8A95B0]">{fmtPct(cat.revShare)}</td>
                      <td className="px-3 py-2 text-right text-[#4CAF82]">{fmtPct(cat.marginPct)}</td>
                      <td
                        className="px-3 py-2 text-right font-semibold"
                        style={{ color: cat.profitLift >= 0 ? "#4CAF82" : "#E05C5C" }}
                      >
                        {cat.profitLift >= 0 ? "+" : ""}{fmtPct(cat.profitLift)}
                      </td>
                      <td className="px-3 py-2 text-right text-[#4F8EF7]">{cat.velocity.toFixed(3)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {subTab === "pol" && (
        <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
          {/* Donut */}
          <div>
            <h3 className="mb-3 text-sm font-semibold text-[#c9d3e4]">Distribucija po Polu</h3>
            <div className="h-[220px]">
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie
                    data={(byGender as unknown) as Record<string, unknown>[]}
                    dataKey="totalRevenue"
                    nameKey="pol"
                    cx="50%"
                    cy="50%"
                    outerRadius={85}
                    innerRadius={45}
                    paddingAngle={3}
                  >
                    {byGender.map((_, i) => (
                      <Cell key={i} fill={DONUT_COLORS[i % DONUT_COLORS.length]} />
                    ))}
                  </Pie>
                  <Tooltip
                    contentStyle={{ background: "#1E2332", border: "1px solid #2A3045", borderRadius: 8, fontSize: 12 }}
                    formatter={(v: number | string | undefined) => [fmtRsd(Number(v ?? 0)), "Prihod"]}
                  />
                  <Legend
                    formatter={(value) => <span style={{ color: "#8A95B0", fontSize: 12 }}>{value}</span>}
                  />
                </PieChart>
              </ResponsiveContainer>
            </div>
          </div>

          {/* Table */}
          <div className="overflow-x-auto rounded-xl border border-[#2A3045] self-start">
            <table className="w-full text-xs">
              <thead>
                <tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
                  <th className="px-3 py-2 text-left">Pol</th>
                  <th className="px-3 py-2 text-right">Prihod</th>
                  <th className="px-3 py-2 text-right">Udeo%</th>
                  <th className="px-3 py-2 text-right">Kom</th>
                </tr>
              </thead>
              <tbody>
                {byGender.map((g, i) => (
                  <tr key={i} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                    <td className="px-3 py-2 font-medium text-[#E8ECF4] flex items-center gap-2">
                      <span className="w-2.5 h-2.5 rounded-full inline-block" style={{ background: DONUT_COLORS[i % DONUT_COLORS.length] }} />
                      {g.pol}
                    </td>
                    <td className="px-3 py-2 text-right text-[#E8ECF4]">{fmtRsd(g.totalRevenue)}</td>
                    <td className="px-3 py-2 text-right text-[#4F8EF7] font-semibold">{fmtPct(g.revShare)}</td>
                    <td className="px-3 py-2 text-right text-[#8A95B0]">{fmtNum(g.totalUnits)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

// ====================== DAILY ANALYSIS TAB ======================

function DailyTab({
  data,
  loading,
  onDateChange,
  selectedDate,
}: {
  data: DailyAnalysis | null;
  loading: boolean;
  onDateChange: (d: string) => void;
  selectedDate: string;
}) {
  const zColorClass =
    !data ? "text-[#8A95B0]" :
    data.isExtremeOutlier ? "text-[#E05C5C]" :
    data.isOutlier ? "text-[#F5C542]" :
    "text-[#4CAF82]";

  return (
    <div className="space-y-5">
      {/* Date picker */}
      <div className="flex items-center gap-3 flex-wrap">
        <label className="text-sm text-[#8A95B0]">Analiziraj dan:</label>
        <input
          type="date"
          value={selectedDate}
          onChange={(e) => onDateChange(e.target.value)}
          className="rounded-lg border border-[#2A3045] bg-[#1E2332] px-3 py-1.5 text-sm text-[#E8ECF4] focus:border-[#4F8EF7] focus:outline-none"
        />
        {loading && <span className="text-xs text-[#8A95B0]">Učitavanje…</span>}
      </div>

      {data && (
        <>
          {/* Stats Row */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-3">
              <div className="text-[10px] uppercase text-[#8A95B0]">Prihod tog dana</div>
              <div className="text-lg font-bold text-[#E8ECF4]">{fmtRsd(data.targetRevenue)}</div>
            </div>
            <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-3">
              <div className="text-[10px] uppercase text-[#8A95B0]">Prosek perioda</div>
              <div className="text-lg font-bold text-[#E8ECF4]">{fmtRsd(data.meanRevenue)}</div>
            </div>
            <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-3">
              <div className="text-[10px] uppercase text-[#8A95B0]">Z-Score</div>
              <div className={`text-lg font-bold ${zColorClass}`}>{data.zScore.toFixed(2)}</div>
            </div>
            <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-3">
              <div className="text-[10px] uppercase text-[#8A95B0]">Outlier?</div>
              <div className={`text-sm font-bold ${zColorClass}`}>
                {data.isExtremeOutlier ? "⚡ Ekstremni" : data.isOutlier ? "⚠ Da" : "✓ Ne"}
              </div>
              <div className="text-[10px] text-[#8A95B0]">|Z| {'>'} {data.isExtremeOutlier ? "3.0" : data.isOutlier ? "2.0" : "2.0 (nije)"}</div>
            </div>
          </div>

          {/* Line Chart */}
          <div>
            <h3 className="mb-2 text-sm font-semibold text-[#c9d3e4]">Dnevna prodaja — 60 dana</h3>
            <div className="h-[200px]">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={data.dailyData}>
                  <CartesianGrid stroke="#2A3045" />
                  <XAxis dataKey="date" tick={{ fill: "#8A95B0", fontSize: 9 }} axisLine={false} tickLine={false}
                    interval={Math.floor(data.dailyData.length / 10)} />
                  <YAxis tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false}
                    tickFormatter={(v) => fmtRsd(v)} width={70} />
                  <Tooltip
                    contentStyle={{ background: "#1E2332", border: "1px solid #2A3045", borderRadius: 8, fontSize: 12 }}
                    formatter={(v: number | string | undefined) => [fmtRsd(Number(v ?? 0)), "Prihod"]}
                  />
                  <ReferenceLine y={data.meanRevenue} stroke="#F5C542" strokeDasharray="4 4" label={{ value: "Prosek", fill: "#F5C542", fontSize: 10 }} />
                  <Line
                    type="monotone"
                    dataKey="revenue"
                    stroke="#4F8EF7"
                    strokeWidth={1.5}
                    dot={(props) => {
                      if (props.payload.isTarget)
                        return <circle key={props.key} cx={props.cx} cy={props.cy} r={5} fill="#E05C5C" stroke="#E05C5C" />;
                      return <></>;
                    }}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>

          {/* Top 5 Articles */}
          {data.top5Articles.length > 0 && (
            <div>
              <h3 className="mb-2 text-sm font-semibold text-[#c9d3e4]">Top 5 artikala — {data.analysisDate}</h3>
              <div className="space-y-2">
                {data.top5Articles.map((a, i) => (
                  <div key={a.artikalId} className="flex items-center gap-3 rounded-lg bg-[#161A23] border border-[#2A3045] px-3 py-2">
                    <span className="text-[#4F8EF7] font-bold w-5 text-center">{i + 1}</span>
                    <div className="flex-1 min-w-0">
                      <div className="text-sm text-[#E8ECF4] truncate">{a.naziv}</div>
                      <div className="text-[10px] text-[#8A95B0]">{a.kategorija}</div>
                    </div>
                    <div className="text-right">
                      <div className="text-sm font-semibold text-[#4CAF82]">{fmtRsd(a.revenue)}</div>
                      <div className="text-[10px] text-[#8A95B0]">{a.units} kom</div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
      {!data && !loading && (
        <p className="text-[#8A95B0] text-sm">Izaberite dan za analizu.</p>
      )}
    </div>
  );
}

// ====================== ABC TAB ======================

function AbcTab({ data, loading }: { data: AbcItem[]; loading: boolean; summary?: object }) {
  const [showAll, setShowAll] = useState(false);

  if (loading) return <TableSkeleton rows={8} />;
  if (!data.length) return <p className="text-[#8A95B0] text-sm">Nema podataka za izabrani period.</p>;

  const revenueA = data.filter((x) => x.abcClass === "A").reduce((s, x) => s + x.totalRevenue, 0);
  const revenueB = data.filter((x) => x.abcClass === "B").reduce((s, x) => s + x.totalRevenue, 0);
  const revenueC = data.filter((x) => x.abcClass === "C").reduce((s, x) => s + x.totalRevenue, 0);
  const total = revenueA + revenueB + revenueC;

  const donutData = [
    { name: "Klasa A", value: revenueA, count: data.filter((x) => x.abcClass === "A").length },
    { name: "Klasa B", value: revenueB, count: data.filter((x) => x.abcClass === "B").length },
    { name: "Klasa C", value: revenueC, count: data.filter((x) => x.abcClass === "C").length },
  ];

  const displayed = showAll ? data : data.slice(0, 15);

  return (
    <div className="space-y-5">
      <div className="grid grid-cols-1 gap-5 lg:grid-cols-3">
        {/* Donut */}
        <div className="rounded-xl border border-[#2A3045] bg-[#161A23] p-4">
          <h3 className="mb-2 text-sm font-semibold text-[#c9d3e4]">ABC Distribucija</h3>
          <div className="h-[180px]">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={donutData}
                  dataKey="value"
                  nameKey="name"
                  cx="50%"
                  cy="50%"
                  outerRadius={75}
                  innerRadius={40}
                  paddingAngle={3}
                >
                  {donutData.map((_, i) => (
                    <Cell key={i} fill={["#4CAF82", "#F5C542", "#E05C5C"][i]} />
                  ))}
                </Pie>
                <Tooltip
                  contentStyle={{ background: "#1E2332", border: "1px solid #2A3045", borderRadius: 8, fontSize: 12 }}
                  formatter={(v: number | string | undefined) => [fmtRsd(Number(v ?? 0)), "Prihod"]}
                />
                <Legend formatter={(v) => <span style={{ color: "#8A95B0", fontSize: 11 }}>{v}</span>} />
              </PieChart>
            </ResponsiveContainer>
          </div>
          <div className="grid grid-cols-3 gap-2 mt-2">
            {donutData.map((d, i) => (
              <div key={i} className="text-center">
                <div className="text-xs font-bold" style={{ color: ["#4CAF82", "#F5C542", "#E05C5C"][i] }}>
                  {d.count} SKU
                </div>
                <div className="text-[10px] text-[#8A95B0]">
                  {total > 0 ? ((d.value / total) * 100).toFixed(0) : 0}% prih.
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Insight cards */}
        <div className="lg:col-span-2 space-y-3">
          <div className="rounded-xl border border-[#2A3045] bg-[#1E2332] px-4 py-3 text-sm text-[#8A95B0]">
            <span className="font-semibold text-[#4CAF82]">Klasa A</span> — top artikli koji čine ~70% prihoda.
            Ovi artikli zahtevaju stalnu zalihu i prioritet u nabavci.
          </div>
          <div className="rounded-xl border border-[#2A3045] bg-[#1E2332] px-4 py-3 text-sm text-[#8A95B0]">
            <span className="font-semibold text-[#F5C542]">Klasa B</span> — artikli između 70% i 90% kumulativnog prihoda.
            Pratiti trend i pravovremeno dopunjavati zalihe.
          </div>
          <div className="rounded-xl border border-[#2A3045] bg-[#1E2332] px-4 py-3 text-sm text-[#8A95B0]">
            <span className="font-semibold text-[#E05C5C]">Klasa C</span> — dugi rep sa niskim prihodom po artiklu.
            Razmotriti likvidaciju sporodajnih C artikala.
          </div>
        </div>
      </div>

      {/* Table */}
      <div>
        <h3 className="mb-2 text-sm font-semibold text-[#c9d3e4]">Svi artikli — ABC rang</h3>
        <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
                <th className="px-3 py-2 text-left">Artikal</th>
                <th className="px-3 py-2 text-left">Kat.</th>
                <th className="px-3 py-2 text-right">Prihod</th>
                <th className="px-3 py-2 text-right">Udeo%</th>
                <th className="px-3 py-2 text-right">Kum.%</th>
                <th className="px-3 py-2 text-right">Kom</th>
                <th className="px-3 py-2 text-center">Klasa</th>
              </tr>
            </thead>
            <tbody>
              {displayed.map((item) => (
                <tr key={item.artikalId} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                  <td className="px-3 py-2 text-[#E8ECF4] max-w-[160px] truncate">{item.naziv}</td>
                  <td className="px-3 py-2 text-[#8A95B0]">{item.kategorija}</td>
                  <td className="px-3 py-2 text-right text-[#E8ECF4]">{fmtRsd(item.totalRevenue)}</td>
                  <td className="px-3 py-2 text-right text-[#8A95B0]">{item.revPct.toFixed(1)}%</td>
                  <td className="px-3 py-2 text-right text-[#8A95B0]">{item.cumulativePct.toFixed(1)}%</td>
                  <td className="px-3 py-2 text-right text-[#8A95B0]">{item.totalUnits}</td>
                  <td className="px-3 py-2 text-center">
                    <span
                      className="rounded px-2 py-0.5 text-[10px] font-bold"
                      style={{ background: `${ABC_COLORS[item.abcClass]}22`, color: ABC_COLORS[item.abcClass] }}
                    >
                      {item.abcClass}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {data.length > 15 && (
          <button
            type="button"
            onClick={() => setShowAll((p) => !p)}
            className="mt-2 text-xs text-[#4F8EF7] hover:underline"
          >
            {showAll ? "Prikaži manje" : `Prikaži sve ${data.length} artikala →`}
          </button>
        )}
      </div>
    </div>
  );
}

// ====================== AGING STOCK TAB ======================

function AgingTab({
  items,
  loading,
  summary,
}: {
  items: AgingItem[];
  loading: boolean;
  summary?: { totalSKU: number; critical: number; warning: number; watch: number; active: number; criticalStockValue: number };
}) {
  const [filter, setFilter] = useState<string>("Sve");
  const [showAll, setShowAll] = useState(false);

  if (loading) return <TableSkeleton rows={8} />;
  if (!items.length) return <p className="text-[#8A95B0] text-sm">Nema zaliha za analizu.</p>;

  const categories = ["Sve", "Kritično", "Upozorenje", "Pazi", "Aktivno"];
  const filtered = filter === "Sve" ? items : items.filter((x) => x.agingCategory === filter);
  const displayed = showAll ? filtered : filtered.slice(0, 20);

  const barData = [
    { name: "Kritično (>90d)", value: summary?.critical ?? 0, fill: "#E05C5C" },
    { name: "Upozorenje (>60d)", value: summary?.warning ?? 0, fill: "#F97316" },
    { name: "Pazi (>30d)", value: summary?.watch ?? 0, fill: "#F5C542" },
    { name: "Aktivno (<30d)", value: summary?.active ?? 0, fill: "#4CAF82" },
  ];

  return (
    <div className="space-y-5">
      {/* Summary KPIs */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {barData.map((b) => (
          <div key={b.name} className="rounded-xl border border-[#2A3045] bg-[#161A23] p-3">
            <div className="text-[10px] text-[#8A95B0] uppercase mb-1">{b.name}</div>
            <div className="text-2xl font-bold" style={{ color: b.fill }}>{b.value}</div>
            <div className="text-[10px] text-[#8A95B0]">SKU</div>
          </div>
        ))}
      </div>

      {summary && summary.criticalStockValue > 0 && (
        <div className="rounded-xl border border-[#E05C5C]/30 bg-[#E05C5C]/10 px-4 py-3 text-sm text-[#E05C5C]">
          ⚠️ Vrednost kritičnih zaliha (&gt;90 dana bez prodaje): <strong>{fmtRsd(summary.criticalStockValue)}</strong>
        </div>
      )}

      {/* Bar chart */}
      <div className="h-[130px]">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={barData} barSize={36}>
            <CartesianGrid stroke="#2A3045" vertical={false} />
            <XAxis dataKey="name" tick={{ fill: "#8A95B0", fontSize: 9 }} axisLine={false} tickLine={false} />
            <YAxis tick={{ fill: "#8A95B0", fontSize: 10 }} axisLine={false} tickLine={false} />
            <Tooltip
              contentStyle={{ background: "#1E2332", border: "1px solid #2A3045", borderRadius: 8, fontSize: 12 }}
            />
            <Bar dataKey="value" radius={[4, 4, 0, 0]}>
              {barData.map((b, i) => (
                <Cell key={i} fill={b.fill} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>

      {/* Filter + Table */}
      <div>
        <div className="flex gap-2 flex-wrap mb-3">
          {categories.map((c) => (
            <button
              key={c}
              type="button"
              onClick={() => { setFilter(c); setShowAll(false); }}
              className={`rounded-lg px-3 py-1 text-xs font-medium transition ${
                filter === c ? "ring-1 ring-current" : "text-[#8A95B0] hover:text-[#c9d3e4]"
              }`}
              style={filter === c && c !== "Sve" ? { color: AGING_COLORS[c], background: `${AGING_COLORS[c]}18` } : undefined}
            >
              {c}
              {c !== "Sve" && (
                <span className="ml-1 opacity-60">
                  ({items.filter((x) => x.agingCategory === c).length})
                </span>
              )}
            </button>
          ))}
        </div>

        <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
                <th className="px-3 py-2 text-left">Artikal</th>
                <th className="px-3 py-2 text-left">Kat.</th>
                <th className="px-3 py-2 text-right">Zaliha</th>
                <th className="px-3 py-2 text-right">Poslednja prod.</th>
                <th className="px-3 py-2 text-right">Dana bez prod.</th>
                <th className="px-3 py-2 text-center">Status</th>
              </tr>
            </thead>
            <tbody>
              {displayed.map((item) => (
                <tr key={item.id} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                  <td className="px-3 py-2 text-[#E8ECF4] max-w-[160px] truncate">{item.naziv}</td>
                  <td className="px-3 py-2 text-[#8A95B0]">{item.kategorija}</td>
                  <td className="px-3 py-2 text-right text-[#E8ECF4]">{item.kolicina} kom</td>
                  <td className="px-3 py-2 text-right text-[#8A95B0]">{item.lastSaleDate}</td>
                  <td className="px-3 py-2 text-right font-semibold" style={{ color: AGING_COLORS[item.agingCategory] }}>
                    {item.daysWithoutSale}d
                  </td>
                  <td className="px-3 py-2 text-center">
                    <span
                      className="rounded px-2 py-0.5 text-[10px] font-semibold"
                      style={{
                        background: `${AGING_COLORS[item.agingCategory]}22`,
                        color: AGING_COLORS[item.agingCategory],
                      }}
                    >
                      {item.agingCategory}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {filtered.length > 20 && (
          <button
            type="button"
            onClick={() => setShowAll((p) => !p)}
            className="mt-2 text-xs text-[#4F8EF7] hover:underline"
          >
            {showAll ? "Prikaži manje" : `Prikaži sve ${filtered.length} →`}
          </button>
        )}
      </div>
    </div>
  );
}

// ====================== REORDER TAB ======================

function ReorderTab({
  items,
  loading,
  summary,
}: {
  items: ReorderItem[];
  loading: boolean;
  summary?: { criticalCount: number; urgentCount: number; recommendedCount: number; totalReorderValue: number };
}) {
  const [urgencyFilter, setUrgencyFilter] = useState<string>("Sve");
  const [showAll, setShowAll] = useState(false);

  if (loading) return <TableSkeleton rows={8} />;
  if (!items.length) return <p className="text-[#8A95B0] text-sm">Nema podataka za planiranje nabavke.</p>;

  const urgencies = ["Sve", "KRITIČNO", "HITNO", "PREPORUČUJE SE", "OK"];
  const filtered = urgencyFilter === "Sve" ? items : items.filter((x) => x.urgency === urgencyFilter);
  const filteredReorder = filtered.filter((x) => urgencyFilter !== "OK" ? true : x.needsReorder);
  const displayed = showAll ? filteredReorder : filteredReorder.slice(0, 15);

  return (
    <div className="space-y-5">
      {/* Summary */}
      {summary && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <div className="rounded-xl border border-[#E05C5C]/30 bg-[#E05C5C]/10 p-3">
            <div className="text-[10px] text-[#E05C5C] uppercase">Kritično</div>
            <div className="text-2xl font-bold text-[#E05C5C]">{summary.criticalCount}</div>
            <div className="text-[10px] text-[#8A95B0]">SKU (&lt;7 dana)</div>
          </div>
          <div className="rounded-xl border border-[#F97316]/30 bg-[#F97316]/10 p-3">
            <div className="text-[10px] text-[#F97316] uppercase">Hitno</div>
            <div className="text-2xl font-bold text-[#F97316]">{summary.urgentCount}</div>
            <div className="text-[10px] text-[#8A95B0]">SKU (&lt;14 dana)</div>
          </div>
          <div className="rounded-xl border border-[#F5C542]/30 bg-[#F5C542]/10 p-3">
            <div className="text-[10px] text-[#F5C542] uppercase">Preporučuje se</div>
            <div className="text-2xl font-bold text-[#F5C542]">{summary.recommendedCount}</div>
            <div className="text-[10px] text-[#8A95B0]">SKU (&lt;30 dana)</div>
          </div>
          <div className="rounded-xl border border-[#4F8EF7]/30 bg-[#4F8EF7]/10 p-3">
            <div className="text-[10px] text-[#4F8EF7] uppercase">Vrednost nabavke</div>
            <div className="text-lg font-bold text-[#4F8EF7]">{fmtRsd(summary.totalReorderValue)}</div>
            <div className="text-[10px] text-[#8A95B0]">Procenjeno</div>
          </div>
        </div>
      )}

      {/* Filter */}
      <div className="flex gap-2 flex-wrap">
        {urgencies.map((u) => (
          <button
            key={u}
            type="button"
            onClick={() => { setUrgencyFilter(u); setShowAll(false); }}
            className={`rounded-lg px-3 py-1 text-xs font-medium transition ${
              urgencyFilter === u
                ? "ring-1 ring-current"
                : "text-[#8A95B0] hover:text-[#c9d3e4]"
            }`}
            style={
              urgencyFilter === u && u !== "Sve"
                ? { color: URGENCY_COLORS[u], background: `${URGENCY_COLORS[u]}18` }
                : undefined
            }
          >
            {u}
          </button>
        ))}
      </div>

      {/* Table */}
      <div className="overflow-x-auto rounded-xl border border-[#2A3045]">
        <table className="w-full text-xs">
          <thead>
            <tr className="border-b border-[#2A3045] bg-[#1E2332] text-[10px] uppercase tracking-wider text-[#8A95B0]">
              <th className="px-3 py-2 text-left">Artikal</th>
              <th className="px-3 py-2 text-left">Kat.</th>
              <th className="px-3 py-2 text-left">Dobavljač</th>
              <th className="px-3 py-2 text-right">Zaliha</th>
              <th className="px-3 py-2 text-right">Vel/dan</th>
              <th className="px-3 py-2 text-right">DOH</th>
              <th className="px-3 py-2 text-right">Preporučena kol.</th>
              <th className="px-3 py-2 text-center">Hitnost</th>
            </tr>
          </thead>
          <tbody>
            {displayed.map((item) => (
              <tr key={item.artikalId} className="border-b border-[#2A3045] hover:bg-[#1E2332] transition">
                <td className="px-3 py-2 text-[#E8ECF4] max-w-[140px] truncate">{item.naziv}</td>
                <td className="px-3 py-2 text-[#8A95B0]">{item.kategorija}</td>
                <td className="px-3 py-2 text-[#8A95B0]">{item.dobavljacNaziv}</td>
                <td className="px-3 py-2 text-right text-[#E8ECF4]">{item.currentStock}</td>
                <td className="px-3 py-2 text-right text-[#8A95B0]">{item.avgDailySales.toFixed(2)}</td>
                <td
                  className="px-3 py-2 text-right font-semibold"
                  style={{ color: item.doh < 7 ? "#E05C5C" : item.doh < 14 ? "#F97316" : item.doh < 30 ? "#F5C542" : "#4CAF82" }}
                >
                  {item.doh > 900 ? "∞" : `${item.doh.toFixed(0)}d`}
                </td>
                <td className="px-3 py-2 text-right font-bold" style={{ color: item.needsReorder ? "#F5C542" : "#4A5270" }}>
                  {item.needsReorder ? `+${item.recommendedQty}` : "—"}
                </td>
                <td className="px-3 py-2 text-center">
                  <span
                    className="rounded px-2 py-0.5 text-[10px] font-semibold"
                    style={{
                      background: `${URGENCY_COLORS[item.urgency]}22`,
                      color: URGENCY_COLORS[item.urgency],
                    }}
                  >
                    {item.urgency}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {filteredReorder.length > 15 && (
        <button
          type="button"
          onClick={() => setShowAll((p) => !p)}
          className="mt-1 text-xs text-[#4F8EF7] hover:underline"
        >
          {showAll ? "Prikaži manje" : `Prikaži sve ${filteredReorder.length} →`}
        </button>
      )}
    </div>
  );
}

// ====================== MAIN PAGE ======================

export default function InsightStudioPage() {
  const [activeTab, setActiveTab] = useState<TabKey>("dobavljaci");
  const [periodDays, setPeriodDays] = useState(30);

  // Computed date range
  const toDate = toDateStr(new Date());
  const fromDate = toDateStr(daysAgo(periodDays));

  // KPI
  const [kpi, setKpi] = useState<KpiSnapshot | null>(null);
  const [kpiLoading, setKpiLoading] = useState(false);
  const [kpiError, setKpiError] = useState<string | null>(null);

  // Suppliers
  const [suppliers, setSuppliers] = useState<SupplierScore[]>([]);
  const [supplierLoading, setSupplierLoading] = useState(false);

  // Categories
  const [catData, setCatData] = useState<{ byCategory: CategoryStat[]; byGender: GenderStat[] } | null>(null);
  const [catLoading, setCatLoading] = useState(false);

  // Daily
  const [dailyDate, setDailyDate] = useState(toDateStr(daysAgo(1)));
  const [daily, setDaily] = useState<DailyAnalysis | null>(null);
  const [dailyLoading, setDailyLoading] = useState(false);

  // ABC
  const [abcData, setAbcData] = useState<AbcItem[]>([]);
  const [abcSummary, setAbcSummary] = useState<object | undefined>(undefined);
  const [abcLoading, setAbcLoading] = useState(false);

  // Aging
  const [agingItems, setAgingItems] = useState<AgingItem[]>([]);
  const [agingSummary, setAgingSummary] = useState<{ totalSKU: number; critical: number; warning: number; watch: number; active: number; criticalStockValue: number } | undefined>(undefined);
  const [agingLoading, setAgingLoading] = useState(false);

  // Reorder
  const [reorderItems, setReorderItems] = useState<ReorderItem[]>([]);
  const [reorderSummary, setReorderSummary] = useState<{ criticalCount: number; urgentCount: number; recommendedCount: number; totalReorderValue: number } | undefined>(undefined);
  const [reorderLoading, setReorderLoading] = useState(false);

  // Load KPI on period change
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

  useEffect(() => {
    loadKpi();
  }, [loadKpi]);

  // Load tab data on first activation
  const loadedTabs = useState<Set<TabKey>>(new Set<TabKey>())[0];

  const loadTabData = useCallback(async (tab: TabKey) => {
    if (loadedTabs.has(tab)) return;
    loadedTabs.add(tab);

    if (tab === "dobavljaci") {
      setSupplierLoading(true);
      try {
        const d = await getSupplierScorecard(fromDate, toDate);
        setSuppliers(d);
      } catch { /* silencieux */ }
      finally { setSupplierLoading(false); }
    }
    if (tab === "kategorije") {
      setCatLoading(true);
      try {
        const d = await getCategoryIntelligence(fromDate, toDate);
        setCatData(d);
      } catch { /* silencieux */ }
      finally { setCatLoading(false); }
    }
    if (tab === "dnevna") {
      setDailyLoading(true);
      try {
        const d = await getDailyAnalysis(dailyDate, toDateStr(daysAgo(60)), toDate);
        setDaily(d);
      } catch { /* silencieux */ }
      finally { setDailyLoading(false); }
    }
    if (tab === "abc") {
      setAbcLoading(true);
      try {
        const d = await getAbcClassification(fromDate, toDate);
        setAbcData(d.items);
        setAbcSummary(d.summary);
      } catch { /* silencieux */ }
      finally { setAbcLoading(false); }
    }
    if (tab === "zalihe") {
      setAgingLoading(true);
      try {
        const d = await getAgingStock();
        setAgingItems(d.items);
        setAgingSummary(d.summary);
      } catch { /* silencieux */ }
      finally { setAgingLoading(false); }
    }
    if (tab === "planiranje") {
      setReorderLoading(true);
      try {
        const d = await getReorderPlan(fromDate, toDate);
        setReorderItems(d.items);
        setReorderSummary(d.summary);
      } catch { /* silencieux */ }
      finally { setReorderLoading(false); }
    }
  }, [fromDate, toDate, dailyDate]);

  // Load initial tab
  useEffect(() => {
    loadTabData("dobavljaci");
  }, [loadTabData]);

  const handleTabChange = (tab: TabKey) => {
    setActiveTab(tab);
    loadTabData(tab);
  };

  // Handle daily date change
  const handleDailyDateChange = async (d: string) => {
    setDailyDate(d);
    setDailyLoading(true);
    try {
      const result = await getDailyAnalysis(d, toDateStr(daysAgo(60)), toDate);
      setDaily(result);
    } catch { /* silencieux */ }
    finally { setDailyLoading(false); }
  };

  return (
    <div className="space-y-5 pb-10">
      {/* Page Header */}
      <div className="flex flex-col gap-1">
        <div className="flex items-center gap-2">
          <span className="text-[#4F8EF7] text-xl">🔬</span>
          <h1 className="text-xl font-bold text-white">Trendplus Insight Studio</h1>
          <span className="rounded bg-[#4F8EF7]/20 px-2 py-0.5 text-[10px] font-semibold text-[#4F8EF7] uppercase tracking-wider">
            Analitika 2
          </span>
        </div>
        <p className="text-xs text-[#8A95B0]">
          Napredna analiza profitabilnosti, dobavljača, zaliha i planiranja nabavke
        </p>
      </div>

      {/* Period Filter Strip */}
      <div className="flex items-center gap-2 flex-wrap rounded-xl border border-[#2A3045] bg-[#161A23] px-4 py-2.5">
        <span className="text-xs text-[#8A95B0] mr-1">Period:</span>
        {PERIOD_PRESETS.map((p) => (
          <button
            key={p.days}
            type="button"
            onClick={() => { setPeriodDays(p.days); loadedTabs.clear(); }}
            className={`rounded-lg px-3 py-1 text-xs font-medium transition ${
              periodDays === p.days
                ? "bg-[#1f2940] text-[#7ea5ff] ring-1 ring-[#32579e]"
                : "text-[#8A95B0] hover:text-[#c9d3e4] hover:bg-[#20222a]"
            }`}
          >
            {p.label}
          </button>
        ))}
        <span className="ml-auto text-[11px] text-[#4A5270]">
          {fromDate} → {toDate}
        </span>
      </div>

      {/* KPI Command Row */}
      {kpiError ? (
        <div className="rounded-xl border border-[#E05C5C]/30 bg-[#E05C5C]/10 px-4 py-3 text-sm text-[#E05C5C]">
          Greška pri učitavanju KPI: {kpiError}
        </div>
      ) : kpiLoading || !kpi ? (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          {Array.from({ length: 5 }).map((_, i) => <CardSkeleton key={i} />)}
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          <KpiCard
            label="Ukupan prihod"
            value={fmtRsd(kpi.revenue)}
            sub={`vs. preth. period`}
            change={kpi.revenueChange}
            accent="#4F8EF7"
            sparkline={kpi.sparkline}
            icon="💰"
          />
          <KpiCard
            label="Bruto marža"
            value={fmtPct(kpi.marginPct)}
            sub="Procenjena marža"
            accent="#4CAF82"
            icon="📈"
          />
          <KpiCard
            label="Prodato kom."
            value={fmtNum(kpi.units)}
            sub="vs. preth. period"
            change={kpi.unitsChange}
            accent="#9B72CF"
            icon="👟"
          />
          <KpiCard
            label="Transakcije"
            value={fmtNum(kpi.transactions)}
            sub={`Avg. ${fmtRsd(kpi.transactions > 0 ? kpi.revenue / kpi.transactions : 0)}/tr`}
            accent="#F5C542"
            icon="🧾"
          />
          <KpiCard
            label="OOS / Malo"
            value={`${kpi.oosCount} / ${kpi.lowStockCount}`}
            sub="SKU bez zaliha / ispod min"
            accent={kpi.oosCount > 10 ? "#E05C5C" : "#F97316"}
            icon="⚠️"
          />
        </div>
      )}

      {/* Tab Navigation */}
      <div className="flex gap-1 flex-wrap rounded-xl border border-[#2A3045] bg-[#161A23] p-1.5">
        {TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            onClick={() => handleTabChange(t.key)}
            className={`flex items-center gap-1.5 rounded-lg px-3 py-2 text-xs font-medium transition ${
              activeTab === t.key
                ? "bg-[#1f2940] text-[#d8e5ff] ring-1 ring-[#32579e]"
                : "text-[#8A95B0] hover:text-[#c9d3e4] hover:bg-[#20222a]"
            }`}
          >
            <span>{t.icon}</span>
            <span>{t.label}</span>
          </button>
        ))}
      </div>

      {/* Tab Content */}
      <div className="rounded-xl border border-[#2A3045] bg-[#0D0F14] p-5">
        {activeTab === "dobavljaci" && (
          <SupplierTab data={suppliers} loading={supplierLoading} />
        )}
        {activeTab === "kategorije" && (
          <CategoryTab
            byCategory={catData?.byCategory ?? []}
            byGender={catData?.byGender ?? []}
            loading={catLoading}
          />
        )}
        {activeTab === "dnevna" && (
          <DailyTab
            data={daily}
            loading={dailyLoading}
            onDateChange={handleDailyDateChange}
            selectedDate={dailyDate}
          />
        )}
        {activeTab === "abc" && (
          <AbcTab data={abcData} loading={abcLoading} summary={abcSummary} />
        )}
        {activeTab === "zalihe" && (
          <AgingTab items={agingItems} loading={agingLoading} summary={agingSummary} />
        )}
        {activeTab === "planiranje" && (
          <ReorderTab items={reorderItems} loading={reorderLoading} summary={reorderSummary} />
        )}
      </div>
    </div>
  );
}
