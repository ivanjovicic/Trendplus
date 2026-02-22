"""
scoring.py – Weighted popularity scorer for Trendplus Global Top 10.

v2 improvements over v1:
  1. Fuzzy product grouping via rapidfuzz
       Same shoe with different translated names (DE/HU/RO) now groups correctly.
  2. Logarithmic rank decay
       Rank #1 vs #2 matters far more than #20 vs #21.
  3. No-image penalty
       Listings without a photo are penalised (likely out-of-stock or bad data).
  4. Price positioning bonus
       Mid-range (30th–70th percentile) products get a bonus – they’re the
       mass-market hits, not niche outliers.
  5. Shannon entropy diversity bonus
       A shoe appearing once on each of 4 sources scores higher than one
       appearing 4 times on the same source.

Full score formula:
  itemScore  = rankScore(log) × sourceWeight × marketWeight
               × imagePenalty × newBonus × saleBonus

  groupScore = Σ(itemScores)
               × (1 + CROSS_SOURCE_BONUS × (uniqueSources − 1))
               × (1 + CROSS_MARKET_BONUS  × (uniqueMarkets  − 1))
               × (1 + entropy_ratio × ENTROPY_BONUS_MAX)
               + pricePositioningBonus
"""

from __future__ import annotations

import re
from collections import defaultdict
from math import log2
from typing import Any, Dict, List, Optional

try:
    from rapidfuzz import fuzz as _fuzz
    _FUZZY_AVAILABLE = True
except ImportError:
    _FUZZY_AVAILABLE = False

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

# Group-level multipliers
CROSS_SOURCE_BONUS  = 0.40   # per additional unique source
CROSS_MARKET_BONUS  = 0.15   # per additional unique market

# Item-level bonuses / penalties (applied as multipliers on itemScore)
SALE_BONUS          = 0.10
NEW_ARRIVAL_BONUS   = 0.20
NO_IMAGE_PENALTY    = 0.50   # items without image get half score

# Fuzzy grouping: minimum similarity score (0–100) to merge two products
FUZZY_THRESHOLD     = 82

# Price positioning: max bonus for a perfectly mid-range price
PRICE_POS_BONUS     = 0.15

# Shannon entropy: max additional multiplier bonus when sources are perfectly distributed
ENTROPY_BONUS_MAX   = 0.25

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

_STRIP_RE = re.compile(r"[^a-z0-9 ]")


def _norm(s: Any) -> str:
    return _STRIP_RE.sub("", str(s or "").lower()).strip()


def _match_key(item: Dict[str, Any]) -> str:
    """Canonical key: normalised brand + first 3 words of name."""
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


def _has_image(item: Dict[str, Any]) -> bool:
    for key in ("image", "imageUrl", "image_url", "ImageUrl"):
        v = item.get(key)
        if v and str(v).strip():
            return True
    return False


def _rank_score(rank: int) -> float:
    """
    Logarithmic rank decay.
    #1 → 1.000 | #2 → 0.631 | #5 → 0.431 | #10 → 0.289 | #24 → 0.189
    """
    return 1.0 / log2(max(rank, 1) + 1)


def _fuzzy_find_group(
    key: str,
    group_keys: List[str],
    brand: str,
) -> Optional[str]:
    """
    Find a matching existing group key using rapidfuzz token_set_ratio.
    Only compares within the same brand to avoid false positives.
    """
    if not _FUZZY_AVAILABLE or not group_keys:
        return None

    brand_norm = _norm(brand)
    # Only compare against groups with the same brand prefix
    candidates = [k for k in group_keys if k.startswith(f"{brand_norm}|")]
    if not candidates:
        return None

    key_name   = key.split("|", 1)[1] if "|" in key else key
    best_score = 0
    best_key: Optional[str] = None

    for candidate in candidates:
        cand_name = candidate.split("|", 1)[1] if "|" in candidate else candidate
        score = _fuzz.token_set_ratio(key_name, cand_name)
        if score > best_score:
            best_score = score
            best_key   = candidate

    return best_key if best_score >= FUZZY_THRESHOLD else None


def _shannon_entropy(source_counts: Dict[str, int]) -> float:
    """H = -Σ p_i * log2(p_i). Range: 0 (monopoly) → log2(N) (equal share)."""
    total = sum(source_counts.values())
    if total == 0:
        return 0.0
    return -sum(
        (c / total) * log2(c / total)
        for c in source_counts.values()
        if c > 0
    )


def _price_position_bonus(price: float, all_prices: List[float]) -> float:
    """
    Returns bonus in [0, PRICE_POS_BONUS].
    Prices in the 30th–70th percentile of the batch get the full bonus;
    outliers (very cheap or very expensive) get no bonus.
    """
    if not all_prices or len(all_prices) < 3:
        return 0.0
    sorted_p    = sorted(all_prices)
    n           = len(sorted_p)
    nearest_idx = min(range(n), key=lambda i: abs(sorted_p[i] - price))
    percentile  = nearest_idx / n
    distance_from_mid = abs(percentile - 0.5)   # 0 = perfect mid, 0.5 = extreme
    return max(0.0, PRICE_POS_BONUS * (1.0 - distance_from_mid / 0.5))


# ---------------------------------------------------------------------------
# Main scorer
# ---------------------------------------------------------------------------

