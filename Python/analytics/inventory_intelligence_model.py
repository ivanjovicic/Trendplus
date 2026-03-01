"""
Inventory Intelligence Model
=============================
Uses trend_score + momentum_score + sales_velocity to recommend order quantities.

Public API:
    trend_multiplier(trend_score, momentum_score)               → float multiplier
    calculate_order_quantity(...)                               → int units to order
    apply_inventory_rules(order_qty, ...)                       → int (clamped)
    compute_inventory_recommendations(date)                     → List[dict] saved to DB
"""

from __future__ import annotations

import logging
from datetime import date
from typing import Any, Dict, List, Optional

from db.connection import fetch, bulk_insert, execute

logger = logging.getLogger("analytics.inventory")

# ── Config ────────────────────────────────────────────────────────────────────

DEFAULT_TARGET_COVERAGE_DAYS = 30
DEFAULT_LEAD_TIME_DAYS       = 10
DEFAULT_MIN_ORDER            = 1
DEFAULT_MAX_ORDER            = 50


# ── Pure math ─────────────────────────────────────────────────────────────────

def trend_multiplier(
    trend_score: Optional[float],
    momentum_score: Optional[float],
) -> float:
    """
    Calculate the demand multiplier from trend and momentum signals.
    trend_score    ~ [0, 1]
    momentum_score ~ [-1, 1]
    Returns a multiplier in roughly [0.7, 1.95].
    """
    t = max(0.0, min(1.0, float(trend_score  or 0.0)))
    m = max(-1.0, min(1.0, float(momentum_score or 0.0)))

    base = 1.0 + 0.5 * t   # max +50% from trend
    mom  = 1.0 + 0.3 * m   # ±30% from momentum

    return round(base * mom, 4)


def calculate_order_quantity(
    sales_velocity:       Optional[float],
    stock_on_hand:        Optional[float],
    lead_time_days:       Optional[int],
    target_coverage_days: int   = DEFAULT_TARGET_COVERAGE_DAYS,
    trend_score:          Optional[float] = None,
    momentum_score:       Optional[float] = None,
) -> int:
    """
    Compute how many units to order.

    coverage = lead_time_days + target_coverage_days
    expected_demand = sales_velocity * coverage
    target_stock    = expected_demand * trend_multiplier
    order_qty       = max(0, target_stock - stock_on_hand)
    """
    sv    = float(sales_velocity  or 0.0)
    stock = float(stock_on_hand   or 0.0)
    lt    = int(lead_time_days    or DEFAULT_LEAD_TIME_DAYS)

    coverage        = lt + target_coverage_days
    expected_demand = sv * coverage
    multiplier      = trend_multiplier(trend_score, momentum_score)
    target_stock    = expected_demand * multiplier

    return max(0, int(round(target_stock - stock)))


def apply_inventory_rules(
    order_qty:        int,
    sales_velocity:   Optional[float],
    trend_score:      Optional[float],
    min_order:        int = DEFAULT_MIN_ORDER,
    max_order:        int = DEFAULT_MAX_ORDER,
) -> int:
    """
    Clamp order_qty within business rules and zero out dead products.
    """
    sv = float(sales_velocity or 0.0)
    ts = float(trend_score    or 0.0)

    # Dead product: no sales and low trend → skip
    if sv == 0.0 and ts < 0.3:
        return 0

    order_qty = max(order_qty, min_order)
    order_qty = min(order_qty, max_order)
    return order_qty


# ── DB operations ─────────────────────────────────────────────────────────────

INVENTORY_QUERY = """
SELECT
    p."ProductId"                        AS product_id,
    p."Brand"                            AS brand,
    p."Category"                         AS category,
    COALESCE(sv.sales_velocity, 0)       AS sales_velocity,
    COALESCE(p."Kolicina", 0)            AS stock_on_hand,
    10                                   AS lead_time_days,
    COALESCE(t.score, 0)                 AS trend_score,
    COALESCE(m.momentum_score, 0)        AS momentum_score
FROM "ProductsDim" p
LEFT JOIN (
    SELECT slf."ProductId"               AS product_id,
           SUM(slf."Qty")::decimal
             / NULLIF(COUNT(DISTINCT sf."SaleTimestampUtc"::date), 0) AS sales_velocity
    FROM "SalesLineFacts" slf
    JOIN "SalesFacts" sf ON sf."SaleId" = slf."SaleId"
    WHERE sf."SaleTimestampUtc" >= CURRENT_DATE - INTERVAL '30 days'
    GROUP BY slf."ProductId"
) sv ON p."ProductId" = sv.product_id
LEFT JOIN (
    SELECT canonical_key, score
    FROM trend_product_snapshots
    WHERE snapshot_date = $1
) t ON LOWER(p."Brand" || '|' || p."ProductName") = t.canonical_key
LEFT JOIN (
    SELECT canonical_key, momentum_score
    FROM trend_product_momentum
    WHERE snapshot_date = $1
) m ON LOWER(p."Brand" || '|' || p."ProductName") = m.canonical_key
"""


async def compute_inventory_recommendations(
    snapshot_date: date,
    target_coverage_days: int = DEFAULT_TARGET_COVERAGE_DAYS,
    min_order: int = DEFAULT_MIN_ORDER,
    max_order: int = DEFAULT_MAX_ORDER,
) -> List[Dict[str, Any]]:
    """
    Fetch product + trend + inventory data and compute order recommendations.
    Saves results and returns the list.
    """
    rows = await fetch(INVENTORY_QUERY, snapshot_date)
    if not rows:
        logger.warning(f"compute_inventory_recommendations: no product data for {snapshot_date}")
        return []

    recommendations: List[Dict[str, Any]] = []
    for r in rows:
        qty = calculate_order_quantity(
            sales_velocity       = r["sales_velocity"],
            stock_on_hand        = r["stock_on_hand"],
            lead_time_days       = r["lead_time_days"],
            target_coverage_days = target_coverage_days,
            trend_score          = r["trend_score"],
            momentum_score       = r["momentum_score"],
        )
        qty = apply_inventory_rules(
            qty,
            sales_velocity = r["sales_velocity"],
            trend_score    = r["trend_score"],
            min_order      = min_order,
            max_order      = max_order,
        )
        if qty > 0:
            recommendations.append({
                "snapshot_date":   snapshot_date,
                "product_id":      r["product_id"],
                "brand":           r["brand"],
                "category":        r["category"],
                "sales_velocity":  float(r["sales_velocity"]),
                "stock_on_hand":   float(r["stock_on_hand"]),
                "trend_score":     float(r["trend_score"]),
                "momentum_score":  float(r["momentum_score"]),
                "recommended_qty": qty,
            })

    if recommendations:
        await execute(
            "DELETE FROM inventory_recommendations WHERE snapshot_date = $1",
            snapshot_date,
        )
        await bulk_insert("inventory_recommendations", recommendations)
        logger.info(
            f"compute_inventory_recommendations({snapshot_date}): "
            f"{len(recommendations)} recommendations saved"
        )

    return recommendations
