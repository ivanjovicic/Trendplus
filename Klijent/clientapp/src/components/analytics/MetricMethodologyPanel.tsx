import { Link } from "react-router-dom";
import {
  getMetricMethodologyItems,
  type AnalyticsMetricKey,
  type AnalyticsMetricDefinition,
} from "../../utils/analyticsMetricDefinitions";
import "./MetricMethodologyPanel.css";

type MetricMethodologyPanelProps = {
  metricKey?: AnalyticsMetricKey | string;
  metricKeys?: Array<AnalyticsMetricKey | string>;
  onClose?: () => void;
  dataQualityHref?: string | null;
};

type MethodologyItem = AnalyticsMetricDefinition | {
  key: string;
  label: string;
  isDocumented: false;
  message: string;
};

function isDocumented(item: MethodologyItem): item is AnalyticsMetricDefinition {
  return !("isDocumented" in item);
}

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
  metricKeys,
  onClose,
  dataQualityHref = "/analytics/data-quality",
}: MetricMethodologyPanelProps) {
  const keys = metricKeys && metricKeys.length > 0
    ? metricKeys
    : metricKey
      ? [metricKey]
      : [];
  const items = getMetricMethodologyItems(keys);

  return (
    <div className="metric-methodology-panel">
      <div className="metric-methodology-header">
        <div>
          <p className="metric-methodology-eyebrow">Kako je izračunato?</p>
          <h2>Metodologija metrika</h2>
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

      {items.length === 0 ? (
        <section className="metric-methodology-section">
          <p>Metodologija za ovu metriku još nije dokumentovana.</p>
        </section>
      ) : null}

      {items.map((item) => (
        <article key={item.key} className="metric-methodology-section">
          <h3>{item.label}</h3>
          {isDocumented(item) ? (
            <>
              <p>{item.shortDescription}</p>
              <section className="metric-methodology-section metric-methodology-formula">
                <h4>Formula</h4>
                <pre className="metric-methodology-formula-block" style={{ whiteSpace: "pre-wrap", overflowX: "auto", padding: 8, background: "var(--surface-darker)", borderRadius: 6 }}>
                  <code style={{ fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, 'Roboto Mono', monospace", fontSize: "0.95rem", color: "var(--text-contrast, var(--text-primary))" }}>
                    {item.formula}
                  </code>
                </pre>
                {/* Optional human-readable example */}
                {("example" in item && (item as any).example) ? (
                  <div className="metric-methodology-formula-example" style={{ marginTop: 8 }}>
                    <strong>Primer:</strong>
                    <div style={{ fontFamily: "ui-monospace, Menlo, monospace", marginTop: 4 }}>{(item as any).example}</div>
                  </div>
                ) : null}
              </section>
              <section className="metric-methodology-section">
                <h4>Izvor podataka</h4>
                <p>{item.dataSource}</p>
              </section>
              <section className="metric-methodology-section">
                <h4>Tumačenje</h4>
                <p>{item.interpretation}</p>
              </section>
              {renderList("Šta može da ograniči pouzdanost", item.limitations)}
              {renderList("Kada signal nije pouzdan", item.blockedWhen)}
              {renderList("Povezane provere kvaliteta podataka", item.dataQualityDependencies)}
              {dataQualityHref ? (
                <Link to={dataQualityHref} className="metric-methodology-link">
                  Otvori kvalitet podataka
                </Link>
              ) : null}
            </>
          ) : (
            <p>{item.message}</p>
          )}
        </article>
      ))}
    </div>
  );
}
