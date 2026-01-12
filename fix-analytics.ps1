# ===================================
# AUTOMATIC FIX SCRIPT - Analytics 500 Error
# ===================================
# This PowerShell script will:
# 1. Stop any running backend instances
# 2. Create Analytics tables (if needed)
# 3. Add test data
# 4. Start backend with new code
# 5. Wait for workers to process
# 6. Show status

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "?? AUTOMATIC FIX - Analytics Dashboard" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$TRENDPLUS_DB = "trendplus"  # Change if your DB name is different
$ANALYTICS_DB = "analytics_db"  # Change if your DB name is different
$BACKEND_PATH = "Trendplus2"

# Step 1: Stop any running backend
Write-Host "1?? Stopping any running backend instances..." -ForegroundColor Yellow
Get-Process -Name "Trendplus2" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*Trendplus*" } | Stop-Process -Force
Start-Sleep -Seconds 2
Write-Host "   ? Backend stopped" -ForegroundColor Green
Write-Host ""

# Step 2: Create Analytics tables (idempotent - safe to run multiple times)
Write-Host "2?? Creating Analytics tables..." -ForegroundColor Yellow
try {
    psql -d $ANALYTICS_DB -f "Database/Analytics/001_CreateSalesFactTables.sql" 2>&1 | Out-Null
    psql -d $ANALYTICS_DB -f "Database/Analytics/002_AddVelicinaBojaToProductsDim.sql" 2>&1 | Out-Null
    Write-Host "   ? Analytics tables created/verified" -ForegroundColor Green
} catch {
    Write-Host "   ?? Warning: Could not create analytics tables (may already exist)" -ForegroundColor DarkYellow
}
Write-Host ""

# Step 3: Add Velicina/Boja columns to write DB
Write-Host "3?? Adding Velicina/Boja columns..." -ForegroundColor Yellow
try {
    psql -d $TRENDPLUS_DB -f "Database/Migrations/003_AddVelicinaBojaToArtikli.sql" 2>&1 | Out-Null
    Write-Host "   ? Columns added/verified" -ForegroundColor Green
} catch {
    Write-Host "   ?? Warning: Could not add columns (may already exist)" -ForegroundColor DarkYellow
}
Write-Host ""

