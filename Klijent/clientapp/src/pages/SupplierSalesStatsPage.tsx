import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { getStores } from "../services/analyticsApi";
import {
  getSupplierSalesStats,
  type SupplierFootwearBreakdown,
  type SupplierSalesStat,
  type SupplierSalesStatsResponse,
} from "../services/supplierSalesStatsApi";
import type { StoreOption } from "../types/analytics";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsControlBar, {
  type AnalyticsControlBarChip,
  type AnalyticsControlBarField,
} from "../components/analytics/AnalyticsControlBar";
import AnalyticsDataTable from "../components/analytics/AnalyticsDataTable";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import AnalyticsUnknownLink from "../components/analytics/AnalyticsUnknownLink";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import KpiExplainButton from "../components/analytics/KpiExplainButton";
import InfoTip from "../components/ui/InfoTip";
import UltraSpinner from "../components/ui/UltraSpinner";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { getDataScope } from "../utils/dataScope";
import { CHART_TOOLTIP_STYLE, CHART_TOOLTIP_LABEL_STYLE } from "../utils/chartTooltipStyle";
import { fmtPct, fmtQty, fmtRsd, fmtSignedPct, getPresetRange, formatDate } from "../utils/analyticsFormatters";
import {
  analyticsMetricDescriptions,
  buildPopMetricDescription,
  buildPrePostNivelacijaImpactDescription,
  canonicalTerms,
} from "../utils/analyticsMetricDescriptions";
import {
  normalizeRecommendationPct,
  normalizeRecommendationQualityStatus,
  recommendationQualityLabel,
  recommendationQualityStyle,
  recommendationReasonHintFromCode,
  RECOMMENDATION_SIGNAL_UNAVAILABLE,
  type RecommendationQualityStatus,
} from "../utils/canonicalRecommendationSemantics";
import { qualityTierIcon, qualityTierClass, tierNeedsWarning, buildCoverageTooltip, buildRecommendationCaveat, buildMarginDetailNote, buildSnapshotBadgeLabel, buildSnapshotTooltip } from "../utils/marginQuality";
import type { SupplierEmbeddedPageProps } from "./supplierSharedState";
import "./SupplierSalesStatsPage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
type SortDir = "asc" | "desc";
type SortField =
  | "dobavljacNaziv"
  | "ukupanPromet"
  | "ukupnaKolicina"
  | "totalCost"
  | "sharePct"
  | "marginContribution"
  | "marginPct"
  | "shareOfMarginContribution"
  | "popRevenueChangePct"
  | "prePostNivelacijaRevenueImpactPct"
  | "status";
type DecisionStatus = "increase_focus" | "maintain" | "review" | "do_not_trust" | "insufficient_data";

type ActiveFilters = {
  fromDate: string;
  toDate: string;
  sezonaId: number | null;
  storeId: number | null;
};

type DecisionSupplier = SupplierSalesStat & {
  sharePct: number;
  totalCost: number;
  shareOfMarginContribution: number;
  shareOfUnits: number;
  reliabilityPct: number;
  reliabilityAvailable: boolean;
  splitCoveragePct: number;
  confidencePct: number;
  confidenceAvailable: boolean;
  primaryFootwearType: string;
  primaryFootwearTypeSharePct: number;
  footwearTypeCount: number;
  footwearBreakdown: SupplierFootwearBreakdown[];
  status: DecisionStatus;
  statusLabel: string;
  statusReason: string;
  reasonCodes: string[];
  dataQualityStatus: RecommendationQualityStatus;
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  increase_focus: 5,
  maintain: 4,
  review: 3,
  insufficient_data: 2,
  do_not_trust: 1,
};

const decisionColumns: AnalyticsTableColumn<DecisionSupplier>[] = [
  { key: "dobavljacNaziv", header: "Dobavljač", dataType: "text" },
  { key: "ukupanPromet", header: "Promet", dataType: "currency" },
  { key: "ukupnaKolicina", header: "Količina", dataType: "number" },
  { key: "totalCost", header: "Nabavna vrednost", dataType: "currency" },
  { key: "sharePct", header: "Udeo prometa %", dataType: "percent" },
  { key: "primaryFootwearType", header: "Vodeća vrsta obuće", dataType: "text" },
  { key: "primaryFootwearTypeSharePct", header: "Udeo vodeće vrste %", dataType: "percent" },
  { key: "marginContribution", header: canonicalTerms.marginContribution.label, dataType: "currency" },
  { key: "marginPct", header: canonicalTerms.marginPct.label, dataType: "percent" },
  { key: "marginQualityLabel", header: "Kvalitet marže", dataType: "text" },
  { key: "shareOfMarginContribution", header: `Udeo u ${canonicalTerms.marginContribution.label} %`, dataType: "percent" },
  { key: "popRevenueChangePct", header: "PoP trend %", dataType: "percent" },
  { key: "status", header: "Preporuka", dataType: "text" },
];

const REASON_CODE_LABELS: Record<string, string> = {
  unknown_entity: "Nepoznat entitet",
  new_entity: "Novi dobavljač",
  previous_period_missing: "Nedostaje prethodni period",
  no_previous_baseline: "Nema prethodne baze",
  missing_cost_coverage: "Nedovoljno pokriće nabavne cene",
  limited_nivelacija_coverage: "Nizak pre/post split coverage",
  unknown_heavy_dataset: "Unknown-heavy dataset",
  tiny_sample: "Premali uzorak",
  unstable_margin: "Nestabilna marža",
  pop_unavailable: "PoP nije dostupan",
};

const CHART_AXIS_TICK = { fill: "var(--dashboard-chart-axis, var(--text-muted, #8ad5a8))", fontSize: 12, fontWeight: 600 };
const CHART_LEGEND_STYLE = { color: "var(--dashboard-chart-axis, var(--text-muted, #8ad5a8))", fontSize: 12, fontWeight: 600, paddingTop: 10 };
const CHART_CURSOR_STYLE = { fill: "var(--dashboard-chart-hover, rgba(102, 255, 126, 0.14))" };
const COMMAND_TOOLTIP_STYLE = {
  ...CHART_TOOLTIP_STYLE,
  background: "var(--dashboard-tooltip-bg, var(--surface-elevated, #0f172a))",
  border: "1px solid var(--dashboard-tooltip-border, var(--border-default, rgba(148, 163, 184, 0.35)))",
  boxShadow: "var(--dashboard-tooltip-shadow, 0 10px 24px rgba(0, 0, 0, 0.28))",
  borderRadius: "12px",
};
const COMMAND_TOOLTIP_LABEL_STYLE = {
  ...CHART_TOOLTIP_LABEL_STYLE,
  color: "var(--dashboard-tooltip-label, var(--text-primary, #dbffe8))",
  fontWeight: 700,
};

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function toUtcRange(fromDate: string, toDate: string): { fromDate: string; toDate: string } {
  return {
    fromDate: `${fromDate}T00:00:00Z`,
    toDate: `${toDate}T23:59:59Z`,
  };
}

function toDateOnly(value: string): string {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value.slice(0, 10);
  return parsed.toISOString().slice(0, 10);
}

function parseDateInputOrDefault(value: string | null, fallback: string): string {
  if (!value) return fallback;
  const normalized = toDateOnly(value);
  return /^\d{4}-\d{2}-\d{2}$/.test(normalized) ? normalized : fallback;
}

function parseNullableInt(value: string | null): number | null {
  if (!value) return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function smoothScrollToElement(element: HTMLElement, durationMs = 850): void {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    element.scrollIntoView({ behavior: "auto", block: "start" });
    return;
  }

  const startY = window.scrollY;
  const targetY = element.getBoundingClientRect().top + window.scrollY - 100;
  const distance = targetY - startY;
  if (Math.abs(distance) < 2) return;

  const startTime = performance.now();
  const easeInOutCubic = (t: number): number => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2);

  const tick = (now: number) => {
    const progress = clamp((now - startTime) / durationMs, 0, 1);
    const nextY = startY + distance * easeInOutCubic(progress);
    window.scrollTo(0, nextY);
    if (progress < 1) window.requestAnimationFrame(tick);
  };

  window.requestAnimationFrame(tick);
}

function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string {
  if (field !== activeField) return " ⇅";
  return dir === "asc" ? " ↑" : " ↓";
}

function isSortActive(field: SortField, activeField: SortField): boolean {
  return field === activeField;
}

function statusClass(status: DecisionStatus): string {
  if (status === "increase_focus") return "supplier-decision-status status-boost";
  if (status === "maintain") return "supplier-decision-status status-keep";
  if (status === "review") return "supplier-decision-status status-reduce";
  return "supplier-decision-status status-na";
}

function displayStatusLabel(status: DecisionStatus): string {
  if (status === "increase_focus") return "Pojačaj";
  if (status === "maintain") return "Zadrži";
  if (status === "review") return "Oprez";
  if (status === "do_not_trust") return "Smanji / Ne veruj";
  return "Nedovoljno podataka";
}

function displaySignalLabel(
  status: DecisionStatus,
  reliabilityAvailable: boolean,
  dataQualityStatus: RecommendationQualityStatus,
): string {
  if (status === "insufficient_data") return "Nedovoljno podataka";
  if (!reliabilityAvailable || dataQualityStatus === "insufficient_data" || dataQualityStatus === "critical") {
    return "Pomoćni signal";
  }

  return displayStatusLabel(status);
}
function trendClass(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "trend-neutral";
  if (value > 0) return "trend-up";
  if (value < 0) return "trend-down";
  return "trend-neutral";
}

type StatusTooltipData = {
  status: DecisionStatus;
  statusLabel: string;
  statusReason: string;
  sharePct: number;
  marginPct: number;
  popRevenueChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  previousPeriodRevenue: number | null;
  splitCoveragePct: number | null;
  reliabilityPct: number;
  reliabilityAvailable: boolean;
  confidencePct: number;
  confidenceAvailable: boolean;
  dataQualityStatus: RecommendationQualityStatus;
  reasonCodes: string[];
};

function formatReasonCode(code: string): string {
  return REASON_CODE_LABELS[code] ?? code;
}

