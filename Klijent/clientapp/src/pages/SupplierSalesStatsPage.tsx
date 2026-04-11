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
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import { getDataScope } from "../utils/dataScope";
import "./SupplierSalesStatsPage.css";

type PeriodPreset = "30d" | "90d" | "custom";
type SortDir = "asc" | "desc";
type SortField =
  | "dobavljacNaziv"
  | "ukupanPromet"
  | "ukupnaKolicina"
  | "sharePct"
  | "marginContribution"
  | "marginPct"
  | "shareOfProfit"
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
  shareOfProfit: number;
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
  { key: "dobavljacNaziv", header: "Dobavljac", dataType: "text" },
  { key: "ukupanPromet", header: "Promet", dataType: "currency" },
  { key: "ukupnaKolicina", header: "Kolicina", dataType: "number" },
  { key: "sharePct", header: "Udeo prometa %", dataType: "percent" },
  { key: "marginContribution", header: "Realna zarada", dataType: "currency" },
  { key: "marginPct", header: "Marza %", dataType: "percent" },
  { key: "shareOfProfit", header: "Udeo zarade %", dataType: "percent" },
  { key: "popRevenueChangePct", header: "PoP trend %", dataType: "percent" },
  { key: "status", header: "Preporuka", dataType: "text" },
];

const REASON_CODE_LABELS: Record<string, string> = {
  unknown_entity: "Nepoznat entitet",
  new_entity: "Novi dobavljac",
  previous_period_missing: "Nedostaje prethodni period",
  no_previous_baseline: "Nema prethodne baze",
  missing_cost_coverage: "Nedovoljno pokrice nabavne cene",
  limited_nivelacija_coverage: "Nizak pre/post split coverage",
  unknown_heavy_dataset: "Unknown-heavy dataset",
  tiny_sample: "Premali uzorak",
  unstable_margin: "Nestabilna marza",
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
  const targetY = element.getBoundingClientRect().top + window.scrollY;
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
  if (status === "increase_focus") return "Pojacaj";
  if (status === "maintain") return "Zadrzi";
  if (status === "review") return "Oprez";
  if (status === "do_not_trust") return "Smanji";
  return "Nedovoljno podataka";
}

function statusLabelSr(status: DecisionStatus): string {
  if (status === "increase_focus") return "Pojacaj";
  if (status === "maintain") return "Zadrzi";
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
    : "nema dodatnih flagova";
  return `${data.statusLabel}: ${data.statusReason} | Udeo ${fmtPct(data.sharePct, 1)} | Marza ${fmtPct(data.marginPct, 1)} | PoP ${popText} | Nivelacija impact ${impactText} | Split pokrice ${fmtPct(data.splitCoveragePct, 1)} | Pouzdanost ${fmtPct(data.reliabilityPct, 0)} | Confidence ${fmtPct(data.confidencePct, 0)} | Razlozi: ${reasons}`;
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
      title: "Dobavljac nije imao promet u prethodnom uporedivom periodu, pa PoP procenat nije smislen.",
      className: "trend-neutral",
    };
  }

  return {
    label: "N/A",
    title: "PoP trend nije dostupan jer ne postoji validna prethodna baza za poredjenje.",
    className: "trend-neutral",
  };
}

