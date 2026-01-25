@echo off
REM Diagnose Global Trends issues

echo ========================================
echo Global Trends Diagnostics
echo ========================================
echo.

echo [Check 1] Python environment
if exist "Python\venv" (
    echo ✅ Python venv exists
) else (
    echo ❌ Python venv NOT found
    echo Fix: cd Python ^&^& setup.bat
)

echo.
echo [Check 2] Python dependencies
cd Python
call venv\Scripts\activate.bat 2>nul
python -c "import fastapi; print('✅ FastAPI installed')" 2>nul || echo ❌ FastAPI not installed
python -c "import requests; print('✅ Requests installed')" 2>nul || echo ❌ Requests not installed
python -c "from scraper import aggregator; print('✅ Scraper modules OK')" 2>nul || echo ❌ Scraper modules missing

echo.
echo [Check 3] Test Python API directly
echo Starting temp Python API...
start /B python api_server.py >nul 2>&1
timeout /t 3 /nobreak >nul

curl -s http://localhost:8000/ 2>nul
if %ERRORLEVEL% EQU 0 (
    echo ✅ Python API responds
    curl -s "http://localhost:8000/trends/social?category=Patike" >temp_trends.json 2>nul
    if exist temp_trends.json (
        echo ✅ Trends endpoint works
        type temp_trends.json
        del temp_trends.json
    )
) else (
    echo ❌ Python API not responding
)

taskkill /F /IM python.exe >nul 2>&1

echo.
echo [Check 4] .NET API
curl -s http://localhost:8080/api/global-trends/social?category=Patike >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo ✅ .NET API responds
) else (
    echo ⚠️ .NET API not running
    echo Start with: cd Api ^&^& dotnet run
)

echo.
echo [Check 5] Frontend
curl -s http://localhost:5173/ >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo ✅ Frontend running
) else (
    echo ⚠️ Frontend not running
    echo Start with: cd Klijent\clientapp ^&^& npm run dev
)

echo.
echo ========================================
echo Diagnostic Complete
echo ========================================
echo.

cd ..
pause
