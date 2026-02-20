@echo off
setlocal DisableDelayedExpansion

echo Creating Python folders...
mkdir scraper 2>nul

echo [1/6] Creating scraper\__init__.py
> scraper\__init__.py echo # Scraper package

echo [2/6] Creating scraper\zalando_playwright.py
> scraper\zalando_playwright.py echo from playwright.sync_api import sync_playwright
>> scraper\zalando_playwright.py echo import time
>> scraper\zalando_playwright.py echo import random
>> scraper\zalando_playwright.py echo
>> scraper\zalando_playwright.py echo def scrape_zalando_playwright(max_pages=3, category="sneaker"):
>> scraper\zalando_playwright.py echo     results = []
>> scraper\zalando_playwright.py echo     print("Scraping Zalando with Playwright: " + category)
>> scraper\zalando_playwright.py echo
>> scraper\zalando_playwright.py echo     base_url = "https://www.zalando.de/" + category + "/?order=popularity&page="
>> scraper\zalando_playwright.py echo
>> scraper\zalando_playwright.py echo     with sync_playwright() as p:
>> scraper\zalando_playwright.py echo         browser = p.chromium.launch(headless=True)
>> scraper\zalando_playwright.py echo         context = browser.new_context()
>> scraper\zalando_playwright.py echo         page = context.new_page()
>> scraper\zalando_playwright.py echo
>> scraper\zalando_playwright.py echo         for i in range(max_pages):
>> scraper\zalando_playwright.py echo             url = base_url + str(i+1)
>> scraper\zalando_playwright.py echo             print(" Page " + str(i+1) + "/" + str(max_pages))
>> scraper\zalando_playwright.py echo             try:
>> scraper\zalando_playwright.py echo                 page.goto(url, timeout=50000)
>> scraper\zalando_playwright.py echo                 page.wait_for_timeout(1500)
>> scraper\zalando_playwright.py echo                 data = page.evaluate("window.__NEXT_DATA__")
>> scraper\zalando_playwright.py echo                 if not data:
>> scraper\zalando_playwright.py echo                     print("NO NEXT_DATA")
>> scraper\zalando_playwright.py echo                     continue
>> scraper\zalando_playwright.py echo
>> scraper\zalando_playwright.py echo                 articles = data["props"]["pageProps"]["catalog"]["articles"]
>> scraper\zalando_playwright.py echo
>> scraper\zalando_playwright.py echo                 for item in articles:
>> scraper\zalando_playwright.py echo                     results.append({
>> scraper\zalando_playwright.py echo                         "product_id": item.get("id"),
>> scraper\zalando_playwright.py echo                         "name": item.get("name"),
>> scraper\zalando_playwright.py echo                         "brand": item.get("brand",{}).get("name"),
>> scraper\zalando_playwright.py echo                         "price": item.get("price",{}).get("amount"),
>> scraper\zalando_playwright.py echo                         "image_url": item.get("images", [{}])[0].get("src","")
>> scraper\zalando_playwright.py echo                     })
>> scraper\zalando_playwright.py echo
>> scraper\zalando_playwright.py echo             except Exception as e:
>> scraper\zalando_playwright.py echo                 print("ERROR")
>> scraper\zalando_playwright.py echo
>> scraper\zalando_playwright.py echo         browser.close()
>> scraper\zalando_playwright.py echo
>> scraper\zalando_playwright.py echo     print("Total scraped: " + str(len(results)))
>> scraper\zalando_playwright.py echo     return results

echo [3/6] Creating scraper\utils.py
> scraper\utils.py echo import requests

echo [4/6] Creating scraper\deichmann_scraper.py
> scraper\deichmann_scraper.py echo print("Deichmann placeholder")

echo [5/6] Creating scraper\social_trends.py
> scraper\social_trends.py echo print("Social trends placeholder")

echo [6/6] DONE!
echo.
echo Finished generating Python scraper structure.
pause
