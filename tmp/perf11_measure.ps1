param(
    [string]$ApiBaseUrl = "http://127.0.0.1:8080",
    [string]$WorkerBaseUrl = "http://127.0.0.1:8081",
    [string]$PgHost = "127.0.0.1",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [string]$PgPassword = "postgres",
    [string]$Database = "trendplus_perf_m",
    [string]$AdminKey = "perf11-worker-key",
    [int]$ConcurrentReads = 10,
    [int]$Waves = 2,
    [switch]$SkipSetup
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "Api\Api.csproj"
$outputJson = Join-Path $repoRoot ".ai\runs\2026-08-12-PERF11-raw.json"
$fromDate = "2026-02-13T06:00:00.0000000Z"
$toDate = "2026-08-12T06:00:00.0000000Z"
$connectionString = "Host=$PgHost;Port=$PgPort;Database=$Database;Username=$PgUser;Password=$PgPassword"
$bootstrapUrl = "$ApiBaseUrl/api/analytics/cached/dashboard/bootstrap?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))&dataScope=all"
$salesUrl = "$ApiBaseUrl/api/analytics/cached/sales/summary?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))"
$global:PerfApiProcess = $null
$global:PerfWorkerProcess = $null
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

function Stop-PerfPorts {
    param([int[]]$Ports)
    foreach ($proc in @($global:PerfApiProcess, $global:PerfWorkerProcess)) {
        if ($proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
    }
    foreach ($port in $Ports) {
        Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
            ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
    }
    Start-Sleep -Seconds 2
}

function Start-PerfProcess {
    param(
        [ValidateSet("api", "worker")]
        [string]$Role,
        [int]$Port,
        [bool]$WorkersEnabled
    )
    if (-not $script:ApiBuilt) {
        dotnet build $apiProject -v q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
        $script:ApiBuilt = $true
    }

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:DOTNET_ENVIRONMENT = "Development"
    $env:PORT = "$Port"
    $env:ASPNETCORE_URLS = "http://localhost:$Port"
    $env:ConnectionStrings__DefaultConnection = $connectionString
    $env:ConnectionStrings__AnalyticsConnection = $connectionString
    $env:ConnectionStrings__OpenProductTrainingConnection = $connectionString
    $env:Workers__Enabled = $WorkersEnabled.ToString().ToLowerInvariant()
    $env:AnalyticsPrewarm__Enabled = "false"
    $env:ADMIN_API_KEY = $AdminKey
    $env:Admin__ApiKey = $AdminKey
    if ($Role -eq "worker") {
        $env:PROCESS_TYPE = "worker"
    } else {
        Remove-Item Env:PROCESS_TYPE -ErrorAction SilentlyContinue
    }

    $log = Join-Path $env:TEMP "perf11_$Role.log"
    $err = Join-Path $env:TEMP "perf11_$Role.err.log"
    $proc = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--project", $apiProject, "--no-launch-profile", "--no-build", "--urls", "http://localhost:$Port") `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput $log -RedirectStandardError $err -WorkingDirectory $repoRoot

    if ($Role -eq "worker") { $global:PerfWorkerProcess = $proc } else { $global:PerfApiProcess = $proc }

    $base = "http://127.0.0.1:$Port"
    $deadline = (Get-Date).AddSeconds(240)
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -Uri "$base/health" -UseBasicParsing -TimeoutSec 5
            if ($resp.StatusCode -eq 200) { return $proc }
        } catch { Start-Sleep -Seconds 2 }
    }
    throw "$Role health probe failed. See $log / $err"
}

function Invoke-TimedGet {
    param([string]$Url, [int]$TimeoutSec = 120)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec
        $sw.Stop()
        $body = $null
        try { $body = $resp.Content | ConvertFrom-Json -Depth 20 } catch {}
        return [pscustomobject]@{
            StatusCode = [int]$resp.StatusCode
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Body = $body
            Raw = $resp.Content
            Error = $null
        }
    } catch {
        $sw.Stop()
        return [pscustomobject]@{
            StatusCode = 0
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Body = $null
            Raw = $null
            Error = $_.Exception.Message
        }
    }
}

