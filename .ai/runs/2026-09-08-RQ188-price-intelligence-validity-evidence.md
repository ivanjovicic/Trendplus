Task ID: RQ188
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-08
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/rq188-price-intelligence-validity-20260908 / local merge
Main commit SHA: pending
Main verification: pending
Evidence state: pending
Queue status: DONE

## What was done
- Promoted and claimed the dependency-safe `RQ188` price-intelligence validity prompt after `RQ187`; no higher-priority queue had a current READY prompt.
- Changed `Database/Analytics/Intelligence/023_price_intelligence_v1.sql` so missing `FirstSalePrice` no longer falls back to `SalePrice`, and invalid/non-positive list prices produce `NULL` `discount_depth` instead of measured `0`.
- Updated the analytics API record and reader to preserve nullable `discount_depth`, and made price-depth sorting put unavailable values last.
- Updated the React API type to `number | null`; existing intelligence derivations already reject unavailable discount depth, and the dashboard formatter renders it as `n/a`.
- Added backend smoke coverage for invalid list-price discount output and a React regression for excluding unavailable discount depth from derived category intelligence.

Analytics safety gate:
- Source of truth: `analytics_intel.vw_price_intelligence_v1` / materialized cache, the backend `PriceIntelligenceItem` contract, and the existing React intelligence derivations.
- Contract changed: yes, `discountDepth` is now nullable and missing list-price provenance is preserved; no endpoint shape removal or unrelated pricing/ML contract change.
- Unit/denominator: discount depth remains a ratio in `[0,1]` when list price is valid; `NULL` means unavailable, not zero.
- True zero case: valid positive list price equal to net price yields measured `0`.
- Missing/unknown case: missing or non-positive list price yields `NULL` discount depth; derived analytics skip it and the dashboard displays `n/a`.
- No-baseline case: not applicable to this metric.
- Freshness/fallback case: unchanged; the existing nightly worker refreshes the intelligence materialized cache after the SQL view is applied.
- Surfaces affected: price-intelligence API, Insight Studio/dashboard price snapshot and derived category/price-sensitivity analytics.
- Tests proving parity: backend view smoke and React derived-signal regression; no new table/detail/export/action surface consumes this field.
- Stop condition hit: no.

## Files changed
- `Database/Analytics/Intelligence/023_price_intelligence_v1.sql`
- `Api/Endpoints/AnalyticsIntelligenceEndpoints.cs`
- `Api.Tests/AnalyticsIntelligenceSmokeTests.cs`
- `Klijent/clientapp/src/services/analyticsIntelligenceApi.ts`
- `Klijent/clientapp/src/services/__tests__/analyticsIntelligenceDerived.spec.ts`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-09-08-RQ188-price-intelligence-validity-evidence.md`

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~AnalyticsIntelligenceSmokeTests"` -> pass, 4/4.
- `npm run test -- --run src/services/__tests__/analyticsIntelligenceDerived.spec.ts` (from `Klijent/clientapp`) -> pass, 12/12.
- `npm run check:analytics-guardrails` (from `Klijent/clientapp`) -> pass: encoding, analytics guardrails and typecheck.
- `npm run build` (from `Klijent/clientapp`) -> pass; TypeScript and Vite production build completed. Existing chunk-size warnings remain.
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore --nologo` -> pass, 0 errors; existing warnings remain.
- `node scripts/check-agent-instructions.mjs`, `node scripts/check-prompt-queues.mjs` and `node scripts/check-planning-architecture.mjs` -> pass before final queue synchronization; final queue check is repeated before delivery.
- `git diff --check` -> pass.

## Validation not run
- Full backend/frontend test suites -> not run; the changed contract and nearest producers/consumers were covered by focused tests and production builds.
- Live production browser/provider verification and remote CI -> not run; no live-data or remote-check access was required for this local contract repair.

## Documentation impact
- Synchronized the canonical analytics queue and `MASTER_ROADMAP.md` with the explicit RQ188 promotion and completion. Added this durable run log.

## What was missed
- Pricing/ML owners were not changed; the prompt was safely narrowed to the existing analytics SQL/API/React consumers.
- Materialized cache contents require the normal startup/nightly refresh path after deployment; this local run did not mutate production data.
- RQ189 and later prompts remain WAITING and were not claimed.

## Risks
- Existing Release-build analyzer/nullable warnings remain outside this task; no unrelated warning cleanup was attempted.
- No live provider/browser proof was performed, so deployment-specific cache refresh behavior remains an operational follow-up rather than local runtime evidence.

## Next
- None for RQ188; the queue returns to no current READY prompt.
