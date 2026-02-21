export async function runHumanicScraper(filters: Record<string, any>) {
    // Prefer direct Python scraper service if configured.
    const PYTHON_API = import.meta.env.VITE_PYTHON_API_URL || "";
    const BACKEND_API = import.meta.env.VITE_API_BASE_URL || "";

    const base = PYTHON_API || BACKEND_API;
    const path = PYTHON_API ? "/scrapers/humanic" : "/api/scrapers/humanic";
    const url = `${base.replace(/\/+$/, "")}${path}`;

    const headers: Record<string, string> = { "Content-Type": "application/json" };
    const SCRAPER_KEY = import.meta.env.VITE_SCRAPER_API_KEY;
    if (SCRAPER_KEY) headers["X-API-Key"] = SCRAPER_KEY;

    const resp = await fetch(url, {
        method: "POST",
        headers,
        body: JSON.stringify(filters),
    });

    if (!resp.ok) {
        let message: string | null = null;
        try {
            const body = await resp.json();
            message = body?.detail ?? body?.message ?? body?.error ?? body?.title ?? JSON.stringify(body);
        } catch {
            try {
                message = await resp.text();
            } catch {
                message = null;
            }
        }
        throw new Error(message ?? `HTTP ${resp.status}`);
    }

    return resp.json();
}
