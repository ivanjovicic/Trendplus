import React, { useState, useEffect } from "react";
import UnosRobeForm from "../components/UnosRobeForm";

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
        alert("Unos robe - coming soon!");
    };

    if (loading) {
        return (
            <div className="card">
                <p style={{ textAlign: 'center', padding: '2rem' }}>Učitavanje dobavljača...</p>
            </div>
        );
    }

    if (error) {
        return (
            <div className="card">
                <p style={{ textAlign: 'center', padding: '2rem', color: '#dc2626' }}>
                    {error}
                </p>
            </div>
        );
    }

    if (dobavljaci.length === 0) {
        return (
            <div className="card">
                <p style={{ textAlign: 'center', padding: '2rem', color: '#dc2626' }}>
                    Nema dostupnih dobavljača. Molimo kreirajte dobavljače pre unosa robe.
                </p>
            </div>
        );
    }

    return <UnosRobeForm dobavljaci={dobavljaci} onSubmit={handleSubmit} />;
}
