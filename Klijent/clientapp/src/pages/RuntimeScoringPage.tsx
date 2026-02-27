import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
    evaluateRuntimeScoring,
    getRuntimeProductImageUrl,
    type RuntimeScoringEvaluateResponse,
} from "../services/runtimeScoringApi";

// ─── constants ────────────────────────────────────────────────────────────────

const MAX_IMAGE_BYTES = 12 * 1024 * 1024;
const ALLOWED_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const SCENARIO_STORAGE_KEY = "runtime-scoring-scenarios-v1";
const MAX_SCENARIOS = 25;

const SHOE_CATEGORIES = [
    { value: "sneakers", label: "Patike / Sneakers" },
    { value: "boots", label: "Čizme / Boots" },
    { value: "heels", label: "Štikle / Heels" },
    { value: "sandals", label: "Sandale / Sandals" },
    { value: "loafers", label: "Mokasinke / Loafers" },
    { value: "slippers", label: "Papuče / Slippers" },
    { value: "oxfords", label: "Oxfords" },
    { value: "flats", label: "Ravne cipele / Flats" },
    { value: "pumps", label: "Pumpe / Pumps" },
    { value: "mules", label: "Mulje / Mules" },
    { value: "general", label: "Opšte / General" },
];

const MATERIAL_OPTIONS = [
    { value: "koža",      label: "Koža / Leather" },
    { value: "nabuk",     label: "Nabuk / Suede" },
    { value: "mesh",      label: "Mesh" },
    { value: "tekstil",   label: "Tekstil / Textile" },
    { value: "platno",    label: "Platno / Canvas" },
    { value: "guma",      label: "Guma / Rubber" },
    { value: "sintetika", label: "Sintetika / Synthetic" },
];

const SCORE_META: Record<
    string,
    { label: string; desc: string; group: string }
> = {
    priceFitScore:     { label: "Cenovni fit",        desc: "Koliko dobro cena prati tržišni standard", group: "Cena i margina" },
    marginScore:       { label: "Margina",             desc: "Profitna margina na osnovu unetih cena",   group: "Cena i margina" },
    dealScore:         { label: "Cena vs tržište",     desc: "Konkurentnost cene u poređenju sa medijanom", group: "Cena i margina" },
    popularityScore:   { label: "Popularnost",         desc: "Popularnost brenda i kategorije u bazi",   group: "Tržišni signali" },
    trendMomentum:     { label: "Trend momentum",      desc: "Trend kretanja u ovom segmentu",           group: "Tržišni signali" },
    marketDemandScore: { label: "Tražnja",             desc: "Ukupna procena potražnje na tržištu",      group: "Tržišni signali" },
    imageSimilarityScore: { label: "Vizuelna sličnost", desc: "Podudarnost sa sličnim modelima iz baze", group: "Pokrivenost" },
    sourceCoverageScore:  { label: "Pokrivenost podataka", desc: "Kvalitet i broj dostupnih izvora",    group: "Pokrivenost" },
    supplierScore:    { label: "Dobavljač",       desc: "Prodajni učinak dobavljača iz sopstvenih podataka", group: "Lokalni signali" },
    shoeTypeScore:    { label: "Tip obuće",       desc: "Popularnost tipa obuće u sopstvenoj prodaji",      group: "Lokalni signali" },
    seasonalScore:    { label: "Sezonalnost",     desc: "Da li je sezona trenutno aktivna",                 group: "Lokalni signali" },
    sizeColorScore:   { label: "Veličina / Boja", desc: "Popularnost kombinacije veličine i boje",          group: "Lokalni signali" },
    materialScore:    { label: "Materijal",       desc: "Kvalitetna ocena materijala gornjišta",            group: "Lokalni signali" },
    localDemandScore: { label: "Lokalna tražnja", desc: "Ukupni lokalni signal iz sopstvenih podataka",     group: "Lokalni signali" },
};

const SCORE_GROUPS = ["Cena i margina", "Tržišni signali", "Pokrivenost", "Lokalni signali"];

const VERDICT_PALETTE = {
    green:  { bg: "#dcfce7", border: "#86efac", text: "#15803d", ring: "#15803d" },
    blue:   { bg: "#eff6ff", border: "#93c5fd", text: "#1d4ed8", ring: "#2563eb" },
    amber:  { bg: "#fefce8", border: "#fde68a", text: "#b45309", ring: "#d97706" },
    orange: { bg: "#fff7ed", border: "#fdba74", text: "#c2410c", ring: "#ea580c" },
    red:    { bg: "#fef2f2", border: "#fca5a5", text: "#b91c1c", ring: "#dc2626" },
    gray:   { bg: "#f9fafb", border: "#d1d5db", text: "#374151", ring: "#6b7280" },
};

// ─── helpers ──────────────────────────────────────────────────────────────────

