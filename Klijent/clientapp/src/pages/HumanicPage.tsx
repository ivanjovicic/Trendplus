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
        <div className="max-w-[1200px] mx-auto my-8 px-4">
            <h1 className="text-2xl font-bold mb-3 text-foreground">Humanic — Scraper EU tržišta</h1>

            <div className="bg-surface-elevated border border-border rounded-xl p-4 mb-6">
                <div className="flex gap-3 items-center flex-wrap">
                    <div className="min-w-[420px] flex-1">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">URL kategorije</label>
                        <input
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all"
                            value={humanicUrl}
                            onChange={(e) => setHumanicUrl(e.target.value)}
                            placeholder="https://www.humanic.net/at/c/Damenschuhe/womenShoes"
                        />
                    </div>

                    <div className="min-w-[200px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Režim stranica</label>
                        <select
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all"
                            value={pageMode}
                            onChange={(e) => setPageMode((e.target.value as "auto" | "manual") || "auto")}
                        >
                            <option value="auto">Automatski (preporučeno)</option>
                            <option value="manual">Ručno</option>
                        </select>
                        {pageMode === "auto" && (
                            <div className="text-[11px] text-muted mt-1 ml-1">Automatski režim se zaustavlja kad nema novih proizvoda.</div>
                        )}
                    </div>

                    {pageMode === "manual" && (
                        <div className="min-w-[160px]">
                            <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Stranice</label>
                            <input
                                type="number"
                                className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all"
                                value={filterPages}
                                min={1}
                                onChange={(e) => setFilterPages(Number(e.target.value) || 1)}
                            />
                            <div className="text-[11px] text-muted mt-1 ml-1">Koristite 1-3 stranice za brže pokretanje.</div>
                        </div>
                    )}

                    <div className="min-w-[180px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Sortiranje</label>
                        <select
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all"
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

                    <div className="min-w-[240px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Brend</label>
                        <SearchableSelect
                            value={filterBrand}
                            onChange={setFilterBrand}
                            placeholder="Select or type brand..."
                            options={brandOptions}
                            multiple={true}
                        />
                    </div>

                    <div className="min-w-[260px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Materijal gornjišta</label>
                        <SearchableSelect
                            value={filterUpperMaterials}
                            onChange={setFilterUpperMaterials}
                            placeholder="Select materials..."
                            options={upperMaterialOptions}
                            multiple={true}
                        />
                    </div>

                    <div className="min-w-[220px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Ključna reč</label>
                        <SearchableSelect
                            value={filterKeyword}
                            onChange={(v) => setFilterKeyword(Array.isArray(v) ? v.join(" ") : v)}
                            placeholder="Select suggestion or type custom..."
                            options={keywordOptions}
                        />
                    </div>

                    <div className="min-w-[150px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Min cena (EUR)</label>
                        <input
                            type="number"
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all"
                            placeholder="0"
                            value={filterPriceMin ?? ""}
                            onChange={(e) => setFilterPriceMin(e.target.value ? Number(e.target.value) : undefined)}
                        />
                    </div>

                    <div className="min-w-[150px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Max cena (EUR)</label>
                        <input
                            type="number"
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all"
                            placeholder="500"
                            value={filterPriceMax ?? ""}
                            onChange={(e) => setFilterPriceMax(e.target.value ? Number(e.target.value) : undefined)}
                        />
                    </div>

                    <div className="ml-auto">
                        <button
                            className="bg-primary hover:bg-primary-hover text-white font-bold py-2 px-4 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed min-w-[160px]"
                            onClick={runHumanicFiltered}
                            disabled={loading}
                        >
                            {loading ? "Pokretanje..." : "Pokreni Humanic"}
                        </button>
                    </div>
                </div>
            </div>

            {results.length > 0 && (
                <div>
                    <h3 className="text-lg font-bold mb-3 text-foreground">Rezultati ({results.length})</h3>
                    <div className="grid grid-cols-[repeat(auto-fill,minmax(190px,1fr))] gap-4">
                        {results.map((p, idx) => (
                            <div key={idx} className="bg-surface-elevated border border-border rounded-xl p-3 shadow-sm hover:shadow-md transition-shadow">
                                <div className="w-full h-[170px] overflow-hidden rounded-lg bg-surface border border-border flex items-center justify-center relative group">
                                    {p.image ? (
                                        <img
                                            src={p.image}
                                            alt={p.name}
                                            className="w-full h-full object-contain cursor-pointer p-1.5 transition-transform group-hover:scale-105"
                                            onClick={() => openImage(p.image, p.name)}
                                            onError={(e) => {
                                                (e.target as HTMLImageElement).src = "https://via.placeholder.com/400x300?text=No+Image";
                                            }}
                                        />
                                    ) : (
                                        <div className="text-4xl opacity-30">👟</div>
                                    )}
                                </div>
                                <div className="mt-3">
                                    <div className="text-[11px] text-muted font-bold uppercase truncate">{p.brand || "Humanic"}</div>
                                    <div className="font-bold text-sm text-foreground leading-tight line-clamp-2 h-10 mb-2">{p.name}</div>
                                    <div className="text-accent-success font-bold text-base">{p.price || '-'}</div>
                                    {p.old_price && p.old_price !== p.price && (
                                        <div className="text-[11px] text-muted line-through">{p.old_price}</div>
                                    )}
                                    {p.url && (
                                        <a href={p.url} target="_blank" rel="noreferrer" className="text-primary hover:text-primary-hover font-bold text-xs">
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
                <div className="flex justify-center items-center p-2">
                    <img
                        src={imageModalSrc}
                        alt={imageModalTitle}
                        className="max-w-full max-h-[75vh] object-contain rounded-lg shadow-xl"
                        onError={(e) => {
                            (e.target as HTMLImageElement).src = "https://via.placeholder.com/800x600?text=No+Image";
                        }}
                    />
                </div>
            </Modal>
        </div>
    );
}
