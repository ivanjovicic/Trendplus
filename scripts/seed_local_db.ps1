<#
PowerShell helper to seed local Docker Postgres with test data from repository SQL files.
Usage (PowerShell):
  .\scripts\seed_local_db.ps1

What it does:
- Verifies Docker is available and the `trendplus-postgres` container is running
- Pipes the SQL file(s) into the Postgres server inside the container using `psql`.

Files executed (in this order):
- Database/Migrations/005_CreateArtikliAndTestData.sql
- Database/Migrations/004_SimpleTestData.sql (if present)

If your Postgres runs under a different container name or non-default credentials, edit the variables below.
#>

$containerName = "trendplus-postgres"
$database = "trendplus"
$user = "postgres"

# Locate files
$repoRoot = Resolve-Path "./"
$migrations = @(
    "Database/Migrations/005_CreateArtikliAndTestData.sql",
    "Database/Migrations/004_SimpleTestData.sql"
) | Where-Object { Test-Path $_ }

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Error "Docker CLI not found. Install Docker Desktop and ensure 'docker' is in PATH."
    exit 1
}

# Check container
$container = docker ps --filter "name=$containerName" --format "{{.Names}}"
if (-not $container) {
    Write-Error "Container '$containerName' is not running. Start it with 'docker compose up -d'."
    exit 1
}

if ($migrations.Count -eq 0) {
    Write-Warning "No migration files found to seed. Check paths or add SQL files to Database/Migrations/."
    exit 0
}

foreach ($file in $migrations) {
    Write-Host "Seeding file: $file"
    # Use input redirection to feed file into psql inside container
    # The following works on PowerShell: type file | docker exec -i container psql -U user -d db -q -f -
    Get-Content $file -Raw | docker exec -i $containerName psql -U $user -d $database -q -f -
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Seeding failed for $file (exit $LASTEXITCODE). Check docker logs and SQL for errors."
        exit $LASTEXITCODE
    }
    Write-Host "Done: $file`n"
}

Write-Host "Seeding finished successfully."
Write-Host "Verify data: docker exec -it $containerName psql -U $user -d $database -c '\dt'"
