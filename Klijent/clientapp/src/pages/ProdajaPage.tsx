import React from "react";
import CreateProdajaForm from "../components/prodaja/CreateProdajaForm";
import { KreirajProdajuDto, ProdajaStavkaDto } from "../types/prodaja/prodaja";

export default function ProdajaPage() {
    const [loadingArtikli, setLoadingArtikli] = React.useState(true);
    const [artikli, setArtikli] = React.useState<{ id: number; naziv: string; cena: number }[]>([]);

    React.useEffect(() => {
        let aborted = false;
        const controller = new AbortController();

        const fetchArtikli = async () => {
            try {
                const res = await fetch("/artikli", { signal: controller.signal });
                if (res.ok) {
                    const data = await res.json();
                    if (!aborted) {
                        setArtikli(data ?? []);
                        setLoadingArtikli(false);
                    }
                } else {
                    console.error("Failed to fetch artikli:", res.status, await res.text());
                    if (!aborted) setLoadingArtikli(false);
                }
            } catch (e) {
                if ((e as any)?.name === "AbortError") return;
                console.error("Error fetching artikli:", e);
                if (!aborted) setLoadingArtikli(false);
            }
        };

        fetchArtikli();

        return () => {
            aborted = true;
            controller.abort();
        };
    }, []);

    const handleSubmit = async (data: KreirajProdajuDto): Promise<void> => {
        // Debug outgoing DTO
        console.debug("Outgoing prodaja DTO:", data);

        try {
            const res = await fetch("/prodaja", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(data),
            });

            // Always read response body so we can log it on error
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
            alert("Prodaja uspešno kreirana ✔️");
        } catch (err: any) {
            console.error("Error submitting prodaja:", err);
            alert("Greška: " + (err?.message ?? "Nepoznata greška"));
        }
    };

    return (
        <div>
            {loadingArtikli ? (
                <p>Učitavanje artikala...</p>
            ) : (
                <CreateProdajaForm artikli={artikli} onSubmit={handleSubmit} />
            )}
        </div>
    );
}