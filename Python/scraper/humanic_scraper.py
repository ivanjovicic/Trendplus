import logging
import os
import re
from typing import Any, Dict, Iterable, List, Optional
from urllib.parse import parse_qsl, urlencode, urljoin, urlparse, urlunparse
import random
from datetime import datetime
from playwright.async_api import async_playwright

from bs4 import BeautifulSoup
from scraper.schema import ScrapedItem
from scraper.normalization import parse_price, infer_category_from_name, compute_rank


DEFAULT_HUMANIC_URL = "https://www.humanic.net/at/c/Damenschuhe/womenShoes"
ITEMS_PER_PAGE_ESTIMATE = 30

SORT_BY_MAP: Dict[str, str] = {
    "bestseller": "live_hum_products_at_sold_items",
    "bestseler": "live_hum_products_at_sold_items",
    "popularity": "live_hum_products_at_sold_items",
    "popular": "live_hum_products_at_sold_items",
    "relevance": "live_hum_products_at",
    "default": "live_hum_products_at",
    "new": "live_hum_products_at_online_date",
    "newest": "live_hum_products_at_online_date",
    "price-asc": "live_hum_products_at_price_asc",
    "price_asc": "live_hum_products_at_price_asc",
    "price-desc": "live_hum_products_at_price_desc",
    "price_desc": "live_hum_products_at_price_desc",
}


def _env_flag(name: str, default: bool) -> bool:
    val = os.environ.get(name)
    if val is None:
        return default
    return val.strip().lower() in {"1", "true", "yes", "y", "on"}


def _split_csv(value: Optional[str]) -> List[str]:
    if not value:
        return []
    return [part.strip() for part in value.split(",") if part and part.strip()]


def _normalize_sort_value(sort: Optional[str]) -> str:
    raw = (sort or "").strip()
    if not raw:
        return SORT_BY_MAP["bestseller"]

    lowered = raw.lower()
    if lowered in SORT_BY_MAP:
        return SORT_BY_MAP[lowered]

    if lowered.startswith("live_hum_products_at"):
        return raw

    return SORT_BY_MAP["bestseller"]


def _normalize_humanic_url(
    *,
    url: Optional[str],
    category: Optional[str],
    sort: Optional[str],
    upper_materials: Optional[List[str]],
    brand: Optional[str],
) -> str:
    if url and url.strip():
        normalized = url.strip()
        if normalized.startswith("/"):
            normalized = f"https://www.humanic.net{normalized}"
        elif not normalized.startswith("http://") and not normalized.startswith("https://"):
            normalized = f"https://{normalized}"
    elif category and category.strip():
        c = category.strip().strip("/")
        if c.startswith("http://") or c.startswith("https://"):
            normalized = c
        elif c.startswith("at/c/"):
            normalized = f"https://www.humanic.net/{c}"
        elif c.startswith("c/"):
            normalized = f"https://www.humanic.net/at/{c}"
        else:
            normalized = f"https://www.humanic.net/at/c/{c}"
    else:
        normalized = DEFAULT_HUMANIC_URL

    parsed = urlparse(normalized)
    query_pairs = parse_qsl(parsed.query, keep_blank_values=True)

    cleaned_pairs: List[tuple[str, str]] = []
    for key, value in query_pairs:
        if key == "sortBy":
            continue
        if key.startswith("refinementList[upperMaterials]"):
            continue
        if key.startswith("refinementList[brand]"):
            continue
        cleaned_pairs.append((key, value))

    cleaned_pairs.append(("sortBy", _normalize_sort_value(sort)))

    materials = upper_materials or []
    for idx, material in enumerate(materials):
        cleaned_pairs.append((f"refinementList[upperMaterials][{idx}]", material))

    brand_filters = _split_csv(brand)
    for idx, brand_name in enumerate(brand_filters):
        cleaned_pairs.append((f"refinementList[brand][{idx}]", brand_name))

    return urlunparse(parsed._replace(query=urlencode(cleaned_pairs, doseq=True)))


def _parse_price_to_float(value: Optional[str]) -> Optional[float]:
    if not value:
        return None
    txt = value.replace("\u00a0", " ").strip()
    txt = re.sub(r"[^0-9,.\-]", "", txt)
    if not txt:
        return None
    txt = txt.replace(".", "").replace(",", ".")
    try:
        return float(txt)
    except Exception:
        return None


def _normalize_for_match(value: Optional[str]) -> str:
    if not value:
        return ""
    text = value.lower()
    text = re.sub(r"[^a-z0-9 ]+", " ", text)
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def _to_tokens(value: Optional[str]) -> set[str]:
    normalized = _normalize_for_match(value)
    if not normalized:
        return set()
    return {token for token in normalized.split() if token}


def _pick_image_src(tile: Any) -> Optional[str]:
    img = tile.select_one("figure.productcell__image img") or tile.select_one("img")
    if not img:
        return None

    src = img.get("src")
    if src:
        return src

    data_src = img.get("data-src")
    if data_src:
        return data_src

    srcset = img.get("srcset")
    if not srcset:
        return None

    first = srcset.split(",")[0].strip()
    if not first:
        return None
    return first.split(" ")[0].strip()


def _tile_to_item(tile: Any) -> Optional[Dict[str, Any]]:
    brand_el = tile.select_one(".productcell__brand")
    name_el = tile.select_one(".productcell__name")
    price_el = tile.select_one(".productcell__price")
    old_price_el = tile.select_one(".productcell__price--old")

    name = name_el.get_text(" ", strip=True) if name_el else ""
    if not name:
        return None

    brand = brand_el.get_text(" ", strip=True) if brand_el else ""
    price = price_el.get_text(" ", strip=True) if price_el else None
    old_price = old_price_el.get_text(" ", strip=True) if old_price_el else None

    link = tile.select_one("a.productcell__image-link[href]") or tile.select_one('a[href*="/at/p/"]')
    href = link.get("href").strip() if link and link.get("href") else ""
    product_url = urljoin("https://www.humanic.net", href) if href else None

    return {
        "brand": brand or None,
        "name": name,
        "price": price,
        "old_price": old_price,
        "image": _pick_image_src(tile),
        "url": product_url,
        "source": "humanic",
    }


