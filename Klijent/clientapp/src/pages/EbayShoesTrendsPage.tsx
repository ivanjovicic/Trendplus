import { useState, useEffect, useCallback } from "react";
import {
    syncEbayShoes,
    getEbayShoesByType,
    getEbayShoeCategories,
    deleteEbayShoeCategory,
    type EbayShoeProduct,
    type EbayCategorySummary,
    type EbayPagedResult,
} from "../services/ebayShoesApi";

// ── Constants ─────────────────────────────────────────────────────────────────

const SHOE_TYPES = [
    "sneakers", "boots", "ankle boots", "sandals", "heels",
    "loafers", "oxfords", "chelsea boots", "running shoes", "mules",
    "stilettos", "wedges", "espadrilles", "flats", "slippers",
];

const EBAY_BRAND_COLOR = "#e53238"; // eBay red

// ── Helpers ───────────────────────────────────────────────────────────────────

function ConditionBadge({ condition }: { condition: string | null }) {
    if (!condition) return null;
    const upper = condition.toUpperCase();
    const bg    = upper.includes("NEW") ? "#dcfce7" : upper.includes("REFURB") ? "#eff6ff" : "#f9fafb";
    const color = upper.includes("NEW") ? "#166534" : upper.includes("REFURB") ? "#1e40af" : "#6b7280";
    return (
        <span style={{ fontSize: 9, fontWeight: 700, textTransform: "uppercase", background: bg, color, borderRadius: 4, padding: "1px 5px", border: `1px solid ${color}22` }}>
            {condition}
        </span>
    );
}

function StarRating({ rating }: { rating: number }) {
    if (rating <= 0) return <span style={{ fontSize: 11, color: "#d1d5db" }}>no ratings</span>;
    const full  = Math.floor(rating);
    const half  = rating - full >= 0.4;
    const empty = 5 - full - (half ? 1 : 0);
    return (
        <span title={`${rating.toFixed(1)} / 5 (seller feedback)`} style={{ color: "#f59e0b", fontSize: 13, letterSpacing: -1 }}>
            {"★".repeat(full)}{"½".repeat(half ? 1 : 0)}{"☆".repeat(empty)}
        </span>
    );
}

function PriceLabel({ price, currency }: { price: number | null; currency: string | null }) {
    if (price == null) return <span style={{ color: "#d1d5db" }}>Price N/A</span>;
    const sym = currency === "EUR" ? "€" : currency === "USD" ? "$" : currency ?? "";
    return (
        <span style={{ fontWeight: 800, fontSize: 15, color: "#e53238" }}>
            {sym}{price.toFixed(2)}
        </span>
    );
}

// ── Category sidebar ──────────────────────────────────────────────────────────

function CategoryPanel({
    categories,
    selected,
    onSelect,
    onDelete,
}: {
    categories: EbayCategorySummary[];
    selected:   string;
    onSelect:   (c: string) => void;
    onDelete:   (c: string) => void;
}) {
    return (
        <div style={{ minWidth: 210 }}>
            <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", color: "#6b7280", marginBottom: 8, letterSpacing: "0.06em" }}>
                Categories in DB
            </div>
            {categories.length === 0 && (
                <div style={{ color: "#9ca3af", fontSize: 12, fontStyle: "italic" }}>No data — sync first</div>
            )}
            {categories.map((c) => {
                const key = c.category ?? "";
                const active = selected === key;
                return (
                    <div
                        key={key}
                        style={{
                            display: "flex", alignItems: "center", justifyContent: "space-between",
                            padding: "6px 10px", borderRadius: 8, marginBottom: 4,
                            background: active ? EBAY_BRAND_COLOR : "#f9fafb",
                            border: `1px solid ${active ? EBAY_BRAND_COLOR : "#e5e7eb"}`,
                            cursor: "pointer", transition: "all .12s",
                        }}
                        onClick={() => onSelect(key)}
                    >
                        <div>
                            <div style={{ fontWeight: 600, fontSize: 13, color: active ? "white" : "#111827" }}>
                                {c.category ?? "—"}
                            </div>
                            <div style={{ fontSize: 10, color: active ? "rgba(255,255,255,.7)" : "#9ca3af" }}>
                                {c.count} items
                                {c.avgPrice != null ? ` · €${c.avgPrice.toFixed(0)}` : ""}
                            </div>
                        </div>
                        <button
                            onClick={(e) => { e.stopPropagation(); onDelete(key); }}
                            title="Delete category"
                            style={{ background: "none", border: "none", cursor: "pointer", color: active ? "rgba(255,255,255,.7)" : "#d1d5db", fontSize: 14, lineHeight: 1, padding: "0 2px" }}
                        >
                            ✕
                        </button>
                    </div>
                );
            })}
        </div>
    );
}