function Invoke-TimedRequest {
    param([string]$Url, [string]$Method = "GET", [hashtable]$Headers = @{}, [int]$TimeoutSec = 120)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-WebRequest -Uri $Url -Method $Method -Headers $Headers -UseBasicParsing -TimeoutSec $TimeoutSec
        $sw.Stop()
        $body = $null
        try { $body = $resp.Content | ConvertFrom-Json -Depth 20 } catch {}
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

function Get-JsonProperty {
    param($Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
    if ($null -ne $prop) { return $prop.Value }
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
Stop-PerfPorts -Ports @(8080, 8081)

# --- D1 envelope + load probe ---
Write-Host "D1 resource envelope + warm load probe ..."
Start-PerfProcess -Role api -Port 8080 -WorkersEnabled $false | Out-Null
$null = Invoke-TimedGet -Url $bootstrapUrl -TimeoutSec 300
$null = Invoke-TimedGet -Url $salesUrl -TimeoutSec 120

$cpuLogical = [Environment]::ProcessorCount
$dbSizeBytes = Invoke-PgScalar -Sql "SELECT pg_database_size(current_database());"
$maxConn = Invoke-PgScalar -Sql "SHOW max_connections;"
$rssBefore = Get-ProcessRssMb -Process $global:PerfApiProcess
$rssPeak = $rssBefore
$loadSamples = @()

for ($wave = 1; $wave -le $Waves; $wave++) {
    Write-Host "  load wave $wave/$Waves"
    $jobs = @()
    for ($i = 1; $i -le $ConcurrentReads; $i++) {
        $url = if (($i % 2) -eq 0) { $salesUrl } else { $bootstrapUrl }
        $jobs += Start-Job -ScriptBlock {
            param($TargetUrl)
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            try {
                $resp = Invoke-WebRequest -Uri $TargetUrl -UseBasicParsing -TimeoutSec 120
                $sw.Stop()
                [pscustomobject]@{ StatusCode = [int]$resp.StatusCode; ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2); TimedOut = $false }
            } catch {
                $sw.Stop()
                [pscustomobject]@{ StatusCode = 0; ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2); TimedOut = ($_.Exception.Message -match "timeout|timed out|TaskCanceled") }
            }
        } -ArgumentList $url
    }
    for ($t = 0; $t -lt 20; $t++) {
        $rss = Get-ProcessRssMb -Process $global:PerfApiProcess
        if ($null -ne $rss -and ($null -eq $rssPeak -or $rss -gt $rssPeak)) { $rssPeak = $rss }
        Start-Sleep -Milliseconds 100
    }
    $waveResults = $jobs | Wait-Job | Receive-Job
    $jobs | Remove-Job -Force
    $loadSamples += @($waveResults)
}
$rssAfter = Get-ProcessRssMb -Process $global:PerfApiProcess
$okLoad = @($loadSamples | Where-Object { $_.StatusCode -eq 200 -and -not $_.TimedOut })
$loadLatencies = @($okLoad | ForEach-Object { [double]$_.ElapsedMs })

Stop-PerfPorts -Ports @(8080)

# --- D4 worker cycle (same host process with workers enabled; PROCESS_TYPE=worker binds 8080 and breaks dedicated port) ---
Write-Host "D4 AnalyticsAggregationWorker cycle ..."
Stop-PerfPorts -Ports @(8080, 8081)
Start-PerfProcess -Role api -Port 8080 -WorkersEnabled $true | Out-Null
$configUrl = "$ApiBaseUrl/api/workers/configuration"
$startUrl = "$ApiBaseUrl/api/workers/AnalyticsAggregationWorker/start"
$beforeConfig = Invoke-TimedGet -Url $configUrl -TimeoutSec 120
$workersBefore = Get-JsonProperty $beforeConfig.Body 'workers'
if ($null -eq $workersBefore) { $workersBefore = Get-JsonProperty $beforeConfig.Body 'Workers' }
$workerBefore = $null
if ($workersBefore) {
    $workerBefore = @($workersBefore) | Where-Object {
        ((Get-JsonProperty $_ 'workerName') -eq 'AnalyticsAggregationWorker') -or ((Get-JsonProperty $_ 'WorkerName') -eq 'AnalyticsAggregationWorker')
    } | Select-Object -First 1
}
$startResponse = Invoke-TimedRequest -Url $startUrl -Method "POST" -Headers @{ "X-Admin-Key" = $AdminKey } -TimeoutSec 120
$cycleTimer = [System.Diagnostics.Stopwatch]::StartNew()
$polls = @()
$workerAfter = $null
$d4Status = "timeout"
while ($cycleTimer.Elapsed.TotalSeconds -lt 300) {
    Start-Sleep -Seconds 5
    $snapResp = Invoke-TimedGet -Url $configUrl -TimeoutSec 120
    $workers = Get-JsonProperty $snapResp.Body 'workers'
    if ($null -eq $workers) { $workers = Get-JsonProperty $snapResp.Body 'Workers' }
    $snap = $null
    if ($workers) {
        $snap = @($workers) | Where-Object {
            ((Get-JsonProperty $_ 'workerName') -eq 'AnalyticsAggregationWorker') -or ((Get-JsonProperty $_ 'WorkerName') -eq 'AnalyticsAggregationWorker')
        } | Select-Object -First 1
    }
    $runtime = Get-JsonProperty $snap 'runtimeStatus'
    if ($null -eq $runtime) { $runtime = Get-JsonProperty $snap 'RuntimeStatus' }
    $heartbeat = Get-JsonProperty $snap 'lastHeartbeat'
    if ($null -eq $heartbeat) { $heartbeat = Get-JsonProperty $snap 'LastHeartbeat' }
    $polls += [pscustomobject]@{
        elapsedMs = [math]::Round($cycleTimer.Elapsed.TotalMilliseconds, 2)
        runtimeStatus = $runtime
        lastHeartbeat = $heartbeat
        configStatusCode = $snapResp.StatusCode
    }
    if ($runtime -eq "Healthy" -and $null -ne $heartbeat) {
        $d4Status = "measured"
        $workerAfter = $snap
        break
    }
}
$cycleTimer.Stop()
if ($null -eq $workerAfter) {
    $final = Invoke-TimedGet -Url $configUrl -TimeoutSec 120
    $workers = Get-JsonProperty $final.Body 'workers'
    if ($null -eq $workers) { $workers = Get-JsonProperty $final.Body 'Workers' }
    if ($workers) {
        $workerAfter = @($workers) | Where-Object {
            ((Get-JsonProperty $_ 'workerName') -eq 'AnalyticsAggregationWorker') -or ((Get-JsonProperty $_ 'WorkerName') -eq 'AnalyticsAggregationWorker')
        } | Select-Object -First 1
    }
}
$workerRssPeak = Get-ProcessRssMb -Process $global:PerfApiProcess
Stop-PerfPorts -Ports @(8080, 8081)

$dbSizeMb = $null
$dbSizeGb = $null
if ($dbSizeBytes.Ok -and $dbSizeBytes.Value) {
    $dbSizeMb = [math]::Round(([double]$dbSizeBytes.Value) / 1MB, 2)
    $dbSizeGb = [math]::Round(([double]$dbSizeBytes.Value) / 1GB, 3)
}

$result = [ordered]@{
    meta = [ordered]@{
        packId = "PERF11-G10-dedicated-deferred-01"
        commit = (git -C $repoRoot rev-parse HEAD).Trim()
        datetimeUtc = (Get-Date).ToUniversalTime().ToString("o")
        milestone = "G10"
        deploymentMode = "dedicated"
        datasetTier = "M"
        seedRecipeId = "M-PERF-01"
        database = $Database
        machine = $env:COMPUTERNAME
        os = (Get-CimInstance Win32_OperatingSystem).Caption
        prewarmEnabled = $false
        workerMode = "web-process-with-Workers__Enabled"
    }
    dimensions = [ordered]@{
        D1 = [ordered]@{
            status = "measured"
            notes = "Observed dedicated-host envelope under warm concurrent analytics load; reserved budgets remain non-SLO placeholders"
            cpuCoresObserved = $cpuLogical
            memoryMbObservedBefore = $rssBefore
            memoryMbPeakObserved = $rssPeak
            memoryMbObservedAfter = $rssAfter
            diskGbDatabaseObserved = $dbSizeGb
            diskMbDatabaseObserved = $dbSizeMb
            postgresMaxConnectionsObserved = if ($maxConn.Ok) { [int]$maxConn.Value } else { $null }
            postgresConnectionsBudget = $null
            cacheFootprintMbBudget = $null
            loadProbe = [ordered]@{
                concurrentReads = $ConcurrentReads
                waves = $Waves
                successSamples = $okLoad.Count
                p50Ms = Get-Percentile -Values $loadLatencies -Percentile 50
                p95Ms = Get-Percentile -Values $loadLatencies -Percentile 95
            }
        }
        D2 = [ordered]@{ status = "cite_PERF10"; notes = "See PERF10 concurrent pack; not re-baselined here" }
        D3 = [ordered]@{ status = "cite_PERF10"; notes = "See PERF10 connection-pressure pack; not re-baselined here" }
        D4 = [ordered]@{
            status = if ($d4Status -eq "measured") { "measured" } else { "blocked" }
            workerType = "AnalyticsAggregationWorker"
            maxParallelJobs = 1
            cycleElapsedMs = [math]::Round($cycleTimer.Elapsed.TotalMilliseconds, 2)
            startRequestMs = $startResponse.ElapsedMs
            startStatusCode = $startResponse.StatusCode
            startError = $startResponse.Error
            beforeRuntimeStatus = if ($null -ne (Get-JsonProperty $workerBefore 'runtimeStatus')) { Get-JsonProperty $workerBefore 'runtimeStatus' } else { Get-JsonProperty $workerBefore 'RuntimeStatus' }
            afterRuntimeStatus = if ($null -ne (Get-JsonProperty $workerAfter 'runtimeStatus')) { Get-JsonProperty $workerAfter 'runtimeStatus' } else { Get-JsonProperty $workerAfter 'RuntimeStatus' }
            afterLastHeartbeat = if ($null -ne (Get-JsonProperty $workerAfter 'lastHeartbeat')) { Get-JsonProperty $workerAfter 'lastHeartbeat' } else { Get-JsonProperty $workerAfter 'LastHeartbeat' }
            workerRssMb = $workerRssPeak
            queueDepthPeak = $null
            retryOrPoisonCount = $null
            polls = @($polls)
            notes = if ($d4Status -eq "measured") {
                "Manual start reached Healthy with heartbeat within 300s on web process with Workers__Enabled"
            } else {
                "Worker cycle did not reach Healthy+heartbeat within 300s; recorded as blocked-with-reason"
            }
        }
        D5 = [ordered]@{
            status = "blocked"
            notes = "No cache cardinality/MB instrumentation exposed to harness; cannot invent footprint"
        }
        D6 = [ordered]@{
            status = "deferred"
            notes = "No M-PERF Access import fixture; same gap as PERF05 B4"
        }
        D7 = [ordered]@{
            status = "deferred"
            notes = "Report/export burst pack not in PERF11 scope"
        }
        D8 = [ordered]@{
            status = "n/a_dedicated"
            notes = "shared_saas requires MT fixtures"
        }
    }
    correctnessChecks = [ordered]@{
        result = if ($okLoad.Count -gt 0) { "pass" } else { "fail" }
        loadSuccessSamples = $okLoad.Count
        workerStartStatusCode = $startResponse.StatusCode
        workerReachedHealthy = ($d4Status -eq "measured")
        notes = "D1 load samples require HTTP 200; D4 records Healthy+heartbeat when available without masking timeout"
    }
    residualRisks = @(
        "observed envelope is not a reserved multi-customer budget",
        "D5 blocked without instrumentation",
        "D6/D7 deferred",
        "single-host dedicated only"
    )
}

[System.IO.File]::WriteAllText($outputJson, ($result | ConvertTo-Json -Depth 12), [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $outputJson"
Write-Host ("D1 cpu={0} peakRssMb={1} dbGb={2}" -f $cpuLogical, $rssPeak, $dbSizeGb)
Write-Host ("D4 status={0} cycleMs={1} startStatus={2}" -f $result.dimensions.D4.status, $result.dimensions.D4.cycleElapsedMs, $startResponse.StatusCode)
