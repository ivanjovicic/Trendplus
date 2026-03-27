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
                <h2 className="mb-4 text-xl font-semibold text-[var(--text-primary)]">Izmena cena po artiklu</h2>

                {loading && <p className="text-sm text-[var(--text-primary)]">Učitavanje artikala...</p>}
                {error && (
                  <div className="flex items-start gap-2 rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-sm text-[var(--text-primary)]">
                    <AlertCircle size={16} className="mt-0.5 shrink-0" />
                    <span className="flex-1">{error}</span>
                    <button type="button" onClick={() => setError(null)} className="shrink-0 hover:text-white"><X size={14} /></button>
                  </div>
                )}
                {success && (
                  <div className="flex items-start gap-2 rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-sm text-[var(--text-primary)]">
                    <Check size={16} className="mt-0.5 shrink-0" />
                    <span className="flex-1">{success}</span>
                    <button type="button" onClick={() => setSuccess(null)} className="shrink-0 hover:text-white"><X size={14} /></button>
                  </div>
                )}

                <div className="mt-4">
                    <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-primary)]">Pretra�i artikal</label>
                    <div className="relative">
                        <input
                            className="w-full rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-sm text-[var(--text-primary)] outline-none transition focus:border-[var(--border-default)]"
                            value={query}
                            onChange={e => {
                                setQuery(e.target.value);
                                setSelected(null);
                            }}
                            placeholder="Unesite naziv artikla..."
                        />

                        {filtered.length > 0 && !selected && (
                            <div className="absolute left-0 right-0 top-full z-50 mt-1 max-h-80 overflow-y-auto rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] shadow-xl">
                                {filtered.map(a => (
                                    <button
                                        key={a.id}
                                        type="button"
                                        onClick={() => selectArtikal(a)}
                                        className="block w-full border-b border-[var(--border-default)] px-3 py-2 text-left hover:bg-[var(--surface-light)]"
                                    >
                                        <div className="text-sm font-semibold text-[var(--text-primary)]">{a.naziv}</div>
                                        <div className="text-xs text-[var(--text-primary)]">
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
                        <div className="mb-3 rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] p-3">
                            <div className="font-semibold text-[var(--text-primary)]">{selected.naziv}</div>
                            <div className="text-sm text-[var(--text-primary)]">
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
                                  <span className="text-xs font-normal text-[var(--text-primary)]">— nova vs stara cena</span>
                                </div>
                              );
                            })()}
                        </div>

                        <div className="grid gap-3 md:grid-cols-3">
                            <div>
                                <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-primary)]">Nova prodajna cena</label>
                                <input
                                    className="w-full rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-sm text-[var(--text-primary)] outline-none transition focus:border-[var(--border-default)]"
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
                                            className="rounded-lg border border-[var(--border-default)] bg-[var(--surface-elevated)] px-2 py-1 text-xs text-[var(--text-primary)] hover:border-[var(--border-default)]"
                                        >
                                            {percent > 0 ? "+" : ""}{percent}%
                                        </button>
                                    ))}
                                </div>
                            </div>

                            <div>
                                <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-primary)]">Nova nabavna cena</label>
                                <input
                                    className="w-full rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-sm text-[var(--text-primary)] outline-none transition focus:border-[var(--border-default)]"
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
                                <label className="mb-1 block text-xs uppercase tracking-wide text-[var(--text-primary)]">Nova prva prodajna cena</label>
                                <input
                                    className="w-full rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-3 py-2 text-sm text-[var(--text-primary)] outline-none transition focus:border-[var(--border-default)]"
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
                            className="mt-4 rounded-xl border border-[var(--border-default)] bg-[var(--surface-elevated)] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[var(--surface-light)] disabled:opacity-60"
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

