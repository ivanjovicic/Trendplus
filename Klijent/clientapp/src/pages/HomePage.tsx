import React from "react";
import { Link } from "react-router-dom";

export default function HomePage() {
  return (
    <div className="card" style={{ margin: "2rem auto", maxWidth: 600 }}>
      <h1 className="text-2xl font-bold mb-4">Trendplus</h1>
      <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
        <Link to="/artikli" className="button-big">
          Kreiraj artikal
        </Link>
        <Link to="/artikli/lista" className="button-big">
          Pregled i izmena artikala
        </Link>
        <Link to="/prodaja" className="button-big">
          Prodaja
        </Link>
      </div>
    </div>
  );
}
