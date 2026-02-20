"""
Test async scraper implementation
"""
import asyncio
import json

async def test_scrapers():
    print("Testing async scrapers...")
    
    from scraper.zalando_playwright import scrape_zalando_playwright
    from scraper.deichmann_scraper import scrape_deichmann_filtered
    
    # Test both scrapers in parallel
    print("\n🔄 Starting Zalando & Deichmann scrapers...")
    
    zalando_task = scrape_zalando_playwright(
        max_pages=1,
        category="sneakers",
        gender="women"
    )
    
    deichmann_task = scrape_deichmann_filtered(
        gender="women",
        category="schuhe-82",
        pages=1
    )
    
    # Run both in parallel
    zalando_results, deichmann_results = await asyncio.gather(
        zalando_task, 
        deichmann_task,
        return_exceptions=True
    )
    
    # Check results
    if isinstance(zalando_results, Exception):
        print(f"\n❌ Zalando error: {zalando_results}")
    else:
        print(f"\n✅ Zalando: {len(zalando_results)} items")
        if zalando_results:
            print(f"   First item: {zalando_results[0].get('name', 'N/A')}")
    
    if isinstance(deichmann_results, Exception):
        print(f"\n❌ Deichmann error: {deichmann_results}")
    else:
        print(f"\n✅ Deichmann: {len(deichmann_results)} items")
        if deichmann_results:
            print(f"   First item: {deichmann_results[0].get('name', 'N/A')}")
    
    # Close browser
    from scraper import browser_manager
    await browser_manager.close_browser()
    
    print("\n✅ Test complete!")

if __name__ == "__main__":
    asyncio.run(test_scrapers())
