"""
Trendplus Pipeline Scheduler
=============================
Uses APScheduler to run the daily pipeline at a configurable time.

Usage:
    python -m pipeline.scheduler

Environment variables:
    PIPELINE_CRON_HOUR   – hour to run (default: 3)
    PIPELINE_CRON_MINUTE – minute to run (default: 0)
    PIPELINE_TOP_N       – top N products to score (default: 500)
    PIPELINE_PAGES       – pages per source to scrape (default: 5)
    TZ                   – timezone string (default: Europe/Berlin)
"""

from __future__ import annotations

import asyncio
import logging
import os
import sys
from datetime import date

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    handlers=[logging.StreamHandler(sys.stdout)],
)
logger = logging.getLogger("pipeline.scheduler")


def _env_int(key: str, default: int) -> int:
    try:
        return int(os.environ.get(key, default))
    except (TypeError, ValueError):
        return default


# ── Config ────────────────────────────────────────────────────────────────────

CRON_HOUR   = _env_int("PIPELINE_CRON_HOUR",   3)
CRON_MINUTE = _env_int("PIPELINE_CRON_MINUTE",  0)
TOP_N       = _env_int("PIPELINE_TOP_N",      500)
PAGES       = _env_int("PIPELINE_PAGES",        5)
TIMEZONE    = os.environ.get("TZ", "Europe/Berlin")


# ── Job ───────────────────────────────────────────────────────────────────────

async def _run_job() -> None:
    from pipeline.daily_pipeline import run_daily_pipeline
    try:
        summary = await run_daily_pipeline(top_n=TOP_N, scrape_pages=PAGES)
        logger.info(f"Scheduled run complete: {summary}")
    except Exception as e:
        logger.exception(f"Scheduled pipeline failed: {e}")


def _job_wrapper() -> None:
    """Sync wrapper called by APScheduler."""
    asyncio.run(_run_job())


# ── Scheduler setup ───────────────────────────────────────────────────────────

def start_scheduler() -> None:
    try:
        from apscheduler.schedulers.blocking import BlockingScheduler
        from apscheduler.triggers.cron import CronTrigger
    except ImportError:
        logger.error(
            "APScheduler not installed. Run: pip install apscheduler"
        )
        sys.exit(1)

    scheduler = BlockingScheduler(timezone=TIMEZONE)

    scheduler.add_job(
        _job_wrapper,
        trigger=CronTrigger(hour=CRON_HOUR, minute=CRON_MINUTE, timezone=TIMEZONE),
        id="daily_pipeline",
        name="Trendplus Daily Pipeline",
        misfire_grace_time=3600,  # allow up to 1h delay if system was down
        coalesce=True,
    )

    logger.info(
        f"Scheduler started — daily pipeline at {CRON_HOUR:02d}:{CRON_MINUTE:02d} ({TIMEZONE})"
    )
    logger.info("Press Ctrl+C to stop.")

    try:
        scheduler.start()
    except (KeyboardInterrupt, SystemExit):
        logger.info("Scheduler stopped.")


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Trendplus Pipeline Scheduler")
    parser.add_argument("--now",  action="store_true", help="Run pipeline immediately and exit")
    parser.add_argument("--date", type=str, default=None, help="Run for specific date YYYY-MM-DD")
    args = parser.parse_args()

    if args.now or args.date:
        # One-shot run
        from pipeline.daily_pipeline import run_daily_pipeline
        run_date = date.fromisoformat(args.date) if args.date else None
        asyncio.run(run_daily_pipeline(run_date=run_date, top_n=TOP_N, scrape_pages=PAGES))
    else:
        start_scheduler()
