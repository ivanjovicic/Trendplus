import { useMemo } from "react";
import type { AnalyticsNamedValue, ResolvedAnalyticsTablePayload } from "../../types/analyticsTable";
import { dataQualityStatusLabel, normalizeDataQualityStatus } from "../../utils/analyticsQuality";
import {
  findAnalyticsMetricKeyByLabel,
  getAnalyticsMetricDefinition,
} from "../../utils/analyticsMetricDefinitions";
import KpiExplainButton from "./KpiExplainButton";
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

function rowValueAny(payload: ResolvedAnalyticsTablePayload, candidates: Array<{ section: string; item: string }>): string | null {
  for (const candidate of candidates) {
    const value = rowValue(payload, candidate.section, candidate.item);
    if (value) return value;
  }

  return null;
}

function groupRowsAny(grouped: Map<string, ReportRow[]>, sectionNames: string[]): ReportRow[] {
  for (const sectionName of sectionNames) {
    const rows = grouped.get(sectionName);
    if (rows && rows.length > 0) return rows;
  }

  return [];
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

  const supplierLabel = rowValueAny(payload, [
    { section: "Header", item: "Dobavljač" },
    { section: "Header", item: "Dobavljac" },
  ]) ?? filterValue(payload, "supplier") ?? "Dobavljač";
  const period = rowValue(payload, "Header", "Period") ?? filterValue(payload, "period") ?? "-";
  const reportTitle = rowValueAny(payload, [
    { section: "Header", item: "Naziv izveštaja" },
    { section: "Header", item: "Naziv izvestaja" },
    { section: "Header", item: "Report" },
  ]) ?? payload.tableTitle ?? "Trendplus izveštaj dobavljača";
  const dataScope = rowValueAny(payload, [
    { section: "Header", item: "Opseg podataka" },
    { section: "Header", item: "Data scope" },
  ]) ?? filterValue(payload, "dataScope") ?? "-";
  const reportDate = rowValueAny(payload, [
    { section: "Header", item: "Datum izveštaja" },
    { section: "Header", item: "Datum izvestaja" },
  ]) ?? metaValue(payload, "generatedAtUtc") ?? "-";
  const lastRefresh = rowValueAny(payload, [
    { section: "Header", item: "Poslednje osveženje" },
    { section: "Header", item: "Poslednji refresh" },
  ]) ?? metaValue(payload, "lastRefreshAtUtc") ?? "-";
  const freshnessLabel = metaValue(payload, "dataFreshness");
  const metaDQ = metaValue(payload, "dataQualityStatus");
  const normalizedDQ = normalizeDataQualityStatus(metaValue(payload, "dataQualityStatus"));
  const recommendationAllowed = metaValue(payload, "recommendationAllowed");

  const warnings = groupRowsAny(grouped, ["Upozorenje"]);
  const kpi = grouped.get("KPI") ?? [];
  const recommendations = groupRowsAny(grouped, ["Preporuke"]);
  const topRevenue = groupRowsAny(grouped, ["Top artikli / dobavljači", "Top artikli / dobavljaci"]);
  const risk = groupRowsAny(grouped, ["Rizik zalihe"]);
  const boost = groupRowsAny(grouped, ["Pojačaj", "Pojacaj"]);
  const reduce = groupRowsAny(grouped, ["Smanji"]);
  const dataQuality = groupRowsAny(grouped, ["Kvalitet podataka", "Data quality"]);
  const methodology = groupRowsAny(grouped, ["Metodologija", "Methodology"]);
  const methodologyMetricKeys = useMemo(
    () =>
      Array.from(
        new Set(
          kpi
            .map((row) => findAnalyticsMetricKeyByLabel(row.item))
            .filter((value): value is NonNullable<typeof value> => Boolean(value))
        )
      ),
    [kpi]
  );

  return (
    <article className={`supplier-decision-report dq-${normalizedDQ}`}>
      <div className="sdr-header">
        <div className="sdr-title">
          <h1>{reportTitle}</h1>
          <p className="sdr-subtitle">
            Dobavljač: <strong>{supplierLabel}</strong> | Period: <strong>{period}</strong>
          </p>
        </div>
        <div className="sdr-badges">
          <span className={`sdr-badge dq-${normalizedDQ}`}>
            {metaDQ ? `Kvalitet podataka: ${metaDQ}` : `Kvalitet podataka: ${dataQualityStatusLabel(metaDQ)}`}
          </span>
          {freshnessLabel ? (
            <span className="sdr-badge neutral">Svežina podataka: {freshnessLabel}</span>
          ) : null}
          {recommendationAllowed != null ? (
            <span className="sdr-badge neutral">Preporuke: {String(recommendationAllowed) === "true" ? "dozvoljene" : "ograničene"}</span>
          ) : null}
        </div>
      </div>

      <section className="sdr-meta">
        <div className="sdr-meta-grid">
          <div className="sdr-meta-item"><span>Opseg podataka</span><strong>{dataScope}</strong></div>
          <div className="sdr-meta-item"><span>Datum izveštaja</span><strong>{reportDate}</strong></div>
          <div className="sdr-meta-item"><span>Poslednje osveženje</span><strong>{lastRefresh}</strong></div>
        </div>
        {renderMetaChips(payload.filters, "sdr-chip-row")}
      </section>

      {warnings.length > 0 ? (
        <section className="sdr-section sdr-warnings">
          <h2>Upozorenja i ograničenja</h2>
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
        <h2>Izvršni sažetak</h2>
        <div className="sdr-kpi-grid">
          {kpi.map((row) => {
            const metricKey = findAnalyticsMetricKeyByLabel(row.item);
            return (
              <article key={`${row.item}-${row.value}`} className="sdr-kpi">
                <span>{row.item}</span>
                <strong>{row.value}</strong>
                {row.secondary ? <small>{row.secondary}</small> : null}
                {metricKey ? <KpiExplainButton metricKey={metricKey} /> : null}
              </article>
            );
          })}
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
        <h2>Top artikli / dobavljači</h2>
        <div className="sdr-two-col">
          <div>
            <h3>Najveći prihod</h3>
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
            <h3>Pojačaj</h3>
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
          <p className="sdr-empty">Detaljan sažetak kvaliteta podataka nije dostupan u ovom report payload-u. Otvorite Data Quality ekran za detalje.</p>
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
        {methodologyMetricKeys.length > 0 ? (
          <div className="sdr-methodology">
            {methodologyMetricKeys.map((metricKey) => {
              const definition = getAnalyticsMetricDefinition(metricKey);
              return (
                <div key={metricKey} className="sdr-method">
                  <strong>{definition.title}</strong>
                  <p>{definition.description}</p>
                  <p><strong>Formula:</strong> {definition.formula}</p>
                  <p><strong>Izvor:</strong> {definition.source}</p>
                  {definition.qualityNote ? <p className="sdr-note">{definition.qualityNote}</p> : null}
                  <KpiExplainButton metricKey={metricKey} ariaLabel={`Kako je izračunat KPI: ${definition.title}`} />
                </div>
              );
            })}
          </div>
        ) : null}
        {methodology.length === 0 ? (
          methodologyMetricKeys.length === 0 ? <p className="sdr-empty">Metodologija nije dostupna.</p> : null
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
