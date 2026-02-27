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

const ABOUTYOU_DEFAULT_URLS: Record<"DE" | "AT" | "CH" | "HU" | "RO", string> = {
    DE: "https://www.aboutyou.de/c/frauen/schuhe/stiefeletten-20276",
    AT: "https://www.aboutyou.at/c/frauen/schuhe/pumps-high-heels-101349?shoeMaterialStyle=35022%2C56630%2C56632%2C56640&color=38932%2C38921%2C38919%2C38931%2C38920%2C38933%2C38935",
    CH: "https://www.aboutyou.ch/c/frauen/schuhe/stiefeletten-20276",
    HU: "https://www.aboutyou.hu/c/frauen/schuhe/stiefeletten-20276",
    RO: "https://www.aboutyou.ro/c/frauen/schuhe/stiefeletten-20276",
};

export default function AboutYouPage() {
    const toast = useToast();
    const [loading, setLoading] = useState(false);
    const [results, setResults] = useState<AboutYouItem[]>([]);

    const [country, setCountry] = useState<"DE" | "AT" | "CH" | "HU" | "RO">("DE");
    const [aboutYouUrl, setAboutYouUrl] = useState(ABOUTYOU_DEFAULT_URLS.DE);
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

    const handleCountryChange = (value: string) => {
        const next = (value || "DE") as "DE" | "AT" | "CH" | "HU" | "RO";
        setCountry(next);
        setAboutYouUrl((prev) => {
            const trimmed = prev.trim();
            const defaults = Object.values(ABOUTYOU_DEFAULT_URLS);
            if (!trimmed || defaults.includes(trimmed)) {
                return ABOUTYOU_DEFAULT_URLS[next];
            }
            return prev;
        });
    };

    const runAboutYouFiltered = async () => {
        setLoading(true);
        try {
            const payload = {
                url: aboutYouUrl || undefined,
                country,
                // pages=0 means "auto" mode in Python scraper
                pages: pageMode === "auto" ? 0 : filterPages,
                sort: filterSort || undefined,
                brand: Array.isArray(filterBrand) ? filterBrand.join(",") : filterBrand || undefined,
                keyword: filterKeyword || undefined,
                priceMin: filterPriceMin,
                priceMax: filterPriceMax,
            };

            toast.info("Pokretanje About You scrapera...");
            const data = await runAboutYouScraper(payload);
            const items: AboutYouItem[] = data?.items || [];
            setResults(items);
            toast.success(`About You: učitano ${data?.count ?? items.length} rezultata`);
        } catch (e) {
            console.error(e);
            toast.error(e instanceof Error ? e.message : "Greška pri pokretanju About You scrapera");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={{ maxWidth: 1200, margin: "2rem auto", padding: "0 1rem" }}>
            <h1 style={{ fontSize: 24, fontWeight: 700, marginBottom: 12, color: "#c9d3e4" }}>About You — Scraper EU tržišta</h1>

            <div className="card" style={{ marginBottom: "1.5rem" }}>
                <div style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
                    <div style={{ minWidth: 420, flex: 1 }}>
                        <label className="field-label">URL kategorije</label>
                        <input
                            className="input-big"
                            value={aboutYouUrl}
                            onChange={(e) => setAboutYouUrl(e.target.value)}
                            placeholder={ABOUTYOU_DEFAULT_URLS[country]}
                        />
                    </div>

                    <div style={{ minWidth: 180 }}>
                        <label className="field-label">Zemlja</label>
                        <select
                            className="input-big"
                            value={country}
                            onChange={(e) => handleCountryChange(e.target.value)}
                        >
                            <option value="DE">Germany (aboutyou.de)</option>
                            <option value="AT">Austria (aboutyou.at)</option>
                            <option value="CH">Switzerland (aboutyou.ch)</option>
                            <option value="HU">Hungary (aboutyou.hu)</option>
                            <option value="RO">Romania (aboutyou.ro)</option>
                        </select>
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
                                Automatski režim staje kada nema novih proizvoda.
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

                    <div style={{ minWidth: 220 }}>
                        <label className="field-label">Brend</label>
                        <SearchableSelect
                            value={filterBrand}
                            onChange={setFilterBrand}
                            placeholder="Select or type brand…"
                            options={aboutYouBrandOptions}
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

                    <div style={{ minWidth: 180 }}>
                        <label className="field-label">Sortiranje</label>
                        <select
                            className="input-big"
                            value={filterSort}
                            onChange={(e) => setFilterSort(e.target.value)}
                        >
                            <option value="popularity">🔥 Popularnost</option>
                            <option value="price-asc">💸 Cena: raste</option>
                            <option value="price-desc">💰 Cena: pada</option>
                            <option value="name-asc">🔤 Naziv A → Z</option>
                            <option value="name-desc">🔤 Naziv Z → A</option>
                        </select>
                    </div>

                    <div style={{ marginLeft: "auto" }}>
                        <button className="button-big" onClick={runAboutYouFiltered} disabled={loading} style={{ minWidth: 170 }}>
                            {loading ? "⏳ Pretraga u toku..." : "🔍 Pokreni About You"}
                        </button>
                    </div>
                </div>
            </div>

            {results.length > 0 && (
                <div>
                    <h3 style={{ marginBottom: 12, color: "#c9d3e4" }}>Rezultati ({results.length})</h3>
                    <div style={{ display: "grid", gap: 16, gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))" }}>
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
                                    <div style={{ fontSize: 12, color: "#8A95B0" }}>{p.brand || "About You"}</div>
                                    <div style={{ fontWeight: 700, margin: "6px 0" }}>{p.name}</div>
                                    <div style={{ color: "#059669", fontWeight: 700 }}>{p.price || "-"}</div>
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
