import requests
import json
import time
import random
from bs4 import BeautifulSoup
from typing import List, Dict, Optional
from .utils import safe_request

HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
    "Accept-Language": "de-DE,de;q=0.9,en;q=0.8",
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9",
}


def scrape_zalando(max_pages: int = 5, category: str = "damen-schuhe") -> List[Dict]:
    results = []
    print(f"🔍 Scraping Zalando.de / {category}")

    for page in range(1, max_pages + 1):
        print(f"  Page {page}/{max_pages}...", end=" ")

        url = f"https://www.zalando.de/{category}/?order=popularity&page={page}"
        html = safe_request(url, HEADERS)

        if not html:
            print("❌ Failed to load page")
            break

        soup = BeautifulSoup(html, "html.parser")

        # find JSON data
        script = (
            soup.find("script", {"id": "__NEXT_DATA__"})
            or soup.find("script", {"data-zalon-data": True})
        )

        if not script:
            print("❌ No JSON data")
            continue

        try:
            data = json.loads(script.text)
        except:
            print("❌ JSON parsing error")
            continue

        articles = (
            data.get("props", {})
                .get("pageProps", {})
                .get("catalog", {})
                .get("articles", [])
        )

        if not articles:
            print("❌ No articles found")
            break

        for i, p in enumerate(articles):
            try:
                product = {
                    "rank": (page - 1) * len(articles) + (i + 1),
                    "product_id": p.get("id"),

                    # brand structure can vary
                    "brand": p.get("brand", {}).get("name")
                    if isinstance(p.get("brand"), dict)
                    else p.get("brand"),

                    "name": p.get("name"),
                    "price": extract_price(p),
                    "image_url": hd_image(p),
                    "color": p.get("color"),
                    "category": p.get("categoryName") or category,
                    "season": detect_season_advanced(p),
                    "url": f"https://www.zalando.de/{p.get('id')}.html",
                    "source": "zalando",
                }

                if product["name"] and product["brand"]:
                    results.append(product)

            except Exception as e:
                continue

        print(f"✔ {len(articles)} products")

        # random delay to avoid anti-bot
        time.sleep(random.uniform(1.5, 3.0))

    print(f"\n📊 Total collected: {len(results)}")
    return results


def extract_price(p):
    try:
        price_data = p.get("price")
        if isinstance(price_data, dict):
            return float(price_data.get("amount") or 0)
        return 0.0
    except:
        return None


def hd_image(p):
    try:
        imgs = p.get("images", [])
        if not imgs:
            return ""

        src = imgs[0].get("src")
        if not src:
            return ""

        # HD trick: remove everything after '?'
        if "?" in src:
            return src.split("?")[0]

        return src
    except:
        return ""


def detect_season_advanced(p):
    name = p.get("name", "").lower()
    cat = p.get("categoryName", "").lower()

    winter_words = ["winter", "boot", "stiefel", "warm", "snow"]
    summer_words = ["summer", "sandal", "sandale", "flip", "espadrille"]

    if any(w in name or w in cat for w in winter_words):
        return "Jesen-Zima"

    if any(w in name or w in cat for w in summer_words):
        return "Prolece-Leto"

    return "Cela godina"
