import { useState, useEffect, useCallback } from "react";
import {
    syncAmazonShoes,
    getAmazonShoesByType,
    getAmazonShoeCategories,
    deleteAmazonShoeCategory,
    type AmazonShoeProduct,
    type CategorySummary,
    type PagedResult,
} from "../services/amazonShoesApi";

// ── Constants ────────────────────────────────────────────────────────────────

const SHOE_TYPES = [
    "sneakers", "boots", "ankle boots", "sandals", "heels",
    "loafers", "oxfords", "mules", "flats", "running shoes",
    "chelsea boots", "stilettos", "wedges", "espadrilles", "slippers",
];

const STAR_COLOR = "#f59e0b";

// ── Helpers ──────────────────────────────────────────────────────────────────

function StarRating({ rating }: { rating: number }) {
    const full  = Math.floor(rating);
    const half  = rating - full >= 0.4;
    const empty = 5 - full - (half ? 1 : 0);
    return (
        <span title={`${rating.toFixed(1)} / 5`} style={{ color: STAR_COLOR, fontSize: 13, letterSpacing: -1 }}>
            {"★".repeat(full)}{"½".repeat(half ? 1 : 0)}{"☆".repeat(empty)}
        </span>
    );
}

function PriceLabel({ price, original, currency }: { price: number | null; original: number | null; currency: string | null }) {
    if (price == null) return <span style={{ color: "#d1d5db" }}>—</span>;
    const fmt = (v: number) => `${v.toFixed(2)} ${currency ?? ""}`.trim();
    return (
        <span>
            <span style={{ fontWeight: 700, color: "#059669" }}>{fmt(price)}</span>
            {original != null && original > price && (
                <span style={{ marginLeft: 5, color: "#9ca3af", textDecoration: "line-through", fontSize: 11 }}>
                    {fmt(original)}
                </span>
            )}
        </span>
    );
}

// ── Category sidebar ─────────────────────────────────────────────────────────

function CategoryPanel({
    categories,
    selected,
    onSelect,
    onDelete,
}: {
    categories: CategorySummary[];
    selected: string;
    onSelect: (c: string) => void;
    onDelete: (c: string) => void;
}) {
    return (
        <div style={{ minWidth: 200 }}>
            <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", color: "#6b7280", marginBottom: 8, letterSpacing: "0.06em" }}>
                Categories in DB
            </div>
            {categories.length === 0 && (
                <div style={{ color: "#9ca3af", fontSize: 12, fontStyle: "italic" }}>No data yet — sync first</div>
            )}
            {categories.map((c) => (
                <div
                    key={c.category ?? "null"}
                    style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        padding: "6px 10px",
                        borderRadius: 8,
                        marginBottom: 4,
                        background: selected === (c.category ?? "") ? "#4f46e5" : "#f9fafb",
                        border: `1px solid ${selected === (c.category ?? "") ? "#4f46e5" : "#e5e7eb"}`,
                        cursor: "pointer",
                        transition: "all .12s",
                    }}
                    onClick={() => onSelect(c.category ?? "")}
                >
                    <div>
                        <div style={{ fontWeight: 600, fontSize: 13, color: selected === (c.category ?? "") ? "white" : "#111827" }}>
                            {c.category ?? "—"}
                        </div>
                        <div style={{ fontSize: 10, color: selected === (c.category ?? "") ? "rgba(255,255,255,.7)" : "#9ca3af" }}>
                            {c.count} items · ★{c.avgRating.toFixed(1)}
                            {c.avgPrice != null ? ` · €${c.avgPrice.toFixed(0)}` : ""}
                        </div>
                    </div>
                    <button
                        onClick={(e) => { e.stopPropagation(); onDelete(c.category ?? ""); }}
                        title="Delete category"
                        style={{
                            background: "none", border: "none", cursor: "pointer",
                            color: selected === (c.category ?? "") ? "rgba(255,255,255,.7)" : "#d1d5db",
                            fontSize: 14, padding: "0 2px", lineHeight: 1,
                        }}
                    >
                        ✕
                    </button>
                </div>
            ))}
        </div>
    );
}

// ── Product card ─────────────────────────────────────────────────────────────

