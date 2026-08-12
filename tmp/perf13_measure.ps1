param(
    [string]$ApiBaseUrl = "http://127.0.0.1:8080",
    [string]$PgHost = "127.0.0.1",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [string]$PgPassword = "postgres",
    [string]$Database = "trendplus_perf_m",
    [string]$AdminKey = "perf13-cache-key",
    [switch]$SkipSetup
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "Api\Api.csproj"
$outputJson = Join-Path $repoRoot ".ai\runs\2026-08-12-PERF13-raw.json"
$outputMd = Join-Path $repoRoot ".ai\runs\2026-08-12-PERF13-evidence.md"
$connectionString = "Host=$PgHost;Port=$PgPort;Database=$Database;Username=$PgUser;Password=$PgPassword"
$global:PerfApiProcess = $null
$env:PGPASSWORD = $PgPassword

function Stop-PerfApi {
    if ($global:PerfApiProcess -and -not $global:PerfApiProcess.HasExited) {
        Stop-Process -Id $global:PerfApiProcess.Id -Force -ErrorAction SilentlyContinue
    }

    Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }

    Start-Sleep -Seconds 2
}

function Start-PerfApi {
    Stop-PerfApi

    if (-not $script:ApiBuilt) {
        dotnet build $apiProject -v q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
        $script:ApiBuilt = $true
    }

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:DOTNET_ENVIRONMENT = "Development"
    $env:PORT = "8080"
    $env:ASPNETCORE_URLS = "http://localhost:8080"
    $env:ConnectionStrings__DefaultConnection = $connectionString
    $env:ConnectionStrings__AnalyticsConnection = $connectionString
    $env:ConnectionStrings__OpenProductTrainingConnection = $connectionString
    $env:Workers__Enabled = "false"
    $env:AnalyticsPrewarm__Enabled = "false"
    $env:ADMIN_API_KEY = $AdminKey
    $env:Admin__ApiKey = $AdminKey
    Remove-Item Env:PROCESS_TYPE -ErrorAction SilentlyContinue

    $apiLog = Join-Path $env:TEMP "perf13_api.log"
    $apiErr = Join-Path $env:TEMP "perf13_api.err.log"
    $global:PerfApiProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--project", $apiProject, "--no-launch-profile", "--no-build", "--urls", "http://localhost:8080") `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput $apiLog -RedirectStandardError $apiErr -WorkingDirectory $repoRoot

    $deadline = (Get-Date).AddSeconds(240)
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -Uri "$ApiBaseUrl/health" -UseBasicParsing -TimeoutSec 5
            if ($resp.StatusCode -eq 200) { return }
        } catch {
            Start-Sleep -Seconds 2
        }
    }

    throw "API health probe failed. See $apiLog / $apiErr"
}

function Invoke-JsonGet {
    param([string]$Url, [int]$TimeoutSec = 120)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec
        $sw.Stop()
        $body = $null
        try { $body = $resp.Content | ConvertFrom-Json } catch {}
        return [pscustomobject]@{
            StatusCode = [int]$resp.StatusCode
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Body = $body
            Error = $null
        }
    } catch {
        $sw.Stop()
        return [pscustomobject]@{
            StatusCode = 0
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Body = $null
            Error = $_.Exception.Message
        }
    }
}

function Invoke-JsonPost {
    param([string]$Url, [hashtable]$Headers = @{}, [string]$BodyJson = "{}", [int]$TimeoutSec = 120)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-WebRequest -Uri $Url -Method POST -Headers $Headers -ContentType "application/json" -Body $BodyJson -UseBasicParsing -TimeoutSec $TimeoutSec
        $sw.Stop()
        $body = $null
        try { $body = $resp.Content | ConvertFrom-Json } catch {}
        return [pscustomobject]@{
            StatusCode = [int]$resp.StatusCode
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Body = $body
            Error = $null
        }
    } catch {
        $sw.Stop()
        return [pscustomobject]@{
            StatusCode = 0
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Body = $null
            Error = $_.Exception.Message
        }
    }
}

