import { Link } from "react-router-dom";
import {
  type AnalyticsMetricKey,
  getAnalyticsMetricDefinition,
} from "../../utils/analyticsMetricDefinitions";
import "./MetricMethodologyPanel.css";

type MetricMethodologyPanelProps = {
  metricKey: AnalyticsMetricKey;
  onClose?: () => void;
  dataQualityHref?: string | null;
};

function renderList(title: string, items: string[]) {
  if (items.length === 0) return null;
  return (
    <section className="metric-methodology-section">
      <h3>{title}</h3>
      <ul>
        {items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    </section>
  );
}

export default function MetricMethodologyPanel({
  metricKey,
  onClose,
  dataQualityHref = "/analytics/data-quality",
}: MetricMethodologyPanelProps) {
  const definition = getAnalyticsMetricDefinition(metricKey);

  return (
    <div className="metric-methodology-panel">
      <div className="metric-methodology-header">
        <div>
          <p className="metric-methodology-eyebrow">Kako je izračunato?</p>
          <h2>{definition.title}</h2>
        </div>
        {onClose ? (
          <button
            type="button"
            className="metric-methodology-close"
            onClick={onClose}
            aria-label="Zatvori objašnjenje metrike"
          >
            Zatvori
          </button>
        ) : null}
      </div>

      <section className="metric-methodology-section">
        <h3>Šta broj znači</h3>
        <p>{definition.description}</p>
      </section>

      <section className="metric-methodology-section metric-methodology-formula">
        <h3>Formula</h3>
        <p>{definition.formula}</p>
      </section>

      <section className="metric-methodology-section">
        <h3>Izvor podataka</h3>
        <p>{definition.source}</p>
      </section>

      {definition.qualityNote ? (
        <section className="metric-methodology-section">
          <h3>Napomena o kvalitetu podataka</h3>
          <p>{definition.qualityNote}</p>
        </section>
      ) : null}

      {renderList("Ulazna polja", definition.inputs)}
      {renderList("Šta može da ograniči pouzdanost", definition.caveats)}
      {renderList("Kada je signal blokiran", definition.blockedWhen)}

      {definition.relatedDataQualityChecks.length > 0 ? (
        <section className="metric-methodology-section">
          <h3>Povezane Data Quality provere</h3>
          <ul>
            {definition.relatedDataQualityChecks.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
          {dataQualityHref ? (
            <Link to={dataQualityHref} className="metric-methodology-link">
              Otvori Data Quality
            </Link>
          ) : null}
        </section>
      ) : null}
    </div>
  );
}
