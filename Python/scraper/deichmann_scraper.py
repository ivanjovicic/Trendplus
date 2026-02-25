import asyncio
from typing import Any, Dict, List, Optional

import scraper.browser_manager as browser_manager


DEICHMANN_BRAND_MAP: Dict[str, str] = {
    "rieker": "rieker_1-94255",
}

# Locale prefix used in the URL path: deichmann.com/{locale}/c/...
DEICHMANN_LOCALE_BY_COUNTRY: Dict[str, str] = {
    "DE": "de-de",
    "AT": "at-at",
    "CH": "ch-de",
    "HU": "hu-hu",
    "RO": "ro-ro",
}

# Women / men segment per country
DEICHMANN_GENDER_BY_COUNTRY: Dict[str, Dict[str, str]] = {
    "DE": {"women": "damen",  "men": "herren"},
    "AT": {"women": "damen",  "men": "herren"},
    "CH": {"women": "damen",  "men": "herren"},
    "HU": {"women": "noi",    "men": "ferfi"},
    "RO": {"women": "femei",  "men": "barbati"},
}

# Default women-shoes category slug per country
DEICHMANN_DEFAULT_CATEGORY_BY_COUNTRY: Dict[str, str] = {
    "DE": "schuhe-82",
    "AT": "schuhe-82",
    "CH": "schuhe-82",
    "HU": "cipok-82",
    "RO": "incaltaminte-82",
}

# Known category slug translations for non-DE markets.
# Slugs not listed here fall back to DEICHMANN_DEFAULT_CATEGORY_BY_COUNTRY.
DEICHMANN_SLUG_TRANSLATIONS: Dict[str, Dict[str, str]] = {
    # heels — DE/AT/CH share the same slug; HU/RO use localised slugs
    "high-heels-131":         {"HU": "magassarku-cipok-127",  "RO": "pantofi-cu-toc-127"},
    "stiefel-187":            {"HU": "bakancsok-95",          "RO": "ghete-cizme-95"},
    "stiefeletten-182":       {"HU": "bokacsizmak-86",        "RO": "botine-86"},
    "sandalen-191":           {"HU": "szandalok-135",         "RO": "sandale-135"},
    "sneaker-143":            {"HU": "sneakerek-143",         "RO": "sneakers-143"},
    "ballerinas-schuhe-183":  {"HU": "balerina-cipok-94",     "RO": "balerini-94"},
    "hausschuhe-211":         {"HU": "hazicipok-114",         "RO": "papuci-de-casa-114"},
}

# Cookie-accept button text per country
DEICHMANN_COOKIE_TEXTS: List[str] = [
    # German (DE / AT / CH)
    "Alle akzeptieren", "Akzeptieren", "Einverstanden",
    # Hungarian
    "Összes elfogadása", "Elfogadom", "Elfogadom az összeset",
    # Romanian
    "Acceptați toate", "Accepta toate", "Acceptați", "Accepta",
    # English fallback
    "Accept all", "Accept",
]


def normalize_deichmann_brand(brand: str | None) -> str | None:
    if not brand:
        return None
    b = brand.strip()
    if not b:
        return None
    return DEICHMANN_BRAND_MAP.get(b.lower(), b)


