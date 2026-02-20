@echo off
setlocal EnableExtensions
cd /d "%~dp0"
REM Quick start all services for Global Trends

echo ========================================
echo Starting Global Trends System
echo ========================================
echo.

echo [0/4] Starting Redis on port 6379...
docker compose up -d redis >nul 2>&1
if %errorlevel% neq 0 (
    echo [WARN] Redis nije pokrenut automatski - proveri Docker Desktop.
) else (
    echo [OK] Redis running on localhost:6379
)
echo.

REM Check if Python venv exists
if not exist "Python\venv" (
    echo [Setup] Python environment not found, setting up...
    cd Python
    call setup.bat
    cd ..
)

echo [1/4] Starting Python API on port 8000...
start "Python API" cmd /k "cd Python && venv\Scripts\activate.bat && python api_server.py"
timeout /t 3 /nobreak >nul

echo [2/4] Starting .NET API on port 8080...
netstat -ano | findstr :8080 >nul
if %errorlevel% equ 0 (
    echo [OK] .NET API already running on localhost:8080
) else (
    start "NET API" cmd /k "cd Api && dotnet run --urls http://localhost:8080"
    timeout /t 5 /nobreak >nul
)

echo [3/4] Starting React Frontend on port 5174...
netstat -ano | findstr :5174 >nul
if %errorlevel% equ 0 (
    echo [OK] Frontend already running on localhost:5174
) else (
    start "React Frontend" cmd /k "cd Klijent\clientapp && npm run dev"
)

echo.
echo ========================================
echo All services starting...
echo ========================================
echo.
echo Services:
echo   - Redis:       localhost:6379
echo   - Python API:  http://localhost:8000
echo   - .NET API:    http://localhost:8080
echo   - Frontend:    http://localhost:5174
echo.
echo Open: http://localhost:5174/global-trends
echo.
echo Press any key to stop all services...
pause >nul

REM Kill all services
taskkill /FI "WINDOWTITLE eq Python API*" /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq NET API*" /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq React Frontend*" /F >nul 2>&1

echo.
echo All services stopped.
