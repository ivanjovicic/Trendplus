import { useMemo } from "react";
import type { AnalyticsNamedValue, ResolvedAnalyticsTablePayload } from "../../types/analyticsTable";
import { dataQualityStatusLabel, normalizeDataQualityStatus } from "../../utils/analyticsQuality";
import { findAnalyticsMetricKeyByLabel } from "../../utils/analyticsMetricDefinitions";
import KpiExplainButton from "./KpiExplainButton";
import MetricMethodologyPanel from "./MetricMethodologyPanel";
import SupplierExplainabilitySnapshot from "../supplierDecisionHub/SupplierExplainabilitySnapshot";
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

function rowEntry(payload: ResolvedAnalyticsTablePayload, section: string, item: string) {
  return payload.rows.find((row) => String(row.section) === section && String(row.item) === item) ?? null;
}

function groupRows(payload: ResolvedAnalyticsTablePayload): Map<string, ReportRow[]> {
  const grouped = new Map<string, ReportRow[]>();
  for (const raw of payload.rows) {
    const section = String(raw.section ?? "");
    if (!section) continue;
    const entry: ReportRow = {
      section,
      item: String(raw.item ?? ""),
      value: String(raw.value ?? ""),
      secondary: raw.secondary == null ? "" : String(raw.secondary),
      note: raw.note == null ? "" : String(raw.note),
    };
    const list = grouped.get(section) ?? [];
    list.push(entry);
    grouped.set(section, list);
  }
  return grouped;
}

function groupRowsAny(grouped: Map<string, ReportRow[]>, sectionNames: string[]): ReportRow[] {
  for (const sectionName of sectionNames) {
    const rows = grouped.get(sectionName);
    if (rows && rows.length > 0) return rows;
  }
  return [];
}

function metaValue(payload: ResolvedAnalyticsTablePayload, key: string): string | null {
  const found = payload.metadata?.find((item) => item.key === key);
  if (!found || found.value == null) return null;
  const text = String(found.value);
  return text.trim() ? text : null;
}

