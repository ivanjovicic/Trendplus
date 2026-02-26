import { Activity, Boxes, TrendingUp } from "lucide-react";
import DashboardCards from "../components/dashboard/DashboardCards";
import TrendModelList from "../components/dashboard/TrendModelList";

function MetricTile({
  label,
  value,
  tone = "neutral",
}: {
  label: string;
  value: string;
  tone?: "neutral" | "positive" | "warning";
}) {
  const toneClass =
    tone === "positive"
      ? "text-emerald-300"
      : tone === "warning"
      ? "text-amber-300"
      : "text-[#dbe6fb]";

  return (
    <div className="rounded-xl border border-[#2a2b32] bg-[#171920] p-4">
      <p className="text-xs uppercase tracking-wide text-[#8ea0bd]">{label}</p>
      <p className={`mt-2 text-2xl font-semibold ${toneClass}`}>{value}</p>
    </div>
  );
}

export default function HomePage() {
  return (
    <div className="space-y-5">
      <section className="rounded-2xl border border-[#2a2b32] bg-gradient-to-br from-[#1a1b1f] via-[#171922] to-[#14161d] p-5 shadow-[0_20px_40px_-28px_rgba(0,0,0,0.9)]">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h2 className="text-2xl font-semibold text-white">Trendplus Dashboard</h2>
            <p className="mt-2 max-w-2xl text-sm text-[#9aabc7]">
              Centralni pregled za prodaju, inventar, analitiku i trend scraping tokove.
              Izaberi sekciju iz levog menija ili pokreni brzu akciju ispod.
            </p>
          </div>
          <div className="rounded-xl border border-[#2e3f68] bg-[#1b2742] px-3 py-2 text-xs text-[#cfe0ff]">
            Dark Operations Mode
          </div>
        </div>

        <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <MetricTile label="Backend status" value="ONLINE" tone="positive" />
          <MetricTile label="Aktivne sekcije" value="32" />
          <MetricTile label="Import queue" value="5 jobs" tone="warning" />
          <MetricTile label="Trend signal" value="+12.8%" tone="positive" />
        </div>
      </section>

      <section>
        <div className="mb-3 flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-[#b7c7e5]">
          <Boxes size={16} className="text-[#82a8ff]" />
          Quick Actions
        </div>
        <DashboardCards />
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.25fr_0.75fr]">
        <TrendModelList />

        <div className="space-y-4 rounded-2xl border border-[#2a2b32] bg-[#1a1b1f] p-4">
          <div className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-[#c4d2ee]">
            <TrendingUp size={16} className="text-[#83a9ff]" />
            Insight Pulse
          </div>
          <div className="grid gap-3">
            <div className="rounded-xl border border-[#2f323b] bg-[#14161d] p-3">
              <div className="mb-2 flex items-center gap-2 text-[#dbe8ff]">
                <Activity size={14} className="text-emerald-300" />
                <span className="text-sm font-medium">Najveci rast</span>
              </div>
              <p className="text-sm text-[#9eadc6]">Google Shopping trend score je porastao 18% u poslednja 24h.</p>
            </div>
            <div className="rounded-xl border border-[#2f323b] bg-[#14161d] p-3">
              <div className="mb-2 flex items-center gap-2 text-[#dbe8ff]">
                <Activity size={14} className="text-amber-300" />
                <span className="text-sm font-medium">Potencijalni rizik</span>
              </div>
              <p className="text-sm text-[#9eadc6]">3 SKU artikla imaju nizak stock i visoku prodajnu dinamiku.</p>
            </div>
            <div className="rounded-xl border border-[#2f323b] bg-[#14161d] p-3">
              <div className="mb-2 flex items-center gap-2 text-[#dbe8ff]">
                <Activity size={14} className="text-sky-300" />
                <span className="text-sm font-medium">Preporuka</span>
              </div>
              <p className="text-sm text-[#9eadc6]">Pokreni “Nivelacija cena” za top 10 artikala pre sledece kampanje.</p>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}

