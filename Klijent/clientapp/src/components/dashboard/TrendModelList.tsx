import { Sparkles, TrendingUp } from "lucide-react";
import { useEffect, useState } from "react";
import { getForecastBaselineBacktest } from "../../services/analyticsApi";
import type { ForecastBaselineBacktestDto, ForecastBaselineBacktestMetricDto } from "../../types/analytics";
import { formatDate, formatDateTime, fmtNumber, fmtPct, fmtSignedPct } from "../../utils/analyticsFormatters";
import InfoTip from "../ui/InfoTip";

const DEFAULT_HORIZON_DAYS = 14;

const REASON_COPY: Record<string, string> = {
  missing_authoritative_evaluation_snapshot:
    "Nema autoritativnog evaluacionog snapshot-a za izabrani opseg, pa panel ne prikazuje numeričku tačnost.",
  missing_trusted_forecast_materializer:
    "Forecast materializer nije dokazano pouzdan za prikaz numeričke evaluacije.",
  no_paired_forecast_outcome_series:
    "Ne postoji dovoljan upareni forecast/observed niz za pouzdano poređenje.",
  insufficient_observed_stock_comparison_window:
    "Posmatrani prozor nije dovoljan za pouzdanu evaluaciju modela.",
};

type LoadState = {
  loading: boolean;
  payload: ForecastBaselineBacktestDto | null;
  errorMessage: string | null;
};

function isFiniteMetricValue(metric: ForecastBaselineBacktestMetricDto): metric is ForecastBaselineBacktestMetricDto & { value: number } {
  return metric.isAvailable && typeof metric.value === "number" && Number.isFinite(metric.value);
}

function canShowMeasuredScores(payload: ForecastBaselineBacktestDto | null): payload is ForecastBaselineBacktestDto {
  if (!payload) return false;
  if (payload.evaluationStatus !== "ready") return false;
  if (!payload.isAuthoritativeMeasurement) return false;
  if (payload.evaluationFreshnessStatus === "stale") return false;
  return payload.metrics.some(isFiniteMetricValue);
}

function formatMetric(metric: ForecastBaselineBacktestMetricDto): string {
  if (!isFiniteMetricValue(metric)) {
    return "Nije dostupno";
  }

  switch (metric.displayKind) {
    case "percent":
      return fmtPct(metric.value, 1, "Nije dostupno");
    case "signed_percent":
      return fmtSignedPct(metric.value, 1);
    default:
      return metric.unitLabel
        ? `${fmtNumber(metric.value, 1, "Nije dostupno")} ${metric.unitLabel}`
        : fmtNumber(metric.value, 1, "Nije dostupno");
  }
}

function mapFreshnessLabel(status?: string | null): string {
  switch (status) {
    case "fresh":
      return "Sveže";
    case "stale":
      return "Zastarelo";
    default:
      return "Nepoznato";
  }
}

function mapEvaluationLabel(status?: string | null): string {
  switch (status) {
    case "ready":
      return "Spremna";
    case "partial":
      return "Delimična";
    default:
      return "Nije dostupna";
  }
}

function mapComparisonWindowLabel(status?: string | null): string {
  switch (status) {
    case "available":
    case "ready":
      return "Dostupan";
    case "partial":
      return "Delimično dostupan";
    case "stale":
      return "Zastareo";
    case "unavailable":
      return "Nije dostupan";
    default:
      return "Nepoznato";
  }
}

function formatWindowLabel(payload: ForecastBaselineBacktestDto | null): string {
  if (!payload?.windowStartUtc || !payload.windowEndUtc) {
    return "Nije dokazano";
  }

  return `${formatDate(payload.windowStartUtc)} - ${formatDate(payload.windowEndUtc)}`;
}

