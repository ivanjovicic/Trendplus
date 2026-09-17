# Audit evidence: 2026-09-16 commits and agent logs

Task ID: direct-yesterday-commit-audit
Queue: direct-user-request
Date: 2026-09-17
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: a3e5ffdd07d1145405afde5e1bfef9f1c8a2953f
Main verification: passed - fresh fetch confirms `origin/main` contains `a3e5ffdd07d1145405afde5e1bfef9f1c8a2953f`
Evidence state: synchronized

## What was done

- Audited the 2026-09-16 local-day delivery set: direct Inventory review, RQ283-RQ290 runtime/docs commits, the RQ290 supplemental hardening and the main-first delivery instruction update.
- Reproduced the current frontend proof instead of trusting historical claims. Focused analytics tests were green, but `npm run check:analytics-guardrails` initially failed on `src/utils/shoeTypeMarginComparison.ts:47` because a boolean finite-value helper did not narrow the nullable denominator.
- Closed that compile gap by making the helper a TypeScript number type predicate; no metric formula or backend contract changed.
- Rechecked RQ291 against its acceptance. The delivered fixture covered click-to-detail but not direct parameter navigation, back-navigation or non-finite quality metadata. Added all three proofs in the focused Pre/Post harness without changing production routing or business semantics.
- Synchronized short/stale evidence SHAs: RQ285-RQ289, RQ291-RQ293 now carry exact commit IDs; RQ286 evidence points to final `74130866c19762d403ca09b02823a0fa093cddc8` hardening; RQ290 hardening points to runtime `e37a134444d7951bdaeed02bec4480d024cec49f` rather than documentation commit `4f82f929`.
- Added missing evidence sections to the RQ290 hardening and RQ291-RQ293 run logs, and recorded the same-owner RQ291/RQ293 hardening in the queue.

## Files changed

- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx`
- `Klijent/clientapp/src/utils/shoeTypeMarginComparison.ts`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `.ai/runs/2026-09-16-RQ285-evidence.md`
- `.ai/runs/2026-09-16-RQ286-evidence.md`
- `.ai/runs/2026-09-16-RQ287-evidence.md`
- `.ai/runs/2026-09-16-RQ288-evidence.md`
- `.ai/runs/2026-09-16-RQ289-evidence.md`
- `.ai/runs/2026-09-16-RQ290-hardening-evidence.md`
- `.ai/runs/2026-09-16-RQ291-evidence.md`
- `.ai/runs/2026-09-16-RQ292-evidence.md`
- `.ai/runs/2026-09-16-RQ293-evidence.md`
- `.ai/runs/2026-09-17-direct-yesterday-commit-audit-evidence.md`

## Validation run

- `npm run test -- --run src/pages/ProdajaPrePostNivelacijePage.spec.tsx` -> pass, 23/23 (includes direct route, detail click/back and missing/non-finite quality states).
- `npm run test -- --run src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx src/utils/__tests__/shoeTypeMarginComparison.spec.ts src/utils/__tests__/shoeTypePercentRange.spec.ts src/utils/__tests__/shoeTypeStatusIdentity.spec.ts` -> pass, 71/71.
- `npm run test -- --run src/pages/__tests__/ColorSalesStatsPage.spec.tsx src/pages/__tests__/ColorSalesStatsPage.premium.spec.tsx src/utils/__tests__/colorPercentRange.spec.ts src/utils/__tests__/colorSalesCoverage.spec.ts src/utils/__tests__/colorStatusIdentity.spec.ts src/utils/__tests__/categoryPrePostDetailMetrics.spec.ts src/utils/__tests__/colorPrePostDetailMetrics.spec.ts` -> pass, 88/88.
- `npm run test -- --run src/pages/__tests__/DailySalesStatsPage.spec.tsx src/pages/__tests__/DailySalesStatsPage.numericState.spec.ts src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx src/utils/__tests__/dailySupplierOrder.spec.ts src/utils/__tests__/dailyShiftSummary.spec.ts src/pages/__tests__/InventoryPage.queueStatus.spec.tsx` -> pass, 49/49.
- `npm run test -- --run src/utils/__tests__/shoeTypeMarginComparison.spec.ts` -> pass, 7/7 after the type-predicate repair.
- `npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx` -> pass, 13/13.
- `npm run typecheck` -> pass.
- `npm run check:analytics-guardrails` -> pass (encoding, analytics guardrails, typecheck).
- `npm run build` -> pass; Vite emitted only the existing chunk-size advisory.
- `node scripts/check-agent-instructions.mjs --self-test` -> pass.
- `node scripts/check-agent-instructions.mjs` -> pass (8 canonical files).
- `node scripts/check-prompt-queues.mjs --self-test` -> pass.
- `node scripts/check-prompt-queues.mjs` -> pass (439 tasks).
- `node scripts/check-planning-architecture.mjs --self-test` -> pass.
- `node scripts/check-planning-architecture.mjs` -> pass (78 planning tasks).
- `git diff --check` -> pass.
- Fresh `git fetch origin main` plus `git merge-base --is-ancestor a3e5ffdd07d1145405afde5e1bfef9f1c8a2953f origin/main` -> pass.

## Validation not run

- Full frontend suite -> not run; the audit used every changed-owner focused group and the production build.
- Deployed browser/manual API smoke -> not run; no live deployment evidence was available in this workspace.
- CI result inspection -> not run; repository policy treats CI as residual evidence unless a prompt names it as acceptance.

## Documentation impact

- Queue and run-log evidence now identify exact implementation SHAs and distinguish runtime commits from documentation synchronization commits.
- RQ291's original missing acceptance proof and RQ293's same-owner hardening are explicitly recorded rather than silently folded into a historical note.

## What was missed

- No additional confirmed product-semantic defect was found in the focused RQ283-RQ290 tests after the compile repair.
- The direct Inventory review's live/browser and full-suite omissions remain historical limitations, not silently marked as passed.

## Risks

- Deployed browser behavior, backend availability and CI remain unverified locally.
- The local `codex/rq291-local-duplicate` backup branch and two older stashes preserve superseded local work; they were not pushed or used for delivery.
- `.codex-remote-attachments/` remains an unrelated untracked workspace artifact and was not touched.

## Next

- Queue current READY remains `none`; promote the next safe prompt only on an explicit owner/user instruction.
