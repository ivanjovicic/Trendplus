Task ID: P-UI-26
Queue: docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
Date: 2026-10-01
Agent/tool: ChatGPT + Vitest/Puppeteer
Delivery target: main
Working branch / PR: cursor/p-ui-26-responsive-shell-51d0 / pending
Main commit SHA: pending delivery
Main verification: pending
Evidence state: pending

## What was done
- Promoted and claimed P-UI-26 after collision review (no active header/sidebar owner; Q83 PR does not touch shell paths).
- Added shared `useDialogA11y` for Esc handling, focus trap, scroll lock and focus return.
- Mobile sidebar is now a dialog with `aria-modal`, safe-area/dvh sizing and focus return to the nav trigger.
- Compact mobile header: single primary row, backend chip, and a **Više** overflow surface that preserves Komande/Obaveštenja/Kontekst, scope, Teme, Osveži and system controls.
- Command/inbox/context panels use viewport-bounded sheet behavior on narrow viewports with dialog semantics.
- Desktop (`lg+`) header/sidebar layout and route grouping unchanged.

## Files changed
- `Klijent/clientapp/src/hooks/useDialogA11y.ts`
- `Klijent/clientapp/src/layout/AppLayout.tsx`
- `Klijent/clientapp/src/layout/components/HeaderStatus.tsx`
- `Klijent/clientapp/src/layout/components/Sidebar.tsx`
- `Klijent/clientapp/src/layout/components/__tests__/HeaderStatus.spec.tsx`
- `Klijent/clientapp/src/layout/components/__tests__/Sidebar.spec.tsx`
- `Klijent/clientapp/scripts/responsive_baseline.mjs` (timeout hardening for flaky screenshot runs)
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`

## Validation run
- `HeaderStatus` + `Sidebar` Vitest -> pass: 2 files, 9 tests.
- `npm run typecheck` -> pass.
- `npm run build` -> pass.
- `git diff --check` -> pass.
- App-shell root-overflow geometry at 320/375/768/1024/1280 via Puppeteer fixture navigation -> pass (no root horizontal overflow).
- Full `responsive:baseline` PNG matrix -> not completed; repeated `Page.captureScreenshot` protocol timeouts in this environment even after protocol/screenshot timeout hardening.

## Validation not run
- Real iOS/iPad Safari device proof.
- Full 70-combination PNG matrix after shell change (blocked by screenshot timeouts; geometry-only shell proof executed instead).

## Documentation impact
- Queue routing updated for P-UI-26 claim/closure.
- No analytics/API contract changes.

## What was missed
- P-UI-27 shared primitives and page-family migrations remain later prompts.
- Products-page baseline overflow observations from P-UI-24 remain unfixed.

## Risks
- Mobile overflow sheet adds an extra tap for some header actions; desktop path unchanged.
- Screenshot baseline runner remains environment-sensitive for full-page captures.

## Next
- Deliver to `main`, promote P-UI-27 after fresh collision check.
