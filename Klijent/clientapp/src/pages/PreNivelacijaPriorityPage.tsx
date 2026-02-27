import { useEffect, useMemo, useState } from "react";
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
import { InventoryKpiRow, InventoryPageShell, InventoryPanel, InventoryState } from "../components/inventory/InventoryPageShell";
import { getPreNivelacijaPrioriteti, type PreNivelacijaQuery } from "../services/preNivelacijaApi";
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
  if (band === "high") return "#f87171";
  if (band === "medium") return "#fbbf24";
  return "#60a5fa";
}

function queueKey(item: PreNivelacijaQueueItem) {
  return `${item.artikalId}`;
}

export default function PreNivelacijaPriorityPage() {
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
        z: Math.max(1, Number(x.scenarioHighlightNow.expectedRevenue30d) / 1000),
        label: x.sku,
        band: x.priorityBand,
      })),
    [data]
  );

  const queueSections = useMemo(() => {
    if (!data) return [];
    return [
      { label: "Highlight now", items: data.queues.highlightNow },
      { label: "Monitor", items: data.queues.monitor },
      { label: "Likely markdown soon", items: data.queues.likelyMarkdownSoon },
    ];
  }, [data]);

  return (
    <InventoryPageShell
      icon={Sparkles}
      title="Pre-Nivelacija Prioriteti Dobavljaca"
      subtitle="Operativna analitika koja otkriva koje modele treba isticati pre markdown-a."
      actions={
        <button
          type="button"
          onClick={() => setPage(1)}
          className="rounded-xl border border-[#3760b7] bg-[#2d4f95] px-3 py-2 text-xs font-semibold text-white"
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
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Dobavljac</label>
            <select
              className="w-full rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-2 text-sm text-[#dbe6fb]"
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
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Sezona</label>
            <select
              className="w-full rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-2 text-sm text-[#dbe6fb]"
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
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Tip obuce</label>
            <select
              className="w-full rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-2 text-sm text-[#dbe6fb]"
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
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Stock min</label>
            <input
              type="number"
              value={filters.stockMin ?? 1}
              onChange={(e) => setFilters((f) => ({ ...f, stockMin: Number(e.target.value) }))}
              className="w-full rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-2 text-sm text-[#dbe6fb]"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">No-sale days min</label>
            <input
              type="number"
              value={filters.noSaleDaysMin ?? 14}
              onChange={(e) => setFilters((f) => ({ ...f, noSaleDaysMin: Number(e.target.value) }))}
              className="w-full rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-2 text-sm text-[#dbe6fb]"
            />
          </div>

          <div>
            <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Min score</label>
            <input
              type="number"
              value={filters.minScore ?? 40}
              onChange={(e) => setFilters((f) => ({ ...f, minScore: Number(e.target.value) }))}
              className="w-full rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-2 text-sm text-[#dbe6fb]"
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
                  ? "border-[#3760b7] bg-[#2d4f95] text-white"
                  : "border-[#3c4458] bg-[#222734] text-[#dbe6fb]"
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
            <div className="overflow-x-auto rounded-xl border border-[#2f323b]">
              <table className="min-w-full divide-y divide-[#2f323b] text-sm">
                <thead className="bg-[#14161d] text-[#93a7c8]">
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
                <tbody className="divide-y divide-[#262a34] bg-[#1a1b1f] text-[#dbe6fb]">
                  {data.candidates.map((row) => (
                    <tr key={row.artikalId} className={`hover:brightness-110 ${
                      row.priorityBand === "high" ? "bg-rose-950/20" :
                      row.priorityBand === "medium" ? "bg-amber-950/15" :
                      "hover:bg-[#1f2330]"
                    }`}>
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
              <p className="text-xs text-[#93a7c8]">
                Formula: {data.formulaDescription}
              </p>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={data.page <= 1}
                  className="rounded-md border border-[#3c4458] bg-[#222734] p-1.5 text-[#dbe6fb] disabled:opacity-50"
                  title="Prethodna"
                >
                  <ChevronLeft size={14} />
                </button>
                <span className="text-xs text-[#93a7c8]">
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
                  className="rounded-md border border-[#3c4458] bg-[#222734] p-1.5 text-[#dbe6fb] disabled:opacity-50"
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
            <div className="overflow-x-auto rounded-xl border border-[#2f323b]">
              <table className="min-w-full divide-y divide-[#2f323b] text-sm">
                <thead className="bg-[#14161d] text-[#93a7c8]">
                  <tr>
                    <th className="px-3 py-2 text-left">Dobavljac</th>
                    <th className="px-3 py-2 text-right">Action score</th>
                    <th className="px-3 py-2 text-right">High SKU</th>
                    <th className="px-3 py-2 text-right">Stock risk</th>
                    <th className="px-3 py-2 text-right">Avoidable loss</th>
                    <th className="px-3 py-2 text-right">WoW risk</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#262a34] bg-[#1a1b1f] text-[#dbe6fb]">
                  {data.supplierLeaderboard.map((s, idx) => (
                    <tr key={`${s.supplierName}-${idx}`} className="hover:bg-[#1f2330]">
                      <td className="px-3 py-2">{s.supplierName}</td>
                      <td className="px-3 py-2 text-right font-semibold">{s.actionScore.toFixed(1)}</td>
                      <td className="px-3 py-2 text-right">{s.highPrioritySkuCount}</td>
                      <td className="px-3 py-2 text-right">{s.stockUnitsAtRisk}</td>
                      <td className="px-3 py-2 text-right">{fmtRsd(s.estimatedAvoidableMarkdownLoss)}</td>
                      <td className={`px-3 py-2 text-right ${s.weekOverWeekRiskDeltaPct > 20 ? "text-rose-300" : "text-[#dbe6fb]"}`}>
                        {fmtPct(s.weekOverWeekRiskDeltaPct)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="h-[320px] rounded-xl border border-[#2f323b] bg-[#14161d] p-3">
              <ResponsiveContainer width="100%" height="100%">
                <ScatterChart>
                  <CartesianGrid stroke="#2f323b" />
                  <XAxis dataKey="x" name="Stock" tick={{ fill: "#9aabc7", fontSize: 11 }} />
                  <YAxis dataKey="y" name="Velocity" tick={{ fill: "#9aabc7", fontSize: 11 }} />
                  <ZAxis dataKey="z" range={[40, 260]} name="Revenue" />
                  <Tooltip cursor={{ strokeDasharray: "4 4" }} />
                  <Legend />
                  <Scatter data={scatterData} name="SKU signal" fill="#4f8cff" />
                </ScatterChart>
              </ResponsiveContainer>
            </div>
          </div>
        )}

        {!loading && !error && data && tab === "simulator" && (
          <div className="space-y-4">
            <div className="overflow-x-auto rounded-xl border border-[#2f323b]">
              <table className="min-w-full divide-y divide-[#2f323b] text-sm">
                <thead className="bg-[#14161d] text-[#93a7c8]">
                  <tr>
                    <th className="px-3 py-2 text-left">SKU</th>
                    <th className="px-3 py-2 text-right">Highlight rev</th>
                    <th className="px-3 py-2 text-right">Markdown rev</th>
                    <th className="px-3 py-2 text-right">Delta rev</th>
                    <th className="px-3 py-2 text-right">Delta margin</th>
                    <th className="px-3 py-2 text-right">Confidence</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#262a34] bg-[#1a1b1f] text-[#dbe6fb]">
                  {data.candidates.slice(0, 20).map((row) => (
                    <tr key={row.artikalId} className="hover:bg-[#1f2330]">
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
                <div key={section.label} className="rounded-xl border border-[#2f323b] bg-[#14161d] p-3">
                  <h3 className="mb-2 text-sm font-semibold text-[#dbe6fb]">{section.label}</h3>
                  <div className="space-y-2">
                    {section.items.slice(0, 8).map((item) => {
                      const key = queueKey(item);
                      const state = queueState[key] ?? (item.status as QueueStatus);
                      return (
                        <div key={key} className="rounded-lg border border-[#2a2f3b] bg-[#1a1b1f] p-2">
                          <div className="flex items-center justify-between gap-2">
                            <p className="truncate text-xs text-[#dbe6fb]">{item.sku}</p>
                            <span className="text-[11px] text-[#93a7c8]">{item.preNivelacijaScore.toFixed(1)}</span>
                          </div>
                          <div className="mt-2 flex items-center gap-2">
                            {(["Unassigned", "Assigned", "Done"] as QueueStatus[]).map((status) => (
                              <button
                                key={status}
                                type="button"
                                onClick={() => setQueueState((s) => ({ ...s, [key]: status }))}
                                className={`rounded px-2 py-1 text-[10px] ${
                                  state === status
                                    ? "bg-[#2d4f95] text-white"
                                    : "bg-[#222734] text-[#9aabc7]"
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
