# ===================================
# NEON - ANALYTICS DB ONLY
# ===================================
# Copy Analytics SQL scripts to clipboard

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "??  NEON - Analytics DB Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$sqlFile1 = "Database/Analytics/001_CreateSalesFactTables.sql"
$sqlFile2 = "Database/Analytics/002_AddVelicinaBojaToProductsDim.sql"

# Check if files exist
if (-not (Test-Path $sqlFile1)) {
    Write-Host "? SQL file not found: $sqlFile1" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $sqlFile2)) {
    Write-Host "? SQL file not found: $sqlFile2" -ForegroundColor Red
    exit 1
}

# Read SQL files
Write-Host "?? Reading SQL files..." -ForegroundColor Yellow
$sql1 = Get-Content $sqlFile1 -Raw
$sql2 = Get-Content $sqlFile2 -Raw

# Combine with instructions
$combinedSql = @"
-- ============================================================
-- NEON ANALYTICS DB SETUP
-- ============================================================
-- Run this in database: analytics_db
--
-- This will create:
-- 1. SalesFacts and SalesLineFacts tables
-- 2. ProductsDim table with Velicina and Boja columns
-- ============================================================

-- PART 1: Create SalesFacts tables
$sql1

-- PART 2: Add Velicina/Boja to ProductsDim
$sql2

-- ============================================================
-- ? Analytics DB Setup Complete!
-- ============================================================

"@

# Copy to clipboard
Write-Host "?? Copying to clipboard..." -ForegroundColor Yellow
try {
    Set-Clipboard -Value $combinedSql
    Write-Host "   ? Analytics SQL copied to clipboard!" -ForegroundColor Green
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
Write-Host "2. Select your project and database: analytics_db" -ForegroundColor White
Write-Host ""
Write-Host "3. Click 'SQL Editor' tab" -ForegroundColor White
Write-Host ""
Write-Host "4. Paste the SQL (Ctrl+V)" -ForegroundColor White
Write-Host ""
Write-Host "5. Click 'Run' button" -ForegroundColor White
Write-Host ""
Write-Host "6. Verify success messages appear" -ForegroundColor White
Write-Host ""

