import { fmtPct, formatDate, formatDateTime } from "../../utils/analyticsFormatters";
import type { RecommendationCode } from "../../services/supplierDecisionHubApi";
import { getRecommendationMeta } from "./utils";
import { recommendationReasonLabel } from "../../utils/canonicalRecommendationSemantics";
import { supplierDecisionDatasetLabel, supplierDecisionProvenanceLabel, supplierDecisionReasonText } from "../../utils/supplierDecisionLabels";

type SupplierExplainabilitySnapshotProps = {
  title?: string;
  subjectLabel?: string | null;
  recommendationCode?: RecommendationCode | string | null;
  compact?: boolean;
  periodLabel?: string | null;
  requestedPeriodFrom?: string | null;
  requestedPeriodTo?: string | null;
  effectivePeriodFrom?: string | null;
  effectivePeriodTo?: string | null;
  observedPeriodFrom?: string | null;
  observedPeriodTo?: string | null;
  lastRefreshAt?: string | null;
  requestedDataset?: string | null;
  effectiveDataset?: string | null;
  effectivePeriodLabel?: string | null;
  provenanceBasis?: string | null;
  dataQualityStatus?: string | null;
  recommendationAllowed?: boolean | null;
  usedFallback?: boolean | null;
  fallbackReason?: string | null;
  fallbackReasonCode?: string | null;
  confidencePct?: number | null;
  reliabilityPct?: number | null;
  reasonCodes?: string[] | null;
  note?: string | null;
};

function normalizeQualityLabel(value?: string | null): string {
  switch ((value ?? "").trim().toLowerCase()) {
    case "good":
      return "Dobar";
    case "warning":
      return "Upozorenje";
    case "critical":
      return "Kritično";
    case "insufficient_data":
      return "Nedovoljno podataka";
    default:
      return value?.trim() || "Nije dostupno";
  }
}

