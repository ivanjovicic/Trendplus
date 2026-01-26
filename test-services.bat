@echo off
echo ========================================
echo Testing Trendplus Services
echo ========================================
echo.

echo [1/3] Testing Python API...
curl -s http://localhost:8000/ >nul 2>&1
if %errorlevel% equ 0 (
    echo ✅ Python API is running at http://localhost:8000
    curl http://localhost:8000/
) else (
    echo ❌ Python API is NOT running
    echo    Start with: cd Python ^&^& start_api.bat
)

echo.
echo [2/3] Testing .NET Backend API...
curl -s http://localhost:8080/health >nul 2>&1
if %errorlevel% equ 0 (
    echo ✅ Backend API is running at http://localhost:8080
) else (
    echo ❌ Backend API is NOT running
    echo    Start with: cd Api ^&^& dotnet run
)

echo.
echo [3/3] Testing Frontend...
curl -s http://localhost:5173/ >nul 2>&1
if %errorlevel% equ 0 (
    echo ✅ Frontend is running at http://localhost:5173
) else (
    echo ❌ Frontend is NOT running
    echo    Start with: cd Klijent\clientapp ^&^& npm run dev
)

echo.
echo ========================================
echo Test Python Trends Endpoint:
echo ========================================
curl "http://localhost:8000/trends/social?category=Patike"

echo.
pause
