import type { SummaryResponse } from "../../services/supplierDecisionHubApi";
import { formatMetricDisplayValue } from "../../utils/analyticsMetricValue";
import {
  formatInteger,
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
  const leadingQualityIndex = summary?.topGrowSuppliers[0]?.supplierQualityIndex;

  const cards = [
    {
      label: "Udeo prihoda bez sniženja",
      value: formatMetricDisplayValue({
        value: summary?.fullPriceRevenueShare ?? null,
        kind: "ratioPercent",
        status: summary ? null : "unavailable",
      }),
      note: `Uzorak: ${formatInteger(supplierCount)} dobavljača`,
    },
    {
      label: "Sell-through bez sniženja",
      value: formatMetricDisplayValue({
        value: summary?.fullPriceSellthrough ?? null,
        kind: "ratioPercent",
        status: summary ? null : "unavailable",
      }),
      note: "Koliko robe odlazi pre prvog spuštanja cene",
    },
    {
      label: "Zavisnost od sniženja",
      value: formatMetricDisplayValue({
        value: summary?.markdownRevenueShare ?? null,
        kind: "ratioPercent",
        status: summary ? null : "unavailable",
      }),
      note: "Veći procenat znači veću zavisnost od prodaje na sniženju",
    },
    {
      label: "Marža pre sniženja",
      value: formatMetricDisplayValue({
        value: summary?.preMarkdownMarginPct ?? null,
        kind: "ratioPercent",
        status: summary ? null : "unavailable",
      }),
      note: "Marža ostvarena dok je roba još na punoj ceni",
    },
    {
      label: "Indeks kvaliteta dobavljača",
      value: formatMetricDisplayValue({
        value: supplierCount > 0 ? leadingQualityIndex ?? null : null,
        kind: "number",
        digits: 1,
        status: supplierCount > 0 ? null : "insufficient_data",
      }) + (supplierCount > 0 && leadingQualityIndex != null ? "/100" : ""),
      note: "Prikazan je vodeći kvalitet u trenutnom filteru",
    },
    {
      label: "Kapital u riziku",
      value: formatMetricDisplayValue({
        value: summary?.capitalAtRisk ?? null,
        kind: "currency",
        status: summary ? null : "unavailable",
      }),
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
