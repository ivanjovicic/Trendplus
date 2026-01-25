@echo off
REM Apply Global Trends tables to Analytics database

echo ========================================
echo Global Trends Tables Setup
echo ========================================
echo.

REM Configuration
set PGHOST=ep-still-unit-agkg41eh-pooler.c-2.eu-central-1.aws.neon.tech
set PGPORT=5432
set PGDATABASE=analytics
set PGUSER=neondb_owner
set PGPASSWORD=npg_7hUftT3sXHgR

echo [INFO] Connecting to Analytics database...
echo   Host: %PGHOST%
echo   Database: %PGDATABASE%
echo.

REM Execute SQL script
psql "postgresql://%PGUSER%:%PGPASSWORD%@%PGHOST%:%PGPORT%/%PGDATABASE%?sslmode=require" -f Database\Analytics\003_AddGlobalTrendsTables.sql

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo SUCCESS! Global Trends tables created
    echo ========================================
    echo.
    echo Tables created:
    echo   - EuTrends
    echo   - SocialTrends
    echo   - GlobalTrendScores
    echo   - TrendHistory
    echo.
    echo Functions created:
    echo   - calculate_final_trend_score
    echo   - find_similar_eu_trends
    echo   - get_top_trending_categories
    echo   - archive_old_trends
    echo.
) else (
    echo.
    echo ========================================
    echo ERROR! Failed to create tables
    echo ========================================
    echo.
)

pause
