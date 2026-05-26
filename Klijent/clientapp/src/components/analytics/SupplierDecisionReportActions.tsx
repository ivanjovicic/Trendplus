import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import type { ResolvedAnalyticsTablePayload } from "../../types/analyticsTable";
import { getAnalyticsActionSourceStatuses, upsertAnalyticsActionWithResult } from "../../services/analyticsApi";
import type { AnalyticsActionDataQualityStatus } from "../../types/analytics";
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
  durableReportHref?: string | null;
};

function readPayloadValue(payload: ResolvedAnalyticsTablePayload | null, key: string): string | null {
  if (!payload) return null;
  const fromMetadata = payload.metadata.find((entry) => entry.key === key)?.value;
  if (fromMetadata != null && String(fromMetadata).trim()) return String(fromMetadata);
  const fromFilters = payload.filters.find((entry) => entry.key === key)?.value;
  if (fromFilters != null && String(fromFilters).trim()) return String(fromFilters);
  return null;
}

function parseSupplierId(payload: ResolvedAnalyticsTablePayload | null): number | null {
  const supplierValue = readPayloadValue(payload, "supplierId") ?? readPayloadValue(payload, "supplier");
  if (!supplierValue) return null;
  const idMatch = supplierValue.match(/\d+/);
  if (!idMatch) return null;
  const parsed = Number(idMatch[0]);
  return Number.isFinite(parsed) ? parsed : null;
}

function parseRecommendationAllowed(payload: ResolvedAnalyticsTablePayload | null): boolean {
  const raw = (readPayloadValue(payload, "recommendationAllowed") ?? "").trim().toLowerCase();
  return raw === "true" || raw === "da";
}

function toActionDataQualityStatus(payload: ResolvedAnalyticsTablePayload | null): AnalyticsActionDataQualityStatus {
  const raw = (readPayloadValue(payload, "dataQualityStatus") ?? "").trim().toLowerCase();
  if (raw === "good" || raw === "dobar") return "good";
  if (raw === "warning" || raw === "upozorenje") return "warning";
  if (raw === "critical" || raw === "kritican" || raw === "kritičan") return "critical";
  return "insufficient_data";
}

