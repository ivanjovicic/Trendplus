import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
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
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import { getDobavljaci } from "../services/dobavljaciApi";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import {
  getVendorSalesNivelacija,
  type VendorSalesNivelacijaResponse,
  type VendorSalesNivelacijaVendorStat,
} from "../services/vendorSalesNivelacijaApi";
import type { Dobavljac } from "../types/Dobavljaci";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import "./ProdajaPrePostNivelacijePage.css";

type PeriodPreset = "30d" | "90d" | "180d" | "365d" | "custom";
type SortDir = "asc" | "desc";
type SortField = "vendorName" | "postRevenue" | "sharePct" | "changeRevenue" | "trendPct" | "volatilityPct" | "status";
type DecisionStatus = "Pojacaj" | "Zadrzi" | "Smanji";
type FocusFilter = "all" | "boost" | "keep" | "reduce" | "lowConfidence" | "volatile";
type ConfidenceTone = "strong" | "watch" | "weak";
type VolatilityTone = "positive" | "negative" | "warning" | "neutral";

type ActiveFilters = {
  fromDate: string;
  toDate: string;
  vendorId: number | null;
  category: string;
};

type DecisionVendor = VendorSalesNivelacijaVendorStat & {
  sharePct: number;
  trendPct: number;
  reliabilityPct: number;
  decisionScore: number;
  status: DecisionStatus;
  statusReason: string;
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
  Pojacaj: 3,
  Zadrzi: 2,
  Smanji: 1,
};
const BOOST_SCORE_THRESHOLD = 68;
const KEEP_SCORE_THRESHOLD = 43;
const BOOST_MIN_RELIABILITY_PCT = 40;

const UNKNOWN_SUPPLIERS = new Set(["", "N/A", "NEPOZNATO", "UNKNOWN", "UNKNOWN SUPPLIER"]);

const decisionColumns: AnalyticsTableColumn<DecisionVendor>[] = [
  { key: "vendorName", header: "Dobavljac", dataType: "text" },
  { key: "preRevenue", header: "Promet pre", dataType: "currency" },
  { key: "postRevenue", header: "Promet posle", dataType: "currency" },
  { key: "sharePct", header: "Udeo %", dataType: "percent" },
  { key: "changeRevenue", header: "Promena prometa", dataType: "currency" },
  { key: "trendPct", header: "Trend %", dataType: "percent" },
  { key: "reliabilityPct", header: "Pouzdanost %", dataType: "percent" },
  { key: "volatilityLabel", header: "Volatilnost", dataType: "text" },
  { key: "status", header: "Preporuka", dataType: "text" },
  { key: "decisionScore", header: "Decision score", dataType: "number" },
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
    <div
      style={{
        backgroundColor: "var(--surface-card)",
        border: "1px solid var(--border-default)",
        borderRadius: "6px",
        padding: "8px 12px",
        color: "var(--text-primary)",
      }}
    >
      <p style={{ margin: 0, fontSize: "12px", fontWeight: 500 }}>{data.name}</p>
      <p style={{ margin: "4px 0 0", fontSize: "12px", color: "var(--text-secondary)" }}>{fmtPct(data.sharePct, 2)}</p>
    </div>
  );
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}

function toDateInput(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function getPresetRange(preset: Exclude<PeriodPreset, "custom">): { fromDate: string; toDate: string } {
  const to = new Date();
  const from = new Date(to);
  if (preset === "30d") from.setDate(from.getDate() - 29);
  if (preset === "90d") from.setDate(from.getDate() - 89);
  if (preset === "180d") from.setDate(from.getDate() - 179);
  if (preset === "365d") from.setDate(from.getDate() - 364);
  return {
    fromDate: toDateInput(from),
    toDate: toDateInput(to),
  };
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

function fmtRsd(value: number): string {
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 0, maximumFractionDigits: 0 })} RSD`;
}

function fmtPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: digits, maximumFractionDigits: digits })}%`;
}

