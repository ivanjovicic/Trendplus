import { AlertTriangle } from "lucide-react";
import type { InventoryAlertListDto } from "../../types/analytics";
import { getAlertSeverityTone } from "./inventoryUtils";

type InventoryAlertsFeedProps = {
  alerts: InventoryAlertListDto | null;
  alertsLoading: boolean;
  alertsError?: string | null;
  alertSeverityFilter: "" | "critical" | "warning" | "info";
  onSeverityFilterChange: (value: "" | "critical" | "warning" | "info") => void;
  displayCount: number;
  onOpenSizeCurve: (skuId: number) => void;
  onOpenDetail: (skuId: number, storeId: number, label?: string) => void;
};

export function InventoryAlertsFeed({
  alerts,
  alertsLoading,
  alertsError,
  alertSeverityFilter,
  onSeverityFilterChange,
  displayCount,
  onOpenSizeCurve,
  onOpenDetail,
}: InventoryAlertsFeedProps) {
  const filteredAlerts = (alerts?.items ?? []).filter((alert) => !alertSeverityFilter || alert.severity === alertSeverityFilter);

    return (
    <section className="rounded-[28px] border border-border bg-surface p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div className="flex items-center gap-3">
          <div className="rounded-2xl border p-2.5 bg-surface-elevated text-muted">
            <AlertTriangle size={18} />
          </div>
          <div>
            <h2 className="text-lg font-semibold text-foreground">Inventory upozorenja</h2>
            <p className="text-sm text-muted">AI-generisani kritični signali iz zaliha. Osvežava se automatski.</p>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {(["", "critical", "warning", "info"] as const).map((severity) => (
            <button
              key={severity || "all"}
              type="button"
              aria-label={severity === "" ? "Prikazi sve inventory alertove" : `Filtriraj alertove po nivou ${severity}`}
              onClick={() => onSeverityFilterChange(severity)}
              className={`rounded-full border px-3 py-1 text-xs font-semibold transition ${alertSeverityFilter === severity ? "border-info bg-surface-elevated text-info" : "border-border bg-surface text-muted"}`}
            >
              {severity === "" ? "Sve" : severity === "critical" ? "Kritično" : severity === "warning" ? "Upozorenje" : "Info"}
            </button>
          ))}
          <div className="rounded-full border border-border bg-surface px-3 py-1 text-xs font-semibold text-muted">
            {alertsLoading ? "..." : `${alerts?.totalCount ?? 0} ukupno`}
          </div>
        </div>
      </div>

      {alertsError ? (
        <div className="mt-4 rounded-2xl border border-[var(--error)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--error)]">
          {alertsError}
        </div>
      ) : !alerts?.snapshotAvailable ? (
        <div className="mt-4 rounded-2xl border border-dashed border-border bg-surface px-4 py-8 text-center text-sm text-muted">
          {alertsLoading ? "Učitavam upozorenja..." : "Upozorenja nisu dostupna. Snapshot tabela je prazna ili nije pokrenuta analitika."}
          {alerts?.warning ? <div className="mt-2 text-xs text-warning">{alerts.warning}</div> : null}
        </div>
      ) : (
        <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {filteredAlerts.slice(0, displayCount).map((alert, index) => (
            <article key={`${alert.alertType}-${alert.skuId}-${alert.sizeCode ?? "all"}-${index}`} onClick={() => onOpenDetail(alert.skuId, alert.storeId, alert.title)} className={`cursor-pointer rounded-2xl border border-border bg-surface p-4 ${alert.severity === "critical" ? "inventory-alert-critical" : ""}`}>
              <div className="flex items-start justify-between gap-3">
                <div className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold ${getAlertSeverityTone(alert.severity)}`}>
                  {alert.severity === "critical" ? "Kritično" : alert.severity === "warning" ? "Upozorenje" : "Info"}
                </div>
                <div className="rounded-full border border-border bg-surface px-2 py-0.5 text-[11px] font-semibold text-muted">
                  {Math.round(alert.confidenceScore * 100)}%
                </div>
              </div>
              <div className="mt-3 text-sm font-semibold text-foreground">{alert.title}</div>
              <div className="mt-1 text-xs leading-5 text-muted">{alert.message}</div>
              <div className="mt-3 flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-muted">
                <span>Tip: {alert.alertType}</span>
                {alert.sizeCode ? <span>Vel: {alert.sizeCode}</span> : null}
                <button type="button" aria-label={`Otvori detalj artikla za alert ${alert.title}`} onClick={(event) => { event.stopPropagation(); onOpenDetail(alert.skuId, alert.storeId, alert.title); }} className="text-muted transition hover:text-foreground">
                  Detalj artikla -&gt;
                </button>
                <button type="button" aria-label={`Otvori size curve za SKU ${alert.skuId}`} onClick={(event) => { event.stopPropagation(); onOpenSizeCurve(alert.skuId); }} className="text-info transition hover:text-info/80">
                  Size curve -&gt;
                </button>
              </div>
            </article>
          ))}
          {filteredAlerts.length === 0 ? (
            <div className="col-span-full rounded-2xl border border-dashed border-border bg-surface px-4 py-8 text-center text-sm text-muted">Nema alertova za izabrani filter.</div>
          ) : null}
        </div>
      )}
      {alerts?.warning ? <p className="mt-3 text-xs text-warning">Napomena: {alerts.warning}</p> : null}
    </section>
  );
}
