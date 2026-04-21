<#
.SYNOPSIS
    Safely mark an EF Core migration as applied by inserting a row into __EFMigrationsHistory
    only after verifying the database schema matches the migration operations.

.DESCRIPTION
    This script performs lightweight, conservative heuristics against a C# EF migration
    file to extract the most common operations (CreateTable, AddColumn, CreateIndex, AddForeignKey).
    For each detected operation it runs corresponding PostgreSQL checks via `psql` and only
    inserts the migration row into `"__EFMigrationsHistory"` if all checks pass.

    The script aborts if it detects raw SQL in the migration (data backfills), or if any check fails.

.PARAMETER MigrationId
    The migration id (file name prefix) to mark applied. Required.

.PARAMETER MigrationFile
    Optional explicit path to the migration C# file. If omitted the repository is searched.

.PARAMETER Host,Port,Database,User,Password
    PostgreSQL connection parameters used by `psql`.

.PARAMETER PsqlPath
    Path to `psql` executable (default: psql, must be in PATH or fully qualified).

.PARAMETER ProductVersion
    Optional explicit `ProductVersion` to insert. If omitted the script tries to read the
    last ProductVersion from `__EFMigrationsHistory` or detects EF package version from csproj.

.PARAMETER DryRun
    If set (default) the script only prints actions and checks, doesn't perform the INSERT.

.PARAMETER AssumeYes
    Skip interactive confirmation when performing the actual INSERT.

.NOTES
    - This tool is intentionally conservative: if it cannot confidently determine that the
      schema already reflects the migration, it will abort and require manual inspection.
    - It requires `psql` to be available on the machine where the script runs.
#>

param(
    [Parameter(Mandatory=$true)] [string]$MigrationId,
    [string]$MigrationFile,
    [string]$DbHost = 'localhost',
    [int]$Port = 5432,
    [string]$Database = 'postgres',
    [string]$User = 'postgres',
    [string]$Password,
    [string]$PsqlPath = 'psql',
    [string]$ProductVersion,
    [string]$ConnString,
    [switch]$DryRun = $true,
    [switch]$AssumeYes = $false
)

Set-StrictMode -Version Latest

function Write-ErrAndExit($msg, $code=1) {
    Write-Host $msg -ForegroundColor Red
    exit $code
}

function Find-MigrationFile($id) {
    if ($MigrationFile) {
        if (Test-Path $MigrationFile) { return (Get-Item $MigrationFile).FullName }
        else { Write-ErrAndExit "Migration file specified but not found: $MigrationFile" 2 }
    }

    $root = Get-Location
    Write-Host "Searching for migration file matching '$id' under $root..."
    $candidates = Get-ChildItem -Path $root -Recurse -Filter "*$id*.cs" -ErrorAction SilentlyContinue |
                  Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\' -and $_.Name -notmatch '\.Designer\.cs$' } |
                  Select-Object -First 5

    if (-not $candidates -or $candidates.Count -eq 0) {
        return $null
    }
    if ($candidates.Count -gt 1) {
        Write-Host "Multiple candidate files found:" -ForegroundColor Yellow
        $candidates | ForEach-Object { Write-Host " - $($_.FullName)" }
        Write-Host "Picking the first match: $($candidates[0].FullName)"
    }
    return $candidates[0].FullName
}

function Run-Psql($sql) {
    if ($Password) { $env:PGPASSWORD = $Password }
    try {
        if ($ConnString) {
            $args = @($ConnString, '-t', '-A', '-c', $sql)
            $output = & $PsqlPath @args 2>&1
        }
        else {
            $args = @('-h', $DbHost, '-p', $Port.ToString(), '-U', $User, '-d', $Database, '-t', '-A', '-c', $sql)
            $output = & $PsqlPath @args 2>&1
        }
        $exit = $LASTEXITCODE
        return @{ ExitCode = $exit; Output = ($output -join "`n").Trim() }
    }
    catch {
        return @{ ExitCode = 1; Output = $_.ToString() }
    }
    finally {
        if ($Password) { Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue }
    }
}

