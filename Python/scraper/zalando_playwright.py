import asyncio
import os
import re
from typing import Any, Dict, List, Optional
from urllib.parse import quote_plus, urljoin

import scraper.browser_manager as browser_manager
from scraper.schema import ScrapedItem
from scraper.normalization import parse_price, infer_category_from_name, compute_rank

ZALANDO_BASE_BY_COUNTRY: Dict[str, str] = {
    "DE": "https://www.zalando.de",
    "AT": "https://www.zalando.at",
    "CH": "https://www.zalando.ch",
    "HU": "https://www.zalando.hu",
    "RO": "https://www.zalando.ro",
}

ZALANDO_LISTING_PATH_BY_COUNTRY: Dict[str, str] = {
    "DE": "/katalog/",
    "AT": "/katalog/",
    "CH": "/katalog/",
    "HU": "/katalogus/",
    "RO": "/catalog/",
}

_PRICE_PATTERNS = [
    re.compile(
        r"(?:€|eur|ft|huf|ron|lei|chf)\s*\d{1,3}(?:[.\s]\d{3})*(?:[.,]\d{2})?",
        flags=re.IGNORECASE,
    ),
    re.compile(
        r"\d{1,3}(?:[.\s]\d{3})*(?:[.,]\d{2})?\s*(?:€|eur|ft|huf|ron|lei|chf)",
        flags=re.IGNORECASE,
    ),
]


def _resolve_zalando_base_domain(country: Optional[str]) -> str:
    code = (country or "DE").strip().upper()
    return ZALANDO_BASE_BY_COUNTRY.get(code, ZALANDO_BASE_BY_COUNTRY["DE"])


def _resolve_listing_path(country: Optional[str]) -> str:
    code = (country or "DE").strip().upper()
    return ZALANDO_LISTING_PATH_BY_COUNTRY.get(code, "/catalog/")


def _localize_category_query(category: Optional[str], country: Optional[str]) -> str:
    code = (country or "DE").strip().upper()
    raw = (category or "").strip().lower()
    if not raw:
        raw = "schuhe"

    normalized = raw.replace("_", " ").replace("-", " ")

    if normalized in {"all", "catalog", "schuhe", "shoe", "shoes"}:
        if code == "HU":
            return "cipo"
        if code == "RO":
            return "pantofi"
        return "schuhe"

    if normalized in {"sneaker", "sneakers"}:
        if code == "HU":
            return "sportcipo"
        return "sneaker"

    if normalized in {"boots", "boot", "stiefel"}:
        if code == "HU":
            return "csizma"
        if code == "RO":
            return "cizme"
        return "stiefel"

    if normalized in {"sandals", "sandal", "sandale"}:
        if code == "HU":
            return "szandal"
        if code == "RO":
            return "sandale"
        return "sandale"

    if normalized in {"heels", "heel", "pumps"}:
        if code == "HU":
            return "magassarku"
        if code == "RO":
            return "tocuri"
        return "pumps"

    if normalized in {"loafers", "loafer", "mokassin", "mokassins"}:
        if code == "HU":
            return "mokaszin"
        if code == "RO":
            return "loafer"
        return "loafer"  # DE / AT / CH

    if normalized in {"flats", "flat", "ballerina", "ballerinas"}:
        if code == "HU":
            return "balerina"
        if code == "RO":
            return "balerina"
        return "ballerina"

    if normalized in {"ankle boots", "ankle boot", "ankle_boots", "stiefelette", "stiefeletten"}:
        if code == "HU":
            return "bokacipo"
        if code == "RO":
            return "botine"
        return "stiefelette"

    if normalized in {"chelsea", "chelsea boot", "chelsea boots"}:
        if code == "HU":
            return "chelsea"
        if code == "RO":
            return "chelsea"
        return "chelsea"

    if normalized in {"knee boots", "knee_boots", "high boots", "kniehoh"}:
        if code == "HU":
            return "terdcizma"
        if code == "RO":
            return "cizme inalte"
        return "kniehohe stiefel"

    if normalized in {"stilettos", "stiletto"}:
        if code == "HU":
            return "stiletto"
        if code == "RO":
            return "stiletto"
        return "stiletto"

    if normalized in {"wedges", "wedge", "keilabsatz"}:
        if code == "HU":
            return "telitalpas"
        if code == "RO":
            return "platforma"
        return "keilabsatz"

    if normalized in {"mules", "mule", "pantolette", "pantoletten"}:
        if code == "HU":
            return "papucs"
        if code == "RO":
            return "papuci"
        return "pantolette"

    if normalized in {"slippers", "slipper", "hausschuh"}:
        if code == "HU":
            return "papucs"
        if code == "RO":
            return "papuci"
        return "hausschuh"

    if normalized in {"espadrilles", "espadrille"}:
        if code == "HU":
            return "espadrille"
        if code == "RO":
            return "espadrile"
        return "espadrille"

    if normalized in {"oxfords", "oxford", "derby", "derbies"}:
        if code == "HU":
            return "oxford"
        if code == "RO":
            return "oxford"
        return "oxford"

    if normalized in {"running", "run", "laufschuh"}:
        if code == "HU":
            return "futocipo"
        if code == "RO":
            return "alergare"
        return "laufschuh"

    return normalized


