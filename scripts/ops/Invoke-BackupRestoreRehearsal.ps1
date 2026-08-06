<#
.SYNOPSIS
  Orchestrates operational + analytics backup/restore rehearsal with production guards.

.EXAMPLE
  .\Invoke-BackupRestoreRehearsal.ps1 -EnvironmentLabel local -DryRun

.EXAMPLE
  .\Invoke-BackupRestoreRehearsal.ps1 -EnvironmentLabel disposable -AllowDestructiveRestore
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EnvironmentLabel,
    [switch]$DryRun,
    [switch]$AllowDestructiveRestore,
    [switch]$SkipAnalytics,
    [switch]$SkipOperational,
    [switch]$IncludePostData
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "PostgresBackupCommon.ps1")

$repoRoot = Get-OpsRepoRoot
$started = Get-Date
Write-Host "=== Trendplus backup/restore rehearsal ==="
Write-Host ("StartedUtc={0}" -f $started.ToUniversalTime().ToString("o"))
Write-Host "EnvironmentLabel=$EnvironmentLabel DryRun=$DryRun"

$roles = @()
if (-not $SkipOperational) { $roles += "operational" }
if (-not $SkipAnalytics) { $roles += "analytics" }
if ($roles.Count -eq 0) {
    throw "Nothing to rehearse: both -SkipOperational and -SkipAnalytics were set."
}

$results = @()
foreach ($role in $roles) {
    Write-Host ""
    Write-Host "--- Role: $role ---"
    $outDir = New-OpsArtifactDirectory -RepoRoot $repoRoot -RoleName $role

    $backupArgs = @{
        EnvironmentLabel = $EnvironmentLabel
        RoleName = $role
        OutputDirectory = $outDir
        DryRun = $DryRun
    }
    & (Join-Path $PSScriptRoot "Backup-PostgresDatabase.ps1") @backupArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Backup failed for role=$role"
    }

    $dumpPath = Join-Path $outDir "$role.dump"
    if ($DryRun) {
        $results += [pscustomobject]@{ Role = $role; Step = "backup-dry-run"; Status = "pass"; Path = $outDir }
        $restoreArgs = @{
            DumpPath = $dumpPath
            EnvironmentLabel = $EnvironmentLabel
            RoleName = $role
            DryRun = $true
            AllowDestructiveRestore = $false
        }
        # Dry-run restore still requires a destination URL to evaluate guards.
        & (Join-Path $PSScriptRoot "Restore-PostgresDatabase.ps1") @restoreArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Restore dry-run failed for role=$role"
        }
        $results += [pscustomobject]@{ Role = $role; Step = "restore-dry-run"; Status = "pass"; Path = $outDir }
        continue
    }

    if (-not (Test-Path -LiteralPath $dumpPath)) {
        throw "Expected dump missing after backup: $dumpPath"
    }

    $restoreArgs = @{
        DumpPath = $dumpPath
        EnvironmentLabel = $EnvironmentLabel
        RoleName = $role
        AllowDestructiveRestore = $AllowDestructiveRestore
        DryRun = $false
        IncludePostData = $IncludePostData
    }
    & (Join-Path $PSScriptRoot "Restore-PostgresDatabase.ps1") @restoreArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed for role=$role"
    }

    $results += [pscustomobject]@{
        Role = $role
        Step = "backup-restore"
        Status = "pass"
        Path = $outDir
        Evidence = (Get-ArtifactChecksum -Path $dumpPath)
    }
}

$ended = Get-Date
Write-Host ""
Write-Host "=== Rehearsal summary ==="
$results | Format-Table -AutoSize | Out-String | Write-Host
Write-Host ("DurationSeconds={0}" -f [int]($ended - $started).TotalSeconds)
Write-Host "Post-restore analytics refresh is REQUIRED before treating restored analytics as current (cache is non-durable)."
Write-Host "Cleanup: delete tmp/ops-rehearsal artifacts after evidence is copied to docs/ops (never commit dumps)."
