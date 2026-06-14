# Pilot Onboarding Import Map

## How to read this map

Use this table when customer data is delivered as CSV, Excel export, or Access tables. The goal is to map source columns to the canonical Trendplus fields that power the pilot analytics.

If the source already uses the same field name, the mapping is direct. If the source uses a synonym, the importer can usually still match it through its alias rules.

## Import map

| Customer column | Trendplus field | Required | Example | Affects |
|---|---|---:|---|---|
| `SifraArtikla` | `ArticleId` / `SKU` | Yes | `TS-1048` | catalog join, sales join, product analytics |
| `PLU` | `ArticleId` / `SKU` | Yes | `8601234567890` | catalog join, sales join, product analytics |
| `NazivArtikla` | `Name` | Yes | `Patike Run Pro` | readable product cards, reports, search |
| `Dobavljac` | `SupplierName` | Recommended | `XYZ Sport` | supplier scorecard, supplier decision hub |
| `SupplierId` | `SupplierId` | Recommended | `17` | supplier joins, dedupe, scorecard |
| `Kolicina` | `Quantity` | Yes | `4` | units sold, stock movement, actions |
| `DatumProdaje` | `SaleDate` | Yes | `2026-06-01` | time series, freshness, reports |
| `Cena` | `SalePrice` | Yes | `7.990,00` | revenue, markdown analysis, gross sales |
| `NabavnaCena` | `CostPrice` | Recommended | `4.200,00` | margin, profitability, confidence |
| `Lager` | `CurrentStock` | Recommended | `18` | inventory, OOS risk, dead stock |
| `Kategorija` | `Category` | Optional | `Obuca` | slicing, grouping, product filtering |
| `Velicina` | `Size` | Optional | `42` | size segmentation, assortments |
| `Boja` | `Color` | Optional | `Crna` | color segmentation, assortments |
| `IDObjekat` | `StoreId` / `Location` | Optional | `3` | store-level inventory and sales analysis |
| `BrojRacuna` | `ReceiptId` / `TransactionId` | Optional | `R-2026-441` | dedupe, transaction traceability |
| `DatumNivelacije` | `MarkdownDate` / `NivelacijaDate` | Optional | `2026-05-18` | markdown / nivelacija analytics |
| `PovracajKolicina` | `ReturnQuantity` | Optional | `1` | returns, net sales, exception analysis |

## Common source aliases already recognized by the importer

Trendplus already recognizes many common synonyms, so customers do not need to rename their source first. Examples:
- `SifraArtikla`, `SKU`, `barcode`, `barkod`, `code`
- `Naziv`, `NazivArtikla`, `Name`, `ProductName`
- `Dobavljac`, `Supplier`, `SupplierName`
- `Kolicina`, `qty`, `quantity`, `stock`, `lager`, `zaliha`
- `Datum`, `DatumProdaje`, `SaleDate`
- `NabavnaCena`, `PurchasePrice`, `Cost`

## What each mapping unlocks

- `CostPrice` present: margin and profitability become usable.
- `SupplierName` or `SupplierId` present: supplier analytics becomes reliable.
- `CurrentStock` present: inventory, OOS risk and dead stock become visible.
- `Category` / `Size` / `Color` present: segmentation is better, but the pilot can start without them.

## Practical guidance for onboarding

1. Send the raw source with the original column names.
2. Mark which table is the product master, sales lines, suppliers and stock.
3. Confirm whether prices are already in RSD.
4. Confirm whether returns and markdowns exist as separate tables.
5. Start with the required fields above, then enrich with optional fields later.

## Confidence notes

- Without `CostPrice`: revenue yes, margin no.
- Without `SupplierName` / `SupplierId`: product analytics yes, supplier scorecard limited.
- Without `CurrentStock`: sales yes, inventory/OOS limited.
- Without `ArticleId` / `SKU`: the import cannot reliably join sales to products.

