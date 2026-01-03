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
        {/*<Link to="/artikli" className="button-big">*/}
        {/*  ➕ Kreiraj artikal*/}
        {/*</Link>*/}
        <Link to="/unos-robe" className="button-big" style={{ background: "#7c3aed" }}>
          📦 Unos robe
        </Link>
        <Link to="/artikli/lista" className="button-big">
          📋 Pregled i izmena artikala
        </Link>
        <Link to="/prodaja" className="button-big">
          🛒 Prodaja
        </Link>
        {/*<Link to="/tipovi-obuce" className="button-big" style={{ background: "#0891b2" }}>*/}
        {/*  👟 Kreiraj tip obuće*/}
        {/*</Link>*/}
        {/*<Link to="/dobavljaci" className="button-big" style={{ background: "#059669" }}>*/}
        {/*  🏢 Kreiraj dobavljača*/}
        {/*</Link>*/}
        {/*<Link to="/sezone" className="button-big" style={{ background: "#7c3aed" }}>*/}
        {/*  📅 Sezone*/}
        {/*</Link>*/}
        {/*<Link to="/logs" className="button-big" style={{ background: "#059669" }}>*/}
        {/*  📋 Pregled logova*/}
        {/*</Link>*/}
        {/*<Link to="/performance" className="button-big" style={{ background: "#f59e0b" }}>*/}
        {/*  ⚡ Performance Dashboard*/}
        {/*</Link>*/}
      </div>
    </div>
  );
}
