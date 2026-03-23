import React, { useState } from "react";
import { useToast } from "../components/Toast";
import SearchableSelect from "../components/SearchableSelect";
import { deichmannBrands } from "../components/brands";
import Modal from "../components/Modal";
import { runDeichmannScraper } from "../services/deichmannApi";

export default function DeichmannPage() {
    const toast = useToast();
    const [loading, setLoading] = useState(false);

    // shared filter state (matching GlobalTrendsPage controls)
    const [selectedCategory, setSelectedCategory] = useState("Cipele");
    const [filterPages, setFilterPages] = useState<number>(1);
    const [filterBrand, setFilterBrand] = useState<string | string[]>("");
    const [filterGender, setFilterGender] = useState<string>("women");
    const [filterPriceMin, setFilterPriceMin] = useState<number | undefined>(undefined);
    const [filterPriceMax, setFilterPriceMax] = useState<number | undefined>(undefined);
    const [filterSort, setFilterSort] = useState<string>("popularity");
    const [filterImportToCore, setFilterImportToCore] = useState<boolean>(false);
    const [filterActivationDate, setFilterActivationDate] = useState<string | undefined>(undefined);

    // deichmann specific optional fields
    const deichmannSizeOptions = [
        { label: "36", value: "36-2214" },
        { label: "36.5", value: "365-2215" },
        { label: "36.666", value: "36666-120786" },
        { label: "37.333", value: "37333-120787" },
        { label: "37.5", value: "375-2217" },
        { label: "38", value: "38-2218" },
        { label: "38.5", value: "385-2219" },
        { label: "38.666", value: "38666-126079" },
        { label: "39.333", value: "39333-120788" },
        { label: "39.5", value: "395-2221" },
        { label: "39", value: "39-2220" },
        { label: "40", value: "40-2197" },
        { label: "40.5", value: "405-125094" },
        { label: "40.666", value: "40666-120868" },
        { label: "41.333", value: "41333-120789" },
        { label: "41.5", value: "415-124114" },
        { label: "42", value: "42-2195" },
        { label: "42.5", value: "425-120944" },
    ];

    // sizes not selected by default � show all until user chooses
    const [size, setSize] = useState<string[] | undefined>(undefined);
    const [saleOnly, setSaleOnly] = useState<boolean>(false);
    const [isNewOnly, setIsNewOnly] = useState<boolean>(false);
    const [isLeather, setIsLeather] = useState<string | undefined>(undefined);
    const [waterResistance, setWaterResistance] = useState<string | undefined>(undefined);

    const [results, setResults] = useState<any[]>([]);
    const [imageModalOpen, setImageModalOpen] = useState(false);
    const [imageModalSrc, setImageModalSrc] = useState("");
    const [imageModalTitle, setImageModalTitle] = useState("");

    const categories = ["Patike", "Sandale", "Cipele", "Cizme"];

    const sortOptions = [
        { label: "Najnovije", value: "new-desc" },
        { label: "Akcija", value: "reduction-desc" },
        { label: "Relevantnost", value: "key-relevance" },
        { label: "Cena: raste", value: "price-asc" },
        { label: "Cena: pada", value: "price-desc" }
    ];

    const openImage = (src?: string, title?: string) => {
        if (!src) return;
        setImageModalSrc(src);
        setImageModalTitle(title || "Image");
        setImageModalOpen(true);
    };

    const runDeichmannFiltered = async () => {
        setLoading(true);
        try {
            const categoryMap: Record<string, string> = {
                Patike: "sneakers-92",
                Sandale: "sandalen-...",
                Cipele: "schuhe-82",
                Cizme: "stiefel-85",
            };

            const payload: any = {
                gender: filterGender || "damen-73",
                category: categoryMap[selectedCategory] || "schuhe-82",
                sort: filterSort === 'new' ? 'new-desc' : (filterSort !== 'popularity' ? filterSort : undefined),
                priceMin: filterPriceMin,
                priceMax: filterPriceMax,
                sale: saleOnly || undefined,
                isNew: isNewOnly || undefined,
                // include size only if user selected one or more sizes
                // when size is undefined (no selection) we omit size filters so scraper returns all sizes
                // size can be string or array
                // we'll compute sizePayload below and conditionally add to payload
                brand: Array.isArray(filterBrand) ? filterBrand.join(",") : filterBrand || undefined,
                isLeather: isLeather,
                waterResistance: waterResistance,
                page: filterPages,
                pages: filterPages
            };

            // prepare size payload
            let sizePayload: string | undefined = undefined;
            if (Array.isArray(size) && size.length > 0) sizePayload = size.join(",");
            else if (typeof size === 'string' && size) sizePayload = size;

            if (sizePayload) {
                payload.size = sizePayload;
                payload.sizeEu = sizePayload;
            }

            console.log("? Calling Deichmann scraper with payload:", payload);
            toast.info("Pokretanje Deichmann scraper-a...");
            const data = await runDeichmannScraper(payload);
            console.log("? Deichmann scraper response:", data);
            const items = data.items || [];
            setResults(items);
            toast.success(`Deichmann: ucitano ${data.count ?? items.length} stavki`);
        } catch (e) {
            console.error(e);
            toast.error(e instanceof Error ? e.message : "Gre�ka pri pokretanju Deichmann scraper-a");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="max-w-[1200px] mx-auto my-8 px-4">
            <h1 className="text-2xl font-bold mb-3 text-foreground">Deichmann � Scraper EU tr�i�ta</h1>

            <div className="bg-surface-elevated border border-border rounded-xl p-4 mb-6 transition-colors">
                <div className="flex gap-3 items-center flex-wrap">
                    <div className="min-w-[180px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Deichmann stranice</label>
                        <input 
                            type="number" 
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all" 
                            value={filterPages} 
                            min={1} 
                            onChange={(e) => setFilterPages(Number(e.target.value) || 1)} 
                        />
                    </div>

                    <div className="min-w-[220px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Kategorija</label>
                        <select 
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all" 
                            value={selectedCategory} 
                            onChange={(e) => setSelectedCategory(e.target.value)}
                        >
                            {categories.map(c => <option key={c} value={c}>{c}</option>)}
                        </select>
                    </div>

                    <div className="min-w-[220px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Brend</label>
                        <SearchableSelect 
                            value={filterBrand}
                            onChange={setFilterBrand}
                            placeholder="Select or type brand�"
                            options={deichmannBrands}
                            multiple={true}
                        />
                    </div>

                    <div className="min-w-[160px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Pol</label>
                        <select
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all"
                            value={filterGender}
                            onChange={(e) => setFilterGender(e.target.value)}
                        >
                            <option value="">Unisex</option>
                            <option value="women">?? �ene</option>
                            <option value="men">?? Mu�karci</option>
                            <option value="kids">?? Deca</option>
                        </select>
                    </div>

                    <div className="min-w-[160px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Sortiranje</label>
                        <select
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all"
                            value={filterSort}
                            onChange={(e) => setFilterSort(e.target.value)}
                        >
                            <option value="popularity">?? Popularnost</option>
                            <option value="price-asc">?? Cena: raste</option>
                            <option value="price-desc">?? Cena: pada</option>
                            <option value="new">? Novo u ponudi</option>
                        </select>
                    </div>

                    <div className="min-w-[150px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Min cena (�)</label>
                        <input
                            type="number"
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all"
                            placeholder="0"
                            value={filterPriceMin ?? ""}
                            onChange={(e) => setFilterPriceMin(e.target.value ? Number(e.target.value) : undefined)}
                        />
                    </div>

                    <div className="min-w-[150px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Max cena (�)</label>
                        <input
                            type="number"
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all"
                            placeholder="500"
                            value={filterPriceMax ?? ""}
                            onChange={(e) => setFilterPriceMax(e.target.value ? Number(e.target.value) : undefined)}
                        />
                    </div>

                    <div className="min-w-[220px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Velicina (EUR)</label>
                        <SearchableSelect
                            value={size ?? []}
                            onChange={(v) => setSize(v as string[])}
                            options={deichmannSizeOptions}
                            placeholder="Select sizes..."
                            multiple={true}
                        />
                    </div>

                    <div className="min-w-[260px]">
                        <label className="block text-[11px] font-bold uppercase tracking-wider text-muted mb-1 ml-1">Nova kolekcija</label>
                        <select
                            className="w-full bg-surface border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:ring-2 focus:ring-[var(--focus-ring)] focus:ring-opacity-50 outline-none transition-all"
                            value={filterActivationDate ?? ""}
                            onChange={(e) => setFilterActivationDate(e.target.value || undefined)}
                        >
                            <option value="">Bez filtera</option>
                            <option value="0-7">Posled. 0-7 dana</option>
                            <option value="0-7.7-14">0-7 i 7-14 dana</option>
                            <option value="0-30.0-7.7-14">0-30, 0-7 i 7-14 dana</option>
                        </select>
                    </div>

                    <label className="flex items-center gap-2 cursor-pointer mt-auto py-2">
                        <input
                            type="checkbox"
                            checked={filterImportToCore}
                            onChange={(e) => setFilterImportToCore(e.target.checked)}
                            className="w-4 h-4 rounded border-border bg-surface text-primary focus:ring-[var(--focus-ring)]"
                        />
                        <span className="text-sm text-foreground font-medium">Uvezi u bazu</span>
                    </label>

                    <div className="ml-auto">
                        <button 
                            className="bg-primary hover:bg-primary-hover text-white font-bold py-2 px-6 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed min-w-[160px]"
                            onClick={runDeichmannFiltered} 
                            disabled={loading}
                        >
                            {loading ? '? Pokretanje...' : '?? Pokreni Deichmann'}
                        </button>
                    </div>
                </div>
            </div>

            {results.length > 0 && (
                <div>
                    <h3 className="text-lg font-bold mb-3 text-foreground">Rezultati ({results.length})</h3>
                    <div className="grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-4">
                        {results.map((p, idx) => (
                            <div key={idx} className="bg-surface-elevated border border-border rounded-xl p-3 shadow-sm hover:shadow-md transition-shadow">
                                <div className="w-full h-[220px] overflow-hidden rounded-lg bg-surface border border-border flex items-center justify-center relative group">
                                    {p.image ? (
                                        <img 
                                            src={p.image} 
                                            alt={p.name} 
                                            className="w-full h-full object-cover cursor-pointer transition-transform group-hover:scale-105" 
                                            onClick={() => openImage(p.image, p.name)} 
                                            onError={(e) => { (e.target as HTMLImageElement).src = 'https://via.placeholder.com/400x300?text=No+Image' }} 
                                        />
                                    ) : (
                                        <div className="text-4xl opacity-30">??</div>
                                    )}
                                </div>
                                <div className="mt-3">
                                    <div className="text-[11px] text-muted font-bold uppercase truncate">{p.brand}</div>
                                    <div className="font-bold text-sm text-foreground leading-tight line-clamp-2 h-10 mb-2">{p.name}</div>
                                    <div className="flex items-center justify-between">
                                        <div className="text-accent-success font-bold text-base">{p.price || '-'}</div>
                                        {p.url && (
                                            <a 
                                                href={p.url} 
                                                target="_blank" 
                                                rel="noreferrer" 
                                                className="text-primary hover:text-primary-hover font-bold text-xs"
                                            >
                                                POGLEDAJ ?
                                            </a>
                                        )}
                                    </div>
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
                        onError={(e) => { (e.target as HTMLImageElement).src = 'https://via.placeholder.com/800x600?text=No+Image' }} 
                    />
                </div>
            </Modal>
        </div>
    );
}

            
