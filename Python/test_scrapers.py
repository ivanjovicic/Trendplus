"""
Test Playwright scrapers
Quick validation before full scraping run
"""

import sys
from scraper.deichmann_scraper import scrape_deichmann
from scraper.zalando_scraper import scrape_zalando

def test_deichmann():
    """Test Deichmann scraper with 1 page"""
    print("=" * 50)
    print("Testing Deichmann Scraper (Playwright)")
    print("=" * 50)
    print()
    
    try:
        products = scrape_deichmann(max_pages=1, headless=True)
        
        if products:
            print(f"\n✅ SUCCESS! Scraped {len(products)} products")
            print("\n📦 Sample product:")
            sample = products[0]
            for key, value in sample.items():
                print(f"  {key}: {value}")
            return True
        else:
            print("\n❌ FAILED! No products scraped")
            return False
            
    except Exception as e:
        print(f"\n❌ ERROR: {e}")
        import traceback
        traceback.print_exc()
        return False


def test_zalando():
    """Test Zalando scraper with 1 page"""
    print("\n" + "=" * 50)
    print("Testing Zalando Scraper (BeautifulSoup)")
    print("=" * 50)
    print()
    
    try:
        products = scrape_zalando(max_pages=1, category="sneaker")
        
        if products:
            print(f"\n✅ SUCCESS! Scraped {len(products)} products")
            print("\n📦 Sample product:")
            sample = products[0]
            for key, value in sample.items():
                print(f"  {key}: {value}")
            return True
        else:
            print("\n❌ FAILED! No products scraped")
            return False
            
    except Exception as e:
        print(f"\n❌ ERROR: {e}")
        import traceback
        traceback.print_exc()
        return False


def main():
    """Run all tests"""
    print("\n🧪 Trendplus Scraper Test Suite\n")
    
    results = {
        "Deichmann (Playwright)": test_deichmann(),
        "Zalando (BeautifulSoup)": test_zalando()
    }
    
    print("\n" + "=" * 50)
    print("📊 Test Results")
    print("=" * 50)
    
    for scraper, passed in results.items():
        status = "✅ PASS" if passed else "❌ FAIL"
        print(f"  {scraper}: {status}")
    
    all_passed = all(results.values())
    
    if all_passed:
        print("\n🎉 All tests passed!")
        return 0
    else:
        print("\n⚠️ Some tests failed!")
        return 1


if __name__ == "__main__":
    sys.exit(main())
