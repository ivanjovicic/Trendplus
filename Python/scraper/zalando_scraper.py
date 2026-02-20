import requests
import json
import time
import random
from bs4 import BeautifulSoup
from typing import List, Dict, Optional
from .utils import safe_request

# ============================
# Strong browser-like headers
# ============================

HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/123.0.0.0 Safari/537.36"
    ),
    "Accept": (
        "text/html,application/xhtml+xml,application/xml;"
        "q=0.9,image/webp,image/apng,*/*;q=0.8"
    ),
    "Accept-Language": "de-DE,de;q=0.9,en;q=0.8",
    "Cache-Control": "no-cache",
    "Pragma": "no-cache",
    "Upgrade-Insecure-Requests": "1",
    "DNT": "1"
}


# ============================
# Main Zalando scraper
# ============================

def scrape_zalando(max_pages: int = 3, category: str = "damen-schuhe") -> List[Dict]:
    session = requests.Session()
    results = []

    if category == "damen-schuhe":
        category = "womens-shoes"

    print(f"🔍 Scraping en.zalando.de → {category}")

    for page in range(1, max_pages + 1):
        print(f"  ▶ Page {page}/{max_pages}...", end=" ")

        url = f"https://en.zalando.de/{category}/?order=popularity&page={page}"
        html = safe_request(url, HEADERS, session=session)

        if not html:
            print("❌ Failed (empty page)")
            continue

        soup = BeautifulSoup(html, "html.parser")

        script = soup.find("script", {"id": "__NEXT_DATA__"})
        if not script:
            script = soup.find("script", {"data-zalon-state": True})
        if not script:
            for s in soup.find_all("script"):
                if s.text.strip().startswith("{") and s.text.strip().endswith("}"):
                    script = s
                    break

        if not script:
            print("❌ No JSON payload → printing HTML preview:")
            print(html[:1000])
            continue

        try:
            data = json.loads(script.text)
        except Exception as e:
            print("❌ JSON decode error:", e)
            continue

        articles = (
            data.get("props", {})
                .get("pageProps", {})
                .get("catalog", {})
                .get("articles", [])
        )

        if not articles:
            print("❌ No articles found in JSON")
            continue

        print(f"✔ {len(articles)} products")

        for i, p in enumerate(articles):
            rank = (page - 1) * len(articles) + (i + 1)

            brand_value = (
                p.get("brand", {}).get("name")
                if isinstance(p.get("brand"), dict)
                else p.get("brand")
            )

            product = {
                "rank": rank,
                "product_id": p.get("id"),
                "brand": brand_value,
                "name": p.get("name"),
                "price": extract_price(p),
                "image_url": hd_image(p),
                "color": p.get("color"),
                "category": p.get("categoryName") or category,
                "season": detect_season_advanced(p),
                "url": f"https://www.zalando.de/{p.get('id')}.html",
                "source": "zalando",
                # 🔥 NOVO: Coming Soon flag
                "coming_soon": is_coming_soon(p),
            }

            if product["brand"] and product["name"]:
                results.append(product)

        time.sleep(random.uniform(1.5, 3.0))

    # 🔥 FINALNO: sortiramo tako da Coming Soon ide prvo
    # coming_soon=True → ide ispred (False postaje 1 pa ide kasnije)
    results_sorted = sorted(
        results,
        key=lambda x: (not x.get("coming_soon", False), x.get("rank", 10**9))
    )

    print(f"\n📊 Total collected: {len(results_sorted)} "
          f"(Coming Soon: {sum(1 for r in results_sorted if r['coming_soon'])})")

    return results_sorted



# ============================
# Helpers
# ============================

def extract_price(p) -> Optional[float]:
    try:
        price_data = p.get("price", {})
        if isinstance(price_data, dict):
            return float(price_data.get("amount") or 0)
        return 0.0
    except:
        return None


def hd_image(p) -> str:
    try:
        imgs = p.get("images", [])
        if not imgs:
            return ""

        src = imgs[0].get("src")
        if not src:
            return ""

        # remove ?quality= params → get full image
        if "?" in src:
            return src.split("?")[0]

        return src
    except:
        return ""
    
def is_coming_soon(p) -> bool:
    """
    Pokušava da detektuje 'Coming Soon' modele iz Zalando JSON-a.
    Pošto Zalando menja strukturu, ovo je heuristika koju možeš
    fino da podesiš kad vidiš konkretan payload u zalando_debug_*.html.
    """
    try:
        # 1) Flags / badges / labels
        for key in ["flags", "badges", "labels"]:
            flags = p.get(key, [])
            if isinstance(flags, list):
                for f in flags:
                    text_parts = []
                    if isinstance(f, dict):
                        text_parts.extend([
                            f.get("label", ""),
                            f.get("text", ""),
                            f.get("name", ""),
                            f.get("type", ""),
                        ])
                    elif isinstance(f, str):
                        text_parts.append(f)
                    for t in text_parts:
                        if t and "coming" in t.lower() and "soon" in t.lower():
                            return True

        # 2) Availability / delivery info
        availability = str(p.get("availability", "")).lower()
        if "coming soon" in availability:
            return True

        delivery_infos = p.get("deliveryInfos") or p.get("deliveryInfo") or []
        if isinstance(delivery_infos, list):
            for d in delivery_infos:
                txt = ""
                if isinstance(d, dict):
                    txt = " ".join(
                        str(d.get(k, "")) for k in d.keys()
                    ).lower()
                else:
                    txt = str(d).lower()
                if "coming soon" in txt:
                    return True

        # 3) Brutal fallback – tražimo string u celom objektu
        import json as _json
        raw = _json.dumps(p).lower()
        if "coming soon" in raw:
            return True

    except Exception:
        pass

    return False



def detect_season_advanced(p) -> str:
    name = (p.get("name") or "").lower()
    cat = (p.get("categoryName") or "").lower()

    winter = ["winter", "boot", "stiefel", "warm", "snow"]
    summer = ["summer", "sandal", "sandale", "flip", "espadrille"]

    if any(w in name or w in cat for w in winter):
        return "Jesen-Zima"

    if any(w in name or w in cat for w in summer):
        return "Prolece-Leto"

    return "Cela godina"
