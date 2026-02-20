@echo off
REM ============================================
REM Start Python FastAPI Server for Global Trends
REM ============================================

echo ========================================
echo Starting Python API Server
echo ========================================
echo.

REM Detect Python executable
set "PYTHON_CMD="

REM --- 1) Use venv if available ---
if exist "venv\Scripts\python.exe" (
    echo [Using venv Python]
    set "PYTHON_CMD=venv\Scripts\python.exe"
) else (
    REM --- 2) Try system python ---
    python --version >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        set "PYTHON_CMD=python"
    ) else (
        REM --- 3) Try py launcher ---
        py --version >nul 2>&1
        if %ERRORLEVEL% EQU 0 (
            set "PYTHON_CMD=py"
        )
    )
)

REM --- If still empty → python not found ---
if "%PYTHON_CMD%"=="" (
    echo [ERROR] Python not found!
    echo Make sure Python 3 is installed or venv exists.
    pause
    exit /b 1
)

echo [OK] Using Python: %PYTHON_CMD%
echo.
echo Server will be available at: http://localhost:8000
echo.
echo Press CTRL+C to stop the server
echo.

REM --- Start the FastAPI server ---
"%PYTHON_CMD%" api_server.py

echo.
echo ========================================
echo Python API stopped.
echo ========================================
pause
