import React, { useState } from "react";
import { useToast } from "../components/Toast";
import SearchableSelect from "../components/SearchableSelect";
import { popularBrands } from "../components/brands";
import Modal from "../components/Modal";
import { runDeichmannScraper } from "../services/deichmannApi";

interface TrendResult {
    productName: string;
    brand: string;
    category: string;
    imageUrl: string;
    priceEur: number;
    tiktokScore: number;
    instagramScore: number;
    finalTrendScore: number;
    trendLevel: string;
    tiktokViews: number;
    tiktokPosts: number;
    instagramPosts: number;
    tiktokEngagement: number;
    keyFeatures: string[];
    popularColors: string[];
}

interface ScraperResult {
    source: string;
    productsCount: number;
    status: string;
}

export default function GlobalTrendsPage() {
    const toast = useToast();
    const [loading, setLoading] = useState(false);
    // Default to 'scrapers' during development so ad-hoc filters are visible on load
    const [activeTab, setActiveTab] = useState<"trends" | "scrapers">("scrapers");
    
    const [selectedCategory, setSelectedCategory] = useState("Patike");
    const [trends, setTrends] = useState<TrendResult[]>([]);
    const [scraperResults, setScraperResults] = useState<ScraperResult[]>([]);
    const [zalandoPages, setZalandoPages] = useState<number>(3);
    const [deichmannPages, setDeichmannPages] = useState<number>(2);
    const [scrapedProducts, setScrapedProducts] = useState<any[]>([]);
    const [adhocProducts, setAdhocProducts] = useState<any[]>([]);
    const [filterBrand, setFilterBrand] = useState<string | string[]>("");
    const [filterGender, setFilterGender] = useState<string>("women");
    const [filterZalandoCountry, setFilterZalandoCountry] = useState<"DE" | "AT" | "CH" | "HU" | "RO">("DE");
    const [filterPriceMin, setFilterPriceMin] = useState<number | undefined>(undefined);
    const [filterPriceMax, setFilterPriceMax] = useState<number | undefined>(undefined);
    const [filterSort, setFilterSort] = useState<string>("popularity");
    const [filterPages, setFilterPages] = useState<number>(1);
    const [filterImportToCore, setFilterImportToCore] = useState<boolean>(false);
    const [imageModalOpen, setImageModalOpen] = useState(false);
    const [imageModalSrc, setImageModalSrc] = useState<string>("");
    const [imageModalTitle, setImageModalTitle] = useState<string>("");
    const [filterActivationDate, setFilterActivationDate] = useState<string | undefined>(undefined);

    const API_URL = import.meta.env.VITE_API_BASE_URL || "http://localhost:8080";
    const PYTHON_API = import.meta.env.VITE_PYTHON_API_URL || "http://localhost:8000";
    const categories = ["Patike", "Sandale", "Cipele", "Cizme"];

    const fetchTrends = async () => {
        setLoading(true);
        try {
            console.log(`🔍 Fetching trends for category: "${selectedCategory}"`);
            const url = `${API_URL}/api/global-trends/social?category=${selectedCategory}`;
            console.log(`📡 URL: ${url}`);
            
            const response = await fetch(url);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            
            const data = await response.json();
            console.log("📦 Received data:", data);
            console.log(`   Category from response: "${data.category}"`);
            console.log(`   Trends count: ${data.trends?.length || 0}`);
            
            if (data.trends && data.trends.length > 0) {
                console.log(`   First hashtag: "${data.trends[0].hashtag}"`);
            }
            
            setTrends(data.trends || []);
            toast.success(`Loaded ${data.trends?.length || 0} trending hashtags for ${data.category}`);
        } catch (error) {
            console.error("❌ Fetch trends error:", error);
            toast.error(error instanceof Error ? error.message : "Failed to fetch trends");
        } finally {
            setLoading(false);
        }
    };

    const fetchProductsForSource = async (source: string) => {
        try {
            const resp = await fetch(`${API_URL}/api/products?source=${encodeURIComponent(source)}`);
            if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
            const data = await resp.json();
            setScrapedProducts(Array.isArray(data) ? data : []);
            toast.success(`Loaded ${Array.isArray(data) ? data.length : 0} products from ${source}`);
        } catch (err) {
            console.error("Failed to fetch scraped products:", err);
            toast.error(err instanceof Error ? err.message : "Failed to load products");
        }
    };

    const getProductsCountForSource = async (source: string) => {
        try {
            const resp = await fetch(`${API_URL}/api/products/debug-count?source=${encodeURIComponent(source)}`);
            if (!resp.ok) return 0;
            const data = await resp.json();
            return data?.count || 0;
        } catch (e) {
            return 0;
        }
    };

    const runScrapers = async () => {
        setLoading(true);
        try {
            const payload = {
                category: selectedCategory,
                zalandoPages,
                deichmannPages
            };

            const response = await fetch(`${API_URL}/api/global-trends/scrape`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                const text = await response.text().catch(() => null);
                throw new Error(text ?? `HTTP ${response.status}`);
            }

            const data = await response.json();
            // Python response forwarded by backend contains 'results' array
            setScraperResults(data.results || []);
            toast.success("Scrapers completed!")

            // If Zalando was scraped, fetch stored products from backend
            const zalandoResult = (data.results || []).find((r: any) => r.source && r.source.toLowerCase() === "zalando");
            if (zalandoResult) {
                // Poll debug-count until the backend import is visible (or timeout)
                const expected = zalandoResult.productsCount || 0;
                toast.info(`Zalando scraped ${expected} products — waiting for import...`);

                const maxAttempts = 15;
                const delayMs = 2000;
                let attempt = 0;
                let found = 0;

                while (attempt < maxAttempts) {
                    attempt++;
                    found = await getProductsCountForSource("zalando");
                    console.log(`Import poll attempt ${attempt}: found ${found}`);
                    if (found >= Math.min(expected, 1)) {
                        // At least one imported product visible — stop polling
                        break;
                    }
                    await new Promise((res) => setTimeout(res, delayMs));
                }

                if (found > 0) {
                    toast.success(`Uvezeno ${found} Zalando proizvoda`);
                    await fetchProductsForSource("zalando");
                } else {
                    toast.warning(`Nisu pronađeni Zalando proizvodi nakon uvoza. Pokušajte ponovo ili proverite backend logove.`);
                }
            }
        } catch (error) {
            console.error("Scraper error:", error);
            toast.error(error instanceof Error ? error.message : "Greška pri pokretanju scraper-a");
        } finally {
            setLoading(false);
        }
    };

    const runZalandoFiltered = async () => {
        setLoading(true);
        try {
            const categoryMap: Record<string, string> = {
                Patike: "sneaker",
                Sandale: "sandale",
                Cipele: "schuhe",
                Cizme: "stiefel",
            };

            const payload: any = {
                category: categoryMap[selectedCategory] || selectedCategory.toLowerCase(),
                pages: filterPages,
            };
            if (filterBrand) {
                if (Array.isArray(filterBrand)) payload.brand = filterBrand.join(",");
                else payload.brand = filterBrand;
            }
            if (filterGender) payload.gender = filterGender;
            if (filterZalandoCountry) payload.country = filterZalandoCountry;
            if (filterPriceMin != null) payload.priceMin = filterPriceMin;
            if (filterPriceMax != null) payload.priceMax = filterPriceMax;
            if (filterSort) payload.sort = filterSort;
            if (filterImportToCore) payload.importToCore = true;
            if (filterActivationDate) payload.activationDate = filterActivationDate;

            const headers: Record<string,string> = { "Content-Type": "application/json" };
            const SCRAPER_KEY = import.meta.env.VITE_SCRAPER_API_KEY;
            if (SCRAPER_KEY) headers["X-API-Key"] = SCRAPER_KEY;

            const resp = await fetch(`${PYTHON_API}/scrapers/zalando`, {
                method: "POST",
                headers,
                body: JSON.stringify(payload)
            });

            if (!resp.ok) {
                const txt = await resp.text().catch(() => null);
                throw new Error(txt ?? `HTTP ${resp.status}`);
            }

            const data = await resp.json();
            setAdhocProducts(data.products || []);
            if (data.imported) {
                toast.success(`Scraped ${data.scraped} proizvoda, uvezeno u bazu`);
            } else {
                toast.success(`Učitano ${data.count || (data.products?.length || 0)} proizvoda sa Zalando-a`);
            }
        } catch (e) {
            console.error("Zalando ad-hoc error:", e);
            toast.error(e instanceof Error ? e.message : "Greška pri pokretanju Zalando scraper-a");
        } finally {
            setLoading(false);
        }
    };

    const runDeichmannFiltered = async () => {
        setLoading(true);
        try {
            const categoryMap: Record<string, string> = {
                Patike: "sneakers-92",
                Sandale: "sandalen-...",
                Cipele: "schuhe-82",
                Cizme: "stiefel-85",
            };

            const payload: any = {
                gender: filterGender || "damen-73",
                category: categoryMap[selectedCategory] || "schuhe-82",
                sort: filterSort === 'new' ? 'new-desc' : filterSort,
                priceMin: filterPriceMin,
                priceMax: filterPriceMax,
                pages: filterPages
            };

            const data = await runDeichmannScraper(payload);
            setScrapedProducts(data.items || data.items || []);
            toast.success(`Deichmann: učitano ${data.count ?? (data.items?.length ?? 0)} stavki`);
        } catch (e) {
            console.error("Deichmann error:", e);
            toast.error(e instanceof Error ? e.message : "Greška pri pokretanju Deichmann scraper-a");
        } finally {
            setLoading(false);
        }
    };

    const openImageModal = (src: string | undefined, title?: string) => {
        if (!src) return;
        setImageModalSrc(src);
        setImageModalTitle(title || "Image");
        setImageModalOpen(true);
    };

    return (
        <div className="max-w-[1400px] mx-auto my-8 px-4">
            {/* Header */}
            <div className="mb-8">
                <h1 className="text-2xl font-bold mb-2 text-contrast">🌍 Globalna analitika trendova</h1>
                <p className="text-muted">Prati EU modne trendove i buzz sa TikToka i Instagrama</p>
            </div>

            {/* Tabs */}
            <div className="flex gap-4 mb-6 border-b-2 border-border">
                <button
                    onClick={() => setActiveTab("trends")}
                    className={`px-6 py-3 rounded-t-lg font-semibold text-base transition ${activeTab === 'trends' ? 'bg-gradient-to-tr from-indigo-500 to-indigo-600 text-white border-b-4 border-indigo-500' : 'bg-transparent text-muted'}`}
                >
                    📊 Trendovi na mrežama
                </button>
                <button
                    onClick={() => setActiveTab("scrapers")}
                    className={`px-6 py-3 rounded-t-lg font-semibold text-base transition ${activeTab === 'scrapers' ? 'bg-gradient-to-tr from-emerald-400 to-emerald-600 text-white border-b-4 border-emerald-500' : 'bg-transparent text-muted'}`}
                >
                    🔍 Scraperi EU tržišta
                </button>
            </div>

            {/* TRENDS TAB */}
            {activeTab === "trends" && (
                <div>
                    <div className="card mb-6">
                        <div className="flex gap-4 items-center flex-wrap">
                            <div className="flex-1 min-w-[200px]">
                                <label className="field-label">Kategorija</label>
                                <select className="input-big w-full" value={selectedCategory} onChange={(e) => setSelectedCategory(e.target.value)}>
                                    {categories.map(cat => (
                                        <option key={cat} value={cat}>{cat}</option>
                                    ))}
                                </select>
                            </div>
                            <button
                                onClick={fetchTrends}
                                disabled={loading}
                                className={`button-big min-w-[200px] ${loading ? 'opacity-60 cursor-not-allowed' : 'bg-gradient-to-tr from-indigo-500 to-indigo-600 text-white'}`}
                            >
                                {loading ? "⏳ Učitavanje..." : "🔍 Učitaj trendove"}
                            </button>
                        </div>
                    </div>

                    {/* Results */}
                    {trends.length > 0 && (
                        <div style={{ display: "grid", gap: "1.5rem", gridTemplateColumns: "repeat(auto-fill, minmax(350px, 1fr))" }}>
                            {trends.map((trend, index) => (
                                <div key={index} className="card rounded-xl overflow-hidden">
                                    {/* Product Image */}
                                    <div className="w-full h-64 bg-surface relative overflow-hidden">
                                        <img 
                                            src={trend.imageUrl} 
                                            alt={trend.productName}
                                            style={{
                                                width: "100%",
                                                height: "100%",
                                                objectFit: "cover"
                                            }}
                                            onError={(e) => {
                                                (e.target as HTMLImageElement).src = "https://via.placeholder.com/400x300?text=No+Image";
                                            }}
                                        />
                                        {/* Price badge */}
                                        <div className="absolute bottom-2 left-2 px-3 py-1 rounded-full bg-info text-white font-bold shadow">
                                            €{trend.priceEur.toFixed(2)}
                                        </div>
                                        <div className="absolute top-2 right-2 bg-surface-elevated px-3 py-1 rounded-full text-sm font-semibold">
                                            {trend.trendLevel}
                                        </div>
                                    </div>

                                    {/* Product Info */}
                                    <div className="p-6">
                                        <div className="mb-4">
                                            <div className="text-sm text-muted mb-1 font-semibold">{trend.brand}</div>
                                            <h3 className="text-lg font-bold text-contrast m-0">{trend.productName}</h3>
                                            <div className="text-2xl font-bold text-info mt-2">€{trend.priceEur.toFixed(2)}</div>
                                        </div>

                                        {/* Trend Scores */}
                                        <div className="grid grid-cols-3 gap-3 mb-4 p-4 bg-surface-darker rounded">
                                            <div className="text-center">
                                                <div className="text-xs text-muted">Trend skor</div>
                                                <div className="text-lg font-bold text-contrast">{trend.finalTrendScore.toFixed(1)}</div>
                                            </div>
                                            <div className="text-center">
                                                <div className="text-xs text-muted">TikTok</div>
                                                <div className="text-lg font-bold text-error">{trend.tiktokScore.toFixed(1)}</div>
                                            </div>
                                            <div className="text-center">
                                                <div className="text-xs text-muted">Instagram</div>
                                                <div className="text-lg font-bold text-accent">{trend.instagramScore.toFixed(1)}</div>
                                            </div>
                                        </div>

                                        {/* Key Features */}
                                        <div className="mb-4">
                                            <div className="text-sm font-semibold text-contrast mb-2">Ključne karakteristike:</div>
                                            <div className="flex flex-wrap gap-2">
                                                {trend.keyFeatures.map((feature, idx) => (
                                                    <span key={idx} className="px-3 py-1 rounded-full text-xs font-medium bg-accent/10 text-accent">{feature}</span>
                                                ))}
                                            </div>
                                        </div>

                                        {/* Popular Colors */}
                                        <div className="mb-4">
                                            <div className="text-sm font-semibold text-muted mb-2">Popularne boje:</div>
                                            <div className="flex flex-wrap gap-2">
                                                {trend.popularColors.map((color, idx) => (
                                                    <span key={idx} className="px-3 py-1 rounded-full text-xs font-medium bg-surface-elevated text-muted">{color}</span>
                                                ))}
                                            </div>
                                        </div>

                                        {/* Social Stats */}
                                        <div className="text-xs text-muted border-t border-border pt-4">
                                            <div>📱 TikTok: {(trend.tiktokViews / 1000000000).toFixed(1)}B views</div>
                                            <div>📸 Instagram: {(trend.instagramPosts / 1000000).toFixed(1)}M posts</div>
                                        </div>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}

                    {trends.length === 0 && !loading && (
                        <div className="card text-center p-12 text-muted">
                            <p className="text-lg mb-2">📊 Nema podataka o trendovima</p>
                            <p>Odaberite kategoriju i kliknite "🔍 Učitaj trendove"</p>
                        </div>
                    )}
                </div>
            )}

            {/* SCRAPERS TAB */}
            {activeTab === "scrapers" && (
                <div>
                    <div className="card mb-6">
                        <div className="flex gap-3 items-center flex-wrap">
                            <div className="min-w-[180px]">
                                <label className="field-label">Zalando stranice</label>
                                <input type="number" className="input-big" value={zalandoPages} min={1} onChange={(e) => setZalandoPages(Number(e.target.value) || 1)} />
                            </div>
                            <div className="min-w-[180px]">
                                <label className="field-label">Deichmann stranice</label>
                                <input type="number" className="input-big" value={deichmannPages} min={1} onChange={(e) => setDeichmannPages(Number(e.target.value) || 1)} />
                            </div>
                            <div className="min-w-[220px]">
                                <label className="field-label">Kategorija</label>
                                <select className="input-big" value={selectedCategory} onChange={(e) => setSelectedCategory(e.target.value)}>
                                    {categories.map(c => <option key={c} value={c}>{c}</option>)}
                                </select>
                            </div>
                            <div>
                                <button
                                    onClick={runScrapers}
                                    disabled={loading}
                                    className={`button-big min-w-[200px] ${loading ? 'opacity-60 cursor-not-allowed' : ''}`}
                                >
                                    {loading ? "⏳ Pokrećanje..." : "🚀 Pokreni scrapere"}
                                </button>
                            </div>
                        </div>
                    </div>

                    {/* FILTER BAR – Modernized */}
                    <div className="mt-6 mb-6 p-5 rounded-lg bg-surface border border-border flex flex-wrap gap-4 items-end">
                        {/* Brand (Searchable Select) */}
                        <div className="min-w-[220px]">
                            <label className="field-label">Brend</label>
                            <SearchableSelect 
                                value={filterBrand}
                                onChange={setFilterBrand}
                                placeholder="Select or type brand…"
                                options={popularBrands}
                                multiple={true}
                            />
                        </div>

                        {/* Gender with icons */}
                        <div className="min-w-[160px]">
                            <label className="field-label">Pol</label>
                            <select
                                className="input-big"
                                value={filterGender}
                                onChange={(e) => setFilterGender(e.target.value)}
                            >
                                <option value="">Unisex</option>
                                <option value="women">👠 Women</option>
                                <option value="men">👞 Men</option>
                                <option value="kids">🧒 Kids</option>
                            </select>
                        </div>

                        <div className="min-w-[200px]">
                            <label className="field-label">Zalando zemlja</label>
                            <select
                                className="input-big"
                                value={filterZalandoCountry}
                                onChange={(e) => setFilterZalandoCountry(e.target.value as "DE" | "AT" | "CH" | "HU" | "RO")}
                            >
                                <option value="DE">Germany (zalando.de)</option>
                                <option value="AT">Austria (zalando.at)</option>
                                <option value="CH">Switzerland (zalando.ch)</option>
                                <option value="HU">Hungary (zalando.hu)</option>
                                <option value="RO">Romania (zalando.ro)</option>
                            </select>
                        </div>

                        {/* Price Range */}
                        <div className="min-w-[150px]">
                            <label className="field-label">Min cena (€)</label>
                            <input
                                type="number"
                                className="input-big"
                                placeholder="0"
                                value={filterPriceMin ?? ""}
                                onChange={(e) =>
                                    setFilterPriceMin(e.target.value ? Number(e.target.value) : undefined)
                                }
                            />
                        </div>

                        <div className="min-w-[150px]">
                            <label className="field-label">Max cena (€)</label>
                            <input
                                type="number"
                                className="input-big"
                                placeholder="500"
                                value={filterPriceMax ?? ""}
                                onChange={(e) =>
                                    setFilterPriceMax(e.target.value ? Number(e.target.value) : undefined)
                                }
                            />
                        </div>

                        {/* Sort */}
                        <div className="min-w-[160px]">
                            <label className="field-label">Sortiranje</label>
                            <select
                                className="input-big"
                                value={filterSort}
                                onChange={(e) => setFilterSort(e.target.value)}
                            >
                                <option value="popularity">🔥 Popularnost</option>
                                <option value="price-asc">💸 Cena: rasтуće</option>
                                <option value="price-desc">💰 Cena: opadajuće</option>
                                <option value="new">✨ Novo u ponudi</option>
                            </select>
                        </div>

                        {/* Pages */}
                        <div className="min-w-[100px]">
                            <label className="field-label">Stranice</label>
                            <input
                                type="number"
                                className="input-big"
                                min={1}
                                value={filterPages}
                                onChange={(e) => setFilterPages(Number(e.target.value) || 1)}
                            />
                        </div>

                        <div className="min-w-[260px]">
                            <label className="field-label">Nova kolekcija</label>
                            <select
                                className="input-big"
                                value={filterActivationDate ?? ""}
                                onChange={(e) => setFilterActivationDate(e.target.value || undefined)}
                            >
                                <option value="">Bez filtera</option>
                                <option value="0-7">Posled. 0-7 dana</option>
                                <option value="0-7.7-14">0-7 i 7-14 dana</option>
                                <option value="0-30.0-7.7-14">0-30, 0-7 i 7-14 dana</option>
                            </select>
                        </div>

                        {/* Import switch */}
                        <label className="flex items-center gap-2 cursor-pointer mt-auto">
                            <input
                                type="checkbox"
                                checked={filterImportToCore}
                                onChange={(e) => setFilterImportToCore(e.target.checked)}
                            />
                            <span className="text-sm text-muted">Uvezi u bazu</span>
                        </label>

                        {/* Run button */}
                        <button
                            onClick={runZalandoFiltered}
                            className="button-big ml-auto min-w-[180px] bg-gradient-to-tr from-indigo-500 to-indigo-600"
                        >
                            🔍 Pokreni pretragu
                        </button>
                    </div>

                    {scraperResults.length > 0 && (
                        <div style={{ display: "grid", gap: "1rem", gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))" }}>
                            {scraperResults.map((result, index) => (
                                <div key={index} className="card">
                                    <h3 className="text-xl font-semibold mb-4">{result.source}</h3>
                                    <div className="text-4xl font-extrabold text-accent-success mb-2">{result.productsCount}</div>
                                    <div className="text-sm text-muted">products scraped</div>
                                    <div className="mt-4 px-3 py-2 rounded text-center font-semibold bg-accent-success/10 text-accent-success">
                                        ✅ {result.status}
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}

                    {scraperResults.length > 0 && (
                        <div style={{ marginTop: 12 }}>
                            <button onClick={() => fetchProductsForSource('zalando')} className="button-big">Prikaži Zalando proizvode</button>
                        </div>
                    )}

                    {scrapedProducts.length > 0 && (
                        <div style={{ marginTop: 20 }}>
                            <h3 style={{ marginBottom: 12, color: "#c9d3e4" }}>Rezultati scraper-a</h3>
                            <div style={{ display: "grid", gap: 16, gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))" }}>
                                {scrapedProducts.map((p) => (
                                    <div key={p.id} className="card p-3">
                                                <div className="w-full h-40 overflow-hidden rounded-md bg-surface">
                                                    {p.imageUrl ? (
                                                        <img
                                                            src={p.imageUrl}
                                                            alt={p.name}
                                                            style={{ width: "100%", height: "100%", objectFit: "cover", cursor: 'pointer' }}
                                                            onClick={() => openImageModal(p.imageUrl, p.name)}
                                                            onError={(e) => { (e.target as HTMLImageElement).src = "https://via.placeholder.com/400x300?text=No+Image" }}
                                                        />
                                                    ) : null}
                                                </div>
                                                <div className="pt-2">
                                                    <div className="text-xs text-muted">{p.brand}</div>
                                                    <div className="font-semibold my-1 text-foreground">{p.name}</div>
                                                    <div className="text-accent-success font-semibold">{p.price != null ? `${p.price}` : "-"}</div>
                                                    {p.url && (
                                                        <a href={p.url} target="_blank" rel="noopener noreferrer" className="text-primary underline">Pogledaj</a>
                                                    )}
                                                </div>
                                            </div>
                                ))}
                            </div>
                        </div>
                    )}

                    {adhocProducts.length > 0 && (
                        <div style={{ marginTop: 20 }}>
                            <h3 style={{ marginBottom: 12, color: "#c9d3e4" }}>Zalando ad-hoc rezultati</h3>
                            <div style={{ display: "grid", gap: 16, gridTemplateColumns: "repeat(auto-fill, minmax(220px, 1fr))" }}>
                                {adhocProducts.map((p: any, idx: number) => (
                                    <div key={idx} className="card p-3">
                                            <div className="w-full h-40 overflow-hidden rounded-md bg-surface-elevated">
                                                {p.image_url ? (
                                                    <img
                                                        src={p.image_url}
                                                        alt={p.name}
                                                        style={{ width: "100%", height: "100%", objectFit: "cover", cursor: 'pointer' }}
                                                        onClick={() => openImageModal(p.image_url, p.name)}
                                                        onError={(e) => { (e.target as HTMLImageElement).src = "https://via.placeholder.com/400x300?text=No+Image" }}
                                                    />
                                                ) : null}
                                            </div>
                                            <div className="pt-2">
                                                <div className="text-xs text-muted">{p.brand}</div>
                                                <div className="font-semibold my-1 text-foreground">{p.name}</div>
                                                <div className="text-accent-success font-semibold">{p.price || "-"}</div>
                                                {p.url && (
                                                    <a href={p.url} target="_blank" rel="noopener noreferrer" className="text-primary underline">Pogledaj</a>
                                                )}
                                            </div>
                                        </div>
                                ))}
                            </div>
                        </div>
                    )}

                    {scraperResults.length === 0 && !loading && (
                        <div className="card" style={{ textAlign: "center", padding: "3rem", color: "#8A95B0" }}>
                            <p style={{ fontSize: "1.25rem", marginBottom: "0.5rem" }}>🔍 Nema rezultata</p>
                            <p>Klikni "🚀 Pokreni scrapere" za učitavanje podataka</p>
                        </div>
                    )}

                    {/* Image modal for enlarged view */}
                    <Modal isOpen={imageModalOpen} onClose={() => setImageModalOpen(false)} title={imageModalTitle} size="lg">
                        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
                            <img src={imageModalSrc} alt={imageModalTitle} style={{ maxWidth: '100%', maxHeight: '75vh', objectFit: 'contain' }} onError={(e) => { (e.target as HTMLImageElement).src = 'https://via.placeholder.com/800x600?text=No+Image' }} />
                        </div>
                    </Modal>
                </div>
            )}
        </div>
    );
}
