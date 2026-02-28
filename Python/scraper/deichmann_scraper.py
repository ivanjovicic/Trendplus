import asyncio
import logging
from datetime import datetime
from typing import Any, Dict, List, Optional
import random
from scraper.schema import ScrapedItem
from scraper.normalization import parse_price, infer_category_from_name, compute_rank

# Initialize logger
logger = logging.getLogger("scraper.deichmann")

# Import scraper.browser_manager as browser_manager
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

# Centralized slug resolver

def resolve_category_slug(country: str, category: str) -> str:
    code = country.strip().upper()
    default_cat = DEICHMANN_DEFAULT_CATEGORY_BY_COUNTRY.get(code, "schuhe-82")
    raw_cat = (category or "").strip() or default_cat

    if code in ("HU", "RO") and raw_cat in DEICHMANN_SLUG_TRANSLATIONS:
        return DEICHMANN_SLUG_TRANSLATIONS[raw_cat].get(code, default_cat)

    is_localized_slug = bool(raw_cat) and raw_cat.rsplit("-", 1)[-1].isdigit()
    if raw_cat not in DEICHMANN_SLUG_TRANSLATIONS and not is_localized_slug:
        return default_cat

    return raw_cat

# Adaptive scrolling
async def adaptive_scroll(page, max_scrolls=20, timeout=5):
    last_card_count = 0
    for _ in range(max_scrolls):
        await page.mouse.wheel(0, 2500)
        await page.wait_for_timeout(400)
        cards = await page.query_selector_all("[data-testid='product-tile']")
        if len(cards) == last_card_count:
            timeout -= 1
            if timeout <= 0:
                break
        else:
            last_card_count = len(cards)

# Deduplication

def deduplicate_results(results):
    seen = set()
    deduped = []
    for result in results:
        key = result.get("url") or result.get("sku")
        if key and key not in seen:
            seen.add(key)
            deduped.append(result)
    return deduped

# Anti-bot measures
async def apply_anti_bot_measures(page):
    await page.set_viewport_size({"width": random.randint(800, 1200), "height": random.randint(600, 900)})
    user_agent = f"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{random.randint(80, 100)}.0.0.0 Safari/537.36"
    await page.set_user_agent(user_agent)

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
        await adaptive_scroll(page)

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


def _to_scraped_item_deichmann(d: Dict[str, Any]) -> ScrapedItem:
    price, currency = parse_price(d.get("price"), market=d.get("country", "DE"))

    page = d.get("page") or 1
    pos = d.get("positionOnPage") or 1
    page_size = len(d.get("_page_cards", [])) if d.get("_page_cards") else 30

    return ScrapedItem(
        source="deichmann",
        market=d.get("country", "DE"),
        brand=d.get("brand") or "",
        name=d.get("name") or "",
        priceValue=price,
        currency=currency,
        url=d.get("url"),
        imageUrl=d.get("image"),
        rank=compute_rank(page, pos, page_size),
        page=page,
        positionOnPage=pos,
        sortMode=d.get("sort") or "popularity",
        sku=None,
        category=infer_category_from_name(d.get("name") or ""),
        gender=d.get("gender"),
        isNew=False,
        isOnSale=bool(d.get("old_price")),
        hasImage=bool(d.get("image")),
        backend=None,
        backendIndex=None,
        backendRank=None,
        raw=d,
    )

async def scrape_deichmann_filtered(**filters: Any) -> List[ScrapedItem]:
    raw_results = await _scrape_deichmann_pages(filters)
    items = [_to_scraped_item_deichmann(r) for r in raw_results]
    return items
