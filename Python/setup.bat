@echo off
REM Setup Python environment for Trendplus Global Trends

echo ========================================
echo Python Environment Setup
echo ========================================
echo.

REM Try different Python commands
set "PYTHON_CMD="

REM Check for py launcher (most reliable on Windows)
py --version >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    set "PYTHON_CMD=py"
    echo [OK] Found Python via py launcher
    goto :create_venv
)

REM Check for python3
python3 --version >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    set "PYTHON_CMD=python3"
    echo [OK] Found python3
    goto :create_venv
)

REM Check for python
python --version >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    set "PYTHON_CMD=python"
    echo [OK] Found python
    goto :create_venv
)

REM Python not found
echo [ERROR] Python is not installed or not in PATH!
echo.
echo Please install Python 3.10+ from https://python.org
echo Make sure to check "Add Python to PATH" during installation
echo.
pause
exit /b 1

:create_venv
echo.
echo [Step 1/3] Creating virtual environment...
if exist "venv" (
    echo Virtual environment already exists, skipping...
) else (
    %PYTHON_CMD% -m venv venv
    if %ERRORLEVEL% NEQ 0 (
        echo [ERROR] Failed to create virtual environment
        pause
        exit /b 1
    )
    echo [OK] Virtual environment created
)

echo.
echo [Step 2/3] Activating virtual environment...
call venv\Scripts\activate.bat
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to activate virtual environment
    pause
    exit /b 1
)

echo.
echo [Step 3/3] Installing dependencies...
python -m pip install --upgrade pip
pip install -r requirements.txt
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to install dependencies from requirements.txt
    pause
    exit /b 1
)

REM Install Playwright and browsers
echo.
echo [Step 4/4] Installing Playwright and browsers...
python -m pip install playwright
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to install Playwright
    pause
    exit /b 1
)
python -m playwright install chromium
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to install Playwright browsers
    pause
    exit /b 1
)

REM Ensure requests is installed (if not in requirements.txt)
python -m pip install requests

echo.
echo ========================================
echo Setup completed successfully!
echo ========================================
echo.
echo To start the API server:
echo   1. cd Python
echo   2. start_api.bat
echo.
echo To run Zalando scraper directly:
echo   1. Activate venv: venv\Scripts\activate
echo   2. python -m scraper.zalando_playwright
echo.
pause