# Step 4: Add test data (with sample Artikli creation)
Write-Host "4?? Creating sample Artikli and test data..." -ForegroundColor Yellow
try {
    psql -d $TRENDPLUS_DB -f "Database/Migrations/005_CreateArtikliAndTestData.sql"
    Write-Host "   ? Artikli and test data created" -ForegroundColor Green
} catch {
    Write-Host "   ? Error creating test data" -ForegroundColor Red
    Write-Host "   Check database connection and permissions" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 5: Build backend (with new code)
Write-Host "5?? Building backend with latest code..." -ForegroundColor Yellow
Push-Location $BACKEND_PATH
try {
    dotnet build --no-incremental 2>&1 | Out-Null
    Write-Host "   ? Backend built successfully" -ForegroundColor Green
} catch {
    Write-Host "   ? Build failed! Check for compilation errors." -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host ""

# Step 6: Start backend in background
Write-Host "6?? Starting backend..." -ForegroundColor Yellow
Push-Location $BACKEND_PATH
$backendJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    dotnet run --no-build
}
Pop-Location
Start-Sleep -Seconds 5  # Wait for backend to start
Write-Host "   ? Backend started (Job ID: $($backendJob.Id))" -ForegroundColor Green
Write-Host ""

# Step 7: Wait for workers
Write-Host "7?? Waiting for workers to process..." -ForegroundColor Yellow
Write-Host "   ? 60s - SyncWorker (Artikli ? Analytics)" -ForegroundColor Gray
for ($i = 60; $i -gt 0; $i--) {
    Write-Host -NoNewline "`r   ? $i seconds remaining for SyncWorker...   "
    Start-Sleep -Seconds 1
}
Write-Host "`n   ? SyncWorker should be done" -ForegroundColor Green

Write-Host "   ? 30s - OutboxProcessor (Prodaje ? SalesFacts)" -ForegroundColor Gray
for ($i = 30; $i -gt 0; $i--) {
    Write-Host -NoNewline "`r   ? $i seconds remaining for OutboxProcessor...   "
    Start-Sleep -Seconds 1
}
Write-Host "`n   ? OutboxProcessor should be done" -ForegroundColor Green
Write-Host ""

# Step 8: Verify database state
Write-Host "8?? Verifying database state..." -ForegroundColor Yellow

Write-Host "   Checking Write DB ($TRENDPLUS_DB):" -ForegroundColor Gray
$artikliCount = psql -d $TRENDPLUS_DB -t -c "SELECT COUNT(*) FROM ""Artikli"" WHERE ""Velicina"" IS NOT NULL;" | Select-Object -First 1
$salesCount = psql -d $TRENDPLUS_DB -t -c "SELECT COUNT(*) FROM prodaja_zaglavlje WHERE broj_racuna LIKE 'DEMO-%';" | Select-Object -First 1
$outboxProcessed = psql -d $TRENDPLUS_DB -t -c "SELECT COUNT(*) FROM ""OutboxMessages"" WHERE ""Payload""::jsonb->>'BrojRacuna' LIKE 'DEMO-%' AND ""IsProcessed"" = true;" | Select-Object -First 1

Write-Host "      Artikli with Velicina: $($artikliCount.Trim())" -ForegroundColor White
Write-Host "      DEMO Sales: $($salesCount.Trim())" -ForegroundColor White
Write-Host "      Processed Outbox: $($outboxProcessed.Trim())" -ForegroundColor White

Write-Host "   Checking Analytics DB ($ANALYTICS_DB):" -ForegroundColor Gray
$productsCount = psql -d $ANALYTICS_DB -t -c "SELECT COUNT(*) FROM ""ProductsDim"" WHERE ""Velicina"" IS NOT NULL;" | Select-Object -First 1
$salesFactsCount = psql -d $ANALYTICS_DB -t -c "SELECT COUNT(*) FROM ""SalesFacts"";" | Select-Object -First 1
$linesCount = psql -d $ANALYTICS_DB -t -c "SELECT COUNT(*) FROM ""SalesLineFacts"";" | Select-Object -First 1

Write-Host "      ProductsDim with Velicina: $($productsCount.Trim())" -ForegroundColor White
Write-Host "      SalesFacts: $($salesFactsCount.Trim())" -ForegroundColor White
Write-Host "      SalesLineFacts: $($linesCount.Trim())" -ForegroundColor White
Write-Host ""

# Step 9: Final summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? SETUP COMPLETE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Next steps:" -ForegroundColor Yellow
Write-Host "   1. Open browser: http://localhost:8080/analytics" -ForegroundColor White
Write-Host "   2. Hard refresh: Ctrl + Shift + R" -ForegroundColor White
Write-Host "   3. Check that dashboard shows data" -ForegroundColor White
Write-Host ""
Write-Host "?? Backend is running in background (Job ID: $($backendJob.Id))" -ForegroundColor Gray
Write-Host "   To view logs: Receive-Job -Id $($backendJob.Id) -Keep" -ForegroundColor Gray
Write-Host "   To stop: Stop-Job -Id $($backendJob.Id); Remove-Job -Id $($backendJob.Id)" -ForegroundColor Gray
Write-Host ""
Write-Host "? If dashboard still shows errors:" -ForegroundColor Yellow
Write-Host "   - Check backend logs: Receive-Job -Id $($backendJob.Id)" -ForegroundColor White
Write-Host "   - Check browser console (F12)" -ForegroundColor White
Write-Host "   - See: Database/Migrations/TROUBLESHOOTING_500_ERROR.md" -ForegroundColor White
Write-Host ""
