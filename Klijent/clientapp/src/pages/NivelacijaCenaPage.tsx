import { useEffect, useMemo, useState } from "react";
import { getArtikli, nivelacijaCena } from "../services/artikliApi";
import { Artikal } from "../types/Artikal";
import { ArtikalFormData } from "../types/artikalformdata";

export default function NivelacijaCenaPage() {
    const [artikli, setArtikli] = useState<Artikal[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const [query, setQuery] = useState("");
    const [selected, setSelected] = useState<Artikal | null>(null);

    const [novaProdajnaCena, setNovaProdajnaCena] = useState<number>(0);
    const [novaNabavnaCena, setNovaNabavnaCena] = useState<number | null>(null);
    const [novaPrvaProdajnaCena, setNovaPrvaProdajnaCena] = useState<number | null>(null);

    const [isSaving, setIsSaving] = useState(false);
    const [success, setSuccess] = useState<string | null>(null);

    useEffect(() => {
        const load = async () => {
            setLoading(true);
            setError(null);
            try {
                const data = await getArtikli();
                setArtikli(data);
            } catch (e: any) {
                setError(e?.message ?? "Greška pri učitavanju artikala");
            } finally {
                setLoading(false);
            }
        };

        load();
    }, []);

    const filtered = useMemo(() => {
        const q = query.trim().toLowerCase();
        if (!q) return [];
        return artikli
            .filter(a => a.naziv.toLowerCase().includes(q))
            .slice(0, 10);
    }, [artikli, query]);

    const selectArtikal = (a: Artikal) => {
        setSelected(a);
        setQuery(a.naziv);
        setNovaProdajnaCena(a.prodajnaCena ?? 0);
        setNovaNabavnaCena(a.nabavnaCena ?? null);
        setNovaPrvaProdajnaCena(a.prvaProdajnaCena ?? null);
        setSuccess(null);
        setError(null);
    };

    const save = async () => {
        if (!selected) return;

        setIsSaving(true);
        setError(null);
        setSuccess(null);

        try {
            await nivelacijaCena(
                selected.id,
                Number(novaProdajnaCena),
                `Nivelacija: ${selected.prodajnaCena} -> ${novaProdajnaCena}`
            );

            // update local list
            setArtikli(prev =>
                prev.map(a =>
                    a.id === selected.id
                        ? {
                              ...a,
                              prodajnaCena: Number(novaProdajnaCena),
                          }
                        : a
                )
            );

            setSelected(prev =>
                prev
                    ? {
                          ...prev,
                          prodajnaCena: Number(novaProdajnaCena),
                      }
                    : prev
            );

            setSuccess("Cena je uspešno izmenjena (trag sačuvan u DnevnikPromena).");
        } catch (e: any) {
            setError(e?.message ?? "Greška pri snimanju");
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <div className="card" style={{ maxWidth: 900 }}>
            <h2 className="text-2xl font-semibold mb-6">Nivelacija cena</h2>

            {loading && <p>Učitavanje artikala...</p>}
            {error && <p className="error-msg">{error}</p>}
            {success && (
                <p style={{ marginTop: "0.75rem", color: "#059669", fontWeight: 600 }}>{success}</p>
            )}

            <div style={{ marginTop: "1rem" }}>
                <label className="field-label">Pretraži artikal</label>
                <div style={{ position: "relative" }}>
                    <input
                        className="input-big"
                        value={query}
                        onChange={e => {
                            setQuery(e.target.value);
                            setSelected(null);
                        }}
                        placeholder="Unesite naziv artikla..."
                    />

                    {filtered.length > 0 && !selected && (
                        <div
                            style={{
                                position: "absolute",
                                left: 0,
                                right: 0,
                                top: "100%",
                                marginTop: 4,
                                background: "white",
                                border: "1px solid #e5e7eb",
                                borderRadius: 8,
                                maxHeight: 320,
                                overflowY: "auto",
                                zIndex: 50,
                                boxShadow: "0 10px 20px rgba(0,0,0,0.12)",
                            }}
                        >
                            {filtered.map(a => (
                                <button
                                    key={a.id}
                                    type="button"
                                    onClick={() => selectArtikal(a)}
                                    style={{
                                        display: "block",
                                        width: "100%",
                                        textAlign: "left",
                                        padding: "10px 12px",
                                        borderBottom: "1px solid #f3f4f6",
                                        background: "white",
                                        cursor: "pointer",
                                    }}
                                >
                                    <div style={{ fontWeight: 600 }}>{a.naziv}</div>
                                    <div style={{ fontSize: "0.8rem", color: "#6b7280" }}>
                                        ID: {a.id} | Trenutna prodajna: {a.prodajnaCena}
                                    </div>
                                </button>
                            ))}
                        </div>
                    )}
                </div>
            </div>

            {selected && (
                <div style={{ marginTop: "1.5rem" }}>
                    <div
                        style={{
                            background: "#eff6ff",
                            border: "1px solid #bfdbfe",
                            padding: 12,
                            borderRadius: 10,
                            marginBottom: 12,
                        }}
                    >
                        <div style={{ fontWeight: 700 }}>{selected.naziv}</div>
                        <div style={{ fontSize: "0.9rem", color: "#334155" }}>
                            Trenutno: prodajna {selected.prodajnaCena} | nabavna {selected.nabavnaCena ?? "-"} | prva {selected.prvaProdajnaCena ?? "-"}
                        </div>
                    </div>

                    <div
                        style={{
                            display: "grid",
                            gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
                            gap: 12,
                        }}
                    >
                        <div>
                            <label className="field-label">Nova prodajna cena</label>
                            <input
                                className="input-big"
                                type="number"
                                step={0.01}
                                min={0}
                                value={novaProdajnaCena}
                                onChange={e => setNovaProdajnaCena(Number(e.target.value))}
                            />
                        </div>

                        <div>
                            <label className="field-label">Nova nabavna cena</label>
                            <input
                                className="input-big"
                                type="number"
                                step={0.01}
                                min={0}
                                value={novaNabavnaCena ?? ""}
                                onChange={e =>
                                    setNovaNabavnaCena(e.target.value === "" ? null : Number(e.target.value))
                                }
                            />
                        </div>

                        <div>
                            <label className="field-label">Nova prva prodajna cena</label>
                            <input
                                className="input-big"
                                type="number"
                                step={0.01}
                                min={0}
                                value={novaPrvaProdajnaCena ?? ""}
                                onChange={e =>
                                    setNovaPrvaProdajnaCena(e.target.value === "" ? null : Number(e.target.value))
                                }
                            />
                        </div>
                    </div>

                    <button
                        className="button-big"
                        style={{ marginTop: 16, maxWidth: 260, background: "#2563eb" }}
                        disabled={isSaving}
                        onClick={save}
                    >
                        {isSaving ? "Snima se..." : "Sačuvaj cenu"}
                    </button>
                </div>
            )}
        </div>
    );
}
