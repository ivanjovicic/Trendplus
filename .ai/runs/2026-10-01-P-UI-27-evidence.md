Task ID: P-UI-27
Queue: docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
Date: 2026-10-01
Agent/tool: ChatGPT + Vitest
Delivery target: main
Working branch: cursor/p-ui-27-responsive-primitives-51d0
Main commit SHA: pending delivery
Main verification: pending
Evidence state: pending

## What was done
- Promoted and claimed P-UI-27 after collision review (no open PR/lock on Modal/InfoTip paths).
- Capped modal width to the viewport with size classes, safe-area padding and a narrow-screen bottom sheet alignment; preserved Esc/focus trap/scroll lock.
- Replaced InfoTip pseudo-button with a native `button`, enlarged coarse-pointer hit area, disabled scroll-dismiss on coarse pointers and added outside-tap dismissal.
- Made toast container width safe at 320px with safe-area aware positioning.
- Did not introduce a new shared tabs primitive; tab hardening remains deferred to page-family prompts.

## Files changed
- `Klijent/clientapp/src/components/Modal.tsx`
- `Klijent/clientapp/src/tailwind.css`
- `Klijent/clientapp/src/components/ui/InfoTip.tsx`
- `Klijent/clientapp/src/components/ui/InfoTip.css`
- `Klijent/clientapp/src/components/ui/InfoTip.spec.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsAccessibility.spec.tsx`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`

## Validation run
- InfoTip + AnalyticsAccessibility (Modal) tests -> pass: 11/11.
- `npm run typecheck` -> pass.
- `npm run build` -> pass.
- `git diff --check` -> pass.

## Validation not run
- Full P-UI-24 PNG matrix on modal/help routes (environment screenshot timeouts from prior runs).
- Real iOS/iPad Safari device proof.

## Risks
- Coarse-pointer InfoTip uses larger hit box; fine-pointer desktop icon size unchanged via media query.
- Shared tabs/chips not migrated in this prompt.

## Next
- Deliver to `main`, then idle recovery for P-UI-28+ after collision checks.
