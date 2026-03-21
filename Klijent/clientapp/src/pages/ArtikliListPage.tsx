import React, { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { PackageSearch, ChevronLeft, ChevronRight, ArrowUp, ArrowDown, ArrowUpDown, X, PackageX } from "lucide-react";
import { getArtikliPaged } from "../services/artikliApi";
import { getSezone } from "../services/sezoneApi";
import type { Sezona } from "../types/Sezona";
import type { Dobavljac } from "../types/Dobavljaci";
import { getDataScope, setDataScope as persistDataScope } from "../utils/dataScope";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel, InventoryState } from "../components/inventory/InventoryPageShell";

type ArtikalListItem = {
  id: number;
  naziv: string;
  prodajnaCena: number;
  kolicina?: number | null;
  tipObuceId?: number | null;
  dobavljacId?: number | null;
  dobavljacNaziv?: string | null;
  idSezona?: number | null;
  nabavnaCena?: number | null;
};

const CACHE_KEY_ARTIKLI_PAGED = "cached_artikli_paged_";
const CACHE_KEY_TOTAL_COUNT = "cached_artikli_total_count_";
const CACHE_KEY_SEZONE = "cached_sezone";

function scopeLabel(scope: "all" | "existing" | "imported"): string {
  return scope === "all" ? "Sve" : scope === "existing" ? "Postojeci" : "Importovani";
}

