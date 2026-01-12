# ===================================
# NEON - COMPLETE SETUP (Both DBs)
# ===================================
# This script copies ALL SQL to clipboard
# For both trendplus and analytics_db

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "??  NEON - Complete Setup (2 databases)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$files = @(
    @{
        Name = "Trendplus DB Migration"
        File = "Database/Migrations/005_CreateArtikliAndTestData.sql"
        Database = "trendplus"
    },
    @{
        Name = "Analytics DB - Create Tables"
        File = "Database/Analytics/001_CreateSalesFactTables.sql"
        Database = "analytics_db"
    },
    @{
        Name = "Analytics DB - Add Velicina/Boja"
        File = "Database/Analytics/002_AddVelicinaBojaToProductsDim.sql"
        Database = "analytics_db"
    }
)

# Check if all files exist
$allExist = $true
foreach ($item in $files) {
    if (-not (Test-Path $item.File)) {
        Write-Host "? SQL file not found: $($item.File)" -ForegroundColor Red
        $allExist = $false
    }
}

if (-not $allExist) {
    Write-Host ""
    Write-Host "Some SQL files are missing!" -ForegroundColor Red
    exit 1
}

# Build combined SQL output with instructions
$output = @"
-- ============================================================
-- NEON COMPLETE SETUP - COPY/PASTE INSTRUCTIONS
-- ============================================================
-- 
-- You need to run these scripts in 2 different databases
-- 
-- DATABASE 1 trendplus
--   Section A Create Artikli and Test Data
-- 
-- DATABASE 2 analytics_db  
--   Section B Create SalesFacts tables
--   Section C Add Velicina/Boja to ProductsDim
--
-- ============================================================

"@

# Add each SQL file with clear separators
$sectionLabel = 'A'
foreach ($item in $files) {
    $content = Get-Content $item.File -Raw
    $itemName = $item.Name
    $itemDb = $item.Database
    $itemFile = $item.File
    
    $output += @"

-- ============================================================
-- SECTION $sectionLabel $itemName
-- DATABASE $itemDb
-- FILE $itemFile
-- ============================================================
-- 
-- IMPORTANT Run this section in database $itemDb
-- 
-- Steps
-- 1. Go to Neon Console https://console.neon.tech
-- 2. Select project Database $itemDb
-- 3. Open SQL Editor
-- 4. Copy section below (from BEGIN to END)
-- 5. Paste and click Run
-- 

-- BEGIN SECTION $sectionLabel --

$content

-- END SECTION $sectionLabel --


"@
    $sectionLabel = [char]([int]$sectionLabel[0] + 1)
}

# Add final instructions
$output += @"

-- ============================================================
-- SETUP COMPLETE - Next Steps
-- ============================================================
-- 
-- After running all sections above
--
-- 1. Update appsettings.json with Neon connection strings
--    "ConnectionStrings" {
--      "TrendplusDb" "postgresql://user:pass@ep-xxx.neon.tech/trendplus?sslmode=require",
--      "AnalyticsDb" "postgresql://user:pass@ep-xxx.neon.tech/analytics_db?sslmode=require"
--    }
--
-- 2. Start backend
--    cd Trendplus2
--    dotnet run
--
-- 3. Wait 90 seconds for workers to process
--
-- 4. Open browser http://localhost:8080/analytics
--
-- 5. Refresh Ctrl + Shift + R
--
-- ============================================================

"@

# Copy to clipboard
Write-Host "?? Copying complete setup to clipboard..." -ForegroundColor Yellow
try {
    Set-Clipboard -Value $output
    Write-Host "   ? Complete SQL setup copied to clipboard!" -ForegroundColor Green
} catch {
    Write-Host "   ? Failed to copy to clipboard: $_" -ForegroundColor Red
    
    # Save to file as fallback
    $outputFile = "neon-complete-setup.sql"
    $output | Out-File -FilePath $outputFile -Encoding UTF8
    Write-Host ""
    Write-Host "   ?? Saved to file instead: $outputFile" -ForegroundColor Yellow
    Write-Host "   Open the file and copy contents manually" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? Ready to Paste!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? What's copied:" -ForegroundColor Yellow
Write-Host ""
foreach ($item in $files) {
    Write-Host "   $($item.Name)" -ForegroundColor White
    Write-Host "   ? Database: $($item.Database)" -ForegroundColor Gray
    Write-Host ""
}
Write-Host "??????????????????????????????????????" -ForegroundColor Gray
Write-Host ""
Write-Host "?? Instructions:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Go to Neon Console: https://console.neon.tech" -ForegroundColor White
Write-Host ""
Write-Host "2. The clipboard contains ALL SQL with instructions" -ForegroundColor White
Write-Host "   telling you which database to use for each section" -ForegroundColor White
Write-Host ""
Write-Host "3. Follow the instructions in the pasted SQL" -ForegroundColor White
Write-Host "   (each section tells you which DB to select)" -ForegroundColor White
Write-Host ""
Write-Host "4. Or run sections one-by-one in correct databases:" -ForegroundColor White
Write-Host "   - Section A ? trendplus database" -ForegroundColor Gray
Write-Host "   - Section B ? analytics_db database" -ForegroundColor Gray
Write-Host "   - Section C ? analytics_db database" -ForegroundColor Gray
Write-Host ""
Write-Host "??????????????????????????????????????" -ForegroundColor Gray
Write-Host ""
Write-Host "?? Alternative: Use individual scripts" -ForegroundColor Yellow
Write-Host "   .\neon-copy-sql.ps1          (just trendplus)" -ForegroundColor Gray
Write-Host "   .\neon-copy-analytics.ps1     (just analytics)" -ForegroundColor Gray
Write-Host ""

