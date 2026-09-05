Task ID: analytics-trust-parity-2026-09-05
Queue: docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md (Q83) + direct-user-request parity scope
Date: 2026-09-05
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: 7b19448ce393538988a99aa736f21a6b43315fac
Main verification: current main and origin/main both resolve to 7b19448ce393538988a99aa736f21a6b43315fac; implementation commit a84d8a42974e4228840ef07e3f0e9f5d03a4068c is an ancestor
Evidence state: synchronized

## What was done
- Audited the `analyticsIntelligenceDerived` path and made the backend/legacy analytics response the production decision source. Frontend-derived signals no longer override backend recommendations, and missing/non-finite inputs are not converted into actionable zeros.
- Completed the raw vendor pre/post nivelacija contract: SQL preserves missing windows as `NULL`, exposes semantic revenue/quantity baseline and change fields, and labels evidence state/reasons. Quantity change is not silently used as revenue change.
- Hardened the pre-nivelacija endpoint and scoring contract: incomplete price evidence is explicit, recommendation status is backend-owned, and query failure returns a structured user-readable error instead of a raw 500.
- Enforced table/detail/export parity for supplier nivelacija and pre-nivelacija surfaces. Untrusted revenue, confidence and reliability remain unavailable in every representation; actions are hidden when `recommendationAllowed=false`.
- Added regression coverage for empty results, nullable evidence, true zero semantics, missing denominator/baseline, non-finite derived values, degraded/fallback metadata, backend ownership and export/detail parity in the changed surfaces.

## Files changed
- `Api/Endpoints/AllEndpoints.cs`
- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs`
- `Api/Models/VendorSalesNivelacijaModels.cs`
- `Api/Models/PreNivelacijaPriorityModels.cs`
- `Api/Services/PreNivelacijaScoringService.cs`
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`
- `Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql`
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
- `Api.Tests/PreNivelacijaScoringServiceTests.cs`
- `Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts`
- `Klijent/clientapp/src/services/vendorSalesNivelacijaApi.ts`
- `Klijent/clientapp/src/types/preNivelacija.ts`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx`
- `Klijent/clientapp/src/services/__tests__/analyticsIntelligenceDerived.spec.ts`
- `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`

## Validation run
- `npm run typecheck` from `Klijent/clientapp` -> pass.
- Focused frontend tests for pre-nivelacija, supplier nivelacija, derived intelligence and table state -> pass, 3 files / 16 tests.
- `npm run check:analytics-guardrails` -> pass, including encoding and analytics guardrails.
- `npm run build` from `Klijent/clientapp` -> pass; Vite built 2599 modules, with existing large-chunk warnings only.
- `dotnet build Api/Api.csproj --no-restore --verbosity:minimal` -> pass, 0 errors; existing warnings only.
- `dotnet build Trendplus2.sln --no-restore --verbosity:minimal` -> pass, 0 errors; existing Microsoft JavaScript SDK warnings only.
- Focused backend filter (`SupplierDecisionSchemaSqlTests`, `PreNivelacijaScoringServiceTests`, `TrendScoringServiceTests`, `AnalyticsDataQualityConsistencyTests`, `PreNivelacijaQueryFailureMetaTests`, `AnalyticsResponseMetaContractTests`) -> pass, 77/77.
- Final narrowed backend filter for changed pre-nivelacija/SQL/query-failure behavior -> pass, 33/33.
- `git diff --check` -> pass; only Git line-ending normalization warnings were emitted.
- `dotnet ef migrations list --project .\Infrastructure\Infrastructure.csproj --startup-project .\Api\Api.csproj --context AnalyticsDbContext` -> build passed and migrations were enumerated through `20260526171949_AddAnalyticsActionOutcomeTracking`.
- Live API probe against `http://localhost:8080`: `/health` 200, `/ready` 503, `/api/analytics/refresh-status` 200 with `unknown`, vendor pre/post 200 structured empty/degraded response, pre-nivelacija 200 structured error response, missing route 404. No raw 500 remained for the exercised pre-nivelacija failure path.
- Headless browser smoke with Puppeteer navigated all requested analytics routes plus pre-nivelacija. No JavaScript `pageerror` was observed in the captured run; route documents loaded, while API readiness/rate-limit failures were visible as console/network errors rather than hidden data.

## Validation not run
- `npm run test:analytics` -> not completed; the non-watch suite produced no output for over 60 seconds and was terminated once. It was not treated as passing.
- A clean browser console under a healthy backend -> not proven; local API emitted 503 readiness and 429 rate-limit responses, plus degraded optional-resource failures.
- Applied/pending migration state against the live analytics database -> not proven; Neon returned PostgreSQL `28P01 password authentication failed for user neondb_owner`.
- Full live refresh-worker success -> not proven; the worker could not authenticate to Neon, and the web process reported no successful refresh.
- CUA browser-console capture -> not run successfully; the CUA runtime failed to initialize with `failed to write kernel assets: system cannot find path specified`, so Puppeteer was used as the available local browser proof.

## Documentation impact
- Updated `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md` for Q83 completion state and evidence backlink.
- The Q83 scope was repaired narrowly because the requested full pre/post and parity outcome required the connected endpoint/frontend contract; unrelated analytics routes were not refactored.

## What was missed
- Production/live database schema application and successful refresh cannot be verified until valid Neon credentials and a healthy database are available.
- A clean zero-console-error browser run is pending a healthy backend without readiness/rate-limit failures.
- Full cross-route export/report runtime parity was covered by the existing shared contract and focused parity tests, but was not fully exercised against live data in this environment.

## Risks
- The compatibility DTO still contains legacy numeric fields for older consumers; trust-sensitive consumers must use the semantic nullable fields and metadata, which the changed UI does.
- The current delivery is `PARTIAL`, not `DONE`, because external DB/runtime proof is unavailable even though static, focused and build validation passed.

## Next
- Restore valid analytics DB credentials/run access, apply or verify the affected view/migration contract, run the refresh worker, then repeat the all-route browser smoke and export/report parity checks with real data.
