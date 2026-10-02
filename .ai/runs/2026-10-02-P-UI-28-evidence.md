Task ID: P-UI-28
Queue: docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
Date: 2026-10-02
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / no PR
Main commit SHA: 512f276a92f5cef539608f77a768c43cef286f71
Main verification: passed - fresh fetch confirmed `HEAD == origin/main` and `origin/main` contains the implementation SHA
Evidence state: synchronized

## What was done
- Promoted and claimed P-UI-28 after canonical idle recovery and a fresh collision check. Selected InventoryPage as the one pilot; filter labels, values, defaults, URL state and request mapping stayed unchanged.
- Added a pilot-scoped responsive filter layout with width-safe controls and a mobile disclosure. The mobile summary reflects selected period, store, supplier and whether search is active; all controls remain mounted and retain their values.
- Extended the responsive baseline runner to isolate a route/theme, use viewport screenshots, wait for a route-ready selector and report control-bar geometry. Pre-change Inventory baseline showed root overflow at 320, 375 and 1024 pixels; the final fixture run showed 0/5 overflow observations and 0 page errors.
- Delivered implementation as `512f276a92f5cef539608f77a768c43cef286f71` on `main` and verified it after a fresh fetch.

## Files changed
- `Klijent/clientapp/scripts/responsive_baseline.mjs`
- `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.css`
- `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsControlBar.spec.tsx`
- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-02-P-UI-28-evidence.md`

## Validation run
- `npm run test -- --run src/components/analytics/__tests__/AnalyticsControlBar.spec.tsx src/pages/__tests__/InventoryPage.retryRecovery.spec.tsx` - pass, 10/10.
- `npm run test -- --run src/pages/__tests__/InventoryPage.signalWindow.spec.tsx src/pages/__tests__/InventoryPage.queueStatus.spec.tsx` - 14/15 passed; one queued-marker synchronization assertion timed out under combined execution. The failing test passed isolated with `npm run test -- --run src/pages/__tests__/InventoryPage.queueStatus.spec.tsx -t "clears queued marker when inventory source keys disappear"` (1 passed, 7 skipped); classified as load-sensitive test behavior, with no filter semantics code change.
- `npm run typecheck` - pass.
- `npm run build` - pass; Vite reports the existing `recharts` chunk size warning (>500 kB).
- `npm run check:analytics-guardrails` - pass; 41 known baseline violations, 0 removed. An initial run detected a line-based baseline drift after helper insertion; moving the display-only summary derivation below the guarded source restored a clean baseline with no new violation.
- `npm run responsive:baseline -- --mode fixture --route-id inventory --theme light --viewport-only --timeout-ms 30000 --output-dir tmp/ui-visual/pui28-inventory-final` - pass; 5 widths, 0 root overflow observations, 0 page errors, intentional-overflow self-test passed.
- Governance self-tests and validators - pass: `check-agent-instructions.mjs`, `check-prompt-queues.mjs` (671 tasks), and `check-planning-architecture.mjs` (79 new planning tasks).
- `git diff --check` - pass.
- `git fetch origin` plus `git merge-base --is-ancestor 512f276a origin/main` - pass; fetched `origin/main` exactly matched implementation `HEAD` at verification time.
- `gh run list --commit 512f276a92f5cef539608f77a768c43cef286f71 --limit 10 --json databaseId,name,status,conclusion,headSha` - found Analytics Quality Gates run `37005335044`, currently `in_progress` on the implementation SHA.

## Validation not run
- Dark-theme and full-page Inventory screenshots - not run in the final matrix because the earlier Puppeteer captures timed out; final proof uses the light-theme viewport-only matrix.
- Full frontend test suite - not run; the focused control, Inventory recovery, period/URL and queue-status coverage was used.

## Documentation impact
- Closed P-UI-28 in the owning UI queue and updated the P-UI roadmap pointer. This run log is the durable evidence record.

## What was missed
- No filter fields, active-filter count semantics, apply/reset behavior, URL parameters or request payloads were changed.

## Risks
- A broader Inventory test showed one combined-run timeout/flaky queued-marker assertion; its isolated rerun passed. The production build still emits the pre-existing large `recharts` chunk warning. Analytics Quality Gates run `37005335044` was still `in_progress` when inspected.

## Next
- P-UI-29 was promoted and claimed after a fresh collision check; the `ColorSalesStatsPage` table is the selected pilot.
