from dataclasses import dataclass, field
from typing import Any, Dict, Optional
from datetime import datetime

@dataclass
class ScrapedItem:
    # Mandatory fields for scoring
    source: str              # "zalando" | "humanic" | "deichmann"
    market: str              # "DE", "AT", "CH", "HU", "RO"
    brand: str
    name: str
    priceValue: float        # Clean float, e.g., 99.95
    currency: str            # "EUR","HUF","RON","CHF"
    url: str
    imageUrl: Optional[str]

    # Ranking information
    rank: int                # Global rank within (source, market, query)
    page: int                # Page number (1-based)
    positionOnPage: int      # Index on the page (1-based)
    sortMode: str            # e.g., "popularity","new","price_asc",...

    # Product identity
    sku: Optional[str] = None
    productId: Optional[str] = None
    category: Optional[str] = None   # "sneaker", "boot", ...
    gender: Optional[str] = None     # "women","men","unisex"

    # Status flags
    isNew: bool = False
    isOnSale: bool = False
    hasImage: bool = True

    # Backend ranking metadata
    backend: Optional[str] = None         # e.g., "algolia"
    backendIndex: Optional[str] = None    # e.g., "live_hum_products_at_sold_items"
    backendRank: Optional[int] = None     # Position provided by backend
    backendQueryId: Optional[str] = None  # Algolia queryID if available

    # Social scoring
    socialScore: Optional[float] = None
    previousSocialScore: Optional[float] = None

    # Extra fields
    scrapedAt: datetime = field(default_factory=datetime.utcnow)
    raw: Dict[str, Any] = field(default_factory=dict)  # Raw data from scraper