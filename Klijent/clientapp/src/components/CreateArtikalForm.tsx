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
        <div 
            className="card" 
            style={{
                background: "linear-gradient(to bottom, #ffffff, #fafbfc)",
                boxShadow: "0 10px 25px -5px rgba(0,0,0,0.1), 0 8px 10px -6px rgba(0,0,0,0.1)",
                border: "1px solid #e5e7eb"
            }}
        >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1.5rem" }}>
                <h2 className="text-2xl font-semibold" style={{ margin: 0, color: "#1f2937" }}>
                    {mode === "edit" ? "✏️ Izmeni artikal" : "➕ Kreiraj novi artikal"}
                </h2>
                <button
                    type="button"
                    onClick={() => setShowAdvanced(!showAdvanced)}
                    style={{
                        background: showAdvanced 
                            ? "linear-gradient(135deg, #0891b2 0%, #06b6d4 100%)" 
                            : "linear-gradient(135deg, #f3f4f6 0%, #e5e7eb 100%)",
                        color: showAdvanced ? "white" : "#374151",
                        padding: "10px 18px",
                        borderRadius: "10px",
                        border: showAdvanced ? "none" : "1px solid #d1d5db",
                        fontSize: "0.875rem",
                        fontWeight: 600,
                        cursor: "pointer",
                        boxShadow: showAdvanced 
                            ? "0 4px 6px -1px rgba(8,145,178,0.3)" 
                            : "0 1px 3px rgba(0,0,0,0.1)",
                        transition: "all 0.2s ease",
                    }}
                    title="Ctrl+Shift+A"
                >
                    {showAdvanced ? "🔽 Sakrij dodatna polja" : "🔼 Prikaži dodatna polja"}
                </button>
            </div>

            <form onSubmit={handleSubmit}>
                {/* OSNOVNI PODACI */}
                <div 
                    style={{ 
                        marginBottom: "2rem",
                        background: "#ffffff",
                        padding: "1.5rem",
                        borderRadius: "12px",
                        border: "1px solid #e5e7eb",
                        boxShadow: "0 2px 8px rgba(0,0,0,0.08)"
                    }}
                >
                    <h3 style={{ 
                        fontSize: "1.125rem", 
                        fontWeight: 600, 
                        marginBottom: "1.25rem", 
                        color: "#374151",
                        display: "flex",
                        alignItems: "center",
                        gap: "8px"
                    }}>
                        <span style={{
                            background: "linear-gradient(135deg, #3b82f6 0%, #2563eb 100%)",
                            color: "white",
                            width: "32px",
                            height: "32px",
                            borderRadius: "8px",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            fontSize: "1rem"
                        }}>📦</span>
                        Osnovni podaci
                    </h3>
                    <div className="form-grid">
                        <div>
                            <label className="field-label" style={{ fontWeight: 600, color: "#374151" }}>
                                Naziv <span style={{ color: "#ef4444" }}>*</span>
                            </label>
                            <input
                                ref={nazivRef}
                                className="input-big"
                                placeholder="npr. Patike Nike Air Max"
                                value={naziv}
                                onChange={(e) => setNaziv(e.target.value)}
                                onBlur={(e) => handleFieldBlur("naziv", e.target.value)}
                                style={{
                                    borderColor: errors.naziv ? "#ef4444" : "#d1d5db",
                                    boxShadow: errors.naziv 
                                        ? "0 0 0 3px rgba(239,68,68,0.15)" 
                                        : "0 1px 3px rgba(0,0,0,0.1)",
                                    transition: "all 0.2s ease"
                                }}
                                onFocus={(e) => e.currentTarget.style.boxShadow = "0 0 0 3px rgba(59,130,246,0.15)"}
                            />
                            {errors.naziv && (
                                <p style={{ 
                                    color: "#ef4444", 
                                    fontSize: "0.875rem", 
                                    marginTop: "0.5rem",
                                    display: "flex",
                                    alignItems: "center",
                                    gap: "4px"
                                }}>
                                    ⚠️ {errors.naziv}
                                </p>
                            )}
                        </div>

                        <div>
                            <label className="field-label" style={{ fontWeight: 600, color: "#374151" }}>
                                Prodajna cena (RSD) <span style={{ color: "#ef4444" }}>*</span>
                            </label>
                            <input
                                className="input-big"
                                placeholder="0.00"
                                type="number"
                                step="0.01"
                                value={prodajnaCena}
                                onChange={(e) => setProdajnaCena(e.target.value)}
                                onBlur={(e) => handleFieldBlur("prodajnaCena", e.target.value)}
                                style={{ 
                                    borderColor: errors.prodajnaCena ? "#ef4444" : "#d1d5db",
                                    boxShadow: errors.prodajnaCena 
                                        ? "0 0 0 3px rgba(239,68,68,0.15)" 
                                        : "0 1px 3px rgba(0,0,0,0.1)",
                                    transition: "all 0.2s ease"
                                }}
                                onFocus={(e) => e.currentTarget.style.boxShadow = "0 0 0 3px rgba(16,185,129,0.15)"}
                            />
                            {errors.prodajnaCena && (
                                <p style={{ 
                                    color: "#ef4444", 
                                    fontSize: "0.875rem", 
                                    marginTop: "0.5rem",
                                    display: "flex",
                                    alignItems: "center",
                                    gap: "4px"
                                }}>
                                    ⚠️ {errors.prodajnaCena}
                                </p>
                            )}
                        </div>

                        <div>
                            <label className="field-label" style={{ fontWeight: 600, color: "#374151" }}>Količina</label>
                            <input
                                className="input-big"
                                placeholder="0"
                                type="number"
                                value={kolicina}
                                onChange={(e) => setKolicina(e.target.value)}
                                onBlur={(e) => handleFieldBlur("kolicina", e.target.value)}
                                style={{ 
                                    borderColor: errors.kolicina ? "#ef4444" : "#d1d5db",
                                    boxShadow: "0 1px 3px rgba(0,0,0,0.1)",
                                    transition: "all 0.2s ease"
                                }}
                                onFocus={(e) => e.currentTarget.style.boxShadow = "0 0 0 3px rgba(139,92,246,0.15)"}
                            />
                            {errors.kolicina && (
                                <p style={{ color: "#ef4444", fontSize: "0.875rem", marginTop: "0.5rem" }}>
                                    ⚠️ {errors.kolicina}
                                </p>
                            )}
                        </div>

                        <div>
                            <label className="field-label" style={{ fontWeight: 600, color: "#374151" }}>
                                <span style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                                    👟 Tip obuće
                                </span>
                            </label>
                            <div style={{ display: "flex", gap: "8px" }}>
                                <select
                                    className="input-big"
                                    value={selectedTip ?? ""}
                                    onChange={(e) => setSelectedTip(e.target.value ? Number(e.target.value) : null)}
                                    style={{ 
                                        flex: 1, 
                                        marginBottom: 0,
                                        boxShadow: "0 1px 3px rgba(0,0,0,0.1)",
                                        borderColor: selectedTip ? "#0891b2" : "#d1d5db",
                                        background: selectedTip ? "linear-gradient(to right, #ffffff, #ecfeff)" : "white",
                                        transition: "all 0.2s ease"
                                    }}
                                >
                                    <option value="">-- izaberite --</option>
                                    {localTipoviObuce.map((t) => (
                                        <option key={t.id} value={t.id}>{t.naziv}</option>
                                    ))}
                                </select>
                                <button
                                    type="button"
                                    onClick={() => setShowNewTipModal(true)}
                                    style={{
                                        background: "linear-gradient(135deg, #0891b2 0%, #06b6d4 100%)",
                                        color: "white",
                                        padding: "10px 16px",
                                        borderRadius: "10px",
                                        border: "none",
                                        fontSize: "1.5rem",
                                        cursor: "pointer",
                                        lineHeight: 1,
                                        boxShadow: "0 4px 10px rgba(8,145,178,0.4)",
                                        transition: "all 0.2s ease",
                                    }}
                                    title="Dodaj novi tip obuće"
                                    onMouseEnter={(e) => e.currentTarget.style.transform = "scale(1.05)"}
                                    onMouseLeave={(e) => e.currentTarget.style.transform = "scale(1)"}
                                >
                                    +
                                </button>
                            </div>
                        </div>

                        <div>
                            <label className="field-label" style={{ fontWeight: 600, color: "#374151" }}>
                                <span style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                                    🏢 Dobavljač
                                </span>
                            </label>
                            <div style={{ display: "flex", gap: "8px" }}>
                                <select
                                    className="input-big"
                                    value={selectedDobavljac ?? ""}
                                    onChange={(e) => setSelectedDobavljac(e.target.value ? Number(e.target.value) : null)}
                                    style={{ 
                                        flex: 1, 
                                        marginBottom: 0,
                                        boxShadow: "0 1px 3px rgba(0,0,0,0.1)",
                                        borderColor: selectedDobavljac ? "#059669" : "#d1d5db",
                                        background: selectedDobavljac ? "linear-gradient(to right, #ffffff, #ecfdf5)" : "white",
                                        transition: "all 0.2s ease"
                                    }}
                                >
                                    <option value="">-- izaberite --</option>
                                    {localDobavljaci.map((d) => (
                                        <option key={d.id} value={d.id}>{d.naziv}</option>
                                    ))}
                                </select>
                                <button
                                    type="button"
                                    onClick={() => setShowNewDobModal(true)}
                                    style={{
                                        background: "linear-gradient(135deg, #059669 0%, #10b981 100%)",
                                        color: "white",
                                        padding: "10px 16px",
                                        borderRadius: "10px",
                                        border: "none",
                                        fontSize: "1.5rem",
                                        cursor: "pointer",
                                        lineHeight: 1,
                                        boxShadow: "0 4px 10px rgba(5,150,105,0.4)",
                                        transition: "all 0.2s ease",
                                    }}
                                    title="Dodaj novog dobavljača"
                                    onMouseEnter={(e) => e.currentTarget.style.transform = "scale(1.05)"}
                                    onMouseLeave={(e) => e.currentTarget.style.transform = "scale(1)"}
                                >
                                    +
                                </button>
                            </div>
                        </div>
                    </div>
                </div>

                {/* DODATNI PODACI (conditional) */}
                {showAdvanced && (
                    <div 
                        style={{ 
                            marginBottom: "2rem",
                            background: "linear-gradient(135deg, #fef3c7 0%, #fde68a 100%)",
                            padding: "1.5rem",
                            borderRadius: "12px",
                            border: "1px solid #fbbf24",
                            boxShadow: "0 4px 12px rgba(251,191,36,0.25)"
                        }}
                    >
                        <h3 style={{ 
                            fontSize: "1.125rem", 
                            fontWeight: 600, 
                            marginBottom: "1.25rem", 
                            color: "#78350f",
                            display: "flex",
                            alignItems: "center",
                            gap: "8px"
                        }}>
                            <span style={{
                                background: "linear-gradient(135deg, #f59e0b 0%, #d97706 100%)",
                                color: "white",
                                width: "32px",
                                height: "32px",
                                borderRadius: "8px",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                fontSize: "1rem"
                            }}>📊</span>
                            Dodatni podaci
                        </h3>
                        <div className="form-grid">
                            <div>
                                <label className="field-label" style={{ fontWeight: 600, color: "#78350f" }}>Nabavna cena</label>
                                <input
                                    className="input-big"
                                    placeholder="0.00"
                                    type="number"
                                    step="0.01"
                                    value={nabavnaCena}
                                    onChange={(e) => setNabavnaCena(e.target.value)}
                                    style={{ 
                                        background: "white",
                                        boxShadow: "0 1px 3px rgba(0,0,0,0.1)",
                                        borderColor: "#fbbf24",
                                        transition: "all 0.2s ease"
                                    }}
                                />
                            </div>

                            <div>
                                <label className="field-label" style={{ fontWeight: 600, color: "#78350f" }}>Nabavna cena (din)</label>
                                <input
                                    className="input-big"
                                    placeholder="0.00"
                                    type="number"
                                    step="0.01"
                                    value={nabavnaCenaDin}
                                    onChange={(e) => setNabavnaCenaDin(e.target.value)}
                                    style={{ 
                                        background: "white",
                                        boxShadow: "0 1px 3px rgba(0,0,0,0.1)",
                                        borderColor: "#fbbf24",
                                        transition: "all 0.2s ease"
                                    }}
                                />
                            </div>

                            <div>
                                <label className="field-label" style={{ fontWeight: 600, color: "#78350f" }}>Prva prodajna cena</label>
                                <input
                                    className="input-big"
                                    placeholder="0.00"
                                    type="number"
                                    step="0.01"
                                    value={prvaProdajnaCena}
                                    onChange={(e) => setPrvaProdajnaCena(e.target.value)}
                                    style={{ 
                                        background: "white",
                                        boxShadow: "0 1px 3px rgba(0,0,0,0.1)",
                                        borderColor: "#fbbf24",
                                        transition: "all 0.2s ease"
                                    }}
                                />
                            </div>

                            <div>
                                <label className="field-label" style={{ fontWeight: 600, color: "#78350f" }}>Sezona</label>
                                <select
                                    className="input-big"
                                    value={selectedSezona ?? ""}
                                    onChange={(e) => setSelectedSezona(e.target.value ? Number(e.target.value) : null)}
                                    style={{ 
                                        background: "white",
                                        boxShadow: "0 1px 3px rgba(0,0,0,0.1)",
                                        borderColor: "#fbbf24",
                                        transition: "all 0.2s ease"
                                    }}
                                >
                                    <option value="">-- izaberite sezonu --</option>
                                    {sezone.map((s) => (
                                        <option key={s.id} value={s.id}>{s.naziv}</option>
                                    ))}
                                </select>
                            </div>

                            <div className="form-full">
                                <label className="field-label" style={{ fontWeight: 600, color: "#78350f" }}>Komentar</label>
                                <textarea
                                    className="input-big"
                                    placeholder="Dodatne napomene..."
                                    value={komentar}
                                    onChange={(e) => setKomentar(e.target.value)}
                                    rows={3}
                                    style={{ 
                                        background: "white",
                                        boxShadow: "0 1px 2px rgba(0,0,0,0.05)",
                                        borderColor: "#fbbf24"
                                    }}
                                />
                            </div>

                            {/* IMAGE UPLOAD SECTION - Only in edit mode */}
                            {mode === "edit" && artikalId && (
                                <div className="form-full" style={{ marginTop: "1rem" }}>
                                    <div style={{
                                        background: "white",
                                        padding: "1.5rem",
                                        borderRadius: "12px",
                                        border: "2px solid #fbbf24",
                                    }}>
                                        <h4 style={{ 
                                            fontSize: "1rem", 
                                            fontWeight: 600, 
                                            marginBottom: "1rem", 
                                            color: "#78350f",
                                            display: "flex",
                                            alignItems: "center",
                                            gap: "8px"
                                        }}>
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
                                                    <span style={{ fontSize: "0.875rem", color: "#78350f", fontWeight: 600 }}>
                                                        Trenutna slika
                                                    </span>
                                                    <button
                                                        type="button"
                                                        onClick={handleDeleteImage}
                                                        style={{
                                                            background: "linear-gradient(135deg, #dc2626 0%, #b91c1c 100%)",
                                                            color: "white",
                                                            padding: "6px 12px",
                                                            borderRadius: "6px",
                                                            border: "none",
                                                            cursor: "pointer",
                                                            fontSize: "0.75rem",
                                                            fontWeight: 600,
                                                        }}
                                                    >
                                                        🗑️ Obriši
                                                    </button>
                                                </div>
                                                <img
                                                    src={getImageUrl(currentImagePath) || ""}
                                                    alt="Product"
                                                    style={{
                                                        maxWidth: "100%",
                                                        maxHeight: "300px",
                                                        borderRadius: "8px",
                                                        border: "1px solid #d1d5db",
                                                        objectFit: "contain",
                                                        display: "block",
                                                        margin: "0 auto",
                                                    }}
                                                />
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
                                            <div style={{
                                                marginTop: "1rem",
                                                padding: "0.75rem",
                                                background: "#eff6ff",
                                                border: "1px solid #3b82f6",
                                                borderRadius: "8px",
                                                textAlign: "center",
                                                color: "#1e40af",
                                                fontWeight: 600,
                                                fontSize: "0.875rem"
                                            }}>
                                                ⏳ Otpremam sliku...
                                            </div>
                                        )}
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                )}

                {/* SUBMIT BUTTON */}
                <div style={{ display: "flex", gap: "12px", alignItems: "center", flexWrap: "wrap" }}>
                    <button
                        type="submit"
                        className="button-big"
                        disabled={isSubmitting}
                        style={{
                            maxWidth: "420px",
                            background: isSubmitting 
                                ? "linear-gradient(135deg, #9ca3af 0%, #6b7280 100%)" 
                                : "linear-gradient(135deg, #10b981 0%, #059669 100%)",
                            cursor: isSubmitting ? "not-allowed" : "pointer",
                            boxShadow: isSubmitting 
                                ? "none" 
                                : "0 4px 6px -1px rgba(16,185,129,0.4), 0 2px 4px -1px rgba(16,185,129,0.2)",
                            border: "none",
                            transition: "all 0.2s ease",
                        }}
                        onMouseEnter={(e) => !isSubmitting && (e.currentTarget.style.transform = "translateY(-2px)")}
                        onMouseLeave={(e) => e.currentTarget.style.transform = "translateY(0)"}
                    >
                        {isSubmitting 
                            ? "⏳ Čuvam..." 
                            : mode === "edit" 
                                ? "💾 Sačuvaj izmene" 
                                : "✅ Kreiraj artikal"}
                    </button>
                    <span style={{ 
                        fontSize: "0.875rem", 
                        color: "#6b7280",
                        background: "#f3f4f6",
                        padding: "8px 12px",
                        borderRadius: "8px",
                        border: "1px solid #e5e7eb"
                    }}>
                        💡 <kbd style={{ 
                            background: "white", 
                            padding: "2px 6px", 
                            borderRadius: "4px",
                            border: "1px solid #d1d5db",
                            fontSize: "0.75rem",
                            fontFamily: "monospace"
                        }}>Ctrl</kbd> + <kbd style={{ 
                            background: "white", 
                            padding: "2px 6px", 
                            borderRadius: "4px",
                            border: "1px solid #d1d5db",
                            fontSize: "0.75rem",
                            fontFamily: "monospace"
                        }}>Enter</kbd> za brzo čuvanje
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
                            className="button-big"
                            style={{ 
                                background: "linear-gradient(135deg, #f3f4f6 0%, #e5e7eb 100%)", 
                                color: "#374151", 
                                width: "auto", 
                                padding: "10px 20px", 
                                marginTop: 0,
                                border: "1px solid #d1d5db"
                            }}
                            onClick={() => setShowNewTipModal(false)}
                        >
                            Otkaži
                        </button>
                        <button
                            className="button-big"
                            style={{ 
                                width: "auto", 
                                padding: "10px 20px", 
                                marginTop: 0,
                                background: "linear-gradient(135deg, #0891b2 0%, #06b6d4 100%)",
                                boxShadow: "0 4px 6px -1px rgba(8,145,178,0.3)"
                            }}
                            onClick={handleCreateTip}
                            disabled={!newTip.trim() || isCreatingTip}
                        >
                            {isCreatingTip ? "Kreiram..." : "Potvrdi"}
                        </button>
                    </>
                }
            >
                <div>
                    <label className="field-label" style={{ fontWeight: 600 }}>Naziv tipa</label>
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
                        style={{ boxShadow: "0 1px 2px rgba(0,0,0,0.05)" }}
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
                        <button
                            className="button-big"
                            style={{ 
                                background: "linear-gradient(135deg, #f3f4f6 0%, #e5e7eb 100%)", 
                                color: "#374151", 
                                width: "auto", 
                                padding: "10px 20px", 
                                marginTop: 0,
                                border: "1px solid #d1d5db"
                            }}
                            onClick={() => setShowNewDobModal(false)}
                        >
                            Otkaži
                        </button>
                        <button
                            className="button-big"
                            style={{ 
                                width: "auto", 
                                padding: "10px 20px", 
                                marginTop: 0,
                                background: "linear-gradient(135deg, #059669 0%, #10b981 100%)",
                                boxShadow: "0 4px 10px rgba(5,150,105,0.4)"
                            }}
                            onClick={handleCreateDob}
                            disabled={!newDob.trim() || isCreatingDob}
                        >
                            {isCreatingDob ? "Kreiram..." : "Potvrdi"}
                        </button>
                    </>
                }
            >
                <div>
                    <label className="field-label" style={{ fontWeight: 600 }}>Naziv dobavljača</label>
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
                        style={{ boxShadow: "0 1px 2px rgba(0,0,0,0.05)" }}
                    />
                </div>
            </Modal>
        </div>
    );
}