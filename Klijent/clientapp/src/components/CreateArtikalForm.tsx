import { ArtikalFormData } from "../types/artikalformdata";
import { createTipObuce } from "../services/tipoviObuceApi";
import { createDobavljac } from "../services/dobavljaciApi";
import { getSezone } from "../services/sezoneApi";
import { uploadImage, deleteProductImage, getImageUrl } from "../services/uploadApi";
import type { Sezona } from "../types/Sezona";
import React, { useState, useEffect, useRef } from "react";
import Modal from "./Modal";
import ImageUpload from "./ImageUpload";
import { useToast } from "./Toast";

export interface CreateArtikalFormProps {
    tipoviObuce: { id: number; naziv: string }[];
    dobavljaci: { id: number; naziv: string }[];
    onSubmit: (data: ArtikalFormData) => Promise<number | void>;
    initialData?: ArtikalFormData;
    mode?: "create" | "edit";
    artikalId?: number; // NEW: For image upload
    currentImagePath?: string | null; // NEW: Current image
    onImageChange?: (imagePath: string | null) => void; // NEW: Callback when image changes
}

export default function CreateArtikalForm({
    tipoviObuce,
    dobavljaci,
    onSubmit,
    initialData,
    mode = "create",
    artikalId,
    currentImagePath: initialImagePath,
    onImageChange,
}: CreateArtikalFormProps) {
    const toast = useToast();
    const [showAdvanced, setShowAdvanced] = useState(false);
    const [sezone, setSezone] = useState<Sezona[]>([]);
    const nazivRef = useRef<HTMLInputElement>(null);

    // Image state
    const [currentImagePath, setCurrentImagePath] = useState<string | null>(initialImagePath ?? null);
    const [uploading, setUploading] = useState(false);

    // Sync image path from props
    useEffect(() => {
        setCurrentImagePath(initialImagePath ?? null);
    }, [initialImagePath]);

    // Local copies of tipoviObuce and dobavljaci for inline additions
    const [localTipoviObuce, setLocalTipoviObuce] = useState(tipoviObuce);
    const [localDobavljaci, setLocalDobavljaci] = useState(dobavljaci);

    useEffect(() => {
        setLocalTipoviObuce(tipoviObuce);
    }, [tipoviObuce]);

    useEffect(() => {
        setLocalDobavljaci(dobavljaci);
    }, [dobavljaci]);

    useEffect(() => {
        const loadSezone = async () => {
            try {
                const data = await getSezone();
                setSezone(data);
            } catch (e) {
                console.error("Failed to load sezone:", e);
            }
        };
        loadSezone();
    }, []);

    // Form state
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
    const [selectedSezona, setSelectedSezona] = useState<number | null>(initialData?.idSezona ?? null);
    
    const [isSubmitting, setIsSubmitting] = useState(false);

    // Validation state
    const [errors, setErrors] = useState<Record<string, string>>({});

    // Modal state for adding new tip/dobavljač
    const [showNewTipModal, setShowNewTipModal] = useState(false);
    const [showNewDobModal, setShowNewDobModal] = useState(false);
    const [newTip, setNewTip] = useState("");
    const [newDob, setNewDob] = useState("");
    const [isCreatingTip, setIsCreatingTip] = useState(false);
    const [isCreatingDob, setIsCreatingDob] = useState(false);

    // Real-time validation
    const validateField = (field: string, value: string): string | null => {
        switch (field) {
            case "naziv":
                if (!value.trim()) return "Naziv je obavezan";
                if (value.length < 2) return "Naziv mora imati minimum 2 karaktera";
                return null;
            case "prodajnaCena": {
                if (!value) return "Prodajna cena je obavezna";
                const cena = Number(value);
                if (isNaN(cena) || cena <= 0) return "Cena mora biti veća od 0";
                return null;
            }
            case "kolicina":
                if (value && isNaN(Number(value))) return "Količina mora biti broj";
                return null;
            default:
                return null;
        }
    };

    const handleFieldBlur = (field: string, value: string) => {
        const error = validateField(field, value);
        setErrors((prev) => ({
            ...prev,
            [field]: error || "",
        }));
    };

    const handleCreateTip = async () => {
        if (!newTip.trim()) return;
        
        setIsCreatingTip(true);
        try {
            const idTip = await createTipObuce(newTip.trim());
            const newTipObj = { id: idTip, naziv: newTip.trim() };
            setLocalTipoviObuce((prev) => [...prev, newTipObj]);
            setSelectedTip(idTip);
            setNewTip("");
            setShowNewTipModal(false);
            toast.success(`Tip obuće "${newTipObj.naziv}" uspešno kreiran!`);
        } catch (e) {
            console.error(e);
            toast.error("Greška pri kreiranju tipa obuće");
        } finally {
            setIsCreatingTip(false);
        }
    };

    const handleCreateDob = async () => {
        if (!newDob.trim()) return;
        
        setIsCreatingDob(true);
        try {
            const idDob = await createDobavljac(newDob.trim());
            const newDobObj = { id: idDob, naziv: newDob.trim() };
            setLocalDobavljaci((prev) => [...prev, newDobObj]);
            setSelectedDobavljac(idDob);
            setNewDob("");
            setShowNewDobModal(false);
            toast.success(`Dobavljač "${newDobObj.naziv}" uspešno kreiran!`);
        } catch (e) {
            console.error(e);
            toast.error("Greška pri kreiranju dobavljača");
        } finally {
            setIsCreatingDob(false);
        }
    };

    const validateForm = (): boolean => {
        const newErrors: Record<string, string> = {};

        const nazivError = validateField("naziv", naziv);
        if (nazivError) newErrors.naziv = nazivError;

        const cenaError = validateField("prodajnaCena", prodajnaCena);
        if (cenaError) newErrors.prodajnaCena = cenaError;

        const kolicinaError = validateField("kolicina", kolicina);
        if (kolicinaError) newErrors.kolicina = kolicinaError;

        setErrors(newErrors);
        return Object.keys(newErrors).length === 0;
    };

    const handleSubmit = async (e?: React.FormEvent) => {
        e?.preventDefault();

        if (!validateForm()) {
            toast.error("Molimo popunite sva obavezna polja ispravno");
            return;
        }

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
            idSezona: selectedSezona,
        };

        setIsSubmitting(true);
        try {
            await onSubmit(formData);
            toast.success(
                mode === "edit" 
                    ? "Artikal uspešno izmenjen! ✅" 
                    : "Artikal uspešno kreiran! ✅"
            );

            if (mode === "create") {
                // Reset form
                setNaziv("");
                setProdajnaCena("");
                setNabavnaCena("");
                setNabavnaCenaDin("");
                setPrvaProdajnaCena("");
                setKolicina("");
                setKomentar("");
                setSelectedTip(null);
                setSelectedDobavljac(null);
                setSelectedSezona(null);
                setErrors({});
                
                // Focus back to naziv
                nazivRef.current?.focus();
            }
        } catch (e: unknown) {
            const msg = e instanceof Error ? e.message : "Greška pri kreiranju artikla";
            toast.error(msg);
            console.error(e);
        } finally {
            setIsSubmitting(false);
            setUploading(false);
        }
    };

    // Keyboard shortcuts
    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            // Ctrl/Cmd + Enter to submit
            if ((e.ctrlKey || e.metaKey) && e.key === "Enter") {
                e.preventDefault();
                handleSubmit();
            }
            // Ctrl/Cmd + Shift + A to toggle advanced
            if ((e.ctrlKey || e.metaKey) && e.shiftKey && e.key === "A") {
                e.preventDefault();
                setShowAdvanced((prev) => !prev);
            }
        };

        document.addEventListener("keydown", handleKeyDown);
        return () => document.removeEventListener("keydown", handleKeyDown);
    }, [naziv, prodajnaCena, nabavnaCena, kolicina, selectedTip, selectedDobavljac]);

    // Image upload handlers
    const handleImageUpload = async (formData: FormData) => {
        if (!artikalId || mode === "create") {
            toast.error("Morate prvo sačuvati artikal pre dodavanja slike");
            return;
        }

        setUploading(true);
        try {
            const result = await uploadImage(formData, artikalId);
            setCurrentImagePath(result.fileName);
            onImageChange?.(result.fileName);
            toast.success("Slika uspešno otpremljena! 📸");
        } catch (error) {
            console.error("Upload error:", error);
            toast.error("Greška pri otpremanju slike");
        } finally {
            setUploading(false);
        }
    };

    const handleDeleteImage = async () => {
        if (!artikalId || mode === "create") {
            toast.error("Nema slike za brisanje");
            return;
        }

        if (!window.confirm("Da li ste sigurni da želite da obrišete sliku?")) {
            return;
        }

        try {
            await deleteProductImage(artikalId);
            setCurrentImagePath(null);
            onImageChange?.(null);
            toast.success("Slika obrisana! 🗑️");
        } catch (error) {
            console.error("Delete error:", error);
            toast.error("Greška pri brisanju slike");
        }
    };

    return (
        <div className="card bg-surface border border-border shadow-sm">
            <div className="flex justify-between items-center mb-6">
                <h2 className="text-2xl font-semibold text-foreground m-0">
                    {mode === "edit" ? "✏️ Izmeni artikal" : "➕ Kreiraj novi artikal"}
                </h2>
                <button
                    type="button"
                    onClick={() => setShowAdvanced(!showAdvanced)}
                    className={`px-4 py-2 rounded-xl text-sm font-semibold transition ${showAdvanced ? 'bg-primary text-white shadow-sm' : 'bg-surface-elevated border border-border text-muted hover:opacity-90'}`}
                    title="Ctrl+Shift+A"
                >
                    {showAdvanced ? "🔽 Sakrij dodatna polja" : "🔼 Prikaži dodatna polja"}
                </button>
            </div>

            <form onSubmit={handleSubmit}>
                {/* OSNOVNI PODACI */}
                <div className="mb-8 bg-surface p-6 rounded-lg border border-border shadow-sm">
                    <h3 className="text-lg font-semibold mb-5 text-muted flex items-center gap-2">
                        <span className="bg-gradient-to-r from-blue-500 to-indigo-600 text-white w-8 h-8 rounded-lg flex items-center justify-center text-base">📦</span>
                        Osnovni podaci
                    </h3>
                    <div className="form-grid">
                        <div>
                            <label className="field-label font-semibold text-foreground">
                                Naziv <span className="text-accent-error">*</span>
                            </label>
                            <input
                                ref={nazivRef}
                                className="input-big"
                                placeholder="npr. Patike Nike Air Max"
                                value={naziv}
                                onChange={(e) => setNaziv(e.target.value)}
                                onBlur={(e) => handleFieldBlur("naziv", e.target.value)}
                                style={{ borderColor: errors.naziv ? 'var(--error)' : 'var(--border-default)' }}
                            />
                            {errors.naziv && (
                                <p className="text-accent-error text-sm mt-2 flex items-center gap-1">⚠️ {errors.naziv}</p>
                            )}
                        </div>

                        <div>
                            <label className="field-label font-semibold text-foreground">
                                Prodajna cena (RSD) <span className="text-accent-error">*</span>
                            </label>
                            <input
                                className="input-big"
                                placeholder="0.00"
                                type="number"
                                step="0.01"
                                value={prodajnaCena}
                                onChange={(e) => setProdajnaCena(e.target.value)}
                                onBlur={(e) => handleFieldBlur("prodajnaCena", e.target.value)}
                                style={{ borderColor: errors.prodajnaCena ? 'var(--error)' : 'var(--border-default)' }}
                            />
                            {errors.prodajnaCena && (
                                <p className="text-accent-error text-sm mt-2 flex items-center gap-1">⚠️ {errors.prodajnaCena}</p>
                            )}
                        </div>

                        <div>
                            <label className="field-label font-semibold text-foreground">Količina</label>
                            <input
                                className="input-big"
                                placeholder="0"
                                type="number"
                                value={kolicina}
                                onChange={(e) => setKolicina(e.target.value)}
                                onBlur={(e) => handleFieldBlur("kolicina", e.target.value)}
                                style={{ borderColor: errors.kolicina ? 'var(--error)' : 'var(--border-default)' }}
                            />
                            {errors.kolicina && (
                                <p className="text-accent-error text-sm mt-2">⚠️ {errors.kolicina}</p>
                            )}
                        </div>

                        <div>
                            <label className="field-label font-semibold text-foreground">
                                <span className="flex items-center gap-2">👟 Tip obuće</span>
                            </label>
                            <div style={{ display: "flex", gap: "8px" }}>
                                <select
                                    className="input-big"
                                    value={selectedTip ?? ""}
                                    onChange={(e) => setSelectedTip(e.target.value ? Number(e.target.value) : null)}
                                    style={{ flex: 1, marginBottom: 0, borderColor: selectedTip ? 'var(--focus-ring)' : 'var(--border-default)' }}
                                >
                                    <option value="">-- izaberite --</option>
                                    {localTipoviObuce.map((t) => (
                                        <option key={t.id} value={t.id}>{t.naziv}</option>
                                    ))}
                                </select>
                                <button
                                    type="button"
                                    onClick={() => setShowNewTipModal(true)}
                                    className="px-3.5 py-1.5 rounded-lg text-xl font-bold text-white bg-gradient-to-r from-cyan-500 to-teal-400 shadow-sm transform transition-transform hover:scale-105"
                                    title="Dodaj novi tip obuće"
                                >
                                    +
                                </button>
                            </div>
                        </div>

                        <div>
                            <label className="field-label font-semibold text-foreground">
                                <span className="flex items-center gap-2">🏢 Dobavljač</span>
                            </label>
                            <div style={{ display: "flex", gap: "8px" }}>
                                <select
                                    className="input-big"
                                    value={selectedDobavljac ?? ""}
                                    onChange={(e) => setSelectedDobavljac(e.target.value ? Number(e.target.value) : null)}
                                    style={{ flex: 1, marginBottom: 0, borderColor: selectedDobavljac ? 'var(--success)' : 'var(--border-default)' }}
                                >
                                    <option value="">-- izaberite --</option>
                                    {localDobavljaci.map((d) => (
                                        <option key={d.id} value={d.id}>{d.naziv}</option>
                                    ))}
                                </select>
                                <button
                                    type="button"
                                    onClick={() => setShowNewDobModal(true)}
                                    className="px-3.5 py-1.5 rounded-lg text-xl font-bold text-white bg-gradient-to-r from-green-500 to-emerald-500 shadow-sm transform transition-transform hover:scale-105"
                                    title="Dodaj novog dobavljača"
                                >
                                    +
                                </button>
                            </div>
                        </div>
                    </div>
                </div>

                {/* DODATNI PODACI (conditional) */}
                {showAdvanced && (
                    <div className="mb-8 bg-accent-warning/10 p-6 rounded-lg border border-accent-warning shadow-sm">
                        <h3 className="text-lg font-semibold mb-5 text-accent-warning flex items-center gap-2">
                            <span className="bg-accent-warning text-white w-8 h-8 rounded-lg flex items-center justify-center text-base">📊</span>
                            Dodatni podaci
                        </h3>
                        <div className="form-grid">
                            <div>
                                <label className="field-label font-semibold text-accent-warning">Nabavna cena</label>
                                <input
                                    className="input-big"
                                    placeholder="0.00"
                                    type="number"
                                    step="0.01"
                                    value={nabavnaCena}
                                    onChange={(e) => setNabavnaCena(e.target.value)}
                                    style={{ background: 'var(--surface-card, var(--theme-color-ffffff, #ffffff))', boxShadow: 'var(--box-shadow-xs)', borderColor: 'var(--warning)' }}
                                />
                            </div>

                            <div>
                                <label className="field-label font-semibold text-accent-warning">Nabavna cena (din)</label>
                                <input
                                    className="input-big"
                                    placeholder="0.00"
                                    type="number"
                                    step="0.01"
                                    value={nabavnaCenaDin}
                                    onChange={(e) => setNabavnaCenaDin(e.target.value)}
                                    style={{ background: 'var(--surface-card, var(--theme-color-ffffff, #ffffff))', boxShadow: 'var(--box-shadow-xs)', borderColor: 'var(--warning)' }}
                                />
                            </div>

                            <div>
                                <label className="field-label font-semibold text-accent-warning">Prva prodajna cena</label>
                                <input
                                    className="input-big"
                                    placeholder="0.00"
                                    type="number"
                                    step="0.01"
                                    value={prvaProdajnaCena}
                                    onChange={(e) => setPrvaProdajnaCena(e.target.value)}
                                    style={{ background: 'var(--surface-card, var(--theme-color-ffffff, #ffffff))', boxShadow: 'var(--box-shadow-xs)', borderColor: 'var(--warning)' }}
                                />
                            </div>

                            <div>
                                <label className="field-label font-semibold text-accent-warning">Sezona</label>
                                <select
                                    className="input-big"
                                    value={selectedSezona ?? ""}
                                    onChange={(e) => setSelectedSezona(e.target.value ? Number(e.target.value) : null)}
                                    style={{ background: 'var(--surface-card, var(--theme-color-ffffff, #ffffff))', boxShadow: 'var(--box-shadow-xs)', borderColor: 'var(--warning)' }}
                                >
                                    <option value="">-- izaberite sezonu --</option>
                                    {sezone.map((s) => (
                                        <option key={s.id} value={s.id}>{s.naziv}</option>
                                    ))}
                                </select>
                            </div>

                            <div className="form-full">
                                <label className="field-label font-semibold text-accent-warning">Komentar</label>
                                <textarea
                                    className="input-big"
                                    placeholder="Dodatne napomene..."
                                    value={komentar}
                                    onChange={(e) => setKomentar(e.target.value)}
                                    rows={3}
                                    style={{ background: 'var(--surface-card, var(--theme-color-ffffff, #ffffff))', boxShadow: 'var(--box-shadow-xs)', borderColor: 'var(--warning)' }}
                                />
                            </div>

                            {/* IMAGE UPLOAD SECTION - Only in edit mode */}
                            {mode === "edit" && artikalId && (
                                <div className="form-full mt-4">
                                        <div className="bg-white p-6 rounded-lg border-2" style={{ borderColor: 'var(--warning)' }}>
                                        <h4 className="text-base font-semibold mb-4 text-accent-warning flex items-center gap-2">
                                            <span>📸</span>
                                            Slika artikla
                                        </h4>

                                        {/* Current Image Display */}
                                        {currentImagePath && (
                                            <div style={{ marginBottom: "1.5rem" }}>
                                                <div style={{
                                                    display: "flex",
                                                    justifyContent: "space-between",
                                                    alignItems: "center",
                                                    marginBottom: "0.75rem",
                                                }}>
                                                    <span className="text-sm font-semibold text-accent-warning">Trenutna slika</span>
                                                    <button type="button" onClick={handleDeleteImage} className="px-3 py-1 rounded-md text-white bg-accent-error font-semibold">🗑️ Obriši</button>
                                                </div>
                                                <img src={getImageUrl(currentImagePath) || ""} alt="Product" className="max-w-full max-h-[300px] rounded-lg border border-border object-contain block mx-auto" />
                                            </div>
                                        )}

                                        {/* Upload Component */}
                                        <ImageUpload
                                            onUpload={handleImageUpload}
                                            label={currentImagePath ? "Promeni sliku" : "Dodaj sliku"}
                                            buttonText={currentImagePath ? "📷 Promeni" : "📷 Dodaj sliku"}
                                            showPreview={true}
                                        />

                                        {uploading && (
                                            <div className="mt-4 p-3 rounded-md border border-primary bg-primary/10 text-primary font-semibold text-sm text-center">⏳ Otpremam sliku...</div>
                                        )}
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                )}

                {/* SUBMIT BUTTON */}
                <div className="flex gap-3 items-center flex-wrap">
                    <button
                        type="submit"
                        className={`button-big ${isSubmitting ? 'opacity-60 cursor-not-allowed bg-gray-400' : 'bg-accent-success text-white'}`}
                        disabled={isSubmitting}
                        style={{ maxWidth: '420px' }}
                    >
                        {isSubmitting 
                            ? "⏳ Čuvam..." 
                            : mode === "edit" 
                                ? "💾 Sačuvaj izmene" 
                                : "✅ Kreiraj artikal"}
                    </button>
                    <span className="text-sm text-muted bg-surface p-2.5 rounded-md border border-border flex items-center gap-2">
                        💡 <kbd className="bg-white px-2 py-0.5 rounded border border-border text-xs font-mono">Ctrl</kbd> + <kbd className="bg-white px-2 py-0.5 rounded border border-border text-xs font-mono">Enter</kbd> za brzo čuvanje
                    </span>
                </div>
            </form>

            {/* MODAL: Novi tip obuće */}
            <Modal
                isOpen={showNewTipModal}
                onClose={() => setShowNewTipModal(false)}
                title="Dodaj novi tip obuće"
                size="sm"
                footer={
                    <>
                        <button
                            className="button-big bg-surface-elevated text-foreground border border-border"
                            onClick={() => setShowNewTipModal(false)}
                        >
                            Otkaži
                        </button>
                        <button
                                className="button-big bg-gradient-to-r from-cyan-500 to-teal-400 text-white shadow-sm"
                            onClick={handleCreateTip}
                            disabled={!newTip.trim() || isCreatingTip}
                        >
                            {isCreatingTip ? "Kreiram..." : "Potvrdi"}
                        </button>
                    </>
                }
            >
                <div>
                    <label className="field-label font-semibold">Naziv tipa</label>
                    <input
                        className="input-big"
                        placeholder="npr. Patike"
                        value={newTip}
                        onChange={(e) => setNewTip(e.target.value)}
                        autoFocus
                        onKeyDown={(e) => {
                            if (e.key === "Enter" && newTip.trim()) {
                                e.preventDefault();
                                handleCreateTip();
                            }
                        }}
                        style={{ boxShadow: "var(--box-shadow-xs, 0 1px 2px var(--theme-color-rgba-0-0-0-0p05, rgba(0,0,0,0.05)))" }}
                    />
                </div>
            </Modal>

            {/* MODAL: Novi dobavljač */}
            <Modal
                isOpen={showNewDobModal}
                onClose={() => setShowNewDobModal(false)}
                title="Dodaj novog dobavljača"
                size="sm"
                footer={
                    <>
                        <button className="button-big bg-surface-elevated text-foreground border border-border" onClick={() => setShowNewDobModal(false)}>Otkaži</button>
                        <button className="button-big bg-gradient-to-r from-green-500 to-emerald-500 text-white shadow-sm" onClick={handleCreateDob} disabled={!newDob.trim() || isCreatingDob}>{isCreatingDob ? "Kreiram..." : "Potvrdi"}</button>
                    </>
                }
            >
                <div>
                    <label className="field-label font-semibold">Naziv dobavljača</label>
                    <input
                        className="input-big"
                        placeholder="npr. Nike"
                        value={newDob}
                        onChange={(e) => setNewDob(e.target.value)}
                        autoFocus
                        onKeyDown={(e) => {
                            if (e.key === "Enter" && newDob.trim()) {
                                e.preventDefault();
                                handleCreateDob();
                            }
                        }}
                        style={{ boxShadow: "var(--box-shadow-xs, 0 1px 2px var(--theme-color-rgba-0-0-0-0p05, rgba(0,0,0,0.05)))" }}
                    />
                </div>
            </Modal>
        </div>
    );
}
