import { useState, useEffect, useCallback } from "react";
import {
    fetchOpenTrainingStats,
    fetchOpenTrainingDatasets,
    fetchTopLabels,
    fetchShoeTypes,
    fetchBrands,
    recomputeLabels,
    fetchDiagnostics,
    type OpenTrainingStats,
    type OpenTrainingDataset,
    type TopLabel,
    type ShoeTypeCount,
    type BrandCount,
    type Diagnostics,
} from "../services/openTrainingApi";

//  Palette 
const C = {
    blue:        "var(--c-4f46e5, #4f46e5)",
    blueLight:   "var(--c-eef2ff, #eef2ff)",
    green:       "var(--c-16a34a, #16a34a)",
    greenLight:  "var(--c-dcfce7, #dcfce7)",
    amber:       "var(--c-d97706, #d97706)",
    amberLight:  "var(--c-fef3c7, #fef3c7)",
    red:         "var(--c-dc2626, #dc2626)",
    redLight:    "var(--c-fee2e2, #fee2e2)",
    purple:      "var(--c-7c3aed, #7c3aed)",
    purpleLight: "var(--c-f5f3ff, #f5f3ff)",
    gray:        "var(--c-6b7280, #6b7280)",
    border:      "var(--c-e5e7eb, #e5e7eb)",
    card:        "var(--c-ffffff, #ffffff)",
    bg:          "var(--c-f9fafb, #f9fafb)",
};

type Tab = "top" | "datasets" | "compute" | "diag";

function fmt(n: number | undefined | null): string {
    if (n == null) return "";
    return n.toLocaleString();
}
function fmtDate(s: string): string {
    return new Date(s).toLocaleString("sr-RS", { dateStyle: "short", timeStyle: "short" });
}

//  ScoreBar 
function ScoreBar({ score, color }: { score: number; color: string }) {
    return (
        <div className="flex items-center gap-2 min-w-[120px]">
            <div className="flex-1 h-1.5 rounded overflow-hidden bg-muted/20">
                <div style={{ width: `${Math.min(score, 100)}%`, height: "100%", background: color, transition: "width .4s" }} />
            </div>
            <span style={{ color }} className="font-extrabold text-base min-w-[40px] text-right">{score.toFixed(1)}</span>
        </div>
    );
}

//  Chip 
function Chip({ label, count, active, onClick }: { label: string; count?: number; active: boolean; onClick: () => void; }) {
    return (
        <button
            onClick={onClick}
            className={`px-3 py-1.5 rounded-full text-sm font-semibold whitespace-nowrap transition-all ${active ? 'bg-info text-white border-info' : 'bg-surface border border-muted text-muted'}`}
        >
            {label}{count != null ? <span className="opacity-70 ml-1">({fmt(count)})</span> : null}
        </button>
    );
}

//  TabBtn 
function TabBtn({ id, label, active, onClick }: { id: Tab; label: string; active: boolean; onClick: (t: Tab) => void; }) {
    return (
        <button
            onClick={() => onClick(id)}
            className={`px-4 py-2 rounded-md text-sm font-bold transition-all ${active ? 'bg-info text-white border-info' : 'bg-white text-muted border border-muted'}`}
        >
            {label}
        </button>
    );
}

