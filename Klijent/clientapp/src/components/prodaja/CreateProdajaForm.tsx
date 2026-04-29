import { useState, useMemo, useCallback, useEffect, useRef } from "react";
import { KreirajProdajuDto } from "../../types/prodaja/prodaja";
import { useToast } from "../Toast";

type ArtikalOption = { id: number; naziv: string; cena: number };

interface CreateProdajaFormProps {
    artikli: ArtikalOption[];
    onSearchArtikli?: (query: string) => Promise<ArtikalOption[]>;
    onSubmit: (data: KreirajProdajuDto) => Promise<void>;
}

function safeNumber(value: unknown, fallback = 0) {
    const n = typeof value === "number" ? value : Number(value);
    return Number.isFinite(n) ? n : fallback;
}

function mergeArtikli(base: ArtikalOption[], incoming: ArtikalOption[]) {
    const map = new Map<number, ArtikalOption>();
    for (const x of base) map.set(x.id, x);
    for (const x of incoming) map.set(x.id, x);
    return Array.from(map.values()).sort((a, b) => a.naziv.localeCompare(b.naziv, "sr-Latn", { sensitivity: "base" }));
}

function normalizeRacun(value: string): string {
    return value.trim();
}

function buildNextRacunSuggestion(value: string): string | null {
    const trimmed = normalizeRacun(value);
    if (!trimmed) return null;

    const match = trimmed.match(/^(.*?)(\d+)(\D*)$/);
    if (!match) return null;

    const [, prefix, digits, suffix] = match;
    const next = (Number(digits) + 1).toString().padStart(digits.length, "0");
    return `${prefix}${next}${suffix}`;
}

