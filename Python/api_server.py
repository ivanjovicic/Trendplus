# Map Zalando categories to Deichmann categories
CATEGORY_MAP = {
    # Deichmann category slugs (used by scraper/deichmann_scraper.py)
    "sneakers": "sneaker-143",
    "sneaker": "sneaker-143",
    "boots": "schuhe-89",
    "sandals": "schuhe-90",
    "heels": "schuhe-88",
}
"""
Trendplus Global Trends API
- FastAPI server koji povezuje .NET backend sa Python scraperima
- Scrapers: Zalando (Playwright), Deichmann, About You
- /scrapers/common: fuzzy matching između Zalando & Deichmann modela
- Redis caching za /scrapers/common (opciono)
- Elasticsearch indeksiranje common match-eva (opciono)
"""

import os
import sys
import logging
import time
import json
import threading
import asyncio
from typing import Optional, Dict, Any, List

from fastapi import FastAPI, HTTPException, Body
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

# ============================================================
# PATH & IMPORTS
# ============================================================

# Omogući "scraper.*" module
sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))

from scraper.aggregator import get_category_trends  # već postoji kod tebe

# Release calendar scraper (opciono)
scrape_zalando_release_calendar = None
try:
    from scraper_release import scrape_zalando_release_calendar as _scraper_func
    scrape_zalando_release_calendar = _scraper_func
except ImportError:
    pass

# ============================================================
# LOGGING
# ============================================================

logging.basicConfig(level=logging.INFO, format='[%(levelname)s] %(message)s')

# ============================================================
# REDIS (opciono)
# ============================================================

redis_client = None
try:
    import redis  # pip install redis
    REDIS_URL = os.environ.get("REDIS_URL", "redis://localhost:6379/0")
    redis_client = redis.Redis.from_url(REDIS_URL)
    # probni ping
    try:
        redis_client.ping()
        logging.info(f"Redis connected: {REDIS_URL}")
    except Exception as e:
        logging.warning(f"Redis ping failed, disabling cache: {e}")
        redis_client = None
except ImportError:
    logging.info("redis package not installed – Redis caching disabled")


def cache_get(key: str) -> Optional[dict]:
    if redis_client is None:
        return None
    try:
        raw = redis_client.get(key)
        if not raw:
            return None
        return json.loads(raw)
    except Exception as e:
        logging.warning(f"Redis GET error: {e}")
        return None


def cache_set(key: str, value: dict, ttl_seconds: int = 600):
    if redis_client is None:
        return
    try:
        redis_client.setex(key, ttl_seconds, json.dumps(value))
    except Exception as e:
        logging.warning(f"Redis SET error: {e}")


# ============================================================
# ELASTICSEARCH (opciono)
# ============================================================

es_client = None
ES_INDEX = "trendplus-common-products"

try:
    from elasticsearch import Elasticsearch, exceptions as es_exceptions  # pip install elasticsearch

    ES_URL = os.environ.get("ELASTICSEARCH_URL", "http://localhost:9200")
    es_client = Elasticsearch(ES_URL)

    # pokuša da kreira index ako ne postoji
    if es_client is not None:
        if not es_client.indices.exists(index=ES_INDEX):
            logging.info(f"Creating Elasticsearch index: {ES_INDEX}")
            es_client.indices.create(
                index=ES_INDEX,
                body={
                    "mappings": {
                        "properties": {
                            "brand": {"type": "keyword"},
                            "type": {"type": "keyword"},
                            "score": {"type": "integer"},
                            "name_zalando": {"type": "text"},
                            "name_deichmann": {"type": "text"},
                            "price_zalando": {"type": "double"},
                            "price_deichmann": {"type": "double"},
                            "url_zalando": {"type": "keyword"},
                            "url_deichmann": {"type": "keyword"},
                            "source": {"type": "keyword"},
                        }
                    }
                }
            )
except ImportError:
    logging.info("elasticsearch package not installed – ES integration disabled")
    es_client = None
except Exception as e:
    logging.warning(f"Elasticsearch init failed: {e}")
    es_client = None


