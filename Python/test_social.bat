@echo off
REM Test social media trends system

echo ========================================
echo Testing Social Media Trends
echo ========================================
echo.

REM Activate virtual environment
call venv\Scripts\activate.bat

REM Run tests
python test_social_trends.py

echo.
echo ========================================
echo.
echo To view cache:
echo   type social_cache.json
echo.
echo To clear cache:
echo   del social_cache.json
echo.

pause
