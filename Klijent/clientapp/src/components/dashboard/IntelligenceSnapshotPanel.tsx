import type { ReactNode } from "react";
import type {
  DemandSignalItem,
  InventoryRiskSignalItem,
  PriceIntelligenceItem,
  TrendMomentumItem,
} from "../../services/analyticsIntelligenceApi";
import { ANALYTICS_UNAVAILABLE_LABEL } from "../../utils/analyticsConstants";

const PAL = {
  blue: 'var(--info)',
  green: 'var(--success)',
  yellow: 'var(--warning)',
  orange: 'var(--warning)',
  red: 'var(--error)',
  cyan: 'var(--info)',
  card: 'var(--surface-default)',
  panel: 'var(--surface-elevated)',
  border: 'var(--border-default)',
  textPrimary: 'var(--text-primary)',
  textSecondary: 'var(--text-secondary)',
};

type IntelligenceSnapshotPanelProps = {
  demand: DemandSignalItem[];
  inventory: InventoryRiskSignalItem[];
  price: PriceIntelligenceItem[];
  trend: TrendMomentumItem[];
  asOfDate?: string | null;
  loading: boolean;
  error?: string | null;
};

type SignalCardProps = {
  title: string;
  subtitle: string;
  accent: string;
  summaryLabel: string;
  summaryValue: string;
  summaryMeta: string;
  children: ReactNode;
};

function isFiniteNumber(value: number | null | undefined): value is number {
  return typeof value === "number" && Number.isFinite(value);
}

