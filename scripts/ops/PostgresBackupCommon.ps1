# Shared helpers for Trendplus backup/restore rehearsal scripts.
# Dot-source only; do not execute directly.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-OpsRepoRoot {
    $here = $PSScriptRoot
    if (-not $here) {
        $here = Split-Path -Parent $MyInvocation.MyCommand.Path
    }
    return (Resolve-Path (Join-Path $here "..\..")).Path
}

function Test-ProductionEnvironmentLabel {
    param([Parameter(Mandatory = $true)][string]$EnvironmentLabel)

    $normalized = $EnvironmentLabel.Trim().ToLowerInvariant()
    $blocked = @(
        "production",
        "prod",
        "live",
        "main-prod",
        "render-prod",
        "neon-prod"
    )
    return $blocked -contains $normalized
}

function Test-AllowedRehearsalEnvironmentLabel {
    param([Parameter(Mandatory = $true)][string]$EnvironmentLabel)

    $normalized = $EnvironmentLabel.Trim().ToLowerInvariant()
    $allowed = @(
        "local",
        "rehearsal",
        "disposable",
        "staging-rehearsal",
        "ci-rehearsal"
    )
    return $allowed -contains $normalized
}

function Get-NpgsqlConnectionParts {
    param([Parameter(Mandatory = $true)][string]$ConnectionString)

    $trimmed = $ConnectionString.Trim()
    $parts = @{
        Host = ""
        Port = ""
        Database = ""
        Username = ""
        Password = ""
        IsUri = $false
        Raw = $trimmed
    }

    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        return $parts
    }

    if ($trimmed -match '(?i)^postgres(ql)?://') {
        $parts.IsUri = $true
        try {
            $uri = [Uri]$trimmed
            $parts.Host = $uri.Host
            $parts.Port = if ($uri.Port -gt 0) { [string]$uri.Port } else { "" }
            $parts.Database = $uri.AbsolutePath.TrimStart("/")
            if ($parts.Database -match "[?]") {
                $parts.Database = $parts.Database.Split("?")[0]
            }
            if (-not [string]::IsNullOrWhiteSpace($uri.UserInfo)) {
                $userInfo = $uri.UserInfo.Split(":", 2)
                $parts.Username = [Uri]::UnescapeDataString($userInfo[0])
                if ($userInfo.Count -eq 2) {
                    $parts.Password = [Uri]::UnescapeDataString($userInfo[1])
                }
            }
        }
        catch {
            # leave empty; callers treat unknown carefully
        }
        return $parts
    }

    foreach ($part in ($trimmed -split ";")) {
        if ([string]::IsNullOrWhiteSpace($part)) { continue }
        $kv = $part.Split("=", 2)
        if ($kv.Count -ne 2) { continue }
        $key = $kv[0].Trim().ToLowerInvariant()
        $value = $kv[1].Trim()
        switch ($key) {
            "host" { $parts.Host = $value }
            "server" { $parts.Host = $value }
            "port" { $parts.Port = $value }
            "database" { $parts.Database = $value }
            "username" { $parts.Username = $value }
            "user id" { $parts.Username = $value }
            "userid" { $parts.Username = $value }
            "user" { $parts.Username = $value }
            "password" { $parts.Password = $value }
            "pwd" { $parts.Password = $value }
        }
    }

    return $parts
}

function ConvertTo-LibpqConnectionUri {
    <#
    .SYNOPSIS
      Convert Npgsql key=value (or existing postgres URI) into a libpq URI for pg_dump/pg_restore/psql.
      Npgsql uses Host=/Username=; libpq rejects those option names when passed as conninfo.
    #>
    param([Parameter(Mandatory = $true)][string]$ConnectionString)

    $trimmed = $ConnectionString.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        throw "ConnectionString is empty."
    }

    if ($trimmed -match '(?i)^postgres(ql)?://') {
        return $trimmed
    }

    $parts = Get-NpgsqlConnectionParts -ConnectionString $trimmed
    if ([string]::IsNullOrWhiteSpace($parts.Host) -or [string]::IsNullOrWhiteSpace($parts.Database)) {
        throw "Cannot convert connection string to libpq URI (need Host/Server and Database)."
    }

    $user = [Uri]::EscapeDataString([string]$parts.Username)
    $pass = [Uri]::EscapeDataString([string]$parts.Password)
    $db = [Uri]::EscapeDataString([string]$parts.Database)
    $hostName = [string]$parts.Host
    $portSegment = if (-not [string]::IsNullOrWhiteSpace($parts.Port)) { ":$($parts.Port)" } else { "" }

    if (-not [string]::IsNullOrWhiteSpace($parts.Username)) {
        if (-not [string]::IsNullOrWhiteSpace($parts.Password)) {
            return "postgresql://${user}:${pass}@${hostName}${portSegment}/${db}"
        }
        return "postgresql://${user}@${hostName}${portSegment}/${db}"
    }

    return "postgresql://${hostName}${portSegment}/${db}"
}

