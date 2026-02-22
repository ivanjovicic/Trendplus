"""
scoring.py – Weighted popularity scorer for Trendplus Global Top 10.

Score formula per item:
    itemScore = rank_score × sourceWeight × marketWeight × bonusMultiplier

Score formula per group:
    groupScore = Σ(itemScores)
                 × (1 + CROSS_SOURCE_BONUS × (uniqueSources - 1))
                 × (1 + CROSS_MARKET_BONUS  × (uniqueMarkets  - 1))

The cross-source multiplier is the key signal: a shoe appearing on both
Zalando AND About You is a much stronger trend indicator than the same shoe
appearing on 3 Zalando markets.
"""

from __future__ import annotations

import re
from collections import defaultdict
from typing import Any, Dict, List, Optional

# ---------------------------------------------------------------------------
# Weight config
# ---------------------------------------------------------------------------

SOURCE_WEIGHT: Dict[str, float] = {
    "zalando":   1.00,
    "aboutyou":  0.85,
    "deichmann": 0.70,
    "humanic":   0.60,
}

MARKET_WEIGHT: Dict[str, float] = {
    "DE": 1.00,
    "AT": 0.80,
    "CH": 0.75,
    "HU": 0.55,
    "RO": 0.50,
}

# Bonus per additional unique source / market beyond the first
CROSS_SOURCE_BONUS = 0.40
CROSS_MARKET_BONUS = 0.15

# Item-level bonuses (applied as multipliers on itemScore)
SALE_BONUS        = 0.10
NEW_ARRIVAL_BONUS = 0.20

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

_STRIP_RE = re.compile(r"[^a-z0-9 ]")


def _norm(s: Any) -> str:
    return _STRIP_RE.sub("", str(s or "").lower()).strip()


def _match_key(item: Dict[str, Any]) -> str:
    """Stable group key: normalised brand + first 3 words of name."""
    brand = _norm(item.get("brand") or "")
    name  = " ".join(_norm(item.get("name") or "").split()[:3])
    return f"{brand}|{name}"


def _source(item: Dict[str, Any]) -> str:
    return (item.get("source") or item.get("sourceName") or "").lower().strip()


def _market(item: Dict[str, Any]) -> str:
    return (item.get("market") or item.get("country") or "DE").upper().strip()


def _price(item: Dict[str, Any]) -> Optional[float]:
    """Return price as float regardless of field name."""
    for key in ("priceValue", "price", "Price", "priceEur"):
        v = item.get(key)
        if v is not None:
            try:
                return float(v)
            except (TypeError, ValueError):
                pass
    return None


# ---------------------------------------------------------------------------
# Main scorer
# ---------------------------------------------------------------------------

def compute_top10(
    all_items: List[Dict[str, Any]],
    requested_type: Optional[str] = None,
    top_n: int = 10,
) -> List[Dict[str, Any]]:
    """
    Group items by product identity, compute weighted popularity scores,
    return sorted top-N with full provenance metadata.
    """

    groups: Dict[str, Dict[str, Any]] = defaultdict(lambda: {
        "items":   [],
        "sources": set(),
        "markets": set(),
        "score":   0.0,
    })

    for idx, item in enumerate(all_items):
        key    = _match_key(item)
        source = _source(item)
        market = _market(item)

        # rank: 1-based position within source listing; fall back to list order
        rank  = item.get("rank") or item.get("position") or (idx + 1)
        pages = item.get("totalPages") or 1

        # Inverse-rank score: rank #1 → 1.0, last item on page → ~0.0
        rank_score = max(0.0, 1.0 - (int(rank) - 1) / max(int(pages) * 24, 24))

        sw = SOURCE_WEIGHT.get(source, 0.50)
        mw = MARKET_WEIGHT.get(market, 0.50)
        item_score = rank_score * sw * mw

        if item.get("isNew") or item.get("is_new"):
            item_score *= (1 + NEW_ARRIVAL_BONUS)
        if item.get("sale") or item.get("onSale") or item.get("is_sale"):
            item_score *= (1 + SALE_BONUS)

        g = groups[key]
        g["items"].append(item)
        g["sources"].add(source)
        g["markets"].add(market)
        g["score"] += item_score

    results: List[Dict[str, Any]] = []

    for key, g in groups.items():
        extra_sources = max(0, len(g["sources"]) - 1)
        extra_markets = max(0, len(g["markets"]) - 1)

        final_score = (
            g["score"]
            * (1 + extra_sources * CROSS_SOURCE_BONUS)
            * (1 + extra_markets * CROSS_MARKET_BONUS)
        )

        # Best representative: prefer highest source weight, then market weight
        best: Dict[str, Any] = max(
            g["items"],
            key=lambda i: (
                SOURCE_WEIGHT.get(_source(i), 0.0),
                MARKET_WEIGHT.get(_market(i), 0.0),
            ),
        )

        # Collect price range per market for display
        price_by_market: Dict[str, List[float]] = defaultdict(list)
        for i in g["items"]:
            p = _price(i)
            if p is not None and p > 0:
                price_by_market[_market(i)].append(p)

        price_by_market_out = {
            m: {"min": round(min(ps), 2), "max": round(max(ps), 2)}
            for m, ps in price_by_market.items()
        }

        results.append({
            **best,
            # scoring metadata (overwrite/add)
            "globalScore":   round(final_score, 4),
            "sourcesCount":  len(g["sources"]),
            "marketsCount":  len(g["markets"]),
            "allSources":    sorted(g["sources"]),
            "allMarkets":    sorted(g["markets"]),
            "occurrences":   len(g["items"]),
            "priceByMarket": price_by_market_out,
            "shoeType":      requested_type or best.get("shoeType") or "other",
        })

    results.sort(key=lambda x: x["globalScore"], reverse=True)
    return results[:top_n]
