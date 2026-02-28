"""
trend_engine/core.py
====================
Čista scoring logika — bez DB, bez HTTP, bez asyncio.

Ulaz:  Lista ScrapedItem  (iz scraper/schema.py)
Izlaz: Lista TrendGroupResult sortirana po final_score opadajuće

Ovo je referentna implementacija. Iste formule portuj 1:1 u C# radnika
(vidi Application/Analytics/Services/TrendScoringService.cs).
"""

from __future__ import annotations

import math
import re
import sys
import os
from collections import Counter
from dataclasses import dataclass, asdict, field
from typing import Any, Dict, List, Optional

# ── Import ScrapedItem iz postojećeg projekta ─────────────────────────────────
# Dodajemo Python root na path ako ovaj modul pozovemo direktno
_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if _ROOT not in sys.path:
    sys.path.insert(0, _ROOT)

from scraper.schema import ScrapedItem  # noqa: E402

# Rapidfuzz je opcionalan — bez njega radi samo exact grupisanje
try:
    from rapidfuzz import fuzz as _fuzz
    _FUZZY_AVAILABLE = True
except ImportError:
    _FUZZY_AVAILABLE = False


# ═══════════════════════════════════════════════════════════════════════════════
#  KONFIGURACIJA TEŽINA
#  Svaka konstanta ima direktan pandan u TrendScoringService.cs
# ═══════════════════════════════════════════════════════════════════════════════

# Pouzdanost izvora — koliko vjerujemo rangiranju pojedinog scrapora
SOURCE_WEIGHT = {
    "zalando":   1.00,
    "aboutyou":  0.90,
    "deichmann": 0.75,
    "humanic":   0.70,
}

# Normalize weights to sum to 1
total_source_weight = sum(SOURCE_WEIGHT.values())
SOURCE_WEIGHT = {k: v / total_source_weight for k, v in SOURCE_WEIGHT.items()}

# Važnost tržišta — veće = relevantnije za EU trend projekciju
MARKET_WEIGHT = {
    "DE": 1.00,
    "AT": 0.85,
    "CH": 0.80,
    "HU": 0.60,
    "RO": 0.55,
}

# Normalize weights to sum to 1
total_market_weight = sum(MARKET_WEIGHT.values())
MARKET_WEIGHT = {k: v / total_market_weight for k, v in MARKET_WEIGHT.items()}

NO_IMAGE_PENALTY    = 0.50   # bez slike → score × 0.5
NEW_ARRIVAL_BONUS   = 0.20   # novi artikal → score × 1.2
SALE_BONUS          = 0.10   # na popustu  → score × 1.1

# Bonus po svakom dodatnom izvoru/tržištu izvan prvog
CROSS_SOURCE_BONUS  = 0.40   # npr. 2 izvora → ×1.40, 3 → ×1.80
CROSS_MARKET_BONUS  = 0.15   # npr. 2 tržišta → ×1.15, 3 → ×1.30

# Maksimalni entropijski bonus — nagrađujemo ravnomjernu distribuciju po izvorima
ENTROPY_BONUS_MAX   = 0.25   # max +25%

# Social boost (vidjeti apply_social_boost)
MAX_SOCIAL_WEIGHT   = 0.30   # social može dodati max 30% na final_score

# Fuzzy prag (Rapidfuzz token_set_ratio)
FUZZY_THRESHOLD     = 82

# Stop tokeni — ne razlikuju isti model po rodu/jeziku
_STOP_TOKENS = frozenset({
    "damen", "herren", "women", "men", "woman", "man", "ladies",
    "girls", "boys", "femme", "homme",
    "noi", "barbati", "damske", "panske",
    "new", "sale", "original", "official",
})

_STRIP_RE = re.compile(r"[^a-z0-9 ]")


# ═══════════════════════════════════════════════════════════════════════════════
#  OUTPUT MODEL
# ═══════════════════════════════════════════════════════════════════════════════

