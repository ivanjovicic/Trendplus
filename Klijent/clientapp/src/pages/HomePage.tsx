import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  AlertTriangle,
  ArrowRight,
  BarChart3,
  Boxes,
  ClipboardCheck,
  Microscope,
  ShieldCheck,
  Sparkles,
  Store,
  Zap,
} from "lucide-react";
import DashboardCards from "../components/dashboard/DashboardCards";
import { useBackendStatus } from "../context/useBackendStatus";
import { getAnalyticsRefreshStatus } from "../services/analyticsApi";
import type { AnalyticsRefreshStatus } from "../types/analytics";
import { toCalendarDate } from "../utils/analyticsDateRanges";

export const HOME_BUSINESS_CARDS = [
  { to: "/analytics", title: "Pregled poslovanja", question: "Kako posluje prodaja u izabranom periodu?", icon: BarChart3 },
  { to: "/analytics/decision-pulse", title: "Puls odluka", question: "Koje otvorene odluke traže pažnju i koliko su pouzdane?", icon: Zap },
  { to: "/analytics/inventory", title: "Analitika zaliha", question: "Gde roba stoji, a gde je nema dovoljno?", icon: Store },
  { to: "/analytics/supplier", title: "Prodaja po dobavljačima", question: "Koji dobavljači nose prodaju i maržu?", icon: Microscope },
  { to: "/analytics/pre-nivelacija-prioriteti", title: "Prioriteti nivelacije", question: "Koje artikle proveriti pre promene cene?", icon: Sparkles },
  { to: "/analytics/data-quality", title: "Kvalitet podataka", question: "Da li su podaci dovoljno potpuni za odluku?", icon: AlertTriangle },
] as const;

export const HOME_FRESHNESS_FALLBACK = "Poslednji potvrđeni period nije dostupan";

type FreshnessState =
  | { kind: "loading" }
  | { kind: "error" }
  | { kind: "loaded"; status: AnalyticsRefreshStatus };

