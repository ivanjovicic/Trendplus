import { useEffect, useMemo, useState } from "react";
import { getNivelacije } from "../services/artikliApi";
import { NivelacijaItem } from "../types/nivelacije";

type SortBy = "datum" | "artikalid" | "stara" | "nova" | "naziv";

type SortDir = "asc" | "desc";

export default function NivelacijePage() {
  const [items, setItems] = useState<NivelacijaItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize] = useState(50);
  const [totalCount, setTotalCount] = useState(0);

  const [artikalId, setArtikalId] = useState<string>("");
  const [naziv, setNaziv] = useState<string>("");
  const [fromDate, setFromDate] = useState<string>("");
  const [toDate, setToDate] = useState<string>("");

  const [sortBy, setSortBy] = useState<SortBy>("datum");
  const [sortDir, setSortDir] = useState<SortDir>("desc");

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const fetchData = async () => {
    setLoading(true);
    setError(null);

    try {
      const res = await getNivelacije(pageNumber, pageSize, {
        artikalId: artikalId ? Number(artikalId) : undefined,
        naziv: naziv || undefined,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        sortBy,
        sortDir,
      });

      setItems(res.items);
      setTotalCount(res.totalCount);
    } catch (e: any) {
      setError(e?.message ?? "Greška pri učitavanju nivelacija");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pageNumber, sortBy, sortDir, artikalId, naziv, fromDate, toDate]);

  const toggleSort = (field: SortBy) => {
    if (sortBy !== field) {
      setSortBy(field);
      setSortDir("desc");
      setPageNumber(1);
      return;
    }
    setSortDir(prev => (prev === "desc" ? "asc" : "desc"));
    setPageNumber(1);
  };

  return (
    <div className="card" style={{ maxWidth: 1400 }}>
      <h2 className="text-2xl font-semibold mb-6">{"\u{1F4C8}"} Pregled nivelacija</h2>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))",
          gap: "1rem",
          marginBottom: "1.5rem",
          padding: "1rem",
          background: "#f9fafb",
          borderRadius: "12px",
        }}
      >
        <div>
          <label className="field-label" style={{ fontSize: "0.875rem" }}>
            Artikal ID
          </label>
          <input
            className="input-big"
            value={artikalId}
            onChange={e => {
              setArtikalId(e.target.value);
              setPageNumber(1);
            }}
            placeholder="npr. 123"
          />
        </div>

        <div>
          <label className="field-label" style={{ fontSize: "0.875rem" }}>
            Naziv sadrži
          </label>
          <input
            className="input-big"
            value={naziv}
            onChange={e => {
              setNaziv(e.target.value);
              setPageNumber(1);
            }}
            placeholder="npr. patike"
          />
        </div>

        <div>
          <label className="field-label" style={{ fontSize: "0.875rem" }}>
            Od datuma
          </label>
          <input
            type="datetime-local"
            className="input-big"
            value={fromDate}
            onChange={e => {
              setFromDate(e.target.value);
              setPageNumber(1);
            }}
          />
        </div>

        <div>
          <label className="field-label" style={{ fontSize: "0.875rem" }}>
            Do datuma
          </label>
          <input
            type="datetime-local"
            className="input-big"
            value={toDate}
            onChange={e => {
              setToDate(e.target.value);
              setPageNumber(1);
            }}
          />
        </div>

        <div style={{ display: "flex", alignItems: "flex-end" }}>
          <button
            className="button-big"
            onClick={() => {
              setArtikalId("");
              setNaziv("");
              setFromDate("");
              setToDate("");
              setSortBy("datum");
              setSortDir("desc");
              setPageNumber(1);
            }}
            style={{ background: "#6b7280", padding: "8px 16px", marginTop: 0, marginBottom: 0 }}
          >
            Reset
          </button>
        </div>
      </div>

      <div
        style={{
          marginBottom: "1rem",
          padding: "0.75rem",
          background: "#f3f4f6",
          borderRadius: "8px",
          fontSize: "0.95rem",
        }}
      >
        <strong>Ukupno:</strong> {totalCount} | <strong>Stranica:</strong> {pageNumber} / {totalPages}
      </div>

      {loading && <p style={{ textAlign: "center", padding: "2rem" }}>Učitavanje...</p>}
      {error && <p className="error-msg">{error}</p>}

      {!loading && !error && (
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
            <thead>
              <tr style={{ background: "#f3f4f6", borderBottom: "2px solid #e5e7eb" }}>
                <th
                  style={{ padding: 12, textAlign: "left", fontWeight: 600, cursor: "pointer" }}
                  onClick={() => toggleSort("datum")}
                >
                  Datum {sortBy === "datum" ? (sortDir === "desc" ? "?" : "?") : ""}
                </th>
                <th
                  style={{ padding: 12, textAlign: "left", fontWeight: 600, cursor: "pointer" }}
                  onClick={() => toggleSort("artikalid")}
                >
                  Artikal {sortBy === "artikalid" ? (sortDir === "desc" ? "?" : "?") : ""}
                </th>
                <th style={{ padding: 12, textAlign: "left", fontWeight: 600, cursor: "pointer" }} onClick={() => toggleSort("naziv")}>
                  Naziv {sortBy === "naziv" ? (sortDir === "desc" ? "?" : "?") : ""}
                </th>
                <th
                  style={{ padding: 12, textAlign: "right", fontWeight: 600, cursor: "pointer" }}
                  onClick={() => toggleSort("stara")}
                >
                  Stara cena {sortBy === "stara" ? (sortDir === "desc" ? "?" : "?") : ""}
                </th>
                <th
                  style={{ padding: 12, textAlign: "right", fontWeight: 600, cursor: "pointer" }}
                  onClick={() => toggleSort("nova")}
                >
                  Nova cena {sortBy === "nova" ? (sortDir === "desc" ? "?" : "?") : ""}
                </th>
                <th style={{ padding: 12, textAlign: "left", fontWeight: 600 }}>Korisnik</th>
                <th style={{ padding: 12, textAlign: "left", fontWeight: 600 }}>Komentar</th>
              </tr>
            </thead>
            <tbody>
              {items.map(it => (
                <tr key={it.id} style={{ borderBottom: "1px solid #e5e7eb" }}>
                  <td style={{ padding: 12, whiteSpace: "nowrap", fontFamily: "monospace", fontSize: "0.8rem" }}>
                    {new Date(it.datum).toLocaleString("sr-RS")}
                  </td>
                  <td style={{ padding: 12 }}>{it.artikalId ?? "-"}</td>
                  <td style={{ padding: 12 }}>{it.artikalNaziv ?? ""}</td>
                  <td style={{ padding: 12, textAlign: "right" }}>{it.staraProdajnaCena ?? "-"}</td>
                  <td style={{ padding: 12, textAlign: "right", fontWeight: 700 }}>{it.novaProdajnaCena ?? "-"}</td>
                  <td style={{ padding: 12 }}>{it.korisnikIme ?? "-"}</td>
                  <td style={{ padding: 12, wordBreak: "break-word" }}>{it.komentar ?? ""}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {items.length === 0 && (
            <p style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>Nema rezultata.</p>
          )}

          {totalPages > 1 && (
            <div style={{ display: "flex", justifyContent: "center", gap: "0.5rem", marginTop: "1.5rem" }}>
              <button
                className="button-big"
                onClick={() => setPageNumber(p => Math.max(1, p - 1))}
                disabled={pageNumber === 1}
                style={{ width: "auto", padding: "8px 16px", marginTop: 0 }}
              >
                Prethodna
              </button>

              <span style={{ padding: "8px 16px", alignSelf: "center", fontWeight: 600 }}>
                {pageNumber} / {totalPages}
              </span>

              <button
                className="button-big"
                onClick={() => setPageNumber(p => Math.min(totalPages, p + 1))}
                disabled={pageNumber === totalPages}
                style={{ width: "auto", padding: "8px 16px", marginTop: 0 }}
              >
                Slede?a
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
