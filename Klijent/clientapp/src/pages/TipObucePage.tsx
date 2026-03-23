import { useState } from "react";
import { createTipObuce } from "../services/tipoviObuceApi";

export default function TipObucePage() {
    const [naziv, setNaziv] = useState("");
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [success, setSuccess] = useState<string | null>(null);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        
        if (!naziv.trim()) {
            setError("Naziv je obavezan.");
            return;
        }

        setLoading(true);
        setError(null);
        setSuccess(null);

        try {
            await createTipObuce(naziv.trim());
            setSuccess(`Tip obuće "${naziv}" uspešno kreiran! ✔️`);
            setNaziv("");
        } catch (err) {
            console.error(err);
            setError((err as Error)?.message ?? "Greška pri kreiranju tipa obuće.");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="card" style={{ maxWidth: "600px" }}>
            <h2 className="text-2xl font-semibold mb-6">👟 Kreiraj tip obuće</h2>

            <form onSubmit={handleSubmit}>
                <div style={{ marginBottom: "1.5rem" }}>
                    <label className="field-label">Naziv tipa obuće</label>
                    <input
                        type="text"
                        className="input-big"
                        placeholder="npr. Patike, Čizme, Sandale..."
                        value={naziv}
                        onChange={(e) => setNaziv(e.target.value)}
                        disabled={loading}
                    />
                </div>

                <button
                    type="submit"
                    className="button-big"
                    disabled={loading}
                    style={{ maxWidth: "300px" }}
                >
                    {loading ? "Kreiram..." : "➕ Kreiraj tip obuće"}
                </button>

                {error && (
                    <p className="error-msg" style={{ marginTop: "1rem" }}>
                        {error}
                    </p>
                )}

                {success && (
                    <p className="success-msg" style={{ marginTop: "1rem" }}>
                        {success}
                    </p>
                )}
            </form>

            <div style={{ marginTop: "2rem", padding: "1rem", background: "var(--surface-light)", borderRadius: "8px" }}>
                <h3 style={{ fontSize: "1rem", fontWeight: 600, marginBottom: "0.5rem" }}>
                    ℹ️ Napomena
                </h3>
                <p className="text-sm text-muted">
                    Tipovi obuće se koriste za kategorizaciju artikala (npr. Patike, čizme, Sandale). 
                    Nakon kreiranja, tip obuće će biti dostupan u dropdown-u na formi za kreiranje artikala.
                </p>
            </div>
        </div>
    );
}