def build_deichmann_url(
    gender: str | None = None,
    category: str | None = None,
    country: str | None = "DE",
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
    code = (country or "DE").strip().upper()
    locale = DEICHMANN_LOCALE_BY_COUNTRY.get(code, "de-de")
    gender_key = "women" if (gender or "").lower() in ("women", "damen", "noi", "femei", "") else "men"
    gender_segment = DEICHMANN_GENDER_BY_COUNTRY.get(code, DEICHMANN_GENDER_BY_COUNTRY["DE"])[gender_key]
    default_cat = DEICHMANN_DEFAULT_CATEGORY_BY_COUNTRY.get(code, "schuhe-82")

    raw_cat = (category or "").strip() or default_cat

    # Resolve per-country slug translations for known DE slugs.
    if code in ("HU", "RO") and raw_cat in DEICHMANN_SLUG_TRANSLATIONS:
        raw_cat = DEICHMANN_SLUG_TRANSLATIONS[raw_cat].get(code, default_cat)
    elif code in ("HU", "RO"):
        # Unknown DE slug — fall back to generic women shoes for the market.
        # Keep already-localized category slugs (typically ending with "-<id>").
        known_de_slugs = set(DEICHMANN_SLUG_TRANSLATIONS.keys()) | {"schuhe-82"}
        is_localized_slug = bool(raw_cat) and raw_cat.rsplit("-", 1)[-1].isdigit()
        if raw_cat in known_de_slugs:
            raw_cat = default_cat
        elif raw_cat not in {default_cat} and not is_localized_slug:
            raw_cat = default_cat

    # Normalise legacy aliases
    if raw_cat in ("sneakers", "sneaker"):
        raw_cat = "sneaker-143" if code not in ("HU", "RO") else DEICHMANN_SLUG_TRANSLATIONS.get("sneaker-143", {}).get(code, default_cat)

    # Strip any gender prefix the caller may have included
    if "/" in raw_cat:
        raw_cat = raw_cat.split("/", 1)[1]

    category_path = f"{gender_segment}/{raw_cat}"
    base = f"https://www.deichmann.com/{locale}/c/{category_path}"
    params: List[str] = []

    if sort:
        params.append(f"sort={sort}")
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

    return base + "?" + "&".join(params)


async def _scrape_deichmann_page(page_index: int, filters: Dict[str, Any]) -> List[Dict[str, Any]]:
    await browser_manager.init_browser()
    context = await browser_manager.get_context()

    url = build_deichmann_url(
        gender=filters.get("gender"),
        category=filters.get("category"),
        country=filters.get("country", "DE"),
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

    print(f"[Deichmann] Loading page {page_index}: {url}")
    page = await context.new_page()
    try:
        await page.goto(url, timeout=0)
        try:
            await page.wait_for_load_state("networkidle", timeout=12_000)
        except Exception as e:
            print(f"[DEBUG] Deichmann page {page_index} did not reach networkidle: {e}")

        current_url = page.url
        if "deichmann.com" not in current_url:
            print(f"[Deichmann] Redirected to {current_url} - skipping")
            return []

        # Cookie banner - covers DE/AT/CH, HU, RO and English fallback
        for txt in DEICHMANN_COOKIE_TEXTS:
            try:
                await page.click(f"button:has-text('{txt}')", timeout=1200)
                print("[Deichmann] Cookies accepted")
                break
            except Exception:
                pass

        # Scroll to trigger lazy-loaded cards
        for _ in range(10):
            await page.mouse.wheel(0, 2500)
            await page.wait_for_timeout(400)

        # Product card selectors (ordered by specificity, most-likely-current first)
        card_selectors = [
            "[data-testid='product-tile']",
            "article.product-tile",
            "li.product-tile article",
            "div.product-tile",
            "[class*='ProductTile']",
            "[class*='product-tile']",
            "div.card",                       # legacy
            "article[class*='card']",
        ]
        cards: List[Any] = []
        for sel in card_selectors:
            try:
                found = await page.query_selector_all(sel)
                if len(found) > len(cards):
                    cards = found
                if len(cards) >= 10:
                    break
            except Exception:
                pass

        print(f"[Deichmann] Found {len(cards)} cards on page {page_index}")

        async def _text(el: Any, selectors: List[str]) -> Optional[str]:
            for s in selectors:
                try:
                    node = await el.query_selector(s)
                    if node:
                        t = (await node.inner_text() or "").strip()
                        if t:
                            return t
                except Exception:
                    pass
            return None

        page_results: List[Dict[str, Any]] = []
        for card in cards:
            try:
                link_el = await card.query_selector("a")
                product_url = await link_el.get_attribute("href") if link_el else None
                if product_url and product_url.startswith("/"):
                    product_url = "https://www.deichmann.com" + product_url

                img_el = (await card.query_selector("img.desktop")) or (await card.query_selector("img"))
                image = await img_el.get_attribute("src") if img_el else None
                alt = await img_el.get_attribute("alt") if img_el else None

                brand_text = await _text(card, [
                    "[data-testid='product-brand']",
                    "div.brand-details h2.brandname",
                    "h2.brandname",
                    "span[class*='brand']",
                    "div[class*='brand']",
                ])

                name = await _text(card, [
                    "[data-testid='product-name']",
                    "div.brand-details h3.taglist",
                    "h3.taglist",
                    "h3",
                    "[class*='product-name']",
                    "[class*='productName']",
                ]) or alt

                price = await _text(card, [
                    "span[data-id='selling-price']",
                    "[data-testid='price-current']",
                    "[data-testid='price']",
                    "[class*='selling-price']",
                    "[class*='sellingPrice']",
                    "[class*='price-current']",
                    "span[class*='price']",
                ])

                old_price = await _text(card, [
                    "span[data-id='cross-price']",
                    "[data-testid='price-original']",
                    "[class*='cross-price']",
                    "[class*='crossPrice']",
                    "[class*='original-price']",
                ])

                if not name:
                    continue

                page_results.append({
                    "brand": (brand_text or "").strip() or None,
                    "name": name.strip(),
                    "price": price.strip() if price else None,
                    "old_price": old_price.strip() if old_price else None,
                    "image": image,
                    "url": product_url,
                    "source": "deichmann",
                })
            except Exception as e:
                print(f"[DEBUG] Deichmann parse error: {e}")
                continue

        print(f"[DEBUG] Deichmann page {page_index}: {len(page_results)} products.")
        return page_results
    finally:
        try:
            await page.close()
        except Exception:
            pass


async def scrape_deichmann_filtered(**filters: Any) -> List[Dict[str, Any]]:
    max_pages = int(filters.get("pages", 1) or 1)
    if max_pages < 1:
        max_pages = 1

    print(
        "[Deichmann] Start "
        f"country={filters.get('country', 'DE')} / "
        f"category={filters.get('category')} / "
        f"brand={filters.get('brand')} / "
        f"gender={filters.get('gender')} / "
        f"sort={filters.get('sort')} / "
        f"pages={max_pages}"
    )

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

    print(f"[Deichmann] Total scraped: {len(results)}")
    return results
