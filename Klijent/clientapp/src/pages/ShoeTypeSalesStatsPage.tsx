import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
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
  getShoeTypeSalesStats,
  type ShoeTypeSalesStat,
  type ShoeTypeSalesStatsResponse,
} from "../services/shoeTypeSalesStatsApi";
import type { StoreOption } from "../types/analytics";
import AnalyticsUnknownLink from "../components/analytics/AnalyticsUnknownLink";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import InfoTip from "../components/ui/InfoTip";
import UltraSpinner from "../components/ui/UltraSpinner";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { getDataScope } from "../utils/dataScope";
import { CHART_TOOLTIP_STYLE, CHART_TOOLTIP_LABEL_STYLE } from "../utils/chartTooltipStyle";
import { fmtPct, fmtQty, fmtRsd, fmtSignedPct, getPresetRange } from "../utils/analyticsFormatters";
import {
  analyticsMetricDescriptions,
  buildPopMetricDescription,
  buildPrePostNivelacijaImpactDescription,
} from "../utils/analyticsMetricDescriptions";
import {
  RECOMMENDATION_CONFIDENCE_LABEL,
  RECOMMENDATION_RELIABILITY_LABEL,
  RECOMMENDATION_STATUS_PRIORITY,
  isCanonicalRecommendationStatus,
  recommendationStatusLabel,
  recommendationStatusTone,
  recommendationStatusTooltipBrief,
  type CanonicalRecommendationStatus,
} from "../utils/canonicalRecommendationSemantics";
import { qualityTierIcon, qualityTierClass, tierNeedsWarning, buildCoverageTooltip, buildRecommendationCaveat, buildMarginDetailNote, buildSnapshotBadgeLabel, buildSnapshotTooltip } from "../utils/marginQuality";
import "./ShoeTypeSalesStatsPage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
type SortDir = "asc" | "desc";
type SortField =
  | "tipObuceNaziv"
  | "ukupanPromet"
  | "ukupnaKolicina"
  | "totalCost"
  | "sharePct"
  | "marginContribution"
  | "marginPct"
  | "popRevenueChangePct"
  | "prePostNivelacijaRevenueImpactPct"
  | "status";
type DecisionStatus = CanonicalRecommendationStatus;

type ActiveFilters = {
  fromDate: string;
  toDate: string;
  sezonaId: number | null;
  storeId: number | null;
};

type DecisionShoeType = ShoeTypeSalesStat & {
  sharePct: number;
  totalCost: number;
  marginContribution: number;
  reliabilityPct: number;
  coveragePct: number;
  splitCoveragePct: number;
  recommendationConfidencePct: number;
  status: DecisionStatus;
  statusReason: string;
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  ...RECOMMENDATION_STATUS_PRIORITY,
};

const decisionColumns: AnalyticsTableColumn<DecisionShoeType>[] = [
  { key: "tipObuceNaziv", header: "Tip obuÄ‡e", dataType: "text" },
  { key: "ukupanPromet", header: "Promet", dataType: "currency" },
  { key: "ukupnaKolicina", header: "KoliÄina", dataType: "number" },
  { key: "totalCost", header: "Nabavna vrednost", dataType: "currency" },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  { key: "marginContribution", header: "MarÅ¾ni doprinos", dataType: "currency" },
  { key: "marginPct", header: "MarÅ¾a %", dataType: "percent" },
  { key: "marginQualityLabel", header: "Kvalitet marÅ¾e", dataType: "text" },
  { key: "popRevenueChangePct", header: "PoP trend %", dataType: "percent" },
  { key: "prePostNivelacijaRevenueImpactPct", header: "Nivelacija impact %", dataType: "percent" },
  { key: "status", header: "Preporuka", dataType: "text" },
  { key: "recommendationConfidencePct", header: RECOMMENDATION_CONFIDENCE_LABEL, dataType: "number" },
];

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