def compute_top10(
    all_items: List[Dict[str, Any]],
    requested_type: Optional[str] = None,
    top_n: int = 10,
) -> List[Dict[str, Any]]:
    """
    Group items by product identity (fuzzy), compute weighted popularity
    scores, return sorted top-N with full provenance + score breakdown.
    """

    # ── Phase 1: collect all batch prices for price-positioning ───────────────
    all_prices: List[float] = [
        p for item in all_items
        if (p := _price(item)) is not None and p > 0
    ]

    # ── Phase 2: group items (exact then fuzzy) ────────────────────────────────
    groups: Dict[str, Dict[str, Any]] = {}
    group_keys_ordered: List[str] = []  # insertion-order list for fuzzy search

    for idx, item in enumerate(all_items):
        exact_key = _match_key(item)
        brand     = _norm(item.get("brand") or "")
        source    = _source(item)
        market    = _market(item)

        # Resolve group key
        if exact_key in groups:
            resolved_key = exact_key
        else:
            fuzzy_key = _fuzzy_find_group(exact_key, group_keys_ordered, brand)
            if fuzzy_key:
                resolved_key = fuzzy_key
            else:
                resolved_key = exact_key
                groups[resolved_key] = {
                    "items":         [],
                    "sources":       set(),
                    "markets":       set(),
                    "source_counts": defaultdict(int),
                    "score":         0.0,
                }
                group_keys_ordered.append(resolved_key)

        rank = int(item.get("rank") or item.get("position") or (idx + 1))

        # Base item score: log rank × source weight × market weight
        rs         = _rank_score(rank)
        item_score = rs * SOURCE_WEIGHT.get(source, 0.50) * MARKET_WEIGHT.get(market, 0.50)

        # Penalties
        if not _has_image(item):
            item_score *= NO_IMAGE_PENALTY

        # Bonuses
        if item.get("isNew") or item.get("is_new"):
            item_score *= (1 + NEW_ARRIVAL_BONUS)
        if item.get("sale") or item.get("onSale") or item.get("is_sale"):
            item_score *= (1 + SALE_BONUS)

        g = groups[resolved_key]
        g["items"].append(item)
        g["sources"].add(source)
        g["markets"].add(market)
        g["source_counts"][source] += 1
        g["score"] += item_score

    # ── Phase 3: group-level scoring ────────────────────────────────────────────
    max_entropy = log2(len(SOURCE_WEIGHT))   # log2(4) ≈ 2.0
    results: List[Dict[str, Any]] = []

    for key, g in groups.items():
        extra_sources = max(0, len(g["sources"]) - 1)
        extra_markets = max(0, len(g["markets"]) - 1)

        # Cross-source / cross-market multipliers
        cross_mult = (
            (1 + extra_sources * CROSS_SOURCE_BONUS)
            * (1 + extra_markets * CROSS_MARKET_BONUS)
        )

        # Shannon entropy diversity bonus
        entropy       = _shannon_entropy(dict(g["source_counts"]))
        entropy_ratio = entropy / max_entropy if max_entropy > 0 else 0.0
        entropy_mult  = 1 + entropy_ratio * ENTROPY_BONUS_MAX

        # Price positioning bonus (additive, not multiplicative)
        group_prices = [p for i in g["items"] if (p := _price(i)) is not None and p > 0]
        rep_price    = group_prices[0] if group_prices else None
        price_bonus  = _price_position_bonus(rep_price, all_prices) if rep_price else 0.0

        final_score = g["score"] * cross_mult * entropy_mult + price_bonus

        # Best representative: highest source weight, then market weight
        best: Dict[str, Any] = max(
            g["items"],
            key=lambda i: (
                SOURCE_WEIGHT.get(_source(i), 0.0),
                MARKET_WEIGHT.get(_market(i), 0.0),
            ),
        )

        # Price range per market
        price_by_market: Dict[str, List[float]] = defaultdict(list)
        for i in g["items"]:
            p = _price(i)
            if p is not None and p > 0:
                price_by_market[_market(i)].append(p)

        price_by_market_out = {
            m: {"min": round(min(ps), 2), "max": round(max(ps), 2)}
            for m, ps in price_by_market.items()
        }

        # Transparent score breakdown (shown in UI tooltip / debug)
        score_breakdown = {
            "baseScore":       round(g["score"], 4),
            "crossSourceMult": round(1 + extra_sources * CROSS_SOURCE_BONUS, 4),
            "crossMarketMult": round(1 + extra_markets * CROSS_MARKET_BONUS, 4),
            "entropyBonus":    round((entropy_mult - 1), 4),
            "priceBonus":      round(price_bonus, 4),
            "entropyValue":    round(entropy, 4),
            "imagePenalized":  not _has_image(best),
            "fuzzyGrouped":    _FUZZY_AVAILABLE,
        }

        results.append({
            **best,
            "globalScore":    round(final_score, 4),
            "sourcesCount":   len(g["sources"]),
            "marketsCount":   len(g["markets"]),
            "allSources":     sorted(g["sources"]),
            "allMarkets":     sorted(g["markets"]),
            "occurrences":    len(g["items"]),
            "priceByMarket":  price_by_market_out,
            "shoeType":       requested_type or best.get("shoeType") or "other",
            "scoreBreakdown": score_breakdown,
        })

    results.sort(key=lambda x: x["globalScore"], reverse=True)
    return results[:top_n]
