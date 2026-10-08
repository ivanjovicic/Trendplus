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
import { fmtRsd, formatDate, formatDateTime } from "../utils/analyticsFormatters";
import { getDataScope } from "../utils/dataScope";
import { parseEntityIdParam } from "../validation/entityId";
import { dataQualityStatusLabel } from "../utils/analyticsQuality";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";

function freshnessLabel(value: string | null | undefined): string {
  switch ((value ?? "").trim().toLowerCase()) {
    case "fresh": return "Sveže";
    case "stale": return "Zastarelo";
    case "critical": return "Kritično";
    default: return "Nije potvrđeno";
  }
}

function sourceLabel(value: string): string {
  switch (value.trim().toLowerCase()) {
    case "product": return "Odluka o proizvodu";
    case "inventory": return "Zalihe";
    case "supplier": return "Odluka o dobavljaču";
    default: return "Analitika";
  }
}

function scopeLabel(value: string | null | undefined): string {
  switch ((value ?? "").trim().toLowerCase()) {
    case "all": return "Svi podaci";
    case "existing": return "Postojeći artikli";
    case "imported": return "Uvezeni podaci";
    default: return "Opseg nije potvrđen";
  }
}

function tenantScopeLabel(value: string | null | undefined): string {
  return value === "n/a_dedicated" ? "Podaci dostupni u ovoj bazi" : "Obuhvat nije potvrđen";
}

function sourceAvailabilityLabel(feed: DecisionPulseResponse): string {
  if (!feed.meta.success) return "Izvori nisu dostupni";
  return feed.meta.isPartial ? "Izvori su delimično dostupni" : "Izvori su dostupni";
}

