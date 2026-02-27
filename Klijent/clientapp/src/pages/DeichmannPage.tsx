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

    // sizes not selected by default — show all until user chooses
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

            console.log("▶ Calling Deichmann scraper with payload:", payload);
            toast.info("Pokretanje Deichmann scraper-a...");
            const data = await runDeichmannScraper(payload);
            console.log("◄ Deichmann scraper response:", data);
            const items = data.items || [];
            setResults(items);
            toast.success(`Deichmann: učitano ${data.count ?? items.length} stavki`);
        } catch (e) {
            console.error(e);
            toast.error(e instanceof Error ? e.message : "Greška pri pokretanju Deichmann scraper-a");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={{ maxWidth: 1200, margin: "2rem auto", padding: "0 1rem" }}>
            <h1 style={{ fontSize: 24, fontWeight: 700, marginBottom: 12, color: "#c9d3e4" }}>Deichmann — Scraper EU tržišta</h1>

            <div className="card" style={{ marginBottom: "1.5rem" }}>
                <div style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
                    <div style={{ minWidth: 180 }}>
                        <label className="field-label">Deichmann stranice</label>
                        <input type="number" className="input-big" value={filterPages} min={1} onChange={(e) => setFilterPages(Number(e.target.value) || 1)} />
                    </div>

                    <div style={{ minWidth: 220 }}>
                        <label className="field-label">Kategorija</label>
                        <select className="input-big" value={selectedCategory} onChange={(e) => setSelectedCategory(e.target.value)}>
                            {categories.map(c => <option key={c} value={c}>{c}</option>)}
                        </select>
                    </div>

                    <div style={{ minWidth: 220 }}>
                        <label className="field-label">Brend</label>
                        <SearchableSelect 
                            value={filterBrand}
                            onChange={setFilterBrand}
                            placeholder="Select or type brand…"
                            options={deichmannBrands}
                            multiple={true}
                        />
                    </div>

                    <div style={{ minWidth: 160 }}>
                        <label className="field-label">Pol</label>
                        <select
                            className="input-big"
                            value={filterGender}
                            onChange={(e) => setFilterGender(e.target.value)}
                        >
                            <option value="">Unisex</option>
                            <option value="women">👠 žene</option>
                            <option value="men">👞 Muškarci</option>
                            <option value="kids">🧒 Deca</option>
                        </select>
                    </div>

                    <div style={{ minWidth: 160 }}>
                        <label className="field-label">Sortiranje</label>
                        <select
                            className="input-big"
                            value={filterSort}
                            onChange={(e) => setFilterSort(e.target.value)}
                        >
                            <option value="popularity">🔥 Popularnost</option>
                            <option value="price-asc">💸 Cena: raste</option>
                            <option value="price-desc">💰 Cena: pada</option>
                            <option value="new">✨ Novo u ponudi</option>
                        </select>
                    </div>

                    <div style={{ minWidth: 150 }}>
                        <label className="field-label">Min cena (€)</label>
                        <input
                            type="number"
                            className="input-big"
                            placeholder="0"
                            value={filterPriceMin ?? ""}
                            onChange={(e) => setFilterPriceMin(e.target.value ? Number(e.target.value) : undefined)}
                        />
                    </div>

                    <div style={{ minWidth: 150 }}>
                        <label className="field-label">Max cena (€)</label>
                        <input
                            type="number"
                            className="input-big"
                            placeholder="500"
                            value={filterPriceMax ?? ""}
                            onChange={(e) => setFilterPriceMax(e.target.value ? Number(e.target.value) : undefined)}
                        />
                    </div>

                    <div style={{ minWidth: 220 }}>
                        <label className="field-label">Veličina (EUR)</label>
                        <SearchableSelect
                            value={size ?? []}
                            onChange={(v) => setSize(v as string[])}
                            options={deichmannSizeOptions}
                            placeholder="Select sizes..."
                            multiple={true}
                        />
                    </div>

                    <div style={{ minWidth: 260 }}>
                        <label className="field-label">Nova kolekcija</label>
                        <select
                            className="input-big"
                            value={filterActivationDate ?? ""}
                            onChange={(e) => setFilterActivationDate(e.target.value || undefined)}
                        >
                            <option value="">Bez filtera</option>
                            <option value="0-7">Posled. 0-7 dana</option>
                            <option value="0-7.7-14">0-7 i 7-14 dana</option>
                            <option value="0-30.0-7.7-14">0-30, 0-7 i 7-14 dana</option>
                        </select>
                    </div>

                    <label 
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: "0.5rem",
                            cursor: "pointer",
                            marginTop: "auto"
                        }}
                    >
                        <input
                            type="checkbox"
                            checked={filterImportToCore}
                            onChange={(e) => setFilterImportToCore(e.target.checked)}
                        />
                        <span style={{ fontSize: 14, color: "#c9d3e4" }}>Uvezi u bazu</span>
                    </label>

                    <div style={{ marginLeft: "auto" }}>
                        <button className="button-big" onClick={runDeichmannFiltered} disabled={loading} style={{ minWidth: 160 }}>
                            {loading ? '⏳ Pokretanje...' : '🔍 Pokreni Deichmann'}
                        </button>
                    </div>
                </div>
            </div>

            {results.length > 0 && (
                <div>
                    <h3 style={{ marginBottom: 12, color: "#c9d3e4" }}>Rezultati ({results.length})</h3>
                    <div style={{ display: 'grid', gap: 16, gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))' }}>
                        {results.map((p, idx) => (
                            <div key={idx} className="card" style={{ padding: 12 }}>
                                <div style={{ width: '100%', height: 220, overflow: 'hidden', borderRadius: 8, background: '#1A1F2E' }}>
                                    {p.image ? (
                                        <img src={p.image} alt={p.name} style={{ width: '100%', height: '100%', objectFit: 'cover', cursor: 'pointer' }} onClick={() => openImage(p.image, p.name)} onError={(e) => { (e.target as HTMLImageElement).src = 'https://via.placeholder.com/400x300?text=No+Image' }} />
                                    ) : null}
                                </div>
                                <div style={{ paddingTop: 12 }}>
                                    <div style={{ fontSize: 12, color: '#8A95B0' }}>{p.brand}</div>
                                    <div style={{ fontWeight: 700, margin: '6px 0', color: '#c9d3e4' }}>{p.name}</div>
                                    <div style={{ color: '#059669', fontWeight: 700 }}>{p.price || '-'}</div>
                                    {p.url && <a href={p.url} target="_blank" rel="noreferrer" style={{ color: '#4F8EF7' }}>Pogledaj</a>}
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            <Modal isOpen={imageModalOpen} onClose={() => setImageModalOpen(false)} title={imageModalTitle} size="lg">
                <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
                    <img src={imageModalSrc} alt={imageModalTitle} style={{ maxWidth: '100%', maxHeight: '75vh', objectFit: 'contain' }} onError={(e) => { (e.target as HTMLImageElement).src = 'https://via.placeholder.com/800x600?text=No+Image' }} />
                </div>
            </Modal>
        </div>
    );
}
