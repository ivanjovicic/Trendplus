import React, { useState, useEffect } from "react";
import { ClipboardPlus, AlertCircle, CheckCircle, X } from "lucide-react";
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
    const [submitResult, setSubmitResult] = useState<{ ok: boolean; msg: string } | null>(null);
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
                        setError("Greška pri učitavanju dobavljača");
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
        setSubmitResult({ ok: false, msg: "Unos robe — funkcionalnost je u pripremi." });
    };

    return (
        <InventoryPageShell
            icon={ClipboardPlus}
            title="Unos robe"
            subtitle="Prijem robe po dobavljaču sa kontrolom dostupnih partnera pre unosa."
        >
            <InventoryKpiRow
                items={[
                    { label: "Dobavljači", value: `${dobavljaci.length}` },
                    { label: "Status", value: loading ? "Učitavanje" : "Spremno", tone: loading ? "warning" : "positive" },
                    { label: "Greške", value: error ? "1" : "0", tone: error ? "danger" : "positive" },
                    { label: "Workflow", value: "Prijem robe" },
                ]}
            />

            <InventoryPanel>
                {submitResult && (
                    <div className={`mb-3 flex items-start gap-2 rounded-xl border px-3 py-2 text-sm ${
                        submitResult.ok
                            ? "border-[#14532d] bg-[#0d2118] text-[#4ade80]"
                            : "border-[#92400e] bg-[#2b1e08] text-[#fbbf24]"
                    }`}>
                        {submitResult.ok
                            ? <CheckCircle size={16} className="mt-0.5 shrink-0" />
                            : <AlertCircle size={16} className="mt-0.5 shrink-0" />}
                        <span className="flex-1">{submitResult.msg}</span>
                        <button type="button" onClick={() => setSubmitResult(null)} className="shrink-0 hover:text-white"><X size={14} /></button>
                    </div>
                )}
                {loading && <InventoryState message="Učitavanje dobavljača..." tone="warning" />}
                {!loading && error && <InventoryState message={error} tone="danger" />}
                {!loading && !error && dobavljaci.length === 0 && (
                    <InventoryState
                        message="Nema dostupnih dobavljača. Kreirajte dobavljača pre unosa robe."
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
