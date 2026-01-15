import React from "react";
import CreateProdajaForm from "../components/prodaja/CreateProdajaForm";
import { KreirajProdajuDto } from "../types/prodaja/prodaja";

const ARTICLES_CACHE_KEY = "cached_artikli_prodaja";
const ARTICLES_CACHE_TS = "cached_artikli_ts";
const CACHE_DURATION = 1000 * 60 * 15; // 15 minutes

export default function ProdajaPage() {
    const [loadingArtikli, setLoadingArtikli] = React.useState(true);
    const [artikli, setArtikli] = React.useState<{ id: number; naziv: string; cena: number }[]>([]);
    const API = import.meta.env.VITE_API_BASE_URL;

    React.useEffect(() => {
        let aborted = false;
        const controller = new AbortController();

        const fetchArtikli = async () => {
            // Try cache first
            try {
                const cached = localStorage.getItem(ARTICLES_CACHE_KEY);
                const ts = localStorage.getItem(ARTICLES_CACHE_TS);
                const now = Date.now();

                if (cached && ts && (now - Number(ts) < CACHE_DURATION)) {
                    const parsed = JSON.parse(cached);
                    if (parsed && Array.isArray(parsed) && parsed.length > 0) {
                        setArtikli(parsed);
                        setLoadingArtikli(false);
                        console.log("🚀 Loaded articles from local cache");
                        // Refresh in background if needed (silent update)
                    }
                }
            } catch (err) {
                console.warn("Failed to read articles cache", err);
            }

            try {
                const res = await fetch(`${API}/artikli`, { signal: controller.signal });
                if (res.ok) {
                    const data = await res.json();
                    if (!aborted) {
                        setArtikli(data ?? []);
                        setLoadingArtikli(false);
                        
                        // Update cache
                        try {
                            localStorage.setItem(ARTICLES_CACHE_KEY, JSON.stringify(data));
                            localStorage.setItem(ARTICLES_CACHE_TS, Date.now().toString());
                        } catch (e) {
                            console.warn("QuotaExceeded or other cache error", e);
                        }
                    }
                } else {
                    console.error("Failed to fetch artikli:", res.status, await res.text());
                    if (!aborted) setLoadingArtikli(false);
                }
            } catch (e: unknown) {
                if (e instanceof DOMException && e.name === "AbortError") return;
                console.error("Error fetching artikli:", e);
                if (!aborted) setLoadingArtikli(false);
            }
        };

        fetchArtikli();

        return () => {
            aborted = true;
            controller.abort();
        };
    }, [API]);

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
                <p style={{ textAlign: 'center', padding: '2rem' }}>Učitavanje artikala...</p>
            </div>
        );
    }

    if (artikli.length === 0) {
        return (
            <div className="card">
                <p style={{ textAlign: 'center', padding: '2rem', color: '#dc2626' }}>
                    Nema dostupnih artikala. Molimo kreirajte artikle pre prodaje.
                </p>
            </div>
        );
    }

    return <CreateProdajaForm artikli={artikli} onSubmit={handleSubmit} />;
}