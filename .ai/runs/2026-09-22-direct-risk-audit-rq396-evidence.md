Task ID: direct-risk-audit-rq396
Queue: direct-user-request
Date: 2026-09-22
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: d30822d9110ce1afba23453e9683264ddafd928d
Main verification: passed - local `main` and `origin/main` both resolve to `d30822d9110ce1afba23453e9683264ddafd928d`; `origin/main` contains the implementation.
Evidence state: synchronized

## What was done

- Re-audited the latest Color analytics commits against the active RQ395/RQ396 contracts and analytics trust invariants.
- Replaced the Color raw `IMemoryCache` entry with the shared analytics cache service, canonical key dimensions and explicit five-minute TTL/three-minute stale policy.
- Registered Color in core cache invalidation and added the same family to the data-quality health refresh path, covering import and refresh invalidation of all period/store/season/scope variants.
- Added source refresh timestamp, cache generation timestamp, provider lineage and fail-closed stale handling. A cache payload without metadata is treated as stale/degraded; a payload without the metadata object is rejected before it can reach the frontend.
- Fixed serializer casing so cache misses and cache hits preserve the camelCase response contract required by the frontend schema.
- Updated the stale frontend Color contract fixture with the required RQ395 meta, lineage, aggregate and data-quality evidence fields.
- Added focused regression coverage for cache dimensions, policy aliases, invalidation family, fresh/stale/missing metadata and safe payload shape.

## Files changed

- `Api/Endpoints/AllEndpoints.cs`
- `Api/Models/ColorSalesStatsCacheEntry.cs`
- `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`
- `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`
- `Workers/AnalyticsDataQualityHealthWorker.cs`
- `Api.Tests/AnalyticsCacheFreshnessTests.cs`
- `Api.Tests/AnalyticsDataQualityHealthWorkerTests.cs`
- `Api.Tests/AnalyticsScreenCacheKeyContractTests.cs`
- `Api.Tests/ColorSalesStatsSafeErrorTests.cs`
- `Klijent/clientapp/src/services/__tests__/analyticsApi.contract.spec.ts`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-09-22-direct-risk-audit-rq396-evidence.md`

## Validation run

- `dotnet test .\Api.Tests\Api.Tests.csproj --no-restore --filter "FullyQualifiedName~AnalyticsCacheFreshnessTests|FullyQualifiedName~AnalyticsScreenCacheKeyContractTests|FullyQualifiedName~ColorSalesStatsSafeErrorTests|FullyQualifiedName~AnalyticsDataQualityHealthWorkerTests" --logger "console;verbosity=minimal"` -> pass, 26/26.
- `npm run test -- --run src/pages/__tests__/ColorSalesStatsPage.spec.tsx src/services/__tests__/analyticsApi.contract.spec.ts` -> first run failed because the existing Color fixture predated the strict RQ395 schema; fixture was repaired, rerun passed 28/28.
- `npm run check:analytics-guardrails` -> pass; encoding clean, guardrail self-test passed, 0 new violations over 51 known baseline findings, typecheck passed.
- `dotnet build .\Api\Api.csproj --no-restore -v:q` -> pass; 0 errors, existing analyzer warnings remain.
- `git diff --check` -> pass.
- `node scripts/check-agent-instructions.mjs --self-test` and `node scripts/check-agent-instructions.mjs` -> pass.
- `node scripts/check-prompt-queues.mjs --self-test` and `node scripts/check-prompt-queues.mjs` -> pass; 544 tasks checked.
- `node scripts/check-planning-architecture.mjs --self-test` and `node scripts/check-planning-architecture.mjs` -> pass; 78 new planning tasks checked.
- `git push origin main` -> pass; local `main` and `origin/main` verified at `d30822d9110ce1afba23453e9683264ddafd928d`.

## Validation not run

- Full backend/frontend suites, live API/provider/database replay, browser smoke and remote CI inspection were not run; this was a focused same-owner cache/contract repair and no live environment proof was available.

## Documentation impact

- Updated the active analytics queue and `MASTER_ROADMAP.md` to mark RQ396 DONE with the verified implementation SHA and run-log backlink. No new prompt was promoted; RQ397-RQ400 remain waiting under their declared boundaries.

## What was missed

- Generic Color detail trust/provenance, canonical identity, source/metric provenance and authoritative decision score remain RQ397-RQ400.
- Shared user-facing English/localization follow-ups remain routed to RQ306/RQ325.
- No live/deployed dataset replay or remote CI result was inspected.

## Risks

- Existing repository analyzer warnings and known frontend guardrail baseline findings remain; no new guardrail violations were introduced.
- Cache freshness still depends on the refresh-status service when a source refresh timestamp exists; unavailable refresh status is represented as unknown rather than invented.
- No production data, schema or secrets were changed.

## Next

- RQ397 - Align Color generic detail with row trust and provenance; promote only after dependency/collision refresh under the queue protocol.
