Task ID: direct-decision-pulse-migration-chart
Queue: direct-user-request
Date: 2026-09-02
Agent/tool: Codex
Delivery target: main
Working branch / PR: main
Main commit SHA: 9d0cb082f22aece233e99d6700f8131a9561227e
Main verification: origin/main contains 9d0cb082f22aece233e99d6700f8131a9561227e
Evidence state: synchronized

## What was done
- Confirmed `DecisionPulse` is registered, mapped at `/api/analytics/decision-pulse`, and live on Render with HTTP 200.
- Fixed migration 029's repeat-safe dependency contract for both 90-day and 180-day supplier score caches.
- Added the missing 180-day evidence-quality and supplier-total coverage columns, preserving append-only output ordering for safe re-application after a partial view.
- Added a positive-size `ResizeObserver` guard before mounting the two Supplier Sales charts, with a visible preparation state.

## Files changed
- Database/Migrations/029_AddSupplierDecisionWindowedViews.sql
- Api.Tests/SupplierDecisionSchemaSqlTests.cs
- Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx
- Klijent/clientapp/src/pages/SupplierSalesStatsPage.css
- render.yaml
- .ai/runs/2026-09-02-direct-decision-pulse-migration-chart-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SupplierDecisionSchemaSqlTests --no-restore` -> pass (25/25)
- Disposable PostgreSQL replay of the actual `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql` after the fixes -> pass; both `mv_supplier_decision_score_cache_90d` and `mv_supplier_decision_score_cache_180d` verified present (`t|t`), including repeat application over the partially-created schema.
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~SupplierDecisionSchemaSqlTests --no-restore --no-build` -> pass (25/25)
- `npm run test:run -- src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx --reporter=dot` -> pass (4/4)
- `npm run check:analytics-guardrails` -> pass
- `npm run build` -> pass
- `dotnet build Api/Api.csproj --no-restore` -> pass (0 errors; existing analyzer warnings)
- GitHub workflow `33655606258` for `9d0cb082` -> failed in `Trigger Render Deploy`; public check details expose only exit code 1, while job logs require repository admin rights.
- Production `/api/runtime/version` -> still `068e59a1`, so the final commit is not active on Render.
- Production supplier summary -> HTTP 200 transport, but `meta.errorCode=MISSING_TABLE` and zero rows.
- Production `GET /api/analytics/decision-pulse?dataScope=all` -> HTTP 200 with `PULSE_PARTIAL`; no actionable items because 124 candidates are suppressed for insufficient evidence and the supplier hub is unavailable.
- `git diff --check` -> pass

## Validation not run
- Direct SQL inspection/re-run against Render Analytics -> not run - Render secret is not available in this session; the local development credential was rejected by the remote database.
- Browser visual smoke -> not run - code/build/test proof was available, but no authenticated browser session was used.

## Documentation impact
- This run log records the implementation, production observations, validation and the external Render deployment limitation.

## Root cause and residual blocker
- The local SQL replay found and fixed the concrete migration defects: an ambiguous duplicate `evidence_quality_status` in the 90-day score-cache query, then missing coverage forwarding in the 180-day supplier totals, in addition to the earlier incomplete dependency projection.
- The remaining production error cannot be declared resolved because Render did not activate the final deploy. A Render owner/admin must inspect the failed deploy log or redeploy `9d0cb082` (and verify the Analytics startup repair on the same database). No production data was mutated from this session.

## Delivery
- Commits pushed to `main`: `f9d07c01` (final migration/test fix) and `9d0cb082` (Render deploy trigger marker).
- `origin/main` contains `9d0cb082`.
