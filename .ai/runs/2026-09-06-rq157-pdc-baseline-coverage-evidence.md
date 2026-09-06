# RQ157 evidence

Task ID: RQ157
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-06
Agent/tool: local-session-ivan / Cursor
Delivery target: main
Working branch / PR: main
Main commit SHA: (set after push)
Main verification: pending push
Evidence state: synchronized
Queue: claimed-as-RQ157 (was WAITING P0; promoted over RQ156 while RQ155 PARTIAL)

## What was done

Preserved missing Product Decision baseline and coverage evidence so missing denominators cannot become measured zeros or fake +100% growth.

1. `ProductDecisionReasoningHelper.ComputeTrendPct` — missing/zero previous baseline → null (never +100%).
2. PDC builder uses `TryGetValue` + `ComputeTrendPct` instead of defaulting missing previous revenue to 0 then emitting 100%.
3. Null `TrendPct` / `MarginPct` → `INSUFFICIENT_DATA` with `insufficient_history` reason codes (no `?? 0` decision path).
4. `AnalyticsDecisionRecommendationEngine` — null margin/split coverage fail closed; `missing_split_coverage` blocks actionable recommendation; measured zero split remains distinct.

## Files changed

- `Application/Analytics/ProductDecisionReasoningHelper.cs`
- `Application/Analytics/AnalyticsDecisionRecommendationEngine.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs` (PDC builder trend only)
- `Api.Tests/ProductDecisionReasoningHelperTests.cs`
- `Api.Tests/AnalyticsDecisionRecommendationEngineTests.cs`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `.ai/task-locks/RQ157-local-session-ivan.lock.md`
- this evidence file

## Validation run

```text
dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ProductDecisionReasoningHelperTests|FullyQualifiedName~AnalyticsDecisionRecommendationEngineTests|FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests"
→ Passed: 26, Failed: 0
```

## Validation not run

- Full `Api.Tests` suite (unrelated connection/admin failures exist in env)
- Frontend / analytics guardrails (backend-only scope)
- Live DB / browser (owned by STAB16)

## Documentation impact

Queue completion note + this run log. No product docs rewrite.

## What was missed

- No separate integration fixture that asserts missing previous product key yields null TrendPct on a seeded row (covered by unit `ComputeTrendPct` + existing builder seed with present baseline).
- Frontend display of null trend already maps via existing N/A paths; not re-proven here.

## Risks

- Stricter fail-closed may increase `INSUFFICIENT_DATA` / block more PDC and supplier recommendations where trend/margin/coverage were previously coalesced to zero — intentional.
- Measured split coverage of `0` still skips `limited_nivelacija_coverage` (only null is `missing_split_coverage`); that distinction is deliberate.

## Next

- RQ158 READY (null inventory stock evidence).
- RQ155 remains PARTIAL (dashboard unknown trends test harness).
- RQ156 remains WAITING until RQ155 closure or explicit promotion.
