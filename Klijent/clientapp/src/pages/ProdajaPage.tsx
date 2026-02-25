import React from "react";
import CreateProdajaForm from "../components/prodaja/CreateProdajaForm";
import { KreirajProdajuDto } from "../types/prodaja/prodaja";
import { fetchProdajaArtikliLookup, type ProdajaArtikalLookupDto } from "../services/prodajaApi";

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

    if (loadingArtikli) {
        return (
            <div className="card">
                <p style={{ textAlign: "center", padding: "2rem" }}>Ucitavanje artikala...</p>
            </div>
        );
    }

    if (artikli.length === 0) {
        return (
            <div className="card">
                <p style={{ textAlign: "center", padding: "2rem", color: "#dc2626" }}>
                    Nema dostupnih artikala. Molimo kreirajte artikle pre prodaje.
                </p>
            </div>
        );
    }

    return (
        <CreateProdajaForm
            artikli={artikli}
            onSearchArtikli={handleSearchArtikli}
            onSubmit={handleSubmit}
        />
    );
}
