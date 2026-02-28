from __future__ import annotations

import re
from collections import defaultdict
from math import log2, sqrt, exp
from statistics import median, mean, pstdev
from typing import Any, Dict, List, Optional, Tuple
from datetime import datetime, timezone

try:
    from rapidfuzz import fuzz as _fuzz
    _FUZZY_AVAILABLE = True
except ImportError:
    _FUZZY_AVAILABLE = False

# ── Konfig ────────────────────────────────────────────────────────────────────

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

CROSS_SOURCE_BONUS  = 0.40   # per additional unique source
CROSS_MARKET_BONUS  = 0.15   # per additional unique market

SALE_BONUS          = 0.10
NEW_ARRIVAL_BONUS   = 0.20
NO_IMAGE_PENALTY    = 0.50   # ×0.5 bez slike

FUZZY_THRESHOLD     = 82

PRICE_POS_BONUS     = 0.15
ENTROPY_BONUS_MAX   = 0.25

MIN_OCCURRENCES            = 2
SINGLE_APPEARANCE_PENALTY  = 0.60
SINGLE_SOURCE_PENALTY      = 0.80
ANTI_GAMING_PENALTY        = 0.90
ANTI_GAMING_THRESHOLD      = 0.70   # >70% iz jednog izvora

BRAND_DIVERSITY_STRENGTH   = 0.35   # 0=no effect, 1=full effect

# time-decay (na nivou item-a)
TIME_HALF_LIFE_DAYS        = 14.0   # posle ~2 nedelje score prepolovljen
MIN_TIME_FACTOR            = 0.40   # ne padni ispod ovoga

# momentum (na nivou grupe)
MOMENTUM_WEIGHT            = 0.20   # koliko jako momentum utiče

_STRIP_RE = re.compile(r"[^a-z0-9 ]")

_STOP_TOKENS = frozenset({
    "damen", "herren", "women", "men", "woman", "man", "ladies", "girls",
    "boys", "femme", "homme", "noi", "barbati", "damske", "panske",
    "nok", "herre", "dame", "femmes", "hommes",
    "noi", "ferfi",
    "new", "sale", "original", "official",
})

# ── Normalizacija / identitet ────────────────────────────────────────────────

def _norm(s: Any) -> str:
    return _STRIP_RE.sub("", str(s or "").lower()).strip()


def _significant_tokens(name: str) -> List[str]:
    return sorted(
        t for t in _norm(name).split()
        if t and t not in _STOP_TOKENS
    )


def _extract_id(item: Dict[str, Any]) -> Optional[str]:
    for field in ("sku", "SKU", "productId", "product_id", "articleId"):
        v = item.get(field)
        if v and str(v).strip():
            return _norm(str(v))
    url = str(item.get("url") or item.get("link") or "")
    m = re.search(r"[/-](\d{6,})", url)
    if m:
        return m.group(1)
    return None


def _match_key(item: Dict[str, Any]) -> str:
    brand = _norm(item.get("brand") or "")
    pid = _extract_id(item)
    if pid:
        return f"{brand}|id:{pid}"
    tokens = _significant_tokens(item.get("name") or "")
    return f"{brand}|{' '.join(tokens)}"


def build_canonical_key(item: Dict[str, Any]) -> str:
    return _match_key(item)


def _source(item: Dict[str, Any]) -> str:
    return (item.get("source") or item.get("sourceName") or "").lower().strip()


def _market(item: Dict[str, Any]) -> str:
    return (item.get("market") or item.get("country") or "DE").upper().strip()


def _price(item: Dict[str, Any]) -> Optional[float]:
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


def _item_timestamp(item: Dict[str, Any]) -> Optional[datetime]:
    """
    Pokušaj da izvučeš timestamp:
      - epoch seconds (int/float) u polju "ts" ili "timestamp"
      - ISO string u "timestamp" ili "seenAt"
    Ako nema, vrati None → bez time-decay.
    """
    v = item.get("ts") or item.get("timestamp") or item.get("seenAt")
    if v is None:
        return None
    try:
        # epoch seconds?
        if isinstance(v, (int, float)):
            return datetime.fromtimestamp(float(v), tz=timezone.utc)
        # ISO string
        s = str(v)
        return datetime.fromisoformat(s.replace("Z", "+00:00"))
    except Exception:
        return None


# ── Rank scoring ─────────────────────────────────────────────────────────────

def _dynamic_rank_score(rank: int, total_items: int) -> float:
    rank = max(rank, 1)
    total_items = max(total_items, 1)
    return (1.0 / log2(rank + 1)) * sqrt(total_items / (rank + 1))


