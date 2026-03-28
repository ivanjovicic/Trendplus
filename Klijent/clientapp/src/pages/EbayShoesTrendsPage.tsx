import { useState, useEffect, useCallback } from "react";
import {
    syncEbayShoes,
    getEbayShoesByType,
    getEbayShoeCategories,
    deleteEbayShoeCategory,
    type EbayShoeProduct,
    type EbayCategorySummary,
} from "../services/ebayShoesApi";

// ── Constants ─────────────────────────────────────────────────────────────────

const SHOE_TYPES = [
    // Sportske / Athletic
    "sneakers", "running shoes", "training shoes", "basketball shoes", "tennis shoes",
    "hiking shoes", "trail running shoes", "football boots", "cycling shoes", "walking shoes",
    "gym shoes", "volleyball shoes", "dance shoes",
    // Casual
    "loafers", "moccasins", "flats", "espadrilles", "slippers", "boat shoes",
    "canvas shoes", "slip-on sneakers", "high top sneakers", "driving shoes", "deck shoes",
    // Trendy / Fashion
    "chunky sneakers", "platform sneakers", "dad shoes", "luxury sneakers", "retro sneakers",
    // Elegantne / Formal
    "oxfords", "derbies", "heels", "pumps", "stilettos", "wedges", "platforms",
    "kitten heels", "block heels", "slingbacks", "peep toe heels", "court shoes",
    // Čizme i gležnjače / Boots
    "boots", "ankle boots", "chelsea boots", "knee high boots", "combat boots", "rain boots", "cowboy boots",
    "riding boots", "winter boots", "hiking boots", "work boots", "snow boots", "thigh high boots",
    // Sandale i ljeto / Sandals
    "sandals", "mules", "flip flops", "slides",
    "gladiator sandals", "platform sandals", "wedge sandals", "sport sandals",
    // Specifični tipovi / Other
    "ballet flats", "mary janes", "brogues", "monk straps", "clogs",
    "birkenstock", "crocs", "ugg boots", "espadrille wedges",
];

const GENDER_OPTIONS = [
    { value: "all",    label: "Sve" },
    { value: "women",  label: "Žene" },
    { value: "men",    label: "Muškarci" },
    { value: "unisex", label: "Unisex" },
];

const SORT_OPTIONS = [
    { value: "score",      label: "🔥 Trend Score" },
    { value: "rating",     label: "⭐ Rating" },
    { value: "popular",   label: "💬 Reviews" },
    { value: "price_asc",  label: "💰 Cijena ↑" },
    { value: "price_desc", label: "💰 Cijena ↓" },
    { value: "newest",     label: "🕐 Najnovije" },
];

const POPULAR_BRANDS: { group: string; brands: string[] }[] = [
    {
        group: "Sport & Street",
        brands: ["Nike", "Adidas", "Puma", "Reebok", "New Balance", "Vans", "Converse", "Skechers", "Fila", "Asics", "Under Armour", "Lacoste", "Hummel"],
    },
    {
        group: "Fashion / Trendy",
        brands: ["Tommy Hilfiger", "Steve Madden", "Calvin Klein", "Guess", "Buffalo", "Karl Lagerfeld", "Esprit", "Desigual", "Vagabond", "Ted Baker"],
    },
    {
        group: "European / Classic",
        brands: ["Geox", "Clarks", "Ecco", "Birkenstock", "Superga", "Tamaris", "Gabor", "Ara", "Rieker", "Caprice", "Marco Tozzi", "Mustang", "s.Oliver", "Jana", "Gioseppo"],
    },
    {
        group: "Premium (\u2264 200\u00a0\u20ac)",
        brands: ["Timberland", "UGG", "Hunter", "Boss", "Michael Kors", "Liu Jo", "Kurt Geiger", "Mango", "Zara"],
    },
];

const EBAY_BRAND_COLOR = "var(--c-e53238, #e53238)"; // kept as reference but UI uses theme classes

// ── Helpers ───────────────────────────────────────────────────────────────────

