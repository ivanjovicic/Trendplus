from playwright.async_api import async_playwright
import asyncio
import os
from typing import Optional

# Global async browser instance
_global_pw = None
_global_browser = None
_global_context = None
_init_lock = None
_browser_pid: Optional[int] = None


def _env_flag(name: str, default: bool) -> bool:
    val = os.environ.get(name)
    if val is None:
        return default
    return val.strip().lower() in {"1", "true", "yes", "y", "on"}

async def init_browser():
    """Initialize global async browser once. All coroutines share the same browser/context."""
    global _global_pw, _global_browser, _global_context, _init_lock, _browser_pid
    
    # Create lock if not exists
    if _init_lock is None:
        _init_lock = asyncio.Lock()
    
    async with _init_lock:
        if _global_browser is not None:
            return
        
        _global_pw = await async_playwright().start()
        headless = _env_flag("PLAYWRIGHT_HEADLESS", False)
        _global_browser = await _global_pw.chromium.launch(headless=headless)
        _global_context = await _global_browser.new_context(
            viewport={"width": 1400, "height": 900},
            locale="en-GB",
            extra_http_headers={"Accept-Language": "en-GB,en;q=0.9"}
        )

        try:
            proc = getattr(_global_browser, "process", None)
            _browser_pid = getattr(proc, "pid", None) if proc else None
        except Exception:
            _browser_pid = None
        print("✓ Async Browser initialized - all tabs will open in one window")

async def get_context():
    """Get the global shared async Playwright context."""
    if _global_context is None:
        await init_browser()
    return _global_context

async def close_browser():
    """Close the global async browser."""
    global _global_browser, _global_context, _global_pw, _init_lock, _browser_pid

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
