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

const VERDICT_MAP: Record<string, { container: string; ring: string }> = {
    green:  { container: 'bg-surface-elevated border-success text-success', ring: 'var(--success)' },
    blue:   { container: 'bg-surface-elevated border-info text-info', ring: 'var(--info)' },
    amber:  { container: 'bg-surface-elevated border-warning text-warning', ring: 'var(--warning)' },
    orange: { container: 'bg-surface-elevated border-warning text-warning', ring: 'var(--warning)' },
    red:    { container: 'bg-surface-elevated border-error text-error', ring: 'var(--error)' },
    gray:   { container: 'bg-surface-elevated border-muted text-muted', ring: 'var(--info)' },
};

// ─── helpers ──────────────────────────────────────────────────────────────────

function scoreBarClass(value: number): string {
    if (value >= 70) return 'bg-success';
    if (value >= 40) return 'bg-warning';
    return 'bg-error';
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
            <circle cx={64} cy={64} r={R} fill="none" stroke="var(--border)" strokeWidth={10} />
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
            <text x={64} y={84} textAnchor="middle" dominantBaseline="middle" fontSize={11} fill="var(--c-8a95b0, #8A95B0)">
                / 100
            </text>
        </svg>
    );
}

/** Single score row card */
function ScoreRow({ fieldKey, value }: { fieldKey: string; value: number }) {
    const meta = SCORE_META[fieldKey];
    if (!meta) return null;
    const barClass = scoreBarClass(value);
    const qlabel = scoreQualityLabel(value);
    return (
        <div className="flex items-center gap-3 py-2 border-b border-muted">
            <div className="flex-none w-[140px] text-sm font-semibold text-contrast">{meta.label}</div>
            <div className="flex-1 h-2 rounded overflow-hidden bg-muted/30">
                <div className={`${barClass} h-full rounded`} style={{ width: `${Math.max(0, Math.min(100, value))}%`, transition: 'width .35s' }} />
            </div>
            <span className="min-w-[36px] text-right font-bold text-sm">{value.toFixed(1)}</span>
            <span className={`min-w-[56px] text-right text-sm font-semibold rounded px-2 ${barClass} bg-opacity-10`}>
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

    const verdictCls = result ? VERDICT_MAP[result.verdictColor] ?? VERDICT_MAP.gray : VERDICT_MAP.gray;

    const sellBarColor = sellProbabilityPercent >= 60 ? "var(--c-22c55e, #22c55e)" : sellProbabilityPercent >= 30 ? "var(--c-f59e0b, #f59e0b)" : "var(--c-ef4444, #ef4444)";

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
        <div className="max-w-[1160px] mx-auto pb-10">
            {/* header */}
            <div className="rounded-2xl border border-muted bg-surface-elevated p-5 mb-4 text-contrast">
                <div className="flex items-center justify-between gap-3 flex-wrap">
                    <div>
                        <h1 className="m-0 text-lg font-extrabold">Runtime Scoring Engine</h1>
                        <p className="mt-1 text-sm opacity-90">Ubaci sliku modela i proceni score, verovatnocu prodaje i signal tržišta u realnom vremenu.</p>
                    </div>
                    <div className="flex items-center gap-2">
                        <button
                            type="button"
                            onClick={handleRunEvaluation}
                            disabled={loading}
                            className={`rounded-md px-4 py-2 font-bold text-white ${loading ? 'bg-muted/70 cursor-not-allowed' : 'bg-success'}`}
                        >
                            {loading ? "Pokrecem..." : "Pokreni procenu"}
                        </button>
                        <button
                            type="button"
                            onClick={handleSaveScenario}
                            disabled={loading || !result}
                            className={`rounded-md px-4 py-2 border border-white/40 font-bold ${loading || !result ? 'text-white/60 bg-surface' : 'bg-surface/90 text-white'}`}
                        >
                            Sacuvaj scenario
                        </button>
                    </div>
                </div>
                {saveNotice && (
                    <div className={`mt-3 rounded-md px-3 py-2 text-sm font-semibold ${saveNotice.kind === 'success' ? 'bg-success/10 border-success text-success' : 'bg-error/10 border-error text-error'}`}>
                        {saveNotice.text}
                    </div>
                )}
            </div>

            <form ref={formRef} onSubmit={handleSubmit} className="grid grid-cols-[340px_1fr] gap-4 items-start">
                {/* ── LEFT: input panel ── */}
                <div className="bg-surface border border-muted rounded-lg p-4 flex flex-col gap-3">
                    <div className="font-bold text-sm text-contrast">Parametri procene</div>

                    {/* drag-drop zone */}
                    <div>
                        <label className="block text-xs font-semibold text-muted mb-1">Slika obuće *</label>
                        <div
                            role="button"
                            tabIndex={0}
                            onClick={() => fileInputRef.current?.click()}
                            onKeyDown={(e) => e.key === "Enter" && fileInputRef.current?.click()}
                            onDrop={onDrop}
                            onDragOver={onDragOver}
                            onDragLeave={onDragLeave}
                            className={`border-2 border-dashed rounded-lg min-h-[200px] flex flex-col items-center justify-center cursor-pointer overflow-hidden transition-all relative ${isDragging ? 'border-info' : formErrors.file ? 'border-error' : 'border-muted'}`}
                        >
                            {previewUrl ? (
                                <>
                                    <img src={previewUrl} alt="Preview" className="w-full max-h-[220px] object-contain" />
                                    <span className="absolute bottom-1.5 right-2 bg-black/70 text-contrast text-xs rounded-md px-2">klikni za zamenu</span>
                                </>
                            ) : (
                                <>
                                    <svg width={36} height={36} fill="none" stroke={isDragging ? "var(--c-4f8ef7, #4F8EF7)" : "var(--c-3a4565, #3A4565)"} strokeWidth={1.5} viewBox="0 0 24 24">
                                        <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5m-13.5-9L12 3m0 0 4.5 4.5M12 3v13.5" />
                                    </svg>
                                    <span className="mt-2 text-sm text-muted text-center">{isDragging ? "Pusti sliku ovde" : "Prevuci sliku ili klikni za izbor"}</span>
                                    <span className="text-xs text-muted/70 mt-1">JPG, PNG, WEBP — maks 12 MB</span>
                                </>
                            )}
                        </div>
                        <input ref={fileInputRef} type="file" accept=".jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp" onChange={(e) => applyFile(e.target.files?.[0] ?? null)} className="hidden" />
                        {formErrors.file && <div style={{ marginTop: 5, color: "var(--c-f87171, #f87171)", fontSize: 12 }}>{formErrors.file}</div>}
                    </div>

                    {/* brand */}
                    <div>
                        <label className="block text-xs font-semibold text-muted mb-1">Brend</label>
                        <input
                            value={brand}
                            maxLength={60}
                            onChange={(e) => { setBrand(e.target.value); if (formErrors.brand) setFormErrors((p) => ({ ...p, brand: undefined })); }}
                            placeholder="npr. Tamaris, Nike, Skechers"
                            className={`w-full box-border px-3 py-2 rounded-md text-contrast bg-surface border ${formErrors.brand ? 'border-error' : 'border-muted'}`}
                        />
                        {formErrors.brand && <div className="mt-1 text-xs text-error">{formErrors.brand}</div>}
                    </div>

                    {/* category — dropdown */}
                    <div>
                        <label className="block text-xs font-semibold text-muted mb-1">Tip obuće</label>
                        <select value={category} onChange={(e) => setCategory(e.target.value)} className="w-full px-3 py-2 rounded-md border border-muted bg-surface text-contrast">
                            {SHOE_CATEGORIES.map((c) => (
                                <option key={c.value} value={c.value}>{c.label}</option>
                            ))}
                        </select>
                    </div>

                    {/* market */}
                    <div>
                        <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "var(--c-8a95b0, #8A95B0)", marginBottom: 5 }}>Tržište</label>
                        <select
                            value={market}
                            onChange={(e) => setMarket(e.target.value)}
                            style={{ width: "100%", padding: "8px 10px", borderRadius: 8, border: "1px solid var(--c-2a3045, #2A3045)", fontSize: 13, background: "var(--c-1a1f2e, #1A1F2E)", color: "var(--c-c9d3e4, #c9d3e4)" }}
                        >
                            {[["RS","Srbija (RS)"],["DE","Nemačka (DE)"],["AT","Austrija (AT)"],["CH","Švajcarska (CH)"],["HU","Mađarska (HU)"],["RO","Rumunija (RO)"]].map(([val, lbl]) => (
                                <option key={val} value={val}>{lbl}</option>
                            ))}
                        </select>
                    </div>

                    {/* prices */}
                    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
                        <div>
                            <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "var(--c-8a95b0, #8A95B0)", marginBottom: 5 }}>Nabavna cena</label>
                            <input
                                type="text" inputMode="decimal" value={cost}
                                onChange={(e) => { setCost(e.target.value); if (formErrors.cost) setFormErrors((p) => ({ ...p, cost: undefined })); }}
                                placeholder="55.90"
                                style={{ width: "100%", boxSizing: "border-box", padding: "8px 10px", borderRadius: 8, border: `1px solid ${formErrors.cost ? "var(--c-ef4444, #ef4444)" : "var(--c-2a3045, #2A3045)"}`, fontSize: 13, background: "var(--c-1a1f2e, #1A1F2E)", color: "var(--c-c9d3e4, #c9d3e4)" }}
                            />
                            {formErrors.cost && <div style={{ marginTop: 5, color: "var(--c-f87171, #f87171)", fontSize: 12 }}>{formErrors.cost}</div>}
                        </div>
                        <div>
                            <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "var(--c-8a95b0, #8A95B0)", marginBottom: 5 }}>Ciljna cena *</label>
                            <input
                                type="text" inputMode="decimal" value={targetPrice}
                                onChange={(e) => { setTargetPrice(e.target.value); if (formErrors.targetPrice) setFormErrors((p) => ({ ...p, targetPrice: undefined })); }}
                                placeholder="89.90"
                                style={{ width: "100%", boxSizing: "border-box", padding: "8px 10px", borderRadius: 8, border: `1px solid ${formErrors.targetPrice ? "var(--c-ef4444, #ef4444)" : "var(--c-2a3045, #2A3045)"}`, fontSize: 13, background: "var(--c-1a1f2e, #1A1F2E)", color: "var(--c-c9d3e4, #c9d3e4)" }}
                            />
                            {formErrors.targetPrice && <div style={{ marginTop: 5, color: "var(--c-f87171, #f87171)", fontSize: 12 }}>{formErrors.targetPrice}</div>}
                        </div>
                    </div>

                    {/* ── Local-signal inputs ── */}
                    <div style={{ borderTop: "1px solid var(--c-2a3045, #2A3045)", paddingTop: 10 }}>
                        <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: ".06em", color: "var(--c-8a95b0, #8A95B0)", marginBottom: 8 }}>Lokalni signali (opcionalno)</div>

                        {/* Veličina + Boja */}
                        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 8 }}>
                            <div>
                                <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "var(--c-8a95b0, #8A95B0)", marginBottom: 5 }}>Veličina</label>
                                <input
                                    value={velicina}
                                    onChange={(e) => setVelicina(e.target.value)}
                                    placeholder="npr. 42"
                                    style={{ width: "100%", boxSizing: "border-box", padding: "8px 10px", borderRadius: 8, border: "1px solid var(--c-2a3045, #2A3045)", fontSize: 13, background: "var(--c-1a1f2e, #1A1F2E)", color: "var(--c-c9d3e4, #c9d3e4)" }}
                                />
                            </div>
                            <div>
                                <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "var(--c-8a95b0, #8A95B0)", marginBottom: 5 }}>Boja</label>
                                <input
                                    value={boja}
                                    onChange={(e) => setBoja(e.target.value)}
                                    placeholder="npr. Crna"
                                    style={{ width: "100%", boxSizing: "border-box", padding: "8px 10px", borderRadius: 8, border: "1px solid var(--c-2a3045, #2A3045)", fontSize: 13, background: "var(--c-1a1f2e, #1A1F2E)", color: "var(--c-c9d3e4, #c9d3e4)" }}
                                />
                            </div>
                        </div>

                        {/* Materijal */}
                        <div>
                            <label style={{ display: "block", fontSize: 12, fontWeight: 600, color: "var(--c-8a95b0, #8A95B0)", marginBottom: 5 }}>Materijal gornjišta</label>
                            <select
                                value={materijal}
                                onChange={(e) => setMaterijal(e.target.value)}
                                style={{ width: "100%", padding: "8px 10px", borderRadius: 8, border: "1px solid var(--c-2a3045, #2A3045)", fontSize: 13, background: "var(--c-1a1f2e, #1A1F2E)", color: "var(--c-c9d3e4, #c9d3e4)" }}
                            >
                                <option value="">— Izaberi materijal —</option>
                                {MATERIAL_OPTIONS.map((m) => (
                                    <option key={m.value} value={m.value}>{m.label}</option>
                                ))}
                            </select>
                        </div>
                    </div>

                    <div className="grid grid-cols-2 gap-2">
                        <button type="submit" disabled={loading} className={`w-full rounded-md py-3 text-white font-bold ${loading ? 'bg-muted/70 cursor-not-allowed' : 'bg-info'}`}>
                            {loading ? (
                                <span className="flex items-center justify-center gap-2">
                                    <span className="inline-block w-3.5 h-3.5 border-2 border-white/50 border-t-white rounded-full animate-spin" />
                                    Racunam...
                                </span>
                            ) : "Pokreni procenu"}
                        </button>
                        <button type="button" onClick={handleSaveScenario} disabled={loading || !result} className={`w-full rounded-md py-3 border ${loading || !result ? 'border-muted text-muted bg-surface' : 'border-muted text-contrast bg-surface'}`}>
                            Sacuvaj scenario
                        </button>
                    </div>
                </div>

                {/* ── RIGHT: results panel ── */}
                <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>

                    {error && (
                        <div className="bg-error/10 text-error border border-error rounded-md p-3 text-sm">⚠️ {error}</div>
                    )}

                    {/* loading skeleton */}
                    {loading && (
                        <div className="bg-surface border border-muted rounded-md p-6">
                            {[80, 60, 45, 70, 55].map((w, i) => (
                                <div key={i} className="h-3.5 rounded-md bg-muted mb-3" style={{ width: `${w}%`, animationDelay: `${i * 0.15}s` }} />
                            ))}
                        </div>
                    )}

                    {/* empty state */}
                    {!result && !loading && !error && (
                        <div style={{ background: "var(--c-161a23, #161A23)", border: "1px solid var(--c-2a3045, #2A3045)", borderRadius: 12, padding: "32px 20px", textAlign: "center", color: "var(--c-8a95b0, #8A95B0)" }}>
                            <svg width={40} height={40} fill="none" stroke="var(--c-3a4565, #3A4565)" strokeWidth={1.5} viewBox="0 0 24 24" style={{ margin: "0 auto 10px" }}>
                                <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" />
                            </svg>
                            <div style={{ fontSize: 14, fontWeight: 600, color: "var(--c-8a95b0, #8A95B0)" }}>Čeka na unos</div>
                            <div style={{ fontSize: 12, marginTop: 4, color: "var(--c-3a4565, #3A4565)" }}>Popuni formu i klikni „Izračunaj score"</div>
                        </div>
                    )}

                    {result && (
                        <>
                            {/* ── verdict banner ── */}
                            <div className={`rounded-xl p-4 border ${verdictCls.container} flex items-center gap-5`}>
                                <ScoreRing score={result.finalScore} color={verdictCls.ring} />
                                <div className="flex-1">
                                    <div className="flex items-baseline gap-3 flex-wrap">
                                        <span className="text-2xl font-extrabold">{result.verdict}</span>
                                        <span className="text-sm font-semibold rounded px-2 py-0.5 bg-opacity-10" style={{ backgroundImage: 'none' }}>{result.scoreLabel}</span>
                                    </div>
                                    {/* sell probability bar */}
                                    <div className="mt-2">
                                        <div className="flex justify-between text-sm text-muted mb-1">
                                            <span>Verovatnoća prodaje ({result.market})</span>
                                            <strong className={`${sellProbabilityPercent >= 60 ? 'text-success' : sellProbabilityPercent >= 30 ? 'text-warning' : 'text-error'}`}>{sellProbabilityPercent.toFixed(1)}%</strong>
                                        </div>
                                        <div className="h-2 rounded overflow-hidden bg-muted/30">
                                            <div className={`${sellProbabilityPercent >= 60 ? 'bg-success' : sellProbabilityPercent >= 30 ? 'bg-warning' : 'bg-error'} h-full`} style={{ width: `${sellProbabilityPercent}%`, transition: 'width .5s' }} />
                                        </div>
                                    </div>
                                    {/* meta badges */}
                                    <div className="flex flex-wrap gap-2 mt-3">
                                        <span className="px-2 py-1 rounded-full text-xs font-semibold bg-surface text-info">{result.usedPythonModel ? "Python model ✓" : "Lokalni model"}</span>
                                        <span className={`px-2 py-1 rounded-full text-xs font-semibold ${result.hasTrainingSignal ? 'bg-success/10 text-success' : 'bg-surface text-muted'}`}>{result.hasTrainingSignal ? "Trening signal ✓" : "Nema trening signala"}</span>
                                        <span className="px-2 py-1 rounded-full text-xs font-semibold bg-success/10 text-success">Pouzdanost: {result.confidence.toFixed(0)}%</span>
                                        <span className="px-2 py-1 rounded-full text-xs font-semibold bg-surface text-muted">{result.sourceCoverageCount} izvora</span>
                                        {result.pricePositioning && (
                                            <span className="px-2 py-1 rounded-full text-xs font-semibold bg-warning/10 text-warning">{result.pricePositioning === "ispod_tržišta" ? "⬇ Ispod tržišta" : result.pricePositioning === "iznad_tržišta" ? "⬆ Iznad tržišta" : "↔ U rangu tržišta"}</span>
                                        )}
                                    </div>
                                    {/* price info */}
                                    <div className="mt-3 flex gap-4 text-sm text-muted flex-wrap">
                                        <span>Preporučeni opseg: <strong className="text-contrast">{result.recommendedPriceRange}</strong></span>
                                        {result.typicalPrice != null && <span>Tipična cena: <strong className="text-contrast">{result.currency ?? "EUR"} {result.typicalPrice}</strong></span>}
                                    </div>
                                </div>
                            </div>

                            {/* ── insights ── */}
                            {result.insights.length > 0 && (
                                <div style={{ background: "var(--c-161a23, #161A23)", border: "1px solid var(--c-2a3045, #2A3045)", borderRadius: 12, padding: "14px 16px" }}>
                                    <div style={{ fontWeight: 700, fontSize: 13, color: "var(--c-c9d3e4, #c9d3e4)", marginBottom: 10 }}>Zaključci i preporuke</div>
                                    <ul style={{ margin: 0, padding: 0, listStyle: "none", display: "flex", flexDirection: "column", gap: 6 }}>
                                        {result.insights.map((insight, i) => (
                                            <li key={i} style={{ fontSize: 13, color: "var(--c-8a95b0, #8A95B0)", display: "flex", alignItems: "flex-start", lineHeight: "1.5" }}>
                                                <span style={{ marginRight: 8 }}>{getInsightIcon(insight)}</span>
                                                <span>{cleanInsightText(insight)}</span>
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                            )}

                            {/* ── score groups ── */}
                            <div style={{ background: "var(--c-161a23, #161A23)", border: "1px solid var(--c-2a3045, #2A3045)", borderRadius: 12, padding: "14px 16px" }}>
                                <div style={{ fontWeight: 700, fontSize: 13, color: "var(--c-c9d3e4, #c9d3e4)", marginBottom: 12 }}>Detaljan pregled bodova</div>
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
                                                <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: ".06em", color: "var(--c-8a95b0, #8A95B0)", marginBottom: 4 }}>{group}</div>
                                            {keys.map((k) => <ScoreRow key={k} fieldKey={k} value={values[k] ?? 0} />)}
                                        </div>
                                    );
                                })}
                            </div>

                            {/* ── similar products ── */}
                            <div style={{ background: "var(--c-161a23, #161A23)", border: "1px solid var(--c-2a3045, #2A3045)", borderRadius: 12, padding: "14px 16px" }}>
                                <div style={{ fontWeight: 700, fontSize: 13, color: "var(--c-c9d3e4, #c9d3e4)", marginBottom: 10 }}>
                                    Slični proizvodi {result.similarProducts.length > 0 && <span style={{ fontWeight: 400, color: "var(--c-8a95b0, #8A95B0)" }}>({result.similarProducts.length})</span>}
                                </div>
                                {result.similarProducts.length === 0 ? (
                                    <div style={{ color: "var(--c-8a95b0, #8A95B0)", fontSize: 13 }}>Nema vizuelno sličnih proizvoda u bazi.</div>
                                ) : (
                                    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                                        {result.similarProducts.map((sp) => {
                                            const imageUrl = getRuntimeProductImageUrl(sp.imageFileName);
                                            const simPct = sp.similarity <= 1 ? sp.similarity * 100 : sp.similarity;
                                            const simColor = simPct >= 75 ? "var(--c-15803d, #15803d)" : simPct >= 55 ? "var(--c-d97706, #d97706)" : "var(--c-dc2626, #dc2626)";
                                            return (
                                                <div
                                                    key={`${sp.productId}-${sp.productName}`}
                                                    style={{ display: "grid", gridTemplateColumns: "52px 1fr auto", gap: 10, alignItems: "center", border: "1px solid var(--c-2a3045, #2A3045)", borderRadius: 10, padding: "8px 10px", background: "var(--c-1a1f2e, #1A1F2E)" }}
                                                >
                                                    <div style={{ width: 52, height: 52, borderRadius: 8, background: "var(--c-1e2332, #1E2332)", border: "1px solid var(--c-2a3045, #2A3045)", overflow: "hidden", display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
                                                        {imageUrl ? (
                                                            <img src={imageUrl} alt={sp.productName} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                                                        ) : (
                                                            <svg width={20} height={20} fill="none" stroke="var(--c-3a4565, #3A4565)" strokeWidth={1.5} viewBox="0 0 24 24">
                                                                <path strokeLinecap="round" strokeLinejoin="round" d="m2.25 15.75 5.159-5.159a2.25 2.25 0 0 1 3.182 0l5.159 5.159m-1.5-1.5 1.409-1.409a2.25 2.25 0 0 1 3.182 0l2.909 2.909M3 20.25h18A2.25 2.25 0 0 0 23.25 18V6A2.25 2.25 0 0 0 21 3.75H3A2.25 2.25 0 0 0 .75 6v12A2.25 2.25 0 0 0 3 20.25Z" />
                                                            </svg>
                                                        )}
                                                    </div>
                                                    <div style={{ minWidth: 0 }}>
                                                        <div style={{ fontWeight: 600, fontSize: 13, color: "var(--c-c9d3e4, #c9d3e4)", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                                                            {sp.productName || `Proizvod #${sp.productId}`}
                                                        </div>
                                                        <div style={{ display: "flex", gap: 6, marginTop: 3, flexWrap: "wrap" }}>
                                                            {sp.brand && <span style={{ fontSize: 11, background: "var(--c-0e1e38, #0e1e38)", color: "var(--c-60a5fa, #60a5fa)", borderRadius: 5, padding: "1px 6px" }}>{sp.brand}</span>}
                                                            {sp.shoeType && <span style={{ fontSize: 11, background: "var(--c-0d2118, #0d2118)", color: "var(--c-4ade80, #4ade80)", borderRadius: 5, padding: "1px 6px" }}>{sp.shoeType}</span>}
                                                            {!sp.brand && !sp.shoeType && <span style={{ fontSize: 11, color: "var(--c-3a4565, #3A4565)" }}>#{sp.productId}</span>}
                                                        </div>
                                                    </div>
                                                    <div style={{ textAlign: "right" }}>
                                                        <div style={{ fontWeight: 700, fontSize: 14, color: simColor }}>{simPct.toFixed(1)}%</div>
                                                        <div style={{ fontSize: 10, color: "var(--c-8a95b0, #8A95B0)" }}>sličnost</div>
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




