import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ArrowRight, CheckCircle2, Circle, Clock3, Search, X } from "lucide-react";

interface Dobavljac {
    id: number;
    naziv: string;
    adresa?: string;
    telefon?: string;
}

interface UnosRobeFormProps {
    dobavljaci: Dobavljac[];
}

type RecentDobavljac = {
    id: number;
    naziv: string;
    adresa?: string;
    telefon?: string;
    lastInvoice?: string;
    lastUsedAt: string;
};

const RECENT_DOBAVLJACI_KEY = "trendplus.unosRobe.recentDobavljaci";

function readRecentDobavljaci(): RecentDobavljac[] {
    try {
        const raw = localStorage.getItem(RECENT_DOBAVLJACI_KEY);
        if (!raw) return [];
        const parsed = JSON.parse(raw) as RecentDobavljac[];
        if (!Array.isArray(parsed)) return [];
        return parsed
            .filter((x) => x && typeof x.id === "number" && typeof x.naziv === "string")
            .slice(0, 8);
    } catch {
        return [];
    }
}

function saveRecentDobavljaci(items: RecentDobavljac[]) {
    try {
        localStorage.setItem(RECENT_DOBAVLJACI_KEY, JSON.stringify(items.slice(0, 8)));
    } catch {
        // ignore storage errors
    }
}

function normalizeInvoice(input: string): string {
    return input.trim();
}

function buildNextInvoiceSuggestion(value: string): string | null {
    const trimmed = normalizeInvoice(value);
    if (!trimmed) return null;

    const match = trimmed.match(/^(.*?)(\d+)(\D*)$/);
    if (!match) return null;

    const [, prefix, digits, suffix] = match;
    const next = (Number(digits) + 1).toString().padStart(digits.length, "0");
    return `${prefix}${next}${suffix}`;
}

function formatUsedAt(value: string): string {
    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) return "";
    return parsed.toLocaleDateString("sr-RS");
}

