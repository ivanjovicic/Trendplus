@echo off
echo ========================================
echo Starting Trendplus FULL STACK
echo ========================================
echo.

REM ============================================
REM STEP 0: Start Redis (Docker)
REM ============================================
echo [0/4] Starting Redis...
set "REDIS_STARTED=0"
call :start_redis
if "%REDIS_STARTED%"=="1" (
    echo ✅ Redis running on localhost:6379
) else (
    echo ⚠ Redis was not started automatically.
    echo   Start Docker Desktop and run: docker compose up -d redis
)
set "POSTGRES_STARTED=0"
call :start_postgres
if "%POSTGRES_STARTED%"=="1" (
    echo ✅ Postgres running on localhost:5434
) else (
    echo ⚠ Postgres was not started automatically.
    echo   Start Docker Desktop and run: docker compose up -d postgres
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
set "PYTHON_CMD="
if exist "venv\Scripts\python.exe" (
    set "PYTHON_CMD=venv\Scripts\python.exe"
) else (
    where py >nul 2>&1
    if %errorlevel% equ 0 (
        set "PYTHON_CMD=py"
    ) else (
        where python >nul 2>&1
        if %errorlevel% equ 0 set "PYTHON_CMD=python"
    )
)
if defined PYTHON_CMD (
    start "Trendplus Python API" cmd /k "%PYTHON_CMD% api_server.py"
) else (
    echo [ERROR] Python executable not found. Run Python\setup.bat manually.
)
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
echo Postgres:     localhost:5434
echo Swagger UI:   http://localhost:8080/swagger
echo.
echo To test Python service:
echo   curl http://localhost:8000/trends/social?category=Patike
echo.
echo Press Ctrl+C in each window to stop services
echo.
pause
exit /b 0

:start_redis
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo ⚠ Docker daemon is not available.
    where redis-server >nul 2>&1
    if %errorlevel% equ 0 (
        echo   Starting local redis-server fallback...
        start "Redis" cmd /k "redis-server"
        timeout /t 2 /nobreak >nul
        set "REDIS_STARTED=1"
    )
    exit /b 0
)

docker compose up -d redis >nul 2>&1
if %errorlevel% equ 0 (
    set "REDIS_STARTED=1"
    exit /b 0
)

docker-compose up -d redis >nul 2>&1
if %errorlevel% equ 0 (
    set "REDIS_STARTED=1"
)
exit /b 0

:start_postgres
docker info >nul 2>&1
if %errorlevel% neq 0 (
    echo ⚠ Docker daemon is not available.
    exit /b 0
)

docker compose up -d postgres >nul 2>&1
if %errorlevel% equ 0 (
    set "POSTGRES_STARTED=1"
    exit /b 0
)

docker-compose up -d postgres >nul 2>&1
if %errorlevel% equ 0 (
    set "POSTGRES_STARTED=1"
)
exit /b 0