@dataclass
class TrendGroupResult:
    """
    Rezultat za jedan model (grupu scraped itema).

    C# ekvivalent: TrendProductSnapshotDto / TrendGroupResult record
    Ovo se upisuje u DB kao TrendProductSnapshot red.
    """
    canonical_key: str       # jedinstven ID modela — source of truth za join u C#
    brand: str
    name: str
    markets: List[str]       # sva tržišta na kojima se pojavljuje
    sources: List[str]       # svi izvori na kojima se pojavljuje

    total_occurrences: int   # ukupan broj ScrapedItem u grupi
    unique_sources: int      # broj različitih izvora
    unique_markets: int      # broj različitih tržišta

    base_score: float        # suma itemScore prije grupnih multiplikatora
    final_score: float       # konačni score (upiši u DB kao "score")

    # Breakdown koji možeš koristiti u BI / debug
    source_counts: Dict[str, int]
    market_counts: Dict[str, int]


# ═══════════════════════════════════════════════════════════════════════════════
#  HELPERS — normalizacija i ključevi
# ═══════════════════════════════════════════════════════════════════════════════

def _norm_text(s: str | None) -> str:
    """Lowercase, ukloni sve osim slova/cifara/razmaka."""
    if not s:
        return ""
    return _STRIP_RE.sub("", s.lower()).strip()


def _significant_tokens(name: str) -> List[str]:
    """Sortirani tokeni bez stop-reči — osnova fallback ključa."""
    return sorted(
        t for t in _norm_text(name).split()
        if t and t not in _STOP_TOKENS
    )


def build_canonical_key(item: ScrapedItem) -> str:
    """
    Stabilan ključ proizvoda koji prepoznaje isti model s različitih izvora.

    Prioritet:
      1. brand + productId  (ako postoji)
      2. brand + sku        (ako postoji)
      3. brand + ID iz URL-a  (regex: 6+ cifara u putanji)
      4. brand + sortirani značajni tokeni iz naziva (fallback)

    C# ekvivalent: TrendScoringService.BuildCanonicalKey(...)
    """
    brand_norm = _norm_text(item.brand)

    # Prioritet 1: productId
    if item.productId:
        return f"{brand_norm}|id:{_norm_text(item.productId)}"

    # Prioritet 2: sku
    if item.sku:
        return f"{brand_norm}|sku:{_norm_text(item.sku)}"

    # Prioritet 3: ID iz URL-a (npr. /p/nike-air-max-270-12345678.html)
    m = re.search(r"/([a-z0-9-]*?)(\d{6,})(?:\.html?)?", item.url or "", flags=re.IGNORECASE)
    if m:
        return f"{brand_norm}|id:{m.group(2)}"

    # Fallback: imenski ključ
    tokens = _significant_tokens(item.name)
    return f"{brand_norm}|{' '.join(tokens)}"


def _fuzzy_find_group(
    key: str,
    existing_keys: List[str],
    brand_norm: str,
) -> Optional[str]:
    """
    Fuzzy fallback: ako exact key nije u grupama, provjeri postoji li
    dovoljno sličan ključ istog brenda.

    Vraća best_key ako score ≥ FUZZY_THRESHOLD, inače None.
    C# ekvivalent: TrendScoringService.FuzzyFindGroup(...) — opcionalno,
    u C# je dovoljno raditi samo exact key.
    """
    if not _FUZZY_AVAILABLE or not existing_keys:
        return None

    prefix = f"{brand_norm}|"
    candidates = [k for k in existing_keys if k.startswith(prefix)]
    if not candidates:
        return None

    key_name = key.split("|", 1)[1] if "|" in key else key
    best_score = 0
    best_key: Optional[str] = None

    for cand in candidates:
        cand_name = cand.split("|", 1)[1] if "|" in cand else cand
        score = _fuzz.token_set_ratio(key_name, cand_name)
        if score > best_score:
            best_score = score
            best_key = cand

    return best_key if best_score >= FUZZY_THRESHOLD else None


# ═══════════════════════════════════════════════════════════════════════════════
#  FORMULE — rank score i entropija
# ═══════════════════════════════════════════════════════════════════════════════

def _rank_score(rank: int) -> float:
    """
    Updated rank scoring formula to improve stability.

    rankScore = 1 / (log2(rank + 1) + 0.5)

    rank=1 → 0.667
    rank=2 → 0.500
    rank=5 → 0.333
    rank=10 → 0.250
    rank=100 → 0.143

    """
    r = max(rank, 1)
    return 1.0 / (math.log2(r + 1) + 0.5)


