Task ID: direct-supplier-decision-reliability
Queue: direct-user-request
Date: 2026-09-02
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: 1a78a0d91d93f897faab7aebddb1a4a247745a2b
Main verification: passed - origin/main contains 1a78a0d9 after push
Evidence state: pending

## What was done

- Registered the Decision Pulse endpoint, service, delivery service and options in the API startup path.
- Added startup verification and forced repair for missing or stale 90-day and 180-day supplier decision materialized views, including the shared-database repair path.
- Added route and schema-repair regression coverage.
- Made the analytics error state readable across themes with theme tokens, stronger borders, explicit primary text, readable error-code styling and keyboard focus states.
- Added zero-width guards around the supplier decision chart containers.
- Corrected Serbian diacritics in supplier decision unavailable messages.

## Files changed

- Api/Program.cs
- Api/Endpoints/DecisionPulseEndpoints.cs
- Api/Endpoints/SupplierDecisionHubEndpoints.cs
- Infrastructure/Seed/DatabaseInitializer.cs
- Api.Tests/AnalyticsCriticalRouteMappingsTests.cs
- Api.Tests/SupplierDecisionSchemaSqlTests.cs
- Klijent/clientapp/src/components/analytics/AnalyticsErrorState.css
- Klijent/clientapp/src/pages/SupplierSalesStatsPage.css

## Validation run

- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsCriticalRouteMappingsTests|FullyQualifiedName~SupplierDecisionSchemaSqlTests" --no-restore` -> pass, 32/32.
- `npm run test:run -- src/pages/__tests__/SupplierDecisionHubPage.spec.tsx` -> pass, 8/8.
- `npm run check:analytics-guardrails` -> pass, including encoding, analytics guardrails and typecheck.
- `npm run build` -> pass.
- `git diff --check` -> pass.
- `git push origin main` -> pass, implementation commit 1a78a0d9 pushed.

## Validation not run

- Full backend test suite -> not run; focused owner tests and build were sufficient for this scoped change.

## Documentation impact

- No owner documentation required updating; the change follows the existing startup SQL and analytics error contracts.

## What was missed

- The public Render endpoint still returned HTTP 404 immediately after the push; the deployment had not yet picked up the commit at verification time.
- Live database migration logs and production materialized-view presence could not be inspected from the repository workspace.

## Risks

- Production confirmation remains dependent on the Render deployment completing and startup repair reaching the configured analytics database.
- If a required upstream relation is unavailable, startup repair will fail loudly and retry on a later startup rather than silently serving fake values.

## Next

- Verify the Render deployment and recheck `/api/analytics/decision-pulse?dataScope=all`; inspect startup logs for the 90d/180d view verification messages if the endpoint is not healthy.
