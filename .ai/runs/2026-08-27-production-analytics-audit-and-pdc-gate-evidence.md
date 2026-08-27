Task ID: production-analytics-audit-and-pdc-gate
Queue: direct-user-request
Date: 2026-08-27
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: 785b88b83572b89e5b5b913503aff510bac5e062
Main verification: passed - after `git push origin main`, `git fetch origin main` and `git merge-base --is-ancestor 785b88b83572b89e5b5b913503aff510bac5e062 origin/main` returned exit 0; `origin/main` resolved to the same SHA.
Evidence state: synchronized

## What was done

- Audited the configured production Render API without mutating production data. It is healthy/reachable but deployed behind current main, has no dedicated analytics-refresh worker, reports unknown freshness, and exposes an unsafe PDC actionability result for insufficient/stale evidence.
- Added a backend-owned Product Decision Center actionability gate. A source-blocked, `INSUFFICIENT_DATA`, `FIX_DATA`, critical/insufficient/error/failed data-quality, stale, critical, or unknown-freshness row is now blocked and cannot expose decision confidence, expected impact, or an impact window.
- Applied the same fail-closed contract to Decision Board aggregation and to Executive Board compatibility presentation, so a legacy numeric diagnostic percentage cannot be shown as recommendation confidence when the row/card is blocked.
- Added focused backend/frontend regressions for stale high-score PDC rows and blocked aggregate cards carrying a legacy diagnostic confidence number.
- Added the dated production audit plus `STAB16` (BLOCKED on provider/read-only audit authority) and `RQ128` (WAITING for the STAB exact-deploy proof). The master router explicitly keeps both non-READY.

Analytics safety gate:
- Surface: Product Decision Center, Decision Board, Executive Board compatibility view, production release evidence.
- Source of truth: backend Product Decision profile; Board and frontend preserve the backend gate.
- Contract changed: yes - unsafe rows now return explicit blocked actionability with null decision confidence/expected impact instead of a mixed actionable payload.
- Unit/denominator: confidence is an integer decision percentage only when actionable; `expectedImpactRsd` remains RSD and is null when the decision is blocked. No denominator/formula was changed.
- True zero: unchanged; a real zero remains distinct from null unavailable impact.
- Missing/unknown and freshness/fallback: unknown/stale/critical evidence fails closed and remains labelled through `product_recommendation_blocked`.
- Affected surfaces: PDC API/profile/detail/action payload, Decision Board aggregate/ranking, Executive Board fallback/aggregate rendering.
- Stop condition: no for the repository patch; live deployment/database proof is separately blocked and routed to STAB16.

## Files changed

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/DecisionBoardEndpoints.cs`
- `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`
- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`
- `Api.Tests/DecisionBoardEndpointsTests.cs`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`
- `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md`
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`

## Validation run

- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AnalyticsProductDecisionConfidenceTests|FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests|FullyQualifiedName~DecisionBoardEndpointsTests|FullyQualifiedName~DecisionBoardAggregationContractTests" --no-restore --logger "console;verbosity=minimal"` -> pass before the final Board-only extension: 52 passed.
- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DecisionBoardEndpointsTests" --no-build --no-restore --logger "console;verbosity=minimal"` -> pass: 36 passed after the final Board extension.
- `npm run test -- --run src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts` -> pass: 13 passed.
- `git diff --check` -> pass.
- `node scripts/check-agent-instructions.mjs --self-test` -> pass.
- `node scripts/check-agent-instructions.mjs` -> pass (8 canonical files).
- `node scripts/check-prompt-queues.mjs --self-test` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass (289 tasks).
- `node scripts/check-planning-architecture.mjs --self-test` -> pass.
- `node scripts/check-planning-architecture.mjs` -> pass (75 planning tasks).
- production API audit of `/health`, `/ready`, `/api/runtime/version`, refresh status, PDC, Decision Board, inventory, data quality, and supplier report -> completed; observations are recorded in the dated audit.

## Validation not run

- Full backend solution suite and full frontend analytics suite -> not run; focused backend/frontend tests cover the changed PDC/Board contract, while no shared build/routing/DI surface changed.
- Read-only production database reconciliation -> not run because `TRENDPLUS_AUDIT_DATABASE_URL` is not present locally.
- Exact-current-main production deploy and dedicated worker smoke -> not run because provider deployment authority is unavailable; routed to `STAB16`.
- Interactive browser production smoke -> not run because the in-app browser failed while initializing browser assets; a static bundle fetch is not considered render proof.

## Documentation impact

- Added `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md` with observed payload facts, a conservative 3.5/10 evidence score, limitations, and required proof.
- Added `STAB16` and `RQ128` with the required queue sections and accurate `BLOCKED`/`WAITING` dependencies; updated the master routing truth.

## What was missed

- No source-table reconciliation or production deployment was performed because neither requires nor grants provider/DB credentials through this repository session.
- The deployed runtime remains at `d9c4d0a8cd893c8e7cb330f47e41e92843fa9875`; it does not yet contain this repair.

## Risks

- Until STAB16 deploys this main SHA and proves a dedicated worker refresh, production users can still see the old PDC false-actionability behavior and unknown freshness.
- The existing `NU1504` duplicate `Microsoft.Data.SqlClient` reference warning remains unrelated to this scoped contract repair.

## Next

- `STAB16` - restore exact-deploy parity and dedicated analytics-refresh worker, then complete the read-only database reconciliation.
- `RQ128` - prove PDC/Decision Board actionability parity against that exact deployed SHA.
