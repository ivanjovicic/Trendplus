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
            setValidationMessage("Izaberite dobavljača pre nastavka.");
            return;
        }

        if (invoice.length < 3) {
            setValidationMessage("Broj računa mora imati najmanje 3 karaktera.");
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
                <div className="form-progress">
                    <div className={`form-step ${invoiceReady ? "form-step--done" : "form-step--pending"}`}>
                        {invoiceReady ? <CheckCircle2 size={14} /> : <Circle size={14} />}
                        <span className="form-step-label">Broj računa</span>
                    </div>
                    <div className={`form-step ${supplierReady ? "form-step--done" : "form-step--pending"}`}>
                        {supplierReady ? <CheckCircle2 size={14} /> : <Circle size={14} />}
                        <span className="form-step-label">Dobavljač</span>
                    </div>
                    <div className={`form-step ${canProceed ? "form-step--done" : "form-step--pending"}`}>
                        {canProceed ? <CheckCircle2 size={14} /> : <Clock3 size={14} />}
                        <span className="form-step-label">Nastavak na stavke</span>
                    </div>
                </div>
            </section>

            <section className="rounded-xl border border-border bg-surface p-4">
                <label className="form-field-label">Broj računa <span className="form-required">*</span></label>
                <input
                    type="text"
                    placeholder="Npr. PR-2026-001"
                    value={brojRacuna}
                    onChange={(event) => {
                        setBrojRacuna(event.target.value);
                        setValidationMessage(null);
                    }}
                    className="form-input"
                />
                <div className="mt-2 flex flex-wrap gap-2">
                    {invoiceSuggestions.map((suggestion) => (
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
                <p className="form-helper">Tip: Ctrl+Enter nastavlja čim su polja validna.</p>
            </section>

            {recentDobavljaci.length > 0 && (
                <section className="rounded-xl border border-border bg-surface p-4">
                    <div className="mb-2 flex items-center justify-between gap-2">
                        <h3 className="text-sm font-semibold text-foreground">Poslednje korišćeni dobavljači</h3>
                        <button
                            type="button"
                            onClick={() => {
                                setRecentDobavljaci([]);
                                saveRecentDobavljaci([]);
                            }}
                            className="form-btn-secondary"
                        >
                            Očisti listu
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
                                className="form-recent-chip"
                            >
                                <div className="form-recent-chip-name">{recent.naziv}</div>
                                {recent.lastInvoice ? <div className="form-recent-chip-meta">Račun: {recent.lastInvoice}</div> : null}
                                <div className="form-recent-chip-meta">Korišćen: {formatUsedAt(recent.lastUsedAt)}</div>
                            </button>
                        ))}
                    </div>
                </section>
            )}

            <section className="relative rounded-xl border border-border bg-surface p-4" ref={searchRef}>
                <label className="form-field-label">Pretraga dobavljača <span className="form-required">*</span></label>
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
                        className="w-full rounded-xl border border-border bg-surface-elevated py-2 pl-9 pr-3 text-sm text-foreground outline-none transition focus:border-primary focus:shadow-[0_0_0_3px_var(--accent-primary-10,rgba(59,130,246,0.18))] shadow-sm placeholder:text-muted"
                    />
                </div>

                {showSearchResults && searchQuery.trim() && (
                    <div className="form-search-results absolute left-4 right-4 top-[calc(100%-4px)] z-20 mt-2">
                        {filteredDobavljaci.length > 0 ? (
                            filteredDobavljaci.map((dobavljac, index) => (
                                <button
                                    key={dobavljac.id}
                                    type="button"
                                    onClick={() => handleSelectDobavljac(dobavljac)}
                                    onMouseEnter={() => setSelectedIndex(index)}
                                    className={`form-search-item ${index === selectedIndex ? "form-search-item--active" : ""}`}
                                >
                                    <div>
                                        <div className="form-search-item-name">{dobavljac.naziv}</div>
                                        {dobavljac.adresa ? <div className="form-search-item-meta">Adresa: {dobavljac.adresa}</div> : null}
                                        {dobavljac.telefon ? <div className="form-search-item-meta">Telefon: {dobavljac.telefon}</div> : null}
                                    </div>
                                </button>
                            ))
                        ) : (
                            <div className="form-search-empty">Nema rezultata za „{searchQuery}“</div>
                        )}
                    </div>
                )}
            </section>

            {selectedDobavljac ? (
                <section className="form-selected-entity">
                    <div className="flex items-start justify-between gap-3">
                        <div>
                            <p className="form-selected-entity-label">Izabrani dobavljač</p>
                            <p className="form-selected-entity-name">{selectedDobavljac.naziv}</p>
                            {selectedDobavljac.adresa ? <p className="form-selected-entity-meta">Adresa: {selectedDobavljac.adresa}</p> : null}
                            {selectedDobavljac.telefon ? <p className="form-selected-entity-meta">Telefon: {selectedDobavljac.telefon}</p> : null}
                        </div>
                        <button
                            type="button"
                            onClick={() => {
                                setSelectedDobavljac(null);
                                setSearchQuery("");
                            }}
                            className="form-btn-danger inline-flex items-center gap-1"
                        >
                            <X size={12} /> Promeni
                        </button>
                    </div>
                </section>
            ) : null}

            <section className={`form-overview ${canProceed ? "form-overview--ready" : "form-overview--pending"}`}>
                <h3 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted">Pregled unosa</h3>
                <div className="space-y-1 text-sm text-foreground">
                    <p><span className="text-muted">Broj računa:</span> {normalizeInvoice(brojRacuna) || "[Nije unet]"}</p>
                    <p><span className="text-muted">Dobavljač:</span> {selectedDobavljac?.naziv || "[Nije izabran]"}</p>
                </div>

                {validationMessage ? (
                    <div className="mt-3 form-error text-xs">
                        {validationMessage}
                    </div>
                ) : null}

                <button
                    onClick={handleContinue}
                    disabled={!canProceed}
                    className={`mt-4 form-btn-continue ${canProceed ? "form-btn-continue--ready" : ""}`}
                >
                    Nastavi na unos artikala <ArrowRight size={14} />
                </button>
            </section>
        </div>
    );
}
