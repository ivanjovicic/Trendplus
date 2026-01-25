@echo off
REM Test Playwright scrapers

echo ========================================
echo Testing Trendplus Scrapers
echo ========================================
echo.

REM Activate virtual environment
call venv\Scripts\activate.bat

REM Run tests
python test_scrapers.py

echo.
pause