def _shannon_entropy(counts: Dict[str, int]) -> float:
    """
    Shannon entropija: H = -Σ p_i * log2(p_i)

    Koristi se da nagradimo proizvode koji se ravnomjerno pojavljuju
    na svim izvorima, a ne samo na jednom.

    C# ekvivalent:
      static double ShannonEntropy(Dictionary<string,int> counts) {
          double total = counts.Values.Sum();
          if (total <= 0) return 0;
          return -counts.Values
              .Where(c => c > 0)
              .Sum(c => { double p = c / total; return p * Math.Log2(p); });
      }
    """
    total = sum(counts.values())
    if total <= 0:
        return 0.0

    H = 0.0
    for c in counts.values():
        if c <= 0:
            continue
        p = c / total
        H -= p * math.log2(p)
    return H


# ═══════════════════════════════════════════════════════════════════════════════
#  GLAVNI PRORAČUN — korak po korak
# ═══════════════════════════════════════════════════════════════════════════════

def compute_trend_groups(items: List[ScrapedItem]) -> List[TrendGroupResult]:
    """
    Ulaz:  Svi ScrapedItem iz svih izvora i tržišta (jedan batch = jedan dan).
    Izlaz: Lista TrendGroupResult sortirana po final_score opadajuće.

    ─── KORAK 1 — Grupisanje ───────────────────────────────────────────────────
    Za svaki item računamo exact canonical_key.
    Ako key ne postoji, pokušavamo fuzzy match unutar istog brenda.
    Ako ni to ne pomaže, otvaramo novu grupu.

    ─── KORAK 2 — itemScore po itemu ───────────────────────────────────────────
    itemScore = rankScore × sourceWeight × marketWeight
              × imageFactor × newFactor × saleFactor

    Gdje:
      rankScore    = 1 / (log2(rank + 1) + 0.5)
      sourceWeight = SOURCE_WEIGHT[source]         (default 0.5)
      marketWeight = MARKET_WEIGHT[market]         (default 0.5)
      imageFactor  = 0.5 ako nema slike, inače 1.0
      newFactor    = 1.2 ako je novi artikal, inače 1.0
      saleFactor   = 1.1 ako je na popustu, inače 1.0

    ─── KORAK 3 — Grupni multiplikatori ────────────────────────────────────────
    cross_src_mult  = 1 + 0.40 × (uniqueSources - 1)
    cross_mkt_mult  = 1 + 0.15 × (uniqueMarkets - 1)
    entropy_mult    = 1 + (H / Hmax) × 0.25

    final_score = base_score × cross_src_mult × cross_mkt_mult × entropy_mult

    ─── KORAK 4 — Sort po final_score ──────────────────────────────────────────
    """
    groups: Dict[str, Dict[str, Any]] = {}
    insertion_order: List[str] = []

    # ── Korak 1 & 2 — grupisanje + itemScore ─────────────────────────────────
    for item in items:
        brand_norm = _norm_text(item.brand)
        exact_key = build_canonical_key(item)

        if exact_key in groups:
            key = exact_key
        else:
            fuzzy = _fuzzy_find_group(exact_key, insertion_order, brand_norm)
            key = fuzzy or exact_key
            if key not in groups:
                groups[key] = {
                    "brand": item.brand,
                    "name": item.name,
                    "items": [],
                    "sources": set(),
                    "markets": set(),
                    "source_counts": Counter(),
                    "market_counts": Counter(),
                    "base_score": 0.0,
                }
                insertion_order.append(key)

        g = groups[key]
        g["items"].append(item)
        g["sources"].add(item.source)
        g["markets"].add(item.market)
        g["source_counts"][item.source] += 1
        g["market_counts"][item.market] += 1

        # ── itemScore formula ─────────────────────────────────────────────────
        item_score = (
            _rank_score(item.rank)
            * SOURCE_WEIGHT.get(item.source.lower(), 0.5)
            * MARKET_WEIGHT.get(item.market.upper(), 0.5)
        )

        # Penali i bonusi (multiplikativan, redosljed nije bitan)
        if not item.hasImage:
            item_score *= NO_IMAGE_PENALTY           # ×0.5
        if item.isNew:
            item_score *= (1.0 + NEW_ARRIVAL_BONUS)  # ×1.2
        if item.isOnSale:
            item_score *= (1.0 + SALE_BONUS)         # ×1.1

        g["base_score"] += item_score

    # ── Korak 3 — grupni multiplikatori ──────────────────────────────────────
    results: List[TrendGroupResult] = []

    for key, g in groups.items():
        base_score = g["base_score"]
        if base_score <= 0:
            continue

        unique_sources = len(g["sources"])
        unique_markets = len(g["markets"])

        # cross_src_mult = 1 + 0.40 × (uniqueSources - 1)
        cross_src_mult = 1.0 + max(0, unique_sources - 1) * CROSS_SOURCE_BONUS

        # cross_mkt_mult = 1 + 0.15 × (uniqueMarkets - 1)
        cross_mkt_mult = 1.0 + max(0, unique_markets - 1) * CROSS_MARKET_BONUS

        # entropy_mult = 1 + (H / log2(N)) × 0.25
        H = _shannon_entropy(g["source_counts"])
        H_max = math.log2(max(unique_sources, 1))
        entropy_ratio = (H / H_max) if H_max > 0 else 0.0
        entropy_mult = 1.0 + entropy_ratio * ENTROPY_BONUS_MAX

        final_score = base_score * cross_src_mult * cross_mkt_mult * entropy_mult

        results.append(TrendGroupResult(
            canonical_key=key,
            brand=g["brand"],
            name=g["name"],
            markets=sorted(g["markets"]),
            sources=sorted(g["sources"]),
            total_occurrences=len(g["items"]),
            unique_sources=unique_sources,
            unique_markets=unique_markets,
            base_score=round(base_score, 6),
            final_score=round(final_score, 6),
            source_counts=dict(g["source_counts"]),
            market_counts=dict(g["market_counts"]),
        ))

    # ── Korak 4 — sort opadajuće ──────────────────────────────────────────────
    results.sort(key=lambda r: r.final_score, reverse=True)
    return results


