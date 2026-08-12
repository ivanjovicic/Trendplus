param(
    [string]$BaseUrl = "http://127.0.0.1:8080",
    [string]$PgHost = "127.0.0.1",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [string]$PgPassword = "postgres",
    [string]$Database = "trendplus_perf_m",
    [string]$SampleId = "PERF07-01",
    [int]$WarmSamples = 1,
    [switch]$SkipSetup
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "Api\Api.csproj"
$setupScript = Join-Path $PSScriptRoot "perf05_setup_db.ps1"
$outputJson = Join-Path $repoRoot ".ai\runs\2026-08-12-PERF07-raw.json"
$sectionLogPath = Join-Path $env:TEMP "perf07_bootstrap_sections.log"
$apiStdOut = Join-Path $env:TEMP "perf07_api.log"
$apiStdErr = Join-Path $env:TEMP "perf07_api.err.log"
$fromDate = "2026-02-13T06:00:00.0000000Z"
$toDate = "2026-08-12T06:00:00.0000000Z"
$connectionString = "Host=$PgHost;Port=$PgPort;Database=$Database;Username=$PgUser;Password=$PgPassword"
$global:Perf07Process = $null

function Invoke-Psql {
    param([string]$DatabaseName, [string]$Sql)
    $env:PGPASSWORD = $PgPassword
    $Sql | & psql -h $PgHost -p $PgPort -U $PgUser -d $DatabaseName -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) {
        throw "psql failed against $DatabaseName"
    }
}

function Invoke-PsqlScalar {
    param([string]$DatabaseName, [string]$Sql)
    $env:PGPASSWORD = $PgPassword
    $result = $Sql | & psql -h $PgHost -p $PgPort -U $PgUser -d $DatabaseName -t -A -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) {
        throw "psql scalar failed against $DatabaseName"
    }
    return [string]($result | Select-Object -First 1).Trim()
}

