param(
    [string]$ApiBaseUrl = "http://127.0.0.1:8080",
    [string]$PgHost = "127.0.0.1",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [string]$PgPassword = "postgres",
    [string]$Database = "trendplus_perf_m",
    [int]$ConcurrentReads = 10,
    [int]$Waves = 3,
    [switch]$SkipSetup
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "Api\Api.csproj"
$outputJson = Join-Path $repoRoot ".ai\runs\2026-08-12-PERF10-raw.json"
$fromDate = "2026-02-13T06:00:00.0000000Z"
$toDate = "2026-08-12T06:00:00.0000000Z"
$connectionString = "Host=$PgHost;Port=$PgPort;Database=$Database;Username=$PgUser;Password=$PgPassword"
$bootstrapPath = "/api/analytics/cached/dashboard/bootstrap?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))&dataScope=all"
$salesPath = "/api/analytics/cached/sales/summary?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))"
$bootstrapUrl = "$ApiBaseUrl$bootstrapPath"
$salesUrl = "$ApiBaseUrl$salesPath"
$global:PerfApiProcess = $null
$env:PGPASSWORD = $PgPassword

function Get-Percentile {
    param([double[]]$Values, [double]$Percentile)
    if ($null -eq $Values -or $Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    $rank = ($Percentile / 100.0) * ($sorted.Count - 1)
    $low = [math]::Floor($rank)
    $high = [math]::Ceiling($rank)
    if ($low -eq $high) { return [math]::Round($sorted[$low], 2) }
    $weight = $rank - $low
    return [math]::Round(($sorted[$low] * (1 - $weight) + $sorted[$high] * $weight), 2)
}

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
    $env:ASPNETCORE_URLS = "http://localhost:8080"
    $env:ConnectionStrings__DefaultConnection = $connectionString
    $env:ConnectionStrings__AnalyticsConnection = $connectionString
    $env:ConnectionStrings__OpenProductTrainingConnection = $connectionString
    $env:Workers__Enabled = "false"
    $env:AnalyticsPrewarm__Enabled = "false"

    $apiLog = Join-Path $env:TEMP "perf10_api.log"
    $apiErr = Join-Path $env:TEMP "perf10_api.err.log"
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

function Invoke-TimedGet {
    param([string]$Url, [int]$TimeoutSec = 120)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec
        $sw.Stop()
        return [pscustomobject]@{
            StatusCode = [int]$resp.StatusCode
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Raw = $resp.Content
            TimedOut = $false
            Error = $null
        }
    } catch {
        $sw.Stop()
        $timedOut = $_.Exception.Message -match "timeout|timed out|TaskCanceled"
        return [pscustomobject]@{
            StatusCode = 0
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Raw = $null
            TimedOut = $timedOut
            Error = $_.Exception.Message
        }
    }
}

function Get-JsonProperty {
    param($Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -ne $prop) { return $prop.Value }
    $alt = $Object.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
    if ($null -ne $alt) { return $alt.Value }
    return $null
}

function Invoke-PgScalar {
    param([string]$Sql)
    $out = & psql -h $PgHost -p $PgPort -U $PgUser -d $Database -t -A -c $Sql 2>&1
    if ($LASTEXITCODE -ne 0) {
        return [pscustomobject]@{ Ok = $false; Value = $null; Error = ($out | Out-String).Trim() }
    }
    return [pscustomobject]@{ Ok = $true; Value = ($out | Out-String).Trim(); Error = $null }
}

function Get-PgConnectionSnapshot {
    $active = Invoke-PgScalar -Sql "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND state = 'active';"
    $total = Invoke-PgScalar -Sql "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database();"
    $waiting = Invoke-PgScalar -Sql "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type IS NOT NULL AND state <> 'idle';"
    $maxConn = Invoke-PgScalar -Sql "SHOW max_connections;"
    return [ordered]@{
        activeConnections = if ($active.Ok) { [int]$active.Value } else { $null }
        totalConnections = if ($total.Ok) { [int]$total.Value } else { $null }
        waitingConnections = if ($waiting.Ok) { [int]$waiting.Value } else { $null }
        maxConnections = if ($maxConn.Ok) { [int]$maxConn.Value } else { $null }
        queryErrors = @($active, $total, $waiting, $maxConn | Where-Object { -not $_.Ok } | ForEach-Object { $_.Error })
    }
}

if (-not $SkipSetup) {
    & (Join-Path $PSScriptRoot "perf05_setup_db.ps1") -TargetDb $Database
}

$script:ApiBuilt = $false
Write-Host "Starting warm API for PERF10 scalability pack ..."
Start-PerfApi

try {
    Write-Host "Priming warm cache (bootstrap + sales) ..."
    $primeBootstrap = Invoke-TimedGet -Url $bootstrapUrl -TimeoutSec 300
    $primeSales = Invoke-TimedGet -Url $salesUrl -TimeoutSec 120
    $warmBootstrap = Invoke-TimedGet -Url $bootstrapUrl -TimeoutSec 120
    $warmSales = Invoke-TimedGet -Url $salesUrl -TimeoutSec 120

    $pgBefore = Get-PgConnectionSnapshot
    $rssBeforeMb = $null
    if ($global:PerfApiProcess -and -not $global:PerfApiProcess.HasExited) {
        $proc = Get-Process -Id $global:PerfApiProcess.Id -ErrorAction SilentlyContinue
        if ($proc) { $rssBeforeMb = [math]::Round($proc.WorkingSet64 / 1MB, 2) }
    }

    Write-Host "D2 concurrent warm reads: $ConcurrentReads x $Waves waves ..."
    $samples = @()
    $pgPeaks = @()
    for ($wave = 1; $wave -le $Waves; $wave++) {
        Write-Host "  wave $wave/$Waves"
        $jobs = @()
        for ($i = 1; $i -le $ConcurrentReads; $i++) {
            $url = if (($i % 2) -eq 0) { $salesUrl } else { $bootstrapUrl }
            $jobs += Start-Job -ScriptBlock {
                param($TargetUrl)
                $sw = [System.Diagnostics.Stopwatch]::StartNew()
                try {
                    $resp = Invoke-WebRequest -Uri $TargetUrl -UseBasicParsing -TimeoutSec 120
                    $sw.Stop()
                    [pscustomobject]@{
                        StatusCode = [int]$resp.StatusCode
                        ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
                        TimedOut = $false
                        Error = $null
                        Url = $TargetUrl
                    }
                } catch {
                    $sw.Stop()
                    [pscustomobject]@{
                        StatusCode = 0
                        ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
                        TimedOut = ($_.Exception.Message -match "timeout|timed out|TaskCanceled")
                        Error = $_.Exception.Message
                        Url = $TargetUrl
                    }
                }
            } -ArgumentList $url
        }

        # Sample connection pressure while jobs run
        Start-Sleep -Milliseconds 200
        $pgPeaks += ,(Get-PgConnectionSnapshot)

        $waveResults = $jobs | Wait-Job | Receive-Job
        $jobs | Remove-Job -Force
        $idx = 0
        foreach ($r in $waveResults) {
            $idx++
            $samples += [pscustomobject]@{
                wave = $wave
                sample = $idx
                route = if ($r.Url -like "*bootstrap*") { "bootstrap" } else { "sales.summary" }
                statusCode = $r.StatusCode
                elapsedMs = $r.ElapsedMs
                timedOut = [bool]$r.TimedOut
                error = $r.Error
            }
        }
        $pgPeaks += ,(Get-PgConnectionSnapshot)
    }

    $pgAfter = Get-PgConnectionSnapshot
    $rssAfterMb = $null
    if ($global:PerfApiProcess -and -not $global:PerfApiProcess.HasExited) {
        $proc = Get-Process -Id $global:PerfApiProcess.Id -ErrorAction SilentlyContinue
        if ($proc) { $rssAfterMb = [math]::Round($proc.WorkingSet64 / 1MB, 2) }
    }

    $okSamples = @($samples | Where-Object { $_.statusCode -eq 200 -and -not $_.timedOut })
    $errorCount = @($samples | Where-Object { $_.statusCode -ne 200 -and -not $_.timedOut }).Count
    $timeoutCount = @($samples | Where-Object { $_.timedOut }).Count
    $latencies = @($okSamples | ForEach-Object { [double]$_.elapsedMs })
    $activePeaks = @($pgPeaks | ForEach-Object { if ($null -ne $_.activeConnections) { [int]$_.activeConnections } })
    $totalPeaks = @($pgPeaks | ForEach-Object { if ($null -ne $_.totalConnections) { [int]$_.totalConnections } })

    # Correctness on a post-burst warm sample
    $correctnessBootstrap = Invoke-TimedGet -Url $bootstrapUrl -TimeoutSec 120
    $body = $null
    try { if ($correctnessBootstrap.Raw) { $body = $correctnessBootstrap.Raw | ConvertFrom-Json -Depth 20 } } catch {}
    $meta = Get-JsonProperty $body 'meta'
    if ($null -eq $meta) { $meta = Get-JsonProperty $body 'Meta' }
    $summary = Get-JsonProperty $body 'summary'
    if ($null -eq $summary) { $summary = Get-JsonProperty $body 'Summary' }
    $inventory = Get-JsonProperty $body 'inventory'
    if ($null -eq $inventory) { $inventory = Get-JsonProperty $body 'Inventory' }
    $success = Get-JsonProperty $meta 'success'
    if ($null -eq $success) { $success = Get-JsonProperty $meta 'Success' }
    $isPartial = Get-JsonProperty $meta 'isPartial'
    if ($null -eq $isPartial) { $isPartial = Get-JsonProperty $meta 'IsPartial' }
    $warningCode = Get-JsonProperty $meta 'warningCode'
    if ($null -eq $warningCode) { $warningCode = Get-JsonProperty $meta 'WarningCode' }
    $totalRevenue = Get-JsonProperty $summary 'totalRevenue'
    if ($null -eq $totalRevenue) { $totalRevenue = Get-JsonProperty $summary 'TotalRevenue' }
    $skuCount = Get-JsonProperty $inventory 'totalSkuCount'
    if ($null -eq $skuCount) { $skuCount = Get-JsonProperty $inventory 'TotalSkuCount' }

    $correctnessPass = ($correctnessBootstrap.StatusCode -eq 200) -and (
        ($null -eq $success) -or ($success -eq $true)
    ) -and (
        ($null -eq $totalRevenue) -or ([double]$totalRevenue -gt 0)
    )

    $result = [ordered]@{
        meta = [ordered]@{
            packId = "PERF10-G10-dedicated-01"
            commit = (git -C $repoRoot rev-parse HEAD).Trim()
            datetimeUtc = (Get-Date).ToUniversalTime().ToString("o")
            milestone = "G10"
            deploymentMode = "dedicated"
            datasetTier = "M"
            seedRecipeId = "M-PERF-01"
            database = $Database
            apiBaseUrl = $ApiBaseUrl
            prewarmEnabled = $false
            workersEnabled = $false
            periodFrom = $fromDate
            periodTo = $toDate
            concurrentAnalyticsReadsAssumed = $ConcurrentReads
            waves = $Waves
        }
        priming = [ordered]@{
            processState = "warm"
            cacheState = "cold-then-warm"
            bootstrapFirstMs = $primeBootstrap.ElapsedMs
            bootstrapFirstStatus = $primeBootstrap.StatusCode
            salesFirstMs = $primeSales.ElapsedMs
            salesFirstStatus = $primeSales.StatusCode
            bootstrapWarmMs = $warmBootstrap.ElapsedMs
            salesWarmMs = $warmSales.ElapsedMs
        }
        dimensions = [ordered]@{
            D1 = [ordered]@{
                status = "partial"
                notes = "API process RSS observed only; CPU/disk/connection budgets unmeasured"
                memoryMbObservedBefore = $rssBeforeMb
                memoryMbObservedAfter = $rssAfterMb
                cpuCoresReserved = $null
                diskGbReserved = $null
                postgresConnectionsBudget = $null
                cacheFootprintMbBudget = $null
            }
            D2 = [ordered]@{
                status = "measured"
                concurrentUsersAssumed = $ConcurrentReads
                concurrentAnalyticsReadsAssumed = $ConcurrentReads
                representativeRoutes = @("dashboard.bootstrap", "sales.summary")
                coldWarmMatrix = "warm-process x warm-cache"
                samples = $samples.Count
                successSamples = $okSamples.Count
                p50Ms = Get-Percentile -Values $latencies -Percentile 50
                p95Ms = Get-Percentile -Values $latencies -Percentile 95
                errorRate = if ($samples.Count -gt 0) { [math]::Round($errorCount / $samples.Count, 4) } else { $null }
                timeoutRate = if ($samples.Count -gt 0) { [math]::Round($timeoutCount / $samples.Count, 4) } else { $null }
                samplesDetail = @($samples)
            }
            D3 = [ordered]@{
                status = "measured"
                poolSizeConfigured = $null
                notes = "pg_stat_activity snapshots during concurrent waves; poolSizeConfigured unknown from this harness"
                before = $pgBefore
                peakActiveConnections = if ($activePeaks.Count -gt 0) { ($activePeaks | Measure-Object -Maximum).Maximum } else { $null }
                peakTotalConnections = if ($totalPeaks.Count -gt 0) { ($totalPeaks | Measure-Object -Maximum).Maximum } else { $null }
                after = $pgAfter
                waitOrTimeoutCount = $null
                statementTimeoutHits = $null
                snapshots = @($pgPeaks)
            }
            D4 = [ordered]@{ status = "deferred"; notes = "workers disabled for this pack" }
            D5 = [ordered]@{ status = "unmeasured"; notes = "cache footprint instrumentation not available in harness" }
            D6 = [ordered]@{ status = "deferred"; notes = "import overlap not in PERF10 first pack" }
            D7 = [ordered]@{ status = "deferred"; notes = "report/export bursts not in PERF10 first pack" }
            D8 = [ordered]@{ status = "n/a_dedicated"; notes = "shared_saas requires MT fixtures" }
        }
        correctnessChecks = [ordered]@{
            result = if ($correctnessPass) { "pass" } else { "fail" }
            bootstrapStatusCode = $correctnessBootstrap.StatusCode
            success = $success
            isPartial = $isPartial
            warningCode = $warningCode
            totalRevenue = $totalRevenue
            totalSkuCount = $skuCount
            notes = "no fake-zero check: revenue null or >0; HTTP 200 required"
        }
        residualRisks = @(
            "single-host dedicated pack; not multi-customer proof",
            "D1 resource envelope incomplete",
            "D4-D7 deferred",
            "connection pool size not read from API config"
        )
    }

    [System.IO.File]::WriteAllText($outputJson, ($result | ConvertTo-Json -Depth 12), [System.Text.UTF8Encoding]::new($false))
    Write-Host "Wrote $outputJson"
    Write-Host ("D2 p50={0}ms p95={1}ms errorRate={2} timeoutRate={3}" -f $result.dimensions.D2.p50Ms, $result.dimensions.D2.p95Ms, $result.dimensions.D2.errorRate, $result.dimensions.D2.timeoutRate)
    Write-Host ("D3 peakActive={0} peakTotal={1}" -f $result.dimensions.D3.peakActiveConnections, $result.dimensions.D3.peakTotalConnections)
}
finally {
    Stop-PerfApi
}
