import asyncio
import logging
import os
import re
from typing import Any, Dict, Iterable, List, Optional
from urllib.parse import parse_qsl, urlencode, urljoin, urlparse, urlunparse

import requests
from bs4 import BeautifulSoup


DEFAULT_ABOUTYOU_URL = "https://www.aboutyou.de/c/frauen/schuhe/stiefeletten-20276"
DEFAULT_HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/122.0.0.0 Safari/537.36"
    ),
    "Accept-Language": "de-DE,de;q=0.9,en;q=0.8",
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


def _is_aboutyou_brand_slug(value: str) -> bool:
    # Examples: "dr-martens-729", "marc-o-polo-596"
    return bool(re.fullmatch(r"[a-z0-9-]+-\d+", value.strip().lower()))


def _to_aboutyou_price_value(value: float | int) -> int:
    """
    About You 'prices' query format is usually integer cents range (e.g. 1290-16400).
    Heuristic:
    - >= 1000 integer -> assume already cents
    - otherwise treat as EUR and convert to cents
    """
    fv = float(value)
    if fv >= 1000 and float(int(fv)) == fv:
        return int(fv)
    return int(round(fv * 100))


def _normalize_aboutyou_url(
    url: Optional[str],
    gender: Optional[str],
    category: Optional[str],
    sort: Optional[str],
    brand: Optional[str],
    price_min: Optional[float],
    price_max: Optional[float],
) -> str:
    if url and url.strip():
        normalized = url.strip()
        if normalized.startswith("/"):
            normalized = f"https://www.aboutyou.de{normalized}"
        elif not normalized.startswith("http://") and not normalized.startswith("https://"):
            normalized = f"https://{normalized}"
    elif category and category.strip():
        c = category.strip().strip("/")
        if c.startswith("http://") or c.startswith("https://"):
            normalized = c
        elif c.startswith("c/"):
            normalized = f"https://www.aboutyou.de/{c}"
        elif c.startswith("/c/"):
            normalized = f"https://www.aboutyou.de{c}"
        elif "/" in c:
            normalized = f"https://www.aboutyou.de/c/{c}"
        else:
            g = (gender or "women").strip().lower()
            gender_segment = "herren" if g in {"men", "herren"} else "frauen"
            normalized = f"https://www.aboutyou.de/c/{gender_segment}/schuhe/{c}"
    else:
        normalized = DEFAULT_ABOUTYOU_URL

    # About You often normalizes unsupported params, but sort is accepted.
    parsed = urlparse(normalized)
    query = dict(parse_qsl(parsed.query, keep_blank_values=True))
    if sort and sort.strip():
        query["sort"] = sort.strip()

    # If brand is provided as About You slugs, forward directly as query param.
    # Example: brand=marc-o-polo-596,dr-martens-729
    brand_tokens = _split_csv(brand)
    if brand_tokens and all(_is_aboutyou_brand_slug(token) for token in brand_tokens):
        query["brand"] = ",".join(brand_tokens)

    # About You price range query param format: prices=min-max (usually in cents)
    if price_min is not None or price_max is not None:
        low = _to_aboutyou_price_value(price_min) if price_min is not None else ""
        high = _to_aboutyou_price_value(price_max) if price_max is not None else ""
        query["prices"] = f"{low}-{high}"

    normalized = urlunparse(parsed._replace(query=urlencode(query)))
    return normalized


def _build_page_url(base_url: str, page_num: int) -> str:
    if page_num <= 1:
        return base_url

    parsed = urlparse(base_url)
    query = dict(parse_qsl(parsed.query, keep_blank_values=True))
    query["page"] = str(page_num)
    return urlunparse(parsed._replace(query=urlencode(query)))


def _parse_price_to_float(value: Optional[str]) -> Optional[float]:
    if not value:
        return None
    txt = value.strip()
    txt = txt.replace("\u00a0", " ")
    txt = re.sub(r"[^0-9,.\-]", "", txt)
    if not txt:
        return None
    txt = txt.replace(".", "").replace(",", ".")
    try:
        return float(txt)
    except Exception:
        return None


