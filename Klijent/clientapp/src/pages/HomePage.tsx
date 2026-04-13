import { Activity, Boxes, TrendingUp } from "lucide-react";
import DashboardCards from "../components/dashboard/DashboardCards";
import TrendModelList from "../components/dashboard/TrendModelList";
import { useBackendStatus } from "../context/useBackendStatus";

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
            <h2 className="text-2xl font-semibold text-contrast">Trendplus Dashboard</h2>
            <p className="mt-2 max-w-2xl text-sm text-muted">
              Centralni pregled za prodaju, inventar, analitiku i trend scraping tokove.
              Izaberi sekciju iz levog menija ili pokreni brzu akciju ispod.
            </p>
          </div>
          <div className="rounded-xl border border-info bg-info/10 px-3 py-2 text-xs text-info">
            Trendplus Core
          </div>
        </div>

        <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="Backend status" value={backendValue} tone={backendTone} />
          <MetricTile label="Aktivne sekcije" value="32" />
          <MetricTile label="Import jobs" value="5" tone="warning" />
          <MetricTile label="Trend signal" value="+12.8%" tone="positive" />
        </div>
      </section>

      <section>
        <div className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted">
          <Boxes size={16} className="text-info" />
          Brze Akcije
        </div>
        <DashboardCards />
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.25fr_0.75fr]">
        <TrendModelList />

        <div className="space-y-4 rounded-2xl border border-muted surface-elevated p-4">
          <div className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-muted">
            <TrendingUp size={16} className="text-info" />
            Insight Pulse
          </div>
          <div className="grid gap-3">
            <div className="rounded-xl border border-muted bg-surface p-3">
              <div className="mb-2 flex items-center gap-2 text-contrast">
                <Activity size={14} className="text-success" />
                <span className="text-sm font-medium">Najveci rast</span>
              </div>
              <p className="text-sm text-muted">Google Shopping trend score je porastao 18% u poslednja 24h.</p>
            </div>
            <div className="rounded-xl border border-muted bg-surface p-3">
              <div className="mb-2 flex items-center gap-2 text-contrast">
                <Activity size={14} className="text-error" />
                <span className="text-sm font-medium">Potencijalni rizik</span>
              </div>
              <p className="text-sm text-muted">3 SKU artikla imaju nizak stock i visoku prodajnu dinamiku.</p>
            </div>
            <div className="rounded-xl border border-muted bg-surface p-3">
              <div className="mb-2 flex items-center gap-2 text-contrast">
                <Activity size={14} className="text-info" />
                <span className="text-sm font-medium">Preporuka</span>
              </div>
              <p className="text-sm text-muted">Pokreni “Nivelacija cena” za top 10 artikala pre sledece kampanje.</p>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}

