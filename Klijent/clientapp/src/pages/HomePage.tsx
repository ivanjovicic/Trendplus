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
          <Link to="/image-upload-test" className="button-big" style={{ background: "#3b82f6" }}>
            📸 Upload slika (Test)
          </Link>
        </div>

        <Link
          to="/global-trends"
          style={{
            textDecoration: "none",
            display: "flex",
            alignItems: "center",
            gap: "1rem",
            padding: "1.5rem",
            background: "linear-gradient(135deg, #8b5cf6 0%, #7c3aed 100%)",
            borderRadius: "16px",
            color: "white",
            boxShadow: "0 10px 25px -5px rgba(139, 92, 246, 0.35), 0 8px 10px -6px rgba(139, 92, 246, 0.25)",
            transition: "all 0.3s ease",
          }}
          onMouseEnter={(e) => {
            e.currentTarget.style.transform = "translateY(-4px)";
            e.currentTarget.style.boxShadow = "0 20px 30px -10px rgba(139, 92, 246, 0.45), 0 12px 15px -8px rgba(139, 92, 246, 0.35)";
          }}
          onMouseLeave={(e) => {
            e.currentTarget.style.transform = "translateY(0)";
            e.currentTarget.style.boxShadow = "0 10px 25px -5px rgba(139, 92, 246, 0.35), 0 8px 10px -6px rgba(139, 92, 246, 0.25)";
          }}
        >
          <div
            style={{
              fontSize: "3rem",
              lineHeight: 1,
            }}
          >
            🌍
          </div>
          <div style={{ flex: 1 }}>
            <div style={{ fontSize: "1.25rem", fontWeight: 700, marginBottom: "0.25rem" }}>
              Global Trends
            </div>
            <div style={{ fontSize: "0.875rem", opacity: 0.9 }}>
              EU Market & Social Media Analytics
            </div>
          </div>
        </Link>
      </div>
    </div>
  );
}