function Get-ConnectionEndpointSummary {
    param([Parameter(Mandatory = $true)][string]$ConnectionString)

    # Never return user/password. Best-effort parse of URI or key=value forms.
    $trimmed = $ConnectionString.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        return @{ Host = "(empty)"; Database = "(empty)"; Port = ""; HasPassword = $false }
    }

    $hasPassword = $trimmed -match '(?i)(password|pwd)=' -or $trimmed -match '://[^:]+:[^@]+@'
    $parts = Get-NpgsqlConnectionParts -ConnectionString $trimmed
    $hostName = if ([string]::IsNullOrWhiteSpace($parts.Host)) { "(unknown)" } else { $parts.Host }
    $database = if ([string]::IsNullOrWhiteSpace($parts.Database)) { "(unknown)" } else { $parts.Database }
    $port = [string]$parts.Port

    return @{
        Host = $hostName
        Database = $database
        Port = $port
        HasPassword = [bool]$hasPassword
    }
}

function Test-LooksLikeProductionTarget {
    param(
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$EnvironmentLabel
    )

    if (Test-ProductionEnvironmentLabel -EnvironmentLabel $EnvironmentLabel) {
        return $true
    }

    $summary = Get-ConnectionEndpointSummary -ConnectionString $ConnectionString
    $hostLower = [string]$summary.Host
    $dbLower = [string]$summary.Database
    $hostLower = $hostLower.ToLowerInvariant()
    $dbLower = $dbLower.ToLowerInvariant()

    $hostMarkers = @(
        "prod.",
        ".prod.",
        "production",
        "neon.tech"   # Neon host alone is not enough; combined with db name below
    )
    foreach ($marker in $hostMarkers) {
        if ($hostLower.Contains($marker) -and ($dbLower -eq "trendplus" -or $dbLower -eq "analytics" -or $dbLower -notmatch "rehearsal|disposable|tmp|test|local")) {
            # Soft signal only when DB name also looks production-like.
            if ($dbLower -eq "trendplus" -or $dbLower -eq "analytics") {
                return $true
            }
        }
    }

    # Explicit production-like database names without rehearsal suffix.
    if ($dbLower -in @("trendplus", "analytics", "trendplus_prod", "analytics_prod", "production")) {
        # Local docker defaults are allowed only when EnvironmentLabel is local/rehearsal and host is loopback.
        $isLoopback = $hostLower -in @("localhost", "127.0.0.1", "::1", "(unknown)", "(empty)")
        if (-not $isLoopback) {
            return $true
        }
    }

    return $false
}

function Assert-SafeRehearsalTarget {
    param(
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$EnvironmentLabel,
        [Parameter(Mandatory = $true)][ValidateSet("source", "destination")][string]$Role
    )

    if (-not (Test-AllowedRehearsalEnvironmentLabel -EnvironmentLabel $EnvironmentLabel)) {
        throw "Refusing ${Role}: EnvironmentLabel '$EnvironmentLabel' is not an allowed rehearsal label. Use local|rehearsal|disposable|staging-rehearsal|ci-rehearsal."
    }

    if (Test-LooksLikeProductionTarget -ConnectionString $ConnectionString -EnvironmentLabel $EnvironmentLabel) {
        $summary = Get-ConnectionEndpointSummary -ConnectionString $ConnectionString
        throw ("Refusing {0}: target looks production-like (host={1}; database={2}; env={3}). Never restore/backup rehearsal against production." -f $Role, $summary.Host, $summary.Database, $EnvironmentLabel)
    }
}

