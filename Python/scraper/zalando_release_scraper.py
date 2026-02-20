from playwright.sync_api import sync_playwright
import time


def scrape_zalando_release_calendar(
    url: str,
    max_scrolls: int = 12,
    headless: bool = True
):
    print(f"🔍 Loading release calendar → {url}")

    results = []

    with sync_playwright() as pw:
        browser = pw.chromium.launch(headless=headless)
        ctx = browser.new_context(
            viewport={"width": 1400, "height": 900},
            locale="en-GB",
            extra_http_headers={"Accept-Language": "en-GB,en;q=0.9"}
        )

        page = ctx.new_page()
        page.goto(url, timeout=0)

        # Accept cookies if needed
        for txt in ["Accept all", "Allow all", "Accept"]:
            try:
                page.click(f"button:has-text('{txt}')", timeout=2000)
                break
            except:
                pass

        # Scroll for loading
        print("📜 Scrolling…")
        for _ in range(max_scrolls):
            page.mouse.wheel(0, 2500)
            time.sleep(0.7)

        # All product cards
        cards = page.query_selector_all("article.z5x6ht")
        print(f"✔ Found {len(cards)} product cards")

        for card in cards:
            # Extract basic info
            link_el = card.query_selector("a._LM")
            url = link_el.get_attribute("href") if link_el else None

            img_el = card.query_selector("img")
            img = img_el.get_attribute("src") if img_el else None
            name = img_el.get_attribute("alt") if img_el else None

            brand_el = card.query_selector("span.OBkCPz")
            brand = brand_el.inner_text().strip() if brand_el else None

            price_el = card.query_selector("p span")
            price = price_el.inner_text().strip() if price_el else None

            # 🔥 COMING SOON DETECTION
            # This block appears AFTER the <article> element:
            # <div class="hD5J5m"><span>5 February @ 9:00 am</span></div>
            coming_date = None
            coming_block = card.evaluate_handle(
                """(card) => card.nextElementSibling""")
            
            if coming_block:
                try:
                    date_el = coming_block.query_selector("span")
                    if date_el:
                        coming_date = date_el.inner_text().strip()
                except:
                    pass

            results.append({
                "name": name,
                "brand": brand,
                "price": price,
                "image": img,
                "url": url,
                "coming_soon": coming_date is not None,
                "release_date": coming_date
            })

        browser.close()

    # Sort: Coming Soon → regular
    results_sorted = sorted(
        results,
        key=lambda x: (not x["coming_soon"], x["name"] or "")
    )

    print(f"\n📊 Total collected: {len(results_sorted)}")
    print(f"🔥 Coming Soon detected: {sum(1 for r in results_sorted if r['coming_soon'])}")

    return results_sorted



if __name__ == "__main__":
    mens = "https://en.zalando.de/release-calendar/mens-shoes-sneakers/"
    womens = "https://en.zalando.de/release-calendar/womens-shoes-sneakers/"

    items = scrape_zalando_release_calendar(mens, headless=False)
    for i in items[:10]:
        print(i)
