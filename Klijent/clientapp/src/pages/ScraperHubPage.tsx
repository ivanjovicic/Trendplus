import React, { useEffect, useMemo, useRef, useState } from "react";
import pLimit from "p-limit";
import Modal from "../components/Modal";
import { useToast } from "../components/Toast";
import { runZalandoScraper } from "../services/zalandoApi";
import { runDeichmannScraper } from "../services/deichmannApi";
import { runAboutYouScraper } from "../services/aboutYouApi";
import { runHumanicScraper } from "../services/humanicApi";
import { fetchGlobalTop10 } from "../services/scoringApi";

/* ─── SOURCE COLOR PALETTE ───────────────────────────────── */
const SOURCE_COLOR: Record<string, { bg: string; text: string; border: string }> = {
    zalando: { bg: "rgba(var(--accent-warning-rgb), 0.1)", text: "var(--accent-warning)", border: "var(--accent-warning)" },
    deichmann: { bg: "rgba(var(--info-rgb), 0.1)", text: "var(--info)", border: "var(--info)" },
    aboutyou: { bg: "rgba(var(--primary-rgb), 0.1)", text: "var(--primary)", border: "var(--primary)" },
    humanic: { bg: "rgba(var(--success-rgb), 0.1)", text: "var(--success)", border: "var(--success)" },
};
const SOURCE_EMOJI: Record<string, string> = {
    zalando: "🧡",
    deichmann: "🔵",
    aboutyou: "🟣",
    humanic: "🟢",
};
const MEDAL: Record<number, string> = { 0: "🥇", 1: "🥈", 2: "🥉" };
const MARKET_FLAG: Record<string, string> = {
    DE: "🇩🇪",
    AT: "🇦🇹",
    CH: "🇨🇭",
    HU: "🇭🇺",
    RO: "🇷🇴",
};
const SCRAPER_CONCURRENCY = Number(import.meta.env.VITE_SCRAPER_MAX_CONCURRENCY || 4);
const MAX_ALL_ITEMS = 3000;
const SCORER_DEBOUNCE_MS = 300;

type SourceId = "zalando" | "deichmann" | "aboutyou" | "humanic";
type MarketCode = "DE" | "AT" | "CH" | "HU" | "RO";
type CurrencyCode = "EUR" | "HUF" | "RON" | "CHF";
type ShoeType =
    | "sneakers" | "running"
    | "ankle_boots" | "chelsea" | "knee_boots" | "boots"
    | "sandals" | "heels" | "stilettos" | "wedges"
    | "loafers" | "oxfords"
    | "flats" | "mules" | "espadrilles" | "slippers"
    | "other";
type ScrapeType = "all" | ShoeType;

type BucketRun = {
    id: string;
    source: SourceId;
    market?: MarketCode;
    label: string;
};

type UnifiedItem = {
    source: SourceId;
    market?: MarketCode;
    bucketId: string;
    bucketLabel: string;
    rank: number;
    brand: string;
    name: string;
    priceValue: number | null;
    currency: CurrencyCode;
    image: string | null;
    url: string | null;
    shoeType: ShoeType;
    shoeStyle: string;
};

type GroupedProduct = {
    key: string;
    brand: string;
    modelName: string;
    shoeType: ShoeType;
    shoeStyle: string;
    minPrice: number;
    maxPrice: number;
    currency: CurrencyCode;
    mixedCurrency: boolean;
    popularityScore: number;
    items: UnifiedItem[];
    representative?: UnifiedItem;
    // Scoring metadata – populated when Python scorer is used
    globalScore?: number;
    allSources?: string[];
    allMarkets?: string[];
    occurrences?: number;
    priceByMarket?: Record<string, { min: number; max: number }>;
};

const SOURCE_LABEL: Record<SourceId, string> = {
    zalando: "Zalando",
    deichmann: "Deichmann",
    aboutyou: "About You",
    humanic: "Humanic",
};

const MARKET_LABEL: Record<MarketCode, string> = {
    DE: "Germany",
    AT: "Austria",
    CH: "Switzerland",
    HU: "Hungary",
    RO: "Romania",
};

const MARKET_CURRENCY: Record<MarketCode, CurrencyCode> = {
    DE: "EUR",
    AT: "EUR",
    CH: "CHF",
    HU: "HUF",
    RO: "RON",
};

const MARKET_LIST: MarketCode[] = ["DE", "AT", "CH", "HU", "RO"];

const SOURCE_SUPPORTS_MARKET: Record<SourceId, boolean> = {
    zalando: true,
    deichmann: true,
    aboutyou: true,
    humanic: false,
};

const DEFAULT_ABOUTYOU_URL_BY_MARKET: Record<MarketCode, string> = {
    DE: "https://www.aboutyou.de/c/frauen/schuhe/stiefeletten-20276",
    AT: "https://www.aboutyou.at/c/frauen/schuhe/stiefeletten-20276",
    CH: "https://www.aboutyou.ch/c/frauen/schuhe/stiefeletten-20276",
    HU: "https://www.aboutyou.hu/c/frauen/schuhe/stiefeletten-20276",
    RO: "https://www.aboutyou.ro/c/frauen/schuhe/stiefeletten-20276",
};

/** Category slug paths are the same across all About You markets – only the domain changes. */
const ABOUTYOU_PATH_BY_SCRAPETYPE: Partial<Record<ScrapeType, string>> = {
    sneakers:    "/c/frauen/schuhe/sneaker-turnschuhe-20278",
    running:     "/c/frauen/schuhe/sneaker-turnschuhe-20278",
    ankle_boots: "/c/frauen/schuhe/stiefeletten-20276",
    chelsea:     "/c/frauen/schuhe/stiefeletten-20276",
    knee_boots:  "/c/frauen/schuhe/stiefel-20277",
    boots:       "/c/frauen/schuhe/stiefel-20277",
    sandals:     "/c/frauen/schuhe/sandalen-riemchensandalen-20279",
    heels:       "/c/frauen/schuhe/pumps-high-heels-101349",
    stilettos:   "/c/frauen/schuhe/pumps-high-heels-101349",
    wedges:      "/c/frauen/schuhe/pumps-high-heels-101349",
    // loafers/oxfords/espadrilles use the broad shoes page + keyword filter
    // because sub-category IDs for these vary across markets and can redirect to wrong pages
    loafers:     "/c/frauen/schuhe",
    oxfords:     "/c/frauen/schuhe",
    espadrilles: "/c/frauen/schuhe",
    flats:       "/c/frauen/schuhe/ballerinas-slipper-20282",
    mules:       "/c/frauen/schuhe/ballerinas-slipper-20282",
    slippers:    "/c/frauen/schuhe/ballerinas-slipper-20282",
};

function getAboutYouUrl(market: MarketCode, type: ScrapeType): string {
    const domain = `https://www.aboutyou.${market.toLowerCase()}`;
    const path = ABOUTYOU_PATH_BY_SCRAPETYPE[type] ?? "/c/frauen/schuhe/stiefeletten-20276";
    return `${domain}${path}`;
}

