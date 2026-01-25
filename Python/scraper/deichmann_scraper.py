"""
Deichmann.com scraper using Playwright
Real browser automation for JavaScript-rendered pages
"""

from playwright.sync_api import sync_playwright, TimeoutError as PlaywrightTimeout
from typing import List, Dict, Optional
import time

def scrape_deichmann(max_pages: int = 10, headless: bool = True) -> List[Dict]:
    """
    Scrape trending products from Deichmann.com using Playwright
    
    Args:
        max_pages: Maximum number of pages to scrape
        headless: Run browser in headless mode (no GUI)
    
    Returns:
        List of product dictionaries
    """
    products = []
    
    print(f"🔍 Scraping Deichmann.com with Playwright")
    print(f"  Headless: {headless}")

    with sync_playwright() as p:
        # Launch browser
        browser = p.chromium.launch(headless=headless)
        
        # Create context with realistic headers
        context = browser.new_context(
            user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            locale="de-DE",
            viewport={"width": 1920, "height": 1080}
        )
        
        page = context.new_page()

        for page_num in range(1, max_pages + 1):
            url = f"https://www.deichmann.com/de-de/damen/schuhe/sneaker?page={page_num}"
            print(f"  Page {page_num}/{max_pages}...", end=" ")

            try:
                # Navigate to page
                page.goto(url, timeout=15000, wait_until="networkidle")
                
                # Wait for products to load
                page.wait_for_selector("article.product-tile", timeout=10000)
                
                # Optional: Scroll to trigger lazy loading
                page.evaluate("window.scrollTo(0, document.body.scrollHeight)")
                time.sleep(1)
                
            except PlaywrightTimeout:
                print("❌ Timeout")
                break
            except Exception as e:
                print(f"❌ Error: {e}")
                break

            # Get all product tiles
            items = page.query_selector_all("article.product-tile")
            
            if not items:
                print("❌ No products")
                break

            # Extract data from each product
            for i, item in enumerate(items):
                try:
                    # Query selectors
                    name_elem = item.query_selector("h3, .product-tile__name, .product-name")
                    price_elem = item.query_selector("span.price, .product-tile__price, .product-price")
                    img_elem = item.query_selector("img")
                    brand_elem = item.query_selector(".product-tile__brand, .brand")
                    
                    # Extract image URL (handle lazy loading)
                    image_url = ""
                    if img_elem:
                        image_url = (
                            img_elem.get_attribute("data-src") or 
                            img_elem.get_attribute("src") or 
                            img_elem.get_attribute("data-lazy-src") or
                            ""
                        )
                    
                    product = {
                        "rank": (page_num - 1) * len(items) + (i + 1),
                        "product_id": item.get_attribute("data-id") or item.get_attribute("data-product-id") or f"deich_{page_num}_{i}",
                        "name": name_elem.inner_text().strip() if name_elem else "",
                        "brand": brand_elem.inner_text().strip() if brand_elem else "Deichmann",
                        "price": parse_price(price_elem.inner_text() if price_elem else ""),
                        "image_url": image_url,
                        "category": "Sneaker",
                        "color": extract_color_from_name(name_elem.inner_text() if name_elem else ""),
                        "season": detect_season(name_elem.inner_text() if name_elem else ""),
                        "source": "deichmann"
                    }
                    
                    if product["name"]:
                        products.append(product)
                        
                except Exception as e:
                    print(f"⚠️ Error parsing product {i}: {e}")
                    continue

            print(f"✅ {len(items)} products")
            
            # Rate limiting
            time.sleep(2)

        # Cleanup
        context.close()
        browser.close()

    print(f"\n📊 Total products scraped: {len(products)}")
    return products


def parse_price(price_str: str) -> Optional[float]:
    """
    Parse German price format (e.g., '49,99 €' or '€49,99')
    """
    try:
        # Remove currency symbols and whitespace
        clean = price_str.replace("€", "").replace("EUR", "").replace(" ", "").strip()
        
        # Replace comma with dot for float conversion
        clean = clean.replace(",", ".")
        
        return float(clean)
    except (ValueError, AttributeError):
        return None


def extract_color_from_name(name: str) -> str:
    """
    Extract color from product name
    """
    name_lower = name.lower()
    
    colors = {
        "schwarz": "Black",
        "weiß": "White",
        "weiss": "White",
        "rot": "Red",
        "blau": "Blue",
        "grün": "Green",
        "gelb": "Yellow",
        "grau": "Grey",
        "braun": "Brown",
        "rosa": "Pink",
        "beige": "Beige"
    }
    
    for german, english in colors.items():
        if german in name_lower:
            return english
    
    return ""


def detect_season(name: str) -> str:
    """
    Detect season from product name
    """
    name_lower = name.lower()
    
    winter_keywords = ["winter", "boot", "stiefel", "warm", "gefüttert"]
    summer_keywords = ["sommer", "sandal", "sandale", "flip", "open"]
    
    if any(word in name_lower for word in winter_keywords):
        return "Jesen-Zima"
    elif any(word in name_lower for word in summer_keywords):
        return "Prolece-Leto"
    else:
        return "Cela godina"


# Test scraper
if __name__ == "__main__":
    print("Testing Deichmann Playwright scraper...\n")
    
    # Run with GUI for debugging (headless=False)
    # Run headless for production (headless=True)
    products = scrape_deichmann(max_pages=2, headless=True)
    
    if products:
        print("\n🎯 Sample results:")
        for p in products[:5]:
            print(f"  {p['rank']}. {p['brand']} - {p['name']}")
            print(f"      €{p['price']} | {p['color']} | {p['season']}")
            print(f"      {p['image_url'][:60]}...")
    else:
        print("❌ No products scraped!")
