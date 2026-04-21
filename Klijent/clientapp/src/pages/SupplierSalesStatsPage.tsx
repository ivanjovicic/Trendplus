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
  type SupplierSalesStat,
  type SupplierSalesStatsResponse,
} from "../services/supplierSalesStatsApi";
import type { StoreOption } from "../types/analytics";
import AnalyticsUnknownLink from "../components/analytics/AnalyticsUnknownLink";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import InfoTip from "../components/ui/InfoTip";
import UltraSpinner from "../components/ui/UltraSpinner";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { getDataScope } from "../utils/dataScope";
import { CHART_TOOLTIP_STYLE, CHART_TOOLTIP_LABEL_STYLE } from "../utils/chartTooltipStyle";
import { qualityTierIcon, qualityTierClass, tierNeedsWarning, buildCoverageTooltip, buildRecommendationCaveat, buildMarginDetailNote, buildSnapshotBadgeLabel, buildSnapshotTooltip } from "../utils/marginQuality";
import "./SupplierSalesStatsPage.css";

type PeriodPreset = "30d" | "90d" | "custom";
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
  splitCoveragePct: number;
  confidencePct: number;
  status: DecisionStatus;
  statusLabel: string;
  statusReason: string;
  reasonCodes: string[];
  dataQualityStatus: "good" | "warning" | "critical";
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
  { key: "marginContribution", header: "Maržni doprinos", dataType: "currency" },
  { key: "marginPct", header: "Marža %", dataType: "percent" },
  { key: "marginQualityLabel", header: "Kvalitet marže", dataType: "text" },
  { key: "shareOfMarginContribution", header: "Udeo maržnog doprinosa %", dataType: "percent" },
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

  return {
    fromDate: toDateInput(from),
    toDate: toDateInput(to),
  };
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

function formatDate(value: string | null | undefined): string {
  if (!value) return "-";
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return parsed.toLocaleDateString("sr-RS");
}

