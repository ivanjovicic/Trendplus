import React from "react";
import { Link } from "react-router-dom";

export default function HomePage() {
  return (
    <div className="card" style={{ maxWidth: "600px" }}>
      <h1
        className="text-2xl font-bold mb-6"
        style={{ marginBottom: "2rem", fontSize: "2rem" }}
      >
        📦 Trendplus
      </h1>
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <Link to="/unos-robe" className="button-big" style={{ background: "#7c3aed" }}>
          📦 Unos robe
        </Link>
        <Link to="/artikli/lista" className="button-big">
          📋 Pregled i izmena artikala
        </Link>
        <Link to="/prodaja" className="button-big">
          🛒 Prodaja
        </Link>
        
        <div style={{ borderTop: "2px solid #e5e7eb", marginTop: "1rem", paddingTop: "1rem" }}>
          <h3 style={{ fontSize: "0.875rem", color: "#6b7280", marginBottom: "0.75rem", fontWeight: 600 }}>
            🔧 Monitoring & Admin
          </h3>
          
          <Link to="/outbox" className="button-big" style={{ background: "#8b5cf6" }}>
            📨 Outbox Dashboard
          </Link>
          <Link to="/performance" className="button-big" style={{ background: "#f59e0b" }}>
            ⚡ Performance Dashboard
          </Link>
          <Link to="/logs" className="button-big" style={{ background: "#059669" }}>
            📋 Logs
          </Link>
        </div>
      </div>
    </div>
  );
}
