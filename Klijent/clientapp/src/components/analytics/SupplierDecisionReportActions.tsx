import { useState } from "react";
import type { ResolvedAnalyticsTablePayload } from "../../types/analyticsTable";
import {
  exportSupplierDecisionReportExcel,
  exportSupplierDecisionReportPdf,
  openSupplierDecisionPrintPreview,
} from "../../services/supplierDecisionReport";

type SupplierDecisionReportActionsProps = {
  payload: ResolvedAnalyticsTablePayload | null;
  disabled?: boolean;
};

export default function SupplierDecisionReportActions({ payload, disabled = false }: SupplierDecisionReportActionsProps) {
  const [busy, setBusy] = useState<"print" | "excel" | "pdf" | null>(null);
  const [status, setStatus] = useState<string | null>(null);

  const actionDisabled = disabled || !payload || busy !== null;

  const run = async (type: "print" | "excel" | "pdf") => {
    if (!payload || actionDisabled) return;
    setBusy(type);
    setStatus(null);
    try {
      if (type === "print") {
        await openSupplierDecisionPrintPreview(payload);
        setStatus("Print preview je otvoren u novom tabu.");
        return;
      }

      if (type === "excel") {
        await exportSupplierDecisionReportExcel(payload);
        setStatus("Excel izvestaj je preuzet.");
        return;
      }

      await exportSupplierDecisionReportPdf(payload);
      setStatus("PDF izvestaj je preuzet.");
    } catch (reason) {
      setStatus(reason instanceof Error ? reason.message : "Izvoz izvestaja nije uspeo.");
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="inline-flex flex-wrap items-center gap-2">
      <button
        type="button"
        className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
        onClick={() => void run("print")}
        disabled={actionDisabled}
      >
        {busy === "print" ? "Otvaram..." : "Print izvestaj"}
      </button>
      <button
        type="button"
        className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
        onClick={() => void run("excel")}
        disabled={actionDisabled}
      >
        {busy === "excel" ? "Izvoz..." : "Export Excel"}
      </button>
      <button
        type="button"
        className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
        onClick={() => void run("pdf")}
        disabled={actionDisabled}
      >
        {busy === "pdf" ? "Izvoz..." : "Export PDF"}
      </button>
      {status ? <span className="text-xs text-[var(--accent-success)]">{status}</span> : null}
    </div>
  );
}
