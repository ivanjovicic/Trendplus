import { fmtPct, formatDateTime } from "../../utils/analyticsFormatters";
import { getRecommendationMeta } from "./utils";

type SupplierExplainabilitySnapshotProps = {
  title?: string;
  subjectLabel?: string | null;
  compact?: boolean;
  periodLabel?: string | null;
  lastRefreshAt?: string | null;
  requestedDataset?: string | null;
  effectiveDataset?: string | null;
  effectivePeriodLabel?: string | null;
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
      return "Kriticno";
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

export default function SupplierExplainabilitySnapshot({
  title = "Snapshot objašnjenja",
  subjectLabel,
  compact = false,
  periodLabel,
  lastRefreshAt,
  requestedDataset,
  effectiveDataset,
  effectivePeriodLabel,
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
  const recommendationMeta = recommendationAllowed == null ? null : getRecommendationMeta(recommendationAllowed ? "EXPAND" : "ASSORTMENT_REDUCE");
  const reasonPreview = (reasonCodes ?? []).filter(Boolean).slice(0, compact ? 4 : 8);
  const hasReasonCodes = reasonPreview.length > 0;
  const requestedLabel = requestedDataset?.trim() || null;
  const effectiveLabel = effectiveDataset?.trim() || null;
  const datasetLabel = requestedLabel && effectiveLabel
    ? `${requestedLabel} → ${effectiveLabel}`
    : (effectiveLabel ?? requestedLabel);
  const periodText = periodLabel?.trim() || "Nedostupan";
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
      label: "Fallback",
      value: usedFallback == null ? "Nedovoljno podataka" : usedFallback ? "Aktivan" : "Neaktivan",
      tone: resolveFallbackTone(usedFallback),
    },
  ] as const;

  const metaCards = [
    { label: "Period", value: periodText },
    { label: "Dataset", value: datasetLabel ?? "Nedostupan", secondary: effectivePeriodLabel?.trim() || null },
    { label: "Osveženje", value: lastRefreshAt ? formatDateTime(lastRefreshAt, "Nedostupno") : "Nedostupno" },
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
              Snapshot koristi backend-led signal, bez lokalne confidence ili decision-tree logike.
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
            {recommendationAllowed == null ? "Preporuka: nedostupno" : recommendationAllowed ? "Preporuka dozvoljena" : "Preporuka blokirana"}
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
        <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">Šifarnici razloga</div>
        <div className="mt-2 flex flex-wrap gap-2">
          {hasReasonCodes ? (
            reasonPreview.map((reason, index) => tonePill(reason, "neutral", `${reason}-${index}`))
          ) : (
            <span className="text-sm text-[var(--text-primary)]">
              {usedFallback ? "Fallback signal bez dodatnih razloga" : "Nema dodatnih razloga"}
            </span>
          )}
          {reasonCodes && reasonCodes.length > reasonPreview.length ? tonePill(`+${reasonCodes.length - reasonPreview.length}`, "neutral") : null}
        </div>
      </div>

      {!compact ? (
        <div className="mt-4 grid gap-2 md:grid-cols-2">
          <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-light)] p-3 text-sm text-[var(--text-primary)]">
            <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">Fallback razlog</div>
            <div className="mt-1 font-semibold">{fallbackReason ?? "Nije aktivan"}</div>
          </div>
          <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-light)] p-3 text-sm text-[var(--text-primary)]">
            <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">Fallback kod</div>
            <div className="mt-1 font-semibold">{fallbackReasonCode ?? "Nije aktivan"}</div>
          </div>
        </div>
      ) : null}

      {note ? <p className="mt-3 text-sm leading-6 text-[var(--text-primary)]">{note}</p> : null}
    </section>
  );
}
