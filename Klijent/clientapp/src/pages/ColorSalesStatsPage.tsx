import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
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
import { CHART_TOOLTIP_STYLE, CHART_TOOLTIP_LABEL_STYLE } from "../utils/chartTooltipStyle";
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
type DecisionStatus = "Pojacaj" | "Zadrzi" | "Smanji" | "NedovoljnoPodataka";

type ActiveFilters = {
  fromDate: string;
  toDate: string;
  sezonaId: number | null;
  storeId: number | null;
};

type DecisionColor = ColorSalesStat & {
  sharePct: number;
  marginContribution: number;
  reliabilityPct?: number;
  coveragePct: number;
  splitCoveragePct: number;
  decisionScore: number | null;
  status: DecisionStatus;
  statusReason: string;
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  Pojacaj: 3,
  Zadrzi: 2,
  Smanji: 1,
  NedovoljnoPodataka: 0,
};

const decisionColumns: AnalyticsTableColumn<DecisionColor>[] = [
  { key: "boja", header: "Boja", dataType: "text" },
  { key: "ukupanPromet", header: "Promet", dataType: "currency" },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  { key: "marginContribution", header: "Maržni doprinos", dataType: "currency" },
  { key: "popRevenueChangePct", header: "PoP trend %", dataType: "percent" },
  { key: "prePostNivelacijaRevenueImpactPct", header: "Nivelacija impact %", dataType: "percent" },
  { key: "status", header: "Preporuka", dataType: "text", getValue: (row) => displayStatusLabel(row.status) },
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

function statusClass(status: DecisionStatus): string {
  if (status === "Pojacaj") return "color-decision-status status-boost";
  if (status === "Smanji") return "color-decision-status status-reduce";
  if (status === "NedovoljnoPodataka") return "color-decision-status status-na";
  return "color-decision-status status-keep";
}

export function displayStatusLabel(status: DecisionStatus): string {
  if (status === "Pojacaj") return "Pojačaj";
  if (status === "Zadrzi") return "Zadrži";
  if (status === "Smanji") return "Smanji";
  if (status === "NedovoljnoPodataka") return "Nedovoljno podataka";
  return status;
}

/** Maps backend recommendation status. Never promotes insufficient_data to Zadrži. */
export function mapRecommendationStatus(status?: string | null): DecisionStatus | null {
  if (!status) return null;
  if (status === "increase_focus") return "Pojacaj";
  if (status === "maintain") return "Zadrzi";
  if (status === "review" || status === "do_not_trust") return "Smanji";
  if (status === "insufficient_data") return "NedovoljnoPodataka";
  return null;
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
  reliabilityPct?: number;
};

const MISSING_BACKEND_RECOMMENDATION_REASON =
  "Backend preporuka nije dostupna; lokalna heuristika se ne koristi kao odluka.";

function buildStatusTooltip(data: StatusTooltipData): string {
  const popText = data.popRevenueChangePct != null
    ? fmtSignedPct(data.popRevenueChangePct, 1)
    : data.previousPeriodRevenue != null && data.previousPeriodRevenue <= 0
      ? "Novo / bez prethodne baze"
      : "N/A";
  const impactText = data.prePostNivelacijaRevenueImpactPct != null
    ? fmtSignedPct(data.prePostNivelacijaRevenueImpactPct, 1)
    : "N/A";
  return `${displayStatusLabel(data.status)}: ${data.statusReason} | Udeo ${fmtPct(data.sharePct, 1)} | Marža ${fmtPct(data.marginPct, 1)} | PoP ${popText} | Nivelacija impact ${impactText} | Split pokriće ${fmtPct(data.splitCoveragePct, 1)} | Pouzdanost ${fmtPct(data.reliabilityPct, 0)}`;
}

function describePopMetric(item: ColorSalesStat): { label: string; title: string; className: string } {
  if (item.popRevenueChangePct != null && !Number.isNaN(item.popRevenueChangePct)) {
    return {
      label: fmtSignedPct(item.popRevenueChangePct, 2),
      title: `PoP trend poredi ukupan promet sa prethodnim uporedivim periodom. Prethodni period: ${fmtRsd(item.previousPeriodRevenue ?? 0)}.`,
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

function describeNivelacijaImpactMetric(item: ColorSalesStat): { label: string; title: string; className: string } {
  if (item.prePostNivelacijaRevenueImpactPct != null && !Number.isNaN(item.prePostNivelacijaRevenueImpactPct)) {
    return {
      label: fmtSignedPct(item.prePostNivelacijaRevenueImpactPct, 2),
      title: `Pre/post nivelacija impact meri promenu prometa unutar artikala sa poznatim prvim datumom nivelacije. Pokriće: ${fmtPct(item.prePostNivelacijaRevenueCoveragePct, 1)} prometa.`,
      className: trendClass(item.prePostNivelacijaRevenueImpactPct),
    };
  }

  if ((item.prePostNivelacijaRevenueCoveragePct ?? 0) <= 0) {
    return {
      label: "N/A",
      title: "Nema dovoljno artikala sa poznatom istorijom nivelacije za pre/post impact metriku.",
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
  const [data, setData] = useState<ColorSalesStatsResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dataScope, setDataScopeValue] = useState<DataScope>(() => getDataScope());
  const [sortField, setSortField] = useState<SortField>("status");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedColorKey, setExpandedColorKey] = useState<string | null>(null);

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

  const load = useCallback(async (filters: ActiveFilters, scope: DataScope) => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const currentRange = toUtcRange(filters.fromDate, filters.toDate);
      const result = await getColorSalesStats({
        ...currentRange,
        sezonaId: filters.sezonaId,
        storeId: filters.storeId,
        dataScope: scope,
      });

      if (requestId !== requestIdRef.current) return;
      setData(result);
    } catch (reason) {
      if (requestId !== requestIdRef.current) return;
      setData(null);
      setError(reason instanceof Error ? reason.message : "Greska pri ucitavanju podataka po boji.");
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    void load(activeFilters, dataScope);
  }, [activeFilters, dataScope, load]);

  const decisionRows = useMemo<DecisionColor[]>(() => {
    const rows = data?.colors ?? [];
    if (rows.length === 0) return [];

    const totalRevenue = rows.reduce((sum, item) => sum + item.ukupanPromet, 0);

    return rows.map((item) => {
      const sharePct = totalRevenue > 0 ? (item.ukupanPromet / totalRevenue) * 100 : 0;
      const marginContribution = item.marginContribution;
      const splitCoveragePct = item.prePostNivelacijaRevenueCoveragePct ?? 0;
      const coveragePct = item.brojArtikalaUkupno > 0
        ? (item.brojArtikalaSaNivelacijom / item.brojArtikalaUkupno) * 100
        : 0;

      const backendStatus = mapRecommendationStatus(item.recommendation?.status);
      if (backendStatus) {
        return {
          ...item,
          sharePct: item.sharePct ?? sharePct,
          marginContribution,
          reliabilityPct: item.recommendation?.reliabilityPct ?? item.reliabilityPct,
          coveragePct,
          splitCoveragePct,
          decisionScore: item.recommendation?.confidencePct == null
            ? null
            : Math.round(item.recommendation.confidencePct),
          status: backendStatus,
          statusReason: item.recommendation?.summary
            ?? (backendStatus === "NedovoljnoPodataka"
              ? "Nedovoljno podataka za pouzdanu preporuku; ne tretirati kao Zadrži."
              : "Backend recommendation summary nije dostupan."),
        };
      }

      // Missing/unmapped backend recommendation: never invent Pojacaj/Zadrzi/Smanji locally.
      return {
        ...item,
        sharePct: item.sharePct ?? sharePct,
        marginContribution,
        reliabilityPct: item.reliabilityPct,
        coveragePct,
        splitCoveragePct,
        decisionScore: null,
        status: "NedovoljnoPodataka" as const,
        statusReason: MISSING_BACKEND_RECOMMENDATION_REASON,
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
        compare = a.sharePct - b.sharePct;
      } else if (sortField === "marginContribution") {
        compare = a.marginContribution - b.marginContribution;
      } else if (sortField === "popRevenueChangePct") {
        compare = (a.popRevenueChangePct ?? -9999) - (b.popRevenueChangePct ?? -9999);
      } else if (sortField === "prePostNivelacijaRevenueImpactPct") {
        compare = (a.prePostNivelacijaRevenueImpactPct ?? -9999) - (b.prePostNivelacijaRevenueImpactPct ?? -9999);
      } else if (sortField === "status") {
        compare = STATUS_PRIORITY[a.status] - STATUS_PRIORITY[b.status];
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
      name: row.boja,
      sharePct: Number(row.sharePct.toFixed(2)),
    }));

    const remaining = ranked.slice(6).reduce((sum, row) => sum + row.sharePct, 0);
    if (remaining > 0.1) {
      topRows.push({ name: "Ostale", sharePct: Number(remaining.toFixed(2)) });
    }

    return topRows;
  }, [sortedRows]);

  const counts = useMemo(() => {
    const boost = sortedRows.filter((row) => row.status === "Pojacaj").length;
    const keep = sortedRows.filter((row) => row.status === "Zadrzi").length;
    const reduce = sortedRows.filter((row) => row.status === "Smanji").length;
    const insufficient = sortedRows.filter((row) => row.status === "NedovoljnoPodataka").length;
    return { boost, keep, reduce, insufficient };
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
    const knownCostShare = missingCostShare == null ? null : Math.max(0, 100 - missingCostShare);
    const unknownShare = data.dataQuality.unknownColorRevenueSharePct;

    if (splitCoverage != null && splitCoverage < 60) {
      notes.push(`Pre/post nivelacija trenutno pokriva ${fmtPct(splitCoverage, 1)} ukupnog prometa, pa taj signal treba čitati kao delimičan.`);
    }

    if (knownCostShare != null && knownCostShare < 100) {
      notes.push(`Marža i maržni doprinos su zasnovani na ${fmtPct(knownCostShare, 1)} prometa sa poznatom nabavnom cenom.`);
    }

    if (unknownShare != null && unknownShare > 0) {
      notes.push(`Nepoznate boje učestvuju sa ${fmtPct(unknownShare, 1)} ukupnog prometa.`);
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
      { key: "bojaCount", label: "Broj boja", value: data?.totals.brojBoja ?? 0 },
      { key: "marginCoverage", label: "Promet sa nabavnom cenom", value: fmtPct(data?.dataQuality.missingCostRevenueSharePct == null ? null : 100 - data.dataQuality.missingCostRevenueSharePct, 1) },
      { key: "splitCoverage", label: "Pre/post pokriće", value: fmtPct(data?.dataQuality.revenueWithNivelacijaSplitSharePct, 1) },
      { key: "boost", label: "Pojačaj", value: counts.boost },
      { key: "keep", label: "Zadrži", value: counts.keep },
      { key: "reduce", label: "Smanji", value: counts.reduce },
      { key: "insufficient", label: "Nedovoljno podataka", value: counts.insufficient },
    ],
    [
      counts.boost,
      counts.keep,
      counts.reduce,
      counts.insufficient,
      data?.dataQuality.missingCostRevenueSharePct,
      data?.dataQuality.revenueWithNivelacijaSplitSharePct,
      data?.dataScope,
      data?.generatedAt,
      data?.totals.brojBoja,
      dataScope,
    ]
  );

  const headerDataQualityStatus = useMemo(() => {
    if (!data) return null;
    if (sortedRows.length === 0) return "insufficient_data";
    return qualityNotes.length > 0 ? "warning" : "good";
  }, [data, qualityNotes.length, sortedRows.length]);

  const emptyStateVariant = useMemo<"no_data" | "insufficient_data" | "filtered_out" | null>(() => {
    if (!data || loading || sortedRows.length > 0) return null;
    if (qualityNotes.length > 0) return "insufficient_data";
    return "no_data";
  }, [data, loading, qualityNotes.length, sortedRows.length]);

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
        subtitle: "Color decision detail",
        columns: decisionColumns,
        row,
        metadata: toolbarFilters,
      })
    );

    navigate(`/analitika/color-sales-stats/${recordId}?${params.toString()}`, {
      state: { backgroundLocation: location },
    });
  }, [activeFilters.fromDate, activeFilters.sezonaId, activeFilters.storeId, activeFilters.toDate, dataScope, location, navigate, toolbarFilters]);

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

    setError(null);
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
    setSortField((previousField) => {
      if (previousField === field) {
        setSortDir((previousDir) => (previousDir === "asc" ? "desc" : "asc"));
        return previousField;
      }

      setSortDir(field === "boja" ? "asc" : "desc");
      return field;
    });
  };

  return (
    <div className="color-decision-page">
      <AnalyticsTrustHeader
        title="Prodaja po boji artikla"
        description="Decision-support pogled za izbor boja koje treba pojačati u nabavci."
        periodFrom={data?.fromDate ?? activeFilters.fromDate}
        periodTo={data?.toDate ?? activeFilters.toDate}
        lastRefreshAt={data?.generatedAt ?? null}
        dataFreshnessStatus="unknown"
        dataSource={`Color sales stats materialized view (scope: ${data?.dataScope ?? dataScope})`}
        dataQualityStatus={headerDataQualityStatus}
        mode="recommendation"
        recommendationNote="Preporuke dolaze iz backenda; ovaj ekran zadržava odluku, period i kvalitet podataka na jednom mestu."
        emptyStateReason={!loading && !error && emptyStateHint ? emptyStateHint : null}
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
      {error ? (
        <AnalyticsErrorState
          title="Boje trenutno nisu dostupne"
          message={error}
          onRetry={() => {
            void load(activeFilters, dataScope);
          }}
          helpHref="/analytics/data-quality"
        />
      ) : null}
      {loading ? (
        <div className="color-decision-message loading">
          <UltraSpinner size="sm" label="Učitavam boje" className="color-decision-loading-spinner" />
          <span>Učitavam boje...</span>
        </div>
      ) : null}
      {!loading && !error && emptyStateVariant ? (
        <AnalyticsEmptyState
          variant={emptyStateVariant ?? undefined}
          emptyReason={emptyStateHint}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          onRetry={() => {
            void load(activeFilters, dataScope);
          }}
        />
      ) : null}
      {!loading && !error && qualityNotes.length > 0 ? (
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
                <span>Udeo top 5 boja</span>
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
                      <Tooltip contentStyle={CHART_TOOLTIP_STYLE} labelStyle={CHART_TOOLTIP_LABEL_STYLE} formatter={(value: number | string | undefined) => `${fmtPct(Number(value ?? 0), 2)}`} />
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
                    Pojačaj: {counts.boost} | Zadrži: {counts.keep} | Smanji: {counts.reduce} | Nedovoljno podataka: {counts.insufficient}
                  </p>
                  <p className="color-decision-metric-note">
                    PoP trend = promena prometa prema prethodnom uporedivom periodu. Nivelacija impact = pre/post promena unutar prometa sa poznatim prvim datumom nivelacije.
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
                          Nivelacija impact{sortMarker("prePostNivelacijaRevenueImpactPct", sortField, sortDir)} <InfoTip text="Pre/post promena prometa unutar artikala sa poznatim prvim datumom nivelacije. Nije isto što i PoP trend." />
                        </button>
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>
                          Preporuka{sortMarker("status", sortField, sortDir)} <InfoTip text="Sistemska preporuka: Pojačaj / Zadrži / Smanji / Nedovoljno podataka. Kliknite na red za detaljnije objašnjenje." />
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
                              <span
                                className={statusClass(row.status)}
                                title={buildStatusTooltip(row)}
                                aria-label={buildStatusTooltip(row)}
                              >
                                {displayStatusLabel(row.status)}
                              </span>
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
                  <span>Nivelacija impact prometa</span>
                  <strong className={describeNivelacijaImpactMetric(selectedRow).className} title={describeNivelacijaImpactMetric(selectedRow).title}>
                    {describeNivelacijaImpactMetric(selectedRow).label}
                  </strong>
                </article>
                <article>
                  <span>Pre/post pokrice prometa</span>
                  <strong>{fmtPct(selectedRow.prePostNivelacijaRevenueCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Pre nivelacije promet</span>
                  <strong>{fmtRsd(selectedRow.preNivelacijePromet)}</strong>
                </article>
                <article>
                  <span>Posle nivelacije promet</span>
                  <strong>{fmtRsd(selectedRow.posleNivelacijePromet)}</strong>
                </article>
                <article>
                  <span>Pre nivo kolicina</span>
                  <strong>{fmtQty(selectedRow.preNivelacijeKolicina)}</strong>
                </article>
                <article>
                  <span>Posle nivo kolicina</span>
                  <strong>{fmtQty(selectedRow.posleNivelacijeKolicina)}</strong>
                </article>
                <article>
                  <span>Artikli sa nivelacijom</span>
                  <strong>{selectedRow.brojArtikalaSaNivelacijom} / {selectedRow.brojArtikalaUkupno}</strong>
                </article>
                <article>
                  <span>Pouzdanost podataka</span>
                  <strong>{fmtPct(selectedRow.reliabilityPct, 1)}</strong>
                </article>
                <article>
                  <span>Pokrice marze</span>
                  <strong>{fmtPct(selectedRow.marginDataCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Marza %</span>
                  <strong>{fmtSignedPct(selectedRow.marginPct, 2)}</strong>
                </article>
                <article>
                  <span>Decision score</span>
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
