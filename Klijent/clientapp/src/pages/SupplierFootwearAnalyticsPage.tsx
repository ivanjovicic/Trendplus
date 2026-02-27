
import { useCallback, useEffect, useMemo, useState } from "react";
import {
    Bar,
    BarChart,
    CartesianGrid,
    Cell,
    Pie,
    PieChart,
    ResponsiveContainer,
    Tooltip,
    Treemap,
    XAxis,
    YAxis,
} from "recharts";
import { getDobavljaci } from "../services/dobavljaciApi";
import {
    getVendorSalesNivelacija,
    getVendorSalesNivelacijaOptions,
    type VendorSalesNivelacijaArticleStat,
    type VendorSalesNivelacijaOption,
    type VendorSalesNivelacijaResponse,
} from "../services/vendorSalesNivelacijaApi";
import type { Dobavljac } from "../types/Dobavljaci";
import "./SupplierFootwearAnalyticsPage.css";

const ALL_EVENTS_OPTION = "__all__";
const DONUT_COLORS = ["#06b6d4", "#14b8a6", "#84cc16", "#f59e0b", "#f97316", "#ef4444", "#a855f7"];

type Direction = "up" | "down" | "flat";
type InsightTone = "pozitivno" | "rizik" | "prilika";

type SupplierDerived = {
    vendorId: number | null;
    vendorName: string;
    preRevenue: number;
    postRevenue: number;
    preQty: number;
    postQty: number;
    changeRevenuePct: number;
    changeQtyPct: number;
    marginPctChange: number;
    logGrowth: number;
    elasticity: number;
    stability: number;
    trend: Direction;
    shiftShare: number;
    prePostProfitLift: number;
    riskDropSales: number;
    score: number;
    opportunityScore: number;
    consistencyScore: number;
    trendPhase: "Rastuci" | "Stagnacija" | "Pad";
    recoveryIndex: number;
    priceSensitivity: number;
    sparkline: number[];
};

type TooltipMetric = {
    key: string;
    naziv: string;
    vrednost: string;
    opis: string;
};

type InsightCard = {
    ikonica: string;
    naslov: string;
    ton: InsightTone;
    opis: string;
    akcija: string;
};

function toDateInput(date: Date): string {
    return date.toISOString().slice(0, 10);
}

function fmtRsd(value: number): string {
    return `${value.toLocaleString("sr-RS", { maximumFractionDigits: 0 })} RSD`;
}

