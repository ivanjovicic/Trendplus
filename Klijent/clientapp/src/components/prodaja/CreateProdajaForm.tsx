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

        if (!stavke.length) {
            setError("Dodajte bar jednu stavku.");
            return;
        }

        const payload: KreirajProdajuDto = {
            brojRacuna,
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
            toast.success("Prodaja uspesna");
        } catch (err: unknown) {
            console.error(err);
            const msg = err instanceof Error ? err.message : "Greska pri kreiranju prodaje";
            setError(msg);
            toast.error(msg);
        } finally {
            setIsSubmitting(false);
        }
    };

    const ukupno = useMemo(() => {
        return stavke.reduce((sum, s) => sum + safeNumber(s.kolicina, 0) * safeNumber(s.cena, 0), 0);
    }, [stavke]);

    return (
        <div className="space-y-4">
            <section className="rounded-xl border border-[#2f323b] bg-[#14161d] p-4">
                <h2 className="mb-4 text-xl font-semibold text-[#f3f6ff]">Nova prodaja</h2>
                <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Broj racuna</label>
                <input
                    placeholder="Broj racuna"
                    value={brojRacuna}
                    onChange={(e) => setBrojRacuna(e.target.value)}
                    className="w-full rounded-xl border border-[#2f323b] bg-[#1a1b1f] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
                />
            </section>

            <section className="relative rounded-xl border border-[#2f323b] bg-[#14161d] p-4" ref={searchRef}>
                <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Pretrazi i dodaj artikal</label>
                <input
                    type="text"
                    placeholder="Pretrazi artikle po nazivu..."
                    value={searchQuery}
                    onChange={(e) => {
                        setSearchQuery(e.target.value);
                        setShowSearchResults(true);
                    }}
                    onFocus={() => setShowSearchResults(true)}
                    onKeyDown={handleKeyDown}
                    className="w-full rounded-xl border border-[#2f323b] bg-[#1a1b1f] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
                />

                {showSearchResults && searchQuery.trim() && (
                    <div className="absolute left-4 right-4 top-[calc(100%-4px)] z-20 mt-2 max-h-80 overflow-y-auto rounded-xl border border-[#2f323b] bg-[#1a1b1f] shadow-xl">
                        {isSearching ? (
                            <div className="px-3 py-5 text-center text-sm text-[#9aabc7]">Pretrazujem...</div>
                        ) : filteredArtikli.length > 0 ? (
                            filteredArtikli.map((art, idx) => (
                                <button
                                    key={art.id}
                                    type="button"
                                    onClick={() => quickAddArtikal(art)}
                                    className={`flex w-full items-center justify-between border-b border-[#262a34] px-3 py-3 text-left transition ${idx === selectedIndex ? "bg-[#1f2d48]" : "hover:bg-[#1f2330]"}`}
                                    onMouseEnter={() => setSelectedIndex(idx)}
                                >
                                    <div>
                                        <div className="font-semibold text-[#e7eeff]">{art.naziv}</div>
                                        <div className="text-xs text-[#8ea0bd]">ID: {art.id}</div>
                                    </div>
                                    <div className="text-sm font-semibold text-emerald-300">{art.cena} RSD</div>
                                </button>
                            ))
                        ) : (
                            <div className="px-3 py-5 text-center text-sm text-[#9aabc7]">Nema rezultata za "{searchQuery}"</div>
                        )}
                    </div>
                )}
            </section>

            <section className="rounded-xl border border-[#2f323b] bg-[#14161d] p-4">
                <div className="mb-3 flex items-center justify-between">
                    <h3 className="text-sm font-semibold uppercase tracking-wide text-[#93a7c8]">Stavke ({stavke.length})</h3>
                    <button
                        type="button"
                        onClick={addStavka}
                        className="rounded-lg border border-[#2d7759] bg-[#1e5b45] px-3 py-1.5 text-xs font-semibold text-emerald-100"
                    >
                        + Dodaj stavku
                    </button>
                </div>

                <div className="space-y-3">
                    {stavke.map((s, i) => (
                        <div key={i} className="grid gap-2 rounded-lg border border-[#2a2f3b] bg-[#1a1b1f] p-3 lg:grid-cols-[1.8fr_0.7fr_0.8fr_auto]">
                            <div>
                                <label className="mb-1 block text-xs text-[#8ea0bd]">Artikal</label>
                                <select
                                    value={s.idArtikal}
                                    onChange={(e) => {
                                        const id = Number(e.target.value);
                                        const art = knownArtikli.find((a) => a.id === id);
                                        updateStavka(i, { idArtikal: id, cena: art?.cena ?? s.cena });
                                    }}
                                    className="w-full rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-2 text-sm text-[#dbe6fb]"
                                >
                                    {artikalOptions}
                                </select>
                            </div>

                            <div>
                                <label className="mb-1 block text-xs text-[#8ea0bd]">Kolicina</label>
                                <input
                                    type="number"
                                    min={1}
                                    value={s.kolicina}
                                    onChange={(e) => updateStavka(i, { kolicina: Number(e.target.value) })}
                                    className="w-full rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-2 text-sm text-[#dbe6fb]"
                                />
                            </div>

                            <div>
                                <label className="mb-1 block text-xs text-[#8ea0bd]">Cena</label>
                                <input
                                    type="number"
                                    min={0}
                                    value={s.cena}
                                    onChange={(e) => updateStavka(i, { cena: Number(e.target.value) })}
                                    className="w-full rounded-lg border border-[#2f323b] bg-[#14161d] px-2 py-2 text-sm text-[#dbe6fb]"
                                />
                            </div>

                            <div className="flex items-end">
                                <button
                                    type="button"
                                    onClick={() => removeStavka(i)}
                                    className="rounded-lg border border-rose-700 bg-rose-900/40 px-3 py-2 text-xs font-semibold text-rose-200"
                                >
                                    Ukloni
                                </button>
                            </div>
                        </div>
                    ))}
                </div>
            </section>

            <section className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-[#2f323b] bg-[#14161d] p-4">
                <div className="text-base font-semibold text-[#f3f6ff]">Ukupno: <span className="text-emerald-300">{safeNumber(ukupno, 0).toFixed(2)} RSD</span></div>
                <button
                    type="button"
                    onClick={handleSubmit}
                    disabled={isSubmitting}
                    className="rounded-xl border border-[#3760b7] bg-[#2d4f95] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[#3760b7] disabled:opacity-60"
                >
                    {isSubmitting ? "Kreiram..." : "Sacuvaj prodaju"}
                </button>
            </section>

            {error && <p className="rounded-lg border border-rose-700 bg-rose-950/30 px-3 py-2 text-sm text-rose-300">{error}</p>}
        </div>
    );
}
