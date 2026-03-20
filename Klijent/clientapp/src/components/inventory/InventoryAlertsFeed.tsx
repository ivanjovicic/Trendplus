import { AlertTriangle } from "lucide-react";
import type { InventoryAlertListDto } from "../../types/analytics";
import { getAlertSeverityTone } from "./inventoryUtils";

type InventoryAlertsFeedProps = {
  alerts: InventoryAlertListDto | null;
  alertsLoading: boolean;
  alertSeverityFilter: "" | "critical" | "warning" | "info";
  onSeverityFilterChange: (value: "" | "critical" | "warning" | "info") => void;
  displayCount: number;
  onOpenSizeCurve: (skuId: number) => void;
  onOpenDetail: (skuId: number, storeId: number, label?: string) => void;
};

export function InventoryAlertsFeed({
  alerts,
  alertsLoading,
  alertSeverityFilter,
  onSeverityFilterChange,
  displayCount,
  onOpenSizeCurve,
  onOpenDetail,
}: InventoryAlertsFeedProps) {
  const filteredAlerts = (alerts?.items ?? []).filter((alert) => !alertSeverityFilter || alert.severity === alertSeverityFilter);

  return (
    <section className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div className="flex items-center gap-3">
          <div className="rounded-2xl border border-[#7d2940] bg-[#411520] p-2.5 text-[#ffb4c2]">
            <AlertTriangle size={18} />
          </div>
          <div>
            <h2 className="text-lg font-semibold text-white">Inventory Alerts</h2>
            <p className="text-sm text-[#90a0ba]">AI-generisani kriticni signali iz zalihe. Osvezava se automatski.</p>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {(["", "critical", "warning", "info"] as const).map((severity) => (
            <button
              key={severity || "all"}
              type="button"
              aria-label={severity === "" ? "Prikazi sve inventory alertove" : `Filtriraj alertove po nivou ${severity}`}
              onClick={() => onSeverityFilterChange(severity)}
              className={`rounded-full border px-3 py-1 text-xs font-semibold transition ${alertSeverityFilter === severity ? "border-[#44d0ff] bg-[#102231] text-[#44d0ff]" : "border-[#33405a] bg-[#182131] text-[#dbe6fb]"}`}
            >
              {severity === "" ? "Sve" : severity === "critical" ? "Kriticno" : severity === "warning" ? "Upozorenje" : "Info"}
            </button>
          ))}
          <div className="rounded-full border border-[#33405a] bg-[#182131] px-3 py-1 text-xs font-semibold text-[#dbe6fb]">
            {alertsLoading ? "..." : `${alerts?.totalCount ?? 0} ukupno`}
          </div>
        </div>
      </div>

      {!alerts?.snapshotAvailable ? (
        <div className="mt-4 rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">
          {alertsLoading ? "Ucitavam alertove..." : "Alertovi nisu dostupni. Snapshot tabela je prazna ili nije pokrenuta analitika."}
          {alerts?.warning ? <div className="mt-2 text-xs text-[#ffd590]">{alerts.warning}</div> : null}
        </div>
      ) : (
        <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {filteredAlerts.slice(0, displayCount).map((alert, index) => (
            <article key={`${alert.alertType}-${alert.skuId}-${alert.sizeCode ?? "all"}-${index}`} onClick={() => onOpenDetail(alert.skuId, alert.storeId, alert.title)} className={`cursor-pointer rounded-2xl border border-[#243040] bg-[#10141b] p-4 ${alert.severity === "critical" ? "inventory-alert-critical" : ""}`}>
              <div className="flex items-start justify-between gap-3">
                <div className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold ${getAlertSeverityTone(alert.severity)}`}>
                  {alert.severity === "critical" ? "Kriticno" : alert.severity === "warning" ? "Upozorenje" : "Info"}
                </div>
                <div className="rounded-full border border-[#33405a] bg-[#182131] px-2 py-0.5 text-[11px] font-semibold text-[#dbe6fb]">
                  {Math.round(alert.confidenceScore * 100)}%
                </div>
              </div>
              <div className="mt-3 text-sm font-semibold text-white">{alert.title}</div>
              <div className="mt-1 text-xs leading-5 text-[#90a0ba]">{alert.message}</div>
              <div className="mt-3 flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-[#7f8fa9]">
                <span>Tip: {alert.alertType}</span>
                {alert.sizeCode ? <span>Vel: {alert.sizeCode}</span> : null}
                <button type="button" aria-label={`Otvori detalj artikla za alert ${alert.title}`} onClick={(event) => { event.stopPropagation(); onOpenDetail(alert.skuId, alert.storeId, alert.title); }} className="text-[#dbe6fb] transition hover:text-white">
                  Detalj artikla -&gt;
                </button>
                <button type="button" aria-label={`Otvori size curve za SKU ${alert.skuId}`} onClick={(event) => { event.stopPropagation(); onOpenSizeCurve(alert.skuId); }} className="text-[#44d0ff] transition hover:text-[#6de0ff]">
                  Size curve -&gt;
                </button>
              </div>
            </article>
          ))}
          {filteredAlerts.length === 0 ? (
            <div className="col-span-full rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Nema alertova za izabrani filter.</div>
          ) : null}
        </div>
      )}
    </section>
  );
}