function describeNivelacijaImpactMetric(supplier: SupplierSalesStat): { label: string; title: string; className: string } {
  if (supplier.prePostNivelacijaRevenueImpactPct != null && !Number.isNaN(supplier.prePostNivelacijaRevenueImpactPct)) {
    return {
      label: fmtSignedPct(supplier.prePostNivelacijaRevenueImpactPct, 2),
      title: `Pre/post nivelacija impact meri promenu prometa unutar artikala sa poznatim prvim datumom nivelacije. Pokrice: ${fmtPct(supplier.prePostNivelacijaRevenueCoveragePct, 1)} prometa.`,
      className: trendClass(supplier.prePostNivelacijaRevenueImpactPct),
    };
  }

  if ((supplier.prePostNivelacijaRevenueCoveragePct ?? 0) <= 0) {
    return {
      label: "N/A",
      title: "Nema dovoljno artikala sa poznatom istorijom nivelacije za pre/post impact metriku.",
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
      title: "Dobavljac nije imao prodatu kolicinu u prethodnom uporedivom periodu.",
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
    return {
      label: fmtSignedPct(supplier.prePostNivelacijaUnitsImpactPct, 2),
      title: "Pre/post promena kolicine unutar artikala sa poznatim prvim datumom nivelacije.",
      className: trendClass(supplier.prePostNivelacijaUnitsImpactPct),
    };
  }

  if ((supplier.prePostNivelacijaRevenueCoveragePct ?? 0) <= 0) {
    return {
      label: "N/A",
      title: "Nema dovoljno artikala sa poznatom istorijom nivelacije za pre/post metriku kolicine.",
      className: "trend-neutral",
    };
  }

  if (supplier.preNivelacijeKolicina <= 0 && supplier.posleNivelacijeKolicina > 0) {
    return {
      label: "Bez baze",
      title: "Postoji kolicina posle prve nivelacije, ali nema pre-nivelacija baze za smislen procenat promene.",
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

  const initialRange = useMemo(() => getPresetRange("90d"), []);
  const initialQueryFilters = useMemo(() => {
    const queryFromDate = parseDateInputOrDefault(searchParams.get("fromDate"), initialRange.fromDate);
    const queryToDate = parseDateInputOrDefault(searchParams.get("toDate"), initialRange.toDate);
    const querySezonaId = parseNullableInt(searchParams.get("sezonaId"));
    const queryStoreId = parseNullableInt(searchParams.get("storeId"));
    const hasExplicitDateQuery = searchParams.has("fromDate") || searchParams.has("toDate");
    const periodPreset: PeriodPreset = hasExplicitDateQuery ? "custom" : "90d";

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
    const queryPreset: PeriodPreset = hasExplicitDateQuery ? "custom" : "90d";

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
      setError(reason instanceof Error ? reason.message : "Greska pri ucitavanju podataka o dobavljacima.");
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
      const shareOfProfit = supplier.shareOfProfit ?? (totalMarginContribution > 0 ? (supplier.marginContribution / totalMarginContribution) * 100 : 0);
      const shareOfUnits = supplier.shareOfUnits ?? (totalUnits > 0 ? (supplier.ukupnaKolicina / totalUnits) * 100 : 0);
      const splitCoveragePct = supplier.prePostNivelacijaRevenueCoveragePct ?? 0;
      const recommended = supplier.recommendation;
      const status = (recommended?.status ?? (supplier.isUnknown ? "do_not_trust" : "insufficient_data")) as DecisionStatus;
      const statusLabel = recommended?.label ?? displayStatusLabel(status);
      const statusReason = recommended?.summary
        ?? (supplier.isUnknown
          ? "Dobavljac je nepoznat u master podacima; signal nije pouzdan za odluku."
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
        shareOfProfit,
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
      } else if (sortField === "sharePct") {
        compare = a.sharePct - b.sharePct;
      } else if (sortField === "marginContribution") {
        compare = a.marginContribution - b.marginContribution;
      } else if (sortField === "marginPct") {
        compare = a.marginPct - b.marginPct;
      } else if (sortField === "shareOfProfit") {
        compare = a.shareOfProfit - b.shareOfProfit;
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
    if (knownSuppliers.length === 0) return [] as Array<{ name: string; udelPrometa: number; udelZarade: number; marza: number }>;

    const ranked = [...knownSuppliers]
      .sort((a, b) => b.ukupanPromet - a.ukupanPromet);

    return ranked.slice(0, 8).map((row) => ({
      name: row.dobavljacNaziv,
      udelPrometa: Number(row.sharePct.toFixed(1)),
      udelZarade: Number(row.shareOfProfit.toFixed(1)),
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
    const knownCostShare = missingCostShare == null ? null : Math.max(0, 100 - missingCostShare);
    const unknownShare = data.dataQuality.unknownSupplierRevenueSharePct;

    if (splitCoverage != null && splitCoverage < 60) {
      notes.push(`Pre/posle nivelacije trenutno pokriva ${fmtPct(splitCoverage, 1)} ukupnog prometa, pa taj signal treba citati kao delimican.`);
    }

    if (knownCostShare != null && knownCostShare < 100) {
      notes.push(`Marza i marzni doprinos su zasnovani na ${fmtPct(knownCostShare, 1)} prometa sa poznatom nabavnom cenom.`);
    }

    if (unknownShare != null && unknownShare > 0) {
      notes.push(`Nepoznati/N-A dobavljaci ucestvuju sa ${fmtPct(unknownShare, 1)} ukupnog prometa.`);
    }

    return notes;
  }, [data]);

  const toolbarFilters = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "fromDate", label: "Od", value: activeFilters.fromDate },
      { key: "toDate", label: "Do", value: activeFilters.toDate },
      { key: "sezonaId", label: "Sezona", value: activeSezonaLabel },
      { key: "storeId", label: "Objekat", value: activeFilters.storeId ?? "Svi objekti" },
      { key: "dataScope", label: "Data scope", value: activeDataScope },
      { key: "includeUnknown", label: "Include unknown", value: includeUnknown ? "true" : "false" },
    ],
    [activeDataScope, activeFilters.fromDate, activeFilters.storeId, activeFilters.toDate, activeSezonaLabel, includeUnknown]
  );

  const toolbarMetadata = useMemo<AnalyticsNamedValue[]>(
    () => [
      { key: "generatedAt", label: "Generisano", value: data?.generatedAt ?? "" },
      { key: "suppliers", label: "Dobavljaca", value: data?.totals.brojDobavljaca ?? 0 },
      { key: "unknownSuppliers", label: "Nepoznato/N-A", value: unknownSuppliers.length },
      { key: "marginCoverage", label: "Promet sa nabavnom cenom", value: fmtPct(data?.dataQuality.missingCostRevenueSharePct == null ? null : 100 - data.dataQuality.missingCostRevenueSharePct, 1) },
      { key: "totalsPopTrend", label: "Ukupan PoP trend", value: fmtPct(data?.totals.popRevenueChangePct, 1) },
      { key: "totalsPrePostImpact", label: "Ukupan nivelacija impact", value: fmtPct(data?.totals.prePostNivelacijaRevenueImpactPct, 1) },
      { key: "splitCoverage", label: "Pre/post pokrice", value: fmtPct(data?.dataQuality.revenueWithNivelacijaSplitSharePct, 1) },
      { key: "increaseFocus", label: "Increase focus", value: supplierCounts.increaseFocus },
      { key: "maintain", label: "Maintain", value: supplierCounts.maintain },
      { key: "review", label: "Review", value: supplierCounts.review },
      { key: "doNotTrust", label: "Do not trust", value: supplierCounts.doNotTrust },
      { key: "insufficientData", label: "Insufficient data", value: supplierCounts.insufficientData },
    ],
    [
      data?.dataQuality.missingCostRevenueSharePct,
      data?.dataQuality.revenueWithNivelacijaSplitSharePct,
      data?.generatedAt,
      data?.totals.brojDobavljaca,
      data?.totals.popRevenueChangePct,
      data?.totals.prePostNivelacijaRevenueImpactPct,
      supplierCounts.increaseFocus,
      supplierCounts.maintain,
      supplierCounts.review,
      supplierCounts.doNotTrust,
      supplierCounts.insufficientData,
      unknownSuppliers.length,
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
    const range = getPresetRange("90d");
    setPeriodPreset("90d");
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
          <h1 className="supplier-decision-title">Prodaja po dobavljacima</h1>
          <p className="supplier-decision-subtitle">
            Decision-support pregled za izbor dobavljaca: fokus na promet, doprinos i akciju.
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
      {loading ? <div className="supplier-decision-message loading">Ucitavam dobavljace...</div> : null}
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
                Pregledaj artikle bez dobavljaca
              </Link>
            ) : null}
          </div>
        </div>
      ) : null}

      {!loading && data ? (
        <>
          <section className="supplier-decision-kpis">
            <article className="supplier-decision-kpi">
              <span>Ukupan promet</span>
              <strong>{fmtRsd(totalRevenue)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Ukupno prodato</span>
              <strong>{fmtQty(data.totals.ukupnaKolicina)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Ukupna realna zarada</span>
              <strong>{fmtRsd(totalMarginContribution)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Prosecna marza</span>
              <strong>{fmtPct(data.totals.prosecnaMarza ?? null, 1)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Udeo top 5 dobavljaca</span>
              <strong>{fmtPct(top5SharePct)}</strong>
            </article>
            <article className="supplier-decision-kpi">
              <span>Ukupan PoP trend</span>
              <strong className={trendClass(periodGrowthPct)}>{fmtSignedPct(periodGrowthPct)}</strong>
            </article>
          </section>

          <section className="supplier-decision-panels">
            <article className="supplier-decision-card">
              <h2>Koncentracija prometa</h2>
              <p>Top udeo prometa za brzu procenu gde je biznis koncentrisan.</p>
              {concentrationData.length > 0 ? (
                <div className="supplier-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={concentrationData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} />
                      <Tooltip
                        contentStyle={{
                          background: "var(--surface-elevated, var(--theme-color-0f1730, #0f1730))",
                          border: "1px solid var(--border-default, var(--theme-color-32406b, #32406b))",
                          color: "var(--text-primary, var(--theme-color-e5e7eb, #e5e7eb))",
                          borderRadius: "8px",
                          boxShadow: "0 4px 6px rgba(0, 0, 0, 0.1)",
                        }}
                        formatter={(value: number | string | undefined) => `${fmtPct(Number(value ?? 0), 2)}`}
                      />
                      <Bar dataKey="sharePct" fill="var(--accent-primary)" radius={[0, 8, 8, 0]} name="Udeo prometa %" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="supplier-decision-empty">Nema podataka za grafikon koncentracije.</div>
              )}
            </article>

            <article className="supplier-decision-card">
              <h2>Promet vs Zarada</h2>
              <p>Poredjenje udela u prometu i udela u zaradi - gde promet vara, a profit govori istinu.</p>
              {comparisonData.length > 0 ? (
                <div className="supplier-decision-chart-wrap">
                  <ResponsiveContainer width="100%" height="100%" minWidth={0} minHeight={260}>
                    <BarChart data={comparisonData} layout="vertical" margin={{ top: 12, right: 16, left: 8, bottom: 8 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="var(--border-default)" />
                      <XAxis type="number" tick={{ fill: "var(--text-secondary)", fontSize: 12 }} unit="%" />
                      <YAxis type="category" dataKey="name" width={180} tick={{ fill: "var(--text-primary)", fontSize: 12 }} />
                      <Tooltip
                        contentStyle={{
                          background: "var(--surface-elevated, var(--theme-color-0f1730, #0f1730))",
                          border: "1px solid var(--border-default, var(--theme-color-32406b, #32406b))",
                          color: "var(--text-primary, var(--theme-color-e5e7eb, #e5e7eb))",
                          borderRadius: "8px",
                          boxShadow: "0 4px 6px rgba(0, 0, 0, 0.1)",
                        }}
                        formatter={((value: any) => `${fmtPct(Number(value ?? 0), 1)}`) as any}
                      />
                      <Legend />
                      <Bar dataKey="udelPrometa" fill="var(--accent-primary)" radius={[0, 4, 4, 0]} name="Udeo prometa %" />
                      <Bar dataKey="udelZarade" fill="var(--accent-success, #22c55e)" radius={[0, 4, 4, 0]} name="Udeo zarade %" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              ) : (
                <div className="supplier-decision-empty">Nema podataka za poredjenje.</div>
              )}
            </article>
          </section>

          <section className="supplier-decision-panels">
            <article className="supplier-decision-card">
              <div className="supplier-decision-table-head">
                <div>
                  <h2>Prioritetna lista dobavljaca</h2>
                  <p>
                    Pojacaj: {supplierCounts.increaseFocus} | Zadrzi: {supplierCounts.maintain} | Oprez: {supplierCounts.review} | Smanji: {supplierCounts.doNotTrust} | N/A: {supplierCounts.insufficientData}
                  </p>
                  <p className="supplier-decision-metric-note">
                    Preporuka uzima u obzir promet, kolicinu, realnu zaradu, marzni procenat i PoP trend.
                  </p>
                  {unknownSuppliers.length > 0 ? (
                    <p className="supplier-unknown-note">
                      N/A dobavljaci su prikazani na dnu i nisu ukljuceni u decision preporuke.
                    </p>
                  ) : null}
                </div>
                <AnalyticsTableToolbar
                  tableKey="supplier-sales-stats"
                  tableTitle="Podrska odluci - dobavljaci"
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
                          Dobavljac{sortMarker("dobavljacNaziv", sortField, sortDir)} <InfoTip text="Naziv dobavljaca. Klikom sortirate abecedno." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("ukupanPromet")}>
                          Promet{sortMarker("ukupanPromet", sortField, sortDir)} <InfoTip text="Ukupna vrednost prodaje u izabranom periodu (RSD)." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("ukupnaKolicina")}>
                          Kolicina{sortMarker("ukupnaKolicina", sortField, sortDir)} <InfoTip text="Ukupan broj prodatih artikala/pari." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("sharePct")}>
                          Udeo prometa{sortMarker("sharePct", sortField, sortDir)} <InfoTip text="Udeo u ukupnom prometu (procenat)." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("marginContribution")}>
                          Realna zarada{sortMarker("marginContribution", sortField, sortDir)} <InfoTip text="Razlika izmedju prodajne i nabavne vrednosti. Sa poznatom nabavnom cenom." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("marginPct")}>
                          Marza %{sortMarker("marginPct", sortField, sortDir)} <InfoTip text="Procenat marze: (zarada / prihod sa poznatom cenom) * 100." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("shareOfProfit")}>
                          Udeo zarade{sortMarker("shareOfProfit", sortField, sortDir)} <InfoTip text="Udeo ovog dobavljaca u ukupnoj realnoj zaradi." />
                        </button>
                      </th>
                      <th className="align-right">
                        <button type="button" onClick={() => handleSort("popRevenueChangePct")}>
                          PoP trend{sortMarker("popRevenueChangePct", sortField, sortDir)} <InfoTip text="Promena prometa vs prethodni period." />
                        </button>
                      </th>
                      <th>
                        <button type="button" onClick={() => handleSort("status")}>
                          Preporuka{sortMarker("status", sortField, sortDir)} <InfoTip text="Pojacaj / Zadrzi / Oprez / Smanji - na osnovu kombinacije prometa, zarade, marze i trenda." />
                        </button>
                      </th>
                      <th className="align-center">Detalj</th>
                    </tr>
                  </thead>
                  <tbody>
                    {visibleSuppliers.length === 0 ? (
                      <tr>
                        <td colSpan={10} className="supplier-decision-empty-row">
                          Nema podataka za izabrane filtere.
                        </td>
                      </tr>
                    ) : (
                      visibleSuppliers.map((supplier) => {
                        const rowKey = supplierKey(supplier);
                        const isExpanded = expandedSupplierKey === rowKey;
                        const popMetric = describePopMetric(supplier);
                        const profitVsRevenueMismatch = !supplier.isUnknown
                          && supplier.sharePct > 5
                          && supplier.shareOfProfit < supplier.sharePct * 0.5;
                        const highProfitLowRevenue = !supplier.isUnknown
                          && supplier.shareOfProfit > supplier.sharePct * 1.5
                          && supplier.sharePct < 10;
                        return (
                          <tr
                            key={rowKey}
                            className={[
                              isExpanded ? "expanded-row" : "",
                              supplier.isUnknown ? "supplier-unknown-row" : "",
                              profitVsRevenueMismatch ? "supplier-mismatch-row" : "",
                              highProfitLowRevenue ? "supplier-high-profit-row" : "",
                            ].filter(Boolean).join(" ")}
                          >
                            <td>
                              {supplier.isUnknown ? (
                                <span
                                  className="supplier-unknown-label"
                                  title="Artikli bez dodeljenog dobavljaca u bazi"
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
                            <td className="align-right">{fmtPct(supplier.sharePct, 1)}</td>
                            <td className="align-right">{fmtRsd(supplier.marginContribution)}</td>
                            <td className="align-right">{fmtPct(supplier.marginPct, 1)}</td>
                            <td className="align-right">{fmtPct(supplier.shareOfProfit, 1)}</td>
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
                  <span>Promet</span>
                  <strong>{fmtRsd(selectedSupplier.ukupanPromet)}</strong>
                </article>
                <article>
                  <span>Prodata kolicina</span>
                  <strong>{fmtQty(selectedSupplier.ukupnaKolicina)}</strong>
                </article>
                <article>
                  <span>Realna zarada</span>
                  <strong className={selectedSupplier.marginContribution > 0 ? "trend-up" : selectedSupplier.marginContribution < 0 ? "trend-down" : ""}>
                    {fmtRsd(selectedSupplier.marginContribution)}
                  </strong>
                </article>
                <article>
                  <span>Marza %</span>
                  <strong className={selectedSupplier.marginPct != null && selectedSupplier.marginPct > 0 ? "trend-up" : selectedSupplier.marginPct != null && selectedSupplier.marginPct < 0 ? "trend-down" : ""}>
                    {fmtSignedPct(selectedSupplier.marginPct, 2)}
                  </strong>
                </article>
                <article>
                  <span>Udeo u prometu</span>
                  <strong>{fmtPct(selectedSupplier.sharePct, 1)}</strong>
                </article>
                <article>
                  <span>Udeo u zaradi</span>
                  <strong>{fmtPct(selectedSupplier.shareOfProfit, 1)}</strong>
                </article>
                <article>
                  <span>Udeo u kolicini</span>
                  <strong>{fmtPct(selectedSupplier.shareOfUnits, 1)}</strong>
                </article>
                <article>
                  <span>Broj artikala</span>
                  <strong>{selectedSupplier.brojArtikalaUkupno}</strong>
                </article>
              </div>

              {/* PoP trendovi */}
              <h4 className="supplier-detail-section-title">Trend u odnosu na prethodni period</h4>
              <div className="supplier-decision-detail-grid">
                <article>
                  <span>PoP trend prometa</span>
                  <strong className={describePopMetric(selectedSupplier).className} title={describePopMetric(selectedSupplier).title}>
                    {describePopMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period promet</span>
                  <strong>{selectedSupplier.previousPeriodRevenue != null ? fmtRsd(selectedSupplier.previousPeriodRevenue) : "N/A"}</strong>
                </article>
                <article>
                  <span>PoP trend kolicine</span>
                  <strong className={describePopUnitsMetric(selectedSupplier).className} title={describePopUnitsMetric(selectedSupplier).title}>
                    {describePopUnitsMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Prethodni period kolicina</span>
                  <strong>{selectedSupplier.previousPeriodUnits != null ? fmtQty(selectedSupplier.previousPeriodUnits) : "N/A"}</strong>
                </article>
              </div>

              {/* Nivelacija detalji */}
              <h4 className="supplier-detail-section-title">Nivelacija</h4>
              <div className="supplier-decision-detail-grid">
                <article>
                  <span>Impact na promet</span>
                  <strong className={describeNivelacijaImpactMetric(selectedSupplier).className} title={describeNivelacijaImpactMetric(selectedSupplier).title}>
                    {describeNivelacijaImpactMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Impact na kolicinu</span>
                  <strong className={describeNivelacijaUnitsImpactMetric(selectedSupplier).className} title={describeNivelacijaUnitsImpactMetric(selectedSupplier).title}>
                    {describeNivelacijaUnitsImpactMetric(selectedSupplier).label}
                  </strong>
                </article>
                <article>
                  <span>Artikli sa nivelacijom</span>
                  <strong>{selectedSupplier.brojArtikalaSaNivelacijom} / {selectedSupplier.brojArtikalaUkupno}</strong>
                </article>
                <article>
                  <span>Pre/post pokrice</span>
                  <strong>{fmtPct(selectedSupplier.prePostNivelacijaRevenueCoveragePct, 1)}</strong>
                </article>
              </div>

              {/* Kvalitet podataka */}
              <h4 className="supplier-detail-section-title">Kvalitet podataka</h4>
              <div className="supplier-decision-detail-grid">
                <article>
                  <span>Pouzdanost</span>
                  <strong>{fmtPct(selectedSupplier.reliabilityPct, 1)}</strong>
                </article>
                <article>
                  <span>Pokrice marze</span>
                  <strong>{fmtPct(selectedSupplier.marginDataCoveragePct, 1)}</strong>
                </article>
                <article>
                  <span>Confidence</span>
                  <strong>{fmtPct(selectedSupplier.confidencePct, 0)}</strong>
                </article>
              </div>

              <p className="supplier-decision-reason">
                <strong>Razlog preporuke:</strong> {selectedSupplier.statusReason}
                {selectedSupplier.reasonCodes.length > 0 ? ` (${selectedSupplier.reasonCodes.map(formatReasonCode).join(", ")})` : ""}
              </p>
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}