function formatCompact(value: number | null | undefined) {
  if (!isFiniteNumber(value)) return ANALYTICS_UNAVAILABLE_LABEL;
  if (Math.abs(value) >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
  if (Math.abs(value) >= 1_000) return `${(value / 1_000).toFixed(1)}k`;
  return value.toLocaleString("sr-RS", { maximumFractionDigits: 1 });
}

function formatPercent(value: number | null | undefined, decimals = 1) {
  if (value == null || Number.isNaN(value)) return ANALYTICS_UNAVAILABLE_LABEL;
  return `${(value * 100).toFixed(decimals)}%`;
}

function formatSignedPercent(value: number | null | undefined, decimals = 1) {
  if (value == null || Number.isNaN(value)) return ANALYTICS_UNAVAILABLE_LABEL;
  const pct = value * 100;
  return `${pct >= 0 ? "+" : ""}${pct.toFixed(decimals)}%`;
}

function externalTrendEvidenceLabel(item: TrendMomentumItem) {
  if (item.externalTrendScore == null || item.externalTrendSource == null) {
    return "Eksterni skor nije dostupan";
  }

  const sourceLabel = item.externalTrendSource === "trend_history"
    ? "Istorija trenda"
    : "Poslednji proračun trenda";
  const observedAt = item.externalTrendObservedAt?.replace("T", " ").slice(0, 16);
  return observedAt ? `${sourceLabel} | ${observedAt}` : sourceLabel;
}

function inventoryStockEvidenceLabel(item: InventoryRiskSignalItem) {
  if (item.stockProxyEvidence === "missing_baseline" || item.stockQty == null) {
    return `stanje ${ANALYTICS_UNAVAILABLE_LABEL} | OOS ${ANALYTICS_UNAVAILABLE_LABEL}`;
  }

  if (item.stockProxyEvidence === "negative_proxy" || item.stockQty < 0) {
    return `proxy ${formatCompact(item.stockQty)} (nije mereni OOS)`;
  }

  const oosLabel = item.stockoutDays == null
    ? ANALYTICS_UNAVAILABLE_LABEL
    : `${item.stockoutDays} d`;
  return `stanje ${formatCompact(item.stockQty)} | OOS ${oosLabel}`;
}

function formatDemandSignal(item: DemandSignalItem) {
  if (item.demandState === "NEW_DEMAND") return "Novi signal";
  if (item.demandState === "NO_BASELINE") return "Nema baze";
  return formatSignedPercent(item.demandAcceleration, 0);
}

function demandSignalTone(item: DemandSignalItem) {
  if (item.demandState === "NEW_DEMAND") return PAL.blue;
  if (item.demandState === "NO_BASELINE") return PAL.yellow;
  return signalTone(item.demandAcceleration ?? 0, 0, PAL.green, PAL.orange);
}

function formatDateLabel(value?: string | null) {
  if (!value) return "Poslednji keš";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleDateString("sr-RS");
}

function signalTone(value: number | null | undefined, warningThreshold: number, goodColor: string, warningColor: string) {
  if (!isFiniteNumber(value)) return PAL.textSecondary;
  return value >= warningThreshold ? goodColor : warningColor;
}

function SignalCard({
  title,
  subtitle,
  accent,
  summaryLabel,
  summaryValue,
  summaryMeta,
  children,
}: SignalCardProps) {
  return (
    <div className="rounded-xl border p-4 bg-[var(--surface-default)]" style={{ borderColor: PAL.border }}>
      <div className="mb-4 flex items-start justify-between gap-3">
        <div>
          <div className="text-[11px] uppercase tracking-[0.24em]" style={{ color: accent }}>
            {title}
          </div>
          <div className="mt-1 text-xs" style={{ color: PAL.textSecondary }}>
            {subtitle}
          </div>
        </div>
        <div className="min-w-[128px] rounded-lg border px-3 py-2 text-right" style={{ borderColor: PAL.border, background: 'var(--surface-elevated)' }}>
          <div className="text-[10px] uppercase" style={{ color: PAL.textSecondary }}>
            {summaryLabel}
          </div>
          <div className="text-base font-bold" style={{ color: PAL.textPrimary }}>
            {summaryValue}
          </div>
          <div className="text-[11px]" style={{ color: PAL.textSecondary }}>
            {summaryMeta}
          </div>
        </div>
      </div>
      <div className="space-y-2">{children}</div>
    </div>
  );
}

function SignalRow({
  title,
  subtitle,
  badge,
  badgeColor,
}: {
  title: string;
  subtitle: string;
  badge: string;
  badgeColor: string;
}) {
  return (
    <div
      className="flex items-center gap-3 rounded-lg border px-3 py-2"
      style={{ borderColor: PAL.border, background: PAL.panel }}
    >
      <div className="min-w-0 flex-1">
        <div className="truncate text-sm font-medium" style={{ color: PAL.textPrimary }}>
          {title}
        </div>
        <div className="truncate text-[11px]" style={{ color: PAL.textSecondary }}>
          {subtitle}
        </div>
      </div>
      <div
        className="shrink-0 rounded px-2 py-1 text-[11px] font-semibold"
        style={{ background: `${badgeColor}22`, color: badgeColor }}
      >
        {badge}
      </div>
    </div>
  );
}

function PanelSkeleton() {
  return (
    <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
      {Array.from({ length: 4 }).map((_, idx) => (
        <div key={idx} className="rounded-xl border p-4 animate-pulse" style={{ borderColor: PAL.border, background: PAL.panel }}>
          <div className="mb-4 h-3 w-28 rounded" style={{ background: PAL.card }} />
          <div className="mb-4 h-10 rounded" style={{ background: PAL.card }} />
          <div className="space-y-2">
            {Array.from({ length: 3 }).map((__, rowIdx) => (
              <div key={rowIdx} className="h-12 rounded" style={{ background: PAL.card }} />
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

export default function IntelligenceSnapshotPanel({
  demand,
  inventory,
  price,
  trend,
  asOfDate,
  loading,
  error,
}: IntelligenceSnapshotPanelProps) {
  if (loading) return <PanelSkeleton />;

  if (error) {
    return (
      <div className="rounded-xl border px-4 py-3 text-sm" style={{ borderColor: `${PAL.red}33`, background: `${PAL.red}10`, color: PAL.red }}>
        Pregled signala trenutno nije dostupan: {error}
      </div>
    );
  }

  if (demand.length === 0 && inventory.length === 0 && price.length === 0 && trend.length === 0) {
    return (
      <div className="rounded-xl border px-4 py-3 text-sm" style={{ borderColor: `${PAL.blue}33`, background: `${PAL.blue}10`, color: PAL.blue }}>
        Keš signala je trenutno prazan ili se još uvek gradi.
      </div>
    );
  }

  const topDemand = demand[0];
  // Negative reconstructed proxy is inconsistency evidence, not actionable dead-stock risk.
  const knownRiskInventory = inventory.filter(
    (item) =>
      isFiniteNumber(item.deadStockRisk) &&
      item.stockProxyEvidence !== "negative_proxy" &&
      item.stockProxyEvidence !== "missing_baseline" &&
      !(isFiniteNumber(item.stockQty) && item.stockQty < 0),
  );
  const topInventory = knownRiskInventory[0];
  const topPrice = price[0];
  const topTrend = trend.find((item) => item.externalTrendScore != null && item.externalTrendSource != null);

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h3 className="text-sm font-semibold text-[var(--text-primary)]">Pregled signala</h3>
          <p className="text-[11px] text-[var(--text-secondary)]">
            Poslednji podaci iz keša analitike za potražnju, zalihe, cene i trend.
          </p>
        </div>
        <div className="text-[11px] text-[var(--text-secondary)]">Pregled: {formatDateLabel(asOfDate)}</div>
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <SignalCard
          title="Potražnja"
          subtitle="Najjači artikli po ubrzanju tražnje i aktuelnoj brzini prodaje."
          accent={PAL.blue}
          summaryLabel="Najveće ubrzanje"
          summaryValue={topDemand ? formatDemandSignal(topDemand) : ANALYTICS_UNAVAILABLE_LABEL}
          summaryMeta={topDemand ? `${topDemand.productName} | brzina ${formatCompact(topDemand.salesVelocity)}` : "Nema signala potražnje"}
        >
          {demand.slice(0, 4).map((item) => (
            <SignalRow
              key={`${item.articleId}-${item.storeId}-${item.date}`}
              title={item.productName}
              subtitle={`${item.storeName} | ${item.category} | pokriće ${item.storeCoverage}`}
              badge={formatDemandSignal(item)}
              badgeColor={demandSignalTone(item)}
            />
          ))}
        </SignalCard>

        <SignalCard
          title="Rizik zaliha"
          subtitle="Najrizičniji artikli po mrtvoj zalihi i kratkom pokriću."
          accent={PAL.red}
          summaryLabel="Najveći rizik"
          summaryValue={topInventory ? formatPercent(topInventory.deadStockRisk, 0) : ANALYTICS_UNAVAILABLE_LABEL}
          summaryMeta={
            topInventory
              ? `${topInventory.productName} | pokriće ${topInventory.daysOfCover?.toFixed(1) ?? ANALYTICS_UNAVAILABLE_LABEL} d`
              : "Nema potvrđenog rizika zaliha"
          }
        >
          {knownRiskInventory.slice(0, 4).map((item) => (
            <SignalRow
              key={`${item.articleId}-${item.date}`}
              title={item.productName}
              subtitle={`${item.category} | ${inventoryStockEvidenceLabel(item)}`}
              badge={formatPercent(item.deadStockRisk, 0)}
              badgeColor={signalTone(item.deadStockRisk, 0.5, PAL.red, PAL.yellow)}
            />
          ))}
        </SignalCard>

        <SignalCard
          title="Cene i marža"
          subtitle="Artikli sa najjačim signalom marže i vidljivom dubinom popusta."
          accent={PAL.green}
          summaryLabel="Najbolja marža"
          summaryValue={topPrice ? formatPercent(topPrice.marginPct, 0) : ANALYTICS_UNAVAILABLE_LABEL}
          summaryMeta={topPrice ? `${topPrice.productName} | popust ${formatPercent(topPrice.discountDepth, 0)}` : "Nema signala cene"}
        >
          {price.slice(0, 4).map((item) => (
            <SignalRow
              key={`${item.articleId}-${item.priceDate}`}
              title={item.productName}
              subtitle={`${item.category} | neto ${formatCompact(item.netPrice)} | indeks ${item.priceIndexVsCategory?.toFixed(2) ?? ANALYTICS_UNAVAILABLE_LABEL}`}
              badge={formatPercent(item.marginPct, 0)}
              badgeColor={signalTone(item.marginPct, 0.2, PAL.green, PAL.yellow)}
            />
          ))}
        </SignalCard>

        <SignalCard
          title="Momentum trenda"
          subtitle="Spoj eksternog trend skora i lokalnog ubrzanja prodaje."
          accent={PAL.cyan}
          summaryLabel="Najveći eksterni skor"
          summaryValue={topTrend?.externalTrendScore == null ? ANALYTICS_UNAVAILABLE_LABEL : topTrend.externalTrendScore.toFixed(1)}
          summaryMeta={
            topTrend
              ? `${topTrend.productName} | lokalno ${formatSignedPercent(topTrend.localSalesAcceleration, 1)} | ${externalTrendEvidenceLabel(topTrend)}`
              : trend.length > 0
                ? "Eksterni skor nije dostupan za prikazane artikle"
                : "Nema trend signala"
          }
        >
          {trend.slice(0, 4).map((item) => (
            <SignalRow
              key={`${item.articleId}-${item.signalDate}`}
              title={item.productName}
              subtitle={`${item.category} | entropija ${item.trendEntropy?.toFixed(2) ?? ANALYTICS_UNAVAILABLE_LABEL} | ${item.supplierName} | ${externalTrendEvidenceLabel(item)}`}
              badge={item.externalTrendScore == null ? ANALYTICS_UNAVAILABLE_LABEL : item.externalTrendScore.toFixed(1)}
              badgeColor={signalTone(item.externalTrendScore, 50, PAL.cyan, PAL.yellow)}
            />
          ))}
        </SignalCard>
      </div>
    </div>
  );
}