def _to_lower_tokens(value: Optional[str]) -> set[str]:
    if not value:
        return set()
    normalized = re.sub(r"[^a-z0-9 ]+", " ", value.lower())
    return {token for token in normalized.split() if token}


def _normalize_for_match(value: Optional[str]) -> str:
    if not value:
        return ""
    normalized = re.sub(r"[^a-z0-9 ]+", " ", value.lower())
    normalized = re.sub(r"\s+", " ", normalized)
    return normalized.strip()


def _normalize_brand_filter_token(token: str) -> str:
    token = token.strip().lower()
    # Convert About You slug-id token to plain brand words:
    # dr-martens-729 -> dr martens
    if _is_aboutyou_brand_slug(token):
        token = token.rsplit("-", 1)[0]
        token = token.replace("-", " ")
    return _normalize_for_match(token)


def _pick_image_src(tile: Any) -> Optional[str]:
    image = tile.select_one('img[data-testid="productImageView"]') or tile.select_one("img")
    if not image:
        return None

    src = image.get("src")
    if src:
        return src

    srcset = image.get("srcset")
    if not srcset:
        return None

    # srcset format: "url 120w, url2 360w, ..."
    first = srcset.split(",")[0].strip()
    if not first:
        return None
    return first.split(" ")[0].strip()


def _tile_to_item(tile: Any) -> Optional[Dict[str, Any]]:
    brand_el = tile.select_one('[data-testid="brandName"]')
    name_el = tile.select_one('[data-testid="productName"]')
    price_el = tile.select_one('[data-testid="finalPrice"]')

    brand = brand_el.get_text(" ", strip=True) if brand_el else ""
    name = name_el.get_text(" ", strip=True) if name_el else ""
    price = price_el.get_text(" ", strip=True) if price_el else ""

    if not name:
        return None

    product_link = tile.select_one('a[data-testid^="productTile-"][href]') or tile.select_one("a[href]")
    href = product_link.get("href").strip() if product_link and product_link.get("href") else ""
    product_url = urljoin("https://www.aboutyou.de", href) if href else None

    item = {
        "brand": brand or None,
        "name": name,
        "price": price or None,
        "old_price": None,
        "image": _pick_image_src(tile),
        "url": product_url,
        "source": "aboutyou",
    }
    return item


def _apply_filters(
    items: Iterable[Dict[str, Any]],
    brand: Optional[str],
    keyword: Optional[str],
    price_min: Optional[float],
    price_max: Optional[float],
) -> List[Dict[str, Any]]:
    filtered: List[Dict[str, Any]] = []

    brand_filters = []
    if brand:
        brand_filters = [_normalize_brand_filter_token(b) for b in brand.split(",") if b.strip()]

    keyword_tokens = _to_lower_tokens(keyword)

    for item in items:
        item_brand = _normalize_for_match(item.get("brand"))
        item_name = item.get("name") or ""
        item_text = f"{item_brand} {item_name}".lower()
        item_price = _parse_price_to_float(item.get("price"))

        if brand_filters:
            if not any(b in item_brand for b in brand_filters):
                continue

        if keyword_tokens:
            if not keyword_tokens.issubset(_to_lower_tokens(item_text)):
                continue

        if price_min is not None and (item_price is None or item_price < price_min):
            continue
        if price_max is not None and (item_price is None or item_price > price_max):
            continue

        filtered.append(item)

    return filtered


def _sort_items(items: List[Dict[str, Any]], sort: Optional[str]) -> List[Dict[str, Any]]:
    mode = (sort or "").strip().lower()
    if mode in {"price-asc", "price_asc"}:
        return sorted(items, key=lambda i: (_parse_price_to_float(i.get("price")) is None, _parse_price_to_float(i.get("price")) or 0.0))
    if mode in {"price-desc", "price_desc"}:
        return sorted(items, key=lambda i: _parse_price_to_float(i.get("price")) or 0.0, reverse=True)
    if mode in {"name-asc", "name_asc"}:
        return sorted(items, key=lambda i: (i.get("name") or "").lower())
    if mode in {"name-desc", "name_desc"}:
        return sorted(items, key=lambda i: (i.get("name") or "").lower(), reverse=True)
    return items


