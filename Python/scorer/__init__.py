"""
Scorer package — thin bridge to the production scoring engine (scoring.py).

Exports:
    compute_topN(items, top_n, **kwargs) → List[Dict]
    compute_top10(items, **kwargs)       → List[Dict]  (compat alias)
"""

from __future__ import annotations

import sys
import os
from typing import Any, Dict, List, Optional
from datetime import datetime

# scoring.py lives in the Python/ root — add it to path if needed
_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if _ROOT not in sys.path:
    sys.path.insert(0, _ROOT)

from scoring import compute_top10 as _compute_top10, build_canonical_key  # noqa: F401


def compute_topN(
    items: List[Dict[str, Any]],
    top_n: int = 200,
    requested_type: Optional[str] = None,
    previous_scores: Optional[Dict[str, float]] = None,
    product_social_scores: Optional[Dict[str, float]] = None,
    prev_social_scores: Optional[Dict[str, float]] = None,
    now_ts: Optional[datetime] = None,
) -> List[Dict[str, Any]]:
    """
    Score all items and return the top-N groups.
    Each group dict has:
        key          – canonical brand|name key
        score        – final float score
        final_score  – same as score (alias)
        items        – list of raw ScrapedItem dicts that matched
        meta         – scoring metadata dict
        source_counts – {source: count}
        socialScore  – highest social score in group (if any)
    """
    if not items:
        return []

    groups = _compute_top10(
        all_items=items,
        requested_type=requested_type,
        top_n=top_n,
        previous_scores=previous_scores,
        now_ts=now_ts,
        product_social_scores=product_social_scores,
        prev_social_scores=prev_social_scores,
    )

    # Enrich each group with fields the pipeline needs
    for g in groups:
        # Normalise key access
        g.setdefault("key", g.get("canonical_key") or "")
        g.setdefault("final_score", g.get("score", 0.0))

        # Aggregate source_counts from items list if not already present
        if "source_counts" not in g or not g["source_counts"]:
            sc: Dict[str, int] = {}
            for it in g.get("items", []):
                src = it.get("source", "unknown")
                sc[src] = sc.get(src, 0) + 1
            g["source_counts"] = sc

        # socialScore = max across items
        scores = [
            float(it.get("socialScore") or 0)
            for it in g.get("items", [])
        ]
        g.setdefault("socialScore", max(scores) if scores else None)

    return groups


# Backward-compat alias
compute_top10 = compute_topN
