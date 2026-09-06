import { AlertCircle, AlertTriangle, GitCompareArrows, ShoppingCart, TrendingDown, Workflow } from "lucide-react";
import { Link } from "react-router-dom";
import type { InventoryActionWorkflow, InventoryBalance } from "../../types/analytics";
import { formatNumber } from "./inventoryUtils";

type DecisionSummaryBarProps = {
  balance: InventoryBalance | null;
  actionWorkflow: InventoryActionWorkflow | null;
  outOfStockCount?: number | null;
  lowStockCount?: number | null;
  dataQualityWarning?: boolean | null;
  dataQualityHref?: string | null;
  loading?: boolean;
};

function formatCount(value: number | null | undefined) {
  if (value == null || !Number.isFinite(value)) return "Nije dostupno";
  return formatNumber(value);
}

export function DecisionSummaryBar({
  balance,
  actionWorkflow,
  outOfStockCount,
  lowStockCount,
  dataQualityWarning,
  dataQualityHref,
  loading,
}: DecisionSummaryBarProps) {
  // Backend low-stock already excludes OOS; never subtract. Null stays unavailable.
  const currentOosCount =
    outOfStockCount == null || !Number.isFinite(outOfStockCount) ? null : Math.max(0, outOfStockCount);
  const currentLowStockCount =
    lowStockCount == null || !Number.isFinite(lowStockCount) ? null : Math.max(0, lowStockCount);
  const p2Transfer = actionWorkflow?.items?.filter((item) => item.actionType === "transfer" && item.status === "pending").length ?? 0;
  const p2DeadStock = actionWorkflow?.items?.filter((item) => (item.actionType === "clearance" || item.actionType === "markdown") && item.status === "pending").length ?? 0;
  const workflowPending = actionWorkflow?.pendingCount ?? 0;
  const hasOos = (currentOosCount ?? 0) > 0;
  const hasLowStock = (currentLowStockCount ?? 0) > 0;

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
        {/* Current OOS (measured zero quantity) */}
        <div className={`rounded-2xl border-2 p-3 transition-colors ${hasOos ? "border-error bg-[var(--surface-darker)]" : "border-border bg-surface"}`}>
          <div className="flex items-center gap-2 text-xs uppercase tracking-[0.18em] font-semibold">
            <ShoppingCart size={12} className={hasOos ? "text-error" : "text-muted"} />
            <span className={hasOos ? "text-error" : "text-muted"}>Trenutno OOS</span>
          </div>
          <div className={`mt-2 text-lg font-bold ${hasOos ? "text-error" : "text-foreground"}`}>{formatCount(currentOosCount)}</div>
          <div className="mt-1 text-[10px] text-muted">bez zalihe (trenutno)</div>
        </div>

        {/* Current low stock — not a 7d risk forecast */}
        <div className={`rounded-2xl border-2 p-3 transition-colors ${hasLowStock ? "border-warning bg-[var(--surface-darker)]" : "border-border bg-surface"}`}>
          <div className="flex items-center gap-2 text-xs uppercase tracking-[0.18em] font-semibold">
            <AlertTriangle size={12} className={hasLowStock ? "text-warning" : "text-muted"} />
            <span className={hasLowStock ? "text-warning" : "text-muted"}>Niska zaliha</span>
          </div>
          <div className={`mt-2 text-lg font-bold ${hasLowStock ? "text-warning" : "text-foreground"}`}>{formatCount(currentLowStockCount)}</div>
          <div className="mt-1 text-[10px] text-muted">pozitivna ≤ minimum (trenutno)</div>
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