def _z_adjust(rank: int, ranks_in_bucket: List[int]) -> float:
    """
    Mali z-score tweak po source+market bucket-u.
    Top pozicije (ispod proseka) dobijaju mali boost, lošije mali minus.
    Efekat je blag (±5%).
    """
    if not ranks_in_bucket or len(ranks_in_bucket) < 3:
        return 1.0
    mu = mean(ranks_in_bucket)
    sigma = pstdev(ranks_in_bucket) or 1.0
    z = (rank - mu) / sigma
    # negativan z (bolji rank) → boost do +5%, pozitivan → do −5%
    z = max(-3.0, min(3.0, z))
    return 1.0 + (-z * 0.05)  # z<0 → +, z>0 → −


# ── Fuzzy grupisanje ────────────────────────────────────────────────────────

def _fuzzy_find_group(
    key: str,
    brand: str,
    brand_groups: Dict[str, List[str]],
) -> Optional[str]:
    if not _FUZZY_AVAILABLE:
        return None
    brand_norm = _norm(brand)
    candidates = brand_groups.get(brand_norm)
    if not candidates:
        return None

    key_name = key.split("|", 1)[1] if "|" in key else key
    best_score = 0
    best_key: Optional[str] = None

    for candidate in candidates:
        cand_name = candidate.split("|", 1)[1] if "|" in candidate else candidate
        score = _fuzz.token_set_ratio(key_name, cand_name)
        if score > best_score:
            best_score = score
            best_key = candidate

    return best_key if best_score >= FUZZY_THRESHOLD else None


# ── Entropy / reliability / penali ──────────────────────────────────────────

def _shannon_entropy(source_counts: Dict[str, int]) -> float:
    total = sum(source_counts.values())
    if total == 0:
        return 0.0
    return -sum(
        (c / total) * log2(c / total)
        for c in source_counts.values()
        if c > 0
    )


def _smoothed_entropy(source_counts: Dict[str, int], total_sources: int) -> float:
    if total_sources <= 1:
        return 0.0
    H = _shannon_entropy(source_counts)
    max_H = log2(total_sources)
    if max_H == 0:
        return 0.0
    return min(1.0, max(0.0, H / max_H))


def _bayesian_reliability(occurrences: int, prior: float = 0.6, weight: int = 3) -> float:
    occurrences = max(0, occurrences)
    return (occurrences + prior * weight) / (occurrences + weight)


def _brand_diversity_factor(brand: str, brand_counts: Dict[str, int]) -> float:
    count = max(1, brand_counts.get(brand, 1))
    base = 1.0 / sqrt(count)  # više grupa → manji base
    return 1.0 - BRAND_DIVERSITY_STRENGTH * (1.0 - base)


def _anti_gaming_factor(source_counts: Dict[str, int]) -> float:
    total = sum(source_counts.values())
    if total == 0:
        return 1.0
    max_share = max(source_counts.values()) / total
    if max_share > ANTI_GAMING_THRESHOLD:
        return ANTI_GAMING_PENALTY
    return 1.0


# ── Price positioning ───────────────────────────────────────────────────────

def _group_price_bonus(group_prices: List[float], all_prices: List[float]) -> float:
    if not group_prices or not all_prices or len(all_prices) < 5:
        return 0.0

    g_price = median(group_prices)
    sorted_p = sorted(all_prices)
    n = len(sorted_p)
    if n < 2:
        return 0.0

    nearest_idx = min(range(n), key=lambda i: abs(sorted_p[i] - g_price))
    percentile = nearest_idx / (n - 1)  # 0..1
    distance_from_mid = abs(percentile - 0.5)   # 0=mid, 0.5=extreme
    return max(0.0, PRICE_POS_BONUS * (1.0 - distance_from_mid / 0.5))


# ── Time-decay i momentum ───────────────────────────────────────────────────

def _time_factor(item: Dict[str, Any], now: datetime) -> float:
    ts = _item_timestamp(item)
    if not ts:
        return 1.0
    age_days = (now - ts).total_seconds() / 86400.0
    if age_days <= 0:
        return 1.0
    # exponential decay: t=half-life → factor ~0.5
    factor = exp(-age_days * (0.693 / TIME_HALF_LIFE_DAYS))
    return max(MIN_TIME_FACTOR, factor)


