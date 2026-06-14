# Pilot Data Requirements

## Purpose
Pilot analytics must have enough source data to answer five basic questions:
- šta se prodaje
- gde je marža stvarna
- gde je mrtav lager
- koji dobavljači nose vrednost
- kojim podacima možemo verovati

If the pilot feed is missing the wrong fields, Trendplus can still ingest data, but the confidence of product, supplier and inventory analytics drops fast.

## Minimum data set

| Dataset | Required fields | Used for | If missing |
|---|---|---|---|
| Products | `ArticleId` / `SKU`, `Name` | product catalog, joins, product decision center | sales cannot be matched to the catalog |
| Sales lines | `SaleDate`, `ArticleId` / `SKU`, `Quantity`, `SalePrice` | revenue, trends, action queue, reports | core sales analytics becomes incomplete or unusable |
| Suppliers | `SupplierId` or `SupplierName` | supplier scorecard, supplier decision hub | supplier analytics is limited and confidence drops |
| Stock | `CurrentStock`, optional `Store` / `Location` | inventory, OOS risk, dead stock | inventory analytics becomes partial |
| Cost prices | `CostPrice` | margin, profitability, recommendation confidence | revenue remains available, but margin cannot be trusted |
| Retail prices | `SalePrice` or list price | revenue, markdown analysis, price sanity checks | price analytics becomes partial |

## Blocking fields

These fields are the practical minimum for a usable pilot import:
- `ArticleId` / `SKU`
- `SaleDate`
- `Quantity`
- `SalePrice`

If any of those are missing, the import may still run, but Trendplus cannot reliably calculate the main pilot KPIs.

## Trust reducers

These fields are not always blocking, but they materially improve the confidence of the pilot:

| Field | What it improves | What degrades if missing |
|---|---|---|
| `CostPrice` | margin contribution, profitability, action quality | revenue still works, but margin is not trustworthy |
| `Supplier` | supplier scorecard, supplier decision hub | supplier ranking becomes weak or partial |
| `Stock` | inventory, OOS risk, dead stock, replenishment decisions | inventory insights become limited |
| `Category` / `Size` / `Color` | segmentation, filtering, product grouping | product insights are less precise |

## Optional but high-value

These are not required for the first pilot import, but they make the analytics much stronger:
- markdown / nivelacija events
- returns
- size
- color
- category
- store / location
- receipt number / transaction id

## What the pilot team should send first

If the customer wants the fastest possible pilot, ask for this order:
1. products
2. sales lines
3. suppliers
4. stock
5. cost prices
6. retail prices

That order gives the best tradeoff between speed and trust.

## What still works without each field

- No cost price: revenue is visible, but margin is not.
- No supplier: product analytics still works, but supplier scorecard is limited.
- No stock: sales analytics still works, but inventory and OOS views are limited.
- No category / size / color: the pilot still works, but filtering and grouping are weaker.

