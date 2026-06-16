import { AlertCircle, AlertTriangle, GitCompareArrows, ShoppingCart, TrendingDown, Workflow } from "lucide-react";
import { Link } from "react-router-dom";
import type { InventoryActionWorkflow, InventoryBalance } from "../../types/analytics";
import { formatNumber } from "./inventoryUtils";

type DecisionSummaryBarProps = {
  balance: InventoryBalance | null;
  actionWorkflow: InventoryActionWorkflow | null;
  outOfStockCount?: number;
  lowStockCount?: number;
  dataQualityWarning?: boolean | null;
  dataQualityHref?: string | null;
  loading?: boolean;
};

export function DecisionSummaryBar({
  balance,
  actionWorkflow,
  outOfStockCount,
  lowStockCount,
  dataQualityWarning,
  dataQualityHref,
  loading,
}: DecisionSummaryBarProps) {
  const p1DopuniOdmah = outOfStockCount ?? 0;
  const p1OosRisk = (lowStockCount ?? 0) - p1DopuniOdmah;
  const p2Transfer = actionWorkflow?.items?.filter((item) => item.actionType === "transfer" && item.status === "pending").length ?? 0;
  const p2DeadStock = actionWorkflow?.items?.filter((item) => (item.actionType === "clearance" || item.actionType === "markdown") && item.status === "pending").length ?? 0;
  const workflowPending = actionWorkflow?.pendingCount ?? 0;

  if (loading && !balance && !actionWorkflow) {
    return (
      <div className="rounded-[28px] border border-border bg-surface p-4 shadow-lg animate-pulse">
        <div className="grid gap-3 grid-cols-2 sm:grid-cols-3 lg:grid-cols-6">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="h-20 rounded-2xl bg-muted opacity-20" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <section className="rounded-[28px] border border-border bg-surface p-4 shadow-lg">
      <div className="grid gap-3 grid-cols-2 sm:grid-cols-3 lg:grid-cols-6">
        {/* P1: Dopuni odmah */}
        <div className={`rounded-2xl border-2 p-3 transition-colors ${p1DopuniOdmah > 0 ? "border-error bg-[var(--surface-darker)]" : "border-border bg-surface"}`}>
          <div className="flex items-center gap-2 text-xs uppercase tracking-[0.18em] font-semibold">
            <ShoppingCart size={12} className={p1DopuniOdmah > 0 ? "text-error" : "text-muted"} />
            <span className={p1DopuniOdmah > 0 ? "text-error" : "text-muted"}>P1 Dopuni</span>
          </div>
          <div className={`mt-2 text-lg font-bold ${p1DopuniOdmah > 0 ? "text-error" : "text-foreground"}`}>{formatNumber(p1DopuniOdmah)}</div>
          <div className="mt-1 text-[10px] text-muted">bez zalihe / nula</div>
        </div>

        {/* P1: Rizik OOS */}
        <div className={`rounded-2xl border-2 p-3 transition-colors ${p1OosRisk > 0 ? "border-warning bg-[var(--surface-darker)]" : "border-border bg-surface"}`}>
          <div className="flex items-center gap-2 text-xs uppercase tracking-[0.18em] font-semibold">
            <AlertTriangle size={12} className={p1OosRisk > 0 ? "text-warning" : "text-muted"} />
            <span className={p1OosRisk > 0 ? "text-warning" : "text-muted"}>P1 OOS 7d</span>
          </div>
          <div className={`mt-2 text-lg font-bold ${p1OosRisk > 0 ? "text-warning" : "text-foreground"}`}>{formatNumber(p1OosRisk)}</div>
          <div className="mt-1 text-[10px] text-muted">uskoro bez zalihe</div>
        </div>

        {/* P2: Transfer kandidati */}
        <div className={`rounded-2xl border-2 p-3 transition-colors ${p2Transfer > 0 ? "border-info bg-[var(--surface-darker)]" : "border-border bg-surface"}`}>
          <div className="flex items-center gap-2 text-xs uppercase tracking-[0.18em] font-semibold">
            <GitCompareArrows size={12} className={p2Transfer > 0 ? "text-info" : "text-muted"} />
            <span className={p2Transfer > 0 ? "text-info" : "text-muted"}>P2 Transfer</span>
          </div>
          <div className={`mt-2 text-lg font-bold ${p2Transfer > 0 ? "text-info" : "text-foreground"}`}>{formatNumber(p2Transfer)}</div>
          <div className="mt-1 text-[10px] text-muted">prebacivanje između</div>
        </div>

        {/* P2: Dead stock / Markdown */}
        <div className={`rounded-2xl border-2 p-3 transition-colors ${p2DeadStock > 0 ? "border-warning bg-[var(--surface-darker)]" : "border-border bg-surface"}`}>
          <div className="flex items-center gap-2 text-xs uppercase tracking-[0.18em] font-semibold">
            <TrendingDown size={12} className={p2DeadStock > 0 ? "text-warning" : "text-muted"} />
            <span className={p2DeadStock > 0 ? "text-warning" : "text-muted"}>P2 Dead stock</span>
          </div>
          <div className={`mt-2 text-lg font-bold ${p2DeadStock > 0 ? "text-warning" : "text-foreground"}`}>{formatNumber(p2DeadStock)}</div>
          <div className="mt-1 text-[10px] text-muted">mrtva zaliha / sniženje</div>
        </div>

        {/* Workflow Pending */}
        <div className={`rounded-2xl border-2 p-3 transition-colors ${workflowPending > 0 ? "border-focus bg-[var(--surface-darker)]" : "border-border bg-surface"}`}>
          <div className="flex items-center gap-2 text-xs uppercase tracking-[0.18em] font-semibold">
            <Workflow size={12} className={workflowPending > 0 ? "text-focus" : "text-muted"} />
            <span className={workflowPending > 0 ? "text-focus" : "text-muted"}>Workflow</span>
          </div>
          <div className={`mt-2 text-lg font-bold ${workflowPending > 0 ? "text-focus" : "text-foreground"}`}>{formatNumber(workflowPending)}</div>
          <div className="mt-1 text-[10px] text-muted">čeka odluku</div>
        </div>

        {/* Data Quality Warning */}
        <div className={`rounded-2xl border-2 p-3 transition-colors ${dataQualityWarning ? "border-error bg-[var(--surface-darker)]" : "border-border bg-surface"}`}>
          <div className="flex items-center gap-2 text-xs uppercase tracking-[0.18em] font-semibold">
            <AlertCircle size={12} className={dataQualityWarning ? "text-error" : "text-muted"} />
            <span className={dataQualityWarning ? "text-error" : "text-muted"}>Kvalitet</span>
          </div>
          <div className={`mt-2 text-lg font-bold ${dataQualityWarning ? "text-error" : "text-success"}`}>{dataQualityWarning ? "⚠" : "✓"}</div>
          <div className={`mt-1 text-[10px] ${dataQualityWarning ? "text-error" : "text-muted"}`}>
            {dataQualityWarning ? "Kvalitet podataka traži proveru" : "podaci OK"}
          </div>
          {dataQualityWarning && dataQualityHref ? (
            <Link to={dataQualityHref} className="mt-2 inline-flex text-[10px] font-medium text-error underline decoration-dotted underline-offset-2">
              Otvori Data Quality
            </Link>
          ) : null}
        </div>
      </div>
    </section>
  );
}
