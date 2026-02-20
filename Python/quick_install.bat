@echo off
REM Quick install dependencies without venv

echo ========================================
echo Installing Python Dependencies
echo ========================================
echo.

echo Installing packages...
echo.

REM Try to find Python
set "PYTHON_EXE="

REM Check common locations
if exist "C:\Python311\python.exe" set "PYTHON_EXE=C:\Python311\python.exe"
if exist "C:\Python310\python.exe" set "PYTHON_EXE=C:\Python310\python.exe"
if exist "%LOCALAPPDATA%\Programs\Python\Python311\python.exe" set "PYTHON_EXE=%LOCALAPPDATA%\Programs\Python\Python311\python.exe"
if exist "%LOCALAPPDATA%\Programs\Python\Python310\python.exe" set "PYTHON_EXE=%LOCALAPPDATA%\Programs\Python\Python310\python.exe"

REM If not found, ask user
if "%PYTHON_EXE%"=="" (
    echo [INFO] Python not found in common locations
    echo Please run this in a NEW terminal AFTER installing Python
    echo.
    echo Or restart your computer to refresh PATH
    echo.
    pause
    exit /b 1
)

echo [OK] Found Python at: %PYTHON_EXE%
echo.

echo Installing FastAPI...
"%PYTHON_EXE%" -m pip install fastapi

echo Installing Uvicorn...
"%PYTHON_EXE%" -m pip install uvicorn

echo Installing Requests...
"%PYTHON_EXE%" -m pip install requests

echo Installing BeautifulSoup4...
"%PYTHON_EXE%" -m pip install beautifulsoup4

echo Installing Playwright...
"%PYTHON_EXE%" -m pip install playwright

echo Installing python-dotenv...
"%PYTHON_EXE%" -m pip install python-dotenv

echo Installing lxml...
"%PYTHON_EXE%" -m pip install lxml

echo.
echo ========================================
echo Installation Complete!
echo ========================================
echo.

echo Now you can run:
echo   start_api.bat
echo.

pause