def es_index_common_matches(matches: List[dict]):
    if es_client is None or not matches:
        return
    try:
        actions = []
        for m in matches:
            z = m.get("zalando", {})
            d = m.get("deichmann", {})
            doc = {
                "brand": m.get("brand"),
                "type": m.get("type"),
                "score": m.get("score"),
                "name_zalando": z.get("name"),
                "name_deichmann": d.get("name"),
                "price_zalando": try_parse_price(z.get("price")),
                "price_deichmann": try_parse_price(d.get("price")),
                "url_zalando": z.get("url"),
                "url_deichmann": d.get("url"),
                "source": "common",
            }
            actions.append({"index": {"_index": ES_INDEX}})
            actions.append(doc)

        if actions:
            es_client.bulk(body=actions, index=ES_INDEX)
    except Exception as e:
        logging.warning(f"ES bulk index error: {e}")


# ============================================================
# TIMEOUT CONSTANTS
# ============================================================

MAX_SCRAPER_TIME = 600  # sekundi (10 minuta) - dovoljan timeout za 10+ stranica paralelno

# ============================================================
# HELPERI ZA NORMALIZACIJU I SCORING
# ============================================================

import re
from difflib import SequenceMatcher


def normalize_text(s: Optional[str]) -> str:
    if not s:
        return ""
    s = s.lower().strip()
    s = re.sub(r"[^a-z0-9 ]+", " ", s)
    s = re.sub(r"\s+", " ", s)
    return s


def get_shoe_type(name: Optional[str]) -> str:
    """
    Vrati tip obuće (čizme, patike, sandale...) po nazivu modela.
    """
    if not name:
        return "unknown"

    n = name.lower()

    if any(
        k in n
        for k in [
            # EN
            "sneaker", "trainer", "trainers", "running",
            # DE
            "sportschuh", "turnschuh", "freizeitschuh", "laufschuh",
        ]
    ):
        return "sneaker"
    if any(k in n for k in ["boot", "stiefel", "biker"]):
        return "boots"
    if any(k in n for k in ["sandale", "sandal"]):
        return "sandals"
    if any(k in n for k in ["pumps", "absatz", "heel"]):
        return "heels"
    if any(k in n for k in ["slipper", "loafer"]):
        return "slippers"

    return "other"


def similarity(a: str, b: str) -> float:
    return SequenceMatcher(None, a or "", b or "").ratio()


# Tokens that add noise across shops (types, colours, generic words).
# Used for a "model token" overlap score that tends to work better than raw string ratio.
MODEL_NOISE_TOKENS = {
    # generic shoe words
    "shoe", "shoes", "schuh", "schuhe",
    # types
    "sneaker", "sneakers", "trainer", "trainers", "sportschuh", "turnschuh", "laufschuh", "running",
    # gender/age
    "women", "womens", "men", "mens", "kids", "unisex", "damen", "herren", "jungen", "madchen",
    # common filler
    "with", "and", "or", "the", "for", "from",
    # common catalogue words
    "carryover", "regular", "price", "originally",
    # colours (EN + DE basics)
    "black", "white", "grey", "gray", "red", "blue", "green", "brown", "beige", "pink", "gold", "silver",
    "schwarz", "weiss", "wei", "grau", "rot", "blau", "grun", "gruen", "braun", "beige", "rosa", "gold", "silber",
}


def normalize_brand(brand: Optional[str]) -> str:
    """
    Normalize brand so sub-brands don't kill matches (e.g. "adidas Originals" == "adidas").
    """
    b = normalize_text(brand)
    # Remove common sub-brand suffixes used on Zalando
    b = re.sub(r"\b(originals|sportswear|sportstyle|performance)\b", " ", b)
    b = re.sub(r"\s+", " ", b).strip()
    return b


def extract_model_tokens(name: Optional[str], brand: Optional[str]) -> set[str]:
    """
    Extract "model-ish" tokens from the product name by removing brand tokens and common noise tokens.
    """
    name_tokens = normalize_text(name).split()
    brand_tokens = set(normalize_brand(brand).split())

    out: set[str] = set()
    for t in name_tokens:
        if not t:
            continue
        if t in brand_tokens:
            continue
        if t in MODEL_NOISE_TOKENS:
            continue
        if len(t) <= 1 and not t.isdigit():
            continue
        out.add(t)
    return out