function ShoeCard({ shoe }: { shoe: AmazonShoeProduct }) {
    return (
        <div style={{
            background: "white",
            border: "1px solid #e5e7eb",
            borderRadius: 12,
            overflow: "hidden",
            display: "flex",
            flexDirection: "column",
            transition: "box-shadow .15s, transform .15s",
            cursor: "default",
        }}
            onMouseEnter={(e) => { const el = e.currentTarget as HTMLDivElement; el.style.boxShadow = "0 8px 24px rgba(0,0,0,.12)"; el.style.transform = "translateY(-2px)"; }}
            onMouseLeave={(e) => { const el = e.currentTarget as HTMLDivElement; el.style.boxShadow = ""; el.style.transform = ""; }}
        >
            {/* Image */}
            <a href={shoe.productUrl ?? "#"} target="_blank" rel="noopener noreferrer" tabIndex={-1}>
                <div style={{ width: "100%", height: 180, background: "#f9fafb", display: "flex", alignItems: "center", justifyContent: "center", overflow: "hidden" }}>
                    {shoe.imageUrl ? (
                        <img
                            src={shoe.imageUrl}
                            alt={shoe.name ?? ""}
                            style={{ maxWidth: "100%", maxHeight: "100%", objectFit: "contain", padding: 8 }}
                            onError={(e) => { (e.target as HTMLImageElement).style.display = "none"; }}
                        />
                    ) : (
                        <span style={{ fontSize: 48 }}>👟</span>
                    )}
                </div>
            </a>

            {/* Body */}
            <div style={{ padding: "10px 12px 12px", flex: 1, display: "flex", flexDirection: "column", gap: 4 }}>
                {shoe.brand && (
                    <div style={{ fontSize: 10, fontWeight: 700, textTransform: "uppercase", color: "#9ca3af", letterSpacing: "0.04em" }}>
                        {shoe.brand}
                    </div>
                )}
                <a
                    href={shoe.productUrl ?? "#"}
                    target="_blank"
                    rel="noopener noreferrer"
                    style={{ fontWeight: 700, fontSize: 13, color: "#111827", lineHeight: 1.35, textDecoration: "none" }}
                    title={shoe.name ?? ""}
                >
                    {shoe.name && shoe.name.length > 70 ? shoe.name.slice(0, 68) + "…" : shoe.name ?? "—"}
                </a>

                <div style={{ display: "flex", alignItems: "center", gap: 6, marginTop: 2 }}>
                    <StarRating rating={shoe.rating} />
                    <span style={{ fontSize: 11, color: "#6b7280" }}>({shoe.reviewCount.toLocaleString()})</span>
                </div>

                <div style={{ marginTop: "auto", paddingTop: 6, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <PriceLabel price={shoe.price} original={shoe.originalPrice} currency={shoe.currency} />
                    <span style={{ fontSize: 10, background: "#f3f4f6", color: "#6b7280", borderRadius: 5, padding: "1px 6px" }}>
                        {shoe.asin}
                    </span>
                </div>
            </div>
        </div>
    );
}

// ── Main page ────────────────────────────────────────────────────────────────

export default function AmazonShoesTrendsPage() {
    const [categories, setCategories]         = useState<CategorySummary[]>([]);
    const [selectedType, setSelectedType]     = useState("sneakers");
    const [result, setResult]                 = useState<PagedResult<AmazonShoeProduct> | null>(null);
    const [page, setPage]                     = useState(1);
    const [pageSize]                          = useState(20);

    const [syncType, setSyncType]             = useState("sneakers");
    const [syncMinPrice, setSyncMinPrice]     = useState("");
    const [syncMaxPrice, setSyncMaxPrice]     = useState("");
    const [syncing, setSyncing]               = useState(false);
    const [syncMsg, setSyncMsg]               = useState<{ ok: boolean; text: string } | null>(null);

    const [loadingItems, setLoadingItems]     = useState(false);

    // ── Load categories ───────────────────────────────────────────────────

    const reloadCategories = useCallback(() => {
        getAmazonShoeCategories()
            .then(setCategories)
            .catch(() => setCategories([]));
    }, []);

    useEffect(() => { reloadCategories(); }, [reloadCategories]);

    // ── Load items when type or page changes ─────────────────────────────

    useEffect(() => {
        if (!selectedType) return;
        setLoadingItems(true);
        getAmazonShoesByType(selectedType, page, pageSize)
            .then(setResult)
            .catch(() => setResult(null))
            .finally(() => setLoadingItems(false));
    }, [selectedType, page, pageSize]);

    // ── Sync ─────────────────────────────────────────────────────────────

    const handleSync = async () => {
        if (!syncType.trim()) return;
        setSyncing(true);
        setSyncMsg(null);
        try {
            const r = await syncAmazonShoes(
                syncType.trim(),
                syncMinPrice ? Number(syncMinPrice) : null,
                syncMaxPrice ? Number(syncMaxPrice) : null,
            );
            setSyncMsg({ ok: true, text: `✅ ${r.total} results — ${r.inserted} inserted, ${r.updated} updated` });
            reloadCategories();
            // Auto-select the synced type
            setSelectedType(syncType.trim());
            setPage(1);
        } catch (e) {
            setSyncMsg({ ok: false, text: `❌ ${e instanceof Error ? e.message : String(e)}` });
        } finally {
            setSyncing(false);
        }
    };

    // ── Delete category ───────────────────────────────────────────────────

    const handleDelete = async (cat: string) => {
        if (!window.confirm(`Delete all ${cat} records from DB?`)) return;
        try {
            await deleteAmazonShoeCategory(cat);
            reloadCategories();
            if (selectedType === cat) { setResult(null); setSelectedType(""); }
        } catch {/* ignore */}
    };

    const items = result?.items ?? [];

    return (
        <div style={{ maxWidth: 1380, margin: "2rem auto", padding: "0 1rem", fontFamily: "system-ui, -apple-system, sans-serif" }}>

            {/* ── Header ── */}
            <div style={{ marginBottom: 24 }}>
                <h1 style={{ fontSize: 26, fontWeight: 800, color: "#111827", margin: 0 }}>
                    🛍 Amazon Shoe Trends
                </h1>
                <p style={{ color: "#6b7280", marginTop: 4, marginBottom: 0, fontSize: 14 }}>
                    Fetch top-rated shoes from Amazon via SerpAPI and track them over time. Data is stored in the Analytics DB.
                </p>
            </div>

            {/* ── Sync card ── */}
            <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 14, padding: "18px 20px", marginBottom: 20, boxShadow: "0 1px 4px rgba(0,0,0,.06)" }}>
                <div style={{ fontSize: 13, fontWeight: 700, color: "#111827", marginBottom: 12 }}>🔄 Sync from Amazon</div>
                <div style={{ display: "flex", flexWrap: "wrap", gap: 10, alignItems: "flex-end" }}>

                    {/* Type */}
                    <div>
                        <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "#6b7280", marginBottom: 4 }}>Shoe type</label>
                        <div style={{ position: "relative" }}>
                            <input
                                value={syncType}
                                onChange={(e) => setSyncType(e.target.value)}
                                list="shoe-type-list"
                                placeholder="e.g. sneakers"
                                style={{ padding: "7px 10px", borderRadius: 8, border: "1.5px solid #e5e7eb", fontSize: 13, width: 200 }}
                            />
                            <datalist id="shoe-type-list">
                                {SHOE_TYPES.map((t) => <option key={t} value={t} />)}
                            </datalist>
                        </div>
                    </div>

                    {/* Min price */}
                    <div>
                        <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "#6b7280", marginBottom: 4 }}>Min price</label>
                        <input
                            type="number" min={0} placeholder="e.g. 20"
                            value={syncMinPrice}
                            onChange={(e) => setSyncMinPrice(e.target.value)}
                            style={{ padding: "7px 10px", borderRadius: 8, border: "1.5px solid #e5e7eb", fontSize: 13, width: 100 }}
                        />
                    </div>

                    {/* Max price */}
                    <div>
                        <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "#6b7280", marginBottom: 4 }}>Max price</label>
                        <input
                            type="number" min={0} placeholder="e.g. 150"
                            value={syncMaxPrice}
                            onChange={(e) => setSyncMaxPrice(e.target.value)}
                            style={{ padding: "7px 10px", borderRadius: 8, border: "1.5px solid #e5e7eb", fontSize: 13, width: 100 }}
                        />
                    </div>

                    <button
                        onClick={handleSync}
                        disabled={syncing || !syncType.trim()}
                        style={{
                            padding: "8px 20px", borderRadius: 8,
                            background: syncing ? "#e5e7eb" : "#4f46e5",
                            color: syncing ? "#9ca3af" : "white",
                            border: "none", fontWeight: 700, fontSize: 13,
                            cursor: syncing ? "not-allowed" : "pointer",
                            transition: "all .15s",
                        }}
                    >
                        {syncing ? "⏳ Syncing…" : "▶ Run Sync"}
                    </button>
                </div>

                {syncMsg && (
                    <div style={{
                        marginTop: 10, padding: "7px 12px", borderRadius: 8,
                        background: syncMsg.ok ? "#ecfdf5" : "#fff1f2",
                        color: syncMsg.ok ? "#065f46" : "#be123c",
                        fontSize: 13, fontWeight: 600,
                        border: `1px solid ${syncMsg.ok ? "#a7f3d0" : "#fecdd3"}`,
                    }}>
                        {syncMsg.text}
                    </div>
                )}

                <div style={{ marginTop: 8, fontSize: 11, color: "#9ca3af" }}>
                    Requires a valid SerpAPI key in <code>appsettings.json → SerpApi:ApiKey</code>.
                    Uses domain <strong>{""}</strong> configured in <code>SerpApi:AmazonDomain</code>.
                </div>
            </div>

            {/* ── Layout: sidebar + grid ── */}
            <div style={{ display: "flex", gap: 20, alignItems: "flex-start" }}>

                {/* Sidebar */}
                <div style={{ width: 220, flexShrink: 0 }}>
                    <CategoryPanel
                        categories={categories}
                        selected={selectedType}
                        onSelect={(c) => { setSelectedType(c); setPage(1); }}
                        onDelete={handleDelete}
                    />
                </div>

                {/* Main */}
                <div style={{ flex: 1, minWidth: 0 }}>

                    {/* Header row */}
                    {selectedType && (
                        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 14 }}>
                            <div>
                                <span style={{ fontWeight: 700, fontSize: 16, color: "#111827", textTransform: "capitalize" }}>{selectedType}</span>
                                {result && (
                                    <span style={{ marginLeft: 8, fontSize: 13, color: "#6b7280" }}>
                                        {result.total} items · page {result.page}/{result.pages}
                                    </span>
                                )}
                            </div>
                            {loadingItems && <span style={{ fontSize: 12, color: "#9ca3af" }}>⏳ Loading…</span>}
                        </div>
                    )}

                    {!selectedType && categories.length === 0 && (
                        <div style={{ textAlign: "center", padding: "60px 0", color: "#9ca3af" }}>
                            <div style={{ fontSize: 40 }}>🛍</div>
                            <div style={{ fontWeight: 600, marginTop: 12 }}>No data yet</div>
                            <div style={{ fontSize: 13, marginTop: 4 }}>Use the sync panel above to fetch shoes from Amazon.</div>
                        </div>
                    )}

                    {/* Cards grid */}
                    {items.length > 0 && (
                        <div style={{ display: "grid", gap: 14, gridTemplateColumns: "repeat(auto-fill, minmax(210px, 1fr))" }}>
                            {items.map((shoe) => <ShoeCard key={shoe.id} shoe={shoe} />)}
                        </div>
                    )}

                    {items.length === 0 && selectedType && !loadingItems && (
                        <div style={{ textAlign: "center", padding: 40, background: "#f9fafb", borderRadius: 14, color: "#6b7280" }}>
                            <div style={{ fontSize: 32 }}>📭</div>
                            <div style={{ marginTop: 8, fontWeight: 600 }}>No results for "{selectedType}"</div>
                            <div style={{ fontSize: 13, marginTop: 4 }}>Click "Run Sync" to fetch from Amazon.</div>
                        </div>
                    )}

                    {/* Pagination */}
                    {result && result.pages > 1 && (
                        <div style={{ display: "flex", justifyContent: "center", gap: 8, marginTop: 24 }}>
                            <button
                                onClick={() => setPage((p) => Math.max(1, p - 1))}
                                disabled={page <= 1}
                                style={{ padding: "6px 14px", borderRadius: 8, border: "1.5px solid #e5e7eb", background: page <= 1 ? "#f9fafb" : "white", cursor: page <= 1 ? "not-allowed" : "pointer", fontWeight: 600 }}
                            >
                                ← Prev
                            </button>
                            {Array.from({ length: Math.min(result.pages, 7) }, (_, i) => {
                                const p = i + 1;
                                return (
                                    <button
                                        key={p}
                                        onClick={() => setPage(p)}
                                        style={{
                                            padding: "6px 12px", borderRadius: 8,
                                            border: `1.5px solid ${page === p ? "#4f46e5" : "#e5e7eb"}`,
                                            background: page === p ? "#4f46e5" : "white",
                                            color: page === p ? "white" : "#374151",
                                            fontWeight: 700, cursor: "pointer",
                                        }}
                                    >
                                        {p}
                                    </button>
                                );
                            })}
                            <button
                                onClick={() => setPage((p) => Math.min(result.pages, p + 1))}
                                disabled={page >= result.pages}
                                style={{ padding: "6px 14px", borderRadius: 8, border: "1.5px solid #e5e7eb", background: page >= result.pages ? "#f9fafb" : "white", cursor: page >= result.pages ? "not-allowed" : "pointer", fontWeight: 600 }}
                            >
                                Next →
                            </button>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
