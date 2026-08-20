import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { AlertTriangle } from "lucide-react";
import { getDecisionPulse, type DecisionPulseResponse } from "../services/decisionPulseApi";

export default function DecisionPulsePage() {
  const [feed, setFeed] = useState<DecisionPulseResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    getDecisionPulse()
      .then((response) => {
        if (!cancelled) setFeed(response);
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setFeed(null);
          setError(err instanceof Error ? err.message : "Decision Pulse nije dostupan.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const metaFailed = feed?.meta?.success === false;
  const items = feed?.items ?? [];

  return (
    <div className="mx-auto max-w-5xl space-y-6 p-6">
      <header className="space-y-2">
        <div className="flex items-center gap-3">
          <div className="rounded-2xl border border-border bg-surface-elevated p-2.5 text-muted">
            <AlertTriangle size={18} />
          </div>
          <div>
            <h1 className="text-2xl font-semibold text-foreground">Decision Pulse</h1>
            <p className="text-sm text-muted">
              Izuzeci iz Product Decision porodice sa Zašto i deep linkom. Stale, prazno i greška nisu alert.
            </p>
          </div>
        </div>
      </header>

      {error || metaFailed ? (
        <div
          className="rounded-2xl border border-[var(--error)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--error)]"
          role="alert"
        >
          {error ?? feed?.meta?.errorMessage ?? feed?.meta?.message ?? "Pulse izvor nije pouzdan."}
          <div className="mt-2 text-xs text-muted">KPI nule se ne prikazuju kao validan alert.</div>
        </div>
      ) : loading ? (
        <div className="rounded-2xl border border-dashed border-border bg-surface px-4 py-8 text-center text-sm text-muted">
          Učitavam Decision Pulse...
        </div>
      ) : items.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-border bg-surface px-4 py-8 text-center text-sm text-muted">
          Nema actionable Pulse stavki. Prazan rezultat nije greška.
          {feed?.meta?.message ? <div className="mt-2 text-xs">{feed.meta.message}</div> : null}
        </div>
      ) : (
        <div className="grid gap-3">
          {items.map((item) => (
            <article key={item.id} className="rounded-2xl border border-border bg-surface p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h2 className="text-sm font-semibold text-foreground">{item.title}</h2>
                  <p className="mt-1 text-sm text-muted">{item.whySummary}</p>
                </div>
                <div className="rounded-full border border-border px-2.5 py-1 text-[11px] font-semibold text-muted">
                  {item.recommendationLabel}
                </div>
              </div>
              <div className="mt-3 flex flex-wrap gap-2 text-[11px] text-muted">
                <span className="rounded-full border border-border px-2 py-0.5">svežina: {item.inputFreshnessStatus}</span>
                <span className="rounded-full border border-border px-2 py-0.5">DQ: {item.dataQualityStatus}</span>
                <span className="rounded-full border border-border px-2 py-0.5">{item.tenantScope}</span>
              </div>
              <div className="mt-3">
                <Link className="text-sm font-semibold text-info underline" to={item.deepLink}>
                  Otvori odluku
                </Link>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
