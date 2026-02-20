import React, { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { getArtikliPaged } from "../services/artikliApi";
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

const CACHE_KEY_ARTIKLI_PAGED = "cached_artikli_paged_";
const CACHE_KEY_TOTAL_COUNT = "cached_artikli_total_count_";
const CACHE_KEY_SEZONE = "cached_sezone";

export default function ArtikliListPage() {
  const [artikli, setArtikli] = useState<ArtikalListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [sezone, setSezone] = useState<Sezona[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Pagination state
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(50);

  // Filter states
  const [searchNaziv, setSearchNaziv] = useState("");
  const [filterSezona, setFilterSezona] = useState<number | "">("");
  const [filterMinCena, setFilterMinCena] = useState("");
  const [filterMaxCena, setFilterMaxCena] = useState("");
  const [filterMinKolicina, setFilterMinKolicina] = useState("");
  const [filterMaxKolicina, setFilterMaxKolicina] = useState("");
  const [showFilters, setShowFilters] = useState(false);

  // Sorting state
  const [sortBy, setSortBy] = useState<"naziv" | "prodajnaCena" | "nabavnaCena" | "kolicina" | "id">("naziv");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("asc");

  // Jump-to-page state
  const [jumpTo, setJumpTo] = useState<string>("1");

  // Handle column header click for sorting
  const handleSort = (column: "naziv" | "prodajnaCena" | "nabavnaCena" | "kolicina" | "id") => {
    if (sortBy === column) {
      // Toggle direction if clicking the same column
      setSortDir(sortDir === "asc" ? "desc" : "asc");
    } else {
      // Set new column and default to ascending
      setSortBy(column);
      setSortDir("asc");
    }
    setPageNumber(1);
  };

  // Render sort indicator (arrow)
  const renderSortIndicator = (column: "naziv" | "prodajnaCena" | "nabavnaCena" | "kolicina" | "id") => {
    if (sortBy !== column) return null;
    return sortDir === "asc" ? " ▲" : " ▼";
  };

  useEffect(() => {
    let aborted = false;

    const loadSezone = async () => {
      try {
        const cached = localStorage.getItem(CACHE_KEY_SEZONE);
        if (cached) {
          setSezone(JSON.parse(cached));
        }

        const sezoneData = await getSezone();
        if (!aborted) {
          setSezone(sezoneData ?? []);
          localStorage.setItem(CACHE_KEY_SEZONE, JSON.stringify(sezoneData));
        }
      } catch {
        // best-effort
      }
    };

    loadSezone();

    return () => {
      aborted = true;
    };
  }, []);

  const filters = useMemo(() => {
    const f: {
      naziv?: string;
      sezonaId?: number | "";
      minCena?: number;
      maxCena?: number;
      minKolicina?: number;
      maxKolicina?: number;
      sortBy?: "naziv" | "prodajnaCena" | "nabavnaCena" | "kolicina" | "id";
      sortDir?: "asc" | "desc";
    } = {};

    if (searchNaziv.trim()) f.naziv = searchNaziv.trim();
    if (filterSezona !== "") f.sezonaId = filterSezona;

    if (filterMinCena) f.minCena = Number(filterMinCena);
    if (filterMaxCena) f.maxCena = Number(filterMaxCena);
    if (filterMinKolicina) f.minKolicina = Number(filterMinKolicina);
    if (filterMaxKolicina) f.maxKolicina = Number(filterMaxKolicina);

    f.sortBy = sortBy;
    f.sortDir = sortDir;

    return f;
  }, [searchNaziv, filterSezona, filterMinCena, filterMaxCena, filterMinKolicina, filterMaxKolicina, sortBy, sortDir]);

  // keep jump input in sync
  useEffect(() => {
    setJumpTo(String(pageNumber));
  }, [pageNumber]);

  useEffect(() => {
    let aborted = false;

    const load = async () => {
      const filterKey = JSON.stringify({ pageNumber, pageSize, ...filters });
      
      const sessionCached = sessionStorage.getItem(CACHE_KEY_ARTIKLI_PAGED + filterKey);
      const sessionTotal = sessionStorage.getItem(CACHE_KEY_TOTAL_COUNT + filterKey);

      if (sessionCached && sessionTotal) {
        setArtikli(JSON.parse(sessionCached));
        setTotalCount(Number(sessionTotal));
        setLoading(false);
      }

      if (!sessionCached) {
        setLoading(true);
      }
      setError(null);

      try {
        const data = await getArtikliPaged<ArtikalListItem>(pageNumber, pageSize, filters);
        if (aborted) return;

        setArtikli(data.items ?? []);
        setTotalCount(data.totalCount ?? 0);

        try {
          sessionStorage.setItem(CACHE_KEY_ARTIKLI_PAGED + filterKey, JSON.stringify(data.items));
          sessionStorage.setItem(CACHE_KEY_TOTAL_COUNT + filterKey, String(data.totalCount));
        } catch {
          sessionStorage.clear();
        }
      } catch (e: unknown) {
        if (aborted) return;
        console.error(e);
        setError(e instanceof Error ? e.message : "Greška pri učitavanju podataka.");
      } finally {
        if (!aborted) setLoading(false);
      }
    };

    load();

    return () => {
      aborted = true;
    };
  }, [pageNumber, pageSize, filters]);

  const clearFilters = () => {
    setSearchNaziv("");
    setFilterSezona("");
    setFilterMinCena("");
    setFilterMaxCena("");
    setFilterMinKolicina("");
    setFilterMaxKolicina("");
    setPageNumber(1);
  };

  const activeFiltersCount = [
    searchNaziv,
    filterSezona !== "",
    filterMinCena,
    filterMaxCena,
    filterMinKolicina,
    filterMaxKolicina,
  ].filter(Boolean).length;

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

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
      {/* Compact header + pagination in single row */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: "0.75rem",
          gap: 12,
        }}
      >
        {/* Left: Title + Pagination controls - single line, no wrap */}
        <div style={{ display: "flex", gap: 12, alignItems: "center", minWidth: 0, overflow: "hidden" }}>
          <h2 style={{ fontSize: "1.125rem", fontWeight: 600, margin: 0, color: "#1f2937", whiteSpace: "nowrap", flexShrink: 0 }}>
            Lista artikala <span style={{ color: "#6b7280", fontWeight: 400, fontSize: "0.9375rem" }}>({artikli.length} / {totalCount})</span>
          </h2>

          <div style={{ display: "flex", gap: 6, alignItems: "center", whiteSpace: "nowrap", flexShrink: 0 }}>
            <button
              className="button-big"
              style={{ padding: "2px 8px", background: pageNumber <= 1 ? "#9ca3af" : "#6b7280", fontSize: "0.75rem", minWidth: "28px", lineHeight: "1.5" }}
              disabled={pageNumber <= 1}
              onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
              title="Prethodna"
            >
              ←
            </button>

            <div style={{ display: "flex", gap: 4, alignItems: "center" }}>
              <input
                className="input-big"
                style={{ marginBottom: 0, width: "42px", padding: "2px 4px", fontSize: "0.75rem", textAlign: "center", height: "24px" }}
                type="number"
                min={1}
                max={totalPages}
                value={jumpTo}
                onChange={(e) => setJumpTo(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    const parsed = Number(jumpTo);
                    if (!Number.isFinite(parsed)) return;
                    const target = Math.min(totalPages, Math.max(1, Math.trunc(parsed)));
                    setPageNumber(target);
                  }
                }}
              />
              <span style={{ color: "#6b7280", fontSize: "0.75rem" }}>/ {totalPages}</span>
            </div>

            <button
              className="button-big"
              style={{ padding: "2px 8px", background: pageNumber >= totalPages ? "#9ca3af" : "#6b7280", fontSize: "0.75rem", minWidth: "28px", lineHeight: "1.5" }}
              disabled={pageNumber >= totalPages}
              onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
              title="Sledeća"
            >
              →
            </button>

            <span style={{ color: "#d1d5db", fontSize: "0.75rem", margin: "0 2px" }}>|</span>

            <span style={{ color: "#6b7280", fontSize: "0.75rem" }}>Po strani:</span>
            <select
              className="input-big"
              style={{ 
                marginBottom: 0, 
                width: "70px", 
                padding: "2px 6px 2px 8px", 
                fontSize: "0.75rem", 
                height: "24px",
                backgroundImage: "url('data:image/svg+xml;charset=UTF-8,%3csvg xmlns=%27http://www.w3.org/2000/svg%27 viewBox=%270 0 16 16%27%3e%3cpath fill=%27none%27 stroke=%27%236b7280%27 stroke-linecap=%27round%27 stroke-linejoin=%27round%27 stroke-width=%272%27 d=%27m2 5 6 6 6-6%27/%3e%3c/svg%3e')",
                backgroundRepeat: "no-repeat",
                backgroundPosition: "right 4px center",
                backgroundSize: "12px",
                paddingRight: "22px",
              }}
              value={pageSize}
              onChange={(e) => {
                setPageSize(Number(e.target.value));
                setPageNumber(1);
              }}
            >
              {[25, 50, 100, 200].map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Right: Compact filter button - unchanged */}
        <button
          onClick={() => setShowFilters(!showFilters)}
          className="button-big"
          style={{
            background: showFilters ? "#dc2626" : "#3b82f6",
            display: "inline-flex",
            alignItems: "center",
            gap: "3px",
            padding: "3px 7px",
            fontSize: "0.6875rem",
            whiteSpace: "nowrap",
            flexShrink: 0,
            maxWidth: "80px",
          }}
        >
          {showFilters ? "Sakrij" : "Filteri"}
          {activeFiltersCount > 0 && (
            <span
              style={{
                background: "white",
                color: "#3b82f6",
                borderRadius: "50%",
                width: "15px",
                height: "15px",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontSize: "0.625rem",
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
          <h3 style={{ fontWeight: 600, fontSize: "1.125rem", marginBottom: "1rem" }}>🔍 Filteri</h3>

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
                onChange={(e) => {
                  setSearchNaziv(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div>
              <label className="field-label">Sezona</label>
              <select
                className="input-big"
                value={filterSezona}
                onChange={(e) => {
                  setFilterSezona(e.target.value ? Number(e.target.value) : "");
                  setPageNumber(1);
                }}
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
                onChange={(e) => {
                  setFilterMinCena(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div>
              <label className="field-label">Max. cena (RSD)</label>
              <input
                type="number"
                className="input-big"
                placeholder="999999"
                value={filterMaxCena}
                onChange={(e) => {
                  setFilterMaxCena(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div>
              <label className="field-label">Min. količina</label>
              <input
                type="number"
                className="input-big"
                placeholder="0"
                value={filterMinKolicina}
                onChange={(e) => {
                  setFilterMinKolicina(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div>
              <label className="field-label">Max. količina</label>
              <input
                type="number"
                className="input-big"
                placeholder="999"
                value={filterMaxKolicina}
                onChange={(e) => {
                  setFilterMaxKolicina(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>
          </div>

          <div style={{ display: "flex", gap: "12px", marginTop: "1rem" }}>
            <button onClick={clearFilters} className="button-big" style={{ background: "#6b7280", maxWidth: "160px", padding: "8px 12px", fontSize: "0.875rem" }}>
              Resetuj filtere
            </button>
          </div>
        </div>
      )}

      {artikli.length === 0 ? (
        <div style={{ textAlign: "center", padding: "3rem", color: "#6b7280" }}>
          <p style={{ fontSize: "1.125rem", fontWeight: 600, marginBottom: "0.5rem" }}>Nema rezultata</p>
          <p>Pokušajte da promenite filtere pretrage</p>
        </div>
      ) : (
        <div style={{ overflowX: "auto" }}>
          <table className="table">
            <thead>
              <tr>
                <th 
                  onClick={() => handleSort("id")}
                  style={{ cursor: "pointer", userSelect: "none" }}
                  title="Klikni za sortiranje po ID-ju"
                >
                  ID{renderSortIndicator("id")}
                </th>
                <th 
                  onClick={() => handleSort("naziv")}
                  style={{ cursor: "pointer", userSelect: "none" }}
                  title="Klikni za sortiranje po nazivu"
                >
                  Naziv{renderSortIndicator("naziv")}
                </th>
                <th 
                  onClick={() => handleSort("prodajnaCena")}
                  style={{ textAlign: "right", cursor: "pointer", userSelect: "none" }}
                  title="Klikni za sortiranje po prodajnoj ceni"
                >
                  Prodajna cena{renderSortIndicator("prodajnaCena")}
                </th>
                <th 
                  onClick={() => handleSort("nabavnaCena")}
                  style={{ textAlign: "right", cursor: "pointer", userSelect: "none" }}
                  title="Klikni za sortiranje po nabavnoj ceni"
                >
                  Nabavna cena{renderSortIndicator("nabavnaCena")}
                </th>
                <th 
                  onClick={() => handleSort("kolicina")}
                  style={{ textAlign: "center", cursor: "pointer", userSelect: "none" }}
                  title="Klikni za sortiranje po količini"
                >
                  Količina{renderSortIndicator("kolicina")}
                </th>
                <th style={{ textAlign: "center" }}>Akcije</th>
              </tr>
            </thead>
            <tbody>
              {artikli.map((a) => (
                <tr key={a.id}>
                  <td style={{ color: "#6b7280" }}>{a.id}</td>
                  <td style={{ fontWeight: 600 }}>{a.naziv}</td>
                  <td style={{ textAlign: "right", color: "#059669", fontWeight: 700 }}>
                    {(a.prodajnaCena ?? 0).toFixed(2)} RSD
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
                        padding: "6px 12px",
                        borderRadius: "8px",
                        textDecoration: "none",
                        fontSize: "0.8125rem",
                        fontWeight: 600,
                        display: "inline-block",
                        boxShadow: "0 4px 10px rgba(37, 99, 235, 0.18)",
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

      {/* Bottom pagination - ultra-minimal */}
      <div style={{ display: "flex", justifyContent: "center", alignItems: "center", gap: 6, marginTop: "0.5rem", paddingTop: "6px", borderTop: "1px solid #e5e7eb" }}>
        <button
          className="button-big"
          style={{ padding: "2px 8px", background: pageNumber <= 1 ? "#9ca3af" : "#6b7280", fontSize: "0.75rem", minWidth: "28px", lineHeight: "1.5" }}
          disabled={pageNumber <= 1}
          onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
        >
          ←
        </button>
        <span style={{ color: "#6b7280", fontSize: "0.75rem", minWidth: "50px", textAlign: "center" }}>
          {pageNumber} / {totalPages}
        </span>
        <button
          className="button-big"
          style={{ padding: "2px 8px", background: pageNumber >= totalPages ? "#9ca3af" : "#6b7280", fontSize: "0.75rem", minWidth: "28px", lineHeight: "1.5" }}
          disabled={pageNumber >= totalPages}
          onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
        >
          →
        </button>
      </div>
    </div>
  );
}
