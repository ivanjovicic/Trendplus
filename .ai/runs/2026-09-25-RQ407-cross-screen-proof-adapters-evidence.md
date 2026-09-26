Task ID: RQ407-cross-screen-proof-adapters
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-25
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: f1f2bc88ce72361e1cb5a61e07ef0fae17b1ff02
Main verification: passed - fresh `origin/main` resolves to f1f2bc88ce72361e1cb5a61e07ef0fae17b1ff02 and contains the implementation SHA
Evidence state: synchronized

## What was done
- Resumed RQ407 under the user's explicit continuation request after the Docker PostgreSQL integration host became available.
- Extended the shared `operations-analytics-v1` route manifest with the real frontend page, focused proof file and separate page/proof contract tokens for all eight Operations families.
- Added a deterministic test that verifies every declared page/proof adapter exists and still contains its declared contract tokens.
- Expanded backend route-registration smoke coverage to the eight Operations endpoint families while preserving the existing analytics route checks.
- Updated the deterministic seed-pack documentation and canonical roadmap continuation note.

## Files changed
- `Api.Tests/PilotAnalyticsSeedPack.cs`
- `Api.Tests/OperationsAnalyticsProofPackTests.cs`
- `Api.Tests/AnalyticsFrontendRouteSmokeTests.cs`
- `docs/qa/ANALYTICS_PILOT_DETERMINISTIC_SEED_PACK_2026-08-24.md`
- `MASTER_ROADMAP.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`

## Validation run
- Initial adapter-contract test attempt -> fail (`1/7`) because page and proof token sets were conflated; the same-owner test contract was corrected before delivery.
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter FullyQualifiedName~OperationsAnalyticsProofPackTests --no-restore --verbosity minimal` -> pass (`7/7`).
- `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter FullyQualifiedName~AnalyticsFrontendRouteSmokeTests --no-restore --verbosity minimal` -> pass (`12/12`).
- Selected frontend Operations page tests for Inventory, Supplier Sales, Shoe Type, Daily Sales, Pre/Post, Color, Pre-Nivelacija and Supplier Footwear -> pass (`8` files, `134/134`).
- `git diff --check` -> pass.

## Validation not run
- Numeric live reconciliation of the same seeded facts across all eight backend endpoint families -> not run; the existing shared fixture has not yet been wired into the Inventory, Pre-Nivelacija and vendor/nivelacija view-backed hosts as one executable database seed.
- Browser/live frontend request-to-render proof against that same database -> not run; selected page tests use deterministic service mocks.
- Remote CI -> not inspected.

## Documentation impact
- The seed-pack document now records the eight frontend page/proof adapters and the distinction between wiring proof and numeric live reconciliation.
- Queue and roadmap now record the explicit RQ407 continuation and remaining acceptance gap.

## What was missed
- RQ407 remains incomplete: one shared database fixture still needs to drive all eight backend endpoint families and then be reconciled to browser/live frontend projections.

## Risks
- Vendor/nivelacija routes depend on view-backed contracts and the plain `postgres:15` image lacks the `vector` extension; the app can start non-strictly but that is not full deployment parity.
- Supplier and Shoe fixtures remain mutually exclusive and must run sequentially against an isolated database.

## Next
- Analytics Backend + QA/Test Infrastructure: create the one-database Operations seed adapter for Inventory, Pre-Nivelacija and vendor/nivelacija views; then run numeric all-eight reconciliation.
- Frontend: replace mock-only projection checks with a browser/live response capture for the same fixture once the host adapter exists.
