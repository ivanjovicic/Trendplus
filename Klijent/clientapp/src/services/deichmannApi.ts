import { postScraperWithFallback } from "./scraperHttp";

export async function runDeichmannScraper(filters: Record<string, any>, signal?: AbortSignal) {
    return postScraperWithFallback("deichmann", filters, { signal });
}
