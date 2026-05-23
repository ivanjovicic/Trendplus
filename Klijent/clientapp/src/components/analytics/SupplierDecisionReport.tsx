import { useMemo } from "react";
import type { AnalyticsNamedValue, ResolvedAnalyticsTablePayload } from "../../types/analyticsTable";
import { dataQualityStatusLabel, normalizeDataQualityStatus } from "../../utils/analyticsQuality";
import "./SupplierDecisionReport.css";

type SupplierDecisionReportProps = {
  payload: ResolvedAnalyticsTablePayload;
};

type ReportRow = {
  section: string;
  item: string;
  value: string;
  secondary?: string;
  note?: string;
};

function rowValue(payload: ResolvedAnalyticsTablePayload, section: string, item: string): string | null {
  const found = payload.rows.find((row) => String(row.section) === section && String(row.item) === item);
  if (!found) return null;
  const value = found.value == null ? "" : String(found.value);
  return value.trim() ? value : null;
}

function groupRows(payload: ResolvedAnalyticsTablePayload): Map<string, ReportRow[]> {
  const grouped = new Map<string, ReportRow[]>();
  for (const raw of payload.rows) {
    const section = raw.section == null ? "" : String(raw.section);
    const item = raw.item == null ? "" : String(raw.item);
    const value = raw.value == null ? "" : String(raw.value);
    const secondary = raw.secondary == null ? "" : String(raw.secondary);
    const note = raw.note == null ? "" : String(raw.note);

    if (!section) continue;
    const entry: ReportRow = { section, item, value, secondary, note };
    const list = grouped.get(section) ?? [];
    list.push(entry);
    grouped.set(section, list);
  }
  return grouped;
}

function metaValue(payload: ResolvedAnalyticsTablePayload, key: string): string | null {
  const found = payload.metadata?.find((item) => item.key === key);
  if (!found) return null;
  if (found.value == null) return null;
  const text = String(found.value);
  return text.trim() ? text : null;
}

function filterValue(payload: ResolvedAnalyticsTablePayload, key: string): string | null {
  const found = payload.filters?.find((item) => item.key === key);
  if (!found) return null;
  if (found.value == null) return null;
  const text = String(found.value);
  return text.trim() ? text : null;
}

function renderMetaChips(items: AnalyticsNamedValue[] | undefined, className: string) {
  if (!items || items.length === 0) return null;
  return (
    <div className={className}>
      {items.map((item) => (
        <span key={`${item.key}:${String(item.value ?? "")}`} className="sdr-chip">
          <span className="sdr-chip-key">{item.label}</span>
          <span className="sdr-chip-val">{item.value == null ? "-" : String(item.value)}</span>
        </span>
      ))}
    </div>
  );
}