function fmtOptionalRsd(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return fmtRsd(value);
}

function fmtMetric(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return value.toLocaleString("sr-RS", { minimumFractionDigits: digits, maximumFractionDigits: digits });
}

function fmtSignedPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  const sign = value > 0 ? "+" : "";
  return `${sign}${fmtPct(value, digits)}`;
}

function fmtQty(value: number): string {
  return `${value.toLocaleString("sr-RS")} kom`;
}

function sortMarker(field: SortField, activeField: SortField, dir: SortDir): string {
  if (field !== activeField) return "";
  return dir === "asc" ? " ^" : " v";
}

function statusClass(status: DecisionStatus): string {
  if (status === "Pojacaj") return "ppn-decision-status status-boost";
  if (status === "Smanji") return "ppn-decision-status status-reduce";
  return "ppn-decision-status status-keep";
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
  if (filter === "boost") return "Pojacaj";
  if (filter === "keep") return "Zadrzi";
  if (filter === "reduce") return "Smanji";
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

function averageNullable(values: Array<number | null | undefined>): number | null {
  const numbers = values.filter((value): value is number => value != null && !Number.isNaN(value));
  if (numbers.length === 0) return null;
  return numbers.reduce((sum, value) => sum + value, 0) / numbers.length;
}

function buildConfidenceMeta(reliabilityPct: number): { label: string; tone: ConfidenceTone } {
  if (reliabilityPct >= 70) return { label: "Visoko", tone: "strong" };
  if (reliabilityPct >= BOOST_MIN_RELIABILITY_PCT) return { label: "Srednje", tone: "watch" };
  return { label: "Nisko", tone: "weak" };
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
  if (filter === "boost") return row.status === "Pojacaj";
  if (filter === "keep") return row.status === "Zadrzi";
  if (filter === "reduce") return row.status === "Smanji";
  if (filter === "lowConfidence") return row.confidenceTone === "weak";
  if (filter === "volatile") return row.volatilityLabel === "Visoka" || row.volatilityLabel === "Promenljivo" || row.volatilityLabel === "Novo";
  return true;
}

type StatusReasonSignals = {
  sharePct: number;
  avgShare: number;
  trendPct: number;
  changeRevenue: number;
  reliabilityPct: number;
};

type StatusTooltipData = {
  status: DecisionStatus;
  statusReason: string;
  sharePct: number;
  trendPct: number;
  changeRevenue: number;
  reliabilityPct: number;
};

function buildStatusReason(status: DecisionStatus, signals: StatusReasonSignals): string {
  const lowReliability = signals.reliabilityPct < BOOST_MIN_RELIABILITY_PCT;
  const positiveTrend = signals.trendPct > 0;
  const negativeTrend = signals.trendPct < 0;

  if (status === "Pojacaj") {
    if (lowReliability) return "Signal je dobar, ali je pouzdanost niska; potvrditi pre veceg ulaganja.";
    if (positiveTrend && signals.changeRevenue > 0) return "Rast prometa i pozitivan trend posle nivelacije.";
    if (signals.sharePct >= signals.avgShare) return "Zdrav udeo i stabilan doprinos nakon promene cene.";
    return "Stabilan doprinos i prostor za veci fokus u nabavci.";
  }

  if (status === "Zadrzi") {
    if (lowReliability) return "Niza pouzdanost podataka; odluku drzati konzervativnom dok se signal ne stabilizuje.";
    if (negativeTrend && signals.changeRevenue < 0) return "Pad trenda posle nivelacije; zadrzati uz pojacan nadzor.";
    return "Stabilan rezultat bez dovoljno jakog signala za promenu prioriteta.";
  }

  if (negativeTrend) return "Pad trenda uz slabiji efekat nivelacije; smanjiti fokus.";
  return "Nizak doprinos i ogranicen signal rasta; kandidat za smanjenje fokusa.";
}

function buildStatusTooltip(data: StatusTooltipData): string {
  return `${data.status}: ${data.statusReason} | Udeo ${fmtPct(data.sharePct, 1)} | Trend ${fmtSignedPct(data.trendPct, 1)} | Delta ${fmtRsd(data.changeRevenue)} | Pouzdanost ${fmtPct(data.reliabilityPct, 0)}`;
}

function normalizeName(value: string | null | undefined): string {
  return (value ?? "").trim().toUpperCase();
}

function vendorKey(vendor: { vendorId: number | null; vendorName: string }): string {
  if (vendor.vendorId != null) return `id:${vendor.vendorId}`;
  return `name:${normalizeName(vendor.vendorName)}`;
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
  const [activeFilters, setActiveFilters] = useState<ActiveFilters>({
    fromDate: initialRange.fromDate,
    toDate: initialRange.toDate,
    vendorId: null,
    category: "",
  });

  const [vendors, setVendors] = useState<Dobavljac[]>([]);
  const [data, setData] = useState<VendorSalesNivelacijaResponse | null>(null);
  const [previousData, setPreviousData] = useState<VendorSalesNivelacijaResponse | null>(null);
  const [previousRevenue, setPreviousRevenue] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sortField, setSortField] = useState<SortField>("status");
  const [sortDir, setSortDir] = useState<SortDir>("desc");
  const [expandedVendorKey, setExpandedVendorKey] = useState<string | null>(null);
  const [focusFilter, setFocusFilter] = useState<FocusFilter>("all");

  const invalidRange = useMemo(() => {
    if (!fromDate || !toDate) return false;
    return new Date(fromDate) > new Date(toDate);
  }, [fromDate, toDate]);

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

  const load = useCallback(async (filters: ActiveFilters) => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const currentRange = toUtcRange(filters.fromDate, filters.toDate);
      const previousRange = buildPreviousRange(filters.fromDate, filters.toDate);

      const [currentResult, previousResult] = await Promise.allSettled([
        getVendorSalesNivelacija({
          ...currentRange,
          vendorId: filters.vendorId,
          category: filters.category || null,
          includeInactive: false,
        }),
        getVendorSalesNivelacija({
          ...previousRange,
          vendorId: filters.vendorId,
          category: filters.category || null,
          includeInactive: false,
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
      } else {
        setPreviousData(null);
        setPreviousRevenue(null);
      }
    } catch (reason) {
      if (requestId !== requestIdRef.current) return;
      setData(null);
      setPreviousData(null);
      setPreviousRevenue(null);
      setError(reason instanceof Error ? reason.message : "Greska pri ucitavanju pre/post analitike.");
    } finally {
      if (requestId === requestIdRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    void load(activeFilters);
  }, [activeFilters, load]);

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
    const topShare = rows.reduce((max, item) => {
      const share = totalRevenue > 0 ? (item.postRevenue / totalRevenue) * 100 : 0;
      return Math.max(max, share);
    }, 0);

    const deltaValues = rows.map((item) => item.changeRevenue);
    const minDelta = Math.min(...deltaValues);
    const maxDelta = Math.max(...deltaValues);
    const deltaSpan = maxDelta - minDelta;
    const avgShare = rows.length > 0 ? 100 / rows.length : 0;

    return rows.map((item) => {
      const sharePct = totalRevenue > 0 ? (item.postRevenue / totalRevenue) * 100 : 0;
      const trendPct = item.changePercent;
      const coveragePct = item.articleCount > 0 ? (item.activeArticlesCount / item.articleCount) * 100 : 0;
      const knownSupplier = !UNKNOWN_SUPPLIERS.has(normalizeName(item.vendorName));
      const reliabilityPct = clamp(coveragePct * 0.7 + (knownSupplier ? 30 : 0), 0, 100);
      const previousPostRevenue = previousRevenueByVendorKey.get(vendorKey(item)) ?? null;
      const confidence = buildConfidenceMeta(reliabilityPct);
      const volatility = buildVolatilityMeta(item.postRevenue, previousPostRevenue);

      const shareNorm = topShare > 0 ? clamp((sharePct / topShare) * 100, 0, 100) : 0;
      const deltaNorm = deltaSpan > 0 ? clamp(((item.changeRevenue - minDelta) / deltaSpan) * 100, 0, 100) : 50;
      const trendNorm = clamp(((trendPct + 50) / 100) * 100, 0, 100);

      const decisionScore = Math.round(
        shareNorm * 0.35 +
        deltaNorm * 0.30 +
        trendNorm * 0.20 +
        reliabilityPct * 0.15
      );

      let status: DecisionStatus = "Smanji";
      if (decisionScore >= BOOST_SCORE_THRESHOLD) status = "Pojacaj";
      else if (decisionScore >= KEEP_SCORE_THRESHOLD) status = "Zadrzi";

      if (reliabilityPct < BOOST_MIN_RELIABILITY_PCT && status === "Pojacaj") {
        status = "Zadrzi";
      }

      const statusReason = buildStatusReason(status, {
        sharePct,
        avgShare,
        trendPct,
        changeRevenue: item.changeRevenue,
        reliabilityPct,
      });

      return {
        ...item,
        sharePct,
        trendPct,
        reliabilityPct,
        decisionScore,
        status,
        statusReason,
        confidenceLabel: confidence.label,
        confidenceTone: confidence.tone,
        previousPostRevenue,
        volatilityPct: volatility.pct,
        volatilityLabel: volatility.label,
        volatilityTone: volatility.tone,
      };
    });
  }, [data?.vendorStats, previousRevenueByVendorKey]);

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
        compare = a.decisionScore - b.decisionScore;
      }

      return sortDir === "asc" ? compare : -compare;
    });
  }, [decisionRows, sortDir, sortField]);

  const focusedRows = useMemo(() => {
    return sortedRows.filter((row) => focusFilterMatches(row, focusFilter));
  }, [focusFilter, sortedRows]);

  const totalRevenue = data?.totals.postRevenue ?? 0;
  const top5SharePct = useMemo(() => {
    if (sortedRows.length === 0 || totalRevenue <= 0) return 0;
    const top5 = [...sortedRows]
      .sort((a, b) => b.postRevenue - a.postRevenue)
      .slice(0, 5)
      .reduce((sum, item) => sum + item.postRevenue, 0);
    return (top5 / totalRevenue) * 100;
  }, [sortedRows, totalRevenue]);

  const totalChangeRevenue = data?.totals.changeRevenue ?? 0;
  const periodGrowthPct = useMemo(() => {
    if (previousRevenue == null || previousRevenue <= 0) return data?.totals.changePercent ?? null;
    return ((totalRevenue - previousRevenue) / previousRevenue) * 100;
  }, [data?.totals.changePercent, previousRevenue, totalRevenue]);

  const vendorCounts = useMemo(() => {
    const boost = sortedRows.filter((row) => row.status === "Pojacaj").length;
    const keep = sortedRows.filter((row) => row.status === "Zadrzi").length;
    const reduce = sortedRows.filter((row) => row.status === "Smanji").length;
    return { boost, keep, reduce };
  }, [sortedRows]);

  const focusFilterCounts = useMemo(() => {
    return {
      all: sortedRows.length,
      boost: sortedRows.filter((row) => row.status === "Pojacaj").length,
      keep: sortedRows.filter((row) => row.status === "Zadrzi").length,
      reduce: sortedRows.filter((row) => row.status === "Smanji").length,
      lowConfidence: sortedRows.filter((row) => row.confidenceTone === "weak").length,
      volatile: sortedRows.filter((row) => focusFilterMatches(row, "volatile")).length,
    } satisfies Record<FocusFilter, number>;
  }, [sortedRows]);

  const dataQualityWarnings = useMemo(() => parseMetricsStatus(data?.metricsStatus), [data?.metricsStatus]);

  const dataTrustSummary = useMemo(() => {
    const analyzedShare = data?.dataQuality.analyzedSharePercent ?? 0;
    const duplicateRows = data?.dataQuality.duplicateRowsRemoved ?? 0;
    const inactiveRows = data?.dataQuality.inactiveRows ?? 0;

    if (analyzedShare >= 70 && dataQualityWarnings.length === 0) {
      return {
        label: "Visoko poverenje",
        tone: "strong" as const,
        details: `Analizirano ${fmtPct(analyzedShare, 0)} redova | duplicati ${duplicateRows} | neaktivni ${inactiveRows}`,
      };
    }

    if (analyzedShare >= 45) {
      return {
        label: "Srednje poverenje",
        tone: "watch" as const,
        details: `Analizirano ${fmtPct(analyzedShare, 0)} redova | duplicati ${duplicateRows} | neaktivni ${inactiveRows}`,
      };
    }

    return {
      label: "Nisko poverenje",
      tone: "weak" as const,
      details: `Analizirano ${fmtPct(analyzedShare, 0)} redova | duplicati ${duplicateRows} | neaktivni ${inactiveRows}`,
    };
  }, [data?.dataQuality.analyzedSharePercent, data?.dataQuality.duplicateRowsRemoved, data?.dataQuality.inactiveRows, dataQualityWarnings.length]);

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
      { label: "Momentum", value: fmtOptionalRsd(data?.avgMomentumRevenue), hint: "avg rev" },
      { label: "Elasticnost", value: fmtMetric(data?.avgElasticity, 2), hint: "avg" },
      { label: "DID", value: fmtOptionalRsd(data?.avgDidRevenue), hint: "avg rev" },
      { label: "Lost sales OOS", value: fmtOptionalRsd(data?.avgLostSalesOOS), hint: "avg" },
    ],
    [data?.avgDidRevenue, data?.avgElasticity, data?.avgLostSalesOOS, data?.avgMomentumRevenue]
  );

  const concentrationData = useMemo(() => {
    if (focusedRows.length === 0) return [] as ConcentrationDatum[];

    const top = [...focusedRows]
      .sort((a, b) => b.sharePct - a.sharePct)
      .slice(0, 7)
      .map((row) => ({
        name: row.vendorName,
        sharePct: row.sharePct,
        vendorKey: vendorKey(row),
        selected: expandedVendorKey === vendorKey(row),
      }));

    const topShare = top.reduce((sum, row) => sum + row.sharePct, 0);
    const rest = clamp(100 - topShare, 0, 100);

    return rest > 0.1
      ? [...top, { name: "Ostali", sharePct: rest, vendorKey: null, selected: false }]
      : top;
  }, [expandedVendorKey, focusedRows]);

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
        label: "Dobavljac",
        value: activeFilters.vendorId != null
          ? vendors.find((vendor) => vendor.id === activeFilters.vendorId)?.naziv ?? activeFilters.vendorId
          : "Svi",
      },
      { key: "category", label: "Kategorija", value: activeFilters.category },
      { key: "focusFilter", label: "Brzi fokus", value: focusFilterLabel(focusFilter) },
    ],
    [activeFilters.category, activeFilters.fromDate, activeFilters.toDate, activeFilters.vendorId, focusFilter, periodPreset, vendors]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
      { key: "vendorsCount", label: "Dobavljaca", value: data?.totals.vendorsCount ?? 0 },
      { key: "articlesCount", label: "Artikala", value: data?.totals.articlesCount ?? 0 },
      { key: "windowDays", label: "Window", value: data?.windowDays ?? 0 },
      { key: "rowsExported", label: "Vidljivih redova", value: focusedRows.length },
      { key: "dataTrust", label: "Poverenje", value: dataTrustSummary.label },
      { key: "analyzedShare", label: "Analizirani redovi", value: fmtPct(data?.dataQuality.analyzedSharePercent, 0) },
      { key: "duplicateRowsRemoved", label: "Duplicati uklonjeni", value: data?.dataQuality.duplicateRowsRemoved ?? 0 },
      { key: "inactiveRows", label: "Neaktivni redovi", value: data?.dataQuality.inactiveRows ?? 0 },
      { key: "metricsStatus", label: "Metrics status", value: data?.metricsStatus ?? "OK" },
    ],
    [
      data?.dataQuality.analyzedSharePercent,
      data?.dataQuality.duplicateRowsRemoved,
      data?.dataQuality.inactiveRows,
      data?.generatedAt,
      data?.metricsStatus,
      data?.totals.articlesCount,
      data?.totals.vendorsCount,
      data?.windowDays,
      dataTrustSummary.label,
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
    });
  };

  const handleResetFilters = () => {
    const range = getPresetRange("30d");
    setPeriodPreset("30d");
    setFromDate(range.fromDate);
    setToDate(range.toDate);
    setVendorId(null);
    setCategory("");
    setFocusFilter("all");
    setActiveFilters({
      fromDate: range.fromDate,
      toDate: range.toDate,
      vendorId: null,
      category: "",
    });
  };

  const openVendorDetail = (row: DecisionVendor) => {
    saveAnalyticsDetailSnapshot(
      buildAnalyticsDetailSnapshot({
        table: "nivelacije-pre-post",
        recordId: String(row.vendorId ?? row.vendorName),
        title: row.vendorName,
        subtitle: "Decision support po dobavljacu",
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
      <header className="ppn-decision-header">
        <div>
          <h1 className="ppn-decision-title">Prodaja Pre/Post Nivelacije</h1>
          <p className="ppn-decision-subtitle">
            Fokusiran ekran za brzu odluku: koji dobavljaci posle nivelacije realno dobijaju promet,
            koje treba pojacati, a koje smanjiti.
          </p>
        </div>
        <div className="ppn-decision-generated">
          Generisano: {data?.generatedAt ? new Date(data.generatedAt).toLocaleString("sr-RS") : "-"}
        </div>
      </header>

      <section className="ppn-decision-filters">
        <label className="ppn-decision-field">
          <span>Period</span>
          <select value={periodPreset} onChange={(e) => handlePresetChange(e.target.value as PeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="180d">Poslednjih 180 dana</option>
            <option value="365d">Poslednjih 365 dana</option>
            <option value="custom">Prilagođeno</option>
          </select>
        </label>

        <label className="ppn-decision-field">
          <span>Od</span>
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
        </label>

        <label className="ppn-decision-field">
          <span>Do</span>
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </label>

        <label className="ppn-decision-field">
          <span>Dobavljac</span>
          <select
            value={vendorId ?? ""}
            onChange={(e) => setVendorId(e.target.value ? Number(e.target.value) : null)}
          >
            <option value="">Svi</option>
            {vendors.map((vendor) => (
              <option key={vendor.id} value={vendor.id}>{vendor.naziv}</option>
            ))}
          </select>
        </label>

        <label className="ppn-decision-field">
          <span>Kategorija</span>
          <select value={category} onChange={(e) => setCategory(e.target.value)}>
            <option value="">Sve</option>
            {(data?.categories ?? []).map((item) => (
              <option key={item} value={item}>{item}</option>
            ))}
          </select>
        </label>

        <div className="ppn-decision-actions">
          <button type="button" onClick={handleApplyFilters} disabled={loading || invalidRange}>Primeni</button>
          <button type="button" className="secondary" onClick={handleResetFilters} disabled={loading}>Reset</button>
        </div>
      </section>

      {invalidRange ? <div className="ppn-decision-message error">Datum od ne moze biti posle datuma do.</div> : null}
      {error ? <div className="ppn-decision-message error">{error}</div> : null}
      {loading ? <div className="ppn-decision-message loading">Ucitavam pre/post signal po dobavljacima...</div> : null}

      {!loading && data ? (
        <>
          <section className="ppn-decision-signals">
            <article className="ppn-decision-card ppn-signal-card">
              <div className="ppn-card-topline">
                <h2>Poverenje u signal</h2>
                <span className={confidenceClass(dataTrustSummary.tone)}>{dataTrustSummary.label}</span>
              </div>
              <p>{dataTrustSummary.details}</p>
              {dataQualityWarnings.length > 0 ? (
                <div className="ppn-chip-wrap">
                  {dataQualityWarnings.slice(0, 4).map((warning) => (
                    <span key={warning} className="ppn-signal-pill signal-watch">{warning}</span>
                  ))}
                </div>
              ) : (
                <div className="ppn-chip-wrap">
                  <span className="ppn-signal-pill signal-strong">Bez aktivnih upozorenja</span>
                  <span className="ppn-signal-pill signal-neutral">Rows {data.dataQuality.analyzedRows}/{data.dataQuality.rawRows}</span>
                </div>
              )}
            </article>

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
                  ? `${leadingPriceDirection.segment} trenutno nosi najveci doprinos promeni prometa.`
                  : "Nema dovoljno price-direction signala za izabrani opseg."}
              </p>
              <div className="ppn-stat-pair">
                <strong>{leadingPriceDirection ? fmtRsd(leadingPriceDirection.changeRevenue) : "N/A"}</strong>
                <span>{leadingPriceDirection ? fmtSignedPct(leadingPriceDirection.avgPriceChangePercent, 1) : "N/A"}</span>
              </div>
            </article>

            <article className="ppn-decision-card ppn-signal-card">
              <div className="ppn-card-topline">
                <h2>Napredni signali</h2>
                <span className="ppn-signal-pill signal-neutral">Avg</span>
              </div>
              <div className="ppn-mini-metrics">
                {advancedSignals.map((item) => (
                  <article key={item.label}>
                    <span>{item.label}</span>
                    <strong>{item.value}</strong>
                    <small>{item.hint}</small>
                  </article>
                ))}
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

          <section className="ppn-decision-kpis">
            <article className="ppn-decision-kpi">
              <span>Ukupan promet posle nivelacije</span>
              <strong>{fmtRsd(totalRevenue)}</strong>
            </article>
            <article className="ppn-decision-kpi">
              <span>Udeo top 5 dobavljaca</span>
              <strong>{fmtPct(top5SharePct)}</strong>
            </article>
            <article className="ppn-decision-kpi">
              <span>Ukupna promena prometa</span>
              <strong className={trendClass(totalChangeRevenue)}>{fmtRsd(totalChangeRevenue)}</strong>
            </article>
            <article className="ppn-decision-kpi">
              <span>Rast/PAD vs prethodni period</span>
              <strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong>
            </article>
          </section>

          <section className="ppn-decision-panels">
            <article className="ppn-decision-card">
              <h2>Koncentracija promena po dobavljacima</h2>
              <p>Udeo post-prometa po dobavljacu za brzu procenu koncentracije.</p>
              {concentrationData.length > 0 ? (
                <div className="ppn-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={concentrationData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }} onClick={handleChartClick}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} label={{ value: null }} />
                      <Tooltip content={<CustomConcentrationTooltip />} />
                      <Bar dataKey="sharePct" radius={[0, 8, 8, 0]}>
                        {concentrationData.map((entry) => (
                          <Cell
                            key={`${entry.name}-${entry.vendorKey ?? "rest"}`}
                            fill={entry.selected ? "var(--accent-secondary)" : entry.vendorKey ? "var(--accent-primary)" : "var(--border-strong)"}
                            onClick={() => handleCellClick(entry)}
                            style={{ cursor: entry.vendorKey ? "pointer" : "default" }}
                          />
                        ))}
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="ppn-decision-empty">Nema podataka za grafikon koncentracije.</div>
              )}
              <div className="ppn-chart-hint">Klik na traku otvara detalj dobavljaca u tabeli.</div>
            </article>

            <article className="ppn-decision-card">
              <div className="ppn-decision-table-head">
                <div>
                  <h2>Prioritetna lista dobavljaca</h2>
                  <p>
                    Pojacaj: {vendorCounts.boost} | Zadrzi: {vendorCounts.keep} | Smanji: {vendorCounts.reduce}
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

              <div className="ppn-chip-wrap ppn-focus-filters">
                {(["all", "boost", "keep", "reduce", "lowConfidence", "volatile"] as FocusFilter[]).map((item) => (
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
                          Dobavljac{sortMarker("vendorName", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("postRevenue")}>
                          Promet posle{sortMarker("postRevenue", sortField, sortDir)}
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("sharePct")}>
                          Udeo{sortMarker("sharePct", sortField, sortDir)}
                        </button>
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
                      </th>
                      <th className="align-center">
                        <button type="button" onClick={() => handleSort("volatilityPct")}>
                          Volatilnost{sortMarker("volatilityPct", sortField, sortDir)}
                        </button>
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>
                          Preporuka{sortMarker("status", sortField, sortDir)}
                        </button>
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
                                <strong title={row.vendorName || "Nepoznat dobavljac"}>{row.vendorName || "Nepoznat dobavljac"}</strong>
                                <div className="ppn-chip-wrap">
                                  <span className={confidenceClass(row.confidenceTone)}>{row.confidenceLabel} signal</span>
                                  <span className="ppn-signal-pill signal-neutral">{row.activeArticlesCount}/{row.articleCount} aktivno</span>
                                </div>
                              </div>
                            </td>
                            <td className="align-right">{fmtRsd(row.postRevenue)}</td>
                            <td className="align-right">{fmtPct(row.sharePct, 2)}</td>
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
                                {row.status}
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
            </article>
          </section>

          {selectedRow ? (
            <section className="ppn-decision-detail">
              <div className="ppn-decision-detail-head">
                <h3 title={selectedRow.vendorName || "Nepoznat dobavljac"}>Detalj odluke: {selectedRow.vendorName || "Nepoznat dobavljac"}</h3>
                <button type="button" onClick={() => openVendorDetail(selectedRow)}>
                  Otvori puni detalj
                </button>
              </div>

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
                  <span>Pouzdanost signala</span>
                  <strong>{fmtPct(selectedRow.reliabilityPct, 1)}</strong>
                </article>
                <article>
                  <span>Volatilnost vs prethodni period</span>
                  <strong>{selectedRow.volatilityPct == null ? selectedRow.volatilityLabel : fmtSignedPct(selectedRow.volatilityPct, 1)}</strong>
                </article>
                <article>
                  <span>SKU sa dizanjem cene</span>
                  <strong>{selectedRow.increasedPriceArticlesCount}</strong>
                </article>
                <article>
                  <span>SKU sa snizenjem cene</span>
                  <strong>{selectedRow.decreasedPriceArticlesCount}</strong>
                </article>
                <article>
                  <span>Decision score</span>
                  <strong>{selectedRow.decisionScore}</strong>
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
                    <strong>{fmtOptionalRsd(selectedDriverSummary.avgMomentumRevenue)}</strong>
                    <small>Elasticnost {fmtMetric(selectedDriverSummary.avgElasticity, 2)}</small>
                  </article>
                  <article>
                    <span>DID / Lost sales OOS</span>
                    <strong>{fmtOptionalRsd(selectedDriverSummary.avgDidRevenue)}</strong>
                    <small>Lost sales {fmtOptionalRsd(selectedDriverSummary.avgLostSalesOOS)}</small>
                  </article>
                  <article>
                    <span>Najcesci metric reason</span>
                    <strong>{selectedDriverSummary.topMetricReasons[0] ?? "N/A"}</strong>
                    <small>{selectedDriverSummary.topMetricReasons.slice(1).join(" | ") || "Bez dodatnih upozorenja"}</small>
                  </article>
                </div>
              ) : null}

              <p className="ppn-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedRow.statusReason}
              </p>
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}
