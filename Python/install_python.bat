@echo off
REM Install Python 3.11.9 automatically

echo ========================================
echo Python 3.11.9 Automatic Installer
echo ========================================
echo.

echo This script will:
echo   1. Download Python 3.11.9 (64-bit)
echo   2. Install it silently with PATH enabled
echo   3. Verify installation
echo.

set "PYTHON_VERSION=3.11.9"
set "PYTHON_URL=https://www.python.org/ftp/python/%PYTHON_VERSION%/python-%PYTHON_VERSION%-amd64.exe"
set "INSTALLER=%TEMP%\python-installer.exe"

echo [Step 1/3] Downloading Python %PYTHON_VERSION%...
echo URL: %PYTHON_URL%
echo.

powershell -Command "& { Invoke-WebRequest -Uri '%PYTHON_URL%' -OutFile '%INSTALLER%' }"

if not exist "%INSTALLER%" (
    echo [ERROR] Download failed!
    pause
    exit /b 1
)

echo [OK] Downloaded to: %INSTALLER%
echo.

echo [Step 2/3] Installing Python %PYTHON_VERSION%...
echo This may take a few minutes...
echo.

REM Silent install with:
REM - Add to PATH
REM - Install for all users
REM - Include pip, tcl/tk, documentation
"%INSTALLER%" /quiet InstallAllUsers=0 PrependPath=1 Include_test=0

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Installation failed!
    pause
    exit /b 1
)

echo [OK] Installation completed
echo.

echo [Step 3/3] Verifying installation...
echo.

REM Wait a bit for PATH to update
timeout /t 3 /nobreak >nul

REM Refresh environment variables
call :RefreshEnv

REM Test Python
python --version 2>nul
if %ERRORLEVEL% EQU 0 (
    echo [OK] Python is ready!
    echo.
    python --version
    echo.
) else (
    echo [WARNING] Python installed but not in PATH yet
    echo Please restart your terminal or computer
    echo.
)

echo.
echo ========================================
echo Installation Complete
echo ========================================
echo.
echo Next steps:
echo   1. CLOSE this terminal
echo   2. Open NEW terminal
echo   3. cd Python
echo   4. setup.bat
echo   5. start_api.bat
echo.

REM Cleanup
del "%INSTALLER%" 2>nul

pause
exit /b 0

:RefreshEnv
REM Refresh PATH from registry
for /f "tokens=2*" %%a in ('reg query "HKCU\Environment" /v PATH 2^>nul') do set "UserPath=%%b"
for /f "tokens=2*" %%a in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v PATH 2^>nul') do set "SystemPath=%%b"
set "PATH=%UserPath%;%SystemPath%"
goto :eof