function fmtRsd(value: number): string {
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 0, maximumFractionDigits: 0 })} RSD`;
}

function fmtPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  return `${value.toLocaleString("sr-RS", { minimumFractionDigits: digits, maximumFractionDigits: digits })}%`;
}

function fmtSignedPct(value: number | null | undefined, digits = 1): string {
  if (value == null || Number.isNaN(value)) return "N/A";
  const sign = value > 0 ? "+" : "";
  return `${sign}${fmtPct(value, digits)}`;
}

function fmtQty(value: number): string {
  return `${value.toLocaleString("sr-RS")} kom`;
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
  if (field !== activeField) return "";
  return dir === "asc" ? " ^" : " v";
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
  if (status === "do_not_trust") return "Smanji";
  return "Nedovoljno podataka";
}

function statusLabelSr(status: DecisionStatus): string {
  if (status === "increase_focus") return "Pojačaj";
  if (status === "maintain") return "Zadrži";
  if (status === "review") return "Oprez";
  if (status === "do_not_trust") return "Smanji";
  return "N/A";
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
  confidencePct: number;
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
  const reasons = data.reasonCodes.length > 0
    ? data.reasonCodes.map(formatReasonCode).join(", ")
    : "Nema dodatnih napomena";
  return `${data.statusLabel}: ${data.statusReason} | Udeo ${fmtPct(data.sharePct, 1)} | Marža ${fmtPct(data.marginPct, 1)} | PoP ${popText} | Nivelacija impact ${impactText} | Split pokrivanje ${fmtPct(data.splitCoveragePct, 1)} | Pouzdanost ${fmtPct(data.reliabilityPct, 0)} | Sigurnost ${fmtPct(data.confidencePct, 0)} | Razlozi: ${reasons}`;
}

function describePopMetric(supplier: SupplierSalesStat): { label: string; title: string; className: string } {
  if (supplier.popRevenueChangePct != null && !Number.isNaN(supplier.popRevenueChangePct)) {
    return {
      label: fmtSignedPct(supplier.popRevenueChangePct, 2),
      title: `PoP trend poredi ukupan promet sa prethodnim uporedivim periodom. Prethodni period: ${fmtRsd(supplier.previousPeriodRevenue ?? 0)}.`,
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
    const noteSuffix = supplier.prePostSignalNote ? ` Napomena: ${supplier.prePostSignalNote}` : "";
    return {
      label: fmtSignedPct(supplier.prePostNivelacijaRevenueImpactPct, 2),
      title: `Pre/post nivelacija impact meri promenu prometa samo na uporedivim artiklima sa prodajom i pre i posle prve nivelacije. Pokrice: ${fmtPct(supplier.prePostNivelacijaRevenueCoveragePct, 1)} prometa.${noteSuffix}`,
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
      title: "PoP promena kolicine prema prethodnom uporedivom periodu.",
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
    title: "PoP promena kolicine nije dostupna.",
    className: "trend-neutral",
  };
}

function describeNivelacijaUnitsImpactMetric(supplier: SupplierSalesStat): { label: string; title: string; className: string } {
  if (supplier.prePostNivelacijaUnitsImpactPct != null && !Number.isNaN(supplier.prePostNivelacijaUnitsImpactPct)) {
    const noteSuffix = supplier.prePostSignalNote ? ` Napomena: ${supplier.prePostSignalNote}` : "";
    return {
      label: fmtSignedPct(supplier.prePostNivelacijaUnitsImpactPct, 2),
      title: `Pre/post promena količine unutar uporedivih artikala sa prodajom i pre i posle prve nivelacije.${noteSuffix}`,
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

export default function SupplierSalesStatsPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const requestIdRef = useRef(0);
  const detailSectionRef = useRef<HTMLElement>(null);

  const initialRange = useMemo(() => getPresetRange("30d"), []);
  const initialQueryFilters = useMemo(() => {
    const queryFromDate = parseDateInputOrDefault(searchParams.get("fromDate"), initialRange.fromDate);
    const queryToDate = parseDateInputOrDefault(searchParams.get("toDate"), initialRange.toDate);
    const querySezonaId = parseNullableInt(searchParams.get("sezonaId"));
    const queryStoreId = parseNullableInt(searchParams.get("storeId"));
    const hasExplicitDateQuery = searchParams.has("fromDate") || searchParams.has("toDate");
    const periodPreset: PeriodPreset = hasExplicitDateQuery ? "custom" : "30d";

    return {
      periodPreset,
      fromDate: queryFromDate,
      toDate: queryToDate,
      sezonaId: querySezonaId,
      storeId: queryStoreId,
    };
  }, [initialRange.fromDate, initialRange.toDate, searchParams]);
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
    () => searchParams.get("dataScope") || getDataScope(),
    [searchParams]
  );
  const includeUnknown = useMemo(
    () => (searchParams.get("includeUnknown") ?? "true").toLowerCase() !== "false",
    [searchParams]
  );
  const focus = useMemo(() => searchParams.get("focus") ?? "", [searchParams]);
  const focusSupplierId = useMemo(() => searchParams.get("supplierId"), [searchParams]);

  const invalidRange = useMemo(() => {
    if (!fromDate || !toDate) return false;
    return new Date(fromDate) > new Date(toDate);
  }, [fromDate, toDate]);

  useEffect(() => {
    const queryFromDate = parseDateInputOrDefault(searchParams.get("fromDate"), activeFilters.fromDate);
    const queryToDate = parseDateInputOrDefault(searchParams.get("toDate"), activeFilters.toDate);
    const querySezonaId = parseNullableInt(searchParams.get("sezonaId"));
    const queryStoreId = parseNullableInt(searchParams.get("storeId"));
    const hasExplicitDateQuery = searchParams.has("fromDate") || searchParams.has("toDate");
    const queryPreset: PeriodPreset = hasExplicitDateQuery ? "custom" : "30d";

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
  }, [activeFilters.fromDate, activeFilters.sezonaId, activeFilters.storeId, activeFilters.toDate, searchParams]);

  useEffect(() => {
    const loadStores = async () => {
      try {
        const items = await getStores(true);
        setStores(items);
      } catch {
        setStores([]);
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
      const statusLabel = recommended?.label ?? displayStatusLabel(status);
      const statusReason = recommended?.summary
        ?? (supplier.isUnknown
          ? "Dobavljač je nepoznat u master podacima; signal nije pouzdan za odluku."
          : "Nedovoljno podataka za pouzdanu preporuku.");
      const confidencePct = recommended?.confidencePct ?? 0;
      const reasonCodes = recommended?.reasonCodes ?? [];
      const dataQualityStatus = recommended?.dataQualityStatus ?? "warning";
      const reliabilityPct = recommended?.reliabilityPct
        ?? supplier.reliabilityPct
        ?? supplier.marginDataCoveragePct
        ?? 0;

      return {
        ...supplier,
        sharePct,
        totalCost,
        shareOfMarginContribution,
        shareOfUnits,
        reliabilityPct,
        splitCoveragePct,
        confidencePct,
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
    () => (includeUnknown ? sortedSuppliers : sortedSuppliers.filter((row) => !row.isUnknown)),
    [includeUnknown, sortedSuppliers]
  );

  const selectedSupplier = useMemo(
    () => visibleSuppliers.find((row) => supplierKey(row) === expandedSupplierKey) ?? null,
    [expandedSupplierKey, visibleSuppliers]
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

  const qualityNotes = useMemo(() => {
    if (!data) return [] as string[];

    const notes: string[] = [];
    const splitCoverage = data.dataQuality.revenueWithNivelacijaSplitSharePct;
    const missingCostShare = data.dataQuality.missingCostRevenueSharePct;
    const historicalCostShare = missingCostShare == null ? null : Math.max(0, 100 - missingCostShare);
    const estimatedCostShare = data.dataQuality.estimatedCostRevenueSharePct;
    const unknownShare = data.dataQuality.unknownSupplierRevenueSharePct;

    if (splitCoverage != null && splitCoverage < 60) {
      notes.push(`Uporediv pre/posle signal trenutno pokriva ${fmtPct(splitCoverage, 1)} ukupnog prometa, pa ga treba citati kao delimican.`);
    }

    if (historicalCostShare != null && historicalCostShare < 100) {
      notes.push(`Istorijska nabavna cena postoji za ${fmtPct(historicalCostShare, 1)} prometa; marza za ostatak nije istorijski potvrdena na prodajnoj stavci.`);
    }

    if (estimatedCostShare != null && estimatedCostShare > 0) {
      notes.push(`Za ${fmtPct(estimatedCostShare, 1)} prometa marza je procenjena iz fallback troska artikla, pa je treba citati oprezno.`);
    }

    if (unknownShare != null && unknownShare > 0) {
      notes.push(`Nepoznati/N-A dobavljači učestvuju sa ${fmtPct(unknownShare, 1)} ukupnog prometa.`);
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
      { key: "dataScope", label: "Opseg podataka", value: activeDataScope },
      { key: "includeUnknown", label: "Uključi nepoznate", value: includeUnknown ? "da" : "ne" },
    ],
    [activeDataScope, activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, activeSezonaLabel, includeUnknown]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
      { key: "suppliers", label: "Dobavljaca", value: data?.totals.brojDobavljaca ?? 0 },
      { key: "unknownSuppliers", label: "Nepoznato/N-A", value: unknownSuppliers.length },
      { key: "marginCoverage", label: "Pokrice istorijskog troska %", value: fmtPct(data?.dataQuality.missingCostRevenueSharePct == null ? null : 100 - data.dataQuality.missingCostRevenueSharePct, 1) },
      { key: "fallbackCoverage", label: "Promet procenjen iz fallback troska %", value: fmtPct(data?.dataQuality.estimatedCostRevenueSharePct, 1) },
      { key: "noCostCoverage", label: "Promet bez troska %", value: fmtPct(data?.dataQuality.missingCostRevenueSharePct, 1) },
      { key: "totalsPopTrend", label: "Ukupan PoP trend", value: fmtPct(data?.totals.popRevenueChangePct, 1) },
      { key: "totalsPrePostImpact", label: "Ukupan nivelacija uticaj", value: fmtPct(data?.totals.prePostNivelacijaRevenueImpactPct, 1) },
      { key: "splitCoverage", label: "Uporedivo pre/post pokrivanje", value: fmtPct(data?.dataQuality.revenueWithNivelacijaSplitSharePct, 1) },
      { key: "snapshotCoverage", label: "Snapshot trosak pokrice %", value: fmtPct(data?.totals.snapshotCostCoveragePct, 1) },
      { key: "isSnapshotActive", label: "Snapshot aktivan", value: data?.totals.isSnapshotActive ? "da" : "ne" },
      { key: "increaseFocus", label: "Pojačaj fokus", value: supplierCounts.increaseFocus },
      { key: "maintain", label: "Zadrži", value: supplierCounts.maintain },
      { key: "review", label: "U pregledu", value: supplierCounts.review },
      { key: "doNotTrust", label: "Smanji", value: supplierCounts.doNotTrust },
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
    params.set("returnTo", `/analytics/supplier-sales-stats?${returnParams.toString()}`);
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

  const handleApplyFilters = () => {
    if (invalidRange) {
      setError("Datum od ne moze biti posle datuma do.");
      return;
    }

    const nextFilters: ActiveFilters = {
      fromDate,
      toDate,
      sezonaId,
      storeId,
    };
    setActiveFilters(nextFilters);

    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("fromDate", nextFilters.fromDate);
    nextParams.set("toDate", nextFilters.toDate);
    if (nextFilters.sezonaId != null) nextParams.set("sezonaId", String(nextFilters.sezonaId));
    else nextParams.delete("sezonaId");
    if (nextFilters.storeId != null) nextParams.set("storeId", String(nextFilters.storeId));
    else nextParams.delete("storeId");
    nextParams.set("dataScope", activeDataScope);
    nextParams.set("includeUnknown", includeUnknown ? "true" : "false");
    if (focus) nextParams.set("focus", focus);
    else nextParams.delete("focus");
    nextParams.delete("supplierId");
    setSearchParams(nextParams, { replace: true });
  };

  const handleResetFilters = () => {
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

    const nextParams = new URLSearchParams(searchParams);
    nextParams.set("fromDate", range.fromDate);
    nextParams.set("toDate", range.toDate);
    nextParams.delete("sezonaId");
    nextParams.delete("storeId");
    nextParams.set("dataScope", activeDataScope);
    nextParams.set("includeUnknown", includeUnknown ? "true" : "false");
    if (focus) nextParams.set("focus", focus);
    else nextParams.delete("focus");
    nextParams.delete("supplierId");
    setSearchParams(nextParams, { replace: true });
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

  return (
    <div className="supplier-decision-page">
      <header className="supplier-decision-header">
        <div>
          <h1 className="supplier-decision-title">Prodaja po dobavljačima</h1>
          <p className="supplier-decision-subtitle">
            Decision-support pregled za izbor dobavljača: fokus na promet, doprinos i akciju.
          </p>
        </div>
        {data?.generatedAt ? (
          <div className="supplier-decision-generated">
            Generisano: {new Date(data.generatedAt).toLocaleString("sr-RS")}
          </div>
        ) : null}
      </header>

      <section className="supplier-decision-filters">
        <label className="supplier-decision-field">
          <span>Period</span>
          <select value={periodPreset} onChange={(event) => applyPreset(event.target.value as PeriodPreset)}>
            <option value="30d">Poslednjih 30 dana</option>
            <option value="90d">Poslednjih 90 dana</option>
            <option value="custom">Prilagodjeno</option>
          </select>
        </label>

        <label className="supplier-decision-field">
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

        <label className="supplier-decision-field">
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

        <label className="supplier-decision-field">
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

        <label className="supplier-decision-field">
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

        <label className="supplier-decision-field supplier-decision-field-checkbox">
          <span>Prikazi unknown</span>
          <input
            type="checkbox"
            checked={includeUnknown}
            onChange={(event) => handleIncludeUnknownChange(event.target.checked)}
          />
        </label>

        <div className="supplier-decision-actions">
          <button type="button" onClick={handleApplyFilters} disabled={loading}>
            Primeni
          </button>
          <button type="button" className="secondary" onClick={handleResetFilters} disabled={loading}>
            Reset
          </button>
        </div>
      </section>

      {invalidRange ? (
        <div className="supplier-decision-message error">Datum od ne moze biti posle datuma do.</div>
      ) : null}
      {error ? <div className="supplier-decision-message error">{error}</div> : null}
      {loading && !data ? (
        <div className="supplier-decision-loading" role="status" aria-live="polite">
          <UltraSpinner size="md" label="Učitavam podatke o dobavljačima" />
          <span>Učitavam podatke o dobavljačima...</span>
        </div>
      ) : null}
      {!loading && !error && emptyStateHint ? (
        <div className="supplier-decision-message info">{emptyStateHint}</div>
      ) : null}
      {!loading && !error && qualityNotes.length > 0 ? (
        <div className="supplier-decision-message info">
          <strong>Kvalitet podataka:</strong> {qualityNotes.join(" ")}
          <div className="supplier-decision-quality-actions">
            <Link
              to={`/analytics/data-quality?${dataQualityContextQuery}`}
              className="supplier-decision-quality-link"
            >
              Otvori Data Quality centar
            </Link>
            {unknownSuppliers.length > 0 ? (
              <Link
                to={`/analytics/data-quality?${dataQualityContextQuery}`}
                className="supplier-decision-quality-link"
              >
                Pregledaj artikle bez dobavljača
              </Link>
            ) : null}
          </div>
        </div>
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
            <article className="supplier-decision-kpi">
              <span>Ukupan promet <InfoTip text="Ukupna vrednost prodaje svih dobavljača u izabranom periodu. Formula: zbir prodajnih vrednosti svih prodajnih stavki u periodu. U promet ne ulaze operativni troškovi." /></span>
              <strong>{fmtRsd(totalRevenue)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Ukupno prodato <InfoTip text="Ukupan broj prodatih komada svih dobavljača u izabranom periodu." /></span>
              <strong>{fmtQty(data.totals.ukupnaKolicina)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Ukupna nabavna vrednost <InfoTip text="Zbir troška robe za deo prometa sa dostupnim troškom. Formula: zbir količina x nabavna cena za stavke sa istorijskim ili fallback troškom. Operativni troškovi nisu uključeni." /></span>
              <strong>{fmtRsd(data.totals.ukupanTrosak ?? 0)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Ukupan maržni doprinos <InfoTip text="Zbir razlike između prodajne i nabavne vrednosti za stavke sa dostupnim troškom. Formula: zbir prodajna vrednost - nabavna vrednost. Operativni troškovi, plate, zakup i ostali indirektni troškovi nisu uključeni." /></span>
              <strong>{fmtRsd(totalMarginContribution)}</strong>
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
            <article className="supplier-decision-kpi">
              <span>Prosečna marža <InfoTip text="Prosečan procenat maržnog doprinosa po dobavljaču. Formula po dobavljaču: maržni doprinos / promet sa dostupnim troškom x 100. Prikaz je aritmetički prosek po dobavljačima, nije ponderisan prometom." /></span>
              <strong>{fmtPct(data.totals.prosecnaMarza ?? null, 1)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Udeo top 5 dobavljača <InfoTip text="Procenat ukupnog prometa koji dolazi od pet dobavljača sa najvećim prometom. Formula: promet top 5 / ukupan promet x 100." /></span>
              <strong>{fmtPct(top5SharePct)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Ukupan PoP trend <InfoTip text="Promena ukupnog prometa u odnosu na prethodni uporedivi period iste duzine. Formula: (trenutni promet - prethodni promet) / prethodni promet x 100." /></span>
              <strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong>
            </article>
          </section>

          <section className="supplier-decision-panels">
            <article className="supplier-decision-card">
              <h2>Koncentracija prometa <InfoTip text="Grafikon prikazuje koliki udeo ukupnog prometa nose najveći dobavljači. Koristi samo promet, bez tumačenja profita ili neto marže." /></h2>
              <p>Top udeo prometa za brzu procenu gde je biznis koncentrisan.</p>
              {concentrationData.length > 0 ? (
                <div className="supplier-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={concentrationData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} />
                      <Tooltip
                        contentStyle={CHART_TOOLTIP_STYLE}
                        labelStyle={CHART_TOOLTIP_LABEL_STYLE}
                        formatter={(value: number | string | undefined) => `${fmtPct(Number(value ?? 0), 2)}`}
                      />
                      <Bar dataKey="sharePct" fill="var(--accent-primary)" radius={[0, 8, 8, 0]} name="Udeo u prometu %" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="supplier-decision-empty">Nema podataka za grafikon koncentracije.</div>
              )}
            </article>

            <article className="supplier-decision-card">
              <h2>Promet vs Maržni doprinos <InfoTip text="Grafikon poredi udeo u prometu i udeo u maržnom doprinosu. Maržni doprinos nije neto profit i ne uključuje operativne troškove. Ako je deo troška procenjen iz fallback izvora, i ovaj signal treba čitati oprezno." /></h2>
              <p>Poređenje udela u prometu i udela u maržnom doprinosu - dobavljači s visokim prometom ne moraju imati i visok maržni doprinos.</p>
              {comparisonData.length > 0 ? (
                <div className="supplier-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={comparisonData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} />
                      <Tooltip
                        contentStyle={CHART_TOOLTIP_STYLE}
                        labelStyle={CHART_TOOLTIP_LABEL_STYLE}
                        formatter={((value: any) => `${fmtPct(Number(value ?? 0), 1)}`) as any}
                      />
                      <Legend />
                      <Bar dataKey="udeoPrometa" fill="var(--accent-primary)" radius={[0, 4, 4, 0]} name="Udeo u prometu %" />
                      <Bar dataKey="udeoMarznogDoprinosa" fill="var(--accent-success, #22c55e)" radius={[0, 4, 4, 0]} name="Udeo u marznom doprinosu %" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="supplier-decision-empty">Nema podataka za poređenje.</div>
              )}
            </article>
          </section>

          <section className="supplier-decision-panels">
            <article className="supplier-decision-card">
              <div className="supplier-decision-table-head">
                <div>
                  <h2>Prioritetna lista dobavljača</h2>
                  <p>
                    Pojačaj: {supplierCounts.increaseFocus} | Zadrži: {supplierCounts.maintain} | Oprez: {supplierCounts.review} | Smanji: {supplierCounts.doNotTrust} | N/A: {supplierCounts.insufficientData}
                  </p>
                  <p className="supplier-decision-metric-note">
                    Preporuka uzima u obzir promet, količinu, maržni doprinos, maržni procenat i PoP trend.
                  </p>
                  {unknownSuppliers.length > 0 ? (
                    <p className="supplier-unknown-note">
                    N/A dobavljači su prikazani na dnu i nisu uključeni u decision preporuke.
                    </p>
                  ) : null}
                </div>
                <AnalyticsTableToolbar
                  tableKey="supplier-sales-stats"
                  tableTitle="Podrška odluci - dobavljači"
                  columns={decisionColumns}
                  rows={visibleSuppliers}
                  filters={toolbarFilters}
                  metadata={toolbarMetadata}
                  defaultOrientation="landscape"
                />
              </div>

              <div className="supplier-decision-table-wrap">
                <table className="supplier-decision-table">
                  <thead>
                    <tr>
                      <th>
                        <button type="button" onClick={() => handleSort("dobavljacNaziv")}>
                          Dobavljač{sortMarker("dobavljacNaziv", sortField, sortDir)} <InfoTip text="Naziv dobavljača. Klikom sortirate abecedno." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("ukupanPromet")}>
                          Promet{sortMarker("ukupanPromet", sortField, sortDir)} <InfoTip text="Ukupna vrednost prodaje u izabranom periodu (RSD)." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("ukupnaKolicina")}>
                          Količina{sortMarker("ukupnaKolicina", sortField, sortDir)} <InfoTip text="Ukupan broj prodatih komada." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("totalCost")}>
                          Nabavna vrednost{sortMarker("totalCost", sortField, sortDir)} <InfoTip text="Zbir troška robe za ovaj red. Formula: zbir količina x nabavna cena za stavke sa istorijskim ili fallback troškom. Operativni troškovi nisu uključeni." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("sharePct")}>
                          Udeo u prometu{sortMarker("sharePct", sortField, sortDir)} <InfoTip text="Koliki procenat ukupnog prometa čini ovaj dobavljač. Formula: promet dobavljača / ukupan promet svih prikazanih dobavljača x 100." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("marginContribution")}>
                          Maržni doprinos{sortMarker("marginContribution", sortField, sortDir)} <InfoTip text="Zbir razlike između prodajne i nabavne vrednosti za stavke sa dostupnim troškom. Formula: zbir prodajna vrednost - nabavna vrednost. Operativni troškovi, plate i ostali indirektni troškovi nisu uključeni." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("marginPct")}>
                          Marža %{sortMarker("marginPct", sortField, sortDir)} <InfoTip text="Procenat maržnog doprinosa u prometu sa dostupnim troškom. Formula: maržni doprinos / promet sa dostupnim troškom x 100. Osnova nije ukupan promet, već samo deo prometa gde je trošak dostupan." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("shareOfMarginContribution")}>
                          Udeo u maržnom doprinosu{sortMarker("shareOfMarginContribution", sortField, sortDir)} <InfoTip text="Koliki procenat ukupnog maržnog doprinosa čini ovaj dobavljač. Formula: maržni doprinos dobavljača / ukupan maržni doprinos svih prikazanih dobavljača x 100. Ovo nije udeo u profitu niti u neto zaradi." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("popRevenueChangePct")}>
                          PoP trend{sortMarker("popRevenueChangePct", sortField, sortDir)} <InfoTip text="Promena prometa vs prethodni period." />
                        </button>
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>
                          Preporuka{sortMarker("status", sortField, sortDir)} <InfoTip text="Pojacaj fokus, Zadrzi, U pregledu ili Smanji - preporuka bazirana na prometu, marznom doprinosu, marzi i trendu, uz proveru kvaliteta podataka." />
                        </button>
                      </th>
                      <th className="align-center">
                        Detalj <InfoTip text="Prikaži detaljne analitike za ovog dobavljača." />
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
                      visibleSuppliers.map((supplier) => {
                        const rowKey = supplierKey(supplier);
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
                            ].filter(Boolean).join(" ")}
                          >
                            <td>
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
                            </td>
                            <td className="align-right">{fmtRsd(supplier.ukupanPromet)}</td>
                            <td className="align-right">{fmtQty(supplier.ukupnaKolicina)}</td>
                            <td className="align-right">{fmtRsd(supplier.totalCost)}</td>
                            <td className="align-right">{fmtPct(supplier.sharePct, 1)}</td>
                            <td className="align-right">{fmtRsd(supplier.marginContribution)}</td>
                            <td className="align-right">{fmtPct(supplier.marginPct, 1)}{tierNeedsWarning(supplier.marginQualityTier) ? <span className={`supplier-decision-kpi-badge ${qualityTierClass(supplier.marginQualityTier)}`} title={supplier.marginQualityTooltip ?? supplier.marginQualityLabel ?? ""}> {qualityTierIcon(supplier.marginQualityTier)}</span> : null}</td>
                            <td className="align-right">{fmtPct(supplier.shareOfMarginContribution, 1)}</td>
                            <td className={["align-right", popMetric.className].join(" ")} title={popMetric.title}>
                              {popMetric.label}
                            </td>
                            <td>
                              <span
                                className={statusClass(supplier.status)}
                                title={buildStatusTooltip(supplier)}
                                aria-label={buildStatusTooltip(supplier)}
                              >
                                {statusLabelSr(supplier.status)}
                              </span>
                              {supplier.statusReason ? (
                                <span className="supplier-status-reason" title={supplier.statusReason}>
                                  {" "}
                                  <InfoTip text={supplier.statusReason} />
                                </span>
                              ) : null}
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
              </div>
            </article>
          </section>

          {selectedSupplier ? (
            <section className="supplier-decision-detail" ref={detailSectionRef}>
              <div className="supplier-decision-detail-head">
                <h3>Detalj: {selectedSupplier.dobavljacNaziv}</h3>
                <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                  <span className={`supplier-decision-status ${statusClass(selectedSupplier.status)}`}>
                    {statusLabelSr(selectedSupplier.status)}
                  </span>
                  <button type="button" onClick={() => openSupplierDetail(selectedSupplier)}>
                    Otvori puni detalj
                  </button>
                </div>
              </div>

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
                  <span>Nabavna vrednost <InfoTip text="Zbir troška robe za ovaj red. Formula: zbir količina x nabavna cena za stavke sa istorijskim ili fallback troškom. Operativni troškovi nisu uključeni." /></span>
                  <strong>{fmtRsd(selectedSupplier.totalCost)}</strong>
                </article>
                <article>
                  <span>Maržni doprinos <InfoTip text="Zbir razlike između prodajne i nabavne vrednosti za stavke sa dostupnim troškom. Operativni troškovi, plate, zakup i ostali indirektni troškovi nisu uključeni." /></span>
                  <strong className={selectedSupplier.marginContribution > 0 ? "trend-up" : selectedSupplier.marginContribution < 0 ? "trend-down" : ""}>
                    {fmtRsd(selectedSupplier.marginContribution)}
                  </strong>
                </article>
                <article>
                  <span>Marža % <InfoTip text="Formula: maržni doprinos / promet sa dostupnim troškom x 100. Osnova je samo promet gde je trošak dostupan, ne ukupan promet." /></span>
                  <strong className={selectedSupplier.marginPct != null && selectedSupplier.marginPct > 0 ? "trend-up" : selectedSupplier.marginPct != null && selectedSupplier.marginPct < 0 ? "trend-down" : ""}>
                    {fmtSignedPct(selectedSupplier.marginPct, 2)}
                  </strong>
                </article>
                <article>
                  <span>Udeo u prometu <InfoTip text="Koliki deo ukupnog prometa čini ovaj dobavljač. Formula: promet dobavljača / ukupan promet svih prikazanih dobavljača x 100." /></span>
                  <strong>{fmtPct(selectedSupplier.sharePct, 1)}</strong>
                </article>
                <article>
                  <span>Udeo u maržnom doprinosu <InfoTip text="Koliki procenat ukupnog maržnog doprinosa čini ovaj dobavljač. Formula: maržni doprinos dobavljača / ukupan maržni doprinos svih prikazanih dobavljača x 100. Ovo nije udeo u profitu niti u neto zaradi." /></span>
                  <strong>{fmtPct(selectedSupplier.shareOfMarginContribution, 1)}</strong>
                </article>
                <article>
                  <span>Udeo u količini <InfoTip text="Koliki deo ukupne prodane količine čini ovaj dobavljač (%)." /></span>
                  <strong>{fmtPct(selectedSupplier.shareOfUnits, 1)}</strong>
                </article>
                <article>
                  <span>Broj artikala <InfoTip text="Ukupan broj različitih proizvoda/stilova od ovog dobavljača." /></span>
                  <strong>{selectedSupplier.brojArtikalaUkupno}</strong>
                </article>
              </div>

              {/* PoP trendovi */}
              <h4 className="supplier-detail-section-title">Trend u odnosu na prethodni period</h4>
              <div className="supplier-decision-detail-grid">
                <article>
                  <span>PoP trend prometa <InfoTip text="Promena vrednosti prometa u odnosu na isti period prethodne godine (%)." /></span>
                  <strong className={describePopMetric(selectedSupplier).className} title={describePopMetric(selectedSupplier).title}>
                    {describePopMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period promet <InfoTip text="Vrednost prometa u istom periodu prethodne godine." /></span>
                  <strong>{selectedSupplier.previousPeriodRevenue != null ? fmtRsd(selectedSupplier.previousPeriodRevenue) : "N/A"}</strong>
                </article>
                <article>
                  <span>PoP trend količine <InfoTip text="Promena količine prodanih komada u odnosu na isti period prethodne godine (%)." /></span>
                  <strong className={describePopUnitsMetric(selectedSupplier).className} title={describePopUnitsMetric(selectedSupplier).title}>
                    {describePopUnitsMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period količina <InfoTip text="Količina prodanih komada u istom periodu prethodne godine." /></span>
                  <strong>{selectedSupplier.previousPeriodUnits != null ? fmtQty(selectedSupplier.previousPeriodUnits) : "N/A"}</strong>
                </article>
              </div>

              {/* Nivelacija detalji */}
              <h4 className="supplier-detail-section-title">Nivelacija</h4>
              <div className="supplier-decision-detail-grid">
                <article>
                  <span>Uticaj na promet <InfoTip text="Promenjenost vrednosti prometa pre i posle primene nivelacije na артиклима koji imaju prodaju u oba perioda (%)." /></span>
                  <strong className={describeNivelacijaImpactMetric(selectedSupplier).className} title={describeNivelacijaImpactMetric(selectedSupplier).title}>
                    {describeNivelacijaImpactMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Uticaj na količinu <InfoTip text="Promenjenost količine prodanih komada pre i posle primene nivelacije (%)." /></span>
                  <strong className={describeNivelacijaUnitsImpactMetric(selectedSupplier).className} title={describeNivelacijaUnitsImpactMetric(selectedSupplier).title}>
                    {describeNivelacijaUnitsImpactMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Artikli sa nivelacijom <InfoTip text="Broj artikala koji imaju primenljive nivelacije / Ukupan broj artikala." /></span>
                  <strong>{selectedSupplier.brojArtikalaSaNivelacijom} / {selectedSupplier.brojArtikalaUkupno}</strong>
                </article>
                <article>
                  <span>Pre/post pokrivanje <InfoTip text="Procenat prometa koji se može meriti za pre/post nivelaciju analizu." /></span>
                  <strong>{fmtPct(selectedSupplier.prePostNivelacijaRevenueCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Uporedivi artikli <InfoTip text="Broj artikala sa prodajom i u pre i u post nivelaciji periodu." /></span>
                  <strong>{selectedSupplier.prePostComparableArticleCount ?? 0}</strong>
                </article>
              </div>

              {/* Kvalitet podataka */}
              <h4 className="supplier-detail-section-title">Kvalitet podataka</h4>
              <div className="supplier-decision-detail-grid">
                <article>
                  <span>Kvalitet marže <InfoTip text="Klasifikacija pouzdanosti obracuna marze na osnovu pokrica nabavne cene: Potvrđena (≥80% istorijski), Delimično (≥50% istorijski), Procenjena (<50% istorijski), Bez troška (0% pokrice)." /></span>
                  <strong>
                    <span className={`supplier-decision-kpi-badge ${qualityTierClass(selectedSupplier.marginQualityTier)}`}>
                      {qualityTierIcon(selectedSupplier.marginQualityTier)} {selectedSupplier.marginQualityLabel}
                    </span>
                  </strong>
                </article>
                <article>
                  <span>Pouzdanost <InfoTip text="Procenat procenjen na osnovu dostupnosti podataka, signala i konzistentnosti." /></span>
                  <strong>{fmtPct(selectedSupplier.reliabilityPct, 1)}</strong>
                </article>
                <article>
                  <span>Pokrice istorijskog troska % <InfoTip text="Procenat prometa za koji je trosak preuzet sa prodajne stavke, bez fallback procene iz artikla. Formula: promet sa istorijskim troskom / ukupan promet x 100." /></span>
                  <strong>{fmtPct(selectedSupplier.historicalCostCoveragePct ?? selectedSupplier.marginDataCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Promet procenjen iz fallback troska % <InfoTip text="Procenat prometa gde je trosak procenjen iz fallback izvora artikla. Formula: promet sa fallback troskom / ukupan promet x 100. Operativni troskovi nisu ukljuceni." /></span>
                  <strong>{fmtPct(selectedSupplier.estimatedCostCoveragePct ?? selectedSupplier.fallbackCostCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Promet bez troska % <InfoTip text="Procenat prometa koji nema ni istorijski ni fallback trosak, pa ne ulazi u obracun marznog doprinosa ni marze %. Formula: promet bez troska / ukupan promet x 100." /></span>
                  <strong>{fmtPct(selectedSupplier.noCostCoveragePct, 1)}</strong>
                </article>
                {(selectedSupplier.snapshotCostCoveragePct ?? 0) > 0 ? (
                  <article>
                    <span>Zamrznuta procena (snapshot) % <InfoTip text="Procenat prometa gde je trosak stabilizovan snapshot-om radi reproduktivnosti izvestaja. Ovo nije istorijska nabavna cena sa trenutka prodaje." /></span>
                    <strong>{fmtPct(selectedSupplier.snapshotCostCoveragePct, 1)}</strong>
                  </article>
                ) : null}
                <article>
                  <span>Sigurnost preporuke <InfoTip text="Ukupna sigurnost preporuke bazirana na svim dostupnim signalima (0-100%)." /></span>
                  <strong>{fmtPct(selectedSupplier.confidencePct, 0)}</strong>
                </article>
              </div>

              {selectedSupplier.prePostSignalNote ? (
                <p className="supplier-decision-reason">
                  <strong>Napomena za pre/post signal:</strong> {selectedSupplier.prePostSignalNote}
                </p>
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
                  <p className="supplier-decision-reason">
                    <strong>Napomena za maržu:</strong> {marginNote}
                  </p>
                ) : null;
              })()}

              {(() => {
                const recCaveat = buildRecommendationCaveat(
                  selectedSupplier.marginQualityTier,
                  selectedSupplier.estimatedCostCoveragePct ?? selectedSupplier.fallbackCostCoveragePct,
                  fmtPct
                );
                return recCaveat ? (
                  <p className="supplier-decision-reason">
                    <strong>Napomena za preporuku:</strong> {recCaveat}
                  </p>
                ) : null;
              })()}

              <p className="supplier-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedSupplier.statusReason}
                {selectedSupplier.reasonCodes.length > 0 ? ` (${selectedSupplier.reasonCodes.map(formatReasonCode).join(", ")})` : ""}
              </p>
            </section>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
