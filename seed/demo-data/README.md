# Trendplus Demo Dataset

Demo-only seed package for local sales demos and pilot rehearsals.

## What is included

This package contains a reproducible demo dataset for Trendplus analytics:

- `operational/`
  - `Dobavljaci.csv`
  - `TipoviObuce.csv`
  - `Sezone.csv`
  - `Artikli.csv`
  - `prodaja_zaglavlje.csv`
  - `prodaja_stavke.csv`
  - `DnevnikPromena.csv`
  - `DataImportBatches.csv`
- `analytics/`
  - `StoresDim.csv`
  - `SuppliersDim.csv`
  - `FootwearTypesDim.csv`
  - `SeasonsDim.csv`
  - `ProductsDim.csv`
  - `SalesFacts.csv`
  - `SalesLineFacts.csv`
  - `InventoryMovementFacts.csv`
  - `InventoryRecommendations.csv`
  - `AnalyticsRefreshRuns.csv`
  - `AnalyticsActionItems.csv`
- `support/`
  - `data_quality_issues.csv`
  - `stock.csv`
  - `manifest.json`

## Dataset shape

- 7 suppliers
- 120 products
- 3 stores
- 180 days of sales history
- 540 sales headers
- 1620 sales lines
- 360 stock snapshot rows
- markdown / nivelacija events
- intentional data quality issues

## How to load locally

The package is meant for the local Docker Postgres setup used by Trendplus.

Default command:

```powershell
cd C:\Users\Alex\source\repos\TrendplusNew
.\scripts\demo-data\load-demo-data.ps1
```

Default behavior:

- loads operational and analytics tables
- clears demo-target tables first
- copies the CSV package into the `trendplus-postgres` container
- targets the same database name for both operational and analytics by default

If your local config uses a split analytics database, override the target names:

```powershell
.\scripts\demo-data\load-demo-data.ps1 -OperationalDatabase trendplus -AnalyticsDatabase analytics
```

If you need to regenerate the CSV package, run:

```powershell
python .\scripts\demo-data\generate-demo-data.py
```

## Warning

**Demo podaci - ne koristiti za poslovne odluke.**

This dataset is not production/customer data and must never be mixed with a real import.

