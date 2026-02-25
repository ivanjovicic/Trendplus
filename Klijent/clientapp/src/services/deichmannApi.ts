import { postScraperWithFallback } from "./scraperHttp";

export async function runDeichmannScraper(filters: Record<string, any>) {
    return postScraperWithFallback("deichmann", filters);
}
