# Run log

Task ID: RQ165
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-06
Agent/tool: local-session-ivan / Composer
Delivery target: main
Working branch / PR: main
Main commit SHA: 22cc2ae497a6bdca233a0c3a5cf9eb246531c269
Main verification: origin/main contains 22cc2ae4
Evidence state: synchronized

## What was done

Aligned Data Quality sales intervals and revenue scope across health, top offenders and issues.

- Added `Application.Analytics.DataQualitySalesWindow` with shared `[fromUtc, toExclusiveUtc)` calendar window.
- Top-offender and issues SQL require `datum_prodaje < @salesToExclusiveUtc` (future sales excluded).
- Health `CaptureAsync` uses the same half-open window and sale-header `DataOrigin` for revenue impact; article origin remains for orphan membership.

## Files changed

- `Application/Analytics/DataQualitySalesWindow.cs`
- `Infrastructure/Services/AnalyticsDataQualityHealthService.cs`
- `Application/Analytics/Queries/GetDataQualityIssues/GetDataQualityIssuesHandler.cs`
- `Api.Tests/AnalyticsDataQualityHealthServiceTests.cs`
- `Api.Tests/DataScopeConsistencyContractTests.cs`
- `Api.Tests/DataQualityMissingCostOffenderContractTests.cs`
- `Api.Tests/DataQualitySalesWindowTests.cs`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `.ai/runs/2026-09-06-rq165-data-quality-window-scope-evidence.md`

## Validation run

- `dotnet test ... filter DataQuality/Health/Scope window tests` — Passed 34 / Failed 0
- `git diff --check` — pass
- `node scripts/check-prompt-queues.mjs` — pass (371 tasks)

## Validation not run

- Full postgres DataQualityPostgresIntegrationTests (optional; needs live DB)
- Full backend suite / frontend guardrails

## Documentation impact

- Queue RQ165 → DONE; RQ166 promoted to READY

## What was missed

- Explain-template SQL in migration `026` still documents lower-only bound (historical template, not runtime)

## Risks

- Environments with future-dated sales will drop those rows from DQ revenue (intended)
- Existing-scope health totals may shrink vs article-only filtering when access-header sales sit on existing articles

## Next

- RQ166 READY: reject reversed action-timeline periods
