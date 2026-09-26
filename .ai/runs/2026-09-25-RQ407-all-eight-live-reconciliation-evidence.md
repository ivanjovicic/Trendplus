Task ID: RQ407-all-eight-live-reconciliation
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-25
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: 0a10a78d
Main verification: passed - fresh `origin/main` resolves to `65a8775cb004ca7d0cac4705296957f2f65894ec` and contains implementation `0a10a78d`
Evidence state: synchronized

## Interpreted outcome and owner

RQ407 required one deterministic Operations fact set to reconcile Inventory, Supplier Sales, Shoe Type, Daily Sales, Pre/Post Nivelacija, Color, Pre-Nivelacija and Supplier Footwear. The owning boundary remains the cross-screen proof adapter; existing route owners remain authoritative.

## What changed

- Added `Api.Tests/Fixtures/operations-analytics-all-routes-seed.sql`, a PostgreSQL seed with signed sale/return facts, two stores, existing/imported scope, unknown dimensions, cost gaps, shift/off-shift rows, inventory states and a comparable price event.
- Added `Api.Tests/OperationsAnalyticsAllRoutesIntegrationTests.cs`, which seeds one database before the host starts and asserts the eight HTTP route families from that same source.
- Repaired the PostgreSQL-sensitive Pre-Nivelacija markdown evidence reduction in `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs`: the database query now materializes bounded evidence rows and preserves the existing in-memory scoring formula.

## Validation

- Live PostgreSQL proof: `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter FullyQualifiedName~OperationsAnalyticsAllRoutesIntegrationTests --no-restore --verbosity minimal` -> `1/1` passed with `TRENDPLUS_RUN_INTEGRATION_TESTS=true` against `127.0.0.1:55432`.
  - Shared sales period: signed quantity `5`, revenue `540 RSD`.
  - Supplier and Shoe Type totals: `5 / 540 RSD`.
  - Daily rows: revenue sums to `540 RSD`, five signed units.
  - Pre/Post event: `2 / 200 RSD` before, `3 / 270 RSD` after, `35%` revenue change.
  - Color: three buckets; totals `5 / 540 RSD`.
  - Pre-Nivelacija: one candidate and successful metadata; no fake zero/error fallback.
  - Inventory: OOS and insufficient-evidence rows are present with blocked recommendations where evidence is insufficient.
  - Supplier Footwear: vendor/type-insight response is authoritative and contains the comparable `NIV-101` row.
- `OperationsAnalyticsProofPackTests` -> `7/7` passed.
- `AnalyticsFrontendRouteSmokeTests` -> `12/12` passed.
- `PreNivelacijaQueryFailureMetaTests` -> `2/2` passed.
- Selected frontend Operations page tests -> `8` files, `134/134` passed.
- `dotnet build Api/Api.csproj --configuration Release --no-restore` -> passed with `0` warnings and `0` errors.
- `git diff --check` -> passed.

## Not run / residual risk

- Browser/deployed request-to-render capture was not run; frontend page/projection tests and static adapter checks remain the local proof for that layer.
- Remote CI was not inspected.
- Plain `postgres:15` lacks the optional `vector` extension. Application startup remains non-strict for that optional analytics initialization, while the eight tested route families use the relational/view-backed contracts proved above.

## Delivery

- Implementation commit: `0a10a78d` (`test(analytics): reconcile all Operations routes on one fixture`), pushed to `origin/main`.
- Queue and evidence closure delivered directly to `main` in `65a8775cb004ca7d0cac4705296957f2f65894ec`; fresh `origin/main` verification contains implementation `0a10a78d`.
