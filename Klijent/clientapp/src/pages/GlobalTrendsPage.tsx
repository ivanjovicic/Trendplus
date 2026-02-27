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
        <div style={{ maxWidth: "1400px", margin: "2rem auto", padding: "0 1rem" }}>
            {/* Header */}
            <div style={{ marginBottom: "2rem" }}>
                <h1 style={{ fontSize: "2rem", fontWeight: 700, color: "#c9d3e4", marginBottom: "0.5rem" }}>
                    🌍 Globalna analitika trendova
                </h1>
                <p style={{ color: "#8A95B0", fontSize: "1rem" }}>
                    Prati EU modne trendove i buzz sa TikToka i Instagrama
                </p>
            </div>

            {/* Tabs */}
            <div style={{ 
                display: "flex", 
                gap: "1rem", 
                marginBottom: "2rem",
                borderBottom: "2px solid #2A3045"
            }}>
                <button
                    onClick={() => setActiveTab("trends")}
                    style={{
                        padding: "1rem 2rem",
                        background: activeTab === "trends" ? "linear-gradient(135deg, #3b82f6 0%, #2563eb 100%)" : "transparent",
                        color: activeTab === "trends" ? "white" : "#8A95B0",
                        border: "none",
                        borderBottom: activeTab === "trends" ? "3px solid #3b82f6" : "none",
                        cursor: "pointer",
                        fontWeight: 600,
                        fontSize: "1rem",
                        borderRadius: "8px 8px 0 0",
                        transition: "all 0.2s"
                    }}
                >
                    📊 Trendovi na mrežama
                </button>
                <button
                    onClick={() => setActiveTab("scrapers")}
                    style={{
                        padding: "1rem 2rem",
                        background: activeTab === "scrapers" ? "linear-gradient(135deg, #10b981 0%, #059669 100%)" : "transparent",
                        color: activeTab === "scrapers" ? "white" : "#8A95B0",
                        border: "none",
                        borderBottom: activeTab === "scrapers" ? "3px solid #10b981" : "none",
                        cursor: "pointer",
                        fontWeight: 600,
                        fontSize: "1rem",
                        borderRadius: "8px 8px 0 0",
                        transition: "all 0.2s"
                    }}
                >
                    🔍 Scraperi EU tržišta
                </button>
            </div>

            {/* TRENDS TAB */}
            {activeTab === "trends" && (
                <div>
                    <div className="card" style={{ marginBottom: "1.5rem" }}>
                        <div style={{ display: "flex", gap: "1rem", alignItems: "center", flexWrap: "wrap" }}>
                            <div style={{ flex: "1", minWidth: "200px" }}>
                                <label style={{ display: "block", marginBottom: "0.5rem", fontWeight: 600, color: "#8A95B0" }}>
                                    Kategorija
                                </label>
                                <select
                                    className="input-big"
                                    value={selectedCategory}
                                    onChange={(e) => setSelectedCategory(e.target.value)}
                                    style={{ width: "100%", marginBottom: 0 }}
                                >
                                    {categories.map(cat => (
                                        <option key={cat} value={cat}>{cat}</option>
                                    ))}
                                </select>
                            </div>
                            <button
                                onClick={fetchTrends}
                                disabled={loading}
                                className="button-big"
                                style={{
                                    background: loading ? "#9ca3af" : "linear-gradient(135deg, #3b82f6 0%, #2563eb 100%)",
                                    alignSelf: "flex-end",
                                    minWidth: "200px",
                                    marginTop: 0
                                }}
                            >
                                {loading ? "⏳ Učitavanje..." : "🔍 Učitaj trendove"}
                            </button>
                        </div>
                    </div>

                    {/* Results */}
                    {trends.length > 0 && (
                        <div style={{ display: "grid", gap: "1.5rem", gridTemplateColumns: "repeat(auto-fill, minmax(350px, 1fr))" }}>
                            {trends.map((trend, index) => (
                                <div 
                                    key={index}
                                    className="card"
                                    style={{
                                        background: "white",
                                        border: "1px solid #e5e7eb",
                                        borderRadius: "12px",
                                        overflow: "hidden",
                                        boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1)"
                                    }}
                                >
                                    {/* Product Image */}
                                    <div style={{ 
                                        width: "100%", 
                                        height: "250px", 
                                        background: "#f3f4f6",
                                        position: "relative",
                                        overflow: "hidden"
                                    }}>
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
                                        <div style={{
                                            position: "absolute",
                                            bottom: "10px",
                                            left: "10px",
                                            background: "rgba(59, 130, 246, 0.95)",
                                            color: "white",
                                            padding: "0.5rem 0.9rem",
                                            borderRadius: "999px",
                                            fontSize: "1.1rem",
                                            fontWeight: 700,
                                            boxShadow: "0 2px 8px rgba(59,130,246,0.15)"
                                        }}>
                                            €{trend.priceEur.toFixed(2)}
                                        </div>
                                        {/* Trend level badge */}
                                        <div style={{
                                            position: "absolute",
                                            top: "10px",
                                            right: "10px",
                                            background: "rgba(255, 255, 255, 0.95)",
                                            padding: "0.5rem 0.75rem",
                                            borderRadius: "999px",
                                            fontSize: "0.875rem",
                                            fontWeight: 600
                                        }}>
                                            {trend.trendLevel}
                                        </div>
                                    </div>

                                    {/* Product Info */}
                                    <div style={{ padding: "1.5rem" }}>
                                        <div style={{ marginBottom: "1rem" }}>
                                            <div style={{ 
                                                fontSize: "0.875rem", 
                                                color: "#8A95B0",
                                                marginBottom: "0.25rem",
                                                fontWeight: 600
                                            }}>
                                                {trend.brand}
                                            </div>
                                            <h3 style={{ 
                                                fontSize: "1.25rem", 
                                                fontWeight: 700, 
                                                margin: 0,
                                                color: "#c9d3e4"
                                            }}>
                                                {trend.productName}
                                            </h3>
                                            <div style={{ 
                                                fontSize: "1.5rem", 
                                                fontWeight: 700, 
                                                color: "#3b82f6",
                                                marginTop: "0.5rem"
                                            }}>
                                                €{trend.priceEur.toFixed(2)}
                                            </div>
                                        </div>

                                        {/* Trend Scores */}
                                        <div style={{ 
                                            display: "grid", 
                                            gridTemplateColumns: "repeat(3, 1fr)", 
                                            gap: "0.75rem", 
                                            marginBottom: "1rem",
                                            padding: "1rem",
                                            background: "#1A1F2E",
                                            borderRadius: "8px"
                                        }}>
                                            <div style={{ textAlign: "center" }}>
                                                <div style={{ fontSize: "0.75rem", color: "#8A95B0" }}>Trend skor</div>
                                                <div style={{ fontSize: "1.25rem", fontWeight: 700, color: "#c9d3e4" }}>
                                                    {trend.finalTrendScore.toFixed(1)}
                                                </div>
                                            </div>
                                            <div style={{ textAlign: "center" }}>
                                                <div style={{ fontSize: "0.75rem", color: "#8A95B0" }}>TikTok</div>
                                                <div style={{ fontSize: "1.25rem", fontWeight: 700, color: "#dc2626" }}>
                                                    {trend.tiktokScore.toFixed(1)}
                                                </div>
                                            </div>
                                            <div style={{ textAlign: "center" }}>
                                                <div style={{ fontSize: "0.75rem", color: "#8A95B0" }}>Instagram</div>
                                                <div style={{ fontSize: "1.25rem", fontWeight: 700, color: "#c026d3" }}>
                                                    {trend.instagramScore.toFixed(1)}
                                                </div>
                                            </div>
                                        </div>

                                        {/* Key Features */}
                                        <div style={{ marginBottom: "1rem" }}>
                                            <div style={{ 
                                                fontSize: "0.875rem", 
                                                fontWeight: 600, 
                                                color: "#c9d3e4",
                                                marginBottom: "0.5rem"
                                            }}>
                                                Ključne karakteristike:
                                            </div>
                                            <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
                                                {trend.keyFeatures.map((feature, idx) => (
                                                    <span 
                                                        key={idx}
                                                        style={{
                                                            padding: "0.25rem 0.75rem",
                                                            background: "rgba(79, 142, 247, 0.15)",
                                                            color: "#4F8EF7",
                                                            borderRadius: "999px",
                                                            fontSize: "0.75rem",
                                                            fontWeight: 500
                                                        }}
                                                    >
                                                        {feature}
                                                    </span>
                                                ))}
                                            </div>
                                        </div>

                                        {/* Popular Colors */}
                                        <div style={{ marginBottom: "1rem" }}>
                                            <div style={{ 
                                                fontSize: "0.875rem", 
                                                fontWeight: 600, 
                                                color: "#c9d3e4",
                                                marginBottom: "0.5rem"
                                            }}>
                                                Popularne boje:
                                            </div>
                                            <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
                                                {trend.popularColors.map((color, idx) => (
                                                    <span 
                                                        key={idx}
                                                        style={{
                                                            padding: "0.25rem 0.75rem",
                                                            background: "#2A3045",
                                                            color: "#c9d3e4",
                                                            borderRadius: "999px",
                                                            fontSize: "0.75rem",
                                                            fontWeight: 500
                                                        }}
                                                    >
                                                        {color}
                                                    </span>
                                                ))}
                                            </div>
                                        </div>

                                        {/* Social Stats */}
                                        <div style={{ 
                                            fontSize: "0.75rem", 
                                            color: "#8A95B0",
                                            borderTop: "1px solid #2A3045",
                                            paddingTop: "1rem"
                                        }}>
                                            <div>📱 TikTok: {(trend.tiktokViews / 1000000000).toFixed(1)}B views</div>
                                            <div>📸 Instagram: {(trend.instagramPosts / 1000000).toFixed(1)}M posts</div>
                                        </div>
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}

                    {trends.length === 0 && !loading && (
                        <div className="card" style={{ textAlign: "center", padding: "3rem", color: "#8A95B0" }}>
                            <p style={{ fontSize: "1.25rem", marginBottom: "0.5rem" }}>📊 Nema podataka o trendovima</p>
                            <p>Odaberite kategoriju i kliknite "🔍 Učitaj trendove"</p>
                        </div>
                    )}
                </div>
            )}

            {/* SCRAPERS TAB */}
            {activeTab === "scrapers" && (
                <div>
                    <div className="card" style={{ marginBottom: "1.5rem" }}>
                        <div style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
                            <div style={{ minWidth: 180 }}>
                                <label className="field-label">Zalando stranice</label>
                                <input type="number" className="input-big" value={zalandoPages} min={1} onChange={(e) => setZalandoPages(Number(e.target.value) || 1)} />
                            </div>
                            <div style={{ minWidth: 180 }}>
                                <label className="field-label">Deichmann stranice</label>
                                <input type="number" className="input-big" value={deichmannPages} min={1} onChange={(e) => setDeichmannPages(Number(e.target.value) || 1)} />
                            </div>
                            <div style={{ minWidth: 220 }}>
                                <label className="field-label">Kategorija</label>
                                <select className="input-big" value={selectedCategory} onChange={(e) => setSelectedCategory(e.target.value)}>
                                    {categories.map(c => <option key={c} value={c}>{c}</option>)}
                                </select>
                            </div>
                            <div>
                                <button
                                    onClick={runScrapers}
                                    disabled={loading}
                                    className="button-big"
                                    style={{
                                        background: loading ? "#9ca3af" : "linear-gradient(135deg, #10b981 0%, #059669 100%)",
                                        minWidth: "200px"
                                    }}
                                >
                                    {loading ? "⏳ Pokrećanje..." : "🚀 Pokreni scrapere"}
                                </button>
                            </div>
                        </div>
                    </div>

                    {/* FILTER BAR – Modernized */}
                    <div 
                        style={{
                            marginTop: "1.5rem",
                            marginBottom: "1.5rem",
                            padding: "1.25rem",
                            borderRadius: "12px",
                            background: "#161A23",
                            border: "1px solid #2A3045",
                            display: "flex",
                            flexWrap: "wrap",
                            gap: "1rem",
                            alignItems: "flex-end"
                        }}
                    >
                        {/* Brand (Searchable Select) */}
                        <div style={{ minWidth: 220 }}>
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
                        <div style={{ minWidth: 160 }}>
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

                        <div style={{ minWidth: 200 }}>
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
                        <div style={{ minWidth: 150 }}>
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

                        <div style={{ minWidth: 150 }}>
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
                        <div style={{ minWidth: 160 }}>
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
                        <div style={{ minWidth: 100 }}>
                            <label className="field-label">Stranice</label>
                            <input
                                type="number"
                                className="input-big"
                                min={1}
                                value={filterPages}
                                onChange={(e) => setFilterPages(Number(e.target.value) || 1)}
                            />
                        </div>

                        <div style={{ minWidth: 260 }}>
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
                        <label 
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: "0.5rem",
                                cursor: "pointer",
                                marginTop: "auto"
                            }}
                        >
                            <input
                                type="checkbox"
                                checked={filterImportToCore}
                                onChange={(e) => setFilterImportToCore(e.target.checked)}
                            />
                            <span style={{ fontSize: 14, color: "#c9d3e4" }}>Uvezi u bazu</span>
                        </label>

                        {/* Run button */}
                        <button
                            onClick={runZalandoFiltered}
                            className="button-big"
                            style={{
                                background: "linear-gradient(135deg, #6366f1 0%, #4f46e5 100%)",
                                marginLeft: "auto",
                                minWidth: 180,
                            }}
                        >
                            🔍 Pokreni pretragu
                        </button>
                    </div>

                    {scraperResults.length > 0 && (
                        <div style={{ display: "grid", gap: "1rem", gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))" }}>
                            {scraperResults.map((result, index) => (
                                <div key={index} className="card">
                                    <h3 style={{ fontSize: "1.25rem", fontWeight: 600, marginBottom: "1rem" }}>
                                        {result.source}
                                    </h3>
                                    <div style={{ fontSize: "2rem", fontWeight: 700, color: "#10b981", marginBottom: "0.5rem" }}>
                                        {result.productsCount}
                                    </div>
                                    <div style={{ fontSize: "0.875rem", color: "#6b7280" }}>products scraped</div>
                                    <div style={{
                                        marginTop: "1rem",
                                        padding: "0.5rem",
                                        background: "#ecfdf5",
                                        borderRadius: "6px",
                                        textAlign: "center",
                                        color: "#059669",
                                        fontWeight: 600
                                    }}>
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
                                    <div key={p.id} className="card" style={{ padding: 12 }}>
                                        <div style={{ width: "100%", height: 160, overflow: "hidden", borderRadius: 8, background: "#f3f4f6" }}>
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
                                        <div style={{ paddingTop: 8 }}>
                                            <div style={{ fontSize: 12, color: "#8A95B0" }}>{p.brand}</div>
                                            <div style={{ fontWeight: 700, margin: "6px 0", color: "#c9d3e4" }}>{p.name}</div>
                                            <div style={{ color: "#059669", fontWeight: 700 }}>{p.price != null ? `${p.price}` : "-"}</div>
                                            {p.url && (
                                                <a href={p.url} target="_blank" rel="noopener noreferrer" style={{ color: "#4F8EF7" }}>Pogledaj</a>
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
                                    <div key={idx} className="card" style={{ padding: 12 }}>
                                        <div style={{ width: "100%", height: 160, overflow: "hidden", borderRadius: 8, background: "#1A1F2E" }}>
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
                                        <div style={{ paddingTop: 8 }}>
                                            <div style={{ fontSize: 12, color: "#8A95B0" }}>{p.brand}</div>
                                            <div style={{ fontWeight: 700, margin: "6px 0", color: "#c9d3e4" }}>{p.name}</div>
                                            <div style={{ color: "#059669", fontWeight: 700 }}>{p.price || "-"}</div>
                                            {p.url && (
                                                <a href={p.url} target="_blank" rel="noopener noreferrer" style={{ color: "#4F8EF7" }}>Pogledaj</a>
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