export default function ArtikliListPage() {
  const [artikli, setArtikli] = useState<ArtikalListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [sezone, setSezone] = useState<Sezona[]>([]);
  const [dobavljaci, setDobavljaci] = useState<Dobavljac[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dataScope, setDataScopeValue] = useState(getDataScope());

  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(50);

  const [searchNaziv, setSearchNaziv] = useState("");
  const [filterSezona, setFilterSezona] = useState<number | "">("");
  const [filterDobavljac, setFilterDobavljac] = useState<number | "">("");
  const [filterMinCena, setFilterMinCena] = useState("");
  const [filterMaxCena, setFilterMaxCena] = useState("");
  const [filterMinKolicina, setFilterMinKolicina] = useState("");
  const [filterMaxKolicina, setFilterMaxKolicina] = useState("");
  const [showFilters, setShowFilters] = useState(false);

  const [sortBy, setSortBy] = useState<"naziv" | "prodajnaCena" | "nabavnaCena" | "kolicina" | "id" | "dobavljac">("naziv");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("asc");

  const [jumpTo, setJumpTo] = useState<string>("1");

  type SortCol = "naziv" | "prodajnaCena" | "nabavnaCena" | "kolicina" | "id" | "dobavljac";

  const handleSort = (column: SortCol) => {
    if (sortBy === column) {
      setSortDir(sortDir === "asc" ? "desc" : "asc");
    } else {
      setSortBy(column);
      setSortDir("asc");
    }
    setPageNumber(1);
  };

  const renderSortIndicator = (column: SortCol) => {
    if (sortBy !== column) return <ArrowUpDown size={12} className="ml-1 inline opacity-30" />;
    return sortDir === "asc"
      ? <ArrowUp size={12} className="ml-1 inline text-[var(--info)]" />
      : <ArrowDown size={12} className="ml-1 inline text-[var(--info)]" />;
  };

  useEffect(() => {
    const handleScopeChange = () => {
      setDataScopeValue(getDataScope());
      setPageNumber(1);
    };

    window.addEventListener("trendplus:data-scope-changed", handleScopeChange);
    return () => {
      window.removeEventListener("trendplus:data-scope-changed", handleScopeChange);
    };
  }, []);

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

  useEffect(() => {
    let aborted = false;
    const API = import.meta.env.VITE_API_BASE_URL as string;
    const loadDobavljaci = async () => {
      try {
        const res = await fetch(`${API}/api/dobavljaci`);
        if (res.ok && !aborted) setDobavljaci(await res.json());
      } catch {
        // best-effort
      }
    };
    loadDobavljaci();
    return () => {
      aborted = true;
    };
  }, []);

  const filters = useMemo(() => {
    const f: {
      naziv?: string;
      sezonaId?: number | "";
      dobavljacId?: number;
      minCena?: number;
      maxCena?: number;
      minKolicina?: number;
      maxKolicina?: number;
      sortBy?: "naziv" | "prodajnaCena" | "nabavnaCena" | "kolicina" | "id" | "dobavljac";
      sortDir?: "asc" | "desc";
    } = {};

    if (searchNaziv.trim()) f.naziv = searchNaziv.trim();
    if (filterSezona !== "") f.sezonaId = filterSezona;
    if (filterDobavljac !== "") f.dobavljacId = Number(filterDobavljac);

    if (filterMinCena) f.minCena = Number(filterMinCena);
    if (filterMaxCena) f.maxCena = Number(filterMaxCena);
    if (filterMinKolicina) f.minKolicina = Number(filterMinKolicina);
    if (filterMaxKolicina) f.maxKolicina = Number(filterMaxKolicina);

    f.sortBy = sortBy;
    f.sortDir = sortDir;

    return f;
  }, [searchNaziv, filterSezona, filterDobavljac, filterMinCena, filterMaxCena, filterMinKolicina, filterMaxKolicina, sortBy, sortDir]);

  useEffect(() => {
    setJumpTo(String(pageNumber));
  }, [pageNumber]);

  useEffect(() => {
    let aborted = false;

    const load = async () => {
      const filterKey = JSON.stringify({ pageNumber, pageSize, dataScope, ...filters });

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
        setError(e instanceof Error ? e.message : "Gre�ka pri ucitavanju podataka.");
      } finally {
        if (!aborted) setLoading(false);
      }
    };

    load();

    return () => {
      aborted = true;
    };
  }, [pageNumber, pageSize, filters, dataScope]);

  const clearFilters = () => {
    setSearchNaziv("");
    setFilterSezona("");
    setFilterDobavljac("");
    setFilterMinCena("");
    setFilterMaxCena("");
    setFilterMinKolicina("");
    setFilterMaxKolicina("");
    setPageNumber(1);
  };

  const activeFiltersCount = [
    searchNaziv,
    filterSezona !== "",
    filterDobavljac !== "",
    filterMinCena,
    filterMaxCena,
    filterMinKolicina,
    filterMaxKolicina,
  ].filter(Boolean).length;

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <InventoryPageShell
      icon={PackageSearch}
      title="Pregled i izmene artikala"
      subtitle="Master lista artikala sa naprednim filterima, sortiranjem i brzim prelaskom na izmenu."
      actions={
        <button
          onClick={() => setShowFilters(!showFilters)}
          className="rounded-xl border border-muted surface-elevated px-3 py-2 text-xs font-semibold text-contrast"
        >
          {showFilters ? "Sakrij filtere" : `Filteri ${activeFiltersCount > 0 ? `(${activeFiltersCount})` : ""}`}
        </button>
      }
    >
      <InventoryKpiRow
        items={[
          { label: "Ukupno artikala", value: `${totalCount}` },
          { label: "Prikazano", value: `${artikli.length}` },
          { label: "Strana", value: `${pageNumber}/${totalPages}` },
          { label: "Data scope", value: dataScope || "all" },
        ]}
      />

      <InventoryPanel>
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <button
            className="flex items-center gap-1 rounded-lg border border-muted bg-surface px-3 py-1.5 text-xs text-contrast disabled:opacity-40"
            disabled={pageNumber <= 1}
            onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
            title="Prethodna strana"
          >
            <ChevronLeft size={14} />
          </button>
          <div className="flex items-center gap-1">
            <span className="text-xs text-muted">Strana</span>
            <input
              className="w-14 rounded-lg border border-muted bg-surface-darker px-2 py-1 text-center text-xs text-contrast"
              type="number"
              min={1}
              max={totalPages}
              value={jumpTo}
              onChange={(e) => setJumpTo(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") {
                  const parsed = Number(jumpTo);
                  if (!Number.isFinite(parsed)) return;
                  const target = Math.min(totalPages, Math.max(1, Math.trunc(parsed)));
                  setPageNumber(target);
                }
              }}
            />
            <span className="text-xs text-muted">/ {totalPages}</span>
          </div>
          <button
            className="flex items-center gap-1 rounded-lg border border-muted bg-surface px-3 py-1.5 text-xs text-contrast disabled:opacity-40"
            disabled={pageNumber >= totalPages}
            onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
            title="Sledeća strana"
          >
            <ChevronRight size={14} />
          </button>
          <span className="mx-1 text-muted">|</span>
          <span className="text-xs text-muted">Po strani</span>
          <select
            className="rounded-lg border border-muted bg-surface-darker px-2 py-1 text-xs text-contrast"
            value={pageSize}
            onChange={(e) => {
              setPageSize(Number(e.target.value));
              setPageNumber(1);
            }}
          >
            {[25, 50, 100, 200].map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </div>

        {showFilters && (
          <div className="mb-4 grid gap-3 rounded-xl border border-muted bg-surface-darker p-3 md:grid-cols-2 xl:grid-cols-4">
            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-muted">Naziv</label>
              <input
                type="text"
                className="w-full rounded-lg border border-muted bg-surface-elevated px-2 py-2 text-sm text-contrast"
                value={searchNaziv}
                onChange={(e) => {
                  setSearchNaziv(e.target.value);
                  setPageNumber(1);
                }}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-muted">Sezona</label>
              <select
                className="w-full rounded-lg border border-muted bg-surface-elevated px-2 py-2 text-sm text-contrast"
                value={filterSezona}
                onChange={(e) => {
                  setFilterSezona(e.target.value ? Number(e.target.value) : "");
                  setPageNumber(1);
                }}
              >
                <option value="">Sve sezone</option>
                {sezone.map((s) => (
                  <option key={s.id} value={s.id}>{s.naziv}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-muted">Dobavljac</label>
              <select
                className="w-full rounded-lg border border-muted bg-surface-elevated px-2 py-2 text-sm text-contrast"
                value={filterDobavljac}
                onChange={(e) => {
                  setFilterDobavljac(e.target.value ? Number(e.target.value) : "");
                  setPageNumber(1);
                }}
              >
                <option value="">Svi dobavljaci</option>
                {dobavljaci.map((d) => (
                  <option key={d.id} value={d.id}>{d.naziv}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-muted">Cena (min/max)</label>
              <div className="grid grid-cols-2 gap-2">
                <input
                  type="number"
                  className="w-full rounded-lg border border-muted bg-surface-elevated px-2 py-2 text-sm text-contrast"
                  value={filterMinCena}
                  onChange={(e) => {
                    setFilterMinCena(e.target.value);
                    setPageNumber(1);
                  }}
                  placeholder="Min"
                />
                <input
                  type="number"
                  className="w-full rounded-lg border border-muted bg-surface-elevated px-2 py-2 text-sm text-contrast"
                  value={filterMaxCena}
                  onChange={(e) => {
                    setFilterMaxCena(e.target.value);
                    setPageNumber(1);
                  }}
                  placeholder="Max"
                />
              </div>
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase tracking-wide text-muted">Kolicina (min/max)</label>
              <div className="grid grid-cols-2 gap-2">
                <input
                  type="number"
                  className="w-full rounded-lg border border-muted bg-surface-elevated px-2 py-2 text-sm text-contrast"
                  value={filterMinKolicina}
                  onChange={(e) => {
                    setFilterMinKolicina(e.target.value);
                    setPageNumber(1);
                  }}
                  placeholder="Min"
                />
                <input
                  type="number"
                  className="w-full rounded-lg border border-muted bg-surface-elevated px-2 py-2 text-sm text-contrast"
                  value={filterMaxKolicina}
                  onChange={(e) => {
                    setFilterMaxKolicina(e.target.value);
                    setPageNumber(1);
                  }}
                  placeholder="Max"
                />
              </div>
            </div>

            <div className="xl:col-span-4 flex flex-wrap items-center gap-2">
              <button
                onClick={clearFilters}
                className="flex items-center gap-1 rounded-lg border border-muted bg-surface px-3 py-2 text-sm text-contrast hover:bg-surface-elevated"
              >
                <X size={13} /> Resetuj sve
              </button>
              {searchNaziv && (
                <span className="flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                  Naziv: {searchNaziv}
                  <button onClick={() => { setSearchNaziv(""); setPageNumber(1); }}><X size={11} /></button>
                </span>
              )}
              {filterSezona !== "" && (
                <span className="flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                  Sezona: {sezone.find(s => s.id === filterSezona)?.naziv ?? filterSezona}
                  <button onClick={() => { setFilterSezona(""); setPageNumber(1); }}><X size={11} /></button>
                </span>
              )}
              {filterDobavljac !== "" && (
                <span className="flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                  Dobavljač: {dobavljaci.find(d => d.id === Number(filterDobavljac))?.naziv ?? filterDobavljac}
                  <button onClick={() => { setFilterDobavljac(""); setPageNumber(1); }}><X size={11} /></button>
                </span>
              )}
              {(filterMinCena || filterMaxCena) && (
                <span className="flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                  Cena: {filterMinCena || "0"} – {filterMaxCena || "∞"}
                  <button onClick={() => { setFilterMinCena(""); setFilterMaxCena(""); setPageNumber(1); }}><X size={11} /></button>
                </span>
              )}
              {(filterMinKolicina || filterMaxKolicina) && (
                <span className="flex items-center gap-1 rounded-full border border-info bg-info/10 px-2 py-0.5 text-xs text-info">
                  Kol: {filterMinKolicina || "0"} – {filterMaxKolicina || "∞"}
                  <button onClick={() => { setFilterMinKolicina(""); setFilterMaxKolicina(""); setPageNumber(1); }}><X size={11} /></button>
                </span>
              )}
            </div>
          </div>
        )}

        {loading && <InventoryState message="Ucitavanje artikala..." tone="warning" />}
        {!loading && error && <InventoryState message={error} tone="danger" />}

        {!loading && !error && (
          <div className="overflow-x-auto rounded-xl border border-muted">
            <table className="min-w-full divide-y divide-muted text-sm">
              <thead className="bg-surface-darker text-muted">
                <tr>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => handleSort("id")}>ID{renderSortIndicator("id")}</th>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => handleSort("naziv")}>Naziv{renderSortIndicator("naziv")}</th>
                  <th className="cursor-pointer px-3 py-3 text-right" onClick={() => handleSort("prodajnaCena")}>Prodajna{renderSortIndicator("prodajnaCena")}</th>
                  <th className="cursor-pointer px-3 py-3 text-right" onClick={() => handleSort("nabavnaCena")}>Nabavna{renderSortIndicator("nabavnaCena")}</th>
                  <th className="cursor-pointer px-3 py-3 text-right" onClick={() => handleSort("kolicina")}>Kolicina{renderSortIndicator("kolicina")}</th>
                  <th className="cursor-pointer px-3 py-3 text-left" onClick={() => handleSort("dobavljac")}>Dobavljac{renderSortIndicator("dobavljac")}</th>
                  <th className="px-3 py-3 text-left">Akcija</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-muted bg-surface-elevated text-contrast">
                {artikli.map((a) => (
                  <tr key={a.id} className="hover:bg-surface/50">
                    <td className="px-3 py-3 text-xs text-muted">{a.id}</td>
                    <td className="px-3 py-3">{a.naziv}</td>
                    <td className="px-3 py-3 text-right">{(a.prodajnaCena ?? 0).toFixed(2)}</td>
                    <td className="px-3 py-3 text-right text-muted">{a.nabavnaCena != null ? a.nabavnaCena.toFixed(2) : "-"}</td>
                    <td className="px-3 py-3 text-right">{a.kolicina ?? "-"}</td>
                    <td className="px-3 py-3">{a.dobavljacNaziv ?? "-"}</td>
                    <td className="px-3 py-3">
                      <Link
                        to={`/artikli/${a.id}`}
                        className="rounded-md border border-info bg-info/10 px-2 py-1 text-xs font-semibold text-info hover:bg-info/20"
                      >
                        Izmeni
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {artikli.length === 0 && (
              <div className="flex flex-col items-center gap-3 py-12 text-center">
                <PackageX size={36} className="text-muted/50" />
                <p className="text-sm font-medium text-muted">
                  {activeFiltersCount > 0
                    ? "Nema artikala za zadate filtere"
                    : `Nema artikala za prikaz '${scopeLabel(dataScope)}'`}
                </p>
                {activeFiltersCount > 0 && (
                  <button
                    onClick={clearFilters}
                    className="flex items-center gap-1 rounded-lg border border-border bg-surface-darker px-3 py-1.5 text-xs text-muted hover:bg-surface-dark"
                  >
                    <X size={12} /> Ukloni filtere
                  </button>
                )}
                {activeFiltersCount === 0 && dataScope !== "all" && (
                  <button
                    onClick={() => {
                      persistDataScope("all");
                      setDataScopeValue("all");
                      window.dispatchEvent(new Event("trendplus:data-scope-changed"));
                    }}
                    className="flex items-center gap-1 rounded-lg border border-primary bg-primary px-3 py-1.5 text-xs text-white hover:bg-primary-dark"
                  >
                    Prikazi sve artikle
                  </button>
                )}
              </div>
            )}
          </div>
        )}
      </InventoryPanel>
    </InventoryPageShell>
  );
}
