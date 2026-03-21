import { postScraperWithFallback } from "./scraperHttp";

export async function runAboutYouScraper(filters: Record<string, any>, signal?: AbortSignal) {
    return postScraperWithFallback("aboutyou", filters, { signal });
}
