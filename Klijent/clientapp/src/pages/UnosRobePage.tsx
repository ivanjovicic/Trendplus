import React, { useEffect, useState } from "react";
import { ClipboardPlus } from "lucide-react";
import UnosRobeForm from "../components/UnosRobeForm";
import GoodsReceiptHistoryPanel from "../components/GoodsReceiptHistoryPanel";
import SefXmlReceiptDraftWizard from "../components/sef-xml-pilot/SefXmlReceiptDraftWizard";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel, InventoryState } from "../components/inventory/InventoryPageShell";
import { apiUrl } from "../utils/apiUrl";

interface Dobavljac {
    id: number;
    naziv: string;
    adresa?: string;
    telefon?: string;
}

export default function UnosRobePage() {
    const [entryMode, setEntryMode] = useState<"manual" | "xml">("manual");
    const [dobavljaci, setDobavljaci] = useState<Dobavljac[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const requestSefXmlAdminKey = () => {
        if (typeof window.prompt !== "function") return null;
        return window.prompt("Unesite admin ključ za read-only SEF XML pregled.")?.trim() || null;
    };
    useEffect(() => {
        let aborted = false;
        const controller = new AbortController();

        const fetchDobavljaci = async () => {
            try {
                const response = await fetch(apiUrl("/api/dobavljaci"), { signal: controller.signal });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const data = await response.json();
                if (!aborted) {
                    setDobavljaci(data ?? []);
                    setError(null);
                }
            } catch (e: unknown) {
                if ((e as { name?: string })?.name === "AbortError") return;
                console.error("Error fetching dobavljaci:", e);
                if (!aborted) {
                    setError("Greška pri učitavanju dobavljača.");
                }
            } finally {
                if (!aborted) {
                    setLoading(false);
                }
            }
        };

        void fetchDobavljaci();

        return () => {
            aborted = true;
            controller.abort();
        };
    }, []);

    return (
        <InventoryPageShell
            icon={ClipboardPlus}
            title="Unos robe"
            subtitle="Izaberite ručni unos ili odvojeni read-only SEF XML pregled i nacrt."
        >
            <InventoryKpiRow
                items={[
                    { label: "Dobavljači", value: `${dobavljaci.length}` },
                    { label: "Status", value: loading ? "Učitavanje" : "Spremno", tone: loading ? "warning" : "positive" },
                    { label: "Greške", value: error ? "1" : "0", tone: error ? "danger" : "positive" },
                    { label: "Tok", value: entryMode === "xml" ? "SEF XML → pregled → NEKNJIŽENO" : "Zaglavlje → Stavke → Potvrda" },
                ]}
            />

            <InventoryPanel>
                <div className="mb-4 flex flex-wrap gap-2" role="group" aria-label="Način unosa robe">
                    <button
                        className={`btn ${entryMode === "manual" ? "btn--primary" : "btn--secondary"}`}
                        type="button"
                        aria-pressed={entryMode === "manual"}
                        onClick={() => setEntryMode("manual")}
                    >
                        Ručni unos
                    </button>
                    <button
                        className={`btn ${entryMode === "xml" ? "btn--primary" : "btn--secondary"}`}
                        type="button"
                        aria-pressed={entryMode === "xml"}
                        onClick={() => setEntryMode("xml")}
                    >
                        Uvezi iz XML-a (pilot)
                    </button>
                </div>

                {entryMode === "xml" ? (
                    <SefXmlReceiptDraftWizard requestAdminKey={requestSefXmlAdminKey} />
                ) : (
                    <>
                        <div className="mb-4 grid gap-3 md:grid-cols-3">
                            <article className="rounded-xl border border-border bg-surface px-3 py-2">
                                <p className="text-[11px] uppercase tracking-wide text-muted">Korak 1</p>
                                <p className="mt-1 text-sm font-semibold text-foreground">Unesite račun ili otpremnicu</p>
                            </article>
                            <article className="rounded-xl border border-border bg-surface px-3 py-2">
                                <p className="text-[11px] uppercase tracking-wide text-muted">Korak 2</p>
                                <p className="mt-1 text-sm font-semibold text-foreground">Izaberite dobavljača i lokaciju</p>
                            </article>
                            <article className="rounded-xl border border-border bg-surface px-3 py-2">
                                <p className="text-[11px] uppercase tracking-wide text-muted">Korak 3</p>
                                <p className="mt-1 text-sm font-semibold text-foreground">Pregledajte stavke pre potvrde</p>
                            </article>
                        </div>

                        {loading && <InventoryState message="Učitavanje dobavljača..." tone="warning" />}
                        {!loading && error && <InventoryState message={error} tone="danger" />}
                        {!loading && !error && dobavljaci.length === 0 && (
                            <InventoryState
                                message="Nema dostupnih dobavljača. Dodajte dobavljača pre unosa robe."
                                tone="warning"
                            />
                        )}
                        {!loading && !error && dobavljaci.length > 0 && <UnosRobeForm dobavljaci={dobavljaci} />}
                    </>
                )}
            </InventoryPanel>
            {entryMode === "manual" && (
                <InventoryPanel>
                    <GoodsReceiptHistoryPanel />
                </InventoryPanel>
            )}
        </InventoryPageShell>
    );
}
