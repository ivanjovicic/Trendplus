import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate, useSearchParams } from "react-router-dom";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { getStores } from "../services/analyticsApi";
import {
  getColorSalesStats,
  type ColorSalesStat,
  type ColorSalesStatsResponse,
} from "../services/colorSalesStatsApi";
import type { StoreOption } from "../types/analytics";
import AnalyticsControlBar, {
  type AnalyticsControlBarChip,
  type AnalyticsControlBarField,
} from "../components/analytics/AnalyticsControlBar";
import AnalyticsDataTable from "../components/analytics/AnalyticsDataTable";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import InfoTip from "../components/ui/InfoTip";
import UltraSpinner from "../components/ui/UltraSpinner";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { getDataScope, type DataScope } from "../utils/dataScope";
import { fmtNumber, fmtPct, fmtQty, fmtRsd, fmtSignedPct, formatDate, getPresetRange } from "../utils/analyticsFormatters";
import { resolvePresetFilterRange } from "../utils/analyticsPeriodPresets";
import {
  RECOMMENDATION_SIGNAL_UNAVAILABLE,
  RECOMMENDATION_STATUS_PRIORITY,
  recommendationStatusLabel,
  recommendationStatusTone,
  recommendationStatusTooltipBrief,
  type CanonicalRecommendationStatus,
} from "../utils/canonicalRecommendationSemantics";
import {
  formatCategoryPrePostQuantityMetric,
  formatCategoryPrePostRevenueMetric,
} from "../utils/categoryPrePostDetailMetrics";
import { buildColorRecommendationProjection } from "../utils/colorStatusIdentity";
import {
  resolveColorComplementPercent,
  resolveColorCountValue,
  resolveColorPercentValue,
} from "../utils/colorPercentRange";
import { resolveColorCoveragePct } from "../utils/colorSalesCoverage";
import { CHART_TOOLTIP_STYLE, CHART_TOOLTIP_LABEL_STYLE } from "../utils/chartTooltipStyle";
import { getAnalyticsDataFreshnessStatus } from "../utils/analyticsResponseMeta";
import { readAnalyticsTableSort, writeAnalyticsTableSort } from "../utils/analyticsTableSortUrl";
import { useReliableAnalyticsQuery } from "../hooks/useReliableAnalyticsQuery";
import "./ColorSalesStatsPage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
type SortDir = "asc" | "desc";
type SortField =
  | "boja"
  | "ukupanPromet"
  | "sharePct"
  | "marginContribution"
  | "popRevenueChangePct"
  | "prePostNivelacijaRevenueImpactPct"
  | "status";
const COLOR_SORT_FIELDS: readonly SortField[] = [
  "boja",
  "ukupanPromet",
  "sharePct",
  "marginContribution",
  "popRevenueChangePct",
  "prePostNivelacijaRevenueImpactPct",
  "status",
];
type ActiveFilters = {
  fromDate: string;
  toDate: string;
  sezonaId: number | null;
  storeId: number | null;
};

type DecisionColor = Omit<ColorSalesStat, "reliabilityPct"> & {
  sharePct: number | null;
  marginContribution: number;
  reliabilityPct: number | null;
  recommendationAllowed: boolean;
  coveragePct: number | null;
  splitCoveragePct: number | null;
  decisionScore: number | null;
  status: CanonicalRecommendationStatus;
  statusReason: string;
  reliabilityAvailable: boolean;
};

const decisionColumns: AnalyticsTableColumn<DecisionColor>[] = [
  { key: "boja", header: "Boja", dataType: "text" },
  { key: "ukupanPromet", header: "Promet", dataType: "currency" },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  { key: "marginContribution", header: "Maržni doprinos", dataType: "currency" },
  { key: "popRevenueChangePct", header: "PoP trend %", dataType: "percent" },
  { key: "prePostNivelacijaRevenueImpactPct", header: "Uticaj nivelacije %", dataType: "percent" },
  {
    key: "preNivelacijePromet",
    header: "Pre nivelacije promet",
    detailLabel: "Pre nivelacije promet",
    dataType: "text",
    getValue: (row) => formatCategoryPrePostRevenueMetric(row.preNivelacijePromet),
  },
  {
    key: "posleNivelacijePromet",
    header: "Posle nivelacije promet",
    detailLabel: "Posle nivelacije promet",
    dataType: "text",
    getValue: (row) => formatCategoryPrePostRevenueMetric(row.posleNivelacijePromet),
  },
  {
    key: "preNivelacijeKolicina",
    header: "Pre nivo kolicina",
    detailLabel: "Pre nivo kolicina",
    dataType: "text",
    getValue: (row) => formatCategoryPrePostQuantityMetric(row.preNivelacijeKolicina),
  },
  {
    key: "posleNivelacijeKolicina",
    header: "Posle nivo kolicina",
    detailLabel: "Posle nivo kolicina",
    dataType: "text",
    getValue: (row) => formatCategoryPrePostQuantityMetric(row.posleNivelacijeKolicina),
  },
  { key: "status", header: "Preporuka", dataType: "text", getValue: (row) => recommendationStatusLabel(row.status) },
  { key: "decisionScore", header: "Skor odluke", dataType: "number" },
];

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

