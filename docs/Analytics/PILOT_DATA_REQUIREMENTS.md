# Pilot Data Requirements

Trendplus cannot be sales-ready if customer data intake depends only on developer memory.

This page explains, in plain language, what data a customer must send for a pilot to work and what is only nice to have.

## Required datasets

| Dataset | Required | Why it matters |
|---|---:|---|
| Products | Yes | Gives each article a stable identity and name. |
| Sales lines | Yes | This is the core source for sales analytics. |
| Suppliers | Yes | Needed for supplier analytics and supplier scorecards. |
| Stock | Yes | Needed for inventory and out-of-stock analysis. |
| Cost prices | Yes | Needed to calculate margin and profitability. |
| Retail prices | Yes | Needed to calculate revenue and compare pricing. |

## Optional datasets

These are useful, but they do not have to be present on day one:

- markdown / nivelacija
- returns
- size
- color
- category

## Blocking fields

These fields are the minimum needed for the pilot to produce trustworthy sales analytics:

- `ArticleId`
- `SaleDate`
- `Quantity`
- `SalePrice`

If one of these is missing, the affected analytics should be treated as unavailable or insufficient data. It should not be shown as a fake zero.

## Trust reducers

These fields do not always block the pilot, but they lower confidence:

- `CostPrice`
- `Supplier`
- `Stock`
- `Category`
- `Size`
- `Color`

What that means:

- without `CostPrice`, revenue is still possible, but margin is not trustworthy
- without `Supplier`, product analytics can still work, but supplier scorecards become limited
- without `Stock`, sales analytics still work, but inventory and out-of-stock analysis are limited
- without `Category`, `Size`, or `Color`, grouping and drill-downs become weaker

## What the pilot needs to answer

For a non-developer, the easiest way to think about the pilot is:

- What was sold?
- When was it sold?
- How many units were sold?
- At what price?
- What did it cost?
- Who supplied it?
- How much stock is left?

If the customer can answer those questions, Trendplus can usually produce a useful pilot. If not, the result may still load, but it should be clearly marked as incomplete or low-confidence.

## Minimal pilot package

If the customer wants the smallest useful pilot import, ask for:

1. products
2. sales lines
3. suppliers
4. stock
5. cost prices
6. retail prices

Markdown / nivelacija, returns, size, color, and category can be added later.

