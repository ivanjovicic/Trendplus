import React, { useState } from "react";
import { useToast } from "../components/Toast";

interface TrendResult {
    hashtag: string;
    category: string;
    tiktokScore: number;
    instagramScore: number;
    finalTrendScore: number;
    trendLevel: string;
    tiktokViews: number;
    tiktokPosts: number;
    instagramPosts: number;
    tiktokEngagement: number;
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
            const response = await fetch(`${API_URL}/api/global-trends/social?category=${selectedCategory}`);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            
            const data = await response.json();
            setTrends(data.trends || []);
            toast.success(`Loaded ${data.trends?.length || 0} trending hashtags`);
        } catch (error) {
            console.error("Fetch trends error:", error);
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
                        <div style={{ display: "grid", gap: "1rem" }}>
                            {trends.map((trend, index) => (
                                <div 
                                    key={index}
                                    className="card"
                                    style={{
                                        background: "linear-gradient(to right, #ffffff, #f9fafb)",
                                        border: "2px solid #e5e7eb"
                                    }}
                                >
                                    <div style={{ display: "flex", alignItems: "center", gap: "1rem", marginBottom: "1rem" }}>
                                        <h3 style={{ fontSize: "1.5rem", fontWeight: 700, margin: 0 }}>
                                            {trend.hashtag}
                                        </h3>
                                        <span style={{
                                            padding: "0.25rem 0.75rem",
                                            borderRadius: "999px",
                                            background: "#fef3c7",
                                            color: "#92400e",
                                            fontSize: "0.875rem",
                                            fontWeight: 600
                                        }}>
                                            {trend.trendLevel}
                                        </span>
                                    </div>

                                    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(150px, 1fr))", gap: "1rem", marginBottom: "1rem" }}>
                                        <div>
                                            <div style={{ fontSize: "0.875rem", color: "#6b7280" }}>Final Score</div>
                                            <div style={{ fontSize: "1.5rem", fontWeight: 700, color: "#1f2937" }}>
                                                {trend.finalTrendScore.toFixed(1)}
                                            </div>
                                            <div style={{
                                                width: "100%",
                                                height: "8px",
                                                background: "#e5e7eb",
                                                borderRadius: "4px",
                                                overflow: "hidden",
                                                marginTop: "0.5rem"
                                            }}>
                                                <div style={{
                                                    width: `${trend.finalTrendScore}%`,
                                                    height: "100%",
                                                    background: "linear-gradient(90deg, #3b82f6, #2563eb)"
                                                }} />
                                            </div>
                                        </div>

                                        <div>
                                            <div style={{ fontSize: "0.875rem", color: "#6b7280" }}>TikTok</div>
                                            <div style={{ fontSize: "1.25rem", fontWeight: 600, color: "#dc2626" }}>
                                                {trend.tiktokScore.toFixed(1)}
                                            </div>
                                        </div>

                                        <div>
                                            <div style={{ fontSize: "0.875rem", color: "#6b7280" }}>Instagram</div>
                                            <div style={{ fontSize: "1.25rem", fontWeight: 600, color: "#c026d3" }}>
                                                {trend.instagramScore.toFixed(1)}
                                            </div>
                                        </div>
                                    </div>

                                    <div style={{ display: "flex", gap: "2rem", fontSize: "0.875rem", color: "#6b7280", flexWrap: "wrap" }}>
                                        <div>
                                            📱 TikTok: {trend.tiktokViews.toLocaleString()} views, {trend.tiktokPosts.toLocaleString()} posts
                                        </div>
                                        <div>
                                            📸 Instagram: {trend.instagramPosts.toLocaleString()} posts
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
