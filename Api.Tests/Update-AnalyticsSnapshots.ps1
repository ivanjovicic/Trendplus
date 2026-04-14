param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$OutputDir = "$PSScriptRoot\Golden"
)

$ErrorActionPreference = 'Stop'

function Get-Json {
    param([string]$Url)
    return Invoke-RestMethod -Method Get -Uri $Url
}

function Write-JsonFile {
    param(
        [string]$Path,
        $Object
    )

    $json = $Object | ConvertTo-Json -Depth 20
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine)
}

function Get-SupplierSnapshot {
    param($Response)

    $suppliers = @(
        $Response.suppliers | ForEach-Object {
            [pscustomobject]@{
                dobavljacNaziv = $_.dobavljacNaziv
                ukupanPromet = [decimal]$_.ukupanPromet
                ukupnaKolicina = [int]$_.ukupnaKolicina
                sharePct = [Math]::Round([double]$_.sharePct, 2)
                isUnknown = ($_.dobavljacNaziv -eq 'Nepoznato')
            }
        }
    )

    [pscustomobject]@{
        suppliers = $suppliers
        totals = [pscustomobject]@{
            ukupanPromet = [decimal]$Response.totals.ukupanPromet
            ukupnaKolicina = [int]$Response.totals.ukupnaKolicina
            brojDobavljaca = [int]$Response.totals.brojDobavljaca
            recommendationSummary = [pscustomobject]@{
                increaseFocus = [int]$Response.totals.recommendationSummary.increaseFocus
                maintain = [int]$Response.totals.recommendationSummary.maintain
                review = [int]$Response.totals.recommendationSummary.review
                doNotTrust = [int]$Response.totals.recommendationSummary.doNotTrust
                insufficientData = [int]$Response.totals.recommendationSummary.insufficientData
            }
        }
        dataQuality = [pscustomobject]@{
            unknownSupplierRevenueSharePct = [Math]::Round([double]$Response.dataQuality.unknownSupplierRevenueSharePct, 2)
        }
    }
}

function Get-ShoeTypeSnapshot {
    param($Response)

    $shoeTypes = @(
        $Response.shoeTypes | ForEach-Object {
            [pscustomobject]@{
                tipObuceNaziv = $_.tipObuceNaziv
                ukupanPromet = [decimal]$_.ukupanPromet
                ukupnaKolicina = [int]$_.ukupnaKolicina
                sharePct = [Math]::Round([double]$_.sharePct, 2)
                isUnknown = ($_.tipObuceNaziv -eq 'Nepoznato')
            }
        }
    )

    [pscustomobject]@{
        shoeTypes = $shoeTypes
        totals = [pscustomobject]@{
            ukupanPromet = [decimal]$Response.totals.ukupanPromet
            ukupnaKolicina = [int]$Response.totals.ukupnaKolicina
            brojTipovaObuce = [int]$Response.totals.brojTipovaObuce
            recommendationSummary = [pscustomobject]@{
                increaseFocus = [int]$Response.totals.recommendationSummary.increaseFocus
                maintain = [int]$Response.totals.recommendationSummary.maintain
                review = [int]$Response.totals.recommendationSummary.review
                doNotTrust = [int]$Response.totals.recommendationSummary.doNotTrust
                insufficientData = [int]$Response.totals.recommendationSummary.insufficientData
            }
        }
        dataQuality = [pscustomobject]@{
            unknownTypeRevenueSharePct = [Math]::Round([double]$Response.dataQuality.unknownTypeRevenueSharePct, 2)
        }
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$supplierResponse = Get-Json "$BaseUrl/api/analytics/supplier-sales-stats?sezonaId=1"
Write-JsonFile -Path (Join-Path $OutputDir 'supplier-sales-stats.contract.json') -Object (Get-SupplierSnapshot $supplierResponse)

$shoeTypeResponse = Get-Json "$BaseUrl/api/analytics/shoe-type-sales-stats?fromDate=2026-06-01&toDate=2026-08-31"
Write-JsonFile -Path (Join-Path $OutputDir 'shoe-type-sales-stats.contract.json') -Object (Get-ShoeTypeSnapshot $shoeTypeResponse)