function Write-SafeEndpointLog {
    param(
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][string]$ConnectionString
    )

    $summary = Get-ConnectionEndpointSummary -ConnectionString $ConnectionString
    Write-Host ("{0}: host={1}; database={2}; port={3}; passwordPresent={4}" -f `
        $Label, $summary.Host, $summary.Database, $summary.Port, $summary.HasPassword)
}

function Get-ArtifactChecksum {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Artifact not found: $Path"
    }
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $Path
    $item = Get-Item -LiteralPath $Path
    return [pscustomobject]@{
        Path = $Path
        SizeBytes = $item.Length
        LastWriteTimeUtc = $item.LastWriteTimeUtc.ToString("o")
        Sha256 = $hash.Hash
    }
}

function Ensure-PgTool {
    param([Parameter(Mandatory = $true)][ValidateSet("pg_dump", "pg_restore", "psql")][string]$Name)

    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "$Name not found in PATH. Install PostgreSQL client tools or skip with -DryRun."
    }
    return $cmd.Source
}

function Get-PgDockerContainer {
    $name = [Environment]::GetEnvironmentVariable("TRENDPLUS_PG_DOCKER_CONTAINER")
    if ([string]::IsNullOrWhiteSpace($name)) {
        return $null
    }
    return $name.Trim()
}

function Get-PgClientTargetHost {
    param([Parameter(Mandatory = $true)]$Parts)

    $hostName = [string]$parts.Host
    if ([string]::IsNullOrWhiteSpace($hostName)) {
        return "localhost"
    }
    $lower = $hostName.ToLowerInvariant()
    # From inside the Postgres container, loopback host means local socket/server.
    if ($lower -in @("127.0.0.1", "localhost", "::1")) {
        return "localhost"
    }
    return $hostName
}

function Invoke-PgDumpDocker {
    param(
        [Parameter(Mandatory = $true)][string]$Container,
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$HostDumpPath
    )

    $parts = Get-NpgsqlConnectionParts -ConnectionString $ConnectionString
    if ([string]::IsNullOrWhiteSpace($parts.Database) -or [string]::IsNullOrWhiteSpace($parts.Username)) {
        throw "Docker pg_dump requires Database and Username in the connection string."
    }

    $remoteDump = "/tmp/trendplus-rehearsal-dump-$([guid]::NewGuid().ToString('N')).dump"
    $targetHost = Get-PgClientTargetHost -Parts $parts
    $dumpArgs = @(
        "exec",
        "-e", "PGPASSWORD=$($parts.Password)",
        $Container,
        "pg_dump",
        "-h", $targetHost,
        "-U", $parts.Username,
        "-d", $parts.Database,
        "--format=custom",
        "--no-owner",
        "--no-acl",
        "-f", $remoteDump
    )
    if (-not [string]::IsNullOrWhiteSpace($parts.Port) -and $targetHost -ne "localhost") {
        $dumpArgs = @(
            "exec",
            "-e", "PGPASSWORD=$($parts.Password)",
            $Container,
            "pg_dump",
            "-h", $targetHost,
            "-p", $parts.Port,
            "-U", $parts.Username,
            "-d", $parts.Database,
            "--format=custom",
            "--no-owner",
            "--no-acl",
            "-f", $remoteDump
        )
    }

    Write-Host "Running pg_dump via docker container '$Container' (server-matched client)..."
    & docker @dumpArgs
    if ($LASTEXITCODE -ne 0) {
        throw "docker pg_dump failed with exit code $LASTEXITCODE"
    }

    & docker cp "${Container}:${remoteDump}" $HostDumpPath
    if ($LASTEXITCODE -ne 0) {
        throw "docker cp (dump out) failed with exit code $LASTEXITCODE"
    }

    & docker exec $Container rm -f $remoteDump | Out-Null
}

function Invoke-PgRestoreDocker {
    param(
        [Parameter(Mandatory = $true)][string]$Container,
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$HostDumpPath,
        [string[]]$Sections = @("pre-data", "data")
    )

    $parts = Get-NpgsqlConnectionParts -ConnectionString $ConnectionString
    if ([string]::IsNullOrWhiteSpace($parts.Database) -or [string]::IsNullOrWhiteSpace($parts.Username)) {
        throw "Docker pg_restore requires Database and Username in the connection string."
    }

    $remoteDump = "/tmp/trendplus-rehearsal-restore-$([guid]::NewGuid().ToString('N')).dump"
    $targetHost = Get-PgClientTargetHost -Parts $parts

    & docker cp $HostDumpPath "${Container}:${remoteDump}"
    if ($LASTEXITCODE -ne 0) {
        throw "docker cp (dump in) failed with exit code $LASTEXITCODE"
    }

    $restoreArgs = @(
        "exec",
        "-e", "PGPASSWORD=$($parts.Password)",
        $Container,
        "pg_restore",
        "-h", $targetHost,
        "-U", $parts.Username,
        "-d", $parts.Database,
        "--clean",
        "--if-exists",
        "--no-owner",
        "--no-acl"
    )
    foreach ($section in $Sections) {
        if (-not [string]::IsNullOrWhiteSpace($section)) {
            $restoreArgs += "--section=$section"
        }
    }
    $restoreArgs += $remoteDump

    Write-Host ("Running pg_restore via docker container '{0}' sections=[{1}]..." -f $Container, ($Sections -join ","))
    & docker @restoreArgs
    $restoreExit = $LASTEXITCODE
    & docker exec $Container rm -f $remoteDump | Out-Null
    if ($restoreExit -gt 1) {
        throw "docker pg_restore failed with exit code $restoreExit"
    }
}

function Invoke-PsqlDocker {
    param(
        [Parameter(Mandatory = $true)][string]$Container,
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$Sql
    )

    $parts = Get-NpgsqlConnectionParts -ConnectionString $ConnectionString
    $targetHost = Get-PgClientTargetHost -Parts $parts
    $psqlArgs = @(
        "exec",
        "-e", "PGPASSWORD=$($parts.Password)",
        $Container,
        "psql",
        "-h", $targetHost,
        "-U", $parts.Username,
        "-d", $parts.Database,
        "-v", "ON_ERROR_STOP=1",
        "-c", $Sql
    )
    & docker @psqlArgs
    if ($LASTEXITCODE -ne 0) {
        throw "docker psql validation failed with exit code $LASTEXITCODE"
    }
}

function New-OpsArtifactDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$RoleName
    )

    $stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
    $dir = Join-Path $RepoRoot "tmp/ops-rehearsal/$RoleName-$stamp"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    return $dir
}
