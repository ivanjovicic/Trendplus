import os
import asyncio
from typing import Any, Dict, List
from urllib.parse import quote_plus

import scraper.browser_manager as browser_manager

# jednostavna mapa brend → slug (po potrebi proširi)
BRAND_MAP: Dict[str, str] = {
    "rieker": "ri10",
    "adidas": "ad1",
    "nike": "ni1",
    "puma": "pu1",
    "tamaris": "ta1",
    "converse": "co1",
    "guess": "gu1",
    "skechers": "sk1",
    "asics": "as1",
}

BRAND_SLUG_MAP = BRAND_MAP


def build_zalando_url(
    category: str = "sneaker",
    brand: str | None = None,
    gender: str | None = None,
    sort: str | None = "popularity",
    priceMin: int | None = None,
    priceMax: int | None = None,
    activationDate: str | None = None,
) -> str:
    """
    Gradi Zalando listing bazni URL (bez page broja na kraju).
    """

    # Normalize category
    if category == "sneakers":
        category = "sneaker"

    base_path = f"https://www.zalando.de/{category}/"
    params: List[str] = []

    # Brand: use `q=` search query (works even when we don't know Zalando's internal brand codes).
    if brand:
        # If user passed a Deichmann brand id like "rieker_1-94255", keep only the human prefix.
        brand_q = brand.split("_", 1)[0]
        params.append(f"q={quote_plus(brand_q)}")

    if priceMin is not None:
        params.append(f"priceMin={priceMin}")
    if priceMax is not None:
        params.append(f"priceMax={priceMax}")
    if activationDate:
        params.append(f"activation_date={activationDate}")

    # sort trenutno često default, ali ostavljamo hook
    if sort:
        params.append(f"order={sort}")

    query = "&".join(params)
    if query:
        return f"{base_path}?{query}&page="
    return f"{base_path}?page="