export default function SupplierDecisionReport({ payload }: SupplierDecisionReportProps) {
  const grouped = useMemo(() => groupRows(payload), [payload]);

  const supplierLabel = rowValue(payload, "Header", "Dobavljac") ?? filterValue(payload, "supplier") ?? "Dobavljac";
  const period = rowValue(payload, "Header", "Period") ?? filterValue(payload, "period") ?? "-";
  const reportTitle = rowValue(payload, "Header", "Report") ?? payload.tableTitle ?? "Supplier Decision Report";
  const dataScope = rowValue(payload, "Header", "Data scope") ?? filterValue(payload, "dataScope") ?? "-";
  const reportDate = rowValue(payload, "Header", "Datum izvestaja") ?? metaValue(payload, "generatedAtUtc") ?? "-";
  const lastRefresh = rowValue(payload, "Header", "Poslednji refresh") ?? metaValue(payload, "lastRefreshAtUtc") ?? "-";
  const freshnessLabel = metaValue(payload, "dataFreshness");
  const metaDQ = metaValue(payload, "dataQualityStatus");
  const normalizedDQ = normalizeDataQualityStatus(metaValue(payload, "dataQualityStatus"));
  const recommendationAllowed = metaValue(payload, "recommendationAllowed");

  const warnings = grouped.get("Upozorenje") ?? [];
  const kpi = grouped.get("KPI") ?? [];
  const recommendations = grouped.get("Preporuke") ?? [];
  const topRevenue = grouped.get("Top artikli / dobavljaci") ?? [];
  const risk = grouped.get("Rizik zalihe") ?? [];
  const boost = grouped.get("Pojacaj") ?? [];
  const reduce = grouped.get("Smanji") ?? [];
  const dataQuality = grouped.get("Data quality") ?? [];
  const methodology = grouped.get("Methodology") ?? [];

  return (
    <article className={`supplier-decision-report dq-${normalizedDQ}`}>
      <div className="sdr-header">
        <div className="sdr-title">
          <h1>{reportTitle}</h1>
          <p className="sdr-subtitle">
            Dobavljac: <strong>{supplierLabel}</strong> | Period: <strong>{period}</strong>
          </p>
        </div>
        <div className="sdr-badges">
          <span className={`sdr-badge dq-${normalizedDQ}`}>
            {metaDQ ? `Data quality: ${metaDQ}` : `Data quality: ${dataQualityStatusLabel(metaDQ)}`}
          </span>
          {freshnessLabel ? (
            <span className="sdr-badge neutral">Freshness: {freshnessLabel}</span>
          ) : null}
          {recommendationAllowed != null ? (
            <span className="sdr-badge neutral">Preporuke: {String(recommendationAllowed) === "true" ? "dozvoljene" : "ogranicene"}</span>
          ) : null}
        </div>
      </div>

      <section className="sdr-meta">
        <div className="sdr-meta-grid">
          <div className="sdr-meta-item"><span>Data scope</span><strong>{dataScope}</strong></div>
          <div className="sdr-meta-item"><span>Datum izvestaja</span><strong>{reportDate}</strong></div>
          <div className="sdr-meta-item"><span>Poslednji refresh</span><strong>{lastRefresh}</strong></div>
        </div>
        {renderMetaChips(payload.filters, "sdr-chip-row")}
      </section>

      {warnings.length > 0 ? (
        <section className="sdr-section sdr-warnings">
          <h2>Upozorenja i ogranicenja</h2>
          <div className="sdr-warning-list">
            {warnings.map((w, idx) => (
              <article key={`${w.item}-${idx}`} className="sdr-warning">
                <strong>{w.item}</strong>
                <p>{w.value}</p>
                {w.note ? <p className="sdr-note">{w.note}</p> : null}
              </article>
            ))}
          </div>
        </section>
      ) : null}

      <section className="sdr-section">
        <h2>Executive summary</h2>
        <div className="sdr-kpi-grid">
          {kpi.map((row) => (
            <article key={`${row.item}-${row.value}`} className="sdr-kpi">
              <span>{row.item}</span>
              <strong>{row.value}</strong>
              {row.secondary ? <small>{row.secondary}</small> : null}
            </article>
          ))}
        </div>
      </section>

      <section className="sdr-section">
        <h2>Preporuke</h2>
        {recommendations.length === 0 ? (
          <p className="sdr-empty">Nema preporuka za prikaz.</p>
        ) : (
          <div className="sdr-reco-list">
            {recommendations.map((row, idx) => (
              <article key={`${row.item}-${idx}`} className="sdr-reco">
                <strong>{row.item}</strong>
                <p>{row.value}</p>
                {row.note ? <p className="sdr-note">{row.note}</p> : null}
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="sdr-section">
        <h2>Top artikli / dobavljaci</h2>
        <div className="sdr-two-col">
          <div>
            <h3>Najveci prihod</h3>
            {topRevenue.length === 0 ? <p className="sdr-empty">Nema stavki.</p> : (
              <ul className="sdr-list">
                {topRevenue.map((row, idx) => (
                  <li key={`${row.item}-${idx}`}>
                    <div className="sdr-list-main">
                      <strong>{row.item}</strong>
                      <span className="sdr-list-val">{row.value}</span>
                    </div>
                    {row.secondary ? <div className="sdr-list-sub">{row.secondary}</div> : null}
                    {row.note ? <div className="sdr-list-note">{row.note}</div> : null}
                  </li>
                ))}
              </ul>
            )}
          </div>
          <div>
            <h3>Rizik zalihe</h3>
            {risk.length === 0 ? <p className="sdr-empty">Nema stavki.</p> : (
              <ul className="sdr-list">
                {risk.map((row, idx) => (
                  <li key={`${row.item}-${idx}`}>
                    <div className="sdr-list-main">
                      <strong>{row.item}</strong>
                      <span className="sdr-list-val">{row.value}</span>
                    </div>
                    {row.secondary ? <div className="sdr-list-sub">{row.secondary}</div> : null}
                    {row.note ? <div className="sdr-list-note">{row.note}</div> : null}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      </section>

      <section className="sdr-section">
        <h2>Akcije po signalu</h2>
        <div className="sdr-two-col">
          <div>
            <h3>Pojacaj</h3>
            {boost.length === 0 ? <p className="sdr-empty">Nema stavki.</p> : (
              <ul className="sdr-list">
                {boost.map((row, idx) => (
                  <li key={`${row.item}-${idx}`}>
                    <div className="sdr-list-main">
                      <strong>{row.item}</strong>
                      <span className="sdr-list-val">{row.value}</span>
                    </div>
                    {row.secondary ? <div className="sdr-list-sub">{row.secondary}</div> : null}
                    {row.note ? <div className="sdr-list-note">{row.note}</div> : null}
                  </li>
                ))}
              </ul>
            )}
          </div>
          <div>
            <h3>Smanji / ne veruj</h3>
            {reduce.length === 0 ? <p className="sdr-empty">Nema stavki.</p> : (
              <ul className="sdr-list">
                {reduce.map((row, idx) => (
                  <li key={`${row.item}-${idx}`}>
                    <div className="sdr-list-main">
                      <strong>{row.item}</strong>
                      <span className="sdr-list-val">{row.value}</span>
                    </div>
                    {row.secondary ? <div className="sdr-list-sub">{row.secondary}</div> : null}
                    {row.note ? <div className="sdr-list-note">{row.note}</div> : null}
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      </section>

      <section className="sdr-section">
        <h2>Kvalitet podataka</h2>
        {dataQuality.length === 0 ? (
          <p className="sdr-empty">Sazetak data quality nije dostupan.</p>
        ) : (
          <div className="sdr-dq-grid">
            {dataQuality.map((row, idx) => (
              <article key={`${row.item}-${idx}`} className="sdr-dq">
                <span>{row.item}</span>
                <strong>{row.value}</strong>
                {row.secondary ? <small>{row.secondary}</small> : null}
                {row.note ? <small className="sdr-note">{row.note}</small> : null}
              </article>
            ))}
          </div>
        )}
      </section>

      <section className="sdr-section">
        <h2>Metodologija</h2>
        {methodology.length === 0 ? (
          <p className="sdr-empty">Metodologija nije dostupna.</p>
        ) : (
          <div className="sdr-methodology">
            {methodology.map((row, idx) => (
              <div key={`${row.item}-${idx}`} className="sdr-method">
                <strong>{row.item}</strong>
                <p>{row.value}</p>
                {row.note ? <p className="sdr-note">{row.note}</p> : null}
              </div>
            ))}
          </div>
        )}
      </section>
    </article>
  );
}
