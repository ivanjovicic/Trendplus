import { useEffect, useMemo, useState } from "react";
import { GaugeCircle } from "lucide-react";
import { getArtikli, nivelacijaCena } from "../services/artikliApi";
import { Artikal } from "../types/Artikal";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel } from "../components/inventory/InventoryPageShell";

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
                setError(e?.message ?? "Greška pri ucitavanju artikala");
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

            setSuccess("Cena je uspešno izmenjena (trag sacuvan u DnevnikPromena).");
        } catch (e: any) {
            setError(e?.message ?? "Greška pri snimanju");
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <InventoryPageShell
            icon={GaugeCircle}
            title="Nivelacija cena"
            subtitle="Centar za promenu cena sa instant preview-om stanja izabranog artikla."
        >
            <InventoryKpiRow
                items={[
                    { label: "Artikli", value: `${artikli.length}` },
                    { label: "Selektovan SKU", value: selected ? `#${selected.id}` : "Nije izabran", tone: selected ? "positive" : "warning" },
                    { label: "Status snimanja", value: isSaving ? "Snimanje" : "Idle", tone: isSaving ? "warning" : "neutral" },
                    { label: "Rezultat", value: success ? "Uspeh" : error ? "Greška" : "-", tone: success ? "positive" : error ? "danger" : "neutral" },
                ]}
            />

            <InventoryPanel className="max-w-5xl">
                <h2 className="mb-4 text-xl font-semibold text-[#f3f6ff]">Izmena cena po artiklu</h2>

                {loading && <p className="text-sm text-[#9aabc7]">Ucitavanje artikala...</p>}
                {error && <p className="text-sm font-medium text-rose-300">{error}</p>}
                {success && <p className="text-sm font-medium text-emerald-300">{success}</p>}

                <div className="mt-4">
                    <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Pretraži artikal</label>
                    <div className="relative">
                        <input
                            className="w-full rounded-xl border border-[#2f323b] bg-[#14161d] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
                            value={query}
                            onChange={e => {
                                setQuery(e.target.value);
                                setSelected(null);
                            }}
                            placeholder="Unesite naziv artikla..."
                        />

                        {filtered.length > 0 && !selected && (
                            <div className="absolute left-0 right-0 top-full z-50 mt-1 max-h-80 overflow-y-auto rounded-xl border border-[#2f323b] bg-[#13151c] shadow-xl">
                                {filtered.map(a => (
                                    <button
                                        key={a.id}
                                        type="button"
                                        onClick={() => selectArtikal(a)}
                                        className="block w-full border-b border-[#242833] px-3 py-2 text-left hover:bg-[#1b1f29]"
                                    >
                                        <div className="text-sm font-semibold text-[#e7eeff]">{a.naziv}</div>
                                        <div className="text-xs text-[#8ea0bd]">
                                            ID: {a.id} | Trenutna prodajna: {a.prodajnaCena}
                                        </div>
                                    </button>
                                ))}
                            </div>
                        )}
                    </div>
                </div>

                {selected && (
                    <div className="mt-6">
                        <div className="mb-3 rounded-xl border border-[#2e3f68] bg-[#1b2742] p-3">
                            <div className="font-semibold text-[#e7eeff]">{selected.naziv}</div>
                            <div className="text-sm text-[#9fc0ff]">
                                Trenutno: prodajna {selected.prodajnaCena} | nabavna {selected.nabavnaCena ?? "-"} | prva {selected.prvaProdajnaCena ?? "-"}
                            </div>
                        </div>

                        <div className="grid gap-3 md:grid-cols-3">
                            <div>
                                <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Nova prodajna cena</label>
                                <input
                                    className="w-full rounded-xl border border-[#2f323b] bg-[#14161d] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
                                    type="number"
                                    step={0.01}
                                    min={0}
                                    value={novaProdajnaCena}
                                    onChange={e => setNovaProdajnaCena(Number(e.target.value))}
                                />
                            </div>

                            <div>
                                <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Nova nabavna cena</label>
                                <input
                                    className="w-full rounded-xl border border-[#2f323b] bg-[#14161d] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
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
                                <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Nova prva prodajna cena</label>
                                <input
                                    className="w-full rounded-xl border border-[#2f323b] bg-[#14161d] px-3 py-2 text-sm text-[#e3ebff] outline-none transition focus:border-[#4f8cff]"
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
                            className="mt-4 rounded-xl border border-[#3760b7] bg-[#2d4f95] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[#3760b7] disabled:opacity-60"
                            disabled={isSaving}
                            onClick={save}
                        >
                            {isSaving ? "Snima se..." : "Sacuvaj cenu"}
                        </button>
                    </div>
                )}
            </InventoryPanel>
        </InventoryPageShell>
    );
}
