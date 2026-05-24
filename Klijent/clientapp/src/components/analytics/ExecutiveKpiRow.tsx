import { Link } from "react-router-dom";
import InfoTip from "../ui/InfoTip";
import { fmtNumber, fmtRsd } from "../../utils/analyticsFormatters";
import { dataQualityStatusLabel } from "../../utils/analyticsQuality";

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
};

function MetricCard(props: { label: string; value: string; tone?: Tone; infoTip?: string }) {
  return (
    <article className={`metric-card ${props.tone ?? "neutral"}`}>
      <span className="metric-label">
        <span>{props.label}</span>
        {props.infoTip ? <InfoTip text={props.infoTip} /> : null}
      </span>
      <strong>{props.value}</strong>
    </article>
  );
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
        value={props.totalRevenue == null ? "N/A" : fmtRsd(props.totalRevenue)}
        tone="good"
        infoTip="Ukupan novac od prodaje u izabranom periodu."
      />
      <MetricCard
        label="Marzni doprinos"
        value={props.marginContributionRsd == null ? "N/A" : fmtRsd(props.marginContributionRsd)}
        tone="neutral"
        infoTip="Zbir marznog doprinosa (procenjeno) za artikle sa dostupnim signalom."
      />
      <MetricCard
        label="Prodate jedinice"
        value={props.totalUnits == null ? "N/A" : fmtNumber(props.totalUnits)}
        tone="neutral"
        infoTip="Ukupan broj prodatih komada."
      />
      <MetricCard
        label="Lager u riziku"
        value={props.inventoryDangerValueRsd == null ? "N/A" : fmtRsd(props.inventoryDangerValueRsd)}
        tone={props.inventoryDangerValueRsd != null && props.inventoryDangerValueRsd > 0 ? "warning" : "neutral"}
        infoTip="Procena kapitala u sporoj ili rizicnoj zalihi (indikativno)."
      />
      <article className={`metric-card ${qualityCardTone}`}>
        <span className="metric-label">
          <span>Kvalitet podataka</span>
          <InfoTip text="Bez pouzdanih podataka nema pouzdane preporuke. Klik vodi na detaljan Data Quality." />
        </span>
        <strong>{dataQualityStatusLabel(props.dataQualityStatus)}</strong>
        <small className="exec-dq-sub">
          Bez dobavljaca: {props.missingSupplierCount == null ? "-" : props.missingSupplierCount.toLocaleString("sr-RS")} | Bez cene: {props.missingCostCount == null ? "-" : props.missingCostCount.toLocaleString("sr-RS")}
        </small>
        <Link to="/analytics/data-quality" className="exec-dq-link">Otvori Data Quality</Link>
      </article>
    </div>
  );
}
