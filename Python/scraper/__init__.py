"""
Scraper package — unified entry point.

Usage:
    items = asyncio.run(scrape_all_markets())
"""

import asyncio
import logging
from dataclasses import asdict
from typing import Any, Dict, List, Optional

logger = logging.getLogger("scraper")

# ── Market / source configuration ────────────────────────────────────────────

MARKETS = ["DE", "AT", "CH", "HU", "RO"]

# Zalando supports all 5 markets
ZALANDO_MARKETS = MARKETS

# Humanic only has AT store publicly (also partial DE)
HUMANIC_MARKETS = ["AT"]

# Deichmann: all 5
DEICHMANN_MARKETS = MARKETS

# AboutYou: all 5
ABOUTYOU_MARKETS = MARKETS

# How many pages to scrape per source/market in production
PAGES_PER_SOURCE = 5

# Default sort mode for each source
DEFAULT_SORT = "popularity"


# ── Individual scraper wrappers ───────────────────────────────────────────────

async def _scrape_zalando(market: str, pages: int) -> List[Any]:
    from scraper.zalando_playwright import scrape_zalando_playwright
    try:
        items = await scrape_zalando_playwright(
            country=market,
            sort=DEFAULT_SORT,
            max_pages=pages,
        )
        logger.info(f"[zalando/{market}] {len(items)} items")
        return items
    except Exception as e:
        logger.error(f"[zalando/{market}] scrape failed: {e}")
        return []


async def _scrape_humanic(market: str, pages: int) -> List[Any]:
    from scraper.humanic_scraper import scrape_humanic_filtered
    try:
        items = await scrape_humanic_filtered(
            country=market,
            sort=DEFAULT_SORT,
            max_pages=pages,
        )
        logger.info(f"[humanic/{market}] {len(items)} items")
        return items
    except Exception as e:
        logger.error(f"[humanic/{market}] scrape failed: {e}")
        return []


async def _scrape_deichmann(market: str, pages: int) -> List[Any]:
    from scraper.deichmann_scraper import scrape_deichmann_filtered
    try:
        items = await scrape_deichmann_filtered(
            country=market,
            sort=DEFAULT_SORT,
            max_pages=pages,
        )
        logger.info(f"[deichmann/{market}] {len(items)} items")
        return items
    except Exception as e:
        logger.error(f"[deichmann/{market}] scrape failed: {e}")
        return []


async def _scrape_aboutyou(market: str, pages: int) -> List[Any]:
    from scraper.aboutyou_scraper import scrape_aboutyou_filtered
    try:
        items = await scrape_aboutyou_filtered(
            country=market,
            sort=DEFAULT_SORT,
            max_pages=pages,
        )
        logger.info(f"[aboutyou/{market}] {len(items)} items")
        return items
    except Exception as e:
        logger.error(f"[aboutyou/{market}] scrape failed: {e}")
        return []


def _to_dict(item: Any) -> Dict[str, Any]:
    """Convert ScrapedItem dataclass to plain dict for scoring pipeline."""
    from scraper.schema import ScrapedItem
    if isinstance(item, ScrapedItem):
        d = asdict(item)
        # scoring.py expects these field names
        d.setdefault("source", item.source)
        d.setdefault("market", item.market)
        d.setdefault("rank", item.rank)
        d.setdefault("priceValue", item.priceValue)
        d.setdefault("imageUrl", item.imageUrl)
        d.setdefault("isNew", item.isNew)
        d.setdefault("isOnSale", item.isOnSale)
        d.setdefault("hasImage", item.hasImage)
        d.setdefault("backendRank", item.backendRank)
        d.setdefault("backendIndex", item.backendIndex)
        d.setdefault("scrapedAt", item.scrapedAt.isoformat() if item.scrapedAt else None)
        return d
    return dict(item) if hasattr(item, "__iter__") else {}


# ── Main orchestrator ─────────────────────────────────────────────────────────

async def scrape_all_markets(
    pages: int = PAGES_PER_SOURCE,
    markets: Optional[List[str]] = None,
) -> List[Dict[str, Any]]:
    """
    Scrape all sources across all markets concurrently.
    Returns a flat list of plain dicts (compatible with scoring.py).
    """
    active_markets = markets or MARKETS

    tasks = []
    # Zalando
    for m in active_markets:
        if m in ZALANDO_MARKETS:
            tasks.append(_scrape_zalando(m, pages))
    # Humanic
    for m in active_markets:
        if m in HUMANIC_MARKETS:
            tasks.append(_scrape_humanic(m, pages))
    # Deichmann
    for m in active_markets:
        if m in DEICHMANN_MARKETS:
            tasks.append(_scrape_deichmann(m, pages))
    # AboutYou
    for m in active_markets:
        if m in ABOUTYOU_MARKETS:
            tasks.append(_scrape_aboutyou(m, pages))

    results = await asyncio.gather(*tasks, return_exceptions=True)

    all_items: List[Dict[str, Any]] = []
    for batch in results:
        if isinstance(batch, Exception):
            logger.error(f"Scrape task failed: {batch}")
            continue
        for item in batch:
            d = _to_dict(item)
            if d:
                all_items.append(d)

    logger.info(f"[scrape_all_markets] total {len(all_items)} items from {len(tasks)} tasks")
    return all_items