const DEFAULT_HUMANIC_URL = "https://www.humanic.net/at/c/Damenschuhe/womenShoes";

const SHOE_TYPE_LABEL: Record<ShoeType, string> = {
    sneakers:    "👟 Sneakers",
    running:     "🏃 Running",
    ankle_boots: "👢 Ankle Boots",
    chelsea:     "🥾 Chelsea Boots",
    knee_boots:  "👢 Knee Boots",
    boots:       "🥾 Boots",
    sandals:     "🩴 Sandals",
    heels:       "👠 Heels / Pumps",
    stilettos:   "👠 Stilettos",
    wedges:      "👡 Wedges",
    loafers:     "🥿 Loafers",
    oxfords:     "👞 Oxfords / Derbies",
    flats:       "🩰 Flats / Ballerinas",
    mules:       "🩴 Mules",
    espadrilles: "👟 Espadrilles",
    slippers:    "🩴 Slippers",
    other:       "👟 Other",
};

function marketCurrency(market?: MarketCode): CurrencyCode {
    if (!market) return "EUR";
    return MARKET_CURRENCY[market] ?? "EUR";
}

function detectCurrencyCode(value: unknown, market?: MarketCode): CurrencyCode {
    const text = String(value ?? "").toLowerCase();
    if (/(^|[^a-z])huf([^a-z]|$)|\bft\b/.test(text)) return "HUF";
    if (/(^|[^a-z])ron([^a-z]|$)|\blei\b/.test(text)) return "RON";
    if (/(^|[^a-z])chf([^a-z]|$)/.test(text)) return "CHF";
    if (text.includes("€") || /(^|[^a-z])eur([^a-z]|$)/.test(text)) return "EUR";
    return marketCurrency(market);
}

