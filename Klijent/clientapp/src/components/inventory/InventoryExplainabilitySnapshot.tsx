import { formatPercent, formatSellThroughRatio, formatStockCoverDays } from "./inventoryUtils";

type InventoryExplainabilitySnapshotProps = {
  title?: string;
  compact?: boolean;
  stockCoverDays?: number | null;
  stockCoverStatus?: string | null;
  stockCoverStatusLabel?: string | null;
  sellThroughRatio?: number | null;
  sellThroughStatus?: string | null;
  sellThroughStatusLabel?: string | null;
  signalConfidencePct?: number | null;
  recommendationAllowed?: boolean | null;
  dataQualityStatus?: string | null;
  reasonCodes?: string[] | null;
};

function resolveDataQualityLabel(value?: string | null): string {
  switch ((value ?? "").trim().toLowerCase()) {
    case "good":
      return "Dobro";
    case "warning":
      return "Upozorenje";
    case "critical":
      return "Kritično";
    case "insufficient_data":
      return "Nedovoljno podataka";
    default:
      return value ?? "Nije dostupno";
  }
}

function renderPill(value: string, tone: "good" | "warning" | "critical" | "neutral" = "neutral", key?: string) {
  const toneClass =
    tone === "good"
      ? "border-[var(--success)] text-[var(--success)]"
      : tone === "warning"
        ? "border-[var(--warning)] text-[var(--warning)]"
        : tone === "critical"
          ? "border-[var(--error)] text-[var(--error)]"
          : "border-[var(--border-default)] text-[var(--text-primary)]";

  return <span key={key} className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${toneClass}`}>{value}</span>;
}

export function InventoryExplainabilitySnapshot({
  title = "Snapshot objašnjenja",
  compact = false,
  stockCoverDays,
  stockCoverStatus,
  stockCoverStatusLabel,
  sellThroughRatio,
  sellThroughStatus,
  sellThroughStatusLabel,
  signalConfidencePct,
  recommendationAllowed,
  dataQualityStatus,
  reasonCodes,
}: InventoryExplainabilitySnapshotProps) {
  const normalizedDataQuality = (dataQualityStatus ?? "").trim().toLowerCase();
  const hasReasonCodes = (reasonCodes ?? []).length > 0;
  const reasonPreview = (reasonCodes ?? []).slice(0, compact ? 3 : 8);

  const cards = [
    {
      label: "Pouzdanost",
      value: signalConfidencePct == null ? "Nedovoljno podataka" : formatPercent(signalConfidencePct),
      tone: normalizedDataQuality === "critical" ? "critical" : normalizedDataQuality === "warning" ? "warning" : "good",
    },
    {
      label: "Preporuka",
      value: recommendationAllowed == null ? "Nedovoljno podataka" : recommendationAllowed ? "Dozvoljena" : "Zabranjena",
      tone: recommendationAllowed == null ? "neutral" : recommendationAllowed ? "good" : "critical",
    },
    {
      label: "Kvalitet",
      value: resolveDataQualityLabel(dataQualityStatus),
      tone: normalizedDataQuality === "critical" ? "critical" : normalizedDataQuality === "warning" ? "warning" : "good",
    },
    {
      label: "Pokrivenost zalihe",
      value: formatStockCoverDays(stockCoverDays, stockCoverStatus),
      tone: stockCoverStatus === "out_of_stock_risk" ? "critical" : stockCoverStatus === "low_cover" || stockCoverStatus === "low" ? "warning" : "good",
    },
    {
      label: "Obrt prodaje",
      value: formatSellThroughRatio(sellThroughRatio, sellThroughStatus),
      tone: sellThroughStatus === "critical" ? "critical" : sellThroughStatus === "warning" ? "warning" : "good",
    },
  ] as const;

  return (
    <section
      aria-label={title}
      className={compact ? "rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-4" : "rounded-2xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-5"}
    >
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-xs uppercase tracking-[0.22em] text-[var(--text-primary)]">{title}</div>
          {!compact ? (
            <p className="mt-1 text-sm text-[var(--text-primary)]">
              Snapshot koristi backend-led signal, bez lokalne confidence ili decision-tree logike.
            </p>
          ) : null}
        </div>
      </div>

      <div className={`mt-4 grid gap-3 ${compact ? "sm:grid-cols-2" : "md:grid-cols-2 xl:grid-cols-5"}`}>
        {cards.map((card) => (
          <div key={card.label} className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-darker)] p-3">
            <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">{card.label}</div>
            <div className="mt-2 flex items-center justify-between gap-2">
              <div className="text-sm font-semibold text-[var(--text-primary)]">{card.value}</div>
              {renderPill(card.tone === "good" ? "Dobro" : card.tone === "warning" ? "Upozorenje" : card.tone === "critical" ? "Kritično" : "Neutralno", card.tone)}
            </div>
          </div>
        ))}
      </div>

      <div className="mt-4">
        <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">Šifarnici razloga</div>
        <div className="mt-2 flex flex-wrap gap-2">
          {hasReasonCodes ? (
            reasonPreview.map((reason, index) => renderPill(reason, "neutral", `${reason}-${index}`))
          ) : (
            <span className="text-sm text-[var(--text-primary)]">{normalizedDataQuality === "insufficient_data" ? "Nedovoljno podataka" : "Nema dodatnih razloga"}</span>
          )}
          {reasonCodes && reasonCodes.length > reasonPreview.length ? (
            renderPill(`+${reasonCodes.length - reasonPreview.length}`, "neutral")
          ) : null}
        </div>
      </div>

      {!compact && stockCoverStatusLabel && sellThroughStatusLabel ? (
        <div className="mt-4 grid gap-2 md:grid-cols-2">
          <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-darker)] p-3 text-sm text-[var(--text-primary)]">
            <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">Stock cover status</div>
            <div className="mt-1 font-semibold">{stockCoverStatusLabel}</div>
          </div>
          <div className="rounded-2xl border border-[var(--border-default)] bg-[var(--surface-darker)] p-3 text-sm text-[var(--text-primary)]">
            <div className="text-[11px] uppercase tracking-[0.18em] text-[var(--text-primary)]">Sell-through status</div>
            <div className="mt-1 font-semibold">{sellThroughStatusLabel}</div>
          </div>
        </div>
      ) : null}
    </section>
  );
}
