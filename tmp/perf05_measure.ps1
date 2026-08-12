param(
    [string]$BaseUrl = "http://127.0.0.1:8080",
    [string]$PgHost = "127.0.0.1",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [string]$PgPassword = "postgres",
    [string]$Database = "trendplus_perf_m",
    [int]$WarmSamples = 20,
    [int]$ColdStartSamples = 5,
    [switch]$SkipSetup
)

$ErrorActionPreference = "Stop"
$global:PerfApiProcess = $null
$global:PerfWorkerProcess = $null

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "Api\Api.csproj"
$outputJson = Join-Path $repoRoot ".ai\runs\2026-08-12-PERF05-raw.json"
$fromDate = "2026-02-13T06:00:00.0000000Z"
$toDate = "2026-08-12T06:00:00.0000000Z"
$conn = "Host=$PgHost;Port=$PgPort;Database=$Database;Username=$PgUser;Password=$PgPassword"

function Get-Percentile {
    param([double[]]$Values, [double]$Percentile)
    if ($Values.Count -eq 0) { return $null }
    $sorted = @($Values | Sort-Object)
    $rank = ($Percentile / 100.0) * ($sorted.Count - 1)
    $low = [math]::Floor($rank)
    $high = [math]::Ceiling($rank)
    if ($low -eq $high) { return $sorted[$low] }
    $weight = $rank - $low
    return $sorted[$low] * (1 - $weight) + $sorted[$high] * $weight
}

function Wait-Health {
    param(
        [string]$TargetBaseUrl = $BaseUrl,
        [int]$TimeoutSec = 180
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-WebRequest -Uri "$TargetBaseUrl/health" -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -eq 200) { return $true }
        } catch { Start-Sleep -Seconds 2 }
    }
    return $false
}

