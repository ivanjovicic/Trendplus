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
    accent: "border-[#2f5d9d] bg-[#13223b]",
    tips: ["Ctrl+Enter za nastavak", "Poslednji dobavljači", "Brzi izbor računa"],
  },
  {
    to: "/prodaja",
    title: "Prodaja",
    description: "POS tok sa pretragom artikala, stavkama i obračunom ukupnog iznosa u realnom vremenu.",
    icon: ShoppingCart,
    accent: "border-[#2f6750] bg-[#10261d]",
    tips: ["Live pretraga artikala", "Brzo dodavanje u listu", "Validacija pre slanja"],
  },
  {
    to: "/povracaj",
    title: "Povraćaj robe",
    description: "Wizard za povraćaj sa statusima, istorijom i paginacijom zapisnika.",
    icon: RotateCcw,
    accent: "border-[#7a5832] bg-[#2a1b0d]",
    tips: ["Korak-po-korak flow", "Kontrola stavki", "Istorija povraćaja"],
  },
  {
    to: "/nivelacija",
    title: "Nivelacija cena",
    description: "Promena cena po artiklu sa instant pregledom delte i evidencijom promene.",
    icon: GaugeCircle,
    accent: "border-[#5a4b86] bg-[#1d1931]",
    tips: ["Live delta cene", "Brza pretraga artikla", "Audit trag u dnevniku"],
  },
  {
    to: "/access-import",
    title: "Access import",
    description: "Kontrolisani ETL uvoz sa analizom šeme, statusima i rollback batch-a.",
    icon: Boxes,
    accent: "border-[#5d5f70] bg-[#1a1c28]",
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
              <article key={workflow.to} className="rounded-xl border border-[#2a2b32] bg-[#14161d] p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <h2 className="text-base font-semibold text-[#f3f6ff]">{workflow.title}</h2>
                    <p className="mt-1 text-sm text-[#9aabc7]">{workflow.description}</p>
                  </div>
                  <span className={`shrink-0 rounded-lg border p-2 text-[#c9d9ff] ${workflow.accent}`}>
                    <Icon size={16} />
                  </span>
                </div>

                <div className="mt-3 flex flex-wrap gap-2">
                  {workflow.tips.map((tip) => (
                    <span
                      key={tip}
                      className="rounded-full border border-[#2f323b] bg-[#1a1b1f] px-2.5 py-1 text-[11px] text-[#a8b8d5]"
                    >
                      {tip}
                    </span>
                  ))}
                </div>

                <div className="mt-4">
                  <Link
                    to={workflow.to}
                    className="inline-flex items-center rounded-xl border border-[#3760b7] bg-[#2d4f95] px-3.5 py-2 text-sm font-semibold text-white transition hover:bg-[#3760b7]"
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

