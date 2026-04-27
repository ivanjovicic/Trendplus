import React from "react";
import { useParams, useSearchParams } from "react-router-dom";
import { getPrintPayload } from "../services/analyticsTableState";

export default function AnalyticsPrintPage() {
  const params = useParams<{ table?: string }>();
  const [searchParams] = useSearchParams();
  const payload = React.useMemo(
    () => getPrintPayload(searchParams.get("stateKey")),
    [searchParams]
  );
  const isDenseDailySalesBlank = payload?.documentType === "daily-sales-blank";
  const isDailySalesPrint = payload?.tableKey === "daily-sales-stats" || payload?.tableKey === "daily-sales-stats-blank" || isDenseDailySalesBlank;
  // Blank forms with many columns need landscape; filled daily-sales still portrait
  const pageOrientation = isDenseDailySalesBlank ? "landscape" : isDailySalesPrint ? "portrait" : "landscape";
  const pageMarginMm = isDenseDailySalesBlank ? 7 : 14;
  const tableFontSizePx = isDenseDailySalesBlank ? 9 : 12;
  // Separate padding for th and td so we can control row height properly
  const thPadding = isDenseDailySalesBlank ? "6px 4px" : isDailySalesPrint ? "10px 8px" : "8px";
  const tdPadding = isDenseDailySalesBlank ? "18px 5px" : isDailySalesPrint ? "10px 8px" : "8px";
  // height on <td> acts as min-height (unlike min-height which is ignored on table cells)
  const blankTdHeightPx = 46;
  const titleFontSizePx = isDenseDailySalesBlank ? 22 : 28;

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
      className={`analytics-print-page${isDenseDailySalesBlank ? " analytics-print-page-dense" : ""}`}
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
        .analytics-print-table { width: 100%; border-collapse: collapse; }
        .analytics-print-table th,
        .analytics-print-table td {
          border: 1px solid var(--border);
          font-size: ${tableFontSizePx}px;
          vertical-align: top;
        }
        .analytics-print-table th { padding: ${thPadding}; }
        .analytics-print-table td { padding: ${tdPadding}; }
        .analytics-print-table th {
          background: var(--surface-elevated);
          text-align: left;
        }

        /* ── Blank daily-sales form ──────────────────────────────── */
        .analytics-print-page-dense .analytics-print-table {
          /* auto layout: named cols size to content, blank supplier cols get min-width below */
          table-layout: auto;
        }
        /* Named columns (Datum, Smena, Prihod, Ostali, Ukupno) */
        .analytics-print-page-dense .analytics-print-table .named-col {
          min-width: 28mm;
        }
        .analytics-print-page-dense .analytics-print-table th.named-col {
          font-size: 8px;
          line-height: 1.35;
          padding: 7px 5px;
          vertical-align: bottom;
          overflow-wrap: break-word;
          word-break: normal;
          hyphens: auto;
          white-space: normal;
        }
        .analytics-print-page-dense .analytics-print-table td.named-col {
          padding: ${tdPadding};
          height: ${blankTdHeightPx}px;
          vertical-align: middle;
        }
        /* Supplier blank columns: narrow slots for hand-written numbers */
        .analytics-print-page-dense .analytics-print-table .blank-col {
          min-width: 16mm;
          max-width: 20mm;
          width: 18mm;
        }
        .analytics-print-page-dense .analytics-print-table th.blank-col {
          font-size: 7px;
          padding: 5px 2px;
          vertical-align: bottom;
          text-align: center;
        }
        .analytics-print-page-dense .analytics-print-table td.blank-col {
          padding: ${tdPadding};
          height: ${blankTdHeightPx}px;
          vertical-align: middle;
          background: #ffffff !important;
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
      `}</style>

      <div className="analytics-print-actions" style={{ marginBottom: 20, display: "flex", gap: 12 }}>
        <button type="button" onClick={() => window.print()} style={{ padding: "8px 14px", borderRadius: 8, border: "1px solid var(--muted)", background: "var(--foreground)", color: "var(--on-foreground)", cursor: "pointer" }}>
          Print
        </button>
        <button type="button" onClick={() => window.close()} style={{ padding: "8px 14px", borderRadius: 8, border: "1px solid var(--border)", background: "var(--surface)", color: "var(--foreground)", cursor: "pointer" }}>
          Zatvori
        </button>
      </div>

      <header style={{ marginBottom: 20 }}>
        <div style={{ fontSize: 12, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.08em" }}>
          Trendplus analitika
        </div>
        <h1 style={{ margin: "6px 0 4px", fontSize: titleFontSizePx }}>{payload.tableTitle}</h1>
        {!isDenseDailySalesBlank && (
          <div style={{ fontSize: 12, color: "var(--muted)" }}>
            Table key: {params.table ?? payload.tableKey} | Generated in browser print view
          </div>
        )}
      </header>

      {(payload.filters.length > 0 || payload.metadata.length > 0) ? (
        <section style={{ marginBottom: 20, display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 16 }}>
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
          <tr>
            {payload.columns.map((column) => (
              <th key={column.key} className={column.header ? "named-col" : "blank-col"}>{column.header}</th>
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
                <td key={column.key} className={column.header ? "named-col" : "blank-col"}>{row[column.key] == null ? "" : String(row[column.key])}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
