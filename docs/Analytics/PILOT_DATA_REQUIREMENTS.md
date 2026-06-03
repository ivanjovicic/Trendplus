# Pilot Data Requirements

This document explains what data Trendplus needs before a pilot analytics setup can be trusted.

The goal is simple:
- show what can be analyzed
- show what is missing
- separate hard blockers from things that only lower confidence

If a field is missing from a blocker list, Trendplus may still show some dashboards, but it should reduce trust or hide the risky recommendation instead of pretending the data is complete.

## Required datasets

| Dataset | Required | Why it matters | If missing |
|---|---:|---|---|
| Products | Yes | Gives every article a stable identity and name | Product-level analytics cannot be joined correctly |
| Sales lines | Yes | This is the core source for sales, units, revenue, and trends | Sales analytics cannot run |
| Suppliers | Yes | Needed for supplier scorecards, supplier trends, and ownership of stock | Supplier analytics becomes incomplete or impossible |
| Stock | Yes | Needed for inventory risk, dead stock, out-of-stock risk, and coverage | Inventory analytics loses trust |
| Cost prices | Yes | Needed to calculate gross margin and real profit | Margin analytics becomes unreliable |
| Retail prices | Yes | Needed to calculate revenue and price comparison | Pricing and sales value analytics become incomplete |
| Markdown / nivelacija | Yes | Needed to explain price changes and pre/post analysis | Price-change analytics becomes blind to the reason behind changes |
| Returns | Optional | Improves net sales and margin accuracy | Core analytics can still run, but net results are less precise |

## Required fields

These are the fields that the pilot needs to connect the data together.

| Field | Required | What it is used for |
|---|---:|---|
| ArticleId / SKU | Yes | Stable product identity and joins across files |
| ArticleName | Yes | Human-readable product name in the UI and reports |
| SaleDate | Yes | Period filtering, trends, freshness, and report windows |
| Quantity | Yes | Units sold, stock movement, and demand velocity |
| SalePrice | Yes | Revenue, pricing, and margin calculations |
| SupplierName / SupplierId | Yes | Supplier grouping, supplier reports, and ownership of inventory |
| CostPrice | Yes | Gross margin, profit, and trustworthy recommendation signals |
| StoreId / StoreName | Yes | Store-level analytics and comparison across locations |

## What blocks analytics

These fields are hard blockers. If they are missing, the analytics layer cannot produce a trustworthy result:

- `ArticleId` / `SKU`
- `SaleDate`
- `Quantity`
- `SalePrice`

Why they block:
- without `ArticleId` there is no stable product join
- without `SaleDate` there is no time period
- without `Quantity` there is no unit movement
- without `SalePrice` there is no revenue

When one of these is missing, Trendplus should treat the result as unavailable or insufficient data, not as zero.

## What only reduces confidence

These fields do not always block the full pilot, but they reduce confidence and can disable some recommendations:

- `CostPrice`
- `SupplierName` / `SupplierId`
- `Stock`
- `Category`

What that means in practice:
- missing `CostPrice` makes margin and profit estimates less reliable
- missing supplier data weakens supplier scorecards and supplier recommendations
- missing stock data weakens inventory risk, dead stock, and replenishment decisions
- missing category data reduces grouping quality and comparison quality

## Pilot trust rules

For the pilot, Trendplus should use these rules:

- blocker missing -> hide or stop the affected analytics block
- reducer missing -> show the analytics block, but lower confidence and show a warning
- never convert unknown data into a fake zero
- clearly explain whether the issue is a missing dataset, a missing field, or a stale refresh

## Minimal pilot package

If you want the smallest useful pilot import, send:

1. products
2. sales lines
3. suppliers
4. stock
5. cost prices
6. retail prices
7. markdown / nivelacija history

Returns can come later if the first pilot needs to be shipped quickly.

