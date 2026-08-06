<#
.SYNOPSIS
  Guardrail self-tests for backup/restore rehearsal scripts (no production DB access).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "PostgresBackupCommon.ps1")

$failures = New-Object System.Collections.Generic.List[string]

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { $script:failures.Add($Message) | Out-Null }
}

function Assert-Throws {
    param([scriptblock]$Block, [string]$Message)
    $threw = $false
    try { & $Block } catch { $threw = $true }
    if (-not $threw) { $script:failures.Add($Message) | Out-Null }
}

Write-Host "Running backup/restore guard self-tests..."

Assert-True (Test-ProductionEnvironmentLabel -EnvironmentLabel "production") "production label should be blocked"
Assert-True (Test-ProductionEnvironmentLabel -EnvironmentLabel "PROD") "PROD label should be blocked"
Assert-True (-not (Test-ProductionEnvironmentLabel -EnvironmentLabel "rehearsal")) "rehearsal should not be production label"
Assert-True (Test-AllowedRehearsalEnvironmentLabel -EnvironmentLabel "local") "local should be allowed"
Assert-True (-not (Test-AllowedRehearsalEnvironmentLabel -EnvironmentLabel "production")) "production should not be allowed rehearsal label"

Assert-Throws {
    Assert-SafeRehearsalTarget -ConnectionString "Host=db.example.com;Database=trendplus;Username=u;Password=secret" -EnvironmentLabel "production" -Role "destination"
} "production env should throw"

Assert-Throws {
    Assert-SafeRehearsalTarget -ConnectionString "Host=db.example.com;Database=trendplus;Username=u;Password=secret" -EnvironmentLabel "rehearsal" -Role "destination"
} "remote trendplus db should throw even with rehearsal label"

Assert-True (-not (Test-LooksLikeProductionTarget -ConnectionString "Host=127.0.0.1;Database=trendplus;Username=u;Password=secret" -EnvironmentLabel "local")) `
    "local loopback trendplus should be allowed for local label"

$summary = Get-ConnectionEndpointSummary -ConnectionString "Host=127.0.0.1;Port=5434;Database=trendplus_rehearsal;Username=u;Password=super-secret"
Assert-True ($summary.Host -eq "127.0.0.1") "host parse failed"
Assert-True ($summary.Database -eq "trendplus_rehearsal") "database parse failed"
Assert-True ($summary.HasPassword -eq $true) "password presence detection failed"
Assert-True (([string]$summary.Host + [string]$summary.Database) -notmatch "super-secret") "summary leaked password"

$libpq = ConvertTo-LibpqConnectionUri -ConnectionString "Host=127.0.0.1;Port=5434;Database=trendplus_rehearsal;Username=u;Password=p@ss"
Assert-True ($libpq -match '^postgresql://') "libpq URI should start with postgresql://"
Assert-True ($libpq -match '127\.0\.0\.1:5434/trendplus_rehearsal') "libpq URI host/db mapping failed"
Assert-True ($libpq -notmatch 'Host=') "libpq URI must not keep Npgsql Host= key"
Assert-True ($libpq -match 'p%40ss') "password should be URI-encoded in libpq URI"

# Empty dump path must fail closed on restore script.
Assert-Throws {
    & (Join-Path $PSScriptRoot "Restore-PostgresDatabase.ps1") `
        -DumpPath "C:\does-not-exist\missing.dump" `
        -DestinationConnectionString "Host=127.0.0.1;Database=trendplus_rehearsal;Username=u;Password=x" `
        -EnvironmentLabel "local" `
        -AllowDestructiveRestore `
        -DryRun:$false
} "missing dump should fail closed"

# Production destination refused on dry-run restore.
Assert-Throws {
    & (Join-Path $PSScriptRoot "Restore-PostgresDatabase.ps1") `
        -DumpPath $PSCommandPath `
        -DestinationConnectionString "Host=db.example.com;Database=trendplus;Username=u;Password=x" `
        -EnvironmentLabel "production" `
        -DryRun
} "production destination dry-run should be refused"

# Help/dry-run backup with local loopback should pass guards without pg_dump when DryRun.
& (Join-Path $PSScriptRoot "Backup-PostgresDatabase.ps1") `
    -ConnectionString "Host=127.0.0.1;Database=trendplus;Username=u;Password=x" `
    -EnvironmentLabel "local" `
    -RoleName "operational" `
    -DryRun
Assert-True ($LASTEXITCODE -eq 0) "local backup dry-run should succeed"

if ($failures.Count -gt 0) {
    Write-Host "FAILED:"
    $failures | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host "All backup/restore guard self-tests passed."
exit 0
