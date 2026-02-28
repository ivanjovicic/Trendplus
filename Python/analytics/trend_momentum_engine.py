"""
Trend Momentum Engine
=====================
- save_trend_snapshot(date, groups)   → bulk insert into trend_product_snapshots
- compute_all_momentums(date)         → compare today vs yesterday, bulk insert into trend_product_momentum
- get_momentum_map(date)              → {canonical_key: momentum_dict} for pipeline use
- compute_momentum(today, yesterday)  → pure momentum math (no IO)

Tables required: analytics/trend_product_snapshots.sql and trend_product_momentum.sql
"""

from __future__ import annotations

import logging
from datetime import date, timedelta
from typing import Any, Dict, List, Optional

from db.connection import bulk_insert, fetch, execute

logger = logging.getLogger("analytics.momentum")


# ── Pure math ────────────────────────────────────────────────────────────────

def compute_momentum(
    snapshot_today: Dict[str, Any],
    snapshot_yesterday: Optional[Dict[str, Any]],
) -> Dict[str, Any]:
    """
    Compute momentum for a single product given two consecutive snapshots.
    Both dicts must contain: score (float), rank_global (int).
    Returns: score_delta, rank_delta, is_new_entry, momentum_score.
    """
    if not snapshot_yesterday:
        # New entry in the system
        return {
            "score_delta":    float(snapshot_today["score"]),
            "rank_delta":     0,
            "is_new_entry":   True,
            "momentum_score": 1.0,
        }

    score_today = float(snapshot_today["score"])
    score_yest  = float(snapshot_yesterday["score"])
    rank_today  = int(snapshot_today["rank_global"])
    rank_yest   = int(snapshot_yesterday["rank_global"])

    score_delta = score_today - score_yest
    rank_delta  = rank_yest - rank_today  # positive = improved rank

    # Normalise to [-1, 1]
    score_component = max(-1.0, min(1.0, score_delta / max(score_yest, 0.01)))
    rank_component  = max(-1.0, min(1.0, rank_delta  / 50.0))

    momentum_score = 0.7 * score_component + 0.3 * rank_component

    return {
        "score_delta":    round(score_delta, 6),
        "rank_delta":     rank_delta,
        "is_new_entry":   False,
        "momentum_score": round(momentum_score, 6),
    }


# ── DB operations ─────────────────────────────────────────────────────────────

async def save_trend_snapshot(
    snapshot_date: date,
    grouped_results: List[Dict[str, Any]],
) -> int:
    """
    Upsert daily snapshot rows from compute_topN output.
    Deletes existing rows for that date first (idempotent re-runs).
    Returns number of rows inserted.
    """
    if not grouped_results:
        logger.warning("save_trend_snapshot: no groups to save")
        return 0

    await execute(
        "DELETE FROM trend_product_snapshots WHERE snapshot_date = $1",
        snapshot_date,
    )

    rows: List[Dict[str, Any]] = []
    for rank, g in enumerate(grouped_results, start=1):
        items = g.get("items", [])
        if not items:
            continue
        # Prefer item that has category set
        main    = next((it for it in items if it.get("category")), items[0])
        sources = {it.get("source", "") for it in items}

        rows.append({
            "snapshot_date":  snapshot_date,
            "canonical_key":  g.get("key") or g.get("canonical_key") or "",
            "product_name":   str(main.get("name") or ""),
            "brand":          str(main.get("brand") or ""),
            "category":       main.get("category"),
            "market":         main.get("market") or "GLOBAL",
            "score":          float(g.get("score") or g.get("final_score") or 0.0),
            "rank_global":    rank,
            "social_score":   g.get("socialScore"),
            "source_count":   len(items),
            "unique_sources": len(sources),
        })

    if not rows:
        return 0

    n = await bulk_insert("trend_product_snapshots", rows)
    logger.info(f"save_trend_snapshot({snapshot_date}): inserted {n} rows")
    return n


async def compute_all_momentums(snapshot_date: date) -> int:
    """
    Load today's and yesterday's snapshots from DB, compute momentum for every
    key present today, bulk-insert results into trend_product_momentum.
    Returns number of rows inserted.
    """
    yesterday = snapshot_date - timedelta(days=1)

    today_rows = await fetch(
        "SELECT canonical_key, score, rank_global "
        "FROM trend_product_snapshots WHERE snapshot_date = $1",
        snapshot_date,
    )
    if not today_rows:
        logger.warning(f"compute_all_momentums: no snapshot data for {snapshot_date}")
        return 0

    yest_rows = await fetch(
        "SELECT canonical_key, score, rank_global "
        "FROM trend_product_snapshots WHERE snapshot_date = $1",
        yesterday,
    )
    yest_map: Dict[str, Any] = {r["canonical_key"]: dict(r) for r in yest_rows}

    # Idempotent: clear previous run for today
    await execute(
        "DELETE FROM trend_product_momentum WHERE snapshot_date = $1",
        snapshot_date,
    )

    momentum_rows: List[Dict[str, Any]] = []
    for row in today_rows:
        key = row["canonical_key"]
        mom = compute_momentum(dict(row), yest_map.get(key))
        momentum_rows.append({
            "snapshot_date":  snapshot_date,
            "canonical_key":  key,
            "momentum_score": mom["momentum_score"],
            "score_delta":    mom["score_delta"],
            "rank_delta":     mom["rank_delta"],
            "is_new_entry":   mom["is_new_entry"],
        })

    if not momentum_rows:
        return 0

    n = await bulk_insert("trend_product_momentum", momentum_rows)
    logger.info(f"compute_all_momentums({snapshot_date}): inserted {n} rows")
    return n


async def get_momentum_map(snapshot_date: date) -> Dict[str, Dict[str, Any]]:
    """
    Return {canonical_key: {momentum_score, score_delta, rank_delta, is_new_entry}}
    for a given date. Used by the pipeline to enrich groups before index computation.
    """
    rows = await fetch(
        "SELECT canonical_key, momentum_score, score_delta, rank_delta, is_new_entry "
        "FROM trend_product_momentum WHERE snapshot_date = $1",
        snapshot_date,
    )
    return {r["canonical_key"]: dict(r) for r in rows}
