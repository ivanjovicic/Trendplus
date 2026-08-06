<#
.SYNOPSIS
  Safe pg_dump wrapper for Trendplus rehearsal backups.

.DESCRIPTION
  Backs up one PostgreSQL database to a custom-format dump.
  Refuses production-like environment labels/targets.
  Never prints connection secrets.

.PARAMETER ConnectionString
  Source Postgres connection string (or set TRENDPLUS_OPS_REHEARSAL_SOURCE_URL /
  TRENDPLUS_ANALYTICS_REHEARSAL_SOURCE_URL).

.PARAMETER EnvironmentLabel
  Must be an allowed rehearsal label (local|rehearsal|disposable|staging-rehearsal|ci-rehearsal).

.PARAMETER RoleName
  Logical role: operational | analytics

.PARAMETER DryRun
  Validate guards and print planned action without executing pg_dump.
#>
[CmdletBinding()]
param(
    [string]$ConnectionString = "",
    [Parameter(Mandatory = $true)][string]$EnvironmentLabel,
    [ValidateSet("operational", "analytics")][string]$RoleName = "operational",
    [string]$OutputDirectory = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "PostgresBackupCommon.ps1")

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $envKey = if ($RoleName -eq "analytics") {
        "TRENDPLUS_ANALYTICS_REHEARSAL_SOURCE_URL"
    } else {
        "TRENDPLUS_OPS_REHEARSAL_SOURCE_URL"
    }
    $ConnectionString = [Environment]::GetEnvironmentVariable($envKey)
    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        if ($DryRun) {
            $ConnectionString = "Host=127.0.0.1;Port=5434;Database=trendplus_${RoleName}_rehearsal;Username=rehearsal;Password=not-used-in-dry-run"
            Write-Host "DryRun: using synthetic local source connection for guard evaluation ($envKey unset)."
        }
        else {
            throw "ConnectionString missing. Pass -ConnectionString or set $envKey."
        }
    }
}

Assert-SafeRehearsalTarget -ConnectionString $ConnectionString -EnvironmentLabel $EnvironmentLabel -Role "source"
Write-SafeEndpointLog -Label "backup-source[$RoleName]" -ConnectionString $ConnectionString

$repoRoot = Get-OpsRepoRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = New-OpsArtifactDirectory -RepoRoot $repoRoot -RoleName $RoleName
}
elseif (-not (Test-Path -LiteralPath $OutputDirectory)) {
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
}

$dumpPath = Join-Path $OutputDirectory "$RoleName.dump"
$metaPath = Join-Path $OutputDirectory "$RoleName.backup.meta.json"

Write-Host "Planned dump path: $dumpPath"
Write-Host "DryRun: $DryRun"

if ($DryRun) {
    Write-Host "DryRun complete: production guards passed; pg_dump not executed."
    exit 0
}

$dockerContainer = Get-PgDockerContainer
if ($dockerContainer) {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw "TRENDPLUS_PG_DOCKER_CONTAINER is set but docker is not on PATH."
    }
    Invoke-PgDumpDocker -Container $dockerContainer -ConnectionString $ConnectionString -HostDumpPath $dumpPath
}
else {
    Ensure-PgTool -Name "pg_dump" | Out-Null
    $env:PGPASSWORD = $null

    # Convert Npgsql Host=/Username= form to libpq URI (pg_dump rejects Npgsql option names).
    $libpqUri = ConvertTo-LibpqConnectionUri -ConnectionString $ConnectionString
    $dumpArgs = @(
        "--format=custom",
        "--no-owner",
        "--no-acl",
        "--file=$dumpPath",
        "--dbname=$libpqUri"
    )

    Write-Host "Running pg_dump (secrets redacted in logs)..."
    & pg_dump @dumpArgs
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed with exit code $LASTEXITCODE"
    }
}

$evidence = Get-ArtifactChecksum -Path $dumpPath
$payload = [ordered]@{
    role = $RoleName
    environmentLabel = $EnvironmentLabel
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    tool = "pg_dump"
    artifact = $evidence
}
$payload | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metaPath -Encoding utf8

Write-Host "Backup complete."
Write-Host ("SHA256={0}; SizeBytes={1}" -f $evidence.Sha256, $evidence.SizeBytes)
Write-Host "Meta: $metaPath"
