# Analytics Second Calculation Audit - 2026-09-06

## Outcome

This is a second static/contract audit after `ANALYTICS_CALCULATION_RECHECK_2026-09-06.md`. It intentionally does not repeat `RQ154`-`RQ166` and does not execute runtime fixes. The scope is analytical calculation and trust-state behavior only; trend, Shopify, forecast and test-only feature work are excluded unless needed to prove a boundary.

The audit found four additional bounded risks and added `RQ167`-`RQ170` to the canonical queue. They remain `WAITING`; `RQ154` remains the single `READY` prompt required by the queue protocol.

## New findings

| Finding | Area | Evidence | Risk | Prompt |
|---|---|---|---|---|
| N6 | Sales/inventory failures | Cached sales summary returns zero revenue, units, transactions and averages with `meta.success=false` (`Api/Endpoints/CachedAnalyticsEndpoints.cs:150-196`). Direct inventory balance does the same (`Api/Endpoints/InventoryEndpoints.cs:66-78`). The failure test asserts these zeros (`Api.Tests/CachedAnalyticsFailureContractTests.cs:18-30`). | Alternate clients, exports or reports can treat a failed query as a real zero. | `RQ167` |
| N7 | Top-product margin | Advanced SQL sets `margin_impact` null only when no positive cost exists (`Api/Endpoints/CachedAnalyticsEndpoints.cs:3438-3463`). One known-cost line therefore makes mixed coverage non-null; mapping labels it `good` and ranks it (`3527-3574`). | Partial margin can appear confirmed and affect the margin leaderboard. | `RQ168` |
| N8 | Data Quality intake readiness | `CalculateIntakeScore` clamps empty article/import denominators to one (`Api/Endpoints/DataQualityEndpoints.cs:1501-1505`), and the report builder resolves readiness even with no articles/import (`481-620`, `622-640`). | Empty evidence can receive a numeric readiness score and misleading usable/green status. | `RQ169` |
| N9 | Pilot intake report period | `ResolveIntakePeriod` swaps reversed dates and `TryParseUtcDate` returns null for invalid input, causing a default period (`Api/Endpoints/DataQualityEndpoints.cs:1533-1559`). | Report, export and stable URL can describe a different period than the user requested. | `RQ170` |

## Existing coverage checked

- `Api.Tests/CachedAnalyticsFailureContractTests.cs` proves stable error metadata and currently locks the problematic zero-valued inventory failure body; it has no unavailable/null KPI assertion for failed sales summary.
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs:67-83` correctly proves successful empty sales data is not an error, so `RQ167` must preserve that behavior.
- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs:174-285` proves trust fields on already-built top-product DTOs, not mixed sale-line cost coverage through SQL.
- `Api.Tests/AnalyticsDataQualityConsistencyTests.cs:113-143` proves one critical intake case but does not cover zero articles, zero rows, no import batch or a populated zero-issue dataset.
- `Api.Tests/AnalyticsReportsContractTests.cs` proves report shape but does not cover invalid/reversed pilot-intake period input.

## Prior work and non-duplicates

Git history was reviewed for the concrete owners before writing prompts:

- Cached analytics trust and failure paths: `29a5943a`, `69511be0`, `e77af0ff`.
- Data Quality readiness/denominator hardening: `196266cb`, `da18187c`, `6ce1591d`, `29a5943a`.
- Inventory scope and reliability work: `01795489`, `e4d53a61`, `ce854624`.
- Sales summary historical handler: `00b205fb`, `5a75ad25`, `e9535985`.

The new prompts are intentionally narrower than existing owners:

- `RQ167` is the residual failed-body representation contract, not a repeat of general numeric-state work in `RQ139`.
- `RQ168` is the concrete top-products mixed-cost coverage path, not a replacement for the broad financial-basis contract in `RQ148`.
- `RQ169` is intake readiness empty-state behavior, not the traffic health denominator contract in `RQ144`.
- `RQ170` is pilot-intake report period validation, distinct from `RQ161` Analytics Details and `RQ166` action timeline period handling.

## Queue and delivery truth

- `RQ154` remains the only `READY` item.
- `RQ167`-`RQ170` are new `WAITING` prompts and are not completed runtime fixes.
- This audit changes documentation/queue planning only; no backend or frontend behavior is claimed as repaired.
- Live database, refresh, browser and console proof remain outstanding.

