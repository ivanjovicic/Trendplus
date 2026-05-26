import React, { useEffect, useState } from "react";
import { ClipboardPlus } from "lucide-react";
import UnosRobeForm from "../components/UnosRobeForm";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel, InventoryState } from "../components/inventory/InventoryPageShell";
import { apiUrl } from "../utils/apiUrl";

interface Dobavljac {
    id: number;
    naziv: string;
    adresa?: string;
    telefon?: string;
}

export default function UnosRobePage() {
    const [dobavljaci, setDobavljaci] = useState<Dobavljac[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
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
                    setError("Greska pri ucitavanju dobavljaca.");
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
            subtitle="Brzi prijem robe: racun + dobavljac + nastavak na stavke, sa fokusom na sto manje klikova."
        >
            <InventoryKpiRow
                items={[
                    { label: "Dobavljaci", value: `${dobavljaci.length}` },
                    { label: "Status", value: loading ? "Ucitavanje" : "Spremno", tone: loading ? "warning" : "positive" },
                    { label: "Greske", value: error ? "1" : "0", tone: error ? "danger" : "positive" },
                    { label: "Workflow", value: "Racun -> Dobavljac -> Stavke" },
                ]}
            />

            <InventoryPanel>
                <div className="mb-4 grid gap-3 md:grid-cols-3">
                    <article className="rounded-xl border border-border bg-surface px-3 py-2">
                        <p className="text-[11px] uppercase tracking-wide text-muted">Korak 1</p>
                        <p className="mt-1 text-sm font-semibold text-foreground">Unesite broj racuna</p>
                    </article>
                    <article className="rounded-xl border border-border bg-surface px-3 py-2">
                        <p className="text-[11px] uppercase tracking-wide text-muted">Korak 2</p>
                        <p className="mt-1 text-sm font-semibold text-foreground">Izaberite dobavljaca</p>
                    </article>
                    <article className="rounded-xl border border-border bg-surface px-3 py-2">
                        <p className="text-[11px] uppercase tracking-wide text-muted">Korak 3</p>
                        <p className="mt-1 text-sm font-semibold text-foreground">Ctrl+Enter za nastavak</p>
                    </article>
                </div>

                {loading && <InventoryState message="Ucitavanje dobavljaca..." tone="warning" />}
                {!loading && error && <InventoryState message={error} tone="danger" />}
                {!loading && !error && dobavljaci.length === 0 && (
                    <InventoryState
                        message="Nema dostupnih dobavljaca. Dodajte dobavljaca pre unosa robe."
                        tone="warning"
                    />
                )}
                {!loading && !error && dobavljaci.length > 0 && <UnosRobeForm dobavljaci={dobavljaci} />}
            </InventoryPanel>
        </InventoryPageShell>
    );
}