function Parse-Migration($path) {
    $text = Get-Content $path -Raw

    if ($text -match 'migrationBuilder\.Sql\(') {
        Write-Host "Migration contains raw SQL (migrationBuilder.Sql). Aborting for manual review." -ForegroundColor Yellow
        return @{ HasSql = $true; Checks = @() }
    }

    $checks = @()

    $callTypes = @('CreateTable','AddColumn','CreateIndex','AddForeignKey')
    foreach ($call in $callTypes) {
        $rx = [regex]::new("$call\((?<inside>.*?)\)\s*;", [System.Text.RegularExpressions.RegexOptions]::Singleline)
        foreach ($m in $rx.Matches($text)) {
            $inside = $m.Groups['inside'].Value
            switch ($call) {
                'CreateTable' {
                    if ($inside -match 'name\s*:\s*"(?<n>[^"]+)"') { $tbl = $Matches['n'] }
                    elseif ($inside -match '"(?<n2>[^"]+)"') { $tbl = $Matches['n2'] }
                    else { $tbl = $null }
                    if ($tbl) { $checks += @{ Type='Table'; Table=$tbl } }
                }
                'AddColumn' {
                    # Try named args first
                    $nameMatch = [regex]::Match($inside, 'name\s*:\s*"(?<col>[^"]+)"')
                    $tableMatch = [regex]::Match($inside, 'table\s*:\s*"(?<tbl>[^"]+)"')
                    if ($nameMatch.Success -and $tableMatch.Success) {
                        $checks += @{ Type='Column'; Table=$tableMatch.Groups['tbl'].Value; Column=$nameMatch.Groups['col'].Value }
                    }
                    else {
                        # fallback to first two string literals: ("Col","Table",...)
                        $strs = ([regex] '"([^"]+)"').Matches($inside) | ForEach-Object { $_.Groups[1].Value }
                        if ($strs.Count -ge 2) { $checks += @{ Type='Column'; Column=$strs[0]; Table=$strs[1] } }
                    }
                }
                'CreateIndex' {
                    $nameMatch = [regex]::Match($inside, 'name\s*:\s*"(?<idx>[^"]+)"')
                    $tableMatch = [regex]::Match($inside, 'table\s*:\s*"(?<tbl>[^"]+)"')
                    if ($nameMatch.Success -and $tableMatch.Success) {
                        $checks += @{ Type='Index'; Table=$tableMatch.Groups['tbl'].Value; Index=$nameMatch.Groups['idx'].Value }
                    }
                    else {
                        $strs = ([regex] '"([^"]+)"').Matches($inside) | ForEach-Object { $_.Groups[1].Value }
                        if ($strs.Count -ge 2) { $checks += @{ Type='Index'; Index=$strs[0]; Table=$strs[1] } }
                    }
                }
                'AddForeignKey' {
                    $fkMatch = [regex]::Match($inside, 'name\s*:\s*"(?<fk>[^"]+)"')
                    $tableMatch = [regex]::Match($inside, 'table\s*:\s*"(?<tbl>[^"]+)"')
                    if ($fkMatch.Success -and $tableMatch.Success) {
                        $fk = $fkMatch.Groups['fk'].Value
                        $tbl = $tableMatch.Groups['tbl'].Value
                        # try column and principalTable
                        $columnMatch = [regex]::Match($inside, 'column\s*:\s*"(?<col>[^"]+)"')
                        $principalMatch = [regex]::Match($inside, 'principalTable\s*:\s*"(?<pt>[^"]+)"')
                        if ($columnMatch.Success -and $principalMatch.Success) {
                            $checks += @{ Type='ForeignKey'; Table=$tbl; Column=$columnMatch.Groups['col'].Value; PrincipalTable=$principalMatch.Groups['pt'].Value; Constraint=$fk }
                        }
                        else { $checks += @{ Type='ForeignKey'; Table=$tbl; Constraint=$fk } }
                    }
                    else {
                        # fallback: string literals
                        $strs = ([regex] '"([^"]+)"').Matches($inside) | ForEach-Object { $_.Groups[1].Value }
                        if ($strs.Count -ge 4) { $checks += @{ Type='ForeignKey'; Constraint=$strs[0]; Table=$strs[1]; Column=$strs[2]; PrincipalTable=$strs[3] } }
                    }
                }
            }
        }
    }

    return @{ HasSql = $false; Checks = $checks }
}

function Build-CheckSql($c) {
    switch ($c.Type) {
        'Table' { return "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '$($c.Table)');" }
        'Column' { return "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '$($c.Table)' AND column_name = '$($c.Column)');" }
        'Index' { return "SELECT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = '$($c.Table)' AND indexname = '$($c.Index)');" }
        'ForeignKey' {
            if ($c.Constraint) { return "SELECT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = '$($c.Constraint)');" }
            elseif ($c.Table -and $c.Column) { return "SELECT EXISTS (SELECT 1 FROM information_schema.table_constraints tc JOIN information_schema.key_column_usage ku ON tc.constraint_name = ku.constraint_name WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_name = '$($c.Table)' AND ku.column_name = '$($c.Column)');" }
            else { return $null }
        }
        default { return $null }
    }
}

function Get-LastProductVersion() {
    $r = Run-Psql('SELECT "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;')
    if ($r.ExitCode -ne 0) { return $null }
    if ([string]::IsNullOrWhiteSpace($r.Output)) { return $null }
    return $r.Output.Trim()
}

function Get-EfVersionFromCsproj() {
    $projects = Get-ChildItem -Path (Get-Location) -Recurse -Filter *.csproj -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\' }
    foreach ($p in $projects) {
        $txt = Get-Content $p -Raw
        if ($txt -match '<PackageReference\s+Include="Microsoft.EntityFrameworkCore"\s+Version="(?<v>[^"]+)"') { return $Matches['v'] }
        if ($txt -match '<PackageReference\s+Include="Microsoft.EntityFrameworkCore"[^>]*>.*<Version>(?<v>[^<]+)</Version>') { return $Matches['v'] }
        if ($txt -match '<PackageReference\s+Include="Microsoft.EntityFrameworkCore.Relational"\s+Version="(?<v>[^"]+)"') { return $Matches['v'] }
    }
    return $null
}

