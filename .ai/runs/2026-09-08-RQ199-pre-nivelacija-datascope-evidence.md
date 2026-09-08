Task ID: RQ199
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-08
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/rq199-pre-nivelacija-datascope-20260908 / local merge
Main commit SHA: pending
Main verification: pending until local merge and push
Evidence state: pending

## What was done

- Promoted and claimed RQ199 after the canonical queue reported no current READY prompt.
- Added a normalized `dataScope` query parameter to the Pre-nivelacija priority endpoint.
- Applied the same scope to article membership, sale headers and nivelacija events so imported and existing populations are not mixed.
- Added scope to both Pre-nivelacija cache-key builders.
- Added focused tests for canonical scope normalization and cache-entry isolation.

## Files changed

- `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs`
- `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`
- `Api.Tests/PreNivelacijaMarginEvidenceTests.cs`
- `Api.Tests/AnalyticsScreenCacheKeyContractTests.cs`
- `MASTER_ROADMAP.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `.ai/runs/2026-09-08-RQ199-pre-nivelacija-datascope-evidence.md`

## Validation run

- `git diff --check` -> pass.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~PreNivelacijaMarginEvidenceTests|FullyQualifiedName~AnalyticsScreenCacheKeyContractTests" --verbosity minimal` -> pass (21 tests).
- `dotnet build Api/Api.csproj --configuration Release --no-restore --nologo --verbosity minimal` -> pass (95 existing warnings, 0 errors).
- `node scripts/check-agent-instructions.mjs` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pending after final delivery.
- `node scripts/check-planning-architecture.mjs` -> pending after final delivery.

## Validation not run

- Full frontend/backend suites -> not run; this is a scoped backend endpoint/cache contract change.
- Live API, database, provider, browser and remote CI proof -> not run; unavailable/not required for this local contract change.

## Documentation impact

- Updated the RQ queue and `MASTER_ROADMAP.md` with the explicit promotion, scope repair, completion and delivery evidence.

## What was missed

- No live mixed-origin dataset proof was run.

## Risks

- Scoped filtering depends on the established `DataOrigin` vocabulary; legacy unexpected values are not treated as either imported or existing.

## Next

- Complete local merge/push, synchronize the exact delivered SHA in this run log and queue note, then leave the queue at no READY prompt.
