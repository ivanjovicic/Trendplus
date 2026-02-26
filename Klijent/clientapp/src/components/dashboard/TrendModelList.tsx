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
    <section className="rounded-2xl border border-[#2a2b32] bg-[#1a1b1f] p-4">
      <div className="mb-3 flex items-center gap-2">
        <Sparkles size={16} className="text-[#83a9ff]" />
        <h3 className="text-sm font-semibold uppercase tracking-wide text-[#c4d2ee]">Trend Models</h3>
      </div>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {TREND_MODELS.map((model) => (
          <article
            key={model.id}
            className="rounded-xl border border-[#31333b] bg-[#14161d] p-3 transition hover:border-[#456fc4]"
          >
            <div className="flex items-center justify-between">
              <p className="text-sm font-medium text-[#d9e3f7]">{model.title}</p>
              <TrendingUp size={14} className="text-[#5f8ff4]" />
            </div>
            <div className="mt-3 flex items-end justify-between">
              <p className="text-2xl font-bold text-white">{model.score}</p>
              <p className={`text-xs font-semibold ${model.change.startsWith("-") ? "text-amber-300" : "text-emerald-300"}`}>
                {model.change}
              </p>
            </div>
            <div className="mt-2 h-1.5 rounded-full bg-[#232630]">
              <div className="h-1.5 rounded-full bg-gradient-to-r from-[#4f8cff] to-[#6ea4ff]" style={{ width: `${model.score}%` }} />
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

