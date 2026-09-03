import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  Bar,
  BarChart,
  Cell,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import AnalyticsControlBar, {
  type AnalyticsControlBarChip,
  type AnalyticsControlBarField,
} from "../components/analytics/AnalyticsControlBar";
import AnalyticsDataTable from "../components/analytics/AnalyticsDataTable";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import AnalyticsTrustHeader from "../components/analytics/AnalyticsTrustHeader";
import AnalyticsErrorState from "../components/analytics/AnalyticsErrorState";
import AnalyticsEmptyState from "../components/analytics/AnalyticsEmptyState";
import InfoTip from "../components/ui/InfoTip";
import { getDobavljaci } from "../services/dobavljaciApi";
import { getStores } from "../services/analyticsApi";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import {
  getVendorSalesNivelacija,
  type VendorSalesNivelacijaRecommendation,
  type VendorSalesNivelacijaResponse,
  type VendorSalesNivelacijaVendorStat,
} from "../services/vendorSalesNivelacijaApi";
import type { Dobavljac } from "../types/Dobavljaci";
import type { StoreOption } from "../types/analytics";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { CHART_TOOLTIP_LABEL_STYLE, CHART_TOOLTIP_STYLE } from "../utils/chartTooltipStyle";
import { fmtNumber, fmtPct, fmtQty, fmtRsd, fmtSignedPct, getPresetRange } from "../utils/analyticsFormatters";
import { analyticsMetricDescriptions } from "../utils/analyticsMetricDescriptions";
import {
  getAnalyticsMetaMessage,
  isAnalyticsMetaInsufficient,
  isAnalyticsMetaWarning,
  shouldShowAnalyticsEmptyState,
} from "../utils/analyticsResponseMeta";
import {
  RECOMMENDATION_CONFIDENCE_LABEL,
  RECOMMENDATION_RELIABILITY_LABEL,
  RECOMMENDATION_SIGNAL_UNAVAILABLE,
  RECOMMENDATION_STATUS_PRIORITY,
  normalizeRecommendationPct,
  normalizeRecommendationQualityStatus,
  recommendationQualityLabel,
  recommendationQualityStyle,
  recommendationReasonHints,
  recommendationStatusLabel,
  recommendationStatusTone,
  recommendationStatusTooltipBrief,
  type RecommendationQualityStatus,
} from "../utils/canonicalRecommendationSemantics";
import { getDataScope, type DataScope } from "../utils/dataScope";
import "./ProdajaPrePostNivelacijePage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
type SortDir = "asc" | "desc";
type SortField = "vendorName" | "postRevenue" | "sharePct" | "changeRevenue" | "trendPct" | "volatilityPct" | "status";
type DecisionStatus = VendorSalesNivelacijaRecommendation["status"];
type FocusFilter = "all" | "increaseFocus" | "maintain" | "review" | "doNotTrust" | "insufficientData" | "lowConfidence" | "volatile";
type ConfidenceTone = "strong" | "watch" | "weak";
type VolatilityTone = "positive" | "negative" | "warning" | "neutral";

type ActiveFilters = {
  fromDate: string;
  toDate: string;
  vendorId: number | null;
  category: string;
  storeId: number | null;
};

type DecisionVendor = VendorSalesNivelacijaVendorStat & {
  absoluteChangeSharePct: number;
  sharePct: number;
  sharePctAvailable: boolean;
  postSharePct: number;
  trendPct: number;
  reliabilityPct: number;
  reliabilityAvailable: boolean;
  avgCoveragePost30Available: boolean;
  confidencePct: number;
  confidenceAvailable: boolean;
  status: DecisionStatus;
  statusReason: string;
  dataQualityStatus: RecommendationQualityStatus;
  reasonCodes: string[];
  confidenceLabel: string;
  confidenceTone: ConfidenceTone;
  previousPostRevenue: number | null;
  volatilityPct: number | null;
  volatilityLabel: string;
  volatilityTone: VolatilityTone;
};

type ConcentrationDatum = {
  name: string;
  sharePct: number;
  changeRevenue: number;
  articleCount: number;
  vendorKey: string | null;
  selected: boolean;
};

type DetailDriverSummary = {
  dominantCategory: string;
  dominantCategoryRevenue: number;
  topWinnerLabel: string;
  topWinnerRevenue: number;
  topRiskLabel: string;
  topRiskRevenue: number;
  avgMomentumRevenue: number | null;
  avgElasticity: number | null;
  avgDidRevenue: number | null;
  avgLostSalesOOS: number | null;
  topMetricReasons: string[];
};

const STATUS_PRIORITY: Record<DecisionStatus, number> = {
  ...RECOMMENDATION_STATUS_PRIORITY,
};
const MEDIUM_SIGNAL_RELIABILITY_PCT = 40;
const VENDOR_NIVELACIJA_MAX_ROWS = 50_000;
const CHART_GRID_STROKE = "var(--dashboard-grid, var(--border-default))";
const CHART_AXIS_TICK = { fill: "var(--dashboard-chart-axis, var(--text-secondary))", fontSize: 12, fontWeight: 600 };
const CHART_CURSOR_STYLE = { fill: "var(--dashboard-chart-hover, var(--accent-soft))" };
const COMMAND_TOOLTIP_STYLE = {
  ...CHART_TOOLTIP_STYLE,
  background: "var(--dashboard-tooltip-bg, var(--surface-elevated))",
  border: "1px solid var(--dashboard-tooltip-border, var(--border-default))",
  color: "var(--dashboard-tooltip-label, var(--text-primary))",
  boxShadow: "var(--dashboard-tooltip-shadow, var(--shadow-md, none))",
};
const COMMAND_TOOLTIP_LABEL_STYLE = {
  ...CHART_TOOLTIP_LABEL_STYLE,
  color: "var(--dashboard-tooltip-label, var(--text-primary))",
};

const decisionColumns: AnalyticsTableColumn<DecisionVendor>[] = [
  { key: "vendorName", header: "Dobavljač", dataType: "text" },
  { key: "preRevenue", header: "Promet pre", dataType: "currency" },
  { key: "postRevenue", header: "Promet posle", dataType: "currency" },
  {
    key: "absoluteChangeSharePct",
    header: "Udeo u apsolutnoj promeni %",
    detailLabel: "Udeo u apsolutnoj promeni %",
    dataType: "percent",
    getValue: (row) => row.sharePctAvailable ? row.absoluteChangeSharePct : null,
  },
  { key: "changeRevenue", header: "Promena prometa", dataType: "currency" },
  { key: "trendPct", header: "Trend %", dataType: "percent" },
  { key: "reliabilityPct", header: RECOMMENDATION_RELIABILITY_LABEL, dataType: "percent" },
  { key: "confidencePct", header: RECOMMENDATION_CONFIDENCE_LABEL, dataType: "percent" },
  { key: "volatilityLabel", header: "Volatilnost", dataType: "text" },
  { key: "status", header: "Preporuka", dataType: "text" },
  { key: "articleCount", header: "Artikala", dataType: "number" },
  { key: "activeArticlesCount", header: "Aktivnih artikala", dataType: "number" },
  { key: "statusReason", header: "Razlog preporuke", dataType: "text" },
];

interface CustomConcentrationTooltipProps {
  active?: boolean;
  payload?: Array<{ payload: ConcentrationDatum }>;
}

