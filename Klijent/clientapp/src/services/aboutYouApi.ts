import { postScraperWithFallback } from "./scraperHttp";

export async function runAboutYouScraper(filters: Record<string, any>) {
    return postScraperWithFallback("aboutyou", filters);
}
