import asyncio
from typing import Any, Dict, List

import scraper.browser_manager as browser_manager


DEICHMANN_BRAND_MAP: Dict[str, str] = {
    # Human brand -> Deichmann brand filter value
    # Example discovered from Deichmann UI:
    # https://www.deichmann.com/de-de/c/damen/schuhe-82?...&brand=rieker_1-94255
    "rieker": "rieker_1-94255",
}


def normalize_deichmann_brand(brand: str | None) -> str | None:
    """
    Deichmann expects a specific brand filter value (e.g. "rieker_1-94255"),
    but our API/UI often sends plain brand names ("rieker").
    This helper maps known brands and falls back to the original value.
    """
    if not brand:
        return None
    b = brand.strip()
    if not b:
        return None
    return DEICHMANN_BRAND_MAP.get(b.lower(), b)


def build_deichmann_url(
    gender: str | None = None,
    category: str = "schuhe-82",
    sort: str | None = None,
    priceMin: int | None = None,
    priceMax: int | None = None,
    sale: bool | None = None,
    isNew: bool | None = None,
    size: str | None = None,
    brand: str | None = None,
    isLeather: str | None = None,
    waterResistance: str | None = None,
    page: int = 1,
) -> str:
    """
    Gradi Deichmann listing URL na osnovu filtera.

    - gender: "women" / "men" (fallback: women → damen, ostalo → herren)
    - category: npr. "schuhe-82" ili već puna putanja "damen/schuhe-82"
    """

    # Mapiranje kategorija ako dođe "sneakers"/"sneaker"
    if category in ("sneakers", "sneaker"):
        category = "sneaker-143"

    # Ako nema '/', tretiramo kao čist category slug pa dodamo gender segment
    if "/" not in category:
        gender_segment = "damen" if (gender or "").lower() in ("women", "damen", "") else "herren"
        category_path = f"{gender_segment}/{category}"
    else:
        category_path = category

    base = f"https://www.deichmann.com/de-de/c/{category_path}"
    params: List[str] = []

    if sort:
        params.append(f"sort={sort}")

    # Prices (range)
    if priceMin is not None or priceMax is not None:
        low = priceMin or 0
        high = priceMax if priceMax is not None else ""
        params.append(f"prices={low}~{high}")

    if sale:
        params.append("sale=true")

    if isNew:
        params.append("isNew=true")

    if size:
        params.append(f"sizeEu={size}")

    brand_param = normalize_deichmann_brand(brand)
    if brand_param:
        params.append(f"brand={brand_param}")

    if isLeather:
        params.append(f"isLeather={isLeather}")

    if waterResistance:
        params.append(f"waterResistanceLevel={waterResistance}")

    params.append(f"page={page}")

    if params:
        return base + "?" + "&".join(params)
    return base


