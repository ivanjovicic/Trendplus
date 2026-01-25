"""
Test social media trends aggregator
Validates TikTok, Instagram scraping and scoring
"""

import sys
from scraper.aggregator import get_social_trend, get_category_trends
from scraper.cache import get_cache_stats, clear_cache

def test_single_hashtag():
    """Test single hashtag aggregation"""
    print("=" * 60)
    print("Test 1: Single Hashtag Aggregation")
    print("=" * 60)
    
    hashtag = "#sneakers"
    print(f"\nTesting: {hashtag}")
    
    try:
        trend = get_social_trend(hashtag, "Patike")
        
        print(f"\n✅ SUCCESS!")
        print(f"   Hashtag: {trend['hashtag']}")
        print(f"   TikTok Score: {trend['tiktok_score']:.2f}/100")
        print(f"   Instagram Score: {trend['instagram_score']:.2f}/100")
        print(f"   Final Score: {trend['final_trend_score']:.2f}/100")
        print(f"   Trend Level: {trend['trend_level']}")
        print(f"\n   Raw Metrics:")
        print(f"   - TikTok Views: {trend['tiktok_views']:,}")
        print(f"   - TikTok Posts: {trend['tiktok_posts']:,}")
        print(f"   - Instagram Posts: {trend['instagram_posts']:,}")
        
        return True
        
    except Exception as e:
        print(f"\n❌ FAILED: {e}")
        import traceback
        traceback.print_exc()
        return False


def test_category_trends():
    """Test category-wide trend aggregation"""
    print("\n" + "=" * 60)
    print("Test 2: Category Trends Aggregation")
    print("=" * 60)
    
    category = "Patike"
    print(f"\nTesting category: {category}")
    
    try:
        trends = get_category_trends(category)
        
        if not trends:
            print("❌ No trends returned")
            return False
        
        print(f"\n✅ SUCCESS! Found {len(trends)} hashtags")
        print(f"\n🔥 Top 3 trending hashtags:")
        
        for i, trend in enumerate(trends[:3], 1):
            print(f"\n{i}. {trend['hashtag']}")
            print(f"   Score: {trend['final_trend_score']:.2f}/100")
            print(f"   Level: {trend['trend_level']}")
            print(f"   TikTok: {trend['tiktok_views']:,} views")
            print(f"   Instagram: {trend['instagram_posts']:,} posts")
        
        return True
        
    except Exception as e:
        print(f"\n❌ FAILED: {e}")
        import traceback
        traceback.print_exc()
        return False


def test_cache_system():
    """Test caching mechanism"""
    print("\n" + "=" * 60)
    print("Test 3: Cache System")
    print("=" * 60)
    
    try:
        stats = get_cache_stats()
        
        print(f"\n📊 Cache Statistics:")
        print(f"   Total Entries: {stats['total_entries']}")
        print(f"   Valid Entries: {stats['valid_entries']}")
        print(f"   Expired Entries: {stats['expired_entries']}")
        print(f"   Cache Size: {stats['cache_size_kb']:.2f} KB")
        print(f"   TTL: {stats['ttl_hours']:.1f} hours")
        
        print(f"\n✅ Cache system working")
        return True
        
    except Exception as e:
        print(f"\n❌ FAILED: {e}")
        return False


def test_scoring_accuracy():
    """Test scoring algorithm accuracy"""
    print("\n" + "=" * 60)
    print("Test 4: Scoring Algorithm")
    print("=" * 60)
    
    test_cases = [
        {
            "name": "Viral Trend",
            "data": {
                "views": 500_000_000,
                "posts": 200_000,
                "engagement_rate": 8.5
            },
            "expected_range": (80, 100)
        },
        {
            "name": "Growing Trend",
            "data": {
                "views": 50_000_000,
                "posts": 30_000,
                "engagement_rate": 4.0
            },
            "expected_range": (40, 60)
        },
        {
            "name": "Cold Trend",
            "data": {
                "views": 1_000_000,
                "posts": 500,
                "engagement_rate": 1.0
            },
            "expected_range": (0, 20)
        }
    ]
    
    from scraper.aggregator import calculate_tiktok_score
    
    all_passed = True
    
    for case in test_cases:
        score = calculate_tiktok_score(case["data"])
        min_score, max_score = case["expected_range"]
        
        passed = min_score <= score <= max_score
        status = "✅" if passed else "❌"
        
        print(f"\n{status} {case['name']}")
        print(f"   Score: {score:.2f}")
        print(f"   Expected: {min_score}-{max_score}")
        
        if not passed:
            all_passed = False
    
    return all_passed


def main():
    """Run all tests"""
    print("\n🧪 Social Media Trends Test Suite")
    print("=" * 60)
    print()
    
    # Optionally clear cache before testing
    # clear_cache()
    
    results = {
        "Single Hashtag": test_single_hashtag(),
        "Category Trends": test_category_trends(),
        "Cache System": test_cache_system(),
        "Scoring Algorithm": test_scoring_accuracy()
    }
    
    print("\n" + "=" * 60)
    print("📊 Test Results Summary")
    print("=" * 60)
    
    for test_name, passed in results.items():
        status = "✅ PASS" if passed else "❌ FAIL"
        print(f"   {test_name}: {status}")
    
    all_passed = all(results.values())
    
    if all_passed:
        print("\n🎉 All tests passed!")
        print("\n💡 Next steps:")
        print("   1. Set RAPIDAPI_KEY in .env for real data")
        print("   2. Run: python -m scraper.social_trends")
        print("   3. Import trends to database")
        return 0
    else:
        print("\n⚠️ Some tests failed!")
        print("\n💡 Troubleshooting:")
        print("   - Check internet connection")
        print("   - Verify RAPIDAPI_KEY in .env")
        print("   - Check if social media sites changed structure")
        return 1


if __name__ == "__main__":
    sys.exit(main())