function ConditionBadge({ condition }: { condition: string | null }) {
    if (!condition) return null;
    const upper = condition.toUpperCase();
    if (upper.includes("NEW")) return <span className="text-[9px] font-bold uppercase text-success bg-success/10 px-1.5 py-0.5 rounded border border-success/20">{condition}</span>;
    if (upper.includes("REFURB")) return <span className="text-[9px] font-bold uppercase text-info bg-info/10 px-1.5 py-0.5 rounded border border-info/20">{condition}</span>;
    return <span className="text-[9px] font-bold uppercase text-muted bg-surface/50 px-1.5 py-0.5 rounded border border-border">{condition}</span>;
}

function StarRating({ rating }: { rating: number }) {
    if (rating <= 0) return <span className="text-xs text-muted">no ratings</span>;
    const full  = Math.floor(rating);
    const half  = rating - full >= 0.4;
    const empty = 5 - full - (half ? 1 : 0);
    return (
        <span title={`${rating.toFixed(1)} / 5 (seller feedback)`} className="text-warning text-base tracking-tight">
            {"★".repeat(full)}{"½".repeat(half ? 1 : 0)}{"☆".repeat(empty)}
        </span>
    );
}

function PriceLabel({ price, currency }: { price: number | null; currency: string | null }) {
    if (price == null) return <span className="text-muted">Price N/A</span>;
    const sym = currency === "EUR" ? "€" : currency === "USD" ? "$" : currency ?? "";
    return (
        <span className="font-extrabold text-base text-error">
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
        <div className="min-w-[210px]">
            <div className="text-xs font-bold text-muted uppercase mb-2 tracking-wide">Categories in DB</div>
            {categories.length === 0 && (
                <div className="text-muted text-sm italic">No data — sync first</div>
            )}
            {categories.map((c) => {
                const key = c.category ?? "";
                const active = selected === key;
                return (
                    <div
                        key={key}
                        className={`flex items-center justify-between p-2 rounded-md mb-1 transition-all cursor-pointer ${active ? 'bg-accent text-white border-accent' : 'bg-surface border-border'}`}
                        onClick={() => onSelect(key)}
                    >
                        <div>
                            <div className={`font-semibold text-sm ${active ? 'text-white' : 'text-foreground'}`}>{c.category ?? '—'}</div>
                            <div className={`text-xs ${active ? 'text-white/80' : 'text-muted'}`}>{c.count} items{c.avgPrice != null ? ` · €${c.avgPrice.toFixed(0)}` : ''}</div>
                        </div>
                        <button
                            onClick={(e) => { e.stopPropagation(); onDelete(key); }}
                            title="Delete category"
                            className={`text-sm ${active ? 'text-white/80' : 'text-muted'}`}
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
        <div className="card overflow-hidden flex flex-col transition-transform hover:-translate-y-1 hover:shadow-lg">
            {/* eBay colour strip */}
            <div className="h-1 bg-gradient-to-r from-red-600 via-yellow-400 to-green-500" />

            {/* Image */}
            <a href={shoe.productUrl ?? "#"} target="_blank" rel="noopener noreferrer" tabIndex={-1}>
                <div className="w-full h-44 bg-surface flex items-center justify-center overflow-hidden">
                    {shoe.imageUrl ? (
                        <img
                            src={shoe.imageUrl}
                            alt={shoe.name ?? ""}
                            className="max-w-full max-h-full object-contain p-2"
                            onError={(e) => { (e.target as HTMLImageElement).style.display = "none"; }}
                        />
                    ) : (
                        <span className="text-3xl">👟</span>
                    )}
                </div>
            </a>

            {/* Body */}
            <div className="p-3 flex-1 flex flex-col gap-2">
                <div className="flex items-center gap-2">
                    {shoe.brand && (
                        <span className="text-xs font-bold uppercase text-muted tracking-wider">{shoe.brand}</span>
                    )}
                    <ConditionBadge condition={shoe.condition} />
                </div>

                <a
                    href={shoe.productUrl ?? "#"}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="font-semibold text-sm text-foreground leading-tight no-underline"
                    title={shoe.name ?? ""}
                >
                    {shoe.name && shoe.name.length > 70 ? shoe.name.slice(0, 68) + "…" : shoe.name ?? "—"}
                </a>

                <div className="flex items-center gap-2 mt-1">
                    <StarRating rating={shoe.rating} />
                    {shoe.reviewCount > 0 && (
                        <span className="text-xs text-muted">({shoe.reviewCount.toLocaleString()} feedback)</span>
                    )}
                </div>

                <div className="mt-auto pt-2 flex justify-between items-center">
                    <PriceLabel price={shoe.price} currency={shoe.currency} />
                    <div className="flex gap-2 items-center">
                        {shoe.trendScore > 0 && (
                            <span className="text-xs font-bold bg-warning/10 text-warning rounded px-2 py-0.5 border border-warning/20">◆ {shoe.trendScore.toFixed(1)}</span>
                        )}
                        <span className="text-xs font-semibold bg-surface-elevated text-muted rounded px-2 py-0.5">eBay</span>
                    </div>
                </div>
            </div>
        </div>
    );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function EbayShoesTrendsPage() {
    const [categories, setCategories]     = useState<EbayCategorySummary[]>([]);
    const [selectedType, setSelectedType] = useState("sneakers");
    const [items, setItems]               = useState<EbayShoeProduct[]>([]);
    const [total, setTotal]               = useState(0);
    const [hasMore, setHasMore]           = useState(false);
    const [nextPage, setNextPage]         = useState(2);
    const [syncCounter, setSyncCounter]   = useState(0);
    const [pageSize]                      = useState(20);
    const [loadingItems, setLoadingItems] = useState(false);
    const [loadingMore, setLoadingMore]   = useState(false);

    const [syncType, setSyncType]         = useState("sneakers");
    const [syncGender, setSyncGender]     = useState("women");
    const [syncMinPrice, setSyncMinPrice] = useState("");
    const [syncMaxPrice, setSyncMaxPrice] = useState("");
    const [syncing, setSyncing]           = useState(false);
    const [syncMsg, setSyncMsg]           = useState<{ ok: boolean; text: string } | null>(null);

    const [browseGender, setBrowseGender] = useState("all");
    const [sortBy, setSortBy]             = useState("score");
    const [brandFilter, setBrandFilter]   = useState("all");

    const reloadCategories = useCallback(() => {
        getEbayShoeCategories()
            .then(setCategories)
            .catch(() => setCategories([]));
    }, []);

    useEffect(() => { reloadCategories(); }, [reloadCategories]);

    useEffect(() => {
        if (!selectedType) return;
        setLoadingItems(true);
        setItems([]);
        setHasMore(false);
        setNextPage(2);
        getEbayShoesByType(selectedType, browseGender === "all" ? null : browseGender, sortBy, 1, pageSize)
            .then(r => { setItems(r.items); setTotal(r.total); setHasMore(1 < r.pages); })
            .catch(() => { setItems([]); setTotal(0); setHasMore(false); })
            .finally(() => setLoadingItems(false));
    }, [selectedType, browseGender, sortBy, pageSize, syncCounter]);

    const handleLoadMore = () => {
        if (loadingMore || !hasMore) return;
        setLoadingMore(true);
        getEbayShoesByType(selectedType, browseGender === "all" ? null : browseGender, sortBy, nextPage, pageSize)
            .then(r => {
                setItems(prev => [...prev, ...r.items]);
                setHasMore(nextPage < r.pages);
                setNextPage(p => p + 1);
            })
            .catch(() => {})
            .finally(() => setLoadingMore(false));
    };

    const handleSync = async () => {
        if (!syncType.trim()) return;
        setSyncing(true);
        setSyncMsg(null);
        try {
            const r = await syncEbayShoes(
                syncType.trim(),
                syncGender === "all" ? null : syncGender,
                syncMinPrice ? Number(syncMinPrice) : null,
                syncMaxPrice ? Number(syncMaxPrice) : null,
            );
            setSyncMsg({ ok: true, text: `✅ ${r.total} results — ${r.inserted} inserted, ${r.updated} updated` });
            reloadCategories();
            setSelectedType(syncType.trim());
            setBrowseGender("all");
            setSyncCounter(c => c + 1);
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
            if (selectedType === cat) { setItems([]); setTotal(0); setHasMore(false); setSelectedType(""); }
        } catch {/* ignore */ }
    };

    return (
        <div style={{ maxWidth: 1380, margin: "2rem auto", padding: "0 1rem", fontFamily: "system-ui, -apple-system, sans-serif" }}>

            {/* ── Header ── */}
            <div style={{ marginBottom: 24 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                    <span style={{ fontSize: 30 }}>🛒</span>
                    <div>
                        <h1 style={{ fontSize: 26, fontWeight: 800, color: "var(--c-111827, #111827)", margin: 0 }}>
                            eBay Shoe Trends
                        </h1>
                        <p style={{ color: "var(--c-6b7280, #6b7280)", marginTop: 2, marginBottom: 0, fontSize: 14 }}>
                            Fetch top shoe listings from eBay via Browse API and track them over time.
                            Requires an eBay OAuth App token in <code>appsettings.json → Ebay:OAuthToken</code>.
                        </p>
                    </div>
                </div>
            </div>

            {/* ── Sync card ── */}
            <div style={{ background: "var(--surface-card, #ffffff)", border: "1px solid var(--border-muted, #e5e7eb)", borderRadius: 14, padding: "18px 20px", marginBottom: 20, boxShadow: "var(--box-shadow-xs, 0 1px 4px rgba(0,0,0,.06))" }}>
                {/* eBay colour bar */}
                <div style={{ height: 4, borderRadius: 4, background: `linear-gradient(90deg, var(--c-e53238, #e53238), var(--c-f5af02, #f5af02) 40%, var(--c-86b817, #86b817) 70%, var(--c-05adee, #05adee))`, marginBottom: 14 }} />

                <div style={{ fontSize: 13, fontWeight: 700, color: "var(--text-primary, #111827)", marginBottom: 12 }}>🔄 Sync from eBay</div>
                <div style={{ display: "flex", flexWrap: "wrap", gap: 10, alignItems: "flex-end" }}>

                    <div>
                        <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "var(--c-6b7280, #6b7280)", marginBottom: 4 }}>Tip cipele</label>
                        <div>
                            <input
                                value={syncType}
                                onChange={(e) => setSyncType(e.target.value)}
                                list="ebay-shoe-type-list"
                                placeholder="e.g. sneakers"
                                style={{ padding: "7px 10px", borderRadius: 8, border: "1.5px solid var(--border-muted, #e5e7eb)", fontSize: 13, width: 200 }}
                            />
                            <datalist id="ebay-shoe-type-list">
                                {SHOE_TYPES.map((t) => <option key={t} value={t} />)}
                            </datalist>
                        </div>
                    </div>

                    {/* Gender */}
                    <div>
                        <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "var(--c-6b7280, #6b7280)", marginBottom: 4 }}>Pol</label>
                        <select
                            value={syncGender}
                            onChange={(e) => setSyncGender(e.target.value)}
                            style={{ padding: "7px 10px", borderRadius: 8, border: "1.5px solid var(--border-muted, #e5e7eb)", fontSize: 13, minWidth: 120, background: "var(--surface-card, #ffffff)" }}
                        >
                            {GENDER_OPTIONS.map((g) => (
                                <option key={g.value} value={g.value}>{g.label}</option>
                            ))}
                        </select>
                    </div>

                    <div>
                        <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "var(--c-6b7280, #6b7280)", marginBottom: 4 }}>Min price (€)</label>
                        <input
                            type="number" min={0} placeholder="e.g. 20"
                            value={syncMinPrice}
                            onChange={(e) => setSyncMinPrice(e.target.value)}
                            style={{ padding: "7px 10px", borderRadius: 8, border: "1.5px solid var(--border-muted, #e5e7eb)", fontSize: 13, width: 100 }}
                        />
                    </div>

                    <div>
                        <label style={{ display: "block", fontSize: 11, fontWeight: 600, color: "var(--c-6b7280, #6b7280)", marginBottom: 4 }}>Max price (€)</label>
                        <input
                            type="number" min={0} placeholder="e.g. 150"
                            value={syncMaxPrice}
                            onChange={(e) => setSyncMaxPrice(e.target.value)}
                            style={{ padding: "7px 10px", borderRadius: 8, border: "1.5px solid var(--c-e5e7eb, #e5e7eb)", fontSize: 13, width: 100 }}
                        />
                    </div>

                    <button
                        onClick={handleSync}
                        disabled={syncing || !syncType.trim()}
                        style={{
                            padding: "8px 20px", borderRadius: 8,
                            background: syncing ? "var(--border-muted, #e5e7eb)" : "var(--brand-ebay, #e53238)",
                            color: syncing ? "var(--text-muted, #9ca3af)" : "var(--text-on-primary, #ffffff)",
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
                        background: syncMsg.ok ? "var(--success-bg, #ecfdf5)" : "var(--error-bg, #fff1f2)",
                        color: syncMsg.ok ? "var(--success-text, #065f46)" : "var(--error-text, #be123c)",
                        fontSize: 13, fontWeight: 600,
                        border: `1px solid ${syncMsg.ok ? "var(--success-border, #a7f3d0)" : "var(--error-border, #fecdd3)"}`,
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
                        onSelect={(c) => { setSelectedType(c); setBrowseGender("all"); setSortBy("score"); setBrandFilter("all"); }}
                        onDelete={handleDelete}
                    />
                </div>

                {/* Main */}
                <div style={{ flex: 1, minWidth: 0 }}>

                    {selectedType && (
                        <div style={{ marginBottom: 14 }}>
                            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 10 }}>
                                <div>
                                    <span style={{ fontWeight: 700, fontSize: 16, color: "var(--text-primary, #111827)", textTransform: "capitalize" }}>{selectedType}</span>
                                    {total > 0 && (
                                        <span style={{ marginLeft: 8, fontSize: 13, color: "var(--text-muted, #6b7280)" }}>
                                            {items.length}/{total} items
                                        </span>
                                    )}
                                </div>
                                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                                    <select
                                        value={brandFilter}
                                        onChange={(e) => setBrandFilter(e.target.value)}
                                        style={{ padding: "4px 8px", borderRadius: 7, border: `1.5px solid ${brandFilter !== "all" ? "var(--accent, #6366f1)" : "var(--border-muted, #e5e7eb)"}`, fontSize: 12, fontWeight: brandFilter !== "all" ? 700 : 400, color: brandFilter !== "all" ? "var(--accent, #4f46e5)" : "var(--text-primary, #374151)", background: brandFilter !== "all" ? "var(--surface-variant, #eef2ff)" : "var(--surface-card, #ffffff)", cursor: "pointer" }}
                                    >
                                        <option value="all">🏷 Svi brendovi</option>
                                        {POPULAR_BRANDS.map(({ group, brands }) => (
                                            <optgroup key={group} label={group}>
                                                {brands.map(b => <option key={b} value={b}>{b}</option>)}
                                            </optgroup>
                                        ))}
                                    </select>
                                    <select
                                        value={sortBy}
                                        onChange={(e) => setSortBy(e.target.value)}
                                        style={{ padding: "4px 8px", borderRadius: 7, border: "1.5px solid var(--c-e5e7eb, #e5e7eb)", fontSize: 12, fontWeight: 600, background: "var(--surface-default, #ffffff)", cursor: "pointer" }}
                                    >
                                        {SORT_OPTIONS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
                                    </select>
                                    {loadingItems && <span style={{ fontSize: 12, color: "var(--c-9ca3af, #9ca3af)" }}>⏳ Loading…</span>}
                                </div>
                            </div>
                            {/* Gender filter tabs */}
                            <div style={{ display: "flex", gap: 6 }}>
                                {GENDER_OPTIONS.map((g) => (
                                    <button
                                        key={g.value}
                                        onClick={() => setBrowseGender(g.value)}
                                        style={{
                                            padding: "4px 14px", borderRadius: 20, fontSize: 12, fontWeight: 600,
                                            border: `1.5px solid ${browseGender === g.value ? EBAY_BRAND_COLOR : "var(--border-muted, #e5e7eb)"}`,
                                            background: browseGender === g.value ? EBAY_BRAND_COLOR : "var(--surface-card, #ffffff)",
                                            color: browseGender === g.value ? "var(--text-on-primary, #ffffff)" : "var(--text-primary, #374151)",
                                            cursor: "pointer", transition: "all .12s",
                                        }}
                                    >
                                        {g.label}
                                    </button>
                                ))}
                            </div>
                        </div>
                    )}

                    {!selectedType && categories.length === 0 && (
                        <div style={{ textAlign: "center", padding: "60px 0", color: "var(--c-9ca3af, #9ca3af)" }}>
                            <div style={{ fontSize: 40 }}>🛒</div>
                            <div style={{ fontWeight: 600, marginTop: 12 }}>No data yet</div>
                            <div style={{ fontSize: 13, marginTop: 4 }}>Use the sync panel above to fetch shoes from eBay.</div>
                        </div>
                    )}

                    {/* Cards grid */}
                    {(() => {
                        const displayedItems = brandFilter === "all"
                            ? items
                            : items.filter(s => (s.brand ?? "").toLowerCase() === brandFilter.toLowerCase());
                        return (
                            <>
                                {displayedItems.length > 0 && (
                                    <div style={{ display: "grid", gap: 14, gridTemplateColumns: "repeat(auto-fill, minmax(210px, 1fr))" }}>
                                        {displayedItems.map((shoe) => <EbayShoeCard key={shoe.id} shoe={shoe} />)}
                                    </div>
                                )}
                                {displayedItems.length === 0 && selectedType && !loadingItems && (
                                    <div style={{ textAlign: "center", padding: 40, background: "var(--c-f9fafb, #f9fafb)", borderRadius: 14, color: "var(--c-6b7280, #6b7280)" }}>
                                        <div style={{ fontSize: 32 }}>💭</div>
                                        {brandFilter !== "all"
                                            ? <><div style={{ marginTop: 8, fontWeight: 600 }}>Nema rezultata za brend "{brandFilter}"</div><div style={{ fontSize: 13, marginTop: 4 }}>Probaj drugi brend ili učitaj više stranica.</div></>
                                            : <><div style={{ marginTop: 8, fontWeight: 600 }}>No results for "{selectedType}"</div><div style={{ fontSize: 13, marginTop: 4 }}>Click "Run Sync" to fetch from eBay.</div></>
                                        }
                                    </div>
                                )}
                            </>
                        );
                    })()}

                    {/* Load More */}
                    {hasMore && (
                        <div style={{ display: "flex", justifyContent: "center", marginTop: 24 }}>
                            <button
                                onClick={handleLoadMore}
                                disabled={loadingMore}
                                style={{
                                    padding: "10px 32px", borderRadius: 10,
                                    background: loadingMore ? "var(--c-e5e7eb, #e5e7eb)" : EBAY_BRAND_COLOR,
                                    color: loadingMore ? "var(--c-9ca3af, #9ca3af)" : "var(--text-on-primary, #ffffff)",
                                    border: "none", fontWeight: 700, fontSize: 14,
                                    cursor: loadingMore ? "not-allowed" : "pointer",
                                    transition: "all .15s",
                                }}
                            >
                                {loadingMore ? "⏳ Učitavam…" : `▼ Još rezultata (${total - items.length} preostalo)`}
                            </button>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