async def _accept_aboutyou_cookies(page: Any) -> None:
    for selector in [
        "button:has-text('Alle akzeptieren')",
        "button:has-text('Accept all')",
        "button:has-text('Only essential')",
        "button:has-text('Nur notwendige')",
        "button:has-text('Akzeptieren')",
    ]:
        try:
            await page.click(selector, timeout=1200)
            await page.wait_for_timeout(350)
            return
        except Exception:
            continue


async def _scrape_aboutyou_infinite_scroll(
    *,
    base_url: str,
    max_pages: int,
    auto_pages: bool,
) -> List[Dict[str, Any]]:
    try:
        from playwright.async_api import async_playwright
    except Exception as ex:
        raise RuntimeError("Playwright is not available for About You infinite scroll") from ex

    all_items: List[Dict[str, Any]] = []
    seen_keys: set[str] = set()

    # About You loads roughly 30 product tiles per scroll batch on this listing.
    target_unique = max_pages * 30
    max_scroll_rounds = 80 if auto_pages else max_pages * 8

    async with async_playwright() as pw:
        browser = await pw.chromium.launch(headless=_env_flag("PLAYWRIGHT_HEADLESS", True))
        context = await browser.new_context(
            viewport={"width": 1400, "height": 900},
            locale="de-DE",
            extra_http_headers={"Accept-Language": "de-DE,de;q=0.9,en;q=0.8"},
        )
        page = await context.new_page()
        try:
            await page.goto(base_url, timeout=60000, wait_until="domcontentloaded")
            await _accept_aboutyou_cookies(page)
            try:
                await page.wait_for_selector('li[data-testid^="productTileTracker-"]', timeout=15000)
            except Exception:
                pass

            idle_rounds = 0
            for round_num in range(1, max_scroll_rounds + 1):
                html = await page.content()
                soup = BeautifulSoup(html, "lxml")
                tiles = soup.select('li[data-testid^="productTileTracker-"]')

                before_count = len(all_items)
                for tile in tiles:
                    item = _tile_to_item(tile)
                    if not item:
                        continue
                    key = item.get("url") or f"{item.get('brand')}|{item.get('name')}"
                    if key in seen_keys:
                        continue
                    seen_keys.add(key)
                    all_items.append(item)

                new_unique = len(all_items) - before_count
                logging.info(
                    "AboutYou infinite round %s -> domTiles=%s newUnique=%s totalUnique=%s",
                    round_num,
                    len(tiles),
                    new_unique,
                    len(all_items),
                )

                if not auto_pages and len(all_items) >= target_unique:
                    break

                prev_height = await page.evaluate("document.body.scrollHeight")
                await page.evaluate("window.scrollTo(0, document.body.scrollHeight)")
                await page.mouse.wheel(0, 2600)
                await page.wait_for_timeout(1300)
                curr_height = await page.evaluate("document.body.scrollHeight")

                if new_unique == 0 and curr_height <= (prev_height + 5):
                    idle_rounds += 1
                else:
                    idle_rounds = 0

                if auto_pages and idle_rounds >= 5:
                    break
                if not auto_pages and idle_rounds >= 4 and len(all_items) >= 30:
                    break
        finally:
            await page.close()
            await context.close()
            await browser.close()

    return all_items


