import { useState, useMemo, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";

interface Dobavljac {
    id: number;
    naziv: string;
    adresa?: string;
    telefon?: string;
}

interface UnosRobeFormProps {
    dobavljaci: Dobavljac[];
    onSubmit: (data: { dobavljacId: number; brojRacuna: string; artikli: unknown[] }) => Promise<void>;
}

export default function UnosRobeForm({ dobavljaci }: UnosRobeFormProps) {
    const navigate = useNavigate();
    const [selectedDobavljac, setSelectedDobavljac] = useState<Dobavljac | null>(null);
    const [brojRacuna, setBrojRacuna] = useState("");
    const [searchQuery, setSearchQuery] = useState("");
    const [showSearchResults, setShowSearchResults] = useState(false);
    const [selectedIndex, setSelectedIndex] = useState(0);
    const searchRef = useRef<HTMLDivElement>(null);

    const filteredDobavljaci = useMemo(() => {
        if (!searchQuery.trim()) return dobavljaci;

        const query = searchQuery.toLowerCase();
        return dobavljaci
            .filter(d =>
                d.naziv.toLowerCase().includes(query) ||
                d.adresa?.toLowerCase().includes(query) ||
                d.telefon?.toLowerCase().includes(query)
            )
            .slice(0, 10);
    }, [dobavljaci, searchQuery]);

    useEffect(() => {
        setSelectedIndex(0);
    }, [filteredDobavljaci.length]);

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (searchRef.current && !searchRef.current.contains(event.target as Node)) {
                setShowSearchResults(false);
            }
        };

        document.addEventListener("mousedown", handleClickOutside);
        return () => document.removeEventListener("mousedown", handleClickOutside);
    }, []);

    const handleSelectDobavljac = (dobavljac: Dobavljac) => {
        setSelectedDobavljac(dobavljac);
        setSearchQuery(dobavljac.naziv);
        setShowSearchResults(false);
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (!showSearchResults || filteredDobavljaci.length === 0) return;

        switch (e.key) {
            case "ArrowDown":
                e.preventDefault();
                setSelectedIndex((prev) => (prev + 1) % filteredDobavljaci.length);
                break;
            case "ArrowUp":
                e.preventDefault();
                setSelectedIndex((prev) => (prev - 1 + filteredDobavljaci.length) % filteredDobavljaci.length);
                break;
            case "Enter":
                e.preventDefault();
                if (filteredDobavljaci[selectedIndex]) {
                    handleSelectDobavljac(filteredDobavljaci[selectedIndex]);
                }
                break;
            case "Escape":
                setShowSearchResults(false);
                break;
        }
    };

    const handleUnosClick = () => {
        if (selectedDobavljac && brojRacuna) {
            navigate("/artikli", {
                state: {
                    dobavljacId: selectedDobavljac.id,
                    dobavljacNaziv: selectedDobavljac.naziv,
                    brojRacuna: brojRacuna,
                },
            });
        }
    };

    const canProceed = selectedDobavljac !== null && brojRacuna.trim() !== "";

    return (
        <div className="space-y-4">
            <section className="rounded-xl border border-[#2f323b] bg-[#14161d] p-4">
                <h2 className="mb-4 text-xl font-semibold text-[#f3f6ff]">Unos robe</h2>
                <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Broj računa *</label>
                <input
                    type="text"
                    placeholder="Unesite broj računa..."
                    value={brojRacuna}
                    onChange={(e) => setBrojRacuna(e.target.value)}
                    className="w-full rounded-xl border border-[#2f323b] bg-[#1a1b1f] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
                />
            </section>

            <section className="relative rounded-xl border border-[#2f323b] bg-[#14161d] p-4" ref={searchRef}>
                <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">
                    Pretraga dobavljača *
                </label>
                <p className="mb-2 text-xs text-[#8193b1]">Strelice gore/dole za navigaciju, Enter za izbor.</p>
                <input
                    type="text"
                    placeholder="Pretraži po nazivu, adresi ili telefonu..."
                    value={searchQuery}
                    onChange={(e) => {
                        setSearchQuery(e.target.value);
                        setShowSearchResults(true);
                        setSelectedDobavljac(null);
                    }}
                    onFocus={() => setShowSearchResults(true)}
                    onKeyDown={handleKeyDown}
                    className="w-full rounded-xl border border-[#2f323b] bg-[#1a1b1f] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
                />

                {showSearchResults && searchQuery.trim() && (
                    <div className="absolute left-4 right-4 top-[calc(100%-4px)] z-20 mt-2 max-h-80 overflow-y-auto rounded-xl border border-[#2f323b] bg-[#1a1b1f] shadow-xl">
                        {filteredDobavljaci.length > 0 ? (
                            filteredDobavljaci.map((dob, idx) => (
                                <button
                                    key={dob.id}
                                    type="button"
                                    onClick={() => handleSelectDobavljac(dob)}
                                    className={`block w-full border-b border-[#262a34] px-3 py-3 text-left transition ${idx === selectedIndex ? "bg-[#1f2d48]" : "hover:bg-[#1f2330]"}`}
                                    onMouseEnter={() => setSelectedIndex(idx)}
                                >
                                    <div className="font-semibold text-[#e7eeff]">{dob.naziv}</div>
                                    {dob.adresa && <div className="text-xs text-[#9aabc7]">Adresa: {dob.adresa}</div>}
                                    {dob.telefon && <div className="text-xs text-[#9aabc7]">Telefon: {dob.telefon}</div>}
                                    <div className="mt-1 text-[11px] text-[#7e8ea9]">ID: {dob.id}</div>
                                </button>
                            ))
                        ) : (
                            <div className="px-3 py-5 text-center text-sm text-[#9aabc7]">Nema rezultata za "{searchQuery}"</div>
                        )}
                    </div>
                )}
            </section>

            {selectedDobavljac && (
                <section className="rounded-xl border border-emerald-700 bg-emerald-950/30 p-4">
                    <div className="flex items-start justify-between gap-3">
                        <div>
                            <h3 className="text-sm font-semibold uppercase tracking-wide text-emerald-300">Izabrani dobavljač</h3>
                            <p className="mt-1 text-base font-semibold text-[#f3f6ff]">{selectedDobavljac.naziv}</p>
                            {selectedDobavljac.adresa && <p className="text-sm text-emerald-200">Adresa: {selectedDobavljac.adresa}</p>}
                            {selectedDobavljac.telefon && <p className="text-sm text-emerald-200">Telefon: {selectedDobavljac.telefon}</p>}
                        </div>
                        <button
                            type="button"
                            onClick={() => {
                                setSelectedDobavljac(null);
                                setSearchQuery("");
                            }}
                            className="rounded-lg border border-rose-700 bg-rose-900/40 px-3 py-1.5 text-xs font-semibold text-rose-200"
                        >
                            Promeni
                        </button>
                    </div>
                </section>
            )}

            {!selectedDobavljac && !searchQuery && (
                <section className="rounded-xl border border-[#2f323b] bg-[#14161d] p-4">
                    <h3 className="mb-3 text-sm font-semibold uppercase tracking-wide text-[#93a7c8]">Lista svih dobavljača</h3>
                    <div className="grid max-h-80 gap-2 overflow-y-auto sm:grid-cols-2">
                        {dobavljaci.map((dob) => (
                            <button
                                key={dob.id}
                                type="button"
                                onClick={() => handleSelectDobavljac(dob)}
                                className="rounded-lg border border-[#2f323b] bg-[#1a1b1f] px-3 py-2 text-left transition hover:border-[#4f8cff] hover:bg-[#1f2d48]"
                            >
                                <div className="font-medium text-[#e7eeff]">{dob.naziv}</div>
                                {dob.adresa && <div className="text-xs text-[#9aabc7]">Adresa: {dob.adresa}</div>}
                                {dob.telefon && <div className="text-xs text-[#9aabc7]">Telefon: {dob.telefon}</div>}
                            </button>
                        ))}
                    </div>
                </section>
            )}

            <section className={`rounded-xl border p-4 ${canProceed ? "border-emerald-700 bg-emerald-950/20" : "border-[#2f323b] bg-[#14161d]"}`}>
                <h3 className="mb-3 text-sm font-semibold uppercase tracking-wide text-[#93a7c8]">Pregled unosa</h3>
                <div className="space-y-1 text-sm text-[#dbe6fb]">
                    <p><span className="text-[#93a7c8]">Broj računa:</span> {brojRacuna || "[Nije unet]"}</p>
                    <p><span className="text-[#93a7c8]">Dobavljač:</span> {selectedDobavljac?.naziv || "[Nije izabran]"}</p>
                </div>
                <button
                    onClick={handleUnosClick}
                    disabled={!canProceed}
                    className="mt-4 rounded-xl border border-[#3760b7] bg-[#2d4f95] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[#3760b7] disabled:cursor-not-allowed disabled:opacity-50"
                >
                    {canProceed ? "Nastavi na unos artikala" : "Popunite sva polja"}
                </button>
            </section>
        </div>
    );
}
