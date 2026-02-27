import React, { useMemo, useState } from "react";
import Modal from "../components/Modal";
import SearchableSelect from "../components/SearchableSelect";
import { useToast } from "../components/Toast";
import { runHumanicScraper } from "../services/humanicApi";

type HumanicItem = {
    brand?: string | null;
    name: string;
    price?: string | null;
    old_price?: string | null;
    image?: string | null;
    url?: string | null;
    source?: string;
};

export default function HumanicPage() {
    const toast = useToast();
    const [loading, setLoading] = useState(false);
    const [results, setResults] = useState<HumanicItem[]>([]);

    const [humanicUrl, setHumanicUrl] = useState("https://www.humanic.net/at/c/Damenschuhe/womenShoes");
    const [pageMode, setPageMode] = useState<"auto" | "manual">("auto");
    const [filterPages, setFilterPages] = useState<number>(2);

    const [filterSort, setFilterSort] = useState<string>("bestseller");
    const [filterBrand, setFilterBrand] = useState<string | string[]>("");
    const [filterUpperMaterials, setFilterUpperMaterials] = useState<string | string[]>([
        "Leder",
        "Glattleder",
        "Leder-Textil",
        "Lederimitat",
    ]);
    const [filterKeyword, setFilterKeyword] = useState<string>("");
    const [filterPriceMin, setFilterPriceMin] = useState<number | undefined>(undefined);
    const [filterPriceMax, setFilterPriceMax] = useState<number | undefined>(undefined);

    const [imageModalOpen, setImageModalOpen] = useState(false);
    const [imageModalSrc, setImageModalSrc] = useState("");
    const [imageModalTitle, setImageModalTitle] = useState("");

    const brandOptions = [
        { label: "Dr. Martens", value: "Dr. Martens" },
        { label: "Marc O'Polo", value: "Marc O'Polo" },
        { label: "Tamaris", value: "TAMARIS" },
        { label: "Rieker", value: "Rieker" },
        { label: "Timberland", value: "Timberland" },
        { label: "Tommy Hilfiger", value: "Tommy Hilfiger" },
        { label: "Adidas", value: "Adidas" },
        { label: "New Balance", value: "New Balance" },
        { label: "Kate Gray", value: "Kate Gray" },
        { label: "Pat Calvin", value: "Pat Calvin" },
    ];

    const upperMaterialOptions = [
        { label: "Leder", value: "Leder" },
        { label: "Glattleder", value: "Glattleder" },
        { label: "Leder-Textil", value: "Leder-Textil" },
        { label: "Lederimitat", value: "Lederimitat" },
        { label: "Nubukleder", value: "Nubukleder" },
        { label: "Lackleder", value: "Lackleder" },
    ];

    const selectedBrandValues = useMemo(() => {
        if (Array.isArray(filterBrand)) return filterBrand;
        if (typeof filterBrand === "string" && filterBrand.trim()) return [filterBrand.trim()];
        return [];
    }, [filterBrand]);

    const keywordOptions = useMemo(() => {
        const base = ["stiefelette", "chelsea boot", "sneaker", "leder", "winter", "black"];
        const dynamic = selectedBrandValues.map((b) => b.toLowerCase());
        const all = Array.from(new Set([...base, ...dynamic]));
        return all.map((k) => ({ label: k, value: k }));
    }, [selectedBrandValues]);

    const openImage = (src?: string | null, title?: string) => {
        if (!src) return;
        setImageModalSrc(src);
        setImageModalTitle(title || "Image");
        setImageModalOpen(true);
    };

    const runHumanicFiltered = async () => {
        setLoading(true);
        try {
            const payload = {
                url: humanicUrl || undefined,
                pages: pageMode === "auto" ? 0 : filterPages,
                sort: filterSort || "bestseller",
                brand: Array.isArray(filterBrand) ? filterBrand.join(",") : filterBrand || undefined,
                upperMaterials: Array.isArray(filterUpperMaterials)
                    ? filterUpperMaterials.join(",")
                    : filterUpperMaterials || undefined,
                keyword: filterKeyword || undefined,
                priceMin: filterPriceMin,
                priceMax: filterPriceMax,
            };

            toast.info("Pokretanje Humanic scraper-a...");
            const data = await runHumanicScraper(payload);
            const items: HumanicItem[] = data?.items || [];
            setResults(items);
            toast.success(`Humanic: učitano ${data?.count ?? items.length} stavki`);
        } catch (e) {
            console.error(e);
            toast.error(e instanceof Error ? e.message : "Greška pri pokretanju Humanic scraper-a");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={{ maxWidth: 1200, margin: "2rem auto", padding: "0 1rem" }}>
            <h1 style={{ fontSize: 24, fontWeight: 700, marginBottom: 12, color: "#c9d3e4" }}>Humanic — Scraper EU tržišta</h1>

            <div className="card" style={{ marginBottom: "1.5rem" }}>
                <div style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
                    <div style={{ minWidth: 420, flex: 1 }}>
                        <label className="field-label">URL kategorije</label>
                        <input
                            className="input-big"
                            value={humanicUrl}
                            onChange={(e) => setHumanicUrl(e.target.value)}
                            placeholder="https://www.humanic.net/at/c/Damenschuhe/womenShoes"
                        />
                    </div>

                    <div style={{ minWidth: 200 }}>
                        <label className="field-label">Režim stranica</label>
                        <select
                            className="input-big"
                            value={pageMode}
                            onChange={(e) => setPageMode((e.target.value as "auto" | "manual") || "auto")}
                        >
                            <option value="auto">Automatski (preporučeno)</option>
                            <option value="manual">Ručno</option>
                        </select>
                        {pageMode === "auto" && (
                            <div style={{ fontSize: 12, color: "#8A95B0", marginTop: 4 }}>
                                Automatski režim se zaustavlja kad nema novih proizvoda.
                            </div>
                        )}
                    </div>

                    {pageMode === "manual" && (
                        <div style={{ minWidth: 160 }}>
                            <label className="field-label">Stranice</label>
                            <input
                                type="number"
                                className="input-big"
                                value={filterPages}
                                min={1}
                                onChange={(e) => setFilterPages(Number(e.target.value) || 1)}
                            />
                            <div style={{ fontSize: 12, color: "#8A95B0", marginTop: 4 }}>
                                Koristite 1-3 stranice za brže pokretanje.
                            </div>
                        </div>
                    )}

                    <div style={{ minWidth: 180 }}>
                        <label className="field-label">Sortiranje</label>
                        <select
                            className="input-big"
                            value={filterSort}
                            onChange={(e) => setFilterSort(e.target.value)}
                        >
                            <option value="bestseller">Bestseller</option>
                            <option value="new">Noviteti</option>
                            <option value="price-asc">Cena: raste</option>
                            <option value="price-desc">Cena: pada</option>
                            <option value="relevance">Relevantnost</option>
                        </select>
                    </div>

                    <div style={{ minWidth: 240 }}>
                        <label className="field-label">Brend</label>
                        <SearchableSelect
                            value={filterBrand}
                            onChange={setFilterBrand}
                            placeholder="Select or type brand..."
                            options={brandOptions}
                            multiple={true}
                        />
                    </div>

                    <div style={{ minWidth: 260 }}>
                        <label className="field-label">Materijal gornjišta</label>
                        <SearchableSelect
                            value={filterUpperMaterials}
                            onChange={setFilterUpperMaterials}
                            placeholder="Select materials..."
                            options={upperMaterialOptions}
                            multiple={true}
                        />
                    </div>

                    <div style={{ minWidth: 220 }}>
                        <label className="field-label">Ključna reč</label>
                        <SearchableSelect
                            value={filterKeyword}
                            onChange={(v) => setFilterKeyword(Array.isArray(v) ? v.join(" ") : v)}
                            placeholder="Select suggestion or type custom..."
                            options={keywordOptions}
                        />
                    </div>

                    <div style={{ minWidth: 150 }}>
                        <label className="field-label">Min cena (EUR)</label>
                        <input
                            type="number"
                            className="input-big"
                            placeholder="0"
                            value={filterPriceMin ?? ""}
                            onChange={(e) => setFilterPriceMin(e.target.value ? Number(e.target.value) : undefined)}
                        />
                    </div>

                    <div style={{ minWidth: 150 }}>
                        <label className="field-label">Max cena (EUR)</label>
                        <input
                            type="number"
                            className="input-big"
                            placeholder="500"
                            value={filterPriceMax ?? ""}
                            onChange={(e) => setFilterPriceMax(e.target.value ? Number(e.target.value) : undefined)}
                        />
                    </div>

                    <div style={{ marginLeft: "auto" }}>
                        <button className="button-big" onClick={runHumanicFiltered} disabled={loading} style={{ minWidth: 160 }}>
                            {loading ? "Pokretanje..." : "Pokreni Humanic"}
                        </button>
                    </div>
                </div>
            </div>

            {results.length > 0 && (
                <div>
                    <h3 style={{ marginBottom: 12, color: "#c9d3e4" }}>Rezultati ({results.length})</h3>
                    <div style={{ display: "grid", gap: 16, gridTemplateColumns: "repeat(auto-fill, minmax(190px, 1fr))" }}>
                        {results.map((p, idx) => (
                            <div key={idx} className="card" style={{ padding: 12 }}>
                                <div
                                    style={{
                                        width: "100%",
                                        height: 170,
                                        overflow: "hidden",
                                        borderRadius: 8,
                                        background: "#1A1F2E",
                                        border: "1px solid #2A3045",
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
                                    <div style={{ fontSize: 12, color: "#8A95B0" }}>{p.brand || "Humanic"}</div>
                                    <div style={{ fontWeight: 700, margin: "6px 0", color: "#c9d3e4" }}>{p.name}</div>
                                    <div style={{ color: "#059669", fontWeight: 700 }}>{p.price || "-"}</div>
                                    {p.old_price && p.old_price !== p.price && (
                                        <div style={{ color: "#8A95B0", fontSize: 12, textDecoration: "line-through" }}>{p.old_price}</div>
                                    )}
                                    {p.url && (
                                        <a href={p.url} target="_blank" rel="noreferrer" style={{ color: "#4F8EF7" }}>
                                            Pogledaj
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