def scrape_aboutyou_filtered_sync(
    url: Optional[str] = None,
    gender: Optional[str] = "women",
    category: Optional[str] = "frauen/schuhe/stiefeletten-20276",
    sort: Optional[str] = "popularity",
    priceMin: Optional[float] = None,
    priceMax: Optional[float] = None,
    brand: Optional[str] = None,
    keyword: Optional[str] = None,
    pages: int = 1,
) -> List[Dict[str, Any]]:
    requested_pages = int(pages or 0)
    auto_pages = requested_pages <= 0
    max_pages = 10 if auto_pages else requested_pages

    base_url = _normalize_aboutyou_url(
        url=url,
        gender=gender,
        category=category,
        sort=sort,
        brand=brand,
        price_min=priceMin,
        price_max=priceMax,
    )
    logging.info(
        "AboutYou scraper -> url=%s brand=%s keyword=%s priceMin=%s priceMax=%s pages=%s mode=%s sort=%s",
        base_url,
        brand,
        keyword,
        priceMin,
        priceMax,
        max_pages,
        "auto" if auto_pages else "manual",
        sort,
    )

    seen_keys: set[str] = set()
    all_items: List[Dict[str, Any]] = []

    with requests.Session() as session:
        session.headers.update(DEFAULT_HEADERS)

        for page_num in range(1, max_pages + 1):
            page_url = _build_page_url(base_url, page_num)
            try:
                resp = session.get(page_url, timeout=25)
                resp.raise_for_status()
            except Exception as ex:
                logging.warning("AboutYou request failed for %s: %s", page_url, ex)
                if page_num == 1:
                    raise
                break

            soup = BeautifulSoup(resp.text, "lxml")
            tiles = soup.select('li[data-testid^="productTileTracker-"]')
            logging.info("AboutYou page %s (%s) -> %s tiles", page_num, page_url, len(tiles))

            if not tiles:
                if page_num == 1:
                    logging.warning("AboutYou returned no tiles for first page")
                break

            new_items = 0
            for tile in tiles:
                item = _tile_to_item(tile)
                if not item:
                    continue
                key = item.get("url") or f"{item.get('brand')}|{item.get('name')}"
                if key in seen_keys:
                    continue
                seen_keys.add(key)
                all_items.append(item)
                new_items += 1

            # AboutYou often canonicalizes unsupported page params; if no new items, stop.
            if page_num > 1 and new_items == 0:
                break

    filtered = _apply_filters(
        items=all_items,
        brand=brand,
        keyword=keyword,
        price_min=priceMin,
        price_max=priceMax,
    )
    filtered = _sort_items(filtered, sort)

    logging.info("AboutYou scraper done: raw=%s filtered=%s", len(all_items), len(filtered))
    return filtered


async def scrape_aboutyou_filtered(**filters: Any) -> List[Dict[str, Any]]:
    url = filters.get("url")
    gender = filters.get("gender", "women")
    category = filters.get("category", "frauen/schuhe/stiefeletten-20276")
    sort = filters.get("sort", "popularity")
    price_min = filters.get("priceMin")
    price_max = filters.get("priceMax")
    brand = filters.get("brand")
    keyword = filters.get("keyword")
    pages = filters.get("pages", 1)

    requested_pages = int(pages or 0)
    auto_pages = requested_pages <= 0
    max_pages = 10 if auto_pages else requested_pages

    base_url = _normalize_aboutyou_url(
        url=url,
        gender=gender,
        category=category,
        sort=sort,
        brand=brand,
        price_min=price_min,
        price_max=price_max,
    )
    logging.info(
        "AboutYou scraper -> url=%s brand=%s keyword=%s priceMin=%s priceMax=%s pages=%s mode=%s sort=%s",
        base_url,
        brand,
        keyword,
        price_min,
        price_max,
        max_pages,
        "auto" if auto_pages else "manual",
        sort,
    )

    # For one page request, requests+BeautifulSoup is faster.
    if not auto_pages and max_pages <= 1:
        return await asyncio.to_thread(
            scrape_aboutyou_filtered_sync,
            url=url,
            gender=gender,
            category=category,
            sort=sort,
            priceMin=price_min,
            priceMax=price_max,
            brand=brand,
            keyword=keyword,
            pages=max_pages,
        )

    try:
        all_items = await _scrape_aboutyou_infinite_scroll(
            base_url=base_url,
            max_pages=max_pages,
            auto_pages=auto_pages,
        )
    except Exception as ex:
        logging.exception("AboutYou infinite scroll scrape failed, falling back to requests pagination: %s", ex)
        return await asyncio.to_thread(
            scrape_aboutyou_filtered_sync,
            url=url,
            gender=gender,
            category=category,
            sort=sort,
            priceMin=price_min,
            priceMax=price_max,
            brand=brand,
            keyword=keyword,
            pages=max_pages,
        )

    filtered = _apply_filters(
        items=all_items,
        brand=brand,
        keyword=keyword,
        price_min=price_min,
        price_max=price_max,
    )
    filtered = _sort_items(filtered, sort)
    logging.info("AboutYou infinite scraper done: raw=%s filtered=%s", len(all_items), len(filtered))
    return filtered
