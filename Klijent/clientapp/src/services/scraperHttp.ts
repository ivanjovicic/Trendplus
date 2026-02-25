function buildCandidates(scraperPath: string): string[] {
    const pythonApi = (import.meta.env.VITE_PYTHON_API_URL || "").replace(/\/+$/, "");
    const backendApi = (import.meta.env.VITE_API_BASE_URL || "").replace(/\/+$/, "");

    const candidates: string[] = [];
    if (pythonApi) candidates.push(`${pythonApi}/scrapers/${scraperPath}`);
    if (backendApi) candidates.push(`${backendApi}/api/scrapers/${scraperPath}`);
    candidates.push(`/api/scrapers/${scraperPath}`);

    // de-duplicate while preserving order
    return Array.from(new Set(candidates));
}

async function tryReadError(resp: Response): Promise<string | null> {
    try {
        const body = await resp.json();
        return body?.detail ?? body?.message ?? body?.error ?? body?.title ?? JSON.stringify(body);
    } catch {
        try {
            return await resp.text();
        } catch {
            return null;
        }
    }
}

export async function postScraperWithFallback(scraperPath: string, filters: Record<string, any>) {
    const headers: Record<string, string> = { "Content-Type": "application/json" };
    const scraperKey = import.meta.env.VITE_SCRAPER_API_KEY;
    if (scraperKey) headers["X-API-Key"] = scraperKey;

    const candidates = buildCandidates(scraperPath);
    const errors: string[] = [];

    for (const url of candidates) {
        try {
            const resp = await fetch(url, {
                method: "POST",
                headers,
                body: JSON.stringify(filters),
            });

            if (resp.ok) {
                const payload = await resp.json().catch(() => null);
                if (
                    payload &&
                    typeof payload === "object" &&
                    "status" in payload &&
                    String((payload as any).status).toLowerCase() === "error"
                ) {
                    const msg =
                        (payload as any)?.error ??
                        (payload as any)?.detail ??
                        (payload as any)?.message ??
                        "Unknown scraper error";
                    errors.push(`${url} -> API error: ${msg}`);
                    continue;
                }
                return payload;
            }

            const message = await tryReadError(resp);
            errors.push(`${url} -> HTTP ${resp.status}${message ? `: ${message}` : ""}`);
        } catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            errors.push(`${url} -> ${message}`);
        }
    }

    throw new Error(errors.join(" | "));
}
