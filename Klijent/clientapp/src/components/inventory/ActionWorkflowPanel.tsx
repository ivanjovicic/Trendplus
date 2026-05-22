import { Archive, CheckCircle2, ClipboardPlus, Clock3, GitCompareArrows, Tag, Truck, XCircle } from "lucide-react";
import type { InventoryActionSuggestion, InventoryActionWorkflow } from "../../types/analytics";
import { formatCurrency, formatNumber, getActionStatusTone, getActionTypeTone, getPriorityTone } from "./inventoryUtils";

type ActionWorkflowPanelProps = {
  actionWorkflow: InventoryActionWorkflow | null;
  operationsLoading: boolean;
  workflowBusyKey: string | null;
  queueBusyKey: string | null;
  centralQueueUrl?: string;
  isSuggestionQueued?: (item: InventoryActionSuggestion) => boolean;
  onUpdateWorkflowStatus: (item: InventoryActionSuggestion, status: "approved" | "deferred" | "closed") => void;
  onAddToCentralQueue: (item: InventoryActionSuggestion) => void;
  sectionId?: string;
};

export function ActionWorkflowPanel({
  actionWorkflow,
  operationsLoading,
  workflowBusyKey,
  queueBusyKey,
  centralQueueUrl = "/analytics/actions?sourceType=inventory",
  isSuggestionQueued,
  onUpdateWorkflowStatus,
  onAddToCentralQueue,
  sectionId,
}: ActionWorkflowPanelProps) {
  const workflowItems = actionWorkflow?.items ?? [];

  return (
    <section id={sectionId} className="rounded-[28px] border border-border bg-surface p-5">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-foreground">Predlog akcije workflow</h2>
          <p className="text-sm text-muted">Dopuna, transfer, markdown i clearance predlozi sa workflow statusom obrade i brzim odlukama.</p>
        </div>
        <div className="flex items-center gap-2">
          <a
            href={centralQueueUrl}
            className="inline-flex items-center gap-1 rounded-full border border-border bg-surface px-3 py-1 text-xs font-semibold text-info transition-colors duration-150 hover:border-info"
            title="Otvori centralni Analytics Action Queue filtriran za inventory."
          >
            Otvori centralni red akcija
          </a>
          <div className="rounded-full border border-border bg-surface px-3 py-1 text-xs font-semibold text-muted">
            {workflowItems.length} aktivnih predloga
          </div>
        </div>
      </div>

      <div className="mt-4 grid gap-3 sm:grid-cols-4">
        <div className="rounded-2xl border border-border bg-surface p-3"><div className="text-xs uppercase tracking-[0.18em] text-info">Na cekanju</div><div className="mt-2 text-xl font-semibold text-foreground">{formatNumber(actionWorkflow?.pendingCount ?? 0)}</div></div>
        <div className="rounded-2xl border border-border bg-surface p-3"><div className="text-xs uppercase tracking-[0.18em] text-success">Odobreno</div><div className="mt-2 text-xl font-semibold text-foreground">{formatNumber(actionWorkflow?.approvedCount ?? 0)}</div></div>
        <div className="rounded-2xl border border-border bg-surface p-3"><div className="text-xs uppercase tracking-[0.18em] text-muted">Odlozeno</div><div className="mt-2 text-xl font-semibold text-foreground">{formatNumber(actionWorkflow?.deferredCount ?? 0)}</div></div>
        <div className="rounded-2xl border border-border bg-surface p-3"><div className="text-xs uppercase tracking-[0.18em] text-warning">Zatvoreno</div><div className="mt-2 text-xl font-semibold text-foreground">{formatNumber(actionWorkflow?.closedCount ?? 0)}</div></div>
      </div>

      <div className="mt-5 space-y-3">
        {operationsLoading && workflowItems.length === 0 ? <div className="rounded-2xl border border-dashed border-border bg-surface px-4 py-8 text-center text-sm text-muted">Ucitavam workflow predloge...</div> : workflowItems.length === 0 ? <div className="rounded-2xl border border-dashed border-border bg-surface px-4 py-8 text-center text-sm text-muted">Za trenutne filtere nema otvorenih predloga akcije.</div> : workflowItems.map((item) => {
          const queued = isSuggestionQueued?.(item) ?? false;
          const queueButtonDisabled = workflowBusyKey === item.suggestionKey || queueBusyKey === item.suggestionKey;

          return (
          <div key={item.suggestionKey} className="rounded-2xl border border-border bg-surface p-4">
            <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <span className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold ${getActionTypeTone(item.actionType)}`}>
                    {item.actionType === "dopuna" ? <Truck size={12} /> : item.actionType === "transfer" ? <GitCompareArrows size={12} /> : item.actionType === "markdown" ? <Tag size={12} /> : <Archive size={12} />}
                    <span className="ml-1 capitalize">{item.actionType}</span>
                  </span>
                  <span className={`inline-flex rounded-full border px-2.5 py-1 text-[11px] font-semibold ${getActionStatusTone(item.status)}`}>workflow: {item.status}</span>
                  <span className={`text-xs font-semibold uppercase tracking-[0.18em] ${getPriorityTone(item.priority)}`}>{item.priority}</span>
                </div>
                <div className="mt-3 text-sm font-semibold text-foreground">{item.label}</div>
                <div className="mt-1 text-sm leading-6 text-muted">{item.reason}</div>
                <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted">
                  <span>Artikal: {item.naziv}</span>
                  {item.fromStoreName ? <span>Iz: {item.fromStoreName}</span> : null}
                  {item.toStoreName ? <span>U: {item.toStoreName}</span> : null}
                  <span>Qty: {formatNumber(item.suggestedQty)}</span>
                  <span>Vrednost: {formatCurrency(item.estimatedValue)}</span>
                </div>
                {item.note ? <div className="mt-2 text-xs text-muted">Napomena: {item.note}</div> : null}
              </div>
              <div className="flex flex-wrap gap-2">
                {queued ? (
                  <a
                    href={centralQueueUrl}
                    title="Ovaj predlog je vec dodat u centralni red akcija."
                    className="inline-flex items-center gap-2 rounded-xl border px-3 py-2 text-xs font-semibold text-success"
                  >
                    <CheckCircle2 size={14} />
                    U centralnim akcijama
                  </a>
                ) : (
                  <button
                    type="button"
                    onClick={() => onAddToCentralQueue(item)}
                    disabled={queueButtonDisabled}
                    title="Dodaj predlog u centralni Analytics Action Queue."
                    className="inline-flex items-center gap-2 rounded-xl border px-3 py-2 text-xs font-semibold text-info disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    <ClipboardPlus size={14} />
                    Dodaj u centralne akcije
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => onUpdateWorkflowStatus(item, "approved")}
                  disabled={queueButtonDisabled}
                  title="Oznaci predlog kao odobren - akcija je validna i moze se izvrsiti."
                  className="inline-flex items-center gap-2 rounded-xl border px-3 py-2 text-xs font-semibold text-success disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <CheckCircle2 size={14} />
                  Odobri
                </button>
                <button
                  type="button"
                  onClick={() => onUpdateWorkflowStatus(item, "deferred")}
                  disabled={queueButtonDisabled}
                  title="Odlozi odluku - ponovo ces videti predlog kasnije."
                  className="inline-flex items-center gap-2 rounded-xl border px-3 py-2 text-xs font-semibold text-muted disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <Clock3 size={14} />
                  Odlozi
                </button>
                <button
                  type="button"
                  onClick={() => onUpdateWorkflowStatus(item, "closed")}
                  disabled={queueButtonDisabled}
                  title="Zatvori predlog - akcija nije relevantna u ovom trenutku ili je resena na drugi nacin."
                  className="inline-flex items-center gap-2 rounded-xl border px-3 py-2 text-xs font-semibold text-warning disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <XCircle size={14} />
                  Zatvori
                </button>
              </div>
            </div>
          </div>
        )})}
      </div>
    </section>
  );
}
