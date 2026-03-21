import asyncio
import logging
import os
import re
from typing import Any, Dict, Iterable, List, Optional
from urllib.parse import parse_qsl, urlencode, urljoin, urlparse, urlunparse

import requests
from bs4 import BeautifulSoup
from scraper.schema import ScrapedItem
from scraper.normalization import parse_price, infer_category_from_name, compute_rank


DEFAULT_ABOUTYOU_URL = "https://www.aboutyou.de/c/frauen/schuhe/stiefeletten-20276"
DEFAULT_ABOUTYOU_BASE_BY_COUNTRY = {
    "DE": "https://www.aboutyou.de",
    "AT": "https://www.aboutyou.at",
    "CH": "https://www.aboutyou.ch",
    "HU": "https://www.aboutyou.hu",
    "RO": "https://www.aboutyou.ro",
}
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


def _extract_base_domain(url: Optional[str]) -> Optional[str]:
    if not url:
        return None
    parsed = urlparse(url)
    if parsed.scheme and parsed.netloc:
        return f"{parsed.scheme}://{parsed.netloc}"
    return None


def _resolve_aboutyou_base_domain(url: Optional[str], country: Optional[str]) -> str:
    base_from_url = _extract_base_domain(url)
    if base_from_url:
        return base_from_url
    code = (country or "DE").strip().upper()
    return DEFAULT_ABOUTYOU_BASE_BY_COUNTRY.get(code, DEFAULT_ABOUTYOU_BASE_BY_COUNTRY["DE"])


def _normalize_aboutyou_url(
    url: Optional[str],
    country: Optional[str],
    gender: Optional[str],
    category: Optional[str],
    sort: Optional[str],
    brand: Optional[str],
    price_min: Optional[float],
    price_max: Optional[float],
) -> str:
    base_domain = _resolve_aboutyou_base_domain(url, country)
    if url and url.strip():
        normalized = url.strip()
        if normalized.startswith("/"):
            normalized = f"{base_domain}{normalized}"
        elif not normalized.startswith("http://") and not normalized.startswith("https://"):
            normalized = f"https://{normalized}"
    elif category and category.strip():
        c = category.strip().strip("/")
        if c.startswith("http://") or c.startswith("https://"):
            normalized = c
        elif c.startswith("c/"):
            normalized = f"{base_domain}/{c}"
        elif c.startswith("/c/"):
            normalized = f"{base_domain}{c}"
        elif "/" in c:
            normalized = f"{base_domain}/c/{c}"
        else:
            g = (gender or "women").strip().lower()
            gender_segment = "herren" if g in {"men", "herren"} else "frauen"
            normalized = f"{base_domain}/c/{gender_segment}/schuhe/{c}"
    else:
        normalized = f"{base_domain}/c/frauen/schuhe/stiefeletten-20276"

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


def _tile_to_item(tile: Any, base_domain: str) -> Optional[Dict[str, Any]]:
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
    product_url = urljoin(base_domain, href) if href else None

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


def _to_scraped_item_aboutyou(
    *,
    raw: Dict[str, Any],
    country: str,
    sort: str,
    page: int,
    position: int,
    page_size: int,
) -> ScrapedItem:
    price, currency = parse_price(raw.get("price"), market=country)
    return ScrapedItem(
        source="aboutyou",
        market=country,
        brand=raw.get("brand") or "",
        name=raw.get("name") or "",
        priceValue=price,
        currency=currency,
        url=raw.get("url"),
        imageUrl=raw.get("image"),
        rank=compute_rank(page, position, page_size),
        page=page,
        positionOnPage=position,
        sortMode=sort or "popularity",
        sku=None,
        category=infer_category_from_name(raw.get("name") or ""),
        gender=raw.get("gender"),
        isNew=False,
        isOnSale=bool(raw.get("old_price")),
        hasImage=bool(raw.get("image")),
        backend=None,
        backendIndex=None,
        backendRank=None,
        raw=raw,
    )


def _scrape_aboutyou_page_sync(page_url: str, base_domain: str) -> List[Dict[str, Any]]:
    try:
        response = requests.get(page_url, headers=DEFAULT_HEADERS, timeout=30)
        response.raise_for_status()
    except Exception as ex:
        logging.warning("[AboutYou] Failed loading %s: %s", page_url, ex)
        return []

    soup = BeautifulSoup(response.text, "lxml")
    tiles = soup.select('[data-testid^="productTile-"]')
    if not tiles:
        tiles = soup.select("article")

    out: List[Dict[str, Any]] = []
    for tile in tiles:
        parsed = _tile_to_item(tile, base_domain)
        if parsed:
            out.append(parsed)
    return out


async def scrape_aboutyou_filtered(**filters: Any) -> List[ScrapedItem]:
    country = (filters.get("country") or "DE").strip().upper()
    sort = (filters.get("sort") or "popularity").strip()
    pages_raw = filters.get("pages", filters.get("max_pages", 1))
    try:
        pages = max(1, int(pages_raw or 1))
    except Exception:
        pages = 1

    base_url = _normalize_aboutyou_url(
        url=filters.get("url"),
        country=country,
        gender=filters.get("gender"),
        category=filters.get("category"),
        sort=sort,
        brand=filters.get("brand"),
        price_min=filters.get("priceMin"),
        price_max=filters.get("priceMax"),
    )
    base_domain = _resolve_aboutyou_base_domain(base_url, country)

    raw_results: List[Dict[str, Any]] = []
    for page_num in range(1, pages + 1):
        page_url = _build_page_url(base_url, page_num)
        page_items = await asyncio.to_thread(_scrape_aboutyou_page_sync, page_url, base_domain)
        for idx, item in enumerate(page_items, start=1):
            row = dict(item)
            row["country"] = country
            row["sort"] = sort
            row["page"] = page_num
            row["positionOnPage"] = idx
            raw_results.append(row)

    filtered = _apply_filters(
        raw_results,
        brand=filters.get("brand"),
        keyword=filters.get("keyword"),
        price_min=filters.get("priceMin"),
        price_max=filters.get("priceMax"),
    )
    ordered = _sort_items(filtered, sort)

    items: List[ScrapedItem] = []
    for idx, raw in enumerate(ordered, start=1):
        page = ((idx - 1) // 30) + 1
        pos = ((idx - 1) % 30) + 1
        items.append(
            _to_scraped_item_aboutyou(
                raw=raw,
                country=country,
                sort=sort,
                page=page,
                position=pos,
                page_size=30,
            )
        )
    return items
