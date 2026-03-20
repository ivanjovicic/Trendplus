@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
cd /d "%ROOT%"
chcp 65001 >nul
set "PYTHONUTF8=1"
set "PYTHONIOENCODING=utf-8"
set "TP_ROOT=%ROOT%"

echo ========================================
echo Stopping Global Trends System
echo ========================================
echo.

echo [1/5] Closing service terminal windows...
taskkill /FI "WINDOWTITLE eq Python API*" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq NET API*" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq React Frontend*" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq *React Frontend*" /T /F >nul 2>&1
echo [OK] Terminal windows closed (if they existed).
echo.

echo [2/5] Stopping repo service processes...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$root = [IO.Path]::GetFullPath($env:TP_ROOT).TrimEnd('\'); $targets = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -and ( $_.CommandLine -like ('*' + $root + '*Api.csproj*') -or $_.CommandLine -like ('*' + $root + '*Api.dll*') -or $_.CommandLine -like ('*' + $root + '*Python*api_server.py*') -or $_.CommandLine -like ('*' + $root + '*trend_engine.api*') -or ( $_.CommandLine -like ('*' + $root + '*Klijent\\clientapp*') -and ( $_.CommandLine -like '*vite*' -or $_.CommandLine -like '*npm*run*dev*' ) ) -or $_.CommandLine -like '*cmd* /k npm run dev*' ) }; if (-not $targets) { Write-Host '[INFO] No matching repo processes found.'; exit 0 }; foreach ($p in $targets) { try { Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop; Write-Host ('[OK] Stopped PID ' + $p.ProcessId) } catch { Write-Host ('[WARN] Could not stop PID ' + $p.ProcessId + ': ' + $_.Exception.Message) } }"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$root = [IO.Path]::GetFullPath($env:TP_ROOT).TrimEnd('\\'); $targets = Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -and ( $_.CommandLine -like ('*' + $root + '*Api.csproj*') -or $_.CommandLine -like ('*' + $root + '*Api.dll*') -or $_.CommandLine -like ('*' + $root + '*Python*api_server.py*') -or $_.CommandLine -like ('*' + $root + '*trend_engine.api*') -or ( $_.CommandLine -like ('*' + $root + '*Klijent\\clientapp*') -and ( $_.CommandLine -like '*vite*' -or $_.CommandLine -like '*npm*run*dev*' ) ) -or $_.CommandLine -like '*cmd* /k npm run dev*' ) }; if (-not $targets) { Write-Host '[INFO] No matching repo processes found.'; exit 0 }; foreach ($p in $targets) { try { Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop; Write-Host ('[OK] Stopped PID ' + $p.ProcessId) } catch { Write-Host ('[WARN] Could not stop PID ' + $p.ProcessId + ': ' + $_.Exception.Message) } }"
echo.

echo [3/5] Releasing common dev ports (8000, 8001, 8080, 5173, 5174, 4173)...
for %%P in (8000 8001 8080 5173 5174 4173) do (
    for /f "tokens=5" %%I in ('netstat -ano ^| findstr /R /C:":%%P .*LISTENING"') do (
        taskkill /PID %%I /F >nul 2>&1
    )
)
echo [OK] Port cleanup done.
echo.

echo [4/5] Stopping dotnet build servers...
dotnet build-server shutdown >nul 2>&1
echo [OK] dotnet build-server stopped.
echo.

echo [5/5] Stopping Redis container (if started by docker compose)...
docker context use desktop-linux >nul 2>&1
if not exist "\\.\pipe\dockerDesktopLinuxEngine" (
    echo [INFO] Docker daemon is not running. Skipping Redis stop.
) else (
    docker compose stop redis >nul 2>&1
    if %errorlevel% equ 0 (
        echo [OK] Redis stopped.
    ) else (
        echo [INFO] Redis was not running ^(or docker compose not configured here^).
    )
)
echo.

echo ========================================
echo All local services stop routine finished
echo ========================================
echo.
echo Tip: run start_all_services.bat to start everything again.

endlocal