def _normalize_search_query(category: Optional[str], brand: Optional[str], country: Optional[str]) -> str:
    tokens: List[str] = []

    cat = _localize_category_query(category, country)
    if cat:
        tokens.append(cat)

    if brand:
        # If brand comes as "rieker_1-94255", keep only human part.
        b = brand.split("_", 1)[0].strip().replace("-", " ")
        if b:
            tokens.insert(0, b)

    if not tokens:
        tokens = ["schuhe"]

    return " ".join(tokens)


def build_zalando_url(
    category: str = "sneaker",
    brand: Optional[str] = None,
    gender: Optional[str] = None,
    country: Optional[str] = "DE",
    sort: Optional[str] = "popularity",
    priceMin: Optional[int] = None,
    priceMax: Optional[int] = None,
    activationDate: Optional[str] = None,
) -> str:
    """
    Build Zalando listing URL.
    We intentionally use /catalog/ + q=... because it is locale-safe across markets
    (DE/AT/CH/HU/RO) unlike category path slugs that can be localized.
    """
    base_domain = _resolve_zalando_base_domain(country)
    listing_path = _resolve_listing_path(country)
    params: List[str] = []

    query_text = _normalize_search_query(category, brand, country)
    params.append(f"q={quote_plus(query_text)}")

    if priceMin is not None:
        params.append(f"priceMin={int(priceMin)}")
    if priceMax is not None:
        params.append(f"priceMax={int(priceMax)}")
    if activationDate:
        params.append(f"activation_date={quote_plus(str(activationDate))}")
    if sort:
        params.append(f"order={quote_plus(str(sort))}")

    # Zalando uses "p" as page parameter on catalog pages.
    return f"{base_domain}{listing_path}?{'&'.join(params)}&p="


def _extract_price_from_text(text: Optional[str]) -> Optional[str]:
    if not text:
        return None
    compact = text.replace("\u00a0", " ")
    for pattern in _PRICE_PATTERNS:
        match = pattern.search(compact)
        if match:
            return re.sub(r"\s+", " ", match.group(0)).strip()
    return None


async def _get_first_text(card: Any, selectors: List[str]) -> Optional[str]:
    for selector in selectors:
        try:
            el = await card.query_selector(selector)
            if not el:
                continue
            value = (await el.inner_text() or "").strip()
            if value:
                return value
        except Exception:
            continue
    return None


async def _extract_image(card: Any) -> Optional[str]:
    try:
        img_el = await card.query_selector("img")
        if not img_el:
            return None

        src = await img_el.get_attribute("src")
        if src:
            return src

        data_src = await img_el.get_attribute("data-src")
        if data_src:
            return data_src

        srcset = await img_el.get_attribute("srcset")
        if srcset:
            first = srcset.split(",")[0].strip()
            if first:
                return first.split(" ")[0].strip()
    except Exception:
        return None
    return None