function downloadDigestCsv(
  items: DecisionPulseResponse["items"],
  feed: DecisionPulseResponse,
  dataScope: string | null,
) {
  const columns = ["Odluka", "Izvor", "Zašto", "Preporučena akcija", "Kvalitet dokaza", "Svežina signala", "Prema stanju do (UTC)", "Osnova dokaza", "Očekivani uticaj (RSD)", "Traženi period od (UTC)", "Traženi period do (UTC)", "Efektivni period od (UTC)", "Efektivni period do (UTC)", "Pregled kreiran (UTC)", "Poslednje uspešno osveženje (UTC)", "Dostupnost izvora", "Neprimenjeni filteri", "Izostavljeni izvori", "Opseg", "Obuhvat", "Izostavljeno", "Link"];
  const rows = items.map((item) => [
    item.title,
    sourceLabel(item.sourceType),
    item.whySummary,
    item.recommendationLabel,
    dataQualityStatusLabel(item.dataQualityStatus),
    freshnessLabel(item.inputFreshnessStatus),
    item.asOfUtc ?? "",
    evidenceBasisLabel(item.evidenceBasis),
    item.expectedImpactRsd == null ? "" : String(item.expectedImpactRsd),
    feed.meta.requestedPeriodFromUtc ?? "",
    feed.meta.requestedPeriodToUtc ?? "",
    feed.meta.effectivePeriodFromUtc ?? feed.periodFromUtc ?? "",
    feed.meta.effectivePeriodToUtc ?? feed.periodToUtc ?? "",
    feed.generatedAtUtc,
    feed.meta.lastRefreshAtUtc ?? "",
    sourceAvailabilityLabel(feed),
    feed.meta.notAppliedDimensions?.join("; ") ?? "",
    feed.meta.suppressedSources?.join("; ") ?? "",
    scopeLabel(dataScope),
    tenantScopeLabel(feed.tenantScope),
    String(feed.suppressedCount),
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

function parsePulseFilterId(params: URLSearchParams, key: "storeId" | "supplierId"): number | null | undefined {
  const values = params.getAll(key);
  if (values.length === 0) return undefined;
  // Access may use a real negative Int32 ID. Reuse the canonical entity identity parser.
  return values.length === 1 ? parseEntityIdParam(values[0]) : null;
}

export default function DecisionPulsePage() {
  const [searchParams] = useSearchParams();
  const routeContext = searchParams.toString();
  const routeParams = new URLSearchParams(routeContext);
  const activeDataScope = routeParams.get("dataScope") ?? getDataScope();
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
    const fromDate = params.get("fromDate") ?? undefined;
    const toDate = params.get("toDate") ?? undefined;
    const storeId = parsePulseFilterId(params, "storeId");
    const supplierId = parsePulseFilterId(params, "supplierId");
    setFeed(null);
    setDispositions({});
    if (storeId === null || supplierId === null) {
      setLoading(false);
      setError("Filter prodavnice ili dobavljača nije validan. Pregled nije proširen na sve podatke.");
      return;
    }
    getDecisionPulse({
      ...(fromDate ? { fromDate } : {}),
      ...(toDate ? { toDate } : {}),
      ...(storeId !== undefined ? { storeId } : {}),
      ...(supplierId !== undefined ? { supplierId } : {}),
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
              "Pregled odluka trenutno nije dostupan.",
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
      const fromDate = params.get("fromDate") ?? undefined;
      const toDate = params.get("toDate") ?? undefined;
      const storeId = parsePulseFilterId(params, "storeId");
      const supplierId = parsePulseFilterId(params, "supplierId");
      if (storeId === null || supplierId === null) throw new Error("Filter prodavnice ili dobavljača nije validan.");
      await recordDecisionPulseDisposition(item, disposition, {
        ...(fromDate ? { fromDate } : {}),
        ...(toDate ? { toDate } : {}),
        ...(storeId !== undefined ? { storeId } : {}),
        ...(supplierId !== undefined ? { supplierId } : {}),
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
              Najviše 10 dozvoljenih odluka o proizvodima, zalihama i dobavljačima, uz izvor dokaza.
            </p>
          </div>
        </div>
      </header>

      {feed && !loading ? (
        <section className="flex flex-wrap gap-x-5 gap-y-2 rounded-2xl border border-border bg-surface px-4 py-3 text-xs text-muted" aria-label="Poreklo pregleda odluka" data-testid="decision-pulse-feed-provenance">
          <span>Period prikaza: {formatDate(feed.periodFromUtc, "nije dostupan")} – {formatDate(feed.periodToUtc, "nije dostupan")}</span>
          {feed.meta.requestedPeriodFromUtc || feed.meta.requestedPeriodToUtc ? (
            <span>Traženi period: {formatDate(feed.meta.requestedPeriodFromUtc, "početak nije zadat")} – {formatDate(feed.meta.requestedPeriodToUtc, "kraj nije zadat")}</span>
          ) : null}
          <span>Dostupnost izvora: {sourceAvailabilityLabel(feed)}</span>
          <span>Obuhvat: {tenantScopeLabel(feed.tenantScope)}</span>
          <span>Opseg podataka: {scopeLabel(activeDataScope)}</span>
          {feed.meta.notAppliedDimensions?.includes("period:inventory") ? (
            <span>Inventarni izvor: {feed.meta.suppressedSources?.includes("inventory") ? "izostavljen za izabrani period" : "koristi sopstveni signalni period"}</span>
          ) : null}
          <span>Pregled kreiran: {formatDateTime(feed.generatedAtUtc, "nije dostupno")}</span>
          {feed.meta.lastRefreshAtUtc ? <span>Poslednje uspešno osveženje: {formatDateTime(feed.meta.lastRefreshAtUtc, "nije dostupno")}</span> : null}
          <span>Izostavljeno: {feed.suppressedCount}</span>
        </section>
      ) : null}

      {!error && !metaFailed && metaPartial ? (
        <div
          className="rounded-2xl border border-amber-500/50 bg-amber-50/60 px-4 py-5 text-sm text-amber-950 dark:bg-amber-950/20 dark:text-amber-100"
          role="alert"
          data-testid="decision-pulse-partial-warning"
        >
          <div className="font-semibold">Pregled odluka je delimično dostupan</div>
          <div className="mt-1">{metaMessage ?? "Jedan ili više Pulse izvora trenutno nisu dostupni."}</div>
          <div className="mt-1 text-xs">
            Izostavljeno kandidata: {feed?.suppressedCount}. Prikazani podaci mogu biti nepotpuni.
          </div>
          <button
            type="button"
            className="mt-3 rounded-lg border border-current px-3 py-1.5 text-xs font-semibold"
            onClick={() => setReloadToken((current) => current + 1)}
            disabled={loading}
          >
            Ponovo učitaj pregled
          </button>
        </div>
      ) : null}

      {dispositionError ? (
        <div className="rounded-xl border border-[var(--error)] bg-[var(--surface-elevated)] px-4 py-3 text-sm text-[var(--error)]" role="alert">
          {dispositionError}
        </div>
      ) : null}

      {error || metaFailed ? (
        <AnalyticsErrorState
          title="Pregled odluka trenutno nije dostupan"
          message={error ?? metaMessage ?? "Izvor pregleda odluka nije pouzdan."}
          meta={feed?.meta}
          onRetry={() => setReloadToken((current) => current + 1)}
        />
      ) : loading ? (
        <div className="rounded-2xl border border-dashed border-border bg-surface px-4 py-8 text-center text-sm text-muted">
          Učitavam pregled odluka...
        </div>
      ) : items.length === 0 ? (
        <AnalyticsEmptyState
          title="Nema odluka za prikaz."
          message="Prazan rezultat nije greška."
          emptyReason={feed?.meta?.emptyReason}
          meta={feed?.meta}
          variant="no_data"
          showDefaultLinks={false}
          onRetry={() => setReloadToken((current) => current + 1)}
        />
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
              onClick={() => feed && downloadDigestCsv(items, feed, activeDataScope)}
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
                  Izvor: {sourceLabel(item.sourceType)}
                </span>
                <span className="rounded-full border border-border px-2 py-0.5">
                  Prema stanju do: {formatDate(item.asOfUtc, "datum nije dostupan")}
                </span>
                <span className="rounded-full border border-border px-2 py-0.5">
                  Očekivani uticaj: {item.expectedImpactRsd == null ? "nije dostupan" : fmtRsd(item.expectedImpactRsd)}
                </span>
                <span className="rounded-full border border-border px-2 py-0.5">
                  Osnova: {evidenceBasisLabel(item.evidenceBasis)}
                </span>
                <span className="rounded-full border border-border px-2 py-0.5">
                  Kvalitet dokaza: {dataQualityStatusLabel(item.dataQualityStatus)}
                </span>
                <span className="rounded-full border border-border px-2 py-0.5">
                  Svežina signala: {freshnessLabel(item.inputFreshnessStatus)}
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
