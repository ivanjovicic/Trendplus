import { postScraperWithFallback } from "./scraperHttp";

export async function runHumanicScraper(filters: Record<string, any>) {
    return postScraperWithFallback("humanic", filters);
}
