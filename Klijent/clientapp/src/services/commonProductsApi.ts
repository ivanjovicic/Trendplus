export async function fetchCommonProducts(filters: Record<string, any>) {
    // Prefer the dedicated Python scraper service if configured (VITE_PYTHON_API_URL)
    const PYTHON_API = import.meta.env.VITE_PYTHON_API_URL || "";
    const BACKEND_API = import.meta.env.VITE_API_BASE_URL || "";

    // If a Python scraper service is configured, call it directly at /scrapers/common
    // otherwise fall back to backend proxy at /api/scrapers/common
    const base = PYTHON_API || BACKEND_API;
    const path = PYTHON_API ? "/scrapers/common" : "/api/scrapers/common";
    const url = `${base}${path}`;

    const headers: Record<string, string> = { "Content-Type": "application/json" };
    const SCRAPER_KEY = import.meta.env.VITE_SCRAPER_API_KEY;
    if (SCRAPER_KEY) headers["X-API-Key"] = SCRAPER_KEY;

    const resp = await fetch(url, {
        method: "POST",
        headers,
        body: JSON.stringify(filters),
    });

    if (!resp.ok) {
        // Try to extract helpful error message from JSON ProblemDetails or similar
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
