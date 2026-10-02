Task ID: P-UI-30
Queue: `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
Date: 2026-10-02
Agent/tool: Codex
Delivery target: main
Working branch / PR: `main` / none
Main commit SHA: `d22e17f0e3f488286c7df933739a68305d78c669`
Main verification: passed - freshly fetched `origin/main` equals implementation SHA `d22e17f0e3f488286c7df933739a68305d78c669`; `gh run list --commit d22e17f0` returned no Actions runs.
Evidence state: synchronized

## What was done
- Applied the shared phone control contract to Sales and Goods Receipt inputs/selects/buttons: controls use at least 16px text and 44px height at phone widths.
- Anchored Sales/Goods Receipt autocomplete panels to the input on coarse-pointer-only devices. Their position and maximum height follow the visual viewport as it resizes or scrolls, so results remain usable with the on-screen keyboard.
- Hid Ctrl+Enter hints only for coarse-pointer/no-hover contexts; both keyboard handlers remain intact.
- Added regression proof for the sales search/add/edit/submit payload, goods-receipt supplier selection/navigation state and viewport-anchored search panel geometry.
- Expanded the responsive fixture runner with synthetic Sales/Goods Receipt/Nivelacija data and a working Sales flow through the mocked POST boundary.
- Queue recovery reconciled stale P-UI-26/P-UI-27 and RQ495 summary states before claiming this task; RQ495 owns Supplier/Shoe Type snapshot reads, outside the three entry-form paths.

## Files changed
- `Klijent/clientapp/scripts/responsive_baseline.mjs`
- `Klijent/clientapp/src/components/UnosRobeForm.tsx`
- `Klijent/clientapp/src/components/UnosRobeForm.spec.tsx`
- `Klijent/clientapp/src/components/forms/useViewportSearchPanel.ts`
- `Klijent/clientapp/src/components/forms/useViewportSearchPanel.spec.ts`
- `Klijent/clientapp/src/components/prodaja/CreateProdajaForm.tsx`
- `Klijent/clientapp/src/components/prodaja/CreateProdajaForm.spec.tsx`
- `Klijent/clientapp/src/styles/forms.css`
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-02-P-UI-30-evidence.md`

## Validation run
- `npm run test:run -- src/components/prodaja/CreateProdajaForm.spec.tsx src/components/UnosRobeForm.spec.tsx src/components/forms/useViewportSearchPanel.spec.ts` -> pass, 3 files / 3 tests.
- `npm run typecheck` -> pass.
- `npm run build` -> pass; existing Recharts chunk-size warning remains.
- `npm run responsive:baseline -- --route-ids prodaja,unos_robe,nivelacija_cena --viewport-width 320 --strict --output-dir tmp/ui-visual/pui30-320` -> pass, 6 route/theme observations, 0 overflow, 0 page errors; Sales mock submit confirmed.
- Same strict responsive baseline command at `--viewport-width 375 --output-dir tmp/ui-visual/pui30-375` -> pass, 6 observations, 0 overflow, 0 page errors; Sales mock submit confirmed.
- Same strict responsive baseline command at `--viewport-width 768 --output-dir tmp/ui-visual/pui30-768` -> pass, 6 observations, 0 overflow, 0 page errors; Sales mock submit confirmed.
- `node scripts/responsive_baseline.mjs --self-test` -> pass.
- `git diff --check` -> pass.
- Queue, instruction and planning governance validators -> pass for the claim/promotion updates and again for this closure.
- Fresh `git fetch origin` and ancestry check -> implementation SHA is on `origin/main`.

## Validation not run
- Full frontend suite -> not run; focused component contracts, changed-project typecheck/build and the responsive browser matrix cover this bounded UI change.
- `npm run check:analytics-guardrails` -> not run; no analytics metric, trust, period, routing or export behavior changed.
- Real iOS/iPad keyboard and auto-zoom check -> unavailable in this environment.

## Documentation impact
- Synchronized P-UI-30 completion, P-UI-33 claim/current pointer, the P-UI queue summary and `MASTER_ROADMAP.md` with the delivered evidence.

## What was missed
- No known repository-local acceptance item remains.

## Risks
- Real-device iOS keyboard/zoom behavior remains unverified. Form validation, numeric conversion, payload shape and persistence semantics were not changed.
- A first test attempt timed out because fake timers were left enabled during async queries; the test was corrected and the final focused run passed. Early browser-fixture attempts exposed unsupported Puppeteer locator helpers; the runner now uses supported APIs and strict runs pass.

## Next
- P-UI-33 is claimed IN_PROGRESS for the independently owned Central Actions responsive migration; keep action semantics and RQ-owned contracts unchanged.