function getStatusCopy(state: LoadState): { title: string; description: string; toneClass: string } {
  if (state.loading) {
    return {
      title: "Tačnost modela: učitavanje",
      description: "Panel proverava evaluacioni osnov pre nego što prikaže bilo kakvu numeriku.",
      toneClass: "text-info",
    };
  }

  if (state.errorMessage) {
    return {
      title: "Tačnost modela: nije dostupna",
      description: state.errorMessage,
      toneClass: "text-warning",
    };
  }

  if (!state.payload) {
    return {
      title: "Tačnost modela: nije dostupna",
      description: "Podaci o evaluaciji nisu vraćeni. Panel ostaje bez numeričkih tvrdnji.",
      toneClass: "text-warning",
    };
  }

  if (state.payload.evaluationFreshnessStatus === "stale") {
    return {
      title: "Tačnost modela: nije dostupna",
      description: "Poslednja evaluacija je zastarela, pa su numerički skorovi sakriveni dok backend ne vrati svež rezultat.",
      toneClass: "text-warning",
    };
  }

  if (canShowMeasuredScores(state.payload)) {
    return {
      title: "Tačnost modela: dostupna",
      description: "Prikazane metrike dolaze iz potvrđene evaluacije i imaju dokazani period, uzorak i status svežine.",
      toneClass: "text-success",
    };
  }

  return {
    title: "Tačnost modela: nije dostupna",
    description: "Evaluacioni osnov postoji, ali nema dovoljno potvrđenih podataka za pouzdane numeričke skorove.",
    toneClass: "text-warning",
  };
}

function collectLimitations(state: LoadState): string[] {
  if (state.errorMessage) {
    return [state.errorMessage];
  }

  const payload = state.payload;
  if (!payload) {
    return ["Podaci o evaluaciji nisu dostupni."];
  }

  const reasons = payload.missingEvidenceReasons
    .map((reason) => REASON_COPY[reason] ?? "Backend je označio da evaluacioni dokaz nije dovoljan za numerički prikaz.")
    .filter((value, index, all) => all.indexOf(value) === index);

  if (reasons.length > 0) {
    return reasons;
  }

  const metricLimitations = payload.metrics
    .map((metric) => normalizeLimitation(metric.limitation))
    .filter((value): value is string => Boolean(value))
    .filter((value, index, all) => all.indexOf(value) === index);

  if (payload.evaluationFreshnessStatus === "stale") {
    return ["Zastarela evaluacija nije dovoljno sveža za prikaz numeričkih skorova."];
  }

  return metricLimitations.length > 0
    ? metricLimitations
    : ["Numerička evaluacija nije prikazana bez potvrđenog backend osnova."];
}

function normalizeLimitation(value?: string | null): string | null {
  const trimmed = value?.trim();
  if (!trimmed) {
    return null;
  }

  if (!trimmed.includes(" ") || trimmed.length < 12) {
    return "Podaci o evaluaciji nisu validni za numerički prikaz.";
  }

  return trimmed;
}