async def _parse_product_card(card: Any, base_domain: str) -> Optional[Dict[str, Any]]:
    try:
        link_el = (
            await card.query_selector("a[href*='.html']")
            or await card.query_selector("a._LM")
            or await card.query_selector("a[href]")
        )
        product_url = await link_el.get_attribute("href") if link_el else None
        if product_url:
            product_url = urljoin(base_domain, product_url)

        brand = await _get_first_text(
            card,
            [
                "span[data-testid='product-brand']",
                "[data-testid='product-card-brand']",
                "h3 span:first-child",
            ],
        )

        title = await _get_first_text(
            card,
            [
                "[data-testid='product-name']",
                "[data-testid='product-card-name']",
                "h3 span:nth-child(2)",
                "h3",
            ],
        )

        if not title:
            # Fallback to image alt if card header is missing.
            try:
                img_el = await card.query_selector("img")
                title = (await img_el.get_attribute("alt")).strip() if img_el else None
            except Exception:
                title = None

        if not title:
            return None

        price = await _get_first_text(
            card,
            [
                "p[data-testid='price']",
                "span[data-testid='price']",
                "p[class*='price']",
                "span[class*='price']",
                "div[class*='price'] p",
                "div[class*='price'] span",
            ],
        )

        if not price:
            try:
                card_text = await card.inner_text()
                price = _extract_price_from_text(card_text)
            except Exception:
                price = None

        image_url = await _extract_image(card)

        return {
            "name": title,
            "brand": brand,
            "price": price,
            "image_url": image_url,
            "url": product_url,
            "source": "zalando",
        }
    except Exception:
        return None


async def _collect_product_cards(page: Any) -> List[Any]:
    selectors = [
        "article[data-testid='product-card']",
        "article[data-testid^='product-card']",
        "article:has(a[href*='.html'])",
        "li:has(article a[href*='.html']) article",
    ]

    best: List[Any] = []
    for selector in selectors:
        try:
            found = await page.query_selector_all(selector)
        except Exception:
            found = []

        if len(found) > len(best):
            best = found
        if len(best) >= 20:
            break

    return best


async def _extract_from_links_fallback(page: Any, base_domain: str) -> List[Dict[str, Any]]:
    """
    Fallback parser for cases where Zalando changes product-card markup.
    """
    results: List[Dict[str, Any]] = []
    seen_urls: set[str] = set()

    try:
        links = await page.query_selector_all("a[href*='.html']")
    except Exception:
        links = []

    for link in links:
        try:
            href = await link.get_attribute("href")
            if not href:
                continue
            product_url = urljoin(base_domain, href)
            if product_url in seen_urls:
                continue

            container_handle = await link.evaluate_handle("el => el.closest('article, li, div')")
            container = container_handle.as_element()

            if container:
                text_blob = await container.inner_text()
                image_url = await _extract_image(container)
                brand = await _get_first_text(
                    container,
                    [
                        "span[data-testid='product-brand']",
                        "[data-testid='product-card-brand']",
                        "h3 span:first-child",
                    ],
                )
                name = await _get_first_text(
                    container,
                    [
                        "[data-testid='product-name']",
                        "[data-testid='product-card-name']",
                        "h3 span:nth-child(2)",
                        "h3",
                    ],
                )
            else:
                text_blob = await link.inner_text()
                image_url = None
                brand = None
                name = (await link.inner_text() or "").strip()

            if not name:
                continue

            price = _extract_price_from_text(text_blob)

            results.append(
                {
                    "name": name.strip(),
                    "brand": (brand or "").strip() or None,
                    "price": price,
                    "image_url": image_url,
                    "url": product_url,
                    "source": "zalando",
                }
            )
            seen_urls.add(product_url)
        except Exception:
            continue

    return results


