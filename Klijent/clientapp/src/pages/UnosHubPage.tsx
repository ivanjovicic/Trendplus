import { Link } from "react-router-dom";
import { ClipboardPlus, GaugeCircle, PackagePlus, RotateCcw, ShoppingCart, Boxes } from "lucide-react";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel } from "../components/inventory/InventoryPageShell";

type WorkflowCard = {
  to: string;
  title: string;
  description: string;
  icon: typeof ShoppingCart;
  accent: string;
  tips: string[];
};

const WORKFLOWS: WorkflowCard[] = [
  {
    to: "/unos-robe",
    title: "Unos robe",
    description: "Brz prijem robe po dobavljaču uz selekciju računa i prelaz na unos stavki.",
    icon: PackagePlus,
    accent: "border-info bg-surface-elevated text-info",
    tips: ["Ctrl+Enter za nastavak", "Poslednji dobavljači", "Brzi izbor računa"],
  },
  {
    to: "/prodaja",
    title: "Prodaja",
    description: "POS tok sa pretragom artikala, stavkama i obračunom ukupnog iznosa u realnom vremenu.",
    icon: ShoppingCart,
    accent: "border-success bg-surface-elevated text-success",
    tips: ["Live pretraga artikala", "Brzo dodavanje u listu", "Validacija pre slanja"],
  },
  {
    to: "/povracaj",
    title: "Povraćaj robe",
    description: "Wizard za povraćaj sa statusima, istorijom i paginacijom zapisnika.",
    icon: RotateCcw,
    accent: "border-warning bg-surface-elevated text-warning",
    tips: ["Korak-po-korak flow", "Kontrola stavki", "Istorija povraćaja"],
  },
  {
    to: "/nivelacija",
    title: "Nivelacija cena",
    description: "Promena cena po artiklu sa instant pregledom delte i evidencijom promene.",
    icon: GaugeCircle,
    accent: "border-info bg-surface-elevated text-info",
    tips: ["Live delta cene", "Brza pretraga artikla", "Audit trag u dnevniku"],
  },
  {
    to: "/access-import",
    title: "Access import",
    description: "Kontrolisani ETL uvoz sa analizom šeme, statusima i rollback batch-a.",
    icon: Boxes,
    accent: "border-border bg-surface-elevated text-muted",
    tips: ["Analiza pre importa", "Batch istorija", "Sigurno brisanje"],
  },
];

export default function UnosHubPage() {
  return (
    <InventoryPageShell
      icon={ClipboardPlus}
      title="Centar unosa"
      subtitle="Jedinstven ulaz za operativni rad: prijem, prodaja, povraćaj, nivelacija i import."
    >
      <InventoryKpiRow
        items={[
          { label: "Workflow moduli", value: `${WORKFLOWS.length}` },
          { label: "Režim", value: "Operativni", tone: "positive" },
          { label: "UX fokus", value: "Brzina + tačnost" },
          { label: "Standard", value: "Enterprise" },
        ]}
      />

      <InventoryPanel>
        <div className="grid gap-3 lg:grid-cols-2">
          {WORKFLOWS.map((workflow) => {
            const Icon = workflow.icon;
            return (
              <article key={workflow.to} className="rounded-xl border border-border bg-surface p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <h2 className="text-base font-semibold text-foreground">{workflow.title}</h2>
                    <p className="mt-1 text-sm text-muted">{workflow.description}</p>
                  </div>
                  <span className={`shrink-0 rounded-lg border p-2 ${workflow.accent}`}>
                    <Icon size={16} />
                  </span>
                </div>

                <div className="mt-3 flex flex-wrap gap-2">
                  {workflow.tips.map((tip) => (
                    <span
                      key={tip}
                      className="rounded-full border border-border bg-surface px-2.5 py-1 text-[11px] text-muted"
                    >
                      {tip}
                    </span>
                  ))}
                </div>

                <div className="mt-4">
                  <Link
                    to={workflow.to}
                    className="inline-flex items-center rounded-xl border bg-primary px-3.5 py-2 text-sm font-semibold text-on-primary transition hover:bg-primary-hover"
                  >
                    Otvori modul
                  </Link>
                </div>
              </article>
            );
          })}
        </div>
      </InventoryPanel>
    </InventoryPageShell>
  );
}

