# ===================================
# NEON - QUICK SETUP (No psql needed!)
# ===================================
# This script copies the SQL to clipboard
# You can paste it directly into Neon SQL Editor

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "??  NEON - Copy SQL to Clipboard" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$sqlFile = "Database/Migrations/005_CreateArtikliAndTestData.sql"

# Check if file exists
if (-not (Test-Path $sqlFile)) {
    Write-Host "? SQL file not found: $sqlFile" -ForegroundColor Red
    exit 1
}

# Read SQL file
Write-Host "?? Reading SQL file..." -ForegroundColor Yellow
$sqlContent = Get-Content $sqlFile -Raw

# Copy to clipboard
Write-Host "?? Copying to clipboard..." -ForegroundColor Yellow
try {
    Set-Clipboard -Value $sqlContent
    Write-Host "   ? SQL copied to clipboard!" -ForegroundColor Green
} catch {
    Write-Host "   ? Failed to copy to clipboard: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? SQL Ready to Paste!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Next steps:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Open Neon Console:" -ForegroundColor White
Write-Host "   https://console.neon.tech" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Select your project and database" -ForegroundColor White
Write-Host ""
Write-Host "3. Click 'SQL Editor' tab" -ForegroundColor White
Write-Host ""
Write-Host "4. Paste the SQL (Ctrl+V)" -ForegroundColor White
Write-Host ""
Write-Host "5. Click 'Run' button" -ForegroundColor White
Write-Host ""
Write-Host "6. Verify success messages appear" -ForegroundColor White
Write-Host ""
Write-Host "??????????????????????????????????????" -ForegroundColor Gray
Write-Host ""
Write-Host "?? Alternative: Use psql with connection string" -ForegroundColor Yellow
Write-Host "   .\run-sql-neon.ps1" -ForegroundColor Gray
Write-Host ""

