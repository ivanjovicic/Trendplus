@echo off
REM Setup script for Trendplus Image Embedding Service

echo ========================================
echo Trendplus Embedding Service - Setup
echo ========================================
echo.

REM Check Python version
python --version >NUL 2>&1
if errorlevel 1 (
    echo [ERROR] Python is not installed or not in PATH
    echo Please install Python 3.9 or higher
    pause
    exit /b 1
)

echo [INFO] Python version:
python --version
echo.

REM Create virtual environment
echo [INFO] Creating virtual environment...
python -m venv venv
if errorlevel 1 (
    echo [ERROR] Failed to create virtual environment
    pause
    exit /b 1
)

REM Activate virtual environment
echo [INFO] Activating virtual environment...
call venv\Scripts\activate.bat

REM Upgrade pip
echo [INFO] Upgrading pip...
python -m pip install --upgrade pip

REM Install dependencies
echo [INFO] Installing dependencies...
pip install -r requirements.txt
if errorlevel 1 (
    echo [ERROR] Failed to install dependencies
    pause
    exit /b 1
)

echo.
echo ========================================
echo Setup Complete!
echo ========================================
echo.
echo Next steps:
echo   1. Run: start-service.bat
echo   2. Open browser: http://localhost:8000/docs
echo   3. Test the API
echo.
echo To change model (CLIP/SigLIP):
echo   Edit app.py, line 35: MODEL_TYPE = "clip" or "siglip"
echo.

pause