export default function SupplierDecisionReportActions({ payload, disabled = false, onError, durableReportHref }: SupplierDecisionReportActionsProps) {
  const navigate = useNavigate();
  const [busy, setBusy] = useState<"durable" | "preview" | "copy" | "csv" | "print" | "excel" | "pdf" | "queue" | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [queued, setQueued] = useState(false);
  const pdfExportEnabled = String(import.meta.env.VITE_ENABLE_PDF_EXPORT ?? "false").toLowerCase() === "true";

  const recommendationAllowed = useMemo(() => parseRecommendationAllowed(payload), [payload]);
  const supplierId = useMemo(() => parseSupplierId(payload), [payload]);
  const periodValue = useMemo(() => readPayloadValue(payload, "period"), [payload]);
  const dataScope = useMemo(() => readPayloadValue(payload, "dataScope") ?? "all", [payload]);
  const dataQualityStatus = useMemo(() => toActionDataQualityStatus(payload), [payload]);

  const sourceKey = useMemo(() => {
    if (!payload) return null;
    const scopePart = dataScope || "all";
    const periodPart = periodValue?.replace(/\s+/g, "").replace(/[^0-9\-]/g, "") || "unknown-period";
    const supplierPart = supplierId ?? "all";
    const actionKind = recommendationAllowed ? "negotiation" : "signal_check";
    return `supplier:${actionKind}:${supplierPart}:${periodPart}:${scopePart}`;
  }, [dataScope, payload, periodValue, recommendationAllowed, supplierId]);

  useEffect(() => {
    let cancelled = false;
    if (!sourceKey) {
      setQueued(false);
      return;
    }

    void (async () => {
      try {
        const response = await getAnalyticsActionSourceStatuses({
          sourceType: "supplier",
          sourceKeys: [sourceKey],
        });
        if (cancelled) return;
        const item = response.items.find((entry: { sourceKey: string; exists: boolean }) => entry.sourceKey === sourceKey);
        setQueued(Boolean(item?.exists));
      } catch {
        if (!cancelled) setQueued(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [sourceKey]);

  const actionDisabled = disabled || !payload || busy !== null;
  const durableActionDisabled = disabled || !durableReportHref || busy !== null;
  const queueDisabled = disabled || !payload || !sourceKey || busy !== null || queued;

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

  const run = async (type: "durable" | "preview" | "copy" | "csv" | "print" | "excel" | "pdf" | "queue") => {
    if (type === "durable") {
      if (durableActionDisabled || !durableReportHref) return;
      setBusy(type);
      setStatus(null);
      try {
        navigate(durableReportHref);
      } finally {
        setBusy(null);
      }
      return;
    }

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
        setStatus("Sažetak je kopiran.");
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

      if (type === "queue") {
        if (!sourceKey) throw new Error("Nedostaje sourceKey za akciju.");
        const title = recommendationAllowed
          ? "Pripremi razgovor sa dobavljačem"
          : "Proveri signal dobavljača";
        const recommendationStatus = recommendationAllowed ? "NEGOTIATE_SUPPLIER" : "SIGNAL_REVIEW";
        const description = recommendationAllowed
          ? "Pripremiti argumente i uslove za pregovor na osnovu scorecard signala."
          : "Finalna preporuka nije dozvoljena za ovaj izveštaj; potrebna je provera signala pre odluke.";

        const result = await upsertAnalyticsActionWithResult({
          sourceType: "supplier",
          sourceKey,
          sourceId: supplierId,
          title,
          description,
          recommendationStatus,
          priority: recommendationAllowed ? "P1" : "P2",
          dataQualityStatus,
          actionUrl: durableReportHref ?? "/analytics/supplier?tab=scorecard",
          metadataJson: JSON.stringify({
            tableKey: payload.tableKey,
            supplierId,
            dataScope,
            period: periodValue,
            recommendationAllowed,
          }),
        });

        if (result.sourceKey) {
          setQueued(true);
        }
        setStatus(result.existing
          ? "Akcija je već u centralnim akcijama."
          : "Akcija je dodata u centralni red.");
        return;
      }

      if (!pdfExportEnabled) {
        throw new Error("PDF izvoz trenutno nije dostupan. Koristite štampu ili Excel.");
      }

      await exportSupplierDecisionReportPdf(payload);
      setStatus("PDF izveštaj je preuzet.");
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : "Izvoz izveštaja nije uspeo.";
      setStatus(message);
      onError?.(type === "pdf"
        ? "PDF izvoz trenutno nije dostupan. Koristite štampu ili Excel."
        : message);
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="inline-flex flex-wrap items-center gap-2">
      {durableReportHref ? (
        <button
          type="button"
          className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
          onClick={() => void run("durable")}
          disabled={durableActionDisabled}
        >
          {busy === "durable" ? "Otvaram..." : "Trajni izveštaj"}
        </button>
      ) : null}
      {payload ? (
        <>
          <button
            type="button"
            className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
            onClick={() => void run("queue")}
            disabled={queueDisabled}
          >
            {busy === "queue" ? "Dodajem..." : queued ? "U akcijama" : "Dodaj u akcije"}
          </button>
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
            {busy === "print" ? "Otvaram..." : "Štampaj izveštaj"}
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
            {busy === "csv" ? "Izvoz..." : "Izvezi CSV"}
          </button>
          <button
            type="button"
            className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
            onClick={() => void run("excel")}
            disabled={actionDisabled}
          >
            {busy === "excel" ? "Izvoz..." : "Izvezi Excel"}
          </button>
          {pdfExportEnabled ? (
            <button
              type="button"
              className="inline-flex items-center rounded-xl border border-border bg-surface px-3 py-2 text-xs font-semibold text-muted"
              onClick={() => void run("pdf")}
              disabled={actionDisabled}
            >
              {busy === "pdf" ? "Izvoz..." : "Izvezi PDF"}
            </button>
          ) : null}
        </>
      ) : null}
      {status ? <span className="text-xs text-[var(--accent-success)]">{status}</span> : null}
    </div>
  );
}