function scoreBarColor(value: number): string {
    if (value >= 70) return "#15803d";
    if (value >= 40) return "#d97706";
    return "#dc2626";
}

function scoreQualityLabel(value: number): string {
    if (value >= 75) return "Odlično";
    if (value >= 60) return "Dobro";
    if (value >= 40) return "Prosečno";
    return "Loše";
}

function parseDecimal(input: string): number | undefined {
    const n = Number(input.trim().replace(",", "."));
    return Number.isFinite(n) ? n : undefined;
}

function isAllowedFile(file: File): boolean {
    return ALLOWED_TYPES.has(file.type) || /\.(jpe?g|png|webp)$/i.test(file.name);
}

// ─── sub-components ───────────────────────────────────────────────────────────

/** Circular SVG score ring */
function ScoreRing({ score, color }: { score: number; color: string }) {
    const R = 52;
    const circ = 2 * Math.PI * R;
    const offset = circ * (1 - Math.max(0, Math.min(100, score)) / 100);
    return (
        <svg width={128} height={128} style={{ display: "block" }}>
            <circle cx={64} cy={64} r={R} fill="none" stroke="#e5e7eb" strokeWidth={10} />
            <circle
                cx={64} cy={64} r={R} fill="none"
                stroke={color} strokeWidth={10}
                strokeDasharray={circ}
                strokeDashoffset={offset}
                strokeLinecap="round"
                style={{ transition: "stroke-dashoffset .6s ease", transformOrigin: "64px 64px", transform: "rotate(-90deg)" }}
            />
            <text x={64} y={62} textAnchor="middle" dominantBaseline="middle" fontSize={22} fontWeight={800} fill={color}>
                {score.toFixed(0)}
            </text>
            <text x={64} y={84} textAnchor="middle" dominantBaseline="middle" fontSize={11} fill="#6b7280">
                / 100
            </text>
        </svg>
    );
}

/** Single score row card */
function ScoreRow({ fieldKey, value }: { fieldKey: string; value: number }) {
    const meta = SCORE_META[fieldKey];
    if (!meta) return null;
    const color = scoreBarColor(value);
    const qlabel = scoreQualityLabel(value);
    return (
        <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 0", borderBottom: "1px solid #f3f4f6" }}>
            <div style={{ flex: "0 0 140px", fontSize: 12, fontWeight: 600, color: "#374151" }}>{meta.label}</div>
            <div style={{ flex: 1, height: 7, borderRadius: 8, background: "#f3f4f6", overflow: "hidden" }}>
                <div style={{ width: `${Math.max(0, Math.min(100, value))}%`, height: "100%", borderRadius: 8, background: color, transition: "width .35s ease" }} />
            </div>
            <span style={{ minWidth: 36, textAlign: "right", fontWeight: 700, fontSize: 13, color }}>{value.toFixed(1)}</span>
            <span style={{ minWidth: 56, textAlign: "right", fontSize: 11, color, background: `${color}18`, borderRadius: 6, padding: "2px 6px" }}>
                {qlabel}
            </span>
        </div>
    );
}

type FormErrors = { file?: string; cost?: string; targetPrice?: string; brand?: string };
type SaveNotice = { kind: "success" | "error"; text: string } | null;
type SavedScenario = {
    id: string;
    createdAt: string;
    name: string;
    input: {
        brand?: string;
        category?: string;
        market?: string;
        cost?: number;
        targetPrice?: number;
        velicina?: string;
        boja?: string;
        materijal?: string;
        imageName?: string;
    };
    output: {
        finalScore: number;
        sellProbabilityRS: number;
        verdict: string;
        recommendedPriceRange: string;
        confidence: number;
    };
};

// ─── page ─────────────────────────────────────────────────────────────────────