async def _scrape_zalando_page(
    page_num: int,
    category: str,
    brand: str | None,
    gender: str | None,
    sort: str | None,
    priceMin: int | None,
    priceMax: int | None,
    activationDate: str | None,
) -> List[Dict[str, Any]]:
    """
    Scrape jedne Zalando stranice (async). Koristi shared context.
    """
    context = await browser_manager.get_context()

    base_url = build_zalando_url(
        category=category,
        brand=brand,
        gender=gender,
        sort=sort,
        priceMin=priceMin,
        priceMax=priceMax,
        activationDate=activationDate,
    )

    page = await context.new_page()
    try:
        url = base_url + str(page_num)
        print(f"\n▶ [Zalando] Loading page {page_num}: {url}")
        await page.goto(url, timeout=0)
        await page.wait_for_timeout(3000)  # Increased wait for dynamic content

        # Cookie banner
        for txt in ["Only essential", "Accept all", "Accept", "Allow all", "Alle akzeptieren"]:
            try:
                await page.click(f"button:has-text('{txt}')", timeout=1000)
                print(f"✔ [Zalando] Accepted cookies: {txt}")
                break
            except Exception:
                pass

        print("[Zalando] Scrolling…")
        for _ in range(10):
            await page.mouse.wheel(0, 2000)
            await page.wait_for_timeout(700)

        # product cards – class pattern iz debug-a
        cards = await page.query_selector_all('article[class*="z5x6ht"]')
        print(f"✔ [Zalando] Found {len(cards)} articles on page {page_num}")

        page_results: List[Dict[str, Any]] = []

        for idx, card in enumerate(cards):
            try:
                link_el = await card.query_selector("a._LM") or await card.query_selector("a")
                product_url = await link_el.get_attribute("href") if link_el else None
                if product_url and product_url.startswith("/"):
                    product_url = "https://www.zalando.de" + product_url

                img_el = await card.query_selector("img")
                img = await img_el.get_attribute("src") if img_el else None
                alt_name = await img_el.get_attribute("alt") if img_el else None

                brand_el = await card.query_selector("span[data-testid='product-brand']")
                if not brand_el:
                    brand_el = await card.query_selector("h3 span:first-child")
                brand_val = await brand_el.inner_text() if brand_el else None
                brand_val = brand_val.strip() if brand_val else None

                # Prefer the visible product title from the card header (usually the 2nd span in <h3>),
                # because img alt is often generic or a descriptive sentence and matches poorly.
                title_val = None
                try:
                    h3_spans = await card.query_selector_all("h3 span")
                    if h3_spans and len(h3_spans) >= 2:
                        title_val = await h3_spans[1].inner_text()
                        title_val = title_val.strip() if title_val else None
                except Exception:
                    title_val = None

                name = title_val or alt_name

                # ===== IMPROVED PRICE EXTRACTION =====
                price = None
                price_el = None

                # Try multiple price selectors in order of specificity
                price_selectors = [
                    # Modern Zalando price selectors (2024-2025)
                    "p[class*='_0Qm8W1']",  # Current main price class
                    "p[class*='DgCKYF']",   # Alternative price class
                    "span[class*='sDq_FX']", # Sale price class
                    "p[data-testid='price']",
                    # Generic fallbacks
                    "p[class*='price']",
                    "span[class*='price']",
                    "div[class*='price'] p",
                    "div[class*='price'] span",
                    # Very broad fallback - find any element with € symbol
                    "p:has-text('€')",
                    "span:has-text('€')",
                ]

                for selector in price_selectors:
                    try:
                        price_el = await card.query_selector(selector)
                        if price_el:
                            price_text = await price_el.inner_text()
                            if price_text and price_text.strip() and "€" in price_text:
                                price = price_text.strip()
                                break
                    except Exception:
                        continue

                # Last resort: Extract price from entire card text using regex
                if not price:
                    try:
                        import re
                        card_text = await card.inner_text()
                        # Match patterns like "65,99 €" or "€65,99" or "65.99 €"
                        price_pattern = r"(?:€\s*)?(\d+[.,]\d{2})\s*€?"
                        match = re.search(price_pattern, card_text)
                        if match:
                            price = match.group(0).strip()
                            if "€" not in price:
                                price = price + " €"
                    except Exception:
                        pass

                # Debug logging for missing prices
                if not price and idx < 3:  # Log first 3 items
                    print(f"⚠️ [Zalando] No price found for product #{idx+1}: {name}")
                    # Try to get all text content for debugging
                    try:
                        all_text = await card.inner_text()
                        if "€" in all_text:
                            print(f"   → Found € symbol in card text: {all_text[:200]}")
                    except Exception:
                        pass

                page_results.append(
                    {
                        "name": name,
                        "brand": brand_val,
                        "price": price,
                        "image_url": img,
                        "url": product_url,
                        "source": "zalando",
                    }
                )
            except Exception as e:
                print(f"[DEBUG] Zalando product parse error on page {page_num}, card #{idx}: {e}")
                continue

        # Count items with prices for debugging
        items_with_price = sum(1 for item in page_results if item.get("price"))
        print(f"📊 [Zalando] Page {page_num}: {items_with_price}/{len(page_results)} items have prices")

        # Sačuvaj HTML za debug (po potrebi)
        debug_path = os.path.join(os.path.dirname(__file__), f"zalando_debug_{page_num}.html")
        try:
            with open(debug_path, "w", encoding="utf-8") as f:
                f.write(await page.content())
            print(f"[DEBUG] Saved HTML → {debug_path}")
        except Exception:
            pass

        return page_results
    finally:
        try:
            await page.close()
        except Exception as e:
            print(f"[DEBUG] Zalando page {page_num} close error: {e}")


async def scrape_zalando_playwright(
    max_pages: int = 1,
    category: str = "sneaker",
    brand: str | None = None,
    gender: str | None = None,
    sort: str | None = "popularity",
    priceMin: int | None = None,
    priceMax: int | None = None,
    activationDate: str | None = None,
) -> List[Dict[str, Any]]:
    """
    Glavni Zalando scraper – paralelno skida više stranica u jednoj Chromium instanci.
    """
    max_pages = int(max_pages or 1)
    if max_pages < 1:
        max_pages = 1

    print(
        f"🔍 Zalando Playwright → {category} / "
        f"brand={brand} / gender={gender} activationDate={activationDate} / pages={max_pages}"
    )

    results: List[Dict[str, Any]] = []

    # Async version using asyncio.gather instead of ThreadPoolExecutor
    tasks = [
        _scrape_zalando_page(
            page_num,
            category,
            brand,
            gender,
            sort,
            priceMin,
            priceMax,
            activationDate,
        )
        for page_num in range(1, max_pages + 1)
    ]

    page_results_list = await asyncio.gather(*tasks, return_exceptions=True)
    
    for page_num, page_results in enumerate(page_results_list, start=1):
        if isinstance(page_results, Exception):
            print(f"Zalando page scrape failed (page={page_num}): {page_results}")
        else:
            results.extend(page_results)

    print(f"\n📊 [Zalando] Total scraped: {len(results)}")
    return results
