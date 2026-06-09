<#
Load the checked-in Trendplus demo dataset into a local Docker Postgres container.

Default behavior:
- operational tables -> trendplus
- analytics tables -> trendplus

If your local setup uses a split analytics database, override -AnalyticsDatabase.

Examples:
  .\scripts\demo-data\load-demo-data.ps1
  .\scripts\demo-data\load-demo-data.ps1 -AnalyticsDatabase analytics
#>

[CmdletBinding()]
param(
    [string]$ContainerName = "trendplus-postgres",
    [string]$User = "postgres",
    [string]$OperationalDatabase = "trendplus",
    [string]$AnalyticsDatabase = "trendplus",
    [string]$DemoDataRoot
)

$ErrorActionPreference = "Stop"

function Assert-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found in PATH."
    }
}

function Invoke-PsqlScript {
    param(
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string]$Sql
    )

    $Sql | docker exec -i $ContainerName psql -U $User -d $Database -v ON_ERROR_STOP=1 -f -
    if ($LASTEXITCODE -ne 0) {
        throw "psql load failed for database '$Database' (exit $LASTEXITCODE)."
    }
}

Assert-Command docker

if (-not (docker ps --filter "name=$ContainerName" --format "{{.Names}}")) {
    throw "Container '$ContainerName' is not running. Start the local Postgres container first."
}

if (-not $DemoDataRoot) {
    $DemoDataRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\seed\demo-data")).Path
}
else {
    $DemoDataRoot = (Resolve-Path $DemoDataRoot).Path
}

$containerPath = "/tmp/trendplus-demo-data"

docker exec $ContainerName sh -lc "rm -rf $containerPath && mkdir -p $containerPath"
if ($LASTEXITCODE -ne 0) {
    throw "Unable to prepare demo staging directory inside container."
}

docker cp "$DemoDataRoot\." "${ContainerName}:$containerPath"
if ($LASTEXITCODE -ne 0) {
    throw "Unable to copy demo dataset into container."
}

$operationalSql = @"
BEGIN;

TRUNCATE TABLE
    "prodaja_stavke",
    "prodaja_zaglavlje",
    "DnevnikPromena",
    "Artikli",
    "Dobavljaci",
    "TipoviObuce",
    "Sezone",
    "DataImportBatches"
RESTART IDENTITY CASCADE;

\copy "Dobavljaci" ("Id","Naziv","Adresa","Telefon","Napomena","DataOrigin") FROM '$containerPath/operational/Dobavljaci.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "TipoviObuce" ("Id","Naziv","DataOrigin") FROM '$containerPath/operational/TipoviObuce.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "Sezone" ("Id","Naziv","DatumOd","DatumDo","DataOrigin") FROM '$containerPath/operational/Sezone.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "Artikli" ("Id","PLU","Naziv","IDTipObuce","IDDobavljac","NabavnaCena","NabavnaCenaDin","PrvaProdajnaCena","ProdajnaCena","Velicina","Boja","Kolicina","MinimalnaKolicina","Komentar","IDObjekat","IDSezona","UpdatedAt","Kategorija","Pol","Materijal","DataOrigin","SourceTableKey","SourceRowId","SourceUpdatedAtUtc","SourceHash","SourceBatchId","ImagePath") FROM '$containerPath/operational/Artikli.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "prodaja_zaglavlje" ("id","broj_racuna","datum_prodaje","nacin_placanja","id_objekat","korisnik_ime","data_origin","source_table_key","source_row_id","source_updated_at_utc","source_hash","source_batch_id") FROM '$containerPath/operational/prodaja_zaglavlje.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "prodaja_stavke" ("id","id_prodaja","id_artikal","kolicina","cena","nabavna_cena","source_table_key","source_row_id","source_updated_at_utc","source_hash","source_batch_id") FROM '$containerPath/operational/prodaja_stavke.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "DnevnikPromena" ("Id","TipPromene","Datum","Iznos","BrojRacuna","DobavljacId","ArtikalId","StaraProdajnaCena","NovaProdajnaCena","Kolicina","IDObjekat","RedniBroj","Komentar","KorisnikIme","DataOrigin","SourceTableKey","SourceRowId","SourceUpdatedAtUtc","SourceHash","SourceBatchId") FROM '$containerPath/operational/DnevnikPromena.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "DataImportBatches" ("Id","SourceSystem","SourceFileName","SourceFilePath","SourceStorageKey","SourceStorageProvider","QueuedAtUtc","StartedAtUtc","CompletedAtUtc","LastHeartbeatUtc","Status","CurrentStep","CurrentTable","SummaryJson","ErrorMessage","ErrorDetailsJson","RequestedBy","ImportMode","ImportStrategy","IncludeAnalytics","OverwriteExisting","IncludeTemporaryTables","SkipInvalidForeignKeys","CancellationRequested","CancellationRequestedAtUtc","RetryCount","ProgressPercent","RowsRead","RowsAccepted","RowsWritten","IsIncremental","CursorSnapshot","CursorBeforeJson","CursorAfterJson","ProcessedRowCount","SkippedRowCount","RowsInserted","RowsUpdated","RowsUnchanged","RowsStaged","RowsSkippedStale","RowsRejected","ShadowMismatchCount","SourceFileHash","DurationSeconds","TotalImported","TotalUpdated","TotalErrors","DataOrigin") FROM '$containerPath/operational/DataImportBatches.csv' WITH (FORMAT csv, HEADER true, NULL '');

