import { useCallback, useEffect, useMemo, useState } from "react";
import {
    Bar,
    BarChart,
    CartesianGrid,
    Legend,
    Line,
    LineChart,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from "recharts";
import { useLocation, useNavigate } from "react-router-dom";
import AnalyticsTableToolbar from "../components/analytics/AnalyticsTableToolbar";
import { getDobavljaci } from "../services/dobavljaciApi";
import { buildAnalyticsDetailSnapshot, saveAnalyticsDetailSnapshot } from "../services/analyticsTableState";
import type { Dobavljac } from "../types/Dobavljaci";
import type { AnalyticsNamedValue, AnalyticsTableColumn } from "../types/analyticsTable";
import {
    getVendorSalesNivelacija,
    getVendorSalesNivelacijaOptions,
    type VendorSalesNivelacijaOption,
    type VendorSalesNivelacijaArticleStat,
    type VendorSalesNivelacijaResponse,
    type VendorSalesNivelacijaVendorStat,
} from "../services/vendorSalesNivelacijaApi";
import "./ProdajaPrePostNivelacijePage.css";

type ChartMetric = "revenue" | "qty";
type FilterMode = "period" | "nivelacija";
type SortDirection = "asc" | "desc";
type VendorSortField =
    | "vendorName"
    | "preQty"
    | "postQty"
    | "changeQty"
    | "preRevenue"
    | "postRevenue"
    | "changeRevenue"
    | "changePercent"
    | "articleCount";
type ArticleSortField =
    | "eventDate"
    | "vendorName"
    | "sku"
    | "articleName"
    | "category"
    | "oldPrice"
    | "newPrice"
    | "preQty"
    | "postQty"
    | "changeQty"
    | "preRevenue"
    | "postRevenue"
    | "changeRevenue"
    | "changePercent";

const ALL_EVENTS_OPTION = "__all__";

const vendorColumns: Array<{ field: VendorSortField; label: string; right?: boolean }> = [
    { field: "vendorName", label: "Dobavljac" },
    { field: "preQty", label: "Pre kolicina", right: true },
    { field: "postQty", label: "Post kolicina", right: true },
    { field: "changeQty", label: "Promena kolicine", right: true },
    { field: "preRevenue", label: "Pre promet", right: true },
    { field: "postRevenue", label: "Post promet", right: true },
    { field: "changeRevenue", label: "Promena prometa", right: true },
    { field: "changePercent", label: "Promena %", right: true },
    { field: "articleCount", label: "Broj artikala", right: true },
];

const articleColumns: Array<{ field: ArticleSortField; label: string; right?: boolean }> = [
    { field: "eventDate", label: "Datum nivelacije" },
    { field: "vendorName", label: "Dobavljac" },
    { field: "sku", label: "SKU" },
    { field: "articleName", label: "Artikal" },
    { field: "category", label: "Kategorija" },
    { field: "oldPrice", label: "Stara cena", right: true },
    { field: "newPrice", label: "Nova cena", right: true },
    { field: "preQty", label: "Pre kolicina", right: true },
    { field: "postQty", label: "Post kolicina", right: true },
    { field: "changeQty", label: "Promena kolicine", right: true },
    { field: "preRevenue", label: "Pre promet", right: true },
    { field: "postRevenue", label: "Post promet", right: true },
    { field: "changeRevenue", label: "Promena prometa", right: true },
    { field: "changePercent", label: "Promena %", right: true },
];

const categoryDetailColumns: AnalyticsTableColumn<VendorSalesNivelacijaResponse["categoryStats"][number]>[] = [
    { key: "category", header: "Kategorija", dataType: "text" },
    { key: "articlesCount", header: "Artikli", dataType: "number" },
    { key: "vendorsCount", header: "Dobavljaci", dataType: "number" },
    { key: "preRevenue", header: "Promet pre", dataType: "currency" },
    { key: "postRevenue", header: "Promet posle", dataType: "currency" },
    { key: "changeRevenue", header: "Promena prometa", dataType: "currency" },
    { key: "changePercent", header: "Promena %", dataType: "percent" },
];

const priceDirectionDetailColumns: AnalyticsTableColumn<VendorSalesNivelacijaResponse["priceDirectionStats"][number]>[] = [
    { key: "segment", header: "Segment", dataType: "text" },
    { key: "articlesCount", header: "Artikli", dataType: "number" },
    { key: "vendorsCount", header: "Dobavljaci", dataType: "number" },
    { key: "avgPriceChangePercent", header: "Prosecna promena cene", dataType: "percent" },
    { key: "changeRevenue", header: "Promena prometa", dataType: "currency" },
    { key: "changePercent", header: "Promena %", dataType: "percent" },
];

const priceBucketDetailColumns: AnalyticsTableColumn<{ bucket: string; count: number; changeQty: number; changeRevenue: number }>[] = [
    { key: "bucket", header: "Razred promene cene", dataType: "text" },
    { key: "count", header: "Broj SKU", dataType: "number" },
    { key: "changeQty", header: "Promena kolicine", dataType: "number" },
    { key: "changeRevenue", header: "Promena prometa", dataType: "currency" },
];

const vendorDetailColumns: AnalyticsTableColumn<VendorSalesNivelacijaVendorStat>[] = [
    { key: "vendorId", header: "Dobavljac ID", dataType: "number" },
    { key: "vendorName", header: "Dobavljac", dataType: "text" },
    { key: "preQty", header: "Pre kolicina", dataType: "number" },
    { key: "postQty", header: "Post kolicina", dataType: "number" },
    { key: "changeQty", header: "Promena kolicine", dataType: "number" },
    { key: "preRevenue", header: "Pre promet", dataType: "currency" },
    { key: "postRevenue", header: "Post promet", dataType: "currency" },
    { key: "changeRevenue", header: "Promena prometa", dataType: "currency" },
    { key: "changePercent", header: "Promena %", dataType: "percent" },
    { key: "articleCount", header: "Broj artikala", dataType: "number" },
];

const articleDetailColumns: AnalyticsTableColumn<VendorSalesNivelacijaArticleStat>[] = [
    { key: "eventDate", header: "Datum nivelacije", dataType: "date" },
    { key: "vendorId", header: "Dobavljac ID", dataType: "number" },
    { key: "vendorName", header: "Dobavljac", dataType: "text" },
    { key: "sku", header: "SKU", dataType: "text" },
    { key: "articleName", header: "Artikal", dataType: "text" },
    { key: "category", header: "Kategorija", dataType: "text" },
    { key: "oldPrice", header: "Stara cena", dataType: "currency" },
    { key: "newPrice", header: "Nova cena", dataType: "currency" },
    { key: "preQty", header: "Pre kolicina", dataType: "number" },
    { key: "postQty", header: "Post kolicina", dataType: "number" },
    { key: "changeQty", header: "Promena kolicine", dataType: "number" },
    { key: "preRevenue", header: "Pre promet", dataType: "currency" },
    { key: "postRevenue", header: "Post promet", dataType: "currency" },
    { key: "changeRevenue", header: "Promena prometa", dataType: "currency" },
    { key: "changePercent", header: "Promena %", dataType: "percent" },
    { key: "priceChangePercent", header: "Promena cene %", dataType: "percent" },
    { key: "priceElasticity", header: "Elasticnost", dataType: "number" },
    { key: "didRevenue", header: "DiD promet", dataType: "currency" },
    { key: "didQty", header: "DiD qty", dataType: "number" },
    { key: "lostSalesOOS", header: "Lost sales OOS", dataType: "currency" },
    { key: "oosRate", header: "OOS %", dataType: "percent" },
    { key: "metricReason", header: "Metric reason", dataType: "text" },
];

function toDateInput(date: Date): string {
    return date.toISOString().slice(0, 10);
}

function fmtRsd(value: number): string {
    return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} RSD`;
}

function fmtPct(value: number): string {
    return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;
}

function fmtQty(value: number): string {
    return `${value.toLocaleString("sr-RS")} kom`;
}

function fmtNullableRsd(value: number | null | undefined): string {
    return value == null ? "N/A" : fmtRsd(Number(value));
}

function fmtNullableQty(value: number | null | undefined): string {
    return value == null ? "N/A" : fmtQty(Number(value));
}

function fmtNullableSharePct(value: number | null | undefined): string {
    return value == null ? "N/A" : fmtPct(Number(value) * 100);
}

function fmtNullableElasticity(value: number | null | undefined): string {
    return value == null ? "N/A" : Number(value).toLocaleString("sr-RS", { minimumFractionDigits: 2, maximumFractionDigits: 4 });
}

function csvEscape(input: string | number): string {
    const str = String(input ?? "");
    if (str.includes(",") || str.includes("\"") || str.includes("\n")) return `"${str.replaceAll("\"", "\"\"")}"`;
    return str;
}

