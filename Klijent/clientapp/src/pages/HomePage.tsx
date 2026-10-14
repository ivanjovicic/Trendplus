import { Link } from "react-router-dom";
import { AlertTriangle, BarChart3, Boxes, CalendarDays, Microscope, ShieldCheck, Sparkles, Store } from "lucide-react";
import DashboardCards from "../components/dashboard/DashboardCards";
import { useBackendStatus } from "../context/useBackendStatus";

const CORE_ANALYTICS_LINKS = [
  { to: "/analytics", title: "Pregled poslovanja", subtitle: "Prodaja, marža i zalihe za izabrani period", icon: BarChart3 },
  { to: "/analytics/daily-sales", title: "Dnevna prodaja", subtitle: "Prodaja po danima, radnjama i dobavljačima", icon: CalendarDays },
  { to: "/analytics/inventory", title: "Analitika zaliha", subtitle: "Raspoložive zalihe i roba koja stoji", icon: Store },
  { to: "/analytics/supplier", title: "Prodaja po dobavljačima", subtitle: "Prodaja i marža po dobavljaču", icon: Microscope },
  { to: "/analytics/pre-nivelacija-prioriteti", title: "Prioriteti nivelacije", subtitle: "Artikli za proveru pre promene cena", icon: AlertTriangle },
  { to: "/analytics/decision-pulse", title: "Puls odluka", subtitle: "Otvorene odluke i njihova pouzdanost", icon: Sparkles },
];

function MetricTile({
  label,
  value,
  tone = "neutral",
}: {
  label: string;
  value: string;
  tone?: "neutral" | "positive" | "warning" | "critical";
}) {
  const toneClass =
    tone === "positive"
      ? "text-success"
      : tone === "critical"
      ? "text-error"
      : tone === "warning"
      ? "text-warning"
      : "text-contrast";

  return (
    <div className="rounded-xl border border-muted bg-surface-elevated p-4">
      <p className="text-xs uppercase tracking-wide text-muted">{label}</p>
      <p className={`mt-2 text-2xl font-semibold ${toneClass}`}>{value}</p>
    </div>
  );
}

export default function HomePage() {
  const { online, checking, lastCheckedAt } = useBackendStatus();
  const isInitialProbe = checking && lastCheckedAt === null;
  const isRecovering = checking && !online;
  const backendValue = isInitialProbe ? "PROVERA" : isRecovering ? "BUDI SE" : online ? "ONLINE" : "OFFLINE";
  const backendTone = isInitialProbe || isRecovering ? "warning" : online ? "positive" : "critical";

  return (
    <div className="space-y-5">
      <section className="rounded-2xl border border-muted surface p-5 shadow-lg shadow-black/20">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h2 className="text-2xl font-semibold text-contrast">Trendplus</h2>
            <p className="mt-2 max-w-2xl text-sm text-muted">
              Polazna tačka za maloprodajnu analitiku: prodaja, marža, zalihe, dobavljači i nivelacije.
              Brojke se prikazuju samo na izveštajima, uz period i stanje podataka na kojem se zasnivaju.
            </p>
          </div>
          <div className="min-w-[12rem]">
            <MetricTile label="Backend status" value={backendValue} tone={backendTone} />
          </div>
        </div>
      </section>

      <section>
        <div className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted">
          <BarChart3 size={16} className="text-info" />
          Analitika
        </div>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {CORE_ANALYTICS_LINKS.map((item) => {
            const Icon = item.icon;
            return (
              <Link
                key={item.to}
                to={item.to}
                className="group rounded-2xl border border-muted surface-elevated p-4 shadow-lg transition hover:-translate-y-0.5 hover:border-[var(--info)] hover:bg-[var(--surface-light)]"
              >
                <div className="mb-3 inline-flex rounded-lg border border-muted bg-[var(--surface-darker)] p-2 text-[var(--info)] transition group-hover:border-secondary group-hover:opacity-80">
                  <Icon size={18} />
                </div>
                <h3 className="text-base font-semibold text-contrast">{item.title}</h3>
                <p className="mt-1 text-sm text-muted">{item.subtitle}</p>
              </Link>
            );
          })}
        </div>
        <div className="mt-4 flex flex-wrap items-start gap-3 rounded-2xl border border-muted surface-elevated p-4 text-sm text-muted">
          <ShieldCheck size={18} className="mt-0.5 shrink-0 text-info" />
          <p className="flex-1">
            Istorijski izveštaji važe za period koji prikazuju. Preporuke za današnje odluke imaju smisla tek kada su
            podaci sveži, a svaki izveštaj navodi datum poslednjeg uvoza i kvalitet podataka.{" "}
            <Link to="/analytics/data-quality" className="font-medium text-info underline-offset-2 hover:underline">
              Proveri svežinu i kvalitet podataka
            </Link>
          </p>
        </div>
      </section>

      <section>
        <div className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted">
          <Boxes size={16} className="text-info" />
          Brze Akcije
        </div>
        <DashboardCards />
      </section>
    </div>
  );
}