def token_containment(a: set[str], b: set[str]) -> float:
    """
    How much of the smaller token set is contained in the larger one (0..1).
    This is more forgiving than Jaccard when one shop adds extra tokens like colours.
    """
    if not a or not b:
        return 0.0
    return len(a & b) / min(len(a), len(b))


def numeric_like_tokens(tokens: set[str]) -> set[str]:
    """
    Tokens containing at least one digit (e.g. "530", "00s", "07") are strong model signals.
    """
    return {t for t in tokens if any(ch.isdigit() for ch in t)}


def try_parse_price(value: Any) -> float:
    if value is None:
        return 0.0
    s = str(value)
    s = s.replace("€", "").replace("\u00a0", "").strip()
    s = s.replace(".", "").replace(",", ".")
    try:
        return float(s)
    except Exception:
        return 0.0


def looks_non_english(text: str) -> bool:
    # jako jednostavna heuristika – možeš posle unaprediti
    return any("а" <= ch <= "я" or "А" <= ch <= "Я" for ch in text)


def translate_to_en(text: str) -> str:
    """
    Hook za prevod (Azure / DeepL / sl).
    Trenutno samo vraća isti tekst.
    Ako želiš, ovde možeš pozvati eksterni API.
    """
    # TODO: Integrate external translation API if desired
    return text


def normalize_name_for_match(name: Optional[str]) -> str:
    if not name:
        return ""
    base = normalize_text(name)
    if looks_non_english(name):
        base = translate_to_en(base)
    return normalize_text(base)


def score_match(z: dict, d: dict) -> int:
    """
    Fuzzy scoring model 0–100 za Zalando vs Deichmann item.
    Blaži algoritam za više matcheva.
    """
    score = 0

    # -----------------------
    # 1) BRAND (hard gate if both exist)
    # -----------------------
    zb = normalize_brand(z.get("brand"))
    db = normalize_brand(d.get("brand"))

    brand_score = 0

    if zb and db:
        if zb == db:
            brand_score = 30
        elif zb in db or db in zb:
            brand_score = 25
        else:
            # If both brands exist and differ, it's almost never the same model.
            return 0

    score += brand_score

    # -----------------------
    # 2) TYPE (soft signal)
    # -----------------------
    z_type = get_shoe_type(z.get("name"))
    d_type = get_shoe_type(d.get("name"))

    if z_type == d_type and z_type not in ("unknown", "other"):
        score += 15
    elif z_type == d_type:
        score += 5
    elif z_type in ("unknown", "other") or d_type in ("unknown", "other"):
        # Don't punish missing type info (Deichmann often has just "Schuh")
        score += 5

    # -----------------------
    # 3) PRICE (optional)
    # -----------------------
    pa = try_parse_price(z.get("price"))
    pb = try_parse_price(d.get("price"))
    if pa > 0 and pb > 0:
        diff_percent = abs(pa - pb) / max(pa, pb)
        if diff_percent <= 0.25:
            score += 20
        elif diff_percent <= 0.50:
            score += 10

    # -----------------------
    # 4) NAME / MODEL TOKENS (main signal)
    # -----------------------
    z_tokens = extract_model_tokens(z.get("name"), z.get("brand"))
    d_tokens = extract_model_tokens(d.get("name"), d.get("brand"))

    inter = z_tokens & d_tokens
    cont = token_containment(z_tokens, d_tokens)

    name_score = 0
    if cont >= 0.80 and len(inter) >= 2:
        name_score = 40
    elif cont >= 0.80 and len(inter) == 1 and (len(z_tokens) <= 2 or len(d_tokens) <= 2):
        # Some models are just one strong token (e.g. "barreda"); treat full containment as a good match.
        name_score = 30
    elif cont >= 0.60 and len(inter) >= 2:
        name_score = 35
    elif cont >= 0.40 and len(inter) >= 2:
        name_score = 25
    else:
        # Fallback: plain ratio on normalized names
        zn = normalize_name_for_match(z.get("name"))
        dn = normalize_name_for_match(d.get("name"))
        s = similarity(zn, dn)
        if s > 0.8:
            name_score = 25
        elif s > 0.6:
            name_score = 15
        elif s > 0.4:
            name_score = 10

    # Numeric tokens (e.g. "530", "00s") boost confidence for same-model matches
    if numeric_like_tokens(z_tokens) & numeric_like_tokens(d_tokens):
        name_score = min(40, name_score + 10)

    score += name_score

    return min(100, int(score))


