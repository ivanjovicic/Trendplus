import React from "react";
import { Copy, RefreshCw } from "lucide-react";
import InfoTip from "../ui/InfoTip";
import { getAnalyticsDetail } from "../../services/analyticsDetailApi";
import { getAnalyticsDetailSnapshot } from "../../services/analyticsTableState";
import type { AnalyticsDetailResponse } from "../../types/analyticsTable";
import { InventoryState } from "../inventory/InventoryPageShell";

function DetailSkeleton() {
  return (
    <div className="space-y-4 animate-pulse">
      {[0, 1, 2].map((section) => (
        <section key={section} className="rounded-2xl border border-muted bg-surface-elevated p-4">
          <div className="mb-4 h-4 w-40 rounded bg-muted/20" />
          <div className="space-y-3">
            {[0, 1, 2, 3].map((row) => (
              <div key={row} className="grid gap-2 sm:grid-cols-[160px_1fr]">
                <div className="h-3 w-24 rounded bg-muted/10" />
                <div className="h-4 w-full rounded bg-muted/15" />
              </div>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}

function DetailRow(props: { label: string; value?: string | null; highlight?: boolean }) {
  return (
    <div className="grid gap-1 border-b border-muted/30 py-3 sm:grid-cols-[160px_1fr] sm:gap-3">
      <div className="text-xs uppercase tracking-wide text-muted">{props.label}</div>
      <div className={props.highlight ? "font-semibold text-success" : "text-contrast"}>
        {props.value || "-"}
      </div>
    </div>
  );
}

async function copyValue(value: string) {
  try {
    await navigator.clipboard.writeText(value);
  } catch {
    // best effort copy
  }
}

export default function AnalyticsDetailView(props: {
  table: string;
  recordId: string;
  queryString?: string;
}) {
  const [detail, setDetail] = React.useState<AnalyticsDetailResponse | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [usedSnapshot, setUsedSnapshot] = React.useState(false);

  const load = React.useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      console.info("Detail opened", { table: props.table, id: props.recordId });
      const backend = await getAnalyticsDetail(props.table, props.recordId, props.queryString);
      if (backend) {
        setDetail(backend);
        setUsedSnapshot(false);
        return;
      }

      const snapshot = getAnalyticsDetailSnapshot(props.table, props.recordId);
      setDetail(snapshot);
      setUsedSnapshot(snapshot != null);
    } catch (reason) {
      const snapshot = getAnalyticsDetailSnapshot(props.table, props.recordId);
      if (snapshot) {
        setDetail(snapshot);
        setUsedSnapshot(true);
        return;
      }

      setError(reason instanceof Error ? reason.message : "Greška pri ucitavanju detalja.");
      setDetail(null);
    } finally {
      setLoading(false);
    }
  }, [props.recordId, props.queryString, props.table]);

  React.useEffect(() => {
    void load();
  }, [load]);

  if (loading) {
    return <DetailSkeleton />;
  }

  if (error) {
    return (
      <div className="space-y-3">
        <InventoryState message={error} tone="danger" />
        <div className="flex justify-center">
          <button
            type="button"
            onClick={() => void load()}
            className="inline-flex items-center gap-2 rounded-lg border border-info bg-info/10 px-3 py-2 text-xs font-semibold text-info"
          >
            <RefreshCw size={14} />
            Pokušaj ponovo
          </button>
        </div>
      </div>
    );
  }

  if (!detail) {
    return <InventoryState message="Detalj nije pronadjen za izabrani zapis." tone="neutral" />;
  }

  return (
    <div className="space-y-5 text-sm">
      <section className="rounded-2xl border border-muted bg-surface-elevated p-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <div className="text-xs uppercase tracking-wide text-muted">Detalj analitike</div>
            <div className="mt-2 text-lg font-semibold text-contrast">{detail.title} <InfoTip text="Detaljan prikaz izabranog zapisa sa dodatnim poljima i metapodacima." /></div>
            {detail.subtitle ? <div className="mt-1 text-sm text-muted">{detail.subtitle}</div> : null}
            {usedSnapshot ? (
              <div className="mt-2 text-xs text-warning">
                Prikazan je sačuvani snapshot reda jer backend detalj nije dostupan za ovu tabelu.
              </div>
            ) : null}
          </div>
          <button
            type="button"
            onClick={() => void copyValue(detail.recordId)}
            className="inline-flex items-center gap-1 rounded-lg border border-muted bg-surface-darker px-3 py-2 text-xs text-muted hover:text-contrast transition-colors"
            title="Kopiraj identifikator zapisa u clipboard"
          >
            <Copy size={13} />
            Kopiraj ID
          </button>
        </div>
      </section>

      <section className="rounded-2xl border border-muted bg-surface-elevated p-4">
        <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">Polja</h3>
        {detail.fields.map((field) => (
          <DetailRow key={field.key} label={field.label} value={field.value} highlight={field.highlight} />
        ))}
      </section>

      {detail.metadata.length > 0 ? (
        <section className="rounded-2xl border border-muted bg-surface-elevated p-4">
          <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-muted">Metadata</h3>
          {detail.metadata.map((field) => (
            <DetailRow key={field.key} label={field.label} value={field.value} />
          ))}
        </section>
      ) : null}
    </div>
  );
}

