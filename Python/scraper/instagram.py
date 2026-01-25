"""
Instagram trends scraper
Scrapes hashtag explore pages for post counts
"""

import requests
import json
import re
from bs4 import BeautifulSoup
from typing import Dict
from .cache import get_cached, set_cached

HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
    "Accept-Language": "en-US,en;q=0.9"
}

def get_instagram_stats(hashtag: str) -> Dict:
    """
    Get Instagram hashtag post count
    
    Args:
        hashtag: Hashtag to analyze (with or without #)
    
    Returns:
        Dict with posts count and engagement metrics
    """
    # Normalize hashtag
    clean_hashtag = hashtag.replace("#", "").strip().lower()
    cache_key = f"ig:{clean_hashtag}"
    
    # Check cache
    cached = get_cached(cache_key)
    if cached:
        return cached
    
    url = f"https://www.instagram.com/explore/tags/{clean_hashtag}/"
    
    try:
        print(f"🔍 Fetching Instagram data for #{clean_hashtag}...")
        response = requests.get(url, headers=HEADERS, timeout=10)
        
        if response.status_code != 200:
            print(f"❌ Instagram error: HTTP {response.status_code}")
            return mock_result(hashtag)
        
        html = response.text
        
        # Check if hashtag exists
        if "Page Not Found" in html or "Sorry, this page isn't available" in html:
            print(f"⚠️ Hashtag #{clean_hashtag} not found on Instagram")
            return mock_result(hashtag)
        
        posts_count = extract_post_count(html)
        
        result = {
            "hashtag": hashtag,
            "posts": posts_count,
            "engagement_estimate": estimate_engagement(posts_count)
        }
        
        # Cache result
        set_cached(cache_key, result)
        
        print(f"✅ Instagram: #{clean_hashtag} - {posts_count:,} posts")
        return result
        
    except requests.exceptions.RequestException as e:
        print(f"❌ Instagram request failed: {e}")
        return mock_result(hashtag)
    except Exception as e:
        print(f"❌ Instagram parsing error: {e}")
        return mock_result(hashtag)


def extract_post_count(html: str) -> int:
    """
    Extract post count from Instagram HTML
    Tries multiple methods as Instagram structure changes frequently
    """
    # Method 1: JSON in script tag
    try:
        soup = BeautifulSoup(html, "html.parser")
        
        # Find script tags with JSON data
        for script in soup.find_all("script", type="text/javascript"):
            script_text = script.string
            if script_text and "graphql" in script_text and "edge_hashtag_to_media" in script_text:
                # Extract JSON object
                match = re.search(r'window\._sharedData = ({.*?});', script_text)
                if match:
                    data = json.loads(match.group(1))
                    
                    # Navigate JSON structure
                    entry_data = data.get("entry_data", {})
                    tag_page = entry_data.get("TagPage", [{}])[0]
                    graphql = tag_page.get("graphql", {})
                    hashtag = graphql.get("hashtag", {})
                    media = hashtag.get("edge_hashtag_to_media", {})
                    count = media.get("count", 0)
                    
                    if count > 0:
                        return count
    except:
        pass
    
    # Method 2: Meta tags
    try:
        soup = BeautifulSoup(html, "html.parser")
        meta = soup.find("meta", property="og:description")
        if meta:
            content = meta.get("content", "")
            # Extract number from "X Posts - See Instagram photos..."
            match = re.search(r'([\d,]+)\s+Posts', content)
            if match:
                count_str = match.group(1).replace(",", "")
                return int(count_str)
    except:
        pass
    
    # Method 3: Simple text search
    try:
        match = re.search(r'"edge_hashtag_to_media":{"count":(\d+)', html)
        if match:
            return int(match.group(1))
    except:
        pass
    
    print("⚠️ Could not extract post count, returning 0")
    return 0


def estimate_engagement(posts_count: int) -> float:
    """
    Estimate engagement rate based on post count
    Higher post count typically means higher engagement
    """
    if posts_count == 0:
        return 0.0
    elif posts_count < 10000:
        return 2.5  # Low engagement
    elif posts_count < 100000:
        return 3.5  # Medium engagement
    elif posts_count < 1000000:
        return 5.0  # High engagement
    else:
        return 7.0  # Very high engagement


def mock_result(hashtag: str) -> Dict:
    """Return mock result when scraping fails"""
    return {
        "hashtag": hashtag,
        "posts": 0,
        "engagement_estimate": 0.0
    }


if __name__ == "__main__":
    # Test Instagram scraper
    print("Testing Instagram hashtag scraper...\n")
    
    test_hashtags = ["#sneakers", "#airmax", "#nike"]
    
    for tag in test_hashtags:
        stats = get_instagram_stats(tag)
        print(f"\n{tag}:")
        for key, value in stats.items():
            print(f"  {key}: {value}")
