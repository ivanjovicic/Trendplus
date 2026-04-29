import React from "react";
import { ShoppingCart } from "lucide-react";
import CreateProdajaForm from "../components/prodaja/CreateProdajaForm";
import { KreirajProdajuDto } from "../types/prodaja/prodaja";
import { fetchProdajaArtikliLookup, type ProdajaArtikalLookupDto } from "../services/prodajaApi";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel, InventoryState } from "../components/inventory/InventoryPageShell";
import { getDataScope, setDataScope, type DataScope } from "../utils/dataScope";
import { apiUrl } from "../utils/apiUrl";

type ProdajaArtikalOption = {
    id: number;
    naziv: string;
    cena: number;
};

function toOption(x: ProdajaArtikalLookupDto): ProdajaArtikalOption {
    return {
        id: x.id,
        naziv: x.naziv,
        cena: Number(x.cena ?? 0),
    };
}

export default function ProdajaPage() {
    const [loadingArtikli, setLoadingArtikli] = React.useState(true);
    const [artikli, setArtikli] = React.useState<ProdajaArtikalOption[]>([]);
    const [catalogError, setCatalogError] = React.useState<string | null>(null);
    const [dataScope, setDataScopeValue] = React.useState<DataScope>(getDataScope());
    React.useEffect(() => {
        const handleScopeChange = () => {
            setDataScopeValue(getDataScope());
        };

        window.addEventListener("trendplus:data-scope-changed", handleScopeChange);
        return () => {
            window.removeEventListener("trendplus:data-scope-changed", handleScopeChange);
        };
    }, []);

    React.useEffect(() => {
        let aborted = false;

        const loadInitial = async () => {
            try {
                const data = await fetchProdajaArtikliLookup("", 150, false);
                if (!aborted) {
                    setArtikli((data ?? []).map(toOption));
                    setCatalogError(null);
                    setLoadingArtikli(false);
                }
            } catch (e: unknown) {
                console.error("Error fetching artikli lookup:", e);
                if (!aborted) {
                    setCatalogError("Neuspešno učitavanje kataloga artikala.");
                    setLoadingArtikli(false);
                }
            }
        };

        loadInitial();
        return () => {
            aborted = true;
        };
    }, []);

    const handleSearchArtikli = React.useCallback(async (query: string) => {
        const data = await fetchProdajaArtikliLookup(query, 25, false);
        return (data ?? []).map(toOption);
    }, []);

    const handleSubmit = async (data: KreirajProdajuDto): Promise<void> => {
        console.debug("Outgoing prodaja DTO:", data);

        try {
            const res = await fetch(apiUrl("/api/prodaja"), {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(data),
            });

            const raw = await res.text();
            let parsedBody: unknown = raw;
            const contentType = res.headers.get("content-type") ?? "";
            if (contentType.includes("application/json")) {
                try {
                    parsedBody = JSON.parse(raw);
                } catch {
                    // fall back to raw text
                }
            }

            if (!res.ok) {
                console.error("Prodaja POST failed:", { status: res.status, body: parsedBody });
                const message = typeof parsedBody === "string" && parsedBody.trim()
                    ? parsedBody
                    : (typeof parsedBody === "object" ? JSON.stringify(parsedBody) : `Status ${res.status}`);
                throw new Error(message);
            }

            console.debug("Prodaja POST succeeded:", parsedBody);
        } catch (err: unknown) {
            console.error("Error submitting prodaja:", err);
            throw err;
        }
    };

    return (
        <InventoryPageShell
            icon={ShoppingCart}
            title="Prodaja"
            subtitle="POS tok prodaje sa pretragom artikala i validacijom cene kroz checkout formu."
        >
            <InventoryKpiRow
                items={[
                    { label: "Artikli učitani", value: `${artikli.length}` },
                    { label: "Status kataloga", value: loadingArtikli ? "Učitavanje" : catalogError ? "Greška" : "Spremno", tone: loadingArtikli ? "warning" : catalogError ? "danger" : "positive" },
                    { label: "Pretraga", value: "Lookup API" },
                    { label: "Prikaz", value: dataScope },
                ]}
            />

            <InventoryPanel>
                {loadingArtikli && <InventoryState message="Učitavanje artikala..." tone="warning" />}
                {!loadingArtikli && catalogError && <InventoryState message={catalogError} tone="danger" />}
                {!loadingArtikli && !catalogError && artikli.length === 0 && (
                    <div className="space-y-3">
                        <InventoryState
                            message={dataScope === "all"
                                ? "Nema dostupnih artikala. Kreirajte artikle pre prodaje."
                                : `Nema dostupnih artikala za prikaz '${dataScope}'.`}
                            tone="danger"
                        />
                        {dataScope !== "all" && (
                            <div className="flex justify-center">
                                <button
                                    type="button"
                                    className="rounded-lg border border-info bg-info/10 px-3 py-2 text-xs font-semibold text-info transition hover:bg-info/20"
                                    onClick={() => {
                                        setDataScope("all");
                                        setDataScopeValue("all");
                                        window.dispatchEvent(new Event("trendplus:data-scope-changed"));
                                        window.location.reload();
                                    }}
                                >
                                    Prikazi sve artikle
                                </button>
                            </div>
                        )}
                    </div>
                )}
                {!loadingArtikli && artikli.length > 0 && (
                    <CreateProdajaForm
                        artikli={artikli}
                        onSearchArtikli={handleSearchArtikli}
                        onSubmit={handleSubmit}
                    />
                )}
            </InventoryPanel>
        </InventoryPageShell>
    );
}