def safe_list(val: Any) -> list:
    if val is None:
        return []
    if isinstance(val, list):
        return val
    return []


def run_with_retry(fn, retries: int = 2, delay: float = 1.0):
    """
    Generic retry wrapper – koristi se za scrapers.
    """
    for i in range(retries):
        try:
            return fn()
        except Exception as e:
            logging.warning(f"Retry {i+1}/{retries} for {fn.__name__}: {e}")
            time.sleep(delay)
    logging.error(f"{fn.__name__} failed after {retries} retries")
    return []


# ============================================================
# FASTAPI APP
# ============================================================

app = FastAPI(
    title="Trendplus Global Trends API",
    description="EU Market Scraping & Social Media Trends Analysis",
    version="1.1.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.on_event("shutdown")
async def _shutdown_cleanup():
    # Ensure Playwright (Chromium) is stopped when the API service exits.
    try:
        from scraper import browser_manager

        await browser_manager.close_browser()
        logging.info("Playwright browser closed on shutdown")
    except Exception as e:
        logging.warning(f"Playwright shutdown cleanup failed: {e}")


# ============================================================
# DTO MODELI
# ============================================================

class ZalandoFilterDTO(BaseModel):
    gender: Optional[str] = None
    brand: Optional[str] = None
    category: Optional[str] = "sneaker"
    sort: Optional[str] = "popularity"
    priceMin: Optional[int] = None
    priceMax: Optional[int] = None
    activationDate: Optional[str] = None
    pages: int = 1
    importToCore: bool = False


class DeichmannFilters(BaseModel):
    gender: Optional[str] = None
    category: str = "schuhe-82"
    sort: Optional[str] = None
    priceMin: Optional[int] = None
    priceMax: Optional[int] = None
    sale: Optional[bool] = None
    isNew: Optional[bool] = None
    size: Optional[str] = None
    brand: Optional[str] = None
    isLeather: Optional[str] = None
    waterResistance: Optional[str] = None
    pages: int = 1


class AboutYouFilters(BaseModel):
    # Optional full category URL (e.g. https://www.aboutyou.de/c/frauen/schuhe/stiefeletten-20276)
    url: Optional[str] = None
    gender: Optional[str] = "women"
    # Can be full path (frauen/schuhe/stiefeletten-20276) or slug (stiefeletten-20276)
    category: Optional[str] = "frauen/schuhe/stiefeletten-20276"
    sort: Optional[str] = "popularity"
    priceMin: Optional[float] = None
    priceMax: Optional[float] = None
    # Comma-separated brand filters (e.g. "dr martens,tamaris")
    brand: Optional[str] = None
    # Free-text keyword filter over brand+name
    keyword: Optional[str] = None
    pages: int = 1


# ============================================================
# ROOT
# ============================================================

@app.get("/")
def root():
    return {
        "service": "Trendplus API",
        "status": "running",
        "version": "1.1.0"
    }


# ============================================================
# ZALANDO SCRAPER ENDPOINT
# ============================================================

@app.post("/scrapers/zalando")
async def api_zalando(filters: ZalandoFilterDTO):
    logging.info(f"/scrapers/zalando filters={filters.model_dump()}")

    try:
        from scraper.zalando_playwright import scrape_zalando_playwright
    except Exception as e:
        logging.error("Zalando scraper unavailable: %s", e)
        raise HTTPException(status_code=500, detail="Zalando scraper is unavailable")

    try:
        items = await scrape_zalando_playwright(
            max_pages=filters.pages,
            category=filters.category,
            brand=filters.brand,
            gender=filters.gender,
            sort=filters.sort,
            priceMin=filters.priceMin,
            priceMax=filters.priceMax,
            activationDate=filters.activationDate
        )

        return {"status": "ok", "count": len(items), "items": items}

    except Exception as e:
        logging.exception("Zalando scraper failed: %s", e)
        return {"status": "error", "error": str(e)}


# ============================================================
# DEICHMANN SCRAPER ENDPOINT
# ============================================================

@app.post("/scrapers/deichmann")
async def api_deichmann(filters: DeichmannFilters):
    logging.info(f"/scrapers/deichmann filters={filters.model_dump()}")

    try:
        from scraper.deichmann_scraper import scrape_deichmann_filtered
    except Exception as e:
        logging.error("Deichmann scraper unavailable: %s", e)
        raise HTTPException(status_code=500, detail="Deichmann scraper is unavailable")

    try:
        items = await scrape_deichmann_filtered(**filters.model_dump())
        return {"status": "ok", "count": len(items), "items": items}

    except Exception as e:
        logging.exception("Deichmann scraper failed: %s", e)
        return {"status": "error", "error": str(e)}


# ============================================================
# ABOUT YOU SCRAPER ENDPOINT
# ============================================================

@app.post("/scrapers/aboutyou")
async def api_aboutyou(filters: AboutYouFilters):
    logging.info(f"/scrapers/aboutyou filters={filters.model_dump()}")

    try:
        from scraper.aboutyou_scraper import scrape_aboutyou_filtered
    except Exception as e:
        logging.error("AboutYou scraper unavailable: %s", e)
        raise HTTPException(status_code=500, detail="AboutYou scraper is unavailable")

    try:
        items = await scrape_aboutyou_filtered(**filters.model_dump())
        return {"status": "ok", "count": len(items), "items": items}
    except Exception as e:
        logging.exception("AboutYou scraper failed: %s", e)
        return {"status": "error", "error": str(e)}


# ============================================================
# COMMON — ZAJEDNIČKI / SLIČNI ARTIKLI (Zalando + Deichmann)
# ============================================================

@app.post("/scrapers/common")
async def api_common_items(filters: Dict[str, Any] = Body(...)):
    """
    Fuzzy matching Zalando + Deichmann proizvoda
    - Paralelno asinhrono
    - Sa retry
    - Sa Redis cache-om (ako je dostupan)
    - Sa Elasticsearch indeksiranjem (ako je dostupan)
    """
    logging.info(f"/scrapers/common filters={filters}")

    # 1) Cache key (stabilan, baziran na filterima)
    cache_key = None
    try:
        cache_key = "common:" + json.dumps(filters, sort_keys=True)
    except Exception:
        cache_key = None

    # 2) Pokušaj da pročitaš iz Redis-a
    if cache_key is not None:
        cached = cache_get(cache_key)
        if cached is not None:
            logging.info(f"/scrapers/common cache HIT: {cache_key}")
            return cached
        logging.info(f"/scrapers/common cache MISS: {cache_key}")

    try:
        from scraper.zalando_playwright import scrape_zalando_playwright
        from scraper.deichmann_scraper import scrape_deichmann_filtered
    except Exception as e:
        logging.error("Scrapers not available: %s", e)
        raise HTTPException(status_code=500, detail="Scrapers not available")

    # 3) Direct async calls instead of run_in_executor
    async def run_zalando():
        return await scrape_zalando_playwright(
            max_pages=filters.get("pages", 1),
            category=filters.get("category"),
            brand=filters.get("brand"),
            gender=filters.get("gender"),
            sort=filters.get("sort"),
            priceMin=filters.get("priceMin"),
            priceMax=filters.get("priceMax"),
            activationDate=filters.get("activationDate")
        )

    async def run_deichmann():
        try:
            print(">>> Starting Deichmann scraper")
            cat = CATEGORY_MAP.get(filters.get("category"), "schuhe-82")
            print(f">>> Deichmann mapped category: {filters.get('category')} -> {cat}")
            result = await scrape_deichmann_filtered(
                gender=filters.get("gender"),
                category=cat,
                sort=filters.get("sort"),
                priceMin=filters.get("priceMin"),
                priceMax=filters.get("priceMax"),
                sale=filters.get("sale"),
                isNew=filters.get("isNew"),
                size=filters.get("size"),
                brand=filters.get("brand"),
                isLeather=filters.get("isLeather"),
                waterResistance=filters.get("waterResistance"),
                pages=filters.get("pages", 1)
            )
            print(f">>> Deichmann returned {len(result) if result else 0} items")
            return result
        except Exception as e:
            import traceback
            print("!!! Deichmann scraper FAILED:")
            print(traceback.format_exc())
            return []

    try:
        zalando_items, deichmann_items = await asyncio.wait_for(
            asyncio.gather(run_zalando(), run_deichmann()),
            timeout=MAX_SCRAPER_TIME
        )
    except asyncio.TimeoutError:
        logging.error("Common scraper timeout")
        raise HTTPException(status_code=504, detail="Scrapers timed out")

    zalando_items = safe_list(zalando_items)
    deichmann_items = safe_list(deichmann_items)

    if not zalando_items:
        logging.warning("Zalando returned empty result in /scrapers/common")
    if not deichmann_items:
        logging.warning("Deichmann returned empty result in /scrapers/common")

    # 4) Fuzzy matching
    matches: List[dict] = []
    min_score = filters.get("minScore", 40)  # Smanjeno sa 60 na 40 za više matcheva
    
    # Debug: Show first few comparisons
    debug_count = 0
    for z in zalando_items:
        for d in deichmann_items:
            score = score_match(z, d)
            
            # Debug: show only meaningful (non-zero) comparisons
            if debug_count < 3 and score > 0:
                logging.info(
                    f"[DEBUG] Score={score} | "
                    f"Z: {z.get('brand')} {str(z.get('name') or '')[:40]} | "
                    f"D: {d.get('brand')} {str(d.get('name') or '')[:40]}"
                )
                debug_count += 1
            
            if score >= min_score:
                matches.append({
                    "score": score,
                    "brand": z.get("brand"),
                    "type": get_shoe_type(z.get("name")),
                    "zalando": {
                        "name": z.get("name"),
                        "price": z.get("price"),
                        "image": z.get("image_url"),
                        "url": z.get("url")
                    },
                    "deichmann": {
                        "name": d.get("name"),
                        "price": d.get("price"),
                        "image": d.get("image"),
                        "url": d.get("url")
                    }
                })

    logging.info(f"Fuzzy matching: {len(zalando_items)} Zalando × {len(deichmann_items)} Deichmann = {len(matches)} matches (minScore={min_score})")

    matches.sort(key=lambda m: -m["score"])

    response = {
        "status": "ok",
        "count": len(matches),
        "items": matches
    }

    # 5) Cache rezultat (ako je moguće)
    if cache_key is not None:
        cache_set(cache_key, response, ttl_seconds=600)

    # 6) Index u Elasticsearch (opciono)
    es_index_common_matches(matches)

    logging.info(f"/scrapers/common done, matches={len(matches)}")
    return response


# ============================================================
# SOCIAL TRENDS ENDPOINT
# ============================================================

@app.get("/trends/social")
def get_social_trends(category: str = "Patike"):
    try:
        trends = get_category_trends(category)
        return {"category": category, "trends": trends, "count": len(trends)}
    except Exception as e:
        logging.error(e)
        return {"category": category, "trends": [], "count": 0}


# ============================================================
# RELEASE CALENDAR (Zalando)
# ============================================================

@app.get("/api/release-calendar")
def get_release_calendar(gender: str = "mens"):
    url = (
        "https://www.zalando.co.uk/release-calendar/mens-shoes-sneakers/"
        if gender == "mens"
        else "https://www.zalando.co.uk/release-calendar/womens-shoes-sneakers/"
    )

    if scrape_zalando_release_calendar:
        try:
            items = scrape_zalando_release_calendar(url, headless=True)
            return {"gender": gender, "items": items, "count": len(items)}
        except Exception as e:
            logging.error("Release calendar failed: %s", e)

    return {"gender": gender, "items": [], "count": 0}


# ============================================================
# UVICORN BOOT
# ============================================================

if __name__ == "__main__":
    import uvicorn
    print("🚀 Trendplus API starting on http://0.0.0.0:8000")
    uvicorn.run(app, host="0.0.0.0", port=8000, log_level="info")