const importTimeFormatter = new Intl.DateTimeFormat("sr-RS", {
  timeZone: "Europe/Belgrade",
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

function formatCalendarDate(value: string | null | undefined): string | null {
  // Sales dates are stored as wall-clock values; keep the stored calendar day.
  const day = toCalendarDate(value);
  if (!day) return null;
  const [year, month, date] = day.split("-");
  return `${date}.${month}.${year}.`;
}

function formatImportTime(value: string | null | undefined): string | null {
  if (!value) return null;
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return null;
  return `${importTimeFormatter.format(parsed)} (Beograd)`;
}

function describeFreshness(status: string | null | undefined): { label: string; tone: "positive" | "warning" | "critical" | "neutral" } {
  switch ((status ?? "").toLowerCase()) {
    case "fresh":
      return { label: "Uvoz podataka je svež", tone: "positive" };
    case "stale":
      return { label: "Podaci su zastareli — proveri pre današnjih odluka", tone: "warning" };
    case "critical":
      return { label: "Podaci su kritično zastareli ili osvežavanje nije uspelo", tone: "critical" };
    default:
      return { label: "Svežina podataka nije potvrđena", tone: "neutral" };
  }
}

const TONE_CLASS: Record<"positive" | "warning" | "critical" | "neutral", string> = {
  positive: "text-success",
  warning: "text-warning",
  critical: "text-error",
  neutral: "text-muted",
};

function DataFreshnessNote({ state }: { state: FreshnessState }) {
  const dataQualityLink = (
    <Link to="/analytics/data-quality" className="font-medium text-info underline-offset-2 hover:underline">
      Kvalitet podataka
    </Link>
  );

  if (state.kind === "loading") {
    return (
      <p className="text-sm text-muted" role="status" aria-live="polite" data-testid="home-freshness">
        Proveravam poslednji potvrđeni period prodaje…
      </p>
    );
  }

  const status = state.kind === "loaded" ? state.status : null;
  const from = formatCalendarDate(status?.observedSalesPeriodFromUtc);
  const to = formatCalendarDate(status?.observedSalesPeriodToUtc);
  const importedAt = formatImportTime(status?.lastSuccessfulImportAtUtc);
  const freshness = describeFreshness(status ? status.dataFreshnessStatus : null);

  return (
    <div className="space-y-1 text-sm" role="status" aria-live="polite" data-testid="home-freshness">
      {from && to ? (
        <p className="text-contrast">
          Poslednji potvrđeni period prodaje iz Access uvoza: <strong>{from} – {to}</strong>
          <span className="text-muted"> (ukupno, ne po radnji ili izvoru podataka)</span>
        </p>
      ) : (
        <p className="text-contrast">
          {HOME_FRESHNESS_FALLBACK}. Detalje proveri na stranici {dataQualityLink}.
        </p>
      )}
      {status ? (
        <p className="text-muted">
          Poslednji uspešan uvoz: {importedAt ?? "nije potvrđen"} ·{" "}
          <span className={TONE_CLASS[freshness.tone]} data-testid="home-freshness-status">{freshness.label}</span>
        </p>
      ) : (
        <p className="text-muted" data-testid="home-freshness-status">
          Status osvežavanja trenutno nije moguće proveriti.
        </p>
      )}
    </div>
  );
}

function BackendStatusBadge() {
  const { online, checking, lastCheckedAt } = useBackendStatus();
  const isInitialProbe = checking && lastCheckedAt === null;
  const isRecovering = checking && !online;
  const value = isInitialProbe ? "PROVERA" : isRecovering ? "BUDI SE" : online ? "ONLINE" : "OFFLINE";
  const tone = isInitialProbe || isRecovering ? "warning" : online ? "positive" : "critical";
  const isOffline = value === "OFFLINE";

  return (
    <div
      className={`inline-flex items-center gap-2 rounded-lg border px-3 py-1.5 text-xs ${
        isOffline ? "border-[var(--error)] bg-[var(--surface-darker)]" : "border-muted"
      }`}
      role={isOffline ? "alert" : "status"}
      data-testid="home-backend-status"
    >
      <span className="uppercase tracking-wide text-muted">Backend status</span>
      <span className={`font-semibold ${TONE_CLASS[tone]}`}>{value}</span>
      {isOffline ? <span className="text-error">Podaci se ne mogu učitati dok se server ne vrati.</span> : null}
    </div>
  );
}

export default function HomePage() {
  const [freshness, setFreshness] = useState<FreshnessState>({ kind: "loading" });

  useEffect(() => {
    // One read on mount; AppLayout does not load refresh status on "/" and Home adds no polling.
    let active = true;
    getAnalyticsRefreshStatus()
      .then((status) => {
        if (active) setFreshness(status ? { kind: "loaded", status } : { kind: "error" });
      })
      .catch(() => {
        if (active) setFreshness({ kind: "error" });
      });
    return () => {
      active = false;
    };
  }, []);

  return (
    <div className="space-y-6">
      <section className="rounded-2xl border border-muted surface p-5 shadow-lg shadow-black/20" aria-labelledby="home-title">
        <h2 id="home-title" className="text-2xl font-semibold text-contrast">Trendplus</h2>
        <p className="mt-2 max-w-2xl text-sm text-muted">
          Početak radnog dana: proveri do kog datuma su podaci potvrđeni, pa otvori ekran koji odgovara na tvoje
          poslovno pitanje. Brojke se prikazuju samo na izveštajima, uz period i stanje podataka.
        </p>
        <div className="mt-3 flex items-start gap-2">
          <ShieldCheck size={16} className="mt-0.5 shrink-0 text-info" aria-hidden="true" />
          <DataFreshnessNote state={freshness} />
        </div>
        <Link
          to="/analytics"
          className="mt-4 inline-flex items-center gap-2 rounded-xl border border-[var(--info)] px-4 py-2 text-sm font-semibold text-info hover:bg-[var(--surface-light)]"
          data-testid="home-primary-cta"
        >
          Otvori pregled poslovanja
          <ArrowRight size={16} aria-hidden="true" />
        </Link>
      </section>

      <section aria-labelledby="home-business-title">
        <h3 id="home-business-title" className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted">
          <ClipboardCheck size={16} className="text-info" aria-hidden="true" />
          Poslovna pitanja
        </h3>
        <ul className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3" data-testid="home-business-cards">
          {HOME_BUSINESS_CARDS.map((item) => {
            const Icon = item.icon;
            return (
              <li key={item.to}>
                <Link
                  to={item.to}
                  className="group block h-full rounded-2xl border border-muted surface-elevated p-4 shadow-lg transition hover:-translate-y-0.5 hover:border-[var(--info)] hover:bg-[var(--surface-light)]"
                >
                  <div className="mb-3 inline-flex rounded-lg border border-muted bg-[var(--surface-darker)] p-2 text-[var(--info)]">
                    <Icon size={18} aria-hidden="true" />
                  </div>
                  <span className="block text-base font-semibold text-contrast">{item.title}</span>
                  <span className="mt-1 block text-sm text-muted">{item.question}</span>
                </Link>
              </li>
            );
          })}
        </ul>
      </section>

      <section aria-labelledby="home-ops-title">
        <h3 id="home-ops-title" className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted">
          <Boxes size={16} className="text-info" aria-hidden="true" />
          Operativne akcije
        </h3>
        <DashboardCards />
      </section>

      <section aria-label="Stanje servera">
        <BackendStatusBadge />
      </section>
    </div>
  );
}
