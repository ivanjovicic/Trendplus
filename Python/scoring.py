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
from math import log2, sqrt
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

# Reliability: groups with fewer than this many (deduplicated) occurrences get penalised
MIN_OCCURRENCES            = 2
SINGLE_APPEARANCE_PENALTY  = 0.60   # ×score when occurrences < MIN_OCCURRENCES
SINGLE_SOURCE_PENALTY      = 0.80   # ×score when only 1 unique source
ANTI_GAMING_PENALTY        = 0.90   # New: penalize excessive top ranks from one source

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

_STRIP_RE = re.compile(r"[^a-z0-9 ]")

# Words that carry no product-identity signal – stripping these prevents
# "Nike Air Max 270 Damen" and "Nike Air Max 270 Women" from creating
# separate groups.
_STOP_TOKENS = frozenset({
    # gender
    "damen", "herren", "women", "men", "woman", "man", "ladies", "girls",
    "boys", "femme", "homme", "noi", "barbati", "damske", "panske",
    "nok", "herre", "dame", "femmes", "hommes",
    # Hungarian gender
    "noi", "ferfi",
    # generic adjectives
    "new", "sale", "original", "official",
})


def _norm(s: Any) -> str:
    return _STRIP_RE.sub("", str(s or "").lower()).strip()


def _significant_tokens(name: str) -> List[str]:
    """Tokenise, drop stop words, sort — gives order-invariant identity."""
    return sorted(
        t for t in _norm(name).split()
        if t and t not in _STOP_TOKENS
    )


def _extract_id(item: Dict[str, Any]) -> Optional[str]:
    """Best stable identifier: SKU → numeric product ID → None."""
    for field in ("sku", "SKU", "productId", "product_id", "articleId"):
        v = item.get(field)
        if v and str(v).strip():
            return _norm(str(v))
    # Try to pull a numeric ID from the product URL
    url = str(item.get("url") or item.get("link") or "")
    m = re.search(r"[/-](\d{6,})", url)   # at least 6 digits to avoid page numbers
    if m:
        return m.group(1)
    return None


def _match_key(item: Dict[str, Any]) -> str:
    """
    Stable group key.  Priority:
      1. brand + SKU/productId  (most stable)
      2. brand + sorted significant name tokens  (order-invariant, gender-neutral)
    """
    brand  = _norm(item.get("brand") or "")
    pid    = _extract_id(item)
    if pid:
        return f"{brand}|id:{pid}"
    tokens = _significant_tokens(item.get("name") or "")
    return f"{brand}|{' '.join(tokens)}"


def build_canonical_key(item: Dict[str, Any]) -> str:
    """
    Public helper for persistence layer.
    Keeps canonical key generation consistent with scorer grouping.
    """
    return _match_key(item)


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


def _log_price_bonus(price: float, all_prices: List[float]) -> float:
    """Logarithmic price-position bonus."""
    if not all_prices or len(all_prices) < 3:
        return 0.0
    sorted_prices = sorted(all_prices)
    n = len(sorted_prices)
    lower = int(0.3 * n)
    upper = int(0.6 * n)
    if lower <= price <= upper:
        return PRICE_POS_BONUS * log2(1 + (price - lower) / (upper - lower))
    return 0.0


# ---------------------------------------------------------------------------
# Main scorer
# ---------------------------------------------------------------------------

def _dynamic_rank_score(rank: int, total_items: int) -> float:
    """Dynamic rank decay based on total items."""
    return 1.0 / log2(rank + 1) * sqrt(total_items / (rank + 1))

def _bayesian_reliability(occurrences: int, prior: float = 0.5, weight: int = 5) -> float:
    """Bayesian reliability adjustment."""
    return (occurrences * prior + weight) / (occurrences + weight)

def _smoothed_entropy(source_counts: Dict[str, int], total_sources: int) -> float:
    """Entropy with smoothing for small sample sizes."""
    total = sum(source_counts.values())
    if total == 0:
        return 0.0
    entropy = -sum((c / total) * log2(c / total) for c in source_counts.values() if c > 0)
    return entropy / log2(total_sources) if total_sources > 1 else 0.0

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

    # ── Phase 1b: normalize rank within each (source, market) sub-batch ───────
    # Sort items per source+market by their reported rank, then reassign
    # 1-based sequential ranks.  This removes the hardcoded page-size dependency
    # and makes rank fair regardless of whether the scraper uses local or global
    # rank numbers.
    from itertools import groupby
    sm_buckets: Dict[tuple, List] = defaultdict(list)
    for idx, item in enumerate(all_items):
        sm_key = (_source(item), _market(item))
        raw_rank = int(item.get("rank") or item.get("position") or (idx + 1))
        sm_buckets[sm_key].append((raw_rank, idx, item))

    # Build a lookup: original list index → normalized rank
    normalized_rank: Dict[int, int] = {}
    for bucket in sm_buckets.values():
        for norm_pos, (_, orig_idx, _item) in enumerate(
            sorted(bucket, key=lambda t: t[0]), start=1
        ):
            normalized_rank[orig_idx] = norm_pos

    # ── Phase 2: group items (exact then fuzzy, with source+market dedup) ─────
    groups: Dict[str, Dict[str, Any]] = {}
    group_keys_ordered: List[str] = []  # insertion-order list for fuzzy search

    for idx, item in enumerate(all_items):
        exact_key = _match_key(item)
        brand     = _norm(item.get("brand") or "")
        source    = _source(item)
        market    = _market(item)
        sm_pair   = (source, market)

        # Resolve group key (exact first, then fuzzy)
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
                    "seen_sm":       {},   # (source, market) → best (rank, item)
                    "score":         0.0,
                    "dedup_skipped": 0,
                }
                group_keys_ordered.append(resolved_key)

        rank = normalized_rank.get(idx, idx + 1)
        rs = _dynamic_rank_score(rank, len(all_items))
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
        g["score"] += item_score
        g["items"].append(item)
        g["sources"].add(source)
        g["source_counts"][_source(item)] += 1

    # ── Phase 3: group-level scoring ────────────────────────────────────────────
    max_entropy = log2(len(SOURCE_WEIGHT))   # log2(4) ≈ 2.0
    results: List[Dict[str, Any]] = []

    for key, g in groups.items():
        entropy = _smoothed_entropy(g["source_counts"], len(SOURCE_WEIGHT))
        reliability = _bayesian_reliability(len(g["items"]))
        price_bonus = _log_price_bonus(g["score"], all_prices)
        final_score = g["score"] * (1 + entropy * ENTROPY_BONUS_MAX) * reliability + price_bonus

        results.append({
            "key": key,
            "score": final_score,
            "items": g["items"],
        })

    results.sort(key=lambda x: x["score"], reverse=True)
    return results[:top_n]
