@echo off
REM Trendplus Image Embedding Service - Startup Script
REM Run this to start the Python embedding service

echo ========================================
echo Trendplus Image Embedding Service
echo ========================================
echo.

REM Check if virtual environment exists
if not exist "venv\" (
    echo [ERROR] Virtual environment not found!
    echo.
    echo Please run setup first:
    echo   python -m venv venv
    echo   venv\Scripts\activate
    echo   pip install -r requirements.txt
    echo.
    pause
    exit /b 1
)

REM Activate virtual environment
echo [INFO] Activating virtual environment...
call venv\Scripts\activate.bat

REM Check if dependencies are installed
python -c "import fastapi" 2>NUL
if errorlevel 1 (
    echo [ERROR] Dependencies not installed!
    echo.
    echo Installing dependencies...
    pip install -r requirements.txt
    if errorlevel 1 (
        echo [ERROR] Failed to install dependencies
        pause
        exit /b 1
    )
)

REM Start service
echo.
echo [INFO] Starting embedding service...
echo [INFO] Service will be available at: http://localhost:8000
echo [INFO] API Documentation: http://localhost:8000/docs
echo [INFO] Health Check: http://localhost:8000/health
echo.
echo Press Ctrl+C to stop the service
echo.

python app.py

pause
