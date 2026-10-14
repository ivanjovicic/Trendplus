[CmdletBinding()]
param(
    [string]$ReportPath = "TestResults/rq647-analytics-endpoint-benchmark.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$resolvedReportPath = if ([IO.Path]::IsPathRooted($ReportPath)) {
    [IO.Path]::GetFullPath($ReportPath)
} else {
    [IO.Path]::GetFullPath((Join-Path $repoRoot $ReportPath))
}
$previousRequired = $env:TRENDPLUS_REQUIRE_POSTGRES_TESTS
$previousReport = $env:TRENDPLUS_BENCHMARK_REPORT_PATH
try {
    $env:TRENDPLUS_REQUIRE_POSTGRES_TESTS = "true"
    $env:TRENDPLUS_BENCHMARK_REPORT_PATH = $resolvedReportPath
    dotnet test Api.Tests/Api.Tests.csproj `
        --configuration Release `
        --filter "FullyQualifiedName~AnalyticsEndpointBenchmarkPostgresTests" `
        --logger "trx;LogFileName=rq647-analytics-benchmark.trx"
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
    if (-not (Test-Path -LiteralPath $resolvedReportPath)) {
        throw "Benchmark completed without creating report: $resolvedReportPath"
    }
    Get-Content -LiteralPath $resolvedReportPath -Raw
}
finally {
    if ($null -eq $previousRequired) {
        Remove-Item Env:TRENDPLUS_REQUIRE_POSTGRES_TESTS -ErrorAction SilentlyContinue
    } else {
        $env:TRENDPLUS_REQUIRE_POSTGRES_TESTS = $previousRequired
    }
    if ($null -eq $previousReport) {
        Remove-Item Env:TRENDPLUS_BENCHMARK_REPORT_PATH -ErrorAction SilentlyContinue
    } else {
        $env:TRENDPLUS_BENCHMARK_REPORT_PATH = $previousReport
    }
}
