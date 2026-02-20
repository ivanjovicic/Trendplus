@echo off
setlocal enabledelayedexpansion

echo Creating Python folders...
mkdir scraper 2>nul

echo [1/6] Creating scraper\__init__.py
echo # Scraper package> scraper\__init__.py

echo [2/6] Creating scraper\zalando_playwright.py
(
echo from playwright.sync_api import sync_playwright
echo import time, random
echo
echo def scrape_zalando_playwright(max_pages=3, category="sneaker"):
echo ^    results = []
echo ^    print(f"Scraping Zalando with Playwright - category: {category}")
echo
echo ^    url_tpl = f"https://www.zalando.de/{category}/?order=popularity&page="
echo
echo ^    with sync_playwright() as p:
echo ^        browser = p.chromium.launch(headless=True)
echo ^        context = browser.new_context()
echo ^        page = context.new_page()
echo
echo ^        for page_num in range(1, max_pages + 1):
echo ^            url = url_tpl + str(page_num)
echo ^            print(f" Page {page_num}/{max_pages} ... ", end="")
echo ^            try:
echo ^                page.goto(url, timeout=50000)
echo ^                page.wait_for_timeout(1500)
echo
echo ^                json_data = page.evaluate("window.__NEXT_DATA__")
echo ^                if not json_data:
echo ^                    print("NO NEXT_DATA")
echo ^                    continue
echo
echo ^                articles = json_data.get("props", {}).get("pageProps", {}).get("catalog", {}).get("articles", [])
echo ^                print(f"{len(articles)} items")
echo
echo ^                for i, p in enumerate(articles):
echo ^                    item = {
echo ^                        "product_id": p.get("id"),
echo ^                        "name": p.get("name"),
echo ^                        "brand": p.get("brand", {}).get("name"),
echo ^                        "price": p.get("price", {}).get("amount"),
echo ^                        "image_url": p.get("images", [{}])[0].get("src", ""),
echo ^                    }
echo ^                    results.append(item)
echo
echo ^            except Exception as e:
echo ^                print("ERROR:", e)
echo
echo ^            time.sleep(random.uniform(1.2, 2.0))
echo
echo ^        browser.close()
echo
echo ^    print(f"Total scraped: {len(results)}")
echo ^    return results
) > scraper\zalando_playwright.py

echo [3/6] Creating scraper\utils.py
echo import requests> scraper\utils.py

echo [4/6] Creating scraper\deichmann_scraper.py
echo print("Deichmann placeholder")> scraper\deichmann_scraper.py

echo [5/6] Creating scraper\social_trends.py
echo print("Social trends placeholder")> scraper\social_trends.py

echo [6/6] DONE!
echo.
echo Python project generated successfully.
pause
