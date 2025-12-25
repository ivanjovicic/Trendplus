import React from "react";
import { Link } from "react-router-dom";

const API = import.meta.env.VITE_API_BASE_URL;

export default function AppLayout({ children }: { children: React.ReactNode }) {
  const [backendOk, setBackendOk] = React.useState<boolean | null>(null);
  const [backendError, setBackendError] = React.useState<string | null>(null);

  React.useEffect(() => {
    let aborted = false;

    const check = async () => {
      try {
        const res = await fetch(`${API}/health`);
        if (aborted) return;

        if (res.ok) {
          setBackendOk(true);
          setBackendError(null);
        } else {
          setBackendOk(false);
          setBackendError(`Status: ${res.status}`);
        }
      } catch (e: any) {
        if (aborted) return;
        setBackendOk(false);
        setBackendError(e?.message ?? "Backend nije dostupan.");
      }
    };

    check();

    return () => {
      aborted = true;
    };
  }, []);

  return (
    <div style={{ minHeight: "100vh", background: "#f3f4f6" }}>
      {/* Header / status bar */}
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
          {backendOk === null && <span>Provera backenda...</span>}
          {backendOk === true && (
            <span style={{ color: "#6ee7b7" }}>Backend OK</span>
          )}
          {backendOk === false && (
            <span style={{ color: "#fecaca" }}>
              Backend ERROR{backendError ? `: ${backendError}` : ""}
            </span>
          )}
        </div>
      </header>

      {/* Glavni sadržaj + globalna navigacija */}
      <main style={{ maxWidth: 1200, margin: "1.5rem auto", padding: "0 1rem" }}>
        <nav
          style={{
            marginBottom: "1rem",
            display: "flex",
            gap: 8,
            flexWrap: "wrap",
          }}
        >
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
        </nav>

        {children}
      </main>
    </div>
  );
}