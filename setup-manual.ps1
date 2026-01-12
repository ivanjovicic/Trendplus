# ===================================
# MANUAL SETUP - Step by Step
# ===================================
# Run these commands ONE BY ONE

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "?? MANUAL SETUP - Analytics Dashboard" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$TRENDPLUS_DB = "trendplus"
$ANALYTICS_DB = "analytics_db"

Write-Host "?? Before we start, let's check your setup..." -ForegroundColor Yellow
Write-Host ""

# Check PostgreSQL
Write-Host "1?? Checking PostgreSQL..." -ForegroundColor Yellow
try {
    $pgVersion = psql --version 2>&1
    Write-Host "   ? PostgreSQL found: $pgVersion" -ForegroundColor Green
} catch {
    Write-Host "   ? PostgreSQL not found in PATH!" -ForegroundColor Red
    Write-Host "   Install PostgreSQL or add it to PATH" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Check database connection
Write-Host "2?? Checking database connection..." -ForegroundColor Yellow
Write-Host "   Testing connection to: $TRENDPLUS_DB" -ForegroundColor Gray
try {
    $result = psql -d $TRENDPLUS_DB -c "SELECT 1;" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ? Connected to $TRENDPLUS_DB" -ForegroundColor Green
    } else {
        Write-Host "   ? Cannot connect to $TRENDPLUS_DB" -ForegroundColor Red
        Write-Host "   Error: $result" -ForegroundColor Red
        Write-Host "" -ForegroundColor Red
        Write-Host "   Possible fixes:" -ForegroundColor Yellow
        Write-Host "   - Create database: createdb $TRENDPLUS_DB" -ForegroundColor White
        Write-Host "   - Or change database name in script" -ForegroundColor White
        exit 1
    }
} catch {
    Write-Host "   ? Error checking database: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# List databases
Write-Host "3?? Available databases:" -ForegroundColor Yellow
psql -l | Select-String -Pattern "^\s+\w" | ForEach-Object {
    Write-Host "   $_" -ForegroundColor Gray
}
Write-Host ""

# Check if tables exist
Write-Host "4?? Checking existing tables..." -ForegroundColor Yellow
$tableCount = psql -d $TRENDPLUS_DB -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('Artikli', 'Dobavljaci', 'Sezone', 'OutboxMessages');"
Write-Host "   Found $($tableCount.Trim()) core tables" -ForegroundColor Gray

$artikliCount = psql -d $TRENDPLUS_DB -t -c "SELECT COUNT(*) FROM ""Artikli"";" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "   Artikli: $($artikliCount.Trim()) rows" -ForegroundColor Gray
} else {
    Write-Host "   Artikli table does not exist!" -ForegroundColor Red
    Write-Host "   You need to run database migrations first" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Now run the SQL script
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? Prerequisites OK! Ready to create test data" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press any key to continue or Ctrl+C to cancel..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
Write-Host ""

Write-Host "5?? Running SQL script..." -ForegroundColor Yellow
try {
    psql -d $TRENDPLUS_DB -f "Database/Migrations/005_CreateArtikliAndTestData.sql"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "   ? SQL script executed successfully!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "   ? SQL script failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "   ? Error running SQL script: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Verify results
Write-Host "6?? Verifying results..." -ForegroundColor Yellow
$artikliWithSize = psql -d $TRENDPLUS_DB -t -c "SELECT COUNT(*) FROM ""Artikli"" WHERE ""Velicina"" IS NOT NULL;" 2>&1
$demoSales = psql -d $TRENDPLUS_DB -t -c "SELECT COUNT(*) FROM prodaja_zaglavlje WHERE broj_racuna LIKE 'DEMO-%';" 2>&1
$outboxEvents = psql -d $TRENDPLUS_DB -t -c "SELECT COUNT(*) FROM ""OutboxMessages"" WHERE ""Payload""::jsonb->>'BrojRacuna' LIKE 'DEMO-%';" 2>&1

Write-Host "   Artikli with Velicina: $($artikliWithSize.Trim())" -ForegroundColor White
Write-Host "   DEMO Sales: $($demoSales.Trim())" -ForegroundColor White
Write-Host "   Outbox Events: $($outboxEvents.Trim())" -ForegroundColor White
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? SETUP COMPLETE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Next steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Restart backend:" -ForegroundColor White
Write-Host "   cd Trendplus2" -ForegroundColor Gray
Write-Host "   dotnet run" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Wait 90 seconds for workers to process" -ForegroundColor White
Write-Host ""
Write-Host "3. Open browser:" -ForegroundColor White
Write-Host "   http://localhost:8080/analytics" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Refresh page: Ctrl + Shift + R" -ForegroundColor White
Write-Host ""
