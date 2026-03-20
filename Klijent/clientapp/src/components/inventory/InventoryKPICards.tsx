import { formatCurrency, formatNumber, formatPercent } from "./inventoryUtils";

type InventoryKPICardsProps = {
  totalSku: number | null | undefined;
  totalOnHand: number | null | undefined;
  lowStockCount: number | null | undefined;
  lowStockShare: number;
  avgUnitsPerSku: number;
  totalValue: number;
};

export function InventoryKPICards({
  totalSku,
  totalOnHand,
  lowStockCount,
  lowStockShare,
  avgUnitsPerSku,
  totalValue,
}: InventoryKPICardsProps) {
  const cards = [
    { label: "Ukupno SKU", value: totalSku != null ? formatNumber(totalSku) : "-", note: "Broj jedinstvenih artikala u izabranom opsegu." },
    { label: "Ukupno na stanju", value: totalOnHand != null ? formatNumber(totalOnHand) : "-", note: "Ukupna raspoloziva kolicina robe." },
    { label: "Niska zaliha", value: lowStockCount != null ? formatNumber(lowStockCount) : "-", note: `${formatPercent(lowStockShare)} fonda je blizu minimuma.` },
    { label: "Prosecno po SKU", value: formatNumber(avgUnitsPerSku, 1), note: "Srednja kolicina robe po artiklu." },
    { label: "Procena vrednosti", value: formatCurrency(totalValue), note: "Nabavna vrednost ukupne zalihe." },
  ];

  return (
    <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
      {cards.map((card) => (
        <article key={card.label} className="rounded-[24px] border border-[#252c39] bg-gradient-to-br from-[#151d2c] to-[#10141b] p-5 shadow-[0_16px_40px_-32px_rgba(0,0,0,0.9)]">
          <div className="text-xs uppercase tracking-[0.22em] text-[#90a2bf]">{card.label}</div>
          <div className="mt-4 text-2xl font-semibold text-white">{card.value}</div>
          <p className="mt-3 text-sm leading-5 text-[#9caac3]">{card.note}</p>
        </article>
      ))}
    </section>
  );
}
