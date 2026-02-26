import React, { useState, useEffect } from "react";
import { ClipboardPlus } from "lucide-react";
import UnosRobeForm from "../components/UnosRobeForm";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel, InventoryState } from "../components/inventory/InventoryPageShell";

interface Dobavljac {
    id: number;
    naziv: string;
    adresa?: string;
    telefon?: string;
    napomena?: string;
}

export default function UnosRobePage() {
    const [dobavljaci, setDobavljaci] = useState<Dobavljac[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const API = import.meta.env.VITE_API_BASE_URL;

    useEffect(() => {
        let aborted = false;
        const controller = new AbortController();

        const fetchDobavljaci = async () => {
            try {
                const res = await fetch(`${API}/api/dobavljaci`, { signal: controller.signal });
                if (res.ok) {
                    const data = await res.json();
                    if (!aborted) {
                        setDobavljaci(data ?? []);
                        setLoading(false);
                    }
                } else {
                    console.error("Failed to fetch dobavljaci:", res.status, await res.text());
                    if (!aborted) {
                        setError("Greška pri ucitavanju dobavljaca");
                        setLoading(false);
                    }
                }
            } catch (e: unknown) {
                if ((e as { name?: string })?.name === "AbortError") return;
                console.error("Error fetching dobavljaci:", e);
                if (!aborted) {
                    setError("Greška pri povezivanju sa serverom");
                    setLoading(false);
                }
            }
        };

        fetchDobavljaci();

        return () => {
            aborted = true;
            controller.abort();
        };
    }, [API]);

    const handleSubmit = async (data: { dobavljacId: number; brojRacuna: string; artikli: unknown[] }): Promise<void> => {
        console.debug("Unos robe data:", data);
        alert("Unos robe - coming soon!");
    };

    return (
        <InventoryPageShell
            icon={ClipboardPlus}
            title="Unos robe"
            subtitle="Prijem robe po dobavljacu sa kontrolom dostupnih partnera pre unosa."
        >
            <InventoryKpiRow
                items={[
                    { label: "Dobavljaci", value: `${dobavljaci.length}` },
                    { label: "Status ucitavanja", value: loading ? "Ucitavanje" : "Spremno", tone: loading ? "warning" : "positive" },
                    { label: "Greške", value: error ? "1" : "0", tone: error ? "danger" : "positive" },
                    { label: "Workflow", value: "Prijem robe" },
                ]}
            />

            <InventoryPanel>
                {loading && <InventoryState message="Ucitavanje dobavljaca..." tone="warning" />}
                {!loading && error && <InventoryState message={error} tone="danger" />}
                {!loading && !error && dobavljaci.length === 0 && (
                    <InventoryState
                        message="Nema dostupnih dobavljaca. Kreirajte dobavljaca pre unosa robe."
                        tone="warning"
                    />
                )}
                {!loading && !error && dobavljaci.length > 0 && (
                    <UnosRobeForm dobavljaci={dobavljaci} onSubmit={handleSubmit} />
                )}
            </InventoryPanel>
        </InventoryPageShell>
    );
}
