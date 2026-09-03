import { Sparkles, TrendingUp } from "lucide-react";
import InfoTip from "../ui/InfoTip";

const TREND_MODELS = [
  {
    id: "tm-1",
    title: "Trend model Alpha",
    description: "Opšti trend signal za pregled promena u vremenu.",
    explanation: "Naziv modela je vidljiv, ali trenutno nema povezanog backend skora, perioda ni validacionog izveštaja.",
  },
  {
    id: "tm-2",
    title: "Trend model Retail",
    description: "Trend maloprodaje po prodaji i dostupnim poslovnim signalima.",
    explanation: "Za pouzdan rezultat potrebni su izvorni podaci, izabrani period i poređenje sa stvarnim ishodom prodaje.",
  },
  {
    id: "tm-3",
    title: "Trend model Signals",
    description: "Jačina i pokrivenost signala koji ulaze u analitiku.",
    explanation: "Ovaj prikaz ne meri kvalitet signala. Data quality i validacioni endpointi su izvor istine kada budu povezani.",
  },
  {
    id: "tm-4",
    title: "Trend model Demand",
    description: "Trend potražnje koji bi trebalo da koristi istoriju prodaje.",
    explanation: "Bez istorijskog uzorka i backtest rezultata nije moguće reći da li model dobro predviđa potražnju.",
  },
  {
    id: "tm-5",
    title: "Trend model Runtime",
    description: "Operativni status i izvršavanje trend modela.",
    explanation: "Runtime status nije isto što i tačnost modela; ovde trenutno nema povezanog health ili evaluation endpointa.",
  },
];

export default function TrendModelList() {
  return (
    <section className="rounded-2xl border border-border bg-surface-elevated p-4">
      <div className="mb-3 flex items-center gap-2">
        <Sparkles size={16} className="text-info" />
        <h3 className="text-sm font-semibold uppercase tracking-wide text-muted">Trend modeli</h3>
        <InfoTip text="Ovaj panel trenutno opisuje modele, ali ne prikazuje izmišljenu tačnost. Skor je validan tek kada postoji backend izvor, period, uzorak i rezultat evaluacije." />
      </div>
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {TREND_MODELS.map((model) => (
          <article
            key={model.id}
            className="rounded-xl border border-muted bg-surface-darker p-3 transition hover:border-info"
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-1.5">
                <p className="text-sm font-medium text-contrast">{model.title}</p>
                <InfoTip text={model.explanation} />
              </div>
              <TrendingUp size={14} className="text-muted" aria-hidden="true" />
            </div>
            <div className="mt-3">
              <p className="text-sm font-semibold text-warning">Tačnost: nije dostupna</p>
              <p className="mt-1 text-xs text-muted">{model.description}</p>
            </div>
            <div className="mt-3 flex items-center justify-between text-xs text-muted">
              <span>Validacija modela</span>
              <span className="font-semibold text-warning">Nije povezana</span>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

