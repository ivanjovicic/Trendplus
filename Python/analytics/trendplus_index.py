"""
Trendplus Index Engine
======================
Computes a 0-100 index for any scope (market / brand / category / brand+market).

Public API:
    compute_trendplus_index(products)          → dict with index_value + components
    compute_index_for_all_scopes(date, groups, momentum_map)  → save to trendplus_index table
    get_index_history(scope_type, scope_value, days)          → list of rows for UI charts
"""

from __future__ import annotations

import logging
from datetime import date
from typing import Any, Dict, List, Optional

from db.connection import bulk_insert, fetch, execute

logger = logging.getLogger("analytics.index")


# ── Pure math ─────────────────────────────────────────────────────────────────

def _normalize_scores(scores: List[float]) -> List[float]:
    max_s = max(scores) if scores else 1.0
    if max_s == 0:
        return [0.0] * len(scores)
    return [s / max_s for s in scores]


def _weighted_average(values: List[float], weights: List[float]) -> float:
    w_sum = sum(weights)
    if w_sum == 0:
        return 0.0
    return sum(v * w for v, w in zip(values, weights)) / w_sum


def compute_trendplus_index(products: List[Dict[str, Any]]) -> Dict[str, Any]:
    """
    Calculate Trendplus Index for a collection of products.

    Each product dict should have:
        score          – popularity score (float)
        momentum_score – from trend_product_momentum, range [-1,1] (optional)
        social_score   – raw social score 0–100 (optional)

    Returns dict with: index_value, base_component, momentum_component, social_component
    """
    if not products:
        return {
            "index_value":          0.0,
            "base_component":       0.0,
            "momentum_component":   0.5,
            "social_component":     0.0,
        }

    scores        = [float(p.get("score") or 0) for p in products]
    momentums     = [float(p.get("momentum_score") or 0) for p in products]
    social_scores = [float(p.get("social_score") or 0)   for p in products]

    norm_scores = _normalize_scores(scores)

    # Base: weighted by inverse position (top rank gets highest weight)
    weights        = [1.0 / (i + 1) for i in range(len(products))]
    base_component = _weighted_average(norm_scores, weights)

    # Momentum: average across group, mapped [-1,1] → [0,1]
    avg_momentum       = sum(momentums) / len(momentums)
    momentum_component = (avg_momentum + 1.0) / 2.0

    # Social: average normalised to [0,1]
    avg_social       = sum(social_scores) / len(social_scores)
    social_component = min(1.0, avg_social / 100.0)

    index_value = 100.0 * (
        0.60 * base_component +
        0.25 * momentum_component +
        0.15 * social_component
    )

    return {
        "index_value":        round(index_value, 4),
        "base_component":     round(base_component, 6),
        "momentum_component": round(momentum_component, 6),
        "social_component":   round(social_component, 6),
    }


# ── Scope builders ────────────────────────────────────────────────────────────

def _build_scope_rows(
    snapshot_date: date,
    groups: List[Dict[str, Any]],
    momentum_map: Dict[str, Dict[str, Any]],
) -> List[Dict[str, Any]]:
    """
    Expand scored groups into per-scope product lists, then compute one index per scope.
    Scopes: market, brand, category, brand_market.
    """
    # Flatten all items with their group score + momentum
    enriched: List[Dict[str, Any]] = []
    for g in groups:
        key        = g.get("key") or ""
        group_score = float(g.get("score") or 0)
        mom         = momentum_map.get(key, {})
        mom_score   = float(mom.get("momentum_score") or 0)

        for it in g.get("items", []):
            enriched.append({
                "market":         (it.get("market") or "GLOBAL").upper(),
                "brand":          (it.get("brand") or "").strip().lower(),
                "category":       (it.get("category") or "").strip().lower(),
                "score":          group_score,
                "momentum_score": mom_score,
                "social_score":   float(it.get("socialScore") or 0),
            })

    if not enriched:
        return []

    def _index_for(subset: List[Dict]) -> Dict[str, Any]:
        return compute_trendplus_index(subset)

    scope_rows: List[Dict[str, Any]] = []

    # ── market scope
    by_market: Dict[str, List] = {}
    for p in enriched:
        by_market.setdefault(p["market"], []).append(p)
    for market, prods in by_market.items():
        idx = _index_for(prods)
        scope_rows.append({
            "snapshot_date":      snapshot_date,
            "scope_type":         "market",
            "scope_value":        market,
            **idx,
        })

    # ── brand scope
    by_brand: Dict[str, List] = {}
    for p in enriched:
        if p["brand"]:
            by_brand.setdefault(p["brand"], []).append(p)
    for brand, prods in by_brand.items():
        idx = _index_for(prods)
        scope_rows.append({
            "snapshot_date":      snapshot_date,
            "scope_type":         "brand",
            "scope_value":        brand,
            **idx,
        })

    # ── category scope
    by_cat: Dict[str, List] = {}
    for p in enriched:
        if p["category"]:
            by_cat.setdefault(p["category"], []).append(p)
    for cat, prods in by_cat.items():
        idx = _index_for(prods)
        scope_rows.append({
            "snapshot_date":      snapshot_date,
            "scope_type":         "category",
            "scope_value":        cat,
            **idx,
        })

    # ── brand_market scope (e.g. "nike|de")
    by_bm: Dict[str, List] = {}
    for p in enriched:
        if p["brand"] and p["market"]:
            k = f"{p['brand']}|{p['market'].lower()}"
            by_bm.setdefault(k, []).append(p)
    for bm_key, prods in by_bm.items():
        idx = _index_for(prods)
        scope_rows.append({
            "snapshot_date":      snapshot_date,
            "scope_type":         "brand_market",
            "scope_value":        bm_key,
            **idx,
        })

    return scope_rows


# ── DB operations ─────────────────────────────────────────────────────────────

async def compute_index_for_all_scopes(
    snapshot_date: date,
    groups: List[Dict[str, Any]],
    momentum_map: Dict[str, Dict[str, Any]],
) -> int:
    """
    Compute and persist Trendplus Index for all scopes for the given date.
    Returns number of rows inserted.
    """
    rows = _build_scope_rows(snapshot_date, groups, momentum_map)
    if not rows:
        logger.warning(f"compute_index_for_all_scopes({snapshot_date}): no rows produced")
        return 0

    await execute(
        "DELETE FROM trendplus_index WHERE snapshot_date = $1",
        snapshot_date,
    )

    n = await bulk_insert("trendplus_index", rows)
    logger.info(f"compute_index_for_all_scopes({snapshot_date}): inserted {n} scope rows")
    return n


async def get_index_history(
    scope_type: str,
    scope_value: str,
    days: int = 30,
) -> List[Dict[str, Any]]:
    """
    Fetch the last N days of index history for a given scope.
    Useful for UI trend charts.
    """
    rows = await fetch(
        """
        SELECT snapshot_date, index_value, base_component, momentum_component, social_component
        FROM trendplus_index
        WHERE scope_type = $1
          AND scope_value = $2
        ORDER BY snapshot_date DESC
        LIMIT $3
        """,
        scope_type, scope_value.lower(), days,
    )
    return [dict(r) for r in rows]
