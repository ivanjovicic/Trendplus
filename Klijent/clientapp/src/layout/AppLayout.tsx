import React, { useContext } from "react";
import { Link } from "react-router-dom";
import AutoReloadOnBackendOnline from "../components/AutoReloadOnBackendOnline";
import { BackendStatusContext } from "../context/BackendStatusContext";

export default function AppLayout({ children }: { children: React.ReactNode }) {
    const { online } = useContext(BackendStatusContext);

    return (
        <div style={{ minHeight: "100vh", background: "#f3f4f6" }}>
            <AutoReloadOnBackendOnline />

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
                    <span style={{ opacity: 0.7, fontSize: 14 }}>– backoffice</span>
                </div>

                <div style={{ fontSize: 14 }}>
                    <span style={{ color: online ? "#6ee7b7" : "#fecaca" }}>
                        Backend: {online ? "ONLINE" : "OFFLINE"}
                    </span>
                </div>
            </header>

            <main style={{ maxWidth: 1200, margin: "1.5rem auto", padding: "0 1rem" }}>
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
                    <Link to="/artikli" className="button-small">
                        Kreiraj artikal
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
                    <Link to="/nivelacija" className="button-small">
                        Nivelacija cena
                    </Link>
                    <Link to="/logs" className="button-small">
                        📋 Logovi
                    </Link>
                    <Link to="/performance" className="button-small">
                        ⚡ Performance
                    </Link>
                </nav>

                {children}
            </main>
        </div>
    );
}