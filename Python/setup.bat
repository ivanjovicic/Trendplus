@echo off
REM Setup Python environment for Global Trends Scraper

echo ========================================
echo Trendplus Global Trends Setup
echo ========================================
echo.

REM Check if Python is installed
python --version >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Python is not installed!
    echo Please install Python 3.10+ from https://python.org
    pause
    exit /b 1
)

echo [1/5] Creating virtual environment...
python -m venv venv

echo [2/5] Activating virtual environment...
call venv\Scripts\activate.bat

echo [3/5] Installing dependencies...
pip install --upgrade pip
pip install -r requirements.txt

echo [4/5] Installing Playwright browsers...
playwright install chromium
echo   Chromium browser installed for web scraping

echo [5/5] Creating .env file...
if not exist .env (
    echo # Database Configuration > .env
    echo DB_HOST=ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech >> .env
    echo DB_PORT=5432 >> .env
    echo DB_NAME=analytics >> .env
    echo DB_USER=neondb_owner >> .env
    echo DB_PASS=npg_7hUftT3sXHgR >> .env
    echo. >> .env
    echo # API Configuration >> .env
    echo RAPIDAPI_KEY= >> .env
    echo. >> .env
    echo # .NET API >> .env
    echo DOTNET_API_URL=http://localhost:8080 >> .env
    
    echo [INFO] .env file created. Please fill in RAPIDAPI_KEY if needed.
)

echo.
echo ========================================
echo SUCCESS! Setup complete
echo ========================================
echo.
echo To activate the environment:
echo   venv\Scripts\activate.bat
echo.
echo To run the scraper:
echo   python run_all.py
echo.
echo To test Deichmann scraper:
echo   python -m scraper.deichmann_scraper
echo.

pause