SELECT setval(pg_get_serial_sequence('"Dobavljaci"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Dobavljaci"), 1), true);
SELECT setval(pg_get_serial_sequence('"TipoviObuce"', 'Id'), COALESCE((SELECT MAX("Id") FROM "TipoviObuce"), 1), true);
SELECT setval(pg_get_serial_sequence('"Sezone"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Sezone"), 1), true);
SELECT setval(pg_get_serial_sequence('"Artikli"', 'Id'), COALESCE((SELECT MAX("Id") FROM "Artikli"), 1), true);
SELECT setval(pg_get_serial_sequence('"prodaja_zaglavlje"', 'id'), COALESCE((SELECT MAX("id") FROM "prodaja_zaglavlje"), 1), true);
SELECT setval(pg_get_serial_sequence('"prodaja_stavke"', 'id'), COALESCE((SELECT MAX("id") FROM "prodaja_stavke"), 1), true);
SELECT setval(pg_get_serial_sequence('"DnevnikPromena"', 'Id'), COALESCE((SELECT MAX("Id") FROM "DnevnikPromena"), 1), true);
SELECT setval(pg_get_serial_sequence('"DataImportBatches"', 'Id'), COALESCE((SELECT MAX("Id") FROM "DataImportBatches"), 1), true);

COMMIT;
"@

$analyticsSql = @"
BEGIN;

TRUNCATE TABLE
    "analytics_action_items",
    "analytics_refresh_runs",
    "InventoryMovementFacts",
    "InventoryRecommendations",
    "SalesLineFacts",
    "SalesFacts",
    "ProductsDim",
    "SuppliersDim",
    "StoresDim",
    "FootwearTypesDim",
    "SeasonsDim"
RESTART IDENTITY CASCADE;

\copy "StoresDim" ("StoreKey","StoreId","StoreName","City","Region","Telefon","Menedzer","DataOrigin") FROM '$containerPath/analytics/StoresDim.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "SuppliersDim" ("SupplierKey","SupplierId","Naziv","Adresa","Telefon","Napomena","DataOrigin","UpdatedAt") FROM '$containerPath/analytics/SuppliersDim.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "FootwearTypesDim" ("TypeKey","TypeId","Naziv","DataOrigin","UpdatedAt") FROM '$containerPath/analytics/FootwearTypesDim.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "SeasonsDim" ("SeasonKey","SeasonId","Naziv","DatumOd","DatumDo","DataOrigin","UpdatedAt") FROM '$containerPath/analytics/SeasonsDim.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "ProductsDim" ("ProductKey","ProductId","PLU","ProductName","Category","SubCategory","Brand","Velicina","Boja","Materijal","FootwearTypeId","SupplierId","SeasonId","PurchasePrice","PurchasePriceRsd","FirstSalePrice","SalePrice","IsActive","Timestamp","Kolicina","MinimalnaKolicina","DataOrigin") FROM '$containerPath/analytics/ProductsDim.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "SalesFacts" ("Id","SaleId","BrojRacuna","SaleTimestampUtc","StoreId","PaymentType","TotalAmount","TotalUnits","TotalLines","DataOrigin") FROM '$containerPath/analytics/SalesFacts.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "SalesLineFacts" ("Id","SaleId","ProductId","Qty","UnitPrice","LineTotal","NabavnaCena","DataOrigin") FROM '$containerPath/analytics/SalesLineFacts.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "InventoryMovementFacts" ("Id","SourceId","TipPromene","Datum","ArtikalId","Kolicina","StaraProdajnaCena","NovaProdajnaCena","Iznos","StoreId","DobavljacId","BrojDokumenta","KorisnikIme","DataOrigin") FROM '$containerPath/analytics/InventoryMovementFacts.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "InventoryRecommendations" ("Id","SnapshotDate","ProductId","Brand","Category","SalesVelocity","StockOnHand","TrendScore","MomentumScore","RecommendedQty","CreatedAt") FROM '$containerPath/analytics/InventoryRecommendations.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "analytics_refresh_runs" ("Id","JobKey","JobName","Status","StartedAtUtc","FinishedAtUtc","DurationSeconds","RefreshedObjectsJson","FailedObjectsJson","ErrorCode","ErrorMessage","CorrelationId","TriggeredBy","ProcessMode","WorkerName","CreatedAtUtc") FROM '$containerPath/analytics/AnalyticsRefreshRuns.csv' WITH (FORMAT csv, HEADER true, NULL '');
\copy "analytics_action_items" ("Id","SourceType","SourceKey","SourceId","Title","Description","RecommendationStatus","Priority","ImpactEstimateRsd","DueAtUtc","ExpectedImpactRsd","MeasuredImpactRsd","OutcomeStatus","OutcomeMeasuredAtUtc","OutcomeNotes","ConfidencePct","ReliabilityPct","DataQualityStatus","Status","ActionUrl","MetadataJson","CreatedAtUtc","UpdatedAtUtc","ResolvedAtUtc","CreatedByUserId","UpdatedByUserId","UpdatedByUserName") FROM '$containerPath/analytics/AnalyticsActionItems.csv' WITH (FORMAT csv, HEADER true, NULL '');

SELECT setval(pg_get_serial_sequence('"StoresDim"', 'StoreKey'), COALESCE((SELECT MAX("StoreKey") FROM "StoresDim"), 1), true);
SELECT setval(pg_get_serial_sequence('"SuppliersDim"', 'SupplierKey'), COALESCE((SELECT MAX("SupplierKey") FROM "SuppliersDim"), 1), true);
SELECT setval(pg_get_serial_sequence('"FootwearTypesDim"', 'TypeKey'), COALESCE((SELECT MAX("TypeKey") FROM "FootwearTypesDim"), 1), true);
SELECT setval(pg_get_serial_sequence('"SeasonsDim"', 'SeasonKey'), COALESCE((SELECT MAX("SeasonKey") FROM "SeasonsDim"), 1), true);
SELECT setval(pg_get_serial_sequence('"ProductsDim"', 'ProductKey'), COALESCE((SELECT MAX("ProductKey") FROM "ProductsDim"), 1), true);
SELECT setval(pg_get_serial_sequence('"SalesFacts"', 'Id'), COALESCE((SELECT MAX("Id") FROM "SalesFacts"), 1), true);
SELECT setval(pg_get_serial_sequence('"SalesLineFacts"', 'Id'), COALESCE((SELECT MAX("Id") FROM "SalesLineFacts"), 1), true);
SELECT setval(pg_get_serial_sequence('"InventoryMovementFacts"', 'Id'), COALESCE((SELECT MAX("Id") FROM "InventoryMovementFacts"), 1), true);
SELECT setval(pg_get_serial_sequence('"InventoryRecommendations"', 'Id'), COALESCE((SELECT MAX("Id") FROM "InventoryRecommendations"), 1), true);
SELECT setval(pg_get_serial_sequence('"analytics_refresh_runs"', 'Id'), COALESCE((SELECT MAX("Id") FROM "analytics_refresh_runs"), 1), true);
SELECT setval(pg_get_serial_sequence('"analytics_action_items"', 'Id'), COALESCE((SELECT MAX("Id") FROM "analytics_action_items"), 1), true);

COMMIT;
"@

Invoke-PsqlScript -Database $OperationalDatabase -Sql $operationalSql
Invoke-PsqlScript -Database $AnalyticsDatabase -Sql $analyticsSql

Write-Host "Demo dataset loaded successfully."
Write-Host "Operational DB: $OperationalDatabase"
Write-Host "Analytics DB:   $AnalyticsDatabase"
