Task ID: RQ184
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-08
Agent/tool: Codex
Delivery target: main
Working branch / PR: main via local merge `164012c85102383c9c15ecd785b710212776c866` from `codex/rq184-velocity-divisor-20260908` / no PR
Main commit SHA: 164012c85102383c9c15ecd785b710212776c866
Main verification: pending - local merge complete; verify `origin/main` after push
Evidence state: pending

## What was done

- Promoted RQ184 on explicit user instruction after the canonical queue reported no current READY prompt, then claimed it with a local lock.
- Confirmed the inventory-list cached endpoint used `UtcNow.AddDays(-30)` but divided sales by fixed `30m`, with no exclusive upper bound.
- Added a shared owner-scope helper that calculates units/day from actual elapsed UTC days, enforced the half-open `[start, end)` sales window, and reused one captured end timestamp for sales and journal reads.
- Added regression tests for 30-day and 10-day windows, zero-duration/reversed windows and negative sales evidence.
- Closed RQ184 as DONE after local proof and local merge commit `164012c85102383c9c15ecd785b710212776c866`; remote verification is pending the push.

## Files changed

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api.Tests/CachedInventoryVelocityTests.cs`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `.ai/runs/2026-09-08-RQ184-velocity-divisor-evidence.md`

## Validation run

- `git diff --check` -> pass; only LF/CRLF normalization notices were reported.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~CachedInventoryVelocityTests|FullyQualifiedName~InventoryListEndpointIntegrationTests"` -> pass, 13/13.
- `node scripts/check-agent-instructions.mjs --self-test` -> pass.
- `node scripts/check-agent-instructions.mjs` -> pass, 8 canonical files checked.
- `node scripts/check-prompt-queues.mjs --self-test` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass, 403 tasks.
- `node scripts/check-planning-architecture.mjs --self-test` -> pass.
- `node scripts/check-planning-architecture.mjs` -> pass, 78 planning tasks checked.
- `dotnet build Api/Api.csproj --no-restore --configuration Release --nologo` -> pass, 0 errors; 95 existing analyzer warnings were reported.

## Validation not run

- Full repository test suite -> not run; the changed backend calculation and its nearest integration path were covered by the focused 13-test selection.
- Live database/import/refresh/browser/deployed proof -> not run; external/runtime state is unavailable or outside this bounded prompt.
- Frontend analytics guardrails/build -> not run; no frontend files or frontend contracts changed.

## Documentation impact

- Updated the canonical RQ queue status, promotion history, completion note and analytics safety gate.
- Added this durable evidence log.

## What was missed

- Active-selling-days versus calendar-days semantics remain RQ185 scope.
- Other velocity producers and live provider freshness remain outside this prompt.

## Risks

- The fixed 30-day inventory-list window still intentionally covers calendar days; only its divisor now follows the captured elapsed duration.
- Existing unrelated analyzer warnings remain in the repository.

## Next

- None for RQ184; keep RQ185 WAITING until explicit owner promotion.
