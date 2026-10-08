import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { AlertTriangle } from "lucide-react";
import {
  getDecisionPulse,
  getDecisionPulseDispositions,
  recordDecisionPulseDisposition,
  type DecisionPulseDisposition,
  type DecisionPulseResponse,
} from "../services/decisionPulseApi";
import { getSafeAnalyticsErrorMessage } from "../utils/analyticsErrorMessages";
import { getAnalyticsMetaMessage } from "../utils/analyticsResponseMeta";
import { fmtRsd, formatDate } from "../utils/analyticsFormatters";
import { getDataScope } from "../utils/dataScope";

function downloadDigestCsv(items: DecisionPulseResponse["items"]) {
  const columns = ["Odluka", "Izvor", "Zašto", "Preporučena akcija", "Prema stanju do (UTC)", "Osnova dokaza", "Očekivani uticaj (RSD)", "Link"];
  const rows = items.map((item) => [
    item.title,
    item.sourceType,
    item.whySummary,
    item.recommendationLabel,
    item.asOfUtc ?? "",
    evidenceBasisLabel(item.evidenceBasis),
    item.expectedImpactRsd == null ? "" : String(item.expectedImpactRsd),
    new URL(item.deepLink, window.location.origin).toString(),
  ]);
  const csv = [columns, ...rows]
    .map((row) => row.map((value) => `"${String(value).replaceAll('"', '""')}"`).join(","))
    .join("\r\n");
  const blob = new Blob(["\uFEFF", csv], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = "pregled-odluka.csv";
  link.click();
  URL.revokeObjectURL(url);
}

function evidenceBasisLabel(value: string | null | undefined): string {
  switch (value) {
    case "product_decision_period": return "Period Product Decision signala";
    case "inventory_signal_window": return "Prozor inventarnog signala";
    case "supplier_scorecard_effective_period": return "Efektivni period dobavljačkog pregleda";
    default: return "Dostupni izvorni dokaz";
  }
}

export default function DecisionPulsePage() {
  const [searchParams] = useSearchParams();
  const routeContext = searchParams.toString();
  const [feed, setFeed] = useState<DecisionPulseResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [dispositions, setDispositions] = useState<Record<string, DecisionPulseDisposition>>({});
  const [savingId, setSavingId] = useState<string | null>(null);
  const [dispositionError, setDispositionError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    const params = new URLSearchParams(routeContext);
    const storeId = Number(params.get("storeId"));
    const supplierId = Number(params.get("supplierId"));
    getDecisionPulse({
      ...(Number.isInteger(storeId) && storeId > 0 ? { storeId } : {}),
      ...(Number.isInteger(supplierId) && supplierId > 0 ? { supplierId } : {}),
      dataScope: params.get("dataScope") ?? getDataScope(),
    })
      .then((response) => {
        if (!cancelled) {
          setFeed(response);
          getDecisionPulseDispositions(response.items)
            .then((saved) => { if (!cancelled) setDispositions(saved); })
            .catch(() => { if (!cancelled) setDispositions({}); });
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setFeed(null);
          setError(
            getSafeAnalyticsErrorMessage(
              err instanceof Error ? err.message : null,
              null,
              "Decision Pulse nije dostupan.",
            ),
          );
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [reloadToken, routeContext]);

  const metaFailed = feed?.meta?.success === false;
  const metaPartial = !metaFailed && (
    feed?.meta?.isPartial === true || Boolean(feed?.meta?.warningCode)
  );
  const items = feed?.items ?? [];
  const metaMessage = getAnalyticsMetaMessage(feed?.meta);

  const saveDisposition = async (item: DecisionPulseResponse["items"][number], disposition: DecisionPulseDisposition) => {
    setSavingId(item.id);
    setDispositionError(null);
    try {
      const params = new URLSearchParams(routeContext);
      const storeId = Number(params.get("storeId"));
      const supplierId = Number(params.get("supplierId"));
      await recordDecisionPulseDisposition(item, disposition, {
        ...(Number.isInteger(storeId) && storeId > 0 ? { storeId } : {}),
        ...(Number.isInteger(supplierId) && supplierId > 0 ? { supplierId } : {}),
        dataScope: params.get("dataScope") ?? getDataScope(),
      });
      setDispositions((current) => ({ ...current, [item.id]: disposition }));
    } catch (err) {
      setDispositionError(getSafeAnalyticsErrorMessage(
        err instanceof Error ? err.message : null,
        null,
        "Odluka nije sačuvana.",
      ));
    } finally {
      setSavingId(null);
    }
  };

  return (
    <div className="mx-auto max-w-5xl space-y-6 p-6">
      <header className="space-y-2">
        <div className="flex items-center gap-3">
          <div className="rounded-2xl border border-border bg-surface-elevated p-2.5 text-muted">
            <AlertTriangle size={18} />
          </div>
          <div>
            <h1 className="text-2xl font-semibold text-foreground">Puls odluka</h1>
            <p className="text-sm text-muted">
              Najviše 10 dozvoljenih odluka iz Product Decision, zaliha i dobavljača, uz izvor dokaza.
            </p>
          </div>
        </div>
      </header>

      {!error && !metaFailed && metaPartial ? (
        <div
          className="rounded-2xl border border-amber-500/50 bg-amber-50/60 px-4 py-5 text-sm text-amber-950 dark:bg-amber-950/20 dark:text-amber-100"
          role="alert"
          data-testid="decision-pulse-partial-warning"
        >
          <div className="font-semibold">Decision Pulse je delimično dostupan</div>
          <div className="mt-1">{metaMessage ?? "Jedan ili više Pulse izvora trenutno nisu dostupni."}</div>
          <div className="mt-1 text-xs">
            Potisnuto kandidata: {feed?.suppressedCount ?? 0}. Prikazani podaci mogu biti nepotpuni.
          </div>
          <button
            type="button"
            className="mt-3 rounded-lg border border-current px-3 py-1.5 text-xs font-semibold"
            onClick={() => setReloadToken((current) => current + 1)}
            disabled={loading}
          >
            Ponovo učitaj Decision Pulse
          </button>
        </div>
      ) : null}

      {dispositionError ? (
        <div className="rounded-xl border border-[var(--error)] bg-[var(--surface-elevated)] px-4 py-3 text-sm text-[var(--error)]" role="alert">
          {dispositionError}
        </div>
      ) : null}

      {error || metaFailed ? (
        <div
          className="rounded-2xl border border-[var(--error)] bg-[var(--surface-elevated)] px-4 py-8 text-center text-sm text-[var(--error)]"
          role="alert"
        >
          {error ?? metaMessage ?? "Pulse izvor nije pouzdan."}
          <div className="mt-2 text-xs text-muted">KPI nule se ne prikazuju kao validan alert.</div>
        </div>
      ) : loading ? (
        <div className="rounded-2xl border border-dashed border-border bg-surface px-4 py-8 text-center text-sm text-muted">
          Učitavam Decision Pulse...
        </div>
      ) : items.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-border bg-surface px-4 py-8 text-center text-sm text-muted">
          Nema actionable Pulse stavki. Prazan rezultat nije greška.
          {metaMessage ? <div className="mt-2 text-xs">{metaMessage}</div> : null}
        </div>
      ) : (
        <div className="space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-surface px-4 py-3">
            <div className="text-sm font-medium text-foreground" data-testid="decision-pulse-currentness">
              {feed?.currentness === "current"
                ? "Pregled odluka za aktuelno stanje"
                : `Pregled odluka prema stanju do ${formatDate(feed?.asOfUtc, "datum nije dostupan")}`}
            </div>
            <button
              type="button"
              className="rounded-lg border border-border px-3 py-1.5 text-xs font-semibold text-foreground"
              onClick={() => downloadDigestCsv(items)}
            >
              Preuzmi CSV
            </button>
          </div>
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
                <span className="rounded-full border border-border px-2 py-0.5">
                  Prema stanju do: {formatDate(item.asOfUtc, "datum nije dostupan")}
                </span>
                <span className="rounded-full border border-border px-2 py-0.5">
                  Očekivani uticaj: {item.expectedImpactRsd == null ? "nije dostupan" : fmtRsd(item.expectedImpactRsd)}
                </span>
                <span className="rounded-full border border-border px-2 py-0.5">
                  Osnova: {evidenceBasisLabel(item.evidenceBasis)}
                </span>
              </div>
              <div className="mt-3">
                <Link className="text-sm font-semibold text-info underline" to={item.deepLink}>
                  Otvori odluku
                </Link>
              </div>
              <fieldset className="mt-4 border-t border-border pt-3">
                <legend className="text-xs font-semibold text-muted">Evidentiraj vlasničku odluku</legend>
                <div className="mt-2 flex flex-wrap gap-2">
                  {([
                    ["accepted", "Prihvaćeno"],
                    ["deferred", "Odloženo"],
                    ["rejected", "Odbijeno"],
                    ["ignored", "Ignorisano"],
                  ] as const).map(([value, label]) => (
                    <button
                      key={value}
                      type="button"
                      className={`rounded-lg border px-3 py-1.5 text-xs font-semibold ${dispositions[item.id] === value ? "border-info bg-info/10 text-info" : "border-border text-foreground"}`}
                      aria-pressed={dispositions[item.id] === value}
                      disabled={savingId === item.id}
                      onClick={() => void saveDisposition(item, value)}
                    >
                      {savingId === item.id ? "Čuvam…" : label}
                    </button>
                  ))}
                </div>
                <p className="mt-2 text-[11px] text-muted">Čuvanje koristi postojeću admin zaštitu. Stavke bez izvornog prioriteta ostaju nerangirane, a evidentiranje nije merenje ishoda.</p>
              </fieldset>
            </article>
          ))}
          </div>
        </div>
      )}

    </div>
  );
}
