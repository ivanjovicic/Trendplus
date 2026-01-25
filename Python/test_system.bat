@echo off
REM Complete test of Global Trends system

echo ========================================
echo Global Trends System Test
echo ========================================
echo.

echo [1/5] Checking Python environment...
cd Python
call venv\Scripts\activate.bat

echo.
echo [2/5] Testing Python imports...
python -c "from scraper.aggregator import get_social_trend; print('✅ Imports OK')" 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ❌ Python imports failed
    echo Run: cd Python ^&^& setup.bat
    pause
    exit /b 1
)

echo.
echo [3/5] Testing social trends (mock)...
python -c "from scraper.aggregator import get_social_trend; result = get_social_trend('#sneakers', 'Patike'); print(f'✅ Score: {result[\"final_trend_score\"]}')"

echo.
echo [4/5] Checking if API server is running...
curl -s http://localhost:8000/ >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo ✅ Python API is running on port 8000
) else (
    echo ⚠️ Python API is NOT running
    echo Start it with: cd Python ^&^& start_api.bat
)

echo.
echo [5/5] Checking .NET API...
curl -s http://localhost:8080/health >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo ✅ .NET API is running on port 8080
) else (
    echo ⚠️ .NET API is NOT running
    echo Start it with: cd Api ^&^& dotnet run
)

echo.
echo ========================================
echo Test Results Summary
echo ========================================
echo.
echo To fix issues:
echo   1. Setup Python: cd Python ^&^& setup.bat
echo   2. Start Python API: cd Python ^&^& start_api.bat
echo   3. Start .NET API: cd Api ^&^& dotnet run
echo   4. Start React: cd Klijent\clientapp ^&^& npm run dev
echo.

pause
