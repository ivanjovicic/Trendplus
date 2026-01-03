import { useState, useEffect } from "react";
import { createDobavljac } from "../services/dobavljaciApi";

interface Dobavljac {
    id: number;
    naziv: string;
    adresa?: string;
    telefon?: string;
    napomena?: string;
}

export default function DobavljaciPage() {
    const [dobavljaci, setDobavljaci] = useState<Dobavljac[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState<string | null>(null);

    const [naziv, setNaziv] = useState("");
    const [adresa, setAdresa] = useState("");
    const [telefon, setTelefon] = useState("");
    const [napomena, setNapomena] = useState("");
    const [isSaving, setIsSaving] = useState(false);

    const API = import.meta.env.VITE_API_BASE_URL;

    const loadDobavljaci = async () => {
        setLoading(true);
        setError(null);
        try {
            const res = await fetch(`${API}/api/dobavljaci`);
            if (!res.ok) throw new Error("Ne mogu da dohvatim dobavljače");
            const data = await res.json();
            setDobavljaci(data);
        } catch (e: any) {
            setError(e?.message ?? "Greška pri učitavanju dobavljača");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        loadDobavljaci();
    }, []);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        if (!naziv.trim()) {
            setError("Naziv je obavezan.");
            return;
        }

        setIsSaving(true);
        setError(null);
        setSuccess(null);

        try {
            await createDobavljac(naziv.trim(), adresa.trim() || undefined, telefon.trim() || undefined, napomena.trim() || undefined);
            setSuccess(`Dobavljač "${naziv}" uspešno kreiran! ✔️`);
            setNaziv("");
            setAdresa("");
            setTelefon("");
            setNapomena("");
            await loadDobavljaci();
        } catch (err) {
            console.error(err);
            setError((err as Error)?.message ?? "Greška pri kreiranju dobavljača.");
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <div className="card" style={{ maxWidth: "900px" }}>
            <h2 className="text-2xl font-semibold mb-6">🏢 Dobavljači</h2>

            {error && <p className="error-msg">{error}</p>}
            {success && <p style={{ marginTop: "0.75rem", color: "#059669", fontWeight: 600 }}>{success}</p>}

            <form onSubmit={handleSubmit} style={{ marginBottom: "2rem" }}>
                <div style={{ display: "grid", gridTemplateColumns: "1fr", gap: "1rem", marginBottom: "1rem" }}>
                    <div>
                        <label className="field-label">Naziv dobavljača *</label>
                        <input
                            type="text"
                            className="input-big"
                            placeholder="npr. ABC Company"
                            value={naziv}
                            onChange={(e) => setNaziv(e.target.value)}
                            disabled={isSaving}
                            required
                        />
                    </div>

                    <div>
                        <label className="field-label">Adresa (opciono)</label>
                        <input
                            type="text"
                            className="input-big"
                            placeholder="npr. Kneza Miloša 10, Beograd"
                            value={adresa}
                            onChange={(e) => setAdresa(e.target.value)}
                            disabled={isSaving}
                        />
                    </div>

                    <div>
                        <label className="field-label">Telefon (opciono)</label>
                        <input
                            type="text"
                            className="input-big"
                            placeholder="npr. +381 11 1234567"
                            value={telefon}
                            onChange={(e) => setTelefon(e.target.value)}
                            disabled={isSaving}
                        />
                    </div>

                    <div>
                        <label className="field-label">Napomena (opciono)</label>
                        <textarea
                            className="input-big"
                            placeholder="Dodatne napomene o dobavljaču..."
                            value={napomena}
                            onChange={(e) => setNapomena(e.target.value)}
                            disabled={isSaving}
                            rows={3}
                        />
                    </div>
                </div>

                <button
                    type="submit"
                    className="button-big"
                    disabled={isSaving}
                    style={{ maxWidth: "300px" }}
                >
                    {isSaving ? "Kreiram..." : "➕ Kreiraj dobavljača"}
                </button>
            </form>

            <h3 className="text-xl font-semibold mb-4">Postojeći dobavljači</h3>

            {loading && <p style={{ textAlign: "center", padding: "2rem" }}>Učitavanje...</p>}

            {!loading && dobavljaci.length === 0 && (
                <p style={{ textAlign: "center", padding: "2rem", color: "#6b7280" }}>Nema kreiranih dobavljača.</p>
            )}

            {!loading && dobavljaci.length > 0 && (
                <div style={{ overflowX: "auto" }}>
                    <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                        <thead>
                            <tr style={{ background: "#f3f4f6", borderBottom: "2px solid #e5e7eb" }}>
                                <th style={{ padding: 12, textAlign: "left", fontWeight: 600 }}>Naziv</th>
                                <th style={{ padding: 12, textAlign: "left", fontWeight: 600 }}>Adresa</th>
                                <th style={{ padding: 12, textAlign: "left", fontWeight: 600 }}>Telefon</th>
                                <th style={{ padding: 12, textAlign: "left", fontWeight: 600 }}>Napomena</th>
                            </tr>
                        </thead>
                        <tbody>
                            {dobavljaci.map((d) => (
                                <tr key={d.id} style={{ borderBottom: "1px solid #e5e7eb" }}>
                                    <td style={{ padding: 12, fontWeight: 600 }}>{d.naziv}</td>
                                    <td style={{ padding: 12, color: "#6b7280" }}>{d.adresa || "-"}</td>
                                    <td style={{ padding: 12, color: "#6b7280" }}>{d.telefon || "-"}</td>
                                    <td style={{ padding: 12, color: "#6b7280" }}>{d.napomena || "-"}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            <div style={{ marginTop: "2rem", padding: "1rem", background: "#f9fafb", borderRadius: "8px" }}>
                <h3 style={{ fontSize: "1rem", fontWeight: 600, marginBottom: "0.5rem" }}>
                    ℹ️ Napomena
                </h3>
                <p style={{ fontSize: "0.875rem", color: "#6b7280" }}>
                    Dobavljači se koriste za označavanje izvora nabavke robe. Nakon kreiranja, dobavljač će biti dostupan 
                    u dropdown-u na formi za unos robe.
                </p>
            </div>
        </div>
    );
}
