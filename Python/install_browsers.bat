@echo off
REM Install Playwright browsers for web scraping

echo ========================================
echo Installing Playwright Browsers
echo ========================================
echo.

echo This will download Chromium browser (~170MB)
echo.
pause

echo Installing playwright and browsers...
python -m pip install playwright

echo.
echo Installing browser binaries...
python -m playwright install chromium

echo.
echo ========================================
echo Installation Complete!
echo ========================================
echo.
echo Playwright browsers are now ready
echo You can now run scrapers with:
echo   python test_scrapers.py
echo.
pause