function parsePrice(value: unknown, currencyHint?: CurrencyCode): number | null {
    if (value == null) return null;

    if (typeof value === "number" && Number.isFinite(value)) {
        if (value <= 0) return null;
        if (Number.isInteger(value) && value >= 1000 && currencyHint && currencyHint !== "HUF") {
            return value / 100;
        }
        return value;
    }

    const raw = String(value).replace(/\u00a0/g, " ").trim();
    if (!raw) return null;
    const cleaned = raw.replace(/[^\d,.\-\s]/g, "").replace(/\s+/g, "");
    if (!cleaned) return null;

    const lastComma = cleaned.lastIndexOf(",");
    const lastDot = cleaned.lastIndexOf(".");
    let normalized = cleaned;

    if (lastComma !== -1 && lastDot !== -1) {
        const decimalSep = lastComma > lastDot ? "," : ".";
        const thousandSep = decimalSep === "," ? "." : ",";
        normalized = cleaned.replace(new RegExp(`\\${thousandSep}`, "g"), "").replace(decimalSep, ".");
    } else if (lastComma !== -1) {
        const digitsAfter = cleaned.length - lastComma - 1;
        normalized = digitsAfter === 2 ? cleaned.replace(/\./g, "").replace(",", ".") : cleaned.replace(/,/g, "");
    } else if (lastDot !== -1) {
        const digitsAfter = cleaned.length - lastDot - 1;
        normalized = digitsAfter === 2 ? cleaned.replace(/,/g, "") : cleaned.replace(/\./g, "");
    }

    const parsed = Number(normalized);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function formatMoney(value: number, currency: CurrencyCode): string {
    const locale =
        currency === "HUF" ? "hu-HU"
            : currency === "RON" ? "ro-RO"
                : currency === "CHF" ? "de-CH"
                    : "de-DE";
    const fractionDigits = currency === "HUF" ? 0 : 2;
    return new Intl.NumberFormat(locale, {
        style: "currency",
        currency,
        minimumFractionDigits: fractionDigits,
        maximumFractionDigits: fractionDigits,
    }).format(value);
}

function formatPriceRange(minPrice: number, maxPrice: number, currency: CurrencyCode, mixedCurrency = false): string {
    if (minPrice <= 0) return "No price";
    if (mixedCurrency) return `${formatMoney(minPrice, currency)}+`;
    if (minPrice === maxPrice) return formatMoney(minPrice, currency);
    return `${formatMoney(minPrice, currency)} - ${formatMoney(maxPrice, currency)}`;
}

function normalize(value?: string | null): string {
    return (value || "").toLowerCase().replace(/[^a-z0-9\s]/g, " ").replace(/\s+/g, " ").trim();
}

function detectShoeType(name: string): ShoeType {
    const n = normalize(name);
    // Running first (before sneakers to avoid overlap)
    if (/(running|laufschuh|jogging|trail run)/.test(n)) return "running";
    // Sneakers / trainers  (incl. HU: tornacip, edzocip)
    if (/(sneaker|trainer|patike|turnschuh|sportschuh|basket|chuck|air force|air max|stan smith|superstar|campus|gazelle|forum|ultraboost|tornacip|edzocip)/.test(n)) return "sneakers";
    // Specific boot subtypes — HU: bokacip(o), RO: botine
    if (/(chelsea)/.test(n)) return "chelsea";
    if (/(stiefelette|ankle boot|ankle-boot|kurze stiefel|half boot|bokacip|botine)/.test(n)) return "ankle_boots";
    if (/(kniehoh|knee.?high|lang stiefel|over.?knee|oberschenkel)/.test(n)) return "knee_boots";
    // Generic boots — HU: csizma, RO: cizme
    if (/(boot|stiefel|biker boot|western|cowboy|csizma|cizme)/.test(n)) return "boots";
    // Sandals / mules / espadrilles / slippers — HU: szandal (á→space), papucs; RO: sandale
    if (/(espadrille)/.test(n)) return "espadrilles";
    if (/(mule|pantolette|clogs)/.test(n)) return "mules";
    if (/(slipper|hausschuh|pantofle|slip.?on flat|papucs)/.test(n)) return "slippers";
    if (/(sandale|sandal|szandal)/.test(n)) return "sandals";
    // Heels — HU: magassark(ú→space), steletto, scarpin; RO: tocuri, pantof cu toc
    if (/(stiletto|steletto)/.test(n)) return "stilettos";
    if (/(wedge|keilabsatz|plateau)/.test(n)) return "wedges";
    if (/(pumps|pump|heel|absatz|high.?heel|pfennigabsatz|slingpump|slingback|court shoe|magassark|tocuri|toc inalt|scarpin)/.test(n)) return "heels";
    // Flat closed shoes
    if (/(oxford|derby|brogue|blucher)/.test(n)) return "oxfords";
    if (/(loafer|mokasin|moccasin|mokassin)/.test(n)) return "loafers";
    if (/(ballerina|ballet flat|flat|ballett|slipper flat)/.test(n)) return "flats";
    return "other";
}

function detectShoeStyle(name: string): string {
    const n = normalize(name);
    if (/(running|laufschuh)/.test(n)) return "Running";
    if (/(chelsea)/.test(n)) return "Chelsea Boot";
    if (/(stiefelette|ankle boot|kurze stiefel|bokacip|botine)/.test(n)) return "Ankle Boot";
    if (/(kniehoh|knee.?high|over.?knee)/.test(n)) return "Knee Boot";
    if (/(biker|western|cowboy)/.test(n)) return "Biker/Western Boot";
    if (/(boot|stiefel|csizma|cizme)/.test(n)) return "Boot";
    if (/(espadrille)/.test(n)) return "Espadrille";
    if (/(mule|pantolette|clogs?)/.test(n)) return "Mule";
    if (/(slipper|hausschuh|papucs)/.test(n)) return "Slipper";
    if (/(sandale|sandal|szandal)/.test(n)) return "Sandal";
    if (/(stiletto|steletto)/.test(n)) return "Stiletto";
    if (/(wedge|keilabsatz|plateau)/.test(n)) return "Wedge";
    if (/(pumps|pump|heel|absatz|slingpump|slingback|magassark|tocuri|toc inalt|scarpin)/.test(n)) return "Heel / Pump";
    if (/(oxford|derby|brogue)/.test(n)) return "Oxford / Derby";
    if (/(loafer|mokasin|moccasin|mokassin)/.test(n)) return "Loafer";
    if (/(ballerina|ballet|flat)/.test(n)) return "Flat / Ballerina";
    if (/(sneaker|trainer|patike|turnschuh|tornacip)/.test(n)) return "Sneaker";
    return "General";
}

function extractRawItems(response: any): any[] {
    if (!response) return [];
    if (Array.isArray(response)) return response;
    if (Array.isArray(response.items)) return response.items;
    if (Array.isArray(response.products)) return response.products;
    return [];
}

function toUnifiedItems(run: BucketRun, rawItems: any[], hintType?: ScrapeType): UnifiedItem[] {
    return rawItems
        .map((raw: any, idx: number) => {
            const name = raw?.name ?? raw?.Name ?? raw?.productName ?? "";
            if (!name) return null;
            const brand = raw?.brand ?? raw?.Brand ?? "Unknown";
            const priceRaw = raw?.price ?? raw?.Price ?? raw?.priceEur ?? null;
            const image = raw?.image ?? raw?.image_url ?? raw?.imageUrl ?? raw?.ImageUrl ?? null;
            const url = raw?.url ?? raw?.Url ?? null;
            const currency = detectCurrencyCode(priceRaw, run.market);
            const detected = detectShoeType(String(name));
            // When scraping a specific category and name-based detection falls back to
            // "other" (e.g. local-language product names in HU/RO), trust the category hint.
            const shoeType: ShoeType =
                detected === "other" && hintType && hintType !== "all" ? hintType : detected;
            return {
                source: run.source,
                market: run.market,
                bucketId: run.id,
                bucketLabel: run.label,
                rank: idx + 1,
                brand: String(brand),
                name: String(name),
                priceValue: parsePrice(priceRaw, currency),
                currency,
                image: image != null ? String(image) : null,
                url: url != null ? String(url) : null,
                shoeType,
                shoeStyle: detectShoeStyle(String(name)),
            } as UnifiedItem;
        })
        .filter((v: UnifiedItem | null): v is UnifiedItem => v !== null);
}

function groupKey(item: UnifiedItem): string {
    const brand = normalize(item.brand) || "unknown";
    const words = normalize(item.name).split(" ").filter(Boolean).slice(0, 3);
    return `${brand}|${words.join("-") || "model"}`;
}

function groupItems(items: UnifiedItem[]): GroupedProduct[] {
    const map = new Map<string, UnifiedItem[]>();
    for (const item of items) {
        const key = groupKey(item);
        const arr = map.get(key);
        if (arr) arr.push(item);
        else map.set(key, [item]);
    }

    const groups: GroupedProduct[] = [];
    for (const [key, arr] of map.entries()) {
        const prices = arr.map((x) => x.priceValue).filter((x): x is number => x != null && x > 0);
        const minPrice = prices.length ? Math.min(...prices) : 0;
        const maxPrice = prices.length ? Math.max(...prices) : 0;
        const representative = [...arr].sort((a, b) => a.rank - b.rank)[0];
        const uniqueBuckets = new Set(arr.map((x) => x.bucketId)).size;
        const score = arr.reduce((sum, x) => sum + Math.max(5, 120 - x.rank * 4), 0) + uniqueBuckets * 40;
        const currencySet = [...new Set(arr.map((x) => x.currency))];
        const mixedCurrency = currencySet.length > 1;
        const currency = (representative?.currency || currencySet[0] || "EUR") as CurrencyCode;

        groups.push({
            key,
            brand: representative?.brand || "Unknown",
            modelName: representative?.name || "Unknown model",
            shoeType: representative?.shoeType || "other",
            shoeStyle: representative?.shoeStyle || "General",
            minPrice,
            maxPrice,
            currency,
            mixedCurrency,
            popularityScore: score,
            items: arr.sort((a, b) => a.rank - b.rank),
            representative,
        });
    }

    return groups.sort((a, b) => b.popularityScore - a.popularityScore);
}

function buildBucketRuns(enabledScrapers: Record<SourceId, boolean>, enabledMarkets: MarketCode[]): BucketRun[] {
    const runs: BucketRun[] = [];
    for (const source of Object.keys(enabledScrapers) as SourceId[]) {
        if (!enabledScrapers[source]) continue;
        if (SOURCE_SUPPORTS_MARKET[source]) {
            for (const market of enabledMarkets) {
                runs.push({
                    id: `${source}-${market}`,
                    source,
                    market,
                    label: `${SOURCE_LABEL[source]} ${MARKET_LABEL[market]}`,
                });
            }
        } else {
            runs.push({ id: source, source, label: SOURCE_LABEL[source] });
        }
    }
    return runs;
}

/** Map a Python scorer response item back to the GroupedProduct shape. */
function scoredToGrouped(s: any): GroupedProduct {
    const rep: UnifiedItem = {
        source:      (s.source ?? "zalando") as SourceId,
        market:      (s.market ?? "DE") as MarketCode,
        bucketId:    s.bucketId ?? s.source ?? "unknown",
        bucketLabel: s.bucketLabel ?? s.source ?? "",
        rank:        s.rank ?? 1,
        brand:       s.brand ?? "",
        name:        s.name ?? "",
        priceValue:  s.priceValue ?? null,
        currency:    (s.currency ?? "EUR") as CurrencyCode,
        image:       s.image ?? null,
        url:         s.url ?? null,
        shoeType:    (s.shoeType ?? "other") as ShoeType,
        shoeStyle:   s.shoeStyle ?? "General",
    };
    const allPrices = Object.values(
        (s.priceByMarket ?? {}) as Record<string, { min: number; max: number }>
    ).flatMap((r) => [r.min, r.max]);
    const minPrice = allPrices.length ? Math.min(...allPrices) : (rep.priceValue ?? 0);
    const maxPrice = allPrices.length ? Math.max(...allPrices) : (rep.priceValue ?? 0);
    const allMkts: string[] = s.allMarkets ?? [];
    const currencies = [...new Set(allMkts.map((m) => marketCurrency(m as MarketCode)))];
    return {
        key:            `${s.brand}|${s.name}`,
        brand:          s.brand ?? "Unknown",
        modelName:      s.name ?? "Unknown",
        shoeType:       (s.shoeType ?? "other") as ShoeType,
        shoeStyle:      s.shoeStyle ?? "General",
        minPrice,
        maxPrice,
        currency:       rep.currency,
        mixedCurrency:  currencies.length > 1,
        popularityScore: Math.round((s.globalScore ?? 0) * 100),
        items:          [rep],
        representative: rep,
        globalScore:    s.globalScore,
        allSources:     s.allSources ?? [],
        allMarkets:     allMkts,
        occurrences:    s.occurrences ?? 1,
        priceByMarket:  s.priceByMarket ?? {},
    };
}

export default function ScraperHubPage() {
    const toast = useToast();
    const [loading, setLoading] = useState(false);
    const [enabledScrapers, setEnabledScrapers] = useState<Record<SourceId, boolean>>({
        zalando: true,
        deichmann: true,
        aboutyou: true,
        humanic: true,
    });
    const [enabledMarkets, setEnabledMarkets] = useState<MarketCode[]>(["DE", "AT", "CH"]);
    const [scrapeType, setScrapeType] = useState<ScrapeType>("all");
    const [pageMode, setPageMode] = useState<"auto" | "manual">("manual");
    const [pages, setPages] = useState<number>(1);
    const [visibleSources, setVisibleSources] = useState<SourceId[]>(["zalando", "deichmann", "aboutyou", "humanic"]);
    const [visibleMarkets, setVisibleMarkets] = useState<MarketCode[]>(["DE", "AT", "CH", "HU", "RO"]);
    const [typeFilter, setTypeFilter] = useState<"all" | ShoeType>("all");
    const [brandFilter, setBrandFilter] = useState<string>("all");

    const [allItems, setAllItems] = useState<UnifiedItem[]>([]);
    const [lastRuns, setLastRuns] = useState<BucketRun[]>([]);
    const [scoredTop10, setScoredTop10] = useState<GroupedProduct[]>([]);
    const [top10Loading, setTop10Loading] = useState(false);
    const [errorsByRun, setErrorsByRun] = useState<Record<string, string>>({});
    const [modalOpen, setModalOpen] = useState(false);
    const [modalSrc, setModalSrc] = useState("");
    const [modalTitle, setModalTitle] = useState("");
    const runAbortRef = useRef<AbortController | null>(null);

    const grouped = useMemo(() => groupItems(allItems), [allItems]);

    // ── Derive sorted list of unique brands for the filter dropdown ──────────
    const availableBrands = useMemo(() => {
        const set = new Set<string>();
        for (const g of grouped) {
            if (g.brand && g.brand.trim()) set.add(g.brand.trim());
        }
        return Array.from(set).sort((a, b) => a.localeCompare(b));
    }, [grouped]);

    const filteredGlobal = useMemo(() => {
        return grouped.filter((g) => {
            if (typeFilter !== "all" && g.shoeType !== typeFilter) return false;
            if (brandFilter !== "all" && g.brand.toLowerCase() !== brandFilter.toLowerCase()) return false;
            return g.items.some((item) => {
                if (!visibleSources.includes(item.source)) return false;
                if (!SOURCE_SUPPORTS_MARKET[item.source]) return true;
                return item.market ? visibleMarkets.includes(item.market) : true;
            });
        });
    }, [grouped, typeFilter, brandFilter, visibleSources, visibleMarkets]);

    // Use Python-scored top10 when available, otherwise fall back to client-side rank
    const globalTop10 = scoredTop10.length > 0 ? scoredTop10 : filteredGlobal.slice(0, 10);

    const top10ByRun = useMemo(() => {
        const out: Record<string, GroupedProduct[]> = {};
        for (const run of lastRuns) {
            let groups = groupItems(allItems.filter((x) => x.bucketId === run.id));
            if (typeFilter !== "all") groups = groups.filter((g) => g.shoeType === typeFilter);
            if (brandFilter !== "all") groups = groups.filter((g) => g.brand.toLowerCase() === brandFilter.toLowerCase());
            out[run.id] = groups.slice(0, 10);
        }
        return out;
    }, [allItems, lastRuns, typeFilter, brandFilter]);

    // ── Python-scored Global Top 10 ──────────────────────────────────────────
    useEffect(() => {
        return () => {
            runAbortRef.current?.abort();
        };
    }, []);

    useEffect(() => {
        if (allItems.length === 0) {
            setScoredTop10([]);
            setTop10Loading(false);
            return;
        }

        const timer = window.setTimeout(() => {
            const itemsForScorer = allItems.filter((item) => {
                if (typeFilter !== "all" && item.shoeType !== typeFilter) return false;
                if (brandFilter !== "all" && item.brand.toLowerCase() !== brandFilter.toLowerCase()) return false;
                if (!visibleSources.includes(item.source)) return false;
                if (!SOURCE_SUPPORTS_MARKET[item.source]) return true;
                return item.market ? visibleMarkets.includes(item.market as MarketCode) : true;
            });

            if (itemsForScorer.length === 0) {
                setScoredTop10([]);
                setTop10Loading(false);
                return;
            }

            setTop10Loading(true);
            fetchGlobalTop10(itemsForScorer, typeFilter !== "all" ? typeFilter : null, 10)
                .then((scored) => setScoredTop10(scored.map(scoredToGrouped)))
                .catch(() => setScoredTop10([]))
                .finally(() => setTop10Loading(false));
        }, SCORER_DEBOUNCE_MS);

        return () => window.clearTimeout(timer);
    }, [allItems, typeFilter, brandFilter, visibleSources, visibleMarkets]);

    const visibleRuns = useMemo(
        () =>
            lastRuns.filter((run) => {
                if (!visibleSources.includes(run.source)) return false;
                if (!SOURCE_SUPPORTS_MARKET[run.source]) return true;
                return run.market ? visibleMarkets.includes(run.market) : false;
            }),
        [lastRuns, visibleSources, visibleMarkets]
    );

    const openModal = (src?: string | null, title?: string) => {
        if (!src) return;
        setModalSrc(src);
        setModalTitle(title || "Product image");
        setModalOpen(true);
    };

    const runAllScrapers = async () => {
        const enabledSources = (Object.entries(enabledScrapers) as [SourceId, boolean][]).filter(([, on]) => on).map(([s]) => s);
        if (enabledSources.length === 0) {
            toast.warning("Select at least one scraper.");
            return;
        }
        if (enabledSources.some((s) => SOURCE_SUPPORTS_MARKET[s]) && enabledMarkets.length === 0) {
            toast.warning("Select at least one market.");
            return;
        }

        const runs = buildBucketRuns(enabledScrapers, enabledMarkets);
        const finitePages = Math.max(1, Number(pages) || 1);
        const infinitePages = pageMode === "auto" ? 0 : finitePages;

        const zalandoCategoryByType: Record<ScrapeType, string> = {
            all:         "schuhe",
            sneakers:    "sneaker",
            running:     "running",
            boots:       "boots",
            ankle_boots: "ankle_boots",
            chelsea:     "chelsea",
            knee_boots:  "knee_boots",
            sandals:     "sandals",
            heels:       "heels",
            stilettos:   "stilettos",
            wedges:      "wedges",
            loafers:     "loafers",
            oxfords:     "oxfords",
            flats:       "flats",
            mules:       "mules",
            slippers:    "slippers",
            espadrilles: "espadrilles",
            other:       "schuhe",
        };
        const deichmannCategoryByType: Record<ScrapeType, string> = {
            all:         "schuhe-82",
            sneakers:    "sneaker-143",
            running:     "sneaker-143",
            boots:       "stiefel-187",
            ankle_boots: "stiefeletten-182",
            chelsea:     "stiefeletten-182",
            knee_boots:  "stiefel-187",
            sandals:     "sandalen-191",
            heels:       "high-heels-131",
            stilettos:   "high-heels-131",
            wedges:      "high-heels-131",
            loafers:     "schuhe-82",
            oxfords:     "schuhe-82",
            flats:       "ballerinas-schuhe-183",
            mules:       "schuhe-82",
            espadrilles: "sandalen-191",
            slippers:    "hausschuhe-211",
            other:       "schuhe-82",
        };
        const aboutYouKeywordByType: Partial<Record<ScrapeType, string>> = {
            sneakers:    "sneaker",
            running:     "running",
            boots:       "stiefel",
            ankle_boots: "stiefelette",
            sandals:     "sandale",
            heels:       "pumps",
            loafers:     "loafer",
            oxfords:     "oxford",
            mules:       "mule",
            espadrilles: "espadrille",
            flats:       "ballerina",
            slippers:    "hausschuh",
        };
        const humanicKeywordByType: Partial<Record<ScrapeType, string>> = {
            sneakers: "sneaker",
            running: "running",
            boots: "stiefel",
            ankle_boots: "stiefelette",
            sandals: "sandale",
            heels: "pumps",
            loafers: "loafer",
            flats: "ballerina",
        };

        const zalandoCategory = zalandoCategoryByType[scrapeType] || "schuhe";
        const deichmannCategory = deichmannCategoryByType[scrapeType] || "schuhe-82";
        const aboutYouKeyword = aboutYouKeywordByType[scrapeType];
        const humanicKeyword = humanicKeywordByType[scrapeType];

        runAbortRef.current?.abort();
        const runController = new AbortController();
        runAbortRef.current = runController;

        const appendCappedItems = (items: UnifiedItem[]) => {
            if (items.length === 0) return;
            setAllItems((prev) => {
                const next = prev.concat(items);
                return next.length > MAX_ALL_ITEMS ? next.slice(0, MAX_ALL_ITEMS) : next;
            });
        };

        setLoading(true);
        setLastRuns(runs);
        setAllItems([]);
        setErrorsByRun({});
        try {
            const limit = pLimit(Math.max(1, SCRAPER_CONCURRENCY));

            const settled = await Promise.allSettled(
                runs.map((run) =>
                    limit(async () => {
                    if (run.source === "zalando") {
                        let response = await runZalandoScraper(
                            {
                                category: zalandoCategory,
                                pages: finitePages,
                                sort: "popularity",
                                country: run.market,
                            },
                            runController.signal
                        );
                        let rawItems = extractRawItems(response);

                        // Fallback to broad shoes if locale-specific type query is empty.
                        if (rawItems.length === 0 && zalandoCategory !== "schuhe") {
                            response = await runZalandoScraper(
                                {
                                    category: "schuhe",
                                    pages: finitePages,
                                    sort: "popularity",
                                    country: run.market,
                                },
                                runController.signal
                            );
                            rawItems = extractRawItems(response);
                        }

                        const items = toUnifiedItems(run, rawItems, scrapeType);
                        appendCappedItems(items);
                        return { run, items };
                    }
                    if (run.source === "deichmann") {
                        const country = run.market || "DE";
                        let response = await runDeichmannScraper(
                            {
                                category: deichmannCategory,
                                pages: finitePages,
                                sort: "key-relevance",
                                gender: "women",
                                country,
                            },
                            runController.signal
                        );
                        let rawItems = extractRawItems(response);

                        // Fallback to broad women shoes category for the market.
                        // Pass empty category so the Python scraper uses the market-appropriate default.
                        if (rawItems.length === 0) {
                            response = await runDeichmannScraper(
                                {
                                    category: "",
                                    pages: finitePages,
                                    sort: "key-relevance",
                                    gender: "women",
                                    country,
                                },
                                runController.signal
                            );
                            rawItems = extractRawItems(response);
                        }

                        const items = toUnifiedItems(run, rawItems, scrapeType);
                        appendCappedItems(items);
                        return { run, items };
                    }
                    if (run.source === "aboutyou") {
                        const market = (run.market || "DE") as MarketCode;
                        const aboutYouUrl = getAboutYouUrl(market, scrapeType);
                        let response = await runAboutYouScraper(
                            {
                                url: aboutYouUrl,
                                country: market,
                                pages: infinitePages,
                                sort: "popularity",
                                keyword: aboutYouKeyword,
                            },
                            runController.signal
                        );
                        let rawItems = extractRawItems(response);

                        // Fallback to broad stiefeletten listing if typed URL yields nothing.
                        if (rawItems.length === 0 && aboutYouUrl !== DEFAULT_ABOUTYOU_URL_BY_MARKET[market]) {
                            response = await runAboutYouScraper(
                                {
                                    url: DEFAULT_ABOUTYOU_URL_BY_MARKET[market],
                                    country: market,
                                    pages: infinitePages,
                                    sort: "popularity",
                                },
                                runController.signal
                            );
                            rawItems = extractRawItems(response);
                        }

                        const items = toUnifiedItems(run, rawItems, scrapeType);
                        appendCappedItems(items);
                        return { run, items };
                    }
                    let response = await runHumanicScraper(
                        {
                            url: DEFAULT_HUMANIC_URL,
                            pages: infinitePages,
                            sort: "bestseller",
                            keyword: humanicKeyword,
                        },
                        runController.signal
                    );
                    let rawItems = extractRawItems(response);

                    // Fallback to broad listing without keyword to avoid empty type niche.
                    if (rawItems.length === 0 && humanicKeyword) {
                        response = await runHumanicScraper(
                            {
                                url: DEFAULT_HUMANIC_URL,
                                pages: infinitePages,
                                sort: "bestseller",
                            },
                            runController.signal
                        );
                        rawItems = extractRawItems(response);
                    }

                    const items = toUnifiedItems(run, rawItems, scrapeType);
                    appendCappedItems(items);
                    return { run, items };
                    })
                )
            );

            if (runController.signal.aborted || runAbortRef.current !== runController) {
                return;
            }

            const nextErrors: Record<string, string> = {};
            let loadedCount = 0;
            settled.forEach((result, idx) => {
                if (result.status === "fulfilled") {
                    loadedCount += result.value.items.length;
                    return;
                }
                const msg = result.reason instanceof Error ? result.reason.message : String(result.reason);
                const run = runs[idx];
                if (run) nextErrors[run.id] = msg;
            });

            setErrorsByRun(nextErrors);
            setVisibleSources(enabledSources);
            if (enabledMarkets.length > 0) setVisibleMarkets(enabledMarkets);
            toast.success(`Učitano ${loadedCount} artikala iz ${runs.length} upita.`);
        } catch (error) {
            if (runController.signal.aborted || runAbortRef.current !== runController) {
                return;
            }
            toast.error(error instanceof Error ? error.message : "Greška pri pokretanju scrapera.");
        } finally {
            if (runAbortRef.current === runController) {
                runAbortRef.current = null;
                setLoading(false);
            }
        }
    };

    /* ── pill toggle helpers ── */
    const PillToggle = ({ label, active, onClick, emoji }: { label: string; active: boolean; onClick: () => void; emoji?: string }) => (
        <button
            onClick={onClick}
            className={`inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-full border transition-all text-sm font-medium select-none ${
                active 
                    ? "bg-primary border-primary text-white shadow-sm" 
                    : "bg-surface-elevated border-muted text-muted hover:border-muted/80 hover:text-contrast"
            }`}
        >
            {emoji && <span>{emoji}</span>}
            {label}
        </button>
    );

    const SourcePill = ({ source, active, onClick }: { source: SourceId; active: boolean; onClick: () => void }) => {
        const colors = SOURCE_COLOR[source];
        return (
            <button
                onClick={onClick}
                className={`inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-full border transition-all text-sm font-semibold select-none ${
                    active 
                        ? "shadow-sm" 
                        : "bg-surface-elevated border-muted text-muted grayscale opacity-60 hover:grayscale-0 hover:opacity-100"
                }`}
                style={active ? { background: colors.bg, borderColor: colors.border, color: colors.text } : {}}
            >
                {SOURCE_EMOJI[source]} {SOURCE_LABEL[source]}
            </button>
        );
    };

    const totalRawItems = allItems.length;
    const totalRuns = lastRuns.length;
    const hasResults = totalRawItems > 0;

    return (
        <div className="max-w-[1380px] mx-auto my-8 px-4 font-sans">

            {/* ── HERO HEADER ── */}
            <div className="mb-6">
                <div className="flex items-center justify-between flex-wrap gap-3">
                    <div>
                        <h1 className="text-2xl md:text-3xl font-extrabold text-foreground m-0">🧩 Scraper Hub — Top 10</h1>
                        <p className="text-sm text-muted mt-1 mb-0">Globalni rang popularnosti + po-izvorni spiskovi. Zalando, Deichmann, About You, Humanic — grupisano i rangirano.</p>
                    </div>
                    {hasResults && (
                        <div className="flex gap-3">
                            <div className="px-4 py-2 rounded-lg text-center bg-surface-elevated border border-border">
                                <div className="text-xl font-extrabold text-accent-success">{totalRawItems}</div>
                                <div className="text-xs text-muted">artikala</div>
                            </div>
                            <div className="px-4 py-2 rounded-lg text-center bg-surface-elevated border border-border">
                                <div className="text-xl font-extrabold text-primary">{filteredGlobal.length}</div>
                                <div className="text-xs text-muted">grupa</div>
                            </div>
                            <div className="px-4 py-2 rounded-lg text-center bg-surface-elevated border border-border">
                                <div className="text-xl font-extrabold text-accent-warning">{totalRuns}</div>
                                <div className="text-xs text-muted">upita</div>
                            </div>
                        </div>
                    )}
                </div>
            </div>

            {/* ── CONFIG CARD ── */}
            <div className="bg-surface border border-border rounded-xl p-5 mb-4 shadow-sm">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-5">

                    <div>
                        <div className="text-xs font-bold uppercase tracking-wider text-muted mb-2">Izvori</div>
                        <div className="flex gap-2 flex-wrap">
                            {(Object.keys(SOURCE_LABEL) as SourceId[]).map((source) => (
                                <SourcePill key={source} source={source} active={!!enabledScrapers[source]} onClick={() => {
                                    const next = !enabledScrapers[source];
                                    setEnabledScrapers((prev) => ({ ...prev, [source]: next }));
                                    setVisibleSources((prev) => next ? [...new Set([...prev, source])] : prev.filter((s) => s !== source));
                                }} />
                            ))}
                        </div>
                    </div>

                    <div>
                        <div className="text-xs font-bold uppercase tracking-wider text-muted mb-2">Tržišta (Zalando / About You)</div>
                        <div className="flex gap-2 flex-wrap">
                            {MARKET_LIST.map((market) => (
                                <PillToggle key={market} label={market} emoji={MARKET_FLAG[market]} active={enabledMarkets.includes(market)} onClick={() => {
                                    setEnabledMarkets((prev) => {
                                        const next = prev.includes(market) ? prev.filter((m) => m !== market) : [...prev, market];
                                        setVisibleMarkets(next);
                                        return next;
                                    });
                                }} />
                            ))}
                        </div>
                    </div>

                    <div className="flex flex-col gap-3 justify-end min-w-[300px]">
                        <div className="flex gap-2">
                            <div className="flex-1">
                                <div className="text-xs font-bold uppercase tracking-wider text-muted mb-1">Tip</div>
                                <select className="input-big w-full" value={scrapeType} onChange={(e) => { setScrapeType(e.target.value as ScrapeType); setTypeFilter(e.target.value as "all" | ShoeType); }}>
                                    <option value="all">Svi tipovi</option>
                                    <optgroup label="Patike i sport">
                                        <option value="sneakers">👟 Patike</option>
                                        <option value="running">🏃 Trkačke</option>
                                    </optgroup>
                                    <optgroup label="Čizme">
                                        <option value="ankle_boots">👢 Gleženjčke</option>
                                        <option value="boots">🥾 Čizme (sve)</option>
                                    </optgroup>
                                    <optgroup label="Sandale / ravna">
                                        <option value="sandals">🩴 Sandale</option>
                                        <option value="flats">🩰 Ravna / Balerinke</option>
                                        <option value="loafers">🥿 Mokasine</option>
                                    </optgroup>
                                    <optgroup label="Potpetice">
                                        <option value="heels">👠 Potpetice / Pumpe</option>
                                    </optgroup>
                                </select>
                            </div>
                            <div className="flex-1">
                                <div className="text-xs font-bold uppercase tracking-wider text-muted mb-1">Stranice</div>
                                <div className="flex gap-2">
                                    <select className="input-big flex-1" value={pageMode} onChange={(e) => setPageMode(e.target.value as "auto" | "manual") }>
                                        <option value="manual">Ručno</option>
                                        <option value="auto">Automatski</option>
                                    </select>
                                    {pageMode === "manual" && (
                                        <input className="input-big w-14" type="number" min={1} max={5} value={pages} onChange={(e) => setPages(Math.max(1, Number(e.target.value) || 1))} />
                                    )}
                                </div>
                            </div>
                        </div>

                        <button
                            onClick={runAllScrapers}
                            disabled={loading}
                            className={`button-big ml-auto ${loading ? 'opacity-60 cursor-not-allowed' : 'bg-gradient-to-tr from-indigo-500 to-indigo-600'}`}
                        >
                            {loading ? '⏳ Pokretanje...' : '🚀 Pokreni sve scrapere'}
                        </button>
                    </div>
                </div>
            </div>

            {/* ── GLOBAL TOP 10 ── */}
            <div className="flex items-center justify-between mb-4 flex-wrap gap-3">
                <h2 className="text-xl font-extrabold text-foreground m-0">🌍 Globalni Top 10</h2>
                <div className="flex items-center gap-3 flex-wrap">
                    {/* ── Brand filter ── */}
                    <div className="flex items-center gap-2">
                        <label className="text-xs font-bold uppercase tracking-wider text-muted whitespace-nowrap">Brend</label>
                        <select
                            value={brandFilter}
                            onChange={(e) => setBrandFilter(e.target.value)}
                            className={`px-3 py-1 rounded-md text-sm min-w-[140px] max-w-[220px] ${brandFilter !== 'all' ? 'border-primary text-primary bg-primary/10 font-semibold' : 'border-border text-muted bg-surface'}`}
                        >
                            <option value="all">Svi brendovi ({availableBrands.length})</option>
                            {availableBrands.map((b) => (
                                <option key={b} value={b}>{b}</option>
                            ))}
                        </select>
                        {brandFilter !== "all" && (
                            <button
                                onClick={() => setBrandFilter("all")}
                                title="Clear brand filter"
                                className="bg-indigo-50 border border-indigo-500 rounded px-2 py-1 text-indigo-600 text-sm font-bold"
                            >
                                ✕
                            </button>
                        )}
                    </div>
                    <span className="text-sm text-muted">
                        {top10Loading
                            ? "⏳ Računanje skorova…"
                            : `${filteredGlobal.length} grupa · prikazano top ${globalTop10.length}${scoredTop10.length > 0 ? " · ★ Python scored" : ""}`
                        }
                    </span>
                </div>
            </div>

            {globalTop10.length === 0 && !loading && (
                <div className="text-center p-10 bg-surface border border-border rounded-lg text-muted mb-7">
                    <div className="text-4xl mb-2">📭</div>
                    <div className="font-semibold">Nema rezultata</div>
                    <div className="text-sm mt-1">Podešavanja gore, zatim klikni Pokreni.</div>
                </div>
            )}

            {globalTop10.length > 0 && (
                <div className="grid grid-cols-[repeat(auto-fill,minmax(240px,1fr))] gap-4 mb-9">
                    {globalTop10.map((group, idx) => {
                        const c = SOURCE_COLOR[group.representative?.source || "zalando"];
                        const uniqueSources = group.allSources?.length
                            ? group.allSources
                            : [...new Set(group.items.map((i) => i.source))];
                        const uniqueMarkets = group.allMarkets?.length
                            ? group.allMarkets
                            : [...new Set(group.items.map((i) => i.market).filter(Boolean))];
                        const medal = MEDAL[idx] ?? `#${idx + 1}`;

                        return (
                            <div
                                key={group.key}
                                className={`rounded-xl overflow-hidden flex flex-col transition-transform duration-200 bg-surface-elevated border ${
                                    idx < 3 ? "shadow-lg border-accent-warning" : "shadow-sm border-border"
                                }`}
                            >
                                {/* image */}
                                <div
                                    className={`w-full h-[180px] bg-surface relative overflow-hidden ${group.representative?.image ? 'cursor-pointer' : 'cursor-default'}`}
                                    onClick={() => openModal(group.representative?.image, `${group.brand} ${group.modelName}`)}
                                >
                                    {group.representative?.image ? (
                                        <img
                                            src={group.representative.image}
                                            alt={group.modelName}
                                            className="w-full h-full object-contain p-2"
                                            onError={(e) => { (e.target as HTMLImageElement).src = "https://via.placeholder.com/400x300?text=No+Image"; }}
                                        />
                                    ) : (
                                        <div className="w-full h-full flex items-center justify-center text-4xl">👟</div>
                                    )}
                                    {/* rank badge */}
                                    <div className={`absolute top-2 left-2 font-bold rounded-lg ${
                                        idx < 3 ? "text-[26px] bg-transparent" : "text-[13px] bg-black/55 text-white px-[7px] py-[2px]"
                                    }`}>
                                        {medal}
                                    </div>
                                    {/* score badge */}
                                    <div className="absolute top-2 right-2 bg-primary text-white rounded-lg px-2 py-1 text-[11px] font-bold">
                                        {group.globalScore !== undefined
                                            ? `★ ${group.globalScore.toFixed(2)}`
                                            : `${group.popularityScore} pts`}
                                    </div>
                                </div>

                                {/* body */}
                                <div className="p-3 flex-1 flex flex-col gap-1">
                                    <div className="text-[11px] text-muted font-semibold uppercase">{group.brand}</div>
                                    <div className="font-bold text-sm text-foreground leading-snug">{group.modelName}</div>

                                    <div className="mt-1 flex justify-between items-center">
                                        <span className="text-accent-success font-bold text-sm">
                                            {formatPriceRange(group.minPrice, group.maxPrice, group.currency, group.mixedCurrency)}
                                        </span>
                                        <span 
                                            style={{ background: c.bg, color: c.text, borderColor: c.border }}
                                            className="border rounded-md text-[10px] px-1.5 py-0.5 font-semibold"
                                        >
                                            {SHOE_TYPE_LABEL[group.shoeType]}
                                        </span>
                                    </div>

                                    {/* source presence */}
                                    <div className="mt-1.5 flex flex-wrap gap-1">
                                        {uniqueSources.map((src) => (
                                            <span 
                                                key={src} 
                                                style={{ background: SOURCE_COLOR[src].bg, color: SOURCE_COLOR[src].text, borderColor: SOURCE_COLOR[src].border }}
                                                className="text-[10px] border rounded-md px-1.5 py-0.5 font-semibold"
                                            >
                                                {SOURCE_EMOJI[src]} {SOURCE_LABEL[src as SourceId]}
                                            </span>
                                        ))}
                                        {uniqueMarkets.map((m) => (
                                            <span key={m} className="text-[10px] bg-surface border border-border text-muted rounded-md px-1.5 py-0.5">
                                                {MARKET_FLAG[m as MarketCode]}{m}
                                            </span>
                                        ))}
                                    </div>

                                    {/* per-market price chips (Python scorer only) */}
                                    {group.priceByMarket && Object.keys(group.priceByMarket).length > 1 && (
                                        <div className="mt-1 flex flex-wrap gap-1">
                                            {Object.entries(group.priceByMarket).map(([mkt, range]) => (
                                                <span key={mkt} className="text-[10px] bg-accent-success/10 text-accent-success border border-accent-success/20 rounded-md px-1.5 py-0.5 font-semibold">
                                                    {MARKET_FLAG[mkt as MarketCode] ?? mkt} {range.min === range.max ? `${range.min}` : `${range.min}–${range.max}`}
                                                </span>
                                            ))}
                                        </div>
                                    )}

                                    {/* provenance row */}
                                    {group.occurrences !== undefined && (
                                        <div className="mt-0.5 text-[10px] text-muted flex gap-2">
                                            <span>🔄 {group.occurrences}×</span>
                                            <span>📦 {group.allSources?.length ?? 1} src</span>
                                            <span>🌍 {group.allMarkets?.length ?? 1} mkt</span>
                                        </div>
                                    )}

                                    {/* link */}
                                    {group.representative?.url && (
                                        <a
                                            href={group.representative.url}
                                            target="_blank"
                                            rel="noreferrer"
                                            className="mt-2 block text-center bg-primary text-white rounded-lg py-1.5 text-xs font-semibold no-underline hover:opacity-90 transition-opacity"
                                        >
                                            Pogledaj →
                                        </a>
                                    )}
                                </div>
                            </div>
                        );
                    })}
                </div>
            )}

            {/* ── PER-SOURCE / MARKET TOP 10 ── */}
            {visibleRuns.length > 0 && (
                <>
                    <div className="flex items-center justify-between mb-3.5">
                        <h2 className="text-xl font-extrabold text-foreground m-0">📋 Top 10 po izvoru i tržištu</h2>
                        <span className="text-[13px] text-muted">{visibleRuns.length} panela</span>
                    </div>
                    <div className="grid grid-cols-[repeat(auto-fit,minmax(340px,1fr))] gap-3.5">
                        {visibleRuns.map((run) => {
                            const rows = top10ByRun[run.id] || [];
                            const c = SOURCE_COLOR[run.source];
                            return (
                                <div
                                    key={run.id}
                                    style={{ borderColor: c.border }}
                                    className="bg-surface-elevated border-[1.5px] rounded-xl overflow-hidden shadow-sm"
                                >
                                    {/* panel header */}
                                    <div 
                                        style={{ background: c.bg, borderBottomColor: c.border }}
                                        className="px-3.5 py-2.5 flex justify-between items-center border-b"
                                    >
                                        <div className="flex items-center gap-2">
                                            <span className="text-base">{SOURCE_EMOJI[run.source]}</span>
                                            <span style={{ color: c.text }} className="font-bold text-sm">{run.label}</span>
                                            {run.market && <span className="text-base">{MARKET_FLAG[run.market]}</span>}
                                        </div>
                                        <span 
                                            style={{ background: c.border, color: c.text }}
                                            className="text-[11px] rounded-full px-2 py-0.5 font-semibold"
                                        >
                                            {rows.length}/10
                                        </span>
                                    </div>

                                    {/* body */}
                                    <div className="px-2.5 py-2">
                                        {errorsByRun[run.id] && (
                                            <div className="text-xs text-accent-error bg-accent-error/20 border border-accent-error/40 rounded-lg p-2.5 mb-2">
                                                ❌ {errorsByRun[run.id]}
                                            </div>
                                        )}
                                        {rows.length === 0 && !errorsByRun[run.id] && (
                                            <div className="text-muted text-[13px] py-3 text-center">Nema scraped stavki.</div>
                                        )}
                                        {rows.map((group, index) => (
                                            <div
                                                key={`${run.id}-${group.key}`}
                                                className={`grid grid-cols-[36px_52px_1fr_auto] items-center gap-2 py-1.5 px-1 ${
                                                    index < rows.length - 1 ? "border-b border-border" : ""
                                                }`}
                                            >
                                                {/* rank */}
                                                <div className={`font-extrabold text-center ${index < 3 ? "text-base" : "text-[13px]"}`}>
                                                    {MEDAL[index] ?? `#${index + 1}`}
                                                </div>
                                                {/* thumbnail */}
                                                <div
                                                    className={`w-[52px] h-[52px] rounded-lg overflow-hidden border border-border bg-surface flex-shrink-0 ${group.representative?.image ? 'cursor-pointer' : 'cursor-default'}`}
                                                    onClick={() => openModal(group.representative?.image, `${group.brand} ${group.modelName}`)}
                                                >
                                                    {group.representative?.image ? (
                                                        <img src={group.representative.image} alt={group.modelName} className="w-full h-full object-contain" onError={(e) => { (e.target as HTMLImageElement).src = "https://via.placeholder.com/100x100?text=?"; }} />
                                                    ) : (
                                                        <div className="w-full h-full flex items-center justify-center text-xl">👟</div>
                                                    )}
                                                </div>
                                                {/* info */}
                                                <div className="overflow-hidden">
                                                    <div className="text-[10px] text-muted font-semibold uppercase truncate">{group.brand}</div>
                                                    <div className="font-semibold text-[13px] truncate text-foreground">{group.modelName}</div>
                                                    <div className="text-[10px] text-muted">{group.shoeStyle}</div>
                                                </div>

                                                {/* price */}
                                                <div className="text-right flex flex-col items-end gap-0.5">
                                                    <div className="text-xs font-bold text-accent-success">
                                                        {formatPriceRange(group.minPrice, group.maxPrice, group.currency)}
                                                    </div>
                                                    <span 
                                                        style={{ background: c.bg, color: c.text, borderColor: c.border }}
                                                        className="text-[9px] px-1.5 py-0.5 rounded border font-semibold"
                                                    >
                                                        {SHOE_TYPE_LABEL[group.shoeType]}
                                                    </span>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </>
            )}

            <Modal isOpen={modalOpen} onClose={() => setModalOpen(false)} title={modalTitle} size="lg">
                <div className="flex justify-center items-center">
                    <img
                        src={modalSrc}
                        alt={modalTitle}
                        className="max-w-full max-h-[75vh] object-contain"
                        onError={(e) => {
                            (e.target as HTMLImageElement).src = "https://via.placeholder.com/800x600?text=No+Image";
                        }}
                    />
                </div>
            </Modal>
        </div>
    );
}

