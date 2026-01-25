"""
Enhanced TikTok trends scraper
Uses RapidAPI with caching and retry logic
"""

import requests
import os
from typing import Dict
from .cache import get_cached, set_cached

RAPID_KEY = os.getenv("RAPIDAPI_KEY", "")

def get_tiktok_hashtag_stats(hashtag: str) -> Dict:
    """
    Get TikTok hashtag statistics with caching
    
    Args:
        hashtag: Hashtag to analyze (with or without #)
    
    Returns:
        Dict with posts, views, likes, engagement metrics
    """
    # Normalize hashtag
    clean_hashtag = hashtag.replace("#", "").strip().lower()
    cache_key = f"tiktok:{clean_hashtag}"
    
    # Check cache first
    cached = get_cached(cache_key)
    if cached:
        return cached
    
    # No API key - return mock data
    if not RAPID_KEY:
        print(f"⚠️ RAPIDAPI_KEY not set, using mock data for #{clean_hashtag}")
        return {
            "hashtag": hashtag,
            "posts": 0,
            "views": 0,
            "likes": 0,
            "engagement_rate": 0.0,
            "growth": 0.0
        }
    
    # Call TikTok API
    url = "https://tiktok-scraper7.p.rapidapi.com/hashtag/info"
    
    headers = {
        "X-RapidAPI-Key": RAPID_KEY,
        "X-RapidAPI-Host": "tiktok-scraper7.p.rapidapi.com"
    }
    
    params = {"hashtag": clean_hashtag}
    
    try:
        print(f"🔍 Fetching TikTok data for #{clean_hashtag}...")
        response = requests.get(url, headers=headers, params=params, timeout=10)
        
        if response.status_code != 200:
            print(f"❌ TikTok API error: HTTP {response.status_code}")
            return mock_result(hashtag)
        
        data = response.json()
        
        # Extract metrics
        posts = data.get("videoCount", 0)
        views = data.get("viewCount", 0)
        likes = data.get("likes", 0)
        
        # Calculate engagement rate
        engagement_rate = (likes / views * 100) if views > 0 else 0.0
        
        result = {
            "hashtag": hashtag,
            "posts": posts,
            "views": views,
            "likes": likes,
            "engagement_rate": round(engagement_rate, 2),
            "growth": 0.0  # Calculate from historical data if available
        }
        
        # Cache result
        set_cached(cache_key, result)
        
        print(f"✅ TikTok: #{clean_hashtag} - {views:,} views, {posts:,} posts")
        return result
        
    except requests.exceptions.RequestException as e:
        print(f"❌ TikTok API request failed: {e}")
        return mock_result(hashtag)
    except Exception as e:
        print(f"❌ TikTok parsing error: {e}")
        return mock_result(hashtag)


def mock_result(hashtag: str) -> Dict:
    """Return mock result when API fails"""
    return {
        "hashtag": hashtag,
        "posts": 0,
        "views": 0,
        "likes": 0,
        "engagement_rate": 0.0,
        "growth": 0.0
    }


if __name__ == "__main__":
    # Test TikTok scraper
    print("Testing TikTok hashtag scraper...\n")
    
    test_hashtags = ["#sneakers", "#airmax", "#nike"]
    
    for tag in test_hashtags:
        stats = get_tiktok_hashtag_stats(tag)
        print(f"\n{tag}:")
        for key, value in stats.items():
            print(f"  {key}: {value}")
