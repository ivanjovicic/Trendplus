@echo off
echo ========================================
echo Starting Trendplus FULL STACK
echo ========================================
echo.

REM ============================================
REM STEP 0: Start Redis (Docker)
REM ============================================
echo [0/4] Starting Redis...
docker compose up -d redis >nul 2>&1
if %errorlevel% neq 0 (
    echo ⚠ Redis nije pokrenut automatski (proveri Docker Desktop).
) else (
    echo ✅ Redis running on localhost:6379
)
echo.

REM ============================================
REM STEP 1: Start Python Trends Service
REM ============================================
echo [1/4] Starting Python Trends Service...
cd Python

REM Check if venv exists
if not exist "venv\" (
    echo Python venv not found. Running setup...
    call setup.bat
)

REM Start Python API server in new window
start "Trendplus Python API" cmd /k "call venv\Scripts\activate.bat && python api_server.py"
echo ✅ Python service starting at http://localhost:8000
timeout /t 5 /nobreak >nul

cd ..

REM ============================================
REM STEP 2: Start .NET Backend API
REM ============================================
echo.
echo [2/4] Starting .NET Backend API...

REM Check if backend is already running
netstat -ano | findstr :8080 >nul
if %errorlevel% equ 0 (
    echo Backend API is already running on port 8080
) else (
    start "Trendplus Backend API" cmd /k "cd Api && dotnet run --urls http://localhost:8080"
    echo ✅ Backend starting at http://localhost:8080
    timeout /t 8 /nobreak >nul
)

REM ============================================
REM STEP 3: Start React Frontend
REM ============================================
echo.
echo [3/4] Starting React Frontend...

REM Check if frontend is already running
netstat -ano | findstr :5173 >nul
if %errorlevel% equ 0 (
    echo Frontend is already running on port 5173
) else (
    start "Trendplus Frontend" cmd /k "cd Klijent\clientapp && npm run dev"
    echo ✅ Frontend starting at http://localhost:5173
    timeout /t 5 /nobreak >nul
)

echo.
echo ========================================
echo 🚀 ALL SERVICES STARTED!
echo ========================================
echo.
echo Python API:   http://localhost:8000
echo Backend API:  http://localhost:8080
echo Frontend:     http://localhost:5173
echo Swagger UI:   http://localhost:8080/swagger
echo.
echo To test Python service:
echo   curl http://localhost:8000/trends/social?category=Patike
echo.
echo Press Ctrl+C in each window to stop services
echo.
pause