function formatDate(value: string | null | undefined): string {
  if (!value) return "-";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleDateString("sr-RS");
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

function normalizeName(value: string | null | undefined): string {
  return (value ?? "").trim().toUpperCase();
}

function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string {
  if (field !== activeField) return " â‡…";
  return dir === "asc" ? " â†‘" : " â†“";
}

function isSortActive(field: SortField, activeField: SortField): boolean {
  return field === activeField;
}

function statusClass(status: DecisionStatus): string {
  const tone = recommendationStatusTone(status);
  if (tone === "boost") return "shoetype-decision-status status-boost";
  if (tone === "keep") return "shoetype-decision-status status-keep";
  if (tone === "review") return "shoetype-decision-status status-review";
  if (tone === "reduce") return "shoetype-decision-status status-reduce";
  return "shoetype-decision-status status-na";
}

function displayStatusLabel(status: DecisionStatus): string {
  return recommendationStatusLabel(status);
}

function mapRecommendationStatus(status?: string | null): DecisionStatus | null {
  return isCanonicalRecommendationStatus(status) ? status : null;
}

function trendClass(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "trend-neutral";
  if (value > 0) return "trend-up";
  if (value < 0) return "trend-down";
  return "trend-neutral";
}

type StatusTooltipData = {
  status: DecisionStatus;
  statusReason: string;
  sharePct: number;
  marginPct: number;
  popRevenueChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  previousPeriodRevenue: number | null;
  splitCoveragePct: number | null;
  reliabilityPct: number;
};

function buildStatusTooltip(data: StatusTooltipData): string {
  const popText = data.popRevenueChangePct != null
    ? fmtSignedPct(data.popRevenueChangePct, 1)
    : data.previousPeriodRevenue != null && data.previousPeriodRevenue <= 0
      ? "Novo / bez prethodne baze"
      : "N/A";
  const impactText = data.prePostNivelacijaRevenueImpactPct != null
    ? fmtSignedPct(data.prePostNivelacijaRevenueImpactPct, 1)
    : "N/A";
  return `${recommendationStatusLabel(data.status)}: ${data.statusReason} | ${recommendationStatusTooltipBrief(data.status)} | Udeo ${fmtPct(data.sharePct, 1)} | Marža ${fmtPct(data.marginPct, 1)} | PoP ${popText} | Nivelacija impact ${impactText} | Split pokrice ${fmtPct(data.splitCoveragePct, 1)} | ${RECOMMENDATION_RELIABILITY_LABEL} ${fmtPct(data.reliabilityPct, 0)}`;
}

function describePopMetric(item: ShoeTypeSalesStat): { label: string; title: string; className: string } {
  if (item.popRevenueChangePct != null && !Number.isNaN(item.popRevenueChangePct)) {
    return {
      label: fmtSignedPct(item.popRevenueChangePct, 2),
      title: buildPopMetricDescription(item.previousPeriodRevenue),
      className: trendClass(item.popRevenueChangePct),
    };
  }

  if (item.previousPeriodRevenue != null && item.previousPeriodRevenue <= 0 && item.ukupanPromet > 0) {
    return {
      label: "Novo",
      title: "Tip obuÄ‡e nije imao promet u prethodnom uporedivom periodu, pa PoP procenat nije smislen.",
      className: "trend-neutral",
    };
  }

  return {
    label: "N/A",
    title: "PoP trend nije dostupan jer ne postoji validna prethodna baza za poreÄ‘enje.",
    className: "trend-neutral",
  };
}

function describeNivelacijaImpactMetric(item: ShoeTypeSalesStat): { label: string; title: string; className: string } {
  if (item.prePostNivelacijaRevenueImpactPct != null && !Number.isNaN(item.prePostNivelacijaRevenueImpactPct)) {
    return {
      label: fmtSignedPct(item.prePostNivelacijaRevenueImpactPct, 2),
      title: buildPrePostNivelacijaImpactDescription(
        item.prePostNivelacijaRevenueCoveragePct,
        item.prePostSignalNote ? `Napomena: ${item.prePostSignalNote}` : undefined
      ),
      className: trendClass(item.prePostNivelacijaRevenueImpactPct),
    };
  }

  if (item.prePostSignalNote) {
    return {
      label: "Low signal",
      title: item.prePostSignalNote,
      className: "trend-neutral",
    };
  }

  if ((item.prePostNivelacijaRevenueCoveragePct ?? 0) <= 0) {
    return {
      label: "N/A",
      title: "Nema dovoljno uporedivih artikala sa prodajom i pre i posle prve nivelacije za pre/post impact metriku.",
      className: "trend-neutral",
    };
  }

  if (item.preNivelacijePromet <= 0 && item.posleNivelacijePromet > 0) {
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

function buildStoreLabel(store: StoreOption): string {
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  return extras ? `${store.storeName} (${extras})` : store.storeName;
}

function shoeTypeKey(item: { tipObuceId: number | null; tipObuceNaziv: string }): string {
  if (item.tipObuceId != null) return `id:${item.tipObuceId}`;
  return `name:${normalizeName(item.tipObuceNaziv)}`;
}

export default function ShoeTypeSalesStatsPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const requestIdRef = useRef(0);
  const detailSectionRef = useRef<HTMLElement>(null);

  const initialRange = useMemo(() => getPresetRange("30d"), []);
  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>("30d");
  const [fromDate, setFromDate] = useState(initialRange.fromDate);
  const [toDate, setToDate] = useState(initialRange.toDate);
  const [sezonaId, setSezonaId] = useState<number | null>(null);
  const [storeId, setStoreId] = useState<number | null>(null);
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({
    fromDate: initialRange.fromDate,
    toDate: initialRange.toDate,
    sezonaId: null,
    storeId: null,
  });

  const [stores, setStores] = useState<StoreOption[]>([]);
  const [data, setData] = useState<ShoeTypeSalesStatsResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sortField, setSortField] = useState<SortField>("status");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedTypeKey, setExpandedTypeKey] = useState<string | null>(null);

  const invalidRange = useMemo(() => {
    if (!fromDate || !toDate) return false;
    return new Date(fromDate) > new Date(toDate);
  }, [fromDate, toDate]);

  useEffect(() => {
    const loadStores = async () => {
      try {
        setStores(await getStores(true));
      } catch {
        setStores([]);
      }
    };

    void loadStores();
  }, []);

  const load = useCallback(async (filters: ActiveFilters) => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const currentRange = toUtcRange(filters.fromDate, filters.toDate);
      const result = await getShoeTypeSalesStats({
        ...currentRange,
        sezonaId: filters.sezonaId,
        storeId: filters.storeId,
      });

      if (requestId !== requestIdRef.current) return;
      setData(result);
    } catch (reason) {
      if (requestId !== requestIdRef.current) return;
      setData(null);
      setError(reason instanceof Error ? reason.message : "GreÅ¡ka pri uÄitavanju podataka po tipu obuÄ‡e.");
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    void load(activeFilters);
  }, [activeFilters, load]);

  const decisionRows = useMemo<DecisionShoeType[]>(() => {
    const rows = data?.shoeTypes ?? [];
    if (rows.length === 0) return [];

    const totalRevenue = rows.reduce((sum, item) => sum + item.ukupanPromet, 0);
    return rows.map((item) => {
      const sharePct = totalRevenue > 0 ? (item.ukupanPromet / totalRevenue) * 100 : 0;
      const totalCost = item.totalCost ?? Math.max(0, item.revenueWithCost - item.marginContribution);
      const marginContribution = item.marginContribution;
      const splitCoveragePct = item.prePostNivelacijaRevenueCoveragePct ?? 0;
      const coveragePct = item.brojArtikalaUkupno > 0
        ? (item.brojArtikalaSaNivelacijom / item.brojArtikalaUkupno) * 100
        : 0;
      const backendStatus = mapRecommendationStatus(item.recommendation?.status) ?? "insufficient_data";
      const reliabilityPct = item.recommendation?.reliabilityPct ?? item.reliabilityPct ?? (item.marginDataCoveragePct ?? 0);
      const recommendationConfidencePct = Math.round(item.recommendation?.confidencePct ?? 0);
      const statusReason = item.recommendation?.summary
        ?? "Backend recommendation payload nedostaje; red ostaje informativan bez lokalnog izvodjenja preporuke.";

      return {
        ...item,
        sharePct: item.sharePct ?? sharePct,
        totalCost,
        marginContribution,
        reliabilityPct,
        coveragePct,
        splitCoveragePct,
        recommendationConfidencePct,
        status: backendStatus,
        statusReason,
      };
    });
  }, [data?.shoeTypes]);

  const sortedRows = useMemo(() => {
    const rows = [...decisionRows];
    return rows.sort((a, b) => {
      let compare = 0;

      if (sortField === "tipObuceNaziv") {
        compare = a.tipObuceNaziv.localeCompare(b.tipObuceNaziv, "sr");
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
      } else if (sortField === "popRevenueChangePct") {
        compare = (a.popRevenueChangePct ?? -9999) - (b.popRevenueChangePct ?? -9999);
      } else if (sortField === "prePostNivelacijaRevenueImpactPct") {
        compare = (a.prePostNivelacijaRevenueImpactPct ?? -9999) - (b.prePostNivelacijaRevenueImpactPct ?? -9999);
      } else if (sortField === "status") {
        compare = STATUS_PRIORITY[a.status] - STATUS_PRIORITY[b.status];
      }

      if (compare === 0) compare = a.recommendationConfidencePct - b.recommendationConfidencePct;
      if (compare === 0) compare = a.ukupanPromet - b.ukupanPromet;

      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const selectedRow = useMemo(
    () => sortedRows.find((row) => shoeTypeKey(row) === expandedTypeKey) ?? null,
    [expandedTypeKey, sortedRows]
  );

  useEffect(() => {
    if (!selectedRow && sortedRows.length > 0 && expandedTypeKey != null) {
      setExpandedTypeKey(null);
    }
  }, [expandedTypeKey, selectedRow, sortedRows.length]);

  useEffect(() => {
    if (!selectedRow || !detailSectionRef.current) return;
    const delay = 120;
    const timeoutId = window.setTimeout(() => {
      if (!detailSectionRef.current) return;
      smoothScrollToElement(detailSectionRef.current);
    }, delay);
    return () => window.clearTimeout(timeoutId);
  }, [selectedRow]);

  const totalRevenue = data?.totals.ukupanPromet ?? 0;
  const top5SharePct = useMemo(() => {
    if (sortedRows.length === 0 || totalRevenue <= 0) return 0;
    const top5Revenue = [...sortedRows]
      .sort((a, b) => b.ukupanPromet - a.ukupanPromet)
      .slice(0, 5)
      .reduce((sum, row) => sum + row.ukupanPromet, 0);
    return (top5Revenue / totalRevenue) * 100;
  }, [sortedRows, totalRevenue]);

  const totalMarginContribution = useMemo(
    () => data?.totals.ukupanMarzniDoprinos ?? 0,
    [data?.totals.ukupanMarzniDoprinos]
  );

  const periodGrowthPct = useMemo(() => data?.totals.popRevenueChangePct ?? null, [data?.totals.popRevenueChangePct]);

  const concentrationData = useMemo(() => {
    if (sortedRows.length === 0) return [] as Array<{ name: string; sharePct: number }>;

    const ranked = [...sortedRows].sort((a, b) => b.sharePct - a.sharePct);
    const topRows = ranked.slice(0, 6).map((row) => ({
      name: row.tipObuceNaziv,
      sharePct: Number(row.sharePct.toFixed(2)),
    }));

    const remaining = ranked.slice(6).reduce((sum, row) => sum + row.sharePct, 0);
    if (remaining > 0.1) {
      topRows.push({ name: "Ostali", sharePct: Number(remaining.toFixed(2)) });
    }

    return topRows;
  }, [sortedRows]);

  const comparisonData = useMemo(() => {
    if (sortedRows.length === 0 || totalMarginContribution <= 0)
      return [] as Array<{ name: string; udeoPrometa: number; udeoMarznogDoprinosa: number }>;

    const ranked = [...sortedRows].sort((a, b) => b.ukupanPromet - a.ukupanPromet);

    return ranked.slice(0, 8).map((row) => ({
      name: row.tipObuceNaziv,
      udeoPrometa: Number(row.sharePct.toFixed(1)),
      udeoMarznogDoprinosa: Number(
        (totalMarginContribution > 0
          ? (row.marginContribution / totalMarginContribution) * 100
          : 0
        ).toFixed(1)
      ),
    }));
  }, [sortedRows, totalMarginContribution]);

  const avgMarginPct = useMemo(() => {
    if (decisionRows.length === 0) return 0;
    const sum = decisionRows.reduce((acc, row) => acc + row.marginPct, 0);
    return sum / decisionRows.length;
  }, [decisionRows]);

  const counts = useMemo(() => {
    const increaseFocus = sortedRows.filter((row) => row.status === "increase_focus").length;
    const maintain = sortedRows.filter((row) => row.status === "maintain").length;
    const review = sortedRows.filter((row) => row.status === "review").length;
    const doNotTrust = sortedRows.filter((row) => row.status === "do_not_trust").length;
    const insufficientData = sortedRows.filter((row) => row.status === "insufficient_data").length;
    return { increaseFocus, maintain, review, doNotTrust, insufficientData };
  }, [sortedRows]);

  const activeSezonaLabel = useMemo(() => {
    if (activeFilters.sezonaId == null) return "Sve sezone";
    return data?.sezone.find((item) => item.id === activeFilters.sezonaId)?.naziv ?? String(activeFilters.sezonaId);
  }, [activeFilters.sezonaId, data?.sezone]);

  const emptyStateHint = useMemo(() => {
    if (!data || sortedRows.length > 0) return null;
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
  }, [activeFilters.fromDate, activeFilters.toDate, data, sortedRows.length]);

  const qualityNotes = useMemo(() => {
    if (!data) return [] as string[];

    const notes: string[] = [];
    const splitCoverage = data.dataQuality.revenueWithNivelacijaSplitSharePct;
    const missingCostShare = data.dataQuality.missingCostRevenueSharePct;
    const historicalCostShare = missingCostShare == null ? null : Math.max(0, 100 - missingCostShare);
    const estimatedCostShare = data.dataQuality.estimatedCostRevenueSharePct;
    const unknownShare = data.dataQuality.unknownTypeRevenueSharePct;

    if (splitCoverage != null && splitCoverage < 60) {
      notes.push(`Uporediv pre/posle signal trenutno pokriva ${fmtPct(splitCoverage, 1)} ukupnog prometa, pa ga treba Äitati kao delimiÄan.`);
    }

    if (historicalCostShare != null && historicalCostShare < 100) {
      notes.push(`Istorijska nabavna cena postoji za ${fmtPct(historicalCostShare, 1)} prometa; marÅ¾a za ostatak nije istorijski potvrdena na prodajnoj stavci.`);
    }

    if (estimatedCostShare != null && estimatedCostShare > 0) {
notes.push(`Za ${fmtPct(estimatedCostShare, 1)} prometa nabavna cena je procenjena (bez direktnog troÅ¡ka) â€” marzu citati oprezno.`);
    }

    if (unknownShare != null && unknownShare > 0) {
      notes.push(`Nepoznati tipovi obuÄ‡e uÄestvuju sa ${fmtPct(unknownShare, 1)} ukupnog prometa.`);
    }

    const snapshotPct = data.totals.snapshotCostCoveragePct;
    if (data.totals.isSnapshotActive && snapshotPct != null && snapshotPct > 0) {
      notes.push(`Za ${fmtPct(snapshotPct, 1)} prometa trosak je stabilizovan zamrznutom procenom (snapshot). Ovo je reproduktivna procena, ne istorijska nabavna cena.`);
    }

    return notes;
  }, [data]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "fromDate", label: "Od", value: activeFilters.fromDate },
      { key: "toDate", label: "Do", value: activeFilters.toDate },
      { key: "sezonaId", label: "Sezona", value: activeSezonaLabel },
      { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "Svi objekti" },
    ],
    [activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, activeSezonaLabel]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
      { key: "tipova", label: "Tipova", value: data?.totals.brojTipovaObuce ?? 0 },
      { key: "marginCoverage", label: "Pokrice direktnom nabavnom %", value: fmtPct(data?.dataQuality.missingCostRevenueSharePct == null ? null : 100 - data.dataQuality.missingCostRevenueSharePct, 1) },
      { key: "fallbackCoverage", label: "Promet sa procenjenom nabavnom %", value: fmtPct(data?.dataQuality.estimatedCostRevenueSharePct, 1) },
      { key: "noCostCoverage", label: "Promet bez nabavne cene %", value: fmtPct(data?.dataQuality.missingCostRevenueSharePct, 1) },
      { key: "splitCoverage", label: "Uporediv pre/post pokrice", value: fmtPct(data?.dataQuality.revenueWithNivelacijaSplitSharePct, 1) },
      { key: "snapshotCoverage", label: "Zamrznuta procena (snapshot) %", value: fmtPct(data?.totals.snapshotCostCoveragePct, 1) },
      { key: "isSnapshotActive", label: "Snapshot aktivan", value: data?.totals.isSnapshotActive ? "da" : "ne" },
      { key: "increaseFocus", label: recommendationStatusLabel("increase_focus"), value: counts.increaseFocus },
      { key: "maintain", label: recommendationStatusLabel("maintain"), value: counts.maintain },
      { key: "review", label: recommendationStatusLabel("review"), value: counts.review },
      { key: "doNotTrust", label: recommendationStatusLabel("do_not_trust"), value: counts.doNotTrust },
      { key: "insufficientData", label: recommendationStatusLabel("insufficient_data"), value: counts.insufficientData },
    ],
    [
      counts.doNotTrust,
      counts.increaseFocus,
      counts.insufficientData,
      counts.maintain,
      counts.review,
      data?.dataQuality.estimatedCostRevenueSharePct,
      data?.dataQuality.missingCostRevenueSharePct,
      data?.dataQuality.revenueWithNivelacijaSplitSharePct,
      data?.generatedAt,
      data?.totals.brojTipovaObuce,
      data?.totals.snapshotCostCoveragePct,
      data?.totals.isSnapshotActive,
    ]
  );

  const openDetail = useCallback((row: DecisionShoeType) => {
    const recordId = row.tipObuceId != null
      ? String(row.tipObuceId)
      : `unknown-${encodeURIComponent(row.tipObuceNaziv)}`;

    const params = new URLSearchParams();
    params.set("fromDate", `${activeFilters.fromDate}T00:00:00Z`);
    params.set("toDate", `${activeFilters.toDate}T23:59:59Z`);
    if (activeFilters.sezonaId != null) params.set("sezonaId", String(activeFilters.sezonaId));
    if (activeFilters.storeId != null) params.set("storeId", String(activeFilters.storeId));
    params.set("dataScope", getDataScope());

    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "shoe-type-sales-stats",
        recordId,
        title: row.tipObuceNaziv,
        subtitle: "Detaljni pregled odluke po tipu obuÄ‡e",
        columns: decisionColumns,
        row,
        metadata: toolbarFilters,
      })
    );

    navigate(`/analitika/shoe-type-sales-stats/${recordId}?${params.toString()}`, {
      state: { backgroundLocation: location },
    });
  }, [activeFilters.fromDate, activeFilters.sezonaId, activeFilters.storeId, activeFilters.toDate, location, navigate, toolbarFilters]);

  const applyPreset = (preset: PeriodPreset) => {
    setPeriodPreset(preset);
    if (preset === "custom") return;
    const range = getPresetRange(preset);
    setSezonaId(null);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
  };

  const handleSeasonChange = (value: string) => {
    const parsed = value ? Number(value) : null;
    setSezonaId(parsed);
    setPeriodPreset("custom");

    if (parsed == null) return;

    const selected = data?.sezone.find((item) => item.id === parsed);
    if (!selected) return;
    setFromDate(toDateOnly(selected.datumOd));
    setToDate(toDateOnly(selected.datumDo));
  };

  const applyFilters = () => {
    if (invalidRange) {
      setError("Datum od ne moze biti posle datuma do.");
      return;
    }

    setActiveFilters({
      fromDate,
      toDate,
      sezonaId,
      storeId,
    });
  };

  const resetFilters = () => {
    const range = getPresetRange("30d");
    setPeriodPreset("30d");
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    setSezonaId(null);
    setStoreId(null);
    setActiveFilters({
      fromDate: range.fromDate,
      toDate: range.toDate,
      sezonaId: null,
      storeId: null,
    });
  };

  const handleSort = (field: SortField) => {
    setSortField((previousField) => {
      if (previousField === field) {
        setSortDir((previousDir) => (previousDir === "asc" ? "desc" : "asc"));
        return previousField;
      }

      setSortDir(field === "tipObuceNaziv" ? "asc" : "desc");
      return field;
    });
  };

  return (
    <div className="shoetype-decision-page">
      <header className="shoetype-decision-header">
        <div>
          <h1 className="shoetype-decision-title">Prodaja po tipu obuÄ‡e</h1>
          <p className="shoetype-decision-subtitle">
            PodrÅ¡ka odluci sa asortimanski fokus po tipu obuÄ‡e.
          </p>
        </div>
        {data?.generatedAt ? (
          <div className="shoetype-decision-generated">
            Generisano: {new Date(data.generatedAt).toLocaleString("sr-RS")}
          </div>
        ) : null}
      </header>

      <section className="shoetype-decision-filters">
        <label className="shoetype-decision-field">
          <span>Period</span>
          <select value={periodPreset} onChange={(event) => applyPreset(event.target.value as PeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="180d">Poslednjih 180 dana</option>
            <option value="365d">Poslednjih 365 dana</option>
            <option value="custom">PrilagoÄ‘eno</option>
          </select>
        </label>

        <label className="shoetype-decision-field">
          <span>Od</span>
          <input
            type="date"
            value={fromDate}
            onChange={(event) => {
              setPeriodPreset("custom");
              setSezonaId(null);
              setFromDate(event.target.value);
            }}
          />
        </label>

        <label className="shoetype-decision-field">
          <span>Do</span>
          <input
            type="date"
            value={toDate}
            onChange={(event) => {
              setPeriodPreset("custom");
              setSezonaId(null);
              setToDate(event.target.value);
            }}
          />
        </label>

        <label className="shoetype-decision-field">
          <span>Sezona</span>
          <select value={sezonaId ?? ""} onChange={(event) => handleSeasonChange(event.target.value)}>
            <option value="">Sve sezone</option>
            {(data?.sezone ?? []).map((sezona) => (
              <option key={sezona.id} value={sezona.id}>
                {sezona.naziv}
              </option>
            ))}
          </select>
        </label>

        <label className="shoetype-decision-field">
          <span>Objekat</span>
          <select
            value={storeId ?? ""}
            onChange={(event) => setStoreId(event.target.value ? Number(event.target.value) : null)}
          >
            <option value="">Svi objekti</option>
            {stores.map((store) => (
              <option key={store.storeId} value={store.storeId}>
                {buildStoreLabel(store)}
              </option>
            ))}
          </select>
        </label>

        <div className="shoetype-decision-actions">
          <button type="button" onClick={applyFilters} disabled={loading}>Primeni</button>
          <button type="button" className="secondary" onClick={resetFilters} disabled={loading}>Reset</button>
        </div>
      </section>

      {invalidRange ? (
        <div className="shoetype-decision-message error">Datum od ne moze biti posle datuma do.</div>
      ) : null}
      {error ? <div className="shoetype-decision-message error">{error}</div> : null}
      {loading && !data ? (
        <div className="shoetype-decision-loading" role="status" aria-live="polite">
          <UltraSpinner size="md" label="UÄitavam tipove obuÄ‡e" />
          <span>UÄitavam tipove obuÄ‡e...</span>
        </div>
      ) : null}
      {!loading && !error && emptyStateHint ? (
        <div className="shoetype-decision-message info">{emptyStateHint}</div>
      ) : null}


      {data ? (
        <div
          className={`shoetype-decision-content${loading ? " shoetype-decision-content--refetching" : ""}`}
          aria-busy={loading || undefined}
        >
          {loading ? (
            <div className="shoetype-decision-refetch-overlay" aria-hidden="true">
              <UltraSpinner size="sm" label="OsveÅ¾avam podatke" />
            </div>
          ) : null}
          <section className="shoetype-decision-kpis">
            <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="Promet svih tipova obuÄ‡e u izabranom periodu.">
              <span>Ukupan promet <InfoTip text="Zbir prodajnih vrednosti svih tipova obuÄ‡e u izabranom periodu. Formula: Î£ prodajna vrednost stavki po tipu u periodu (RSD)." /></span>
              <strong>{fmtRsd(totalRevenue)}</strong>
            </article>
            <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Ukupan broj prodatih komada kroz sve tipove.">
              <span>Ukupno prodato <InfoTip text="Ukupan broj prodatih komada svih tipova obuÄ‡e u izabranom periodu." /></span>
              <strong>{fmtQty(data.totals.ukupnaKolicina)}</strong>
            </article>
            <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-neutral" data-note="TroÅ¡ak robe sa dostupnim ili procenjenim ulazom.">
              <span>Ukupna nabavna vrednost <InfoTip text="Zbir troÅ¡ka robe za deo prometa sa dostupnim troÅ¡kom. Formula: zbir koliÄina x nabavna cena za stavke sa istorijskim ili procenjenim troÅ¡kom. Operativni troÅ¡kovi nisu ukljuÄeni." /></span>
              <strong>{fmtRsd(data.totals.ukupanTrosak ?? 0)}</strong>
            </article>
            <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-value" data-note="Bruto marzni doprinos po tipovima obuÄ‡e.">
              <span>Ukupan marÅ¾ni doprinos <InfoTip text="Zbir razlike izmeÄ‘u prodajne i nabavne vrednosti za sve stavke sa dostupnim troÅ¡kom, grupisano po tipu obuÄ‡e. Operativni troÅ¡kovi, plate, zakup i ostali indirektni troÅ¡kovi nisu ukljuÄeni." /></span>
              <strong>{fmtRsd(totalMarginContribution)}</strong>
              <small
                className={`shoetype-decision-kpi-badge ${qualityTierClass(data.totals.marginQualityTier)}`}
                title={data.totals.marginQualityTooltip ?? buildCoverageTooltip(data.totals.historicalCostCoveragePct, data.totals.estimatedCostCoveragePct, data.totals.noCostCoveragePct, fmtPct, data.totals.snapshotCostCoveragePct)}
              >
                {qualityTierIcon(data.totals.marginQualityTier)} {data.totals.marginQualityShortLabel ?? data.totals.marginQualityLabel}
              </small>
              {data.totals.isSnapshotActive && (data.totals.snapshotCostCoveragePct ?? 0) > 0 ? (
                <small
                  className="shoetype-decision-kpi-badge quality-snapshot"
                  title={buildSnapshotTooltip(data.totals.snapshotCostCoveragePct ?? 0, data.totals.snapshotGeneratedAtUtc, fmtPct)}
                >
                  â„ {buildSnapshotBadgeLabel(data.totals.snapshotGeneratedAtUtc)}
                </small>
              ) : null}
            </article>
            <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="Aritmeticka sredina marze kroz tipove obuÄ‡e.">
              <span>ProseÄna marÅ¾a <InfoTip text="ProseÄan procenat marÅ¾nog doprinosa po tipu obuÄ‡e. Formula po tipu: marÅ¾ni doprinos / promet sa dostupnim troÅ¡kom x 100. Prikazani prosek je aritmetiÄki prosek meÄ‘u tipovima, nije ponderisan prometom." /></span>
              <strong>{fmtPct(avgMarginPct, 1)}</strong>
            </article>
            <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Koliko je promet koncentrisan na top 5 tipova.">
              <span>Udeo top 5 tipova <InfoTip text="Procenat ukupnog prometa koji dolazi od pet tipova obuÄ‡e sa najveÄ‡im prometom. Formula: promet top 5 / ukupan promet x 100." /></span>
              <strong>{fmtPct(top5SharePct)}</strong>
            </article>
            <article className="shoetype-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Promena prometa prema prethodnom uporedivom periodu.">
              <span>PoP trend prometa <InfoTip text="Promena ukupnog prometa u odnosu na prethodni uporedivi period iste duÅ¾ine. Formula: (trenutni promet âˆ’ prethodni promet) / prethodni promet Ã— 100. N/A ako prethodni period nije dostupan." /></span>
              <strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong>
            </article>
          </section>

          {qualityNotes.length > 0 ? (
            <div className="shoetype-decision-message info">
              <strong>Kvalitet podataka:</strong> {qualityNotes.join(" ")}
            </div>
          ) : null}

          <section className="shoetype-decision-panels">
            <article className="shoetype-decision-card shoetype-decision-card--chart analytics-surface-panel">
              <h2>Koncentracija prometa po tipu obuÄ‡e <InfoTip text="Grafikon prikazuje koliki udeo ukupnog prometa nose tipovi obuÄ‡e. Koristi samo promet, bez tumaÄenja profita ili neto marÅ¾e." /></h2>
              <p>Brz pregled koji tipovi nose najveÄ‡i deo prihoda.</p>
              {concentrationData.length > 0 ? (
                <div className="shoetype-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={concentrationData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <defs>
                        <linearGradient id="shoeShareGradient" x1="0" y1="0" x2="1" y2="0">
                          <stop offset="0%" stopColor="var(--dashboard-gradient-share-start, var(--dashboard-accent, #33f28b))" />
                          <stop offset="100%" stopColor="var(--dashboard-gradient-share-end, var(--dashboard-secondary, #1ec8ff))" />
                        </linearGradient>
                      </defs>
                      <CartesianGrid strokeDasharray="2 6" stroke="var(--dashboard-grid, rgba(102, 255, 126, 0.16))" />
                      <XAxis type="number" tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} />
                      <Tooltip contentStyle={COMMAND_TOOLTIP_STYLE} labelStyle={COMMAND_TOOLTIP_LABEL_STYLE} cursor={CHART_CURSOR_STYLE} formatter={(value: number | string | undefined) => `${fmtPct(Number(value ?? 0), 2)}`} />
                      <Legend wrapperStyle={CHART_LEGEND_STYLE} iconType="circle" iconSize={8} />
                      <Bar dataKey="sharePct" fill="url(#shoeShareGradient)" radius={[0, 10, 10, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="shoetype-decision-empty">Nema podataka za grafikon koncentracije.</div>
              )}
            </article>

            <article className="shoetype-decision-card shoetype-decision-card--chart analytics-surface-panel">
              <h2>Promet vs MarÅ¾ni doprinos <InfoTip text="Grafikon poredi udeo u prometu i udeo u marÅ¾nom doprinosu po tipu obuÄ‡e. MarÅ¾ni doprinos nije neto profit i ne ukljuÄuje operativne troÅ¡kove. Ako je deo troÅ¡ka procenjen iz raspoloÅ¾ivih podataka, i ovaj signal treba Äitati oprezno." /></h2>
              <p>PoreÄ‘enje udela u prometu i udela u marznom doprinosu - tipovi obuÄ‡e s visokim prometom ne moraju imati i visok marzni doprinos.</p>
              {comparisonData.length > 0 ? (
                <div className="shoetype-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={comparisonData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <defs>
                        <linearGradient id="shoeRevenueGradient" x1="0" y1="0" x2="1" y2="0">
                          <stop offset="0%" stopColor="var(--dashboard-gradient-revenue-start, var(--dashboard-accent-strong, #8bff00))" />
                          <stop offset="100%" stopColor="var(--dashboard-gradient-revenue-end, var(--dashboard-accent, #33f28b))" />
                        </linearGradient>
                        <linearGradient id="shoeMarginGradient" x1="0" y1="0" x2="1" y2="0">
                          <stop offset="0%" stopColor="var(--dashboard-gradient-margin-start, var(--dashboard-secondary, #1ec8ff))" />
                          <stop offset="100%" stopColor="var(--dashboard-gradient-margin-end, var(--dashboard-accent, #11f59e))" />
                        </linearGradient>
                      </defs>
                      <CartesianGrid strokeDasharray="2 6" stroke="var(--dashboard-grid, rgba(102, 255, 126, 0.16))" />
                      <XAxis type="number" tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} />
                      <Tooltip
                        contentStyle={COMMAND_TOOLTIP_STYLE}
                        labelStyle={COMMAND_TOOLTIP_LABEL_STYLE}
                        cursor={CHART_CURSOR_STYLE}
                        formatter={((value: any) => `${fmtPct(Number(value ?? 0), 1)}`) as any}
                      />
                      <Legend wrapperStyle={CHART_LEGEND_STYLE} iconType="circle" iconSize={8} />
                      <Bar dataKey="udeoPrometa" fill="url(#shoeRevenueGradient)" radius={[0, 6, 6, 0]} name="Udeo u prometu %" />
                      <Bar dataKey="udeoMarznogDoprinosa" fill="url(#shoeMarginGradient)" radius={[0, 6, 6, 0]} name="Udeo u marÅ¾nom doprinosu %" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="shoetype-decision-empty">Nema podataka za poreÄ‘enje.</div>
              )}
            </article>
          </section>

          <section className="shoetype-decision-panels">
            <article className="shoetype-decision-card analytics-surface-panel">
              <div className="shoetype-decision-table-head">
                <div>
                  <h2>Prioritetna lista tipova obuÄ‡e</h2>
                  <div className="shoetype-priority-chip-row" aria-label="Raspodela preporuka">
                    <span className="priority-chip priority-chip-boost">{recommendationStatusLabel("increase_focus")} <strong>{counts.increaseFocus}</strong></span>
                    <span className="priority-chip priority-chip-keep">{recommendationStatusLabel("maintain")} <strong>{counts.maintain}</strong></span>
                    <span className="priority-chip priority-chip-review">{recommendationStatusLabel("review")} <strong>{counts.review}</strong></span>
                    <span className="priority-chip priority-chip-reduce">{recommendationStatusLabel("do_not_trust")} <strong>{counts.doNotTrust}</strong></span>
                    <span className="priority-chip priority-chip-na">{recommendationStatusLabel("insufficient_data")} <strong>{counts.insufficientData}</strong></span>
                  </div>
                  <p className="shoetype-decision-metric-note">
                    PoP trend = promena prometa prema prethodnom uporedivom periodu. Nivelacija impact = pre/post promena unutar prometa sa poznatim prvim datumom nivelacije.
                  </p>
                </div>
                <AnalyticsTableToolbar
                  tableKey="shoe-type-sales-stats"
                  tableTitle="PodrÅ¡ka odluci - tipovi obuÄ‡e"
                  columns={decisionColumns}
                  rows={sortedRows}
                  filters={toolbarFilters}
                  metadata={toolbarMetadata}
                  defaultOrientation="landscape"
                />
              </div>

              <div className="shoetype-decision-table-wrap">
                <table className="shoetype-decision-table">
                  <thead>
                    <tr>
                      <th className={isSortActive("tipObuceNaziv", sortField) ? "is-sorted" : undefined}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("tipObuceNaziv", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("tipObuceNaziv", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("tipObuceNaziv", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("tipObuceNaziv")}
                        >
                          Tip obuÄ‡e <span className="sort-indicator" aria-hidden="true">{sortMarker("tipObuceNaziv", sortField, sortDir)}</span> <InfoTip text="Naziv tipa obuÄ‡e (npr. patike, sandale)." />
                        </button>
                      </th>
                      <th className={isSortActive("ukupanPromet", sortField) ? "align-right is-sorted" : "align-right"}>
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
                      <th className={isSortActive("ukupnaKolicina", sortField) ? "align-right is-sorted" : "align-right"}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("ukupnaKolicina", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("ukupnaKolicina", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("ukupnaKolicina", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("ukupnaKolicina")}
                        >
                          KoliÄina <span className="sort-indicator" aria-hidden="true">{sortMarker("ukupnaKolicina", sortField, sortDir)}</span> <InfoTip text="Ukupan broj prodatih komada u izabranom periodu." />
                        </button>
                      </th>
                      <th className={isSortActive("totalCost", sortField) ? "align-right is-sorted" : "align-right"}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("totalCost", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("totalCost", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("totalCost", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("totalCost")}
                        >
                          Nabavna vrednost <span className="sort-indicator" aria-hidden="true">{sortMarker("totalCost", sortField, sortDir)}</span> <InfoTip text="Zbir troÅ¡ka robe za ovaj red. Formula: zbir koliÄina x nabavna cena za stavke sa istorijskim ili procenjenim troÅ¡kom. Operativni troÅ¡kovi nisu ukljuÄeni." />
                        </button>
                      </th>
                      <th className={isSortActive("sharePct", sortField) ? "align-right is-sorted" : "align-right"}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("sharePct", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("sharePct", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("sharePct", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("sharePct")}
                        >
                          Udeo u prometu <span className="sort-indicator" aria-hidden="true">{sortMarker("sharePct", sortField, sortDir)}</span> <InfoTip text="Udeo ovog tipa obuÄ‡e u ukupnom prometu svih prikazanih tipova. Formula: promet tipa / ukupan promet x 100." />
                        </button>
                      </th>
                      <th className={isSortActive("marginContribution", sortField) ? "align-right is-sorted" : "align-right"}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("marginContribution", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("marginContribution", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("marginContribution", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("marginContribution")}
                        >
                          MarÅ¾ni doprinos <span className="sort-indicator" aria-hidden="true">{sortMarker("marginContribution", sortField, sortDir)}</span> <InfoTip text="Zbir razlike izmeÄ‘u prodajne i nabavne vrednosti za stavke ovog tipa sa dostupnim troÅ¡kom. Operativni troÅ¡kovi, plate, zakup i ostali indirektni troÅ¡kovi nisu ukljuÄeni." />
                        </button>
                      </th>
                      <th className={isSortActive("marginPct", sortField) ? "align-right is-sorted" : "align-right"}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("marginPct", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("marginPct", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("marginPct", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("marginPct")}
                        >
                          MarÅ¾a % <span className="sort-indicator" aria-hidden="true">{sortMarker("marginPct", sortField, sortDir)}</span> <InfoTip text={analyticsMetricDescriptions.marginPct} />
                        </button>
                      </th>
                      <th className={isSortActive("popRevenueChangePct", sortField) ? "align-right is-sorted" : "align-right"}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("popRevenueChangePct", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("popRevenueChangePct", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("popRevenueChangePct", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("popRevenueChangePct")}
                        >
                          PoP trend <span className="sort-indicator" aria-hidden="true">{sortMarker("popRevenueChangePct", sortField, sortDir)}</span> <InfoTip text={analyticsMetricDescriptions.popRevenueChangePct} />
                        </button>
                      </th>
                      <th className={isSortActive("prePostNivelacijaRevenueImpactPct", sortField) ? "align-right is-sorted" : "align-right"}>
                        <button
                          type="button"
                          className={`sortable-header ${isSortActive("prePostNivelacijaRevenueImpactPct", sortField) ? "is-active" : ""}`}
                          data-sort-active={isSortActive("prePostNivelacijaRevenueImpactPct", sortField) ? "true" : "false"}
                          data-sort-dir={isSortActive("prePostNivelacijaRevenueImpactPct", sortField) ? sortDir : "none"}
                          onClick={() => handleSort("prePostNivelacijaRevenueImpactPct")}
                        >
                          Nivelacija impact <span className="sort-indicator" aria-hidden="true">{sortMarker("prePostNivelacijaRevenueImpactPct", sortField, sortDir)}</span> <InfoTip text={analyticsMetricDescriptions.prePostNivelacijaImpactPct} />
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
                      <th className="align-center">Detalj <InfoTip text="ProÅ¡iri inline detalj ili otvori puni detalj za ovaj tip obuÄ‡e." /></th>
                    </tr>
                  </thead>
                  <tbody>
                    {sortedRows.length === 0 ? (
                      <tr>
                        <td colSpan={11} className="shoetype-decision-empty-row">
                          Nema podataka za izabrane filtere.
                        </td>
                      </tr>
                    ) : (
                      sortedRows.map((row, index) => {
                        const rowKey = shoeTypeKey(row);
                        const rank = index + 1;
                        const expanded = expandedTypeKey === rowKey;
                        const popMetric = describePopMetric(row);
                        const nivelacijaImpactMetric = describeNivelacijaImpactMetric(row);
                        return (
                          <tr key={rowKey} className={[expanded ? "expanded-row" : "", rank <= 3 ? `shoetype-rank-row shoetype-rank-row-${rank}` : ""].filter(Boolean).join(" ")}>
                            <td>
                              <div className="shoetype-name-cell">
                                <span className={`shoetype-rank-badge ${rank <= 3 ? `rank-${rank}` : "rank-other"}`}>#{rank}</span>
                                <AnalyticsUnknownLink
                                  value={row.tipObuceNaziv}
                                  issueType="missingShoeType"
                                  context={{
                                    originTable: "shoe-type-sales-stats",
                                    fromDate: activeFilters.fromDate,
                                    toDate: activeFilters.toDate,
                                    sezonaId: activeFilters.sezonaId,
                                    storeId: activeFilters.storeId,
                                    dataScope: getDataScope(),
                                  }}
                                />
                              </div>
                            </td>
                            <td className="align-right metric-strong">{fmtRsd(row.ukupanPromet)}</td>
                            <td className="align-right">{fmtQty(row.ukupnaKolicina)}</td>
                            <td className="align-right">{fmtRsd(row.totalCost)}</td>
                            <td className="align-right"><span className="metric-chip metric-chip-neutral">{fmtPct(row.sharePct, 2)}</span></td>
                            <td className="align-right metric-strong">{fmtRsd(row.marginContribution)}</td>
                            <td className="align-right">
                              <span>{fmtPct(row.marginPct, 1)}</span>
                              {tierNeedsWarning(row.marginQualityTier) ? (
                                <span className={`quality-pill ${qualityTierClass(row.marginQualityTier)}`} title={row.marginQualityTooltip ?? row.marginQualityLabel ?? ""}>
                                  marÅ¾a
                                </span>
                              ) : null}
                            </td>
                            <td className="align-right" title={popMetric.title}><span className={`metric-chip trend-pill ${popMetric.className}`}>{popMetric.label}</span></td>
                            <td className="align-right" title={nivelacijaImpactMetric.title}><span className={`metric-chip trend-pill ${nivelacijaImpactMetric.className}`}>{nivelacijaImpactMetric.label}</span></td>
                            <td>
                              <div className="shoetype-status-stack">
                                <span
                                  className={statusClass(row.status)}
                                  title={buildStatusTooltip(row)}
                                  aria-label={buildStatusTooltip(row)}
                                >
                                  {displayStatusLabel(row.status)}
                                </span>
                                <span className="shoetype-status-reason-chip" title={row.statusReason}>
                                  Razlog <InfoTip text={row.statusReason} />
                                </span>
                              </div>
                            </td>
                            <td className="align-center">
                              <button
                                type="button"
                                className="shoetype-decision-detail-btn"
                                onClick={() => setExpandedTypeKey(expanded ? null : rowKey)}
                              >
                                {expanded ? "Sakrij" : "Detalji"}
                              </button>
                            </td>
                          </tr>
                        );
                      })
                    )}
                  </tbody>
                </table>
              </div>
            </article>
          </section>

          {selectedRow ? (
            <section className="shoetype-decision-detail" ref={detailSectionRef}>
              <div className="shoetype-decision-detail-head">
                <h3>Detalj odluke: {selectedRow.tipObuceNaziv}</h3>
                <button type="button" onClick={() => openDetail(selectedRow)}>Otvori puni detalj</button>
              </div>

              <h4 className="shoetype-decision-detail-section-title">Poslovni pokazatelji</h4>
              <div className="shoetype-decision-detail-grid">
                <article>
                  <span>Promet <InfoTip text="Ukupna vrednost prodaje ovog tipa obuÄ‡e u izabranom periodu. Formula: zbir prodajnih vrednosti stavki ovog tipa." /></span>
                  <strong>{fmtRsd(selectedRow.ukupanPromet)}</strong>
                </article>
                <article>
                  <span>KoliÄina <InfoTip text="Ukupan broj prodatih komada ovog tipa obuÄ‡e." /></span>
                  <strong>{fmtQty(selectedRow.ukupnaKolicina)}</strong>
                </article>
                <article>
                  <span>Nabavna vrednost <InfoTip text="Zbir troÅ¡ka robe za ovaj red. Formula: zbir koliÄina x nabavna cena za stavke sa istorijskim ili procenjenim troÅ¡kom. Operativni troÅ¡kovi nisu ukljuÄeni." /></span>
                  <strong>{fmtRsd(selectedRow.totalCost)}</strong>
                </article>
                <article>
                  <span>MarÅ¾ni doprinos <InfoTip text="Zbir razlike izmeÄ‘u prodajne i nabavne vrednosti za stavke sa dostupnim troÅ¡kom. Operativni troÅ¡kovi, plate, zakup i ostali indirektni troÅ¡kovi nisu ukljuÄeni." /></span>
                  <strong>{fmtRsd(selectedRow.marginContribution)}</strong>
                </article>
                <article>
                  <span>MarÅ¾a % <InfoTip text={analyticsMetricDescriptions.marginPct} /></span>
                  <strong>{fmtSignedPct(selectedRow.marginPct, 2)}</strong>
                </article>
                <article>
                  <span>Udeo u prometu <InfoTip text="Procenat koji ovaj tip obuÄ‡e Äini u ukupnom prometu. Formula: promet tipa / ukupan promet svih prikazanih tipova x 100." /></span>
                  <strong>{fmtPct(selectedRow.sharePct, 2)}</strong>
                </article>
                <article>
                  <span>Udeo u marÅ¾nom doprinosu <InfoTip text="Procenat koji ovaj tip obuÄ‡e Äini u ukupnom marÅ¾nom doprinosu. Formula: marÅ¾ni doprinos tipa / ukupan marÅ¾ni doprinos svih tipova x 100. Ovo nije udeo u profitu niti u neto zaradi." /></span>
                  <strong>{fmtPct(totalMarginContribution > 0 ? (selectedRow.marginContribution / totalMarginContribution) * 100 : 0, 2)}</strong>
                </article>
                <article>
                  <span>Udeo u koliÄini <InfoTip text="Procenat koji ovaj tip obuÄ‡e Äini u ukupno prodatoj koliÄini." /></span>
                  <strong>{fmtPct((data?.totals.ukupnaKolicina ?? 0) > 0 ? (selectedRow.ukupnaKolicina / (data?.totals.ukupnaKolicina ?? 1)) * 100 : 0, 2)}</strong>
                </article>
                <article>
                  <span>Broj artikala <InfoTip text="Ukupan broj razliÄitih artikala ovog tipa obuÄ‡e koji su prodati." /></span>
                  <strong>{selectedRow.brojArtikalaUkupno}</strong>
                </article>
              </div>

              <h4 className="shoetype-decision-detail-section-title">Trend u odnosu na prethodni period</h4>
              <div className="shoetype-decision-detail-grid">
                <article>
                  <span>PoP trend prometa <InfoTip text={analyticsMetricDescriptions.popRevenueChangePct} /></span>
                  <strong className={describePopMetric(selectedRow).className} title={describePopMetric(selectedRow).title}>
                    {describePopMetric(selectedRow).label}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period promet <InfoTip text="Ukupan promet ovog tipa obuÄ‡e u prethodnom periodu (iste duÅ¾ine kao trenutni)." /></span>
                  <strong>{selectedRow.previousPeriodRevenue != null ? fmtRsd(selectedRow.previousPeriodRevenue) : "N/A"}</strong>
                </article>
                <article>
                  <span>PoP trend koliÄine <InfoTip text="Procenat promene prodatih komada u odnosu na prethodni uporediv period." /></span>
                  <strong className={trendClass(selectedRow.popUnitsChangePct ?? null)}>
                    {fmtSignedPct(selectedRow.popUnitsChangePct)}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period koliÄina <InfoTip text="Broj prodatih komada ovog tipa u prethodnom periodu." /></span>
                  <strong>{selectedRow.previousPeriodUnits != null ? fmtQty(selectedRow.previousPeriodUnits) : "N/A"}</strong>
                </article>
              </div>

              <h4 className="shoetype-decision-detail-section-title">Nivelacija</h4>
              <div className="shoetype-decision-detail-grid">
                <article>
                  <span>Nivelacija impact prometa <InfoTip text={analyticsMetricDescriptions.prePostNivelacijaImpactPct} /></span>
                  <strong className={describeNivelacijaImpactMetric(selectedRow).className} title={describeNivelacijaImpactMetric(selectedRow).title}>
                    {describeNivelacijaImpactMetric(selectedRow).label}
                  </strong>
                </article>
                <article>
                  <span>Pre/post pokrice prometa <InfoTip text="Procenat prometa koji dolazi od artikala sa prodajom i pre i posle nivelacije." /></span>
                  <strong>{fmtPct(selectedRow.prePostNivelacijaRevenueCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Uporedivi artikli <InfoTip text="Broj artikala sa prodajom i pre i posle nivelacije (koristi se za proraÄun pre/post uticaja)." /></span>
                  <strong>{selectedRow.prePostComparableArticleCount ?? 0}</strong>
                </article>
                <article>
                  <span>Pre nivelacije promet <InfoTip text="Zbir vrednosti prodaja pre prvog datuma nivelacije (aÅ¾uriranja cene) za ovaj tip." /></span>
                  <strong>{fmtRsd(selectedRow.preNivelacijePromet)}</strong>
                </article>
                <article>
                  <span>Posle nivelacije promet <InfoTip text="Zbir vrednosti prodaja od prvog datuma nivelacije (aÅ¾uriranja cene) nadalje." /></span>
                  <strong>{fmtRsd(selectedRow.posleNivelacijePromet)}</strong>
                </article>
                <article>
                  <span>Pre nivo koliÄina <InfoTip text="Ukupan broj prodanih komada pre prvog datuma nivelacije." /></span>
                  <strong>{fmtQty(selectedRow.preNivelacijeKolicina)}</strong>
                </article>
                <article>
                  <span>Posle nivo koliÄina <InfoTip text="Ukupan broj prodanih komada od prvog datuma nivelacije nadalje." /></span>
                  <strong>{fmtQty(selectedRow.posleNivelacijeKolicina)}</strong>
                </article>
                <article>
                  <span>Artikli sa nivelacijom <InfoTip text="Broj artikala sa registrovnom nivelacijom / ukupan broj artikala ovog tipa." /></span>
                  <strong>{selectedRow.brojArtikalaSaNivelacijom} / {selectedRow.brojArtikalaUkupno}</strong>
                </article>
              </div>

              <h4 className="shoetype-decision-detail-section-title">Kvalitet podataka</h4>
              <div className="shoetype-decision-detail-grid">
                <article>
                  <span>Kvalitet marÅ¾e <InfoTip text="Klasifikacija pouzdanosti obracuna marze na osnovu pokrica nabavne cene: PotvrÄ‘ena (â‰¥80% istorijski), DelimiÄno (â‰¥50% istorijski), Procenjena (<50% istorijski), Bez troÅ¡ka (0% pokrice)." /></span>
                  <strong>
                    <span className={`shoetype-decision-kpi-badge ${qualityTierClass(selectedRow.marginQualityTier)}`}>
                      {qualityTierIcon(selectedRow.marginQualityTier)} {selectedRow.marginQualityLabel}
                    </span>
                  </strong>
                </article>
                <article>
                  <span>{RECOMMENDATION_RELIABILITY_LABEL} <InfoTip text={analyticsMetricDescriptions.reliabilityPct} /></span>
                  <strong>{fmtPct(selectedRow.reliabilityPct, 1)}</strong>
                </article>
                <article>
                  <span>Pokrice direktnom nabavnom % <InfoTip text={analyticsMetricDescriptions.costCoverage} /></span>
                  <strong>{fmtPct(selectedRow.historicalCostCoveragePct ?? selectedRow.marginDataCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Promet sa procenjenom nabavnom % <InfoTip text="Procenat prometa gde je nabavna cena procenjena iz artikla (bez direktnog troska na stavci prodaje). Formula: promet sa procenjenom nabavnom / ukupan promet x 100. Operativni troskovi nisu ukljuceni." /></span>
                  <strong>{fmtPct(selectedRow.estimatedCostCoveragePct ?? selectedRow.fallbackCostCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Promet bez nabavne cene % <InfoTip text="Procenat prometa koji nema ni direktni ni procenjeni trosak, pa ne ulazi u obracun marznog doprinosa ni marze %. Formula: promet bez troska / ukupan promet x 100." /></span>
                  <strong>{fmtPct(selectedRow.noCostCoveragePct, 1)}</strong>
                </article>
                {(selectedRow.snapshotCostCoveragePct ?? 0) > 0 ? (
                  <article>
                    <span>Zamrznuta procena (snapshot) % <InfoTip text="Procenat prometa gde je trosak stabilizovan snapshot-om radi reproduktivnosti izvestaja. Ovo nije istorijska nabavna cena sa trenutka prodaje." /></span>
                    <strong>{fmtPct(selectedRow.snapshotCostCoveragePct, 1)}</strong>
                  </article>
                ) : null}
                <article>
                  <span>{RECOMMENDATION_CONFIDENCE_LABEL} <InfoTip text={analyticsMetricDescriptions.recommendationConfidencePct} /></span>
                  <strong>{selectedRow.recommendationConfidencePct}</strong>
                </article>
              </div>

              {selectedRow.prePostSignalNote ? (
                <p className="shoetype-decision-reason">
                  <strong>Napomena za pre/post signal:</strong> {selectedRow.prePostSignalNote}
                </p>
              ) : null}

              {(() => {
                const marginNote = buildMarginDetailNote(
                  selectedRow.marginQualityTier,
                  selectedRow.estimatedCostCoveragePct ?? selectedRow.fallbackCostCoveragePct,
                  selectedRow.historicalCostCoveragePct ?? selectedRow.marginDataCoveragePct,
                  fmtPct,
                  selectedRow.snapshotCostCoveragePct,
                  data.totals.isSnapshotActive
                );
                return marginNote ? (
                  <p className="shoetype-decision-reason">
                    <strong>Napomena za marÅ¾u:</strong> {marginNote}
                  </p>
                ) : null;
              })()}

              {(() => {
                const recCaveat = buildRecommendationCaveat(
                  selectedRow.marginQualityTier,
                  selectedRow.estimatedCostCoveragePct ?? selectedRow.fallbackCostCoveragePct,
                  fmtPct
                );
                return recCaveat ? (
                  <p className="shoetype-decision-reason">
                    <strong>Napomena za preporuku:</strong> {recCaveat}
                  </p>
                ) : null;
              })()}

              <p className="shoetype-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedRow.statusReason}
              </p>
            </section>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
