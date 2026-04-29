import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
    EntitySearchCombobox,
    FormLayout,
    FormProgress,
    FormSection,
    FormField,
    ReadonlyField,
    StickyActionBar,
    SummaryPanel,
    ValidationChecklist,
    type EntitySearchItem,
} from "./forms/FormSystem";

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
        return parsed.filter((item) => item && typeof item.id === "number" && typeof item.naziv === "string").slice(0, 8);
    } catch {
        return [];
    }
}

function saveRecentDobavljaci(items: RecentDobavljac[]) {
    try {
        localStorage.setItem(RECENT_DOBAVLJACI_KEY, JSON.stringify(items.slice(0, 8)));
    } catch {
        // local storage is best-effort only
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
    const [recentDobavljaci, setRecentDobavljaci] = useState<RecentDobavljac[]>([]);
    const [validationMessage, setValidationMessage] = useState<string | null>(null);

    useEffect(() => {
        setRecentDobavljaci(readRecentDobavljaci());
    }, []);

    const invoice = normalizeInvoice(brojRacuna);
    const invoiceReady = invoice.length >= 3;
    const supplierReady = selectedDobavljac !== null;
    const canProceed = invoiceReady && supplierReady;
    const disabledReason = !invoiceReady
        ? "Unesite broj računa od najmanje 3 karaktera."
        : !supplierReady
            ? "Izaberite dobavljača."
            : undefined;

    const filteredDobavljaci = useMemo(() => {
        const query = searchQuery.trim().toLowerCase();
        if (!query) return dobavljaci.slice(0, 20);
        return dobavljaci
            .filter((dobavljac) =>
                dobavljac.naziv.toLowerCase().includes(query) ||
                dobavljac.adresa?.toLowerCase().includes(query) ||
                dobavljac.telefon?.toLowerCase().includes(query)
            )
            .slice(0, 12);
    }, [dobavljaci, searchQuery]);

    const searchItems = useMemo<EntitySearchItem[]>(
        () =>
            filteredDobavljaci.map((dobavljac) => ({
                id: dobavljac.id,
                title: dobavljac.naziv,
                meta: [dobavljac.adresa, dobavljac.telefon].filter(Boolean).join(" | "),
            })),
        [filteredDobavljaci]
    );

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

    const persistRecentSupplier = useCallback((dobavljac: Dobavljac, invoiceNumber: string) => {
        const nextItem: RecentDobavljac = {
            id: dobavljac.id,
            naziv: dobavljac.naziv,
            adresa: dobavljac.adresa,
            telefon: dobavljac.telefon,
            lastInvoice: invoiceNumber,
            lastUsedAt: new Date().toISOString(),
        };
        setRecentDobavljaci((current) => {
            const next = [nextItem, ...current.filter((item) => item.id !== dobavljac.id)].slice(0, 8);
            saveRecentDobavljaci(next);
            return next;
        });
    }, []);

    const handleSelectDobavljac = useCallback((dobavljac: Dobavljac) => {
        setSelectedDobavljac(dobavljac);
        setSearchQuery(dobavljac.naziv);
        setValidationMessage(null);
    }, []);

    const handleContinue = useCallback(() => {
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
    }, [invoice, navigate, persistRecentSupplier, selectedDobavljac]);

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

    return (
        <>
            <FormProgress
                steps={[
                    { label: "Broj računa", state: invoiceReady ? "complete" : "pending" },
                    { label: "Dobavljač", state: supplierReady ? "complete" : "pending" },
                    { label: "Nastavak na stavke", state: canProceed ? "complete" : "pending" },
                ]}
            />

            <FormLayout
                main={(
                    <>
                        <FormSection title="Račun" description="Unesite broj dokumenta za prijem robe." complete={invoiceReady}>
                            <FormField label="Broj računa" required helper="Ctrl+Enter nastavlja čim su polja validna.">
                                <input
                                    className="form-control"
                                    value={brojRacuna}
                                    placeholder="Npr. PR-2026-001"
                                    onChange={(event) => {
                                        setBrojRacuna(event.target.value);
                                        setValidationMessage(null);
                                    }}
                                />
                            </FormField>
                            {invoiceSuggestions.length > 0 ? (
                                <div className="line-row__header">
                                    {invoiceSuggestions.map((suggestion) => (
                                        <button key={suggestion} type="button" className="btn btn--secondary" onClick={() => setBrojRacuna(suggestion)}>
                                            {suggestion}
                                        </button>
                                    ))}
                                </div>
                            ) : null}
                        </FormSection>

                        {recentDobavljaci.length > 0 ? (
                            <FormSection
                                title="Brzi izbor"
                                description="Poslednje korišćeni dobavljači i računi."
                                complete={false}
                                actions={(
                                    <button
                                        type="button"
                                        className="btn btn--secondary"
                                        onClick={() => {
                                            setRecentDobavljaci([]);
                                            saveRecentDobavljaci([]);
                                        }}
                                    >
                                        Očisti
                                    </button>
                                )}
                            >
                                <div className="form-grid form-grid--two">
                                    {recentDobavljaci.map((recent) => (
                                        <button
                                            key={recent.id}
                                            type="button"
                                            className="btn btn--secondary"
                                            onClick={() => {
                                                const match = dobavljaci.find((item) => item.id === recent.id);
                                                if (match) handleSelectDobavljac(match);
                                                if (recent.lastInvoice && !brojRacuna.trim()) setBrojRacuna(recent.lastInvoice);
                                            }}
                                        >
                                            {recent.naziv} | {recent.lastInvoice ?? formatUsedAt(recent.lastUsedAt)}
                                        </button>
                                    ))}
                                </div>
                            </FormSection>
                        ) : null}

                        <FormSection title="Dobavljač" description="Pretražite po nazivu, adresi ili telefonu." complete={supplierReady}>
                            <EntitySearchCombobox
                                label="Pretraga dobavljača"
                                required
                                value={searchQuery}
                                placeholder="Naziv, adresa ili telefon..."
                                items={searchItems}
                                onQueryChange={(value) => {
                                    setSearchQuery(value);
                                    setSelectedDobavljac(null);
                                    setValidationMessage(null);
                                }}
                                onSelect={(item) => {
                                    const match = dobavljaci.find((dobavljac) => dobavljac.id === Number(item.id));
                                    if (match) handleSelectDobavljac(match);
                                }}
                            />
                        </FormSection>
                    </>
                )}
                aside={(
                    <SummaryPanel
                        title="Pregled prijema"
                        actions={(
                            <>
                                {validationMessage ? <p className="form-error">{validationMessage}</p> : null}
                                {disabledReason ? <p className="form-helper">{disabledReason}</p> : null}
                                <button type="button" className="btn btn--primary btn--full" disabled={!canProceed} onClick={handleContinue}>
                                    Nastavi na unos artikala
                                </button>
                            </>
                        )}
                    >
                        <ReadonlyField label="Broj računa" value={invoice || "Nije unet"} />
                        <ReadonlyField label="Dobavljač" value={selectedDobavljac?.naziv ?? "Nije izabran"} />
                        <ValidationChecklist
                            items={[
                                { label: "Broj računa je unet", valid: invoiceReady },
                                { label: "Dobavljač je izabran", valid: supplierReady },
                            ]}
                        />
                    </SummaryPanel>
                )}
            />

            <StickyActionBar
                primaryLabel="Nastavi na unos artikala"
                disabled={!canProceed}
                disabledReason={disabledReason}
                onPrimary={handleContinue}
            />
        </>
    );
}
