Task ID: BCI12
Queue: docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
Date: 2026-09-27
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main implementation SHA: e6913573
Main verification: passed - current `HEAD` and `origin/main` resolve to the evidence-sync commit 2c5d7074; `git merge-base --is-ancestor e6913573 origin/main` passed.
Evidence state: synchronized

## What was done
- Confirmed the production Color cache-key builder intentionally emits `color-sales-stats:v5`.
- Confirmed the key remains sensitive to from/to dates, store, season and data scope.
- Updated the stale focused contract assertion from v4 to v5 without weakening dimension or uniqueness checks.
- Promoted BCI13 to READY after BCI11 and BCI12 focused deterministic drifts were closed.

## Files changed
- Api.Tests/AnalyticsScreenCacheKeyContractTests.cs
- docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
- MASTER_ROADMAP.md
- .ai/runs/2026-09-27-BCI12-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~AnalyticsScreenCacheKeyContractTests" --verbosity minimal --logger "console;verbosity=minimal"`: pass, 18/18, 0 skipped.
- Build performed as part of the test command: succeeded with pre-existing warnings.
- `git diff --check`: pass.

## Validation not run
- Full backend suite: not rerun; BCI10 already has a fresh red broad result and BCI12 changes only one stale contract assertion.
- Browser/production checks: not applicable to this backend cache-key contract.
- GitHub Actions run `36343166491` for implementation SHA `e6913573da717de16eb7978b0c607d9f349335b0` is `in_progress`; build/certification steps have started, but no remote test result is inferred before completion.

## Documentation impact
- BCI12 completion and evidence are recorded in the owning backend CI queue.
- BCI13 is now the primary READY prompt because its focused dependency gate is satisfied.

## What was missed
- The broad backend suite remains red from unrelated residual families; BCI10 remains PARTIAL.

## Risks
- A fresh broad run may still expose endpoint-host or provider/order-isolation failures owned by BCI13/BCI14.

## Next
- BCI13: repair endpoint test-host wiring without weakening auth contracts.
