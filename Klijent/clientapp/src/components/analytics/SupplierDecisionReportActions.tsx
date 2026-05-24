import { useState } from "react";
import { useNavigate } from "react-router-dom";
import type { ResolvedAnalyticsTablePayload } from "../../types/analyticsTable";
import {
  buildSupplierDecisionReportSummaryText,
  exportSupplierDecisionReportCsv,
  exportSupplierDecisionReportExcel,
  exportSupplierDecisionReportPdf,
  openSupplierDecisionPrintPreview,
} from "../../services/supplierDecisionReport";
import { savePrintPayload } from "../../services/analyticsTableState";

type SupplierDecisionReportActionsProps = {
  payload: ResolvedAnalyticsTablePayload | null;
  disabled?: boolean;
  onError?: (message: string) => void;
};

export default function SupplierDecisionReportActions({ payload, disabled = false, onError }: SupplierDecisionReportActionsProps) {
  const navigate = useNavigate();
  const [busy, setBusy] = useState<"preview" | "copy" | "csv" | "print" | "excel" | "pdf" | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const pdfExportEnabled = String(import.meta.env.VITE_ENABLE_PDF_EXPORT ?? "false").toLowerCase() === "true";

  const actionDisabled = disabled || !payload || busy !== null;

  const copyToClipboard = async (text: string) => {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return;
    }

    const textarea = document.createElement("textarea");
    textarea.value = text;
    textarea.style.position = "fixed";
    textarea.style.left = "-9999px";
    document.body.appendChild(textarea);
    textarea.select();
    document.execCommand("copy");
    document.body.removeChild(textarea);
  };

  const run = async (type: "preview" | "copy" | "csv" | "print" | "excel" | "pdf") => {
    if (!payload || actionDisabled) return;
    setBusy(type);
    setStatus(null);
    try {
      if (type === "preview") {
        const stateKey = savePrintPayload(payload);
        navigate(`/analytics/supplier/report?stateKey=${encodeURIComponent(stateKey)}`);
        return;
      }

      if (type === "copy") {
        const text = buildSupplierDecisionReportSummaryText(payload);
        await copyToClipboard(text);
        setStatus("Sažetak je kopiran u clipboard.");
        return;
      }

      if (type === "csv") {
        exportSupplierDecisionReportCsv(payload);
        setStatus("CSV izveštaj je preuzet.");
        return;
      }

      if (type === "print") {
        await openSupplierDecisionPrintPreview(payload);
        setStatus("Print preview je otvoren u novom tabu.");
        return;
      }

      if (type === "excel") {
        await exportSupplierDecisionReportExcel(payload);
        setStatus("Excel izveštaj je preuzet.");
        return;
      }

      if (!pdfExportEnabled) {
        throw new Error("PDF export trenutno nije dostupan. Koristite Print izveštaj ili Export Excel.");
      }

      await exportSupplierDecisionReportPdf(payload);
      setStatus("PDF izveštaj je preuzet.");
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Izvoz izveštaja nije uspeo.";
      setStatus(message);
      onError?.(type === "pdf"
        ? "PDF export trenutno nije dostupan. Koristite Print izveštaj ili Export Excel."
        : message);
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="inline-flex flex-wrap items-center gap-2">
      <button
        type="button"
        className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
        onClick={() => void run("preview")}
        disabled={actionDisabled}
      >
        {busy === "preview" ? "Otvaram..." : "Pregled u aplikaciji"}
      </button>
      <button
        type="button"
        className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
        onClick={() => void run("print")}
        disabled={actionDisabled}
      >
        {busy === "print" ? "Otvaram..." : "Print izveštaj"}
      </button>
      <button
        type="button"
        className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
        onClick={() => void run("copy")}
        disabled={actionDisabled}
        title="Kopira executive sažetak izveštaja"
      >
        {busy === "copy" ? "Kopiram..." : "Kopiraj sažetak"}
      </button>
      <button
        type="button"
        className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
        onClick={() => void run("csv")}
        disabled={actionDisabled}
      >
        {busy === "csv" ? "Izvoz..." : "Export CSV"}
      </button>
      <button
        type="button"
        className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
        onClick={() => void run("excel")}
        disabled={actionDisabled}
      >
        {busy === "excel" ? "Izvoz..." : "Export Excel"}
      </button>
      {pdfExportEnabled ? (
        <button
          type="button"
          className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
          onClick={() => void run("pdf")}
          disabled={actionDisabled}
        >
          {busy === "pdf" ? "Izvoz..." : "Export PDF"}
        </button>
      ) : null}
      {status ? <span className="text-xs text-[var(--accent-success)]">{status}</span> : null}
    </div>
  );
}
