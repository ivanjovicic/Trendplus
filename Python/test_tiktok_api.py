"""
Quick test for TikTok API with your RapidAPI key
"""

import os
import sys

# Add parent directory to path
sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))

from scraper.tiktok import get_tiktok_hashtag_stats
from dotenv import load_dotenv

# Load .env
load_dotenv()

print("=" * 60)
print("🧪 Testing TikTok API with Real Data")
print("=" * 60)
print()

# Check if API key is loaded
api_key = os.getenv("RAPIDAPI_KEY")
if api_key:
    print(f"✅ API Key loaded: {api_key[:10]}...{api_key[-5:]}")
else:
    print("❌ API Key not found in .env!")
    sys.exit(1)

print()

# Test popular hashtags
test_hashtags = [
    "#sneakers",
    "#nike",
    "#airmax"
]

for hashtag in test_hashtags:
    print(f"\n{'─' * 60}")
    print(f"Testing: {hashtag}")
    print(f"{'─' * 60}")
    
    stats = get_tiktok_hashtag_stats(hashtag)
    
    print(f"\n📊 Results:")
    print(f"  Hashtag: {stats['hashtag']}")
    print(f"  Posts: {stats['posts']:,}")
    print(f"  Views: {stats['views']:,}")
    print(f"  Likes: {stats['likes']:,}")
    print(f"  Engagement Rate: {stats['engagement_rate']}%")
    
    if stats['views'] > 0:
        print(f"\n✅ SUCCESS - Real data received!")
    else:
        print(f"\n⚠️ No data - API might have rate limits")

print("\n" + "=" * 60)
print("✅ Test complete!")
print("=" * 60)
