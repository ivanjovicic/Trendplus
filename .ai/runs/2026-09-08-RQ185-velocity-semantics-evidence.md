# RQ185 evidence

Task ID: RQ185
Queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
Date: 2026-09-08
Agent/tool: Codex
Delivery target: `main`
Working branch / PR: `codex/rq185-velocity-semantics-20260908` / local merge, no PR
Main commit SHA: pending
Main verification: pending
Evidence state: pending

## What was done

RQ185 was explicitly promoted because the queue had no current READY prompt, then claimed with the local task lock. The affected dashboard Quick Insights and Top Products Advanced producers now calculate velocity over inclusive calendar days rather than active selling days. The compatible `velocityUnitsPerDay` field is retained, while backend cards, dashboard tables/exports, Analytics Details, Product Decision Center labels and evidence text explicitly state the calendar-day basis.

Known example: 100 units sold on 5 of 30 calendar days now yields 3.33 units/calendar day; a same-date window uses one calendar day.

## Files changed

- `Api/Endpoints/CachedAnalyticsEndpoints.cs` - calendar-day SQL denominators, explicit card/evidence wording and tested formula helper.
- `Api.Tests/CachedInventoryVelocityTests.cs` - intermittent, one-day and invalid-evidence regression coverage.
- `Klijent/clientapp/src/utils/analyticsVelocitySemantics.ts` - shared velocity labels/help text.
- `Klijent/clientapp/src/utils/analyticsVelocitySemantics.spec.ts` - label contract test.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx` - dashboard table, card, export and glossary labels.
- `Klijent/clientapp/src/pages/AnalyticsDetails.tsx` - detail table labels.
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` - decision surface labels.
- `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.tableSystem.spec.tsx` - rendered calendar-day header assertion.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` - promotion, claim and completion record.

## Validation run

- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~CachedInventoryVelocityTests --no-restore` - passed, 6/6.
- `npm run test -- --run src/utils/analyticsVelocitySemantics.spec.ts` - passed, 1/1.
- `npm run test -- --run src/pages/__tests__/AnalyticsDashboard.tableSystem.spec.tsx` - passed, 4/4.
- `npm run check:analytics-guardrails` - passed: encoding, analytics guardrails and TypeScript typecheck.
- `npm run build` - passed; Vite production build completed with existing chunk-size warnings.
- `node scripts/check-agent-instructions.mjs` - passed.
- `node scripts/check-prompt-queues.mjs` - passed, 403 tasks.
- `node scripts/check-planning-architecture.mjs` - passed, 78 planning tasks.
- `git diff --check` - passed.

## Validation not run

- Live PostgreSQL endpoint proof and browser proof were not run; no configured live database proof was required for the focused formula/contract slice.
- Full backend and frontend suites were not run; the narrow changed-surface tests and production build were used.
- Product Decision Center focused Vitest command was started but did not exit after initialization and was cancelled; typecheck and analytics guardrails still passed for that UI change.

## Documentation impact

The queue promotion and completion evidence are recorded here and in the canonical analytics queue. No product methodology document required a separate change because the UI help text and backend contract wording now state the divisor directly.

## What was missed

No separate active-selling-days metric was added; the affected surfaces intentionally standardize on calendar-day velocity. Other metrics such as Insight Studio's `avgVelocityPerSku` were not silently redefined because they are separate contracts and were outside RQ185's evidence/scope.

## Analytics safety gate

- Source of truth: backend SQL/period calculation remains authoritative; the frontend only labels and renders the returned compatible field.
- Contract changed: divisor for the two RQ185 velocity producers is inclusive calendar days; field name remains backward compatible.
- Units: units per calendar day, with explicit labels in cards, tables, exports and decision evidence.
- True zero: zero units remains zero; no non-finite or missing value is converted by this change.
- Missing/empty: existing response and fallback behavior is unchanged; no new fake-zero path was introduced.
- Baseline: not applicable to the direct velocity rate; trend calculation remains separate.
- Freshness/fallback: cache and freshness behavior are unchanged.
- Surfaces/parity: Quick Insights, Top Products Advanced, Analytics Dashboard, Analytics Details and Product Decision Center wording are aligned.
- Stop condition: not hit.

## Risks

The null-date fallback period is derived from the filtered Quick Insights sales span and the existing Top Products period metadata; live database data was not available for runtime parity proof. Existing separate Insight Studio velocity semantics remain a follow-up boundary rather than being changed by inference.

## Next

Remove the local lock before commit, merge the feature branch locally into `main`, push `main`, then replace the pending SHA/verification fields with the exact delivered merge SHA and verify `origin/main` contains it. RQ186 remains WAITING.
