import asyncio
import logging
import os
from typing import Optional

from playwright.async_api import Page, async_playwright

logger = logging.getLogger("scraper.browser_manager")

# Global async browser instance
_global_pw = None
_global_browser = None
_global_context = None
_init_lock: Optional[asyncio.Lock] = None
_browser_pid: Optional[int] = None

_max_tabs = max(1, int(os.environ.get("PLAYWRIGHT_MAX_TABS", "8")))
_tab_semaphore = asyncio.Semaphore(_max_tabs)
_active_tabs = 0
_active_tabs_lock = asyncio.Lock()
_tracked_page_ids: set[int] = set()


def _env_flag(name: str, default: bool) -> bool:
    val = os.environ.get(name)
    if val is None:
        return default
    return val.strip().lower() in {"1", "true", "yes", "y", "on"}


async def init_browser() -> None:
    """Initialize global async browser once. All coroutines share the same browser/context."""
    global _global_pw, _global_browser, _global_context, _init_lock, _browser_pid

    if _init_lock is None:
        _init_lock = asyncio.Lock()

    async with _init_lock:
        if _global_browser is not None and _global_context is not None:
            return

        _global_pw = await async_playwright().start()
        headless = _env_flag("PLAYWRIGHT_HEADLESS", True)
        _global_browser = await _global_pw.chromium.launch(headless=headless)
        _global_context = await _global_browser.new_context(
            viewport={"width": 1400, "height": 900},
            locale="en-GB",
            extra_http_headers={"Accept-Language": "en-GB,en;q=0.9"},
        )

        try:
            proc = getattr(_global_browser, "process", None)
            _browser_pid = getattr(proc, "pid", None) if proc else None
        except Exception:
            _browser_pid = None

        logger.info(
            "Playwright browser initialized (pid=%s, max_tabs=%s).",
            _browser_pid,
            _max_tabs,
        )


async def get_context():
    """Get the global shared async Playwright context."""
    if _global_context is None:
        await init_browser()
    return _global_context


async def block_heavy_resources(page: Page) -> None:
    """Block heavy assets to reduce memory and network usage."""

    async def _abort_route(route):
        try:
            await route.abort()
        except Exception:
            await route.continue_()

    patterns = [
        "**/*.png",
        "**/*.jpg",
        "**/*.jpeg",
        "**/*.gif",
        "**/*.svg",
        "**/*.webp",
        "**/*.woff",
        "**/*.woff2",
        "**/*.ttf",
        "**/*.css",
    ]

    for pattern in patterns:
        await page.route(pattern, _abort_route)


async def _mark_page_acquired(page: Page) -> None:
    global _active_tabs
    async with _active_tabs_lock:
        _active_tabs += 1
        _tracked_page_ids.add(id(page))
        logger.info(
            "Tab acquired (active=%s, available=%s, page_id=%s).",
            _active_tabs,
            getattr(_tab_semaphore, "_value", "?"),
            id(page),
        )


async def _mark_page_released(page: Page, should_release_semaphore: bool) -> None:
    global _active_tabs
    async with _active_tabs_lock:
        if should_release_semaphore:
            _active_tabs = max(0, _active_tabs - 1)
        logger.info(
            "Tab released (active=%s, available=%s, page_id=%s).",
            _active_tabs,
            getattr(_tab_semaphore, "_value", "?"),
            id(page),
        )


async def new_page() -> Page:
    """
    Acquire semaphore + open a new page in the shared context.
    Must be paired with `release_page(page)` in a finally block.
    """
    await _tab_semaphore.acquire()
    try:
        context = await get_context()
        page = await context.new_page()
        await block_heavy_resources(page)
        await _mark_page_acquired(page)
        return page
    except Exception:
        _tab_semaphore.release()
        raise


async def release_page(page: Optional[Page]) -> None:
    """Close page and release semaphore exactly once for tracked pages."""
    if page is None:
        return

    page_id = id(page)
    should_release = False
    async with _active_tabs_lock:
        if page_id in _tracked_page_ids:
            _tracked_page_ids.remove(page_id)
            should_release = True

    try:
        await page.close()
    except Exception:
        pass
    finally:
        if should_release:
            _tab_semaphore.release()
        await _mark_page_released(page, should_release)


async def close_browser() -> None:
    """Close the global async browser."""
    global _global_browser, _global_context, _global_pw, _init_lock, _browser_pid, _active_tabs

    if _init_lock is None:
        _init_lock = asyncio.Lock()

    async with _init_lock:
        context = _global_context
        browser = _global_browser
        pw = _global_pw

        _global_context = None
        _global_browser = None
        _global_pw = None
        _browser_pid = None

        if context:
            try:
                await context.close()
            except Exception:
                pass

        if browser:
            try:
                await browser.close()
            except Exception:
                pass

        if pw:
            try:
                await pw.stop()
            except Exception:
                pass

        async with _active_tabs_lock:
            _active_tabs = 0
            _tracked_page_ids.clear()

