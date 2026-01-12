# SIMPLE - Just run the SQL script
# No fancy checks, just execute

$DB = "trendplus"  # Change this if your database has a different name

Write-Host "Running SQL script on database: $DB" -ForegroundColor Cyan
Write-Host ""

# Quick check if psql exists
try {
    $null = Get-Command psql -ErrorAction Stop
} catch {
    Write-Host "? PostgreSQL not found in PATH!" -ForegroundColor Red
    Write-Host "   Install PostgreSQL or add it to PATH" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   Alternative: Use run-sql-auto.ps1 to auto-detect PostgreSQL" -ForegroundColor Gray
    exit 1
}

# Run the SQL script
psql -d $DB -f "Database/Migrations/005_CreateArtikliAndTestData.sql"

# Check if it worked
if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "? Done! If you saw RAISE NOTICE messages above, it worked!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next: Restart backend (cd Trendplus2 && dotnet run)" -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "? SQL script failed!" -ForegroundColor Red
    Write-Host "   Common issues:" -ForegroundColor Yellow
    Write-Host "   - Database '$DB' does not exist" -ForegroundColor White
    Write-Host "   - Wrong database name (change `$DB at top of script)" -ForegroundColor White
    Write-Host ""
    exit 1
}