// ── Product card ──────────────────────────────────────────────────────────────

function EbayShoeCard({ shoe }: { shoe: EbayShoeProduct }) {
    return (
        <div
            style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 12, overflow: "hidden", display: "flex", flexDirection: "column", transition: "box-shadow .15s, transform .15s" }}
            onMouseEnter={(e) => { const el = e.currentTarget as HTMLDivElement; el.style.boxShadow = "0 8px 24px rgba(0,0,0,.12)"; el.style.transform = "translateY(-2px)"; }}
            onMouseLeave={(e) => { const el = e.currentTarget as HTMLDivElement; el.style.boxShadow = ""; el.style.transform = ""; }}
        >
            {/* eBay colour strip */}
            <div style={{ height: 3, background: `linear-gradient(90deg, #e53238, #f5af02, #86b817, #05adee)` }} />

            {/* Image */}
            <a href={shoe.productUrl ?? "#"} target="_blank" rel="noopener noreferrer" tabIndex={-1}>
                <div style={{ width: "100%", height: 175, background: "#f9fafb", display: "flex", alignItems: "center", justifyContent: "center", overflow: "hidden" }}>
                    {shoe.imageUrl ? (
                        <img
                            src={shoe.imageUrl}
                            alt={shoe.name ?? ""}
                            style={{ maxWidth: "100%", maxHeight: "100%", objectFit: "contain", padding: 8 }}
                            onError={(e) => { (e.target as HTMLImageElement).style.display = "none"; }}
                        />
                    ) : (
                        <span style={{ fontSize: 44 }}>👟</span>
                    )}
                </div>
            </a>

            {/* Body */}
            <div style={{ padding: "10px 12px 12px", flex: 1, display: "flex", flexDirection: "column", gap: 4 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    {shoe.brand && (
                        <span style={{ fontSize: 10, fontWeight: 700, textTransform: "uppercase", color: "#9ca3af", letterSpacing: "0.04em" }}>
                            {shoe.brand}
                        </span>
                    )}
                    <ConditionBadge condition={shoe.condition} />
                </div>

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
                    {shoe.reviewCount > 0 && (
                        <span style={{ fontSize: 10, color: "#6b7280" }}>({shoe.reviewCount.toLocaleString()} feedback)</span>
                    )}
                </div>

                <div style={{ marginTop: "auto", paddingTop: 6, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <PriceLabel price={shoe.price} currency={shoe.currency} />
                    <span style={{ fontSize: 10, background: "#fff7ed", color: "#ea580c", borderRadius: 5, padding: "1px 6px", border: "1px solid #fed7aa", fontWeight: 600 }}>
                        eBay
                    </span>
                </div>
            </div>
        </div>
    );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function EbayShoesTrendsPage() {
    const [categories, setCategories]     = useState<EbayCategorySummary[]>([]);
    const [selectedType, setSelectedType] = useState("sneakers");
    const [result, setResult]             = useState<EbayPagedResult<EbayShoeProduct> | null>(null);
    const [page, setPage]                 = useState(1);
    const [pageSize]                      = useState(20);
    const [loadingItems, setLoadingItems] = useState(false);

    const [syncType, setSyncType]         = useState("sneakers");
    const [syncMinPrice, setSyncMinPrice] = useState("");
    const [syncMaxPrice, setSyncMaxPrice] = useState("");
    const [syncing, setSyncing]           = useState(false);
    const [syncMsg, setSyncMsg]           = useState<{ ok: boolean; text: string } | null>(null);

    const reloadCategories = useCallback(() => {
        getEbayShoeCategories()
            .then(setCategories)
            .catch(() => setCategories([]));
    }, []);

    useEffect(() => { reloadCategories(); }, [reloadCategories]);

    useEffect(() => {
        if (!selectedType) return;
        setLoadingItems(true);
        getEbayShoesByType(selectedType, page, pageSize)
            .then(setResult)
            .catch(() => setResult(null))
            .finally(() => setLoadingItems(false));
    }, [selectedType, page, pageSize]);

    const handleSync = async () => {
        if (!syncType.trim()) return;
        setSyncing(true);
        setSyncMsg(null);
        try {
            const r = await syncEbayShoes(
                syncType.trim(),
                syncMinPrice ? Number(syncMinPrice) : null,
                syncMaxPrice ? Number(syncMaxPrice) : null,
            );
            setSyncMsg({ ok: true, text: `✅ ${r.total} results — ${r.inserted} inserted, ${r.updated} updated` });
            reloadCategories();
            setSelectedType(syncType.trim());
            setPage(1);
        } catch (e) {
            setSyncMsg({ ok: false, text: `❌ ${e instanceof Error ? e.message : String(e)}` });
        } finally {
            setSyncing(false);
        }
    };

    const handleDelete = async (cat: string) => {
        if (!window.confirm(`Delete all "${cat}" eBay records from DB?`)) return;
        try {
            await deleteEbayShoeCategory(cat);
            reloadCategories();
            if (selectedType === cat) { setResult(null); setSelectedType(""); }
        } catch {/* ignore */ }
    };

    const items = result?.items ?? [];

    return (
        <div style={{ maxWidth: 1380, margin: "2rem auto", padding: "0 1rem", fontFamily: "system-ui, -apple-system, sans-serif" }}>

            {/* ── Header ── */}
            <div style={{ marginBottom: 24 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                    <span style={{ fontSize: 30 }}>🛒</span>
                    <div>
                        <h1 style={{ fontSize: 26, fontWeight: 800, color: "#111827", margin: 0 }}>
                            eBay Shoe Trends
                        </h1>
                        <p style={{ color: "#6b7280", marginTop: 2, marginBottom: 0, fontSize: 14 }}>
                            Fetch top shoe listings from eBay via Browse API and track them over time.
                            Requires an eBay OAuth App token in <code>appsettings.json → Ebay:OAuthToken</code>.
                        </p>
                    </div>
                </div>
            </div>

            {/* ── Sync card ── */}
            <div style={{ background: "white", border: "1px solid #e5e7eb", borderRadius: 14, padding: "18px 20px", marginBottom: 20, boxShadow: "0 1px 4px rgba(0,0,0,.06)" }}>
                {/* eBay colour bar */}
                <div style={{ height: 4, borderRadius: 4, background: `linear-gradient(90deg, #e53238, #f5af02 40%, #86b817 70%, #05adee)`, marginBottom: 14 }} />

                <div style={{ fontSize: 13, fontWeight: 700, color: "#111827", marginBottom: 12 }}>🔄 Sync from eBay</div>
                <div style={{ display: "flex", flexWrap: "wrap", gap: 10, alignItems: "flex-end" }}>

                    <div>
                        <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "#6b7280", marginBottom: 4 }}>Shoe type</label>
                        <div>
                            <input
                                value={syncType}
                                onChange={(e) => setSyncType(e.target.value)}
                                list="ebay-shoe-type-list"
                                placeholder="e.g. sneakers"
                                style={{ padding: "7px 10px", borderRadius: 8, border: "1.5px solid #e5e7eb", fontSize: 13, width: 200 }}
                            />
                            <datalist id="ebay-shoe-type-list">
                                {SHOE_TYPES.map((t) => <option key={t} value={t} />)}
                            </datalist>
                        </div>
                    </div>

                    <div>
                        <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "#6b7280", marginBottom: 4 }}>Min price (€)</label>
                        <input
                            type="number" min={0} placeholder="e.g. 20"
                            value={syncMinPrice}
                            onChange={(e) => setSyncMinPrice(e.target.value)}
                            style={{ padding: "7px 10px", borderRadius: 8, border: "1.5px solid #e5e7eb", fontSize: 13, width: 100 }}
                        />
                    </div>

                    <div>
                        <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "#6b7280", marginBottom: 4 }}>Max price (€)</label>
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
                            background: syncing ? "#e5e7eb" : EBAY_BRAND_COLOR,
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
                            <div style={{ fontSize: 40 }}>🛒</div>
                            <div style={{ fontWeight: 600, marginTop: 12 }}>No data yet</div>
                            <div style={{ fontSize: 13, marginTop: 4 }}>Use the sync panel above to fetch shoes from eBay.</div>
                        </div>
                    )}

                    {/* Cards grid */}
                    {items.length > 0 && (
                        <div style={{ display: "grid", gap: 14, gridTemplateColumns: "repeat(auto-fill, minmax(210px, 1fr))" }}>
                            {items.map((shoe) => <EbayShoeCard key={shoe.id} shoe={shoe} />)}
                        </div>
                    )}

                    {items.length === 0 && selectedType && !loadingItems && (
                        <div style={{ textAlign: "center", padding: 40, background: "#f9fafb", borderRadius: 14, color: "#6b7280" }}>
                            <div style={{ fontSize: 32 }}>📭</div>
                            <div style={{ marginTop: 8, fontWeight: 600 }}>No results for "{selectedType}"</div>
                            <div style={{ fontSize: 13, marginTop: 4 }}>Click "Run Sync" to fetch from eBay.</div>
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
                                            border: `1.5px solid ${page === p ? EBAY_BRAND_COLOR : "#e5e7eb"}`,
                                            background: page === p ? EBAY_BRAND_COLOR : "white",
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
