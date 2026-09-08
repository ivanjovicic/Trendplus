Task ID: RQ189
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-08
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/rq189-new-demand-state-20260908 / local merge
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done
- RQ189 was explicitly promoted by the owner, claimed locally, and implemented within the Analytics owner boundary.
- Replaced the SQL `1.0` demand-acceleration sentinel with `NULL` when no positive prior 7-day baseline exists.
- Added the backend-owned `demand_state` enum: `NEW_DEMAND`, `NO_BASELINE`, `ACCELERATING`, `DECELERATING`, or `STABLE`.
- Propagated nullable acceleration and demand state through the API DTO/query, React API types, derived aggregation, and Intelligence Snapshot UI. New demand and no-baseline signals render as distinct Serbian copy and do not discard aging data merely because acceleration is unavailable.
- Changed numeric minimum filtering and ordering to preserve unknown acceleration semantics instead of treating `NULL` as zero.

Analytics safety gate:
- Source of truth: the backend SQL demand-signal view owns acceleration/state; the API exposes it; React maps and renders it.
- Contract changed: yes; acceleration is nullable and `demandState` is explicit. No fake zero is introduced.
- Units: acceleration is a relative ratio; it is `NULL` without a positive prior baseline.
- True zero: a positive prior with unchanged velocity remains measured `0` and `STABLE`.
- Missing/unknown: zero prior yields unavailable acceleration with `NEW_DEMAND` when current velocity is positive, otherwise `NO_BASELINE`.
- Freshness: unchanged; the existing nightly intelligence cache/refresh remains the operational freshness path.
- Affected surfaces: demand API, Insight Studio demand data, dashboard intelligence snapshot, and derived aging aggregation.
- Parity proof: backend SQL smoke coverage and focused React derived/panel tests cover the contract.
- Stop gate: not triggered.

## Files changed
- Api.Tests/AnalyticsIntelligenceSmokeTests.cs
- Api/Endpoints/AnalyticsIntelligenceEndpoints.cs
- Database/Analytics/Intelligence/021_product_demand_signals_v1.sql
- Klijent/clientapp/src/components/dashboard/IntelligenceSnapshotPanel.tsx
- Klijent/clientapp/src/components/dashboard/IntelligenceSnapshotPanel.spec.tsx
- Klijent/clientapp/src/services/analyticsIntelligenceApi.ts
- Klijent/clientapp/src/services/analyticsIntelligenceDerived.ts
- Klijent/clientapp/src/services/__tests__/analyticsIntelligenceDerived.spec.ts
- MASTER_ROADMAP.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
- .ai/runs/2026-09-08-RQ189-new-demand-state-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --nologo --filter "FullyQualifiedName~AnalyticsIntelligenceSmokeTests"` -> pass, 5/5.
- `npm run test -- --run src/services/__tests__/analyticsIntelligenceDerived.spec.ts src/components/dashboard/IntelligenceSnapshotPanel.spec.tsx` -> pass, 13/13.
- `npm run check:analytics-guardrails` -> pass.
- `npm run build` -> pass; Vite completed successfully with the existing large `recharts` chunk warning.
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore --nologo` -> pass, 0 errors and 157 existing warnings.
- `git diff --check` -> pass.
- Governance checks after final queue synchronization: pending.

## Validation not run
- Full backend and frontend suites -> not run; the focused tests and release build cover the changed contracts, while unrelated full-suite execution was outside the narrow proof.
- Live production database refresh, browser/provider integration, and remote CI result inspection -> not run; no production mutation or remote CI execution was authorized or required for this local delivery.

## Documentation impact
- Synchronized the canonical analytics queue and `MASTER_ROADMAP.md` with the RQ189 promotion, completion, and evidence backlink.
- Added this durable run log; no separate product documentation was required.

## What was missed
- RQ190 remains WAITING on RQ141 and was not promoted because its dependency is not complete.
- No live cache refresh was performed; the normal operational refresh remains required after deployment of the SQL view.

## Risks
- Existing release-build analyzer/nullable warnings remain outside this scoped change.
- No live provider, browser, production database, or remote CI proof was performed.

## Next
- none for RQ189; the queue returns to no READY prompt.
