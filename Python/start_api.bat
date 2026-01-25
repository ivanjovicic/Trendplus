@echo off
REM Start Python FastAPI server for Global Trends

echo ========================================
echo Starting Global Trends API Server
echo ========================================
echo.

REM Activate virtual environment
call venv\Scripts\activate.bat

REM Start FastAPI server
echo Starting server on http://localhost:8000
echo.
echo Endpoints:
echo   GET  /trends/social?category=Patike
echo   POST /scrapers/run
echo   GET  /cache/stats
echo.
echo Press Ctrl+C to stop
echo.

python api_server.py
