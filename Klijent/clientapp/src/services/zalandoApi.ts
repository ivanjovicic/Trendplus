import { postScraperWithFallback } from "./scraperHttp";

export async function runZalandoScraper(filters: Record<string, any>, signal?: AbortSignal) {
    return postScraperWithFallback("zalando", filters, { signal });
}
