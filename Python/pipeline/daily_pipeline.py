"""
Trendplus Daily Pipeline
========================
Orchestrates the full daily data flow:

1. Scrape all markets (Zalando, Deichmann, Humanic, AboutYou)
2. Normalize → ScrapedItem dicts
3. compute_topN → popularity-scored groups
4. Save daily snapshots to trend_product_snapshots
5. Compute trend_product_momentum (today vs yesterday)
6. Compute Trendplus Index for all scopes (market / brand / category / brand_market)
7. Compute inventory order recommendations

Run manually:
    python -m pipeline.daily_pipeline

Or imported by the scheduler:
    from pipeline.daily_pipeline import run_daily_pipeline
"""

from __future__ import annotations

import asyncio
import logging
import sys
from datetime import date
from typing import Any, Dict, List, Optional

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    handlers=[logging.StreamHandler(sys.stdout)],
)
logger = logging.getLogger("pipeline.daily")


# Helper function to log step summaries
def log_step_summary(step_name: str, summary: Dict[str, Any], logger: logging.Logger) -> None:
    step_summary = summary.get("steps", {}).get(step_name, {})
    logger.info(f"Step {step_name}: {step_summary}")

# Helper function to handle dry-run logic
def handle_dry_run(dry_run: bool, action: str, logger: logging.Logger) -> None:
    if dry_run:
        logger.info(f"  (dry_run: skipping {action})")

async def run_daily_pipeline(
    run_date: Optional[date] = None,
    top_n: int = 500,
    scrape_pages: int = 5,
    dry_run: bool = False,
) -> Dict[str, Any]:
    """
    Execute full daily pipeline. Returns summary dict.
    dry_run=True skips DB writes (useful for testing).
    """
    today = run_date or date.today()
    summary: Dict[str, Any] = {"date": str(today), "steps": {}}

    logger.info(f"=== Trendplus Daily Pipeline — {today} ===")

    # ── 1. Scrape ─────────────────────────────────────────────────────────────
    logger.info("Step 1: Scraping all markets …")
    from scraper import scrape_all_markets
    scraped_items: List[Dict[str, Any]] = await scrape_all_markets(pages=scrape_pages)
    summary["steps"]["scrape"] = {"items": len(scraped_items)}
    log_step_summary("scrape", summary, logger)

    if not scraped_items:
        logger.error("No items scraped — aborting pipeline")
        return summary

    # ── 2. Popularity scoring ─────────────────────────────────────────────────
    logger.info(f"Step 2: Scoring top {top_n} groups …")
    from scorer import compute_topN
    groups = compute_topN(scraped_items, top_n=top_n)
    summary["steps"]["scoring"] = {"groups": len(groups)}
    log_step_summary("scoring", summary, logger)

    # ── 3. Save snapshots ─────────────────────────────────────────────────────
    logger.info("Step 3: Saving trend snapshots …")
    from analytics.trend_momentum_engine import (
        save_trend_snapshot,
        compute_all_momentums,
        get_momentum_map,
    )
    if not dry_run:
        n_snap = await save_trend_snapshot(today, groups)
    else:
        n_snap = len(groups)
        handle_dry_run(dry_run, "DB write", logger)
    summary["steps"]["snapshots"] = {"rows": n_snap}
    log_step_summary("snapshots", summary, logger)

    # ── 4. Compute momentum ───────────────────────────────────────────────────
    logger.info("Step 4: Computing momentum (today vs yesterday) …")
    if not dry_run:
        n_mom = await compute_all_momentums(today)
    else:
        n_mom = 0
        handle_dry_run(dry_run, "momentum computation", logger)
    summary["steps"]["momentum"] = {"rows": n_mom}
    log_step_summary("momentum", summary, logger)

    # Fetch momentum map so later steps can enrich groups
    momentum_map: Dict[str, Any] = {}
    if not dry_run:
        momentum_map = await get_momentum_map(today)

    # Enrich groups with momentum_score for index calculation
    for g in groups:
        key = g.get("key") or ""
        mom = momentum_map.get(key, {})
        g["momentum_score"] = mom.get("momentum_score", 0.0)

    # ── 5. Trendplus Index ────────────────────────────────────────────────────
    logger.info("Step 5: Computing Trendplus Index for all scopes …")
    from analytics.trendplus_index import compute_index_for_all_scopes
    if not dry_run:
        n_idx = await compute_index_for_all_scopes(today, groups, momentum_map)
    else:
        from analytics.trendplus_index import _build_scope_rows
        n_idx = len(_build_scope_rows(today, groups, momentum_map))
        handle_dry_run(dry_run, "index computation", logger)
    summary["steps"]["index"] = {"scope_rows": n_idx}
    log_step_summary("index", summary, logger)

    # ── 6. Inventory recommendations ─────────────────────────────────────────
    logger.info("Step 6: Computing inventory recommendations …")
    from analytics.inventory_intelligence_model import compute_inventory_recommendations
    if not dry_run:
        recs = await compute_inventory_recommendations(today)
    else:
        recs = []
        handle_dry_run(dry_run, "inventory recommendations", logger)
    summary["steps"]["inventory"] = {"recommendations": len(recs)}
    log_step_summary("inventory", summary, logger)

    # ── Done ──────────────────────────────────────────────────────────────────
    logger.info(f"=== Pipeline complete for {today} ===")
    logger.info(f"Summary: {summary}")

    from db.connection import close_pool
    await close_pool()

    return summary


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Trendplus Daily Pipeline")
    parser.add_argument("--date",   type=str,  default=None,  help="Run for specific date YYYY-MM-DD")
    parser.add_argument("--top-n",  type=int,  default=500,   help="Top N products to score")
    parser.add_argument("--pages",  type=int,  default=5,     help="Pages per source")
    parser.add_argument("--dry-run",action="store_true",      help="Skip all DB writes")
    args = parser.parse_args()

    run_date = date.fromisoformat(args.date) if args.date else None

    asyncio.run(run_daily_pipeline(
        run_date    = run_date,
        top_n       = args.top_n,
        scrape_pages= args.pages,
        dry_run     = args.dry_run,
    ))
