import { useEffect, useMemo, useState } from "react";
import { GaugeCircle } from "lucide-react";
import { getArtikli, nivelacijaCena } from "../services/artikliApi";
import { Artikal } from "../types/Artikal";
import {
    CalculatedField,
    EntitySearchCombobox,
    FormField,
    FormLayout,
    FormPageShell,
    FormProgress,
    FormSection,
    ReadonlyField,
    StickyActionBar,
    SummaryPanel,
    ValidationChecklist,
    type EntitySearchItem,
} from "../components/forms/FormSystem";

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
                setError(e?.message ?? "Greška pri učitavanju artikala");
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

            setSuccess("Cena je uspešno izmenjena (trag sačuvan u DnevnikPromena).");
        } catch (e: any) {
            setError(e?.message ?? "Greška pri snimanju");
        } finally {
            setIsSaving(false);
        }
    }

    const searchItems = useMemo<EntitySearchItem[]>(
        () =>
            filtered.map((artikal) => ({
                id: artikal.id,
                title: artikal.naziv,
                meta: `ID: ${artikal.id}`,
                value: `${artikal.prodajnaCena ?? 0} RSD`,
            })),
        [filtered]
    );
    const delta = selected ? Number(novaProdajnaCena) - Number(selected.prodajnaCena ?? 0) : 0;
    const deltaPct = selected?.prodajnaCena ? (delta / Number(selected.prodajnaCena)) * 100 : 0;
    const saveDisabledReason = !selected
        ? "Izaberite artikal."
        : Number(novaProdajnaCena) <= 0
            ? "Nova prodajna cena mora biti veća od 0."
            : !hasPriceChanged
                ? "Promenite prodajnu cenu za snimanje."
                : undefined;

    return (
        <FormPageShell
            icon={GaugeCircle}
            title="Nivelacija cena"
            subtitle="Centar za promenu cena sa instant preview-om stanja izabranog artikla."
        >
            <FormProgress
                steps={[
                    { label: "Izbor artikla", state: selected ? "complete" : "pending" },
                    { label: "Nova cena", state: canSave ? "complete" : "pending" },
                    { label: success ? "Sačuvano" : error ? "Proveri" : "Spremno", state: error ? "warning" : success ? "complete" : "pending" },
                ]}
            />

            <FormLayout
                main={(
                    <>
                        <FormSection title="Izbor artikla" description="Pretraga koristi već učitan katalog ove stranice." complete={!!selected}>
                            {loading ? <div className="form-note">Učitavanje artikala...</div> : null}
                            <EntitySearchCombobox
                                label="Pretraži artikal"
                                value={query}
                                placeholder="Unesite naziv artikla..."
                                items={selected ? [] : searchItems}
                                onQueryChange={(value) => {
                                    setQuery(value);
                                    setSelected(null);
                                    setSuccess(null);
                                }}
                                onSelect={(item) => {
                                    const artikal = filtered.find((entry) => entry.id === Number(item.id));
                                    if (artikal) selectArtikal(artikal);
                                }}
                            />
                        </FormSection>

                        {selected ? (
                            <>
                                <FormSection title="Trenutne vrednosti" description="Ovo su read-only vrednosti iz kataloga." complete>
                                    <div className="form-grid form-grid--three">
                                        <ReadonlyField label="Artikal" value={selected.naziv} />
                                        <ReadonlyField label="Prodajna cena" value={`${selected.prodajnaCena ?? 0} RSD`} />
                                        <ReadonlyField label="Nabavna cena" value={selected.nabavnaCena == null ? "-" : `${selected.nabavnaCena} RSD`} />
                                    </div>
                                </FormSection>

                                <FormSection title="Nova cena" description="Snimanje je omogućeno tek kada je prodajna cena promenjena." complete={canSave} warning={!!saveDisabledReason}>
                                    <div className="form-grid form-grid--three">
                                        <FormField label="Nova prodajna cena" required>
                                            <input className="form-control form-control--number" type="number" step={0.01} min={0} value={novaProdajnaCena} onChange={(event) => setNovaProdajnaCena(Number(event.target.value))} />
                                        </FormField>
                                        <FormField label="Nova nabavna cena">
                                            <input className="form-control form-control--number" type="number" step={0.01} min={0} value={novaNabavnaCena ?? ""} onChange={(event) => setNovaNabavnaCena(event.target.value === "" ? null : Number(event.target.value))} />
                                        </FormField>
                                        <FormField label="Nova prva prodajna cena">
                                            <input className="form-control form-control--number" type="number" step={0.01} min={0} value={novaPrvaProdajnaCena ?? ""} onChange={(event) => setNovaPrvaProdajnaCena(event.target.value === "" ? null : Number(event.target.value))} />
                                        </FormField>
                                    </div>
                                    <div className="line-row__header">
                                        {[-10, -5, 5, 10].map((percent) => (
                                            <button key={percent} type="button" className="btn btn--secondary" onClick={() => applyPercentAdjustment(percent)}>
                                                {percent > 0 ? "+" : ""}{percent}%
                                            </button>
                                        ))}
                                    </div>
                                </FormSection>
                            </>
                        ) : null}
                    </>
                )}
                aside={(
                    <SummaryPanel
                        title="Pregled nivelacije"
                        actions={(
                            <>
                                {error ? <p className="form-error">{error}</p> : null}
                                {success ? <p className="validation-list__item validation-list__item--valid">{success}</p> : null}
                                {saveDisabledReason ? <p className="form-helper">{saveDisabledReason}</p> : null}
                                <button type="button" className="btn btn--primary btn--full" disabled={!canSave} onClick={() => void save()}>
                                    {isSaving ? "Snima se..." : "Sačuvaj cenu"}
                                </button>
                            </>
                        )}
                    >
                        <ReadonlyField label="Artikli u katalogu" value={artikli.length} />
                        <ReadonlyField label="SKU" value={selected ? `#${selected.id}` : "Nije izabran"} />
                        <CalculatedField
                            label="Razlika"
                            value={selected ? `${delta > 0 ? "+" : ""}${delta.toFixed(2)} RSD (${deltaPct > 0 ? "+" : ""}${deltaPct.toFixed(1)}%)` : "-"}
                            tone={delta === 0 ? "neutral" : delta > 0 ? "success" : "warning"}
                        />
                        <ValidationChecklist
                            items={[
                                { label: "Artikal je izabran", valid: !!selected },
                                { label: "Nova cena je veća od 0", valid: Number(novaProdajnaCena) > 0 },
                                { label: "Prodajna cena je promenjena", valid: hasPriceChanged },
                            ]}
                        />
                    </SummaryPanel>
                )}
            />

            <StickyActionBar
                primaryLabel={isSaving ? "Snima se..." : "Sačuvaj cenu"}
                disabled={!canSave}
                disabledReason={saveDisabledReason}
                onPrimary={() => void save()}
            />
        </FormPageShell>
    );
}

