import React, { useEffect, useState } from "react";
import PovracajWizard from "../components/povracaj/PovracajWizard";
import { getPovracaji } from "../services/povracajApi";
import type { PovracajZaglavlje } from "../types/povracaj";

export default function PovracajPage() {
  const [showWizard, setShowWizard] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const [items, setItems] = useState<PovracajZaglavlje[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize] = useState(25);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getPovracaji(pageNumber, pageSize);
      setItems(res.items ?? []);
      setTotalCount(res.totalCount ?? 0);
    } catch (e: unknown) {
      console.error(e);
      setError(e instanceof Error ? e.message : "Greška pri učitavanju povraćaja");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pageNumber]);

  const handleSuccess = () => {
    setShowWizard(false);
    setSuccessMessage("Zapisnik o povraćaju uspešno kreiran!");
    setTimeout(() => setSuccessMessage(null), 5000);
    setPageNumber(1);
    load();
  };

  const handleCancel = () => {
    setShowWizard(false);
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const formatDate = (iso: string) => {
    const d = new Date(iso);
    return d.toLocaleString("sr-RS", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  return (
    <div style={{ padding: "2rem" }}>
      <div style={{ maxWidth: 1200, margin: "0 auto" }}>
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: "1.5rem",
          }}
        >
          <h1 style={{ fontSize: "2rem", fontWeight: 700, color: "#1f2937" }}>
            ↩️ Povraćaj robe
          </h1>
          {!showWizard && (
            <button
              className="button-big"
              onClick={() => setShowWizard(true)}
              style={{ background: "#3b82f6", fontSize: "1rem", padding: "0.75rem 1.5rem" }}
            >
              + Novi povraćaj
            </button>
          )}
        </div>

        {successMessage && (
          <div
            style={{
              background: "#f0fdf4",
              border: "1px solid #a7f3d0",
              color: "#059669",
              padding: "1rem",
              borderRadius: "8px",
              marginBottom: "1.5rem",
              fontSize: "1rem",
              fontWeight: 600,
            }}
          >
            {successMessage}
          </div>
        )}

        {showWizard ? (
          <PovracajWizard onSuccess={handleSuccess} onCancel={handleCancel} />
        ) : (
          <div className="card">
            <h2 style={{ fontSize: "1.25rem", fontWeight: 700, marginBottom: "1rem" }}>
              Kreirani povraćaji
            </h2>

            {loading && <p style={{ textAlign: "center", padding: "2rem" }}>Učitavanje...</p>}
            {error && <p className="error-msg">{error}</p>}

            {!loading && !error && items.length === 0 && (
              <div style={{ textAlign: "center", padding: "3rem" }}>
                <p style={{ fontSize: "1.125rem", color: "#6b7280", marginBottom: "1rem" }}>
                  Nema kreiranih povraćaja
                </p>
                <p style={{ color: "#9ca3af" }}>
                  Kliknite na dugme "Novi povraćaj" da kreirate zapisnik o povraćaju robe
                </p>
              </div>
            )}

            {!loading && !error && items.length > 0 && (
              <>
                <div style={{ overflowX: "auto" }}>
                  <table className="table">
                    <thead>
                      <tr>
                        <th>Broj</th>
                        <th>Datum</th>
                        <th>Dobavljač</th>
                        <th>Status</th>
                        <th style={{ textAlign: "right" }}>Iznos</th>
                        <th style={{ textAlign: "center" }}>Stavke</th>
                      </tr>
                    </thead>
                    <tbody>
                      {items.map((p) => (
                        <tr key={p.id}>
                          <td style={{ fontFamily: "monospace", fontWeight: 700 }}>{p.brojZapisnika}</td>
                          <td style={{ whiteSpace: "nowrap" }}>{formatDate(p.datumPovracaja)}</td>
                          <td>{p.dobavljacNaziv ?? `#${p.dobavljacId}`}</td>
                          <td>{p.status}</td>
                          <td style={{ textAlign: "right", fontWeight: 700 }}>{p.ukupanIznos.toFixed(2)} RSD</td>
                          <td style={{ textAlign: "center" }}>{p.brojStavki ?? "-"}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    gap: 12,
                    marginTop: "1rem",
                  }}
                >
                  <div style={{ color: "#6b7280", fontSize: "0.875rem" }}>
                    Prikazano: {items.length} / {totalCount}
                  </div>

                  <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                    <button
                      className="button-big button-secondary"
                      type="button"
                      disabled={pageNumber <= 1}
                      onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                      style={{ padding: "6px 10px", marginTop: 0 }}
                    >
                      ←
                    </button>
                    <span style={{ color: "#6b7280", fontSize: "0.875rem" }}>
                      {pageNumber} / {totalPages}
                    </span>
                    <button
                      className="button-big button-secondary"
                      type="button"
                      disabled={pageNumber >= totalPages}
                      onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
                      style={{ padding: "6px 10px", marginTop: 0 }}
                    >
                      →
                    </button>
                  </div>
                </div>
              </>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
