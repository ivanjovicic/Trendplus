from math import sqrt, log2
from scorer.config import MAX_SOCIAL_BOOST, MAX_MOMENTUM_BOOST, ENTROPY_BONUS_MAX, ANTI_GAMING_LIMIT

def compute_social_multiplier(current, previous=None):
    if not current:
        return 1.0

    norm = min(1.0, current / 100.0)
    abs_boost = 1 + sqrt(norm) * MAX_SOCIAL_BOOST

    momentum_boost = 1.0
    if previous:
        delta = (current - previous) / 100.0
        delta = max(-1, min(1, delta))
        momentum_boost = 1 + delta * MAX_MOMENTUM_BOOST

    return abs_boost * momentum_boost

def entropy_ratio(source_counts):
    total = sum(source_counts.values())
    if total == 0:
        return 0

    entropy = -sum(
        (c / total) * log2(c / total)
        for c in source_counts.values() if c > 0
    )
    max_entropy = log2(len(source_counts)) if len(source_counts) > 1 else 1
    return entropy / max_entropy

def anti_gaming_penalty(source_counts):
    total = sum(source_counts.values())
    if total == 0:
        return 1.0

    max_share = max(source_counts.values()) / total
    if max_share > ANTI_GAMING_LIMIT:
        return 0.85
    return 1.0

def compute_top10(groups):
    for group in groups:
        base_score = sum(group.get("item_scores", []))

        diversity_boost = 1 + entropy_ratio(group["source_counts"]) * ENTROPY_BONUS_MAX

        social_multiplier = compute_social_multiplier(
            group.get("socialScore"),
            group.get("previousSocialScore")
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