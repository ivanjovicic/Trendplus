import { useState, useMemo, useCallback, useEffect, useRef } from "react";
import { KreirajProdajuDto } from "../../types/prodaja/prodaja";
import { useToast } from "../Toast";

interface CreateProdajaFormProps {
    artikli: { id: number; naziv: string; cena: number }[];
    onSubmit: (data: KreirajProdajuDto) => Promise<void>;
}

function safeNumber(value: unknown, fallback = 0) {
    const n = typeof value === "number" ? value : Number(value);
    return Number.isFinite(n) ? n : fallback;
}

export default function CreateProdajaForm({ artikli, onSubmit }: CreateProdajaFormProps) {
    const toast = useToast();

    const [brojRacuna, setBrojRacuna] = useState("");
    const [stavke, setStavke] = useState<{ idArtikal: number; kolicina: number; cena: number }[]>(
        [
            { idArtikal: artikli[0]?.id ?? 0, kolicina: 1, cena: artikli[0]?.cena ?? 0 },
        ]
    );
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [error, setError] = useState<string | null>(null);

    // Memoize options to prevent heavy re-renders
    const artikalOptions = useMemo(() => {
        console.log("🛠️ Rendering artikal options...");
        return artikli.map((a) => (
            <option key={a.id} value={a.id}>
                {a.naziv} — {a.cena} RSD
            </option>
        ));
    }, [artikli]);

    // Search state
    const [searchQuery, setSearchQuery] = useState("");
    const [debouncedQuery, setDebouncedQuery] = useState("");
    const [showSearchResults, setShowSearchResults] = useState(false);
    const [selectedIndex, setSelectedIndex] = useState(0);
    const searchRef = useRef<HTMLDivElement>(null);

    // Debounce search query
    useEffect(() => {
        const timer = setTimeout(() => {
            setDebouncedQuery(searchQuery);
        }, 300);

        return () => clearTimeout(timer);
    }, [searchQuery]);

    // Filter articles based on search
    const filteredArtikli = useMemo(() => {
        if (!debouncedQuery.trim()) return [];
        
        const query = debouncedQuery.toLowerCase();
        return artikli
            .filter(a => a.naziv.toLowerCase().includes(query))
            .slice(0, 10); // Limit to 10 results
    }, [artikli, debouncedQuery]);

    // Reset selected index when results change
    useEffect(() => {
        setSelectedIndex(0);
    }, [filteredArtikli.length]);

    // Close search results when clicking outside
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
            { idArtikal: artikli[0]?.id ?? 0, kolicina: 1, cena: artikli[0]?.cena ?? 0 },
        ]);

    const quickAddArtikal = useCallback((artikal: { id: number; naziv: string; cena: number }) => {
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
            setStavke([{ idArtikal: artikli[0]?.id ?? 0, kolicina: 1, cena: artikli[0]?.cena ?? 0 }]);
            toast.success("Prodaja uspešna");
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

    return (
        <div className="card">
            <h2 className="text-2xl font-semibold mb-6">🛒 Nova prodaja</h2>

            <div style={{ marginBottom: '1.5rem' }}>
                <label className="field-label">Broj računa</label>
                <input
                    placeholder="Broj računa"
                    value={brojRacuna}
                    onChange={(e) => setBrojRacuna(e.target.value)}
                    className="input-big"
                />
            </div>

            {/* SEARCH SECTION */}
            <div style={{ marginBottom: '1.5rem', position: 'relative' }} ref={searchRef}>
                <label className="field-label">
                    🔍 Pretraži i dodaj artikal
                    <span style={{ fontSize: '0.875rem', fontWeight: 400, marginLeft: '8px', color: '#6b7280' }}>
                        (↑↓ za navigaciju, Enter za dodavanje)
                    </span>
                </label>
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
                    className="input-big"
                    style={{
                        borderColor: showSearchResults && filteredArtikli.length > 0 ? '#2563eb' : undefined,
                    }}
                />

                {/* Search Results Dropdown */}
                {showSearchResults && searchQuery.trim() && (
                    <div style={{
                        position: 'absolute',
                        top: '100%',
                        left: 0,
                        right: 0,
                        background: 'white',
                        border: '2px solid #e5e7eb',
                        borderRadius: '8px',
                        marginTop: '4px',
                        maxHeight: '300px',
                        overflowY: 'auto',
                        zIndex: 1000,
                        boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)',
                    }}>
                        {filteredArtikli.length > 0 ? (
                            filteredArtikli.map((art, idx) => (
                                <div
                                    key={art.id}
                                    onClick={() => quickAddArtikal(art)}
                                    style={{
                                        padding: '12px',
                                        borderBottom: '1px solid #f3f4f6',
                                        cursor: 'pointer',
                                        display: 'flex',
                                        justifyContent: 'space-between',
                                        alignItems: 'center',
                                        transition: 'background 0.15s',
                                        background: idx === selectedIndex ? '#eff6ff' : 'white',
                                    }}
                                    onMouseEnter={() => setSelectedIndex(idx)}
                                >
                                    <div>
                                        <div style={{ fontWeight: 600, color: '#111827' }}>
                                            {art.naziv}
                                        </div>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>
                                            ID: {art.id}
                                        </div>
                                    </div>
                                    <div style={{
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '8px',
                                    }}>
                                        <span style={{
                                            fontWeight: 600,
                                            color: '#059669',
                                            fontSize: '1.125rem',
                                        }}>
                                            {art.cena} RSD
                                        </span>
                                        <button
                                            onClick={(e) => {
                                                e.stopPropagation();
                                                quickAddArtikal(art);
                                            }}
                                            style={{
                                                background: idx === selectedIndex ? '#2563eb' : '#3b82f6',
                                                color: 'white',
                                                padding: '6px 12px',
                                                borderRadius: '6px',
                                                border: 'none',
                                                cursor: 'pointer',
                                                fontSize: '0.875rem',
                                                fontWeight: 600,
                                            }}
                                        >
                                            + Dodaj
                                        </button>
                                    </div>
                                </div>
                            ))
                        ) : (
                            <div style={{
                                padding: '16px',
                                textAlign: 'center',
                                color: '#6b7280',
                            }}>
                                Nema rezultata za "{searchQuery}"
                            </div>
                        )}
                    </div>
                )}
            </div>

            <div style={{ marginBottom: '1.5rem' }}>
                <h3 className="text-lg font-semibold mb-4">Stavke ({stavke.length})</h3>
                
                {stavke.map((s, i) => (
                    <div key={i} style={{ display: 'flex', gap: 12, marginBottom: 12, alignItems: 'flex-start', flexWrap: 'wrap' }}>
                        <div style={{ flex: '1 1 200px', minWidth: '200px' }}>
                            <label className="field-label" style={{ fontSize: '0.875rem' }}>Artikal</label>
                            <select
                                value={s.idArtikal}
                                onChange={(e) => {
                                    const id = Number(e.target.value);
                                    const art = artikli.find((a) => a.id === id);
                                    updateStavka(i, { idArtikal: id, cena: art?.cena ?? s.cena });
                                }}
                                className="input-big"
                                style={{ marginTop: '0.25rem', marginBottom: 0 }}
                                aria-label={`Artikal ${i + 1}`}
                            >
                                {artikalOptions}
                            </select>
                        </div>

                        <div style={{ flex: '0 1 100px' }}>
                            <label className="field-label" style={{ fontSize: '0.875rem' }}>Količina</label>
                            <input
                                type="number"
                                min={1}
                                value={s.kolicina}
                                onChange={(e) => updateStavka(i, { kolicina: Number(e.target.value) })}
                                className="input-big"
                                style={{ marginTop: '0.25rem', marginBottom: 0 }}
                                aria-label={`Količina ${i + 1}`}
                            />
                        </div>

                        <div style={{ flex: '0 1 120px' }}>
                            <label className="field-label" style={{ fontSize: '0.875rem' }}>Cena (RSD)</label>
                            <input
                                type="number"
                                min={0}
                                value={s.cena}
                                onChange={(e) => updateStavka(i, { cena: Number(e.target.value) })}
                                className="input-big"
                                style={{ marginTop: '0.25rem', marginBottom: 0 }}
                                aria-label={`Cena ${i + 1}`}
                            />
                        </div>

                        <div style={{ flex: '0 0 auto', paddingTop: '1.75rem' }}>
                            <button
                                type="button"
                                className="button-big"
                                onClick={() => removeStavka(i)}
                                style={{ 
                                    background: '#dc2626', 
                                    width: 'auto', 
                                    padding: '10px 16px',
                                    marginTop: 0
                                }}
                            >
                                Ukloni
                            </button>
                        </div>
                    </div>
                ))}

                <button 
                    type="button" 
                    className="button-big" 
                    onClick={addStavka}
                    style={{ 
                        background: '#059669', 
                        maxWidth: '200px',
                        marginTop: '1rem'
                    }}
                >
                    + Dodaj stavku
                </button>
            </div>

            <div style={{ 
                borderTop: '2px solid #e5e7eb', 
                paddingTop: '1rem', 
                marginBottom: '1rem',
                fontSize: '1.25rem',
                fontWeight: 600
            }}>
                Ukupno: {safeNumber(ukupno, 0).toFixed(2)} RSD
            </div>

            <button 
                type="button" 
                className="button-big" 
                onClick={handleSubmit} 
                disabled={isSubmitting}
                style={{ maxWidth: '300px' }}
            >
                {isSubmitting ? "Kreiram..." : "💰 Sačuvaj prodaju"}
            </button>

            {error && <p className="error-msg" style={{ marginTop: '1rem' }}>{error}</p>}
        </div>
    );
}
