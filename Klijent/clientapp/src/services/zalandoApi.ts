import { postScraperWithFallback } from "./scraperHttp";

export async function runZalandoScraper(filters: Record<string, any>) {
    return postScraperWithFallback("zalando", filters);
}
