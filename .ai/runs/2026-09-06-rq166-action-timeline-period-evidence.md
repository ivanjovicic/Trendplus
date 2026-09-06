# Run log

Task ID: RQ166
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-06
Agent/tool: local-session-ivan / Composer
Delivery target: main
Working branch / PR: main
Main commit SHA: b930c4a2a0f5d6f42d2e46451d3cb96f7fbcb9f3
Main verification: origin/main contains b930c4a2 (81f8d6fa44ace95f3f1c0e9262a2998132727f46)
Evidence state: synchronized

## What was done

Fail-closed reversed action-timeline periods instead of silently swapping `from`/`to`.

- `AnalyticsActionTimelineFilterProjection` returns `invalid_period` with requested dates preserved.
- `BuildProductDecisionTimelineFilterAsync` and timeline export no longer swap; invalid periods get error meta and `DecisionTimelineExportProjection.Error`.
- Equal from/to remains a valid inclusive one-day window.

## Files changed

- `Infrastructure/Services/Analytics/AnalyticsActionTimelineFilterProjection.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api.Tests/AnalyticsActionTimelineFilterProjectionTests.cs`
- `Api.Tests/DecisionTimelineExportProjectionTests.cs`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `.ai/runs/2026-09-06-rq166-action-timeline-period-evidence.md`

## Validation run

- `dotnet test ... AnalyticsActionTimelineFilterProjectionTests|DecisionTimelineExportProjectionTests` — Passed 14 / Failed 0
- `git diff --check` — pass
- `node scripts/check-prompt-queues.mjs` — pass (371 tasks)

## Validation not run

- Full backend suite
- Frontend timeline UI messaging

## Documentation impact

- Queue RQ166 → DONE; RQ167 promoted to READY

## What was missed

- No HTTP integration test against live analytics DB for the endpoint path

## Risks

- Clients that relied on silent swap will now see invalid-period errors (intended)

## Next

- RQ167 READY: keep failed KPI payloads unavailable
