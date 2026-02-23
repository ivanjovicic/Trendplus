import React, { useContext, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import AutoReloadOnBackendOnline from "../components/AutoReloadOnBackendOnline";
import WorkerStatusAlert from "../components/WorkerStatusAlert";
import SeasonalImageCarousel from "../components/trendshoes/SeasonalImageCarousel";
import Footer from "../components/Footer";
import { BackendStatusContext } from "../context/BackendStatusContext";
import { getDataScope, setDataScope, type DataScope } from "../utils/dataScope";

export default function AppLayout({ children }: { children: React.ReactNode }) {
    const { online } = useContext(BackendStatusContext);
    const [dataScope, setLocalDataScope] = useState<DataScope>(getDataScope());

    useEffect(() => {
        setDataScope(dataScope);
        window.dispatchEvent(new Event("trendplus:data-scope-changed"));
    }, [dataScope]);

    return (
        <div style={{ minHeight: "100vh", background: "#f3f4f6", display: "flex", flexDirection: "column" }}>
            <AutoReloadOnBackendOnline />
            <WorkerStatusAlert />

            <header
                style={{
                    background: "#111827",
                    color: "white",
                    padding: "0.75rem 1.5rem",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                }}
            >
                <div>
                    <span style={{ fontWeight: 600 }}>Trendplus</span>{" "}
                    <span style={{ opacity: 0.7, fontSize: 14 }}>- backoffice</span>
                </div>

                <div style={{ fontSize: 14, display: "flex", alignItems: "center", gap: 8 }}>
                    <span style={{ color: online ? "#6ee7b7" : "#fecaca" }}>
                        Backend: {online ? "ONLINE" : "OFFLINE"}
                    </span>
                    <span style={{ color: "#d1d5db" }}>Prikaz:</span>
                    <select
                        value={dataScope}
                        onChange={(e) => setLocalDataScope(e.target.value as DataScope)}
                        style={{
                            padding: "2px 8px",
                            borderRadius: 6,
                            border: "1px solid #4b5563",
                            background: "#1f2937",
                            color: "white",
                            fontSize: 12,
                        }}
                        title="Globalni filter izvora podataka"
                    >
                        <option value="all">Sve</option>
                        <option value="existing">Postojeći</option>
                        <option value="imported">Importovani</option>
                    </select>
                </div>
            </header>

            <main style={{ maxWidth: 1200, margin: "1.5rem auto", padding: "0 1rem", flex: 1, width: "100%" }}>
                <nav
                    style={{
                        marginBottom: "1rem",
                        display: "flex",
                        gap: 8,
                        flexWrap: "wrap",
                    }}>
                    <Link to="/" className="button-small">
                        Početna
                    </Link>
                    <Link to="/artikli/lista" className="button-small">
                        Pregled/izmene artikala
                    </Link>
                    <Link to="/prodaja" className="button-small">
                        Prodaja
                    </Link>
                    <Link to="/unos-robe" className="button-small">
                        Unos robe
                    </Link>
                    <Link to="/povracaj" className="button-small">
                        ↩️ Povraćaj robe
                    </Link>
                    <Link to="/nivelacija" className="button-small">
                        Nivelacija cena
                    </Link>
                    <Link to="/nivelacije" className="button-small">
                        Pregled nivelacija
                    </Link>

                    <Link to="/dnevnik-promena" className="button-small">
                        📋 Dnevnik promena
                    </Link>

                    <Link to="/sezone" className="button-small">
                        Sezone
                    </Link>
                    <Link to="/tipovi-obuce" className="button-small">
                        👟 Tipovi obuće
                    </Link>
                    <Link to="/dobavljaci" className="button-small">
                        🏢 Dobavljači
                    </Link>
                    <Link to="/logs" className="button-small">
                        📋 Logovi
                    </Link>
                    <Link to="/performance" className="button-small">
                        ⚡ Performance
                    </Link>
                    <Link to="/analytics" className="button-small">
                        📈 Analitika
                    </Link>
                    <Link to="/analytics-details" className="button-small">
                        📊 Detaljne analize
                    </Link>
                    <Link to="/global-trends" className="button-small">
                        🌍 Global Trends
                    </Link>
                    <Link to="/admin/common-products" className="button-small">
                        🔗 Zajednički proizvodi
                    </Link>
                    <Link to="/release-calendar" className="button-small">
                        🗓️ Release Calendar
                    </Link>
                    <Link to="/deichmann" className="button-small">
                        🥿 Deichmann Scraper
                    </Link>
                    <Link to="/aboutyou" className="button-small">
                        👢 About You Scraper
                    </Link>
                    <Link to="/humanic" className="button-small">
                        👠 Humanic Scraper
                    </Link>
                    <Link to="/scraper-hub" className="button-small">
                        🧩 Scraper Hub Top 10
                    </Link>
                    <Link to="/trend-dashboard" className="button-small">
                        📊 Trend Dashboard
                    </Link>
                    <Link to="/amazon-shoes" className="button-small">
                        🛍 Amazon Shoes
                    </Link>
                    <Link to="/ebay-shoes" className="button-small">
                        🛍️ eBay Shoes
                    </Link>
                    <Link to="/google-shopping" className="button-small">
                        🛒 Google Shopping
                    </Link>
                    <Link to="/open-training" className="button-small">
                        🧠 Open Training
                    </Link>
                    <Link to="/runtime-scoring" className="button-small">
                        🎯 Runtime Scoring
                    </Link>
                    <Link to="/access-import" className="button-small">
                        🗄 Access Import
                    </Link>
                 </nav>

                 {children}

                 <SeasonalImageCarousel />
            </main>

            <Footer />
        </div>
    );
}
