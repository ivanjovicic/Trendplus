import { ArtikalFormData } from "../types/artikalformdata";
import { createTipObuce } from "../services/tipoviObuceApi";
import { createDobavljac } from "../services/dobavljaciApi";
import React, { useState } from "react";

export interface CreateArtikalFormProps {
    tipoviObuce: { id: number; naziv: string }[];
    dobavljaci: { id: number; naziv: string }[];
    onSubmit: (data: ArtikalFormData) => Promise<number | void>;
    loadingOptions?: boolean;
    initialData?: ArtikalFormData;
    mode?: "create" | "edit";
}

export default function CreateArtikalForm({
    tipoviObuce,
    dobavljaci,
    onSubmit,
    loadingOptions = false,
    initialData,
    mode = "create",
}: CreateArtikalFormProps) {
    React.useEffect(() => {
        if (!initialData) return;
        setNaziv(initialData.naziv);
        setProdajnaCena(initialData.prodajnaCena != null ? String(initialData.prodajnaCena) : "");
        setNabavnaCena(initialData.nabavnaCena != null ? String(initialData.nabavnaCena) : "");
        setNabavnaCenaDin(initialData.nabavnaCenaDin != null ? String(initialData.nabavnaCenaDin) : "");
        setPrvaProdajnaCena(
            initialData.prvaProdajnaCena != null ? String(initialData.prvaProdajnaCena) : ""
        );
        setKolicina(initialData.kolicina != null ? String(initialData.kolicina) : "");
        setKomentar(initialData.komentar ?? "");
        setSelectedTip(initialData.tipObuceId ?? null);
        setSelectedDobavljac(initialData.dobavljacId ?? null);
    }, [initialData]);

    const [naziv, setNaziv] = useState(initialData?.naziv ?? "");
    const [prodajnaCena, setProdajnaCena] = useState(
        initialData?.prodajnaCena != null ? String(initialData.prodajnaCena) : ""
    );
    const [nabavnaCena, setNabavnaCena] = useState(
        initialData?.nabavnaCena != null ? String(initialData.nabavnaCena) : ""
    );
    const [nabavnaCenaDin, setNabavnaCenaDin] = useState(
        initialData?.nabavnaCenaDin != null ? String(initialData.nabavnaCenaDin) : ""
    );
    const [prvaProdajnaCena, setPrvaProdajnaCena] = useState(
        initialData?.prvaProdajnaCena != null ? String(initialData.prvaProdajnaCena) : ""
    );
    const [kolicina, setKolicina] = useState(
        initialData?.kolicina != null ? String(initialData.kolicina) : ""
    );
    const [komentar, setKomentar] = useState(initialData?.komentar ?? "");
    const [selectedTip, setSelectedTip] = useState<number | null>(initialData?.tipObuceId ?? null);
    const [selectedDobavljac, setSelectedDobavljac] = useState<number | null>(
        initialData?.dobavljacId ?? null
    );
    const [msg, setMsg] = useState("");
    const [error, setError] = useState("");
    const [isSubmitting, setIsSubmitting] = useState(false);

    const [newTip, setNewTip] = useState("");
    const [newDob, setNewDob] = useState("");
    const [showNewTipConfirm, setShowNewTipConfirm] = useState(false);
    const [showNewDobConfirm, setShowNewDobConfirm] = useState(false);

    const handleCreateTip = async () => {
        if (!newTip.trim()) return;
        setShowNewTipConfirm(false);
        try {
            const idTip = await createTipObuce(newTip.trim());
            setSelectedTip(idTip);
            setNewTip("");
            window.location.reload();
        } catch (e) {
            console.error(e);
            setError("Ne mogu da kreiram tip obuće.");
        }
    };

    const handleCreateDob = async () => {
        if (!newDob.trim()) return;
        setShowNewDobConfirm(false);
        try {
            const idDob = await createDobavljac(newDob.trim());
            setSelectedDobavljac(idDob);
            setNewDob("");
            window.location.reload();
        } catch (e) {
            console.error(e);
            setError("Ne mogu da kreiram dobavljača.");
        }
    };

    const handleSubmit = async () => {
        setMsg("");
        setError("");

        const formData: ArtikalFormData = {
            naziv,
            prodajnaCena: Number(prodajnaCena),
            nabavnaCena: nabavnaCena ? Number(nabavnaCena) : null,
            nabavnaCenaDin: nabavnaCenaDin ? Number(nabavnaCenaDin) : null,
            prvaProdajnaCena: prvaProdajnaCena ? Number(prvaProdajnaCena) : null,
            kolicina: kolicina ? Number(kolicina) : null,
            komentar: komentar || null,
            tipObuceId: selectedTip,
            dobavljacId: selectedDobavljac,
        };

        console.debug("CreateArtikalForm submitting formData:", formData);

        setIsSubmitting(true);
        try {
            await onSubmit(formData);
            setMsg("Artikal uspešno kreiran ✔️");
           
            setNaziv("");
            setProdajnaCena("");
            setNabavnaCena("");
            setNabavnaCenaDin("");
            setPrvaProdajnaCena("");
            setKolicina("");
            setKomentar("");
            setSelectedTip(null);
            setSelectedDobavljac(null);
        } catch (e) {
            setError("Greška pri kreiranju artikla.");
            console.error(e);
        } finally {
            setIsSubmitting(false);
        }
    };

    const newTipIsEmpty = !newTip.trim();
    const newDobIsEmpty = !newDob.trim();

    return (
        <div className="card">
            <h2 className="text-2xl font-semibold mb-6">
                {mode === "edit" ? "✏️ Izmeni artikal" : "➕ Kreiraj artikal"}
            </h2>

            <div className="form-grid">
                <div>                   
                    <label className="field-label">Naziv</label>
                    <input
                        className="input-big"
                        placeholder="Naziv artikla"
                        value={naziv}
                        onChange={(e) => setNaziv(e.target.value)}
                    />

                    <label className="field-label">Prodajna cena (RSD)</label>
                    <input
                        className="input-big"
                        placeholder="Prodajna cena"
                        type="number"
                        value={prodajnaCena}
                        onChange={(e) => setProdajnaCena(e.target.value)}
                    />

                    <label className="field-label">Nabavna cena</label>
                    <input
                        className="input-big"
                        placeholder="Nabavna cena"
                        type="number"
                        value={nabavnaCena}
                        onChange={(e) => setNabavnaCena(e.target.value)}
                    />

                    <label className="field-label">Nabavna cena (din)</label>
                    <input
                        className="input-big"
                        placeholder="Nabavna cena (din)"
                        type="number"
                        value={nabavnaCenaDin}
                        onChange={(e) => setNabavnaCenaDin(e.target.value)}
                    />

                    <label className="field-label">Prva prodajna cena</label>
                    <input
                        className="input-big"
                        placeholder="Prva prodajna cena"
                        type="number"
                        value={prvaProdajnaCena}
                        onChange={(e) => setPrvaProdajnaCena(e.target.value)}
                    />

                    <label className="field-label">Količina</label>
                    <input
                        className="input-big"
                        placeholder="Količina"
                        type="number"
                        value={kolicina}
                        onChange={(e) => setKolicina(e.target.value)}
                    />
                </div>

                <div>
                    <label className="field-label">Komentar</label>
                    <textarea
                        className="input-big"
                        placeholder="Komentar"
                        value={komentar}
                        onChange={(e) => setKomentar(e.target.value)}
                    />

                    <label className="field-label">Tip obuće</label>
                    <div className="flex-row">
                        <select
                            className="input-big"
                            value={selectedTip ?? ""}
                            onChange={(e) => {
                                const v = e.target.value ? Number(e.target.value) : null;
                                console.debug('selectedTip change ->', v);
                                setSelectedTip(v);
                            }}
                        >
                            <option value="">-- izaberite --</option>
                            {tipoviObuce.map((t) => (
                                <option key={t.id} value={t.id}>{t.naziv}</option>
                            ))}
                        </select>

                        <input
                            className="input-big"
                            placeholder="Novi tip..."
                            value={newTip}
                            onChange={(e) => setNewTip(e.target.value)}
                        />

                        <button
                            type="button"
                            className="button-big"
                            disabled={newTipIsEmpty}
                            onClick={() => setShowNewTipConfirm(true)}
                        >
                            Dodaj
                        </button>
                    </div>

                    <label className="field-label">Dobavljač</label>
                    <div className="flex-row">
                        <select
                            className="input-big"
                            value={selectedDobavljac ?? ""}
                            onChange={(e) => {
                                const v = e.target.value ? Number(e.target.value) : null;
                                console.debug('selectedDobavljac change ->', v);
                                setSelectedDobavljac(v);
                            }}
                        >
                            <option value="">-- izaberite --</option>
                            {dobavljaci.map((d) => (
                                <option key={d.id} value={d.id}>{d.naziv}</option>
                            ))}
                        </select>

                        <input
                            type="text"
                            className="input-big"
                            placeholder="Novi dobavljač..."
                            value={newDob}
                            onChange={(e) => setNewDob(e.target.value)}
                        />

                        <button
                            type="button"
                            className="button-big"
                            onClick={() => setShowNewDobConfirm(true)}
                            disabled={newDobIsEmpty}
                        >
                            Dodaj
                        </button>
                    </div>
                </div> 

                <div className="form-full">
                    <button 
                        className="button-big" 
                        onClick={handleSubmit} 
                        disabled={isSubmitting}
                        style={{ maxWidth: '420px' }}
                    >
                        {isSubmitting ? "Kreiram..." : mode === "edit" ? "Sačuvaj izmene" : "Kreiraj artikal"}
                    </button>
                </div>

                {msg && <p className="form-full success-msg">{msg}</p>}
                {error && <p className="form-full error-msg">{error}</p>}
            </div>

            {showNewTipConfirm && (
                <div style={{ position: 'fixed', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 2000 }}>
                    <div style={{ background: 'rgba(0,0,0,0.4)', position: 'absolute', inset: 0 }} onClick={() => setShowNewTipConfirm(false)} />
                    <div style={{ background: '#fff', padding: 24, borderRadius: 12, zIndex: 2001, minWidth: 320, maxWidth: 480 }}>
                        <p style={{ fontWeight: 600, marginBottom: 12, fontSize: '1.125rem' }}>Potvrdi kreiranje tipa obuće</p>
                        <p style={{ marginBottom: 16 }}>Da li želite da kreirate tip: <strong>{newTip}</strong>?</p>
                        <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
                            <button 
                                className="button-big" 
                                style={{ background: '#e5e7eb', color: '#111', width: 'auto', padding: '10px 20px', marginTop: 0 }} 
                                onClick={() => setShowNewTipConfirm(false)}
                            >
                                Otkaži
                            </button>
                            <button 
                                className="button-big" 
                                style={{ width: 'auto', padding: '10px 20px', marginTop: 0 }} 
                                onClick={handleCreateTip}
                            >
                                Potvrdi
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {showNewDobConfirm && (
                <div style={{ position: 'fixed', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 2000 }}>
                    <div style={{ background: 'rgba(0,0,0,0.4)', position: 'absolute', inset: 0 }} onClick={() => setShowNewDobConfirm(false)} />
                    <div style={{ background: '#fff', padding: 24, borderRadius: 12, zIndex: 2001, minWidth: 320, maxWidth: 480 }}>
                        <p style={{ fontWeight: 600, marginBottom: 12, fontSize: '1.125rem' }}>Potvrdi kreiranje novog dobavljača</p>
                        <p style={{ marginBottom: 16 }}>Da li želite da kreirate dobavljača: <strong>{newDob}</strong>?</p>
                        <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
                            <button 
                                className="button-big" 
                                style={{ background: '#e5e7eb', color: '#111', width: 'auto', padding: '10px 20px', marginTop: 0 }} 
                                onClick={() => setShowNewDobConfirm(false)}
                            >
                                Otkaži
                            </button>
                            <button 
                                className="button-big" 
                                style={{ width: 'auto', padding: '10px 20px', marginTop: 0 }} 
                                onClick={handleCreateDob}
                            >
                                Potvrdi
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}