//  Main Page 
export default function OpenTrainingPage() {
    const [tab, setTab] = useState<Tab>("top");

    //  stats (header) 
    const [stats, setStats] = useState<OpenTrainingStats | null>(null);

    const loadStats = useCallback(async () => {
        try { setStats(await fetchOpenTrainingStats()); } catch { /* silent */ }
    }, []);

    useEffect(() => { void loadStats(); }, [loadStats]);

    //  shoe types 
    const [shoeTypes, setShoeTypes] = useState<ShoeTypeCount[]>([]);

    useEffect(() => {
        fetchShoeTypes().then(setShoeTypes).catch(() => setShoeTypes([]));
    }, []);

    //  top products 
    const [labelType, setLabelType] = useState<"popularity_prior" | "deal_score">("popularity_prior");
    const [selectedShoeType, setSelectedShoeType] = useState<string>("");
    const [selectedBrand, setSelectedBrand] = useState<string>("");
    const [topTake, setTopTake] = useState(20);
    const [topLabels, setTopLabels] = useState<TopLabel[]>([]);
    const [topLoading, setTopLoading] = useState(false);
    const [topErr, setTopErr] = useState<string | null>(null);

    const [brands, setBrands] = useState<BrandCount[]>([]);

    useEffect(() => {
        setSelectedBrand("");
        fetchBrands(selectedShoeType || undefined).then(setBrands).catch(() => setBrands([]));
    }, [selectedShoeType]);

    const loadTop = useCallback(async () => {
        setTopLoading(true);
        setTopErr(null);
        try {
            setTopLabels(await fetchTopLabels(
                labelType, topTake,
                selectedShoeType || undefined,
                selectedBrand || undefined,
            ));
        } catch (e: unknown) {
            setTopErr(e instanceof Error ? e.message : "Greška");
        } finally {
            setTopLoading(false);
        }
    }, [labelType, topTake, selectedShoeType, selectedBrand]);

    useEffect(() => {
        if (tab === "top") void loadTop();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [tab, labelType, topTake, selectedShoeType, selectedBrand]);

    //  datasets 
    const [datasets, setDatasets] = useState<OpenTrainingDataset[]>([]);
    const [datasetsLoading, setDatasetsLoading] = useState(false);
    const [datasetsErr, setDatasetsErr] = useState<string | null>(null);

    const loadDatasets = useCallback(async () => {
        setDatasetsLoading(true);
        setDatasetsErr(null);
        try { setDatasets(await fetchOpenTrainingDatasets()); }
        catch (e: unknown) { setDatasetsErr(e instanceof Error ? e.message : "Greška"); }
        finally { setDatasetsLoading(false); }
    }, []);

    useEffect(() => {
        if (tab === "datasets" || tab === "compute") void loadDatasets();
    }, [tab, loadDatasets]);

    //  recompute 
    const [rcSelected, setRcSelected] = useState<Set<string>>(new Set());
    const [rcMin, setRcMin] = useState(10);
    const [rcLoading, setRcLoading] = useState(false);
    const [rcResult, setRcResult] = useState<{
        datasetCount: number; candidateProducts: number; scoredProducts: number;
        groupCount: number; removedLabels: number; insertedLabels: number; computedAtUtc: string;
    } | null>(null);
    const [rcErr, setRcErr] = useState<string | null>(null);

    const toggleDataset = (name: string) => {
        setRcSelected(prev => {
            const next = new Set(prev);
            next.has(name) ? next.delete(name) : next.add(name);
            return next;
        });
    };

    const handleRecompute = async () => {
        setRcLoading(true);
        setRcResult(null);
        setRcErr(null);
        try {
            const names = Array.from(rcSelected);
            const res = await recomputeLabels({
                datasetNames: names.length > 0 ? names : undefined,
                minProductsPerGroup: rcMin,
            });
            setRcResult(res);
            void loadStats();
        } catch (e: unknown) {
            setRcErr(e instanceof Error ? e.message : "Greška");
        } finally {
            setRcLoading(false);
        }
    };

    const scoreColor = labelType === "popularity_prior" ? C.amber : C.red;

    // ── diagnostics ─────────────────────────────────────────────────────────
    const [diag, setDiag] = useState<Diagnostics | null>(null);
    const [diagLoading, setDiagLoading] = useState(false);
    const [diagErr, setDiagErr] = useState<string | null>(null);
    const [diagLabelType, setDiagLabelType] = useState<"popularity_prior" | "deal_score">("popularity_prior");

    const loadDiag = useCallback(async () => {
        setDiagLoading(true);
        setDiagErr(null);
        try { setDiag(await fetchDiagnostics(diagLabelType)); }
        catch (e: unknown) { setDiagErr(e instanceof Error ? e.message : "Greška"); }
        finally { setDiagLoading(false); }
    }, [diagLabelType]);

    useEffect(() => {
        if (tab === "diag") void loadDiag();
    }, [tab, loadDiag]);

    return (
        <div className="max-w-[1000px] mx-auto pb-12">

            {/*  Header  */}
            <div className="rounded-2xl p-5 mb-5 bg-surface-elevated text-contrast">
                <div className="text-2xl font-extrabold">Open Product Training</div>
                <div className="text-sm opacity-80 mt-1 mb-3">Popularity &amp; deal score iz open dataset-ova koriste se kao signal za Trending Score</div>
                {stats && (
                    <div className="flex gap-6 flex-wrap">
                        {[
                            { label: "Dataseta",          value: fmt(stats.datasetCount),          icon: "" },
                            { label: "Proizvoda",          value: fmt(stats.productCount),          icon: "" },
                            { label: "Popularity labela",  value: fmt(stats.popularityLabelCount),  icon: "" },
                            { label: "Deal labela",        value: fmt(stats.dealLabelCount),        icon: "" },
                        ].map(s => (
                            <div key={s.label}>
                                <div className="text-xs opacity-70">{s.icon} {s.label}</div>
                                <div className="text-xl font-extrabold leading-tight">{s.value}</div>
                            </div>
                        ))}
                    </div>
                )}
            </div>

            {/*  Tabs  */}
            <div style={{ display: "flex", gap: 8, marginBottom: 20 }}>
                <TabBtn id="top"      label=" Top Proizvodi" active={tab === "top"}      onClick={setTab} />
                <TabBtn id="datasets" label=" Dataseti"      active={tab === "datasets"} onClick={setTab} />
                <TabBtn id="compute"  label="⚙️ Recompute"     active={tab === "compute"}  onClick={setTab} />
                <TabBtn id="diag"     label="📊 Dijagnostika"  active={tab === "diag"}     onClick={setTab} />
            </div>

            {/*  TOP PRODUCTS  */}
            {tab === "top" && (
                <div>
                    {/* Filter bar */}
                    <div style={{
                        background: C.card, border: `1.5px solid ${C.border}`,
                        borderRadius: 12, padding: "16px 18px", marginBottom: 14,
                    }}>
                        {/* Score type toggle */}
                        <div style={{ display: "flex", gap: 0, borderRadius: 8, overflow: "hidden", border: `1.5px solid ${C.border}`, width: "fit-content", marginBottom: 14 }}>
                            {(["popularity_prior", "deal_score"] as const).map(lt => (
                                <button
                                    key={lt}
                                    onClick={() => setLabelType(lt)}
                                    style={{
                                        padding: "7px 20px", fontSize: 13, fontWeight: 700, cursor: "pointer", border: "none",
                                        background: labelType === lt ? (lt === "popularity_prior" ? C.amber : C.red) : "white",
                                        color: labelType === lt ? "white" : "var(--c-374151, #374151)",
                                    }}
                                >
                                    {lt === "popularity_prior" ? " Popularity" : " Deal Score"}
                                </button>
                            ))}
                        </div>

                        {/* Shoe type chips */}
                        {shoeTypes.length > 0 && (
                            <div style={{ marginBottom: 12 }}>
                                <div style={{ fontSize: 11, color: C.gray, marginBottom: 6, fontWeight: 600, textTransform: "uppercase", letterSpacing: .5 }}>
                                    Tip obuće
                                </div>
                                <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                                    <Chip label="Sve" active={selectedShoeType === ""} onClick={() => setSelectedShoeType("")} />
                                    {shoeTypes.map(st => (
                                        <Chip
                                            key={st.shoeType}
                                            label={st.shoeType}
                                            count={st.productCount}
                                            active={selectedShoeType === st.shoeType}
                                            onClick={() => setSelectedShoeType(selectedShoeType === st.shoeType ? "" : st.shoeType)}
                                        />
                                    ))}
                                </div>
                            </div>
                        )}

                        {/* Brand + take */}
                        <div style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
                            {brands.length > 0 && (
                                <select
                                    value={selectedBrand}
                                    onChange={e => setSelectedBrand(e.target.value)}
                                    style={{ padding: "6px 10px", borderRadius: 7, border: `1.5px solid ${C.border}`, fontSize: 12, cursor: "pointer", minWidth: 140 }}
                                >
                                    <option value="">Sve marke</option>
                                    {brands.map(b => (
                                        <option key={b.brand} value={b.brand}>{b.brand} ({fmt(b.productCount)})</option>
                                    ))}
                                </select>
                            )}
                            <select
                                value={topTake}
                                onChange={e => setTopTake(Number(e.target.value))}
                                style={{ padding: "6px 10px", borderRadius: 7, border: `1.5px solid ${C.border}`, fontSize: 12, cursor: "pointer" }}
                            >
                                {[10, 20, 50, 100].map(n => <option key={n} value={n}>Top {n}</option>)}
                            </select>
                            {topLoading && <span style={{ fontSize: 12, color: C.gray }}> Učitavam</span>}
                        </div>
                    </div>

                    {topErr && (
                        <div style={{ background: C.redLight, color: C.red, padding: "10px 14px", borderRadius: 10, fontSize: 13, marginBottom: 12 }}>
                             {topErr}
                        </div>
                    )}

                    {!topLoading && !topErr && topLabels.length === 0 && (
                        <div style={{ background: C.amberLight, color: C.amber, padding: "16px 20px", borderRadius: 12, fontSize: 13 }}>
                            <strong>Nema rezultata.</strong> Pokreni <strong>Recompute</strong> da generišeš labele, ili promeni filtere.
                        </div>
                    )}

                    {topLabels.length > 0 && (
                        <>
                            <div style={{ fontSize: 11, color: C.gray, marginBottom: 8 }}>
                                {fmt(topLabels.length)} proizvoda
                                {selectedShoeType ? `  ${selectedShoeType}` : ""}
                                {selectedBrand ? `  ${selectedBrand}` : ""}
                                {"  "}{labelType === "popularity_prior" ? "Popularity Prior" : "Deal Score"}
                            </div>
                            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                                {topLabels.map((item, i) => (
                                    <div key={item.productId} style={{
                                        background: C.card, border: `1.5px solid ${C.border}`,
                                        borderRadius: 10, padding: "10px 14px",
                                        display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap",
                                    }}>
                                        <div style={{
                                            width: 30, height: 30, borderRadius: "50%", flexShrink: 0,
                                            background: i === 0 ? "var(--c-fbbf24, #fbbf24)" : i === 1 ? "var(--c-9ca3af, #9ca3af)" : i === 2 ? "var(--c-b45309, #b45309)" : C.bg,
                                            color: i < 3 ? "white" : C.gray,
                                            display: "flex", alignItems: "center", justifyContent: "center",
                                            fontWeight: 800, fontSize: 12,
                                        }}>
                                            {i + 1}
                                        </div>
                                        {item.imageUrl ? (
                                            <img
                                                src={item.imageUrl} alt={item.title}
                                                style={{ width: 44, height: 44, objectFit: "contain", borderRadius: 8, flexShrink: 0 }}
                                                onError={e => { (e.target as HTMLImageElement).style.display = "none"; }}
                                            />
                                        ) : (
                                            <div style={{ width: 44, height: 44, background: C.bg, borderRadius: 8, flexShrink: 0, display: "flex", alignItems: "center", justifyContent: "center", fontSize: 18 }}></div>
                                        )}
                                        <div style={{ flex: "1 1 180px", minWidth: 0 }}>
                                            <div style={{ fontWeight: 600, fontSize: 13, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                                                {item.title}
                                            </div>
                                            <div style={{ display: "flex", gap: 5, marginTop: 4, flexWrap: "wrap" }}>
                                                {item.brand && (
                                                    <span style={{ background: C.blueLight, color: C.blue, fontSize: 11, fontWeight: 700, padding: "2px 7px", borderRadius: 20 }}>
                                                        {item.brand}
                                                    </span>
                                                )}
                                                {item.shoeType && (
                                                    <span style={{ background: C.bg, color: C.gray, fontSize: 11, padding: "2px 7px", borderRadius: 20, border: `1px solid ${C.border}` }}>
                                                        {item.shoeType}
                                                    </span>
                                                )}
                                                {item.price != null && (
                                                    <span style={{ fontSize: 11, color: C.gray }}>
                                                        {item.price.toFixed(2)} {item.currency ?? "EUR"}
                                                    </span>
                                                )}
                                            </div>
                                        </div>
                                        <div style={{ minWidth: 130, flexShrink: 0 }}>
                                            <ScoreBar score={item.score} color={scoreColor} />
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </>
                    )}

                    <div style={{ marginTop: 20, background: C.blueLight, borderRadius: 10, padding: "12px 16px", fontSize: 12, color: "var(--c-3730a3, #3730a3)" }}>
                         <strong>Kako se ovo koristi?</strong> Popularity Prior i Deal Score iz ovih dataseta se automatski
                        primenjuju kao signal pri računanju <strong>Trending Score-a</strong> za svaki artikal u katalogu.
                        Filtriraj po tipu obuće da vidiš referentne vrednosti za konkretan segment.
                    </div>
                </div>
            )}

            {/*  DATASETS  */}
            {tab === "datasets" && (
                <div>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 14 }}>
                        <div style={{ fontWeight: 700, fontSize: 15 }}>Registrovani dataseti</div>
                        <button onClick={loadDatasets} style={{ fontSize: 12, color: C.blue, background: "none", border: "none", cursor: "pointer" }}> Osveži</button>
                    </div>

                    {datasetsLoading && <p style={{ color: C.gray, fontSize: 14 }}> Učitavanje</p>}
                    {datasetsErr && (
                        <div style={{ background: C.redLight, color: C.red, padding: 12, borderRadius: 10, fontSize: 13 }}> {datasetsErr}</div>
                    )}
                    {!datasetsLoading && datasets.length === 0 && !datasetsErr && (
                        <div style={{ background: C.amberLight, color: C.amber, padding: "16px 20px", borderRadius: 12, fontSize: 13 }}>
                            <strong>Nema unetih dataseta.</strong> Unesi dataset u tabelu <code>dataset</code>, pa pokreni Recompute.
                        </div>
                    )}
                    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                        {datasets.map(ds => (
                            <div key={ds.id} style={{
                                background: C.card, border: `1.5px solid ${C.border}`, borderRadius: 12,
                                padding: "14px 18px", display: "flex", gap: 14, alignItems: "center", flexWrap: "wrap",
                            }}>
                                <div style={{ flex: "1 1 200px" }}>
                                    <div style={{ fontWeight: 700, fontSize: 14 }}>{ds.name}</div>
                                    <div style={{ fontSize: 12, color: C.gray, marginTop: 2 }}>{ds.description ?? ""}</div>
                                    {ds.rawLocation && <div style={{ fontSize: 11, color: C.gray, marginTop: 3 }}> {ds.rawLocation}</div>}
                                </div>
                                <div style={{ display: "flex", gap: 6, flexWrap: "wrap", alignItems: "center" }}>
                                    <span style={{ background: C.blueLight, color: C.blue, fontSize: 11, fontWeight: 700, padding: "3px 10px", borderRadius: 20 }}>{ds.sourceType}</span>
                                    <span style={{ background: C.greenLight, color: C.green, fontSize: 11, fontWeight: 700, padding: "3px 10px", borderRadius: 20 }}> {fmt(ds.productCount)}</span>
                                    {ds.license && <span style={{ background: C.purpleLight, color: C.purple, fontSize: 11, fontWeight: 700, padding: "3px 10px", borderRadius: 20 }}> {ds.license}</span>}
                                    <span style={{ fontSize: 11, color: C.gray }}>{fmtDate(ds.createdAt)}</span>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            {/*  RECOMPUTE  */}
            {tab === "compute" && (
                <div style={{ maxWidth: 560 }}>
                    <div style={{ background: C.card, border: `1.5px solid ${C.border}`, borderRadius: 14, padding: "22px 24px" }}>
                        <div style={{ fontWeight: 700, fontSize: 16, marginBottom: 4 }}> Recompute Training Labela</div>
                        <div style={{ fontSize: 13, color: C.gray, marginBottom: 20 }}>
                            Izračunava <strong>Popularity Prior</strong> i <strong>Deal Score</strong> po brendu i tipu obuće.
                            Rezultati se odmah koriste u Trending Score-u.
                        </div>

                        {/* Dataset multiselect */}
                        <div style={{ marginBottom: 18 }}>
                            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 8 }}>
                                <div style={{ fontSize: 12, fontWeight: 600 }}>
                                    Dataseti
                                    <span style={{ color: C.gray, fontWeight: 400, marginLeft: 6 }}>
                                        {rcSelected.size === 0 ? "(svi iz konfiguracije)" : `${rcSelected.size} izabrano`}
                                    </span>
                                </div>
                                {datasets.length > 0 && (
                                    <div style={{ display: "flex", gap: 8 }}>
                                        <button onClick={() => setRcSelected(new Set(datasets.map(d => d.name)))} style={{ fontSize: 11, color: C.blue, background: "none", border: "none", cursor: "pointer", textDecoration: "underline" }}>Sve</button>
                                        <button onClick={() => setRcSelected(new Set())} style={{ fontSize: 11, color: C.gray, background: "none", border: "none", cursor: "pointer", textDecoration: "underline" }}>Resetuj</button>
                                    </div>
                                )}
                            </div>

                            {datasetsLoading && <div style={{ fontSize: 12, color: C.gray }}> Učitavanje</div>}
                            {!datasetsLoading && datasets.length === 0 && (
                                <div style={{ background: C.amberLight, color: C.amber, borderRadius: 8, padding: "10px 14px", fontSize: 12 }}>
                                    Nema registrovanih dataseta.
                                </div>
                            )}

                            <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
                                {datasets.map(ds => {
                                    const checked = rcSelected.has(ds.name);
                                    return (
                                        <label
                                            key={ds.id}
                                            onClick={() => toggleDataset(ds.name)}
                                            style={{
                                                display: "flex", alignItems: "center", gap: 10,
                                                padding: "9px 13px", borderRadius: 8, cursor: "pointer",
                                                border: `1.5px solid ${checked ? C.blue : C.border}`,
                                                background: checked ? C.blueLight : C.card,
                                                userSelect: "none",
                                            }}
                                        >
                                            <div style={{
                                                width: 17, height: 17, borderRadius: 4, flexShrink: 0,
                                                border: `2px solid ${checked ? C.blue : "var(--c-d1d5db, #d1d5db)"}`,
                                                background: checked ? C.blue : "white",
                                                display: "flex", alignItems: "center", justifyContent: "center",
                                            }}>
                                                {checked && <span style={{ color: "white", fontSize: 10, lineHeight: 1 }}></span>}
                                            </div>
                                            <div style={{ flex: 1, minWidth: 0 }}>
                                                <span style={{ fontWeight: 700, fontSize: 13, color: checked ? C.blue : "var(--c-111827, #111827)" }}>{ds.name}</span>
                                                {ds.description && <span style={{ fontSize: 11, color: C.gray, marginLeft: 8 }}>{ds.description}</span>}
                                            </div>
                                            <span style={{ fontSize: 11, color: checked ? C.green : C.gray, fontWeight: 700, flexShrink: 0 }}>
                                                {fmt(ds.productCount)}
                                            </span>
                                        </label>
                                    );
                                })}
                            </div>
                        </div>

                        {/* Min products per group */}
                        <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 22 }}>
                            <span style={{ fontSize: 12, fontWeight: 600, flexShrink: 0 }}>Min. po grupi</span>
                            <input
                                type="number" value={rcMin} min={1} max={500}
                                onChange={e => setRcMin(Math.max(1, Number(e.target.value)))}
                                style={{ width: 80, padding: "7px 10px", borderRadius: 7, border: `1.5px solid ${C.border}`, fontSize: 13 }}
                            />
                            <span style={{ fontSize: 11, color: C.gray }}>proizvoda po tipu/brendu</span>
                        </div>

                        <button
                            onClick={handleRecompute}
                            disabled={rcLoading}
                            style={{
                                width: "100%", padding: "12px 0",
                                background: rcLoading ? C.gray : C.blue,
                                color: "white", border: "none", borderRadius: 9,
                                fontSize: 14, fontWeight: 700, cursor: rcLoading ? "not-allowed" : "pointer",
                            }}
                        >
                            {rcLoading ? " Izračunavanje" : " Pokreni Recompute"}
                        </button>
                    </div>

                    {rcErr && (
                        <div style={{ marginTop: 12, background: C.redLight, color: C.red, padding: "12px 16px", borderRadius: 10, fontSize: 13 }}>
                             {rcErr}
                        </div>
                    )}

                    {rcResult && (
                        <div style={{ marginTop: 14, background: C.greenLight, border: `1.5px solid var(--c-86efac, #86efac)`, borderRadius: 12, padding: "16px 20px" }}>
                            <div style={{ fontWeight: 700, color: C.green, fontSize: 14, marginBottom: 10 }}>
                                 Gotovo  {fmtDate(rcResult.computedAtUtc)}
                            </div>
                            <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                                {[
                                    { label: "Dataseta",       value: rcResult.datasetCount },
                                    { label: "Kandidata",      value: rcResult.candidateProducts },
                                    { label: "Okskorirano",    value: rcResult.scoredProducts },
                                    { label: "Grupa",          value: rcResult.groupCount },
                                    { label: "Ubačeno labela", value: rcResult.insertedLabels },
                                ].map(r => (
                                    <div key={r.label} style={{ background: "white", borderRadius: 8, padding: "8px 12px", flex: "1 1 80px", textAlign: "center" }}>
                                        <div style={{ fontSize: 18, fontWeight: 800, color: C.green }}>{fmt(r.value)}</div>
                                        <div style={{ fontSize: 10, color: C.gray, marginTop: 1 }}>{r.label}</div>
                                    </div>
                                ))}
                            </div>
                            <div style={{ marginTop: 10, fontSize: 12, color: C.green }}>
                                 Idi na <strong>Top Proizvodi</strong> da vidiš rezultate filtrirane po tipu obuće.
                            </div>
                        </div>
                    )}
                </div>
            )}

            {/* ══════ DIJAGNOSTIKA ══════════════════════════════════════════ */}
            {tab === "diag" && (
                <div>
                    {/* Label type toggle */}
                    <div style={{ display: "flex", gap: 0, borderRadius: 8, overflow: "hidden", border: `1.5px solid ${C.border}`, width: "fit-content", marginBottom: 16 }}>
                        {(["popularity_prior", "deal_score"] as const).map(lt => (
                            <button key={lt} onClick={() => setDiagLabelType(lt)} style={{
                                padding: "7px 18px", fontSize: 12, fontWeight: 700, cursor: "pointer", border: "none",
                                background: diagLabelType === lt ? C.blue : "white",
                                color: diagLabelType === lt ? "white" : "var(--c-374151, #374151)",
                            }}>
                                {lt === "popularity_prior" ? "⭐ Popularity" : "🔥 Deal Score"}
                            </button>
                        ))}
                        <button onClick={loadDiag} style={{ padding: "7px 14px", fontSize: 12, cursor: "pointer", border: "none", borderLeft: `1px solid ${C.border}`, background: "white", color: C.blue }}>
                            🔄
                        </button>
                    </div>

                    {diagLoading && <p style={{ color: C.gray, fontSize: 14 }}>⏳ Učitavanje dijagnostike…</p>}
                    {diagErr && <div style={{ background: C.redLight, color: C.red, padding: "10px 14px", borderRadius: 10, fontSize: 13, marginBottom: 12 }}>⚠️ {diagErr}</div>}

                    {diag && (
                        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>

                            {/* Score distribution histogram */}
                            <div style={{ background: C.card, border: `1.5px solid ${C.border}`, borderRadius: 12, padding: "16px 20px" }}>
                                <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>
                                    Raspodela skor-ova
                                    {diag.scoreStats && (
                                        <span style={{ fontWeight: 400, fontSize: 12, color: C.gray, marginLeft: 12 }}>
                                            ukupno {fmt(diag.scoreStats.count)} · avg {diag.scoreStats.avg} · median {diag.scoreStats.median} · P25 {diag.scoreStats.p25} · P75 {diag.scoreStats.p75}
                                        </span>
                                    )}
                                </div>
                                {diag.histogram.length === 0 || diag.histogram.every(b => b.count === 0) ? (
                                    <div style={{ color: C.gray, fontSize: 13 }}>Nema labela. Pokreni Recompute.</div>
                                ) : (() => {
                                    const maxCount = Math.max(...diag.histogram.map(b => b.count), 1);
                                    const totalLabels = diag.histogram.reduce((s, b) => s + b.count, 0);
                                    const scoreBucketColor = diagLabelType === "popularity_prior" ? C.amber : C.red;
                                    return (
                                        <div style={{ display: "flex", gap: 4, alignItems: "flex-end", height: 100 }}>
                                            {diag.histogram.map(bin => {
                                                const pct = totalLabels > 0 ? Math.round(bin.count / totalLabels * 100) : 0;
                                                const barH = Math.round((bin.count / maxCount) * 80);
                                                return (
                                                    <div key={bin.rangeLabel} style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", gap: 3 }}>
                                                        {bin.count > 0 && (
                                                            <span style={{ fontSize: 9, color: C.gray, lineHeight: 1 }}>{pct}%</span>
                                                        )}
                                                        <div
                                                            title={`${bin.rangeLabel}: ${fmt(bin.count)} labela`}
                                                            style={{
                                                                width: "100%", height: barH || 3, minHeight: bin.count > 0 ? 8 : 3,
                                                                background: bin.count > 0 ? scoreBucketColor : "var(--c-e5e7eb, #e5e7eb)",
                                                                borderRadius: "3px 3px 0 0", transition: "height .4s",
                                                                opacity: bin.count > 0 ? 0.7 + 0.3 * (bin.count / maxCount) : 1,
                                                            }}
                                                        />
                                                        <span style={{ fontSize: 9, color: C.gray, lineHeight: 1 }}>{bin.rangeLabel.split("-")[0]}</span>
                                                    </div>
                                                );
                                            })}
                                        </div>
                                    );
                                })()}
                                {diag.scoreStats && (
                                    <div style={{ marginTop: 12, display: "flex", gap: 8, flexWrap: "wrap" }}>
                                        {[
                                            { label: "Min",    value: diag.scoreStats.min,    color: C.red   },
                                            { label: "P25",    value: diag.scoreStats.p25,    color: C.amber },
                                            { label: "Median", value: diag.scoreStats.median, color: C.blue  },
                                            { label: "Avg",    value: diag.scoreStats.avg,    color: C.blue  },
                                            { label: "P75",    value: diag.scoreStats.p75,    color: C.green },
                                            { label: "Max",    value: diag.scoreStats.max,    color: C.green },
                                        ].map(s => (
                                            <div key={s.label} style={{ background: C.bg, borderRadius: 8, padding: "6px 12px", textAlign: "center", minWidth: 52 }}>
                                                <div style={{ fontSize: 15, fontWeight: 800, color: s.color }}>{s.value}</div>
                                                <div style={{ fontSize: 10, color: C.gray }}>{s.label}</div>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>

                            {/* Data quality */}
                            {diag.quality && (
                                <div style={{ background: C.card, border: `1.5px solid ${C.border}`, borderRadius: 12, padding: "16px 20px" }}>
                                    <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 14 }}>Kvalitet podataka — {fmt(diag.quality.total)} proizvoda</div>
                                    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                                        {[
                                            { label: "Sa ocenom (rating)",          n: diag.quality.withRating,           color: C.amber },
                                            { label: "Sa brojem recenzija",         n: diag.quality.withReviews,          color: C.amber },
                                            { label: "Sa ocenom I recenzijama",     n: diag.quality.withRatingAndReviews, color: C.green },
                                            { label: "Sa cenom",                   n: diag.quality.withPrice,            color: C.blue  },
                                            { label: "Sa brendom",                 n: diag.quality.withBrand,            color: C.blue  },
                                            { label: "Sa tipom obuće",             n: diag.quality.withShoeType,         color: C.purple },
                                        ].map(row => {
                                            const pct = diag.quality!.total > 0 ? Math.round(row.n / diag.quality!.total * 100) : 0;
                                            return (
                                                <div key={row.label}>
                                                    <div style={{ display: "flex", justifyContent: "space-between", fontSize: 12, marginBottom: 3 }}>
                                                        <span style={{ fontWeight: 600 }}>{row.label}</span>
                                                        <span style={{ color: C.gray }}>{fmt(row.n)} / {fmt(diag.quality!.total)} ({pct}%)</span>
                                                    </div>
                                                    <div style={{ height: 7, background: "var(--c-e5e7eb, #e5e7eb)", borderRadius: 4 }}>
                                                        <div style={{ width: `${pct}%`, height: "100%", background: row.color, borderRadius: 4, transition: "width .5s" }} />
                                                    </div>
                                                </div>
                                            );
                                        })}
                                    </div>
                                </div>
                            )}

                            {/* Top groups */}
                            {diag.topGroups.length > 0 && (
                                <div style={{ background: C.card, border: `1.5px solid ${C.border}`, borderRadius: 12, padding: "16px 20px" }}>
                                    <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 12 }}>
                                        Top grupe (brend + tip obuće)
                                        <span style={{ fontWeight: 400, fontSize: 11, color: C.gray, marginLeft: 8 }}>— grupe sa najviše proizvoda dobijaju stabilnije scoring</span>
                                    </div>
                                    <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
                                        {diag.topGroups.map((g, i) => {
                                            const ratingPct = g.productCount > 0 ? Math.round(g.withRating / g.productCount * 100) : 0;
                                            return (
                                                <div key={i} style={{
                                                    display: "flex", gap: 10, alignItems: "center",
                                                    padding: "8px 12px", borderRadius: 8,
                                                    background: i < 3 ? C.amberLight : C.bg,
                                                    border: `1px solid ${C.border}`,
                                                }}>
                                                    <span style={{ fontWeight: 700, fontSize: 12, color: C.gray, minWidth: 20 }}>#{i + 1}</span>
                                                    <div style={{ flex: 1, minWidth: 0 }}>
                                                        <span style={{ fontWeight: 700, fontSize: 13 }}>{g.brand}</span>
                                                        <span style={{ background: C.blueLight, color: C.blue, fontSize: 11, padding: "1px 7px", borderRadius: 20, marginLeft: 7, fontWeight: 600 }}>{g.shoeType}</span>
                                                    </div>
                                                    <span style={{ fontSize: 12, color: C.gray }}>{fmt(g.productCount)} proi.</span>
                                                    <span style={{
                                                        fontSize: 11, fontWeight: 700, padding: "2px 8px", borderRadius: 20,
                                                        background: ratingPct >= 80 ? C.greenLight : ratingPct >= 40 ? C.amberLight : C.redLight,
                                                        color: ratingPct >= 80 ? C.green : ratingPct >= 40 ? C.amber : C.red,
                                                    }}>⭐ {ratingPct}%</span>
                                                </div>
                                            );
                                        })}
                                    </div>
                                    <div style={{ marginTop: 12, background: C.blueLight, borderRadius: 8, padding: "10px 14px", fontSize: 12, color: "var(--c-3730a3, #3730a3)" }}>
                                        💡 <strong>Šta popravlja rezultate?</strong> Dodaj datasete sa više recenzija (Amazon, eBay) ili ubaci
                                        EU Trend podatke (dataset name: <code>eu_trends_zalando</code>, source type: <code>eutrend</code>).
                                        Više recenzija po grupi → stabilniji, rasprostranjeniji scoring.
                                    </div>
                                </div>
                            )}
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}