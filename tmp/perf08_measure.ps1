param(
    [string]$ApiBaseUrl = "http://127.0.0.1:8080",
    [string]$FrontendBaseUrl = "http://127.0.0.1:5174",
    [string]$PgHost = "127.0.0.1",
    [int]$PgPort = 5432,
    [string]$PgUser = "postgres",
    [string]$PgPassword = "postgres",
    [string]$Database = "trendplus_perf_m",
    [int]$BackendColdSamples = 5,
    [int]$FrontendColdSamples = 3,
    [switch]$SkipSetup,
    [switch]$SkipFrontendBuild,
    [switch]$FrontendOnly
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "Api\Api.csproj"
$clientRoot = Join-Path $repoRoot "Klijent\clientapp"
$outputJson = Join-Path $repoRoot ".ai\runs\2026-08-12-PERF08-raw.json"
$fromDate = "2026-02-13T06:00:00.0000000Z"
$toDate = "2026-08-12T06:00:00.0000000Z"
$connectionString = "Host=$PgHost;Port=$PgPort;Database=$Database;Username=$PgUser;Password=$PgPassword"
$bootstrapPath = "/api/analytics/cached/dashboard/bootstrap?fromDate=$([uri]::EscapeDataString($fromDate))&toDate=$([uri]::EscapeDataString($toDate))&dataScope=all"
$bootstrapUrl = "$ApiBaseUrl$bootstrapPath"
$global:PerfApiProcess = $null
$global:PerfPreviewProcess = $null

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

function Invoke-TimedRequest {
    param([string]$Url, [int]$TimeoutSec = 600)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $resp = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec
        $sw.Stop()
        return [pscustomobject]@{
            StatusCode = [int]$resp.StatusCode
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Raw = $resp.Content
        }
    } catch {
        $sw.Stop()
        return [pscustomobject]@{
            StatusCode = 0
            ElapsedMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 2)
            Raw = $null
            Error = $_.Exception.Message
        }
    }
}

