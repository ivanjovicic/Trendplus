# Run log

Task ID: RQ163
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-06
Agent/tool: local-session-ivan / Composer
Delivery target: main
Working branch / PR: main
Main commit SHA: 27d2a1ecdb12a17be4abf9495dfa3a845a6769fa
Main verification: origin/main = fad3862558bfad5f354a5481cf4480656a7077c6 contains 27d2a1ec
Evidence state: synchronized

## What was done

Preserved explicit supplier post-nivelacija observation state so unmatched left-joins no longer become measured zero for dead-stock, ratios, confidence or trusted recommendations.

- Migration `018` keeps `post_qty`/`post_revenue` nullable and requires `has_post_signal` for dead-stock denominators.
- Live supplier SQL adds `has_post_signal` / `post_signal_coverage`, gates recommendations to `REVIEW_QUALITY` when coverage `< 1`, and scales confidence by coverage.
- Precomputed path selects `post_signal_coverage` from decision-score MVs.
- `BuildScorecardTrustMetadata` blocks `recommendationAllowed` when any row has incomplete post coverage and exposes `missing_post_observation`.
- Article/history/category paths carry `has_post_signal` and do not treat absent post as markdown-dependent evidence.
- `ArticleDecisionItem.HasPostSignal` distinguishes measured post zero from missing observation.

## Files changed

- `Database/Migrations/018_AddSupplierDecisionHubViews.sql`
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
- `Api.Tests/SupplierDecisionHubContractTests.cs`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `.ai/runs/2026-09-06-rq163-post-observation-evidence.md`

## Validation run

- `git diff --check` — pass
- `node scripts/check-prompt-queues.mjs` — pass (371 tasks)
- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SupplierDecisionHubContractTests|FullyQualifiedName~SupplierDecisionSchemaSqlTests"` — Passed 44 / Failed 0

## Validation not run

- Full `dotnet test` suite — not required for this scoped SQL/contract change
- Live DB refresh / browser proof — remains with RQ140 / STAB follow-up
- Frontend analytics guardrails — no frontend product logic changed

## Documentation impact

- Queue RQ163 → DONE; RQ164 promoted to READY
- No separate architecture doc rewrite; contract locked in schema/endpoint tests

## What was missed

- Materialized views still need a successful analytics refresh/recreate for live DBs to pick up migration `018` dead-stock denominator change
- Precomputed markdown-dependency cache may still coalesce absent post until refresh rebuilds from updated views

## Risks

- Environments that have not re-applied view SQL keep the old dead-stock false-zero until refresh
- Aggregate post sums still use SQL null-as-absent contribution; trust is gated by coverage rather than inventing post totals

## Next

- RQ164 READY: gate pre-nivelacija margin on positive cost evidence
