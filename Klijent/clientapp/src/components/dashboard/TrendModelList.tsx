import { Sparkles, TrendingUp } from "lucide-react";

const TREND_MODELS = [
  { id: "tm-1", title: "Trend model Alpha", score: 84, change: "+4.2%" },
  { id: "tm-2", title: "Trend model Retail", score: 76, change: "+1.1%" },
  { id: "tm-3", title: "Trend model Signals", score: 68, change: "-0.8%" },
  { id: "tm-4", title: "Trend model Demand", score: 71, change: "+2.5%" },
  { id: "tm-5", title: "Trend model Runtime", score: 89, change: "+6.7%" },
];

export default function TrendModelList() {
  return (
    <section className="rounded-2xl border border-border bg-surface-elevated p-4">
      <div className="mb-3 flex items-center gap-2">
        <Sparkles size={16} className="text-info" />
        <h3 className="text-sm font-semibold uppercase tracking-wide text-muted">Trend Models</h3>
      </div>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {TREND_MODELS.map((model) => (
          <article
            key={model.id}
            className="rounded-xl border border-muted bg-surface-darker p-3 transition hover:border-info"
          >
            <div className="flex items-center justify-between">
              <p className="text-sm font-medium text-contrast">{model.title}</p>
              <TrendingUp size={14} className="text-info" />
            </div>
            <div className="mt-3 flex items-end justify-between">
              <p className="text-2xl font-bold text-contrast">{model.score}</p>
              <p className={`text-xs font-semibold ${model.change.startsWith("-") ? "text-error" : "text-success"}`}>
                {model.change}
              </p>
            </div>
            <div className="mt-2 h-1.5 rounded-full bg-surface">
              <div className="h-1.5 rounded-full bg-info" style={{ width: `${model.score}%` }} />
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

