import React, { useEffect, useMemo, useState } from "react";
import { getDnevnikPromena } from "../services/dnevnikPromenaApi";
import type { DnevnikPromenaItem } from "../types/dnevnikPromena";

export default function DnevnikPromenaPage() {
  const [promene, setPromene] = useState<DnevnikPromenaItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Tip promene dropdown options
  const [tipoviPromena, setTipoviPromena] = useState<string[]>([]);

  // Pagination
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(50);

  // Filters
  const [filterTipPromene, setFilterTipPromene] = useState<string | "">("");
  const [searchNaziv, setSearchNaziv] = useState("");
  const [searchBrojRacuna, setSearchBrojRacuna] = useState("");
  const [filterFromDate, setFilterFromDate] = useState("");
  const [filterToDate, setFilterToDate] = useState("");
  const [showFilters, setShowFilters] = useState(false);

  // Sorting
  const [sortBy, setSortBy] = useState<"datum" | "tipPromene" | "iznos" | "naziv">("datum");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("desc");

  // Jump to page
  const [jumpTo, setJumpTo] = useState<string>("1");

  // Load tip promene options
  useEffect(() => {
    let aborted = false;

    const loadTipovi = async () => {
      try {
        const API = import.meta.env.VITE_API_BASE_URL;
        const res = await fetch(`${API}/api/dnevnik-promena/tipovi`);
        if (!res.ok) throw new Error("Failed to load tipovi");
        const data = await res.json();
        if (!aborted) setTipoviPromena(data ?? []);
      } catch (err) {
        console.error("Failed to load tip promene options:", err);
      }
    };

    loadTipovi();

    return () => {
      aborted = true;
    };
  }, []);

  const handleSort = (column: "datum" | "tipPromene" | "iznos" | "naziv") => {
    if (sortBy === column) {
      setSortDir(sortDir === "asc" ? "desc" : "asc");
    } else {
      setSortBy(column);
      setSortDir("asc");
    }
    setPageNumber(1);
  };

  const renderSortIndicator = (column: "datum" | "tipPromene" | "iznos" | "naziv") => {
    if (sortBy !== column) return null;
    return sortDir === "asc" ? " ▲" : " ▼";
  };

  const filters = useMemo(() => {
    const f: Record<string, string | number> = {};

    if (filterTipPromene !== "" && filterTipPromene.trim()) f.tipPromene = filterTipPromene.trim();
    if (searchNaziv.trim()) f.naziv = searchNaziv.trim();
    if (searchBrojRacuna.trim()) f.brojRacuna = searchBrojRacuna.trim();
    if (filterFromDate) f.fromDate = filterFromDate;
    if (filterToDate) f.toDate = filterToDate;

    f.sortBy = sortBy;
    f.sortDir = sortDir;

    return f;
  }, [filterTipPromene, searchNaziv, searchBrojRacuna, filterFromDate, filterToDate, sortBy, sortDir]);

  useEffect(() => {
    setJumpTo(String(pageNumber));
  }, [pageNumber]);

  useEffect(() => {
    let aborted = false;

    const load = async () => {
      setLoading(true);
      setError(null);

      try {
        const data = await getDnevnikPromena(pageNumber, pageSize, filters);
        if (aborted) return;

        setPromene(data.items ?? []);
        setTotalCount(data.totalCount ?? 0);
      } catch (err: unknown) {
        if (aborted) return;
        console.error(err);
        setError((err as Error)?.message ?? "Greška pri učitavanju dnevnika promena.");
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
    setFilterTipPromene("");
    setSearchNaziv("");
    setSearchBrojRacuna("");
    setFilterFromDate("");
    setFilterToDate("");
    setPageNumber(1);
  };

  const activeFiltersCount = [
    filterTipPromene !== "",
    searchNaziv,
    searchBrojRacuna,
    filterFromDate,
    filterToDate,
  ].filter(Boolean).length;

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    return date.toLocaleString("sr-RS", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  const getTipPromeneColor = (tip: string) => {
    const tipLower = tip.toLowerCase();
    if (tipLower.includes("prodaja")) return "#059669"; // green
    if (tipLower.includes("nivelacija")) return "#dc2626"; // red
    if (tipLower.includes("unos")) return "#3b82f6"; // blue
    if (tipLower.includes("korekcija")) return "#f59e0b"; // amber
    if (tipLower.includes("povraćaj")) return "#9333ea"; // purple
    return "#6b7280"; // gray
  };

  if (loading && promene.length === 0) {
    return (
      <div className="card">
        <p style={{ textAlign: "center", padding: "2rem" }}>Učitavanje dnevnika promena...</p>
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
    <div className="card" style={{ margin: "2rem auto", maxWidth: "1400px" }}>
      {/* Header + Pagination */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: "0.75rem",
          gap: 12,
        }}
      >
        <div style={{ display: "flex", gap: 12, alignItems: "center", minWidth: 0, overflow: "hidden" }}>
          <h2 style={{ fontSize: "1.125rem", fontWeight: 600, margin: 0, color: "#1f2937", whiteSpace: "nowrap", flexShrink: 0 }}>
            📋 Dnevnik Promena <span style={{ color: "#6b7280", fontWeight: 400, fontSize: "0.9375rem" }}>({promene.length} / {totalCount})</span>
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
            border: "1px solid #e5e7eb",
            borderRadius: "8px",
            padding: "1rem",
            marginBottom: "1rem",
          }}
        >
          <h3 style={{ fontWeight: 600, fontSize: "0.9375rem", marginBottom: "0.75rem", color: "#374151" }}>🔍 Filteri</h3>

          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
              gap: "0.75rem",
            }}
          >
            <div>
              <label className="field-label" style={{ fontSize: "0.8125rem", marginBottom: "0.25rem" }}>Tip promene</label>
              <select
                className="input-big"
                value={filterTipPromene}
                onChange={(e) => {
                  setFilterTipPromene(e.target.value);
                  setPageNumber(1);
                }}
                style={{ fontSize: "0.875rem", padding: "6px 8px", height: "32px" }}
              >
                <option value="">Sve promene</option>
                {tipoviPromena.map((tip) => (
                  <option key={tip} value={tip}>
                    {tip}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="field-label" style={{ fontSize: "0.8125rem", marginBottom: "0.25rem" }}>Artikal (naziv)</label>
              <input
                type="text"
                className="input-big"
                placeholder="Unesite naziv..."
                value={searchNaziv}
                onChange={(e) => {
                  setSearchNaziv(e.target.value);
                  setPageNumber(1);
                }}
                style={{ fontSize: "0.875rem", padding: "6px 8px", height: "32px" }}
              />
            </div>

            <div>
              <label className="field-label" style={{ fontSize: "0.8125rem", marginBottom: "0.25rem" }}>Broj računa</label>
              <input
                type="text"
                className="input-big"
                placeholder="SEED-0001..."
                value={searchBrojRacuna}
                onChange={(e) => {
                  setSearchBrojRacuna(e.target.value);
                  setPageNumber(1);
                }}
                style={{ fontSize: "0.875rem", padding: "6px 8px", height: "32px" }}
              />
            </div>

            <div>
              <label className="field-label" style={{ fontSize: "0.8125rem", marginBottom: "0.25rem" }}>Datum od</label>
              <input
                type="date"
                className="input-big"
                value={filterFromDate}
                onChange={(e) => {
                  setFilterFromDate(e.target.value);
                  setPageNumber(1);
                }}
                style={{ fontSize: "0.875rem", padding: "6px 8px", height: "32px" }}
              />
            </div>

            <div>
              <label className="field-label" style={{ fontSize: "0.8125rem", marginBottom: "0.25rem" }}>Datum do</label>
              <input
                type="date"
                className="input-big"
                value={filterToDate}
                onChange={(e) => {
                  setFilterToDate(e.target.value);
                  setPageNumber(1);
                }}
                style={{ fontSize: "0.875rem", padding: "6px 8px", height: "32px" }}
              />
            </div>
          </div>

          <div style={{ display: "flex", gap: "8px", marginTop: "0.75rem" }}>
            <button onClick={clearFilters} className="button-big" style={{ background: "#6b7280", padding: "6px 12px", fontSize: "0.8125rem", height: "32px" }}>
              Resetuj filtere
            </button>
          </div>
        </div>
      )}

      {promene.length === 0 ? (
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
                  onClick={() => handleSort("datum")}
                  style={{ cursor: "pointer", userSelect: "none", minWidth: "140px" }}
                  title="Klikni za sortiranje po datumu"
                >
                  Datum{renderSortIndicator("datum")}
                </th>
                <th 
                  onClick={() => handleSort("tipPromene")}
                  style={{ cursor: "pointer", userSelect: "none" }}
                  title="Klikni za sortiranje po tipu promene"
                >
                  Tip promene{renderSortIndicator("tipPromene")}
                </th>
                <th 
                  onClick={() => handleSort("naziv")}
                  style={{ cursor: "pointer", userSelect: "none" }}
                  title="Klikni za sortiranje po nazivu artikla"
                >
                  Artikal{renderSortIndicator("naziv")}
                </th>
                <th>Dobavljač</th>
                <th>Račun</th>
                <th 
                  onClick={() => handleSort("iznos")}
                  style={{ textAlign: "right", cursor: "pointer", userSelect: "none" }}
                  title="Klikni za sortiranje po iznosu"
                >
                  Iznos{renderSortIndicator("iznos")}
                </th>
                <th style={{ textAlign: "center" }}>Stara cena</th>
                <th style={{ textAlign: "center" }}>Nova cena</th>
                <th>Komentar</th>
                <th>Korisnik</th>
              </tr>
            </thead>
            <tbody>
              {promene.map((item) => (
                <tr key={item.id}>
                  <td style={{ fontSize: "0.8125rem", color: "#6b7280" }}>
                    {formatDate(item.datum)}
                  </td>
                  <td>
                    <span
                      style={{
                        display: "inline-block",
                        padding: "4px 10px",
                        borderRadius: "6px",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        background: getTipPromeneColor(item.tipPromene),
                        color: "white",
                      }}
                    >
                      {item.tipPromene}
                    </span>
                  </td>
                  <td style={{ fontWeight: 500 }}>
                    {item.artikalNaziv || "-"}
                    {item.artikalId && (
                      <span style={{ fontSize: "0.75rem", color: "#9ca3af", marginLeft: "6px" }}>
                        (#{item.artikalId})
                      </span>
                    )}
                  </td>
                  <td style={{ color: "#6b7280", fontSize: "0.875rem" }}>
                    {item.dobavljacNaziv || "-"}
                  </td>
                  <td style={{ fontSize: "0.8125rem", color: "#6b7280", fontFamily: "monospace" }}>
                    {item.brojRacuna || "-"}
                  </td>
                  <td style={{ textAlign: "right", fontWeight: 600, color: item.iznos >= 0 ? "#059669" : "#dc2626" }}>
                    {item.iznos.toFixed(2)} RSD
                  </td>
                  <td style={{ textAlign: "center", fontSize: "0.875rem", color: "#6b7280" }}>
                    {item.staraProdajnaCena != null ? `${item.staraProdajnaCena.toFixed(2)} RSD` : "-"}
                  </td>
                  <td style={{ textAlign: "center", fontSize: "0.875rem", color: "#059669", fontWeight: 600 }}>
                    {item.novaProdajnaCena != null ? `${item.novaProdajnaCena.toFixed(2)} RSD` : "-"}
                  </td>
                  <td style={{ maxWidth: "200px", fontSize: "0.8125rem", color: "#6b7280" }}>
                    {item.komentar || "-"}
                  </td>
                  <td style={{ fontSize: "0.8125rem", color: "#6b7280" }}>
                    {item.korisnikIme || "-"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Bottom pagination */}
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
