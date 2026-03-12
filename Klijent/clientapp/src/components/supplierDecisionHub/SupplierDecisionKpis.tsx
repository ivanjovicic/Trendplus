import type { SummaryResponse } from "../../services/supplierDecisionHubApi";
import {
  formatCurrency,
  formatInteger,
  formatRatioPercent,
  formatScore,
} from "./utils";

type SupplierDecisionKpisProps = {
  summary: SummaryResponse | null;
  loading?: boolean;
};

type KpiCardProps = {
  label: string;
  value: string;
  note: string;
  loading: boolean;
};

function KpiCard({ label, value, note, loading }: KpiCardProps) {
  return (
    <article className="supplier-decision-kpi-card">
      <div className="supplier-decision-kpi-label">{label}</div>
      <div className="supplier-decision-kpi-value">
        {loading ? <span className="supplier-decision-skeleton-line" /> : value}
      </div>
      <div className="supplier-decision-kpi-note">
        {loading ? <span className="supplier-decision-skeleton-line short" /> : note}
      </div>
    </article>
  );
}

export default function SupplierDecisionKpis({
  summary,
  loading = false,
}: SupplierDecisionKpisProps) {
  const supplierCount = summary?.supplierCount ?? 0;

  const cards = [
    {
      label: "Udeo prihoda bez sniženja",
      value: formatRatioPercent(summary?.fullPriceRevenueShare ?? 0),
      note: `Uzorak: ${formatInteger(supplierCount)} dobavljača`,
    },
    {
      label: "Sell-through bez sniženja",
      value: formatRatioPercent(summary?.fullPriceSellthrough ?? 0),
      note: "Koliko robe odlazi pre prvog spuštanja cene",
    },
    {
      label: "Zavisnost od sniženja",
      value: formatRatioPercent(summary?.markdownRevenueShare ?? 0),
      note: "Veći procenat znači veću zavisnost od prodaje na sniženju",
    },
    {
      label: "Marža pre sniženja",
      value: formatRatioPercent(summary?.preMarkdownMarginPct ?? 0),
      note: "Marža ostvarena dok je roba još na punoj ceni",
    },
    {
      label: "Indeks kvaliteta dobavljača",
      value: formatScore(
        supplierCount > 0
          ? (summary?.topGrowSuppliers[0]?.supplierQualityIndex ?? 0)
          : 0
      ),
      note: "Prikazan je vodeći kvalitet u trenutnom filteru",
    },
    {
      label: "Kapital u riziku",
      value: formatCurrency(summary?.capitalAtRisk ?? 0),
      note: "Vrednost robe koja ostaje zaključana u sporo pokretnim zalihama",
    },
  ];

  return (
    <div className="supplier-decision-kpi-grid">
      {cards.map((card) => (
        <KpiCard
          key={card.label}
          label={card.label}
          value={card.value}
          note={card.note}
          loading={loading}
        />
      ))}
    </div>
  );
}