function buildStatusTooltip(data: StatusTooltipData): string {
  const popText = data.popRevenueChangePct != null
    ? fmtSignedPct(data.popRevenueChangePct, 1)
    : data.previousPeriodRevenue != null && data.previousPeriodRevenue <= 0
      ? "Novo / bez prethodne baze"
      : "N/A";
  const impactText = data.prePostNivelacijaRevenueImpactPct != null
    ? fmtSignedPct(data.prePostNivelacijaRevenueImpactPct, 1)
    : "N/A";
  const reliabilityText = data.reliabilityAvailable ? fmtPct(data.reliabilityPct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const confidenceText = data.confidenceAvailable ? fmtPct(data.confidencePct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const qualityText = recommendationQualityLabel(data.dataQualityStatus);
  const reasonHints = data.reasonCodes
    .map((code) => recommendationReasonHintFromCode(code))
    .filter((hint): hint is string => Boolean(hint));
  const reasons = data.reasonCodes.length > 0
    ? data.reasonCodes.map(formatReasonCode).join(", ")
    : "Nema dodatnih napomena";
  const hintText = reasonHints.length > 0 ? ` | Napomene: ${reasonHints.join(" | ")}` : "";
  return `${data.statusLabel}: ${data.statusReason} | Udeo ${fmtPct(data.sharePct, 1)} | Marža ${fmtPct(data.marginPct, 1)} | PoP ${popText} | Nivelacija impact ${impactText} | Split pokrivanje ${fmtPct(data.splitCoveragePct, 1)} | Pouzdanost ${reliabilityText} | Sigurnost ${confidenceText} | Kvalitet ${qualityText} | Razlozi: ${reasons}${hintText}`;
}

function describePopMetric(supplier: SupplierSalesStat): { label: string; title: string; className: string } {
  if (supplier.popRevenueChangePct != null && !Number.isNaN(supplier.popRevenueChangePct)) {
    return {
      label: fmtSignedPct(supplier.popRevenueChangePct, 2),
      title: buildPopMetricDescription(supplier.previousPeriodRevenue),
      className: trendClass(supplier.popRevenueChangePct),
    };
  }

  if (supplier.previousPeriodRevenue != null && supplier.previousPeriodRevenue <= 0 && supplier.ukupanPromet > 0) {
    return {
      label: "Novo",
      title: "Dobavljač nije imao promet u prethodnom uporedivom periodu, pa PoP procenat nije smislen.",
      className: "trend-neutral",
    };
  }

  return {
    label: "N/A",
    title: "PoP trend nije dostupan jer ne postoji validna prethodna baza za poređenje.",
    className: "trend-neutral",
  };
}

function describeNivelacijaImpactMetric(supplier: SupplierSalesStat): { label: string; title: string; className: string } {
  if (supplier.prePostNivelacijaRevenueImpactPct != null && !Number.isNaN(supplier.prePostNivelacijaRevenueImpactPct)) {
    return {
      label: fmtSignedPct(supplier.prePostNivelacijaRevenueImpactPct, 2),
      title: buildPrePostNivelacijaImpactDescription(
        supplier.prePostNivelacijaRevenueCoveragePct,
        supplier.prePostSignalNote ? `Napomena: ${supplier.prePostSignalNote}` : undefined
      ),
      className: trendClass(supplier.prePostNivelacijaRevenueImpactPct),
    };
  }

  if (supplier.prePostSignalNote) {
    return {
      label: "Low signal",
      title: supplier.prePostSignalNote,
      className: "trend-neutral",
    };
  }

  if ((supplier.prePostNivelacijaRevenueCoveragePct ?? 0) <= 0) {
    return {
      label: "N/A",
      title: "Nema dovoljno uporedivih artikala sa prodajom i pre i posle prve nivelacije za pre/post impact metriku.",
      className: "trend-neutral",
    };
  }

  if (supplier.preNivelacijePromet <= 0 && supplier.posleNivelacijePromet > 0) {
    return {
      label: "Bez baze",
      title: "Postoji promet posle prve nivelacije, ali nema pre-nivelacija baze za smislen procenat promene.",
      className: "trend-neutral",
    };
  }

  return {
    label: "N/A",
    title: "Pre/post nivelacija impact nije dostupan za izabrani skup podataka.",
    className: "trend-neutral",
  };
}

function describePopUnitsMetric(supplier: SupplierSalesStat): { label: string; title: string; className: string } {
  if (supplier.popUnitsChangePct != null && !Number.isNaN(supplier.popUnitsChangePct)) {
    return {
      label: fmtSignedPct(supplier.popUnitsChangePct, 2),
      title: "Promena prodane kolicine u odnosu na prethodni uporedivi period iste dužine.",
      className: trendClass(supplier.popUnitsChangePct),
    };
  }

  if (supplier.previousPeriodUnits != null && supplier.previousPeriodUnits <= 0 && supplier.ukupnaKolicina > 0) {
    return {
      label: "Novo",
      title: "Dobavljač nije imao prodatu količinu u prethodnom uporedivom periodu.",
      className: "trend-neutral",
    };
  }

  return {
    label: "N/A",
    title: "Promena prodane količine nije dostupna jer nedostaje prethodni period za poređenje.",
    className: "trend-neutral",
  };
}

function describeNivelacijaUnitsImpactMetric(supplier: SupplierSalesStat): { label: string; title: string; className: string } {
  if (supplier.prePostNivelacijaUnitsImpactPct != null && !Number.isNaN(supplier.prePostNivelacijaUnitsImpactPct)) {
    const noteSuffix = supplier.prePostSignalNote ? ` Napomena: ${supplier.prePostSignalNote}` : "";
    return {
      label: fmtSignedPct(supplier.prePostNivelacijaUnitsImpactPct, 2),
      title: `Pre/post promena kolicine unutar uporedivih artikala sa prodajom i pre i posle prve nivelacije.${noteSuffix}`,
      className: trendClass(supplier.prePostNivelacijaUnitsImpactPct),
    };
  }

  if (supplier.prePostSignalNote) {
    return {
      label: "Low signal",
      title: supplier.prePostSignalNote,
      className: "trend-neutral",
    };
  }

  if ((supplier.prePostNivelacijaRevenueCoveragePct ?? 0) <= 0) {
    return {
      label: "N/A",
      title: "Nema dovoljno uporedivih artikala sa prodajom i pre i posle prve nivelacije za pre/post metriku kolicine.",
      className: "trend-neutral",
    };
  }

  if (supplier.preNivelacijeKolicina <= 0 && supplier.posleNivelacijeKolicina > 0) {
    return {
      label: "Bez baze",
      title: "Postoji količina posle prve nivelacije, ali nema pre-nivelacija baze za smislen procenat promene.",
      className: "trend-neutral",
    };
  }

  return {
    label: "N/A",
    title: "Pre/post impact kolicine nije dostupan za izabrani skup podataka.",
    className: "trend-neutral",
  };
}

function describeFootwearMix(supplier: Pick<DecisionSupplier, "primaryFootwearType" | "primaryFootwearTypeSharePct" | "footwearTypeCount">): string {
  if (supplier.footwearTypeCount <= 0 || supplier.primaryFootwearType === "N/A") {
    return "Nema dovoljno podataka o vrstama obuće za ovog dobavljača.";
  }

  if (supplier.primaryFootwearTypeSharePct >= 65) {
    return `${supplier.primaryFootwearType} nosi većinu prometa dobavljača (${fmtPct(supplier.primaryFootwearTypeSharePct, 1)}). Ovo je jak signal koncentracije asortimana.`;
  }

  if (supplier.primaryFootwearTypeSharePct >= 40) {
    return `${supplier.primaryFootwearType} je vodeća vrsta obuće, ali dobavljač ima i sekundarne segmente.`;
  }

  return `Promet je raspoređen kroz ${supplier.footwearTypeCount} vrste obuće — nema jedne dominantne kategorije.`;
}

function footwearMixTone(sharePct: number): string {
  if (sharePct >= 65) return "mix-high";
  if (sharePct >= 40) return "mix-medium";
  return "mix-balanced";
}

function buildFootwearRowTooltip(row: SupplierFootwearBreakdown): string {
  return `${row.tipObuceNaziv}: promet ${fmtRsd(row.ukupanPromet)}, količina ${fmtQty(row.ukupnaKolicina)}, maržni doprinos ${fmtRsd(row.marginContribution)}, marža ${fmtPct(row.marginPct, 1)}, udeo kod dobavljača ${fmtPct(row.shareOfSupplierRevenuePct, 1)}.`;
}

function normalizeName(value: string | null | undefined): string {
  return (value ?? "").trim().toUpperCase();
}

function buildStoreLabel(store: StoreOption): string {
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  return extras ? `${store.storeName} (${extras})` : store.storeName;
}

function supplierKey(supplier: { dobavljacId: number | null; dobavljacNaziv: string }): string {
  if (supplier.dobavljacId != null) return `id:${supplier.dobavljacId}`;
  return `name:${normalizeName(supplier.dobavljacNaziv)}`;
}

export default function SupplierSalesStatsPage({ embedded = false, sharedFilters, onTrustMetadataChange }: SupplierEmbeddedPageProps = {}) {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const requestIdRef = useRef(0);
  const detailSectionRef = useRef<HTMLElement>(null);

  const initialRange = useMemo(() => getPresetRange("30d"), []);
  const initialQueryFilters = useMemo(() => {
    const queryFromDate = sharedFilters?.fromDate ?? parseDateInputOrDefault(searchParams.get("fromDate"), initialRange.fromDate);
    const queryToDate = sharedFilters?.toDate ?? parseDateInputOrDefault(searchParams.get("toDate"), initialRange.toDate);
    const querySezonaId = parseNullableInt(searchParams.get("sezonaId"));
    const queryStoreId = sharedFilters ? sharedFilters.storeId : parseNullableInt(searchParams.get("storeId"));
    const hasExplicitDateQuery = searchParams.has("fromDate") || searchParams.has("toDate");
    const periodPreset: PeriodPreset = sharedFilters?.periodPreset ?? (hasExplicitDateQuery ? "custom" : "30d");

    return {
      periodPreset,
      fromDate: queryFromDate,
      toDate: queryToDate,
      sezonaId: querySezonaId,
      storeId: queryStoreId,
    };
  }, [initialRange.fromDate, initialRange.toDate, searchParams, sharedFilters]);
  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>(initialQueryFilters.periodPreset);
  const [fromDate, setFromDate] = useState(initialQueryFilters.fromDate);
  const [toDate, setToDate] = useState(initialQueryFilters.toDate);
  const [sezonaId, setSezonaId] = useState<number | null>(initialQueryFilters.sezonaId);
  const [storeId, setStoreId] = useState<number | null>(initialQueryFilters.storeId);
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({
    fromDate: initialQueryFilters.fromDate,
    toDate: initialQueryFilters.toDate,
    sezonaId: initialQueryFilters.sezonaId,
    storeId: initialQueryFilters.storeId,
  });

  const [stores, setStores] = useState<StoreOption[]>([]);
  const [data, setData] = useState<SupplierSalesStatsResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sortField, setSortField] = useState<SortField>("status");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedSupplierKey, setExpandedSupplierKey] = useState<string | null>(null);
  const activeDataScope = useMemo(
    () => sharedFilters?.dataScope ?? searchParams.get("dataScope") ?? getDataScope(),
    [searchParams, sharedFilters?.dataScope]
  );
  const includeUnknown = useMemo(
    () => (searchParams.get("includeUnknown") ?? "true").toLowerCase() !== "false",
    [searchParams]
  );
  const focus = useMemo(() => searchParams.get("focus") ?? "", [searchParams]);
  const focusSupplierId = useMemo(() => searchParams.get("supplierId"), [searchParams]);
  const activeSupplierId = sharedFilters?.supplierId ?? parseNullableInt(focusSupplierId);

  const invalidRange = useMemo(() => {
    if (!fromDate || !toDate) return false;
    return new Date(fromDate) > new Date(toDate);
  }, [fromDate, toDate]);

  useEffect(() => {
    const queryFromDate = sharedFilters?.fromDate ?? parseDateInputOrDefault(searchParams.get("fromDate"), activeFilters.fromDate);
    const queryToDate = sharedFilters?.toDate ?? parseDateInputOrDefault(searchParams.get("toDate"), activeFilters.toDate);
    const querySezonaId = parseNullableInt(searchParams.get("sezonaId"));
    const queryStoreId = sharedFilters ? sharedFilters.storeId : parseNullableInt(searchParams.get("storeId"));
    const hasExplicitDateQuery = searchParams.has("fromDate") || searchParams.has("toDate");
    const queryPreset: PeriodPreset = sharedFilters?.periodPreset ?? (hasExplicitDateQuery ? "custom" : "30d");

    const isSame =
      activeFilters.fromDate === queryFromDate &&
      activeFilters.toDate === queryToDate &&
      activeFilters.sezonaId === querySezonaId &&
      activeFilters.storeId === queryStoreId;

    if (isSame) return;

    setPeriodPreset(queryPreset);
    setFromDate(queryFromDate);
    setToDate(queryToDate);
    setSezonaId(querySezonaId);
    setStoreId(queryStoreId);
    setActiveFilters({
      fromDate: queryFromDate,
      toDate: queryToDate,
      sezonaId: querySezonaId,
      storeId: queryStoreId,
    });
  }, [
    activeFilters.fromDate,
    activeFilters.sezonaId,
    activeFilters.storeId,
    activeFilters.toDate,
    searchParams,
    sharedFilters,
  ]);

  useEffect(() => {
    const loadStores = async () => {
      try {
        const items = await getStores(true);
        setStores(items);
      } catch {
        // Preserve the last known store list on transient failures instead of faking an empty filter set.
      }
    };

    void loadStores();
  }, []);

  const load = useCallback(async (filters: ActiveFilters, signal?: AbortSignal) => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const currentRange = toUtcRange(filters.fromDate, filters.toDate);

      const currentResult = await getSupplierSalesStats({
        ...currentRange,
        sezonaId: filters.sezonaId,
        storeId: filters.storeId,
        dataScope: activeDataScope,
        signal,
      });

      if (requestId !== requestIdRef.current) return;
      setData(currentResult);
      setLoading(false);

    } catch (reason) {
      if (reason instanceof DOMException && reason.name === "AbortError") {
        return;
      }
      if (requestId !== requestIdRef.current) return;
      setData(null);
      setLoading(false);
      setError(reason instanceof Error ? reason.message : "Greška pri učitavanju podataka o dobavljačima.");
    }
  }, [activeDataScope]);

  useEffect(() => {
    const controller = new AbortController();
    void load(activeFilters, controller.signal);
    return () => controller.abort();
  }, [activeFilters, load]);

  const decisionSuppliers = useMemo<DecisionSupplier[]>(() => {
    const suppliers = data?.suppliers ?? [];
    if (suppliers.length === 0) return [];

    const totalRevenue = data?.totals.ukupanPromet ?? suppliers.reduce((sum, item) => sum + item.ukupanPromet, 0);
    const totalMarginContribution = data?.totals.ukupanMarzniDoprinos ?? suppliers.reduce((sum, item) => sum + item.marginContribution, 0);
    const totalUnits = data?.totals.ukupnaKolicina ?? suppliers.reduce((sum, item) => sum + item.ukupnaKolicina, 0);

    return suppliers.map((supplier) => {
      const sharePct = supplier.sharePct ?? (totalRevenue > 0 ? (supplier.ukupanPromet / totalRevenue) * 100 : 0);
      const totalCost = supplier.totalCost ?? Math.max(0, supplier.revenueWithCost - supplier.marginContribution);
      const shareOfMarginContribution = supplier.shareOfMarginContribution
        ?? supplier.shareOfProfit
        ?? (totalMarginContribution > 0 ? (supplier.marginContribution / totalMarginContribution) * 100 : 0);
      const shareOfUnits = supplier.shareOfUnits ?? (totalUnits > 0 ? (supplier.ukupnaKolicina / totalUnits) * 100 : 0);
      const splitCoveragePct = supplier.prePostNivelacijaRevenueCoveragePct ?? 0;
      const recommended = supplier.recommendation;
      const status = (recommended?.status ?? (supplier.isUnknown ? "do_not_trust" : "insufficient_data")) as DecisionStatus;
      const statusReason = recommended?.summary
        ?? (supplier.isUnknown
          ? "Dobavljač je nepoznat u master podacima; signal nije pouzdan za odluku."
          : "Nedovoljno podataka za pouzdanu preporuku.");
      const confidencePctValue = normalizeRecommendationPct(recommended?.confidencePct);
      const reliabilityPctValue = normalizeRecommendationPct(recommended?.reliabilityPct ?? supplier.reliabilityPct);
      const confidenceAvailable = confidencePctValue != null;
      const reliabilityAvailable = reliabilityPctValue != null;
      const normalizedConfidencePct = confidencePctValue ?? 0;
      const reasonCodes = recommended?.reasonCodes ?? [];
      const dataQualityStatus = normalizeRecommendationQualityStatus(recommended?.dataQualityStatus);
      const normalizedReliabilityPct = reliabilityPctValue ?? 0;
      const statusLabel = displaySignalLabel(status, reliabilityAvailable, dataQualityStatus);
      const footwearBreakdown = supplier.footwearBreakdown ?? [];
      const primaryFootwearType = supplier.primaryFootwearType
        ?? footwearBreakdown[0]?.tipObuceNaziv
        ?? "N/A";
      const primaryFootwearTypeSharePct = supplier.primaryFootwearTypeSharePct
        ?? footwearBreakdown[0]?.shareOfSupplierRevenuePct
        ?? 0;
      const footwearTypeCount = supplier.footwearTypeCount ?? footwearBreakdown.length;

      return {
        ...supplier,
        sharePct,
        totalCost,
        shareOfMarginContribution,
        shareOfUnits,
        reliabilityPct: normalizedReliabilityPct,
        reliabilityAvailable,
        splitCoveragePct,
        confidencePct: normalizedConfidencePct,
        confidenceAvailable,
        primaryFootwearType,
        primaryFootwearTypeSharePct,
        footwearTypeCount,
        footwearBreakdown,
        status,
        statusLabel,
        statusReason,
        reasonCodes,
        dataQualityStatus,
      };
    });
  }, [data?.suppliers, data?.totals.ukupanPromet]);

  const sortedSuppliers = useMemo(() => {
    const rows = [...decisionSuppliers];
    return rows.sort((a, b) => {
      if (a.isUnknown !== b.isUnknown) {
        return a.isUnknown ? 1 : -1;
      }

      let compare = 0;

      if (sortField === "dobavljacNaziv") {
        compare = a.dobavljacNaziv.localeCompare(b.dobavljacNaziv, "sr");
      } else if (sortField === "ukupanPromet") {
        compare = a.ukupanPromet - b.ukupanPromet;
      } else if (sortField === "ukupnaKolicina") {
        compare = a.ukupnaKolicina - b.ukupnaKolicina;
      } else if (sortField === "totalCost") {
        compare = a.totalCost - b.totalCost;
      } else if (sortField === "sharePct") {
        compare = a.sharePct - b.sharePct;
      } else if (sortField === "marginContribution") {
        compare = a.marginContribution - b.marginContribution;
      } else if (sortField === "marginPct") {
        compare = a.marginPct - b.marginPct;
      } else if (sortField === "shareOfMarginContribution") {
        compare = a.shareOfMarginContribution - b.shareOfMarginContribution;
      } else if (sortField === "popRevenueChangePct") {
        compare = (a.popRevenueChangePct ?? -9999) - (b.popRevenueChangePct ?? -9999);
      } else if (sortField === "prePostNivelacijaRevenueImpactPct") {
        compare = (a.prePostNivelacijaRevenueImpactPct ?? -9999) - (b.prePostNivelacijaRevenueImpactPct ?? -9999);
      } else if (sortField === "status") {
        compare = STATUS_PRIORITY[a.status] - STATUS_PRIORITY[b.status];
      }

      if (compare === 0) {
        compare = a.confidencePct - b.confidencePct;
      }

      if (compare === 0) {
        compare = a.ukupanPromet - b.ukupanPromet;
      }

      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionSuppliers, sortDir, sortField]);

  const visibleSuppliers = useMemo(
    () => {
      const baseRows = includeUnknown ? sortedSuppliers : sortedSuppliers.filter((row) => !row.isUnknown);
      if (activeSupplierId == null) return baseRows;
      return baseRows.filter((row) => row.dobavljacId === activeSupplierId);
    },
    [activeSupplierId, includeUnknown, sortedSuppliers]
  );

  const selectedSupplier = useMemo(
    () => visibleSuppliers.find((row) => supplierKey(row) === expandedSupplierKey) ?? null,
    [expandedSupplierKey, visibleSuppliers]
  );

  const selectedFootwearRows = useMemo(
    () => [...(selectedSupplier?.footwearBreakdown ?? [])]
      .sort((a, b) => b.ukupanPromet - a.ukupanPromet)
      .slice(0, 8),
    [selectedSupplier?.footwearBreakdown]
  );

  useEffect(() => {
    if (!selectedSupplier && visibleSuppliers.length > 0 && expandedSupplierKey != null) {
      setExpandedSupplierKey(null);
    }
  }, [expandedSupplierKey, selectedSupplier, visibleSuppliers.length]);

  useEffect(() => {
    if (!focusSupplierId || visibleSuppliers.length === 0) return;

    const normalizedFocus = focusSupplierId.trim().toLowerCase();
    const match = visibleSuppliers.find((row) => {
      if (row.dobavljacId != null) {
        return String(row.dobavljacId) === normalizedFocus;
      }

      return normalizeName(row.dobavljacNaziv) === normalizeName(focusSupplierId);
    });

    if (!match) return;
    setExpandedSupplierKey(supplierKey(match));
  }, [focusSupplierId, visibleSuppliers]);

  useEffect(() => {
    if (!selectedSupplier || !detailSectionRef.current) return;
    const delay = 120;
    const timeoutId = window.setTimeout(() => {
      if (!detailSectionRef.current) return;
      smoothScrollToElement(detailSectionRef.current);
    }, delay);
    return () => window.clearTimeout(timeoutId);
  }, [selectedSupplier]);

  const totalRevenue = data?.totals.ukupanPromet ?? 0;
  const knownSuppliers = useMemo(
    () => visibleSuppliers.filter((row) => !row.isUnknown),
    [visibleSuppliers]
  );

  const top5SharePct = useMemo(() => {
    if (knownSuppliers.length === 0 || totalRevenue <= 0) return 0;
    const top5Revenue = [...knownSuppliers]
      .sort((a, b) => b.ukupanPromet - a.ukupanPromet)
      .slice(0, 5)
      .reduce((sum, row) => sum + row.ukupanPromet, 0);
    return (top5Revenue / totalRevenue) * 100;
  }, [knownSuppliers, totalRevenue]);

  const totalMarginContribution = useMemo(
    () => data?.totals.ukupanMarzniDoprinos ?? 0,
    [data?.totals.ukupanMarzniDoprinos]
  );

  const periodGrowthPct = useMemo(() => {
    return data?.totals.popRevenueChangePct ?? null;
  }, [data?.totals.popRevenueChangePct]);

  const concentrationData = useMemo(() => {
    if (knownSuppliers.length === 0) return [] as Array<{ name: string; sharePct: number }>;

    const ranked = [...knownSuppliers]
      .sort((a, b) => b.sharePct - a.sharePct);

    const topRows = ranked.slice(0, 6).map((row) => ({
      name: row.dobavljacNaziv,
      sharePct: Number(row.sharePct.toFixed(2)),
    }));

    const remaining = ranked.slice(6).reduce((sum, row) => sum + row.sharePct, 0);
    if (remaining > 0.1) {
      topRows.push({ name: "Ostali", sharePct: Number(remaining.toFixed(2)) });
    }

    return topRows;
  }, [knownSuppliers]);

  const comparisonData = useMemo(() => {
    if (knownSuppliers.length === 0) return [] as Array<{ name: string; udeoPrometa: number; udeoMarznogDoprinosa: number; marza: number }>;

    const ranked = [...knownSuppliers]
      .sort((a, b) => b.ukupanPromet - a.ukupanPromet);

    return ranked.slice(0, 8).map((row) => ({
      name: row.dobavljacNaziv,
      udeoPrometa: Number(row.sharePct.toFixed(1)),
      udeoMarznogDoprinosa: Number(row.shareOfMarginContribution.toFixed(1)),
      marza: Number(row.marginPct.toFixed(1)),
    }));
  }, [knownSuppliers]);

  const supplierCounts = useMemo(() => {
    const increaseFocus = knownSuppliers.filter((row) => row.status === "increase_focus").length;
    const maintain = knownSuppliers.filter((row) => row.status === "maintain").length;
    const review = knownSuppliers.filter((row) => row.status === "review").length;
    const doNotTrust = knownSuppliers.filter((row) => row.status === "do_not_trust").length;
    const insufficientData = knownSuppliers.filter((row) => row.status === "insufficient_data").length;
    return { increaseFocus, maintain, review, doNotTrust, insufficientData };
  }, [knownSuppliers]);

  const unknownSuppliers = useMemo(
    () => sortedSuppliers.filter((row) => row.isUnknown),
    [sortedSuppliers]
  );

  const activeSezonaLabel = useMemo(() => {
    if (activeFilters.sezonaId == null) return "Sve sezone";
    return data?.sezone.find((item) => item.id === activeFilters.sezonaId)?.naziv ?? String(activeFilters.sezonaId);
  }, [activeFilters.sezonaId, data?.sezone]);

  const emptyStateHint = useMemo(() => {
    if (!data || visibleSuppliers.length > 0) return null;
    if (!data.dataWindowFrom || !data.dataWindowTo) {
      return "Nema podataka za izabrane filtere.";
    }

    const selectedFrom = new Date(`${activeFilters.fromDate}T00:00:00Z`);
    const selectedTo = new Date(`${activeFilters.toDate}T23:59:59Z`);
    const dataFrom = new Date(data.dataWindowFrom);
    const dataTo = new Date(data.dataWindowTo);

    if (
      Number.isNaN(selectedFrom.getTime()) ||
      Number.isNaN(selectedTo.getTime()) ||
      Number.isNaN(dataFrom.getTime()) ||
      Number.isNaN(dataTo.getTime())
    ) {
      return "Nema podataka za izabrane filtere.";
    }

    if (selectedTo < dataFrom || selectedFrom > dataTo) {
      return `Izabrani period je van dostupnog raspona prodaje (${formatDate(data.dataWindowFrom)} - ${formatDate(data.dataWindowTo)}).`;
    }

    return "Nema podataka za izabrane filtere.";
  }, [activeFilters.fromDate, activeFilters.toDate, data, visibleSuppliers.length]);

  const showBlockingError = Boolean(error && !data);
  const showStaleError = Boolean(error && data);

  const qualityNotes = useMemo(() => {
    if (!data) return [] as string[];

    const notes: string[] = [];
    const splitCoverage = data.dataQuality.revenueWithNivelacijaSplitSharePct;
    const missingCostShare = data.dataQuality.missingCostRevenueSharePct;
    const historicalCostShare = missingCostShare == null ? null : Math.max(0, 100 - missingCostShare);
    const estimatedCostShare = data.dataQuality.estimatedCostRevenueSharePct;
    const unknownShare = data.dataQuality.unknownSupplierRevenueSharePct;

    if (splitCoverage != null && splitCoverage < 60) {
      notes.push(`Signal pre/posle nivelacije pokriva samo ${fmtPct(splitCoverage, 1)} ukupnog prometa, pa ga treba tumaciti kao delimican.`);
    }

    if (historicalCostShare != null && historicalCostShare < 100) {
      notes.push(`Istorijska nabavna cena dostupna je za ${fmtPct(historicalCostShare, 1)} prometa; za preostali promet maržu ne možemo potvrditi iz istorijskih transakcija.`);
    }

    if (estimatedCostShare != null && estimatedCostShare > 0) {
      notes.push(`Za ${fmtPct(estimatedCostShare, 1)} prometa nabavna cena je procenjena (bez direktnog troška) — maržu čitati oprezno.`);
    }

    if (unknownShare != null && unknownShare > 0) {
      notes.push(`Nepoznati/N-A dobavljači učestvuju sa ${fmtPct(unknownShare, 1)} ukupnog prometa.`);
    }

    const snapshotPct = data.totals.snapshotCostCoveragePct;
    if (data.totals.isSnapshotActive && snapshotPct != null && snapshotPct > 0) {
      notes.push(`Za ${fmtPct(snapshotPct, 1)} prometa trošak je stabilizovan zamrznutom procenom (snapshot). Ovo je reproduktivna procena, ne istorijska nabavna cena.`);
    }

    return notes;
  }, [data]);

  const headerDataQualityStatus = useMemo(() => {
    if (!data) return null;
    if ((data.suppliers ?? []).length === 0) return "insufficient_data";
    const missingCostShare = data.dataQuality.missingCostRevenueSharePct ?? 0;
    const unknownShare = data.dataQuality.unknownSupplierRevenueSharePct ?? 0;
    if (missingCostShare >= 50 || unknownShare >= 20) return "critical";
    if (qualityNotes.length > 0) return "warning";
    return "good";
  }, [data, qualityNotes.length]);

  const emptyStateVariant = useMemo<"no_data" | "insufficient_data" | "filtered_out" | null>(() => {
    if (!data || visibleSuppliers.length > 0) return null;
    if (headerDataQualityStatus === "insufficient_data") return "insufficient_data";
    if ((data.suppliers ?? []).length > 0) return "filtered_out";
    return "no_data";
  }, [data, headerDataQualityStatus, visibleSuppliers.length]);

  useEffect(() => {
    if (!embedded || !onTrustMetadataChange) return;
    if (!data) {
      onTrustMetadataChange(null);
      return;
    }

    onTrustMetadataChange({
      periodFrom: data.fromDate ?? activeFilters.fromDate,
      periodTo: data.toDate ?? activeFilters.toDate,
      lastRefreshAt: data.generatedAt ?? null,
      dataFreshnessStatus: "unknown",
      dataSource: `Supplier sales stats (scope: ${activeDataScope})`,
      dataQualityStatus: headerDataQualityStatus,
      recommendationAllowed: headerDataQualityStatus === "good" || headerDataQualityStatus === "warning",
      recommendationNote: "Pregled je canonical decision surface za dobavljače. Preporuke dolaze iz backenda.",
      emptyStateReason: emptyStateHint,
    });
  }, [activeDataScope, activeFilters.fromDate, activeFilters.toDate, data, embedded, emptyStateHint, headerDataQualityStatus, onTrustMetadataChange]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "fromDate", label: "Od", value: activeFilters.fromDate },
      { key: "toDate", label: "Do", value: activeFilters.toDate },
      { key: "sezonaId", label: "Sezona", value: activeSezonaLabel },
      { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "Svi objekti" },
      { key: "dataScope", label: "Opseg podataka", value: activeDataScope },
      { key: "supplierId", label: "Dobavljač", value: activeSupplierId ?? "Svi dobavljači" },
      { key: "includeUnknown", label: "Uključi nepoznate", value: includeUnknown ? "da" : "ne" },
    ],
    [activeDataScope, activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, activeSezonaLabel, activeSupplierId, includeUnknown]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
      { key: "suppliers", label: "Dobavljača", value: data?.totals.brojDobavljaca ?? 0 },
      { key: "unknownSuppliers", label: "Nepoznato/N-A", value: unknownSuppliers.length },
      { key: "marginCoverage", label: "Pokriće istorijskog troška %", value: fmtPct(data?.dataQuality.missingCostRevenueSharePct == null ? null : 100 - data.dataQuality.missingCostRevenueSharePct, 1) },
      { key: "fallbackCoverage", label: "Promet sa procenjenom nabavnom %", value: fmtPct(data?.dataQuality.estimatedCostRevenueSharePct, 1) },
      { key: "noCostCoverage", label: "Promet bez nabavne cene %", value: fmtPct(data?.dataQuality.missingCostRevenueSharePct, 1) },
      { key: "totalsPopTrend", label: "Ukupan PoP trend", value: fmtPct(data?.totals.popRevenueChangePct, 1) },
      { key: "totalsPrePostImpact", label: "Ukupan nivelacija uticaj", value: fmtPct(data?.totals.prePostNivelacijaRevenueImpactPct, 1) },
      { key: "splitCoverage", label: "Uporedivo pre/post pokrivanje", value: fmtPct(data?.dataQuality.revenueWithNivelacijaSplitSharePct, 1) },
      { key: "snapshotCoverage", label: "Zamrznuta procena (snapshot) %", value: fmtPct(data?.totals.snapshotCostCoveragePct, 1) },
      { key: "isSnapshotActive", label: "Snapshot aktivan", value: data?.totals.isSnapshotActive ? "da" : "ne" },
      { key: "increaseFocus", label: "Pojačaj fokus", value: supplierCounts.increaseFocus },
      { key: "maintain", label: "Zadrži", value: supplierCounts.maintain },
      { key: "review", label: "U pregledu", value: supplierCounts.review },
      { key: "doNotTrust", label: "Smanji / Ne veruj", value: supplierCounts.doNotTrust },
      { key: "insufficientData", label: "Nedovoljno podataka", value: supplierCounts.insufficientData },
    ],
    [
      data?.dataQuality.missingCostRevenueSharePct,
      data?.dataQuality.revenueWithNivelacijaSplitSharePct,
      data?.generatedAt,
      data?.totals.brojDobavljaca,
      data?.totals.popRevenueChangePct,
      data?.totals.prePostNivelacijaRevenueImpactPct,
      data?.totals.snapshotCostCoveragePct,
      data?.totals.isSnapshotActive,
      supplierCounts.increaseFocus,
      supplierCounts.maintain,
      supplierCounts.review,
      supplierCounts.doNotTrust,
      supplierCounts.insufficientData,
      unknownSuppliers.length,
      data?.dataQuality.estimatedCostRevenueSharePct,
    ]
  );

  const dataQualityContextQuery = useMemo(() => {
    const params = new URLSearchParams();
    const returnParams = new URLSearchParams();
    params.set("type", "missingSupplier");
    params.set("originTable", "supplier-sales-stats");
    params.set("fromDate", activeFilters.fromDate);
    params.set("toDate", activeFilters.toDate);
    params.set("focus", focus || "supplier-unknown");
    params.set("includeUnknown", includeUnknown ? "true" : "false");
    params.set("dataScope", activeDataScope);
    returnParams.set("fromDate", activeFilters.fromDate);
    returnParams.set("toDate", activeFilters.toDate);
    returnParams.set("dataScope", activeDataScope);
    returnParams.set("includeUnknown", includeUnknown ? "true" : "false");
    if (focus) returnParams.set("focus", focus);
    if (focusSupplierId) returnParams.set("supplierId", focusSupplierId);
    if (activeFilters.sezonaId != null) params.set("sezonaId", String(activeFilters.sezonaId));
    if (activeFilters.storeId != null) params.set("storeId", String(activeFilters.storeId));
    if (focusSupplierId) params.set("supplierId", focusSupplierId);
    if (activeFilters.sezonaId != null) returnParams.set("sezonaId", String(activeFilters.sezonaId));
    if (activeFilters.storeId != null) returnParams.set("storeId", String(activeFilters.storeId));
    returnParams.set("tab", "overview");
    params.set("returnTo", `/analytics/supplier?${returnParams.toString()}`);
    return params.toString();
  }, [
    activeDataScope,
    activeFilters.fromDate,
    activeFilters.sezonaId,
    activeFilters.storeId,
    activeFilters.toDate,
    focus,
    focusSupplierId,
    includeUnknown,
  ]);

  const openSupplierDetail = useCallback((supplier: DecisionSupplier) => {
    const recordId = supplier.dobavljacId != null
      ? String(supplier.dobavljacId)
      : `unknown-${encodeURIComponent(supplier.dobavljacNaziv)}`;

    const params = new URLSearchParams();
    params.set("fromDate", `${activeFilters.fromDate}T00:00:00Z`);
    params.set("toDate", `${activeFilters.toDate}T23:59:59Z`);
    if (activeFilters.sezonaId != null) params.set("sezonaId", String(activeFilters.sezonaId));
    if (activeFilters.storeId != null) params.set("storeId", String(activeFilters.storeId));
    params.set("dataScope", activeDataScope);
    params.set("includeUnknown", includeUnknown ? "true" : "false");
    params.set("focus", focus || "supplier-detail");
    params.set("supplierId", recordId);

    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "supplier-sales-stats",
        recordId,
        title: supplier.dobavljacNaziv,
        subtitle: "Supplier decision detail",
        columns: decisionColumns,
        row: supplier,
        metadata: toolbarFilters,
      })
    );

    navigate(`/analitika/supplier-sales-stats/${recordId}?${params.toString()}`, {
      state: { backgroundLocation: location },
    });
  }, [
    activeDataScope,
    activeFilters.fromDate,
    activeFilters.sezonaId,
    activeFilters.storeId,
    activeFilters.toDate,
    focus,
    includeUnknown,
    location,
    navigate,
    toolbarFilters,
  ]);

  const applyPreset = (preset: PeriodPreset) => {
    setPeriodPreset(preset);
    if (preset === "custom") return;
    const range = getPresetRange(preset);
    setSezonaId(null);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    commitFilters({ fromDate: range.fromDate, toDate: range.toDate, sezonaId: null, storeId });
  };

  const handleSeasonChange = (value: string) => {
    const parsed = value ? Number(value) : null;
    setSezonaId(parsed);
    setPeriodPreset("custom");

    if (parsed == null) {
      commitFilters({ fromDate, toDate, sezonaId: null, storeId });
      return;
    }

    const selected = data?.sezone.find((item) => item.id === parsed);
    if (!selected) {
      commitFilters({ fromDate, toDate, sezonaId: parsed, storeId });
      return;
    }
    const newFrom = toDateOnly(selected.datumOd);
    const newTo = toDateOnly(selected.datumDo);
    setFromDate(newFrom);
    setToDate(newTo);
    commitFilters({ fromDate: newFrom, toDate: newTo, sezonaId: parsed, storeId });
  };

  const commitFilters = (next: ActiveFilters) => {
    if (next.fromDate && next.toDate && new Date(next.fromDate) > new Date(next.toDate)) return;
    setActiveFilters(next);
    const p = new URLSearchParams(searchParams);
    p.set("fromDate", next.fromDate);
    p.set("toDate", next.toDate);
    if (next.sezonaId != null) p.set("sezonaId", String(next.sezonaId)); else p.delete("sezonaId");
    if (next.storeId != null) p.set("storeId", String(next.storeId)); else p.delete("storeId");
    p.set("dataScope", activeDataScope);
    p.set("includeUnknown", includeUnknown ? "true" : "false");
    if (focus) p.set("focus", focus); else p.delete("focus");
    p.delete("supplierId");
    setSearchParams(p, { replace: true });
  };

  const handleResetFilters = () => {
    const range = getPresetRange("30d");
    setPeriodPreset("30d");
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    setSezonaId(null);
    setStoreId(null);
    commitFilters({ fromDate: range.fromDate, toDate: range.toDate, sezonaId: null, storeId: null });
  };

  const handleSort = (field: SortField) => {
    setSortField((previousField) => {
      if (previousField === field) {
        setSortDir((previousDir) => (previousDir === "asc" ? "desc" : "asc"));
        return previousField;
      }

      setSortDir(field === "dobavljacNaziv" ? "asc" : "desc");
      return field;
    });
  };

  const handleIncludeUnknownChange = (value: boolean) => {
    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("includeUnknown", value ? "true" : "false");
    setSearchParams(nextParams, { replace: true });
  };

  const controlBarChips = useMemo<AnalyticsControlBarChip[]>(
    () => [
      {
        key: "scope",
        label: "Opseg",
        value: activeDataScope,
        tone: "info",
      },
      {
        key: "period",
        label: "Period",
        value: `${activeFilters.fromDate} → ${activeFilters.toDate}`,
        tone: "neutral",
      },
      {
        key: "rows",
        label: "Prikazano",
        value: `${visibleSuppliers.length.toLocaleString("sr-RS")} / ${(data?.suppliers?.length ?? 0).toLocaleString("sr-RS")}`,
        tone: visibleSuppliers.length < (data?.suppliers?.length ?? 0) ? "warning" : "success",
      },
    ],
    [activeDataScope, activeFilters.fromDate, activeFilters.toDate, data?.suppliers?.length, visibleSuppliers.length],
  );

  const controlBarFields = useMemo<AnalyticsControlBarField[]>(
    () => [
      {
        key: "preset",
        label: "Period",
        control: (
          <select value={periodPreset} onChange={(event) => applyPreset(event.target.value as PeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="180d">Poslednjih 180 dana</option>
            <option value="365d">Poslednjih 365 dana</option>
            <option value="custom">Prilagođeno</option>
          </select>
        ),
      },
      {
        key: "from",
        label: "Od",
        control: (
          <input
            type="date"
            value={fromDate}
            onChange={(event) => {
              const newFrom = event.target.value;
              setPeriodPreset("custom");
              setSezonaId(null);
              setFromDate(newFrom);
              if (newFrom.length === 10) {
                commitFilters({ fromDate: newFrom, toDate, sezonaId: null, storeId });
              }
            }}
          />
        ),
      },
      {
        key: "to",
        label: "Do",
        control: (
          <input
            type="date"
            value={toDate}
            onChange={(event) => {
              const newTo = event.target.value;
              setPeriodPreset("custom");
              setSezonaId(null);
              setToDate(newTo);
              if (newTo.length === 10) {
                commitFilters({ fromDate, toDate: newTo, sezonaId: null, storeId });
              }
            }}
          />
        ),
      },
      {
        key: "season",
        label: "Sezona",
        control: (
          <select value={sezonaId ?? ""} onChange={(event) => handleSeasonChange(event.target.value)}>
            <option value="">Sve sezone</option>
            {(data?.sezone ?? []).map((sezona) => (
              <option key={sezona.id} value={sezona.id}>
                {sezona.naziv}
              </option>
            ))}
          </select>
        ),
      },
      {
        key: "store",
        label: "Objekat",
        control: (
          <select
            value={storeId ?? ""}
            onChange={(event) => {
              const newStore = event.target.value ? Number(event.target.value) : null;
              setStoreId(newStore);
              commitFilters({ fromDate, toDate, sezonaId, storeId: newStore });
            }}
          >
            <option value="">Svi objekti</option>
            {stores.map((store) => (
              <option key={store.storeId} value={store.storeId}>
                {buildStoreLabel(store)}
              </option>
            ))}
          </select>
        ),
      },
      {
        key: "unknown",
        label: "Nepoznati",
        control: (
          <label className="supplier-decision-inline-check">
            <input
              type="checkbox"
              checked={includeUnknown}
              onChange={(event) => handleIncludeUnknownChange(event.target.checked)}
            />
            <span>Prikaži nepoznate</span>
          </label>
        ),
      },
    ],
    [
      commitFilters,
      data?.sezone,
      fromDate,
      handleSeasonChange,
      includeUnknown,
      periodPreset,
      sezonaId,
      storeId,
      stores,
      toDate,
    ],
  );
  const popTrendSortMarker = sortMarker("popRevenueChangePct", sortField, sortDir);
  const popTrendTooltip = analyticsMetricDescriptions.popRevenueChangePct;

  return (
    <div className={`supplier-decision-page ${embedded ? "supplier-decision-page--embedded" : ""}`}>
      {!embedded ? (
        <AnalyticsTrustHeader
          title="Dobavljači: Pregled"
          description="Canonical pregled prodaje po dobavljačima za poslovnu odluku. Preporuke dolaze iz backenda."
          periodFrom={data?.fromDate ?? activeFilters.fromDate}
          periodTo={data?.toDate ?? activeFilters.toDate}
          lastRefreshAt={data?.generatedAt ?? null}
          dataFreshnessStatus="unknown"
          dataSource="Supplier decision materialized view"
          dataQualityStatus={headerDataQualityStatus ?? null}
          mode="recommendation"
          recommendationNote="Ovo je glavni recommendation pogled. Skorkarta je dodatni signal u odvojenom tabu."
          emptyStateReason={!loading && !showBlockingError && emptyStateHint ? emptyStateHint : null}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          compact
        />
      ) : null}

      {!embedded ? (
        <AnalyticsControlBar
          title="Opseg i filteri"
          description="Period, sezona i objekat ostaju ovde; prioritetna lista ispod ostaje fokusirana na preporuku."
          chips={controlBarChips}
          primaryAction={{
            key: "reset",
            label: loading ? "Učitavanje..." : "Reset filtera",
            onClick: handleResetFilters,
            disabled: loading,
          }}
          secondaryActions={[
            {
              key: "data-quality",
              label: "Kvalitet podataka",
              to: "/analytics/data-quality",
              tone: "secondary",
            },
          ]}
          fields={controlBarFields}
        />
      ) : null}

      {invalidRange ? (
        <div className="supplier-decision-message error" role="alert">Datum 'od' ne može biti posle datuma 'do'.</div>
      ) : null}
      {showBlockingError ? (
        <AnalyticsErrorState
          title="Podaci trenutno nisu dostupni"
          message="Ne prikazujemo nule jer nije potvrdjeno da je period stvarno prazan."
          onRetry={() => {
            void load(activeFilters);
          }}
          helpHref="/analytics/data-quality"
        />
      ) : null}
      {showStaleError ? (
        <div className="supplier-decision-message info" role="status" aria-live="polite">
          Prikazujemo prethodno učitane podatke. Novi upit nije uspeo.
        </div>
      ) : null}
      {loading && !data ? (
        <div className="supplier-decision-loading" role="status" aria-live="polite">
          <UltraSpinner size="md" label="Učitavam podatke o dobavljačima" />
          <span>Učitavam podatke o dobavljačima...</span>
        </div>
      ) : null}
      {!loading && !showBlockingError && emptyStateHint ? (
        embedded ? (
          <div className="supplier-decision-message info" role="status" aria-live="polite">{emptyStateHint}</div>
        ) : (
          <AnalyticsEmptyState
            variant={emptyStateVariant ?? "no_data"}
            title="Nema dovoljno podataka za izabrani period"
            message={
              emptyStateVariant === "insufficient_data"
                ? "Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak."
                : emptyStateVariant === "filtered_out"
                  ? "Promenite filtere ili proširite period."
                  : emptyStateHint
            }
            reasons={[
              "Nije bilo prodaje u periodu",
              "Period je van dostupnog raspona prodaje",
              "Filteri (objekat/sezona) su previše uski",
              "Dobavljači nisu pravilno povezani",
            ]}
            actions={[
              { label: "Proširite period." },
              { label: "Uklonite uske filtere i pokušajte ponovo." },
              { label: "Otvori Data Quality", href: "/analytics/data-quality" },
            ]}
            dataQualityHref="/analytics/data-quality"
            refreshStatusHref="/admin/configuration?panel=workers"
            emptyReason={emptyStateHint}
            onRetry={() => {
              void load(activeFilters);
            }}
          />
        )
      ) : null}


      {data ? (
        <div
          className={`supplier-decision-content${loading ? " supplier-decision-content--refetching" : ""}`}
          aria-busy={loading || undefined}
        >
          {loading ? (
            <div className="supplier-decision-refetch-overlay" aria-hidden="true">
              <UltraSpinner size="sm" label="Osvežavam podatke" />
            </div>
          ) : null}
          <section className="supplier-decision-kpis">
            <article className="supplier-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="Vrednost prodaje kroz aktivne dobavljače u periodu.">
              <span>Ukupan promet <InfoTip text="Ukupna vrednost prodaje svih dobavljača u izabranom periodu. Formula: zbir prodajnih vrednosti svih prodajnih stavki u periodu. U promet ne ulaze operativni troškovi." /></span>
              <strong>{fmtRsd(totalRevenue)}</strong>
              <KpiExplainButton metricKey="revenue" ariaLabel="Kako je izračunat ukupan promet" />
            </article>
            <article className="supplier-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Ukupan obim prodaje izražen u komadima.">
              <span>Ukupno prodato <InfoTip text="Ukupan broj prodatih komada svih dobavljača u izabranom periodu." /></span>
              <strong>{fmtQty(data.totals.ukupnaKolicina)}</strong>
              <KpiExplainButton metricKey="unitsSold" ariaLabel="Kako je izračunat ukupan broj prodatih jedinica" />
            </article>
            <article className="supplier-decision-kpi analytics-kpi-card analytics-kpi-card--tone-neutral" data-note="Trošak robe pokriven istorijskim ili procenjenim ulazom.">
              <span>Ukupna nabavna vrednost <InfoTip text="Zbir troška robe za deo prometa sa dostupnim troškom. Formula: zbir količina x nabavna cena za stavke sa istorijskim ili procenjenim troškom. Operativni troškovi nisu uključeni." /></span>
              <strong>{fmtRsd(data.totals.ukupanTrosak ?? 0)}</strong>
              <KpiExplainButton metricKey="totalCost" ariaLabel="Kako je izračunata ukupna nabavna vrednost" />
            </article>
            <article className="supplier-decision-kpi analytics-kpi-card analytics-kpi-card--tone-value" data-note="Bruto doprinos marže pre operativnih troškova.">
              <span>{canonicalTerms.marginContribution.label} <InfoTip text={canonicalTerms.marginContribution.desc} /></span>
              <strong>{fmtRsd(totalMarginContribution)}</strong>
              <KpiExplainButton metricKey="marginContribution" ariaLabel="Kako je izračunat maržni doprinos" />
              <small
                className={`supplier-decision-kpi-badge ${qualityTierClass(data.totals.marginQualityTier)}`}
                title={data.totals.marginQualityTooltip ?? buildCoverageTooltip(data.totals.historicalCostCoveragePct, data.totals.estimatedCostCoveragePct, data.totals.noCostCoveragePct, fmtPct, data.totals.snapshotCostCoveragePct)}
              >
                {qualityTierIcon(data.totals.marginQualityTier)} {data.totals.marginQualityShortLabel ?? data.totals.marginQualityLabel}
              </small>
              {data.totals.isSnapshotActive && (data.totals.snapshotCostCoveragePct ?? 0) > 0 ? (
                <small
                  className="supplier-decision-kpi-badge quality-snapshot"
                  title={buildSnapshotTooltip(data.totals.snapshotCostCoveragePct ?? 0, data.totals.snapshotGeneratedAtUtc, fmtPct)}
                >
                  ❄ {buildSnapshotBadgeLabel(data.totals.snapshotGeneratedAtUtc)}
                </small>
              ) : null}
            </article>
            <article className="supplier-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="Signal kvaliteta miks marže kroz dobavljače.">
              <span>Prosečna marža <InfoTip text="Prosečan procenat maržnog doprinosa po dobavljaču. Formula po dobavljaču: maržni doprinos / promet sa dostupnim troškom × 100. Prikazana vrednost je aritmetički prosek po dobavljačima — nije ponderisana prometom." /></span>
              <strong>{fmtPct(data.totals.prosecnaMarza ?? null, 1)}</strong>
              <KpiExplainButton metricKey="grossMarginPct" ariaLabel="Kako je izračunata prosečna marža" />
            </article>
            <article className="supplier-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Koncentracija prometa na najjacim partnerima.">
              <span>Udeo top 5 dobavljača <InfoTip text="Procenat ukupnog prometa koji dolazi od pet dobavljača sa najvećim prometom. Formula: promet top 5 / ukupan promet x 100." /></span>
              <strong>{fmtPct(top5SharePct)}</strong>
              <KpiExplainButton metricKey="topSupplierRevenueShare" ariaLabel="Kako je izračunat udeo top 5 dobavljača" />
            </article>
            <article className="supplier-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Momentum prema prethodnom uporedivom periodu.">
              <span>Ukupan PoP trend <InfoTip text="Promena ukupnog prometa u odnosu na prethodni uporedivi period iste dužine. Formula: (trenutni promet − prethodni promet) / prethodni promet × 100. N/A ako prethodni period nije dostupan." /></span>
              <strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong>
              <KpiExplainButton metricKey="popRevenueChangePct" ariaLabel="Kako je izračunat Ukupan PoP trend" />
            </article>
          </section>

          {qualityNotes.length > 0 ? (
            <div className="supplier-decision-message info" role="status" aria-live="polite">
              <strong>Kvalitet podataka:</strong> {qualityNotes.join(" ")}
              <div className="supplier-decision-quality-actions">
                <Link to={`/analytics/data-quality?${dataQualityContextQuery}`} className="supplier-decision-quality-link">
                  Otvori pregled kvaliteta podataka
                </Link>
                {unknownSuppliers.length > 0 ? (
                  <Link to={`/analytics/data-quality?${dataQualityContextQuery}`} className="supplier-decision-quality-link">
                    Pregledaj artikle bez dobavljača
                  </Link>
                ) : null}
              </div>
            </div>
          ) : null}

          <section className="supplier-decision-panels">
            <article className="supplier-decision-card supplier-decision-card--chart analytics-surface-panel">
              <h2>Koncentracija prometa <InfoTip text="Grafikon prikazuje koliki udeo ukupnog prometa nose najveći dobavljači. Koristi samo promet, bez tumačenja profita ili neto marže." /></h2>
              <p>Top udeo prometa za brzu procenu gde je biznis koncentrisan.</p>
              {concentrationData.length > 0 ? (
                <div className="supplier-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={concentrationData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <defs>
                        <linearGradient id="supplierShareGradient" x1="0" y1="0" x2="1" y2="0">
                          <stop offset="0%" stopColor="var(--dashboard-gradient-share-start, var(--dashboard-accent, #33f28b))" />
                          <stop offset="100%" stopColor="var(--dashboard-gradient-share-end, var(--dashboard-secondary, #1ec8ff))" />
                        </linearGradient>
                      </defs>
                      <CartesianGrid strokeDasharray="2 6" stroke="var(--dashboard-grid, rgba(102, 255, 126, 0.16))" />
                      <XAxis type="number" tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} />
                      <Tooltip
                        contentStyle={COMMAND_TOOLTIP_STYLE}
                        labelStyle={COMMAND_TOOLTIP_LABEL_STYLE}
                        cursor={CHART_CURSOR_STYLE}
                        formatter={(value: number | string | undefined) => `${fmtPct(Number(value ?? 0), 2)}`}
                      />
                      <Legend wrapperStyle={CHART_LEGEND_STYLE} iconType="circle" iconSize={8} />
                      <Bar dataKey="sharePct" fill="url(#supplierShareGradient)" radius={[0, 10, 10, 0]} name="Udeo u prometu %" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="supplier-decision-empty">Nema podataka za grafikon koncentracije.</div>
              )}
            </article>

            <article className="supplier-decision-card supplier-decision-card--chart analytics-surface-panel">
              <h2>{canonicalTerms.revenue.label} vs {canonicalTerms.marginContribution.label} <InfoTip text="Grafikon poredi udeo u prometu i udeo u maržnom doprinosu. Maržni doprinos nije neto profit i ne uključuje operativne troškove. Ako je deo troška procenjen iz raspoloživih podataka, i ovaj signal treba čitati oprezno." /></h2>
              <p className="supplier-decision-chart-desc">Poređenje udela u prometu i udela u {canonicalTerms.marginContribution.label.toLowerCase()} - dobavljači s visokim prometom ne moraju imati i visok maržni doprinos.</p>
              {comparisonData.length > 0 ? (
                <div className="supplier-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={comparisonData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="2 6" stroke="var(--dashboard-grid, rgba(102, 255, 126, 0.16))" />
                      <XAxis type="number" tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} />
                      <Tooltip
                        contentStyle={COMMAND_TOOLTIP_STYLE}
                        labelStyle={COMMAND_TOOLTIP_LABEL_STYLE}
                        cursor={CHART_CURSOR_STYLE}
                        formatter={((value: any) => `${fmtPct(Number(value ?? 0), 1)}`) as any}
                      />
                      <Legend
                        wrapperStyle={CHART_LEGEND_STYLE}
                        iconType="circle"
                        iconSize={8}
                        itemSorter={(item) => (item.dataKey === "udeoPrometa" ? 0 : 1)}
                      />
                      <Bar dataKey="udeoPrometa" fill="var(--dashboard-accent, #66ff7e)" radius={[0, 6, 6, 0]} name="Udeo u prometu %" />
                      <Bar dataKey="udeoMarznogDoprinosa" fill="var(--dashboard-secondary, #1ec8ff)" radius={[0, 6, 6, 0]} name={`Udeo u ${canonicalTerms.marginContribution.label} %`} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="supplier-decision-empty">Nema podataka za poređenje.</div>
              )}
            </article>
          </section>

          <section className="supplier-decision-panels">
            <article className="supplier-decision-card analytics-surface-panel">
              <div className="supplier-decision-table-head">
                <div>
                  <h2>Prioritetna lista dobavljača</h2>
                  <div className="supplier-priority-chip-row" aria-label="Raspodela preporuka">
                    <span className="priority-chip priority-chip-boost">Pojačaj <strong>{supplierCounts.increaseFocus}</strong></span>
                    <span className="priority-chip priority-chip-keep">Zadrži <strong>{supplierCounts.maintain}</strong></span>
                    <span className="priority-chip priority-chip-watch">Oprez <strong>{supplierCounts.review}</strong></span>
                    <span className="priority-chip priority-chip-reduce">Smanji / Ne veruj <strong>{supplierCounts.doNotTrust}</strong></span>
                    <span className="priority-chip priority-chip-na">Nedovoljno podataka <strong>{supplierCounts.insufficientData}</strong></span>
                  </div>
                  <p className="supplier-decision-metric-note">
                    Preporuka uzima u obzir promet, količinu, maržni doprinos, maržni procenat i PoP trend.
                  </p>
                  {unknownSuppliers.length > 0 ? (
                    <p className="supplier-unknown-note">
                    Nepoznati dobavljači su prikazani na dnu i nisu uključeni u decision preporuke.
                    </p>
                  ) : null}
                </div>
              </div>

              <AnalyticsDataTable
                testId="supplier-sales-stats-data-table"
                rowCount={visibleSuppliers.length}
                truncationLabel={
                  (data.suppliers?.length ?? 0) > visibleSuppliers.length
                    ? `Ukupno u rezultatu: ${(data.suppliers?.length ?? 0).toLocaleString("sr-RS")} (deo redova je sakriven filterom ili nepoznatim dobavljačima)`
                    : undefined
                }
                toolbar={(
                  <AnalyticsTableToolbar
                    tableKey="supplier-sales-stats"
                    tableTitle="Podrška odluci - dobavljači"
                    columns={decisionColumns}
                    rows={visibleSuppliers}
                    filters={toolbarFilters}
                    metadata={toolbarMetadata}
                    defaultOrientation="landscape"
                  />
                )}
              >
                <table>
                  <thead>
                    <tr>
                      <th className={isSortActive("dobavljacNaziv", sortField) ? "is-sorted" : undefined}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("dobavljacNaziv", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("dobavljacNaziv", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("dobavljacNaziv", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("dobavljacNaziv")}
                        >
                          Dobavljač <span className="sort-indicator" aria-hidden="true">{sortMarker("dobavljacNaziv", sortField, sortDir)}</span> <InfoTip text="Naziv dobavljača. Klikom sortirate abecedno." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("ukupanPromet", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("ukupanPromet", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("ukupanPromet", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("ukupanPromet", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("ukupanPromet")}
                        >
                          Promet <span className="sort-indicator" aria-hidden="true">{sortMarker("ukupanPromet", sortField, sortDir)}</span> <InfoTip text="Ukupna vrednost prodaje u izabranom periodu (RSD)." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("ukupnaKolicina", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("ukupnaKolicina", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("ukupnaKolicina", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("ukupnaKolicina", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("ukupnaKolicina")}
                        >
                          Količina <span className="sort-indicator" aria-hidden="true">{sortMarker("ukupnaKolicina", sortField, sortDir)}</span> <InfoTip text="Ukupan broj prodatih komada." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("totalCost", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("totalCost", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("totalCost", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("totalCost", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("totalCost")}
                        >
                          Nabavna vrednost <span className="sort-indicator" aria-hidden="true">{sortMarker("totalCost", sortField, sortDir)}</span> <InfoTip text="Zbir troška robe za ovaj red. Formula: zbir količina x nabavna cena za stavke sa istorijskim ili procenjenim troškom. Operativni troškovi nisu uključeni." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("sharePct", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("sharePct", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("sharePct", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("sharePct", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("sharePct")}
                        >
                          Udeo u prometu <span className="sort-indicator" aria-hidden="true">{sortMarker("sharePct", sortField, sortDir)}</span> <InfoTip text="Koliki procenat ukupnog prometa čini ovaj dobavljač. Formula: promet dobavljača / ukupan promet svih prikazanih dobavljača x 100." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("marginContribution", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("marginContribution", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("marginContribution", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("marginContribution", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("marginContribution")}
                        >
                          {canonicalTerms.marginContribution.label} <span className="sort-indicator" aria-hidden="true">{sortMarker("marginContribution", sortField, sortDir)}</span> <InfoTip text={canonicalTerms.marginContribution.desc} />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("marginPct", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("marginPct", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("marginPct", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("marginPct", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("marginPct")}
                        >
                          {canonicalTerms.marginPct.label} <span className="sort-indicator" aria-hidden="true">{sortMarker("marginPct", sortField, sortDir)}</span> <InfoTip text={analyticsMetricDescriptions.marginPct} />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("shareOfMarginContribution", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("shareOfMarginContribution", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("shareOfMarginContribution", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("shareOfMarginContribution", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("shareOfMarginContribution")}
                        >
                          {`Udeo u ${canonicalTerms.marginContribution.label}`} <span className="sort-indicator" aria-hidden="true">{sortMarker("shareOfMarginContribution", sortField, sortDir)}</span> <InfoTip text="Koliki procenat ukupnog maržnog doprinosa čini ovaj dobavljač. Formula: maržni doprinos dobavljača / ukupan maržni doprinos svih prikazanih dobavljača x 100. Ovo nije udeo u profitu niti u neto zaradi." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("popRevenueChangePct", sortField) ? " is-sorted" : ""}`}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("popRevenueChangePct", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("popRevenueChangePct", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("popRevenueChangePct", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("popRevenueChangePct")}
                        >
                          PoP trend <span className="sort-indicator" aria-hidden="true">{popTrendSortMarker}</span> <InfoTip text={popTrendTooltip} />
                        </button>
                      </th>
                      <th className={isSortActive("status", sortField) ? "is-sorted" : undefined}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("status", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("status", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("status", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("status")}
                        >
                          Preporuka <span className="sort-indicator" aria-hidden="true">{sortMarker("status", sortField, sortDir)}</span> <InfoTip text={analyticsMetricDescriptions.recommendation} />
                        </button>
                      </th>
                      <th className="align-center">
                        Detalj <InfoTip text="Prikaži detaljan pregled po vrstama obuće, trendovima i kvalitetu podataka." />
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleSuppliers.length === 0 ? (
                      <tr>
                        <td colSpan={11} className="supplier-decision-empty-row">
                          Nema podataka za izabrane filtere.
                        </td>
                      </tr>
                    ) : (
                      visibleSuppliers.map((supplier, index) => {
                        const rowKey = supplierKey(supplier);
                        const rank = index + 1;
                        const isExpanded = expandedSupplierKey === rowKey;
                        const popMetric = describePopMetric(supplier);
                        const contributionVsRevenueMismatch = !supplier.isUnknown
                          && supplier.sharePct > 5
                          && supplier.shareOfMarginContribution < supplier.sharePct * 0.5;
                        const highContributionLowRevenue = !supplier.isUnknown
                          && supplier.shareOfMarginContribution > supplier.sharePct * 1.5
                          && supplier.sharePct < 10;
                        return (
                          <tr
                            key={rowKey}
                            className={[
                              isExpanded ? "expanded-row" : "",
                              supplier.isUnknown ? "supplier-unknown-row" : "",
                              contributionVsRevenueMismatch ? "supplier-mismatch-row" : "",
                              highContributionLowRevenue ? "supplier-high-profit-row" : "",
                              rank <= 3 ? `supplier-rank-row supplier-rank-row-${rank}` : "",
                            ].filter(Boolean).join(" ")}
                          >
                            <td>
                              <div className="supplier-name-cell">
                                <span className={`supplier-rank-badge ${rank <= 3 ? `rank-${rank}` : "rank-other"}`}>#{rank}</span>
                                {supplier.isUnknown ? (
                                  <span
                                    className="supplier-unknown-label"
                                    title="Artikli bez dodeljenog dobavljača u bazi"
                                  >
                                    {supplier.dobavljacNaziv}
                                  </span>
                                ) : (
                                  <AnalyticsUnknownLink
                                    value={supplier.dobavljacNaziv}
                                    issueType="missingSupplier"
                                    context={{
                                      originTable: "supplier-sales-stats",
                                      fromDate: activeFilters.fromDate,
                                      toDate: activeFilters.toDate,
                                      sezonaId: activeFilters.sezonaId,
                                      storeId: activeFilters.storeId,
                                      dataScope: activeDataScope,
                                    }}
                                  />
                                )}
                              </div>
                            </td>
                            <td className="analytics-data-table__numeric metric-strong">{fmtRsd(supplier.ukupanPromet)}</td>
                            <td className="analytics-data-table__numeric">{fmtQty(supplier.ukupnaKolicina)}</td>
                            <td className="analytics-data-table__numeric">{fmtRsd(supplier.totalCost)}</td>
                            <td className="analytics-data-table__numeric"><span className="metric-chip metric-chip-neutral">{fmtPct(supplier.sharePct, 1)}</span></td>
                            <td className="analytics-data-table__numeric metric-strong">{fmtRsd(supplier.marginContribution)}</td>
                            <td className="analytics-data-table__numeric">
                              <span>{fmtPct(supplier.marginPct, 1)}</span>
                              {tierNeedsWarning(supplier.marginQualityTier) ? (
                                <span className={`quality-pill ${qualityTierClass(supplier.marginQualityTier)}`} title={supplier.marginQualityTooltip ?? supplier.marginQualityLabel ?? ""}>
                                  marža
                                </span>
                              ) : null}
                            </td>
                            <td className="analytics-data-table__numeric">{fmtPct(supplier.shareOfMarginContribution, 1)}</td>
                            <td className="analytics-data-table__numeric" title={popMetric.title}>
                              <span className={`metric-chip trend-pill ${popMetric.className}`}>{popMetric.label}</span>
                            </td>
                            <td>
                              <div className="supplier-status-stack">
                                <span
                                  className={statusClass(supplier.status)}
                                  title={buildStatusTooltip(supplier)}
                                  aria-label={buildStatusTooltip(supplier)}
                                >
                                  {supplier.statusLabel}
                                </span>
                                {supplier.statusReason ? (
                                  <span className="supplier-status-reason-chip" title={supplier.statusReason}>
                                    Razlog <InfoTip text={supplier.statusReason} />
                                  </span>
                                ) : null}
                              </div>
                            </td>
                            <td className="align-center">
                              <button
                                type="button"
                                className="supplier-decision-detail-btn"
                                onClick={() => setExpandedSupplierKey(isExpanded ? null : rowKey)}
                              >
                                {isExpanded ? "Sakrij" : "Detalji"}
                              </button>
                            </td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </AnalyticsDataTable>
            </article>
          </section>

          {selectedSupplier ? (
            <section className="supplier-decision-detail" ref={detailSectionRef}>
              {/* ── Hero glava ── */}
              <div className="supplier-detail-hero-head">
                <div className="supplier-detail-hero-left">
                  <div className="supplier-detail-overline">Detaljan pregled dobavljača</div>
                  <h3 className="supplier-detail-name">{selectedSupplier.dobavljacNaziv}</h3>
                  <div className="supplier-detail-meta-row">
                    <span className="supplier-detail-meta-chip">
                      {activeFilters.fromDate} → {activeFilters.toDate}
                    </span>
                    {activeFilters.storeId != null ? (
                      <span className="supplier-detail-meta-chip">
                        {stores.find((s) => s.storeId === activeFilters.storeId)?.storeName ?? `Objekat ${activeFilters.storeId}`}
                      </span>
                    ) : null}
                  </div>
                </div>
                <div className="supplier-detail-hero-right">
                  <div
                    className={`${statusClass(selectedSupplier.status)} supplier-detail-status-badge`}
                    title={buildStatusTooltip(selectedSupplier)}
                  >
                    {selectedSupplier.statusLabel}
                  </div>
                  <button
                    type="button"
                    className="supplier-detail-open-btn"
                    onClick={() => openSupplierDetail(selectedSupplier)}
                    title="Otvori puni AI detalj sa preporukom, historijom i analizom artikala"
                  >
                    Puni detalj →
                  </button>
                  <button
                    type="button"
                    className="supplier-detail-close-btn"
                    onClick={() => setExpandedSupplierKey(null)}
                    title="Zatvori detalj"
                    aria-label="Zatvori detalj"
                  >
                    ✕
                  </button>
                </div>
              </div>

              {/* Sažetak razloga preporuke */}
              {selectedSupplier.statusReason ? (
                <div className="supplier-detail-reason-banner">
                  <span className="supplier-detail-reason-label">Razlog preporuke:</span>
                  <span>{selectedSupplier.statusReason}</span>
                  {selectedSupplier.reasonCodes.length > 0 ? (
                    <span className="supplier-detail-reason-codes">
                      {selectedSupplier.reasonCodes.map(formatReasonCode).join(" ? ")}
                    </span>
                  ) : null}
                </div>
              ) : null}

              {/* Poslovni pokazatelji */}
              <h4 className="supplier-detail-section-title">Poslovni pokazatelji</h4>
              <div className="supplier-decision-detail-grid">
                <article>
                  <span>Promet <InfoTip text="Ukupna vrednost prodaje ovog dobavljača u izabranom periodu. Formula: zbir prodajnih vrednosti stavki ovog dobavljača." /></span>
                  <strong>{fmtRsd(selectedSupplier.ukupanPromet)}</strong>
                </article>
                <article>
                  <span>Prodata količina <InfoTip text="Ukupan broj prodatih komada ovog dobavljača u izabranom periodu." /></span>
                  <strong>{fmtQty(selectedSupplier.ukupnaKolicina)}</strong>
                </article>
                <article>
                  <span>Nabavna vrednost <InfoTip text="Zbir troška robe za ovaj red. Formula: zbir količina x nabavna cena za stavke sa istorijskim ili procenjenim troškom. Operativni troškovi nisu uključeni." /></span>
                  <strong>{fmtRsd(selectedSupplier.totalCost)}</strong>
                </article>
                <article>
                  <span>{canonicalTerms.marginContribution.label} <InfoTip text={canonicalTerms.marginContribution.desc} /></span>
                  <strong className={selectedSupplier.marginContribution > 0 ? "trend-up" : selectedSupplier.marginContribution < 0 ? "trend-down" : ""}>
                    {fmtRsd(selectedSupplier.marginContribution)}
                  </strong>
                </article>
                <article>
                  <span>{canonicalTerms.marginPct.label} <InfoTip text={analyticsMetricDescriptions.marginPct} /></span>
                  <strong className={selectedSupplier.marginPct != null && selectedSupplier.marginPct > 0 ? "trend-up" : selectedSupplier.marginPct != null && selectedSupplier.marginPct < 0 ? "trend-down" : ""}>
                    {fmtSignedPct(selectedSupplier.marginPct, 2)}
                  </strong>
                </article>
                <article>
                  <span>Udeo u prometu <InfoTip text="Koliki deo ukupnog prometa čini ovaj dobavljač. Formula: promet dobavljača / ukupan promet svih prikazanih dobavljača x 100." /></span>
                  <strong>{fmtPct(selectedSupplier.sharePct, 1)}</strong>
                </article>
                <article>
                  <span>{`Udeo u ${canonicalTerms.marginContribution.label}`} <InfoTip text="Koliki procenat ukupnog maržnog doprinosa čini ovaj dobavljač. Formula: maržni doprinos dobavljača / ukupan maržni doprinos svih prikazanih dobavljača x 100. Ovo nije udeo u profitu niti u neto zaradi." /></span>
                  <strong>{fmtPct(selectedSupplier.shareOfMarginContribution, 1)}</strong>
                </article>
                <article>
                  <span>Udeo u količini <InfoTip text="Koliki deo ukupne prodate količine čini ovaj dobavljač (%)." /></span>
                  <strong>{fmtPct(selectedSupplier.shareOfUnits, 1)}</strong>
                </article>
                <article>
                  <span>Broj artikala <InfoTip text="Ukupan broj različitih proizvoda/stilova od ovog dobavljača." /></span>
                  <strong>{selectedSupplier.brojArtikalaUkupno}</strong>
                </article>
                <article>
                  <span>Vodeća vrsta obuće <InfoTip text="Vrsta obuće sa najvećim prometom kod ovog dobavljača u izabranom periodu." /></span>
                  <strong>{selectedSupplier.primaryFootwearType}</strong>
                </article>
                <article>
                  <span>Udeo vodeće vrste <InfoTip text="Procenat ukupnog prometa dobavljača koji potiče od vodeće vrste obuće. Formula: promet vodeće vrste / ukupan promet dobavljača × 100. Udeo iznad 65% signal je koncentracije asortimana." /></span>
                  <strong>{fmtPct(selectedSupplier.primaryFootwearTypeSharePct, 1)}</strong>
                </article>
              </div>

              <h4 className="supplier-detail-section-title">
                Asortiman po vrstama obuće <InfoTip text="Raspodela asortimana po vrstama obuće. Prikazuje gde dobavljač pravi promet i maržni doprinos, i da li jedan tip obuće dominira ukupnim rezultatom." />
              </h4>
              {selectedFootwearRows.length > 0 ? (
                <div className="supplier-footwear-breakdown">
                  <div className="supplier-footwear-summary">
                    <span className={`supplier-footwear-mix ${footwearMixTone(selectedSupplier.primaryFootwearTypeSharePct)}`}>
                      {selectedSupplier.primaryFootwearType}
                    </span>
                    <p>{describeFootwearMix(selectedSupplier)}</p>
                  </div>
                  <div className="supplier-footwear-table-wrap">
                    <table className="supplier-footwear-table">
                      <thead>
                        <tr>
                          <th>Vrsta obuće <InfoTip text="Vrsta obuće iz matičnih podataka artikla. 'Nepoznato' znači da artikli nemaju dodeljenu vrstu obuće u bazi." /></th>
                          <th className="align-right">Promet <InfoTip text="Ukupna vrednost prodaje za ovu kombinaciju dobavljač × vrsta obuće u izabranom periodu." /></th>
                          <th className="align-right">Udeo kod dobavljača <InfoTip text="Procenat ukupnog prometa dobavljača koji čini ova vrsta obuće. Formula: promet vrste / ukupan promet dobavljača × 100." /></th>
                          <th className="align-right">{canonicalTerms.marginContribution.label} <InfoTip text="Razlika prodajne i nabavne vrednosti za deo prometa gde je trošak dostupan ili procenjen. Operativni troškovi nisu uključeni." /></th>
                          <th className="align-right">{canonicalTerms.marginPct.label} <InfoTip text="Procenat maržnog doprinosa od prodaje. Formula: maržni doprinos / promet sa dostupnim troškom × 100. Ako je trošak procenjen, signal čitati oprezno." /></th>
                          <th className="align-right">PoP trend <InfoTip text="Promena prometa ove kombinacije u odnosu na prethodni uporedivi period iste dužine. N/A ako prethodni period nije dostupan." /></th>
                          <th className="align-right">Pokriće troška <InfoTip text="Udeo prometa za koji postoji direktna istorijska nabavna cena. Procenjeni i nedostajući troškovi prikazani su u opisu reda." /></th>
                        </tr>
                      </thead>
                      <tbody>
                        {selectedFootwearRows.map((row) => (
                          <tr key={`${row.tipObuceId ?? "unknown"}-${row.tipObuceNaziv}`} title={buildFootwearRowTooltip(row)}>
                            <td>
                              <div className="supplier-footwear-name">
                                <strong>{row.tipObuceNaziv}</strong>
                                <span>{row.brojArtikala} artikala - {fmtQty(row.ukupnaKolicina)}</span>
                              </div>
                            </td>
                            <td className="align-right metric-strong">{fmtRsd(row.ukupanPromet)}</td>
                            <td className="align-right">
                              <div className="supplier-footwear-share">
                                <span>{fmtPct(row.shareOfSupplierRevenuePct, 1)}</span>
                                <div className="supplier-footwear-share-track" aria-hidden="true">
                                  <div style={{ width: `${clamp(row.shareOfSupplierRevenuePct, 0, 100)}%` }} />
                                </div>
                              </div>
                            </td>
                            <td className={`align-right ${row.marginContribution > 0 ? "trend-up" : row.marginContribution < 0 ? "trend-down" : ""}`}>
                              {fmtRsd(row.marginContribution)}
                            </td>
                            <td className="align-right">
                              <span>{fmtPct(row.marginPct, 1)}</span>
                              {tierNeedsWarning(row.marginQualityTier) ? (
                                <span className={`quality-pill ${qualityTierClass(row.marginQualityTier)}`} title={row.marginQualityTooltip ?? row.marginQualityLabel ?? ""}>
                                  marza
                                </span>
                              ) : null}
                            </td>
                            <td className={`align-right ${trendClass(row.popRevenueChangePct)}`}>{fmtSignedPct(row.popRevenueChangePct, 1)}</td>
                            <td className="align-right">
                              <span>{fmtPct(row.historicalCostCoveragePct, 1)}</span>
                              <span className="supplier-footwear-cost-note" title={`Procenjeno: ${fmtPct(row.estimatedCostCoveragePct, 1)} | Bez troška: ${fmtPct(row.noCostCoveragePct, 1)} | Snapshot: ${fmtPct(row.snapshotCostCoveragePct, 1)}`}>
                                detalj
                              </span>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ) : (
                <div className="supplier-decision-empty supplier-footwear-empty">
                  Nema dovoljno podataka o vrstama obuće za ovog dobavljača u izabranom periodu.
                </div>
              )}

              {/* PoP trendovi */}
              <h4 className="supplier-detail-section-title">Trend u odnosu na prethodni period</h4>
              <div className="supplier-decision-detail-grid">
                <article>
                  <span>PoP trend prometa <InfoTip text="Promena vrednosti prometa u odnosu na prethodni uporedivi period iste dužine (%)." /></span>
                  <strong className={describePopMetric(selectedSupplier).className} title={describePopMetric(selectedSupplier).title}>
                    {describePopMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period promet <InfoTip text="Vrednost prometa u prethodnom uporedivom periodu iste dužine." /></span>
                  <strong>{selectedSupplier.previousPeriodRevenue != null ? fmtRsd(selectedSupplier.previousPeriodRevenue) : "N/A"}</strong>
                </article>
                <article>
                  <span>PoP trend kolicine <InfoTip text="Promena kolicine prodanih komada u odnosu na prethodni uporedivi period iste dužine (%)." /></span>
                  <strong className={describePopUnitsMetric(selectedSupplier).className} title={describePopUnitsMetric(selectedSupplier).title}>
                    {describePopUnitsMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period količina <InfoTip text="Količina prodatih komada u prethodnom uporedivom periodu iste dužine." /></span>
                  <strong>{selectedSupplier.previousPeriodUnits != null ? fmtQty(selectedSupplier.previousPeriodUnits) : "N/A"}</strong>
                </article>
              </div>

              {/* Nivelacija detalji */}
              <h4 className="supplier-detail-section-title">Nivelacija</h4>
              <div className="supplier-decision-detail-grid">
                <article>
                  <span>Uticaj na promet <InfoTip text="Procentualna promena prometa pre i posle prve nivelacije, merena samo na artiklima koji su imali prodaju u oba perioda." /></span>
                  <strong className={describeNivelacijaImpactMetric(selectedSupplier).className} title={describeNivelacijaImpactMetric(selectedSupplier).title}>
                    {describeNivelacijaImpactMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Uticaj na kolicinu <InfoTip text="Procentualna promena prodane kolicine pre i posle prve nivelacije, merena na artiklima koji su imali prodaju u oba perioda." /></span>
                  <strong className={describeNivelacijaUnitsImpactMetric(selectedSupplier).className} title={describeNivelacijaUnitsImpactMetric(selectedSupplier).title}>
                    {describeNivelacijaUnitsImpactMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Artikli sa nivelacijom <InfoTip text="Broj artikala koji su imali primenjenu nivelaciju, od ukupnog broja artikala ovog dobavljača." /></span>
                  <strong>{selectedSupplier.brojArtikalaSaNivelacijom} / {selectedSupplier.brojArtikalaUkupno}</strong>
                </article>
                <article>
                  <span>Pre/post pokrivanje <InfoTip text="Udeo prometa koji se može pratiti kroz pre/post nivelacija analizu — samo artikli sa prodajom u oba perioda ulaze u ovu metriku." /></span>
                  <strong>{fmtPct(selectedSupplier.prePostNivelacijaRevenueCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Uporedivi artikli <InfoTip text="Broj artikala koji su imali prodaju i pre i posle nivelacije — jedini koji daju merodavan signal o uticaju promene cene." /></span>
                  <strong>{selectedSupplier.prePostComparableArticleCount ?? 0}</strong>
                </article>
              </div>

              {/* Kvalitet podataka */}
              <h4 className="supplier-detail-section-title">Kvalitet podataka</h4>
              <div className="supplier-decision-detail-grid">
                <article>
                  <span>Kvalitet marže <InfoTip text="Klasifikacija pouzdanosti obračuna marže na osnovu pokrića nabavnom cenom: Potvrđena (≥80% iz istorije), Delimično (≥50% iz istorije), Procenjena (<50%), Bez troška (0% pokriće)." /></span>
                  <strong>
                    <span className={`supplier-decision-kpi-badge ${qualityTierClass(selectedSupplier.marginQualityTier)}`}>
                      {qualityTierIcon(selectedSupplier.marginQualityTier)} {selectedSupplier.marginQualityLabel}
                    </span>
                  </strong>
                </article>
                <article>
                  <span>Pouzdanost <InfoTip text="Indeks pouzdanosti preporuke (0–100%) — uzima u obzir pokriće troškom, dostupnost PoP podataka i konzistentnost signala." /></span>
                  <strong>{selectedSupplier.reliabilityAvailable ? fmtPct(selectedSupplier.reliabilityPct, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
                <article>
                  <span>Status kvaliteta podataka <InfoTip text="Good = zeleno i upotrebljivo. Warning = oprez. Critical = ne veruj bez rucne provere. Insufficient data = backend nije dostavio kompletan quality payload." /></span>
                  <strong style={recommendationQualityStyle(selectedSupplier.dataQualityStatus)}>{recommendationQualityLabel(selectedSupplier.dataQualityStatus)}</strong>
                </article>
                <article>
                  <span>Pokriće direktnom nabavnom % <InfoTip text="Procenat prometa za koji trošak potiče direktno sa prodajne stavke (istorijska nabavna cena). Formula: promet sa direktnim troškom / ukupan promet × 100." /></span>
                  <strong>{fmtPct(selectedSupplier.historicalCostCoveragePct ?? selectedSupplier.marginDataCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Promet sa procenjenom nabavnom % <InfoTip text="Procenat prometa gde je nabavna cena procenjena iz kataloga artikla — bez direktnog troška na stavci prodaje. Formula: promet sa procenjenom nabavnom / ukupan promet × 100. Operativni troškovi nisu uključeni." /></span>
                  <strong>{fmtPct(selectedSupplier.estimatedCostCoveragePct ?? selectedSupplier.fallbackCostCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Promet bez nabavne cene % <InfoTip text="Procenat prometa koji nema ni direktni ni procenjeni trošak — ne ulazi u obračun maržnog doprinosa ni marže %. Formula: promet bez troška / ukupan promet × 100." /></span>
                  <strong>{fmtPct(selectedSupplier.noCostCoveragePct, 1)}</strong>
                </article>
                {(selectedSupplier.snapshotCostCoveragePct ?? 0) > 0 ? (
                  <article>
                    <span>Zamrznuta procena (snapshot) % <InfoTip text="Procenat prometa gde je trošak stabilizovan zamrznutom procenom (snapshot) radi reproduktivnosti izveštaja. Ovo nije istorijska nabavna cena sa trenutka prodaje." /></span>
                    <strong>{fmtPct(selectedSupplier.snapshotCostCoveragePct, 1)}</strong>
                  </article>
                ) : null}
                <article>
                  <span>Sigurnost preporuke <InfoTip text="Ukupna sigurnost sistemske preporuke, bazirana na svim dostupnim signalima (0–100%)." /></span>
                  <strong>{selectedSupplier.confidenceAvailable ? fmtPct(selectedSupplier.confidencePct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
              </div>

              {selectedSupplier.prePostSignalNote ? (
                <div className="supplier-detail-note-box">
                  <strong>Napomena za pre/post signal:</strong> {selectedSupplier.prePostSignalNote}
                </div>
              ) : null}

              {(() => {
                const marginNote = buildMarginDetailNote(
                  selectedSupplier.marginQualityTier,
                  selectedSupplier.estimatedCostCoveragePct ?? selectedSupplier.fallbackCostCoveragePct,
                  selectedSupplier.historicalCostCoveragePct ?? selectedSupplier.marginDataCoveragePct,
                  fmtPct,
                  selectedSupplier.snapshotCostCoveragePct,
                  data.totals.isSnapshotActive
                );
                return marginNote ? (
                  <div className="supplier-detail-note-box supplier-detail-note-warning">
                    <strong>Napomena za maržu:</strong> {marginNote}
                  </div>
                ) : null;
              })()}

              {(() => {
                const recCaveat = buildRecommendationCaveat(
                  selectedSupplier.marginQualityTier,
                  selectedSupplier.estimatedCostCoveragePct ?? selectedSupplier.fallbackCostCoveragePct,
                  fmtPct
                );
                return recCaveat ? (
                  <div className="supplier-detail-note-box supplier-detail-note-caution">
                    <strong>Napomena za preporuku:</strong> {recCaveat}
                  </div>
                ) : null;
              })()}
            </section>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

