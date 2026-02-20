import React, { useMemo, useState } from "react";
import Modal from "../components/Modal";
import SearchableSelect from "../components/SearchableSelect";
import { useToast } from "../components/Toast";
import { runAboutYouScraper } from "../services/aboutYouApi";

type AboutYouItem = {
    brand?: string | null;
    name: string;
    price?: string | null;
    image?: string | null;
    url?: string | null;
    source?: string;
};

export default function AboutYouPage() {
    const toast = useToast();
    const [loading, setLoading] = useState(false);
    const [results, setResults] = useState<AboutYouItem[]>([]);

    const [aboutYouUrl, setAboutYouUrl] = useState("https://www.aboutyou.de/c/frauen/schuhe/stiefeletten-20276");
    const [pageMode, setPageMode] = useState<"auto" | "manual">("auto");
    const [filterPages, setFilterPages] = useState<number>(2);
    const [filterBrand, setFilterBrand] = useState<string | string[]>("");
    const [filterKeyword, setFilterKeyword] = useState<string>("");
    const [filterPriceMin, setFilterPriceMin] = useState<number | undefined>(undefined);
    const [filterPriceMax, setFilterPriceMax] = useState<number | undefined>(undefined);
    const [filterSort, setFilterSort] = useState<string>("popularity");

    const [imageModalOpen, setImageModalOpen] = useState(false);
    const [imageModalSrc, setImageModalSrc] = useState("");
    const [imageModalTitle, setImageModalTitle] = useState("");

    const aboutYouBrandOptions = [
        { label: "Marc O'Polo", value: "marc-o-polo-596" },
        { label: "Dr. Martens", value: "dr-martens-729" },
        { label: "Tamaris", value: "tamaris-201" },
        { label: "Rieker", value: "rieker-269" },
        { label: "Timberland", value: "timberland-38" },
        { label: "Tommy Hilfiger", value: "tommy-hilfiger-364" },
        { label: "Bershka", value: "bershka-4739" },
        { label: "Crocs", value: "crocs-301" },
    ];

    const brandLabelByValue = useMemo(() => {
        const map = new Map<string, string>();
        for (const b of aboutYouBrandOptions) {
            map.set(b.value, b.label);
        }
        return map;
    }, []);

    const selectedBrandValues = useMemo(() => {
        if (Array.isArray(filterBrand)) return filterBrand;
        if (typeof filterBrand === "string" && filterBrand.trim()) return [filterBrand.trim()];
        return [];
    }, [filterBrand]);

    const keywordOptions = useMemo(() => {
        const base = [
            "chelsea boots",
            "schnurstiefelette",
            "ankle boots",
            "platform",
            "leder",
            "black",
            "winter",
            "casual",
            "chunky",
            "waterproof",
        ];

        const dynamic: string[] = [];
        for (const brandValue of selectedBrandValues) {
            const brandLabel = brandLabelByValue.get(brandValue);
            if (!brandLabel) continue;
            const b = brandLabel.toLowerCase();
            dynamic.push(
                b,
                `${b} boots`,
                `${b} chelsea boots`,
                `${b} black`
            );
        }

        const all = Array.from(new Set([...base, ...dynamic]));
        return all.map((k) => ({ label: k, value: k }));
    }, [brandLabelByValue, selectedBrandValues]);

    const openImage = (src?: string | null, title?: string) => {
        if (!src) return;
        setImageModalSrc(src);
        setImageModalTitle(title || "Image");
        setImageModalOpen(true);
    };

    const runAboutYouFiltered = async () => {
        setLoading(true);
        try {
            const payload = {
                url: aboutYouUrl || undefined,
                // pages=0 means "auto" mode in Python scraper
                pages: pageMode === "auto" ? 0 : filterPages,
                sort: filterSort || undefined,
                brand: Array.isArray(filterBrand) ? filterBrand.join(",") : filterBrand || undefined,
                keyword: filterKeyword || undefined,
                priceMin: filterPriceMin,
                priceMax: filterPriceMax,
            };

            toast.info("Calling About You scraper...");
            const data = await runAboutYouScraper(payload);
            const items: AboutYouItem[] = data?.items || [];
            setResults(items);
            toast.success(`About You: loaded ${data?.count ?? items.length} items`);
        } catch (e) {
            console.error(e);
            toast.error(e instanceof Error ? e.message : "Failed to run About You scraper");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={{ maxWidth: 1200, margin: "2rem auto", padding: "0 1rem" }}>
            <h1 style={{ fontSize: 24, fontWeight: 700, marginBottom: 12 }}>About You — EU Market Scraper</h1>

            <div className="card" style={{ marginBottom: "1.5rem" }}>
                <div style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
                    <div style={{ minWidth: 420, flex: 1 }}>
                        <label className="field-label">Category URL</label>
                        <input
                            className="input-big"
                            value={aboutYouUrl}
                            onChange={(e) => setAboutYouUrl(e.target.value)}
                            placeholder="https://www.aboutyou.de/c/frauen/schuhe/stiefeletten-20276"
                        />
                    </div>

                    <div style={{ minWidth: 200 }}>
                        <label className="field-label">Page Mode</label>
                        <select
                            className="input-big"
                            value={pageMode}
                            onChange={(e) => setPageMode((e.target.value as "auto" | "manual") || "auto")}
                        >
                            <option value="auto">Auto (Recommended)</option>
                            <option value="manual">Manual</option>
                        </select>
                        {pageMode === "auto" && (
                            <div style={{ fontSize: 12, color: "#6b7280", marginTop: 4 }}>
                                Auto mode stops when no new products are found.
                            </div>
                        )}
                    </div>

                    {pageMode === "manual" && (
                        <div style={{ minWidth: 160 }}>
                            <label className="field-label">Pages</label>
                            <input
                                type="number"
                                className="input-big"
                                value={filterPages}
                                min={1}
                                onChange={(e) => setFilterPages(Number(e.target.value) || 1)}
                            />
                            <div style={{ fontSize: 12, color: "#6b7280", marginTop: 4 }}>
                                Use 1-3 pages to keep scraper fast.
                            </div>
                        </div>
                    )}

                    <div style={{ minWidth: 220 }}>
                        <label className="field-label">Brand</label>
                        <SearchableSelect
                            value={filterBrand}
                            onChange={setFilterBrand}
                            placeholder="Select or type brand…"
                            options={aboutYouBrandOptions}
                            multiple={true}
                        />
                    </div>

                    <div style={{ minWidth: 220 }}>
                        <label className="field-label">Keyword</label>
                        <SearchableSelect
                            value={filterKeyword}
                            onChange={(v) => setFilterKeyword(Array.isArray(v) ? v.join(" ") : v)}
                            placeholder="Select suggestion or type custom..."
                            options={keywordOptions}
                        />
                    </div>

                    <div style={{ minWidth: 150 }}>
                        <label className="field-label">Price Min (€)</label>
                        <input
                            type="number"
                            className="input-big"
                            placeholder="0"
                            value={filterPriceMin ?? ""}
                            onChange={(e) => setFilterPriceMin(e.target.value ? Number(e.target.value) : undefined)}
                        />
                    </div>

                    <div style={{ minWidth: 150 }}>
                        <label className="field-label">Price Max (€)</label>
                        <input
                            type="number"
                            className="input-big"
                            placeholder="500"
                            value={filterPriceMax ?? ""}
                            onChange={(e) => setFilterPriceMax(e.target.value ? Number(e.target.value) : undefined)}
                        />
                    </div>

                    <div style={{ minWidth: 180 }}>
                        <label className="field-label">Sort</label>
                        <select
                            className="input-big"
                            value={filterSort}
                            onChange={(e) => setFilterSort(e.target.value)}
                        >
                            <option value="popularity">🔥 Popularity</option>
                            <option value="price-asc">💸 Price: Low → High</option>
                            <option value="price-desc">💰 Price: High → Low</option>
                            <option value="name-asc">🔤 Name A → Z</option>
                            <option value="name-desc">🔤 Name Z → A</option>
                        </select>
                    </div>

                    <div style={{ marginLeft: "auto" }}>
                        <button className="button-big" onClick={runAboutYouFiltered} disabled={loading} style={{ minWidth: 170 }}>
                            {loading ? "⏳ Scraping..." : "🔍 Run About You"}
                        </button>
                    </div>
                </div>
            </div>

            {results.length > 0 && (
                <div>
                    <h3 style={{ marginBottom: 12 }}>Results ({results.length})</h3>
                    <div style={{ display: "grid", gap: 16, gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))" }}>
                        {results.map((p, idx) => (
                            <div key={idx} className="card" style={{ padding: 12 }}>
                                <div
                                    style={{
                                        width: "100%",
                                        height: 170,
                                        overflow: "hidden",
                                        borderRadius: 8,
                                        background: "#ffffff",
                                        border: "1px solid #e5e7eb",
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                    }}
                                >
                                    {p.image ? (
                                        <img
                                            src={p.image}
                                            alt={p.name}
                                            style={{ width: "100%", height: "100%", objectFit: "contain", cursor: "pointer", padding: 6 }}
                                            onClick={() => openImage(p.image, p.name)}
                                            onError={(e) => {
                                                (e.target as HTMLImageElement).src = "https://via.placeholder.com/400x300?text=No+Image";
                                            }}
                                        />
                                    ) : null}
                                </div>
                                <div style={{ paddingTop: 12 }}>
                                    <div style={{ fontSize: 12, color: "#6b7280" }}>{p.brand || "About You"}</div>
                                    <div style={{ fontWeight: 700, margin: "6px 0" }}>{p.name}</div>
                                    <div style={{ color: "#059669", fontWeight: 700 }}>{p.price || "-"}</div>
                                    {p.url && (
                                        <a href={p.url} target="_blank" rel="noreferrer" style={{ color: "#2563eb" }}>
                                            View
                                        </a>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            <Modal isOpen={imageModalOpen} onClose={() => setImageModalOpen(false)} title={imageModalTitle} size="lg">
                <div style={{ display: "flex", justifyContent: "center", alignItems: "center" }}>
                    <img
                        src={imageModalSrc}
                        alt={imageModalTitle}
                        style={{ maxWidth: "100%", maxHeight: "75vh", objectFit: "contain" }}
                        onError={(e) => {
                            (e.target as HTMLImageElement).src = "https://via.placeholder.com/800x600?text=No+Image";
                        }}
                    />
                </div>
            </Modal>
        </div>
    );
}