function metaNumber(payload: ResolvedAnalyticsTablePayload, key: string): number | null {
  const value = metaValue(payload, key);
  if (value == null) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function metaBoolean(payload: ResolvedAnalyticsTablePayload, key: string): boolean | null {
  const value = metaValue(payload, key);
  if (value == null) return null;
  if (value.toLowerCase() === "true") return true;
  if (value.toLowerCase() === "false") return false;
  return null;
}

function filterValue(payload: ResolvedAnalyticsTablePayload, key: string): string | null {
  const found = payload.filters?.find((item) => item.key === key);
  if (!found || found.value == null) return null;
  const text = String(found.value);
  return text.trim() ? text : null;
}

function buildNegotiationMeetingSummary(rows: ReportRow[]): string {
  const summaryRows = rows.filter((row) => row.secondary === "Sažetak");
  const argumentRows = rows.filter((row) => row.secondary === "Argumenti" || row.secondary === "Argumenti za dobavljača" || row.secondary === "Argumenti za pregovor");
  const proposalRows = rows.filter((row) => row.secondary === "Predlog razgovora" || row.secondary === "Pomoćni signal");
  const warningRows = rows.filter((row) => row.secondary === "Upozorenja");

  const lines: string[] = ["Paket za razgovor sa dobavljačem"];

  if (summaryRows.length > 0) {
    lines.push("", "Sažetak:");
    for (const row of summaryRows) lines.push(`- ${row.item}: ${row.value}`);
  }

  if (argumentRows.length > 0) {
    lines.push("", "Argumenti:");
    for (const row of argumentRows) lines.push(`- ${row.item}: ${row.value}`);
  }

  if (proposalRows.length > 0) {
    lines.push("", "Predlog razgovora:");
    for (const row of proposalRows) lines.push(`- ${row.item}: ${row.value}`);
  }

  if (warningRows.length > 0) {
    lines.push("", "Upozorenja:");
    for (const row of warningRows) lines.push(`- ${row.item}: ${row.value}`);
  }

  return lines.join("\n");
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
  const confidencePct = metaNumber(payload, "confidencePct");
  const reliabilityPct = metaNumber(payload, "reliabilityPct");
  const reasonCodesPreview = metaValue(payload, "reasonCodesPreview")
    ?.split(" | ")
    .map((reason) => reason.trim())
    .filter(Boolean) ?? [];
  const effectiveDatasetRow = rowEntry(payload, "Header", "Efektivni dataset");
  const effectivePeriodLabel = effectiveDatasetRow?.secondary?.trim() || metaValue(payload, "effectivePeriodLabel");
  const usedFallback = metaBoolean(payload, "usedFallback");
  const fallbackRow = rowEntry(payload, "Header", "KoriÅ¡Ä‡en fallback");

  const warnings = groupRowsAny(grouped, ["Upozorenje"]);
  const kpi = grouped.get("KPI") ?? [];
  const recommendations = groupRowsAny(grouped, ["Preporuke"]);
  const topRevenue = groupRowsAny(grouped, ["Top artikli / dobavljači", "Top artikli / dobavljaci"]);
  const risk = groupRowsAny(grouped, ["Rizik zalihe"]);
  const boost = groupRowsAny(grouped, ["Pojačaj", "Pojacaj"]);
  const reduce = groupRowsAny(grouped, ["Smanji"]);
  const negotiationPack = groupRowsAny(grouped, ["supplier_negotiation_pack", "Paket za razgovor sa dobavljačem"]);
  const dataQuality = groupRowsAny(grouped, ["Kvalitet podataka", "Data quality"]);
  const methodology = groupRowsAny(grouped, ["Metodologija", "Methodology"]);

  const methodologyMetricKeys = useMemo(
    () => payload.methodologyMetricKeys?.length
      ? Array.from(new Set(payload.methodologyMetricKeys))
      : Array.from(new Set(kpi.map((row) => findAnalyticsMetricKeyByLabel(row.item)).filter((value): value is NonNullable<typeof value> => Boolean(value)))),
    [kpi, payload.methodologyMetricKeys]
  );

  const negotiationMeetingSummary = useMemo(() => buildNegotiationMeetingSummary(negotiationPack), [negotiationPack]);

  const copyNegotiationSummary = async () => {
    if (!negotiationPack.length) return;
    if (!navigator?.clipboard?.writeText) return;
    await navigator.clipboard.writeText(negotiationMeetingSummary);
  };

  const negotiationSummaryRows = negotiationPack.filter((row) => row.secondary === "Sažetak");
  const negotiationArgumentRows = negotiationPack.filter((row) => row.secondary === "Argumenti" || row.secondary === "Argumenti za dobavljača" || row.secondary === "Argumenti za pregovor");
  const negotiationProposalRows = negotiationPack.filter((row) => row.secondary === "Predlog razgovora" || row.secondary === "Pomoćni signal");
  const negotiationWarningRows = negotiationPack.filter((row) => row.secondary === "Upozorenja");

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
          {freshnessLabel ? <span className="sdr-badge neutral">Svežina podataka: {freshnessLabel}</span> : null}
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

      <section className="sdr-section">
        <SupplierExplainabilitySnapshot
          title="Supplier explainability snapshot"
          subjectLabel={supplierLabel}
          periodLabel={period}
          lastRefreshAt={metaValue(payload, "lastRefreshAtUtc")}
          requestedDataset={metaValue(payload, "requestedDataset")}
          effectiveDataset={metaValue(payload, "effectiveDataset") ?? effectiveDatasetRow?.value ?? null}
          effectivePeriodLabel={effectivePeriodLabel}
          dataQualityStatus={metaDQ}
          recommendationAllowed={metaBoolean(payload, "recommendationAllowed")}
          usedFallback={usedFallback}
          fallbackReason={metaValue(payload, "fallbackReason") ?? fallbackRow?.note ?? null}
          fallbackReasonCode={metaValue(payload, "fallbackReasonCode")}
          confidencePct={confidencePct}
          reliabilityPct={reliabilityPct}
          reasonCodes={reasonCodesPreview}
          note="Report koristi isti backend-led explainability snapshot kao i hub, bez lokalnih decision-tree derivacija."
        />
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
                <KpiExplainButton metricKey={metricKey ?? row.item} ariaLabel={`Kako je izračunato: ${row.item}`} />
              </article>
            );
          })}
        </div>
      </section>

      <section className="sdr-section">
        <h2>Preporuke</h2>
        {recommendations.length === 0 ? <p className="sdr-empty">Nema preporuka za prikaz.</p> : (
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
                    <div className="sdr-list-main"><strong>{row.item}</strong><span className="sdr-list-val">{row.value}</span></div>
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
                    <div className="sdr-list-main"><strong>{row.item}</strong><span className="sdr-list-val">{row.value}</span></div>
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
                    <div className="sdr-list-main"><strong>{row.item}</strong><span className="sdr-list-val">{row.value}</span></div>
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
                    <div className="sdr-list-main"><strong>{row.item}</strong><span className="sdr-list-val">{row.value}</span></div>
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
        <div className="sdr-section-head">
          <h2>Paket za razgovor sa dobavljačem</h2>
          <button type="button" className="sdr-copy-btn" onClick={copyNegotiationSummary} disabled={negotiationPack.length === 0}>
            Kopiraj sažetak za sastanak
          </button>
        </div>
        {negotiationPack.length === 0 ? (
          <p className="sdr-empty">Paket nije dostupan za trenutni opseg.</p>
        ) : (
          <div className="sdr-negotiation-pack">
            <div className="sdr-negotiation-grid">
              {negotiationSummaryRows.map((row, idx) => (
                <article key={`${row.item}-${idx}`} className="sdr-negotiation-card">
                  <span>{row.item}</span>
                  <strong>{row.value}</strong>
                  {row.secondary ? <small className="sdr-note">{row.secondary}</small> : null}
                  {row.note ? <small className="sdr-note">{row.note}</small> : null}
                </article>
              ))}
            </div>

            <div className="sdr-two-col">
              <div>
                <h3>Argumenti za razgovor</h3>
                {negotiationArgumentRows.length === 0 ? <p className="sdr-empty">Nema dostupnih argumenata.</p> : (
                  <ul className="sdr-list">
                    {negotiationArgumentRows.map((row, idx) => (
                      <li key={`${row.item}-${idx}`}>
                        <div className="sdr-list-main"><strong>{row.item}</strong><span className="sdr-list-val">{row.value}</span></div>
                        {row.secondary ? <div className="sdr-list-sub">{row.secondary}</div> : null}
                        {row.note ? <div className="sdr-list-note">{row.note}</div> : null}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
              <div>
                <h3>Predlog razgovora</h3>
                {negotiationProposalRows.length === 0 ? <p className="sdr-empty">Nema dostupnog predloga razgovora.</p> : (
                  <ul className="sdr-list">
                    {negotiationProposalRows.map((row, idx) => (
                      <li key={`${row.item}-${idx}`}>
                        <div className="sdr-list-main"><strong>{row.item}</strong><span className="sdr-list-val">{row.value}</span></div>
                        {row.secondary ? <div className="sdr-list-sub">{row.secondary}</div> : null}
                        {row.note ? <div className="sdr-list-note">{row.note}</div> : null}
                      </li>
                    ))}
                  </ul>
                )}

                <h3>Upozorenja</h3>
                {negotiationWarningRows.length === 0 ? <p className="sdr-empty">Nema upozorenja.</p> : (
                  <ul className="sdr-list">
                    {negotiationWarningRows.map((row, idx) => (
                      <li key={`${row.item}-${idx}`}>
                        <div className="sdr-list-main"><strong>{row.item}</strong><span className="sdr-list-val">{row.value}</span></div>
                        {row.secondary ? <div className="sdr-list-sub">{row.secondary}</div> : null}
                        {row.note ? <div className="sdr-list-note">{row.note}</div> : null}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </div>
        )}
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
        <MetricMethodologyPanel metricKeys={methodologyMetricKeys} dataQualityHref="/analytics/data-quality" />
        {methodology.length === 0 ? (
          <p className="sdr-empty">Metodologija nije dostupna.</p>
        ) : (
          <div className="sdr-methodology">
            <h3>Napomene iz backend payload-a</h3>
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
