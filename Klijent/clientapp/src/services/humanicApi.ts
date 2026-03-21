import { postScraperWithFallback } from "./scraperHttp";

export async function runHumanicScraper(filters: Record<string, any>, signal?: AbortSignal) {
    return postScraperWithFallback("humanic", filters, { signal });
}
