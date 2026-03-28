import { useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { AlertTriangle, Lightbulb, Siren, Sparkles, ChevronLeft, ChevronRight } from "lucide-react";
import {
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Scatter,
  ScatterChart,
  Tooltip,
  XAxis,
  YAxis,
  ZAxis,
} from "recharts";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel, InventoryState } from "../components/inventory/InventoryPageShell";
import { getPreNivelacijaPrioriteti, type PreNivelacijaQuery } from "../services/preNivelacijaApi";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import type { PreNivelacijaPriorityResponse, PreNivelacijaQueueItem } from "../types/preNivelacija";

type Tab = "candidates" | "suppliers" | "simulator" | "alerts";
type QueueStatus = "Unassigned" | "Assigned" | "Done";

function fmtRsd(value: number) {
  return `${new Intl.NumberFormat("sr-RS", { maximumFractionDigits: 0 }).format(value)} RSD`;
}

function fmtPct(value: number, digits = 1) {
  return `${value.toFixed(digits)}%`;
}

function priorityColor(band: string) {
  if (band === "high") return "var(--c-f87171, #f87171)";
  if (band === "medium") return "var(--c-fbbf24, #fbbf24)";
  return "var(--c-60a5fa, #60a5fa)";
}

function queueKey(item: PreNivelacijaQueueItem) {
  return `${item.artikalId}`;
}

const candidateColumns: AnalyticsTableColumn<PreNivelacijaPriorityResponse["candidates"][number]>[] = [
  { key: "sku", header: "SKU", dataType: "text" },
  { key: "supplierName", header: "Dobavljac", dataType: "text" },
  { key: "stockUnits", header: "Stock", dataType: "number" },
  { key: "velocity180", header: "Velocity180", dataType: "number" },
  { key: "daysSinceLastSale", header: "No sale days", dataType: "number" },
  { key: "grossMarginPctEst", header: "Margin %", dataType: "percent" },
  { key: "preNivelacijaScore", header: "Score", dataType: "number" },
];

const supplierColumns: AnalyticsTableColumn<PreNivelacijaPriorityResponse["supplierLeaderboard"][number]>[] = [
  { key: "supplierName", header: "Dobavljac", dataType: "text" },
  { key: "actionScore", header: "Action score", dataType: "number" },
  { key: "highPrioritySkuCount", header: "High SKU", dataType: "number" },
  { key: "stockUnitsAtRisk", header: "Stock risk", dataType: "number" },
  { key: "estimatedAvoidableMarkdownLoss", header: "Avoidable loss", dataType: "currency" },
  { key: "weekOverWeekRiskDeltaPct", header: "WoW risk", dataType: "percent" },
];

const simulatorColumns: AnalyticsTableColumn<PreNivelacijaPriorityResponse["candidates"][number]>[] = [
  { key: "sku", header: "SKU", dataType: "text" },
  { key: "scenarioHighlightNowRevenue", header: "Highlight rev", dataType: "currency", getValue: (row) => row.scenarioHighlightNow.expectedRevenue30d },
  { key: "scenarioMarkdownNowRevenue", header: "Markdown rev", dataType: "currency", getValue: (row) => row.scenarioMarkdownNow.expectedRevenue30d },
  { key: "revenueDeltaHighlightVsMarkdown", header: "Delta rev", dataType: "currency" },
  { key: "marginDeltaHighlightVsMarkdown", header: "Delta margin", dataType: "currency" },
  { key: "confidence", header: "Confidence", dataType: "text" },
];

export default function PreNivelacijaPriorityPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [tab, setTab] = useState<Tab>("candidates");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [data, setData] = useState<PreNivelacijaPriorityResponse | null>(null);
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<Omit<PreNivelacijaQuery, "page" | "pageSize">>({
    stockMin: 1,
    noSaleDaysMin: 14,
    minScore: 40,
  });
  const [queueState, setQueueState] = useState<Record<string, QueueStatus>>({});

  useEffect(() => {
    let aborted = false;
    const load = async () => {
      setLoading(true);
      setError(null);
      try {
        const result = await getPreNivelacijaPrioriteti({
          ...filters,
          page,
          pageSize: 20,
        });
        if (!aborted) setData(result);
      } catch (e: unknown) {
        if (!aborted) setError(e instanceof Error ? e.message : "Greska pri ucitavanju");
      } finally {
        if (!aborted) setLoading(false);
      }
    };
    void load();
    return () => {
      aborted = true;
    };
  }, [filters, page]);

  const supplierOptions = useMemo(
    () => (data?.supplierLeaderboard ?? []).filter((x) => x.supplierId != null),
    [data]
  );

  const seasonOptions = useMemo(() => {
    const map = new Map<number, string>();
    (data?.candidates ?? []).forEach((x) => {
      if (x.seasonId != null && x.season && x.season !== "N/A") {
        map.set(x.seasonId, x.season);
      }
    });
    return [...map.entries()]
      .map(([id, label]) => ({ id, label }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [data]);

  const footwearTypeOptions = useMemo(() => {
    const map = new Map<number, string>();
    (data?.candidates ?? []).forEach((x) => {
      if (x.footwearTypeId != null && x.footwearType && x.footwearType !== "N/A") {
        map.set(x.footwearTypeId, x.footwearType);
      }
    });
    return [...map.entries()]
      .map(([id, label]) => ({ id, label }))
      .sort((a, b) => a.label.localeCompare(b.label));
  }, [data]);

  const scatterData = useMemo(
    () =>
      (data?.candidates ?? []).map((x) => ({
        x: x.stockUnits,
        y: Number(x.velocity180),
        z: Math.max(1, Number(x.scenarioHighlightNow?.expectedRevenue30d ?? 0) / 1000),
        label: x.sku,
        band: x.priorityBand,
      })),
    [data]
  );

  const queueSections = useMemo(() => {
    if (!data) return [];
    const queues = data.queues ?? { highlightNow: [], monitor: [], likelyMarkdownSoon: [] };
    return [
      { label: "Highlight now", items: queues.highlightNow ?? [] },
      { label: "Monitor", items: queues.monitor ?? [] },
      { label: "Likely markdown soon", items: queues.likelyMarkdownSoon ?? [] },
    ];
  }, [data]);

  const sharedFilters = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "supplierId", label: "Dobavljac", value: filters.supplierId ?? "" },
    { key: "seasonId", label: "Sezona", value: filters.seasonId ?? "" },
    { key: "footwearTypeId", label: "Tip obuce", value: filters.footwearTypeId ?? "" },
    { key: "stockMin", label: "Stock min", value: filters.stockMin ?? "" },
    { key: "noSaleDaysMin", label: "No-sale days min", value: filters.noSaleDaysMin ?? "" },
    { key: "minScore", label: "Min score", value: filters.minScore ?? "" },
  ], [filters]);

  const sharedMetadata = useMemo<AnalyticsNamedValue[]>(() => [
    { key: "generatedAtUtc", label: "Generisano", value: data?.generatedAtUtc ?? "" },
    { key: "formulaVersion", label: "Formula", value: data?.formulaVersion ?? "" },
  ], [data?.formulaVersion, data?.generatedAtUtc]);

  const openSnapshotDetail = <Row,>(
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
        metadata: sharedFilters,
      })
    );

    navigate(`/analitika/${table}/${encodeURIComponent(recordId)}`, {
      state: { backgroundLocation: location },
    });
  };

  return (
    <InventoryPageShell
      icon={Sparkles}
      title="Pre-Nivelacija Prioriteti Dobavljaca"
      subtitle="Operativna analitika koja otkriva koje modele treba isticati pre markdown-a."
      actions={
        <button
          type="button"
          onClick={() => setPage(1)}
          className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-xs font-semibold text-white"
        >
          Osvezi
        </button>
      }
    >
      <InventoryKpiRow
        items={[
          { label: "Dobavljaci", value: `${data?.summary.supplierCount ?? 0}` },
          { label: "Kandidati", value: `${data?.summary.candidatesCount ?? 0}` },
          { label: "High priority", value: `${data?.summary.highPriorityCount ?? 0}`, tone: "warning" },
          { label: "Stock at risk", value: `${data?.summary.totalStockAtRisk ?? 0}` },
          { label: "Avoidable loss", value: fmtRsd(data?.summary.estimatedAvoidableMarkdownLoss ?? 0), tone: "danger" },
          { label: "Expected uplift", value: fmtRsd(data?.summary.expectedHighlightRevenueUplift ?? 0), tone: "positive" },
        ]}
      />

      <InventoryPanel>
        <div className="mb-4 grid gap-3 md:grid-cols-2 xl:grid-cols-6">
          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-primary)]">Dobavljac</label>
            <select
              className="w-full rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2 py-2 text-sm text-[var(--text-primary)]"
              value={filters.supplierId ?? ""}
              onChange={(e) =>
                setFilters((f) => ({
                  ...f,
                  supplierId: e.target.value ? Number(e.target.value) : undefined,
                }))
              }
            >
              <option value="">Svi</option>
              {supplierOptions.map((s, idx) => (
                <option key={`${s.supplierId}`} value={s.supplierId ?? ""}>
                  {s.supplierName}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-primary)]">Sezona</label>
            <select
              className="w-full rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2 py-2 text-sm text-[var(--text-primary)]"
              value={filters.seasonId ?? ""}
              onChange={(e) =>
                setFilters((f) => ({
                  ...f,
                  seasonId: e.target.value ? Number(e.target.value) : undefined,
                }))
              }
            >
              <option value="">Sve</option>
              {seasonOptions.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-primary)]">Tip obuce</label>
            <select
              className="w-full rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2 py-2 text-sm text-[var(--text-primary)]"
              value={filters.footwearTypeId ?? ""}
              onChange={(e) =>
                setFilters((f) => ({
                  ...f,
                  footwearTypeId: e.target.value ? Number(e.target.value) : undefined,
                }))
              }
            >
              <option value="">Svi</option>
              {footwearTypeOptions.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-primary)]">Stock min</label>
            <input
              type="number"
              value={filters.stockMin ?? 1}
              onChange={(e) => setFilters((f) => ({ ...f, stockMin: Number(e.target.value) }))}
              className="w-full rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2 py-2 text-sm text-[var(--text-primary)]"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-primary)]">No-sale days min</label>
            <input
              type="number"
              value={filters.noSaleDaysMin ?? 14}
              onChange={(e) => setFilters((f) => ({ ...f, noSaleDaysMin: Number(e.target.value) }))}
              className="w-full rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2 py-2 text-sm text-[var(--text-primary)]"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-primary)]">Min score</label>
            <input
              type="number"
              value={filters.minScore ?? 40}
              onChange={(e) => setFilters((f) => ({ ...f, minScore: Number(e.target.value) }))}
              className="w-full rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2 py-2 text-sm text-[var(--text-primary)]"
            />
          </div>
        </div>

        <div className="mb-4 flex gap-2">
          {[
            { key: "candidates", label: "Pre-Nivelacija kandidati", icon: Lightbulb },
            { key: "suppliers", label: "Supplier action board", icon: Sparkles },
            { key: "simulator", label: "Scenario simulator", icon: Siren },
            { key: "alerts", label: "Alerts & anomalies", icon: AlertTriangle },
          ].map((x) => (
            <button
              key={x.key}
              type="button"
              onClick={() => setTab(x.key as Tab)}
              className={`rounded-lg border px-3 py-2 text-xs font-semibold ${
                tab === x.key
                  ? "border-[var(--border-default)] bg-[var(--surface-elevated)] text-white"
                  : "border-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]"
              }`}
            >
              {x.label}
            </button>
          ))}
        </div>

        {loading && <InventoryState message="Ucitavanje pre-nivelacija analitike..." tone="warning" />}
        {!loading && error && <InventoryState message={error} tone="danger" />}
        {!loading && !error && !data && <InventoryState message="Nema podataka." />}

        {!loading && !error && data && tab === "candidates" && (
          <div className="space-y-4">
            <div className="flex justify-end">
              <AnalyticsTableToolbar
                tableKey="pre-nivelacija-candidates"
                tableTitle="Pre-nivelacija kandidati"
                columns={candidateColumns}
                rows={data.candidates}
                filters={sharedFilters}
                metadata={sharedMetadata}
                defaultOrientation="landscape"
              />
            </div>
            <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
              <table className="min-w-full divide-y divide-[var(--border-default)] text-sm">
                <thead className="bg-[var(--surface-elevated)] text-[var(--text-primary)]">
                  <tr>
                    <th className="px-3 py-2 text-left">SKU</th>
                    <th className="px-3 py-2 text-left">Dobavljac</th>
                    <th className="px-3 py-2 text-right">Stock</th>
                    <th className="px-3 py-2 text-right">Velocity180</th>
                    <th className="px-3 py-2 text-right">No sale days</th>
                    <th className="px-3 py-2 text-right">Margin %</th>
                    <th className="px-3 py-2 text-right">Score</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]">
                  {data.candidates.map((row) => (
                    <tr
                      key={row.artikalId}
                      className={`cursor-pointer hover:brightness-110 ${
                      row.priorityBand === "high" ? "bg-rose-950/20" :
                      row.priorityBand === "medium" ? "bg-amber-950/15" :
                      "hover:bg-[var(--surface-light)]"
                    }`}
                      onClick={() => openSnapshotDetail("pre-nivelacija-candidates", String(row.artikalId), row.sku, row.supplierName, candidateColumns, row)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          openSnapshotDetail("pre-nivelacija-candidates", String(row.artikalId), row.sku, row.supplierName, candidateColumns, row);
                        }
                      }}
                      tabIndex={0}
                    >
                      <td className="px-3 py-2">{row.sku}</td>
                      <td className="px-3 py-2">{row.supplierName}</td>
                      <td className="px-3 py-2 text-right">{row.stockUnits}</td>
                      <td className="px-3 py-2 text-right">{row.velocity180.toFixed(3)}</td>
                      <td className="px-3 py-2 text-right">{row.daysSinceLastSale}</td>
                      <td className="px-3 py-2 text-right">{fmtPct(row.grossMarginPctEst)}</td>
                      <td className="px-3 py-2 text-right font-semibold" style={{ color: priorityColor(row.priorityBand) }}>
                        {row.preNivelacijaScore.toFixed(1)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="flex items-center justify-between">
              <p className="text-xs text-[var(--text-primary)]">
                Formula: {data.formulaDescription}
              </p>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={data.page <= 1}
                  className="rounded-md border border-[var(--border-default)] bg-[var(--surface-elevated)] p-1.5 text-[var(--text-primary)] disabled:opacity-50"
                  title="Prethodna"
                >
                  <ChevronLeft size={14} />
                </button>
                <span className="text-xs text-[var(--text-primary)]">
                  {data.page} / {Math.max(1, Math.ceil(data.totalCandidates / data.pageSize))}
                </span>
                <button
                  type="button"
                  onClick={() =>
                    setPage((p) => {
                      const max = Math.max(1, Math.ceil(data.totalCandidates / data.pageSize));
                      return Math.min(max, p + 1);
                    })
                  }
                  disabled={data.page >= Math.max(1, Math.ceil(data.totalCandidates / data.pageSize))}
                  className="rounded-md border border-[var(--border-default)] bg-[var(--surface-elevated)] p-1.5 text-[var(--text-primary)] disabled:opacity-50"
                  title="Sledeca"
                >
                  <ChevronRight size={14} />
                </button>
              </div>
            </div>
          </div>
        )}

        {!loading && !error && data && tab === "suppliers" && (
          <div className="space-y-4">
            <div className="flex justify-end">
              <AnalyticsTableToolbar
                tableKey="pre-nivelacija-suppliers"
                tableTitle="Pre-nivelacija supplier action board"
                columns={supplierColumns}
                rows={data.supplierLeaderboard}
                filters={sharedFilters}
                metadata={sharedMetadata}
                defaultOrientation="landscape"
              />
            </div>
            <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
              <table className="min-w-full divide-y divide-[var(--border-default)] text-sm">
                <thead className="bg-[var(--surface-elevated)] text-[var(--text-primary)]">
                  <tr>
                    <th className="px-3 py-2 text-left">Dobavljac</th>
                    <th className="px-3 py-2 text-right">Action score</th>
                    <th className="px-3 py-2 text-right">High SKU</th>
                    <th className="px-3 py-2 text-right">Stock risk</th>
                    <th className="px-3 py-2 text-right">Avoidable loss</th>
                    <th className="px-3 py-2 text-right">WoW risk</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]">
                  {data.supplierLeaderboard.map((s, idx) => (
                    <tr
                      key={`${s.supplierName}-${idx}`}
                      className="cursor-pointer hover:bg-[var(--surface-light)]"
                      onClick={() => openSnapshotDetail("pre-nivelacija-suppliers", String(s.supplierId ?? idx), s.supplierName, "Supplier action board", supplierColumns, s)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          openSnapshotDetail("pre-nivelacija-suppliers", String(s.supplierId ?? idx), s.supplierName, "Supplier action board", supplierColumns, s);
                        }
                      }}
                      tabIndex={0}
                    >
                      <td className="px-3 py-2">{s.supplierName}</td>
                      <td className="px-3 py-2 text-right font-semibold">{s.actionScore.toFixed(1)}</td>
                      <td className="px-3 py-2 text-right">{s.highPrioritySkuCount}</td>
                      <td className="px-3 py-2 text-right">{s.stockUnitsAtRisk}</td>
                      <td className="px-3 py-2 text-right">{fmtRsd(s.estimatedAvoidableMarkdownLoss)}</td>
                      <td className={`px-3 py-2 text-right ${s.weekOverWeekRiskDeltaPct > 20 ? "text-rose-300" : "text-[var(--text-primary)]"}`}>
                        {fmtPct(s.weekOverWeekRiskDeltaPct)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="h-[320px] rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-3">
              <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={220}>
                <ScatterChart>
                  <CartesianGrid stroke="var(--c-2f323b, #2f323b)" />
                  <XAxis dataKey="x" name="Stock" tick={{ fill: "var(--c-9aabc7, #9aabc7)", fontSize: 11 }} />
                  <YAxis dataKey="y" name="Velocity" tick={{ fill: "var(--c-9aabc7, #9aabc7)", fontSize: 11 }} />
                  <ZAxis dataKey="z" range={[40, 260]} name="Revenue" />
                  <Tooltip cursor={{ strokeDasharray: "4 4" }} />
                  <Legend />
                  <Scatter data={scatterData} name="SKU signal" fill="var(--c-4f8cff, #4f8cff)" />
                </ScatterChart>
              </ResponsiveContainer>
            </div>
          </div>
        )}

        {!loading && !error && data && tab === "simulator" && (
          <div className="space-y-4">
            <div className="flex justify-end">
              <AnalyticsTableToolbar
                tableKey="pre-nivelacija-simulator"
                tableTitle="Pre-nivelacija scenario simulator"
                columns={simulatorColumns}
                rows={data.candidates.slice(0, 20)}
                filters={sharedFilters}
                metadata={sharedMetadata}
                defaultOrientation="landscape"
              />
            </div>
            <div className="overflow-x-auto rounded-xl border border-[var(--border-default)]">
              <table className="min-w-full divide-y divide-[var(--border-default)] text-sm">
                <thead className="bg-[var(--surface-elevated)] text-[var(--text-primary)]">
                  <tr>
                    <th className="px-3 py-2 text-left">SKU</th>
                    <th className="px-3 py-2 text-right">Highlight rev</th>
                    <th className="px-3 py-2 text-right">Markdown rev</th>
                    <th className="px-3 py-2 text-right">Delta rev</th>
                    <th className="px-3 py-2 text-right">Delta margin</th>
                    <th className="px-3 py-2 text-right">Confidence</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[var(--border-default)] bg-[var(--surface-elevated)] text-[var(--text-primary)]">
                  {data.candidates.slice(0, 20).map((row) => (
                    <tr
                      key={row.artikalId}
                      className="cursor-pointer hover:bg-[var(--surface-light)]"
                      onClick={() => openSnapshotDetail("pre-nivelacija-simulator", String(row.artikalId), row.sku, "Scenario simulator", simulatorColumns, row)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          openSnapshotDetail("pre-nivelacija-simulator", String(row.artikalId), row.sku, "Scenario simulator", simulatorColumns, row);
                        }
                      }}
                      tabIndex={0}
                    >
                      <td className="px-3 py-2">{row.sku}</td>
                      <td className="px-3 py-2 text-right">{fmtRsd(row.scenarioHighlightNow.expectedRevenue30d)}</td>
                      <td className="px-3 py-2 text-right">{fmtRsd(row.scenarioMarkdownNow.expectedRevenue30d)}</td>
                      <td className={`px-3 py-2 text-right ${row.revenueDeltaHighlightVsMarkdown >= 0 ? "text-emerald-300" : "text-rose-300"}`}>
                        {fmtRsd(row.revenueDeltaHighlightVsMarkdown)}
                      </td>
                      <td className={`px-3 py-2 text-right ${row.marginDeltaHighlightVsMarkdown >= 0 ? "text-emerald-300" : "text-rose-300"}`}>
                        {fmtRsd(row.marginDeltaHighlightVsMarkdown)}
                      </td>
                      <td className="px-3 py-2 text-right">{row.confidence}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="grid gap-3 xl:grid-cols-3">
              {queueSections.map((section) => (
                <div key={section.label} className="rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-3">
                  <h3 className="mb-2 text-sm font-semibold text-[var(--text-primary)]">{section.label}</h3>
                  <div className="space-y-2">
                    {section.items.slice(0, 8).map((item) => {
                      const key = queueKey(item);
                      const state = queueState[key] ?? (item.status as QueueStatus);
                      return (
                        <div key={key} className="rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] p-2">
                          <div className="flex items-center justify-between gap-2">
                            <p className="truncate text-xs text-[var(--text-primary)]">{item.sku}</p>
                            <span className="text-[11px] text-[var(--text-primary)]">{item.preNivelacijaScore.toFixed(1)}</span>
                          </div>
                          <div className="mt-2 flex items-center gap-2">
                            {(["Unassigned", "Assigned", "Done"] as QueueStatus[]).map((status) => (
                              <button
                                key={status}
                                type="button"
                                onClick={() => setQueueState((s) => ({ ...s, [key]: status }))}
                                className={`rounded px-2 py-1 text-[10px] ${
                                  state === status
                                    ? "bg-[var(--surface-elevated)] text-white"
                                    : "bg-[var(--surface-elevated)] text-[var(--text-primary)]"
                                }`}
                              >
                                {status}
                              </button>
                            ))}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {!loading && !error && data && tab === "alerts" && (
          <div className="space-y-3">
            {data.alerts.length === 0 && <InventoryState message="Nema aktivnih upozorenja." tone="neutral" />}
            {data.alerts.map((a, idx) => (
              <div
                key={`${a.type}-${idx}`}
                className={`rounded-xl border px-3 py-2 text-sm ${
                  a.severity === "critical"
                    ? "border-rose-700 bg-rose-950/30 text-rose-300"
                    : "border-amber-700 bg-amber-950/30 text-amber-300"
                }`}
              >
                <div className="font-semibold">{a.type}</div>
                <div>{a.message}</div>
              </div>
            ))}
          </div>
        )}
      </InventoryPanel>
    </InventoryPageShell>
  );
}

