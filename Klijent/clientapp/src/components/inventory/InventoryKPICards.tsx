import InfoTip from "../ui/InfoTip";
import { formatCurrency, formatNumber, formatPercent } from "./inventoryUtils";
import KpiExplainButton from "../analytics/KpiExplainButton";
import type { AnalyticsMetricKey } from "../../utils/analyticsMetricDefinitions";

type InventoryKPICardsProps = {
  totalSku: number | null | undefined;
  totalOnHand: number | null | undefined;
  lowStockCount: number | null | undefined;
  lowStockShare: number;
  avgUnitsPerSku: number;
  totalValue: number;
};

type KpiTone = "info" | "success" | "warning" | "neutral" | "value";

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
  const cards: Array<{ label: string; value: string; note: string; tone: KpiTone; metricKey: AnalyticsMetricKey }> = [
    { label: "Ukupno SKU", value: totalSku != null ? formatNumber(totalSku) : "-", note: "Broj jedinstvenih artikala u izabranom opsegu.", tone: "info", metricKey: "skuCount" },
    { label: "Ukupno na stanju", value: totalOnHand != null ? formatNumber(totalOnHand) : "-", note: "Ukupna pozitivna raspoloziva kolicina robe.", tone: "success", metricKey: "onHandUnits" },
    { label: "Niska zaliha", value: lowStockCount != null ? formatNumber(lowStockCount) : "-", note: `${formatPercent(lowStockShare)} fonda je blizu minimuma.`, tone: "warning", metricKey: "lowStockCount" },
    { label: "Prosecno po SKU", value: formatNumber(avgUnitsPerSku, 1), note: "Srednja kolicina robe po artiklu.", tone: "neutral", metricKey: "avgUnitsPerSku" },
    { label: "Procena vrednosti", value: formatCurrency(totalValue), note: "Nabavna vrednost pozitivne zalihe.", tone: "value", metricKey: "inventoryTotalValue" },
  ];

  return (
    <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
      {cards.map((card) => (
        <article
          key={card.label}
          className={`analytics-kpi-card analytics-kpi-card--tone-${card.tone}`}
        >
          <div className="analytics-kpi-card__label">
            {card.label}
            <InfoTip text={KPI_TIPS[card.label] ?? card.label} />
          </div>
          <div className="analytics-kpi-card__value">{card.value}</div>
          <p className="analytics-kpi-card__note">{card.note}</p>
          <KpiExplainButton metricKey={card.metricKey} />
        </article>
      ))}
    </section>
  );
}
