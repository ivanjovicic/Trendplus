@echo off
REM Quick start all services for Global Trends

echo ========================================
echo Starting Global Trends System
echo ========================================
echo.

REM Check if Python venv exists
if not exist "Python\venv" (
    echo [Setup] Python environment not found, setting up...
    cd Python
    call setup.bat
    cd ..
)

echo [1/3] Starting Python API on port 8000...
start "Python API" cmd /k "cd Python && venv\Scripts\activate.bat && python api_server.py"
timeout /t 3 /nobreak >nul

echo [2/3] Starting .NET API on port 8080...
start "NET API" cmd /k "cd Api && dotnet run"
timeout /t 5 /nobreak >nul

echo [3/3] Starting React Frontend on port 5173...
start "React Frontend" cmd /k "cd Klijent\clientapp && npm run dev"

echo.
echo ========================================
echo All services starting...
echo ========================================
echo.
echo Services:
echo   - Python API:  http://localhost:8000
echo   - .NET API:    http://localhost:8080
echo   - Frontend:    http://localhost:5173
echo.
echo Open: http://localhost:5173/global-trends
echo.
echo Press any key to stop all services...
pause >nul

REM Kill all services
taskkill /FI "WINDOWTITLE eq Python API*" /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq NET API*" /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq React Frontend*" /F >nul 2>&1

echo.
echo All services stopped.
