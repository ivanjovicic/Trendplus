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
from typing import Any, Dict, List, Optional

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

async def _scrape_all(
    sources: List[str],
    markets: List[str],
) -> List[Dict[str, Any]]:
    """
    Scrape all sources and markets with fallback mechanisms.
    """
    results = []
    tasks = []

    for source in sources:
        for market in markets:
            tasks.append(_scrape_source_market(source, market))

    completed, pending = await asyncio.wait(tasks, return_when=asyncio.ALL_COMPLETED)

    for task in completed:
        try:
            results.append(task.result())
        except Exception as e:
            logger.error(f"Scraping failed for a task: {e}")

    if pending:
        logger.warning(f"Some scraping tasks did not complete: {len(pending)} tasks pending.")

    return results

async def _scrape_source_market(source: str, market: str) -> Dict[str, Any]:
    """
    Scrape a specific source and market with error handling.
    """
    try:
        # Simulate scraping logic
        return {"source": source, "market": market, "data": []}
    except Exception as e:
        logger.error(f"Error scraping {source} for {market}: {e}")
        return {"source": source, "market": market, "data": None}


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
    scraped = await _scrape_all(sources=["tiktok", "instagram"], markets=markets)
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
