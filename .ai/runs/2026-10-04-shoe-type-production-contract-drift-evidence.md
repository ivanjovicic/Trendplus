Task ID: shoe-type-production-contract-drift-2026-10-04
Queue: direct-user-request
Date: 2026-10-04
Agent/tool: Cursor agent
Delivery target: main
Working branch / PR: main (direct)
Main commit SHA: `37acd8031a8d3bbcafb08d7bc2bfb53f40a35acd`
Main verification: verified as an ancestor of current `main` during the 2026-10-04 owner-decision refresh
Evidence state: synchronized

## What was done
- Reproduced the production route `https://trendplus.vercel.app/analytics/shoe-type-sales-stats?periodPreset=90d&fromDate=2026-07-07&toDate=2026-10-04`.
- Confirmed the API returns HTTP 200 with 14 shoe-type rows, real revenue/margin data, `meta.success=true`, `dataQuality`, and `decisionReadiness`.
- Confirmed the frontend rejected the whole response during Zod validation because production rows omit the optional presentation fields `marginQualityTier`, `marginQualityShortLabel`, and `marginQualityTooltip`. The API response did contain the response-level quality fields and totals.
- Made those row-level presentation fields optional in the Shoe Type response schema so older cached/API payloads remain usable without weakening backend-owned recommendation and response-level trust metadata.
- Bumped the backend Shoe Type analytics cache key from `v4` to `v5`, forcing a fresh projection after deployment instead of replaying a stale response shape.
- Added a frontend regression test for the observed legacy payload shape and backend contract assertions for the three row-level fields.

## Files changed
- Klijent/clientapp/src/validation/analyticsResponseSchemas.ts
- Klijent/clientapp/src/validation/__tests__/analyticsResponseSchemas.spec.ts
- Infrastructure/Services/Caching/IAnalyticsCacheService.cs
- Api.Tests/AnalyticsShoeTypeSalesIntegrationTests.cs
- .ai/runs/2026-10-04-shoe-type-production-contract-drift-evidence.md

## Validation run
- Production browser/API inspection -> pass: endpoint HTTP 200; 14 rows; `meta.success=true`; response-level `dataQuality` and `decisionReadiness` present; row-level quality presentation fields missing.
- `npm run typecheck` in `Klijent/clientapp` -> pass.
- `npm run test:run -- src/validation/__tests__/analyticsResponseSchemas.spec.ts` -> pass, 21/21.
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --filter FullyQualifiedName~AnalyticsShoeTypeSalesUnitTests` -> pass, 7/7.
- Backend compile completed successfully as part of the focused `dotnet test` run.
- Focused backend test run had 1 unrelated pre-existing failure: `PreNivelacijaPriority_SeparatesStoreGrainCacheEntries` still asserts cache key `v10` while current main code is already `v11`; 21 passed and 11 skipped in that run.
- Deployed API runtime version observed: Render commit `02f9915887f99115348bd241590da581dafee45d`, build `2026-10-04T16:56:14Z`.

## Validation not run
- Post-deploy browser verification -> not run yet; requires the frontend/backend deployment to complete.
- Full backend suite -> not run; the focused cache-contract command includes a known stale assertion unrelated to this patch.

## Documentation impact
- Existing planning/audit coverage exists in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` (RQ511 runtime schema coverage) and `docs/qa/SHOE_TYPE_CROSS_SCREEN_AUDIT_2026-09-29.md`. This was a direct production contract-drift repair, so no new queue prompt was needed.

## What was missed
- The Render deployment still needs to pick up the cache-key bump. Until then, the frontend compatibility fix is the immediate protection.
- The pre-existing `v10` pre-nivelacija test assertion remains open and should be synchronized with the already-current `v11` implementation.

## Risks
- Row-level margin badges can use their existing unknown/estimated visual fallback when an older payload omits row-level presentation fields; response-level backend trust and recommendation gates remain authoritative.
- Cache key `v5` causes one cold recomputation for each Shoe Type context after API deployment.

## Next
- Deploy API and frontend, then reload the supplied URL and verify rows render with `meta.dataQualityStatus=critical` rather than the generic unavailable state.
- After deploy, re-fetch `/api/runtime/version` and the Shoe Type endpoint; confirm row-level margin fields are present and the frontend no longer reports schema validation failure.
