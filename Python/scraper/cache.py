"""
Cache system for social media API responses
Reduces API calls and improves performance
"""

import json
import os
import time
from typing import Any, Optional

# Cache configuration
CACHE_FILE = "social_cache.json"
TTL = 60 * 60 * 6  # 6 hours cache validity

def load_cache() -> dict:
    """Load cache from JSON file"""
    if not os.path.exists(CACHE_FILE):
        return {}
    
    try:
        with open(CACHE_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    except (json.JSONDecodeError, IOError):
        print("⚠️ Cache file corrupted, creating new cache")
        return {}


def save_cache(cache: dict) -> None:
    """Save cache to JSON file"""
    try:
        with open(CACHE_FILE, "w", encoding="utf-8") as f:
            json.dump(cache, f, indent=2, ensure_ascii=False)
    except IOError as e:
        print(f"⚠️ Failed to save cache: {e}")


def get_cached(key: str) -> Optional[Any]:
    """
    Get cached value if not expired
    
    Args:
        key: Cache key
    
    Returns:
        Cached value or None if expired/not found
    """
    cache = load_cache()
    entry = cache.get(key)
    
    if entry and time.time() - entry.get("timestamp", 0) < TTL:
        print(f"💾 Cache HIT: {key}")
        return entry.get("value")
    
    print(f"🔍 Cache MISS: {key}")
    return None


def set_cached(key: str, value: Any) -> None:
    """
    Store value in cache with timestamp
    
    Args:
        key: Cache key
        value: Value to cache
    """
    cache = load_cache()
    cache[key] = {
        "value": value,
        "timestamp": time.time()
    }
    save_cache(cache)
    print(f"✅ Cached: {key}")


def clear_cache() -> None:
    """Clear entire cache"""
    if os.path.exists(CACHE_FILE):
        os.remove(CACHE_FILE)
        print("🗑️ Cache cleared")


def get_cache_stats() -> dict:
    """Get cache statistics"""
    cache = load_cache()
    
    now = time.time()
    valid_entries = sum(1 for entry in cache.values() 
                       if now - entry.get("timestamp", 0) < TTL)
    expired_entries = len(cache) - valid_entries
    
    return {
        "total_entries": len(cache),
        "valid_entries": valid_entries,
        "expired_entries": expired_entries,
        "cache_size_kb": os.path.getsize(CACHE_FILE) / 1024 if os.path.exists(CACHE_FILE) else 0,
        "ttl_hours": TTL / 3600
    }


if __name__ == "__main__":
    # Test cache system
    print("Testing cache system...\n")
    
    # Set test data
    set_cached("test_key", {"data": "test_value"})
    
    # Get cached data
    result = get_cached("test_key")
    print(f"Retrieved: {result}\n")
    
    # Get stats
    stats = get_cache_stats()
    print("Cache stats:")
    for key, value in stats.items():
        print(f"  {key}: {value}")
    
    # Clear cache
    clear_cache()
