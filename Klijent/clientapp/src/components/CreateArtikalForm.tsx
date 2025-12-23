import { useState } from "react";
import { ArtikalFormData } from "../types/artikalformdata";
import { createTipObuce } from "../services/tipoviObuceApi";
import { createDobavljac } from "../services/dobavljaciApi";

export interface CreateArtikalFormProps {
    tipoviObuce: { id: number; naziv: string }[];
    dobavljaci: { id: number; naziv: string }[];
    onSubmit: (data: ArtikalFormData) => Promise<number | void>;
}

export default function CreateArtikalForm({
    tipoviObuce,
    dobavljaci,
    onSubmit,
}: CreateArtikalFormProps) {
    const [plu, setPlu] = useState("");
    const [naziv, setNaziv] = useState("");
    const [prodajnaCena, setProdajnaCena] = useState("");
    const [nabavnaCena, setNabavnaCena] = useState("");
    const [nabavnaCenaDin, setNabavnaCenaDin] = useState("");
    const [prvaProdajnaCena, setPrvaProdajnaCena] = useState("");
    const [kolicina, setKolicina] = useState("");
    const [komentar, setKomentar] = useState("");
    const [msg, setMsg] = useState("");
    const [error, setError] = useState("");
    const [isSubmitting, setIsSubmitting] = useState(false);

    const [selectedTip, setSelectedTip] = useState<number | null>(null);
    const [selectedDobavljac, setSelectedDobavljac] = useState<number | null>(null);

    // new tip UI
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
            // ideally refresh dropdowns here; useDropdownData doesn't expose refresh, so reload page
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

        if (!naziv.trim() || Number(prodajnaCena) <= 0) {
            setError("SUnesite ispravne vrednosti.");
            return;
        }

        // Formiraš objekat koji šalješ spolja
        const formData: ArtikalFormData = {
            plu: plu || null,
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

        // Debug: log what will be sent
        /*try {*/
            console.debug("CreateArtikalForm submitting formData:", formData);
     /*   } catch {}*/

        setIsSubmitting(true);
        try {
            await onSubmit(formData); // await the async submit
            // Optionally assert result (e.g., created id)
            setMsg("Artikal uspešno kreiran ✔️");
            setPlu("");
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
        <div className="card" style={{ margin: '2rem auto', maxWidth: 1000 }}>
            <h2 className="text-2xl font-semibold mb-6">➕ Kreiraj artikal</h2>

            <div className="form-grid" style={{ gap: '1.25rem' }}>
                <div>
                    <label className="field-label">PLU</label>
                    <input
                        className="input-big mb-4"
                        placeholder="PLU (šifra)"
                        value={plu}
                        onChange={(e) => setPlu(e.target.value)}
                        style={{ maxWidth: '240px' }}
                    />

                    <label className="field-label">Naziv</label>
                    <input
                        className="input-big mb-4"
                        placeholder="Naziv artikla"
                        value={naziv}
                        onChange={(e) => setNaziv(e.target.value)}
                    />

                    <label className="field-label">Prodajna cena (RSD)</label>
                    <input
                        className="input-big mb-4"
                        placeholder="Prodajna cena"
                        type="number"
                        value={prodajnaCena}
                        onChange={(e) => setProdajnaCena(e.target.value)}
                        style={{ maxWidth: '240px' }}
                    />

                    <label className="field-label">Nabavna cena</label>
                    <input
                        className="input-big mb-4"
                        placeholder="Nabavna cena"
                        type="number"
                        value={nabavnaCena}
                        onChange={(e) => setNabavnaCena(e.target.value)}
                        style={{ maxWidth: '240px' }}
                    />

                    <label className="field-label">Nabavna cena (din)</label>
                    <input
                        className="input-big mb-4"
                        placeholder="Nabavna cena (din)"
                        type="number"
                        value={nabavnaCenaDin}
                        onChange={(e) => setNabavnaCenaDin(e.target.value)}
                        style={{ maxWidth: '240px' }}
                    />

                    <label className="field-label">Prva prodajna cena</label>
                    <input
                        className="input-big mb-4"
                        placeholder="Prva prodajna cena"
                        type="number"
                        value={prvaProdajnaCena}
                        onChange={(e) => setPrvaProdajnaCena(e.target.value)}
                        style={{ maxWidth: '240px' }}
                    />

                    <label className="field-label">Količina</label>
                    <input
                        className="input-big mb-4"
                        placeholder="Količina"
                        type="number"
                        value={kolicina}
                        onChange={(e) => setKolicina(e.target.value)}
                        style={{ maxWidth: '240px' }}
                    />
                </div>

                <div>
                    
                    <label className="field-label">Komentar</label>
                    <textarea
                        className="input-big mb-4 form-full"
                        placeholder="Komentar"
                        value={komentar}
                        onChange={(e) => setKomentar(e.target.value)}
                        style={{ maxWidth: '100%', minHeight: 100 }}
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
                            style={{ maxWidth: '320px' }}
                        >
                            <option value="">-- izaberite --</option>
                            {tipoviObuce.map((t) => (
                                <option key={t.id} value={t.id}>{t.naziv}</option>
                            ))}
                        </select>

                        <input
                            className="input-big mb-4"
                            placeholder="Novi tip..."
                            value={newTip}
                            onChange={(e) => setNewTip(e.target.value)}
                            style={{ flex: '1 1 360px', minWidth: 220, height: 40 }}
                        />

                        <button
                            type="button"
                            className="button-big"
                            style={{ width: 'auto', height: 40, marginTop: -14, alignSelf: 'center', ...(newTipIsEmpty ? { opacity: 0.6, cursor: 'not-allowed' } : {}), }}
                            disabled={newTipIsEmpty}
                            onClick={() => setShowNewTipConfirm(true)}
                        >
                            Dodaj
                        </button>
                    </div>

                    <label className="field-label" style={{ marginTop: '6px' }}>Dobavljac</label>
                    <div className="flex-row">
                    <select
                        className="input-big mb-4"
                        value={selectedDobavljac ?? ""}
                        onChange={(e) => {
                            const v = e.target.value ? Number(e.target.value) : null;
                            console.debug('selectedDobavljac change ->', v);
                            setSelectedDobavljac(v);
                        }}
                        style={{ maxWidth: '320px' }}
                    >
                        <option value="">-- izaberite --</option>
                        {dobavljaci.map((d) => (
                            <option key={d.id} value={d.id}>{d.naziv}</option>
                        ))}
                            </select>
                            <input
                            type="text"
                            className="input-big mb-4"
                            placeholder="Novi dobavljač..."
                            value={newDob}
                                onChange={(e) => setNewDob(e.target.value)}
                                style={{ flex: '1 1 360px', minWidth: 220, height: 50, padding: '8px 12px', marginBottom: 12 }}
                            />

                            <button
                                type="button"
                                className="button-big"
                                style={{
                                    padding: '8px 12px',
                                    width: 'auto',
                                    alignSelf: 'center',
                                    height: 40,
                                    flexShrink: 0,
                                    marginTop: 6, // move button slightly down
                                   /* marginTop: 2, */// nudge 2px up
                                    marginBottom: 0,
                                    ...(newDobIsEmpty ? { opacity: 0.6, cursor: 'not-allowed' } : {}),
                                }}
                                onClick={() => setShowNewDobConfirm(true)}
                            disabled={newDobIsEmpty}
                            >
                                Dodaj
                            </button>
                      
                    </div>
                </div> 
                <div className="form-full">
                    <button className="button-big" onClick={handleSubmit} style={{ maxWidth: 420 }} disabled={isSubmitting}>
                        {isSubmitting ? "Kreiram..." : "Kreiraj artikal"}
                    </button>
                </div>

                {msg && <p className="mt-4 text-lg font-medium success-msg">{msg}</p>}
                {error && <p className="mt-4 text-lg font-medium error-msg">{error}</p>}
            </div>

            {showNewTipConfirm && (
                <div style={{ position: 'fixed', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 2000 }}>
                    <div style={{ background: 'rgba(0,0,0,0.4)', position: 'absolute', inset: 0 }} />
                    <div style={{ background: '#fff', padding: 24, borderRadius: 12, zIndex: 2001, minWidth: 320 }}>
                        <p style={{ fontWeight: 600, marginBottom: 12 }}>Potvrdi kreiranje tipa obuće</p>
                        <p style={{ marginBottom: 16 }}>Da li želite da kreirate tip: <strong>{newTip}</strong>?</p>
                        <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
                            <button className="button-big" style={{ background: '#e5e7eb', color: '#111', width: 'auto', padding: '8px 12px' }} onClick={() => setShowNewTipConfirm(false)}>Otkaži</button>
                            <button className="button-big" style={{ width: 'auto', padding: '8px 12px' }} onClick={handleCreateTip}>Potvrdi</button>
                        </div>
                    </div>
                </div>
            )}
            {showNewDobConfirm && (
                <div style={{ position: 'fixed', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 2000 }}>
                    <div style={{ background: 'rgba(0,0,0,0.4)', position: 'absolute', inset: 0 }} />
                    <div style={{ background: '#fff', padding: 24, borderRadius: 12, zIndex: 2001, minWidth: 320 }}>
                        <p style={{ fontWeight: 600, marginBottom: 12 }}>Potvrdi kreiranje novog dobavljača</p>
                        <p style={{ marginBottom: 16 }}>Da li želite da kreirate dobavljača: <strong>{newDob}</strong>?</p>
                        <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
                            <button className="button-big" style={{ background: '#e5e7eb', color: '#111', width: 'auto', padding: '8px 12px' }} onClick={() => setShowNewDobConfirm(false)}>Otkaži</button>
                            <button className="button-big" style={{ width: 'auto', padding: '8px 12px' }} onClick={handleCreateDob}>Potvrdi</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}