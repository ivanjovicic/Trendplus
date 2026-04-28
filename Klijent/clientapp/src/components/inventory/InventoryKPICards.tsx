import InfoTip from "../ui/InfoTip";
import { formatCurrency, formatNumber, formatPercent } from "./inventoryUtils";

type InventoryKPICardsProps = {
  totalSku: number | null | undefined;
  totalOnHand: number | null | undefined;
  lowStockCount: number | null | undefined;
  lowStockShare: number;
  avgUnitsPerSku: number;
  totalValue: number;
};

type KpiTone = "cyan" | "green" | "amber" | "blue" | "value";

const KPI_TIPS: Record<string, string> = {
  "Ukupno SKU": "Broj jedinstvenih artikala (SKU) u izabranom filteru (prodavnica + dobavljač). Prikazuje obim asortimana, ne zalihu.",
  "Ukupno na stanju": "Zbir pozitivnih raspolozivih kolicina za sve SKU u filteru. Ne uracunava negativne (korektivne) kolicine niti prednarudzbine.",
  "Niska zaliha": "Broj SKU kod kojih je trenutna kolicina <= minimalnog nivoa (ili <= 2 kom ako minimum nije definisan). Procenat u napomeni je udeo ovih SKU u ukupnom broju.",
  "Prosecno po SKU": "Srednja raspoloziva kolicina po jedinstvenom artiklu u filteru. Formula: ukupno na stanju / ukupno SKU. Visoke vrednosti mogu znaciti prekomerne zalihe.",
  "Procena vrednosti": "Procenjena nabavna vrednost pozitivne raspolozive zalihe. Formula: kolicina × nabavna cena po SKU. Nabavna cena moze biti istorijska ili fallback procena — nije garantovano tacna za sve artikle.",
};

export function InventoryKPICards({
  totalSku,
  totalOnHand,
  lowStockCount,
  lowStockShare,
  avgUnitsPerSku,
  totalValue,
}: InventoryKPICardsProps) {
  const cards: Array<{ label: string; value: string; note: string; tone: KpiTone }> = [
    { label: "Ukupno SKU", value: totalSku != null ? formatNumber(totalSku) : "-", note: "Broj jedinstvenih artikala u izabranom opsegu.", tone: "cyan" },
    { label: "Ukupno na stanju", value: totalOnHand != null ? formatNumber(totalOnHand) : "-", note: "Ukupna pozitivna raspoloziva kolicina robe.", tone: "green" },
    { label: "Niska zaliha", value: lowStockCount != null ? formatNumber(lowStockCount) : "-", note: `${formatPercent(lowStockShare)} fonda je blizu minimuma.`, tone: "amber" },
    { label: "Prosecno po SKU", value: formatNumber(avgUnitsPerSku, 1), note: "Srednja kolicina robe po artiklu.", tone: "blue" },
    { label: "Procena vrednosti", value: formatCurrency(totalValue), note: "Nabavna vrednost pozitivne zalihe.", tone: "value" },
  ];

  const toneClasses: Record<KpiTone, string> = {
    cyan: "border-[rgba(30,200,255,0.34)] text-[var(--info)] shadow-[0_0_28px_rgba(30,200,255,0.10)]",
    green: "border-[rgba(102,255,126,0.34)] text-[var(--success)] shadow-[0_0_28px_rgba(102,255,126,0.12)]",
    amber: "border-[rgba(250,204,21,0.36)] text-[var(--warning)] shadow-[0_0_28px_rgba(250,204,21,0.10)]",
    blue: "border-[rgba(30,200,255,0.28)] text-[var(--text-primary)] shadow-[0_0_28px_rgba(30,200,255,0.08)]",
    value: "border-[rgba(139,255,0,0.46)] text-[var(--success)] shadow-[0_0_34px_rgba(102,255,126,0.18)]",
  };

  return (
    <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
      {cards.map((card) => (
        <article
          key={card.label}
          className={`rounded-[18px] border bg-[linear-gradient(165deg,color-mix(in_srgb,var(--surface-elevated)_86%,var(--success)_14%),var(--surface-darker))] p-5 shadow-sm transition duration-200 hover:-translate-y-0.5 ${toneClasses[card.tone]}`}
        >
          <div className="text-xs font-bold uppercase tracking-[0.22em] text-muted flex items-center gap-1">
            {card.label}
            <InfoTip text={KPI_TIPS[card.label] ?? card.label} />
          </div>
          <div className="mt-4 text-2xl font-extrabold tracking-tight drop-shadow-[0_0_12px_currentColor]">{card.value}</div>
          <p className="mt-3 text-sm leading-5 text-muted">{card.note}</p>
        </article>
      ))}
    </section>
  );
}
