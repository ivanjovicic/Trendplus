from math import sqrt, log2
from scorer.config import MAX_SOCIAL_BOOST, MAX_MOMENTUM_BOOST, ENTROPY_BONUS_MAX, ANTI_GAMING_LIMIT
from typing import Dict, List

# Helper function to normalize values
def normalize(value: float, max_value: float) -> float:
    return min(1.0, value / max_value)

# Improved compute_social_multiplier with better readability
def compute_social_multiplier(current: float, previous: float = None) -> float:
    """
    Compute the social multiplier based on current and previous social scores.
    """
    if not current:
        return 1.0

    norm = normalize(current, 100.0)
    abs_boost = 1 + sqrt(norm) * MAX_SOCIAL_BOOST

    momentum_boost = 1.0
    if previous:
        delta = normalize(current - previous, 100.0)
        delta = max(-1, min(1, delta))
        momentum_boost = 1 + delta * MAX_MOMENTUM_BOOST

    return abs_boost * momentum_boost

# Improved entropy_ratio with better readability
def entropy_ratio(source_counts: Dict[str, int]) -> float:
    """
    Calculate the entropy ratio for source counts to measure diversity.
    """
    total = sum(source_counts.values())
    if total == 0:
        return 0.0

    entropy = -sum(
        (c / total) * log2(c / total)
        for c in source_counts.values() if c > 0
    )
    max_entropy = log2(len(source_counts)) if len(source_counts) > 1 else 1.0
    return entropy / max_entropy

# Improved anti_gaming_penalty with better readability
def anti_gaming_penalty(source_counts: Dict[str, int]) -> float:
    """
    Apply a penalty if one source dominates the total counts.
    """
    total = sum(source_counts.values())
    if total == 0:
        return 1.0

    max_share = max(source_counts.values()) / total
    return 0.85 if max_share > ANTI_GAMING_LIMIT else 1.0

# Improved compute_top10 with better readability and comments
def compute_top10(groups: List[Dict[str, any]]) -> List[Dict[str, any]]:
    """
    Compute the top 10 groups based on their final scores.
    """
    for group in groups:
        base_score = sum(group.get("item_scores", []))

        diversity_boost = 1 + entropy_ratio(group["source_counts"]) * ENTROPY_BONUS_MAX

        social_multiplier = compute_social_multiplier(
            group.get("socialScore", 0),
            group.get("previousSocialScore", 0)
        )

        gaming_penalty = anti_gaming_penalty(group["source_counts"])

        final_score = (
            base_score
            * diversity_boost
            * social_multiplier
            * gaming_penalty
        )

        group["final_score"] = final_score

    return sorted(groups, key=lambda g: g["final_score"], reverse=True)[:10]