def _momentum_factor(current_raw: float, prev: Optional[float]) -> float:
    if prev is None or prev <= 0 or current_raw <= 0:
        return 1.0
    delta = current_raw - prev
    rel = delta / (abs(prev) + 1e-6)
    rel = max(-1.0, min(1.0, rel))  # clamp
    return 1.0 + MOMENTUM_WEIGHT * rel


# ── Confidence score (za UI / debugging) ────────────────────────────────────

def _confidence(
    occurrences: int,
    unique_sources: int,
    entropy_ratio: float,
) -> float:
    # grub ali intuitivan score 0–1
    occ_term = min(1.0, occurrences / 5.0)        # saturira na 5 pojavljivanja
    src_term = min(1.0, unique_sources / 3.0)     # 3+ izvora → max
    ent_term = entropy_ratio                     # već je 0–1
    return max(0.0, min(1.0, 0.4 * occ_term + 0.3 * src_term + 0.3 * ent_term))


# ── Glavna funkcija ─────────────────────────────────────────────────────────

def compute_top10(
    all_items: List[Dict[str, Any]],
    requested_type: Optional[str] = None,
    top_n: int = 10,
    previous_scores: Optional[Dict[str, float]] = None,
    now_ts: Optional[datetime] = None,
    product_social_scores: Optional[Dict[str, float]] = None,
    prev_social_scores: Optional[Dict[str, float]] = None,
) -> List[Dict[str, Any]]:
    """
    Vraća top-N grupisanih proizvoda sa:
      - "key"   → canonical ključ grupe
      - "score" → finalni skor
      - "items" → sve sirove pojave
      - "meta"  → dodatni signali (confidence, entropy, itd.)
    """
    if requested_type:
        filtered = [
            it for it in all_items
            if str(it.get("type") or "").lower() == requested_type.lower()
        ]
        if filtered:
            all_items = filtered

    if not all_items:
        return []

    now = now_ts or datetime.now(timezone.utc)

    # sve cene u batch-u
    all_prices: List[float] = [
        p for item in all_items
        if (p := _price(item)) is not None and p > 0
    ]

    # rank normalizacija po (source,market) i priprema za z-score
    sm_buckets: Dict[Tuple[str, str], List[Tuple[int, int]]] = defaultdict(list)
    for idx, item in enumerate(all_items):
        sm_key = (_source(item), _market(item))
        raw_rank = int(item.get("rank") or item.get("position") or (idx + 1))
        sm_buckets[sm_key].append((raw_rank, idx))

    normalized_rank: Dict[int, int] = {}
    bucket_ranks: Dict[Tuple[str, str], List[int]] = {}
    for sm_key, bucket in sm_buckets.items():
        bucket_sorted = sorted(bucket, key=lambda t: t[0])
        ranks = []
        for norm_pos, (_, orig_idx) in enumerate(bucket_sorted, start=1):
            normalized_rank[orig_idx] = norm_pos
            ranks.append(norm_pos)
        bucket_ranks[sm_key] = ranks

    # grupisanje: exact → fuzzy
    groups: Dict[str, Dict[str, Any]] = {}
    brand_groups: Dict[str, List[str]] = defaultdict(list)  # brand → [group_keys]

    for idx, item in enumerate(all_items):
        exact_key = _match_key(item)
        brand_norm = _norm(item.get("brand") or "")
        source = _source(item)
        market = _market(item)
        sm_pair = (source, market)

        if exact_key in groups:
            resolved_key = exact_key
        else:
            fuzzy_key = _fuzzy_find_group(exact_key, brand_norm, brand_groups)
            if fuzzy_key:
                resolved_key = fuzzy_key
            else:
                resolved_key = exact_key
                groups[resolved_key] = {
                    "brand":         brand_norm,
                    "items":         [],
                    "sources":       set(),
                    "markets":       set(),
                    "source_counts": defaultdict(int),
                    "seen_sm":       {},     # (source,market) → (best_score, best_item)
                    "prices":        [],
                    "score":         0.0,
                    "dedup_skipped": 0,
                }
                brand_groups[brand_norm].append(resolved_key)

        rank = normalized_rank.get(idx, idx + 1)
        ranks_in_bucket = bucket_ranks.get(sm_pair, [rank])
        base_rs = _dynamic_rank_score(rank, len(ranks_in_bucket))
        z_factor = _z_adjust(rank, ranks_in_bucket)
        item_score = base_rs * z_factor * SOURCE_WEIGHT.get(source, 0.50) * MARKET_WEIGHT.get(market, 0.50)

        if not _has_image(item):
            item_score *= NO_IMAGE_PENALTY
        if item.get("isNew") or item.get("is_new"):
            item_score *= (1 + NEW_ARRIVAL_BONUS)
        if item.get("sale") or item.get("onSale") or item.get("is_sale"):
            item_score *= (1 + SALE_BONUS)

        # time-decay
        item_score *= _time_factor(item, now)

        g = groups[resolved_key]
        g["items"].append(item)
        g["sources"].add(source)
        g["markets"].add(market)
        g["source_counts"][source] += 1

        if (p := _price(item)) is not None and p > 0:
            g["prices"].append(p)

        # dedup po (source, market) → u skor ulazi samo najbolji
        if sm_pair in g["seen_sm"]:
            prev_score, prev_item = g["seen_sm"][sm_pair]
            if item_score > prev_score:
                g["score"] += (item_score - prev_score)
                g["seen_sm"][sm_pair] = (item_score, item)
            else:
                g["dedup_skipped"] += 1
        else:
            g["score"] += item_score
            g["seen_sm"][sm_pair] = (item_score, item)

    # brand counts za diversity faktor
    brand_counts: Dict[str, int] = defaultdict(int)
    for g in groups.values():
        brand_counts[g["brand"]] += 1

    total_sources = len(SOURCE_WEIGHT) or 1
    results: List[Dict[str, Any]] = []

    for key, g in groups.items():
        base_sum = g["score"]
        occurrences = len(g["items"])
        unique_sources = len(g["sources"])
        unique_markets = len(g["markets"])

        entropy_ratio = _smoothed_entropy(g["source_counts"], total_sources)
        reliability = _bayesian_reliability(occurrences)
        brand_factor = _brand_diversity_factor(g["brand"], brand_counts)
        anti_gaming = _anti_gaming_factor(g["source_counts"])
        price_bonus = _group_price_bonus(g["prices"], all_prices)

        source_boost = 1.0 + CROSS_SOURCE_BONUS * max(0, unique_sources - 1)
        market_boost = 1.0 + CROSS_MARKET_BONUS * max(0, unique_markets - 1)
        entropy_boost = 1.0 + entropy_ratio * ENTROPY_BONUS_MAX

        raw_score = (
            base_sum
            * source_boost
            * market_boost
            * entropy_boost
            * reliability
            * brand_factor
            * anti_gaming
        )

        if occurrences < MIN_OCCURRENCES:
            raw_score *= SINGLE_APPEARANCE_PENALTY
        if unique_sources == 1:
            raw_score *= SINGLE_SOURCE_PENALTY

        raw_score += price_bonus

        # Social multiplier
        social_score = product_social_scores.get(key, 0) if product_social_scores else 0
        prev_social = prev_social_scores.get(key) if prev_social_scores else None
        social_mult = compute_social_multiplier(social_score, prev_social)

        final_score = raw_score * social_mult

        conf = _confidence(occurrences, unique_sources, entropy_ratio)

        results.append({
            "key": key,
            "score": final_score,
            "items": g["items"],
            "meta": {
                "brand": g["brand"],
                "sources": list(g["sources"]),
                "markets": list(g["markets"]),
                "occurrences": occurrences,
                "entropy_ratio": entropy_ratio,
                "reliability": reliability,
                "brand_factor": brand_factor,
                "anti_gaming": anti_gaming,
                "price_bonus": price_bonus,
                "social_score": social_score,
                "social_multiplier": social_mult,
                "confidence": conf,
            },
        })

    results.sort(key=lambda x: x["score"], reverse=True)
    return results[:top_n]


# ── Social Trend Integration ───────────────────────────────────────────────

MAX_SOCIAL_BOOST = 0.20
MAX_MOMENTUM_BOOST = 0.15

def compute_social_multiplier(
    current_social: float,
    previous_social: Optional[float] = None
) -> float:
    """
    Combines absolute social trend and momentum.
    current_social: 0-100
    previous_social: 0-100 or None
    """
    if current_social <= 0:
        return 1.0

    # Absolute effect
    social_norm = min(1.0, max(0.0, current_social / 100.0))
    abs_effect = sqrt(social_norm)
    abs_multiplier = 1.0 + abs_effect * MAX_SOCIAL_BOOST

    # Momentum effect
    momentum_multiplier = 1.0
    if previous_social is not None and previous_social > 0:
        delta = (current_social - previous_social) / 100.0
        delta = max(-1.0, min(1.0, delta))
        momentum_multiplier = 1.0 + delta * MAX_MOMENTUM_BOOST

    return abs_multiplier * momentum_multiplier