# ---- Main flow ----

$migFile = Find-MigrationFile -id $MigrationId
if (-not $migFile) { Write-ErrAndExit "Migration file for id '$MigrationId' not found. Try passing -MigrationFile path." 3 }

Write-Host "Using migration file: $migFile"

$parse = Parse-Migration -path $migFile
if ($parse.HasSql) { Write-ErrAndExit "Migration contains raw SQL/backfill. Manual process required." 4 }

$checks = $parse.Checks
if (-not $checks -or $checks.Count -eq 0) {
    Write-Host "No deterministic table/column/index/foreign-key operations detected."
    Write-ErrAndExit "This script only handles explicit schema changes. Manual verification required." 5
}

Write-Host "Detected checks to run:" -ForegroundColor Cyan
foreach ($c in $checks) {
    $out = " - $($c.Type)"
    if ($c.ContainsKey('Table')) { $out += " $($c.Table)" }
    if ($c.ContainsKey('Column')) { $out += " / $($c.Column)" }
    if ($c.ContainsKey('Index')) { $out += " / $($c.Index)" }
    Write-Host $out
}

# Verify __EFMigrationsHistory exists
$histCheck = Run-Psql("SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory');")
if ($histCheck.ExitCode -ne 0) { Write-ErrAndExit "Failed to query __EFMigrationsHistory: $($histCheck.Output)" 6 }
if ($histCheck.Output -notmatch 't|1') { Write-ErrAndExit "__EFMigrationsHistory table not found in database '$Database' (schema public)." 7 }

# Run each check
$failed = @()
foreach ($c in $checks) {
    $sql = Build-CheckSql $c
    if (-not $sql) { $failed += @{ Check=$c; Reason='Unsupported/unknown operation' }; continue }
    Write-Host "Running check: $sql"
    $r = Run-Psql($sql)
    if ($r.ExitCode -ne 0) {
        $failed += @{ Check=$c; Reason="psql error: $($r.Output)" }
        continue
    }
    $ok = $false
    try {
        if ($r.Output -match 't' -or $r.Output -match '^1$' -or ($r.Output -as [int] -gt 0)) { $ok = $true }
    } catch { $ok = $false }
    if (-not $ok) { $failed += @{ Check=$c; Reason='check returned false/zero' } }
}

if ($failed.Count -gt 0) {
    Write-Host "One or more schema checks failed. Aborting. Details:" -ForegroundColor Yellow
    foreach ($f in $failed) {
        $c = $f.Check
        $out = " - $($c.Type)"
        if ($c.ContainsKey('Table')) { $out += " $($c.Table)" }
        if ($c.ContainsKey('Column')) { $out += " / $($c.Column)" }
        if ($c.ContainsKey('Index')) { $out += " / $($c.Index)" }
        $out += " : $($f.Reason)"
        Write-Host $out
    }
    Write-ErrAndExit "Schema does not match migration expectations. Do NOT insert history row." 8
}

Write-Host "All checks passed. Preparing to insert migration row into __EFMigrationsHistory." -ForegroundColor Green

if (-not $ProductVersion) { $ProductVersion = Get-LastProductVersion }
if (-not $ProductVersion) { $ProductVersion = Get-EfVersionFromCsproj }
if (-not $ProductVersion) {
    Write-Host "Could not determine ProductVersion automatically. Provide -ProductVersion or inspect the project." -ForegroundColor Yellow
    if (-not $AssumeYes) { $pv = Read-Host 'Enter ProductVersion to insert (e.g. 8.0.4)'; $ProductVersion = $pv }
}

if (-not $ProductVersion) { Write-ErrAndExit "ProductVersion is required to insert history row." 9 }

$insertSql = "BEGIN; INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") VALUES ('$MigrationId', '$ProductVersion'); COMMIT;"

Write-Host "Insert SQL:" -ForegroundColor Cyan
Write-Host $insertSql

if ($DryRun) { Write-Host "DryRun set - not performing INSERT."; exit 0 }

if (-not $AssumeYes) {
    $confirm = Read-Host "Perform the INSERT above? Type 'yes' to proceed"
    if ($confirm -ne 'yes') { Write-ErrAndExit "Aborted by user." 10 }
}

Write-Host "Executing INSERT..."
$ir = Run-Psql($insertSql)
if ($ir.ExitCode -ne 0) { Write-ErrAndExit "INSERT failed: $($ir.Output)" 11 }

Write-Host "Migration '$MigrationId' inserted into __EFMigrationsHistory with ProductVersion '$ProductVersion'." -ForegroundColor Green
exit 0
