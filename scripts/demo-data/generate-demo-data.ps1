<#
Wrapper for the Python demo dataset generator.

This is a maintenance script, not an auto-seed path.
#>

[CmdletBinding()]
param(
    [string]$Python = "python",
    [string]$OutputRoot
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command $Python -ErrorAction SilentlyContinue)) {
    throw "Python executable '$Python' was not found in PATH."
}

$scriptPath = Join-Path $PSScriptRoot "generate-demo-data.py"
$args = @($scriptPath)
if ($OutputRoot) {
    $args += @("--output-root", $OutputRoot)
}

& $Python @args
if ($LASTEXITCODE -ne 0) {
    throw "Demo dataset generation failed with exit code $LASTEXITCODE."
}

