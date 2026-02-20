export async function runDeichmannScraper(filters: Record<string, any>) {
    const PYTHON_API = import.meta.env.VITE_PYTHON_API_URL || "http://localhost:8000";
    const url = `${PYTHON_API.replace(/\/+$/, '')}/scrapers/deichmann`;

    const headers: Record<string, string> = { "Content-Type": "application/json" };
    const SCRAPER_KEY = import.meta.env.VITE_SCRAPER_API_KEY;
    if (SCRAPER_KEY) headers["X-API-Key"] = SCRAPER_KEY;

    const resp = await fetch(url, {
        method: "POST",
        headers,
        body: JSON.stringify(filters)
    });

    if (!resp.ok) {
        const text = await resp.text().catch(() => null);
        throw new Error(text ?? `HTTP ${resp.status}`);
    }

    return resp.json();
}