function CustomConcentrationTooltip({ active, payload }: CustomConcentrationTooltipProps) {
  if (!active || !payload || payload.length === 0) return null;
  const data = payload[0].payload as ConcentrationDatum;
  return (
    <div className="ppn-chart-tooltip">
      <p className="ppn-chart-tooltip-title">{data.name}</p>
      <p className="ppn-chart-tooltip-value">{fmtPct(data.sharePct, 2)}</p>
      {data.vendorKey ? (
        <p className="ppn-chart-tooltip-subtitle">
          Promena: {fmtRsd(data.changeRevenue)} | artikala: {data.articleCount}
        </p>
      ) : null}
    </div>
  );
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function toUtcRange(fromDate: string, toDate: string): { from: string; to: string } {
  return {
    from: `${fromDate}T00:00:00Z`,
    to: `${toDate}T23:59:59Z`,
  };
}

function buildPreviousRange(fromDate: string, toDate: string): { from: string; to: string } {
  const currentFrom = new Date(`${fromDate}T00:00:00Z`);
  const currentTo = new Date(`${toDate}T23:59:59Z`);
  const durationMs = currentTo.getTime() - currentFrom.getTime() + 1000;

  const previousTo = new Date(currentFrom.getTime() - 1000);
  const previousFrom = new Date(previousTo.getTime() - durationMs + 1000);

  return {
    from: previousFrom.toISOString(),
    to: previousTo.toISOString(),
  };
}

function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string {
  if (field !== activeField) return "";
  return dir === "asc" ? " ^" : " v";
}

function statusClass(status: DecisionStatus): string {
  const tone = recommendationStatusTone(status);
  if (tone === "boost") return "ppn-decision-status status-boost";
  if (tone === "keep") return "ppn-decision-status status-keep";
  if (tone === "review") return "ppn-decision-status status-review";
  if (tone === "reduce") return "ppn-decision-status status-reduce";
  return "ppn-decision-status status-na";
}

function statusDisplayLabel(status: DecisionStatus): string {
  return recommendationStatusLabel(status);
}

function trendClass(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "trend-neutral";
  if (value > 0) return "trend-up";
  if (value < 0) return "trend-down";
  return "trend-neutral";
}

function confidenceClass(tone: ConfidenceTone): string {
  if (tone === "strong") return "ppn-signal-pill signal-strong";
  if (tone === "weak") return "ppn-signal-pill signal-weak";
  return "ppn-signal-pill signal-watch";
}

function volatilityClass(tone: VolatilityTone): string {
  if (tone === "positive") return "ppn-signal-pill signal-positive";
  if (tone === "negative") return "ppn-signal-pill signal-negative";
  if (tone === "warning") return "ppn-signal-pill signal-watch";
  return "ppn-signal-pill signal-neutral";
}

function insightToneClass(tone: string): string {
  const normalized = tone.trim().toLowerCase();
  if (normalized === "positive") return "ppn-insight positive";
  if (normalized === "negative") return "ppn-insight negative";
  if (normalized === "warning") return "ppn-insight warning";
  return "ppn-insight neutral";
}

function focusFilterLabel(filter: FocusFilter): string {
  if (filter === "increaseFocus") return recommendationStatusLabel("increase_focus");
  if (filter === "maintain") return recommendationStatusLabel("maintain");
  if (filter === "review") return recommendationStatusLabel("review");
  if (filter === "doNotTrust") return recommendationStatusLabel("do_not_trust");
  if (filter === "insufficientData") return "Nedovoljno podataka";
  if (filter === "lowConfidence") return "Nisko poverenje";
  if (filter === "volatile") return "Visoka volatilnost";
  return "Sve";
}

function parseMetricsStatus(value: string | null | undefined): string[] {
  if (!value) return [];
  return value
    .split(";")
    .map((item) => item.trim())
    .filter(Boolean);
}

type MetricWarningMeta = {
  label: string;
  severity: "info" | "watch";
  explanation: string;
  isExpected: boolean;
};

const METRIC_WARNING_META: Record<string, MetricWarningMeta> = {
  "Rolling pre/post unavailable (no eventDate filter)": {
    label: "Rolling analiza preskočena",
    severity: "info",
    explanation:
      "Rolling 7-dnevni pre/post zahteva tačan datum nivelacije. U mode-u pregleda po periodu, ova metrika se ne računa, što je očekivano ponašanje.",
    isExpected: true,
  },
  "No rolling data (view missing)": {
    label: "Rolling 7d view nedostupan",
    severity: "info",
    explanation: "vw_sales_rolling_7d nije aktivan na ovoj bazi. Ostali podaci su ispravni.",
    isExpected: true,
  },
  "No momentum data (view missing)": {
    label: "Momentum signal nedostupan",
    severity: "info",
    explanation:
      "vw_sales_momentum view nije kreiran. Momentum signal nije uključen u ocenu, ali je osnovna analiza ispravna.",
    isExpected: true,
  },
  "No OOS data (view missing)": {
    label: "OOS metrika nedostupna",
    severity: "info",
    explanation:
      "vw_stock_red_zone view nije kreiran. Procena prodajnog gubitka zbog iscrpljenosti zalihe nije dostupna.",
    isExpected: true,
  },
  "No DiD data (view missing)": {
    label: "DiD metrika nedostupna",
    severity: "info",
    explanation:
      "vw_nivelacija_did view nije kreiran. Difference-in-Differences procena nije uključena.",
    isExpected: true,
  },
  "Article stats capped": {
    label: "Podaci ograničeni (cap)",
    severity: "watch",
    explanation:
      "Broj article redova je ograničen zbog veličine upita. Neke stavke možda nisu vidljive, pa suzite filter.",
    isExpected: false,
  },
  "OOS/DiD mapping failed": {
    label: "OOS/DiD mapiranje neuspešno",
    severity: "watch",
    explanation:
      "Neočekivana greška pri učitavanju OOS ili DiD podataka. Osnovna analiza je ispravna, ali proverite konfiguraciju baze.",
    isExpected: false,
  },
  "Metrics mapping failed": {
    label: "Mapiranje metrika neuspešno",
    severity: "watch",
    explanation:
      "Greška pri obradi naprednih metrika. Osnovna analiza prometa pre/posle i promene cene ostaje ispravna.",
    isExpected: false,
  },
};

function getMetricWarningMeta(rawKey: string): MetricWarningMeta {
  if (METRIC_WARNING_META[rawKey]) return METRIC_WARNING_META[rawKey];
  const prefixMatch = Object.entries(METRIC_WARNING_META).find(([key]) => rawKey.startsWith(key));
  if (prefixMatch) return prefixMatch[1];
  return {
    label: rawKey,
    severity: "watch",
    explanation: "Neočekivano upozorenje pri obradi podataka.",
    isExpected: false,
  };
}

function averageNullable(values: Array<number | null | undefined>): number | null {
  const numbers = values.filter((value): value is number => value != null && !Number.isNaN(value));
  if (numbers.length === 0) return null;
  return numbers.reduce((sum, value) => sum + value, 0) / numbers.length;
}

function buildConfidenceMeta(
  reliabilityPct: number | null,
  reliabilityAvailable: boolean,
): { label: string; tone: ConfidenceTone } {
  if (!reliabilityAvailable || reliabilityPct == null) {
    return { label: "Nije dostupno", tone: "watch" };
  }

  const tone: ConfidenceTone =
    reliabilityPct >= 70
      ? "strong"
      : reliabilityPct >= MEDIUM_SIGNAL_RELIABILITY_PCT
        ? "watch"
        : "weak";
  return { label: fmtPct(reliabilityPct, 0), tone };
}

function buildVolatilityMeta(currentRevenue: number, previousRevenue: number | null): {
  pct: number | null;
  label: string;
  tone: VolatilityTone;
} {
  if (previousRevenue == null) {
    return { pct: null, label: "Bez baze", tone: "neutral" };
  }

  if (previousRevenue <= 0) {
    if (currentRevenue > 0) {
      return { pct: 100, label: "Novo", tone: "positive" };
    }
    return { pct: 0, label: "Bez baze", tone: "neutral" };
  }

  const pct = ((currentRevenue - previousRevenue) / previousRevenue) * 100;
  const magnitude = Math.abs(pct);

  if (magnitude >= 45) {
    return { pct, label: "Visoka", tone: pct >= 0 ? "positive" : "negative" };
  }
  if (magnitude >= 20) {
    return { pct, label: "Promenljivo", tone: "warning" };
  }
  return { pct, label: "Stabilno", tone: "neutral" };
}

function focusFilterMatches(row: DecisionVendor, filter: FocusFilter): boolean {
  if (filter === "increaseFocus") return row.status === "increase_focus";
  if (filter === "maintain") return row.status === "maintain";
  if (filter === "review") return row.status === "review";
  if (filter === "doNotTrust") return row.status === "do_not_trust";
  if (filter === "insufficientData") return row.status === "insufficient_data";
  if (filter === "lowConfidence") return row.confidenceTone === "weak";
  if (filter === "volatile") return row.volatilityLabel === "Visoka" || row.volatilityLabel === "Promenljivo" || row.volatilityLabel === "Novo";
  return true;
}

type StatusTooltipData = {
  status: DecisionStatus;
  statusReason: string;
  sharePct: number;
  sharePctAvailable: boolean;
  trendPct: number;
  changeRevenue: number;
  reliabilityPct: number;
  confidencePct: number;
  reliabilityAvailable: boolean;
  confidenceAvailable: boolean;
  dataQualityStatus: RecommendationQualityStatus;
  reasonCodes: string[];
};

function buildStatusTooltip(data: StatusTooltipData): string {
  const reliabilityText = data.reliabilityAvailable ? fmtPct(data.reliabilityPct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const confidenceText = data.confidenceAvailable ? fmtPct(data.confidencePct, 0) : RECOMMENDATION_SIGNAL_UNAVAILABLE;
  const qualityText = recommendationQualityLabel(data.dataQualityStatus);
  const hintText = recommendationReasonHints(data.reasonCodes).join(" | ");
  const shareText = data.sharePctAvailable ? fmtPct(data.sharePct, 1) : "Nije dostupno";
  return `${statusDisplayLabel(data.status)}: ${data.statusReason} | ${recommendationStatusTooltipBrief(data.status)} | Udeo ${shareText} | Trend ${fmtSignedPct(data.trendPct, 1)} | Delta ${fmtRsd(data.changeRevenue)} | ${RECOMMENDATION_RELIABILITY_LABEL} ${reliabilityText} | ${RECOMMENDATION_CONFIDENCE_LABEL} ${confidenceText} | Kvalitet ${qualityText}${hintText ? ` | Napomene: ${hintText}` : ""}`;
}

function normalizeName(value: string | null | undefined): string {
  return (value ?? "").trim().toUpperCase();
}

function vendorKey(vendor: { vendorId: number | null; vendorName: string }): string {
  if (vendor.vendorId != null) return `id:${vendor.vendorId}`;
  return `name:${normalizeName(vendor.vendorName)}`;
}

function buildStoreLabel(store: StoreOption): string {
  const extras = [store.city, store.region].filter(Boolean).join(", ");
  return extras ? `${store.storeName} (${extras})` : store.storeName;
}

export default function ProdajaPrePostNivelacijePage() {
  const navigate = useNavigate();
  const location = useLocation();
  const requestIdRef = useRef(0);

  const initialRange = useMemo(() => getPresetRange("30d"), []);
  const [periodPreset, setPeriodPreset] = useState<PeriodPreset>("30d");
  const [fromDate, setFromDate] = useState(initialRange.fromDate);
  const [toDate, setToDate] = useState(initialRange.toDate);
  const [vendorId, setVendorId] = useState<number | null>(null);
  const [category, setCategory] = useState("");
  const [storeId, setStoreId] = useState<number | null>(null);
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({
    fromDate: initialRange.fromDate,
    toDate: initialRange.toDate,
    vendorId: null,
    category: "",
    storeId: null,
  });

  const [vendors, setVendors] = useState<Dobavljac[]>([]);
  const [stores, setStores] = useState<StoreOption[]>([]);
  const [data, setData] = useState<VendorSalesNivelacijaResponse | null>(null);
  const [previousData, setPreviousData] = useState<VendorSalesNivelacijaResponse | null>(null);
  const [previousRevenue, setPreviousRevenue] = useState<number | null>(null);
  const [previousComparisonError, setPreviousComparisonError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dataScope, setDataScopeValue] = useState<DataScope>(() => getDataScope());
  const [sortField, setSortField] = useState<SortField>("status");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedVendorKey, setExpandedVendorKey] = useState<string | null>(null);
  const [trustPanelOpen, setTrustPanelOpen] = useState(false);
  const [focusFilter, setFocusFilter] = useState<FocusFilter>("all");

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
    const loadVendors = async () => {
      try {
        setVendors(await getDobavljaci());
      } catch {
        setVendors([]);
      }
    };
    void loadVendors();
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
    setPreviousComparisonError(null);

    try {
      const currentRange = toUtcRange(filters.fromDate, filters.toDate);
      const previousRange = buildPreviousRange(filters.fromDate, filters.toDate);

      const [currentResult, previousResult] = await Promise.allSettled([
        getVendorSalesNivelacija({
          ...currentRange,
          vendorId: filters.vendorId,
          category: filters.category || null,
          includeInactive: false,
          maxRows: VENDOR_NIVELACIJA_MAX_ROWS,
          storeId: filters.storeId,
          dataScope: scope,
        }),
        getVendorSalesNivelacija({
          ...previousRange,
          vendorId: filters.vendorId,
          category: filters.category || null,
          includeInactive: false,
          maxRows: VENDOR_NIVELACIJA_MAX_ROWS,
          storeId: filters.storeId,
          dataScope: scope,
        }),
      ]);

      if (requestId !== requestIdRef.current) return;

      if (currentResult.status === "rejected") {
        throw currentResult.reason;
      }

      setData(currentResult.value);
      setExpandedVendorKey(null);
      setFocusFilter("all");

      if (previousResult.status === "fulfilled") {
        setPreviousData(previousResult.value);
        setPreviousRevenue(previousResult.value.totals.postRevenue);
        setPreviousComparisonError(null);
      } else {
        setPreviousData(null);
        setPreviousRevenue(null);
        const reason = previousResult.reason;
        setPreviousComparisonError(
          reason instanceof Error
            ? reason.message
            : "Zahtev za prethodni uporedivi period nije uspeo."
        );
      }
    } catch (reason) {
      if (requestId !== requestIdRef.current) return;
      setData(null);
      setPreviousData(null);
      setPreviousRevenue(null);
      setPreviousComparisonError(null);
      setError(reason instanceof Error ? reason.message : "Greška pri ucitavanju pre/post analitike.");
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    void load(activeFilters, dataScope);
  }, [activeFilters, dataScope, load]);

  const previousRevenueByVendorKey = useMemo(() => {
    const map = new Map<string, number>();
    for (const row of previousData?.vendorStats ?? []) {
      map.set(vendorKey(row), row.postRevenue);
    }
    return map;
  }, [previousData?.vendorStats]);

  const decisionRows = useMemo<DecisionVendor[]>(() => {
    const rows = data?.vendorStats ?? [];
    if (rows.length === 0) return [];

    const totalRevenue = rows.reduce((sum, item) => sum + item.postRevenue, 0);
    const totalAbsoluteChangeRevenue =
      data?.totals.absoluteChangeRevenue ??
      rows.reduce((sum, item) => sum + Math.abs(item.changeRevenue), 0);


    return rows.map((item) => {
      const backendRecommendation = item.recommendation;
      const status = backendRecommendation?.status ?? "insufficient_data";
      const statusReason = backendRecommendation?.summary
        ?? "Backend recommendation payload nije dostupan za ovaj red; frontend ne računa zamenski poslovni status.";
      const confidencePctValue = normalizeRecommendationPct(backendRecommendation?.confidencePct);
      const recommendationReliabilityPct = normalizeRecommendationPct(backendRecommendation?.reliabilityPct ?? item.reliabilityPct);

      const absoluteChangeSharePct = item.changeSharePercent ?? (
        totalAbsoluteChangeRevenue > 0 ? (Math.abs(item.changeRevenue) / totalAbsoluteChangeRevenue) * 100 : 0
      );
      const sharePctAvailable = item.changeSharePercent != null || totalAbsoluteChangeRevenue > 0;
      const sharePct = absoluteChangeSharePct;
      const postSharePct = item.postRevenueSharePercent ?? (
        totalRevenue > 0 ? (item.postRevenue / totalRevenue) * 100 : 0
      );
      const trendPct = item.changePercent;
      const avgCoveragePost30 = item.avgCoveragePost30 != null ? item.avgCoveragePost30 * 100 : 0;
      const normalizedReliabilityPct = recommendationReliabilityPct ?? 0;
      const previousPostRevenue = previousRevenueByVendorKey.get(vendorKey(item)) ?? null;
      const confidence = buildConfidenceMeta(recommendationReliabilityPct, recommendationReliabilityPct != null);
      const volatility = previousComparisonError
        ? { pct: null, label: "Nedostupno", tone: "neutral" as const }
        : buildVolatilityMeta(item.postRevenue, previousPostRevenue);

      return {
        ...item,
        absoluteChangeSharePct,
        sharePct,
        sharePctAvailable,
        postSharePct,
        trendPct,
        reliabilityPct: normalizedReliabilityPct,
        reliabilityAvailable: recommendationReliabilityPct != null,
        avgCoveragePost30,
        avgCoveragePost30Available: item.avgCoveragePost30 != null,
        confidencePct: confidencePctValue ?? 0,
        confidenceAvailable: confidencePctValue != null,
        status,
        statusReason,
        dataQualityStatus: normalizeRecommendationQualityStatus(backendRecommendation?.dataQualityStatus),
        reasonCodes: backendRecommendation?.reasonCodes ?? [],
        confidenceLabel: confidence.label,
        confidenceTone: confidence.tone,
        previousPostRevenue,
        volatilityPct: volatility.pct,
        volatilityLabel: volatility.label,
        volatilityTone: volatility.tone,
      };
    });
  }, [data?.totals.absoluteChangeRevenue, data?.vendorStats, previousComparisonError, previousRevenueByVendorKey]);

  const sortedRows = useMemo(() => {
    const rows = [...decisionRows];
    return rows.sort((a, b) => {
      let compare = 0;

      if (sortField === "vendorName") {
        compare = a.vendorName.localeCompare(b.vendorName, "sr");
      } else if (sortField === "postRevenue") {
        compare = a.postRevenue - b.postRevenue;
      } else if (sortField === "sharePct") {
        compare = a.sharePct - b.sharePct;
      } else if (sortField === "changeRevenue") {
        compare = a.changeRevenue - b.changeRevenue;
      } else if (sortField === "trendPct") {
        compare = a.trendPct - b.trendPct;
      } else if (sortField === "volatilityPct") {
        compare = (a.volatilityPct ?? Number.NEGATIVE_INFINITY) - (b.volatilityPct ?? Number.NEGATIVE_INFINITY);
      } else if (sortField === "status") {
        compare = STATUS_PRIORITY[a.status] - STATUS_PRIORITY[b.status];
      }

      if (compare === 0) {
        compare = a.confidencePct - b.confidencePct;
      }

      if (compare === 0) {
        compare = a.reliabilityPct - b.reliabilityPct;
      }

      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const focusedRows = useMemo(() => {
    return sortedRows.filter((row) => focusFilterMatches(row, focusFilter));
  }, [focusFilter, sortedRows]);

  const totalRevenue = data?.totals.postRevenue ?? 0;
  const totalAbsoluteChangeRevenue = data?.totals.absoluteChangeRevenue
    ?? sortedRows.reduce((sum, item) => sum + Math.abs(item.changeRevenue), 0);
  const top5SharePct = useMemo<number | null>(() => {
    if (sortedRows.length === 0 || totalAbsoluteChangeRevenue <= 0) return null;
    const top5 = [...sortedRows]
      .sort((a, b) => b.sharePct - a.sharePct)
      .slice(0, 5)
      .reduce((sum, item) => sum + item.sharePct, 0);
    return top5;
  }, [sortedRows, totalAbsoluteChangeRevenue]);

  const totalChangeRevenue = data?.totals.changeRevenue ?? 0;
  const periodGrowthPct = useMemo(() => {
    if (previousRevenue == null || previousRevenue <= 0) return null;
    return ((totalRevenue - previousRevenue) / previousRevenue) * 100;
  }, [previousRevenue, totalRevenue]);

  const periodGrowthDisplay = useMemo(() => {
    if (previousComparisonError) return "Nedostupno";
    if (previousRevenue == null) return "N/A";
    if (previousRevenue <= 0) return totalRevenue > 0 ? "Nova baza" : "Bez baze";
    return fmtSignedPct(periodGrowthPct);
  }, [periodGrowthPct, previousComparisonError, previousRevenue, totalRevenue]);

  const vendorCounts = useMemo(() => {
    const increaseFocus = sortedRows.filter((row) => row.status === "increase_focus").length;
    const maintain = sortedRows.filter((row) => row.status === "maintain").length;
    const review = sortedRows.filter((row) => row.status === "review").length;
    const doNotTrust = sortedRows.filter((row) => row.status === "do_not_trust").length;
    const insufficientData = sortedRows.filter((row) => row.status === "insufficient_data").length;
    return { increaseFocus, maintain, review, doNotTrust, insufficientData };
  }, [sortedRows]);

  const focusFilterCounts = useMemo(() => {
    return {
      all: sortedRows.length,
      increaseFocus: sortedRows.filter((row) => row.status === "increase_focus").length,
      maintain: sortedRows.filter((row) => row.status === "maintain").length,
      review: sortedRows.filter((row) => row.status === "review").length,
      doNotTrust: sortedRows.filter((row) => row.status === "do_not_trust").length,
      insufficientData: sortedRows.filter((row) => row.status === "insufficient_data").length,
      lowConfidence: sortedRows.filter((row) => row.confidenceTone === "weak").length,
      volatile: sortedRows.filter((row) => focusFilterMatches(row, "volatile")).length,
    } satisfies Record<FocusFilter, number>;
  }, [sortedRows]);

  const dataQualityWarnings = useMemo(() => parseMetricsStatus(data?.metricsStatus), [data?.metricsStatus]);
  const dataMeta = data?.meta ?? null;
  const dataMetaMessage = getAnalyticsMetaMessage(dataMeta);
  const showMetaWarning = !loading && !error && isAnalyticsMetaWarning(dataMeta);
  const showEmptyState = !loading && !error && Boolean(data) && decisionRows.length === 0;
  const showInsufficientEmptyState = shouldShowAnalyticsEmptyState(dataMeta, decisionRows.length) && isAnalyticsMetaInsufficient(dataMeta);
  const emptyStateVariant: "no_data" | "insufficient_data" | "filtered_out" =
    showInsufficientEmptyState
      ? "insufficient_data"
      : focusFilter !== "all"
        ? "filtered_out"
        : "no_data";

  const dataTrustSummary = useMemo(() => {
    const analyzedShare = data?.dataQuality.analyzedSharePercent ?? 0;
    const duplicateRows = data?.dataQuality.duplicateRowsRemoved ?? 0;
    const inactiveRows = data?.dataQuality.inactiveRows ?? 0;
    const analyzedRows = data?.dataQuality.analyzedRows ?? 0;
    const deduplicatedRows = data?.dataQuality.deduplicatedRows ?? 0;
    const unchangedPriceRows = data?.dataQuality.unchangedPriceRows ?? 0;

    const countDetail = deduplicatedRows > 0
      ? `${analyzedRows} od ${deduplicatedRows} nivelacija redova (${fmtPct(analyzedShare, 0)})`
      : `${fmtPct(analyzedShare, 0)} redova`;

    const details = `Analizirano: ${countDetail} | bez prodajnog prozora: ${inactiveRows} | nepromenjene cene: ${unchangedPriceRows} | duplikati uklonjeni: ${duplicateRows}`;

    const hasUnexpectedWarnings = dataQualityWarnings.some((warning) => !getMetricWarningMeta(warning).isExpected);

    if (analyzedShare >= 70 && !hasUnexpectedWarnings) {
      return {
        label: "Visoko poverenje",
        tone: "strong" as const,
        details,
      };
    }

    if (analyzedShare >= 45) {
      return {
        label: "Srednje poverenje",
        tone: "watch" as const,
        details,
      };
    };

    return {
      label: "Nisko poverenje",
      tone: "weak" as const,
      details,
    };
  }, [
    data?.dataQuality.analyzedSharePercent,
    data?.dataQuality.duplicateRowsRemoved,
    data?.dataQuality.inactiveRows,
    data?.dataQuality.analyzedRows,
    data?.dataQuality.deduplicatedRows,
    data?.dataQuality.unchangedPriceRows,
    dataQualityWarnings,
  ]);

  const concentrationQuality = useMemo(() => {
    const analyzedRows = data?.dataQuality.analyzedRows ?? 0;
    const vendorRows = decisionRows.length;
    const nonZeroChangeVendors = decisionRows.filter((row) => Math.abs(row.changeRevenue) > 0.0001).length;
    const avgPostCoveragePct = data?.dataQuality.avgCoveragePost30 != null
      ? data.dataQuality.avgCoveragePost30 * 100
      : null;

    if (analyzedRows === 0 || vendorRows === 0 || totalAbsoluteChangeRevenue <= 0) {
      return {
        tone: "weak" as const,
        label: "Nema pouzdanog signala",
        details: "Nema dovoljno analiziranih promena za koncentraciju po dobavljačima.",
      };
    }

    if (avgPostCoveragePct == null) {
      return {
        tone: "weak" as const,
        label: "Pokrivenost nije potvrđena",
        details: `Nema validnog podatka o post-window pokrivenosti za ${analyzedRows} analiziranih redova; koncentraciju ne treba tumačiti kao potvrđen signal.`,
      };
    }

    if (analyzedRows < 40 || nonZeroChangeVendors < 5 || avgPostCoveragePct < 20) {
      return {
        tone: "weak" as const,
        label: "Nizak signal",
        details: `Koncentracija je izračunata iz ${analyzedRows} redova i ${nonZeroChangeVendors} dobavljača sa promenom; post-window pokrivenost je ${fmtPct(avgPostCoveragePct, 0)}. Kratak ili svež period tumači oprezno.`,
      };
    }

    if (analyzedRows < 120 || avgPostCoveragePct < 60) {
      return {
        tone: "watch" as const,
        label: "Srednji signal",
        details: `Uzorak je upotrebljiv, ali nije potpuno zreo: ${analyzedRows} redova, post-window pokrivenost ${fmtPct(avgPostCoveragePct, 0)}.`,
      };
    }

    return {
      tone: "strong" as const,
      label: "Stabilan signal",
      details: `Dovoljno promena i pokrivenosti za citanje koncentracije: ${analyzedRows} redova, post-window pokrivenost ${fmtPct(avgPostCoveragePct, 0)}.`,
    };
  }, [
    data?.dataQuality.analyzedRows,
    data?.dataQuality.avgCoveragePost30,
    decisionRows,
    totalAbsoluteChangeRevenue,
  ]);

  const leadingCategory = useMemo(() => {
    const rows = [...(data?.categoryStats ?? [])].sort((left, right) => right.changeRevenue - left.changeRevenue);
    return rows[0] ?? null;
  }, [data?.categoryStats]);

  const leadingPriceDirection = useMemo(() => {
    const rows = [...(data?.priceDirectionStats ?? [])].sort((left, right) => right.changeRevenue - left.changeRevenue);
    return rows[0] ?? null;
  }, [data?.priceDirectionStats]);

const advancedSignals = useMemo(
    () => [
      {
        label: "Momentum",
        value: fmtRsd(data?.avgMomentumRevenue),
        hint: "avg rev",
        tip: "Prosečan prihod od ubrzanja prodaje (momentum signal). Pokazuje da li prodajni trend dobija na brzini pre/posle nivelacije. Nedostupno ako vw_sales_momentum view nije kreiran u bazi.",
      },
      {
        label: "Elasticnost",
        value: fmtNumber(data?.avgElasticity, 2),
        hint: "avg",
        tip: "Prosečna cenovna elastičnost po artiklima dobavljača. Vrednost < 0 znači da rast cene smanjuje prodaju. Računa se kao %Δqty / %Δcena za svaki artikal.",
      },
      {
        label: "DID",
        value: fmtRsd(data?.avgDidRevenue),
        hint: "avg rev",
        tip: "Difference-in-Differences procena uzročnog efekta nivelacije. Poredi promenu prodaje sa kontrolnom grupom (artikli bez nivelacije). Nedostupno ako vw_nivelacija_did nije kreiran.",
      },
      {
        label: "Lost sales OOS",
        value: fmtRsd(data?.avgLostSalesOOS),
        hint: "avg",
        tip: "Procena prihoda izgubljenog zbog iscrpljenosti zalihe (Out of Stock). Izračunava se iz vw_stock_red_zone podataka. Nedostupno dok taj view nije kreiran u bazi.",
      },
    ],
    [data?.avgDidRevenue, data?.avgElasticity, data?.avgLostSalesOOS, data?.avgMomentumRevenue]
  );

  const concentrationData = useMemo(() => {
    if (focusedRows.length === 0 || totalAbsoluteChangeRevenue <= 0) return [] as ConcentrationDatum[];

    const top = [...focusedRows]
      .sort((a, b) => b.sharePct - a.sharePct)
      .slice(0, 7)
      .map((row) => ({
        name: row.vendorName,
        sharePct: row.sharePct,
        changeRevenue: row.changeRevenue,
        articleCount: row.articleCount,
        vendorKey: vendorKey(row),
        selected: expandedVendorKey === vendorKey(row),
      }));

    const topShare = top.reduce((sum, row) => sum + row.sharePct, 0);
    const rest = clamp(100 - topShare, 0, 100);

    return rest > 0.1
      ? [...top, { name: "Ostali", sharePct: rest, changeRevenue: 0, articleCount: 0, vendorKey: null, selected: false }]
      : top;
  }, [expandedVendorKey, focusedRows, totalAbsoluteChangeRevenue]);

  const selectedRow = useMemo(() => {
    if (!expandedVendorKey) return null;
    return sortedRows.find((row) => vendorKey(row) === expandedVendorKey) ?? null;
  }, [expandedVendorKey, sortedRows]);

  const selectedDriverSummary = useMemo<DetailDriverSummary | null>(() => {
    if (!selectedRow || !data) return null;

    const vendorArticles = data.articleStats.filter((item) => vendorKey(item) === vendorKey(selectedRow));
    if (vendorArticles.length === 0) return null;

    const dominantCategoryMap = new Map<string, number>();
    const metricReasonCounts = new Map<string, number>();
    for (const article of vendorArticles) {
      dominantCategoryMap.set(article.category || "N/A", (dominantCategoryMap.get(article.category || "N/A") ?? 0) + article.changeRevenue);
      if (article.metricReason) {
        metricReasonCounts.set(article.metricReason, (metricReasonCounts.get(article.metricReason) ?? 0) + 1);
      }
    }

    const dominantCategoryEntry = [...dominantCategoryMap.entries()].sort((left, right) => right[1] - left[1])[0] ?? ["N/A", 0];
    const topWinner = [...vendorArticles].sort((left, right) => right.changeRevenue - left.changeRevenue)[0];
    const topRisk = [...vendorArticles].sort((left, right) => left.changeRevenue - right.changeRevenue)[0];
    const topMetricReasons = [...metricReasonCounts.entries()]
      .sort((left, right) => right[1] - left[1])
      .slice(0, 3)
      .map(([reason, count]) => `${reason} (${count})`);

    return {
      dominantCategory: dominantCategoryEntry[0],
      dominantCategoryRevenue: dominantCategoryEntry[1],
      topWinnerLabel: topWinner ? `${topWinner.sku || "-"} • ${topWinner.articleName}` : "N/A",
      topWinnerRevenue: topWinner?.changeRevenue ?? 0,
      topRiskLabel: topRisk ? `${topRisk.sku || "-"} • ${topRisk.articleName}` : "N/A",
      topRiskRevenue: topRisk?.changeRevenue ?? 0,
      avgMomentumRevenue: averageNullable(vendorArticles.map((item) => item.momentumRevenue)),
      avgElasticity: averageNullable(vendorArticles.map((item) => item.priceElasticity)),
      avgDidRevenue: averageNullable(vendorArticles.map((item) => item.didRevenue)),
      avgLostSalesOOS: averageNullable(vendorArticles.map((item) => item.lostSalesOOS)),
      topMetricReasons,
    };
  }, [data, selectedRow]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "periodPreset", label: "Period", value: periodPreset },
      { key: "fromDate", label: "Od", value: activeFilters.fromDate },
      { key: "toDate", label: "Do", value: activeFilters.toDate },
      {
        key: "vendorId",
        label: "Dobavljač",
        value: activeFilters.vendorId != null
          ? vendors.find((vendor) => vendor.id === activeFilters.vendorId)?.naziv ?? activeFilters.vendorId
          : "Svi",
      },
      { key: "category", label: "Kategorija", value: activeFilters.category },
      {
        key: "storeId",
        label: "Objekat",
        value: activeFilters.storeId != null
          ? stores.find((store) => store.storeId === activeFilters.storeId)?.storeName ?? activeFilters.storeId
          : "Svi objekti",
      },
      { key: "dataScope", label: "Opseg podataka", value: dataScope },
      { key: "focusFilter", label: "Brzi fokus", value: focusFilterLabel(focusFilter) },
    ],
    [activeFilters.category, activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, activeFilters.vendorId, dataScope, focusFilter, periodPreset, stores, vendors]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
      { key: "dataScope", label: "Opseg podataka", value: dataScope },
      { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "Svi objekti" },
      {
        key: "previousComparison",
        label: "Uporedni period",
        value: previousComparisonError ? `Greška: ${previousComparisonError}` : "Učitan",
      },
      { key: "vendorsCount", label: "Dobavljača", value: data?.totals.vendorsCount ?? 0 },
      { key: "articlesCount", label: "Artikala", value: data?.totals.articlesCount ?? 0 },
      { key: "windowDays", label: "Prozor analize", value: data?.windowDays ?? 0 },
      { key: "rowsExported", label: "Vidljivih redova", value: focusedRows.length },
      {
        key: "absoluteChangeShareFormula",
        label: "Formula udele promene",
        value: "abs(promena prometa) / zbir apsolutnih promena prometa",
      },
      { key: "dataTrust", label: "Poverenje", value: dataTrustSummary.label },
      { key: "analyzedShare", label: "Analizirani redovi", value: fmtPct(data?.dataQuality.analyzedSharePercent, 0) },
      { key: "duplicateRowsRemoved", label: "Duplicati uklonjeni", value: data?.dataQuality.duplicateRowsRemoved ?? 0 },
      { key: "inactiveRows", label: "Neaktivni redovi", value: data?.dataQuality.inactiveRows ?? 0 },
      { key: "metricsStatus", label: "Status metrika", value: data?.metricsStatus ?? "OK" },
    ],
    [
      activeFilters.storeId,
      data?.dataQuality.analyzedSharePercent,
      data?.dataQuality.duplicateRowsRemoved,
      data?.dataQuality.inactiveRows,
      data?.generatedAt,
      data?.metricsStatus,
      data?.totals.articlesCount,
      data?.totals.vendorsCount,
      data?.windowDays,
      dataScope,
      dataTrustSummary.label,
      focusedRows.length,
      previousComparisonError,
    ]
  );

  const controlBarChips = useMemo<AnalyticsControlBarChip[]>(
    () => [
      { key: "scope", label: "Opseg", value: dataScope, tone: "info" },
      {
        key: "period",
        label: "Period",
        value: `${activeFilters.fromDate} → ${activeFilters.toDate}`,
        tone: "neutral",
      },
      {
        key: "focus",
        label: "Fokus",
        value: focusFilterLabel(focusFilter),
        tone: focusFilter === "all" ? "success" : "warning",
      },
      {
        key: "rows",
        label: "Prikazano",
        value: `${focusedRows.length.toLocaleString("sr-RS")} / ${decisionRows.length.toLocaleString("sr-RS")}`,
        tone: focusedRows.length === 0 ? "warning" : "success",
      },
      {
        key: "trust",
        label: "Poverenje",
        value: dataTrustSummary.label,
        tone:
          dataTrustSummary.tone === "strong"
            ? "success"
            : dataTrustSummary.tone === "watch"
              ? "warning"
              : "critical",
      },
    ],
    [
      activeFilters.fromDate,
      activeFilters.toDate,
      dataScope,
      dataTrustSummary.label,
      dataTrustSummary.tone,
      decisionRows.length,
      focusFilter,
      focusedRows.length,
    ]
  );

  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortDir((current) => (current === "asc" ? "desc" : "asc"));
      return;
    }
    setSortField(field);
    setSortDir(field === "vendorName" ? "asc" : "desc");
  };

  const handlePresetChange = (value: PeriodPreset) => {
    setPeriodPreset(value);
    if (value === "custom") return;
    const range = getPresetRange(value);
    setFromDate(range.fromDate);
    setToDate(range.toDate);
  };

  const handleApplyFilters = () => {
    if (invalidRange) return;
    setFocusFilter("all");
    setActiveFilters({
      fromDate,
      toDate,
      vendorId,
      category,
      storeId,
    });
  };

  const handleResetFilters = () => {
    const range = getPresetRange("30d");
    setPeriodPreset("30d");
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    setVendorId(null);
    setCategory("");
    setStoreId(null);
    setFocusFilter("all");
    setActiveFilters({
      fromDate: range.fromDate,
      toDate: range.toDate,
      vendorId: null,
      category: "",
      storeId: null,
    });
  };

  const controlBarFields = useMemo<AnalyticsControlBarField[]>(
    () => [
      {
        key: "periodPreset",
        label: "Period",
        control: (
          <select value={periodPreset} onChange={(event) => handlePresetChange(event.target.value as PeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="180d">Poslednjih 180 dana</option>
            <option value="365d">Poslednjih 365 dana</option>
            <option value="custom">Prilagođeno</option>
          </select>
        ),
      },
      {
        key: "fromDate",
        label: "Od",
        control: <input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />,
      },
      {
        key: "toDate",
        label: "Do",
        control: <input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />,
      },
      {
        key: "vendorId",
        label: "Dobavljač",
        span: "wide",
        control: (
          <select value={vendorId ?? ""} onChange={(event) => setVendorId(event.target.value ? Number(event.target.value) : null)}>
            <option value="">Svi</option>
            {vendors.map((vendor) => (
              <option key={vendor.id} value={vendor.id}>
                {vendor.naziv}
              </option>
            ))}
          </select>
        ),
      },
      {
        key: "category",
        label: "Kategorija",
        control: (
          <select value={category} onChange={(event) => setCategory(event.target.value)}>
            <option value="">Sve</option>
            {(data?.categories ?? []).map((item) => (
              <option key={item} value={item}>
                {item}
              </option>
            ))}
          </select>
        ),
      },
      {
        key: "storeId",
        label: "Objekat",
        span: "wide",
        control: (
          <select value={storeId ?? ""} onChange={(event) => setStoreId(event.target.value ? Number(event.target.value) : null)}>
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
    [category, data?.categories, fromDate, handlePresetChange, periodPreset, storeId, stores, toDate, vendorId, vendors]
  );

  const openVendorDetail = (row: DecisionVendor) => {
    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "nivelacije-pre-post",
        recordId: String(row.vendorId ?? row.vendorName),
        title: row.vendorName,
        subtitle: "Decision support po dobavljaču",
        columns: decisionColumns,
        row,
        metadata: [...toolbarFilters, ...toolbarMetadata],
      })
    );

    navigate(`/analitika/nivelacije-pre-post/${encodeURIComponent(String(row.vendorId ?? row.vendorName))}`, {
      state: { backgroundLocation: location },
    });
  };

  const handleChartClick = (state: unknown) => {
    const payload = (state as { activePayload?: Array<{ payload?: ConcentrationDatum }> } | undefined)?.activePayload?.[0]?.payload;
    if (!payload?.vendorKey) return;
    setExpandedVendorKey(payload.vendorKey);
  };

  const handleCellClick = (data: ConcentrationDatum) => {
    if (!data.vendorKey) return;
    setExpandedVendorKey(data.vendorKey);
  };

  return (
    <div className="ppn-decision-page">
      <AnalyticsTrustHeader
        title="Prodaja pre/posle nivelacije"
        description="Event-window analiza: poredi 30 dana pre i 30 dana posle svake nivelacije, pa sabira signal po dobavljaču."
        periodFrom={activeFilters.fromDate}
        periodTo={activeFilters.toDate}
        lastRefreshAt={data?.generatedAt ?? null}
        dataSource={`Nivelacija analytics (scope: ${dataScope}${activeFilters.storeId != null ? `, store: ${activeFilters.storeId}` : ""})`}
        mode="report"
        dataQualityStatus={dataMeta?.dataQualityStatus ?? null}
        isPartial={showMetaWarning}
        emptyStateReason={showEmptyState ? (dataMetaMessage ?? null) : null}
        methodologyHref="/analytics/data-quality"
        dataQualityHref="/analytics/data-quality"
        refreshStatusHref="/admin/configuration?panel=workers"
        compact
      />
      <AnalyticsControlBar
        title="Kontrole i opseg"
        description="Period, dobavljač, kategorija i objekat ostaju ovde; tabela ispod ostaje fokusirana na pre/post signal po dobavljaču."
        chips={controlBarChips}
        primaryAction={{
          key: "apply",
          label: loading ? "Učitavanje..." : "Primeni",
          onClick: handleApplyFilters,
          disabled: loading || invalidRange,
        }}
        secondaryActions={[
          {
            key: "reset",
            label: "Reset",
            onClick: handleResetFilters,
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
      <header className="ppn-decision-header">
        <div>
          <h1 className="ppn-decision-title">Prodaja pre/posle nivelacije</h1>
          <p className="ppn-decision-subtitle">
            Event-window analiza: poredi 30 dana pre i 30 dana posle svake nivelacije, pa sabira signal po dobavljaču.
            Nije izolovani profit, već poslovni signal za prioritet nabavke i nadzor cene.
          </p>
        </div>
        <div className="ppn-decision-generated">
          Generisano: {data?.generatedAt ? new Date(data.generatedAt).toLocaleString("sr-RS") : "-"}
        </div>
      </header>

      {invalidRange ? <div className="ppn-decision-message error">Datum 'od' ne može biti posle datuma 'do'.</div> : null}
      {error ? (
        <AnalyticsErrorState
          title="Podaci trenutno nisu dostupni"
          message={error || "Ne prikazujemo nule jer nije potvrđeno da je period stvarno prazan."}
          onRetry={() => void load(activeFilters, dataScope)}
          helpHref="/analytics/data-quality"
        />
      ) : null}
      {showMetaWarning ? (
        <div className="ppn-decision-message warning" role="status">
          Prikazani podaci su delimični. {dataMetaMessage ?? "Proverite analytics refresh status."}
        </div>
      ) : null}
      {previousComparisonError ? (
        <div className="ppn-decision-message warning" role="status" data-testid="previous-comparison-warning">
          Uporedni prethodni period nije učitan ({previousComparisonError}). PoP rast i volatilnost nisu dostupni zbog
          greške zahteva, ne zbog stvarnog nedostatka baze — ovo nije „Nova baza“.
        </div>
      ) : null}
      {showEmptyState ? (
        <AnalyticsEmptyState
          variant={emptyStateVariant}
          message={
            emptyStateVariant === "insufficient_data"
              ? "Ne prikazujemo automatsku preporuku jer signal nije dovoljno jak."
              : emptyStateVariant === "filtered_out"
                ? "Promenite filtere ili proširite period."
                : (dataMetaMessage ?? "Nije bilo prodaje u izabranom periodu.")
          }
          actions={[
            { label: "Proširite period pretrage." },
            { label: "Uklonite filter dobavljača ili prodavnice." },
            { label: "Proverite analytics refresh.", href: "/analytics/data-quality" },
          ]}
          dataQualityHref="/analytics/data-quality"
          refreshStatusHref="/admin/configuration?panel=workers"
          emptyReason={dataMeta?.emptyReason ?? dataMetaMessage ?? null}
          onRetry={() => void load(activeFilters, dataScope)}
        />
      ) : null}
      {loading ? <div className="ppn-decision-message loading">Učitavam pre/post signal po dobavljačima...</div> : null}

      {!loading && data ? (
        <>
          {/* Compact Data Health badge – collapsible trust/quality layer */}
          <div className="ppn-data-health-bar">
            <button
              type="button"
              className={`ppn-data-health-badge ppn-data-health-badge--${dataTrustSummary.tone}`}
              onClick={() => setTrustPanelOpen((prev) => !prev)}
              aria-expanded={trustPanelOpen}
              title={trustPanelOpen ? "Sakrij detalje kvaliteta signala" : "Prikaži detalje kvaliteta signala"}
            >
              {dataTrustSummary.tone === "strong" ? "✓" : "⚠"} Kvalitet signala: {dataTrustSummary.label}
              <span className="ppn-health-caret">{trustPanelOpen ? " ▾" : " ▸"}</span>
            </button>
            <span className="ppn-data-health-hint">
              Analiza poredjena po nivelacionom prozoru od {data.windowDays ?? 30} dana.
            </span>
          </div>

          {trustPanelOpen ? (
            <div className="ppn-trust-drawer">
              <p className="ppn-trust-details">{dataTrustSummary.details}</p>
              {dataQualityWarnings.length > 0 ? (
                <div className="ppn-warning-list">
                  {dataQualityWarnings.slice(0, 5).map((warning) => {
                    const meta = getMetricWarningMeta(warning);
                    return (
                      <div key={warning} className={`ppn-warning-item ppn-warning-${meta.severity}`}>
                        <span className="ppn-warning-label">{meta.label}</span>
                        <span className="ppn-warning-explanation">{meta.explanation}</span>
                      </div>
                    );
                  })}
                </div>
              ) : (
                <div className="ppn-chip-wrap">
                  <span className="ppn-signal-pill signal-strong">Bez aktivnih upozorenja</span>
                  <span className="ppn-signal-pill signal-neutral">Redovi {data.dataQuality.analyzedRows}/{data.dataQuality.rawRows}</span>
                </div>
              )}
            </div>
          ) : null}

          <section className="ppn-decision-signals">
            <article className="ppn-decision-card ppn-signal-card">
              <div className="ppn-card-topline">
                <h2>Najjaca kategorija</h2>
                <span className="ppn-signal-pill signal-neutral">Kategorija</span>
              </div>
              <p>
                {leadingCategory
                  ? `${leadingCategory.category} vodi po efektu nakon nivelacije.`
                  : "Kategorijski signal nije dostupan za izabrani opseg."}
              </p>
              <div className="ppn-stat-pair">
                <strong>{leadingCategory ? fmtRsd(leadingCategory.changeRevenue) : "N/A"}</strong>
                <span>{leadingCategory ? fmtSignedPct(leadingCategory.changePercent, 1) : "N/A"}</span>
              </div>
            </article>

            <article className="ppn-decision-card ppn-signal-card">
              <div className="ppn-card-topline">
                <h2>Dominantna promena cene</h2>
                <span className="ppn-signal-pill signal-neutral">Mix</span>
              </div>
              <p>
                {leadingPriceDirection
                  ? `${leadingPriceDirection.segment} trenutno nosi najveći doprinos promeni prometa.`
                  : "Nema dovoljno price-direction signala za izabrani opseg."}
              </p>
              <div className="ppn-stat-pair">
                <strong>{leadingPriceDirection ? fmtRsd(leadingPriceDirection.changeRevenue) : "N/A"}</strong>
                <span>{leadingPriceDirection ? fmtSignedPct(leadingPriceDirection.avgPriceChangePercent, 1) : "N/A"}</span>
              </div>
            </article>
          </section>

          {data.insights.length > 0 ? (
            <section className="ppn-insight-grid">
              {data.insights.slice(0, 4).map((insight) => (
                <article key={`${insight.title}-${insight.value}`} className={insightToneClass(insight.tone)}>
                  <span>{insight.title}</span>
                  <strong>{insight.value}</strong>
                  <p>{insight.details}</p>
                </article>
              ))}
            </section>
          ) : null}

          {advancedSignals.some((item) => item.value !== "N/A") ? (
            <section className="ppn-advanced-signals-secondary">
              <h3 className="ppn-section-label">
                Dodatni analitički signali
                <InfoTip text="Dodatni signali izračunati iz naprednih pogleda. Dostupni su samo ako su potrebni pogledi kreirani u bazi – osnovna analiza ostaje ispravna i kada su ovi signali nedostupni." />
              </h3>
              <div className="ppn-mini-metrics ppn-mini-metrics--secondary">
                {advancedSignals.filter((item) => item.value !== "N/A").map((item) => (
                  <article key={item.label}>
                    <span className="ppn-mini-metric-label">
                      {item.label}
                      <InfoTip text={item.tip} />
                    </span>
                    <strong>{item.value}</strong>
                    <small>{item.hint}</small>
                  </article>
                ))}
              </div>
            </section>
          ) : null}

          <section className="ppn-decision-kpis">
            <article className="ppn-decision-kpi analytics-kpi-card analytics-kpi-card--tone-info" data-note="Promet realizovan u post-window periodu nakon nivelacije.">
              <span>Post-window promet posle nivelacije</span>
              <strong>{fmtRsd(totalRevenue)}</strong>
            </article>
            <article className="ppn-decision-kpi analytics-kpi-card analytics-kpi-card--tone-warning" data-note="Koliko top 5 dobavljača nosi ukupnu promenu signala.">
              <span>Top 5 udeo u promeni</span>
              <strong>{fmtPct(top5SharePct)}</strong>
            </article>
            <article className="ppn-decision-kpi analytics-kpi-card analytics-kpi-card--tone-value" data-note="Apsolutna promena prometa pre i posle nivelacije.">
              <span>Ukupna promena prometa</span>
              <strong className={trendClass(totalChangeRevenue)}>{fmtRsd(totalChangeRevenue)}</strong>
            </article>
            <article className="ppn-decision-kpi analytics-kpi-card analytics-kpi-card--tone-success" data-note="Trend prema prethodnom uporedivom event-opsegu.">
              <span>Rast/pad vs prethodni event-opseg</span>
              <strong className={trendClass(periodGrowthPct)}>{periodGrowthDisplay}</strong>
            </article>
          </section>

          <section className="ppn-decision-panels">
            <article className="ppn-decision-card analytics-surface-panel">
              <div className="ppn-decision-card-heading">
                <div>
                  <h2>Koncentracija promena po dobavljačima</h2>
                  <p>Udeo dobavljača u apsolutnoj promeni prometa: |promena dobavljača| / zbir |promena svih dobavljača|.</p>
                </div>
                <span className={confidenceClass(concentrationQuality.tone)}>
                  {concentrationQuality.label}
                  <InfoTip text={concentrationQuality.details} />
                </span>
              </div>
              {concentrationQuality.tone === "weak" ? (
                <div className="ppn-signal-note">
                  {concentrationQuality.details}
                </div>
              ) : null}
              {concentrationData.length > 0 ? (
                <div className="ppn-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={concentrationData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }} onClick={handleChartClick}>
                      <CartesianGrid strokeDasharray="2 6" stroke={CHART_GRID_STROKE} />
                      <XAxis type="number" tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={CHART_AXIS_TICK} tickLine={false} axisLine={false} label={{ value: null }} />
                      <Tooltip content={<CustomConcentrationTooltip />} contentStyle={COMMAND_TOOLTIP_STYLE} labelStyle={COMMAND_TOOLTIP_LABEL_STYLE} cursor={CHART_CURSOR_STYLE} />
                      <Bar dataKey="sharePct" radius={[0, 8, 8, 0]}>
                        {concentrationData.map((entry) => (
                          <Cell
                            key={`${entry.name}-${entry.vendorKey ?? "rest"}`}
                            fill={entry.selected ? "var(--dashboard-secondary)" : entry.vendorKey ? "var(--dashboard-accent)" : "var(--dashboard-border)"}
                            onClick={() => handleCellClick(entry)}
                            style={{ cursor: entry.vendorKey ? "pointer" : "default" }}
                          />
                        ))}
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="ppn-decision-empty">Nedovoljno promena u izabranom periodu za pouzdanu koncentraciju.</div>
              )}
              <div className="ppn-chart-hint">Klik na traku otvara detalj dobavljača u tabeli. Svež 30d period može imati nizak post-window signal.</div>
            </article>

            <article className="ppn-decision-card analytics-surface-panel">
              <AnalyticsDataTable
                testId="prodaja-pre-post-nivelacije-data-table"
                rowCount={focusedRows.length}
                toolbar={(
                  <div className="ppn-decision-table-head">
                    <div>
                      <h2>Prioritetna lista dobavljača</h2>
                      <p>
                        {recommendationStatusLabel("increase_focus")}: {vendorCounts.increaseFocus} | {recommendationStatusLabel("maintain")}: {vendorCounts.maintain} | {recommendationStatusLabel("review")}: {vendorCounts.review} | {recommendationStatusLabel("do_not_trust")}: {vendorCounts.doNotTrust} | {recommendationStatusLabel("insufficient_data")}: {vendorCounts.insufficientData}
                      </p>
                    </div>
                    <AnalyticsTableToolbar
                      tableKey="nivelacije-pre-post"
                      tableTitle="Decision support pre/post nivelacije"
                      columns={decisionColumns}
                      rows={focusedRows}
                      filters={toolbarFilters}
                      metadata={toolbarMetadata}
                      defaultOrientation="landscape"
                    />
                  </div>
                )}
              >
              <div className="ppn-chip-wrap ppn-focus-filters">
                {(["all", "increaseFocus", "maintain", "review", "doNotTrust", "insufficientData", "lowConfidence", "volatile"] as FocusFilter[]).map((item) => (
                  <button
                    key={item}
                    type="button"
                    className={focusFilter === item ? "ppn-focus-chip active" : "ppn-focus-chip"}
                    onClick={() => setFocusFilter(item)}
                  >
                    {focusFilterLabel(item)} <span>{focusFilterCounts[item]}</span>
                  </button>
                ))}
              </div>

              <div className="ppn-decision-table-wrap">
                <table className="ppn-decision-table">
                  <thead>
                    <tr>
                      <th>
                        <button type="button" onClick={() => handleSort("vendorName")}>
                          Dobavljač{sortMarker("vendorName", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("postRevenue")}>
                          Promet posle{sortMarker("postRevenue", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("sharePct")}>
                          Udeo apsolutne promene{sortMarker("sharePct", sortField, sortDir)}
                        </button>
                        <InfoTip text="Udeo apsolutne promene = abs(promena prometa) / zbir apsolutnih promena prometa. Ako je post-window pokrivenost niska, signal pokazuje koncentraciju rizika, ali ne i konačan efekat nivelacije." />
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("changeRevenue")}>
                          Promena{sortMarker("changeRevenue", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("trendPct")}>
                          Trend{sortMarker("trendPct", sortField, sortDir)}
                        </button>
                        <InfoTip text="Procentualna promena prometa: (postRevenue − preRevenue) / preRevenue. Pozitivno = rast posle nivelacije." />
                      </th>
                      <th className="align-center">
                        <button type="button" onClick={() => handleSort("volatilityPct")}>
                          Volatilnost{sortMarker("volatilityPct", sortField, sortDir)}
                        </button>
                        <InfoTip text="Variranje post-window prometa vs prethodni event-opseg istog perioda. Visoka volatilnost znači nestabilan signal – preporuku treba uzeti s rezervom." />
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>
                          Preporuka{sortMarker("status", sortField, sortDir)}
                        </button>
                        <InfoTip text="Backend-authoritative recommendation za ovaj pre/post red. Status, razlog i sigurnost preporuke dolaze iz server-side analytics recommendation engine-a; frontend više ne računa lokalne threshold odluke." />
                      </th>
                      <th className="align-center">Detalj</th>
                    </tr>
                  </thead>
                  <tbody>
                    {focusedRows.length === 0 ? (
                      <tr>
                        <td colSpan={8} className="ppn-decision-empty-row">
                          Nema podataka za izabrane filtere.
                        </td>
                      </tr>
                    ) : (
                      focusedRows.map((row) => {
                        const rowId = vendorKey(row);
                        const expanded = expandedVendorKey === rowId;
                        return (
                          <tr key={rowId} className={expanded ? "expanded-row" : ""}>
                            <td>
                              <div className="ppn-vendor-cell">
                                <strong title={row.vendorName || "Nepoznat dobavljač"}>{row.vendorName || "Nepoznat dobavljač"}</strong>
                                <div className="ppn-chip-wrap">
                                  <span className={confidenceClass(row.confidenceTone)}>
                                    {row.confidenceLabel} signal
                                    <InfoTip text={`${analyticsMetricDescriptions.reliabilityPct} Aktivni artikli: ${row.activeArticlesCount}/${row.articleCount}. Post-window pokrivenost: ${row.avgCoveragePost30Available ? fmtPct(row.avgCoveragePost30, 0) : "Nije dostupno"}.`} />
                                  </span>
                                  <span className="ppn-signal-pill signal-neutral">{row.activeArticlesCount}/{row.articleCount} aktivno</span>
                                </div>
                              </div>
                            </td>
                            <td className="align-right">{fmtRsd(row.postRevenue)}</td>
                            <td className="align-right">{row.sharePctAvailable ? fmtPct(row.sharePct, 2) : "Nije dostupno"}</td>
                            <td className={`align-right ${trendClass(row.changeRevenue)}`}>{fmtRsd(row.changeRevenue)}</td>
                            <td className={`align-right ${trendClass(row.trendPct)}`}>{fmtSignedPct(row.trendPct, 2)}</td>
                            <td className="align-center">
                              <span className={volatilityClass(row.volatilityTone)} title={fmtSignedPct(row.volatilityPct, 1)}>
                                {row.volatilityLabel}
                              </span>
                            </td>
                            <td>
                              <span
                                className={statusClass(row.status)}
                                title={buildStatusTooltip(row)}
                                aria-label={buildStatusTooltip(row)}
                              >
                                {statusDisplayLabel(row.status)}
                              </span>
                            </td>
                            <td className="align-center">
                              <button
                                type="button"
                                className="ppn-decision-detail-btn"
                                onClick={() => setExpandedVendorKey(expanded ? null : rowId)}
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
              </AnalyticsDataTable>
            </article>
          </section>

          {selectedRow ? (
            <section className="ppn-decision-detail">
              <div className="ppn-decision-detail-head">
                <h3 title={selectedRow.vendorName || "Nepoznat dobavljač"}>Detalj odluke: {selectedRow.vendorName || "Nepoznat dobavljač"}</h3>
                <button type="button" onClick={() => openVendorDetail(selectedRow)}>
                  Otvori puni detalj
                </button>
              </div>
              <p className="ppn-decision-reason">
                <strong>Napomena o udelu promene:</strong> kolona koristi abs(promena prometa) / zbir apsolutnih promena prometa, pa ne pokazuje udeo u ukupnom prometu.
              </p>

              <div className="ppn-decision-detail-grid">
                <article>
                  <span>Pre nivelacije promet</span>
                  <strong>{fmtRsd(selectedRow.preRevenue)}</strong>
                </article>
                <article>
                  <span>Posle nivelacije promet</span>
                  <strong>{fmtRsd(selectedRow.postRevenue)}</strong>
                </article>
                <article>
                  <span>Pre nivo kolicina</span>
                  <strong>{fmtQty(selectedRow.preQty)}</strong>
                </article>
                <article>
                  <span>Posle nivo kolicina</span>
                  <strong>{fmtQty(selectedRow.postQty)}</strong>
                </article>
                <article>
                  <span>Aktivni artikli</span>
                  <strong>{selectedRow.activeArticlesCount} / {selectedRow.articleCount}</strong>
                </article>
                <article>
                  <span>
                    {RECOMMENDATION_RELIABILITY_LABEL}
                    <InfoTip text={analyticsMetricDescriptions.reliabilityPct} />
                  </span>
                  <strong>{selectedRow.reliabilityAvailable ? fmtPct(selectedRow.reliabilityPct, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
                <article>
                  <span>Status kvaliteta preporuke</span>
                  <strong style={recommendationQualityStyle(selectedRow.dataQualityStatus)}>{recommendationQualityLabel(selectedRow.dataQualityStatus)}</strong>
                </article>
                <article>
                  <span>
                    Volatilnost vs prethodni period
                    <InfoTip text="Procentualna razlika post-window prometa ovog perioda vs prethodnog event-opsega. Visoka volatilnost (>30%) znači nestabilan signal – preporuka je manje sigurna." />
                  </span>
                  <strong>{selectedRow.volatilityPct == null ? selectedRow.volatilityLabel : fmtSignedPct(selectedRow.volatilityPct, 1)}</strong>
                </article>
                <article>
                  <span>SKU sa dizanjem cene</span>
                  <strong>{selectedRow.increasedPriceArticlesCount}</strong>
                </article>
                <article>
                  <span>SKU sa sniženjem cene</span>
                  <strong>{selectedRow.decreasedPriceArticlesCount}</strong>
                </article>
                <article>
                  <span>
                    {RECOMMENDATION_CONFIDENCE_LABEL}
                    <InfoTip text={analyticsMetricDescriptions.recommendationConfidencePct} />
                  </span>
                  <strong>{selectedRow.confidenceAvailable ? fmtPct(selectedRow.confidencePct, 1) : RECOMMENDATION_SIGNAL_UNAVAILABLE}</strong>
                </article>
              </div>

              {selectedDriverSummary ? (
                <div className="ppn-driver-grid">
                  <article>
                    <span>Dominantna kategorija</span>
                    <strong>{selectedDriverSummary.dominantCategory}</strong>
                    <small>{fmtRsd(selectedDriverSummary.dominantCategoryRevenue)}</small>
                  </article>
                  <article>
                    <span>Top dobitnik SKU</span>
                    <strong>{selectedDriverSummary.topWinnerLabel}</strong>
                    <small>{fmtRsd(selectedDriverSummary.topWinnerRevenue)}</small>
                  </article>
                  <article>
                    <span>Top rizik SKU</span>
                    <strong>{selectedDriverSummary.topRiskLabel}</strong>
                    <small>{fmtRsd(selectedDriverSummary.topRiskRevenue)}</small>
                  </article>
                  <article>
                    <span>Momentum / Elasticnost</span>
                    <strong>{fmtRsd(selectedDriverSummary.avgMomentumRevenue)}</strong>
                    <small>Elasticnost {fmtNumber(selectedDriverSummary.avgElasticity, 2)}</small>
                  </article>
                  <article>
                    <span>DID / Lost sales OOS</span>
                    <strong>{fmtRsd(selectedDriverSummary.avgDidRevenue)}</strong>
                    <small>Lost sales {fmtRsd(selectedDriverSummary.avgLostSalesOOS)}</small>
                  </article>
                  <article>
                    <span>
                      Najcesci metric reason
                      <InfoTip text="Interni razlog zašto neki artikli nemaju sve metrike (rolling, momentum, OOS, DiD). Obično se radi o nedostajućim analytics view-ovima – ne utiče na ispravnost osnovne pre/post analize." />
                    </span>
                    <strong>{selectedDriverSummary.topMetricReasons[0] ? getMetricWarningMeta(selectedDriverSummary.topMetricReasons[0].split(" (")[0]).label : "N/A"}</strong>
                    <small>{selectedDriverSummary.topMetricReasons.slice(1).map((r) => getMetricWarningMeta(r.split(" (")[0]).label).join(" | ") || "Bez dodatnih upozorenja"}</small>
                  </article>
                </div>
              ) : null}

              <p className="ppn-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedRow.statusReason}
              </p>
              {selectedRow.reasonCodes.length > 0 ? (
                <p className="ppn-decision-reason">
                  <strong>Reason codes:</strong> {selectedRow.reasonCodes.join(" | ")}
                </p>
              ) : null}
              {recommendationReasonHints(selectedRow.reasonCodes).map((hint) => (
                <p key={hint} className="ppn-decision-reason">
                  <strong>Napomena:</strong> {hint}
                </p>
              ))}
              {(!selectedRow.reliabilityAvailable || !selectedRow.confidenceAvailable || selectedRow.dataQualityStatus !== "good") ? (
                <p className="ppn-decision-reason">
                  <strong>Data quality:</strong> Otvori <Link to="/analytics/data-quality">Data Quality</Link> da proveris i ispravis signal.
                </p>
              ) : null}
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}


