"""
Social media trends tracking - Enhanced version
Uses aggregator to combine TikTok, Instagram data with caching
"""

from typing import Dict, List
from .aggregator import get_social_trend, get_category_trends

def get_social_trends_for_category(category: str) -> List[Dict]:
    """
    Get comprehensive social media trends for a product category
    
    Args:
        category: Product category (Patike, Sandale, Cipele, Cizme)
    
    Returns:
        List of trend dictionaries sorted by final score
    """
    print(f"\n{'=' * 60}")
    print(f"📱 Fetching social trends for category: {category}")
    print(f"{'=' * 60}")
    
    try:
        # Use aggregator to get all category trends
        trends = get_category_trends(category)
        
        # Transform to database-compatible format
        results = []
        for trend in trends:
            result = {
                "category": trend["category"],
                "hashtag": trend["hashtag"],
                "posts_this_month": trend["tiktok_posts"],
                "posts_last_month": 0,  # Historical data tracking needed
                "tiktok_growth": 0.0,  # Calculate from historical data
                "instagram_growth": 0.0,
                "tiktok_views": trend["tiktok_views"],
                "instagram_posts": trend["instagram_posts"],
                "tiktok_engagement": trend["tiktok_engagement"],
                "average_engagement": trend["tiktok_engagement"],  # Simplified
                "final_trend_score": trend["final_trend_score"],
                "trend_level": trend["trend_level"]
            }
            results.append(result)
        
        print(f"\n✅ Found {len(results)} trending hashtags")
        print(f"🔥 Top trend: {results[0]['hashtag']} (Score: {results[0]['final_trend_score']:.2f})")
        
        return results
        
    except Exception as e:
        print(f"❌ Error fetching trends: {e}")
        return []


def get_trending_hashtags(limit: int = 10, min_score: float = 40.0) -> List[Dict]:
    """
    Get top trending hashtags across all categories
    
    Args:
        limit: Maximum number of results
        min_score: Minimum trend score threshold
    
    Returns:
        List of top trending hashtags
    """
    all_trends = []
    
    categories = ["Patike", "Sandale", "Cipele", "Cizme"]
    
    for category in categories:
        trends = get_social_trends_for_category(category)
        all_trends.extend(trends)
    
    # Filter by minimum score and sort
    filtered = [t for t in all_trends if t["final_trend_score"] >= min_score]
    filtered.sort(key=lambda x: x["final_trend_score"], reverse=True)
    
    return filtered[:limit]


if __name__ == "__main__":
    print("\n🧪 Testing Social Trends Module\n")
    
    # Test single category
    trends = get_social_trends_for_category("Patike")
    
    if trends:
        print("\n📊 Top 3 trends:")
        for i, trend in enumerate(trends[:3], 1):
            print(f"\n{i}. {trend['hashtag']} - {trend['trend_level']}")
            print(f"   Score: {trend['final_trend_score']:.2f}")
            print(f"   TikTok: {trend['tiktok_views']:,} views")
            print(f"   Instagram: {trend['instagram_posts']:,} posts")
    
    # Test top trends across all categories
    print("\n" + "=" * 60)
    print("🔥 Overall Top Trending Hashtags")
    print("=" * 60)
    
    top_trends = get_trending_hashtags(limit=5, min_score=50.0)
    
    for i, trend in enumerate(top_trends, 1):
        print(f"\n{i}. {trend['hashtag']} ({trend['category']})")
        print(f"   Score: {trend['final_trend_score']:.2f} - {trend['trend_level']}")
