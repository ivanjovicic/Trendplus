# ===================================
# QUICK DIAGNOSTIC - Check Database State
# ===================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "?? DATABASE DIAGNOSTIC CHECK" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Read connection strings from appsettings.json
$appSettings = Get-Content "Trendplus2/appsettings.json" | ConvertFrom-Json
$trendplusConn = $appSettings.ConnectionStrings.DefaultConnection
$analyticsConn = $appSettings.ConnectionStrings.AnalyticsConnection

Write-Host "?? Connection Strings Found:" -ForegroundColor Yellow
Write-Host "   Trendplus: $($trendplusConn.Substring(0, 50))..." -ForegroundColor Gray
Write-Host "   Analytics: $($analyticsConn.Substring(0, 50))..." -ForegroundColor Gray
Write-Host ""

# Parse Neon connection string
function Parse-NeonConnection {
    param([string]$connString)
    
    $host = ""
    $db = ""
    $user = ""
    $pass = ""
    
    if ($connString -match "Host=([^;]+)") { $host = $matches[1] }
    if ($connString -match "Database=([^;]+)") { $db = $matches[1] }
    if ($connString -match "Username=([^;]+)") { $user = $matches[1] }
    if ($connString -match "Password=([^;]+)") { $pass = $matches[1] }
    
    return @{
        Host = $host
        Database = $db
        Username = $user
        Password = $pass
    }
}

$trendplusInfo = Parse-NeonConnection $trendplusConn
$analyticsInfo = Parse-NeonConnection $analyticsConn

Write-Host "? DIAGNOSIS:" -ForegroundColor Green
Write-Host ""
Write-Host "You are using NEON (Cloud PostgreSQL)" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Trendplus DB:" -ForegroundColor Yellow
Write-Host "   Host: $($trendplusInfo.Host)" -ForegroundColor White
Write-Host "   Database: $($trendplusInfo.Database)" -ForegroundColor White
Write-Host ""
Write-Host "?? Analytics DB:" -ForegroundColor Yellow
Write-Host "   Host: $($analyticsInfo.Host)" -ForegroundColor White
Write-Host "   Database: $($analyticsInfo.Database)" -ForegroundColor White
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "?? NEXT STEPS TO FIX 500 ERROR:" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "The automatic initialization may not work if:" -ForegroundColor Yellow
Write-Host "  - EF migrations created empty tables" -ForegroundColor White
Write-Host "  - But no test data was inserted" -ForegroundColor White
Write-Host ""

Write-Host "?? SOLUTION - Run SQL manually in Neon:" -ForegroundColor Green
Write-Host ""

Write-Host "Option 1: Copy SQL to clipboard (RECOMMENDED)" -ForegroundColor Cyan
Write-Host "   .\neon-complete-setup.ps1" -ForegroundColor White
Write-Host "   Then paste in Neon SQL Editor" -ForegroundColor Gray
Write-Host ""

Write-Host "Option 2: Use separate scripts" -ForegroundColor Cyan
Write-Host "   .\neon-copy-sql.ps1          (for trendplus db)" -ForegroundColor White
Write-Host "   .\neon-copy-analytics.ps1     (for analytics db)" -ForegroundColor White
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "?? DETAILED INSTRUCTIONS:" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "1. Run: .\neon-complete-setup.ps1" -ForegroundColor White
Write-Host ""

Write-Host "2. Go to: https://console.neon.tech" -ForegroundColor White
Write-Host ""

Write-Host "3. Select database: $($trendplusInfo.Database)" -ForegroundColor White
Write-Host "   - Paste Section A (Artikli + Test Data)" -ForegroundColor Gray
Write-Host "   - Click Run" -ForegroundColor Gray
Write-Host ""

Write-Host "4. Select database: $($analyticsInfo.Database)" -ForegroundColor White
Write-Host "   - Paste Section B (SalesFacts tables)" -ForegroundColor Gray
Write-Host "   - Click Run" -ForegroundColor Gray
Write-Host "   - Paste Section C (ProductsDim table)" -ForegroundColor Gray
Write-Host "   - Click Run" -ForegroundColor Gray
Write-Host ""

Write-Host "5. Restart backend (or wait ~2 minutes)" -ForegroundColor White
Write-Host ""

Write-Host "6. Open: http://localhost:8080/analytics" -ForegroundColor White
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "?? WHY THIS HAPPENS:" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "EF Core migrations create TABLES but not DATA." -ForegroundColor White
Write-Host "The automatic seeder checks:" -ForegroundColor White
Write-Host '  if (!await context.Artikli.AnyAsync()) { ... }' -ForegroundColor Gray
Write-Host ""
Write-Host "If EF migration already ran, tables exist but are EMPTY." -ForegroundColor White
Write-Host "So we need to manually insert test data." -ForegroundColor White
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host ""