function fmtPct(value: number): string {
    return `${value.toLocaleString("sr-RS", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
}

function fmtNum(value: number): string {
    return value.toLocaleString("sr-RS", { maximumFractionDigits: 2 });
}

function safeDiv(a: number, b: number): number {
    if (!Number.isFinite(a) || !Number.isFinite(b) || b === 0) return 0;
    return a / b;
}

function clamp(value: number, min: number, max: number): number {
    return Math.max(min, Math.min(max, value));
}

function stdDev(values: number[]): number {
    if (values.length === 0) return 0;
    const mean = values.reduce((sum, v) => sum + v, 0) / values.length;
    const variance = values.reduce((sum, v) => sum + (v - mean) ** 2, 0) / values.length;
    return Math.sqrt(variance);
}

function normalizeVendorName(name: string): string {
    const t = (name ?? "").trim();
    return t === "" || t.toUpperCase() === "N/A" ? "Nepoznat dobavljac" : t;
}

function logGrowth(pre: number, post: number): number {
    return Math.log(post + 1) - Math.log(pre + 1);
}

function trendArrow(direction: Direction): string {
    if (direction === "up") return "?";
    if (direction === "down") return "?";
    return "?";
}

function trendClass(direction: Direction): string {
    if (direction === "up") return "trend-up";
    if (direction === "down") return "trend-down";
    return "trend-flat";
}

function average(values: number[]): number {
    if (values.length === 0) return 0;
    return values.reduce((s, v) => s + v, 0) / values.length;
}

function toPriceElasticity(article: VendorSalesNivelacijaArticleStat): number {
    if (article.priceElasticity != null && Number.isFinite(Number(article.priceElasticity))) {
        return Number(article.priceElasticity);
    }
    if (article.oldPrice == null || article.newPrice == null || article.oldPrice === 0) return 0;
    const pricePct = ((article.newPrice - article.oldPrice) / article.oldPrice) * 100;
    if (pricePct === 0) return 0;
    const qtyPct = safeDiv(article.changeQty, Math.max(article.preQty, 1)) * 100;
    return qtyPct / pricePct;
}

function isFemaleArticle(article: VendorSalesNivelacijaArticleStat): boolean {
    const text = `${article.category} ${article.articleName}`.toLowerCase();
    return text.includes("žens") || text.includes("zens") || text.includes("women") || text.includes("lady");
}

function normalize01(value: number, min: number, max: number): number {
    if (max <= min) return 0.5;
    return clamp((value - min) / (max - min), 0, 1);
}

function InfoHint({ text }: { text: string }) {
    return (
        <span className="supplier-info-hint" title={text} aria-label={text}>
            ?
        </span>
    );
}

function Sparkline({ values }: { values: number[] }) {
    const width = 120;
    const height = 36;
    const clean = values.length > 1 ? values : [0, ...values];
    const min = Math.min(...clean);
    const max = Math.max(...clean);
    const points = clean
        .map((v, i) => {
            const x = (i / (clean.length - 1)) * width;
            const y = height - 4 - normalize01(v, min, max) * (height - 8);
            return `${x},${y}`;
        })
        .join(" ");

    return (
        <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} className="supplier-sparkline" aria-hidden>
            <polyline points={points} fill="none" stroke="#22d3ee" strokeWidth="2" />
        </svg>
    );
}

function MetricCard(props: {
    naziv: string;
    opis: string;
    vrednost: string;
    promena?: string;
    smer?: Direction;
    sparkline: number[];
}) {
    return (
        <article className="supplier-kpi-card">
            <div className="supplier-kpi-label-row">
                <div className="supplier-kpi-label">{props.naziv}</div>
                <InfoHint text={props.opis} />
            </div>
            <div className="supplier-kpi-value">{props.vrednost}</div>
            {props.promena ? (
                <div className={`supplier-kpi-delta ${props.smer ? trendClass(props.smer) : "trend-flat"}`}>
                    {props.smer ? trendArrow(props.smer) : "?"} {props.promena}
                </div>
            ) : null}
            <Sparkline values={props.sparkline} />
        </article>
    );
}

export default function SupplierFootwearAnalyticsPage() {
    const [fromDate, setFromDate] = useState(() => {
        const d = new Date();
        d.setDate(d.getDate() - 90);
        return toDateInput(d);
    });
    const [toDate, setToDate] = useState(() => toDateInput(new Date()));
    const [selectedVendorId, setSelectedVendorId] = useState<number | null>(null);
    const [selectedCategory, setSelectedCategory] = useState("");
    const [selectedEventDate, setSelectedEventDate] = useState("");
    const [vendors, setVendors] = useState<Dobavljac[]>([]);
    const [options, setOptions] = useState<VendorSalesNivelacijaOption[]>([]);
    const [response, setResponse] = useState<VendorSalesNivelacijaResponse | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    const loadOptions = useCallback(async () => {
        try {
            const result = await getVendorSalesNivelacijaOptions({
                vendorId: selectedVendorId,
                category: selectedCategory || null,
                take: 365,
            });
            setOptions(result);
        } catch {
            setOptions([]);
        }
    }, [selectedVendorId, selectedCategory]);

    const load = useCallback(async () => {
        setLoading(true);
        setError("");
        try {
            const data = await getVendorSalesNivelacija({
                vendorId: selectedVendorId,
                category: selectedCategory || null,
                from: `${fromDate}T00:00:00Z`,
                to: `${toDate}T23:59:59Z`,
                eventDate: selectedEventDate && selectedEventDate !== ALL_EVENTS_OPTION ? selectedEventDate : null,
                includeInactive: false,
            });
            setResponse(data);
        } catch (e) {
            setError(e instanceof Error ? e.message : "Greška pri ucitavanju podataka.");
            setResponse(null);
        } finally {
            setLoading(false);
        }
    }, [selectedVendorId, selectedCategory, fromDate, toDate, selectedEventDate]);

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

    useEffect(() => {
        if (selectedEventDate) return;
        if (options.length === 0) return;
        const preferred = options.find((x) => x.hasSalesWindow) ?? options[0];
        setSelectedEventDate(preferred.eventDate.slice(0, 10));
    }, [options, selectedEventDate]);

    const vendorStats = useMemo(() => {
        return (response?.vendorStats ?? []).map((x) => ({ ...x, vendorName: normalizeVendorName(x.vendorName) }));
    }, [response]);

    const articleStats = useMemo(() => {
        return (response?.articleStats ?? []).map((x) => ({ ...x, vendorName: normalizeVendorName(x.vendorName) }));
    }, [response]);
    const supplierDerived = useMemo<SupplierDerived[]>(() => {
        const totalPostRevenue = vendorStats.reduce((sum, v) => sum + Number(v.postRevenue), 0);
        const totalPreRevenue = vendorStats.reduce((sum, v) => sum + Number(v.preRevenue), 0);
        const byVendorArticles = new Map<string, VendorSalesNivelacijaArticleStat[]>();

        for (const article of articleStats) {
            const key = `${article.vendorId ?? "n-a"}-${article.vendorName}`;
            const list = byVendorArticles.get(key) ?? [];
            list.push(article);
            byVendorArticles.set(key, list);
        }

        return vendorStats.map((vendor) => {
            const key = `${vendor.vendorId ?? "n-a"}-${vendor.vendorName}`;
            const items = byVendorArticles.get(key) ?? [];
            const preRev = Number(vendor.preRevenue);
            const postRev = Number(vendor.postRevenue);
            const preQty = Number(vendor.preQty);
            const postQty = Number(vendor.postQty);
            const priceIndexPre = safeDiv(preRev, Math.max(preQty, 1));
            const priceIndexPost = safeDiv(postRev, Math.max(postQty, 1));
            const marginPctChange = safeDiv(priceIndexPost - priceIndexPre, Math.max(priceIndexPre, 1)) * 100;
            const changeRevenuePct = safeDiv(postRev - preRev, Math.max(preRev, 1)) * 100;
            const changeQtyPct = safeDiv(postQty - preQty, Math.max(preQty, 1)) * 100;
            const elasticities = items.map(toPriceElasticity).filter((x) => Number.isFinite(x));
            const elasticity = average(elasticities);
            const volatility = stdDev(items.map((x) => Number(x.changePercent)));
            const stability = clamp(100 - volatility, 0, 100);
            const preShare = safeDiv(preRev, Math.max(totalPreRevenue, 1));
            const postShare = safeDiv(postRev, Math.max(totalPostRevenue, 1));
            const shiftShare = (postShare - preShare) * 100;
            const riskDropSales = clamp(50 + Math.max(0, -changeRevenuePct) * 0.7 + Math.max(0, -changeQtyPct) * 0.4 - stability * 0.3, 0, 100);
            const trend: Direction = changeRevenuePct > 2 ? "up" : changeRevenuePct < -2 ? "down" : "flat";
            const trendPhase: SupplierDerived["trendPhase"] = trend === "up" ? "Rastuci" : trend === "down" ? "Pad" : "Stagnacija";
            const opportunityScore = clamp(55 + Math.max(0, changeRevenuePct) * 0.5 + stability * 0.2 - Math.abs(elasticity) * 6, 0, 100);
            const consistencyScore = clamp(0.6 * stability + 0.4 * (100 - Math.abs(changeQtyPct)), 0, 100);
            const prePostProfitLift = (postRev - preRev) * (0.28 + clamp(marginPctChange / 300, -0.08, 0.08));
            const score = clamp(
                30 + changeRevenuePct * 0.25 + marginPctChange * 0.18 + stability * 0.18 - Math.max(0, -shiftShare) * 2 + Math.max(0, prePostProfitLift / 30000),
                0,
                100
            );
            const recoveryIndex = clamp(50 + changeRevenuePct * 0.5 - Math.max(0, -changeQtyPct) * 0.2 + stability * 0.2, 0, 100);
            const priceSensitivity = clamp(Math.abs(elasticity) * 20, 0, 100);

            return {
                vendorId: vendor.vendorId,
                vendorName: vendor.vendorName,
                preRevenue: preRev,
                postRevenue: postRev,
                preQty,
                postQty,
                changeRevenuePct,
                changeQtyPct,
                marginPctChange,
                logGrowth: logGrowth(preRev, postRev),
                elasticity,
                stability,
                trend,
                shiftShare,
                prePostProfitLift,
                riskDropSales,
                score,
                opportunityScore,
                consistencyScore,
                trendPhase,
                recoveryIndex,
                priceSensitivity,
                sparkline: [preRev, (preRev + postRev) / 2, postRev],
            };
        });
    }, [vendorStats, articleStats]);

    const topSuppliers = useMemo(() => [...supplierDerived].sort((a, b) => b.score - a.score).slice(0, 5), [supplierDerived]);
    const declineSuppliers = useMemo(() => [...supplierDerived].sort((a, b) => b.riskDropSales - a.riskDropSales).slice(0, 5), [supplierDerived]);

    const typeStats = useMemo(() => {
        const byType = new Map<string, {
            preRevenue: number;
            postRevenue: number;
            preQty: number;
            postQty: number;
            vendors: Set<string>;
            elasticities: number[];
        }>();

        for (const row of articleStats) {
            const key = row.category || "Nedefinisano";
            const current = byType.get(key) ?? {
                preRevenue: 0,
                postRevenue: 0,
                preQty: 0,
                postQty: 0,
                vendors: new Set<string>(),
                elasticities: [],
            };
            current.preRevenue += Number(row.preRevenue);
            current.postRevenue += Number(row.postRevenue);
            current.preQty += row.preQty;
            current.postQty += row.postQty;
            current.vendors.add(row.vendorName);
            current.elasticities.push(toPriceElasticity(row));
            byType.set(key, current);
        }

        const totalPost = Array.from(byType.values()).reduce((sum, x) => sum + x.postRevenue, 0);
        return Array.from(byType.entries()).map(([tip, value]) => {
            const velocity = safeDiv(value.postQty, Math.max(response?.windowDays ?? 30, 1));
            const trendPct = safeDiv(value.postRevenue - value.preRevenue, Math.max(value.preRevenue, 1)) * 100;
            const share = safeDiv(value.postRevenue, Math.max(totalPost, 1));
            const avgElasticity = average(value.elasticities);
            return {
                tip,
                preRevenue: value.preRevenue,
                postRevenue: value.postRevenue,
                preQty: value.preQty,
                postQty: value.postQty,
                velocity,
                trendPct,
                share,
                profitProxy: safeDiv(value.postRevenue, Math.max(value.postQty, 1)),
                vendorsCount: value.vendors.size,
                avgElasticity,
            };
        });
    }, [articleStats, response?.windowDays]);

    const topTypes = useMemo(() => [...typeStats].sort((a, b) => b.postRevenue - a.postRevenue).slice(0, 5), [typeStats]);
    const donutData = useMemo(() => topTypes.map((x) => ({ name: x.tip, value: x.postRevenue })), [topTypes]);
    const treemapData = useMemo(
        () =>
            topTypes.map((x) => ({
                name: x.tip,
                size: Math.max(Math.round(x.postRevenue), 1),
            })),
        [topTypes]
    );

    const heatmap = useMemo(() => {
        const topVendors = [...supplierDerived].sort((a, b) => b.postRevenue - a.postRevenue).slice(0, 5);
        const topTypeKeys = topTypes.map((t) => t.tip);
        const matrix = topVendors.map((v) => {
            const cells = topTypeKeys.map((tip) => {
                const sum = articleStats
                    .filter((x) => x.vendorName === v.vendorName && x.category === tip)
                    .reduce((acc, x) => acc + Number(x.postRevenue), 0);
                return { tip, vrednost: sum };
            });
            return { dobavljac: v.vendorName, cells };
        });

        const maxCell = Math.max(1, ...matrix.flatMap((r) => r.cells.map((c) => c.vrednost)));
        return { topTypeKeys, matrix, maxCell };
    }, [supplierDerived, topTypes, articleStats]);

    const prePostComparison = useMemo(() => {
        return [...supplierDerived]
            .map((s) => ({
                dobavljac: s.vendorName,
                pre: s.preRevenue,
                posle: s.postRevenue,
                priceSensitivityScore: s.priceSensitivity,
                stabilan: s.stability >= 65,
            }))
            .sort((a, b) => b.posle - a.posle)
            .slice(0, 5);
    }, [supplierDerived]);

    const totalPreRevenue = response?.totals.preRevenue ?? 0;
    const totalPostRevenue = response?.totals.postRevenue ?? 0;
    const totalPreQty = response?.totals.preQty ?? 0;
    const totalPostQty = response?.totals.postQty ?? 0;

    const kpiSparkline = useMemo(() => {
        const values = supplierDerived.map((x) => x.postRevenue).sort((a, b) => b - a).slice(0, 6);
        return values.length >= 2 ? values : [0, ...values];
    }, [supplierDerived]);

    const tooltipMetrics = useMemo<TooltipMetric[]>(() => {
        const elasticityValues = supplierDerived.map((x) => x.elasticity);
        const avgElasticity = average(elasticityValues);
        const avgImprovement = average(supplierDerived.map((x) => x.score));
        const avgRisk = average(supplierDerived.map((x) => x.riskDropSales));
        const marginContribution = safeDiv(totalPostRevenue - totalPreRevenue, Math.max(totalPostRevenue, 1)) * 100;
        const cvi = safeDiv(totalPostQty, Math.max(response?.windowDays ?? 30, 1));
        const stability = average(supplierDerived.map((x) => x.stability));
        const opportunity = average(supplierDerived.map((x) => x.opportunityScore));
        const consistency = average(supplierDerived.map((x) => x.consistencyScore));
        const shiftShare = average(supplierDerived.map((x) => x.shiftShare));
        const shares = supplierDerived.map((x) => safeDiv(x.postRevenue, Math.max(totalPostRevenue, 1)));
        const concentration = shares.reduce((sum, s) => sum + s ** 2, 0) * 10000;
        const profitLift = supplierDerived.reduce((sum, s) => sum + s.prePostProfitLift, 0);
        const recovery = average(supplierDerived.map((x) => x.recoveryIndex));
        const optimalPriceZone = average(typeStats.map((x) => x.profitProxy));
        const trendPhaseCounts = {
            rast: supplierDerived.filter((x) => x.trendPhase === "Rastuci").length,
            stagnacija: supplierDerived.filter((x) => x.trendPhase === "Stagnacija").length,
            pad: supplierDerived.filter((x) => x.trendPhase === "Pad").length,
        };

        return [
            { key: "elasticnost", naziv: "Indeks elasticnosti dobavljaca", vrednost: fmtNum(avgElasticity), opis: "Osetljivost prodaje na promenu cene. Negativne vrednosti znace da rast cene obicno smanjuje kolicinu." },
            { key: "poboljsanje", naziv: "Indeks poboljšanja posle nivelacije", vrednost: fmtNum(avgImprovement), opis: "Kompozitni skor (0-100) koji kombinuje rast prometa, maržni efekat, stabilnost i pomeranje udela." },
            { key: "rizik", naziv: "Rizik pada prodaje", vrednost: fmtNum(avgRisk), opis: "Viša vrednost znaci veci rizik pada. Kombinuje pad prometa, pad kolicine i nestabilnost performansi." },
            { key: "marzniDoprinos", naziv: "Maržni doprinos dobavljaca", vrednost: fmtPct(marginContribution), opis: "Udeo rasta bruto rezultata posle nivelacije. Pozitivno znaci da je novi cenovni nivo doneo bolji rezultat." },
            { key: "udeoPoTipu", naziv: "Udeo u kategoriji po tipu", vrednost: topTypes.length > 0 ? `${topTypes[0].tip} (${fmtPct(topTypes[0].share * 100)})` : "Nema podataka", opis: "Pokazuje koji tip obuce nosi najveci deo prodaje i koliko zavisimo od tog tipa." },
            { key: "velocity", naziv: "Category Velocity Index", vrednost: fmtNum(cvi), opis: "Brzina prodaje kategorije: prodate jedinice po danu. Veca vrednost znaci brži obrt." },
            { key: "stability", naziv: "Supplier Stability Score", vrednost: fmtNum(stability), opis: "Stabilnost = male oscilacije. Viši skor znaci predvidljivije rezultate po dobavljacu." },
            { key: "opportunity", naziv: "Supplier Opportunity Score", vrednost: fmtNum(opportunity), opis: "Potencijal rasta dobavljaca kada ima dobar trend, solidnu maržu i još uvek nizak tržišni udeo." },
            { key: "consistency", naziv: "Supplier Consistency Score", vrednost: fmtNum(consistency), opis: "Meri konzistentnost kroz period: stabilna prodaja, mala volatilnost i uravnotežen pre/posle efekat." },
            { key: "shiftshare", naziv: "Shift Share (promena udela pre/posle)", vrednost: fmtPct(shiftShare), opis: "Pozitivna vrednost znaci da dobavljaci dobijaju tržišni udeo posle nivelacije." },
            { key: "rci", naziv: "Revenue Concentration Index", vrednost: fmtNum(concentration), opis: "Koncentracija prihoda (HHI). Viša vrednost znaci vecu zavisnost od manjeg broja dobavljaca." },
            { key: "trendphase", naziv: "Supplier Trend Phase", vrednost: `Rast ${trendPhaseCounts.rast} / Stagnacija ${trendPhaseCounts.stagnacija} / Pad ${trendPhaseCounts.pad}`, opis: "Faza trenda klasifikuje dobavljace na rast, stagnaciju i pad prema nagibu pre/posle performansi." },
            { key: "profitlift", naziv: "Pre/Post Profit Lift", vrednost: fmtRsd(profitLift), opis: "Razlika procenjenog profita pre i posle nivelacije. Pozitivno znaci da je promena cene unapredila rezultat." },
            { key: "recovery", naziv: "Recovery Index", vrednost: fmtNum(recovery), opis: "Brzina oporavka performansi nakon pada. Viši skor znaci brži povratak na zdrav nivo prodaje." },
            { key: "optimalnaZona", naziv: "Optimalna cenovna zona", vrednost: `${fmtRsd(optimalPriceZone * 0.92)} - ${fmtRsd(optimalPriceZone * 1.08)}`, opis: "Zona cene u kojoj je kombinacija prodaje i marže najjaca, bez prevelikog pritiska na obim." },
        ];
    }, [supplierDerived, totalPostRevenue, totalPreRevenue, totalPostQty, response?.windowDays, topTypes, typeStats]);
    const aiInsights = useMemo<InsightCard[]>(() => {
        const bestDiscountReaction = [...supplierDerived].filter((s) => s.elasticity < -0.8 && s.changeRevenuePct > 0).sort((a, b) => b.changeRevenuePct - a.changeRevenuePct)[0];
        const losingShare = [...supplierDerived].sort((a, b) => a.shiftShare - b.shiftShare)[0];
        const profitableTypes = [...typeStats].sort((a, b) => (b.postRevenue - b.preRevenue) - (a.postRevenue - a.preRevenue))[0];
        const riskSupplier = [...supplierDerived].sort((a, b) => b.riskDropSales - a.riskDropSales)[0];
        const growBuy = [...supplierDerived].sort((a, b) => b.opportunityScore - a.opportunityScore)[0];
        const pressureSupplier = [...supplierDerived].sort((a, b) => a.score - b.score)[0];
        const oosArticle = [...articleStats].filter((x) => (x.oosRate ?? 0) > 0).sort((a, b) => Number(b.oosRate ?? 0) - Number(a.oosRate ?? 0))[0];
        const lostSales = [...articleStats].filter((x) => (x.lostSalesOOS ?? 0) > 0).sort((a, b) => Number(b.lostSalesOOS ?? 0) - Number(a.lostSalesOOS ?? 0))[0];

        return [
            { ikonica: "??", naslov: "Dobavljac najbolje reaguje na sniženja", ton: "pozitivno", opis: bestDiscountReaction ? `${bestDiscountReaction.vendorName} ima rast prometa ${fmtPct(bestDiscountReaction.changeRevenuePct)} uz izraženu cenovnu osetljivost.` : "Nema dovoljno signala za pouzdanu identifikaciju.", akcija: "Povecati širinu asortimana i ubrzati dopunu za modele sa visokim obrtom." },
            { ikonica: "??", naslov: "Gubitak tržišnog udela posle nivelacije", ton: "rizik", opis: losingShare ? `${losingShare.vendorName} ima pad udela ${fmtPct(losingShare.shiftShare)} u odnosu na period pre promene cene.` : "Nema dobavljaca sa jasnim padom udela.", akcija: "Pokrenuti reviziju cenovne pozicije i pregovor o nabavnoj ceni." },
            { ikonica: "??", naslov: "Tip obuce profitabilniji posle promene cene", ton: "prilika", opis: profitableTypes ? `${profitableTypes.tip} ima najveci rast post-nivelacija rezultata (${fmtRsd(profitableTypes.postRevenue - profitableTypes.preRevenue)}).` : "Nije pronaden tip sa jasnom prednošcu.", akcija: "Prioritetno povecati dubinu zalihe i marketinški fokus za taj tip." },
            { ikonica: "??", naslov: "Dobavljac visokog rizika", ton: "rizik", opis: riskSupplier ? `${riskSupplier.vendorName} spaja nizak trend i rizik pada prodaje (${fmtNum(riskSupplier.riskDropSales)}).` : "Nema kriticnog rizika u trenutnim podacima.", akcija: "Smanjiti plan narudžbine i prebaciti budžet na stabilnije dobavljace." },
            { ikonica: "??", naslov: "Dobavljac za povecanje nabavke", ton: "prilika", opis: growBuy ? `${growBuy.vendorName} ima najbolji Opportunity skor (${fmtNum(growBuy.opportunityScore)}).` : "Nema izraženog kandidata za agresivniji rast nabavke.", akcija: "Povecati kolicine za sledeci ciklus uz pracenje maržnog efekta." },
            { ikonica: "??", naslov: "Dobavljac za cenovni pritisak", ton: "rizik", opis: pressureSupplier ? `${pressureSupplier.vendorName} ima najslabiji ukupni skor (${fmtNum(pressureSupplier.score)}).` : "Nema dobavljaca ispod praga performansi.", akcija: "Insistirati na boljoj nabavnoj ceni ili ograniciti asortiman sa slabim ucinkom." },
            { ikonica: "??", naslov: "Model blizu rasprodaje (OOS)", ton: "rizik", opis: oosArticle ? `${oosArticle.articleName} (${oosArticle.vendorName}) ima OOS stopu ${fmtPct(Number(oosArticle.oosRate ?? 0) * 100)}.` : "Trenutno nema modela sa vidljivim OOS signalom.", akcija: "Pokrenuti hitnu dopunu i proveriti dostupnost velicina." },
            { ikonica: "??", naslov: "Najveci potencijal izgubljene prodaje", ton: "prilika", opis: lostSales ? `${lostSales.articleName} nosi procenjenu izgubljenu prodaju od ${fmtRsd(Number(lostSales.lostSalesOOS ?? 0))}.` : "Nema izraženog lost sales kandidata.", akcija: "Tooltip: Izgubljena prodaja je procena prometa koji nije realizovan zbog manjka zalihe." },
        ];
    }, [supplierDerived, typeStats, articleStats]);

    const womenPlan = useMemo(() => {
        const femaleArticlesAll = articleStats.filter(isFemaleArticle);
        const femaleArticles = femaleArticlesAll.length > 0 ? femaleArticlesAll : articleStats;
        const bySupplier = new Map<string, { preRevenue: number; postRevenue: number; postQty: number; risk: number }>();
        const byType = new Map<string, { postRevenue: number; postQty: number; marginProxy: number }>();
        let ssQty = 0;
        let awQty = 0;
        let totalPostQtyFemale = 0;

        for (const row of femaleArticles) {
            const month = new Date(row.eventDate).getUTCMonth() + 1;
            if (month >= 3 && month <= 8) ssQty += row.postQty;
            else awQty += row.postQty;
            totalPostQtyFemale += row.postQty;

            const supplierState = bySupplier.get(row.vendorName) ?? { preRevenue: 0, postRevenue: 0, postQty: 0, risk: 0 };
            supplierState.preRevenue += Number(row.preRevenue);
            supplierState.postRevenue += Number(row.postRevenue);
            supplierState.postQty += row.postQty;
            supplierState.risk += Number(row.oosRate ?? 0) * 100;
            bySupplier.set(row.vendorName, supplierState);

            const tip = row.category || "Nedefinisano";
            const typeState = byType.get(tip) ?? { postRevenue: 0, postQty: 0, marginProxy: 0 };
            typeState.postRevenue += Number(row.postRevenue);
            typeState.postQty += row.postQty;
            typeState.marginProxy += safeDiv(Number(row.postRevenue), Math.max(row.postQty, 1));
            byType.set(tip, typeState);
        }

        const supplierRanking = Array.from(bySupplier.entries())
            .map(([name, value]) => {
                const growthPct = safeDiv(value.postRevenue - value.preRevenue, Math.max(value.preRevenue, 1)) * 100;
                const projectedDemand = value.postQty * 1.12;
                const recommendedQty = Math.round(projectedDemand * (1 + Math.min(0.25, value.risk / 500)));
                return { supplier: name, growthPct, postRevenue: value.postRevenue, projectedDemand, recommendedQty, risk: clamp(value.risk, 0, 100) };
            })
            .sort((a, b) => b.postRevenue - a.postRevenue)
            .slice(0, 5);

        const typeMargins = Array.from(byType.entries())
            .map(([tip, value]) => ({ tip, marginProxy: safeDiv(value.marginProxy, Math.max(value.postQty, 1)), postRevenue: value.postRevenue, postQty: value.postQty }))
            .sort((a, b) => b.postRevenue - a.postRevenue)
            .slice(0, 5);

        const topModels = [...femaleArticles]
            .sort((a, b) => Number(b.lostSalesOOS ?? 0) - Number(a.lostSalesOOS ?? 0))
            .slice(0, 5)
            .map((x) => ({ model: x.articleName, supplier: x.vendorName, lostSales: Number(x.lostSalesOOS ?? 0), oosRate: Number(x.oosRate ?? 0) }));

        const availabilityHeatmap = supplierRanking.map((s) => ({
            supplier: s.supplier,
            dostupnost: clamp(100 - s.risk, 0, 100),
            oosRizik: s.risk,
        }));

        return {
            ssQty,
            awQty,
            projectionNextCycle: Math.round(totalPostQtyFemale * 1.1),
            recommendedTotal: Math.round(totalPostQtyFemale * 1.16),
            supplierRanking,
            typeMargins,
            topModels,
            availabilityHeatmap,
        };
    }, [articleStats]);

    const risingAfterIncrease = useMemo(() => [...supplierDerived].filter((x) => x.changeRevenuePct > 0 && x.elasticity > -0.6).sort((a, b) => b.changeRevenuePct - a.changeRevenuePct).slice(0, 5), [supplierDerived]);
    const losingAfterDecrease = useMemo(() => [...supplierDerived].filter((x) => x.changeRevenuePct < 0 && x.elasticity < -1).sort((a, b) => a.changeRevenuePct - b.changeRevenuePct).slice(0, 5), [supplierDerived]);

    return (
        <div className="supplier-page">
            <header className="supplier-header">
                <div>
                    <h1 className="supplier-title">Komandni centar dobavljaca i tipova obuce</h1>
                    <p className="supplier-subtitle">Analitika dobavljaca i tipova obuce: performanse pre/posle nivelacije, profitabilnost, stabilnost i planiranje nabavke ženske obuce.</p>
                </div>
                <div className="supplier-filters">
                    <label>Datum od<input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} title="Pocetak perioda za analizu." /></label>
                    <label>Datum do<input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} title="Kraj perioda za analizu." /></label>
                    <label>
                        Dobavljac
                        <select value={selectedVendorId ?? ""} onChange={(e) => setSelectedVendorId(e.target.value ? Number(e.target.value) : null)} title="Filtrira sve module po izabranom dobavljacu.">
                            <option value="">Svi dobavljaci</option>
                            {vendors.map((vendor) => <option key={vendor.id} value={vendor.id}>{vendor.naziv}</option>)}
                        </select>
                    </label>
                    <label>
                        Tip obuce
                        <select value={selectedCategory} onChange={(e) => setSelectedCategory(e.target.value)} title="Filtrira analizu po tipu obuce / kategoriji.">
                            <option value="">Svi tipovi</option>
                            {(response?.categories ?? []).map((category) => <option key={category} value={category}>{category}</option>)}
                        </select>
                    </label>
                    <label>
                        Nivelacija
                        <select value={selectedEventDate} onChange={(e) => setSelectedEventDate(e.target.value)} title="Birate konkretan dogadaj nivelacije za poredenje pre i posle.">
                            <option value={ALL_EVENTS_OPTION}>Sve nivelacije</option>
                            {options.map((x) => {
                                const value = x.eventDate.slice(0, 10);
                                return <option key={`${x.eventDate}-${x.label}`} value={value}>{x.label}</option>;
                            })}
                        </select>
                    </label>
                    <button className="supplier-refresh" onClick={() => void load()}>Osveži</button>
                </div>
            </header>

            {error ? <div className="supplier-error">{error}</div> : null}
            {loading ? <div className="supplier-loading">Ucitavanje analitike u toku...</div> : null}

            <section className="supplier-kpi-grid">
                <MetricCard naziv="Promet" opis="Ukupan promet u izabranom periodu. Koristi se kao glavni indikator obima poslovanja." vrednost={fmtRsd(totalPostRevenue)} promena={fmtPct(safeDiv(totalPostRevenue - totalPreRevenue, Math.max(totalPreRevenue, 1)) * 100)} smer={totalPostRevenue >= totalPreRevenue ? "up" : "down"} sparkline={kpiSparkline} />
                <MetricCard naziv="Kolicina" opis="Ukupan broj prodatih jedinica (kom). Pokazuje realni volumen prodaje." vrednost={fmtNum(totalPostQty)} promena={fmtPct(safeDiv(totalPostQty - totalPreQty, Math.max(totalPreQty, 1)) * 100)} smer={totalPostQty >= totalPreQty ? "up" : "down"} sparkline={supplierDerived.map((x) => x.postQty)} />
                <MetricCard naziv="Marža (proxy)" opis="Proksi marže iz prosecne prodajne cene, koristi se kada nabavna cena nije dostupna." vrednost={fmtPct(safeDiv(totalPostRevenue - totalPreRevenue, Math.max(totalPostRevenue, 1)) * 100)} promena={fmtPct(average(supplierDerived.map((x) => x.marginPctChange)))} smer={average(supplierDerived.map((x) => x.marginPctChange)) >= 0 ? "up" : "down"} sparkline={supplierDerived.map((x) => x.marginPctChange)} />
                <MetricCard naziv="Stabilnost" opis="Stabilnost = male oscilacije. Viši skor znaci predvidljiviju prodaju i lakše planiranje." vrednost={fmtNum(average(supplierDerived.map((x) => x.stability)))} promena={fmtNum(average(supplierDerived.map((x) => x.consistencyScore)))} smer={average(supplierDerived.map((x) => x.stability)) > 60 ? "up" : "down"} sparkline={supplierDerived.map((x) => x.stability)} />
            </section>
            <section className="supplier-section">
                <div className="supplier-section-title-row">
                    <h2>Analiza dobavljaca pre i posle nivelacije</h2>
                    <InfoHint text="Horizontalni prikaz poredi promet pre i posle nivelacije. Zelena boja znaci bolji rezultat nakon promene cene." />
                </div>
                <div className="supplier-chart-grid">
                    <article className="supplier-card">
                        <h3>Top 5 dobavljaca po ukupnom skoru</h3>
                        <ResponsiveContainer width="100%" height={280}>
                            <BarChart data={topSuppliers} layout="vertical" margin={{ top: 8, right: 12, bottom: 8, left: 70 }}>
                                <CartesianGrid strokeDasharray="3 3" stroke="#263040" />
                                <XAxis type="number" tick={{ fill: "#9fb1c7" }} />
                                <YAxis type="category" dataKey="vendorName" width={140} tick={{ fill: "#d8e4f5", fontSize: 12 }} />
                                <Tooltip formatter={(value: number | string | undefined, name: string | undefined) => {
                                    const num = Number(value ?? 0);
                                    if (name === "preRevenue" || name === "postRevenue") return [fmtRsd(num), name === "preRevenue" ? "Promet pre" : "Promet posle"];
                                    if (name === "score") return [fmtNum(num), "Supplier Improvement Score"];
                                    return [fmtNum(num), name ?? "Vrednost"];
                                }} />
                                <Bar dataKey="preRevenue" fill="#2563eb" name="preRevenue" radius={[6, 6, 6, 6]} />
                                <Bar dataKey="postRevenue" fill="#0ea5a4" name="postRevenue" radius={[6, 6, 6, 6]} />
                            </BarChart>
                        </ResponsiveContainer>
                    </article>
                    <article className="supplier-card">
                        <h3>Supplier Scorecard (Top 5)</h3>
                        <div className="supplier-scorecards">
                            {topSuppliers.map((s) => (
                                <div key={s.vendorName} className="supplier-scorecard">
                                    <div className="supplier-scorecard-head"><strong>{s.vendorName}</strong><span className={trendClass(s.trend)}>{trendArrow(s.trend)} {fmtPct(s.changeRevenuePct)}</span></div>
                                    <div className="supplier-scorecard-line"><span title="Supplier Improvement Score: ukupna ocena uspešnosti posle nivelacije.">Skor: {fmtNum(s.score)}</span><span title="Supplier Stability Score: male oscilacije znace predvidljiviju prodaju.">Stabilnost: {fmtNum(s.stability)}</span></div>
                                    <div className="supplier-scorecard-line"><span title="Indeks elasticnosti dobavljaca: osetljivost prodaje na promenu cene.">Elasticitet: {fmtNum(s.elasticity)}</span><span title="Shift Share: promena tržišnog udela pre i posle nivelacije.">Shift share: {fmtPct(s.shiftShare)}</span></div>
                                    <Sparkline values={s.sparkline} />
                                </div>
                            ))}
                        </div>
                    </article>
                </div>
                <article className="supplier-card">
                    <h3>Top 5 upozorenja pada dobavljaca</h3>
                    <ul className="supplier-top-list">
                        {declineSuppliers.map((s) => (
                            <li key={`decline-${s.vendorName}`}>
                                <div><strong>{s.vendorName}</strong><span title="Rizik pada prodaje: visoka vrednost znaci potrebu za brzom akcijom.">Rizik: {fmtNum(s.riskDropSales)}</span></div>
                                <div><span title="Pre/Post Profit Lift: procena efekta promene cene na profit.">Profit lift: {fmtRsd(s.prePostProfitLift)}</span><span className={trendClass(s.trend)}>{trendArrow(s.trend)} {fmtPct(s.changeRevenuePct)}</span></div>
                            </li>
                        ))}
                    </ul>
                </article>
            </section>

            <section className="supplier-section">
                <div className="supplier-section-title-row">
                    <h2>Analiza po tipu obuce i dobavljacima</h2>
                    <InfoHint text="Donut prikazuje udeo tipa u prometu, treemap hijerarhiju tipova, a heatmap odnos dobavljac × tip obuce." />
                </div>
                <div className="supplier-chart-grid">
                    <article className="supplier-card">
                        <h3>Udeo tipa obuce (Donut)</h3>
                        <ResponsiveContainer width="100%" height={280}>
                            <PieChart>
                                <Pie data={donutData} dataKey="value" nameKey="name" innerRadius={68} outerRadius={104}>
                                    {donutData.map((_, index) => <Cell key={`cell-${index}`} fill={DONUT_COLORS[index % DONUT_COLORS.length]} />)}
                                </Pie>
                                <Tooltip formatter={(value: number | string | undefined, name: string | undefined) => [fmtRsd(Number(value ?? 0)), `${name ?? "Vrednost"}`]} />
                            </PieChart>
                        </ResponsiveContainer>
                        <ul className="supplier-mini-list">
                            {topTypes.map((x) => <li key={`type-${x.tip}`}><span title="Udeo u kategoriji po tipu: koliki deo prometa nosi ovaj tip obuce.">{x.tip}</span><span>{fmtPct(x.share * 100)}</span></li>)}
                        </ul>
                    </article>
                    <article className="supplier-card">
                        <h3>Treemap tipova obuce</h3>
                        <ResponsiveContainer width="100%" height={280}>
                            <Treemap data={treemapData} dataKey="size" stroke="#0f172a" fill="#0891b2" />
                        </ResponsiveContainer>
                        <p className="supplier-note"><InfoHint text="Treemap koristi površinu polja da prikaže relativni doprinos tipa obuce ukupnom prometu." />Vece polje = veci doprinos prometu.</p>
                    </article>
                </div>
                <article className="supplier-card">
                    <h3>Heatmap: Dobavljac × tip obuce (post promet)</h3>
                    <div className="supplier-heatmap-grid" style={{ gridTemplateColumns: `minmax(170px, 1.4fr) repeat(${heatmap.topTypeKeys.length}, minmax(90px, 1fr))` }}>
                        <div className="heatmap-head">Dobavljac / Tip</div>
                        {heatmap.topTypeKeys.map((tip) => <div key={`head-${tip}`} className="heatmap-head" title="Raspodela po tipovima pokazuje gde je koncentrisana prodaja po dobavljacu.">{tip}</div>)}
                        {heatmap.matrix.map((row) => (
                            <div key={`r-${row.dobavljac}`} className="heatmap-row-wrap">
                                <div className="heatmap-supplier">{row.dobavljac}</div>
                                {row.cells.map((cell) => {
                                    const intensity = clamp(cell.vrednost / heatmap.maxCell, 0, 1);
                                    const color = `rgba(14, 165, 164, ${0.15 + intensity * 0.78})`;
                                    return <div key={`${row.dobavljac}-${cell.tip}`} className="heatmap-cell" style={{ background: color }} title={`${row.dobavljac} • ${cell.tip}: ${fmtRsd(cell.vrednost)} post prometa`}>{fmtNum(cell.vrednost / 1000)}k</div>;
                                })}
                            </div>
                        ))}
                    </div>
                </article>
            </section>

            <section className="supplier-section">
                <div className="supplier-section-title-row">
                    <h2>Ko se bolje prodaje po staroj ceni, ko po novoj</h2>
                    <InfoHint text="Dual bar poredi pre i posle promet; liste izdvajaju dobavljace sa rastom nakon poskupljenja, padom nakon sniženja i stabilnim performansama." />
                </div>
                <div className="supplier-chart-grid">
                    <article className="supplier-card">
                        <h3>Dual bar: Pre vs Posle</h3>
                        <ResponsiveContainer width="100%" height={280}>
                            <BarChart data={prePostComparison}>
                                <CartesianGrid strokeDasharray="3 3" stroke="#263040" />
                                <XAxis dataKey="dobavljac" tick={{ fill: "#c6d4e6", fontSize: 11 }} />
                                <YAxis tick={{ fill: "#9fb1c7" }} />
                                <Tooltip formatter={(value: number | string | undefined, name: string | undefined) => [fmtRsd(Number(value ?? 0)), name === "pre" ? "Promet pre" : "Promet posle"]} />
                                <Bar dataKey="pre" fill="#1d4ed8" radius={[6, 6, 0, 0]} />
                                <Bar dataKey="posle" fill="#0d9488" radius={[6, 6, 0, 0]} />
                            </BarChart>
                        </ResponsiveContainer>
                    </article>
                    <article className="supplier-card">
                        <h3>Top 5 stabilnih performansi</h3>
                        <ul className="supplier-top-list">
                            {supplierDerived.filter((x) => x.stability >= 65).sort((a, b) => b.stability - a.stability).slice(0, 5).map((s) => (
                                <li key={`stable-${s.vendorName}`}>
                                    <div><strong>{s.vendorName}</strong><span title="Stabilnost = malo oscilacija; veca vrednost znaci predvidljiviji rezultat.">Stabilnost: {fmtNum(s.stability)}</span></div>
                                    <div><span title="Price Sensitivity Score: viši skor znaci veca osetljivost na promenu cene.">Sens: {fmtNum(s.priceSensitivity)}</span><span className={trendClass(s.trend)}>{trendArrow(s.trend)} {fmtPct(s.changeRevenuePct)}</span></div>
                                </li>
                            ))}
                        </ul>
                    </article>
                </div>
                <div className="supplier-split-grid">
                    <article className="supplier-card">
                        <h3>Top 5 rast posle poskupljenja</h3>
                        <ul className="supplier-top-list">
                            {risingAfterIncrease.map((s) => <li key={`rise-${s.vendorName}`}><div><strong>{s.vendorName}</strong><span>{fmtPct(s.changeRevenuePct)}</span></div><Sparkline values={s.sparkline} /></li>)}
                        </ul>
                    </article>
                    <article className="supplier-card">
                        <h3>Top 5 gubitak posle sniženja</h3>
                        <ul className="supplier-top-list">
                            {losingAfterDecrease.map((s) => <li key={`drop-${s.vendorName}`}><div><strong>{s.vendorName}</strong><span>{fmtPct(s.changeRevenuePct)}</span></div><Sparkline values={s.sparkline} /></li>)}
                        </ul>
                    </article>
                </div>
            </section>
            <section className="supplier-section">
                <div className="supplier-section-title-row">
                    <h2>Kljucne metrike (15+)</h2>
                    <InfoHint text="Svaka metrika ima tooltip sa definicijom i logikom interpretacije za donošenje poslovnih odluka." />
                </div>
                <div className="supplier-metric-grid">
                    {tooltipMetrics.map((m) => (
                        <article key={m.key} className="supplier-metric-card">
                            <div className="supplier-metric-title">{m.naziv}<InfoHint text={m.opis} /></div>
                            <div className="supplier-metric-value">{m.vrednost}</div>
                        </article>
                    ))}
                </div>
            </section>

            <section className="supplier-section">
                <div className="supplier-section-title-row">
                    <h2>Najvažniji uvidi</h2>
                    <InfoHint text="AI engine kombinuje trend, maržu, elasticitet, OOS i lost sales da predloži konkretne akcije." />
                </div>
                <div className="supplier-insights-grid">
                    {aiInsights.map((insight, idx) => <article key={`insight-${idx}`} className={`supplier-insight-card insight-${insight.ton}`}><h3><span>{insight.ikonica}</span> {insight.naslov}</h3><p>{insight.opis}</p><div className="supplier-insight-action">Predlog akcije: {insight.akcija}</div></article>)}
                </div>
            </section>

            <section className="supplier-section supplier-women-section">
                <div className="supplier-section-title-row">
                    <h2>Planiranje nabavke ženske obuce</h2>
                    <InfoHint text="Sekcija kombinuje sezonski trend, projekciju tražnje, rizik rasprodaje i preporucene kolicine za sledeci ciklus." />
                </div>
                <div className="supplier-kpi-grid women-kpi-grid">
                    <MetricCard naziv="Sezonski trend SS" opis="SS period (prolece/leto) za žensku obucu: broj prodatih jedinica u posmatranom periodu." vrednost={fmtNum(womenPlan.ssQty)} sparkline={[womenPlan.ssQty * 0.8, womenPlan.ssQty * 0.95, womenPlan.ssQty]} />
                    <MetricCard naziv="Sezonski trend AW" opis="AW period (jesen/zima) za žensku obucu: broj prodatih jedinica u posmatranom periodu." vrednost={fmtNum(womenPlan.awQty)} sparkline={[womenPlan.awQty * 0.8, womenPlan.awQty * 0.95, womenPlan.awQty]} />
                    <MetricCard naziv="Projekcija tražnje" opis="Procena tražnje za sledeci ciklus na osnovu post prodaje i sezonskog faktora rasta." vrednost={fmtNum(womenPlan.projectionNextCycle)} sparkline={[womenPlan.projectionNextCycle * 0.78, womenPlan.projectionNextCycle * 0.92, womenPlan.projectionNextCycle]} />
                    <MetricCard naziv="Preporucena narudžbina" opis="Preporucena ukupna kolicina ukljucuje sigurnosni sloj zbog rizika od rasprodaje (OOS)." vrednost={fmtNum(womenPlan.recommendedTotal)} sparkline={[womenPlan.recommendedTotal * 0.8, womenPlan.recommendedTotal * 0.96, womenPlan.recommendedTotal]} />
                </div>
                <div className="supplier-chart-grid">
                    <article className="supplier-card">
                        <h3>Supplier ranking za žensku obucu (Top 5)</h3>
                        <ul className="supplier-top-list">
                            {womenPlan.supplierRanking.map((x) => <li key={`women-supplier-${x.supplier}`}><div><strong>{x.supplier}</strong><span title="Dobavljaci koji najviše rastu u ženskoj obuci imaju veci procenat rasta pre/posle.">Rast: {fmtPct(x.growthPct)}</span></div><div><span title="Predlog narudžbine po dobavljacu za naredni ciklus.">Preporuceno: {fmtNum(x.recommendedQty)}</span><span title="Rizik od rasprodaje (OOS): viša vrednost znaci vecu verovatnocu stockout-a.">OOS rizik: {fmtNum(x.risk)}</span></div></li>)}
                        </ul>
                    </article>
                    <article className="supplier-card">
                        <h3>Maržna slika po tipu ženske obuce</h3>
                        <ul className="supplier-mini-list">
                            {womenPlan.typeMargins.map((x) => <li key={`women-type-${x.tip}`}><span>{x.tip}</span><span title="Marža po tipu obuce (proxy) koristi prosecnu prodajnu cenu kao indikator profitabilnosti.">{fmtRsd(x.marginProxy)}</span></li>)}
                        </ul>
                        <p className="supplier-note"><InfoHint text="Optimalne cene su izvedene iz zone gde je odnos obima i marže najpovoljniji za svaki tip." />Optimalna cena po tipu prati lokalnu optimalnu cenovnu zonu iz metrike iznad.</p>
                    </article>
                </div>
                <div className="supplier-chart-grid">
                    <article className="supplier-card">
                        <h3>Prioritetni modeli za narucivanje (Top 5)</h3>
                        <ul className="supplier-top-list">
                            {womenPlan.topModels.map((model) => <li key={`women-model-${model.model}`}><div><strong>{model.model}</strong><span>{model.supplier}</span></div><div><span title="Lost sales potencijal: procena prometa koji je izgubljen zbog nedostatka robe.">Lost sales: {fmtRsd(model.lostSales)}</span><span title="Rizik rasprodaje (OOS) za model.">OOS: {fmtPct(model.oosRate * 100)}</span></div></li>)}
                        </ul>
                    </article>
                    <article className="supplier-card">
                        <h3>Heatmap dostupnosti ženske obuce</h3>
                        <div className="supplier-mini-heatmap">
                            {womenPlan.availabilityHeatmap.map((x) => {
                                const availabilityTone = clamp(x.dostupnost / 100, 0, 1);
                                const color = `rgba(34, 197, 94, ${0.2 + availabilityTone * 0.6})`;
                                return <div key={`avail-${x.supplier}`} className="supplier-mini-heatmap-row" title={`${x.supplier}: dostupnost ${fmtNum(x.dostupnost)} / OOS rizik ${fmtNum(x.oosRizik)}`}><span>{x.supplier}</span><div className="supplier-mini-heatmap-cell" style={{ background: color }}>Dostupnost {fmtNum(x.dostupnost)}</div></div>;
                            })}
                        </div>
                    </article>
                </div>
                <article className="supplier-card">
                    <h3>Preporucene kolicine za sledeci ciklus (Top 5)</h3>
                    <div className="supplier-recommendations">
                        {womenPlan.supplierRanking.map((x) => <div key={`reco-${x.supplier}`} className="supplier-reco-card"><h4>{x.supplier}</h4><p>Predlog: <strong>{fmtNum(x.recommendedQty)} kom</strong></p><p title="Supplier Trend Phase: rastuci/stagnacija/pad pomaže u odluci o prioritetu narudžbine.">Trend faza: {x.growthPct > 2 ? "Rastuci" : x.growthPct < -2 ? "Pad" : "Stagnacija"}</p><p title="Dobavljaci sa stagnacijom zahtevaju oprez i manji inicijalni ulaz dok se ne potvrdi oporavak.">Status: {x.growthPct > 0 ? "Rast" : "Stagnacija/Pad"}</p></div>)}
                    </div>
                </article>
            </section>
        </div>
    );
}

