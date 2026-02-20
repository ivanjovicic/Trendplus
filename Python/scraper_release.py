from playwright.sync_api import sync_playwright
import time
import os
import logging
from urllib.parse import urlparse, urlunparse

logging.basicConfig(level=logging.INFO)


def _normalize_zalando_url(url: str) -> str:
    try:
        p = urlparse(url)
        host = p.netloc or ''
        new_host = host
        # Map various Zalando hosts to the en.zalando.de host
        if 'zalando.co.uk' in host:
            new_host = 'en.zalando.de'
        elif 'zalando.de' in host:
            # ensure en subdomain
            if not host.startswith('en.'):
                new_host = 'en.zalando.de'
        # rebuild url
        if new_host != host:
            new_p = p._replace(netloc=new_host, scheme='https')
            new_url = urlunparse(new_p)
            logging.info('Normalized Zalando URL from %s to %s', url, new_url)
            return new_url
    except Exception:
        pass
    return url


def scrape_zalando_release_calendar(url: str, headless: bool = False):
    """Open a Zalando release-calendar URL and extract simple release items.

    This is a lightweight best-effort parser: it scrolls the page and tries a
    few common selectors to find product cards or release entries. Returns a
    list of dicts with keys: name, brand (optional), image (optional), release_date (optional).
    """
    logging.info("Scraping release calendar URL: %s (headless=%s)", url, headless)
    results = []

    # Normalize to en.zalando.de to avoid .co.uk or other domains
    url = _normalize_zalando_url(url)

    with sync_playwright() as pw:
        browser = pw.chromium.launch(headless=headless)
        context = browser.new_context(locale="en-GB", extra_http_headers={"Accept-Language": "en-GB,en;q=0.9"})
        page = context.new_page()

        try:
            page.goto(url, timeout=0)
            time.sleep(2)

            # close cookie banners if present
            for txt in ["Only essential", "Accept all", "Accept", "Allow all", "Nur das Nötigste", "Akzeptieren"]:
                try:
                    page.click(f"button:has-text('{txt}')", timeout=500)
                except Exception:
                    pass

            # scroll a bit to load lazy content
            for _ in range(12):
                page.mouse.wheel(0, 1500)
                time.sleep(0.5)

            # try several selectors
            selectors = [
                'article[class*="z5x6ht"]',
                'div[class*="release"]',
                'div[class*="release-item"]',
                'a[data-testid="product-link"]',
                'div[data-testid="release-item"]'
            ]

            seen = set()
            elements = []
            for sel in selectors:
                try:
                    els = page.query_selector_all(sel)
                    if els:
                        elements.extend(els)
                except Exception:
                    pass

            # Fallback: collect image links and anchors
            if not elements:
                try:
                    anchors = page.query_selector_all('a')
                    elements.extend(anchors[:200])
                except Exception:
                    pass

            for el in elements:
                try:
                    # Try to extract a name
                    name = None
                    try:
                        name = el.inner_text().strip()
                    except Exception:
                        pass

                    # Try to find image
                    img = None
                    try:
                        img_el = el.query_selector('img')
                        if img_el:
                            img = img_el.get_attribute('src') or img_el.get_attribute('data-src')
                    except Exception:
                        pass

                    # Try to find brand or release date nearby
                    brand = None
                    release_date = None
                    try:
                        brand_el = el.query_selector("span[data-testid='product-brand']")
                        if brand_el:
                            brand = brand_el.inner_text().strip()
                    except Exception:
                        pass

                    try:
                        # common markup may include <time> for dates
                        time_el = el.query_selector('time')
                        if time_el:
                            release_date = time_el.get_attribute('datetime') or time_el.inner_text().strip()
                    except Exception:
                        pass

                    key = (name or '') + '|' + (img or '')
                    if key in seen:
                        continue
                    seen.add(key)

                    if name or img:
                        results.append({
                            'name': name,
                            'brand': brand,
                            'image': img,
                            'release_date': release_date
                        })
                except Exception:
                    continue

            # Save debug HTML
            debug_path = os.path.join(os.path.dirname(__file__), 'release_calendar_debug.html')
            try:
                with open(debug_path, 'w', encoding='utf-8') as f:
                    f.write(page.content())
                logging.info('Saved debug HTML to %s', debug_path)
            except Exception:
                pass

        finally:
            browser.close()

    logging.info('Scraped %d release items', len(results))
    return results