export default function TrendModelList() {
  const [state, setState] = useState<LoadState>({
    loading: true,
    payload: null,
    errorMessage: null,
  });

  useEffect(() => {
    let cancelled = false;

    setState({
      loading: true,
      payload: null,
      errorMessage: null,
    });

    void getForecastBaselineBacktest({ horizonDays: DEFAULT_HORIZON_DAYS })
      .then((payload) => {
        if (cancelled) return;
        setState({
          loading: false,
          payload,
          errorMessage: null,
        });
      })
      .catch(() => {
        if (cancelled) return;
        setState({
          loading: false,
          payload: null,
          errorMessage: "Evaluacija trend modela trenutno nije dostupna. Panel ostaje bez numeričkih tvrdnji.",
        });
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const status = getStatusCopy(state);
  const visibleMetrics = canShowMeasuredScores(state.payload)
    ? state.payload.metrics.filter(isFiniteMetricValue)
    : [];
  const limitations = collectLimitations(state);

  return (
    <section className="rounded-2xl border border-border bg-surface-elevated p-4">
      <div className="mb-3 flex items-center gap-2">
        <Sparkles size={16} className="text-info" />
        <h3 className="text-sm font-semibold uppercase tracking-wide text-muted">Trend modeli</h3>
        <InfoTip text="Trend modeli prikazuju numeričku tačnost samo kada postoji potvrđena evaluacija sa periodom, uzorkom, svežinom i ograničenjima." />
      </div>

      <div className="rounded-xl border border-muted bg-surface-darker p-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className={`text-sm font-semibold ${status.toneClass}`}>{status.title}</p>
            <p className="mt-1 max-w-2xl text-xs text-muted">{status.description}</p>
          </div>
          <div className="rounded-lg border border-muted bg-surface px-3 py-2 text-right text-xs text-muted">
            <div>Horizont</div>
            <div className="font-semibold text-contrast">
              {state.payload?.horizonDays ?? DEFAULT_HORIZON_DAYS} dana
            </div>
          </div>
        </div>

        <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <article className="rounded-xl border border-muted bg-surface p-3">
            <div className="flex items-center justify-between text-xs text-muted">
              <span>Status evaluacije</span>
              <TrendingUp size={14} aria-hidden="true" className="text-muted" />
            </div>
            <p className="mt-2 text-sm font-semibold text-contrast">
              {mapEvaluationLabel(state.payload?.evaluationStatus)}
            </p>
            <p className="mt-1 text-xs text-muted">Primarni baseline: {state.payload?.primaryBaselineLabel ?? "Nije dokazano"}</p>
          </article>

          <article className="rounded-xl border border-muted bg-surface p-3">
            <div className="text-xs text-muted">Posmatrani prozor</div>
            <p className="mt-2 text-sm font-semibold text-contrast">{formatWindowLabel(state.payload)}</p>
            <p className="mt-1 text-xs text-muted">
              Uzorak: {fmtNumber(state.payload?.aggregates?.sampleCount ?? null, 0, "Nije dokazano")}
            </p>
          </article>

          <article className="rounded-xl border border-muted bg-surface p-3">
            <div className="text-xs text-muted">Poslednja evaluacija</div>
            <p className="mt-2 text-sm font-semibold text-contrast">
              {formatDateTime(state.payload?.lastEvaluatedAtUtc ?? null, "Nije merena")}
            </p>
            <p className="mt-1 text-xs text-muted">
              Generisano: {formatDateTime(state.payload?.generatedAtUtc ?? null, "-")}
            </p>
          </article>

          <article className="rounded-xl border border-muted bg-surface p-3">
            <div className="text-xs text-muted">Svežina</div>
            <p className="mt-2 text-sm font-semibold text-contrast">
              {mapFreshnessLabel(state.payload?.evaluationFreshnessStatus)}
            </p>
            <p className="mt-1 text-xs text-muted">
              Status prozora: {mapComparisonWindowLabel(state.payload?.comparisonWindowStatus)}
            </p>
          </article>
        </div>
      </div>

      {visibleMetrics.length > 0 ? (
        <div className="mt-4 grid gap-3 md:grid-cols-3">
          {visibleMetrics.map((metric) => (
            <article key={metric.metricId} className="rounded-xl border border-muted bg-surface-darker p-3">
              <div className="text-xs uppercase tracking-wide text-muted">{metric.label}</div>
              <p className="mt-2 text-xl font-semibold text-success">{formatMetric(metric)}</p>
              <p className="mt-1 text-xs text-muted">Prikazano samo zato što je backend vratio autoritativan i svež rezultat.</p>
            </article>
          ))}
        </div>
      ) : (
        <div className="mt-4 rounded-xl border border-muted bg-surface p-4">
          <div className="text-xs font-semibold uppercase tracking-wide text-muted">Ograničenja evaluacije</div>
          <ul className="mt-3 space-y-2 text-sm text-muted">
            {limitations.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