function normalizeName(value: string | null | undefined): string {
  return (value ?? "").trim().toUpperCase();
}

function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string {
  if (field !== activeField) return "";
  return dir === "asc" ? " ^" : " v";
}

function isSortActive(field: SortField, activeField: SortField): boolean {
  return field === activeField;
}

function statusClass(status: CanonicalRecommendationStatus): string {
  const tone = recommendationStatusTone(status);
  if (tone === "boost") return "color-decision-status status-boost";
  if (tone === "keep") return "color-decision-status status-keep";
  if (tone === "review") return "color-decision-status status-review";
  if (tone === "reduce") return "color-decision-status status-reduce";
  return "color-decision-status status-na";
}

function displayStatusLabel(status: CanonicalRecommendationStatus): string {
  return recommendationStatusLabel(status);
}

function trendClass(value: number | null | undefined): string {
  if (value == null || !Number.isFinite(value)) return "trend-neutral";
  if (value > 0) return "trend-up";
  if (value < 0) return "trend-down";
  return "trend-neutral";
}

type StatusTooltipData = {
  status: CanonicalRecommendationStatus;
  statusReason: string;
  sharePct: number | null;
  marginPct: number | null;
  popRevenueChangePct: number | null;
  prePostNivelacijaRevenueImpactPct: number | null;
  previousPeriodRevenue: number | null;
  splitCoveragePct: number | null;
  reliabilityPct: number | null;
  reliabilityAvailable: boolean;
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
  const reliabilityText = data.reliabilityAvailable ? fmtPct(data.reliabilityPct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  return `${recommendationStatusLabel(data.status)}: ${data.statusReason} | ${recommendationStatusTooltipBrief(data.status)} | Udeo ${fmtPct(data.sharePct, 1)} | Marža ${fmtPct(data.marginPct, 1)} | Trend ${popText} | Uticaj nivelacije ${impactText} | Pokriće podele ${fmtPct(data.splitCoveragePct, 1)} | Pouzdanost ${reliabilityText}`;
}

export function describePopMetric(item: ColorSalesStat): { label: string; title: string; className: string } {
  if (item.popRevenueChangePct != null && Number.isFinite(item.popRevenueChangePct)) {
    return {
      label: fmtSignedPct(item.popRevenueChangePct, 2),
      title: `PoP trend poredi ukupan promet sa prethodnim uporedivim periodom. Prethodni period: ${fmtRsd(item.previousPeriodRevenue, 0, "Nije dostupno")}.`,
      className: trendClass(item.popRevenueChangePct),
    };
  }

  if (item.previousPeriodRevenue != null && item.previousPeriodRevenue <= 0 && item.ukupanPromet > 0) {
    return {
      label: "Novo",
      title: "Boja nije imala promet u prethodnom uporedivom periodu, pa PoP procenat nije smislen.",
      className: "trend-neutral",
    };
  }

  return {
    label: "N/A",
    title: "PoP trend nije dostupan jer ne postoji validna prethodna baza za poređenje.",
    className: "trend-neutral",
  };
}

export function describeNivelacijaImpactMetric(item: ColorSalesStat): { label: string; title: string; className: string } {
  if (Number.isFinite(item.prePostNivelacijaRevenueImpactPct)) {
    return {
      label: fmtSignedPct(item.prePostNivelacijaRevenueImpactPct, 2),
      title: `Pre/post nivelacija impact meri promenu prometa unutar artikala sa poznatim prvim datumom nivelacije. Pokriće: ${fmtPct(resolveColorPercentValue(item.prePostNivelacijaRevenueCoveragePct), 1)} prometa.`,
      className: trendClass(item.prePostNivelacijaRevenueImpactPct),
    };
  }

  const coverage = resolveColorPercentValue(item.prePostNivelacijaRevenueCoveragePct);
  if (coverage == null) {
    return {
      label: "N/A",
      title: "Pre/post pokriće nije dostupno jer validno pokriće nije dostupno za ovaj skup podataka.",
      className: "trend-neutral",
    };
  }

  if (coverage === 0) {
    return {
      label: "0% pokriće",
      title: "Pre/post pokriće je izmereno kao 0%; nema artikala sa poznatom istorijom nivelacije, pa impact nije merljiv.",
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

function colorKey(item: { boja: string }): string {
  return normalizeName(item.boja);
}

export default function ColorSalesStatsPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const detailSectionRef = useRef<HTMLElement>(null);

  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>("30d");
  const [fromDate, setFromDate] = useState(() => getPresetRange("30d").fromDate);
  const [toDate, setToDate] = useState(() => getPresetRange("30d").toDate);
  const [sezonaId, setSezonaId] = useState<number | null>(null);
  const [storeId, setStoreId] = useState<number | null>(null);
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>(() => {
    const range = getPresetRange("30d");
    return {
      fromDate: range.fromDate,
      toDate: range.toDate,
      sezonaId: null,
      storeId: null,
    };
  });

  const [stores, setStores] = useState<StoreOption[]>([]);
  const [dataScope, setDataScopeValue] = useState<DataScope>(() => getDataScope());
  const [sortField, setSortField] = useState<SortField>(() => readAnalyticsTableSort(searchParams, COLOR_SORT_FIELDS, "status", "desc").field);
  const [sortDir, setSortDir] = useState<SortDir>(() => readAnalyticsTableSort(searchParams, COLOR_SORT_FIELDS, "status", "desc").dir);
  const [expandedColorKey, setExpandedColorKey] = useState<string | null>(null);

  useEffect(() => {
    const nextSort = readAnalyticsTableSort(searchParams, COLOR_SORT_FIELDS, "status", "desc");
    setSortField((current) => current === nextSort.field ? current : nextSort.field);
    setSortDir((current) => current === nextSort.dir ? current : nextSort.dir);
  }, [searchParams]);

  const invalidRange = useMemo(() => {
    if (!fromDate || !toDate) return false;
    return new Date(fromDate) > new Date(toDate);
  }, [fromDate, toDate]);

  useEffect(() => {
    const handleScopeChange = () => {
      setDataScopeValue(getDataScope());
    };

    window.addEventListener("trendplus:data-scope-changed", handleScopeChange);
    return () => {
      window.removeEventListener("trendplus:data-scope-changed", handleScopeChange);
    };
  }, []);

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

  const colorQuery = useCallback((signal: AbortSignal) => {
    const currentRange = toUtcRange(activeFilters.fromDate, activeFilters.toDate);
    return getColorSalesStats({
      ...currentRange,
      sezonaId: activeFilters.sezonaId,
      storeId: activeFilters.storeId,
      dataScope,
      signal,
    });
  }, [activeFilters, dataScope]);
  const {
    data,
    initialLoading,
    refetching,
    error: queryError,
    staleWarning,
    refetch,
  } = useReliableAnalyticsQuery<ColorSalesStatsResponse>({
    query: colorQuery,
    getErrorMessage: useCallback((reason: unknown) => reason instanceof Error
      ? reason.message
      : "Greska pri ucitavanju podataka po boji.", []),
  });
  const loading = initialLoading || refetching;
  const error = queryError;

  const decisionRows = useMemo<DecisionColor[]>(() => {
    const rows = data?.colors ?? [];
    if (rows.length === 0) return [];

    return rows.map((item) => {
      const sharePct = resolveColorPercentValue(item.sharePct);
      const marginContribution = item.marginContribution;
      const splitCoveragePct = resolveColorPercentValue(item.prePostNivelacijaRevenueCoveragePct);
      const coveragePct = resolveColorCoveragePct(
        item.brojArtikalaSaNivelacijom,
        item.brojArtikalaUkupno,
      );

      const recommendationProjection = buildColorRecommendationProjection(
        item.recommendation,
        item.reliabilityPct,
      );

      return {
        ...item,
        sharePct,
        marginContribution,
        reliabilityPct: recommendationProjection.reliabilityPct,
        reliabilityAvailable: recommendationProjection.reliabilityAvailable,
        recommendationAllowed: recommendationProjection.recommendationAllowed,
        coveragePct,
        splitCoveragePct,
        decisionScore: recommendationProjection.recommendationAllowed
          ? resolveColorPercentValue(item.decisionScore)
          : null,
        status: recommendationProjection.status,
        statusReason: recommendationProjection.statusReason,
      };
    });
  }, [data?.colors]);

  const sortedRows = useMemo(() => {
    const rows = [...decisionRows];
    return rows.sort((a, b) => {
      let compare = 0;

      if (sortField === "boja") {
        compare = a.boja.localeCompare(b.boja, "sr");
      } else if (sortField === "ukupanPromet") {
        compare = a.ukupanPromet - b.ukupanPromet;
      } else if (sortField === "sharePct") {
        compare = (a.sharePct ?? -1) - (b.sharePct ?? -1);
      } else if (sortField === "marginContribution") {
        compare = a.marginContribution - b.marginContribution;
      } else if (sortField === "popRevenueChangePct") {
        compare = (a.popRevenueChangePct ?? -9999) - (b.popRevenueChangePct ?? -9999);
      } else if (sortField === "prePostNivelacijaRevenueImpactPct") {
        compare = (a.prePostNivelacijaRevenueImpactPct ?? -9999) - (b.prePostNivelacijaRevenueImpactPct ?? -9999);
      } else if (sortField === "status") {
        compare = RECOMMENDATION_STATUS_PRIORITY[a.status] - RECOMMENDATION_STATUS_PRIORITY[b.status];
      }

      if (compare === 0) compare = (a.decisionScore ?? -1) - (b.decisionScore ?? -1);
      if (compare === 0) compare = a.ukupanPromet - b.ukupanPromet;
      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const selectedRow = useMemo(
    () => sortedRows.find((row) => colorKey(row) === expandedColorKey) ?? null,
    [expandedColorKey, sortedRows]
  );

  useEffect(() => {
    if (!selectedRow && sortedRows.length > 0 && expandedColorKey != null) {
      setExpandedColorKey(null);
    }
  }, [expandedColorKey, selectedRow, sortedRows.length]);

  useEffect(() => {
    if (selectedRow && detailSectionRef.current) {
      const delay = 100;
      setTimeout(() => {
        detailSectionRef.current?.scrollIntoView({
          behavior: "smooth",
          block: "start",
        });
      }, delay);
    }
  }, [selectedRow]);

  const totalRevenue = data ? data.totals.ukupanPromet : null;
  const top5SharePct = null;

  const totalMarginContribution = useMemo(
    () => data ? data.totals.ukupanMarzniDoprinos : null,
    [data?.totals.ukupanMarzniDoprinos]
  );

  const periodGrowthPct = useMemo(() => data?.totals.popRevenueChangePct ?? null, [data?.totals.popRevenueChangePct]);

  const concentrationData = useMemo(() => {
    if (sortedRows.length === 0) return [] as Array<{ name: string; sharePct: number }>;

    const ranked = [...sortedRows]
      .filter((row): row is typeof row & { sharePct: number } => resolveColorPercentValue(row.sharePct) != null)
      .sort((a, b) => b.sharePct - a.sharePct);
    if (ranked.length === 0) return [];
    const topRows = ranked.slice(0, 6).map((row) => ({
      name: row.boja,
      sharePct: Number(row.sharePct.toFixed(2)),
    }));

    const remaining = ranked.slice(6).reduce((sum, row) => sum + row.sharePct, 0);
    const ostaleSharePct = resolveColorPercentValue(Number(remaining.toFixed(2)));
    if (ostaleSharePct != null && ostaleSharePct > 0.1) {
      topRows.push({ name: "Ostale", sharePct: ostaleSharePct });
    }

    return topRows;
  }, [sortedRows]);

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
    const splitCoverage = resolveColorPercentValue(data.dataQuality.revenueWithNivelacijaSplitSharePct);
    const missingCostShare = resolveColorPercentValue(data.dataQuality.missingCostRevenueSharePct);
    const knownCostShare = resolveColorComplementPercent(missingCostShare);
    const unknownShare = resolveColorPercentValue(data.dataQuality.unknownColorRevenueSharePct);
    const costQualityDenominatorStatus = data.dataQuality.costQualityDenominatorStatus;

    if (splitCoverage != null && splitCoverage < 60) {
      notes.push(`Pre/post nivelacija trenutno pokriva ${fmtPct(splitCoverage, 1)} ukupnog prometa, pa taj signal treba čitati kao delimičan.`);
    }

    if (knownCostShare != null && knownCostShare < 100) {
      notes.push(`Marža i maržni doprinos su zasnovani na ${fmtPct(knownCostShare, 1)} prometa sa poznatom nabavnom cenom.`);
    }

    if (unknownShare != null && unknownShare > 0) {
      notes.push(`Nepoznate boje učestvuju sa ${fmtPct(unknownShare, 1)} ukupnog prometa.`);
    }

    if (costQualityDenominatorStatus === "unavailable_non_positive_net_revenue") {
      notes.push("Neto promet nije pozitivan, pa coverage troška i automatska preporuka nisu merljivi; signed iznosi ostaju prikazani.");
    }

    return notes;
  }, [data]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "fromDate", label: "Od", value: activeFilters.fromDate },
      { key: "toDate", label: "Do", value: activeFilters.toDate },
      { key: "sezonaId", label: "Sezona", value: activeSezonaLabel },
      { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "Svi objekti" },
      { key: "dataScope", label: "Opseg podataka", value: dataScope },
    ],
    [activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, activeSezonaLabel, dataScope]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
      { key: "dataScope", label: "Opseg podataka", value: data?.dataScope ?? dataScope },
      { key: "lineageBasis", label: "Osnova događaja nivelacije", value: data?.lineage ? `${data.lineage.salesArticlesWithMatchingNivelacija}/${data.lineage.salesArticleCount} artikala ima potvrđen događaj u istom opsegu` : "Nije dostupno" },
      { key: "nivelacijaEventCount", label: "Događaji nivelacije", value: data?.lineage?.eventCount ?? null },
      { key: "bojaCount", label: "Broj boja", value: fmtNumber(resolveColorCountValue(data?.totals.brojBoja)) },
      { key: "marginCoverage", label: "Promet sa nabavnom cenom", value: fmtPct(resolveColorComplementPercent(data?.dataQuality.missingCostRevenueSharePct), 1) },
      { key: "splitCoverage", label: "Pre/post pokriće", value: fmtPct(resolveColorPercentValue(data?.dataQuality.revenueWithNivelacijaSplitSharePct), 1) },
      { key: "signedEvidence", label: "Neto dokaz", value: data?.dataQuality.signedRevenuePolicy === "signed_net_revenue_preserved" ? "Neto promet i količina" : "Nije dostupno" },
      { key: "costDenominator", label: "Imenilac pokrića", value: data?.dataQuality.costQualityDenominatorStatus === "measured_positive_net_revenue" ? "Pozitivan neto promet" : "Nije merljivo" },
      { key: "weightedMargin", label: "Ponderisana poznata marža", value: fmtPct(data?.dataQuality.weightedKnownMarginPct, 1) },
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
      data?.dataQuality.missingCostRevenueSharePct,
      data?.dataQuality.costQualityDenominatorStatus,
      data?.dataQuality.revenueWithNivelacijaSplitSharePct,
      data?.dataQuality.signedRevenuePolicy,
      data?.dataQuality.weightedKnownMarginPct,
      data?.dataScope,
      data?.generatedAt,
      data?.lineage,
      data?.totals.brojBoja,
      dataScope,
    ]
  );

  const headerDataQualityStatus = useMemo(() => {
    if (!data) return null;
    if (sortedRows.length === 0) return "insufficient_data";
    const missingCostShare = resolveColorPercentValue(data.dataQuality.missingCostRevenueSharePct);
    const splitCoverage = resolveColorPercentValue(data.dataQuality.revenueWithNivelacijaSplitSharePct);
    if (missingCostShare == null || splitCoverage == null) {
      return "insufficient_data";
    }
    return qualityNotes.length > 0 ? "warning" : "good";
  }, [data, qualityNotes.length, sortedRows.length]);

  const responseMeta = data?.meta ?? null;
  const trustDataQualityStatus = responseMeta?.dataQualityStatus ?? headerDataQualityStatus;
  const trustLastRefreshAt = responseMeta?.lastRefreshAtUtc ?? null;
  const trustIsPartial = responseMeta?.isPartial ?? false;
  const trustDataFreshnessStatus = getAnalyticsDataFreshnessStatus(responseMeta);
  const trustEmptyStateReason = responseMeta?.message ?? emptyStateHint;
  const lineageBasis = useMemo(() => {
    if (!data?.lineage) return null;
    const scopeLabel = data.lineage.dataScope === "imported"
      ? "uvezeni podaci"
      : data.lineage.dataScope === "existing"
        ? "postojeći podaci"
        : "svi izvori podataka";
    const storeLabel = data.lineage.storeId == null ? "svi objekti" : `objekat ${data.lineage.storeId}`;
    const matchedLabel = `${data.lineage.salesArticlesWithMatchingNivelacija}/${data.lineage.salesArticleCount} artikala sa potvrđenim događajem nivelacije`;
    const storePolicyLabel = data.lineage.storeId == null
      ? "događaji sa svih objekata"
      : "samo tačan objekat; događaji bez objekta su izuzeti";
    return `${scopeLabel}; ${storeLabel}; ${matchedLabel}; ${storePolicyLabel}`;
  }, [data?.lineage]);
  const showBlockingError = Boolean(queryError && !data);
  const showStaleError = Boolean(staleWarning && data);

  const emptyStateVariant = useMemo<"no_data" | "insufficient_data" | "filtered_out" | null>(() => {
    if (!data || loading || sortedRows.length > 0) return null;
    if (data.meta?.emptyReason === "filtered_out") return "filtered_out";
    return "no_data";
  }, [data, loading, sortedRows.length]);

  const controlBarChips = useMemo<AnalyticsControlBarChip[]>(
    () => [
      {
        key: "scope",
        label: "Opseg",
        value: dataScope,
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
        value: `${sortedRows.length.toLocaleString("sr-RS")} / ${(data?.colors?.length ?? 0).toLocaleString("sr-RS")}`,
        tone: sortedRows.length === 0 ? "warning" : "success",
      },
    ],
    [activeFilters.fromDate, activeFilters.toDate, data?.colors?.length, dataScope, sortedRows.length],
  );

  const openDetail = useCallback((row: DecisionColor) => {
    const recordId = encodeURIComponent(row.boja);

    const params = new URLSearchParams();
    params.set("fromDate", `${activeFilters.fromDate}T00:00:00Z`);
    params.set("toDate", `${activeFilters.toDate}T23:59:59Z`);
    if (activeFilters.sezonaId != null) params.set("sezonaId", String(activeFilters.sezonaId));
    if (activeFilters.storeId != null) params.set("storeId", String(activeFilters.storeId));
    params.set("dataScope", dataScope);

    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "color-sales-stats",
        recordId,
        title: row.boja,
        subtitle: "Detalj odluke po boji",
        columns: decisionColumns,
        row,
        metadata: [...toolbarFilters, ...toolbarMetadata],
      })
    );

    navigate(`/analitika/color-sales-stats/${recordId}?${params.toString()}`, {
      state: { backgroundLocation: location },
    });
  }, [activeFilters.fromDate, activeFilters.sezonaId, activeFilters.storeId, activeFilters.toDate, dataScope, location, navigate, toolbarFilters, toolbarMetadata]);

  function applyPreset(preset: PeriodPreset) {
    setPeriodPreset(preset);
    if (preset === "custom") return;
    const range = getPresetRange(preset);
    setSezonaId(null);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
  }

  function handleSeasonChange(value: string) {
    const parsed = value ? Number(value) : null;
    setSezonaId(parsed);
    setPeriodPreset("custom");

    if (parsed == null) return;

    const selected = data?.sezone.find((item) => item.id === parsed);
    if (!selected) return;
    setFromDate(toDateOnly(selected.datumOd));
    setToDate(toDateOnly(selected.datumDo));
  }

  const applyFilters = () => {
    if (invalidRange) {
      return;
    }

    const range = resolvePresetFilterRange(periodPreset, fromDate, toDate);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    setActiveFilters({
      fromDate: range.fromDate,
      toDate: range.toDate,
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
              setPeriodPreset("custom");
              setSezonaId(null);
              setFromDate(event.target.value);
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
              setPeriodPreset("custom");
              setSezonaId(null);
              setToDate(event.target.value);
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
            onChange={(event) => setStoreId(event.target.value ? Number(event.target.value) : null)}
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
    ],
    [data?.sezone, fromDate, handleSeasonChange, periodPreset, sezonaId, storeId, stores, toDate],
  );

  const handleSort = (field: SortField) => {
    const nextDir: SortDir = sortField === field
      ? (sortDir === "asc" ? "desc" : "asc")
      : (field === "boja" ? "asc" : "desc");
    setSortField(field);
    setSortDir(nextDir);
    setSearchParams((current) => writeAnalyticsTableSort(current, field, nextDir), { replace: true });
  };

  return (
    <div className="color-decision-page">
      <AnalyticsTrustHeader
        title="Prodaja po boji artikla"
        description="Podrška za odluku o bojama koje treba pojačati u nabavci."
        periodFrom={data?.fromDate ?? activeFilters.fromDate}
        periodTo={data?.toDate ?? activeFilters.toDate}
        lastRefreshAt={trustLastRefreshAt}
        dataFreshnessStatus={trustDataFreshnessStatus}
        dataSource={`Prodaja po boji artikla (opseg: ${data?.dataScope ?? dataScope})`}
        provenanceBasis={lineageBasis}
        dataQualityStatus={trustDataQualityStatus}
        mode="recommendation"
        isPartial={trustIsPartial}
        recommendationNote="Preporuke dolaze iz backenda; ovaj ekran zadržava odluku, period i kvalitet podataka na jednom mestu."
        emptyStateReason={!loading && !showBlockingError && trustEmptyStateReason ? trustEmptyStateReason : null}
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />

      <AnalyticsControlBar
        title="Opseg i filteri"
        description="Period, sezona i objekat ostaju ovde; prioritetna lista ispod ostaje fokusirana na boje."
        chips={controlBarChips}
        primaryAction={{
          key: "apply",
          label: loading ? "Učitavanje..." : "Primeni filtere",
          onClick: applyFilters,
          disabled: loading,
        }}
        secondaryActions={[
          {
            key: "reset",
            label: "Reset filtera",
            onClick: resetFilters,
            disabled: loading,
            tone: "secondary",
          },
          {
            key: "data-quality",
            label: "Kvalitet podataka",
            to: "/analytics/data-quality",
            tone: "secondary",
          },
        ]}
        fields={controlBarFields}
      />

      {invalidRange ? (
        <div className="color-decision-message error">Datum „Od” ne može biti posle datuma „Do”.</div>
      ) : null}
      {showBlockingError ? (
        <AnalyticsErrorState
          title="Boje trenutno nisu dostupne"
          message={error || "Ne prikazujemo nule jer nije potvrđeno da je period stvarno prazan."}
          onRetry={refetch}
          helpHref="/analytics/data-quality"
        />
      ) : null}
      {showStaleError ? (
        <div
          className="color-decision-message info"
          role="status"
          aria-live="polite"
          data-testid="color-stale-refetch-warning"
        >
          Prikazujemo prethodno učitane podatke. Novi upit nije uspeo.
        </div>
      ) : null}
      {loading ? (
        <div className="color-decision-message loading">
          <UltraSpinner size="sm" label="Učitavam boje" className="color-decision-loading-spinner" />
          <span>Učitavam boje...</span>
        </div>
      ) : null}
      {!loading && !showBlockingError && emptyStateVariant ? (
        <AnalyticsEmptyState
          variant={emptyStateVariant ?? undefined}
          message={emptyStateHint ?? undefined}
          emptyReason={responseMeta?.emptyReason ?? null}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          onRetry={refetch}
        />
      ) : null}
      {!loading && !showBlockingError && qualityNotes.length > 0 ? (
        <div className="color-decision-message info">
          <strong>Kvalitet podataka:</strong> {qualityNotes.join(" ")}
        </div>
      ) : null}

      {!loading && data ? (
        <>
          {!emptyStateHint ? (
            <section className="color-decision-kpis">
              <article className="color-decision-kpi">
                <span>Ukupan promet</span>
                <strong>{fmtRsd(totalRevenue)}</strong>
              </article>
              <article className="color-decision-kpi">
                <span>Udeo top 5 boja <InfoTip text="N/A dok backend ne vrati autoritativni udeo top 5 boja; frontend ne računa ovaj procenat iz redova." /></span>
                <strong>{fmtPct(top5SharePct)}</strong>
              </article>
              <article className="color-decision-kpi">
                <span>Ukupan marzni doprinos</span>
                <strong>{fmtRsd(totalMarginContribution)}</strong>
              </article>
              <article className="color-decision-kpi">
                <span>Rast/PAD vs prethodni period</span>
                <strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong>
              </article>
            </section>
          ) : null}

          <section className="color-decision-panels">
            <article className="color-decision-card">
              <h2>Koncentracija prometa po bojama</h2>
              <p>Top boje koje nose najveći deo prodaje.</p>
              {concentrationData.length > 0 ? (
                <div className="color-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={concentrationData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} />
                      <Tooltip contentStyle={CHART_TOOLTIP_STYLE} labelStyle={CHART_TOOLTIP_LABEL_STYLE} formatter={(value: number | string | undefined) => value == null ? "N/A" : fmtPct(Number(value), 2)} />
                      <Bar dataKey="sharePct" fill="var(--accent-primary)" radius={[0, 8, 8, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="color-decision-empty">Nema podataka za grafikon koncentracije.</div>
              )}
            </article>

            <article className="color-decision-card">
              <div className="color-decision-table-head">
                <div>
                  <h2>Prioritetna lista boja</h2>
                  <p>
                    {recommendationStatusLabel("increase_focus")}: {counts.increaseFocus} | {recommendationStatusLabel("maintain")}: {counts.maintain} | {recommendationStatusLabel("review")}: {counts.review} | {recommendationStatusLabel("do_not_trust")}: {counts.doNotTrust} | {recommendationStatusLabel("insufficient_data")}: {counts.insufficientData}
                  </p>
                  <p className="color-decision-metric-note">
                    Trend = promena prometa prema prethodnom uporedivom periodu. Uticaj nivelacije = pre/post promena unutar prometa sa poznatim prvim datumom nivelacije.
                  </p>
                </div>
              </div>

              <AnalyticsDataTable
                rowCount={sortedRows.length}
                toolbar={(
                  <AnalyticsTableToolbar
                    tableKey="color-sales-stats"
                    tableTitle="Podrška odluci - boje artikala"
                    columns={decisionColumns}
                    rows={sortedRows}
                    filters={toolbarFilters}
                    metadata={toolbarMetadata}
                    defaultOrientation="landscape"
                  />
                )}
              >
                <table className="color-decision-table">
                  <thead>
                    <tr>
                      <th>
                        <button type="button" onClick={() => handleSort("boja")}>
                          Boja{sortMarker("boja", sortField, sortDir)} <InfoTip text="Naziv boje artikla." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("ukupanPromet", sortField) ? " is-sorted" : ""}`}>
                        <button type="button" onClick={() => handleSort("ukupanPromet")}>
                          Promet{sortMarker("ukupanPromet", sortField, sortDir)} <InfoTip text="Ukupna vrednost prodaje u izabranom periodu (RSD)." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("sharePct", sortField) ? " is-sorted" : ""}`}>
                        <button type="button" onClick={() => handleSort("sharePct")}>
                          Udeo %{sortMarker("sharePct", sortField, sortDir)} <InfoTip text="Udeo u ukupnom prometu (procenat)." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("marginContribution", sortField) ? " is-sorted" : ""}`}>
                        <button type="button" onClick={() => handleSort("marginContribution")}>
                          Maržni doprinos{sortMarker("marginContribution", sortField, sortDir)} <InfoTip text="Doprinos marže: razlika između prodajne vrednosti i nabavne vrednosti za prodatu robu." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("popRevenueChangePct", sortField) ? " is-sorted" : ""}`}>
                        <button type="button" onClick={() => handleSort("popRevenueChangePct")}>
                          PoP trend{sortMarker("popRevenueChangePct", sortField, sortDir)} <InfoTip text="Promena ukupnog prometa u odnosu na prethodni uporedivi period. N/A ako prethodni period nije dostupan; Novo ako je prethodni promet bio 0." />
                        </button>
                      </th>
                      <th className={`analytics-data-table__numeric${isSortActive("prePostNivelacijaRevenueImpactPct", sortField) ? " is-sorted" : ""}`}>
                        <button type="button" onClick={() => handleSort("prePostNivelacijaRevenueImpactPct")}>
                          Uticaj nivelacije{sortMarker("prePostNivelacijaRevenueImpactPct", sortField, sortDir)} <InfoTip text="Pre/post promena prometa unutar artikala sa poznatim prvim datumom nivelacije. Nije isto što i trend prema prethodnom periodu." />
                        </button>
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>
                          Preporuka{sortMarker("status", sortField, sortDir)} <InfoTip text="Sistemska preporuka: Pojacaj / Zadrzi / Pregledaj / Ne veruj / Nedovoljno podataka. Status i izvrsivost akcije su odvojeni signali." />
                        </button>
                      </th>
                      <th className="align-center">Detalj</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sortedRows.length === 0 ? (
                      <tr>
                        <td colSpan={8} className="color-decision-empty-row">
                          Nema podataka za izabrane filtere.
                        </td>
                      </tr>
                    ) : (
                      sortedRows.map((row) => {
                        const rowKey = colorKey(row);
                        const expanded = expandedColorKey === rowKey;
                        const popMetric = describePopMetric(row);
                        const nivelacijaImpactMetric = describeNivelacijaImpactMetric(row);
                        return (
                          <tr key={rowKey} className={expanded ? "expanded-row" : ""}>
                            <td>{row.boja}</td>
                            <td className="analytics-data-table__numeric">{fmtRsd(row.ukupanPromet)}</td>
                            <td className="analytics-data-table__numeric">{fmtPct(row.sharePct, 2)}</td>
                            <td className="analytics-data-table__numeric">{fmtRsd(row.marginContribution)}</td>
                            <td className={["analytics-data-table__numeric", popMetric.className].join(" ")} title={popMetric.title}>{popMetric.label}</td>
                            <td className={["analytics-data-table__numeric", nivelacijaImpactMetric.className].join(" ")} title={nivelacijaImpactMetric.title}>{nivelacijaImpactMetric.label}</td>
                            <td>
                              <div className="color-status-stack">
                                <span
                                  className={statusClass(row.status)}
                                  title={buildStatusTooltip(row)}
                                  aria-label={buildStatusTooltip(row)}
                                >
                                  {displayStatusLabel(row.status)}
                                </span>
                                <span className="color-status-reason-chip" title={row.statusReason}>
                                  {row.recommendationAllowed ? "Razlog" : "Akcija blokirana"} <InfoTip text={row.statusReason} />
                                </span>
                              </div>
                            </td>
                            <td className="align-center">
                              <button
                                type="button"
                                className="color-decision-detail-btn"
                                onClick={() => setExpandedColorKey(expanded ? null : rowKey)}
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
              </AnalyticsDataTable>
            </article>
          </section>

          {selectedRow ? (
            <section className="color-decision-detail" ref={detailSectionRef}>
              <div className="color-decision-detail-head">
                <h3>Detalj odluke: {selectedRow.boja}</h3>
                <button type="button" onClick={() => openDetail(selectedRow)}>Otvori puni detalj</button>
              </div>

              <div className="color-decision-detail-grid">
                <article>
                  <span>PoP trend prometa</span>
                  <strong className={describePopMetric(selectedRow).className} title={describePopMetric(selectedRow).title}>
                    {describePopMetric(selectedRow).label}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period promet</span>
                  <strong>{selectedRow.previousPeriodRevenue != null ? fmtRsd(selectedRow.previousPeriodRevenue) : "N/A"}</strong>
                </article>
                <article>
                  <span>Uticaj nivelacije na promet</span>
                  <strong className={describeNivelacijaImpactMetric(selectedRow).className} title={describeNivelacijaImpactMetric(selectedRow).title}>
                    {describeNivelacijaImpactMetric(selectedRow).label}
                  </strong>
                </article>
                <article>
                  <span>Pre/post pokrice prometa</span>
                  <strong>{fmtPct(resolveColorPercentValue(selectedRow.prePostNivelacijaRevenueCoveragePct), 1)}</strong>
                </article>
                <article>
                  <span>Pre nivelacije promet</span>
                  <strong>{formatCategoryPrePostRevenueMetric(selectedRow.preNivelacijePromet)}</strong>
                </article>
                <article>
                  <span>Posle nivelacije promet</span>
                  <strong>{formatCategoryPrePostRevenueMetric(selectedRow.posleNivelacijePromet)}</strong>
                </article>
                <article>
                  <span>Pre nivo kolicina</span>
                  <strong>{formatCategoryPrePostQuantityMetric(selectedRow.preNivelacijeKolicina)}</strong>
                </article>
                <article>
                  <span>Posle nivo kolicina</span>
                  <strong>{formatCategoryPrePostQuantityMetric(selectedRow.posleNivelacijeKolicina)}</strong>
                </article>
                <article>
                  <span>Artikli sa nivelacijom</span>
                  <strong>{selectedRow.brojArtikalaSaNivelacijom} / {selectedRow.brojArtikalaUkupno}</strong>
                </article>
                <article>
                  <span>Pouzdanost podataka</span>
                  <strong>{selectedRow.reliabilityAvailable ? fmtPct(selectedRow.reliabilityPct, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
                <article>
                  <span>Pokrice marze</span>
                  <strong>{fmtPct(resolveColorPercentValue(selectedRow.marginDataCoveragePct), 1)}</strong>
                </article>
                <article>
                  <span>Marza %</span>
                  <strong>{fmtSignedPct(selectedRow.marginPct, 2)}</strong>
                </article>
                <article>
                  <span>Ocena odluke</span>
                  <strong>{selectedRow.decisionScore == null ? "N/A" : fmtNumber(selectedRow.decisionScore, 0)}</strong>
                </article>
              </div>

              <p className="color-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedRow.statusReason}
              </p>
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}
