import unicodedata
import re


def normalize_text(s: str):
    if not s:
        return ""
    s = s.lower().strip()

    # remove accents
    s = unicodedata.normalize("NFKD", s).encode("ascii", "ignore").decode("ascii")

    # auto-translation map (DE/RS/EN) for brands/types/keywords
    translation_map = {
        # Brands (example)
        "adidas": "adidas", "адидас": "adidas",
        "nike": "nike", "најк": "nike",
        "reebok": "reebok", "рибок": "reebok",
        "buffalo": "buffalo", "буффало": "buffalo",
        # Types
        "boot": "boot", "boots": "boot", "stiefel": "boot", "чизме": "boot", "ботинки": "boot",
        "sneaker": "sneaker", "sneakers": "sneaker", "patike": "sneaker", "кроссовки": "sneaker", "кеды": "sneaker",
        "sandale": "sandal", "sandals": "sandal", "сандале": "sandal", "сандалии": "sandal",
        "loafer": "loafer", "loafers": "loafer", "мокасине": "loafer",
        "heel": "heel", "heels": "heel", "pumps": "heel", "штикле": "heel",
        "flat": "flat", "ballerinas": "flat", "ballet": "flat", "балетанке": "flat",
        # Colors (optional, for future)
        "schwarz": "black", "crna": "black", "black": "black",
        "weiss": "white", "bela": "white", "white": "white",
        "braun": "brown", "braon": "brown", "brown": "brown",
        # Add more as needed
    }

    # Tokenize and translate
    tokens = s.split()
    tokens = [translation_map.get(tok, tok) for tok in tokens]
    s = " ".join(tokens)

    # keep only letters and numbers
    s = re.sub(r"[^a-z0-9\s]", " ", s)

    # reduce multiple spaces
    s = re.sub(r"\s+", " ", s)

    return s.strip()


# --- Shoe type extraction ---
def get_shoe_type(name: str) -> str:
    n = (name or "").lower()
    if any(x in n for x in ["sneak", "trainer", "sport"]):
        return "sneaker"
    if any(x in n for x in ["boot", "stiefel"]):
        return "boot"
    if any(x in n for x in ["sand", "sandal"]):
        return "sandal"
    if any(x in n for x in ["heel", "pum"]):
        return "heel"
    if any(x in n for x in ["loafer", "flat", "ballet"]):
        return "flat"
    return "other"

# --- Price parser ---
def parse_price(val):
    import re
    if val is None:
        return 0.0
    try:
        return float(val)
    except:
        pass
    s = str(val).replace('\u00a0', '').replace("€", "").strip()
    m = re.search(r"[0-9\.,]+", s)
    if not m:
        return 0.0
    s = m.group(0)
    if "." in s and "," in s:
        s = s.replace(".", "").replace(",", ".")
    elif "," in s:
        s = s.replace(",", ".")
    try:
        return float(s)
    except:
        return 0.0

# --- Fuzzy string similarity ---
from difflib import SequenceMatcher
def similarity(a, b):
    return SequenceMatcher(None, a or "", b or "").ratio()

# --- Weighted scoring model ---
def score_match(z, d):
    score = 0
    # brand
    if normalize_text(z.get("brand")) == normalize_text(d.get("brand")):
        score += 40
    # type
    if get_shoe_type(z.get("name")) == get_shoe_type(d.get("name")):
        score += 20
    # price
    pa = parse_price(z.get("price"))
    pb = parse_price(d.get("price"))
    if pa > 0 and abs(pa - pb) < 0.2 * pa:
        score += 20
    # name fuzzy
    zn = normalize_text(z.get("name"))
    dn = normalize_text(d.get("name"))
    if similarity(zn, dn) > 0.6:
        score += 10
    return score

# --- Main similarity function ---
def is_similar(z_item, d_item, min_score=60):
    return score_match(z_item, d_item) >= min_score
"""
Social media trends aggregator
Combines data from TikTok, Instagram, Pinterest, Google Trends
"""

from typing import Dict
from .tiktok import get_tiktok_hashtag_stats
from .instagram import get_instagram_stats

