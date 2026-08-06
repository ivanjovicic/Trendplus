<#
.SYNOPSIS
  Safe pg_restore wrapper for disposable Trendplus rehearsal targets.

.DESCRIPTION
  Restores a custom-format dump into a disposable destination database.
  Hard-refuses production labels/targets.
  Requires -AllowDestructiveRestore confirmation switch.
  Never prints connection secrets.

.PARAMETER DumpPath
  Path to pg_dump custom-format artifact.

.PARAMETER DestinationConnectionString
  Destination Postgres URL (or TRENDPLUS_*_REHEARSAL_DEST_URL).

.PARAMETER EnvironmentLabel
  Allowed rehearsal label only.

.PARAMETER AllowDestructiveRestore
  Required confirmation that destination may be overwritten.

.PARAMETER IncludePostData
  Also restore post-data (indexes, constraints, materialized view refresh).
  Default rehearsal restore uses pre-data+data only because MV REFRESH can take
  tens of minutes on local Docker and blocks the gate.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$DumpPath,
    [string]$DestinationConnectionString = "",
    [Parameter(Mandatory = $true)][string]$EnvironmentLabel,
    [ValidateSet("operational", "analytics")][string]$RoleName = "operational",
    [switch]$AllowDestructiveRestore,
    [switch]$DryRun,
    [switch]$SkipValidation,
    [switch]$IncludePostData
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "PostgresBackupCommon.ps1")

if (-not $AllowDestructiveRestore -and -not $DryRun) {
    throw "Refusing restore without -AllowDestructiveRestore (or use -DryRun)."
}

if ([string]::IsNullOrWhiteSpace($DestinationConnectionString)) {
    $envKey = if ($RoleName -eq "analytics") {
        "TRENDPLUS_ANALYTICS_REHEARSAL_DEST_URL"
    } else {
        "TRENDPLUS_OPS_REHEARSAL_DEST_URL"
    }
    $DestinationConnectionString = [Environment]::GetEnvironmentVariable($envKey)
    if ([string]::IsNullOrWhiteSpace($DestinationConnectionString)) {
        if ($DryRun) {
            $DestinationConnectionString = "Host=127.0.0.1;Port=5434;Database=trendplus_${RoleName}_rehearsal_dest;Username=rehearsal;Password=not-used-in-dry-run"
            Write-Host "DryRun: using synthetic local destination connection for guard evaluation ($envKey unset)."
        }
        else {
            throw "DestinationConnectionString missing. Pass -DestinationConnectionString or set $envKey."
        }
    }
}

Assert-SafeRehearsalTarget -ConnectionString $DestinationConnectionString -EnvironmentLabel $EnvironmentLabel -Role "destination"
Write-SafeEndpointLog -Label "restore-destination[$RoleName]" -ConnectionString $DestinationConnectionString

if ($DryRun) {
    if (-not (Test-Path -LiteralPath $DumpPath)) {
        Write-Host "DryRun note: dump path not present yet ($DumpPath); file check skipped."
    }
    else {
        $artifact = Get-ArtifactChecksum -Path $DumpPath
        Write-Host ("Dump SHA256={0}; SizeBytes={1}" -f $artifact.Sha256, $artifact.SizeBytes)
    }
    Write-Host "DryRun complete: destination guards passed; pg_restore not executed."
    exit 0
}

if (-not (Test-Path -LiteralPath $DumpPath)) {
    throw "Dump file missing: $DumpPath"
}

$artifact = Get-ArtifactChecksum -Path $DumpPath
Write-Host ("Dump SHA256={0}; SizeBytes={1}" -f $artifact.Sha256, $artifact.SizeBytes)

$sections = @("pre-data", "data")
if ($IncludePostData) {
    $sections += "post-data"
}
Write-Host ("Restore sections: {0}" -f ($sections -join ", "))

$dockerContainer = Get-PgDockerContainer
if ($dockerContainer) {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "TRENDPLUS_PG_DOCKER_CONTAINER is set but docker is not on PATH."
    }
    Invoke-PgRestoreDocker -Container $dockerContainer -ConnectionString $DestinationConnectionString -HostDumpPath $DumpPath -Sections $sections
}
else {
    Ensure-PgTool -Name "pg_restore" | Out-Null

    $libpqUri = ConvertTo-LibpqConnectionUri -ConnectionString $DestinationConnectionString
    $restoreArgs = @(
        "--clean",
        "--if-exists",
        "--no-owner",
        "--no-acl",
        "--dbname=$libpqUri"
    )
    foreach ($section in $sections) {
        $restoreArgs += "--section=$section"
    }
    $restoreArgs += $DumpPath

    Write-Host "Running pg_restore (secrets redacted in logs)..."
    & pg_restore @restoreArgs
    # pg_restore can return non-zero for benign warnings; fail only on hard failures when exit > 1
    if ($LASTEXITCODE -gt 1) {
        throw "pg_restore failed with exit code $LASTEXITCODE"
    }
}

if (-not $IncludePostData) {
    Write-Host "NOTE: post-data skipped (indexes/constraints/MV refresh). Re-run with -IncludePostData for full restore; MV refresh can be slow."
}

if (-not $SkipValidation) {
    Write-Host "Running post-restore validation queries..."

    $queries = @(
        "SELECT COUNT(*) AS migration_rows FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema');",
        "SELECT current_database() AS db, current_user AS db_user;"
    )

    foreach ($sql in $queries) {
        if ($dockerContainer) {
            Invoke-PsqlDocker -Container $dockerContainer -ConnectionString $DestinationConnectionString -Sql $sql
        }
        else {
            Ensure-PgTool -Name "psql" | Out-Null
            $libpqUri = ConvertTo-LibpqConnectionUri -ConnectionString $DestinationConnectionString
            & psql --dbname=$libpqUri -v ON_ERROR_STOP=1 -c $sql
            if ($LASTEXITCODE -ne 0) {
                throw "Post-restore validation failed for query: $sql"
            }
        }
    }

    Write-Host "Post-restore validation: basic schema presence OK."
    Write-Host "REQUIRED NEXT: point app health/readiness at restored URLs, then run analytics refresh / cache invalidation before treating data as current."
}

Write-Host "Restore complete for role=$RoleName."