export default function CreateProdajaForm({ artikli, onSearchArtikli, onSubmit }: CreateProdajaFormProps) {
    const toast = useToast();

    const [knownArtikli, setKnownArtikli] = useState<ArtikalOption[]>(artikli);
    const [brojRacuna, setBrojRacuna] = useState("");
    const [stavke, setStavke] = useState<{ idArtikal: number; kolicina: number; cena: number }[]>([
        { idArtikal: artikli[0]?.id ?? 0, kolicina: 1, cena: artikli[0]?.cena ?? 0 },
    ]);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const artikalOptions = useMemo(() => {
        return knownArtikli.map((a) => (
            <option key={a.id} value={a.id}>
                {a.naziv} - {a.cena} RSD
            </option>
        ));
    }, [knownArtikli]);

    const [searchQuery, setSearchQuery] = useState("");
    const [debouncedQuery, setDebouncedQuery] = useState("");
    const [showSearchResults, setShowSearchResults] = useState(false);
    const [selectedIndex, setSelectedIndex] = useState(0);
    const [filteredArtikli, setFilteredArtikli] = useState<ArtikalOption[]>([]);
    const [isSearching, setIsSearching] = useState(false);
    const searchRef = useRef<HTMLDivElement>(null);
    const remoteSearchSeq = useRef(0);

    useEffect(() => {
        setKnownArtikli((prev) => mergeArtikli(prev, artikli));
    }, [artikli]);

    useEffect(() => {
        if (!knownArtikli.length) return;
        setStavke((prev) => {
            if (!prev.length) {
                return [{ idArtikal: knownArtikli[0].id, kolicina: 1, cena: knownArtikli[0].cena }];
            }
            return prev.map((s, idx) => {
                if (idx === 0 && s.idArtikal === 0) {
                    return { ...s, idArtikal: knownArtikli[0].id, cena: knownArtikli[0].cena };
                }
                return s;
            });
        });
    }, [knownArtikli]);

    useEffect(() => {
        const timer = setTimeout(() => {
            setDebouncedQuery(searchQuery);
        }, 250);
        return () => clearTimeout(timer);
    }, [searchQuery]);

    useEffect(() => {
        if (onSearchArtikli) return;
        const q = debouncedQuery.trim().toLowerCase();
        if (!q) {
            setFilteredArtikli([]);
            setIsSearching(false);
            return;
        }
        const local = knownArtikli
            .filter((a) => a.naziv.toLowerCase().includes(q))
            .slice(0, 10);
        setFilteredArtikli(local);
        setIsSearching(false);
    }, [debouncedQuery, knownArtikli, onSearchArtikli]);

    useEffect(() => {
        if (!onSearchArtikli) return;
        const q = debouncedQuery.trim();
        if (!q) {
            setFilteredArtikli([]);
            setIsSearching(false);
            return;
        }

        const seq = ++remoteSearchSeq.current;
        let cancelled = false;
        setIsSearching(true);

        onSearchArtikli(q)
            .then((rows) => {
                if (cancelled || seq !== remoteSearchSeq.current) return;
                const data = (rows ?? []).slice(0, 20);
                setFilteredArtikli(data);
                if (data.length > 0) {
                    setKnownArtikli((prev) => mergeArtikli(prev, data));
                }
            })
            .catch((err) => {
                if (cancelled || seq !== remoteSearchSeq.current) return;
                console.error("Search artikli failed:", err);
                setFilteredArtikli([]);
            })
            .finally(() => {
                if (!cancelled && seq === remoteSearchSeq.current) {
                    setIsSearching(false);
                }
            });

        return () => {
            cancelled = true;
        };
    }, [debouncedQuery, onSearchArtikli]);

    useEffect(() => {
        setSelectedIndex(0);
    }, [filteredArtikli.length]);

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (searchRef.current && !searchRef.current.contains(event.target as Node)) {
                setShowSearchResults(false);
            }
        };

        document.addEventListener("mousedown", handleClickOutside);
        return () => document.removeEventListener("mousedown", handleClickOutside);
    }, []);

    const addStavka = () =>
        setStavke((s) => [
            ...s,
            { idArtikal: knownArtikli[0]?.id ?? 0, kolicina: 1, cena: knownArtikli[0]?.cena ?? 0 },
        ]);

    const quickAddArtikal = useCallback((artikal: ArtikalOption) => {
        setKnownArtikli((prev) => mergeArtikli(prev, [artikal]));
        setStavke((s) => [...s, { idArtikal: artikal.id, kolicina: 1, cena: safeNumber(artikal.cena, 0) }]);
        setSearchQuery("");
        setShowSearchResults(false);
        setSelectedIndex(0);
    }, []);

    const removeStavka = (index: number) => setStavke((s) => s.filter((_, i) => i !== index));

    const updateStavka = (
        index: number,
        patch: Partial<{ idArtikal: number; kolicina: number; cena: number }>
    ) =>
        setStavke((s) => {
            const copy = [...s];
            const prev = copy[index];

            const next = {
                ...prev,
                ...patch,
                kolicina: safeNumber(patch.kolicina ?? prev.kolicina, 1),
                cena: safeNumber(patch.cena ?? prev.cena, 0),
            };

            copy[index] = next;
            return copy;
        });

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (!showSearchResults || filteredArtikli.length === 0) return;

        switch (e.key) {
            case "ArrowDown":
                e.preventDefault();
                setSelectedIndex((prev) => (prev + 1) % filteredArtikli.length);
                break;
            case "ArrowUp":
                e.preventDefault();
                setSelectedIndex((prev) => (prev - 1 + filteredArtikli.length) % filteredArtikli.length);
                break;
            case "Enter":
                e.preventDefault();
                if (filteredArtikli[selectedIndex]) {
                    quickAddArtikal(filteredArtikli[selectedIndex]);
                }
                break;
            case "Escape":
                setShowSearchResults(false);
                break;
        }
    };

    const handleSubmit = async () => {
        setError(null);

        const normalizedRacun = normalizeRacun(brojRacuna);
        if (normalizedRacun.length < 3) {
            setError("Broj računa mora imati najmanje 3 karaktera.");
            return;
        }

        if (!stavke.length) {
            setError("Dodajte bar jednu stavku.");
            return;
        }

        if (invalidStavkeCount > 0) {
            setError("Proverite stavke: količina mora biti > 0 i cena ne može biti negativna.");
            return;
        }

        const payload: KreirajProdajuDto = {
            brojRacuna: normalizedRacun,
            idObjekat: 1,
            nacinPlacanja: "Gotovina",
            stavke: stavke.map((s) => ({
                ...s,
                kolicina: Math.max(1, Math.trunc(safeNumber(s.kolicina, 1))),
                cena: safeNumber(s.cena, 0),
            })),
        };

        setIsSubmitting(true);
        try {
            await onSubmit(payload);
            setBrojRacuna("");
            setStavke([{ idArtikal: knownArtikli[0]?.id ?? 0, kolicina: 1, cena: knownArtikli[0]?.cena ?? 0 }]);
            toast.success("Prodaja uspešna!");
        } catch (err: unknown) {
            console.error(err);
            const msg = err instanceof Error ? err.message : "Greška pri kreiranju prodaje";
            setError(msg);
            toast.error(msg);
        } finally {
            setIsSubmitting(false);
        }
    };

    const ukupno = useMemo(() => {
        return stavke.reduce((sum, s) => sum + safeNumber(s.kolicina, 0) * safeNumber(s.cena, 0), 0);
    }, [stavke]);

    const invalidStavkeCount = useMemo(() => {
        return stavke.filter((s) => safeNumber(s.kolicina, 0) <= 0 || safeNumber(s.cena, 0) < 0 || s.idArtikal <= 0).length;
    }, [stavke]);

    const racunSuggestions = useMemo(() => {
        const suggestions = new Set<string>();
        const next = buildNextRacunSuggestion(brojRacuna);
        if (next) suggestions.add(next);
        if (!normalizeRacun(brojRacuna)) {
            suggestions.add(`POS-${new Date().getFullYear()}-001`);
        }
        return Array.from(suggestions).slice(0, 4);
    }, [brojRacuna]);

    const canSubmit =
        normalizeRacun(brojRacuna).length >= 3 &&
        stavke.length > 0 &&
        invalidStavkeCount === 0 &&
        !isSubmitting;

    useEffect(() => {
        const onWindowKeyDown = (event: KeyboardEvent) => {
            if ((event.ctrlKey || event.metaKey) && event.key === "Enter" && canSubmit) {
                event.preventDefault();
                void handleSubmit();
            }
        };

        window.addEventListener("keydown", onWindowKeyDown);
        return () => window.removeEventListener("keydown", onWindowKeyDown);
    }, [canSubmit, handleSubmit]);

    return (
        <div className="space-y-4">
            <section className="rounded-xl border border-border bg-surface p-4">
                <h2 className="mb-4 text-xl font-semibold text-foreground">Nova prodaja</h2>
                <div className="form-progress mb-3">
                    <div className={`form-step ${normalizeRacun(brojRacuna).length >= 3 ? "form-step--done" : "form-step--pending"}`}>
                        <span className="form-step-label">Broj računa</span>
                    </div>
                    <div className={`form-step ${stavke.length > 0 ? "form-step--done" : "form-step--pending"}`}>
                        <span className="form-step-label">Stavke: {stavke.length}</span>
                    </div>
                    <div className={`form-step ${invalidStavkeCount === 0 ? "form-step--done" : "form-step--warning"}`}>
                        <span className="form-step-label">Validacija: {invalidStavkeCount === 0 ? "OK" : `${invalidStavkeCount} problema`}</span>
                    </div>
                </div>

                <label className="form-field-label">Broj računa <span className="form-required">*</span></label>
                <input
                    placeholder="Npr. POS-2026-001"
                    value={brojRacuna}
                    onChange={(e) => setBrojRacuna(e.target.value)}
                    className="form-input"
                />
                {racunSuggestions.length > 0 && (
                    <div className="mt-2 flex flex-wrap gap-2">
                        {racunSuggestions.map((suggestion) => (
                            <button
                                key={suggestion}
                                type="button"
                                onClick={() => setBrojRacuna(suggestion)}
                                className="form-suggestion"
                            >
                                {suggestion}
                            </button>
                        ))}
                    </div>
                )}
                <p className="form-helper">Tip: Ctrl+Enter čuva prodaju kada je forma validna.</p>
            </section>

            <section className="relative rounded-xl border border-border bg-surface p-4" ref={searchRef}>
                <label className="form-field-label">Pretraži i dodaj artikal</label>
                <input
                    type="text"
                    placeholder="Pretraži artikle po nazivu..."
                    value={searchQuery}
                    onChange={(e) => {
                        setSearchQuery(e.target.value);
                        setShowSearchResults(true);
                    }}
                    onFocus={() => setShowSearchResults(true)}
                    onKeyDown={handleKeyDown}
                    className="form-input"
                />

                {showSearchResults && searchQuery.trim() && (
                    <div className="form-search-results absolute left-4 right-4 top-[calc(100%-4px)] z-20 mt-2">
                        {isSearching ? (
                            <div className="form-search-loading">Pretražujem...</div>
                        ) : filteredArtikli.length > 0 ? (
                            filteredArtikli.map((art, idx) => (
                                <button
                                    key={art.id}
                                    type="button"
                                    onClick={() => quickAddArtikal(art)}
                                    className={`form-search-item ${idx === selectedIndex ? "form-search-item--active" : ""}`}
                                    onMouseEnter={() => setSelectedIndex(idx)}
                                >
                                    <div>
                                        <div className="form-search-item-name">{art.naziv}</div>
                                        <div className="form-search-item-meta">ID: {art.id}</div>
                                    </div>
                                    <div className="form-search-item-price">{art.cena} RSD</div>
                                </button>
                            ))
                        ) : (
                            <div className="form-search-empty">Nema rezultata za „{searchQuery}“</div>
                        )}
                    </div>
                )}
            </section>

            <section className="rounded-xl border border-border bg-surface p-4">
                <div className="mb-3 flex items-center justify-between">
                    <h3 className="text-sm font-semibold uppercase tracking-wide text-muted">Stavke ({stavke.length})</h3>
                    <button
                        type="button"
                        onClick={addStavka}
                        className="form-btn-add"
                    >
                        + Dodaj stavku
                    </button>
                </div>

                <div className="space-y-3">
                    {stavke.map((s, i) => (
                        <div key={i} className="form-stavka">
                            <div>
                                <label className="mb-1 block text-xs text-muted">Artikal</label>
                                <select
                                    value={s.idArtikal}
                                    onChange={(e) => {
                                        const id = Number(e.target.value);
                                        const art = knownArtikli.find((a) => a.id === id);
                                        updateStavka(i, { idArtikal: id, cena: art?.cena ?? s.cena });
                                    }}
                                    className="form-input"
                                >
                                    {artikalOptions}
                                </select>
                            </div>

                            <div>
                                <label className="mb-1 block text-xs text-muted">Količina</label>
                                <input
                                    type="number"
                                    min={1}
                                    value={s.kolicina}
                                    onChange={(e) => updateStavka(i, { kolicina: Number(e.target.value) })}
                                    className="form-input"
                                />
                            </div>

                            <div>
                                <label className="mb-1 block text-xs text-muted">Cena</label>
                                <input
                                    type="number"
                                    min={0}
                                    value={s.cena}
                                    onChange={(e) => updateStavka(i, { cena: Number(e.target.value) })}
                                    className="form-input"
                                />
                            </div>

                            <div>
                                <label className="mb-1 block text-xs text-muted">Iznos (obračunato)</label>
                                <div className="form-calculated">
                                    {(safeNumber(s.kolicina, 0) * safeNumber(s.cena, 0)).toFixed(2)} RSD
                                </div>
                            </div>

                            <div className="flex items-end">
                                <button
                                    type="button"
                                    onClick={() => removeStavka(i)}
                                    className="form-btn-danger"
                                >
                                    Ukloni
                                </button>
                            </div>
                        </div>
                    ))}
                </div>
            </section>

            <section className="form-summary-bar">
                <div className="form-summary-total">Ukupno: <span>{safeNumber(ukupno, 0).toFixed(2)} RSD</span></div>
                <button
                    type="button"
                    onClick={handleSubmit}
                    disabled={!canSubmit}
                    className="form-btn-primary"
                >
                    {isSubmitting ? "Kreiram..." : "Sačuvaj prodaju"}
                </button>
            </section>

            {error && <p className="form-error">{error}</p>}
        </div>
    );
}