def _apply_local_filters(
    *,
    items: Iterable[Dict[str, Any]],
    brand: Optional[str],
    keyword: Optional[str],
    price_min: Optional[float],
    price_max: Optional[float],
) -> List[Dict[str, Any]]:
    result: List[Dict[str, Any]] = []
    brand_filters = [_normalize_for_match(b) for b in _split_csv(brand)]
    keyword_tokens = _to_tokens(keyword)

    for item in items:
        item_brand = _normalize_for_match(item.get("brand"))
        item_name = item.get("name") or ""
        item_text = f"{item_brand} {_normalize_for_match(item_name)}"
        item_price = _parse_price_to_float(item.get("price"))

        if brand_filters and not any(b and b in item_brand for b in brand_filters):
            continue

        if keyword_tokens:
            item_tokens = _to_tokens(item_text)
            if not keyword_tokens.issubset(item_tokens):
                continue

        if price_min is not None and (item_price is None or item_price < price_min):
            continue
        if price_max is not None and (item_price is None or item_price > price_max):
            continue

        result.append(item)

    return result


def _sort_items(items: List[Dict[str, Any]], sort: Optional[str]) -> List[Dict[str, Any]]:
    sort_value = _normalize_sort_value(sort).lower()
    if sort_value.endswith("price_asc"):
        return sorted(
            items,
            key=lambda i: (
                _parse_price_to_float(i.get("price")) is None,
                _parse_price_to_float(i.get("price")) or 0.0,
            ),
        )
    if sort_value.endswith("price_desc"):
        return sorted(items, key=lambda i: _parse_price_to_float(i.get("price")) or 0.0, reverse=True)
    if sort_value in {"name-asc", "name_asc"}:
        return sorted(items, key=lambda i: (i.get("name") or "").lower())
    if sort_value in {"name-desc", "name_desc"}:
        return sorted(items, key=lambda i: (i.get("name") or "").lower(), reverse=True)
    return items


async def _accept_humanic_cookies(page: Any) -> None:
    for selector in [
        "button:has-text('Alle akzeptieren')",
        "button:has-text('Akzeptieren')",
        "button:has-text('Accept all')",
        "button:has-text('Accept')",
    ]:
        try:
            await page.click(selector, timeout=1200)
            await page.wait_for_timeout(300)
            return
        except Exception:
            continue


async def _scrape_humanic_infinite_scroll(
    *,
    base_url: str,
    max_pages: int,
    auto_pages: bool,
) -> List[Dict[str, Any]]:
    try:
        from playwright.async_api import async_playwright
    except Exception as ex:
        raise RuntimeError("Playwright is not available for Humanic scraper") from ex

    seen_keys: set[str] = set()
    all_items: List[Dict[str, Any]] = []

    target_unique = max_pages * ITEMS_PER_PAGE_ESTIMATE
    max_scroll_rounds = 100 if auto_pages else max(12, max_pages * 8)

    async with async_playwright() as pw:
        browser = await pw.chromium.launch(headless=_env_flag("PLAYWRIGHT_HEADLESS", True))
        context = await browser.new_context(
            viewport={"width": 1400, "height": 900},
            locale="de-AT",
            extra_http_headers={"Accept-Language": "de-AT,de;q=0.9,en;q=0.8"},
        )
        page = await context.new_page()
        try:
            await page.goto(base_url, timeout=60000, wait_until="domcontentloaded")
            await _accept_humanic_cookies(page)

            try:
                await page.wait_for_selector("li.productcell", timeout=20000)
            except Exception:
                pass

            idle_rounds = 0
            start_time = datetime.utcnow()

            while True:
                tiles = await page.query_selector_all("li.productcell")
                before_count = len(all_items)

                for tile in tiles:
                    item = await self._tile_to_item(tile)
                    if not item:
                        continue
                    key = item.get("url") or f"{item.get('brand')}|{item.get('name')}|{item.get('price')}"
                    if key in seen_keys:
                        continue
                    seen_keys.add(key)
                    all_items.append(item)

                new_unique = len(all_items) - before_count
                if new_unique == 0:
                    idle_rounds += 1
                else:
                    idle_rounds = 0

                elapsed_time = (datetime.utcnow() - start_time).total_seconds()
                if idle_rounds >= 3 or elapsed_time > 45:
                    break

                await page.mouse.wheel(0, 3000)
                await page.wait_for_timeout(random.randint(200, 600))

            return all_items
        finally:
            await page.close()
            await context.close()
            await browser.close()

    return all_items


def _to_scraped_item_humanic(d: Dict[str, Any]) -> ScrapedItem:
    price, currency = parse_price(d.get("price"), market=d.get("country", "AT"))

    page = d.get("page") or 1
    pos = d.get("positionOnPage") or 1
    page_size = len(d.get("_page_cards", [])) if d.get("_page_cards") else 30

    return ScrapedItem(
        source="humanic",
        market=d.get("country", "AT"),
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
        backend="algolia",
        backendIndex=d.get("backendIndex"),
        backendRank=d.get("backendRank"),
        raw=d,
    )


async def scrape_humanic_filtered(**filters: Any) -> List[ScrapedItem]:
    raw_results = await _scrape_humanic_infinite_scroll(filters)
    items = [_to_scraped_item_humanic(r) for r in raw_results]
    return items
