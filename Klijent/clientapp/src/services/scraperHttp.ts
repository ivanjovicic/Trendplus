type PostScraperOptions = {
    signal?: AbortSignal;
    timeoutMs?: number;
    requestId?: string;
};

type CombinedSignalContext = {
    signal: AbortSignal;
    cleanup: () => void;
};

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

function combineSignalsWithTimeout(signal: AbortSignal | undefined, timeoutMs: number): CombinedSignalContext {
    const timeoutController = new AbortController();
    const timeoutId = window.setTimeout(() => {
        timeoutController.abort(new DOMException(`Scraper request timed out after ${timeoutMs}ms`, "TimeoutError"));
    }, timeoutMs);

    if (!signal) {
        return {
            signal: timeoutController.signal,
            cleanup: () => window.clearTimeout(timeoutId),
        };
    }

    if (signal.aborted) {
        timeoutController.abort(signal.reason);
    }

    const mergedController = new AbortController();

    const onExternalAbort = () =>
        mergedController.abort(signal.reason ?? new DOMException("Scraper request aborted", "AbortError"));
    const onTimeoutAbort = () =>
        mergedController.abort(timeoutController.signal.reason ?? new DOMException("Scraper request timed out", "TimeoutError"));

    signal.addEventListener("abort", onExternalAbort, { once: true });
    timeoutController.signal.addEventListener("abort", onTimeoutAbort, { once: true });

    return {
        signal: mergedController.signal,
        cleanup: () => {
            window.clearTimeout(timeoutId);
            signal.removeEventListener("abort", onExternalAbort);
            timeoutController.signal.removeEventListener("abort", onTimeoutAbort);
        },
    };
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

export async function postScraperWithFallback(
    scraperPath: string,
    filters: Record<string, any>,
    options: PostScraperOptions = {}
) {
    const requestId = options.requestId ?? crypto.randomUUID();
    const timeoutMs = options.timeoutMs ?? Number(import.meta.env.VITE_SCRAPER_TIMEOUT_MS || 120000);
    const { signal, cleanup } = combineSignalsWithTimeout(options.signal, timeoutMs);

    const headers: Record<string, string> = {
        "Content-Type": "application/json",
        "X-Request-ID": requestId,
    };
    const scraperKey = import.meta.env.VITE_SCRAPER_API_KEY;
    if (scraperKey) headers["X-API-Key"] = scraperKey;

    const candidates = buildCandidates(scraperPath);
    const errors: string[] = [];

    try {
        for (const url of candidates) {
            try {
                const resp = await fetch(url, {
                    method: "POST",
                    headers,
                    body: JSON.stringify(filters),
                    signal,
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
                if (err instanceof DOMException && err.name === "AbortError") {
                    throw new Error(`Scraper request aborted [requestId=${requestId}]`);
                }
                const message = err instanceof Error ? err.message : String(err);
                errors.push(`${url} -> ${message}`);
            }
        }

        throw new Error(`${errors.join(" | ")} [requestId=${requestId}]`);
    } finally {
        cleanup();
    }
}
