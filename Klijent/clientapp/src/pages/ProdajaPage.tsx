import React from "react";
import { ShoppingCart } from "lucide-react";
import CreateProdajaForm from "../components/prodaja/CreateProdajaForm";
import { KreirajProdajuDto } from "../types/prodaja/prodaja";
import { fetchProdajaArtikliLookup, type ProdajaArtikalLookupDto } from "../services/prodajaApi";
import { InventoryKpiRow, InventoryPageShell, InventoryPanel, InventoryState } from "../components/inventory/InventoryPageShell";

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
    const API = import.meta.env.VITE_API_BASE_URL;

    React.useEffect(() => {
        let aborted = false;

        const loadInitial = async () => {
            try {
                const data = await fetchProdajaArtikliLookup("", 150, false);
                if (!aborted) {
                    setArtikli((data ?? []).map(toOption));
                    setLoadingArtikli(false);
                }
            } catch (e: unknown) {
                console.error("Error fetching artikli lookup:", e);
                if (!aborted) setLoadingArtikli(false);
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
            const res = await fetch(`${API}/api/prodaja`, {
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
                    { label: "Artikli ucitani", value: `${artikli.length}` },
                    { label: "Status kataloga", value: loadingArtikli ? "Ucitavanje" : "Spremno", tone: loadingArtikli ? "warning" : "positive" },
                    { label: "Pretraga", value: "Lookup API" },
                    { label: "Tok", value: "Cart + Submit" },
                ]}
            />

            <InventoryPanel>
                {loadingArtikli && <InventoryState message="Ucitavanje artikala..." tone="warning" />}
                {!loadingArtikli && artikli.length === 0 && (
                    <InventoryState
                        message="Nema dostupnih artikala. Kreirajte artikle pre prodaje."
                        tone="danger"
                    />
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
