# ===================================
# NEON DATABASE - Run SQL Script
# ===================================
# For Neon serverless PostgreSQL
# https://neon.tech

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "??  NEON DATABASE - SQL Script Runner" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Your Neon connection string
# Get this from: https://console.neon.tech > Your Project > Connection Details
$NEON_CONNECTION_STRING = ""

# Check if connection string is provided
if ([string]::IsNullOrWhiteSpace($NEON_CONNECTION_STRING)) {
    Write-Host "? Neon connection string not set!" -ForegroundColor Red
    Write-Host ""
    Write-Host "?? Steps to get your connection string:" -ForegroundColor Yellow
    Write-Host "   1. Go to: https://console.neon.tech" -ForegroundColor White
    Write-Host "   2. Select your project" -ForegroundColor White
    Write-Host "   3. Click 'Connection Details'" -ForegroundColor White
    Write-Host "   4. Copy the connection string (psql format)" -ForegroundColor White
    Write-Host ""
    Write-Host "   Example format:" -ForegroundColor Gray
    Write-Host '   postgresql://user:password@ep-xxx-xxx.region.aws.neon.tech/dbname?sslmode=require' -ForegroundColor Gray
    Write-Host ""
    Write-Host "?? Then edit this script and paste it into the `$NEON_CONNECTION_STRING variable" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Check if psql is available
Write-Host "1?? Checking for psql..." -ForegroundColor Yellow
try {
    $null = Get-Command psql -ErrorAction Stop
    $psqlPath = "psql"
    Write-Host "   ? psql found in PATH" -ForegroundColor Green
} catch {
    # Try to find psql in common locations
    $commonPaths = @(
        "C:\Program Files\PostgreSQL\16\bin\psql.exe",
        "C:\Program Files\PostgreSQL\15\bin\psql.exe",
        "C:\Program Files\PostgreSQL\14\bin\psql.exe",
        "C:\Program Files (x86)\PostgreSQL\16\bin\psql.exe",
        "C:\Program Files (x86)\PostgreSQL\15\bin\psql.exe"
    )
    
    $psqlPath = $null
    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $psqlPath = $path
            Write-Host "   ? Found psql at: $path" -ForegroundColor Green
            break
        }
    }
    
    if (-not $psqlPath) {
        Write-Host "   ? psql not found!" -ForegroundColor Red
        Write-Host ""
        Write-Host "   ?? Install PostgreSQL client:" -ForegroundColor Yellow
        Write-Host "      https://www.postgresql.org/download/windows/" -ForegroundColor White
        Write-Host ""
        Write-Host "   Or use the Neon SQL Editor in browser:" -ForegroundColor Yellow
        Write-Host "      https://console.neon.tech" -ForegroundColor White
        Write-Host ""
        exit 1
    }
}
Write-Host ""

# Test connection
Write-Host "2?? Testing connection to Neon..." -ForegroundColor Yellow
try {
    $testResult = & $psqlPath $NEON_CONNECTION_STRING -c "SELECT 1;" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ? Connected to Neon successfully!" -ForegroundColor Green
    } else {
        Write-Host "   ? Connection failed!" -ForegroundColor Red
        Write-Host "   Error: $testResult" -ForegroundColor Red
        Write-Host ""
        Write-Host "   ?? Troubleshooting:" -ForegroundColor Yellow
        Write-Host "      - Check connection string is correct" -ForegroundColor White
        Write-Host "      - Ensure database exists in Neon" -ForegroundColor White
        Write-Host "      - Check firewall/network settings" -ForegroundColor White
        Write-Host ""
        exit 1
    }
} catch {
    Write-Host "   ? Error: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Run the SQL script
Write-Host "3?? Running SQL script..." -ForegroundColor Yellow
Write-Host "   Script: Database/Migrations/005_CreateArtikliAndTestData.sql" -ForegroundColor Gray
Write-Host ""

try {
    & $psqlPath $NEON_CONNECTION_STRING -f "Database/Migrations/005_CreateArtikliAndTestData.sql"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "? SQL Script Executed Successfully!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "?? Next steps:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "1. Update your appsettings.json with Neon connection strings:" -ForegroundColor White
        Write-Host '   "ConnectionStrings": {' -ForegroundColor Gray
        Write-Host '     "TrendplusDb": "your-neon-connection-string",' -ForegroundColor Gray
        Write-Host '     "AnalyticsDb": "your-neon-analytics-connection-string"' -ForegroundColor Gray
        Write-Host '   }' -ForegroundColor Gray
        Write-Host ""
        Write-Host "2. Start backend:" -ForegroundColor White
        Write-Host "   cd Trendplus2" -ForegroundColor Gray
        Write-Host "   dotnet run" -ForegroundColor Gray
        Write-Host ""
        Write-Host "3. Wait 90 seconds for workers to process" -ForegroundColor White
        Write-Host ""
        Write-Host "4. Open browser:" -ForegroundColor White
        Write-Host "   http://localhost:8080/analytics" -ForegroundColor Gray
        Write-Host ""
    } else {
        Write-Host ""
        Write-Host "? SQL script failed!" -ForegroundColor Red
        Write-Host ""
        Write-Host "?? Troubleshooting:" -ForegroundColor Yellow
        Write-Host "   - Check if tables already exist" -ForegroundColor White
        Write-Host "   - Verify user has CREATE permissions" -ForegroundColor White
        Write-Host "   - Check script syntax" -ForegroundColor White
        Write-Host ""
        exit 1
    }
} catch {
    Write-Host ""
    Write-Host "? Error running SQL script: $_" -ForegroundColor Red
    exit 1
}