function toQueryDateRange(fromDate: string, toDate: string): { from: string; to: string } {
    return { from: `${fromDate}T00:00:00Z`, to: `${toDate}T23:59:59Z` };
}

function normalizeTooltipValue(value: number | string | readonly (number | string)[] | undefined): number {
    if (typeof value === "number") return value;
    if (typeof value === "string") return Number.isFinite(Number(value)) ? Number(value) : 0;
    if (Array.isArray(value) && value.length > 0) {
        const first = value[0];
        const parsed = typeof first === "number" ? first : Number(first);
        return Number.isFinite(parsed) ? parsed : 0;
    }
    return 0;
}

function safeNumber(value: number | string | null | undefined): number {
    const num = Number(value);
    return Number.isFinite(num) ? num : 0;
}

function sortMark(field: string, activeField: string, dir: SortDirection): string {
    if (field !== activeField) return "";
    return dir === "asc" ? " ▲" : " ▼";
}

function compareSort(a: string | number, b: string | number, dir: SortDirection): number {
    const result = typeof a === "string" && typeof b === "string" ? a.localeCompare(b, "sr") : Number(a) - Number(b);
    return dir === "asc" ? result : -result;
}

// --- Analytics helpers ---

type PriceBucket = "-5%→-10%" | "-10%→-20%" | "<-20%" | "+5%→+10%" | ">+10%" | "Ostalo";

function normalizeVendorName(name: string): string {
    const t = (name ?? "").trim();
    return t === "" || t.toUpperCase() === "N/A" ? "Unknown Supplier" : t;
}

function skuClassify(x: VendorSalesNivelacijaArticleStat): "NEW" | "REVIVED" | "GAINER" | "LOSER" | "NEUTRAL" {
    if (x.preQty === 0 && Number(x.preRevenue) === 0) return "NEW";
    if (x.preQty < 3 && Number(x.preRevenue) < 20000) return "REVIVED";
    if (Number(x.changeRevenue) > 0) return "GAINER";
    if (Number(x.changeRevenue) < 0) return "LOSER";
    return "NEUTRAL";
}

function confidenceScore(x: VendorSalesNivelacijaArticleStat): "Low" | "Medium" | "High" {
    const total = x.preQty + x.postQty;
    if (total >= 50) return "High";
    if (total >= 10) return "Medium";
    return "Low";
}

function logGrowthPct(pre: number, post: number): number | null {
    if (pre <= 0 || post <= 0) return null;
    return 100 * Math.log(post / pre);
}

function priceBucket(pct: number | null): PriceBucket {
    if (pct == null) return "Ostalo";
    if (pct <= -20) return "<-20%";
    if (pct <= -10) return "-10%→-20%";
    if (pct <= -5) return "-5%→-10%";
    if (pct >= 10) return ">+10%";
    if (pct >= 5) return "+5%→+10%";
    return "Ostalo";
}

function actionRecommendation(x: VendorSalesNivelacijaArticleStat): "KEEP" | "MONITOR" | "ROLLBACK" {
    const cls = skuClassify(x);
    if (cls === "NEW" || cls === "REVIVED") return "MONITOR";
    const conf = confidenceScore(x);
    if (conf === "Low" || !x.hasSalesWindow) return "MONITOR";
    if (Number(x.changeRevenue) < -10000) return "ROLLBACK";
    if (Number(x.changeRevenue) >= 0) return "KEEP";
    return "MONITOR";
}

function SortButton(props: { label: string; right?: boolean; onClick: () => void; marker: string }) {
    return (
        <button className={`nivelacija-sort-btn ${props.right ? "align-right" : ""}`} onClick={props.onClick}>
            {props.label}
            {props.marker}
        </button>
    );
}

