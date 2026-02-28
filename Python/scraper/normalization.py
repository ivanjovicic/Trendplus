import re
from typing import Optional, Tuple
from urllib.parse import urlparse, urlunparse

def parse_price(value: Optional[str], market: str) -> Tuple[Optional[float], Optional[str]]:
    if not value:
        return None, None

    value = value.replace("\u00a0", " ").strip()
    currency = None

    if market in {"DE", "AT", "CH"}:
        currency = "EUR"
    elif market == "HU":
        currency = "HUF"
    elif market == "RO":
        currency = "RON"

    value = re.sub(r"[^0-9,."]", "", value)

    if "," in value and "." in value:
        value = value.replace(".", "").replace(",", ".")
    elif "," in value:
        value = value.replace(",", ".")

    try:
        return float(value), currency
    except ValueError:
        return None, currency

def normalize_brand(name: Optional[str]) -> str:
    if not name:
        return ""
    return name.strip().title()

def infer_category_from_name(name: str) -> Optional[str]:
    name = name.lower()
    if "sneaker" in name:
        return "sneaker"
    if "boot" in name or "stiefel" in name:
        return "boot"
    if "heel" in name or "pumps" in name:
        return "heels"
    if "sandals" in name or "sandale" in name:
        return "sandals"
    if "loafer" in name or "mokassin" in name:
        return "loafers"
    if "ballerina" in name:
        return "flats"
    return None

def compute_rank(page: int, position_on_page: int, page_size: int) -> int:
    return (page - 1) * page_size + position_on_page

def normalize_canonical_url(url: str) -> Optional[str]:
    if not url:
        return None
    parsed = urlparse(url)
    return urlunparse(parsed._replace(query=""))

def extract_numeric_suffix(url: str) -> Optional[str]:
    if not url:
        return None
    match = re.search(r"-([0-9]{5,})", url)
    return match.group(1) if match else None