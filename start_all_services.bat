@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
cd /d "%ROOT%"
chcp 65001 >nul
set "PYTHONUTF8=1"
set "PYTHONIOENCODING=utf-8"

echo ========================================
echo Starting Global Trends System
echo ========================================
echo.

if not exist "%ROOT%Python\api_server.py" (
    echo [ERROR] Python API file not found: %ROOT%Python\api_server.py
    goto :end
)
if not exist "%ROOT%Api\Api.csproj" (
    echo [ERROR] .NET API project not found: %ROOT%Api\Api.csproj
    goto :end
)
if not exist "%ROOT%Klijent\clientapp\package.json" (
    echo [ERROR] Frontend project not found: %ROOT%Klijent\clientapp\package.json
    goto :end
)

echo [0/4] Starting Redis on port 6379...
docker context use desktop-linux >nul 2>&1
set "REDIS_STARTED=0"
call :start_redis
if "%REDIS_STARTED%"=="1" (
    echo [OK] Redis running on localhost:6379
) else (
    echo [WARN] Redis was not started automatically.
    echo [WARN] Start Docker Desktop and run: docker compose up -d redis
)
set "POSTGRES_STARTED=0"
call :start_postgres
if "%POSTGRES_STARTED%"=="1" (
    echo [OK] Postgres running on localhost:5434
) else (
    echo [WARN] Postgres was not started automatically.
    echo [WARN] Start Docker Desktop and run: docker compose up -d postgres
)
echo.

if not exist "%ROOT%Python\venv" (
    echo [Setup] Python environment not found, setting up...
    pushd "%ROOT%Python"
    call setup.bat
    popd
)

echo [1/4] Starting Python API on port 8000...
netstat -ano | findstr /R /C:":8000 .*LISTENING" >nul
if %errorlevel% equ 0 (
    echo [OK] Python API already running on localhost:8000
) else (
    set "PYTHON_CMD="
    if exist "%ROOT%Python\venv\Scripts\python.exe" (
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
        start "Python API" /D "%ROOT%Python" cmd /k "%PYTHON_CMD% api_server.py"
    ) else (
        echo [ERROR] Python executable not found. Run Python\setup.bat manually.
    )
    timeout /t 3 /nobreak >nul
)
echo.

echo [2/4] Starting .NET API on port 8080...
netstat -ano | findstr /R /C:":8080 .*LISTENING" >nul
if %errorlevel% equ 0 (
    echo [OK] .NET API already running on localhost:8080
) else (
    start "NET API" /D "%ROOT%Api" cmd /k "dotnet run --urls http://127.0.0.1:8080"
    timeout /t 5 /nobreak >nul
)
echo.

echo [3/4] Hosted .NET workers start together with the API...
echo [OK] Workers are hosted inside the NET API process.
echo.

echo [4/4] Starting React Frontend on port 5174...
netstat -ano | findstr /R /C:":5174 .*LISTENING" >nul
if %errorlevel% equ 0 (
    echo [OK] Frontend already running on localhost:5174
) else (
    start "React Frontend" /D "%ROOT%Klijent\clientapp" cmd /k "npm run dev"
)
echo.

echo ========================================
echo Services are starting in separate windows
echo ========================================
echo.
echo Services:
echo   - Redis:       localhost:6379
echo   - Postgres:    localhost:5434
echo   - Python API:  http://localhost:8000
echo   - .NET API:    http://localhost:8080
echo   - Workers:     hosted inside .NET API
echo   - Frontend:    http://localhost:5174
echo.
echo Open: http://localhost:5174/global-trends
echo.
echo This window can now be closed.
echo To stop services, run: stop_all_services.bat

:end
endlocal
exit /b 0

:start_redis
if not exist "\\.\pipe\dockerDesktopLinuxEngine" (
    set "DOCKER_RUNNING=0"
) else (
    set "DOCKER_RUNNING=1"
)
if "%DOCKER_RUNNING%"=="0" (
    echo [WARN] Docker Linux engine is not available. Skipping host redis fallback.
    echo [INFO] Start Docker Desktop ^(Linux containers^) and run: docker compose up -d redis
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
if not exist "\\.\pipe\dockerDesktopLinuxEngine" (
    set "DOCKER_RUNNING=0"
) else (
    set "DOCKER_RUNNING=1"
)
if "%DOCKER_RUNNING%"=="0" (
    echo [WARN] Docker daemon is not available.
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
