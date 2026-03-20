import { Archive, CheckCircle2, Clock3, GitCompareArrows, Tag, Truck, XCircle } from "lucide-react";
import type { InventoryActionSuggestion, InventoryActionWorkflow } from "../../types/analytics";
import { formatCurrency, formatNumber, getActionStatusTone, getActionTypeTone, getPriorityTone } from "./inventoryUtils";

type ActionWorkflowPanelProps = {
  actionWorkflow: InventoryActionWorkflow | null;
  operationsLoading: boolean;
  workflowBusyKey: string | null;
  onUpdateWorkflowStatus: (item: InventoryActionSuggestion, status: "approved" | "deferred" | "closed") => void;
};

export function ActionWorkflowPanel({
  actionWorkflow,
  operationsLoading,
  workflowBusyKey,
  onUpdateWorkflowStatus,
}: ActionWorkflowPanelProps) {
  const workflowItems = actionWorkflow?.items ?? [];

  return (
    <section className="rounded-[28px] border border-[#232935] bg-[#12161f] p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-white">Predlog akcije workflow</h2>
          <p className="text-sm text-[#90a0ba]">Dopuna, transfer, markdown i clearance predlozi sa statusom obrade i brzim odlukama.</p>
        </div>
        <div className="rounded-full border border-[#33405a] bg-[#182131] px-3 py-1 text-xs font-semibold text-[#dbe6fb]">
          {workflowItems.length} aktivnih predloga
        </div>
      </div>

      <div className="mt-4 grid gap-3 sm:grid-cols-4">
        <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#8edbff]">Pending</div><div className="mt-2 text-xl font-semibold text-white">{formatNumber(actionWorkflow?.pendingCount ?? 0)}</div></div>
        <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#9ff0c7]">Approved</div><div className="mt-2 text-xl font-semibold text-white">{formatNumber(actionWorkflow?.approvedCount ?? 0)}</div></div>
        <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#dbe6fb]">Deferred</div><div className="mt-2 text-xl font-semibold text-white">{formatNumber(actionWorkflow?.deferredCount ?? 0)}</div></div>
        <div className="rounded-2xl border border-[#243040] bg-[#10141b] p-3"><div className="text-xs uppercase tracking-[0.18em] text-[#ffbdcb]">Closed</div><div className="mt-2 text-xl font-semibold text-white">{formatNumber(actionWorkflow?.closedCount ?? 0)}</div></div>
      </div>

      <div className="mt-5 space-y-3">
        {operationsLoading && workflowItems.length === 0 ? <div className="rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Ucitavam workflow predloge...</div> : workflowItems.length === 0 ? <div className="rounded-2xl border border-dashed border-[#2b3446] bg-[#10151d] px-4 py-8 text-center text-sm text-[#8797b4]">Za trenutne filtere nema otvorenih predloga akcije.</div> : workflowItems.map((item) => (
          <div key={item.suggestionKey} className="rounded-2xl border border-[#243040] bg-[#10141b] p-4">
            <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <span className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold ${getActionTypeTone(item.actionType)}`}>
                    {item.actionType === "dopuna" ? <Truck size={12} /> : item.actionType === "transfer" ? <GitCompareArrows size={12} /> : item.actionType === "markdown" ? <Tag size={12} /> : <Archive size={12} />}
                    <span className="ml-1 capitalize">{item.actionType}</span>
                  </span>
                  <span className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold ${getActionStatusTone(item.status)}`}>{item.status}</span>
                  <span className={`text-xs font-semibold uppercase tracking-[0.18em] ${getPriorityTone(item.priority)}`}>{item.priority}</span>
                </div>
                <div className="mt-3 text-sm font-semibold text-white">{item.label}</div>
                <div className="mt-1 text-sm leading-6 text-[#90a0ba]">{item.reason}</div>
                <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-[#7f8fa9]">
                  <span>Artikal: {item.naziv}</span>
                  {item.fromStoreName ? <span>Iz: {item.fromStoreName}</span> : null}
                  {item.toStoreName ? <span>U: {item.toStoreName}</span> : null}
                  <span>Qty: {formatNumber(item.suggestedQty)}</span>
                  <span>Vrednost: {formatCurrency(item.estimatedValue)}</span>
                </div>
                {item.note ? <div className="mt-2 text-xs text-[#dbe6fb]">Napomena: {item.note}</div> : null}
              </div>
              <div className="flex flex-wrap gap-2">
                <button type="button" onClick={() => onUpdateWorkflowStatus(item, "approved")} disabled={workflowBusyKey === item.suggestionKey} className="inline-flex items-center gap-2 rounded-xl border border-[#28574d] bg-[#102b24] px-3 py-2 text-xs font-semibold text-[#9ff0c7] disabled:cursor-not-allowed disabled:opacity-60"><CheckCircle2 size={14} />Odobri</button>
                <button type="button" onClick={() => onUpdateWorkflowStatus(item, "deferred")} disabled={workflowBusyKey === item.suggestionKey} className="inline-flex items-center gap-2 rounded-xl border border-[#33405a] bg-[#182131] px-3 py-2 text-xs font-semibold text-[#dbe6fb] disabled:cursor-not-allowed disabled:opacity-60"><Clock3 size={14} />Odlozi</button>
                <button type="button" onClick={() => onUpdateWorkflowStatus(item, "closed")} disabled={workflowBusyKey === item.suggestionKey} className="inline-flex items-center gap-2 rounded-xl border border-[#6b2c38] bg-[#281319] px-3 py-2 text-xs font-semibold text-[#ffc3cf] disabled:cursor-not-allowed disabled:opacity-60"><XCircle size={14} />Zatvori</button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
