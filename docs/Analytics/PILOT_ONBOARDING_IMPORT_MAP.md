# Pilot Onboarding Import Map

Use this document when mapping a customer export or ERP dump into Trendplus.

The table below is intentionally written for non-developers:
- `Customer column` is what the source file calls the field
- `Trendplus field` is the canonical field Trendplus expects
- `Required` says whether the pilot can work without it
- `Affects` explains which analytics area uses it

## Mapping table

| Customer column | Trendplus field | Required | Example | Affects |
|---|---|---:|---|---|
| ArticleId / SKU / ItemCode | `ArticleId` | Yes | `SKU-10293` | Product identity, joins, all product-level analytics |
| ArticleName / ProductName | `ArticleName` | Yes | `Patike Air Max 90` | UI labels, reports, search, exports |
| SaleDate / InvoiceDate / TransactionDate | `SaleDate` | Yes | `2026-05-17` | Period filters, refresh windows, trends |
| Quantity / Qty / Units | `Quantity` | Yes | `2` | Units sold, demand, stock movement |
| SalePrice / UnitPrice / RetailPrice | `SalePrice` | Yes | `12990` | Revenue, sales value, margin inputs |
| SupplierId / VendorId | `SupplierId` | Yes | `42` | Supplier joins, supplier reports, supplier decisions |
| SupplierName / VendorName | `SupplierName` | Yes | `Sport d.o.o.` | Readable supplier grouping, reports, exports |
| CostPrice / PurchasePrice / NabavnaCena | `CostPrice` | Yes | `8500` | Gross margin, profit, recommendation confidence |
| StoreId / LocationId | `StoreId` | Yes | `3` | Store analytics, store comparison, inventory views |
| StoreName / LocationName | `StoreName` | Yes | `Novi Sad TC` | Readable store labels in UI and reports |
| CurrentStock / OnHandQty / StockQty | `Stock` | Recommended | `14` | Inventory risk, dead stock, replenishment, coverage |
| Category / CategoryName | `Category` | Recommended | `Running shoes` | Grouping, drill-downs, trend comparisons |
| MarkdownDate / NivelacijaDate | `MarkdownDate` | Recommended | `2026-04-01` | Pre/post analysis, markdown history |
| MarkdownFromPrice / OldPrice | `MarkdownFromPrice` | Recommended | `14990` | Price-change analysis, markdown effect |
| MarkdownToPrice / NewPrice | `MarkdownToPrice` | Recommended | `12990` | Price-change analysis, revenue lift / loss |
| ReturnDate / ReturnQty / ReturnAmount | `Return*` | Optional | `1` | Net sales, returns accuracy, margin corrections |

## How to read the map

Some source systems will not have the exact same column names as Trendplus. That is fine.

The important part is that the customer can tell us:
- which column is the product key
- which column is the sale date
- which column is the quantity
- which column is the selling price
- which column is the cost price
- which column is the supplier
- which column is the store

If the source file only has one field from a pair, Trendplus can still work, but the missing half may make the report harder to read.

## What is a blocker

These are blockers for the pilot:

- missing `ArticleId` / `SKU`
- missing `SaleDate`
- missing `Quantity`
- missing `SalePrice`

If one of these is missing, the pilot cannot produce trustworthy sales analytics for that slice of data.

## What is only a trust reducer

These fields usually do not stop the pilot, but they lower confidence:

- `CostPrice`
- `SupplierName` / `SupplierId`
- `Stock`
- `Category`

What changes when they are missing:
- missing `CostPrice` weakens margin estimates
- missing supplier data weakens supplier ranking and decision output
- missing stock weakens inventory risk and replenishment advice
- missing category weakens grouping and comparison quality

## Example import contract

If a customer sends a CSV, a good pilot file would usually have:

- one row per sale line
- one column for product ID or SKU
- one column for sale date
- one column for quantity
- one column for sale price
- one column for supplier
- one column for store
- one column for cost price
- optional columns for stock, category, and markdown history

## Recommended onboarding order

1. Confirm the product key and sale line grain.
2. Confirm the sales period and time zone.
3. Confirm the store and supplier fields.
4. Confirm cost and retail price logic.
5. Add stock and category if available.
6. Add markdown and returns if available.

If the customer can answer those six steps, the pilot usually has enough structure to move forward.

