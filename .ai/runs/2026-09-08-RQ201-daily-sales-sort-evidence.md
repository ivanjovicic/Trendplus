Task ID: RQ201
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-08
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/rq201-daily-sales-sort-20260908 / local merge
Main commit SHA: fdbc2a00
Main verification: passed - local `main` and `origin/main` both contain delivered merge `fdbc2a00`; implementation commit `8d7082ce` is contained in `origin/main`.
Evidence state: synchronized

## What was done
- Promoted and claimed RQ201 after the queue reported no current READY prompt.
- Extracted the Daily Sales table sort into the shared `sortDailySalesRows` helper.
- Reused the selected sort order for the trend and shift-mix chart data passed to Recharts.
- Kept MA7 and day-over-day calculations based on the chronological source sequence, then reordered the chart points to match the table without changing those metric semantics.
- Added unit and component regressions covering default date-desc order and a user-selected revenue sort.

## Files changed
- Klijent/clientapp/src/pages/DailySalesStatsPage.tsx
- Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.numericState.spec.ts
- Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx
- MASTER_ROADMAP.md
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md

## Validation run
- `npm run test:run -- --run src/pages/__tests__/DailySalesStatsPage.numericState.spec.ts src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx --reporter=dot` -> pass (12 tests).
- `npm run test:run -- --run src/pages/__tests__/DailySalesStatsPage.spec.tsx src/pages/__tests__/analyticsTrustStateProof.spec.tsx src/pages/__tests__/analyticsIndicatorRegression.spec.ts --reporter=dot` -> pass (19 tests; one pre-existing React act warning).
- `npm run check:analytics-guardrails` -> pass (encoding, analytics guardrails, typecheck).
- `npm run build` -> pass (frontend production build).
- `git diff --check` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass (403 tasks).
- `node scripts/check-planning-architecture.mjs` -> pass (78 new planning tasks checked).
- `git push -u origin codex/rq201-daily-sales-sort-20260908` -> pass.
- `git merge --no-ff codex/rq201-daily-sales-sort-20260908 -m "merge: deliver RQ201 daily sales sort parity"` -> pass; merge `fdbc2a00`.
- `git push origin main` -> pass; `origin/main` advanced to `fdbc2a00`.

## Validation not run
- Full frontend/backend suites, live provider/database/API/browser proof and remote CI were not run; focused behavior, semantic unit coverage and production build were the narrowest credible local proof.

## Documentation impact
- Marked RQ201 DONE and returned the analytics queue to `Current READY prompt: none`.
- Recorded the completed promotion in `MASTER_ROADMAP.md` and this durable run log.

## What was missed
- No live browser chart/table inspection against production data.

## Risks
- When users sort by a non-date column, chart point order follows that selection as required, while MA7/day-over-day values remain chronological and therefore comparable.
- The existing Daily Sales integration test emits one React act warning but all 19 tests pass; it is unrelated to this change.

## Next
- None for RQ201; return to the canonical queue for the next explicit promotion.