function Stop-PerfApi {
    if ($global:PerfApiProcess -and -not $global:PerfApiProcess.HasExited) {
        Stop-Process -Id $global:PerfApiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Get-NetTCPConnection -LocalPort 8080 -State Listen -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 2
}

function Stop-PerfPreview {
    if ($global:PerfPreviewProcess -and -not $global:PerfPreviewProcess.HasExited) {
        Stop-Process -Id $global:PerfPreviewProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Get-NetTCPConnection -LocalPort 5174 -State Listen -ErrorAction SilentlyContinue |
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

    $apiLog = Join-Path $env:TEMP "perf08_api.log"
    $apiErr = Join-Path $env:TEMP "perf08_api.err.log"
    $processStartedAt = Get-Date
    $global:PerfApiProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--project", $apiProject, "--no-launch-profile", "--no-build", "--urls", "http://localhost:8080") `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput $apiLog -RedirectStandardError $apiErr -WorkingDirectory $repoRoot

    $healthReadyMs = $null
    $deadline = (Get-Date).AddSeconds(240)
    while ((Get-Date) -lt $deadline) {
        try {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $resp = Invoke-WebRequest -Uri "$ApiBaseUrl/health" -UseBasicParsing -TimeoutSec 5
            $sw.Stop()
            if ($resp.StatusCode -eq 200) {
                $healthReadyMs = [math]::Round(((Get-Date) - $processStartedAt).TotalMilliseconds, 2)
                break
            }
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    if ($null -eq $healthReadyMs) { throw "API health probe failed" }

    return [pscustomobject]@{
        ProcessStartedAtUtc = $processStartedAt.ToUniversalTime().ToString("o")
        HealthReadyMs = $healthReadyMs
    }
}

function Start-PerfPreview {
    Stop-PerfPreview
    $previewLog = Join-Path $env:TEMP "perf08_preview.log"
    $previewErr = Join-Path $env:TEMP "perf08_preview.err.log"
    $processStartedAt = Get-Date
    $global:PerfPreviewProcess = Start-Process -FilePath "npm.cmd" `
        -ArgumentList @("run", "dev", "--", "--host", "127.0.0.1", "--port", "5174", "--strictPort") `
        -PassThru -WindowStyle Hidden -RedirectStandardOutput $previewLog -RedirectStandardError $previewErr -WorkingDirectory $clientRoot

    $previewReadyMs = $null
    $deadline = (Get-Date).AddSeconds(180)
    while ((Get-Date) -lt $deadline) {
        try {
            $resp = Invoke-WebRequest -Uri "$FrontendBaseUrl/" -UseBasicParsing -TimeoutSec 5
            if ($resp.StatusCode -eq 200) {
                $previewReadyMs = [math]::Round(((Get-Date) - $processStartedAt).TotalMilliseconds, 2)
                break
            }
        } catch {
            Start-Sleep -Seconds 1
        }
    }
    if ($null -eq $previewReadyMs) { throw "Vite dev server failed to start" }

    return [pscustomobject]@{
        ProcessStartedAtUtc = $processStartedAt.ToUniversalTime().ToString("o")
        PreviewReadyMs = $previewReadyMs
        mode = "vite-dev-proxy"
    }
}

function Get-JsonProperty {
    param($Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -ne $prop) { return $prop.Value }
    $prop = $Object.PSObject.Properties[$Name.ToLowerInvariant()]
    if ($null -ne $prop) { return $prop.Value }
    return $null
}

if (-not $SkipSetup) {
    & (Join-Path $PSScriptRoot "perf05_setup_db.ps1") -TargetDb $Database
}

$script:ApiBuilt = $false
$script:FrontendBuilt = $false
$backendCold = @()

Write-Host "Backend cold-start samples ($BackendColdSamples) ..."
if (-not $FrontendOnly) {
for ($i = 1; $i -le $BackendColdSamples; $i++) {
    Write-Host "  backend cold sample $i/$BackendColdSamples"
    $startup = Start-PerfApi
    $bootstrap = Invoke-TimedRequest -Url $bootstrapUrl
    $body = $null
    try { if ($bootstrap.Raw) { $body = $bootstrap.Raw | ConvertFrom-Json -Depth 20 } } catch {}
    $meta = Get-JsonProperty $body 'meta'
    if ($null -eq $meta) { $meta = Get-JsonProperty $body 'Meta' }
    $firstUsefulMs = [math]::Round($startup.HealthReadyMs + $bootstrap.ElapsedMs, 2)
    $backendCold += [pscustomobject]@{
        sample = $i
        path = "backend"
        processState = "cold"
        cacheState = "cold"
        healthReadyMs = $startup.HealthReadyMs
        firstAnalyticsRequestMs = $bootstrap.ElapsedMs
        firstUsefulAnalyticsMs = $firstUsefulMs
        statusCode = $bootstrap.StatusCode
        success = Get-JsonProperty $meta 'success'
        isPartial = Get-JsonProperty $meta 'isPartial'
        warningCode = Get-JsonProperty $meta 'warningCode'
    }
    Stop-PerfApi
}
}

Write-Host "Backend warm marker ..."
if (-not $FrontendOnly) {
$startupWarm = Start-PerfApi
$warmBootstrap = Invoke-TimedRequest -Url $bootstrapUrl
$warmSecond = Invoke-TimedRequest -Url $bootstrapUrl
Stop-PerfApi
} else {
    $prior = Get-Content (Join-Path $repoRoot ".ai\runs\2026-08-12-PERF08-raw.json") -Raw | ConvertFrom-Json
    $backendCold = @($prior.backendColdStart)
    $warmBootstrap = [pscustomobject]@{ ElapsedMs = [double]$prior.backendWarmMarker.firstBootstrapMs; StatusCode = 200 }
    $warmSecond = [pscustomobject]@{ ElapsedMs = [double]$prior.backendWarmMarker.secondBootstrapMs; StatusCode = 200 }
}

Write-Host "Frontend cold-start samples ($FrontendColdSamples) with warm API ..."
$startupWarmApi = Start-PerfApi
$frontendCold = @()
for ($i = 1; $i -le $FrontendColdSamples; $i++) {
    Write-Host "  frontend cold sample $i/$FrontendColdSamples"
    $previewStartup = Start-PerfPreview
    $index = Invoke-TimedRequest -Url "$FrontendBaseUrl/analytics"
    $entryAssetMs = $null
    if ($index.Raw -match 'src="(?<src>/assets/[^"]+\.js)"') {
        $asset = Invoke-TimedRequest -Url "$FrontendBaseUrl$($Matches.src)"
        $entryAssetMs = $asset.ElapsedMs
    }

    Push-Location $clientRoot
    try {
        $renderScript = Join-Path $clientRoot "scripts\perf08_frontend_render.mjs"
        $renderJson = & node $renderScript $FrontendBaseUrl "/analytics" 120000
    } finally {
        Pop-Location
    }
    $render = $renderJson | ConvertFrom-Json

    $frontendCold += [pscustomobject]@{
        sample = $i
        path = "frontend"
        processState = "cold-vite-dev"
        cacheState = "cold-shell-warm-api"
        previewReadyMs = $previewStartup.PreviewReadyMs
        previewMode = $previewStartup.mode
        indexHtmlMs = $index.ElapsedMs
        entryAssetMs = $entryAssetMs
        domContentLoadedMs = $render.domContentLoadedMs
        usefulRenderMs = $render.usefulRenderMs
        bootstrapStatus = $render.bootstrapStatus
        bootstrapMs = $render.bootstrapMs
        renderTimedOut = $render.renderTimedOut
        totalBrowserMs = $render.totalMs
    }
    Stop-PerfPreview
}
Stop-PerfApi

$backendFirstUseful = @($backendCold | ForEach-Object { [double]$_.firstUsefulAnalyticsMs })
$frontendUseful = @($frontendCold | ForEach-Object { [double]$_.usefulRenderMs })

$result = [ordered]@{
    meta = [ordered]@{
        commit = (git -C $repoRoot rev-parse HEAD).Trim()
        datetimeUtc = (Get-Date).ToUniversalTime().ToString("o")
        datasetTier = "M"
        seedRecipeId = "M-PERF-01"
        database = $Database
        apiBaseUrl = $ApiBaseUrl
        frontendBaseUrl = $FrontendBaseUrl
        prewarmEnabled = $false
        workersEnabled = $false
        periodFrom = $fromDate
        periodTo = $toDate
    }
    backendColdStart = @($backendCold)
    backendColdSummary = [ordered]@{
        samples = $backendCold.Count
        healthReadyP50Ms = [math]::Round((Get-Percentile -Values @($backendCold | ForEach-Object { [double]$_.healthReadyMs }) -Percentile 50), 2)
        firstUsefulAnalyticsP50Ms = [math]::Round((Get-Percentile -Values $backendFirstUseful -Percentile 50), 2)
        firstUsefulAnalyticsP95Ms = [math]::Round((Get-Percentile -Values $backendFirstUseful -Percentile 95), 2)
    }
    backendWarmMarker = [ordered]@{
        processState = "warm"
        cacheState = "cold-then-warm"
        firstBootstrapMs = $warmBootstrap.ElapsedMs
        secondBootstrapMs = $warmSecond.ElapsedMs
        statusCode = $warmSecond.StatusCode
    }
    frontendColdStart = @($frontendCold)
    frontendColdSummary = [ordered]@{
        samples = $frontendCold.Count
        previewReadyP50Ms = [math]::Round((Get-Percentile -Values @($frontendCold | ForEach-Object { [double]$_.previewReadyMs }) -Percentile 50), 2)
        usefulRenderP50Ms = [math]::Round((Get-Percentile -Values $frontendUseful -Percentile 50), 2)
        usefulRenderP95Ms = [math]::Round((Get-Percentile -Values $frontendUseful -Percentile 95), 2)
    }
}

[System.IO.File]::WriteAllText($outputJson, ($result | ConvertTo-Json -Depth 10), [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $outputJson"