export default function ProdajaPrePostNivelacijePage() {
    const navigate = useNavigate();
    const location = useLocation();
    const [filterMode, setFilterMode] = useState<FilterMode>("nivelacija");
    const [fromDate, setFromDate] = useState(() => {
        const d = new Date();
        d.setDate(d.getDate() - 90);
        return toDateInput(d);
    });
    const [toDate, setToDate] = useState(() => toDateInput(new Date()));
    const [selectedEventDate, setSelectedEventDate] = useState("");
    const [selectedVendorId, setSelectedVendorId] = useState<number | null>(null);
    const [selectedCategory, setSelectedCategory] = useState("");
    const [includeInactive, setIncludeInactive] = useState(false);
    const [chartMetric, setChartMetric] = useState<ChartMetric>("revenue");
    const [vendorSort, setVendorSort] = useState<{ field: VendorSortField; direction: SortDirection }>({
        field: "changeRevenue",
        direction: "desc",
    });
    const [articleSort, setArticleSort] = useState<{ field: ArticleSortField; direction: SortDirection }>({
        field: "changeRevenue",
        direction: "desc",
    });

    const [vendors, setVendors] = useState<Dobavljac[]>([]);
    const [nivelacijaOptions, setNivelacijaOptions] = useState<VendorSalesNivelacijaOption[]>([]);
    const [loadingOptions, setLoadingOptions] = useState(false);
    const [response, setResponse] = useState<VendorSalesNivelacijaResponse | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");
    const [minValidityFilter, setMinValidityFilter] = useState(false);

    const loadOptions = useCallback(async () => {
        setLoadingOptions(true);
        try {
            const options = await getVendorSalesNivelacijaOptions({
                vendorId: selectedVendorId,
                category: selectedCategory || null,
                take: 365,
            });
            setNivelacijaOptions(options);
        } catch {
            setNivelacijaOptions([]);
        } finally {
            setLoadingOptions(false);
        }
    }, [selectedVendorId, selectedCategory]);

    const load = useCallback(async () => {
        if (filterMode === "nivelacija" && !selectedEventDate) {
            return;
        }

        setLoading(true);
        setError("");
        try {
            const query: {
                vendorId: number | null;
                eventDate?: string | null;
                from?: string | null;
                to?: string | null;
                category: string | null;
                includeInactive: boolean;
            } = {
                vendorId: selectedVendorId,
                category: selectedCategory || null,
                includeInactive,
            };

            if (filterMode === "nivelacija") {
                query.eventDate =
                    selectedEventDate && selectedEventDate !== ALL_EVENTS_OPTION
                        ? selectedEventDate
                        : null;
            } else {
                const { from, to } = toQueryDateRange(fromDate, toDate);
                query.from = from;
                query.to = to;
            }

            const data = await getVendorSalesNivelacija(query);
            setResponse(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : "Greska pri ucitavanju izvestaja.");
            setResponse(null);
        } finally {
            setLoading(false);
        }
    }, [filterMode, selectedEventDate, fromDate, toDate, selectedVendorId, selectedCategory, includeInactive]);

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
        void loadOptions();
    }, [loadOptions]);

    useEffect(() => {
        void load();
    }, [load]);

    const vendorStatsRaw = response?.vendorStats ?? [];
    const articleStatsRaw = response?.articleStats ?? [];
    const categories = response?.categories ?? [];
    const categoryStats = response?.categoryStats ?? [];
    const priceDirectionStats = response?.priceDirectionStats ?? [];
    const insights = response?.insights ?? [];
    const dataQuality = response?.dataQuality ?? null;

    const normalizedVendorStats = useMemo(
        () => vendorStatsRaw.map((x) => ({ ...x, vendorName: normalizeVendorName(x.vendorName) })),
        [vendorStatsRaw]
    );

    const filteredArticleStats = useMemo(() => {
        const normalized = articleStatsRaw.map((x) => ({ ...x, vendorName: normalizeVendorName(x.vendorName) }));
        if (!minValidityFilter) return normalized;
        return normalized.filter((x) => x.preQty >= 3 || Number(x.preRevenue) >= 20000);
    }, [articleStatsRaw, minValidityFilter]);

    const nivelacijaSelectOptions = useMemo(
        () =>
            nivelacijaOptions.map((x) => ({
                ...x,
                value: x.eventDate.slice(0, 10),
            })),
        [nivelacijaOptions]
    );

    useEffect(() => {
        if (filterMode !== "nivelacija") return;
        if (selectedEventDate) return;
        if (nivelacijaSelectOptions.length === 0) return;
        const preferred = nivelacijaSelectOptions.find((x) => x.hasSalesWindow) ?? nivelacijaSelectOptions[0];
        setSelectedEventDate(preferred.value);
    }, [filterMode, selectedEventDate, nivelacijaSelectOptions]);

    useEffect(() => {
        if (selectedEventDate === ALL_EVENTS_OPTION) return;
        if (!selectedEventDate) return;
        const hasSelection = nivelacijaSelectOptions.some((x) => x.value === selectedEventDate);
        if (!hasSelection) setSelectedEventDate("");
    }, [nivelacijaSelectOptions, selectedEventDate]);

    const sortedVendorStats = useMemo(() => {
        const get = (x: VendorSalesNivelacijaVendorStat, field: VendorSortField): string | number => {
            if (field === "vendorName") return x.vendorName;
            return Number(x[field]);
        };
        return [...normalizedVendorStats].sort((a, b) => compareSort(get(a, vendorSort.field), get(b, vendorSort.field), vendorSort.direction));
    }, [normalizedVendorStats, vendorSort]);

    const sortedArticleStats = useMemo(() => {
        const get = (x: VendorSalesNivelacijaArticleStat, field: ArticleSortField): string | number => {
            if (field === "eventDate") return new Date(x.eventDate).getTime();
            if (field === "vendorName" || field === "sku" || field === "articleName" || field === "category") return x[field];
            if (field === "oldPrice") return x.oldPrice ?? 0;
            if (field === "newPrice") return x.newPrice ?? 0;
            return Number(x[field]);
        };
        return [...filteredArticleStats].sort((a, b) => compareSort(get(a, articleSort.field), get(b, articleSort.field), articleSort.direction));
    }, [filteredArticleStats, articleSort]);

    const vendorChartData = useMemo(
        () =>
            sortedVendorStats.slice(0, 12).map((x) => ({
                name: x.vendorName,
                preValue: chartMetric === "revenue" ? safeNumber(x.preRevenue) : safeNumber(x.preQty),
                postValue: chartMetric === "revenue" ? safeNumber(x.postRevenue) : safeNumber(x.postQty),
            })),
        [sortedVendorStats, chartMetric]
    );

    const articleChartData = useMemo(
        () =>
            sortedArticleStats.slice(0, 20).map((x) => ({
                name: `${x.articleName} (${x.sku})`,
                preValue: chartMetric === "revenue" ? safeNumber(x.preRevenue) : safeNumber(x.preQty),
                postValue: chartMetric === "revenue" ? safeNumber(x.postRevenue) : safeNumber(x.postQty),
            })),
        [sortedArticleStats, chartMetric]
    );

    const topGrowth = useMemo(() => {
        const delta = (x: VendorSalesNivelacijaArticleStat) => (chartMetric === "revenue" ? Number(x.changeRevenue) : x.changeQty);
        return [...filteredArticleStats].filter((x) => delta(x) > 0).sort((a, b) => delta(b) - delta(a)).slice(0, 5);
    }, [filteredArticleStats, chartMetric]);

    const topDrop = useMemo(() => {
        const delta = (x: VendorSalesNivelacijaArticleStat) => (chartMetric === "revenue" ? Number(x.changeRevenue) : x.changeQty);
        return [...filteredArticleStats].filter((x) => delta(x) < 0).sort((a, b) => delta(a) - delta(b)).slice(0, 5);
    }, [filteredArticleStats, chartMetric]);

    const priceBucketStats = useMemo(() => {
        const buckets: Record<PriceBucket, { count: number; changeQty: number; changeRevenue: number }> = {
            "-5%→-10%": { count: 0, changeQty: 0, changeRevenue: 0 },
            "-10%→-20%": { count: 0, changeQty: 0, changeRevenue: 0 },
            "<-20%": { count: 0, changeQty: 0, changeRevenue: 0 },
            "+5%→+10%": { count: 0, changeQty: 0, changeRevenue: 0 },
            ">+10%": { count: 0, changeQty: 0, changeRevenue: 0 },
            "Ostalo": { count: 0, changeQty: 0, changeRevenue: 0 },
        };
        for (const x of filteredArticleStats) {
            const b = priceBucket(x.priceChangePercent);
            buckets[b].count++;
            buckets[b].changeQty += x.changeQty;
            buckets[b].changeRevenue += Number(x.changeRevenue);
        }
        return Object.entries(buckets)
            .filter(([, s]) => s.count > 0)
            .map(([bucket, s]) => ({ bucket, ...s }));
    }, [filteredArticleStats]);

    const setSortVendor = (field: VendorSortField) => {
        setVendorSort((prev) =>
            prev.field === field ? { field, direction: prev.direction === "asc" ? "desc" : "asc" } : { field, direction: field === "vendorName" ? "asc" : "desc" }
        );
    };

    const setSortArticle = (field: ArticleSortField) => {
        const text = field === "vendorName" || field === "sku" || field === "articleName" || field === "category";
        setArticleSort((prev) =>
            prev.field === field ? { field, direction: prev.direction === "asc" ? "desc" : "asc" } : { field, direction: text ? "asc" : "desc" }
        );
    };

    const tooltipFormatter = useCallback((value: number | string | readonly (number | string)[] | undefined, name: string | undefined): [string, string] => {
        const normalized = normalizeTooltipValue(value);
        const safe = name ?? "";
        if (safe.includes("Promet")) return [fmtRsd(normalized), safe];
        if (safe.includes("Kolicina")) return [fmtQty(normalized), safe];
        return [normalized.toLocaleString("sr-RS"), safe];
    }, []);

    const preSeriesName = chartMetric === "revenue" ? "Promet pre" : "Kolicina pre";
    const postSeriesName = chartMetric === "revenue" ? "Promet posle" : "Kolicina posle";
    const metricWord = chartMetric === "revenue" ? "prometa" : "kolicine";
    const metricDelta = (x: VendorSalesNivelacijaArticleStat) => (chartMetric === "revenue" ? Number(x.changeRevenue) : x.changeQty);
    const formatMetricDelta = (v: number) => (chartMetric === "revenue" ? fmtRsd(v) : fmtQty(v));
    const articleExtraColumns = 13;

    const sharedFilters = useMemo<AnalyticsNamedValue[]>(
        () => [
            { key: "filterMode", label: "Nacin filtriranja", value: filterMode },
            { key: "selectedEventDate", label: "Nivelacija", value: selectedEventDate || "" },
            { key: "fromDate", label: "Od datuma", value: fromDate },
            { key: "toDate", label: "Do datuma", value: toDate },
            { key: "vendorId", label: "Dobavljac ID", value: selectedVendorId ?? "" },
            { key: "category", label: "Kategorija", value: selectedCategory || "" },
            { key: "includeInactive", label: "Ukljuci neaktivne", value: includeInactive },
            { key: "minValidityFilter", label: "Samo validni uzorci", value: minValidityFilter },
        ],
        [filterMode, fromDate, includeInactive, minValidityFilter, selectedCategory, selectedEventDate, selectedVendorId, toDate]
    );

    const sharedMetadata = useMemo<AnalyticsNamedValue[]>(
        () => [
            { key: "generatedAt", label: "Generisano", value: response?.generatedAt ?? "" },
            { key: "windowDays", label: "Window days", value: response?.windowDays ?? "" },
            { key: "avgMomentumRevenue", label: "Avg momentum", value: response?.avgMomentumRevenue ?? "" },
            { key: "avgDidRevenue", label: "Avg DiD promet", value: response?.avgDidRevenue ?? "" },
            { key: "avgElasticity", label: "Avg elasticnost", value: response?.avgElasticity ?? "" },
            { key: "metricsStatus", label: "Metrics status", value: response?.metricsStatus ?? "OK" },
        ],
        [response?.avgDidRevenue, response?.avgElasticity, response?.avgMomentumRevenue, response?.generatedAt, response?.metricsStatus, response?.windowDays]
    );

    const openSnapshotDetail = useCallback(<Row,>(
        table: string,
        recordId: string,
        title: string,
        subtitle: string,
        columns: AnalyticsTableColumn<Row>[],
        row: Row
    ) => {
        saveAnalyticsDetailSnapshot(
            buildAnalyticsDetailSnapshot({
                table,
                recordId,
                title,
                subtitle,
                columns,
                row,
                metadata: [...sharedFilters, ...sharedMetadata],
            })
        );

        navigate(`/analitika/${table}/${encodeURIComponent(recordId)}`, {
            state: { backgroundLocation: location },
        });
    }, [location, navigate, sharedFilters, sharedMetadata]);

    const metricText = (valueText: string, reason?: string | null) =>
        valueText === "N/A" && reason
            ? `${valueText} (${reason})`
            : valueText;

    const exportCsv = () => {
        if (!response) return;
        const quality = response.dataQuality ?? {
            rawRows: 0,
            deduplicatedRows: 0,
            duplicateRowsRemoved: 0,
            inactiveRows: 0,
            unchangedPriceRows: 0,
            analyzedRows: 0,
            analyzedSharePercent: 0,
        };
        const lines: string[] = [];
        lines.push("Meta");
        lines.push(`GeneratedAt,${response.generatedAt}`);
        lines.push(`FilterMode,${filterMode}`);
        lines.push(`IncludeInactive,${includeInactive}`);
        lines.push(`DataQualityRawRows,${quality.rawRows}`);
        lines.push(`DataQualityDedupRows,${quality.deduplicatedRows}`);
        lines.push(`DataQualityAnalyzedRows,${quality.analyzedRows}`);
        lines.push(`DataQualityAnalyzedSharePercent,${Number(quality.analyzedSharePercent).toFixed(2)}`);
        lines.push(`AvgMomentumRevenue,${response.avgMomentumRevenue != null ? Number(response.avgMomentumRevenue).toFixed(2) : ""}`);
        lines.push(`AvgElasticity,${response.avgElasticity != null ? Number(response.avgElasticity).toFixed(4) : ""}`);
        lines.push(`AvgDidRevenue,${response.avgDidRevenue != null ? Number(response.avgDidRevenue).toFixed(2) : ""}`);
        lines.push(`AvgLostSalesOOS,${response.avgLostSalesOOS != null ? Number(response.avgLostSalesOOS).toFixed(2) : ""}`);
        lines.push(`AvgOOSRate,${response.oosRate != null ? Number(response.oosRate).toFixed(4) : ""}`);
        lines.push(`MetricsStatus,${csvEscape(response.metricsStatus ?? "")}`);
        lines.push("");
        lines.push("Prodaja po dobavljacima (pre/post nivelacije)");
        lines.push("DobavljacId,Dobavljac,PreKolicina,PostKolicina,PromenaKolicine,PrePromet,PostPromet,PromenaPrometa,PromenaProcenat,BrojArtikala");
        for (const x of sortedVendorStats) {
            lines.push(
                [x.vendorId ?? "", csvEscape(x.vendorName), x.preQty, x.postQty, x.changeQty, Number(x.preRevenue).toFixed(2), Number(x.postRevenue).toFixed(2), Number(x.changeRevenue).toFixed(2), Number(x.changePercent).toFixed(2), x.articleCount].join(",")
            );
        }
        lines.push("");
        lines.push("Prodaja po artiklima (pre/post nivelacije)");
        lines.push("DatumNivelacije,DobavljacId,Dobavljac,SKU,Artikal,Kategorija,StaraCena,NovaCena,PreKolicina,PostKolicina,PromenaKolicine,PrePromet,PostPromet,PromenaPrometa,PromenaProcenat,ImaProdajuProzor,PromenaCene,PromenaCeneProcenat,Klasifikacija,LogRast,Pouzdanost,Akcija,Rolling7dPreRevenue,Rolling7dPostRevenue,MomentumRevenue,OOSRatePct,LostSalesOOS,DiDRevenue,DiDQty,PriceElasticity,MetricReason");
        for (const x of sortedArticleStats) {
            lines.push(
                [
                    x.eventDate,
                    x.vendorId ?? "",
                    csvEscape(x.vendorName),
                    csvEscape(x.sku),
                    csvEscape(x.articleName),
                    csvEscape(x.category),
                    x.oldPrice != null ? Number(x.oldPrice).toFixed(2) : "",
                    x.newPrice != null ? Number(x.newPrice).toFixed(2) : "",
                    x.preQty,
                    x.postQty,
                    x.changeQty,
                    Number(x.preRevenue).toFixed(2),
                    Number(x.postRevenue).toFixed(2),
                    Number(x.changeRevenue).toFixed(2),
                    Number(x.changePercent).toFixed(2),
                    x.hasSalesWindow ? "1" : "0",
                    x.priceChanged ? "1" : "0",
                    x.priceChangePercent != null ? Number(x.priceChangePercent).toFixed(2) : "",
                    csvEscape(skuClassify(x)),
                    (() => { const lg = logGrowthPct(Number(x.preRevenue), Number(x.postRevenue)); return lg != null ? lg.toFixed(2) : ""; })(),
                    confidenceScore(x),
                    actionRecommendation(x),
                    x.rolling7dPreRevenue != null ? Number(x.rolling7dPreRevenue).toFixed(2) : "",
                    x.rolling7dPostRevenue != null ? Number(x.rolling7dPostRevenue).toFixed(2) : "",
                    x.momentumRevenue != null ? Number(x.momentumRevenue).toFixed(2) : "",
                    x.oosRate != null ? Number(x.oosRate * 100).toFixed(2) : "",
                    x.lostSalesOOS != null ? Number(x.lostSalesOOS).toFixed(2) : "",
                    x.didRevenue != null ? Number(x.didRevenue).toFixed(2) : "",
                    x.didQty != null ? Number(x.didQty).toFixed(2) : "",
                    x.priceElasticity != null ? Number(x.priceElasticity).toFixed(4) : "",
                    csvEscape(x.metricReason ?? ""),
                ].join(",")
            );
        }
        const blob = new Blob([lines.join("\n")], { type: "text/csv;charset=utf-8;" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        const scope = filterMode === "nivelacija"
            ? selectedEventDate === ALL_EVENTS_OPTION
                ? "sve-nivelacije"
                : selectedEventDate || "najnovija-nivelacija"
            : `${fromDate}-${toDate}`;
        a.download = `prodaja-pre-post-nivelacije-${scope}.csv`;
        a.click();
        URL.revokeObjectURL(url);
    };

    return (
        <div className="nivelacija-page">
            <h1 className="nivelacija-title">Prodaja pre i posle nivelacije</h1>
            <p className="nivelacija-subtitle">Izvestaj za period 30 dana pre i 30 dana posle promene cene, sa deduplikacijom i kontrolom kvaliteta uzorka po artiklu i dobavljacu.</p>

            <div className="nivelacija-filterbar">
                <div className="nivelacija-field nivelacija-mode-field">
                    <label className="nivelacija-label">Nacin filtriranja</label>
                    <div className="nivelacija-mode-group">
                        <button
                            className={`nivelacija-mode-btn ${filterMode === "nivelacija" ? "active" : ""}`}
                            onClick={() => setFilterMode("nivelacija")}
                            type="button"
                        >
                            Po nivelaciji
                        </button>
                        <button
                            className={`nivelacija-mode-btn ${filterMode === "period" ? "active" : ""}`}
                            onClick={() => setFilterMode("period")}
                            type="button"
                        >
                            Po periodu
                        </button>
                    </div>
                </div>
                {filterMode === "period" ? (
                    <>
                        <div className="nivelacija-field">
                            <label className="nivelacija-label">Od datuma nivelacije</label>
                            <input className="nivelacija-input" type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
                        </div>
                        <div className="nivelacija-field">
                            <label className="nivelacija-label">Do datuma nivelacije</label>
                            <input className="nivelacija-input" type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
                        </div>
                    </>
                ) : (
                    <div className="nivelacija-field">
                        <label className="nivelacija-label">
                            Ucitana nivelacija
                            {loadingOptions && <span className="nivelacija-options-loading"> (ucitavam...)</span>}
                        </label>
                        <div className="nivelacija-options-row">
                            <select
                                className="nivelacija-input"
                                value={selectedEventDate}
                                onChange={(e) => setSelectedEventDate(e.target.value)}
                                disabled={loadingOptions}
                            >
                                {nivelacijaSelectOptions.length === 0 && !loadingOptions && (
                                    <option value="" disabled>Nema nivelacija u bazi</option>
                                )}
                                {nivelacijaSelectOptions.length > 0 && (
                                    <option value={ALL_EVENTS_OPTION}>Sve ucitane nivelacije</option>
                                )}
                                {nivelacijaSelectOptions.map((opt) => (
                                    <option key={opt.value} value={opt.value}>
                                        {opt.label}
                                        {!opt.hasSalesWindow ? " (bez prodaje u +/-30 dana)" : ""}
                                    </option>
                                ))}
                            </select>
                            <button
                                className="nivelacija-btn nivelacija-btn-secondary nivelacija-refresh-btn"
                                onClick={() => void loadOptions()}
                                disabled={loadingOptions}
                                title="Osvezi listu nivelacija"
                                type="button"
                            >&var(--c-8635, #8635);</button>
                        </div>
                    </div>
                )}
                <div className="nivelacija-field">
                    <label className="nivelacija-label">Dobavljac</label>
                    <select className="nivelacija-input" value={selectedVendorId ?? ""} onChange={(e) => setSelectedVendorId(e.target.value === "" ? null : Number(e.target.value))}>
                        <option value="">Svi dobavljaci</option>
                        {vendors.map((v) => (
                            <option key={v.id} value={v.id}>{v.naziv}</option>
                        ))}
                    </select>
                </div>
                <div className="nivelacija-field">
                    <label className="nivelacija-label">Kategorija</label>
                    <select className="nivelacija-input" value={selectedCategory} onChange={(e) => setSelectedCategory(e.target.value)}>
                        <option value="">Sve kategorije</option>
                        {categories.map((c) => (
                            <option key={c} value={c}>{c}</option>
                        ))}
                    </select>
                </div>
                <div className="nivelacija-field">
                    <label className="nivelacija-label">Kvalitet uzorka</label>
                    <label className="nivelacija-checkbox-row">
                        <input
                            type="checkbox"
                            checked={includeInactive}
                            onChange={(e) => setIncludeInactive(e.target.checked)}
                        />
                        Ukljuci artikle bez prodaje u +/-30 dana
                    </label>
                    <label className="nivelacija-checkbox-row">
                        <input
                            type="checkbox"
                            checked={minValidityFilter}
                            onChange={(e) => setMinValidityFilter(e.target.checked)}
                        />
                        Samo validni uzorci (pre qty ≥ 3 ili promet ≥ 20.000 RSD)
                    </label>
                </div>
                <div className="nivelacija-actions">
                    <button className="nivelacija-btn nivelacija-btn-primary" onClick={() => void load()} disabled={loading}>Primeni</button>
                    <button className="nivelacija-btn nivelacija-btn-secondary" onClick={() => {
                        const d = new Date();
                        const start = new Date();
                        start.setDate(start.getDate() - 90);
                        setFromDate(toDateInput(start));
                        setToDate(toDateInput(d));
                        setSelectedEventDate("");
                        setSelectedVendorId(null);
                        setSelectedCategory("");
                        setIncludeInactive(false);
                        setMinValidityFilter(false);
                    }} disabled={loading}>Ocisti</button>
                    <button className="nivelacija-btn nivelacija-btn-export" onClick={exportCsv} disabled={loading || !response}>Izvezi CSV</button>
                </div>
            </div>

            <div className="nivelacija-metric-switch">
                <span className="nivelacija-label">Grafikoni:</span>
                <div className="nivelacija-metric-group">
                    <button className={`nivelacija-metric-btn ${chartMetric === "revenue" ? "active" : ""}`} onClick={() => setChartMetric("revenue")}>Promet (RSD)</button>
                    <button className={`nivelacija-metric-btn ${chartMetric === "qty" ? "active" : ""}`} onClick={() => setChartMetric("qty")}>Kolicina (kom)</button>
                </div>
            </div>

            {!loading && !error && response && (response.totals?.articlesCount ?? 0) === 0 && (
                <div className="nivelacija-empty-state">
                    Nema podataka za izabrane filtere. Proverite da li postoji prodaja u periodu 30 dana pre/posle izabrane nivelacije.
                </div>
            )}
            {error && <div className="error-state">{error}</div>}

            {!!response && !loading && dataQuality && (
                <div className="nivelacija-quality">
                    <div className="nivelacija-quality-title">Kvalitet podataka</div>
                    <div className="nivelacija-quality-row">
                        <span>Analizirani redovi:</span>
                        <strong>{dataQuality.analyzedRows} / {dataQuality.deduplicatedRows} ({fmtPct(Number(dataQuality.analyzedSharePercent))})</strong>
                    </div>
                    <div className="nivelacija-quality-row">
                        <span>Uklonjeni duplikati:</span>
                        <strong>{dataQuality.duplicateRowsRemoved}</strong>
                    </div>
                    <div className="nivelacija-quality-row">
                        <span>Bez prodaje u +/-30 dana:</span>
                        <strong>{dataQuality.inactiveRows}</strong>
                    </div>
                    <div className="nivelacija-quality-row">
                        <span>Bez realne promene cene:</span>
                        <strong>{dataQuality.unchangedPriceRows}</strong>
                    </div>
                </div>
            )}

            {!!response && !loading && insights.length > 0 && (
                <div className="nivelacija-insights-grid">
                    {insights.slice(0, 6).map((item, idx) => (
                        <div key={`${item.title}-${idx}`} className="nivelacija-insight-card">
                            <div className="nivelacija-insight-title">{item.title}</div>
                            <div className={`nivelacija-insight-value tone-${item.tone}`}>{item.value}</div>
                            <div className="nivelacija-insight-details">{item.details}</div>
                        </div>
                    ))}
                </div>
            )}

            {!!response && !loading && (
                <div className="nivelacija-kpis">
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">Ukupan promet pre</div><div className="nivelacija-kpi-value">{fmtRsd(Number(response.totals.preRevenue))}</div></div>
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">Ukupan promet posle</div><div className="nivelacija-kpi-value">{fmtRsd(Number(response.totals.postRevenue))}</div></div>
                    <div className="nivelacija-kpi">
                        <div className="nivelacija-kpi-label">Promena prometa</div>
                        <div className="nivelacija-kpi-value">{fmtRsd(Number(response.totals.changeRevenue))}</div>
                        <div className={`nivelacija-kpi-delta ${Number(response.totals.changeRevenue) >= 0 ? "delta-pos" : "delta-neg"}`}>{fmtPct(Number(response.totals.changePercent))}</div>
                    </div>
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">Prosecno po artiklu (pre)</div><div className="nivelacija-kpi-value">{fmtRsd(Number(response.totals.avgRevenuePerArticlePre))}</div></div>
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">Prosecno po artiklu (posle)</div><div className="nivelacija-kpi-value">{fmtRsd(Number(response.totals.avgRevenuePerArticlePost))}</div></div>
                    <div className="nivelacija-kpi">
                        <div className="nivelacija-kpi-label">Prosecna promena cene</div>
                        <div className="nivelacija-kpi-value">{fmtPct(Number(response.totals.avgPriceChangePercent))}</div>
                    </div>
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">Obuhvat</div><div className="nivelacija-kpi-value">{response.totals?.vendorsCount ?? 0} dobavljaca / {response.totals?.articlesCount ?? 0} artikala</div></div>
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">Aktivni artikli</div><div className="nivelacija-kpi-value">{response.totals.activeArticlesCount}</div></div>
                </div>
            )}

            {!!response && !loading && (
                <div className="nivelacija-kpis nivelacija-kpis-advanced">
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">⚡ Prosecan momentum</div><div className="nivelacija-kpi-value">{fmtNullableRsd(response.avgMomentumRevenue)}</div></div>
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">🟦 Prosecan DiD promet</div><div className="nivelacija-kpi-value">{fmtNullableRsd(response.avgDidRevenue)}</div></div>
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">📉 Prosecna elasticnost</div><div className="nivelacija-kpi-value">{fmtNullableElasticity(response.avgElasticity)}</div></div>
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">🚨 OOS stopa</div><div className="nivelacija-kpi-value">{fmtNullableSharePct(response.oosRate)}</div></div>
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">💸 Prosecna izgubljena prodaja (OOS)</div><div className="nivelacija-kpi-value">{fmtNullableRsd(response.avgLostSalesOOS)}</div></div>
                    <div className="nivelacija-kpi"><div className="nivelacija-kpi-label">📊 Status metrika</div><div className="nivelacija-kpi-value">{response.metricsStatus ? "Partial" : "OK"}</div></div>
                </div>
            )}

            {!!response?.metricsStatus && !loading && (
                <div className="nivelacija-metrics-status">
                    ⚠️ {response.metricsStatus}
                </div>
            )}

            <div className="nivelacija-top-grid">
                <div className="nivelacija-card">
                    <h3 className="nivelacija-card-title">Najveci rast {metricWord}</h3>
                    <ul className="nivelacija-top-list">
                        {topGrowth.length === 0 && <li className="empty-state compact">Nema rasta.</li>}
                        {topGrowth.map((x, i) => (
                            <li key={`g-${x.sku}-${i}`} className="nivelacija-top-item">
                                <div className="top-main">{x.articleName}</div>
                                <div className="top-meta">{x.vendorName} | {x.sku}</div>
                                <div className="top-value delta-pos">{formatMetricDelta(metricDelta(x))}</div>
                            </li>
                        ))}
                    </ul>
                </div>
                <div className="nivelacija-card">
                    <h3 className="nivelacija-card-title">Najveci pad {metricWord}</h3>
                    <ul className="nivelacija-top-list">
                        {topDrop.length === 0 && <li className="empty-state compact">Nema pada.</li>}
                        {topDrop.map((x, i) => (
                            <li key={`d-${x.sku}-${i}`} className="nivelacija-top-item">
                                <div className="top-main">{x.articleName}</div>
                                <div className="top-meta">{x.vendorName} | {x.sku}</div>
                                <div className="top-value delta-neg">{formatMetricDelta(metricDelta(x))}</div>
                            </li>
                        ))}
                    </ul>
                </div>
            </div>

            {(categoryStats.length > 0 || priceDirectionStats.length > 0) && (
                <div className="nivelacija-segment-grid">
                    <div className="nivelacija-card">
                        <h3 className="nivelacija-card-title">Segmentacija po kategorijama</h3>
                        <div className="mb-3">
                            <AnalyticsTableToolbar
                                tableKey="vendor-sales-nivelacija-categories"
                                tableTitle="Prodaja pre/post nivelacije - kategorije"
                                columns={categoryDetailColumns}
                                rows={categoryStats.slice(0, 12)}
                                filters={sharedFilters}
                                metadata={sharedMetadata}
                                defaultOrientation="landscape"
                            />
                        </div>
                        <div className="nivelacija-scroll">
                            <table className="nivelacija-table">
                                <thead>
                                    <tr>
                                        <th>Kategorija</th>
                                        <th className="align-right">Artikli</th>
                                        <th className="align-right">Dobavljaci</th>
                                        <th className="align-right">Promena prometa</th>
                                        <th className="align-right">Promena %</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {categoryStats.slice(0, 12).map((x) => (
                                        <tr
                                            key={x.category}
                                            className="cursor-pointer"
                                            onClick={() => openSnapshotDetail("vendor-sales-nivelacija-categories", x.category, x.category, "Segmentacija po kategorijama", categoryDetailColumns, x)}
                                        >
                                            <td>{x.category}</td>
                                            <td className="align-right">{x.articlesCount}</td>
                                            <td className="align-right">{x.vendorsCount}</td>
                                            <td className={`align-right ${Number(x.changeRevenue) >= 0 ? "delta-pos" : "delta-neg"}`}>{fmtRsd(Number(x.changeRevenue))}</td>
                                            <td className={`align-right ${Number(x.changePercent) >= 0 ? "delta-pos" : "delta-neg"}`}>{fmtPct(Number(x.changePercent))}</td>
                                        </tr>
                                    ))}
                                    {categoryStats.length === 0 && <tr><td colSpan={5} className="empty-state">Nema segmentacije po kategorijama.</td></tr>}
                                </tbody>
                            </table>
                        </div>
                    </div>
                    <div className="nivelacija-card">
                        <h3 className="nivelacija-card-title">Segmentacija po smeru cene</h3>
                        <div className="mb-3">
                            <AnalyticsTableToolbar
                                tableKey="vendor-sales-nivelacija-price-direction"
                                tableTitle="Prodaja pre/post nivelacije - smer cene"
                                columns={priceDirectionDetailColumns}
                                rows={priceDirectionStats}
                                filters={sharedFilters}
                                metadata={sharedMetadata}
                                defaultOrientation="landscape"
                            />
                        </div>
                        <div className="nivelacija-scroll">
                            <table className="nivelacija-table">
                                <thead>
                                    <tr>
                                        <th>Segment</th>
                                        <th className="align-right">Artikli</th>
                                        <th className="align-right">Dobavljaci</th>
                                        <th className="align-right">Prosecna promena cene</th>
                                        <th className="align-right">Promena prometa</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {priceDirectionStats.map((x) => (
                                        <tr
                                            key={x.segment}
                                            className="cursor-pointer"
                                            onClick={() => openSnapshotDetail("vendor-sales-nivelacija-price-direction", x.segment, x.segment, "Segmentacija po smeru cene", priceDirectionDetailColumns, x)}
                                        >
                                            <td>{x.segment}</td>
                                            <td className="align-right">{x.articlesCount}</td>
                                            <td className="align-right">{x.vendorsCount}</td>
                                            <td className={`align-right ${Number(x.avgPriceChangePercent) >= 0 ? "delta-pos" : "delta-neg"}`}>{fmtPct(Number(x.avgPriceChangePercent))}</td>
                                            <td className={`align-right ${Number(x.changeRevenue) >= 0 ? "delta-pos" : "delta-neg"}`}>{fmtRsd(Number(x.changeRevenue))}</td>
                                        </tr>
                                    ))}
                                    {priceDirectionStats.length === 0 && <tr><td colSpan={5} className="empty-state">Nema segmentacije po promeni cene.</td></tr>}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
            )}

            {priceBucketStats.length > 0 && (
                <div className="nivelacija-table-wrap">
                    <h3 className="nivelacija-table-title">Segmentacija po cenovnom razredu (Price Bucket)</h3>
                    <div className="mb-3">
                        <AnalyticsTableToolbar
                            tableKey="vendor-sales-nivelacija-price-buckets"
                            tableTitle="Prodaja pre/post nivelacije - price bucket"
                            columns={priceBucketDetailColumns}
                            rows={priceBucketStats}
                            filters={sharedFilters}
                            metadata={sharedMetadata}
                            defaultOrientation="landscape"
                        />
                    </div>
                    <div className="nivelacija-scroll">
                        <table className="nivelacija-table">
                            <thead>
                                <tr>
                                    <th>Razred promene cene</th>
                                    <th className="align-right">Broj SKU</th>
                                    <th className="align-right">Promena kolicine</th>
                                    <th className="align-right">Promena prometa</th>
                                </tr>
                            </thead>
                            <tbody>
                                {priceBucketStats.map((b) => (
                                    <tr
                                        key={b.bucket}
                                        className="cursor-pointer"
                                        onClick={() => openSnapshotDetail("vendor-sales-nivelacija-price-buckets", b.bucket, b.bucket, "Segmentacija po cenovnom razredu", priceBucketDetailColumns, b)}
                                    >
                                        <td><span className={`bucket-badge bucket-${b.bucket.replace(/[^a-zA-Z0-9]/g, "-")}`}>{b.bucket}</span></td>
                                        <td className="align-right">{b.count}</td>
                                        <td className={`align-right ${b.changeQty >= 0 ? "delta-pos" : "delta-neg"}`}>{b.changeQty}</td>
                                        <td className={`align-right ${b.changeRevenue >= 0 ? "delta-pos" : "delta-neg"}`}>{fmtRsd(b.changeRevenue)}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>
            )}

            <div className="nivelacija-grid">
                <div className="nivelacija-card">
                    <h3 className="nivelacija-card-title">Pre vs posle po dobavljacu</h3>
                    <ResponsiveContainer width="100%" height={320} minWidth={0} minHeight={0}>
                        <BarChart data={vendorChartData}>
                            <CartesianGrid strokeDasharray="3 3" stroke="var(--c-374151, #374151)" />
                            <XAxis dataKey="name" tick={{ fill: "var(--c-d1d5db, #d1d5db)", fontSize: 12 }} />
                            <YAxis tick={{ fill: "var(--c-d1d5db, #d1d5db)", fontSize: 12 }} />
                            <Tooltip contentStyle={{ background: "var(--c-0b1220, #0b1220)", border: "1px solid var(--c-374151, #374151)", color: "var(--c-e5e7eb, #e5e7eb)" }} formatter={tooltipFormatter} />
                            <Legend />
                            <Bar dataKey="preValue" name={preSeriesName} fill="var(--c-60a5fa, #60a5fa)" />
                            <Bar dataKey="postValue" name={postSeriesName} fill="var(--c-34d399, #34d399)" />
                        </BarChart>
                    </ResponsiveContainer>
                </div>
                <div className="nivelacija-card">
                    <h3 className="nivelacija-card-title">Pre vs posle po artiklima (Top 20)</h3>
                    <ResponsiveContainer width="100%" height={320} minWidth={0} minHeight={0}>
                        <LineChart data={articleChartData}>
                            <CartesianGrid strokeDasharray="3 3" stroke="var(--c-374151, #374151)" />
                            <XAxis dataKey="name" tick={{ fill: "var(--c-d1d5db, #d1d5db)", fontSize: 11 }} interval={0} angle={-20} textAnchor="end" height={80} tickFormatter={(value: string) => (value.length > 24 ? `${value.slice(0, 24)}...` : value)} />
                            <YAxis tick={{ fill: "var(--c-d1d5db, #d1d5db)", fontSize: 12 }} />
                            <Tooltip contentStyle={{ background: "var(--c-0b1220, #0b1220)", border: "1px solid var(--c-374151, #374151)", color: "var(--c-e5e7eb, #e5e7eb)" }} formatter={tooltipFormatter} />
                            <Legend />
                            <Line type="monotone" dataKey="preValue" name={preSeriesName} stroke="var(--c-60a5fa, #60a5fa)" strokeWidth={2} />
                            <Line type="monotone" dataKey="postValue" name={postSeriesName} stroke="var(--c-34d399, #34d399)" strokeWidth={2} />
                        </LineChart>
                    </ResponsiveContainer>
                </div>
            </div>

            <div className="nivelacija-table-wrap">
                <h3 className="nivelacija-table-title">Prodaja po dobavljacima</h3>
                <div className="mb-3">
                    <AnalyticsTableToolbar
                        tableKey="vendor-sales-nivelacija-vendors"
                        tableTitle="Prodaja pre/post nivelacije - dobavljaci"
                        columns={vendorDetailColumns}
                        rows={sortedVendorStats}
                        filters={sharedFilters}
                        metadata={sharedMetadata}
                        defaultOrientation="landscape"
                    />
                </div>
                <div className="nivelacija-scroll">
                    <table className="nivelacija-table">
                        <thead>
                            <tr>
                                {vendorColumns.map((col) => (
                                    <th key={col.field} className={col.right ? "align-right" : ""}>
                                        <SortButton label={col.label} right={col.right} onClick={() => setSortVendor(col.field)} marker={sortMark(col.field, vendorSort.field, vendorSort.direction)} />
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {sortedVendorStats.length === 0 && <tr><td colSpan={vendorColumns.length} className="empty-state">Nema podataka za izabrane filtere.</td></tr>}
                            {sortedVendorStats.map((x) => (
                                <tr
                                    key={`${x.vendorId ?? "n/a"}-${x.vendorName}`}
                                    className="cursor-pointer"
                                    onClick={() => openSnapshotDetail("vendor-sales-nivelacija-vendors", String(x.vendorId ?? x.vendorName), x.vendorName, "Prodaja po dobavljacima", vendorDetailColumns, x)}
                                >
                                    <td>{x.vendorName}</td>
                                    <td className="align-right">{x.preQty}</td>
                                    <td className="align-right">{x.postQty}</td>
                                    <td className={`align-right ${x.changeQty >= 0 ? "delta-pos" : "delta-neg"}`}>{x.changeQty}</td>
                                    <td className="align-right">{fmtRsd(Number(x.preRevenue))}</td>
                                    <td className="align-right">{fmtRsd(Number(x.postRevenue))}</td>
                                    <td className={`align-right ${Number(x.changeRevenue) >= 0 ? "delta-pos" : "delta-neg"}`}>{fmtRsd(Number(x.changeRevenue))}</td>
                                    <td className={`align-right ${Number(x.changePercent) >= 0 ? "delta-pos" : "delta-neg"}`}>{fmtPct(Number(x.changePercent))}</td>
                                    <td className="align-right">{x.articleCount}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </div>

            <div className="nivelacija-table-wrap">
                <h3 className="nivelacija-table-title">Prodaja po artiklima</h3>
                <div className="mb-3">
                    <AnalyticsTableToolbar
                        tableKey="vendor-sales-nivelacija-articles"
                        tableTitle="Prodaja pre/post nivelacije - artikli"
                        columns={articleDetailColumns}
                        rows={sortedArticleStats}
                        filters={sharedFilters}
                        metadata={sharedMetadata}
                        defaultOrientation="landscape"
                    />
                </div>
                <div className="nivelacija-scroll">
                    <table className="nivelacija-table">
                        <thead>
                            <tr>
                                {articleColumns.map((col) => (
                                    <th key={col.field} className={col.right ? "align-right" : ""}>
                                        <SortButton label={col.label} right={col.right} onClick={() => setSortArticle(col.field)} marker={sortMark(col.field, articleSort.field, articleSort.direction)} />
                                    </th>
                                ))}
                                <th>Klasifikacija</th>
                                <th className="align-right">Log rast %</th>
                                <th>Pouzdanost</th>
                                <th>Akcija</th>
                                <th className="align-right">Rolling 7d pre</th>
                                <th className="align-right">Rolling 7d posle</th>
                                <th className="align-right">Momentum</th>
                                <th className="align-right">OOS %</th>
                                <th className="align-right">Lost sales (OOS)</th>
                                <th className="align-right">DiD promet</th>
                                <th className="align-right">DiD qty</th>
                                <th className="align-right">Elasticnost</th>
                                <th>Metric reason</th>
                            </tr>
                        </thead>
                        <tbody>
                            {sortedArticleStats.length === 0 && <tr><td colSpan={articleColumns.length + articleExtraColumns} className="empty-state">Nema podataka za izabrane filtere.</td></tr>}
                            {sortedArticleStats.map((x, idx) => (
                                <tr
                                    key={`${x.vendorId ?? "n/a"}-${x.sku}-${x.eventDate}-${idx}`}
                                    className="cursor-pointer"
                                    onClick={() => openSnapshotDetail("vendor-sales-nivelacija-articles", `${x.sku}-${x.eventDate}`, x.articleName, `${x.vendorName} | ${x.sku}`, articleDetailColumns, x)}
                                >
                                    <td>{new Date(x.eventDate).toLocaleDateString("sr-RS")}</td>
                                    <td>{x.vendorName}</td>
                                    <td>{x.sku}</td>
                                    <td>{x.articleName}</td>
                                    <td>{x.category}</td>
                                    <td className="align-right">{x.oldPrice != null ? fmtRsd(Number(x.oldPrice)) : "—"}</td>
                                    <td className="align-right">{x.newPrice != null ? fmtRsd(Number(x.newPrice)) : "—"}</td>
                                    <td className="align-right">{x.preQty}</td>
                                    <td className="align-right">{x.postQty}</td>
                                    <td className={`align-right ${x.changeQty >= 0 ? "delta-pos" : "delta-neg"}`}>{x.changeQty}</td>
                                    <td className="align-right">{fmtRsd(Number(x.preRevenue))}</td>
                                    <td className="align-right">{fmtRsd(Number(x.postRevenue))}</td>
                                    <td className={`align-right ${Number(x.changeRevenue) >= 0 ? "delta-pos" : "delta-neg"}`}>{fmtRsd(Number(x.changeRevenue))}</td>
                                    <td className={`align-right ${Number(x.changePercent) >= 0 ? "delta-pos" : "delta-neg"}`}>{fmtPct(Number(x.changePercent))}</td>
                                    <td>{(() => { const cls = skuClassify(x); return <span className={`sku-badge sku-badge-${cls}`}>{cls}</span>; })()}</td>
                                    <td className="align-right">{(() => { const lg = logGrowthPct(Number(x.preRevenue), Number(x.postRevenue)); return lg != null ? fmtPct(lg) : "—"; })()}</td>
                                    <td>{(() => { const c = confidenceScore(x); return <span className={`confidence-badge confidence-badge-${c}`}>{c}</span>; })()}</td>
                                    <td>{(() => { const a = actionRecommendation(x); return <span className={`action-badge action-badge-${a}`}>{a}</span>; })()}</td>
                                    <td className="align-right"><span title={x.metricReason ?? ""}>{metricText(fmtNullableRsd(x.rolling7dPreRevenue), x.metricReason)}</span></td>
                                    <td className="align-right"><span title={x.metricReason ?? ""}>{metricText(fmtNullableRsd(x.rolling7dPostRevenue), x.metricReason)}</span></td>
                                    <td className={`align-right ${Number(x.momentumRevenue ?? 0) >= 0 ? "delta-pos" : "delta-neg"}`}><span title={x.metricReason ?? ""}>{metricText(fmtNullableRsd(x.momentumRevenue), x.metricReason)}</span></td>
                                    <td className="align-right"><span title={x.metricReason ?? ""}>{metricText(fmtNullableSharePct(x.oosRate), x.metricReason)}</span></td>
                                    <td className="align-right"><span title={x.metricReason ?? ""}>{metricText(fmtNullableRsd(x.lostSalesOOS), x.metricReason)}</span></td>
                                    <td className={`align-right ${Number(x.didRevenue ?? 0) >= 0 ? "delta-pos" : "delta-neg"}`}><span title={x.metricReason ?? ""}>{metricText(fmtNullableRsd(x.didRevenue), x.metricReason)}</span></td>
                                    <td className={`align-right ${Number(x.didQty ?? 0) >= 0 ? "delta-pos" : "delta-neg"}`}><span title={x.metricReason ?? ""}>{metricText(fmtNullableQty(x.didQty), x.metricReason)}</span></td>
                                    <td className="align-right"><span title={x.metricReason ?? ""}>{metricText(fmtNullableElasticity(x.priceElasticity), x.metricReason)}</span></td>
                                    <td><span className="metric-reason-badge" title={x.metricReason ?? ""}>{x.metricReason ?? "OK"}</span></td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    );
}