export default function RuntimeScoringPage() {
    const [file, setFile]             = useState<File | null>(null);
    const [previewUrl, setPreviewUrl] = useState<string | null>(null);
    const [isDragging, setIsDragging] = useState(false);
    const [brand, setBrand]           = useState("");
    const [category, setCategory]     = useState("sneakers");
    const [market, setMarket]         = useState("RS");
    const [cost, setCost]             = useState("");
    const [targetPrice, setTargetPrice] = useState("");
    // Local-signal fields
    const [velicina, setVelicina]     = useState("");
    const [boja, setBoja]             = useState("");
    const [materijal, setMaterijal]   = useState("");
    const [loading, setLoading]       = useState(false);
    const [error, setError]           = useState<string | null>(null);
    const [formErrors, setFormErrors] = useState<FormErrors>({});
    const [result, setResult]         = useState<RuntimeScoringEvaluateResponse | null>(null);
    const [saveNotice, setSaveNotice] = useState<SaveNotice>(null);
    const fileInputRef = useRef<HTMLInputElement>(null);
    const formRef = useRef<HTMLFormElement>(null);

    useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl); }, [previewUrl]);

    const sellProbabilityPercent = useMemo(() => {
        if (!result) return 0;
        const v = result.sellProbabilityRS <= 1 ? result.sellProbabilityRS * 100 : result.sellProbabilityRS;
        return Math.max(0, Math.min(100, v));
    }, [result]);

    const applyFile = useCallback((next: File | null) => {
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        if (!next) { setFile(null); setPreviewUrl(null); return; }
        if (!isAllowedFile(next)) {
            setFormErrors((p) => ({ ...p, file: "Dozvoljeni formati: JPG, PNG, WEBP." }));
            return;
        }
        if (next.size > MAX_IMAGE_BYTES) {
            setFormErrors((p) => ({ ...p, file: "Slika je prevelika (max 12 MB)." }));
            return;
        }
        setFile(next);
        setPreviewUrl(URL.createObjectURL(next));
        setFormErrors((p) => ({ ...p, file: undefined }));
    }, [previewUrl]);

    const onDrop = useCallback((e: React.DragEvent) => {
        e.preventDefault();
        setIsDragging(false);
        applyFile(e.dataTransfer.files?.[0] ?? null);
    }, [applyFile]);

    const onDragOver = (e: React.DragEvent) => { e.preventDefault(); setIsDragging(true); };
    const onDragLeave = () => setIsDragging(false);

    const validate = (): boolean => {
        const errs: FormErrors = {};
        if (!file) errs.file = "Izaberi sliku obuće.";
        const t = parseDecimal(targetPrice);
        if (!targetPrice.trim()) errs.targetPrice = "Ciljna cena je obavezna.";
        else if (!t || t <= 0) errs.targetPrice = "Unesi validnu cenu > 0.";
        else if (t > 20000) errs.targetPrice = "Prevysoka cena (max 20.000).";
        const c = parseDecimal(cost);
        if (cost.trim() && (c === undefined || c < 0)) errs.cost = "Nabavna cena mora biti ≥ 0.";
        if (cost.trim() && t && c !== undefined && c >= t) errs.cost = "Nabavna cena mora biti manja od ciljne.";
        if (brand.trim().length > 60) errs.brand = "Brend — maks 60 znakova.";
        setFormErrors(errs);
        return Object.keys(errs).length === 0;
    };

    const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        setError(null);
        setResult(null);
        setSaveNotice(null);
        if (!validate() || !file) return;
        setLoading(true);
        try {
            const data = await evaluateRuntimeScoring({
                imageFile: file,
                cost: parseDecimal(cost),
                targetPrice: parseDecimal(targetPrice),
                brand: brand || undefined,
                category: category || undefined,
                market: market || undefined,
                velicina: velicina.trim() || undefined,
                boja: boja.trim() || undefined,
                materijal: materijal || undefined,
            });
            setResult(data);
        } catch (ex: unknown) {
            setError(ex instanceof Error ? ex.message : "Greška pri računanju score-a.");
        } finally {
            setLoading(false);
        }
    };

    const verdictPalette = result ? VERDICT_PALETTE[result.verdictColor] ?? VERDICT_PALETTE.gray : VERDICT_PALETTE.gray;

    const sellBarColor = sellProbabilityPercent >= 60 ? "#15803d" : sellProbabilityPercent >= 30 ? "#d97706" : "#dc2626";

    const handleRunEvaluation = useCallback(() => {
        formRef.current?.requestSubmit();
    }, []);

    const handleSaveScenario = useCallback(() => {
        if (!result) {
            setSaveNotice({ kind: "error", text: "Prvo pokreni procenu, pa zatim sačuvaj scenario." });
            return;
        }

        const scenarioName = `${brand.trim() || "Bez brenda"} / ${category.toUpperCase()} / ${market}`;
        const scenario: SavedScenario = {
            id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
            createdAt: new Date().toISOString(),
            name: scenarioName,
            input: {
                brand: brand.trim() || undefined,
                category: category || undefined,
                market: market || undefined,
                cost: parseDecimal(cost),
                targetPrice: parseDecimal(targetPrice),
                velicina: velicina.trim() || undefined,
                boja: boja.trim() || undefined,
                materijal: materijal || undefined,
                imageName: file?.name,
            },
            output: {
                finalScore: result.finalScore,
                sellProbabilityRS: result.sellProbabilityRS,
                verdict: result.verdict,
                recommendedPriceRange: result.recommendedPriceRange,
                confidence: result.confidence,
            },
        };

        try {
            const currentRaw = localStorage.getItem(SCENARIO_STORAGE_KEY);
            const current = currentRaw ? (JSON.parse(currentRaw) as SavedScenario[]) : [];
            const next = [scenario, ...current].slice(0, MAX_SCENARIOS);
            localStorage.setItem(SCENARIO_STORAGE_KEY, JSON.stringify(next));
            setSaveNotice({ kind: "success", text: `Scenario je sačuvan (${next.length}/${MAX_SCENARIOS}).` });
        } catch {
            setSaveNotice({ kind: "error", text: "Nije moguće sačuvati scenario (localStorage nije dostupan)." });
        }
    }, [result, brand, category, market, cost, targetPrice, velicina, boja, materijal, file]);

    return (
        <div style={{ maxWidth: 1160, margin: "0 auto", paddingBottom: 40 }}>
            {/* header */}
            <div style={{ background: "linear-gradient(135deg, #1d4ed8 0%, #4338ca 100%)", color: "white", borderRadius: 14, padding: "20px 24px", marginBottom: 18 }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
                    <div>
                        <h1 style={{ margin: 0, fontSize: 22, fontWeight: 800 }}>Runtime Scoring Engine</h1>
                        <p style={{ margin: "6px 0 0", opacity: 0.9, fontSize: 13 }}>
                            Ubaci sliku modela i proceni score, verovatnocu prodaje i signal tr�i�ta u realnom vremenu.
                        </p>
                    </div>
                    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                        <button
                            type="button"
                            onClick={handleRunEvaluation}
                            disabled={loading}
                            style={{
                                border: "none",
                                borderRadius: 8,
                                padding: "10px 14px",
                                color: "white",
                                background: loading ? "#9ca3af" : "#0f766e",
                                fontWeight: 700,
                                cursor: loading ? "not-allowed" : "pointer",
                            }}
                        >
                            {loading ? "Pokrecem..." : "Pokreni procenu"}
                        </button>
                        <button
                            type="button"
                            onClick={handleSaveScenario}
                            disabled={loading || !result}
                            style={{
                                border: "1px solid #ffffff66",
                                borderRadius: 8,
                                padding: "10px 14px",
                                color: "white",
                                background: loading || !result ? "rgba(255,255,255,.2)" : "rgba(255,255,255,.14)",
                                fontWeight: 700,
                                cursor: loading || !result ? "not-allowed" : "pointer",
                            }}
                        >
                            Sacuvaj scenario
                        </button>
                    </div>
                </div>
                {saveNotice && (
                    <div
                        style={{
                            marginTop: 12,
                            borderRadius: 8,
                            padding: "8px 10px",
                            fontSize: 12,
                            fontWeight: 600,
                            background: saveNotice.kind === "success" ? "#dcfce7" : "#fef2f2",
                            color: saveNotice.kind === "success" ? "#166534" : "#b91c1c",
                        }}
                    >
                        {saveNotice.text}
                    </div>
                )}
            </div>

            <form ref={formRef} onSubmit={handleSubmit} style={{ display: "grid", gridTemplateColumns: "340px 1fr", gap: 16, alignItems: "start" }}>
                {/* ── LEFT: input panel ── */}
                <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 12, padding: 16, display: "flex", flexDirection: "column", gap: 12 }}>
                    <div style={{ fontWeight: 700, fontSize: 14, color: "#111827" }}>Parametri procene</div>

                    {/* drag-drop zone */}
                    <div>
                        <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "#374151", marginBottom: 6 }}>Slika obuće *</label>
                        <div
                            role="button"
                            tabIndex={0}
                            onClick={() => fileInputRef.current?.click()}
                            onKeyDown={(e) => e.key === "Enter" && fileInputRef.current?.click()}
                            onDrop={onDrop}
                            onDragOver={onDragOver}
                            onDragLeave={onDragLeave}
                            style={{
                                border: `2px dashed ${isDragging ? "#2563eb" : formErrors.file ? "#dc2626" : "#d1d5db"}`,
                                borderRadius: 12,
                                background: isDragging ? "#eff6ff" : previewUrl ? "#f9fafb" : "#fafafa",
                                minHeight: 200,
                                display: "flex",
                                flexDirection: "column",
                                alignItems: "center",
                                justifyContent: "center",
                                cursor: "pointer",
                                overflow: "hidden",
                                transition: "border-color .15s, background .15s",
                                position: "relative",
                            }}
                        >
                            {previewUrl ? (
                                <>
                                    <img src={previewUrl} alt="Preview" style={{ width: "100%", maxHeight: 220, objectFit: "contain" }} />
                                    <span style={{ position: "absolute", bottom: 6, right: 8, background: "rgba(0,0,0,.5)", color: "white", fontSize: 11, borderRadius: 6, padding: "2px 8px" }}>
                                        klikni za zamenu
                                    </span>
                                </>
                            ) : (
                                <>
                                    <svg width={36} height={36} fill="none" stroke={isDragging ? "#2563eb" : "#9ca3af"} strokeWidth={1.5} viewBox="0 0 24 24">
                                        <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5m-13.5-9L12 3m0 0 4.5 4.5M12 3v13.5" />
                                    </svg>
                                    <span style={{ marginTop: 8, fontSize: 13, color: "#6b7280", textAlign: "center", lineHeight: "1.4" }}>
                                        {isDragging ? "Pusti sliku ovde" : "Prevuci sliku ili klikni za izbor"}
                                    </span>
                                    <span style={{ fontSize: 11, color: "#9ca3af", marginTop: 4 }}>JPG, PNG, WEBP — maks 12 MB</span>
                                </>
                            )}
                        </div>
                        <input
                            ref={fileInputRef}
                            type="file"
                            accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp"
                            onChange={(e) => applyFile(e.target.files?.[0] ?? null)}
                            style={{ display: "none" }}
                        />
                        {formErrors.file && <div style={{ marginTop: 5, color: "#b91c1c", fontSize: 12 }}>{formErrors.file}</div>}
                    </div>

                    {/* brand */}
                    <div>
                        <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "#374151", marginBottom: 5 }}>Brend</label>
                        <input
                            value={brand}
                            maxLength={60}
                            onChange={(e) => { setBrand(e.target.value); if (formErrors.brand) setFormErrors((p) => ({ ...p, brand: undefined })); }}
                            placeholder="npr. Tamaris, Nike, Skechers"
                            style={{ width: "100%", boxSizing: "border-box", padding: "8px 10px", borderRadius: 8, border: `1px solid ${formErrors.brand ? "#dc2626" : "#d1d5db"}`, fontSize: 13 }}
                        />
                        {formErrors.brand && <div style={{ marginTop: 5, color: "#b91c1c", fontSize: 12 }}>{formErrors.brand}</div>}
                    </div>

                    {/* category — dropdown */}
                    <div>
                        <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "#374151", marginBottom: 5 }}>Tip obuće</label>
                        <select
                            value={category}
                            onChange={(e) => setCategory(e.target.value)}
                            style={{ width: "100%", padding: "8px 10px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, background: "white" }}
                        >
                            {SHOE_CATEGORIES.map((c) => (
                                <option key={c.value} value={c.value}>{c.label}</option>
                            ))}
                        </select>
                    </div>

                    {/* market */}
                    <div>
                        <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "#374151", marginBottom: 5 }}>Tržište</label>
                        <select
                            value={market}
                            onChange={(e) => setMarket(e.target.value)}
                            style={{ width: "100%", padding: "8px 10px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, background: "white" }}
                        >
                            {[["RS","Srbija (RS)"],["DE","Nemačka (DE)"],["AT","Austrija (AT)"],["CH","Švajcarska (CH)"],["HU","Mađarska (HU)"],["RO","Rumunija (RO)"]].map(([val, lbl]) => (
                                <option key={val} value={val}>{lbl}</option>
                            ))}
                        </select>
                    </div>

                    {/* prices */}
                    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
                        <div>
                            <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "#374151", marginBottom: 5 }}>Nabavna cena</label>
                            <input
                                type="text" inputMode="decimal" value={cost}
                                onChange={(e) => { setCost(e.target.value); if (formErrors.cost) setFormErrors((p) => ({ ...p, cost: undefined })); }}
                                placeholder="55.90"
                                style={{ width: "100%", boxSizing: "border-box", padding: "8px 10px", borderRadius: 8, border: `1px solid ${formErrors.cost ? "#dc2626" : "#d1d5db"}`, fontSize: 13 }}
                            />
                            {formErrors.cost && <div style={{ marginTop: 5, color: "#b91c1c", fontSize: 12 }}>{formErrors.cost}</div>}
                        </div>
                        <div>
                            <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "#374151", marginBottom: 5 }}>Ciljna cena *</label>
                            <input
                                type="text" inputMode="decimal" value={targetPrice}
                                onChange={(e) => { setTargetPrice(e.target.value); if (formErrors.targetPrice) setFormErrors((p) => ({ ...p, targetPrice: undefined })); }}
                                placeholder="89.90"
                                style={{ width: "100%", boxSizing: "border-box", padding: "8px 10px", borderRadius: 8, border: `1px solid ${formErrors.targetPrice ? "#dc2626" : "#d1d5db"}`, fontSize: 13 }}
                            />
                            {formErrors.targetPrice && <div style={{ marginTop: 5, color: "#b91c1c", fontSize: 12 }}>{formErrors.targetPrice}</div>}
                        </div>
                    </div>

                    {/* ── Local-signal inputs ── */}
                    <div style={{ borderTop: "1px solid #f3f4f6", paddingTop: 10 }}>
                        <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: ".06em", color: "#9ca3af", marginBottom: 8 }}>Lokalni signali (opcionalno)</div>

                        {/* Veličina + Boja */}
                        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 8 }}>
                            <div>
                                <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "#374151", marginBottom: 5 }}>Veličina</label>
                                <input
                                    value={velicina}
                                    onChange={(e) => setVelicina(e.target.value)}
                                    placeholder="npr. 42"
                                    style={{ width: "100%", boxSizing: "border-box", padding: "8px 10px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }}
                                />
                            </div>
                            <div>
                                <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "#374151", marginBottom: 5 }}>Boja</label>
                                <input
                                    value={boja}
                                    onChange={(e) => setBoja(e.target.value)}
                                    placeholder="npr. Crna"
                                    style={{ width: "100%", boxSizing: "border-box", padding: "8px 10px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13 }}
                                />
                            </div>
                        </div>

                        {/* Materijal */}
                        <div>
                            <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "#374151", marginBottom: 5 }}>Materijal gornjišta</label>
                            <select
                                value={materijal}
                                onChange={(e) => setMaterijal(e.target.value)}
                                style={{ width: "100%", padding: "8px 10px", borderRadius: 8, border: "1px solid #d1d5db", fontSize: 13, background: "white" }}
                            >
                                <option value="">— Izaberi materijal —</option>
                                {MATERIAL_OPTIONS.map((m) => (
                                    <option key={m.value} value={m.value}>{m.label}</option>
                                ))}
                            </select>
                        </div>
                    </div>

                    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
                        <button
                            type="submit"
                            disabled={loading}
                            style={{ width: "100%", border: "none", borderRadius: 8, padding: "11px 12px", color: "white", background: loading ? "#9ca3af" : "#2563eb", fontWeight: 700, cursor: loading ? "not-allowed" : "pointer", fontSize: 14, letterSpacing: ".02em", transition: "background .15s" }}
                        >
                            {loading ? (
                                <span style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 8 }}>
                                    <span style={{ display: "inline-block", width: 14, height: 14, border: "2px solid #ffffff55", borderTopColor: "#fff", borderRadius: "50%", animation: "spin 0.7s linear infinite" }} />
                                    Racunam...
                                </span>
                            ) : "Pokreni procenu"}
                        </button>
                        <button
                            type="button"
                            onClick={handleSaveScenario}
                            disabled={loading || !result}
                            style={{
                                width: "100%",
                                border: "1px solid #d1d5db",
                                borderRadius: 8,
                                padding: "11px 12px",
                                color: loading || !result ? "#9ca3af" : "#374151",
                                background: "white",
                                fontWeight: 700,
                                cursor: loading || !result ? "not-allowed" : "pointer",
                                fontSize: 14,
                            }}
                        >
                            Sacuvaj scenario
                        </button>
                    </div>
                </div>

                {/* ── RIGHT: results panel ── */}
                <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>

                    {error && (
                        <div style={{ background: "#fef2f2", color: "#b91c1c", border: "1px solid #fecaca", borderRadius: 10, padding: "10px 14px", fontSize: 13 }}>
                            ⚠️ {error}
                        </div>
                    )}

                    {/* loading skeleton */}
                    {loading && (
                        <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 12, padding: "24px 20px" }}>
                            {[80, 60, 45, 70, 55].map((w, i) => (
                                <div key={i} style={{ height: 14, borderRadius: 8, background: "#f3f4f6", marginBottom: 12, width: `${w}%`, animation: "pulse 1.4s ease-in-out infinite", animationDelay: `${i * 0.15}s` }} />
                            ))}
                        </div>
                    )}

                    {/* empty state */}
                    {!result && !loading && !error && (
                        <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 12, padding: "32px 20px", textAlign: "center", color: "#9ca3af" }}>
                            <svg width={40} height={40} fill="none" stroke="#d1d5db" strokeWidth={1.5} viewBox="0 0 24 24" style={{ margin: "0 auto 10px" }}>
                                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                            </svg>
                            <div style={{ fontSize: 14, fontWeight: 600, color: "#6b7280" }}>Čeka na unos</div>
                            <div style={{ fontSize: 12, marginTop: 4 }}>Popuni formu i klikni „Izračunaj score"</div>
                        </div>
                    )}

                    {result && (
                        <>
                            {/* ── verdict banner ── */}
                            <div style={{ background: verdictPalette.bg, border: `1px solid ${verdictPalette.border}`, borderRadius: 14, padding: "18px 20px", display: "flex", alignItems: "center", gap: 20 }}>
                                <ScoreRing score={result.finalScore} color={verdictPalette.ring} />
                                <div style={{ flex: 1 }}>
                                    <div style={{ display: "flex", alignItems: "baseline", gap: 8, flexWrap: "wrap" }}>
                                        <span style={{ fontSize: 22, fontWeight: 800, color: verdictPalette.text }}>{result.verdict}</span>
                                        <span style={{ fontSize: 13, fontWeight: 600, background: verdictPalette.ring + "22", color: verdictPalette.text, borderRadius: 8, padding: "2px 8px" }}>
                                            {result.scoreLabel}
                                        </span>
                                    </div>
                                    {/* sell probability bar */}
                                    <div style={{ marginTop: 10 }}>
                                        <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12, color: "#6b7280", marginBottom: 4 }}>
                                            <span>Verovatnoća prodaje ({result.market})</span>
                                            <strong style={{ color: sellBarColor }}>{sellProbabilityPercent.toFixed(1)}%</strong>
                                        </div>
                                        <div style={{ height: 10, borderRadius: 8, background: "#e5e7eb", overflow: "hidden" }}>
                                            <div style={{ width: `${sellProbabilityPercent}%`, height: "100%", borderRadius: 8, background: sellBarColor, transition: "width .5s ease" }} />
                                        </div>
                                    </div>
                                    {/* meta badges */}
                                    <div style={{ display: "flex", flexWrap: "wrap", gap: 6, marginTop: 10 }}>
                                        <Badge bg="#eef2ff" color="#3730a3">{result.usedPythonModel ? "Python model ✓" : "Lokalni model"}</Badge>
                                        <Badge bg={result.hasTrainingSignal ? "#dcfce7" : "#f3f4f6"} color={result.hasTrainingSignal ? "#166534" : "#374151"}>
                                            {result.hasTrainingSignal ? "Trening signal ✓" : "Nema trening signala"}
                                        </Badge>
                                        <Badge bg="#f0fdf4" color="#15803d">Pouzdanost: {result.confidence.toFixed(0)}%</Badge>
                                        <Badge bg="#f3f4f6" color="#374151">{result.sourceCoverageCount} izvora</Badge>
                                        {result.pricePositioning && (
                                            <Badge bg="#fffbeb" color="#92400e">
                                                {result.pricePositioning === "ispod_tržišta" ? "⬇ Ispod tržišta" : result.pricePositioning === "iznad_tržišta" ? "⬆ Iznad tržišta" : "↔ U rangu tržišta"}
                                            </Badge>
                                        )}
                                    </div>
                                    {/* price info */}
                                    <div style={{ marginTop: 10, display: "flex", gap: 16, fontSize: 12, color: "#6b7280", flexWrap: "wrap" }}>
                                        <span>Preporučeni opseg: <strong style={{ color: "#111827" }}>{result.recommendedPriceRange}</strong></span>
                                        {result.typicalPrice != null && <span>Tipična cena: <strong style={{ color: "#111827" }}>{result.currency ?? "EUR"} {result.typicalPrice}</strong></span>}
                                    </div>
                                </div>
                            </div>

                            {/* ── insights ── */}
                            {result.insights.length > 0 && (
                                <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 12, padding: "14px 16px" }}>
                                    <div style={{ fontWeight: 700, fontSize: 13, color: "#111827", marginBottom: 10 }}>Zaključci i preporuke</div>
                                    <ul style={{ margin: 0, padding: 0, listStyle: "none", display: "flex", flexDirection: "column", gap: 6 }}>
                                        {result.insights.map((insight, i) => (
                                            <li key={i} style={{ fontSize: 13, color: "#374151", display: "flex", alignItems: "flex-start", lineHeight: "1.5" }}>
                                                <span style={{ marginRight: 8 }}>{getInsightIcon(insight)}</span>
                                                <span>{cleanInsightText(insight)}</span>
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                            )}

                            {/* ── score groups ── */}
                            <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 12, padding: "14px 16px" }}>
                                <div style={{ fontWeight: 700, fontSize: 13, color: "#111827", marginBottom: 12 }}>Detaljan pregled bodova</div>
                                {SCORE_GROUPS.map((group) => {
                                    const keys = Object.entries(SCORE_META)
                                        .filter(([, m]) => m.group === group)
                                        .map(([k]) => k);
                                    const values: Record<string, number> = {
                                        priceFitScore: result.priceFitScore,
                                        marginScore: result.marginScore,
                                        dealScore: result.dealScore,
                                        popularityScore: result.popularityScore,
                                        trendMomentum: result.trendMomentum,
                                        marketDemandScore: result.marketDemandScore,
                                        imageSimilarityScore: result.imageSimilarityScore,
                                        sourceCoverageScore: result.sourceCoverageScore,
                                        supplierScore: result.supplierScore,
                                        shoeTypeScore: result.shoeTypeScore,
                                        seasonalScore: result.seasonalScore,
                                        sizeColorScore: result.sizeColorScore,
                                        materialScore: result.materialScore,
                                        localDemandScore: result.localDemandScore,
                                    };
                                    return (
                                        <div key={group} style={{ marginBottom: 14 }}>
                                            <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: ".06em", color: "#9ca3af", marginBottom: 4 }}>{group}</div>
                                            {keys.map((k) => <ScoreRow key={k} fieldKey={k} value={values[k] ?? 0} />)}
                                        </div>
                                    );
                                })}
                            </div>

                            {/* ── similar products ── */}
                            <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 12, padding: "14px 16px" }}>
                                <div style={{ fontWeight: 700, fontSize: 13, color: "#111827", marginBottom: 10 }}>
                                    Slični proizvodi {result.similarProducts.length > 0 && <span style={{ fontWeight: 400, color: "#9ca3af" }}>({result.similarProducts.length})</span>}
                                </div>
                                {result.similarProducts.length === 0 ? (
                                    <div style={{ color: "#9ca3af", fontSize: 13 }}>Nema vizuelno sličnih proizvoda u bazi.</div>
                                ) : (
                                    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                                        {result.similarProducts.map((sp) => {
                                            const imageUrl = getRuntimeProductImageUrl(sp.imageFileName);
                                            const simPct = sp.similarity <= 1 ? sp.similarity * 100 : sp.similarity;
                                            const simColor = simPct >= 75 ? "#15803d" : simPct >= 55 ? "#d97706" : "#dc2626";
                                            return (
                                                <div
                                                    key={`${sp.productId}-${sp.productName}`}
                                                    style={{ display: "grid", gridTemplateColumns: "52px 1fr auto", gap: 10, alignItems: "center", border: "1px solid #f3f4f6", borderRadius: 10, padding: "8px 10px" }}
                                                >
                                                    <div style={{ width: 52, height: 52, borderRadius: 8, background: "#f9fafb", border: "1px solid #e5e7eb", overflow: "hidden", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
                                                        {imageUrl ? (
                                                            <img src={imageUrl} alt={sp.productName} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                                                        ) : (
                                                            <svg width={20} height={20} fill="none" stroke="#d1d5db" strokeWidth={1.5} viewBox="0 0 24 24">
                                                                <path strokeLinecap="round" strokeLinejoin="round" d="m2.25 15.75 5.159-5.159a2.25 2.25 0 0 1 3.182 0l5.159 5.159m-1.5-1.5 1.409-1.409a2.25 2.25 0 0 1 3.182 0l2.909 2.909M3 20.25h18A2.25 2.25 0 0 0 23.25 18V6A2.25 2.25 0 0 0 21 3.75H3A2.25 2.25 0 0 0 .75 6v12A2.25 2.25 0 0 0 3 20.25Z" />
                                                            </svg>
                                                        )}
                                                    </div>
                                                    <div style={{ minWidth: 0 }}>
                                                        <div style={{ fontWeight: 600, fontSize: 13, color: "#111827", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                                                            {sp.productName || `Proizvod #${sp.productId}`}
                                                        </div>
                                                        <div style={{ display: "flex", gap: 6, marginTop: 3, flexWrap: "wrap" }}>
                                                            {sp.brand && <span style={{ fontSize: 11, background: "#eff6ff", color: "#1d4ed8", borderRadius: 5, padding: "1px 6px" }}>{sp.brand}</span>}
                                                            {sp.shoeType && <span style={{ fontSize: 11, background: "#f0fdf4", color: "#15803d", borderRadius: 5, padding: "1px 6px" }}>{sp.shoeType}</span>}
                                                            {!sp.brand && !sp.shoeType && <span style={{ fontSize: 11, color: "#9ca3af" }}>#{sp.productId}</span>}
                                                        </div>
                                                    </div>
                                                    <div style={{ textAlign: "right" }}>
                                                        <div style={{ fontWeight: 700, fontSize: 14, color: simColor }}>{simPct.toFixed(1)}%</div>
                                                        <div style={{ fontSize: 10, color: "#9ca3af" }}>sličnost</div>
                                                    </div>
                                                </div>
                                            );
                                        })}
                                    </div>
                                )}
                            </div>
                        </>
                    )}
                </div>
            </form>

            {/* keyframe animations injected as a <style> */}
            <style>{`
                @keyframes spin { to { transform: rotate(360deg); } }
                @keyframes pulse { 0%,100% { opacity:1; } 50% { opacity:.4; } }
            `}</style>
        </div>
    );
}

// ─── tiny helpers ─────────────────────────────────────────────────────────────

function Badge({ bg, color, children }: { bg: string; color: string; children: React.ReactNode }) {
    return (
        <span style={{ background: bg, color, padding: "3px 9px", borderRadius: 999, fontSize: 11, fontWeight: 600, whiteSpace: "nowrap" }}>
            {children}
        </span>
    );
}

/** Extract the leading emoji from an insight string (first char if it's an emoji) */
function getInsightIcon(text: string): string {
    const match = text.match(/^(\p{Emoji_Presentation}|\p{Extended_Pictographic})\s*/u);
    return match ? match[1] : "•";
}

/** Remove the leading emoji from the insight text */
function cleanInsightText(text: string): string {
    return text.replace(/^(\p{Emoji_Presentation}|\p{Extended_Pictographic})\s*/u, "");
}




