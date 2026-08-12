param(
    [string]$PgHost = "127.0.0.1",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [string]$PgPassword = "postgres",
    [string]$SourceDb = "trendplus_test",
    [string]$TargetDb = "trendplus_perf_m",
    [string]$SeedScript = "Database/Perf/M-PERF-01_seed.sql"
)

$ErrorActionPreference = "Stop"
$env:PGPASSWORD = $PgPassword
$repoRoot = Split-Path -Parent $PSScriptRoot

$seedPath = Join-Path $repoRoot $SeedScript
if (-not (Test-Path $seedPath)) {
    throw "Seed script not found: $seedPath"
}

function Invoke-Psql {
    param([string]$Database, [string]$Sql)
    $Sql | & psql -h $PgHost -p $PgPort -U $PgUser -d $Database -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw "psql failed against $Database" }
}

Write-Host "Recreating database $TargetDb from template $SourceDb ..."
$setupSql = @"
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname IN ('$SourceDb', '$TargetDb') AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS $TargetDb;
CREATE DATABASE $TargetDb WITH TEMPLATE $SourceDb;
"@
Invoke-Psql -Database "postgres" -Sql $setupSql

Write-Host "Running M-PERF-01 seed on $TargetDb ..."
& psql -h $PgHost -p $PgPort -U $PgUser -d $TargetDb -v ON_ERROR_STOP=1 -f $seedPath
if ($LASTEXITCODE -ne 0) { throw "Seed script failed" }

Write-Host "Seed verification complete."
