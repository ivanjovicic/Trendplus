import React from "react";
import { useParams, useSearchParams } from "react-router-dom";
import { getPrintPayload } from "../services/analyticsTableState";

function isManualSupplierColumnKey(key: string): boolean {
  return key.startsWith("manualSupplier:");
}

function normalizeColumnClassName(key: string): string {
  return key.replace(/[^a-zA-Z0-9_-]+/g, "-");
}

function getPrintColumnClassName(column: { key: string; header: string }): string {
  const baseClass = !column.header || isManualSupplierColumnKey(column.key) ? "blank-col" : "named-col";
  return `${baseClass} analytics-print-col-${normalizeColumnClassName(column.key)}`;
}

export default function AnalyticsPrintPage() {
  const params = useParams<{ table?: string }>();
  const [searchParams] = useSearchParams();
  const payload = React.useMemo(
    () => getPrintPayload(searchParams.get("stateKey")),
    [searchParams]
  );
  const isPortraitShiftBlank = payload?.documentType === "daily-sales-blank-portrait";
  const isDenseDailySalesBlank =
    payload?.documentType === "daily-sales-blank" ||
    (payload?.tableKey === "daily-sales-stats-blank" && !isPortraitShiftBlank);
  const isFilledDailySalesPrint =
    payload?.tableKey === "daily-sales-stats" && !isDenseDailySalesBlank && !isPortraitShiftBlank;
  const isDailySalesPrint = payload?.tableKey === "daily-sales-stats" || payload?.tableKey === "daily-sales-stats-blank" || isDenseDailySalesBlank || isPortraitShiftBlank;
  const manualSupplierColumnCount = payload?.columns.filter((column) => isManualSupplierColumnKey(column.key)).length ?? 0;
  const manualSupplierStartIndex = payload?.columns.findIndex((column) => isManualSupplierColumnKey(column.key)) ?? -1;
  const manualSupplierEndColSpan =
    manualSupplierStartIndex >= 0 && payload
      ? payload.columns.length - manualSupplierStartIndex - manualSupplierColumnCount
      : 0;
  const pageOrientation = isDenseDailySalesBlank || isFilledDailySalesPrint ? "landscape" : "portrait";
  const pageMarginMm = isDenseDailySalesBlank || isFilledDailySalesPrint ? 6 : isPortraitShiftBlank ? 8 : 14;
  const tableFontSizePx = isDenseDailySalesBlank || isFilledDailySalesPrint ? 8 : 12;
  // Separate padding for th and td so we can control row height properly
  const thPadding = isDenseDailySalesBlank ? "5px 3px" : isFilledDailySalesPrint ? "5px 3px" : isDailySalesPrint ? "10px 8px" : "8px";
  const tdPadding = isDenseDailySalesBlank ? "10px 3px" : isFilledDailySalesPrint ? "4px 3px" : isDailySalesPrint ? "10px 8px" : "8px";
  // height on <td> acts as min-height (unlike min-height which is ignored on table cells)
  const blankTdHeightPx = 36;
  const titleFontSizePx = isDenseDailySalesBlank ? 18 : isFilledDailySalesPrint ? 20 : isPortraitShiftBlank ? 16 : 28;

  React.useEffect(() => {
    if (!payload) return;
    const timer = window.setTimeout(() => window.print(), 150);
    return () => window.clearTimeout(timer);
  }, [payload]);

  if (!payload) {
    return (
      <div style={{ padding: 32, fontFamily: "Arial, sans-serif" }}>
        <h1>Print data nije dostupna</h1>
        <p>Otvorite print direktno iz analytics tabele. Print podaci se cuvaju kratko i vezani su za poslednje pokretanje print akcije.</p>
      </div>
    );
  }

  return (
    <div
      className={`analytics-print-page${isFilledDailySalesPrint ? " analytics-print-page-daily-sales" : ""}${isDenseDailySalesBlank ? " analytics-print-page-dense" : ""}${isPortraitShiftBlank ? " analytics-print-page-portrait-shift" : ""}`}
      style={{ background: "var(--surface)", color: "var(--foreground)", minHeight: "100vh", padding: 24, fontFamily: "Arial, sans-serif" }}
    >
      <style>{`
        @page { size: A4 ${pageOrientation}; margin: ${pageMarginMm}mm; }
        @media print {
          .analytics-print-actions { display: none !important; }
          html, body { margin: 0 !important; padding: 0 !important; }
          body { background: var(--c-fff, var(--theme-color-ffffff, #ffffff)) !important; }
          .analytics-print-page { padding: 0 !important; }
        }
        .analytics-print-table {
          width: 100%;
          max-width: 100%;
          border-collapse: collapse;
          table-layout: fixed;
          page-break-inside: auto;
        }
        .analytics-print-table thead { display: table-header-group; }
        .analytics-print-table tfoot { display: table-footer-group; }
        .analytics-print-table tr {
          break-inside: avoid;
          page-break-inside: avoid;
        }
        .analytics-print-table th,
        .analytics-print-table td {
          border: 1px solid var(--border);
          font-size: ${tableFontSizePx}px;
          line-height: 1.18;
          vertical-align: top;
          overflow-wrap: anywhere;
          word-break: normal;
        }
        .analytics-print-table th { padding: ${thPadding}; }
        .analytics-print-table td { padding: ${tdPadding}; }
        .analytics-print-table th {
          background: var(--surface-elevated);
          text-align: left;
          font-weight: 700;
        }
        @media print {
          .analytics-print-page,
          .analytics-print-table {
            overflow: visible !important;
            -webkit-print-color-adjust: exact;
            print-color-adjust: exact;
          }
        }

        /* Filled daily-sales print: landscape A4, compact fixed columns */
        .analytics-print-page-daily-sales .analytics-print-header {
          margin-bottom: 8px !important;
        }
        .analytics-print-page-daily-sales .analytics-print-meta {
          margin-bottom: 8px !important;
          gap: 8px !important;
        }
        .analytics-print-page-daily-sales .analytics-print-meta h2 {
          font-size: 10px !important;
          margin-bottom: 3px !important;
        }
        .analytics-print-page-daily-sales .analytics-print-meta div {
          font-size: 8px !important;
          margin-bottom: 2px !important;
        }
        .analytics-print-page-daily-sales .analytics-print-table th,
        .analytics-print-page-daily-sales .analytics-print-table td {
          border-color: #666666;
        }
        .analytics-print-page-daily-sales .analytics-print-table th {
          background: #e9edf3 !important;
          color: #111111 !important;
          font-size: 7px;
          line-height: 1.12;
          padding: 5px 2px;
          vertical-align: bottom;
          text-align: right;
          white-space: normal;
        }
        .analytics-print-page-daily-sales .analytics-print-table td {
          background: #ffffff !important;
          color: #111111 !important;
          font-size: 8px;
          line-height: 1.15;
          padding: 4px 3px;
          text-align: right;
          vertical-align: middle;
          white-space: nowrap;
        }
        .analytics-print-page-daily-sales .analytics-print-table tbody tr:nth-child(even) td {
          background: #f7f8fa !important;
        }
        .analytics-print-page-daily-sales .analytics-print-col-date {
          width: 20mm;
          text-align: left !important;
        }
        .analytics-print-page-daily-sales .analytics-print-col-firstShiftTotalItems,
        .analytics-print-page-daily-sales .analytics-print-col-secondShiftTotalItems {
          width: 16mm;
        }
        .analytics-print-page-daily-sales .analytics-print-col-totalRevenue {
          width: 24mm;
        }
        .analytics-print-page-daily-sales .analytics-print-col-othersCount {
          width: 15mm;
        }
        .analytics-print-page-daily-sales .analytics-print-col-totalItemsSold {
          width: 17mm;
        }
        .analytics-print-page-daily-sales th[class*="analytics-print-col-supplier-"],
        .analytics-print-page-daily-sales td[class*="analytics-print-col-supplier-"] {
          width: auto;
        }

        /* Blank daily-sales form: 15 manual supplier columns on landscape A4 */
        .analytics-print-page-dense .analytics-print-table {
          table-layout: fixed;
        }
        .analytics-print-page-dense .analytics-print-group-row th {
          border: 1px solid #555555;
          background: #f0f0f0 !important;
          color: #111111 !important;
          padding: 3px 2px;
          font-size: 7px;
          line-height: 1.1;
          text-align: center;
          vertical-align: middle;
        }
        .analytics-print-page-dense .analytics-print-group-row .manual-supplier-group {
          background: #e6edf7 !important;
          font-weight: 700;
        }
        .analytics-print-page-dense .analytics-print-table th.named-col {
          font-size: 7px;
          line-height: 1.15;
          padding: 5px 3px;
          vertical-align: bottom;
          white-space: normal;
          background: #eeeeee !important;
          color: #111111 !important;
        }
        .analytics-print-page-dense .analytics-print-table td.named-col {
          padding: ${tdPadding};
          height: ${blankTdHeightPx}px;
          vertical-align: middle;
          background: #ffffff !important;
        }
        .analytics-print-page-dense .analytics-print-table th.blank-col {
          font-size: 7px;
          line-height: 1.1;
          padding: 5px 1px;
          vertical-align: bottom;
          text-align: center;
          background: #e6edf7 !important;
          color: #111111 !important;
        }
        .analytics-print-page-dense .analytics-print-table td.blank-col {
          padding: ${tdPadding};
          height: ${blankTdHeightPx}px;
          vertical-align: middle;
          background: #ffffff !important;
        }
        /* Fixed A4 landscape widths for the blank form */
        .analytics-print-page-dense .analytics-print-col-date { width: 17mm; }
        .analytics-print-page-dense .analytics-print-col-worker1,
        .analytics-print-page-dense .analytics-print-col-worker2 { width: 25mm; }
        .analytics-print-page-dense .analytics-print-col-shift1,
        .analytics-print-page-dense .analytics-print-col-shift2 { width: 10mm; }
        .analytics-print-page-dense .analytics-print-col-revenue { width: 18mm; }
        .analytics-print-page-dense .analytics-print-col-total { width: 16mm; }
        .analytics-print-page-dense th[class*="analytics-print-col-manualSupplier-"],
        .analytics-print-page-dense td[class*="analytics-print-col-manualSupplier-"] {
          width: 10.7mm;
        }
        /* Even rows stay white on blank form — no tinting for clean writing space */
        .analytics-print-page-dense .analytics-print-table tbody tr:nth-child(even) td {
          background: #ffffff !important;
        }
        @media print {
          .analytics-print-page-dense .analytics-print-table td {
            background: #ffffff !important;
            height: ${blankTdHeightPx}px;
          }
        }

        /* ── Portrait shift blank form (30 rows on A4 portrait) ───── */
        /* A4 portrait: 297mm - 16mm margins = 281mm.
           Title block ~18mm + table header ~8mm = 26mm overhead.
           255mm / 30 rows = 8.5mm ≈ 32px per row. */
        .analytics-print-page-portrait-shift .analytics-print-table {
          table-layout: fixed;
          width: 100%;
          border-collapse: collapse;
        }
        .analytics-print-page-portrait-shift .analytics-print-table th,
        .analytics-print-page-portrait-shift .analytics-print-table td {
          border: 1px solid #888;
          font-size: 8px;
          overflow: hidden;
        }
        .analytics-print-page-portrait-shift .analytics-print-table th {
          padding: 4px 3px;
          background: #eeeeee !important;
          font-weight: bold;
          text-align: left;
          vertical-align: bottom;
          line-height: 1.3;
          word-break: break-word;
          white-space: normal;
        }
        .analytics-print-page-portrait-shift .analytics-print-table td {
          padding: 0 3px;
          height: 32px;
          background: #ffffff !important;
          vertical-align: middle;
        }
        /* Alternating row shade — very light, so writing stays visible */
        .analytics-print-page-portrait-shift .analytics-print-table tbody tr:nth-child(even) td {
          background: #f5f5f5 !important;
        }
        /* Column widths (total = 194mm on portrait with 8mm margins) */
        .analytics-print-page-portrait-shift .analytics-print-table th:nth-child(1),
        .analytics-print-page-portrait-shift .analytics-print-table td:nth-child(1) { width: 22mm; }
        .analytics-print-page-portrait-shift .analytics-print-table th:nth-child(2),
        .analytics-print-page-portrait-shift .analytics-print-table td:nth-child(2) { width: 40mm; }
        .analytics-print-page-portrait-shift .analytics-print-table th:nth-child(3),
        .analytics-print-page-portrait-shift .analytics-print-table td:nth-child(3) { width: 20mm; }
        .analytics-print-page-portrait-shift .analytics-print-table th:nth-child(4),
        .analytics-print-page-portrait-shift .analytics-print-table td:nth-child(4) { width: 40mm; }
        .analytics-print-page-portrait-shift .analytics-print-table th:nth-child(5),
        .analytics-print-page-portrait-shift .analytics-print-table td:nth-child(5) { width: 20mm; }
        .analytics-print-page-portrait-shift .analytics-print-table th:nth-child(6),
        .analytics-print-page-portrait-shift .analytics-print-table td:nth-child(6) { width: 28mm; }
        .analytics-print-page-portrait-shift .analytics-print-table th:nth-child(7),
        .analytics-print-page-portrait-shift .analytics-print-table td:nth-child(7) { width: 24mm; }
        @media print {
          .analytics-print-page-portrait-shift .analytics-print-table td {
            background: #ffffff !important;
            height: 32px;
          }
          .analytics-print-page-portrait-shift .analytics-print-table tbody tr:nth-child(even) td {
            background: #f5f5f5 !important;
          }
        }
      `}</style>

      <div className="analytics-print-actions" style={{ marginBottom: 20, display: "flex", gap: 12 }}>
        <button type="button" onClick={() => window.print()} style={{ padding: "8px 14px", borderRadius: 8, border: "1px solid var(--muted)", background: "var(--foreground)", color: "var(--on-foreground)", cursor: "pointer" }}>
          Print
        </button>
        <button type="button" onClick={() => window.close()} style={{ padding: "8px 14px", borderRadius: 8, border: "1px solid var(--border)", background: "var(--surface)", color: "var(--foreground)", cursor: "pointer" }}>
          Zatvori
        </button>
      </div>

      <header className="analytics-print-header" style={{ marginBottom: isDailySalesPrint ? 10 : 20 }}>
        <div style={{ fontSize: 12, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.08em" }}>
          Trendplus analitika
        </div>
        <h1 style={{ margin: "6px 0 4px", fontSize: titleFontSizePx }}>{payload.tableTitle}</h1>
        {!isDenseDailySalesBlank && !isPortraitShiftBlank && (
          <div style={{ fontSize: 12, color: "var(--muted)" }}>
            Table key: {params.table ?? payload.tableKey} | Generated in browser print view
          </div>
        )}
      </header>

      {(payload.filters.length > 0 || payload.metadata.length > 0) ? (
        <section className="analytics-print-meta" style={{ marginBottom: isDailySalesPrint ? 10 : 20, display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 16 }}>
          {payload.filters.length > 0 ? (
            <div>
              <h2 style={{ margin: "0 0 8px", fontSize: 14 }}>Filteri</h2>
              {payload.filters.map((item) => (
                <div key={item.key} style={{ fontSize: 12, marginBottom: 4 }}>
                  <strong>{item.label}:</strong> {item.value ?? "-"}
                </div>
              ))}
            </div>
          ) : null}

          {payload.metadata.length > 0 ? (
            <div>
              <h2 style={{ margin: "0 0 8px", fontSize: 14 }}>Metadata</h2>
              {payload.metadata.map((item) => (
                <div key={item.key} style={{ fontSize: 12, marginBottom: 4 }}>
                  <strong>{item.label}:</strong> {item.value ?? "-"}
                </div>
              ))}
            </div>
          ) : null}
        </section>
      ) : null}

      <table className="analytics-print-table">
        <thead>
          {isDenseDailySalesBlank && manualSupplierStartIndex >= 0 ? (
            <tr className="analytics-print-group-row">
              {manualSupplierStartIndex > 0 ? <th className="group-spacer" colSpan={manualSupplierStartIndex} /> : null}
              <th className="manual-supplier-group" colSpan={manualSupplierColumnCount}>
                Dobavljači za ručni unos
              </th>
              {manualSupplierEndColSpan > 0 ? <th className="group-spacer" colSpan={manualSupplierEndColSpan} /> : null}
            </tr>
          ) : null}
          <tr>
            {payload.columns.map((column) => (
              <th key={column.key} className={getPrintColumnClassName(column)}>{column.header}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {payload.rows.length === 0 ? (
            <tr>
              <td colSpan={payload.columns.length}>Nema podataka za print.</td>
            </tr>
          ) : payload.rows.map((row, index) => (
            <tr key={`${payload.tableKey}-${index}`}>
              {payload.columns.map((column) => (
                <td key={column.key} className={getPrintColumnClassName(column)}>{row[column.key] == null ? "" : String(row[column.key])}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
