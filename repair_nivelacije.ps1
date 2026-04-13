param(
    [string]$ApiBaseUrl = "http://localhost:5000",
    [string]$AdminKey = $env:ADMIN_API_KEY,
    [string]$PostgresUrl = $env:TRENDPLUS_PG_URL,
    [string]$SourceFilePath = "",
    [int]$MaxRowsToModify = 10000,
    [string]$OutputDirectory = ".\tmp\nivelacija-repair",
    [switch]$SkipBackup,
    [switch]$DryRunOnly
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($AdminKey)) {
    throw "Admin key is required. Set ADMIN_API_KEY or pass -AdminKey."
}

$normalizedBaseUrl = $ApiBaseUrl.TrimEnd("/")
$preflightUrl = "$normalizedBaseUrl/admin/repair/nivelacije/preflight"
$repairUrl = "$normalizedBaseUrl/admin/repair/nivelacije"
$headers = @{ "X-Admin-Key" = $AdminKey }
$jsonHeaders = @{ "X-Admin-Key" = $AdminKey; "Content-Type" = "application/json" }

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

function Write-Step {
    param([string]$Message)
    Write-Host "`n== $Message ==" -ForegroundColor Cyan
}

Write-Step "1. Pre-flight check"
$resolvedPreflightUrl = if ([string]::IsNullOrWhiteSpace($SourceFilePath)) {
    $preflightUrl
}
else {
    "$preflightUrl?sourceFilePath=$([uri]::EscapeDataString($SourceFilePath))"
}

$preflightResponse = Invoke-RestMethod -Method Get -Uri $resolvedPreflightUrl -Headers $headers -ErrorAction Stop -Verbose:$false
if (-not $preflightResponse.databaseReachable) {
    throw "Pre-flight failed. Database or required objects are not reachable."
}

Write-Host "Resolved source file: $($preflightResponse.resolvedSourceFilePath)"
Write-Host "Required objects:"
$preflightResponse.requiredObjects.GetEnumerator() | Sort-Object Name | ForEach-Object {
    Write-Host (" - {0}: {1}" -f $_.Key, $_.Value)
}

Write-Step "2. Backup snapshot"
if ($SkipBackup) {
    Write-Host "Backup skipped by request."
}
else {
    if ([string]::IsNullOrWhiteSpace($PostgresUrl)) {
        throw "PostgresUrl is required for backup. Set TRENDPLUS_PG_URL or pass -PostgresUrl, or use -SkipBackup."
    }

    $pgDump = Get-Command pg_dump -ErrorAction SilentlyContinue
    if ($null -eq $pgDump) {
        throw "pg_dump was not found in PATH. Install PostgreSQL client tools or rerun with -SkipBackup."
    }

    $backupFile = Join-Path $OutputDirectory "backup_before_nivelacija_fix_$timestamp.sql"
    & $pgDump.Source --dbname=$PostgresUrl --file=$backupFile
    Write-Host "Backup written to $backupFile"
}

Write-Step "3. Dry run execution"
$resolvedSourceFilePath = if ([string]::IsNullOrWhiteSpace($SourceFilePath)) { $preflightResponse.resolvedSourceFilePath } else { $SourceFilePath }
$dryRunBody = @{
    dryRun = $true
    confirm = $false
    sourceFilePath = $resolvedSourceFilePath
    maxRowsToModify = $MaxRowsToModify
} | ConvertTo-Json

$dryRunResponse = Invoke-RestMethod -Method Post -Uri $repairUrl -Headers $jsonHeaders -Body $dryRunBody
$dryRunFile = Join-Path $OutputDirectory "nivelacija_dry_run_$timestamp.json"
$dryRunResponse | ConvertTo-Json -Depth 12 | Set-Content -Path $dryRunFile -Encoding UTF8

Write-Host "Dry run saved to $dryRunFile"
Write-Host ("Detected issues: {0}" -f $dryRunResponse.estimatedImpact.detectedIssuesCount)
Write-Host ("Proposed fixes: {0}" -f $dryRunResponse.estimatedImpact.proposedFixesCount)
Write-Host ("Threshold: {0}" -f $dryRunResponse.estimatedImpact.maxRowsThreshold)
Write-Host ("Can execute: {0}" -f $dryRunResponse.estimatedImpact.canExecute)

if (-not $dryRunResponse.estimatedImpact.canExecute) {
    throw "Dry run produced a plan that exceeds the safety threshold. Review the JSON output before continuing."
}

if ($DryRunOnly) {
    Write-Host "Dry-run only mode enabled. Live repair skipped."
    exit 0
}

Write-Step "4. Human confirmation"
$confirmation = Read-Host "Type YES to continue with live repair"
if ($confirmation -cne "YES") {
    Write-Host "Repair aborted by operator."
    exit 0
}

Write-Step "5. Live execution"
$liveBody = @{
    dryRun = $false
    confirm = $true
    sourceFilePath = $resolvedSourceFilePath
    maxRowsToModify = $MaxRowsToModify
} | ConvertTo-Json

$liveResponse = Invoke-RestMethod -Method Post -Uri $repairUrl -Headers $jsonHeaders -Body $liveBody
$liveFile = Join-Path $OutputDirectory "nivelacija_live_repair_$timestamp.json"
$liveResponse | ConvertTo-Json -Depth 12 | Set-Content -Path $liveFile -Encoding UTF8

Write-Host "Live repair saved to $liveFile"
Write-Host ("Fixed rows: {0}" -f $liveResponse.fixedRows)
Write-Host ("Skipped rows: {0}" -f $liveResponse.skippedRows)
Write-Host ("Audit id: {0}" -f $liveResponse.auditId)
Write-Host ("Remaining issues after repair: {0}" -f $liveResponse.remainingIssuesAfterRepair)