# ═══════════════════════════════════════════════════════════════════════════════
#  SOCIAL BOOST — opcionalan post-processing
# ═══════════════════════════════════════════════════════════════════════════════

def apply_social_boost(
    groups: List[TrendGroupResult],
    social_scores_by_brand: Dict[str, float],
    social_weight: float = MAX_SOCIAL_WEIGHT,
) -> List[TrendGroupResult]:
    """
    Adjusted social boost formula to ensure smoother application and alignment with constraints.
    """
    for g in groups:
        brand_key = g.brand.lower().strip()
        s = social_scores_by_brand.get(brand_key)
        if s is None:
            continue
        social_norm = max(0.0, min(1.0, s / 100.0))
        boost_factor = 1.0 + social_weight * social_norm
        g.final_score = round(g.final_score * boost_factor, 6)

    groups.sort(key=lambda r: r.final_score, reverse=True)
    return groups


# ═══════════════════════════════════════════════════════════════════════════════
#  SERIALIZATION — za JSON output i C# deserijalizaciju
# ═══════════════════════════════════════════════════════════════════════════════

def serialize_trend_groups(groups: List[TrendGroupResult]) -> List[Dict[str, Any]]:
    """
    Konvertuj u plain dict listu pogodnu za JSON serializaciju.

    JSON format koji C# worker deserijalizuje:
    {
      "canonical_key":      "nike|id:12345678",
      "brand":              "Nike",
      "name":               "Air Max 270",
      "markets":            ["AT", "DE"],
      "sources":            ["aboutyou", "zalando"],
      "total_occurrences":  14,
      "unique_sources":     2,
      "unique_markets":     2,
      "base_score":         0.5234,
      "final_score":        0.8865,
      "source_counts":      {"zalando": 10, "aboutyou": 4},
      "market_counts":      {"DE": 9, "AT": 5}
    }
    """
    out = []
    for i, g in enumerate(groups):
        d = asdict(g)
        d["rank"] = i + 1  # 1-based rank za upis u DB
        out.append(d)
    return out
