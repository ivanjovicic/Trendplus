# AUTO-DETECT PostgreSQL and run SQL script
# This script will find psql.exe automatically

$DB = "trendplus"  # Change this if your database has a different name

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "?? Auto-detecting PostgreSQL..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Common PostgreSQL installation paths
$commonPaths = @(
    "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files\PostgreSQL\15\bin\psql.exe",
    "C:\Program Files\PostgreSQL\14\bin\psql.exe",
    "C:\Program Files\PostgreSQL\13\bin\psql.exe",
    "C:\Program Files (x86)\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files (x86)\PostgreSQL\15\bin\psql.exe",
    "C:\Program Files (x86)\PostgreSQL\14\bin\psql.exe",
    "C:\PostgreSQL\16\bin\psql.exe",
    "C:\PostgreSQL\15\bin\psql.exe",
    "C:\PostgreSQL\14\bin\psql.exe"
)

# Try to find psql in PATH first
$psqlPath = $null
try {
    $psqlPath = (Get-Command psql -ErrorAction SilentlyContinue).Source
    if ($psqlPath) {
        Write-Host "? Found psql in PATH: $psqlPath" -ForegroundColor Green
    }
} catch {
    # Not in PATH, will search common locations
}

# If not in PATH, search common locations
if (-not $psqlPath) {
    Write-Host "?? psql not in PATH, searching common locations..." -ForegroundColor Yellow
    
    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $psqlPath = $path
            Write-Host "? Found PostgreSQL at: $path" -ForegroundColor Green
            break
        }
    }
}

# If still not found, prompt user
if (-not $psqlPath) {
    Write-Host ""
    Write-Host "? Could not find PostgreSQL automatically!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please enter the full path to psql.exe:" -ForegroundColor Yellow
    Write-Host "Example: C:\Program Files\PostgreSQL\16\bin\psql.exe" -ForegroundColor Gray
    Write-Host ""
    $psqlPath = Read-Host "Path to psql.exe"
    
    if (-not (Test-Path $psqlPath)) {
        Write-Host ""
        Write-Host "? File not found: $psqlPath" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please install PostgreSQL or check the path." -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "??? Running SQL script..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Database: $DB" -ForegroundColor Gray
Write-Host "psql: $psqlPath" -ForegroundColor Gray
Write-Host ""

# Run SQL script
try {
    & $psqlPath -d $DB -f "Database/Migrations/005_CreateArtikliAndTestData.sql"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "? SQL script executed successfully!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "?? Next steps:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "1. Restart backend:" -ForegroundColor White
        Write-Host "   cd Trendplus2" -ForegroundColor Gray
        Write-Host "   dotnet run" -ForegroundColor Gray
        Write-Host ""
        Write-Host "2. Wait 90 seconds for workers" -ForegroundColor White
        Write-Host ""
        Write-Host "3. Open browser:" -ForegroundColor White
        Write-Host "   http://localhost:8080/analytics" -ForegroundColor Gray
        Write-Host ""
        Write-Host "4. Refresh: Ctrl + Shift + R" -ForegroundColor White
        Write-Host ""
    } else {
        Write-Host ""
        Write-Host "? SQL script failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        Write-Host ""
        Write-Host "Common issues:" -ForegroundColor Yellow
        Write-Host "- Database '$DB' does not exist (run: createdb $DB)" -ForegroundColor White
        Write-Host "- Wrong database name (change `$DB variable at top of script)" -ForegroundColor White
        Write-Host "- No permission (try: psql -U postgres -d $DB -f ...)" -ForegroundColor White
        Write-Host ""
        exit 1
    }
} catch {
    Write-Host ""
    Write-Host "? Error running SQL script: $_" -ForegroundColor Red
    exit 1
}
