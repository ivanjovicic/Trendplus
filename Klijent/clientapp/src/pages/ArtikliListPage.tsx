import React, { useState, useEffect, useMemo } from "react";
import { Link } from "react-router-dom";
import { getArtikli } from "../services/artikliApi";
import { getSezone } from "../services/sezoneApi";
import type { Sezona } from "../types/Sezona";

type ArtikalListItem = {
  id: number;
  naziv: string;
  prodajnaCena: number;
  kolicina?: number | null;
  tipObuceId?: number | null;
  dobavljacId?: number | null;
  idSezona?: number | null;
  nabavnaCena?: number | null;
};

export default function ArtikliListPage() {
  const [artikli, setArtikli] = useState<ArtikalListItem[]>([]);
  const [sezone, setSezone] = useState<Sezona[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filter states
  const [searchNaziv, setSearchNaziv] = useState("");
  const [filterSezona, setFilterSezona] = useState<number | "">("");
  const [filterMinCena, setFilterMinCena] = useState("");
  const [filterMaxCena, setFilterMaxCena] = useState("");
  const [filterMinKolicina, setFilterMinKolicina] = useState("");
  const [filterMaxKolicina, setFilterMaxKolicina] = useState("");
  const [showFilters, setShowFilters] = useState(false);

  const API = import.meta.env.VITE_API_BASE_URL;

  useEffect(() => {
    let aborted = false;

    const load = async () => {
      try {
        const [artikliData, sezoneData] = await Promise.all([
          getArtikli(),
          getSezone(),
        ]);

        if (!aborted) {
          setArtikli(artikliData ?? []);
          setSezone(sezoneData ?? []);
          setLoading(false);
        }
      } catch (e: any) {
        if (!aborted) {
          console.error(e);
          setError(e?.message ?? "Greška pri učitavanju podataka.");
          setLoading(false);
        }
      }
    };

    load();

    return () => {
      aborted = true;
    };
  }, []);

  const filteredArtikli = useMemo(() => {
    let result = [...artikli];

    // Filter by naziv
    if (searchNaziv.trim()) {
      const query = searchNaziv.toLowerCase();
      result = result.filter((a) =>
        a.naziv.toLowerCase().includes(query)
      );
    }

    // Filter by sezona
    if (filterSezona !== "") {
      result = result.filter((a) => a.idSezona === filterSezona);
    }

    // Filter by min cena
    if (filterMinCena) {
      const min = parseFloat(filterMinCena);
      result = result.filter((a) => a.prodajnaCena >= min);
    }

    // Filter by max cena
    if (filterMaxCena) {
      const max = parseFloat(filterMaxCena);
      result = result.filter((a) => a.prodajnaCena <= max);
    }

    // Filter by min kolicina
    if (filterMinKolicina) {
      const min = parseInt(filterMinKolicina);
      result = result.filter((a) => (a.kolicina ?? 0) >= min);
    }

    // Filter by max kolicina
    if (filterMaxKolicina) {
      const max = parseInt(filterMaxKolicina);
      result = result.filter((a) => (a.kolicina ?? 0) <= max);
    }

    return result;
  }, [
    artikli,
    searchNaziv,
    filterSezona,
    filterMinCena,
    filterMaxCena,
    filterMinKolicina,
    filterMaxKolicina,
  ]);

  const clearFilters = () => {
    setSearchNaziv("");
    setFilterSezona("");
    setFilterMinCena("");
    setFilterMaxCena("");
    setFilterMinKolicina("");
    setFilterMaxKolicina("");
  };

  const activeFiltersCount = [
    searchNaziv,
    filterSezona !== "",
    filterMinCena,
    filterMaxCena,
    filterMinKolicina,
    filterMaxKolicina,
  ].filter(Boolean).length;

  if (loading) {
    return (
      <div className="card">
        <p style={{ textAlign: "center", padding: "2rem" }}>Učitavanje artikala...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="card">
        <p className="error-msg">{error}</p>
      </div>
    );
  }

  return (
    <div className="card" style={{ margin: "2rem auto", maxWidth: "1200px" }}>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: "1.5rem",
        }}
      >
        <h2 className="text-2xl font-semibold">
          Lista artikala ({filteredArtikli.length}/{artikli.length})
        </h2>
        <button
          onClick={() => setShowFilters(!showFilters)}
          className="button-big"
          style={{
            maxWidth: "200px",
            background: showFilters ? "#dc2626" : "#3b82f6",
            display: "flex",
            alignItems: "center",
            gap: "8px",
          }}
        >
          {showFilters ? "Sakrij filtere" : "Prikaži filtere"}
          {activeFiltersCount > 0 && (
            <span
              style={{
                background: "white",
                color: "#3b82f6",
                borderRadius: "50%",
                width: "24px",
                height: "24px",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontSize: "0.75rem",
                fontWeight: 700,
              }}
            >
              {activeFiltersCount}
            </span>
          )}
        </button>
      </div>

      {showFilters && (
        <div
          style={{
            background: "#f9fafb",
            border: "2px solid #e5e7eb",
            borderRadius: "12px",
            padding: "1.5rem",
            marginBottom: "1.5rem",
          }}
        >
          <h3
            style={{
              fontWeight: 600,
              fontSize: "1.125rem",
              marginBottom: "1rem",
            }}
          >
            🔍 Filteri
          </h3>

          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(250px, 1fr))",
              gap: "1rem",
            }}
          >
            <div>
              <label className="field-label">Pretraži po nazivu</label>
              <input
                type="text"
                className="input-big"
                placeholder="Unesite naziv..."
                value={searchNaziv}
                onChange={(e) => setSearchNaziv(e.target.value)}
              />
            </div>

            <div>
              <label className="field-label">Sezona</label>
              <select
                className="input-big"
                value={filterSezona}
                onChange={(e) =>
                  setFilterSezona(e.target.value ? Number(e.target.value) : "")
                }
              >
                <option value="">Sve sezone</option>
                {sezone.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.naziv}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="field-label">Min. cena (RSD)</label>
              <input
                type="number"
                className="input-big"
                placeholder="0"
                value={filterMinCena}
                onChange={(e) => setFilterMinCena(e.target.value)}
              />
            </div>

            <div>
              <label className="field-label">Max. cena (RSD)</label>
              <input
                type="number"
                className="input-big"
                placeholder="999999"
                value={filterMaxCena}
                onChange={(e) => setFilterMaxCena(e.target.value)}
              />
            </div>

            <div>
              <label className="field-label">Min. količina</label>
              <input
                type="number"
                className="input-big"
                placeholder="0"
                value={filterMinKolicina}
                onChange={(e) => setFilterMinKolicina(e.target.value)}
              />
            </div>

            <div>
              <label className="field-label">Max. količina</label>
              <input
                type="number"
                className="input-big"
                placeholder="999"
                value={filterMaxKolicina}
                onChange={(e) => setFilterMaxKolicina(e.target.value)}
              />
            </div>
          </div>

          <div
            style={{
              display: "flex",
              gap: "12px",
              marginTop: "1rem",
            }}
          >
            <button
              onClick={clearFilters}
              className="button-big"
              style={{
                background: "#6b7280",
                maxWidth: "200px",
              }}
            >
              Resetuj filtere
            </button>
          </div>
        </div>
      )}

      {filteredArtikli.length === 0 ? (
        <div
          style={{
            textAlign: "center",
            padding: "3rem",
            color: "#6b7280",
          }}
        >
          <p
            style={{
              fontSize: "1.125rem",
              fontWeight: 600,
              marginBottom: "0.5rem",
            }}
          >
            Nema rezultata
          </p>
          <p>Pokušajte da promenite filtere pretrage</p>
        </div>
      ) : (
        <div style={{ overflowX: "auto" }}>
          <table className="table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Naziv</th>
                <th style={{ textAlign: "right" }}>Prodajna cena</th>
                <th style={{ textAlign: "right" }}>Nabavna cena</th>
                <th style={{ textAlign: "center" }}>Količina</th>
                <th style={{ textAlign: "center" }}>Akcije</th>
              </tr>
            </thead>
            <tbody>
              {filteredArtikli.map((a) => (
                <tr key={a.id}>
                  <td style={{ color: "#6b7280" }}>{a.id}</td>
                  <td style={{ fontWeight: 600 }}>{a.naziv}</td>
                  <td style={{ textAlign: "right", color: "#059669", fontWeight: 700 }}>
                    {a.prodajnaCena.toFixed(2)} RSD
                  </td>
                  <td style={{ textAlign: "right", color: "#6b7280" }}>
                    {a.nabavnaCena ? `${a.nabavnaCena.toFixed(2)} RSD` : "-"}
                  </td>
                  <td style={{ textAlign: "center", color: "#6b7280" }}>{a.kolicina ?? 0}</td>
                  <td style={{ textAlign: "center" }}>
                    <Link
                      to={`/artikli/${a.id}/edit`}
                      style={{
                        background: "#3b82f6",
                        color: "white",
                        padding: "8px 16px",
                        borderRadius: "10px",
                        textDecoration: "none",
                        fontSize: "0.875rem",
                        fontWeight: 700,
                        display: "inline-block",
                        boxShadow: "0 8px 18px rgba(37, 99, 235, 0.20)",
                      }}
                    >
                      Izmeni
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}