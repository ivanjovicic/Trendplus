"""
Utility functions for web scraping
"""

import time
import random
import requests
from typing import Optional

def safe_request(url: str, headers: dict, retries: int = 3, timeout: int = 10) -> Optional[str]:
    """
    Make HTTP request with retry logic and rate limiting
    
    Args:
        url: URL to request
        headers: HTTP headers
        retries: Number of retry attempts
        timeout: Request timeout in seconds
    
    Returns:
        HTML content or None on failure
    """
    for attempt in range(retries):
        try:
            response = requests.get(url, headers=headers, timeout=timeout)
            
            if response.status_code == 200:
                return response.text
            elif response.status_code == 429:  # Rate limited
                wait_time = (attempt + 1) * 5
                print(f"⏳ Rate limited, waiting {wait_time}s...")
                time.sleep(wait_time)
            else:
                print(f"❌ HTTP {response.status_code}")
                
        except requests.exceptions.Timeout:
            print(f"⏱️ Timeout (attempt {attempt + 1}/{retries})")
        except requests.exceptions.RequestException as e:
            print(f"❌ Request error: {e}")
        
        # Random delay between retries
        if attempt < retries - 1:
            delay = random.uniform(1.5, 3.0)
            time.sleep(delay)
    
    return None


def clean_price(price_str: str) -> float:
    """
    Clean and convert price string to float
    Handles: €49,99 / $49.99 / 49,99€ / etc.
    """
    try:
        # Remove currency symbols
        clean = price_str.replace("€", "").replace("$", "").replace("USD", "").replace("EUR", "")
        clean = clean.strip()
        
        # Handle comma as decimal separator (EU format)
        if "," in clean and "." not in clean:
            clean = clean.replace(",", ".")
        elif "," in clean and "." in clean:
            # Handle formats like 1.234,56 (EU) or 1,234.56 (US)
            if clean.index(",") > clean.index("."):
                # EU format: 1.234,56
                clean = clean.replace(".", "").replace(",", ".")
            else:
                # US format: 1,234.56
                clean = clean.replace(",", "")
        
        return float(clean)
    except (ValueError, AttributeError):
        return 0.0


def extract_season_from_text(text: str) -> str:
    """
    Detect season from product name or description
    """
    text_lower = text.lower()
    
    winter_keywords = ["winter", "boot", "stiefel", "warm", "fur", "snow", "winterstiefel"]
    summer_keywords = ["summer", "sandal", "sandale", "flip", "open", "beach", "sommerschuhe"]
    spring_keywords = ["spring", "frühjahr", "light", "sneaker"]
    
    if any(word in text_lower for word in winter_keywords):
        return "Jesen-Zima"
    elif any(word in text_lower for word in summer_keywords):
        return "Prolece-Leto"
    elif any(word in text_lower for word in spring_keywords):
        return "Prolece"
    else:
        return "Cela godina"


def normalize_category(category: str) -> str:
    """
    Normalize product category to match your database
    """
    category_mapping = {
        "sneaker": "Patike",
        "sneakers": "Patike",
        "turnschuhe": "Patike",
        "sandalen": "Sandale",
        "sandals": "Sandale",
        "stiefel": "Cizme",
        "boots": "Cizme",
        "schuhe": "Cipele",
        "shoes": "Cipele"
    }
    
    category_lower = category.lower()
    return category_mapping.get(category_lower, category)


if __name__ == "__main__":
    # Test functions
    print("Testing clean_price:")
    print(f"  €49,99 -> {clean_price('€49,99')}")
    print(f"  $49.99 -> {clean_price('$49.99')}")
    print(f"  1.234,56€ -> {clean_price('1.234,56€')}")
    
    print("\nTesting extract_season:")
    print(f"  'Winter Boots' -> {extract_season_from_text('Winter Boots')}")
    print(f"  'Summer Sandals' -> {extract_season_from_text('Summer Sandals')}")
    print(f"  'Air Max' -> {extract_season_from_text('Air Max')}")
