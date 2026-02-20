"""
Test tylko Zalando async scraper
"""
import asyncio

async def test_zalando_only():
    print("Testing Zalando async scraper...")
    
    from scraper.zalando_playwright import scrape_zalando_playwright
    
    # Test only Zalando
    print("\n🔄 Starting Zalando scraper...")
    
    try:
        zalando_results = await scrape_zalando_playwright(
            max_pages=1,
            category="sneakers",
            gender="women"
        )
        
        print(f"\n✅ Zalando: {len(zalando_results)} items")
        if zalando_results:
            print(f"   First 3 items:")
            for i, item in enumerate(zalando_results[:3], 1):
                print(f"   {i}. {item.get('brand')} - {item.get('name')} - {item.get('price')}")
    
    except Exception as e:
        print(f"\n❌ Zalando error: {e}")
        import traceback
        traceback.print_exc()
    
    # Close browser
    from scraper import browser_manager
    await browser_manager.close_browser()
    
    print("\n✅ Test complete!")

if __name__ == "__main__":
    asyncio.run(test_zalando_only())
