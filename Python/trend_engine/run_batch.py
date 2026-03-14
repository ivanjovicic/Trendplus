"""
trend_engine/run_batch.py
=========================
Orchestrira cijeli trend batch:
  1. Scrape svih izvora i tržišta (async)
  2. compute_trend_groups     → pure math
  3. apply_social_boost       → opcionalno
  4. serialize_trend_groups   → List[dict] spreman za JSON / C# deserialization

Nema DB poziva — sve što dolazi ovdje je čista Python logika.
Za upis u DB koristi C# worker ili pipeline/daily_pipeline.py.

Primjer poziva:
    import asyncio
    from trend_engine.run_batch import generate_trend_results
    results = asyncio.run(generate_trend_results(pages=3))
"""

from __future__ import annotations

import asyncio
import logging
from datetime import datetime
from typing import Any, Dict, List, Optional

from scraper import (
    ABOUTYOU_MARKETS,
    DEICHMANN_MARKETS,
    HUMANIC_MARKETS,
    MARKETS,
    ZALANDO_MARKETS,
    _scrape_aboutyou,
    _scrape_deichmann,
    _scrape_humanic,
    _scrape_zalando,
)
from scraper.schema import ScrapedItem
from trend_engine.core import (
    compute_trend_groups,
    apply_social_boost,
    serialize_trend_groups,
)

logger = logging.getLogger("trend_engine.run_batch")


# ─── Social score provider ────────────────────────────────────────────────────

async def _get_social_scores() -> Dict[str, float]:
    """
    Vraća social scores po brendu iz dostupnih izvora.
    Prioritet:
      1. Tiktok trends (scraper/tiktok.py)      — async
      2. Instagram trends (scraper/instagram.py) — async
      3. Fallback: prazan dict (social boost = 0)

    Format: {"nike": 82.5, "adidas": 67.1, ...}  (0–100 skala)
    """
    scores: Dict[str, float] = {}

    try:
        from scraper.tiktok import get_brand_tiktok_scores  # type: ignore
        tiktok = await get_brand_tiktok_scores()
        scores.update({k.lower(): v for k, v in tiktok.items()})
        logger.info(f"[social] TikTok scores loaded: {len(tiktok)} brendova")
    except Exception as e:
        logger.debug(f"[social] TikTok modul nije dostupan: {e}")

    try:
        from scraper.instagram import get_brand_instagram_scores  # type: ignore
        gram = await get_brand_instagram_scores()
        # Instagram merge: uzimamo max od oba izvora za isti brend
        for brand, val in gram.items():
            key = brand.lower()
            scores[key] = max(scores.get(key, 0.0), val)
        logger.info(f"[social] Instagram scores merged: {len(gram)} brendova")
    except Exception as e:
        logger.debug(f"[social] Instagram modul nije dostupan: {e}")

    return scores


# ─── Scraper wrapper ──────────────────────────────────────────────────────────

def _parse_optional_int(value: Any) -> Optional[int]:
    if value in (None, ""):
        return None

    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _parse_optional_float(value: Any) -> Optional[float]:
    if value in (None, ""):
        return None

    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def _parse_scraped_at(value: Any) -> Optional[datetime]:
    if isinstance(value, datetime):
        return value

    if isinstance(value, str) and value:
        try:
            return datetime.fromisoformat(value.replace("Z", "+00:00"))
        except ValueError:
            return None

    return None


def _coerce_scraped_item(item: Any) -> Optional[ScrapedItem]:
    if isinstance(item, ScrapedItem):
        if asyncio.iscoroutine(item.sku):
            item.sku = None
        if asyncio.iscoroutine(item.productId):
            item.productId = None
        if asyncio.iscoroutine(item.url):
            item.url = ""
        return item

    if not isinstance(item, dict):
        return None

    source = str(item.get("source") or "").strip()
    market = str(item.get("market") or "").strip()
    brand = str(item.get("brand") or "").strip()
    name = str(item.get("name") or "").strip()

    if not source or not market or not brand or not name:
        logger.debug("[batch] Skipping malformed scraped item with missing core fields: %s", item)
        return None

    image_url = item.get("imageUrl")
    rank = _parse_optional_int(item.get("rank")) or 1
    page = _parse_optional_int(item.get("page")) or 1
    position_on_page = _parse_optional_int(item.get("positionOnPage")) or rank
    backend_rank = _parse_optional_int(item.get("backendRank"))
    social_score = _parse_optional_float(item.get("socialScore"))
    previous_social_score = _parse_optional_float(item.get("previousSocialScore"))
    scraped_at = _parse_scraped_at(item.get("scrapedAt"))

    return ScrapedItem(
        source=source,
        market=market,
        brand=brand,
        name=name,
        priceValue=_parse_optional_float(item.get("priceValue")) or 0.0,
        currency=str(item.get("currency") or "EUR"),
        url=str(item.get("url") or ""),
        imageUrl=image_url if image_url else None,
        rank=rank,
        page=page,
        positionOnPage=position_on_page,
        sortMode=str(item.get("sortMode") or "popularity"),
        sku=item.get("sku"),
        productId=item.get("productId"),
        category=item.get("category"),
        gender=item.get("gender"),
        isNew=bool(item.get("isNew", False)),
        isOnSale=bool(item.get("isOnSale", False)),
        hasImage=bool(item.get("hasImage", bool(image_url))),
        backend=item.get("backend"),
        backendIndex=item.get("backendIndex"),
        backendRank=backend_rank,
        backendQueryId=item.get("backendQueryId"),
        socialScore=social_score,
        previousSocialScore=previous_social_score,
        scrapedAt=scraped_at or datetime.utcnow(),
        raw=item,
    )


