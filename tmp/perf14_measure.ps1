param(
    [string]$WebApiBaseUrl = "http://127.0.0.1:8080",
    [string]$WorkerApiBaseUrl = "http://127.0.0.1:8081",
    [string]$PgHost = "127.0.0.1",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [string]$PgPassword = "postgres",
    [string]$Database = "trendplus_perf_m",
    [string]$AdminKey = "perf14-import-key",
    [string]$ImportFixturePath = "C:\Users\Ivan\Downloads\Trend plus.mdb",
    [int]$ProbeBurst = 3,
    [int]$PollSeconds = 2,
    [switch]$SkipSetup
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "Api\Api.csproj"
$outputJson = Join-Path $repoRoot ".ai\runs\2026-08-12-PERF14-raw.json"
$outputEvidence = Join-Path $repoRoot ".ai\runs\2026-08-12-PERF14-evidence.md"
$connectionString = "Host=$PgHost;Port=$PgPort;Database=$Database;Username=$PgUser;Password=$PgPassword"
$global:PerfProcesses = @()
$env:PGPASSWORD = $PgPassword

function Stop-PerfApi {
    param([int[]]$Ports)

    foreach ($port in $Ports) {
        Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
            ForEach-Object {
                try {
                    Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue
                } catch {}
            }
    }

    foreach ($proc in @($global:PerfProcesses)) {
        if ($null -ne $proc -and -not $proc.HasExited) {
            try {
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            } catch {}
        }
    }

    $global:PerfProcesses = @()
    Start-Sleep -Seconds 2
}

function Start-PerfApi {
    param(
        [int]$Port,
        [ValidateSet("web", "worker")]
        [string]$ProcessType = "web",
        [bool]$WorkersEnabled = $false,
        [switch]$SkipHealthProbe
    )

    Stop-PerfApi -Ports @($Port)
    if (-not $script:ApiBuilt) {
        dotnet build $apiProject -v q | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
        $script:ApiBuilt = $true
    }

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:DOTNET_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "http://localhost:$Port"
    $env:ConnectionStrings__DefaultConnection = $connectionString
    $env:ConnectionStrings__AnalyticsConnection = $connectionString
    $env:ConnectionStrings__OpenProductTrainingConnection = $connectionString
    $env:Workers__Enabled = $WorkersEnabled.ToString().ToLowerInvariant()
    $env:AnalyticsPrewarm__Enabled = "false"
    $env:ADMIN_API_KEY = $AdminKey
    $env:Admin__ApiKey = $AdminKey
    $env:PROCESS_TYPE = $ProcessType

    $apiLog = Join-Path $env:TEMP "perf14_$ProcessType`_$Port.log"
    $apiErr = Join-Path $env:TEMP "perf14_$ProcessType`_$Port.err.log"
    $proc = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--project", $apiProject, "--no-launch-profile", "--no-build", "--urls", "http://localhost:$Port") `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput $apiLog -RedirectStandardError $apiErr -WorkingDirectory $repoRoot
    $global:PerfProcesses += $proc

    if ($SkipHealthProbe) {
        Start-Sleep -Seconds 15
        return
    }

    $deadline = (Get-Date).AddSeconds(240)
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -Uri "http://localhost:$Port/health" -UseBasicParsing -TimeoutSec 5
            if ($resp.StatusCode -eq 200) { return }
        } catch {
            Start-Sleep -Seconds 2
        }
    }

    throw "API health probe failed for port $Port. See $apiLog / $apiErr"
}

