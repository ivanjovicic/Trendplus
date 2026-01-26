import React, { useState } from "react";
import { useToast } from "../components/Toast";

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
    const [activeTab, setActiveTab] = useState<"trends" | "scrapers">("trends");
    
    const [selectedCategory, setSelectedCategory] = useState("Patike");
    const [trends, setTrends] = useState<TrendResult[]>([]);
    const [scraperResults, setScraperResults] = useState<ScraperResult[]>([]);

    const API_URL = import.meta.env.VITE_API_BASE_URL || "http://localhost:8080";
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

    const runScrapers = async () => {
        setLoading(true);
        try {
            const response = await fetch(`${API_URL}/api/global-trends/scrape`, {
                method: "POST"
            });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            
            const data = await response.json();
            setScraperResults(data.results || []);
            toast.success("Scrapers completed!");
        } catch (error) {
            console.error("Scraper error:", error);
            toast.error(error instanceof Error ? error.message : "Scraper failed");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={{ maxWidth: "1400px", margin: "2rem auto", padding: "0 1rem" }}>
            {/* Header */}
            <div style={{ marginBottom: "2rem" }}>
                <h1 style={{ fontSize: "2rem", fontWeight: 700, color: "#1f2937", marginBottom: "0.5rem" }}>
                    🌍 Global Trends Analytics
                </h1>
                <p style={{ color: "#6b7280", fontSize: "1rem" }}>
                    Track EU fashion trends and social media buzz from TikTok & Instagram
                </p>
            </div>

            {/* Tabs */}
            <div style={{ 
                display: "flex", 
                gap: "1rem", 
                marginBottom: "2rem",
                borderBottom: "2px solid #e5e7eb"
            }}>
                <button
                    onClick={() => setActiveTab("trends")}
                    style={{
                        padding: "1rem 2rem",
                        background: activeTab === "trends" ? "linear-gradient(135deg, #3b82f6 0%, #2563eb 100%)" : "transparent",
                        color: activeTab === "trends" ? "white" : "#6b7280",
                        border: "none",
                        borderBottom: activeTab === "trends" ? "3px solid #3b82f6" : "none",
                        cursor: "pointer",
                        fontWeight: 600,
                        fontSize: "1rem",
                        borderRadius: "8px 8px 0 0",
                        transition: "all 0.2s"
                    }}
                >
                    📊 Social Media Trends
                </button>
                <button
                    onClick={() => setActiveTab("scrapers")}
                    style={{
                        padding: "1rem 2rem",
                        background: activeTab === "scrapers" ? "linear-gradient(135deg, #10b981 0%, #059669 100%)" : "transparent",
                        color: activeTab === "scrapers" ? "white" : "#6b7280",
                        border: "none",
                        borderBottom: activeTab === "scrapers" ? "3px solid #10b981" : "none",
                        cursor: "pointer",
                        fontWeight: 600,
                        fontSize: "1rem",
                        borderRadius: "8px 8px 0 0",
                        transition: "all 0.2s"
                    }}
                >
                    🔍 EU Market Scrapers
                </button>
            </div>

            {/* TRENDS TAB */}
            {activeTab === "trends" && (
                <div>
                    <div className="card" style={{ marginBottom: "1.5rem" }}>
                        <div style={{ display: "flex", gap: "1rem", alignItems: "center", flexWrap: "wrap" }}>
                            <div style={{ flex: "1", minWidth: "200px" }}>
                                <label style={{ display: "block", marginBottom: "0.5rem", fontWeight: 600 }}>
                                    Category
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
                                {loading ? "⏳ Loading..." : "🔍 Fetch Trends"}
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
                                                (e.target as HTMLImageElement).src = 'https://via.placeholder.com/400x300?text=No+Image';
                                            }}
                                        />
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
                                                color: "#6b7280",
                                                marginBottom: "0.25rem",
                                                fontWeight: 600
                                            }}>
                                                {trend.brand}
                                            </div>
                                            <h3 style={{ 
                                                fontSize: "1.25rem", 
                                                fontWeight: 700, 
                                                margin: 0,
                                                color: "#1f2937"
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
                                            background: "#f9fafb",
                                            borderRadius: "8px"
                                        }}>
                                            <div style={{ textAlign: "center" }}>
                                                <div style={{ fontSize: "0.75rem", color: "#6b7280" }}>Trend Score</div>
                                                <div style={{ fontSize: "1.25rem", fontWeight: 700, color: "#1f2937" }}>
                                                    {trend.finalTrendScore.toFixed(1)}
                                                </div>
                                            </div>
                                            <div style={{ textAlign: "center" }}>
                                                <div style={{ fontSize: "0.75rem", color: "#6b7280" }}>TikTok</div>
                                                <div style={{ fontSize: "1.25rem", fontWeight: 700, color: "#dc2626" }}>
                                                    {trend.tiktokScore.toFixed(1)}
                                                </div>
                                            </div>
                                            <div style={{ textAlign: "center" }}>
                                                <div style={{ fontSize: "0.75rem", color: "#6b7280" }}>Instagram</div>
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
                                                color: "#374151",
                                                marginBottom: "0.5rem"
                                            }}>
                                                Key Features:
                                            </div>
                                            <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
                                                {trend.keyFeatures.map((feature, idx) => (
                                                    <span 
                                                        key={idx}
                                                        style={{
                                                            padding: "0.25rem 0.75rem",
                                                            background: "#dbeafe",
                                                            color: "#1e40af",
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
                                                color: "#374151",
                                                marginBottom: "0.5rem"
                                            }}>
                                                Popular Colors:
                                            </div>
                                            <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem" }}>
                                                {trend.popularColors.map((color, idx) => (
                                                    <span 
                                                        key={idx}
                                                        style={{
                                                            padding: "0.25rem 0.75rem",
                                                            background: "#f3f4f6",
                                                            color: "#4b5563",
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
                                            color: "#6b7280",
                                            borderTop: "1px solid #e5e7eb",
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
                        <div className="card" style={{ textAlign: "center", padding: "3rem", color: "#6b7280" }}>
                            <p style={{ fontSize: "1.25rem", marginBottom: "0.5rem" }}>📊 No trends data</p>
                            <p>Select a category and click "Fetch Trends"</p>
                        </div>
                    )}
                </div>
            )}

            {/* SCRAPERS TAB */}
            {activeTab === "scrapers" && (
                <div>
                    <div className="card" style={{ marginBottom: "1.5rem" }}>
                        <button
                            onClick={runScrapers}
                            disabled={loading}
                            className="button-big"
                            style={{
                                background: loading ? "#9ca3af" : "linear-gradient(135deg, #10b981 0%, #059669 100%)",
                                maxWidth: "300px"
                            }}
                        >
                            {loading ? "⏳ Scraping..." : "🚀 Run All Scrapers"}
                        </button>
                        <p style={{ marginTop: "1rem", color: "#6b7280", fontSize: "0.875rem" }}>
                            Scrape trending products from Zalando, Deichmann and other EU retailers
                        </p>
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

                    {scraperResults.length === 0 && !loading && (
                        <div className="card" style={{ textAlign: "center", padding: "3rem", color: "#6b7280" }}>
                            <p style={{ fontSize: "1.25rem", marginBottom: "0.5rem" }}>🔍 No results yet</p>
                            <p>Click "Run All Scrapers" to fetch data</p>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}