async def _scrape_all(
    pages: int,
    markets: Optional[List[str]],
) -> List[ScrapedItem]:
    """
    Scrape all configured sources and coerce results to ScrapedItem objects.
    """
    active_markets = markets or MARKETS
    tasks: List[asyncio.Task[List[Any]]] = []

    for market in active_markets:
        if market in ZALANDO_MARKETS:
            tasks.append(asyncio.create_task(_scrape_zalando(market, pages)))
        if market in HUMANIC_MARKETS:
            tasks.append(asyncio.create_task(_scrape_humanic(market, pages)))
        if market in DEICHMANN_MARKETS:
            tasks.append(asyncio.create_task(_scrape_deichmann(market, pages)))
        if market in ABOUTYOU_MARKETS:
            tasks.append(asyncio.create_task(_scrape_aboutyou(market, pages)))

    results = await asyncio.gather(*tasks, return_exceptions=True)
    items: List[ScrapedItem] = []

    for result in results:
        if isinstance(result, Exception):
            logger.error("[batch] Scrape task failed: %s", result)
            continue

        for raw_item in result:
            coerced = _coerce_scraped_item(raw_item)
            if coerced is not None:
                items.append(coerced)

    logger.info("[batch] Collected %s scraped items after coercion", len(items))
    return items


# ─── Glavni entry point ────────────────────────────────────────────────────────

async def generate_trend_results(
    pages: int = 5,
    markets: Optional[List[str]] = None,
    top_n: Optional[int] = None,
    social_weight: float = 0.30,
    include_social: bool = True,
) -> List[Dict[str, Any]]:
    """
    Kompletni trend batch pipeline (bez DB).

    Parametri:
      pages:          broj stranica po izvoru/tržištu (default 5)
      markets:        lista tržišta, npr. ["DE","AT"] (default: sva)
      top_n:          broj top rezultata koji se vraćaju (default: svi)
      social_weight:  jačina social boosta (0–1)
      include_social: da li primijeniti social boost

    Vraća:
      List[dict] — serialize_trend_groups format, spreman za JSON izlaz
      Svaki dict ima: canonical_key, brand, name, markets, sources,
                      total_occurrences, unique_sources, unique_markets,
                      base_score, final_score, rank,
                      source_counts, market_counts
    """
    # 1) Scraping
    scraped = await _scrape_all(pages=pages, markets=markets)
    if not scraped:
        logger.warning("[batch] Nema scraped podataka!")
        return []

    # 2) Pure scoring — nema DB, nema IO
    groups = compute_trend_groups(scraped)
    logger.info(f"[batch] Grupisano u {len(groups)} jedinstvenih proizvoda")

    # 3) Social boost (opcionalno)
    if include_social:
        social_scores = await _get_social_scores()
        if social_scores:
            groups = apply_social_boost(groups, social_scores, social_weight)
            logger.info(f"[batch] Social boost primijenjen ({len(social_scores)} brendova)")

    # 4) Optional top-N trim
    if top_n is not None:
        groups = groups[:top_n]

    # 5) Serijalizacija → JSON-friendly List[dict]
    return serialize_trend_groups(groups)


# ─── CLI ─────────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    import json
    import argparse

    parser = argparse.ArgumentParser(description="Trend Engine – batch run")
    parser.add_argument("--pages", type=int, default=5, help="Broj stranica po izvoru")
    parser.add_argument("--markets", nargs="*", default=None, help="Tržišta npr. DE AT")
    parser.add_argument("--top", type=int, default=100, help="Top N rezultata")
    parser.add_argument("--no-social", action="store_true", help="Isključi social boost")
    parser.add_argument("--out", type=str, default=None, help="Output JSON fajl (default: stdout)")
    args = parser.parse_args()

    logging.basicConfig(level=logging.INFO, format="%(levelname)s %(name)s: %(message)s")

    results = asyncio.run(generate_trend_results(
        pages=args.pages,
        markets=args.markets,
        top_n=args.top,
        include_social=not args.no_social,
    ))

    json_str = json.dumps(results, indent=2, ensure_ascii=False)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write(json_str)
        print(f"✅ Saved {len(results)} results to {args.out}")
    else:
        print(json_str)