function Start-Api {
    Stop-Api
    if (-not $script:Built) {
        dotnet build $apiProject -v q | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed"
        }
        $script:Built = $true
    }

    $env:ASPNETCORE_ENVIRONMENT = "Staging"
    $env:DOTNET_ENVIRONMENT = "Staging"
    $env:ASPNETCORE_URLS = "http://localhost:8080"
    $env:PORT = "8080"
    $env:ConnectionStrings__DefaultConnection = $connectionString
    $env:ConnectionStrings__AnalyticsConnection = $connectionString
    $env:ConnectionStrings__OpenProductTrainingConnection = $connectionString
    $env:Workers__Enabled = "false"
    $env:AnalyticsPrewarm__Enabled = "false"

    $global:Perf07Process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @("run", "--project", $apiProject, "--no-launch-profile", "--no-build", "--urls", "http://localhost:8080") `
        -PassThru `
        -WindowStyle Hidden `
        -RedirectStandardOutput $apiStdOut `
        -RedirectStandardError $apiStdErr `
        -WorkingDirectory $repoRoot

    $deadline = (Get-Date).AddSeconds(240)
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -Uri "$BaseUrl/health" -UseBasicParsing -TimeoutSec 5
            if ($resp.StatusCode -eq 200) {
                return
            }
        } catch {
            Start-Sleep -Seconds 2
        }
    }

    throw "API did not become healthy within timeout. See $apiStdOut"
}

function Stop-Api {
    if ($global:Perf07Process -and -not $global:Perf07Process.HasExited) {
        Stop-Process -Id $global:Perf07Process.Id -Force -ErrorAction SilentlyContinue
    }
    Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 2
}

function Invoke-TimedRequest {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [hashtable]$Headers = $null,
        [object]$Body = $null,
        [int]$TimeoutSec = 600
    )

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $responsePath = [System.IO.Path]::GetTempFileName()
    try {
        $curlArgs = @(
            '--silent',
            '--show-error',
            '--location',
            '--compressed',
            '--noproxy', '*',
            '--max-time', [string]$TimeoutSec,
            '--output', $responsePath,
            '--write-out', '%{http_code} %{time_total}',
            '--request', $Method,
            '--header', 'Accept: application/json'
        )

        if ($Headers) {
            foreach ($key in $Headers.Keys) {
                $curlArgs += @('--header', ($key + ': ' + [string]$Headers[$key]))
            }
        }

        if ($null -ne $Body) {
            $payload = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 8 -Compress }
            $curlArgs += @('--header', 'Content-Type: application/json', '--data-raw', $payload)
        }

        $curlArgs += $Url
        $curlOutput = & curl.exe @curlArgs 2>&1
        $sw.Stop()

        $transport = ([string]$curlOutput).Trim()
        $statusCode = 0
        $timeTotalSeconds = 0.0
        if ($transport -match '^(?<status>\d{3})\s+(?<time>[0-9.]+)$') {
            $statusCode = [int]$Matches.status
            $timeTotalSeconds = [double]::Parse($Matches.time, [System.Globalization.CultureInfo]::InvariantCulture)
        }

        $content = if (Test-Path $responsePath) { Get-Content -Path $responsePath -Raw } else { '' }

        $body = $null
        try {
            if (-not [string]::IsNullOrWhiteSpace($content)) {
                $body = $content | ConvertFrom-Json -Depth 20
            }
        } catch {
        }

        return [pscustomobject]@{
            ElapsedMs = [math]::Round([math]::Max($sw.Elapsed.TotalMilliseconds, $timeTotalSeconds * 1000), 2)
            StatusCode = $statusCode
            Body = $body
            Raw = $content
            Transport = $transport
        }
    } catch {
        $sw.Stop()
        return [pscustomobject]@{
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            StatusCode = 0
            Body = $null
            Raw = $null
            Error = $_.Exception.Message
        }
    } finally {
        if (Test-Path $responsePath) {
            Remove-Item -Path $responsePath -Force -ErrorAction SilentlyContinue
        }
    }
}

function Get-JsonProperty {
    param($Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary]) {
        return $Object[$Name]
    }

    $prop = $Object.PSObject.Properties[$Name]
    if ($null -ne $prop) {
        return $prop.Value
    }

    return $null
}

function Extract-SectionTimings {
    param([string]$Path, [string]$Sample)
    if (-not (Test-Path $Path)) {
        return @()
    }

    $pattern = "dashboard\.bootstrap\.section sample=$([regex]::Escape($Sample)) section=(?<section>\S+) priority=(?<priority>\S+) elapsedMs=(?<elapsed>[0-9.]+) success=(?<success>True|False) errors=(?<errors>\d+)"
    $timings = @()
    $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $sr = New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8, $true)
        try {
            while (-not $sr.EndOfStream) {
                $line = $sr.ReadLine()
                if ($line -match $pattern) {
                    $timings += [pscustomobject]@{
                        section = $Matches.section
                        priority = $Matches.priority
                        elapsedMs = [double]$Matches.elapsed
                        success = [bool]::Parse($Matches.success)
                        errors = [int]$Matches.errors
                    }
                }
            }
        } finally {
            $sr.Dispose()
        }
    } finally {
        $fs.Dispose()
    }

    return $timings
}

if (-not $SkipSetup) {
    & $setupScript -TargetDb $Database
}

$script:Built = $false
Start-Api
try {
    $bootstrapUrl = "$BaseUrl/api/analytics/cached/dashboard/bootstrap?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))&dataScope=all&profileSections=true&profileSample=$([uri]::EscapeDataString($SampleId))"
    $cold = Invoke-TimedRequest -Url $bootstrapUrl -TimeoutSec 600

    $warm = $null
    for ($i = 0; $i -lt $WarmSamples; $i++) {
        $warm = Invoke-TimedRequest -Url "$BaseUrl/api/analytics/cached/dashboard/bootstrap?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))&dataScope=all" -TimeoutSec 600
    }

    $meta = Get-JsonProperty $cold.Body 'Meta'
    if ($null -eq $meta) { $meta = Get-JsonProperty $cold.Body 'meta' }
    $summary = Get-JsonProperty $cold.Body 'Summary'
    if ($null -eq $summary) { $summary = Get-JsonProperty $cold.Body 'summary' }
    $inventory = Get-JsonProperty $cold.Body 'Inventory'
    if ($null -eq $inventory) { $inventory = Get-JsonProperty $cold.Body 'inventory' }
    $warmMeta = Get-JsonProperty $warm.Body 'Meta'
    if ($null -eq $warmMeta) { $warmMeta = Get-JsonProperty $warm.Body 'meta' }
    $sectionTimings = Extract-SectionTimings -Path $apiStdOut -Sample $SampleId

    $result = [ordered]@{
    meta = [ordered]@{
        machine = $env:COMPUTERNAME
        os = (Get-CimInstance Win32_OperatingSystem).Caption
        osVersion = (Get-CimInstance Win32_OperatingSystem).Version
        dotnetSdk = (dotnet --version).Trim()
        postgresVersion = ((& psql --version) -join ' ')
        commit = (git -C $repoRoot rev-parse HEAD).Trim()
        datetimeUtc = (Get-Date).ToUniversalTime().ToString("o")
        datasetTier = "M"
        seedRecipeId = "M-PERF-01"
        database = $Database
        port = 8080
        prewarmEnabled = $false
        workersEnabled = $false
        profileSectionsEnabled = $true
        sampleId = $SampleId
        periodFrom = $fromDate
        periodTo = $toDate
    }
    counts = @{
        Artikli = [int64](Invoke-PsqlScalar -DatabaseName $Database -Sql "SELECT COUNT(*) FROM ""Artikli"" WHERE ""Naziv"" LIKE 'M-PERF Product %';")
        ProdajaZaglavlja = [int64](Invoke-PsqlScalar -DatabaseName $Database -Sql "SELECT COUNT(*) FROM prodaja_zaglavlje WHERE broj_racuna LIKE 'MPERF-%';")
        ProdajaStavke = [int64](Invoke-PsqlScalar -DatabaseName $Database -Sql "SELECT COUNT(*) FROM prodaja_stavke ps JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja WHERE pz.broj_racuna LIKE 'MPERF-%';")
        Dobavljaci = [int64](Invoke-PsqlScalar -DatabaseName $Database -Sql "SELECT COUNT(*) FROM ""Dobavljaci"" WHERE ""Naziv"" LIKE 'M-PERF Supplier %';")
    }
    coldBootstrap = [ordered]@{
        elapsedMs = $cold.ElapsedMs
        statusCode = $cold.StatusCode
        success = Get-JsonProperty $meta 'success'
        isPartial = Get-JsonProperty $meta 'isPartial'
        warningCode = Get-JsonProperty $meta 'warningCode'
        dataQualityStatus = Get-JsonProperty $meta 'dataQualityStatus'
        totalRevenue = Get-JsonProperty $summary 'totalRevenue'
        totalSkuCount = Get-JsonProperty $inventory 'totalSkuCount'
        raw = $cold.Raw
    }
    warmBootstrap = [ordered]@{
        elapsedMs = $warm.ElapsedMs
        statusCode = $warm.StatusCode
        success = Get-JsonProperty $warmMeta 'success'
        isPartial = Get-JsonProperty $warmMeta 'isPartial'
        raw = $warm.Raw
    }
    sectionTimings = @($sectionTimings)
    }

    $json = $result | ConvertTo-Json -Depth 12
    Set-Content -Path $outputJson -Value $json -Encoding UTF8
    Write-Host "Wrote $outputJson"
    Write-Host ($json.Substring(0, [Math]::Min(1200, $json.Length)))
}
finally {
    Stop-Api
}