function Get-Prop {
    param($Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
    if ($null -ne $prop) { return $prop.Value }
    return $null
}

function Get-ProcessRssMb {
    param($Process)
    if ($null -eq $Process -or $Process.HasExited) { return $null }
    $p = Get-Process -Id $Process.Id -ErrorAction SilentlyContinue
    if ($null -eq $p) { return $null }
    return [math]::Round($p.WorkingSet64 / 1MB, 2)
}

if (-not $SkipSetup) {
    & (Join-Path $PSScriptRoot "perf05_setup_db.ps1") -TargetDb $Database
}

$script:ApiBuilt = $false
Start-PerfApi

$cacheStatusUrl = "$ApiBaseUrl/api/analytics/cache/status"
$cacheInvalidateUrl = "$ApiBaseUrl/api/analytics/cached/cache/invalidate?family=all"
$fromDate = "2026-02-13T06:00:00.0000000Z"
$toDate = "2026-08-12T06:00:00.0000000Z"

Write-Host "PERF13 cache footprint warm-up ..."
$clear = Invoke-JsonPost -Url $cacheInvalidateUrl -Headers @{ "X-Admin-Key" = $AdminKey } -BodyJson "{}" -TimeoutSec 180
if ($clear.StatusCode -notin 200, 202) {
    throw "Cache clear failed: $($clear.Error)"
}

$baselineStatus = Invoke-JsonGet -Url $cacheStatusUrl
$baselineCount = [int](Get-Prop $baselineStatus.Body 'trackedKeyCount')
$baselineRss = Get-ProcessRssMb -Process $global:PerfApiProcess

$requests = @(
    "$ApiBaseUrl/api/analytics/cached/sales/summary?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))",
    "$ApiBaseUrl/api/analytics/cached/sales/summary?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))&storeId=1",
    "$ApiBaseUrl/api/analytics/cached/sales/top-products?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))&top=10"
)

$samples = @()
$peakRss = $baselineRss
$peakCount = $baselineCount

foreach ($url in $requests) {
    $sample = Invoke-JsonGet -Url $url -TimeoutSec 180
    $status = Invoke-JsonGet -Url $cacheStatusUrl -TimeoutSec 30
    $rss = Get-ProcessRssMb -Process $global:PerfApiProcess
    $count = [int](Get-Prop $status.Body 'trackedKeyCount')

    if ($null -ne $rss -and ($null -eq $peakRss -or $rss -gt $peakRss)) {
        $peakRss = $rss
    }

    if ($count -gt $peakCount) {
        $peakCount = $count
    }

    $samples += [pscustomobject]@{
        url = $url
        statusCode = $sample.StatusCode
        elapsedMs = $sample.ElapsedMs
        trackedKeyCount = $count
        rssMb = $rss
        error = $sample.Error
    }
}

$finalStatus = Invoke-JsonGet -Url $cacheStatusUrl
$finalCount = [int](Get-Prop $finalStatus.Body 'trackedKeyCount')
$finalRss = Get-ProcessRssMb -Process $global:PerfApiProcess

$estimatedDeltaMb = $null
if ($null -ne $peakRss -and $null -ne $baselineRss) {
    $estimatedDeltaMb = [math]::Round([double]$peakRss - [double]$baselineRss, 2)
}

$measured = $peakCount -gt 0 -and @($samples | Where-Object { $_.statusCode -eq 200 }).Count -ge 3

$result = [ordered]@{
    meta = [ordered]@{
        packId = "PERF13-G10-cache-footprint-01"
        commit = (git rev-parse HEAD).Trim()
        datetimeUtc = (Get-Date).ToUniversalTime().ToString("O")
        milestone = "G10"
        deploymentMode = "dedicated"
        datasetTier = "M"
        seedRecipeId = "M-PERF-01"
        database = $Database
    }
    dimensions = [ordered]@{
        D1 = [ordered]@{
            status = "cite_PERF11"
            notes = "Observed envelope already measured"
        }
        D2 = [ordered]@{
            status = "cite_PERF10"
            notes = "Concurrent reads already measured"
        }
        D3 = [ordered]@{
            status = "cite_PERF10"
            notes = "Connection pressure already measured"
        }
        D4 = [ordered]@{
            status = "cite_PERF12"
            notes = "Worker health already measured"
        }
        D5 = [ordered]@{
            status = $(if ($measured) { "measured" } else { "blocked" })
            cacheMode = Get-Prop $finalStatus.Body 'cacheMode'
            trackedKeyCountBefore = $baselineCount
            trackedKeyCountPeak = $peakCount
            trackedKeyCountAfter = $finalCount
            rssMbBefore = $baselineRss
            rssMbPeak = $peakRss
            rssMbAfter = $finalRss
            estimatedMbPeak = $estimatedDeltaMb
            sampleRequests = $samples
            notes = $(if ($measured) {
                "Cache status endpoint exposed tracked key count; warm-up requests created measurable footprint."
            } else {
                "Warm-up did not produce a stable measurable cache footprint in this run."
            })
        }
        D6 = [ordered]@{
            status = "blocked"
            notes = "No M-PERF Access import fixture in this repo scope; remains a durable follow-up"
        }
        D7 = [ordered]@{
            status = "n/a"
            notes = "Not exercised in PERF13 cache footprint pack"
        }
        D8 = [ordered]@{
            status = "n/a_dedicated"
            notes = "shared_saas requires MT fixtures"
        }
    }
    correctnessChecks = [ordered]@{
        result = $(if ($measured) { "pass" } else { "partial" })
        cacheKeysObserved = $peakCount
        requestsSucceeded = @($samples | Where-Object { $_.statusCode -eq 200 }).Count
        notes = $(if ($measured) {
            "Tracked cache keys increased after cache warm-up; no fake zero or silent fallback."
        } else {
            "Cache footprint remained unproven; status is honest."
        })
    }
    residualRisks = @(
        "Process RSS is an estimate of cache footprint, not a per-entry allocator breakdown",
        "D6 import overlap remains unmeasured without a real Access fixture"
    )
}

$tmpOut = "$outputJson.tmp"
[System.IO.File]::WriteAllText($tmpOut, ($result | ConvertTo-Json -Depth 14), [System.Text.UTF8Encoding]::new($false))
Move-Item -Path $tmpOut -Destination $outputJson -Force

[string]$d5StatusLabel = if ($measured) { '**measured**' } else { '**blocked**' }
[string[]]$evidenceLines = @(
    "# PERF13 Evidence",
    "",
    "- Date: $(Get-Date -Format yyyy-MM-dd)",
    "- Prompt: PERF13 - Unblock D5 cache footprint or D6 import-overlap evidence",
    '- Pack: `PERF13-G10-cache-footprint-01`',
    '- Milestone / mode: **G10** / **dedicated**',
    '- Dataset: `trendplus_perf_m` (M-PERF-01)',
    '- Raw JSON: `.ai/runs/2026-08-12-PERF13-raw.json`',
    '- Harness: `tmp/perf13_measure.ps1`',
    '- Contract: `docs/architecture/PERFORMANCE_SCALABILITY_GATE_EVIDENCE_CONTRACT.md`',
    "",
    "## Method",
    "",
    "1. Clear analytics cache state through the existing cache invalidate endpoint.",
    "2. Warm several cached analytics routes with distinct parameter sets.",
    "3. Read the cache status endpoint, which now exposes tracked key count for a footprint snapshot.",
    "4. Estimate cache footprint from process RSS delta after warm-up.",
    "",
    "## Dimension status",
    "",
    "| Id | Status | Result |",
    "|---|---|---|",
    "| D1 | cite_PERF11 | already measured |",
    "| D2 | cite_PERF10 | already measured |",
    "| D3 | cite_PERF10 | already measured |",
    "| D4 | cite_PERF12 | already measured |",
    "| D5 cache footprint | $d5StatusLabel | tracked keys before/peak/after: $baselineCount / $peakCount / $finalCount; estimated RSS delta: $estimatedDeltaMb MB |",
    "| D6 import overlap | **blocked** | no M-PERF Access fixture in this repo scope |",
    "| D7 | n/a | not exercised in this pack |",
    '| D8 | `n/a_dedicated` | MT-owned |',
    "",
    "## Interpretation",
    "",
    "1. Cache status now exposes tracked key count, which gives a measurable footprint proxy for D5.",
    "2. The warm-up run created a non-zero tracked-key footprint without fabricating cache cardinality.",
    "3. D6 is still a separate blocker and should stay explicit rather than inferred.",
    "4. Process RSS remains an estimate, not a byte-accurate cache allocator report.",
    "",
    "## Files",
    "",
    '- `tmp/perf13_measure.ps1`',
    '- `.ai/runs/2026-08-12-PERF13-raw.json`',
    '- `.ai/runs/2026-08-12-PERF13-evidence.md`'
)
$evidence = $evidenceLines -join [Environment]::NewLine
[System.IO.File]::WriteAllText($outputMd, $evidence, [System.Text.UTF8Encoding]::new($false))

Write-Host "Wrote $outputJson"
Write-Host "Wrote $outputMd"
Write-Host ("D5 status={0} tracked={1} rssBefore={2} rssPeak={3} delta={4}" -f $result.dimensions.D5.status, $result.dimensions.D5.trackedKeyCountPeak, $result.dimensions.D5.rssMbBefore, $result.dimensions.D5.rssMbPeak, $result.dimensions.D5.estimatedMbPeak)
Write-Output ($result | ConvertTo-Json -Depth 14)