def get_social_trend(hashtag: str, category: str = "Patike") -> Dict:
    """
    Aggregate social media trends from multiple sources
    
    Args:
        hashtag: Hashtag to analyze
        category: Product category for weighting
    
    Returns:
        Dict with aggregated trend score and source breakdown
    """
    print(f"\n📊 Aggregating social trends for {hashtag}...")
    
    # Fetch data from all sources
    tiktok = get_tiktok_hashtag_stats(hashtag)
    instagram = get_instagram_stats(hashtag)
    
    # Calculate individual scores (0-100 scale)
    tiktok_score = calculate_tiktok_score(tiktok)
    instagram_score = calculate_instagram_score(instagram)
    
    # Weighted final score
    # TikTok: 60% (most important for trending)
    # Instagram: 40% (established popularity)
    final_score = (
        tiktok_score * 0.60 +
        instagram_score * 0.40
    )
    
    result = {
        "hashtag": hashtag,
        "category": category,
        
        # Individual scores
        "tiktok_score": round(tiktok_score, 2),
        "instagram_score": round(instagram_score, 2),
        
        # Raw metrics
        "tiktok_views": tiktok.get("views", 0),
        "tiktok_posts": tiktok.get("posts", 0),
        "instagram_posts": instagram.get("posts", 0),
        
        # Engagement
        "tiktok_engagement": tiktok.get("engagement_rate", 0.0),
        
        # Final aggregated score (0-100)
        "final_trend_score": round(final_score, 2),
        
        # Trend classification
        "trend_level": classify_trend(final_score)
    }
    
    print(f"✅ Final trend score: {result['final_trend_score']:.2f} ({result['trend_level']})")
    
    return result


def calculate_tiktok_score(tiktok_data: Dict) -> float:
    """
    Calculate TikTok trend score (0-100)
    Based on views, posts, engagement
    """
    views = tiktok_data.get("views", 0)
    posts = tiktok_data.get("posts", 0)
    engagement = tiktok_data.get("engagement_rate", 0.0)
    
    # View score (0-50 points)
    # 100M+ views = 50 points
    view_score = min(50, (views / 100_000_000) * 50)
    
    # Post score (0-30 points)
    # 100K+ posts = 30 points
    post_score = min(30, (posts / 100_000) * 30)
    
    # Engagement score (0-20 points)
    # 5%+ engagement = 20 points
    engagement_score = min(20, (engagement / 5.0) * 20)
    
    return view_score + post_score + engagement_score


def calculate_instagram_score(instagram_data: Dict) -> float:
    """
    Calculate Instagram trend score (0-100)
    Based on post count and engagement estimate
    """
    posts = instagram_data.get("posts", 0)
    engagement = instagram_data.get("engagement_estimate", 0.0)
    
    # Post score (0-70 points)
    # 1M+ posts = 70 points
    post_score = min(70, (posts / 1_000_000) * 70)
    
    # Engagement score (0-30 points)
    # 7%+ engagement = 30 points
    engagement_score = min(30, (engagement / 7.0) * 30)
    
    return post_score + engagement_score


def classify_trend(score: float) -> str:
    """
    Classify trend level based on score
    """
    if score >= 80:
        return "🔥 Viral"
    elif score >= 60:
        return "📈 Trending"
    elif score >= 40:
        return "👀 Growing"
    elif score >= 20:
        return "💤 Stable"
    else:
        return "❄️ Cold"


def get_category_trends(category: str) -> list:
    """
    Get trends for all hashtags in a category
    
    Args:
        category: Product category (Patike, Sandale, etc.)
    
    Returns:
        List of trend results sorted by score
    """
    # Predefined hashtags by category
    hashtags_db = {
        "Patike": ["#sneakers", "#sneakerhead", "#airmax", "#yeezy", "#jordans", "#nike", "#adidas"],
        "Sandale": ["#summershoes", "#sandals", "#birkenstock", "#slides"],
        "Cipele": ["#shoes", "#fashion", "#style", "#ootd"],
        "Cizme": ["#boots", "#wintershoes", "#timberland"]
    }
    
    hashtags = hashtags_db.get(category, [])
    results = []
    
    for hashtag in hashtags:
        trend = get_social_trend(hashtag, category)
        results.append(trend)
    
    # Sort by final score descending
    results.sort(key=lambda x: x["final_trend_score"], reverse=True)
    
    return results


if __name__ == "__main__":
    print("Testing Social Media Trends Aggregator\n")
    print("=" * 60)
    
    # Test single hashtag
    trend = get_social_trend("#sneakers", "Patike")
    
    print("\n📊 Trend breakdown:")
    print(f"  Hashtag: {trend['hashtag']}")
    print(f"  TikTok Score: {trend['tiktok_score']:.2f}")
    print(f"  Instagram Score: {trend['instagram_score']:.2f}")
    print(f"  Final Score: {trend['final_trend_score']:.2f}")
    print(f"  Trend Level: {trend['trend_level']}")
    
    print("\n" + "=" * 60)
    
    # Test category trends
    print("\n🔥 Top 3 trending hashtags in Patike:")
    category_trends = get_category_trends("Patike")
    
    for i, t in enumerate(category_trends[:3], 1):
        print(f"\n{i}. {t['hashtag']} - Score: {t['final_trend_score']:.2f}")
        print(f"   TikTok: {t['tiktok_views']:,} views, {t['tiktok_posts']:,} posts")
        print(f"   Instagram: {t['instagram_posts']:,} posts")
        print(f"   Level: {t['trend_level']}")
