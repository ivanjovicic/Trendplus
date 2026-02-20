@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
cd /d "%ROOT%"

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
docker compose up -d redis >nul 2>&1
if %errorlevel% neq 0 (
    echo [WARN] Redis nije pokrenut automatski - proveri Docker Desktop.
) else (
    echo [OK] Redis running on localhost:6379
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
    start "Python API" /D "%ROOT%Python" cmd /k "call venv\Scripts\activate.bat && python api_server.py"
    timeout /t 3 /nobreak >nul
)
echo.

echo [2/4] Starting .NET API on port 8080...
netstat -ano | findstr /R /C:":8080 .*LISTENING" >nul
if %errorlevel% equ 0 (
    echo [OK] .NET API already running on localhost:8080
) else (
    start "NET API" /D "%ROOT%Api" cmd /k "dotnet run --urls http://localhost:8080"
    timeout /t 5 /nobreak >nul
)
echo.

echo [3/4] Starting React Frontend on port 5174...
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
echo   - Python API:  http://localhost:8000
echo   - .NET API:    http://localhost:8080
echo   - Frontend:    http://localhost:5174
echo.
echo Open: http://localhost:5174/global-trends
echo.
echo This window can now be closed.
echo To stop services, close their terminal windows (or use Ctrl+C in each).

:end
endlocal
