Task ID: BCI15
Queue: docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
Date: 2026-10-08
Agent/tool: cursor-cloud
Delivery target: main
Working branch / PR: cursor/bci15-action-constants-tests-7269 / pending
Main commit SHA: pending
Main verification: pending
Evidence state: pending
Ownership transfer: none

## What was done
- Idle recovery after P-UI-38 Zero-READY re-verified no READY prompt, then applied the Mandatory no-READY ladder.
- Confirmed a concrete repo-local CI gap with no owning prompt: `AnalyticsActionConstantsTests` still asserted pre-`UNRANKED`/`ignored` cardinalities while runtime constants already include those values.
- Registered/claimed `BCI15` and aligned the focused unit tests (membership, counts, validity theories, ClosedStatuses coherence) to the runtime contract without changing production code.

## Files changed
- `Api.Tests/AnalyticsActionConstantsTests.cs`
- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-08-BCI15-evidence.md`

## Validation run
- Pre-fix repro: `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~AnalyticsActionConstantsTests` → 2 failed / 36 passed.
- Post-fix: same filter → **40 passed / 0 failed**.
- `git diff --check` → pass after EOF normalize.

## Validation not run
- Full `Api.Tests` Release suite → not required for this unit-contract slice; remote Analytics Tests workflow will exercise the full suite after delivery.
- Provider/deployed proof → not applicable.

## Documentation impact
- BCI queue Current READY claimed as BCI15; MASTER BCI row updated.

## What was missed
- none known within BCI15 test-contract scope.

## Risks
- Other unrelated backend failures may still exist in the broad suite; this slice only owns the constants cardinality drift proven red on current-main.

## Post-close routing recovery
- pending delivery

## Next
- pending delivery
