# Pilot Onboarding Import Map

Use this document when mapping a customer export or ERP dump into Trendplus.

The goal is to make the intake understandable for non-developers:

- `Customer column` is what the source file calls the field
- `Trendplus field` is the canonical field Trendplus expects
- `Required` says whether it belongs in the standard pilot intake package
- `Affects` explains which analytics area uses it

Some rows are still trust reducers rather than hard blockers. The blockers are listed below the table.

## Mapping table

| Customer column | Trendplus field | Required | Example | Affects |
|---|---|---:|---|---|
| SifraArtikla / ArticleId / SKU | `ArticleId` | Yes | `SKU-10293` | Product identity, joins, product analytics |
| NazivArtikla / ArticleName / ProductName | `Name` | Yes | `Patike Air Max 90` | UI labels, reports, search, exports |
| Dobavljac / Supplier / Vendor | `SupplierName` | No | `Sport d.o.o.` | Supplier analytics, supplier scorecard, ownership |
| Kolicina / Quantity / Qty / Units | `Quantity` | Yes | `2` | Units sold, demand, stock movement |
| DatumProdaje / SaleDate / InvoiceDate / TransactionDate | `SaleDate` | Yes | `2026-05-17` | Period filters, refresh windows, trends |
| Cena / SalePrice / UnitPrice / RetailPrice | `SalePrice` | Yes | `12990` | Revenue, sales value, margin inputs |
| NabavnaCena / CostPrice / PurchasePrice | `CostPrice` | No | `8500` | Margin, profitability, recommendation confidence |
| Lager / CurrentStock / StockQty / OnHandQty | `CurrentStock` | No | `14` | Inventory risk, dead stock, replenishment, OOS |
| StoreId / LocationId | `StoreId` | No | `3` | Store analytics, store comparison, inventory views |
| StoreName / LocationName | `StoreName` | No | `Novi Sad TC` | Readable store labels in UI and reports |
| Category / CategoryName | `Category` | No | `Running shoes` | Grouping, drill-downs, trend comparisons |
| Size / SizeName | `Size` | No | `42` | Size analysis, assortment views, stock sizing |
| Color / ColorName | `Color` | No | `Black` | Color analysis, assortment views, stock mix |
| MarkdownDate / NivelacijaDate | `MarkdownDate` | No | `2026-04-01` | Pre/post analysis, markdown history |
| MarkdownFromPrice / OldPrice | `MarkdownFromPrice` | No | `14990` | Price-change analysis, markdown effect |
| MarkdownToPrice / NewPrice | `MarkdownToPrice` | No | `12990` | Price-change analysis, revenue lift / loss |
| ReturnDate / ReturnQty / ReturnAmount | `Return*` | No | `1` | Net sales, returns accuracy, margin corrections |

## What the customer needs to send

In practical terms, Trendplus needs the customer to send:

- one row per sale line
- a product identifier
- a sale date
- a quantity
- a sale price
- a cost price if available
- a supplier if available
- stock if available

## What blocks the pilot

These fields are blockers:

- `ArticleId`
- `SaleDate`
- `Quantity`
- `SalePrice`

If any blocker is missing, the pilot cannot produce trustworthy sales analytics for that slice of data.

## What only reduces confidence

These fields usually do not stop the pilot, but they lower confidence:

- `CostPrice`
- `Supplier`
- `Stock`
- `Category`
- `Size`
- `Color`

What that means in plain language:

- without cost price, revenue is still visible, but margin is not
- without supplier, product analytics can still work, but supplier scorecards are limited
- without stock, sales still work, but inventory / OOS views are limited

## Example customer mapping

If a customer sends a CSV, a good pilot file would usually look like this:

- `SifraArtikla` -> `ArticleId`
- `NazivArtikla` -> `Name`
- `Dobavljac` -> `SupplierName`
- `Kolicina` -> `Quantity`
- `DatumProdaje` -> `SaleDate`
- `Cena` -> `SalePrice`
- `NabavnaCena` -> `CostPrice`
- `Lager` -> `CurrentStock`

## Recommended onboarding order

1. Confirm the product key and sale line grain.
2. Confirm the sales date column.
3. Confirm the quantity and sale price columns.
4. Confirm cost price if the customer has it.
5. Add supplier and stock if available.
6. Add markdown / nivelacija, returns, size, color, and category if available.

If the customer can answer those steps, the pilot usually has enough structure to move forward.