export default function UnosRobeForm({ dobavljaci }: UnosRobeFormProps) {
    const navigate = useNavigate();

    const [selectedDobavljac, setSelectedDobavljac] = useState<Dobavljac | null>(null);
    const [brojRacuna, setBrojRacuna] = useState("");
    const [searchQuery, setSearchQuery] = useState("");
    const [showSearchResults, setShowSearchResults] = useState(false);
    const [selectedIndex, setSelectedIndex] = useState(0);
    const [recentDobavljaci, setRecentDobavljaci] = useState<RecentDobavljac[]>([]);
    const [validationMessage, setValidationMessage] = useState<string | null>(null);

    const searchRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        setRecentDobavljaci(readRecentDobavljaci());
    }, []);

    const filteredDobavljaci = useMemo(() => {
        const query = searchQuery.trim().toLowerCase();
        if (!query) return dobavljaci.slice(0, 50);

        return dobavljaci
            .filter((dobavljac) =>
                dobavljac.naziv.toLowerCase().includes(query) ||
                dobavljac.adresa?.toLowerCase().includes(query) ||
                dobavljac.telefon?.toLowerCase().includes(query)
            )
            .slice(0, 12);
    }, [dobavljaci, searchQuery]);

    const invoiceReady = normalizeInvoice(brojRacuna).length >= 3;
    const supplierReady = selectedDobavljac !== null;
    const canProceed = invoiceReady && supplierReady;

    const invoiceSuggestions = useMemo(() => {
        const unique = new Set<string>();

        const currentNext = buildNextInvoiceSuggestion(brojRacuna);
        if (currentNext) unique.add(currentNext);

        for (const recent of recentDobavljaci) {
            if (!recent.lastInvoice) continue;
            unique.add(recent.lastInvoice);
            const next = buildNextInvoiceSuggestion(recent.lastInvoice);
            if (next) unique.add(next);
        }

        return Array.from(unique).slice(0, 6);
    }, [brojRacuna, recentDobavljaci]);

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

    const persistRecentSupplier = useCallback((dobavljac: Dobavljac, invoice: string) => {
        const now = new Date().toISOString();
        const nextItem: RecentDobavljac = {
            id: dobavljac.id,
            naziv: dobavljac.naziv,
            adresa: dobavljac.adresa,
            telefon: dobavljac.telefon,
            lastInvoice: invoice,
            lastUsedAt: now,
        };

        const next = [nextItem, ...recentDobavljaci.filter((x) => x.id !== dobavljac.id)]
            .slice(0, 8);
        setRecentDobavljaci(next);
        saveRecentDobavljaci(next);
    }, [recentDobavljaci]);

    const handleContinue = useCallback(() => {
        const invoice = normalizeInvoice(brojRacuna);
        if (!selectedDobavljac) {
            setValidationMessage("Izaberite dobavljaca pre nastavka.");
            return;
        }

        if (invoice.length < 3) {
            setValidationMessage("Broj racuna mora imati najmanje 3 karaktera.");
            return;
        }

        persistRecentSupplier(selectedDobavljac, invoice);
        navigate("/artikli", {
            state: {
                dobavljacId: selectedDobavljac.id,
                dobavljacNaziv: selectedDobavljac.naziv,
                brojRacuna: invoice,
            },
        });
    }, [brojRacuna, navigate, persistRecentSupplier, selectedDobavljac]);

    useEffect(() => {
        const onWindowKeyDown = (event: KeyboardEvent) => {
            if ((event.ctrlKey || event.metaKey) && event.key === "Enter" && canProceed) {
                event.preventDefault();
                handleContinue();
            }
        };

        window.addEventListener("keydown", onWindowKeyDown);
        return () => window.removeEventListener("keydown", onWindowKeyDown);
    }, [canProceed, handleContinue]);

    const handleSelectDobavljac = (dobavljac: Dobavljac) => {
        setSelectedDobavljac(dobavljac);
        setSearchQuery(dobavljac.naziv);
        setShowSearchResults(false);
        setValidationMessage(null);
    };

    const handleKeyDown = (event: React.KeyboardEvent) => {
        if (!showSearchResults || filteredDobavljaci.length === 0) return;

        switch (event.key) {
            case "ArrowDown":
                event.preventDefault();
                setSelectedIndex((prev) => (prev + 1) % filteredDobavljaci.length);
                break;
            case "ArrowUp":
                event.preventDefault();
                setSelectedIndex((prev) => (prev - 1 + filteredDobavljaci.length) % filteredDobavljaci.length);
                break;
            case "Enter":
                event.preventDefault();
                if (filteredDobavljaci[selectedIndex]) {
                    handleSelectDobavljac(filteredDobavljaci[selectedIndex]);
                }
                break;
            case "Escape":
                setShowSearchResults(false);
                break;
        }
    };

    return (
        <div className="space-y-4">
            <section className="rounded-xl border border-border bg-surface p-4">
                <h2 className="mb-3 text-xl font-semibold text-foreground">Unos robe</h2>
                <div className="grid gap-2 md:grid-cols-3">
                    <div className={`rounded-lg border px-3 py-2 text-xs ${invoiceReady ? "border-emerald-700 bg-emerald-950/20 text-emerald-300" : "border-border bg-surface text-muted"}`}>
                        {invoiceReady ? <CheckCircle2 size={14} className="mb-1" /> : <Circle size={14} className="mb-1" />}
                        1) Broj racuna
                    </div>
                    <div className={`rounded-lg border px-3 py-2 text-xs ${supplierReady ? "border-emerald-700 bg-emerald-950/20 text-emerald-300" : "border-border bg-surface text-muted"}`}>
                        {supplierReady ? <CheckCircle2 size={14} className="mb-1" /> : <Circle size={14} className="mb-1" />}
                        2) Dobavljac
                    </div>
                    <div className={`rounded-lg border px-3 py-2 text-xs ${canProceed ? "border-emerald-700 bg-emerald-950/20 text-emerald-300" : "border-border bg-surface text-muted"}`}>
                        {canProceed ? <CheckCircle2 size={14} className="mb-1" /> : <Clock3 size={14} className="mb-1" />}
                        3) Nastavak na stavke
                    </div>
                </div>
            </section>

            <section className="rounded-xl border border-border bg-surface p-4">
                <label className="mb-1 block text-xs uppercase tracking-wide text-muted">Broj racuna *</label>
                <input
                    type="text"
                    placeholder="Npr. PR-2026-001"
                    value={brojRacuna}
                    onChange={(event) => {
                        setBrojRacuna(event.target.value);
                        setValidationMessage(null);
                    }}
                    className="w-full rounded-xl border border-border bg-surface px-3 py-2 text-sm text-foreground outline-none transition focus:border-primary"
                />
                <div className="mt-2 flex flex-wrap gap-2">
                    {invoiceSuggestions.map((suggestion) => (
                        <button
                            key={suggestion}
                            type="button"
                            onClick={() => setBrojRacuna(suggestion)}
                            className="rounded-full border border-border bg-surface px-2.5 py-1 text-[11px] text-muted hover:border-primary hover:text-foreground"
                        >
                            {suggestion}
                        </button>
                    ))}
                </div>
                <p className="mt-2 text-xs text-muted">Tip: `Ctrl+Enter` nastavlja cim su polja validna.</p>
            </section>

            {recentDobavljaci.length > 0 && (
                <section className="rounded-xl border border-border bg-surface p-4">
                    <div className="mb-2 flex items-center justify-between gap-2">
                        <h3 className="text-sm font-semibold text-foreground">Poslednje korisceni dobavljaci</h3>
                        <button
                            type="button"
                            onClick={() => {
                                setRecentDobavljaci([]);
                                saveRecentDobavljaci([]);
                            }}
                            className="rounded-lg border border-border bg-surface-elevated px-2.5 py-1 text-[11px] text-foreground hover:bg-surface-elevated"
                        >
                            Ocisti listu
                        </button>
                    </div>
                    <div className="flex flex-wrap gap-2">
                        {recentDobavljaci.map((recent) => (
                            <button
                                key={recent.id}
                                type="button"
                                onClick={() => {
                                    const match = dobavljaci.find((x) => x.id === recent.id);
                                    if (match) handleSelectDobavljac(match);
                                    if (recent.lastInvoice && !brojRacuna.trim()) setBrojRacuna(recent.lastInvoice);
                                }}
                                className="rounded-lg border border-border bg-surface px-3 py-1.5 text-left text-xs text-foreground hover:border-primary"
                            >
                                <div className="font-semibold">{recent.naziv}</div>
                                {recent.lastInvoice ? <div className="text-muted">Racun: {recent.lastInvoice}</div> : null}
                                <div className="text-[11px] text-muted">Koriscen: {formatUsedAt(recent.lastUsedAt)}</div>
                            </button>
                        ))}
                    </div>
                </section>
            )}

            <section className="relative rounded-xl border border-border bg-surface p-4" ref={searchRef}>
                <label className="mb-1 block text-xs uppercase tracking-wide text-muted">Pretraga dobavljaca *</label>
                <div className="relative">
                    <Search size={14} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted" />
                    <input
                        type="text"
                        placeholder="Naziv, adresa ili telefon..."
                        value={searchQuery}
                        onChange={(event) => {
                            setSearchQuery(event.target.value);
                            setShowSearchResults(true);
                            setSelectedDobavljac(null);
                            setValidationMessage(null);
                        }}
                        onFocus={() => setShowSearchResults(true)}
                        onKeyDown={handleKeyDown}
                        className="w-full rounded-xl border border-border bg-surface py-2 pl-9 pr-3 text-sm text-foreground outline-none transition focus:border-primary"
                    />
                </div>

                {showSearchResults && searchQuery.trim() && (
                    <div className="absolute left-4 right-4 top-[calc(100%-4px)] z-20 mt-2 max-h-80 overflow-y-auto rounded-xl border border-[#2f323b] bg-[#1a1b1f] shadow-xl">
                        {filteredDobavljaci.length > 0 ? (
                            filteredDobavljaci.map((dobavljac, index) => (
                                <button
                                    key={dobavljac.id}
                                    type="button"
                                    onClick={() => handleSelectDobavljac(dobavljac)}
                                    onMouseEnter={() => setSelectedIndex(index)}
                                    className={`block w-full border-b border-border px-3 py-3 text-left transition ${
                                        index === selectedIndex ? "bg-surface-elevated" : "hover:bg-surface-elevated"
                                    }`}
                                >
                                    <div className="font-semibold text-foreground">{dobavljac.naziv}</div>
                                    {dobavljac.adresa ? <div className="text-xs text-muted">Adresa: {dobavljac.adresa}</div> : null}
                                    {dobavljac.telefon ? <div className="text-xs text-muted">Telefon: {dobavljac.telefon}</div> : null}
                                </button>
                            ))
                        ) : (
                            <div className="px-3 py-5 text-center text-sm text-muted">Nema rezultata za "{searchQuery}"</div>
                        )}
                    </div>
                )}
            </section>

            {selectedDobavljac ? (
                <section className="rounded-xl border border-emerald-700 bg-emerald-950/30 p-4">
                    <div className="flex items-start justify-between gap-3">
                        <div>
                            <h3 className="text-sm font-semibold uppercase tracking-wide text-emerald-300">Izabrani dobavljac</h3>
                            <p className="mt-1 text-base font-semibold text-[#f3f6ff]">{selectedDobavljac.naziv}</p>
                            {selectedDobavljac.adresa ? <p className="text-sm text-emerald-200">Adresa: {selectedDobavljac.adresa}</p> : null}
                            {selectedDobavljac.telefon ? <p className="text-sm text-emerald-200">Telefon: {selectedDobavljac.telefon}</p> : null}
                        </div>
                        <button
                            type="button"
                            onClick={() => {
                                setSelectedDobavljac(null);
                                setSearchQuery("");
                            }}
                            className="inline-flex items-center gap-1 rounded-lg border border-rose-700 bg-rose-900/40 px-3 py-1.5 text-xs font-semibold text-rose-200"
                        >
                            <X size={12} /> Promeni
                        </button>
                    </div>
                </section>
            ) : null}

            <section className={`rounded-xl border p-4 ${canProceed ? "border-emerald-700 bg-emerald-950/20" : "border-[#2f323b] bg-[#14161d]"}`}>
                <h3 className="mb-3 text-sm font-semibold uppercase tracking-wide text-[#93a7c8]">Pregled unosa</h3>
                <div className="space-y-1 text-sm text-[#dbe6fb]">
                    <p><span className="text-[#93a7c8]">Broj racuna:</span> {normalizeInvoice(brojRacuna) || "[Nije unet]"}</p>
                    <p><span className="text-[#93a7c8]">Dobavljac:</span> {selectedDobavljac?.naziv || "[Nije izabran]"}</p>
                </div>

                {validationMessage ? (
                    <div className="mt-3 rounded-lg border border-[#7f1d1d] bg-[#2b0a0a] px-3 py-2 text-xs text-[#fda4af]">
                        {validationMessage}
                    </div>
                ) : null}

                <button
                    onClick={handleContinue}
                    disabled={!canProceed}
                    className="mt-4 inline-flex items-center gap-2 rounded-xl border border-[#3760b7] bg-[#2d4f95] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[#3760b7] disabled:cursor-not-allowed disabled:opacity-50"
                >
                    Nastavi na unos artikala <ArrowRight size={14} />
                </button>
            </section>
        </div>
    );
}
