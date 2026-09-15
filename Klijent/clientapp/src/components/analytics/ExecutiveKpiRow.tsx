import { Link } from "react-router-dom";
import InfoTip from "../ui/InfoTip";
import { fmtNumber, fmtRsd } from "../../utils/analyticsFormatters";
import { dataQualityStatusLabel } from "../../utils/analyticsQuality";
import KpiExplainButton from "./KpiExplainButton";
import type { AnalyticsMetricKey } from "../../utils/analyticsMetricDefinitions";

type Tone = "good" | "warning" | "critical" | "neutral" | "insufficient_data";

type Props = {
  loading: boolean;
  totalRevenue: number | null;
  marginContributionRsd: number | null;
  totalUnits: number | null;
  inventoryDangerValueRsd: number | null;
  dataQualityTone: Tone;
  dataQualityStatus: string | null;
  missingSupplierCount: number | null;
  missingCostCount: number | null;
  readinessLabel?: string | null;
  dataQualityScopeLabel?: string | null;
};

type MetricPresentation = {
  value: string;
  tone: Tone;
  available: boolean;
};

function MetricCard(props: { label: string; presentation: MetricPresentation; infoTip?: string; metricKey?: AnalyticsMetricKey }) {
  return (
    <article
      className={`metric-card ${props.presentation.tone}`}
      data-value-state={props.presentation.available ? "available" : "unavailable"}
      aria-label={`${props.label}: ${props.presentation.value}`}
    >
      <span className="metric-label">
        <span>{props.label}</span>
        {props.infoTip ? <InfoTip text={props.infoTip} /> : null}
      </span>
      <strong>{props.presentation.value}</strong>
      {props.metricKey ? <KpiExplainButton metricKey={props.metricKey} ariaLabel={`Kako je izračunat ${props.label}`} /> : null}
    </article>
  );
}

function buildMetricPresentation(
  value: number | null,
  formatter: (amount: number) => string,
  positiveTone: Exclude<Tone, "insufficient_data">,
): MetricPresentation {
  const available = typeof value === "number" && Number.isFinite(value);
  if (!available) {
    return { value: "Nije dostupno", tone: "insufficient_data", available: false };
  }

  return {
    value: formatter(value),
    tone: value > 0 ? positiveTone : value < 0 ? "critical" : "neutral",
    available: true,
  };
}

export default function ExecutiveKpiRow(props: Props) {
  if (props.loading) {
    return (
      <div className="analytics-skeleton-grid">
        {Array.from({ length: 5 }).map((_, index) => <div key={`exec-kpi-${index}`} className="analytics-skeleton-card" />)}
      </div>
    );
  }

  return (
    <div className="analytics-card-grid analytics-exec-kpi-grid">
      <MetricCard
        label="Prihod"
        presentation={buildMetricPresentation(props.totalRevenue, (amount) => fmtRsd(amount, 0, "Nije dostupno"), "good")}
        infoTip="Formula: zbir prodajne vrednosti svih prodaja u izabranom periodu."
        metricKey="revenue"
      />
      <MetricCard
        label="Maržni doprinos"
        presentation={buildMetricPresentation(props.marginContributionRsd, (amount) => fmtRsd(amount, 0, "Nije dostupno"), "good")}
        infoTip="Formula: zbir (prodajna vrednost - nabavna vrednost) za stavke sa dostupnim troškom."
        metricKey="marginContribution"
      />
      <MetricCard
        label="Prodate jedinice"
        presentation={buildMetricPresentation(props.totalUnits, (amount) => fmtNumber(amount, 0, "Nije dostupno"), "neutral")}
        infoTip="Formula: zbir prodatih komada u izabranom periodu."
        metricKey="unitsSold"
      />
      <MetricCard
        label="Lager u riziku"
        presentation={buildMetricPresentation(props.inventoryDangerValueRsd, (amount) => fmtRsd(amount, 0, "Nije dostupno"), "warning")}
        infoTip="Procena kapitala vezanog u sporoj i rizičnoj zalihi (indikativno)."
        metricKey="stockAtRisk"
      />
      <article
        className={`metric-card ${props.dataQualityTone}`}
        data-value-state={props.dataQualityTone === "insufficient_data" ? "unavailable" : "available"}
        aria-label={`Spremnost za preporuke: ${props.readinessLabel ?? dataQualityStatusLabel(props.dataQualityStatus)}`}
      >
        <span className="metric-label">
          <span>Spremnost za preporuke</span>
          <InfoTip text="Najlošiji status kompletnosti i svežine za skup podataka koji dashboard koristi za odluke." />
        </span>
        <strong>{props.readinessLabel ?? dataQualityStatusLabel(props.dataQualityStatus)}</strong>
        <small className="exec-dq-sub">
          Obuhvat: {props.dataQualityScopeLabel ?? "Artikli u skupu odluka"} · Bez dobavljača: {fmtNumber(props.missingSupplierCount, 0, "-")} | Bez cene: {fmtNumber(props.missingCostCount, 0, "-")}
        </small>
        <KpiExplainButton metricKey="dataReadinessScore" ariaLabel="Kako je izračunata spremnost za preporuke" />
        <Link to="/analytics/data-quality" className="exec-dq-link">Otvori Data Quality</Link>
      </article>
    </div>
  );
}
