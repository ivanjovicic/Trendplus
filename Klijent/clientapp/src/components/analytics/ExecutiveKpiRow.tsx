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

function MetricCard(props: { label: string; value: string; tone?: Tone; infoTip?: string; metricKey?: AnalyticsMetricKey }) {
  return (
    <article className={`metric-card ${props.tone ?? "neutral"}`}>
      <span className="metric-label">
        <span>{props.label}</span>
        {props.infoTip ? <InfoTip text={props.infoTip} /> : null}
      </span>
      <strong>{props.value}</strong>
      {props.metricKey ? <KpiExplainButton metricKey={props.metricKey} ariaLabel={`Kako je izračunat ${props.label}`} /> : null}
    </article>
  );
}

function formatMetricValue(value: number | null, formatter: (amount: number) => string): string {
  if (value == null) return "Nije dostupno";
  return formatter(value);
}

export default function ExecutiveKpiRow(props: Props) {
  const qualityCardTone = props.dataQualityTone === "insufficient_data" ? "neutral" : props.dataQualityTone;

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
        value={formatMetricValue(props.totalRevenue, (amount) => fmtRsd(amount, 0, "Nije dostupno"))}
        tone="good"
        infoTip="Formula: zbir prodajne vrednosti svih prodaja u izabranom periodu."
        metricKey="revenue"
      />
      <MetricCard
        label="Maržni doprinos"
        value={formatMetricValue(props.marginContributionRsd, (amount) => fmtRsd(amount, 0, "Nije dostupno"))}
        tone={props.marginContributionRsd != null && props.marginContributionRsd > 0 ? "good" : "neutral"}
        infoTip="Formula: zbir (prodajna vrednost - nabavna vrednost) za stavke sa dostupnim troškom."
        metricKey="marginContribution"
      />
      <MetricCard
        label="Prodate jedinice"
        value={formatMetricValue(props.totalUnits, (amount) => fmtNumber(amount, 0, "Nije dostupno"))}
        tone="neutral"
        infoTip="Formula: zbir prodatih komada u izabranom periodu."
        metricKey="unitsSold"
      />
      <MetricCard
        label="Lager u riziku"
        value={formatMetricValue(props.inventoryDangerValueRsd, (amount) => fmtRsd(amount, 0, "Nije dostupno"))}
        tone={props.inventoryDangerValueRsd != null && props.inventoryDangerValueRsd > 0 ? "warning" : "neutral"}
        infoTip="Procena kapitala vezanog u sporoj i rizičnoj zalihi (indikativno)."
        metricKey="stockAtRisk"
      />
      <article className={`metric-card ${qualityCardTone}`}>
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