async def _scrape_deichmann_page(page_index: int, filters: Dict[str, Any]) -> List[Dict[str, Any]]:
    """
    Scrape jedne Deichmann stranice, koristi thread-local context.
    """
    await browser_manager.init_browser()
    context = await browser_manager.get_context()

    url = build_deichmann_url(
        gender=filters.get("gender"),
        category=filters.get("category", "schuhe-82"),
        sort=filters.get("sort"),
        priceMin=filters.get("priceMin"),
        priceMax=filters.get("priceMax"),
        sale=filters.get("sale"),
        isNew=filters.get("isNew"),
        size=filters.get("size"),
        brand=filters.get("brand"),
        isLeather=filters.get("isLeather"),
        waterResistance=filters.get("waterResistance"),
        page=page_index,
    )

    print(f"\n▶ [Deichmann] Loading page {page_index}: {url}")
    page = await context.new_page()
    try:
        await page.goto(url, timeout=0)

        # Pričekaj bar neki load, ali nemoj da blokiraš zauvek
        try:
            await page.wait_for_load_state("networkidle", timeout=12_000)
        except Exception as e:
            print(f"[DEBUG] Deichmann page {page_index} did not reach networkidle: {e}")

        # Proveri da li je Deichmann izvršio redirect (npr. na zalando.de kad nema rezultata)
        current_url = page.url
        if "deichmann.com" not in current_url:
            print(f"⚠️ [Deichmann] Page {page_index} redirected to {current_url} - skipping")
            return []

        # Accept cookies
        for txt in ["Alle akzeptieren", "Akzeptieren", "Einverstanden"]:
            try:
                await page.click(f"button:has-text('{txt}')", timeout=1500)
                print(f"[DEBUG] Deichmann accepted cookies with button: {txt}")
                break
            except Exception:
                pass

        # Scroll da se učitaju svi proizvodi
        print("[Deichmann] Scrolling…")
        for _ in range(10):
            await page.mouse.wheel(0, 2500)
            await page.wait_for_timeout(400)

        cards = await page.query_selector_all("div.card")
        print(f"✔ [Deichmann] Found {len(cards)} products on page {page_index}")

        page_results: List[Dict[str, Any]] = []

        for card in cards:
            try:
                link_el = await card.query_selector("a")
                product_url = await link_el.get_attribute("href") if link_el else None
                if product_url and product_url.startswith("/"):
                    product_url = "https://www.deichmann.com" + product_url

                img_el = await card.query_selector("img.desktop") or await card.query_selector("img")
                image = await img_el.get_attribute("src") if img_el else None
                alt = await img_el.get_attribute("alt") if img_el else None

                brand_el = await card.query_selector("div.brand-details h2.brandname")
                brand_text = await brand_el.inner_text() if brand_el else None
                brand_text = brand_text.strip() if brand_text else None

                name_el = await card.query_selector("div.brand-details h3.taglist")
                name = await name_el.inner_text() if name_el else alt
                name = name.strip() if name else None

                price_el = await card.query_selector("span[data-id='selling-price']")
                price = await price_el.inner_text() if price_el else None
                price = price.strip() if price else None

                old_price_el = await card.query_selector("span[data-id='cross-price']")
                old_price = await old_price_el.inner_text() if old_price_el else None
                old_price = old_price.strip() if old_price else None

                page_results.append(
                    {
                        "brand": brand_text,
                        "name": name,
                        "price": price,
                        "old_price": old_price,
                        "image": image,
                        "url": product_url,
                        "source": "deichmann",
                    }
                )
            except Exception as e:
                print(f"[DEBUG] Deichmann product parse error on page {page_index}: {e}")
                continue

        print(f"[DEBUG] Deichmann page {page_index} done, found {len(page_results)} products.")
        return page_results
    finally:
        try:
            await page.close()
        except Exception as e:
            print(f"[DEBUG] Deichmann page {page_index} close error: {e}")


async def scrape_deichmann_filtered(**filters: Any) -> List[Dict[str, Any]]:
    """
    Playwright scraper koji prihvata fleksibilne Deichmann filtere i vraća listu dictova.

    Podržani ključevi: gender, category, sort, priceMin, priceMax, sale, isNew,
    size, brand, isLeather, waterResistance, pages
    """

    max_pages = int(filters.get("pages", 1) or 1)
    if max_pages < 1:
        max_pages = 1

    print(
        "🔍 Deichmann Playwright → "
        f"category={filters.get('category')} / "
        f"brand={filters.get('brand')} / "
        f"gender={filters.get('gender')} / "
        f"sort={filters.get('sort')} / "
        f"priceMin={filters.get('priceMin')} / "
        f"priceMax={filters.get('priceMax')} / "
        f"sale={filters.get('sale')} / "
        f"isNew={filters.get('isNew')} / "
        f"pages={max_pages}"
    )
    print(f"[Deichmann] Filters: {filters}")

    results: List[Dict[str, Any]] = []

    # Async version using asyncio.gather instead of ThreadPoolExecutor
    tasks = [
        _scrape_deichmann_page(page_idx, filters)
        for page_idx in range(1, max_pages + 1)
    ]

    page_results_list = await asyncio.gather(*tasks, return_exceptions=True)
    
    for page_idx, page_results in enumerate(page_results_list, start=1):
        if isinstance(page_results, Exception):
            print(f"Deichmann page scrape failed (page={page_idx}): {page_results}")
        else:
            results.extend(page_results)

    print(f"\n📊 [Deichmann] Total scraped: {len(results)}")
    return results
