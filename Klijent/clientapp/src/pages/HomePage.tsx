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
        <Link to="/artikli" className="button-big">
          ➕ Kreiraj artikal
        </Link>
        <Link to="/artikli/lista" className="button-big">
          📋 Pregled i izmena artikala
        </Link>
        <Link to="/prodaja" className="button-big">
          🛒 Prodaja
        </Link>
        <Link to="/logs" className="button-big" style={{ background: "#059669" }}>
          📋 Pregled logova
        </Link>
      </div>
    </div>
  );
}