function Get-Json {
    param([string]$Url, [int]$TimeoutSec = 120)

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec
        $sw.Stop()
        $body = $null
        try { $body = $resp.Content | ConvertFrom-Json } catch {}
        return [pscustomobject]@{
            Url = $Url
            StatusCode = [int]$resp.StatusCode
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Body = $body
            Error = $null
        }
    } catch {
        $sw.Stop()
        $status = 0
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        return [pscustomobject]@{
            Url = $Url
            StatusCode = $status
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

function Find-Worker {
    param($ConfigBody, [string]$WorkerName)
    $workers = Get-Prop $ConfigBody 'workers'
    if ($null -eq $workers) { return $null }
    return @($workers) | Where-Object {
        (Get-Prop $_ 'workerName') -eq $WorkerName
    } | Select-Object -First 1
}

function Invoke-ImportRun {
    param(
        [string]$BaseUrl,
        [string]$FilePath,
        [string]$AdminKey
    )

    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $bodyFile = [System.IO.Path]::GetTempFileName()
        $args = @(
            "-sS",
            "--connect-timeout", "30",
            "--max-time", "1800",
            "-o", $bodyFile,
            "-w", "%{http_code}",
            "-H", "X-Admin-Key: $AdminKey",
            "-F", "file=@$FilePath;type=application/octet-stream",
            "-F", "includeAnalytics=true",
            "-F", "overwriteExisting=true",
            "$BaseUrl/api/access-import/run"
        )

        $statusText = & curl.exe @args
        $exitCode = $LASTEXITCODE
        $sw.Stop()
        $rawBody = ""
        if (Test-Path $bodyFile) {
            $rawBody = Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue
            Remove-Item -Path $bodyFile -Force -ErrorAction SilentlyContinue
        }
        $body = $null
        if (-not [string]::IsNullOrWhiteSpace($rawBody)) {
            try { $body = $rawBody | ConvertFrom-Json } catch { }
        }

        $parsedStatus = 0
        [void][int]::TryParse(($statusText | Out-String).Trim(), [ref]$parsedStatus)

        return [pscustomobject]@{
            StatusCode = $parsedStatus
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Body = $body
            RawBody = $rawBody
            Error = if ($exitCode -eq 0) { $null } else { "curl exit code $exitCode" }
        }
    } catch {
        return [pscustomobject]@{
            StatusCode = 0
            ElapsedMs = if ($sw) { [math]::Round($sw.Elapsed.TotalMilliseconds, 2) } else { 0 }
            Body = $null
            RawBody = $null
            Error = $_.Exception.Message
        }
    }
}

function Invoke-AnalyticsProbe {
    param(
        [string]$BaseUrl,
        [string]$Path = "/api/analytics/cached/dashboard/bootstrap?dataScope=all",
        [int]$TimeoutSec = 120
    )

    return Get-Json -Url ($BaseUrl.TrimEnd("/") + $Path) -TimeoutSec $TimeoutSec
}

function Is-TerminalStatus {
    param([string]$Status)
    return $Status -in @("completed", "failed", "cancelled", "interrupted")
}

if (-not $SkipSetup) {
    & (Join-Path $PSScriptRoot "perf05_setup_db.ps1") -TargetDb $Database
}

$script:ApiBuilt = $false

Write-Host "Starting web API on 8080..."
Start-PerfApi -Port 8080 -ProcessType web -WorkersEnabled $false

Write-Host "Starting worker API on 8081..."
Start-PerfApi -Port 8081 -ProcessType worker -WorkersEnabled $true -SkipHealthProbe

if (-not (Test-Path $ImportFixturePath)) {
    throw "Import fixture not found: $ImportFixturePath"
}

$fixtureInfo = Get-Item $ImportFixturePath
$stagedFixture = Join-Path $env:TEMP ("perf14-import" + $fixtureInfo.Extension)
Copy-Item -Path $ImportFixturePath -Destination $stagedFixture -Force
$workerConfigBefore = Get-Json -Url "$WorkerApiBaseUrl/api/workers/configuration"
$accessWorkerBefore = Find-Worker -ConfigBody $workerConfigBefore.Body -WorkerName "AccessImportBackgroundWorker"

Write-Host "Submitting Access import run..."
$importRun = Invoke-ImportRun -BaseUrl $WebApiBaseUrl -FilePath $stagedFixture -AdminKey $AdminKey
if ($importRun.StatusCode -ne 202 -or $null -eq $importRun.Body) {
    Stop-PerfApi -Ports @(8080, 8081)
    $result = [ordered]@{
        meta = [ordered]@{
            packId = "PERF14-G10-import-overlap-01"
            commit = (git -C $repoRoot rev-parse HEAD).Trim()
            datetimeUtc = (Get-Date).ToUniversalTime().ToString("o")
            milestone = "G10"
            deploymentMode = "dedicated"
            datasetTier = "M"
            seedRecipeId = "M-PERF-01"
            importFixturePath = $ImportFixturePath
            importFixtureSizeBytes = $fixtureInfo.Length
            stagedFixturePath = $stagedFixture
        }
        dimensions = [ordered]@{
            D6 = [ordered]@{
                status = "blocked"
                notes = "Access import run did not return 202 Accepted."
                importRunStatusCode = $importRun.StatusCode
                importRunError = $importRun.Error
                importRunBody = $importRun.RawBody
            }
            D8 = [ordered]@{ status = "n/a_dedicated"; notes = "shared_saas requires MT fixtures" }
        }
        correctnessChecks = [ordered]@{
            result = "pass"
            notes = "Honest blocker recorded without inventing overlap evidence."
        }
        residualRisks = @(
            "Import fixture or runtime mismatch",
            "single-host dedicated only",
            "D8 remains MT-owned"
        )
    }

    [System.IO.File]::WriteAllText($outputJson, ($result | ConvertTo-Json -Depth 16), [System.Text.UTF8Encoding]::new($false))
    Set-Content -Path $outputEvidence -Value @(
        "# PERF14 Evidence",
        "",
        "- Date: $((Get-Date).ToString('yyyy-MM-dd'))",
        "- Prompt: PERF14 - Unblock D6 import-overlap evidence",
        "- Pack: PERF14-G10-import-overlap-01",
        "- Milestone / mode: G10 / dedicated",
        "- Dataset: trendplus_perf_m (M-PERF-01)",
        "- Import fixture: $ImportFixturePath",
        "- Raw JSON: .ai/runs/2026-08-12-PERF14-raw.json",
        "",
        "## Result",
        "",
        "- **blocked** - Access import run did not return `202 Accepted`.",
        "- No overlap evidence was fabricated.",
        "",
        "## Files",
        "",
        "- tmp/perf14_measure.ps1",
        "- .ai/runs/2026-08-12-PERF14-raw.json",
        "- .ai/runs/2026-08-12-PERF14-evidence.md"
    ) -Encoding UTF8
    throw "PERF14 import run did not start. See evidence files."
}

$batchId = Get-Prop $importRun.Body 'batchId'
if ($null -eq $batchId) {
    $batchId = Get-Prop $importRun.Body 'BatchId'
}

$readProbes = @()
$workerSnapshots = @()
$batchSnapshots = @()
$analyticsDuringRunning = 0
$runningObserved = $false
$terminalStatus = $null
$terminalBatch = $null
for ($iteration = 1; $iteration -le 120; $iteration++) {
    $batch = Get-Json -Url "$WebApiBaseUrl/api/access-import/jobs/$batchId" -TimeoutSec 120
    $batchStatus = Get-Prop $batch.Body 'status'
    $batchSnapshots += [pscustomobject]@{
        iteration = $iteration
        elapsedMs = $batch.ElapsedMs
        statusCode = $batch.StatusCode
        status = $batchStatus
        completedAtUtc = Get-Prop $batch.Body 'completedAtUtc'
        startedAtUtc = Get-Prop $batch.Body 'startedAtUtc'
        progressPercent = Get-Prop $batch.Body 'progressPercent'
    }

    if ($batchStatus -eq "running") {
        $runningObserved = $true
    }

    for ($probe = 1; $probe -le $ProbeBurst; $probe++) {
        $analytics = Invoke-AnalyticsProbe -BaseUrl $WebApiBaseUrl
        $readProbes += [pscustomobject]@{
            iteration = $iteration
            probe = $probe
            elapsedMs = $analytics.ElapsedMs
            statusCode = $analytics.StatusCode
            success = ($analytics.StatusCode -eq 200 -and $null -ne $analytics.Body)
            successMeta = Get-Prop (Get-Prop $analytics.Body 'meta') 'success'
            partialMeta = Get-Prop (Get-Prop $analytics.Body 'meta') 'isPartial'
            dataQualityStatus = Get-Prop (Get-Prop $analytics.Body 'meta') 'dataQualityStatus'
            timestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        }
        if ($runningObserved -and $analytics.StatusCode -eq 200) {
            $analyticsDuringRunning++
        }
    }

    $workerConfig = Get-Json -Url "$WorkerApiBaseUrl/api/workers/configuration" -TimeoutSec 120
    $accessWorker = Find-Worker -ConfigBody $workerConfig.Body -WorkerName "AccessImportBackgroundWorker"
    $workerSnapshots += [pscustomobject]@{
        iteration = $iteration
        elapsedMs = $workerConfig.ElapsedMs
        statusCode = $workerConfig.StatusCode
        status = Get-Prop $accessWorker 'status'
        lastHeartbeat = Get-Prop $accessWorker 'lastHeartbeat'
        isRegisteredInCurrentProcess = Get-Prop $accessWorker 'isRegisteredInCurrentProcess'
        isConfiguredButNotRunning = Get-Prop $accessWorker 'isConfiguredButNotRunning'
    }

    if (Is-TerminalStatus $batchStatus) {
        $terminalStatus = $batchStatus
        $terminalBatch = $batch.Body
        break
    }

    Start-Sleep -Seconds $PollSeconds
}

if ($null -eq $terminalStatus) {
    $terminalStatus = "timeout"
}

Stop-PerfApi -Ports @(8080, 8081)

$d6Measured = ($runningObserved -and $analyticsDuringRunning -gt 0 -and $terminalStatus -ne "timeout")

$result = [ordered]@{
    meta = [ordered]@{
        packId = "PERF14-G10-import-overlap-01"
        commit = (git -C $repoRoot rev-parse HEAD).Trim()
        datetimeUtc = (Get-Date).ToUniversalTime().ToString("o")
        milestone = "G10"
        deploymentMode = "dedicated"
        datasetTier = "M"
        seedRecipeId = "M-PERF-01"
        importFixturePath = $ImportFixturePath
        importFixtureSizeBytes = $fixtureInfo.Length
        stagedFixturePath = $stagedFixture
        webApiBaseUrl = $WebApiBaseUrl
        workerApiBaseUrl = $WorkerApiBaseUrl
        batchId = $batchId
    }
    dimensions = [ordered]@{
        D1 = [ordered]@{ status = "cite_PERF11"; notes = "Observed envelope already measured" }
        D2 = [ordered]@{ status = "cite_PERF10"; notes = "Concurrent reads already measured" }
        D3 = [ordered]@{ status = "cite_PERF10"; notes = "Connection pressure already measured" }
        D4 = [ordered]@{ status = "cite_PERF12"; notes = "Worker concurrency already measured" }
        D5 = [ordered]@{ status = "cite_PERF13"; notes = "Cache footprint already measured" }
        D6 = [ordered]@{
            status = if ($d6Measured) { "measured" } else { "blocked" }
            importBatchId = $batchId
            importStatus = $terminalStatus
            runningObserved = $runningObserved
            analyticsProbeCount = $readProbes.Count
            analyticsProbeDuringRunningCount = $analyticsDuringRunning
            workerSnapshots = @($workerSnapshots)
            batchSnapshots = @($batchSnapshots)
            firstRunningObservedIteration = if ($runningObserved) { ($batchSnapshots | Where-Object { $_.status -eq 'running' } | Select-Object -First 1).iteration } else { $null }
            completedStatus = Get-Prop $terminalBatch 'status'
            completedAtUtc = Get-Prop $terminalBatch 'completedAtUtc'
            notes = if ($d6Measured) {
                "Observed analytics bootstrap probes succeeding while the import batch was running on the dedicated worker process."
            } elseif ($terminalStatus -eq "timeout") {
                "Import batch never reached a terminal state within the harness timeout."
            } else {
                "Import started, but the harness did not observe a running batch with concurrent analytics success."
            }
        }
        D7 = [ordered]@{ status = "n/a"; notes = "Not exercised in PERF14" }
        D8 = [ordered]@{ status = "n/a_dedicated"; notes = "shared_saas requires MT fixtures" }
    }
    correctnessChecks = [ordered]@{
        result = "pass"
        notes = "No fake overlap: measured status only if analytics probes overlapped a running import batch."
    }
    residualRisks = @(
        "single-host dedicated only",
        "D8 remains MT-owned",
        "overlap evidence depends on local worker process and fixture availability"
    )
}

$tmpOut = "$outputJson.tmp"
[System.IO.File]::WriteAllText($tmpOut, ($result | ConvertTo-Json -Depth 18), [System.Text.UTF8Encoding]::new($false))
Move-Item -Path $tmpOut -Destination $outputJson -Force

$evidenceLines = @(
    "# PERF14 Evidence",
    "",
    "- Date: $((Get-Date).ToString('yyyy-MM-dd'))",
    "- Prompt: PERF14 - Unblock D6 import-overlap evidence",
    "- Pack: PERF14-G10-import-overlap-01",
    "- Milestone / mode: G10 / dedicated",
    "- Dataset: trendplus_perf_m (M-PERF-01)",
    "- Import fixture: $ImportFixturePath",
    "- Staged fixture: $stagedFixture",
    "- Import fixture size: $($fixtureInfo.Length) bytes",
    "- Raw JSON: .ai/runs/2026-08-12-PERF14-raw.json",
    "",
    "## Method",
    "",
    "1. Start the web API on port 8080 and the worker process on port 8081.",
    "2. Submit POST /api/access-import/run with the fixture file and admin key.",
    "3. Poll GET /api/access-import/jobs/{batchId} until terminal.",
    "4. Probe GET /api/analytics/cached/dashboard/bootstrap?dataScope=all during the import window.",
    "5. Sample GET /api/workers/configuration from the worker process.",
    "",
    "## Dimension status",
    "",
    "| Id | Status | Result |",
    "|---|---|---|",
    "| D1 | cite_PERF11 | already measured |",
    "| D2 | cite_PERF10 | already measured |",
    "| D3 | cite_PERF10 | already measured |",
    "| D4 | cite_PERF12 | already measured |",
    "| D5 | cite_PERF13 | already measured |",
    ("| D6 import overlap | **{0}** | analytics probes during import: {1} |" -f ($(if ($d6Measured) { 'measured' } else { 'blocked' })), $analyticsDuringRunning),
    "| D7 | n/a | not exercised in this pack |",
    "| D8 | `n/a_dedicated` | MT-owned |",
    "",
    "## Interpretation",
    "",
    "1. D6 is only measured if analytics probes succeeded while the batch was in running status.",
    "2. The worker process on port 8081 kept the import job honest without inventing a fixture.",
    "3. If D6 remains blocked, the evidence stays explicit rather than silently deferred.",
    "",
    "## Files",
    "",
    "- tmp/perf14_measure.ps1",
    "- .ai/runs/2026-08-12-PERF14-raw.json",
    "- .ai/runs/2026-08-12-PERF14-evidence.md"
)

Remove-Item -Path $stagedFixture -Force -ErrorAction SilentlyContinue

[System.IO.File]::WriteAllText($outputEvidence, ($evidenceLines -join [Environment]::NewLine), [System.Text.UTF8Encoding]::new($false))

Write-Host "Wrote $outputJson"
Write-Host "Wrote $outputEvidence"
Write-Output ($result | ConvertTo-Json -Depth 18)
