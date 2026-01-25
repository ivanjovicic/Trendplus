@echo off
REM Quick test TikTok API with real key

echo ========================================
echo Testing TikTok API
echo ========================================
echo.

REM Activate venv
call venv\Scripts\activate.bat

REM Install python-dotenv if not installed
pip install python-dotenv --quiet

REM Run test
python test_tiktok_api.py

echo.
pause