async def _scrape_zalando_page(
    page_num: int,
    category: str,
    brand: Optional[str],
    gender: Optional[str],
    country: Optional[str],
    sort: Optional[str],
    priceMin: Optional[int],
    priceMax: Optional[int],
    activationDate: Optional[str],
) -> List[Dict[str, Any]]:
    base_url = build_zalando_url(
        category=category,
        brand=brand,
        gender=gender,
        country=country,
        sort=sort,
        priceMin=priceMin,
        priceMax=priceMax,
        activationDate=activationDate,
    )
    base_domain = _resolve_zalando_base_domain(country)

    page = await browser_manager.new_page()
    try:
        url = base_url + str(page_num)
        print(f"[Zalando] Loading page {page_num}: {url}")
        await page.goto(url, timeout=0)
        await page.wait_for_timeout(2500)

        # Cookie banner — covers DE/AT/CH (German), HU (Hungarian), RO (Romanian), EN fallback
        for txt in ["Only essential", "Accept all", "Accept", "Allow all", "Alle akzeptieren",
                    "Elfogadom az összeset", "Elfogad", "Mindent elfogad",
                    "Acceptați toate", "Accepta toate", "Acceptați", "Accepta"]:
            try:
                await page.click(f"button:has-text('{txt}')", timeout=800)
                print(f"[Zalando] Accepted cookies: {txt}")
                break
            except Exception:
                continue

        # Scroll enough for lazy content.
        for _ in range(8):
            await page.mouse.wheel(0, 2200)
            await page.wait_for_timeout(550)

        cards = await _collect_product_cards(page)
        print(f"[Zalando] Page {page_num} cards: {len(cards)}")

        page_results: List[Dict[str, Any]] = []
        seen_urls: set[str] = set()

        for card in cards:
            item = await _parse_product_card(card, base_domain)
            if not item:
                continue
            url_key = item.get("url")
            if url_key and url_key in seen_urls:
                continue
            if url_key:
                seen_urls.add(url_key)
            page_results.append(item)

        if not page_results:
            fallback_items = await _extract_from_links_fallback(page, base_domain)
            for item in fallback_items:
                url_key = item.get("url")
                if url_key and url_key in seen_urls:
                    continue
                if url_key:
                    seen_urls.add(url_key)
                page_results.append(item)
            print(f"[Zalando] Page {page_num} fallback items: {len(fallback_items)}")

        items_with_price = sum(1 for item in page_results if item.get("price"))
        print(f"[Zalando] Page {page_num}: {items_with_price}/{len(page_results)} items have prices")

        debug_path = os.path.join(os.path.dirname(__file__), f"zalando_debug_{page_num}.html")
        try:
            with open(debug_path, "w", encoding="utf-8") as f:
                f.write(await page.content())
        except Exception:
            pass

        return page_results
    finally:
        await browser_manager.release_page(page)


def _extract_zalando_sku(url: str) -> Optional[str]:
    import re
    match = re.search(r"-([a-z0-9]+-[a-z0-9]+)\.html", url)
    return match.group(1) if match else None


def _to_scraped_item_zalando(d: Dict[str, Any]) -> ScrapedItem:
    price, currency = parse_price(d.get("price"), market=d.get("country", "DE"))

    page = d.get("page") or 1
    pos = d.get("positionOnPage") or 1
    page_size = 72  # Zalando often has 72 items per page

    return ScrapedItem(
        source="zalando",
        market=d.get("country", "DE"),
        brand=d.get("brand") or "",
        name=d.get("name") or "",
        priceValue=price,
        currency=currency,
        url=d.get("url"),
        imageUrl=d.get("image_url"),
        rank=compute_rank(page, pos, page_size),
        page=page,
        positionOnPage=pos,
        sortMode=d.get("sort") or "popularity",
        sku=_extract_zalando_sku(d.get("url")),
        category=infer_category_from_name(d.get("name") or ""),
        gender=d.get("gender"),
        isNew=False,
        isOnSale="%" in (d.get("price") or "") or "statt" in (d.get("price") or "").lower(),
        hasImage=bool(d.get("image_url")),
        backend=None,
        backendIndex=None,
        backendRank=None,
        raw=d,
    )

async def scrape_zalando_playwright(
    max_pages: int = 1,
    category: str = "sneaker",
    brand: Optional[str] = None,
    gender: Optional[str] = None,
    country: Optional[str] = "DE",
    sort: Optional[str] = "popularity",
    priceMin: Optional[int] = None,
    priceMax: Optional[int] = None,
    activationDate: Optional[str] = None,
) -> List[ScrapedItem]:
    max_pages = int(max_pages or 1)
    if max_pages < 1:
        max_pages = 1

    print(
        "[Zalando] Start "
        f"category={category} brand={brand} gender={gender} country={country} pages={max_pages}"
    )

    tasks = [
        _scrape_zalando_page(
            page_num,
            category,
            brand,
            gender,
            country,
            sort,
            priceMin,
            priceMax,
            activationDate,
        )
        for page_num in range(1, max_pages + 1)
    ]

    results: List[Dict[str, Any]] = []
    page_results_list = await asyncio.gather(*tasks, return_exceptions=True)

    for page_num, page_results in enumerate(page_results_list, start=1):
        if isinstance(page_results, Exception):
            print(f"[Zalando] Page {page_num} failed: {page_results}")
            continue
        results.extend(page_results)

    print(f"[Zalando] Total scraped: {len(results)}")
    return [_to_scraped_item_zalando(r) for r in results]
