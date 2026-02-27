import React, { useEffect, useMemo, useState } from "react";
import Modal from "../components/Modal";
import { useToast } from "../components/Toast";
import { runZalandoScraper } from "../services/zalandoApi";
import { runDeichmannScraper } from "../services/deichmannApi";
import { runAboutYouScraper } from "../services/aboutYouApi";
import { runHumanicScraper } from "../services/humanicApi";
import { fetchGlobalTop10 } from "../services/scoringApi";

/* ─── SOURCE COLOR PALETTE ───────────────────────────────── */
const SOURCE_COLOR: Record<string, { bg: string; text: string; border: string }> = {
    zalando: { bg: "#fff1f3", text: "#be123c", border: "#fecdd3" },
    deichmann: { bg: "#eff6ff", text: "#1d4ed8", border: "#bfdbfe" },
    aboutyou: { bg: "#f5f3ff", text: "#6d28d9", border: "#ddd6fe" },
    humanic: { bg: "#ecfdf5", text: "#065f46", border: "#a7f3d0" },
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

type SourceId = "zalando" | "deichmann" | "aboutyou" | "humanic";
type MarketCode = "DE" | "AT" | "CH" | "HU" | "RO";
type CurrencyCode = "EUR" | "HUF" | "RON" | "CHF";
type ShoeType =
    | "sneakers" | "running"
    | "ankle_boots" | "chelsea" | "knee_boots" | "boots"
    | "sandals"
    | "heels" | "stilettos" | "wedges"
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
        if (allItems.length === 0) { setScoredTop10([]); return; }
        const itemsForScorer = allItems.filter((item) => {
            if (typeFilter !== "all" && item.shoeType !== typeFilter) return false;
            if (brandFilter !== "all" && item.brand.toLowerCase() !== brandFilter.toLowerCase()) return false;
            if (!visibleSources.includes(item.source)) return false;
            if (!SOURCE_SUPPORTS_MARKET[item.source]) return true;
            return item.market ? visibleMarkets.includes(item.market as MarketCode) : true;
        });
        if (itemsForScorer.length === 0) { setScoredTop10([]); return; }
        setTop10Loading(true);
        fetchGlobalTop10(itemsForScorer, typeFilter !== "all" ? typeFilter : null, 10)
            .then((scored) => setScoredTop10(scored.map(scoredToGrouped)))
            .catch(() => setScoredTop10([]))
            .finally(() => setTop10Loading(false));
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

        setLoading(true);
        setLastRuns(runs);
        try {
            const settled = await Promise.allSettled(
                runs.map(async (run) => {
                    if (run.source === "zalando") {
                        let response = await runZalandoScraper({
                            category: zalandoCategory,
                            pages: finitePages,
                            sort: "popularity",
                            country: run.market,
                        });
                        let rawItems = extractRawItems(response);

                        // Fallback to broad shoes if locale-specific type query is empty.
                        if (rawItems.length === 0 && zalandoCategory !== "schuhe") {
                            response = await runZalandoScraper({
                                category: "schuhe",
                                pages: finitePages,
                                sort: "popularity",
                                country: run.market,
                            });
                            rawItems = extractRawItems(response);
                        }

                        return { run, items: toUnifiedItems(run, rawItems, scrapeType) };
                    }
                    if (run.source === "deichmann") {
                        const country = run.market || "DE";
                        let response = await runDeichmannScraper({
                            category: deichmannCategory,
                            pages: finitePages,
                            sort: "key-relevance",
                            gender: "women",
                            country,
                        });
                        let rawItems = extractRawItems(response);

                        // Fallback to broad women shoes category for the market.
                        // Pass empty category so the Python scraper uses the market-appropriate default.
                        if (rawItems.length === 0) {
                            response = await runDeichmannScraper({
                                category: "",
                                pages: finitePages,
                                sort: "key-relevance",
                                gender: "women",
                                country,
                            });
                            rawItems = extractRawItems(response);
                        }

                        return { run, items: toUnifiedItems(run, rawItems, scrapeType) };
                    }
                    if (run.source === "aboutyou") {
                        const market = (run.market || "DE") as MarketCode;
                        const aboutYouUrl = getAboutYouUrl(market, scrapeType);
                        let response = await runAboutYouScraper({
                            url: aboutYouUrl,
                            country: market,
                            pages: infinitePages,
                            sort: "popularity",
                            keyword: aboutYouKeyword,
                        });
                        let rawItems = extractRawItems(response);

                        // Fallback to broad stiefeletten listing if typed URL yields nothing.
                        if (rawItems.length === 0 && aboutYouUrl !== DEFAULT_ABOUTYOU_URL_BY_MARKET[market]) {
                            response = await runAboutYouScraper({
                                url: DEFAULT_ABOUTYOU_URL_BY_MARKET[market],
                                country: market,
                                pages: infinitePages,
                                sort: "popularity",
                            });
                            rawItems = extractRawItems(response);
                        }

                        return { run, items: toUnifiedItems(run, rawItems, scrapeType) };
                    }
                    let response = await runHumanicScraper({
                        url: DEFAULT_HUMANIC_URL,
                        pages: infinitePages,
                        sort: "bestseller",
                        keyword: humanicKeyword,
                    });
                    let rawItems = extractRawItems(response);

                    // Fallback to broad listing without keyword to avoid empty type niche.
                    if (rawItems.length === 0 && humanicKeyword) {
                        response = await runHumanicScraper({
                            url: DEFAULT_HUMANIC_URL,
                            pages: infinitePages,
                            sort: "bestseller",
                        });
                        rawItems = extractRawItems(response);
                    }

                    return { run, items: toUnifiedItems(run, rawItems, scrapeType) };
                })
            );

            const nextItems: UnifiedItem[] = [];
            const nextErrors: Record<string, string> = {};
            settled.forEach((result, idx) => {
                if (result.status === "fulfilled") {
                    nextItems.push(...result.value.items);
                    return;
                }
                const msg = result.reason instanceof Error ? result.reason.message : String(result.reason);
                const run = runs[idx];
                if (run) nextErrors[run.id] = msg;
            });

            setAllItems(nextItems);
            setErrorsByRun(nextErrors);
            setVisibleSources(enabledSources);
            if (enabledMarkets.length > 0) setVisibleMarkets(enabledMarkets);
            toast.success(`Učitano ${nextItems.length} artikala iz ${runs.length} upita.`);
        } catch (error) {
            toast.error(error instanceof Error ? error.message : "Greška pri pokretanju scrapera.");
        } finally {
            setLoading(false);
        }
    };

    /* ── pill toggle helpers ── */
    const PillToggle = ({ label, active, onClick, emoji }: { label: string; active: boolean; onClick: () => void; emoji?: string }) => (
        <button
            onClick={onClick}
            style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 5,
                padding: "5px 13px",
                borderRadius: 999,
                border: `1.5px solid ${active ? "#4f46e5" : "#2A3045"}`,
                background: active ? "#4f46e5" : "#1A1F2E",
                color: active ? "#fff" : "#c9d3e4",
                fontWeight: active ? 600 : 400,
                fontSize: 13,
                cursor: "pointer",
                transition: "all .15s",
                userSelect: "none",
            }}
        >
            {emoji && <span>{emoji}</span>}
            {label}
        </button>
    );

    const SourcePill = ({ source, active, onClick }: { source: SourceId; active: boolean; onClick: () => void }) => {
        const c = SOURCE_COLOR[source];
        return (
            <button
                onClick={onClick}
                style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: 5,
                    padding: "5px 13px",
                    borderRadius: 999,
                    border: `1.5px solid ${active ? c.border : "#e5e7eb"}`,
                    background: active ? c.bg : "#f9fafb",
                    color: active ? c.text : "#9ca3af",
                    fontWeight: active ? 700 : 400,
                    fontSize: 13,
                    cursor: "pointer",
                    transition: "all .15s",
                    userSelect: "none",
                }}
            >
                {SOURCE_EMOJI[source]} {SOURCE_LABEL[source]}
            </button>
        );
    };

    const totalRawItems = allItems.length;
    const totalRuns = lastRuns.length;
    const hasResults = totalRawItems > 0;

    return (
        <div style={{ maxWidth: 1380, margin: "2rem auto", padding: "0 1rem", fontFamily: "system-ui, -apple-system, sans-serif" }}>

            {/* ── HERO HEADER ── */}
            <div style={{ marginBottom: 24 }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
                    <div>
                        <h1 style={{ fontSize: 26, fontWeight: 800, color: "#c9d3e4", margin: 0 }}>
                            🧩 Scraper Hub — Top 10
                        </h1>
                        <p style={{ color: "#8A95B0", marginTop: 4, marginBottom: 0, fontSize: 14 }}>
                            Globalni rang popularnosti + po-izvorni spiskovi. Zalando, Deichmann, About You, Humanic — grupisano i rangirano.
                        </p>
                    </div>
                    {hasResults && (
                        <div style={{ display: "flex", gap: 10 }}>
                            <div style={{ background: "rgba(5, 150, 105, 0.15)", border: "1px solid #065f46", borderRadius: 10, padding: "8px 16px", textAlign: "center" }}>
                                <div style={{ fontSize: 20, fontWeight: 800, color: "#059669" }}>{totalRawItems}</div>
                                <div style={{ fontSize: 11, color: "#8A95B0" }}>artikala</div>
                            </div>
                            <div style={{ background: "rgba(37, 99, 235, 0.15)", border: "1px solid #1d4ed8", borderRadius: 10, padding: "8px 16px", textAlign: "center" }}>
                                <div style={{ fontSize: 20, fontWeight: 800, color: "#2563eb" }}>{filteredGlobal.length}</div>
                                <div style={{ fontSize: 11, color: "#8A95B0" }}>grupa</div>
                            </div>
                            <div style={{ background: "rgba(217, 119, 6, 0.15)", border: "1px solid #b45309", borderRadius: 10, padding: "8px 16px", textAlign: "center" }}>
                                <div style={{ fontSize: 20, fontWeight: 800, color: "#d97706" }}>{totalRuns}</div>
                                <div style={{ fontSize: 11, color: "#8A95B0" }}>upita</div>
                            </div>
                        </div>
                    )}
                </div>
            </div>

            {/* ── CONFIG CARD ── */}
            <div style={{ background: "#161A23", border: "1px solid #2A3045", borderRadius: 14, padding: 20, marginBottom: 14, boxShadow: "0 1px 4px rgba(0,0,0,.3)" }}>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto", gap: 20, flexWrap: "wrap" }}>

                    <div>
                        <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.06em", color: "#8A95B0", marginBottom: 8 }}>Izvori</div>
                        <div style={{ display: "flex", gap: 7, flexWrap: "wrap" }}>
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
                        <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.06em", color: "#8A95B0", marginBottom: 8 }}>Tržišta (Zalando / About You)</div>
                        <div style={{ display: "flex", gap: 7, flexWrap: "wrap" }}>
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

                    <div style={{ display: "flex", flexDirection: "column", gap: 8, justifyContent: "flex-end", minWidth: 300 }}>
                        <div style={{ display: "flex", gap: 8 }}>
                            <div style={{ flex: 1 }}>
                                <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.06em", color: "#8A95B0", marginBottom: 6 }}>Tip</div>
                                <select className="input-big" value={scrapeType} onChange={(e) => { setScrapeType(e.target.value as ScrapeType); setTypeFilter(e.target.value as "all" | ShoeType); }} style={{ width: "100%" }}>
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
                            <div style={{ flex: 1 }}>
                                <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.06em", color: "#8A95B0", marginBottom: 6 }}>Stranice</div>
                                <div style={{ display: "flex", gap: 6 }}>
                                    <select className="input-big" value={pageMode} onChange={(e) => setPageMode(e.target.value as "auto" | "manual")} style={{ flex: 1 }}>
                                        <option value="manual">Ručno</option>
                                        <option value="auto">Automatski</option>
                                    </select>
                                    {pageMode === "manual" && (
                                        <input className="input-big" type="number" min={1} max={5} value={pages} onChange={(e) => setPages(Math.max(1, Number(e.target.value) || 1))} style={{ width: 56 }} />
                                    )}
                                </div>
                            </div>
                        </div>

                        <button
                            onClick={runAllScrapers}
                            disabled={loading}
                            style={{
                                padding: "10px 24px",
                                borderRadius: 10,
                                border: "none",
                                cursor: loading ? "not-allowed" : "pointer",
                                background: loading ? "#9ca3af" : "linear-gradient(135deg, #6366f1 0%, #4f46e5 100%)",
                                color: "white",
                                fontWeight: 700,
                                fontSize: 14,
                                boxShadow: loading ? "none" : "0 4px 12px rgba(79,70,229,.35)",
                                transition: "all .2s",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                gap: 8,
                            }}
                        >
                            {loading ? (
                                <>⏳ Pokretanje...</>
                            ) : (
                                <>🚀 Pokreni sve scrapere</>
                            )}
                        </button>
                    </div>
                </div>
            </div>

            {/* ── GLOBAL TOP 10 ── */}
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 14, flexWrap: "wrap", gap: 10 }}>
                <h2 style={{ fontSize: 20, fontWeight: 800, color: "#c9d3e4", margin: 0 }}>🌍 Globalni Top 10</h2>
                <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
                    {/* ── Brand filter ── */}
                    <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                        <label style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.06em", color: "#8A95B0", whiteSpace: "nowrap" }}>Brend</label>
                        <select
                            value={brandFilter}
                            onChange={(e) => setBrandFilter(e.target.value)}
                            style={{
                                padding: "5px 10px",
                                borderRadius: 8,
                                border: `1.5px solid ${brandFilter !== "all" ? "#6366f1" : "#2A3045"}`,
                                fontSize: 13,
                                fontWeight: brandFilter !== "all" ? 700 : 400,
                                color: brandFilter !== "all" ? "#a5b4fc" : "#c9d3e4",
                                background: brandFilter !== "all" ? "rgba(99, 102, 241, 0.15)" : "#1A1F2E",
                                cursor: "pointer",
                                minWidth: 140,
                                maxWidth: 220,
                            }}
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
                                style={{ background: "#eef2ff", border: "1.5px solid #6366f1", borderRadius: 7, cursor: "pointer", fontSize: 12, color: "#4f46e5", padding: "4px 8px", fontWeight: 700 }}
                            >
                                ✕
                            </button>
                        )}
                    </div>
                    <span style={{ fontSize: 13, color: "#8A95B0" }}>
                        {top10Loading
                            ? "⏳ Računanje skorova…"
                            : `${filteredGlobal.length} grupa · prikazano top ${globalTop10.length}${scoredTop10.length > 0 ? " · ★ Python scored" : ""}`
                        }
                    </span>
                </div>
            </div>

            {globalTop10.length === 0 && !loading && (
                <div style={{ textAlign: "center", padding: 40, background: "#161A23", border: "1px solid #2A3045", borderRadius: 14, color: "#8A95B0", marginBottom: 28 }}>
                    <div style={{ fontSize: 36, marginBottom: 8 }}>📭</div>
                    <div style={{ fontWeight: 600 }}>Nema rezultata</div>
                    <div style={{ fontSize: 13, marginTop: 4 }}>Podešavanja gore, zatim klikni Pokreni.</div>
                </div>
            )}

            {globalTop10.length > 0 && (
                <div style={{ display: "grid", gap: 14, gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))", marginBottom: 36 }}>
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
                                style={{
                                    background: "#161A23",
                                    borderRadius: 14,
                                    boxShadow: idx < 3 ? "0 6px 20px rgba(0,0,0,.4)" : "0 2px 8px rgba(0,0,0,.3)",
                                    border: idx < 3 ? "2px solid #fbbf24" : "1px solid #2A3045",
                                    overflow: "hidden",
                                    display: "flex",
                                    flexDirection: "column",
                                    transition: "transform .2s",
                                }}
                            >
                                {/* image */}
                                <div
                                    style={{ width: "100%", height: 180, background: "#1A1F2E", position: "relative", cursor: group.representative?.image ? "pointer" : "default", overflow: "hidden" }}
                                    onClick={() => openModal(group.representative?.image, `${group.brand} ${group.modelName}`)}
                                >
                                    {group.representative?.image ? (
                                        <img
                                            src={group.representative.image}
                                            alt={group.modelName}
                                            style={{ width: "100%", height: "100%", objectFit: "contain", padding: 8 }}
                                            onError={(e) => { (e.target as HTMLImageElement).src = "https://via.placeholder.com/400x300?text=No+Image"; }}
                                        />
                                    ) : (
                                        <div style={{ width: "100%", height: "100%", display: "flex", alignItems: "center", justifyContent: "center", fontSize: 42 }}>👟</div>
                                    )}
                                    {/* rank badge */}
                                    <div style={{ position: "absolute", top: 8, left: 8, fontSize: idx < 3 ? 26 : 13, lineHeight: 1, background: idx < 3 ? "transparent" : "rgba(0,0,0,.55)", color: "white", borderRadius: 8, padding: idx < 3 ? 0 : "2px 7px", fontWeight: 700 }}>
                                        {medal}
                                    </div>
                                    {/* score badge */}
                                    <div style={{ position: "absolute", top: 8, right: 8, background: "#4f46e5", color: "white", borderRadius: 8, padding: "3px 8px", fontSize: 11, fontWeight: 700 }}>
                                        {group.globalScore !== undefined
                                            ? `★ ${group.globalScore.toFixed(2)}`
                                            : `${group.popularityScore} pts`}
                                    </div>
                                </div>

                                {/* body */}
                                <div style={{ padding: "10px 12px 14px", flex: 1, display: "flex", flexDirection: "column", gap: 4 }}>
                                    <div style={{ fontSize: 11, color: "#9ca3af", fontWeight: 600, textTransform: "uppercase" }}>{group.brand}</div>
                                    <div style={{ fontWeight: 700, fontSize: 14, color: "#c9d3e4", lineHeight: 1.3 }}>{group.modelName}</div>

                                    <div style={{ marginTop: 4, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                                        <span style={{ color: "#059669", fontWeight: 700, fontSize: 14 }}>
                                            {formatPriceRange(group.minPrice, group.maxPrice, group.currency, group.mixedCurrency)}
                                        </span>
                                        <span style={{ background: c.bg, color: c.text, border: `1px solid ${c.border}`, borderRadius: 6, fontSize: 10, padding: "2px 6px", fontWeight: 600 }}>
                                            {SHOE_TYPE_LABEL[group.shoeType]}
                                        </span>
                                    </div>

                                    {/* source presence */}
                                    <div style={{ marginTop: 6, display: "flex", flexWrap: "wrap", gap: 4 }}>
                                        {uniqueSources.map((src) => (
                                            <span key={src} style={{ fontSize: 10, background: SOURCE_COLOR[src].bg, color: SOURCE_COLOR[src].text, border: `1px solid ${SOURCE_COLOR[src].border}`, borderRadius: 5, padding: "1px 6px", fontWeight: 600 }}>
                                                {SOURCE_EMOJI[src]} {SOURCE_LABEL[src as SourceId]}
                                            </span>
                                        ))}
                                        {uniqueMarkets.map((m) => (
                                            <span key={m} style={{ fontSize: 10, background: "#2A3045", color: "#c9d3e4", borderRadius: 5, padding: "1px 6px" }}>
                                                {MARKET_FLAG[m as MarketCode]}{m}
                                            </span>
                                        ))}
                                    </div>

                                    {/* per-market price chips (Python scorer only) */}
                                    {group.priceByMarket && Object.keys(group.priceByMarket).length > 1 && (
                                        <div style={{ marginTop: 4, display: "flex", flexWrap: "wrap", gap: 4 }}>
                                            {Object.entries(group.priceByMarket).map(([mkt, range]) => (
                                                <span key={mkt} style={{ fontSize: 10, background: "rgba(5,150,105,0.15)", color: "#34d399", borderRadius: 5, padding: "1px 6px", fontWeight: 600 }}>
                                                    {MARKET_FLAG[mkt as MarketCode] ?? mkt} {range.min === range.max ? `${range.min}` : `${range.min}–${range.max}`}
                                                </span>
                                            ))}
                                        </div>
                                    )}

                                    {/* provenance row */}
                                    {group.occurrences !== undefined && (
                                        <div style={{ marginTop: 2, fontSize: 10, color: "#9ca3af", display: "flex", gap: 8 }}>
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
                                            style={{ marginTop: 8, display: "block", textAlign: "center", background: "#4f46e5", color: "white", borderRadius: 7, padding: "6px", fontSize: 12, fontWeight: 600, textDecoration: "none" }}
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
                    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 14 }}>
                        <h2 style={{ fontSize: 20, fontWeight: 800, color: "#c9d3e4", margin: 0 }}>📋 Top 10 po izvoru i tržištu</h2>
                        <span style={{ fontSize: 13, color: "#8A95B0" }}>{visibleRuns.length} panela</span>
                    </div>
                    <div style={{ display: "grid", gap: 14, gridTemplateColumns: "repeat(auto-fit, minmax(340px, 1fr))" }}>
                        {visibleRuns.map((run) => {
                            const rows = top10ByRun[run.id] || [];
                            const c = SOURCE_COLOR[run.source];
                            return (
                                <div
                                    key={run.id}
                                    style={{
                                        background: "white",
                                        border: `1.5px solid ${c.border}`,
                                        borderRadius: 14,
                                        overflow: "hidden",
                                        boxShadow: "0 2px 8px rgba(0,0,0,.06)",
                                    }}
                                >
                                    {/* panel header */}
                                    <div style={{ background: c.bg, padding: "10px 14px", display: "flex", justifyContent: "space-between", alignItems: "center", borderBottom: `1px solid ${c.border}` }}>
                                        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                                            <span style={{ fontSize: 16 }}>{SOURCE_EMOJI[run.source]}</span>
                                            <span style={{ fontWeight: 700, fontSize: 14, color: c.text }}>{run.label}</span>
                                            {run.market && <span style={{ fontSize: 16 }}>{MARKET_FLAG[run.market]}</span>}
                                        </div>
                                        <span style={{ fontSize: 11, background: c.border, color: c.text, borderRadius: 999, padding: "2px 8px", fontWeight: 600 }}>
                                            {rows.length}/10
                                        </span>
                                    </div>

                                    {/* body */}
                                    <div style={{ padding: "8px 10px" }}>
                                        {errorsByRun[run.id] && (
                                            <div style={{ color: "#fca5a5", fontSize: 12, background: "rgba(127,29,29,0.25)", border: "1px solid #7f1d1d", borderRadius: 7, padding: "6px 10px", marginBottom: 8 }}>
                                                ❌ {errorsByRun[run.id]}
                                            </div>
                                        )}
                                        {rows.length === 0 && !errorsByRun[run.id] && (
                                            <div style={{ color: "#8A95B0", fontSize: 13, padding: "12px 4px", textAlign: "center" }}>Nema scraped stavki.</div>
                                        )}
                                        {rows.map((group, index) => (
                                            <div
                                                key={`${run.id}-${group.key}`}
                                                style={{
                                                    display: "grid",
                                                    gridTemplateColumns: "36px 52px 1fr auto",
                                                    alignItems: "center",
                                                    gap: 8,
                                                    borderBottom: index < rows.length - 1 ? "1px solid #2A3045" : "none",
                                                    padding: "7px 4px",
                                                }}
                                            >
                                                {/* rank */}
                                                <div style={{ fontWeight: 800, fontSize: index < 3 ? 16 : 13, textAlign: "center" }}>
                                                    {MEDAL[index] ?? `#${index + 1}`}
                                                </div>
                                                {/* thumbnail */}
                                                <div
                                                    style={{ width: 52, height: 52, borderRadius: 8, overflow: "hidden", border: "1px solid #2A3045", background: "#1A1F2E", cursor: group.representative?.image ? "pointer" : "default", flexShrink: 0 }}
                                                    onClick={() => openModal(group.representative?.image, `${group.brand} ${group.modelName}`)}
                                                >
                                                    {group.representative?.image ? (
                                                        <img src={group.representative.image} alt={group.modelName} style={{ width: "100%", height: "100%", objectFit: "contain" }} onError={(e) => { (e.target as HTMLImageElement).src = "https://via.placeholder.com/100x100?text=?"; }} />
                                                    ) : (
                                                        <div style={{ width: "100%", height: "100%", display: "flex", alignItems: "center", justifyContent: "center", fontSize: 20 }}>👟</div>
                                                    )}
                                                </div>
                                                {/* info */}
                                                <div style={{ overflow: "hidden" }}>
                                                    <div style={{ fontSize: 10, color: "#9ca3af", fontWeight: 600, textTransform: "uppercase", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{group.brand}</div>
                                                    <div style={{ fontWeight: 600, fontSize: 13, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{group.modelName}</div>
                                                    <div style={{ fontSize: 10, color: "#8A95B0" }}>{group.shoeStyle}</div>
                                                </div>
                                                {/* price + link */}
                                                <div style={{ textAlign: "right", flexShrink: 0 }}>
                                                    <div style={{ color: "#059669", fontWeight: 700, fontSize: 12, whiteSpace: "nowrap" }}>
                                                        {formatPriceRange(group.minPrice, group.maxPrice, group.currency, group.mixedCurrency)}
                                                    </div>
                                                    {group.representative?.url && (
                                                        <a href={group.representative.url} target="_blank" rel="noreferrer" style={{ fontSize: 11, color: c.text, fontWeight: 600, textDecoration: "none" }}>
                                                            Otvori ↗
                                                        </a>
                                                    )}
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
                <div style={{ display: "flex", justifyContent: "center", alignItems: "center" }}>
                    <img
                        src={modalSrc}
                        alt={modalTitle}
                        style={{ maxWidth: "100%", maxHeight: "75vh", objectFit: "contain" }}
                        onError={(e) => {
                            (e.target as HTMLImageElement).src = "https://via.placeholder.com/800x600?text=No+Image";
                        }}
                    />
                </div>
            </Modal>
        </div>
    );
}