function tonePill(value: string, tone: "good" | "warning" | "critical" | "neutral" = "neutral", key?: string) {
  const toneClass =
    tone === "good"
      ? "border-[var(--success)] text-[var(--success)]"
      : tone === "warning"
        ? "border-[var(--warning)] text-[var(--warning)]"
        : tone === "critical"
          ? "border-[var(--error)] text-[var(--error)]"
          : "border-[var(--border-default)] text-[var(--text-primary)]";

  return (
    <span key={key} className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${toneClass}`}>
      {value}
    </span>
  );
}

function resolveConfidenceTone(value: number | null | undefined): "good" | "warning" | "critical" | "neutral" {
  if (value == null) return "neutral";
  if (value >= 75) return "good";
  if (value >= 55) return "warning";
  return "critical";
}

function resolveRecommendationTone(value: boolean | null | undefined, usedFallback?: boolean | null): "good" | "warning" | "critical" | "neutral" {
  if (value == null) return "neutral";
  if (value) return "good";
  return usedFallback ? "warning" : "critical";
}

function resolveFallbackTone(value?: boolean | null): "good" | "warning" | "critical" | "neutral" {
  if (value == null) return "neutral";
  return value ? "warning" : "good";
}

function isKnownRecommendationCode(value?: string | null): value is RecommendationCode {
  return value === "EXPAND"
    || value === "EXPAND_SELECTIVELY"
    || value === "HOLD"
    || value === "PRICE_NEGOTIATE"
    || value === "ASSORTMENT_REDUCE"
    || value === "OOS_FALSE_NEGATIVE"
    || value === "REVIEW_QUALITY";
}

export default function SupplierExplainabilitySnapshot({
  title = "Sažetak objašnjenja signala",
  subjectLabel,
  recommendationCode,
  compact = false,
  periodLabel,
  requestedPeriodFrom,
  requestedPeriodTo,
  effectivePeriodFrom,
  effectivePeriodTo,
  observedPeriodFrom,
  observedPeriodTo,
  lastRefreshAt,
  requestedDataset,
  effectiveDataset,
  effectivePeriodLabel,
  provenanceBasis,
  dataQualityStatus,
  recommendationAllowed,
  usedFallback,
  fallbackReason,
  fallbackReasonCode,
  confidencePct,
  reliabilityPct,
  reasonCodes,
  note,
}: SupplierExplainabilitySnapshotProps) {
  const signalMeta = isKnownRecommendationCode(recommendationCode)
    ? getRecommendationMeta(recommendationCode)
    : null;
  const recommendationMeta = recommendationAllowed === true ? signalMeta : null;
  const reasonPreview = (reasonCodes ?? []).filter(Boolean).slice(0, compact ? 4 : 8);
  const hasReasonCodes = reasonPreview.length > 0;
  const requestedLabel = supplierDecisionDatasetLabel(requestedDataset);
  const effectiveLabel = supplierDecisionDatasetLabel(effectiveDataset);
  const datasetLabel = requestedLabel && effectiveLabel
    ? `${requestedLabel} → ${effectiveLabel}`
    : (effectiveLabel ?? requestedLabel);
  const periodText = periodLabel?.trim() || "Nedostupan";
  const formatRange = (from?: string | null, to?: string | null) => from && to
    ? `${formatDate(from, "Nedostupno")} - ${formatDate(to, "Nedostupno")}`
    : null;
  const requestedPeriodText = formatRange(requestedPeriodFrom, requestedPeriodTo) ?? periodText;
  const effectivePeriodText = formatRange(effectivePeriodFrom, effectivePeriodTo);
  const observedPeriodText = formatRange(observedPeriodFrom, observedPeriodTo);
  const provenanceText = supplierDecisionProvenanceLabel(provenanceBasis);
  const qualityLabel = normalizeQualityLabel(dataQualityStatus);

  const cards = [
    {
      label: "Sigurnost",
      value: confidencePct == null ? "Nedovoljno podataka" : fmtPct(confidencePct, 1),
      tone: resolveConfidenceTone(confidencePct),
    },
    {
      label: "Pouzdanost",
      value: reliabilityPct == null ? "Nedovoljno podataka" : fmtPct(reliabilityPct, 1),
      tone: resolveConfidenceTone(reliabilityPct),
    },
    {
      label: "Preporuka",
      value: recommendationAllowed == null ? "Nedovoljno podataka" : recommendationAllowed ? "Dozvoljena" : "Blokirana",
      tone: resolveRecommendationTone(recommendationAllowed, usedFallback),
    },
    {
      label: "Kvalitet",
      value: qualityLabel,
      tone:
        (dataQualityStatus ?? "").trim().toLowerCase() === "critical"
          ? "critical"
          : (dataQualityStatus ?? "").trim().toLowerCase() === "warning"
            ? "warning"
            : (dataQualityStatus ?? "").trim().toLowerCase() === "insufficient_data"
              ? "warning"
              : "good",
    },
    {
      label: "Pomoćni skup",
      value: usedFallback == null ? "Nedovoljno podataka" : usedFallback ? "Aktivan" : "Neaktivan",
      tone: resolveFallbackTone(usedFallback),
    },
  ] as const;

  const metaCards = [
    { label: "Traženi period", value: requestedPeriodText },
    { label: "Efektivni period", value: effectivePeriodText ?? effectivePeriodLabel?.trim() ?? "Nedostupan", secondary: effectivePeriodText && effectivePeriodLabel?.trim() ? effectivePeriodLabel.trim() : null },
    { label: "Posmatrani period", value: observedPeriodText ?? "Nedostupan" },
    { label: "Skup podataka", value: datasetLabel ?? "Nedostupan" },
    { label: "Osveženje", value: lastRefreshAt ? formatDateTime(lastRefreshAt, "Nedostupno") : "Nedostupno" },
    { label: "Osnova generisanja", value: provenanceText ?? "Nedostupna" },
  ];

  return (
    <section
      data-testid="supplier-explainability-snapshot"
      aria-label={title}
      className="rounded-2xl border border-[var(--border-default)] bg-[linear-gradient(135deg,var(--surface-elevated)_0%,var(--surface-default)_52%,var(--surface-darker)_100%)] p-4 shadow-[0_20px_58px_-48px_rgba(0,0,0,0.95)]"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="text-[11px] font-extrabold uppercase tracking-[0.18em] text-[var(--text-primary)]">{title}</div>
          {subjectLabel ? (
            <p className="mt-1 text-sm font-semibold text-[var(--text-primary)]">
              Izabrani dobavljač: <span>{subjectLabel} (pregled)</span>
            </p>
          ) : null}
          {compact ? null : (
            <p className="mt-1 max-w-3xl text-sm leading-6 text-[var(--text-primary)]">
              Sažetak koristi serverski signal, bez lokalne logike za sigurnost ili stablo odluke.
            </p>
          )}
        </div>

        {recommendationMeta ? (
          <span className={`inline-flex rounded-full border px-3 py-1 text-xs font-semibold ${
            recommendationMeta.ton === "pozitivno"
              ? "border-[var(--success)] text-[var(--success)]"
              : recommendationMeta.ton === "upozorenje"
                ? "border-[var(--warning)] text-[var(--warning)]"
                : recommendationMeta.ton === "rizik"
                  ? "border-[var(--error)] text-[var(--error)]"
                  : "border-[var(--border-default)] text-[var(--text-primary)]"
          }`}>
            {recommendationMeta.label}: {recommendationAllowed == null ? "preporuka nedostupna" : recommendationAllowed ? "preporuka dozvoljena" : "preporuka blokirana"}
          </span>
        ) : recommendationAllowed === false && signalMeta ? (
          <span className="inline-flex rounded-full border border-[var(--warning)] px-3 py-1 text-xs font-semibold text-[var(--warning)]">
            Signal: {signalMeta.label}; preporuka blokirana
          </span>
        ) : null}
      </div>

      <div className={`mt-4 grid gap-3 ${compact ? "sm:grid-cols-3" : "md:grid-cols-3"}`}>
        {metaCards.map((card) => (
          <div key={card.label} className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-light)] p-3">
            <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">{card.label}</div>
            <div className="mt-1 text-sm font-semibold text-[var(--text-primary)]">{card.value}</div>
            {"secondary" in card && card.secondary ? <div className="mt-1 text-xs text-[var(--text-secondary)]">{card.secondary}</div> : null}
          </div>
        ))}
      </div>

      <div className={`mt-4 grid gap-3 ${compact ? "sm:grid-cols-2" : "md:grid-cols-2 xl:grid-cols-5"}`}>
        {cards.map((card) => (
          <div key={card.label} className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-light)] p-3">
            <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">{card.label}</div>
            <div className="mt-2 flex items-center justify-between gap-2">
              <div className="text-sm font-semibold text-[var(--text-primary)]">{card.value}</div>
              {tonePill(
                card.tone === "good" ? "Dobro" : card.tone === "warning" ? "Upozorenje" : card.tone === "critical" ? "Kritično" : "Neutralno",
                card.tone
              )}
            </div>
          </div>
        ))}
      </div>

      <div className="mt-4 rounded-2xl border border-[var(--border-default)] bg-[var(--surface-light)] p-3">
        <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">Šifre razloga</div>
        <div className="mt-2 flex flex-wrap gap-2">
          {hasReasonCodes ? (
            reasonPreview.map((reason, index) => tonePill(recommendationReasonLabel(reason), "neutral", `${reason}-${index}`))
          ) : (
            <span className="text-sm text-[var(--text-primary)]">
              {usedFallback ? "Pomoćni signal bez dodatnih razloga" : "Nema dodatnih razloga"}
            </span>
          )}
          {reasonCodes && reasonCodes.length > reasonPreview.length ? tonePill(`+${reasonCodes.length - reasonPreview.length}`, "neutral") : null}
        </div>
      </div>

      {!compact ? (
        <div className="mt-4 grid gap-2 md:grid-cols-2">
          <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-light)] p-3 text-sm text-[var(--text-primary)]">
            <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">Razlog pomoćnog skupa</div>
            <div className="mt-1 font-semibold">{supplierDecisionReasonText(fallbackReason) ?? "Nije aktivan"}</div>
          </div>
          <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-light)] p-3 text-sm text-[var(--text-primary)]">
            <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">Kod pomoćnog skupa</div>
            <div className="mt-1 font-semibold">{fallbackReasonCode ? "Dodatno objašnjenje je dostupno" : "Nije aktivan"}</div>
          </div>
        </div>
      ) : null}

      {note ? <p className="mt-3 text-sm leading-6 text-[var(--text-primary)]">{note}</p> : null}
    </section>
  );
}
