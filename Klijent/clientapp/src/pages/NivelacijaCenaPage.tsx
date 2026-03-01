import { useEffect, useMemo, useState } from "react";
import { GaugeCircle, Check, AlertCircle, X, TrendingUp, TrendingDown } from "lucide-react";
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
    const hasPriceChanged = selected ? Number(novaProdajnaCena) !== Number(selected.prodajnaCena ?? 0) : false;
    const canSave = !!selected && Number(novaProdajnaCena) > 0 && hasPriceChanged && !isSaving;

    const applyPercentAdjustment = (percent: number) => {
        if (!selected) return;
        const base = Number(selected.prodajnaCena ?? 0);
        if (base <= 0) return;
        const next = base * (1 + percent / 100);
        setNovaProdajnaCena(Number(next.toFixed(2)));
    };

    useEffect(() => {
        const load = async () => {
            setLoading(true);
            setError(null);
            try {
                const data = await getArtikli();
                setArtikli(data);
            } catch (e: any) {
                setError(e?.message ?? "Gre�ka pri ucitavanju artikala");
            } finally {
                setLoading(false);
            }
        };

        load();
    }, []);

    useEffect(() => {
        const onWindowKeyDown = (event: KeyboardEvent) => {
            if ((event.ctrlKey || event.metaKey) && event.key === "Enter" && canSave) {
                event.preventDefault();
                void save();
            }
        };

        window.addEventListener("keydown", onWindowKeyDown);
        return () => window.removeEventListener("keydown", onWindowKeyDown);
    }, [canSave, save]);

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

    async function save() {
        if (!selected) return;
        if (!canSave) {
            setError("Unesite novu prodajnu cenu (vecu od 0) i promenite vrednost.");
            return;
        }

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

            setSuccess("Cena je uspe�no izmenjena (trag sacuvan u DnevnikPromena).");
        } catch (e: any) {
            setError(e?.message ?? "Gre�ka pri snimanju");
        } finally {
            setIsSaving(false);
        }
    }

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
                    { label: "Rezultat", value: success ? "Uspeh" : error ? "Gre�ka" : "-", tone: success ? "positive" : error ? "danger" : "neutral" },
                ]}
            />

            <InventoryPanel className="max-w-5xl">
                <h2 className="mb-4 text-xl font-semibold text-[#f3f6ff]">Izmena cena po artiklu</h2>

                {loading && <p className="text-sm text-[#9aabc7]">Učitavanje artikala...</p>}
                {error && (
                  <div className="flex items-start gap-2 rounded-xl border border-[#7f1d1d] bg-[#2b0a0a] px-3 py-2 text-sm text-[#f87171]">
                    <AlertCircle size={16} className="mt-0.5 shrink-0" />
                    <span className="flex-1">{error}</span>
                    <button type="button" onClick={() => setError(null)} className="shrink-0 hover:text-white"><X size={14} /></button>
                  </div>
                )}
                {success && (
                  <div className="flex items-start gap-2 rounded-xl border border-[#14532d] bg-[#0d2118] px-3 py-2 text-sm text-[#4ade80]">
                    <Check size={16} className="mt-0.5 shrink-0" />
                    <span className="flex-1">{success}</span>
                    <button type="button" onClick={() => setSuccess(null)} className="shrink-0 hover:text-white"><X size={14} /></button>
                  </div>
                )}

                <div className="mt-4">
                    <label className="mb-1 block text-xs uppercase tracking-wide text-[#93a7c8]">Pretra�i artikal</label>
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
                            {(() => {
                              const delta = Number(novaProdajnaCena) - (selected.prodajnaCena ?? 0);
                              const pct = selected.prodajnaCena ? (delta / selected.prodajnaCena) * 100 : 0;
                              if (delta === 0) return null;
                              const up = delta > 0;
                              return (
                                <div className={`mt-2 flex items-center gap-1.5 text-sm font-semibold ${up ? "text-emerald-300" : "text-rose-300"}`}>
                                  {up ? <TrendingUp size={14} /> : <TrendingDown size={14} />}
                                  {up ? "+" : ""}{delta.toFixed(2)} RSD ({up ? "+" : ""}{pct.toFixed(1)}%)
                                  <span className="text-xs font-normal text-[#8A95B0]">— nova vs stara cena</span>
                                </div>
                              );
                            })()}
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
                                <div className="mt-2 flex flex-wrap gap-1.5">
                                    {[-10, -5, 5, 10].map((percent) => (
                                        <button
                                            key={percent}
                                            type="button"
                                            onClick={() => applyPercentAdjustment(percent)}
                                            className="rounded-lg border border-[#2f323b] bg-[#1a1b1f] px-2 py-1 text-xs text-[#c7d6ef] hover:border-[#4f8cff]"
                                        >
                                            {percent > 0 ? "+" : ""}{percent}%
                                        </button>
                                    ))}
                                </div>
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
                            disabled={!canSave}
                            onClick={save}
                        >
                            {isSaving ? "Snima se..." : hasPriceChanged ? "Sacuvaj cenu" : "Promenite cenu za snimanje"}
                        </button>
                    </div>
                )}
            </InventoryPanel>
        </InventoryPageShell>
    );
}