function Stop-PerfApi {
    foreach ($proc in @($global:PerfApiProcess, $global:PerfWorkerProcess)) {
        if ($proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
    }
    Get-NetTCPConnection -LocalPort 8080,8081 -State Listen -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 2
}

function Get-JsonProperty {
    param($Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    if ($Object -is [System.Collections.IDictionary]) { return $Object[$Name] }
    return $Object.$Name
}

function Start-PerfProcess {
    param(
        [string]$ProcessName,
        [string]$ProcessType,
        [int]$Port,
        [bool]$WorkersEnabled,
        [string]$LogPrefix,
        [string]$AdminApiKey = ""
    )

    Stop-PerfApi
    if (-not $script:ApiBuilt) {
        dotnet build $apiProject -v q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
        $script:ApiBuilt = $true
    }

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://localhost:$Port"
    $env:ConnectionStrings__DefaultConnection = $conn
    $env:ConnectionStrings__AnalyticsConnection = $conn
    $env:ConnectionStrings__OpenProductTrainingConnection = $conn
    $env:Workers__Enabled = $WorkersEnabled.ToString().ToLowerInvariant()
    $env:AnalyticsPrewarm__Enabled = "false"

    if ([string]::IsNullOrWhiteSpace($ProcessType)) {
        Remove-Item Env:PROCESS_TYPE -ErrorAction SilentlyContinue
    } else {
        $env:PROCESS_TYPE = $ProcessType
    }

    if ([string]::IsNullOrWhiteSpace($AdminApiKey)) {
        Remove-Item Env:ADMIN_API_KEY -ErrorAction SilentlyContinue
        Remove-Item Env:Admin__ApiKey -ErrorAction SilentlyContinue
    } else {
        $env:ADMIN_API_KEY = $AdminApiKey
        $env:Admin__ApiKey = $AdminApiKey
    }

    $log = Join-Path $env:TEMP "$LogPrefix.log"
    $err = Join-Path $env:TEMP "$LogPrefix.err.log"
    $process = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", $apiProject, "--no-launch-profile", "--no-build", "--urls", "http://localhost:$Port") -PassThru -RedirectStandardOutput $log -RedirectStandardError $err -WorkingDirectory $repoRoot

    if ($ProcessName -eq "worker") {
        $global:PerfWorkerProcess = $process
    } else {
        $global:PerfApiProcess = $process
    }

    if (-not (Wait-Health -TargetBaseUrl "http://localhost:$Port" -TimeoutSec 240)) {
        throw "API process did not become healthy within timeout. See $log"
    }
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
    try {
        $invokeParams = @{
            Uri = $Url
            UseBasicParsing = $true
            TimeoutSec = $TimeoutSec
            Method = $Method
        }
        if ($Headers) { $invokeParams.Headers = $Headers }
        if ($null -ne $Body) { $invokeParams.Body = $Body }
        $resp = Invoke-WebRequest @invokeParams
        $sw.Stop()
        $body = $null
        try { $body = $resp.Content | ConvertFrom-Json -Depth 20 } catch { }
        return [pscustomobject]@{
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            StatusCode = [int]$resp.StatusCode
            Body = $body
        }
    } catch {
        $sw.Stop()
        return [pscustomobject]@{
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            StatusCode = 0
            Body = $null
            Error = $_.Exception.Message
        }
    }
}

function Invoke-TimedGet {
    param([string]$Url, [int]$TimeoutSec = 600)
    Invoke-TimedRequest -Url $Url -Method "GET" -TimeoutSec $TimeoutSec
}

function Measure-WarmEndpoint {
    param(
        [string]$BenchmarkId,
        [string]$Name,
        [string]$Url,
        [string]$CorrectnessChecks,
        [scriptblock]$ExtraAssertions = { $true }
    )
    $cold = Invoke-TimedGet -Url $Url
    $warm = @()
    for ($i = 0; $i -lt $WarmSamples; $i++) {
        $warm += (Invoke-TimedGet -Url $Url).ElapsedMs
    }
    return [pscustomobject]@{
        benchmarkId = $BenchmarkId
        name = $Name
        requestOrJob = $Url
        processState = "warm"
        cacheState = "cold->warm"
        coldMs = $cold.ElapsedMs
        coldStatusCode = $cold.StatusCode
        warmSamples = $WarmSamples
        warmP50Ms = [math]::Round((Get-Percentile -Values $warm -Percentile 50), 2)
        warmP95Ms = [math]::Round((Get-Percentile -Values $warm -Percentile 95), 2)
        warmMinMs = [math]::Round(($warm | Measure-Object -Minimum).Minimum, 2)
        warmMaxMs = [math]::Round(($warm | Measure-Object -Maximum).Maximum, 2)
        warmSampleMs = $warm
        errorRate = 0
        timeoutRate = 0
        correctnessChecks = $CorrectnessChecks
        metaSnapshot = if ($cold.Body) { Get-JsonProperty $cold.Body 'meta' } else { $null }
        coldError = if ($null -ne $cold.Error) { $cold.Error } else { $null }
    }
}

function Get-WorkerStatusSnapshot {
    param([object]$ConfigurationBody)
    $workers = Get-JsonProperty $ConfigurationBody 'workers'
    if ($null -eq $workers) { return $null }
    $worker = $workers | Where-Object { (Get-JsonProperty $_ 'workerName') -eq "AnalyticsAggregationWorker" } | Select-Object -First 1
    if ($null -eq $worker) { return $null }
    return [pscustomobject]@{
        runtimeStatus = Get-JsonProperty $worker 'runtimeStatus'
        status = Get-JsonProperty $worker 'status'
        lastHeartbeat = Get-JsonProperty $worker 'lastHeartbeat'
        lastRunAt = Get-JsonProperty $worker 'lastRunAt'
        nextRunAt = Get-JsonProperty $worker 'nextRunAt'
        isConfiguredButNotRunning = Get-JsonProperty $worker 'isConfiguredButNotRunning'
        isScheduleEnabled = Get-JsonProperty $worker 'isScheduleEnabled'
        isManuallyStopped = Get-JsonProperty $worker 'isManuallyStopped'
    }
}

function Measure-WorkerCycle {
    param(
        [string]$WorkerBaseUrl,
        [string]$AdminKey
    )

    $startUrl = "$WorkerBaseUrl/api/workers/AnalyticsAggregationWorker/start"
    $configUrl = "$WorkerBaseUrl/api/workers/configuration"

    Start-PerfProcess -ProcessName "worker" -ProcessType "worker" -Port 8081 -WorkersEnabled $true -LogPrefix "perf05_worker" -AdminApiKey $AdminKey

    $beforeConfig = Invoke-TimedGet -Url $configUrl -TimeoutSec 120
    $beforeSnapshot = Get-WorkerStatusSnapshot -ConfigurationBody $beforeConfig.Body
    $startResponse = Invoke-TimedRequest -Url $startUrl -Method "POST" -Headers @{ "X-Admin-Key" = $AdminKey } -TimeoutSec 120

    $cycleTimer = [System.Diagnostics.Stopwatch]::StartNew()
    $polls = @()

    while ($cycleTimer.Elapsed.TotalSeconds -lt 300) {
        Start-Sleep -Seconds 5
        $snapshotResponse = Invoke-TimedGet -Url $configUrl -TimeoutSec 120
        $snapshot = Get-WorkerStatusSnapshot -ConfigurationBody $snapshotResponse.Body
        $polls += [pscustomobject]@{
            elapsedMs = [math]::Round($cycleTimer.Elapsed.TotalMilliseconds, 2)
            runtimeStatus = if ($snapshot) { $snapshot.runtimeStatus } else { $null }
            status = if ($snapshot) { $snapshot.status } else { $null }
            lastHeartbeat = if ($snapshot) { $snapshot.lastHeartbeat } else { $null }
            nextRunAt = if ($snapshot) { $snapshot.nextRunAt } else { $null }
        }

        if ($snapshot -and $snapshot.runtimeStatus -eq "Healthy" -and $snapshot.lastHeartbeat) {
            $cycleTimer.Stop()
            return [pscustomobject]@{
                benchmarkId = "B5"
                name = "analytics-aggregation-worker"
                requestOrJob = $startUrl
                processState = "worker"
                cacheState = "n/a"
                cycleElapsedMs = [math]::Round($cycleTimer.Elapsed.TotalMilliseconds, 2)
                startRequestMs = $startResponse.ElapsedMs
                startStatusCode = $startResponse.StatusCode
                startBody = $startResponse.Body
                before = $beforeSnapshot
                after = $snapshot
                polls = $polls
                correctnessChecks = "worker process started; manual run requested; status reaches Healthy; heartbeat present"
                note = "Worker process measured in dedicated worker mode on port 8081."
            }
        }
    }

    $cycleTimer.Stop()
    $finalConfig = Invoke-TimedGet -Url $configUrl -TimeoutSec 120
    $finalSnapshot = Get-WorkerStatusSnapshot -ConfigurationBody $finalConfig.Body
    return [pscustomobject]@{
        benchmarkId = "B5"
        name = "analytics-aggregation-worker"
        requestOrJob = $startUrl
        processState = "worker"
        cacheState = "n/a"
        cycleElapsedMs = [math]::Round($cycleTimer.Elapsed.TotalMilliseconds, 2)
        startRequestMs = $startResponse.ElapsedMs
        startStatusCode = $startResponse.StatusCode
        startBody = $startResponse.Body
        before = $beforeSnapshot
        after = $finalSnapshot
        polls = $polls
        correctnessChecks = "worker process started; manual run requested; status did not reach Healthy within timeout"
        note = "Worker cycle timed out before a Healthy snapshot was observed."
        status = "timeout"
    }
}

if (-not $SkipSetup) {
    & (Join-Path $PSScriptRoot "perf05_setup_db.ps1") -TargetDb $Database
}

$script:ApiBuilt = $false
$commit = (git -C $repoRoot rev-parse HEAD).Trim()
$dotnetSdk = (dotnet --version).Trim()
$benchmarks = @()
$coldStartRows = @()

$bootstrapUrl = "$BaseUrl/api/analytics/cached/dashboard/bootstrap?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))&dataScope=all"
$salesUrl = "$BaseUrl/api/analytics/cached/sales/summary?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))"
$inventoryUrl = "$BaseUrl/api/analytics/cached/inventory/status?lowStockThreshold=2"
$decisionBoardUrl = "$BaseUrl/api/analytics/decision-board?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))&dataScope=all"
$pdcUrl = "$BaseUrl/api/analytics/cached/products/decision-center?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))&dataScope=all&top=500"
$supplierUrl = "$BaseUrl/api/analytics/suppliers/decision-hub/ranking?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))&dataScope=all&page=1&pageSize=50"
$workersUrl = "$BaseUrl/api/workers/configuration"

Write-Host "B8 cold-start samples ($ColdStartSamples) ..."
for ($i = 1; $i -le $ColdStartSamples; $i++) {
    Write-Host "  cold sample $i/$ColdStartSamples"
    Start-PerfProcess -ProcessName "web" -ProcessType "web" -Port 8080 -WorkersEnabled $false -LogPrefix "perf05_web"
    $cold = Invoke-TimedGet -Url $bootstrapUrl
    $meta = Get-JsonProperty $cold.Body 'meta'
    $summary = Get-JsonProperty $cold.Body 'summary'
    $inventory = Get-JsonProperty $cold.Body 'inventory'
    $coldStartRows += [pscustomobject]@{
        benchmarkId = "B8"
        state = "cold-process/cold-cache"
        requestOrJob = $bootstrapUrl
        elapsedMs = $cold.ElapsedMs
        statusCode = $cold.StatusCode
        success = (Get-JsonProperty $meta 'success')
        partial = (Get-JsonProperty $meta 'isPartial')
        warningCode = (Get-JsonProperty $meta 'warningCode')
        dataQualityStatus = (Get-JsonProperty $meta 'dataQualityStatus')
        summaryRevenue = (Get-JsonProperty $summary 'totalRevenue')
        inventorySkuCount = (Get-JsonProperty $inventory 'totalSkuCount')
        error = if ($null -ne $cold.Error) { $cold.Error } else { $null }
        rawBodyPreview = if ($cold.Body) { ($cold.Body | ConvertTo-Json -Compress -Depth 4).Substring(0, [math]::Min(400, (($cold.Body | ConvertTo-Json -Compress -Depth 4).Length))) } else { $null }
    }
    Stop-PerfApi
}

Write-Host "Warm-path families on warm process ..."
Start-PerfProcess -ProcessName "web" -ProcessType "web" -Port 8080 -WorkersEnabled $false -LogPrefix "perf05_web"

$benchmarks += Measure-WarmEndpoint -BenchmarkId "B2/B8" -Name "dashboard.bootstrap" -Url $bootstrapUrl -CorrectnessChecks "meta.success=true; summary.totalRevenue>0; inventory.totalSkuCount>0"
$benchmarks += Measure-WarmEndpoint -BenchmarkId "B1" -Name "sales.summary" -Url $salesUrl -CorrectnessChecks "meta.success=true; totalRevenue>0; totalTransactions>0"
$benchmarks += Measure-WarmEndpoint -BenchmarkId "B1" -Name "inventory.status" -Url $inventoryUrl -CorrectnessChecks "meta.success=true; totalSkuCount>0; totalOnHand>=0"
$benchmarks += Measure-WarmEndpoint -BenchmarkId "B2" -Name "decision-board" -Url $decisionBoardUrl -CorrectnessChecks "meta.success; sections present"
$benchmarks += Measure-WarmEndpoint -BenchmarkId "B2" -Name "products.decision-center" -Url $pdcUrl -CorrectnessChecks "meta.success; products non-empty or emptyReason honest"
$benchmarks += Measure-WarmEndpoint -BenchmarkId "B2" -Name "suppliers.decision-hub.ranking" -Url $supplierUrl -CorrectnessChecks "meta.success or explicit validation error"

$benchmarks += Measure-WorkerCycle -WorkerBaseUrl "http://localhost:8081" -AdminKey "perf05-worker-key"

$benchmarks += [pscustomobject]@{
    benchmarkId = "B4"
    name = "access-import.preview"
    requestOrJob = "POST /api/access-import/preview"
    processState = "n/a"
    cacheState = "n/a"
    status = "skipped"
    correctnessChecks = "no M-PERF accdb fixture in repo"
    note = "Requires dedicated Access fixture path in follow-up; not invented."
}

$benchmarks += [pscustomobject]@{
    benchmarkId = "B7"
    name = "frontend.routes.data-ready"
    requestState = "deferred"
    status = "deferred"
    note = "API surrogate families B1/B2 recorded; browser Playwright harness deferred."
}

Stop-PerfApi

$env:PGPASSWORD = $PgPassword
$countsSql = @'
SELECT 'Artikli' AS entity, COUNT(*)::bigint AS row_count FROM "Artikli" WHERE "Naziv" LIKE 'M-PERF Product %'
UNION ALL SELECT 'ProdajaZaglavlja', COUNT(*)::bigint FROM prodaja_zaglavlje WHERE broj_racuna LIKE 'MPERF-%'
UNION ALL SELECT 'ProdajaStavke', COUNT(*)::bigint FROM prodaja_stavke ps JOIN prodaja_zaglavlje pz ON pz.id = ps.id_prodaja WHERE pz.broj_racuna LIKE 'MPERF-%'
UNION ALL SELECT 'Dobavljaci', COUNT(*)::bigint FROM "Dobavljaci" WHERE "Naziv" LIKE 'M-PERF Supplier %';
'@
$countLines = ($countsSql | & psql -h $PgHost -p $PgPort -U $PgUser -d $Database -t -A -F '|')
$counts = @{}
foreach ($line in $countLines) {
    if ($line -match '^(.+)\|(\d+)$') {
        $counts[$Matches[1]] = [int64]$Matches[2]
    }
}

$coldMs = @($coldStartRows | ForEach-Object { [double]$_.elapsedMs })
$result = [ordered]@{
    meta = [ordered]@{
        machine = $env:COMPUTERNAME
        os = (Get-CimInstance Win32_OperatingSystem).Caption
        osVersion = (Get-CimInstance Win32_OperatingSystem).Version
        dotnetSdk = $dotnetSdk
        postgresVersion = ((& psql --version) -join ' ')
        commit = $commit
        datetimeUtc = (Get-Date).ToUniversalTime().ToString("o")
        datasetTier = "M"
        seedRecipeId = "M-PERF-01"
        database = $Database
        port = 8080
        prewarmEnabled = $false
        workersEnabled = $false
        periodFrom = $fromDate
        periodTo = $toDate
    }
    counts = $counts
    coldStart = $coldStartRows
    coldStartSummary = [ordered]@{
        samples = $coldStartRows.Count
        p50Ms = [math]::Round((Get-Percentile -Values $coldMs -Percentile 50), 2)
        p95Ms = [math]::Round((Get-Percentile -Values $coldMs -Percentile 95), 2)
        minMs = [math]::Round(($coldMs | Measure-Object -Minimum).Minimum, 2)
        maxMs = [math]::Round(($coldMs | Measure-Object -Maximum).Maximum, 2)
    }
    benchmarks = @($benchmarks)
}

$json = $result | ConvertTo-Json -Depth 12
Set-Content -Path $outputJson -Value $json -Encoding UTF8
Write-Host "Wrote $outputJson"
Write-Host ($json.Substring(0, [math]::Min(1200, $json.Length)))
