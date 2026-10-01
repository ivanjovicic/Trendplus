# Trendplus responsive UI audit — verified findings, corrections and queue plan

Date: 2026-10-01  
Repo: `ivanjovicic/Trendplus`  
Verified base: `origin/main` `5257014a3bb6925cb13d6bffdb47affb41437c3d`  
Owner programs: `P-UI` for responsive/presentation work; `PERF` for the Recharts preload/bundle regression  
Source audit: user-supplied `RESPONSIVE_UI_AUDIT_PROMPTS_2026-10-01.md` + evidence note, rechecked against current-main code and existing queue/roadmap contracts.

## Executive verdict

The original audit is directionally strong and most code-level findings are real, but it was **not safe to register verbatim**. The re-review found one material false P1 statement, several over-absolute claims, duplicate tooling proposals and multiple prompts whose scope was too broad for the repository's queue/collision model.

This revision keeps the useful findings, corrects the inaccurate ones, routes work to the existing owners, and replaces the 32 draft prompts with smaller canonical execution prompts:

- `P-UI-24`..`P-UI-38` own responsive UI/browser-proof work.
- `PERF18` owns the current Recharts initial-preload regression and reconciliation of the earlier `PERF17` bundle contract.
- No analytics formula, recommendation, trust-state, filter meaning or API contract may be changed by these UI prompts.
- `P-UI-24` is the only newly promoted responsive task; later responsive tasks remain `WAITING` behind measured evidence/shared primitives.
- Current higher-priority RQ/SQL correctness work is not displaced.

## Evidence classes

Every finding below is classified as:

- **CODE** — directly proven from current source.
- **BUILD** — proven by the production build captured in the source audit.
- **BROWSER-PENDING** — plausible from code/CSS but must not be treated as observed runtime behavior until browser/device evidence exists.
- **DEVICE-PENDING** — requires real iOS/iPad/Safari or equivalent device evidence; Chromium emulation is not enough.

Line numbers are audit aids, not contracts. Executors must re-resolve the named symbol/rule on fresh `main`.

## Material corrections to the supplied audit

| Original claim/proposal | Re-review | Corrected contract |
|---|---|---|
| Supplier mobile filter stack remains sticky below the app header | **Incorrect** | `SupplierConsolidatedPage.css` sets `position: static` at `max-width: 900px`. The real issue is that at `<=768px` 11 filters collapse to one column and controls shrink to `2.2rem`; fix density/interaction, not a non-existent mobile sticky stack. |
| “No browser automation; add Playwright” | **Incomplete/outdated** | There is no dedicated responsive screenshot suite, but the repo already contains `puppeteer`, `puppeteer-core`, `scripts/perf08_frontend_render.mjs` and `tools/contrast_check.js`. Extend the existing browser toolchain first; do not add Playwright merely for this audit. |
| Add a new bundle script / Lighthouse budget as the first bundle gate | **Duplicate/unsupported** | `npm run check:bundle-budget` and `PERF17` already own the measured bundle guard. Extend the existing guard only after current-main measurement. Lighthouse thresholds are not gates until a baseline justifies them. |
| Recharts is only a large route chunk | **No longer true on current main** | The 2026-10-01 build shows `dist/index.html` modulepreloading `recharts`, and the entry imports runtime symbols from that chunk. This contradicts the older `PERF17` route-isolation contract. Route to `PERF18`. |
| Every field below 16 px “causes iOS zoom on every focus” | **Too absolute** | `<16px` text controls are a known iOS Safari auto-zoom risk; source proves many sub-16px fields, but actual zoom remains device/browser evidence. Use `>=16px` as the mobile form-control design contract and verify on iOS when available. |
| 44x44 is the WCAG universal minimum | **Too broad** | 44x44 remains the Trendplus coarse-pointer design target. Accessibility conformance rules have exceptions and must not be reduced to a blanket size assertion. Automated checks need documented inline-link/spacing exceptions. |
| Normalize every media query to exactly 640/768/1024/1280 | **Over-constrained** | Prefer shared design breakpoints and mobile-first/container-aware layout, but allow documented content-driven thresholds when they prevent actual overflow. Do not mass-rewrite working CSS only to satisfy a number allow-list. |
| Add Stylelint to ban non-token breakpoints | **New dependency without need proof** | Use the existing Node guard pattern if a deterministic responsive lint is later valuable. Add Stylelint only after a separate tooling decision. |
| Require `document.getAnimations().length === 0` under reduced motion | **Wrong acceptance shape** | Reduced motion should stop non-essential autoplay/long-running movement and shorten/disable non-essential transitions. Some short state feedback may legitimately remain. Test behavior, not a global zero-animation count. |
| Require `@tanstack/react-virtual` for 1,200 rows | **Premature implementation choice** | `top: 1200` + `sortedRows.map` is CODE-proven; performance harm is not. Measure DOM/render/interaction first, then choose server paging, progressive rendering or virtualization. |
| One shared table/filter prompt migrates all pages | **Too broad/collision-prone** | Build/pilot the primitive first, then migrate page families in bounded prompts with existing semantics frozen. |
| Header consumes 25–35% of a 667px phone | **BROWSER-PENDING estimate** | Source proves `flex-wrap`, many controls and sticky positioning; actual height is a measurement for `P-UI-24`. Do not use the percentage as a fact before capture. |
| Modal definitely overflows 375px | **BROWSER-PENDING, high confidence** | `Modal.tsx` has `minWidth: 400/600` for md/lg and no width cap in `.modal-content`; browser proof should capture each size. |
| Nested 620px table scroll definitely creates a scroll trap | **BROWSER-PENDING** | Source proves nested scroll containers; actual usability/scroll trap is a rendered interaction test. |

## Verified current-main findings

### P1 — foundation / high-frequency workflow

1. **CODE — mobile density is desktop-biased.** `themes.css` has base `0.9rem`, small `0.78rem`, xs `0.72rem`, controls `2.5rem`; `forms.css` has 40px controls, 34px compact controls and 38px buttons.
2. **CODE — many form fields are below the new mobile 16px design target.** Examples include `AnalyticsControlBar.css` at 13px and data-entry forms using Tailwind `text-sm`.
3. **CODE — many coarse-pointer targets are below the project 44px target.** Header/nav, InfoTip, compact buttons/chips and pagination are representative cases.
4. **CODE — header is a sticky `flex-wrap` surface with many actions and a second system-control strip.** Runtime height/overflow remains `BROWSER-PENDING`.
5. **CODE — mobile drawer lacks dialog semantics/focus trap/Esc/body-scroll-lock.** The shared `Modal` already contains behavior that can inform the implementation, but extraction into a hook is optional, not mandated.
6. **CODE — shared `Modal` has inline min widths `320/400/600`; md/lg need a responsive width cap.** Existing Esc/focus trap/scroll lock must be preserved.
7. **CODE — `AnalyticsDataTable` is scroll-only (`min-width:760`, 12px cells), while important pages still own tables up to 1400px.**
8. **CODE — data-entry forms `/prodaja`, `/unos-robe`, `/nivelacija` use small labels/fields/quick-action chips and number inputs without an explicit mobile keyboard/locale contract.**
9. **BUILD — current `recharts` chunk remains ~548k raw / ~164k gzip, but now participates in the initial graph.** This is a contract drift from `PERF17`, not merely a visual issue.

### P2 — shared usability / analytics surfaces

10. **CODE — breakpoint debt exists:** many desktop-first media-query values and copy-pasted page-specific responsive rules.
11. **CODE — `InfoTip` is a focusable `span role=button`, ~18px target, and closes on captured scroll.**
12. **CODE — reduced-motion is not global; seasonal carousel auto-scrolls every four seconds and is mounted on every `AppLayout` route.**
13. **CODE — page tables and several analytics tables have wide minimum widths and/or nested scroll containers.**
14. **CODE — charts use fixed heights, narrow tick text and wide categorical axes on several pages. Exact phone readability is `BROWSER-PENDING`.**
15. **CODE — `ProductDecisionCenter` requests `top:1200` and maps all current rows into DOM. Need for virtualization is `BROWSER-PENDING` until measured.**
16. **CODE — Dashboard KPI grid goes 5 -> 3 -> 1 columns; a phone 2-up layout may improve density but must be browser-tested, not assumed.**
17. **CODE — focus suppression exists in both CSS and Tailwind call sites; focus visibility should be proven per interactive primitive.**
18. **CODE — `100vh`/`h-screen` and missing safe-area handling exist in mobile shell paths; `dvh`/safe-area is a bounded shell improvement.**

### P3 — cleanup / long tail

19. **CODE — z-index values are ad hoc across header/drawer/modal/toast; a small documented layer scale would reduce future collisions.**
20. **CODE — many hard-coded font sizes and duplicated filter styles reduce leverage of tokens.**
21. **CODE — long-tail admin/observability/legacy screens can remain scroll-first if document overflow and target sizing are controlled; they do not all require phone card redesign.**
22. **CODE — OS theme preference, favicon and marketing metadata are not prerequisites for responsive correctness and are not bundled into the P1 execution chain.**

## Existing repository contracts that must be reused

- `docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md` — existing visual review contract; update/extend rather than replace.
- `Klijent/clientapp/scripts/perf08_frontend_render.mjs` — existing Puppeteer browser automation example.
- `Klijent/clientapp/scripts/check-bundle-budget.mjs` — existing bundle budget guard.
- `docs/architecture/PERFORMANCE_FRONTEND_BUNDLE_BUDGET.md` / `PERF17` — existing bundle ownership.
- `AnalyticsControlBar`, `AnalyticsDataTable`, `Modal`, `InfoTip` — improve/migrate incrementally instead of creating parallel primitives with overlapping ownership.
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md` — every live prompt needs Problem/Evidence/Scope/Read first/Do/Tests/Acceptance/Dependencies and main-first delivery.

## Responsive design contract

This is a product target, not a claim that current UI already satisfies it.

| Surface | Phone `<640` | Tablet `640–1023` | Laptop/desktop `>=1024` |
|---|---|---|---|
| Body copy | normally 16px | 15–16px | 15px target, denser tables allowed if readable |
| Form controls | text >=16px; target height 44px | text >=16px on coarse pointer; target 44px | current desktop density may stay ~40px |
| Coarse-pointer primary controls | target >=44x44 | target >=44x44 | n/a for fine pointer |
| Secondary/meta text | avoid decision-critical text below 13px | >=13px | >=12px when contrast/readability pass |
| Page gutter | 16px | 20–24px | 24px / existing max-width |
| Header | measured compact mode, not an arbitrary wrap stack | compact mode | full action surface |
| Tables | prioritized cards/fields where semantics remain clear; otherwise explicit contained horizontal scroll | sticky key column or contained scroll | full table |
| Modals | width capped to viewport; sheet only when it materially helps | centered/capped | centered/current size |
| Reflow evidence | 320px mandatory | 768px | 1024/1280 |
| Motion | non-essential autoplay/movement off under reduced motion | same | same |

Reference viewport matrix: **320, 375, 768, 1024, 1280**.  
Reference themes: **light + dark** for shared shell/primitives.  
Real-device residual: at least one iOS Safari phone and one coarse-pointer tablet before calling iOS zoom/keyboard behavior fully proven.

## Canonical prompt plan

Detailed executable bodies are registered in their owning queues.

| New ID | Owner | Priority | Purpose | Replaces/absorbs |
|---|---|---:|---|---|
| P-UI-24 | P-UI | P1 | Responsive browser baseline using existing Puppeteer + visual protocol | RSP-T1/T2 baseline portions |
| P-UI-25 | P-UI | P1 | Type/control/input/focus foundation | F1/F2/F8 + safe subset C8 |
| P-UI-26 | P-UI | P1 | Compact header + accessible mobile drawer + shell viewport behavior | F3/F4 + shell part F6 |
| P-UI-27 | P-UI | P1 | Modal/help/tabs/coarse-pointer shared primitives | C3/C4/C6 |
| P-UI-28 | P-UI | P1 | Responsive FilterBar primitive + one pilot, semantics frozen | C2 |
| P-UI-29 | P-UI | P1 | Responsive AnalyticsDataTable primitive + one pilot | C1 |
| P-UI-30 | P-UI | P1 | Data-entry mobile workflow (`prodaja`, `unos-robe`, `nivelacija`) | P4 + remaining C8 |
| P-UI-31 | P-UI | P1 | Supplier overview responsive migration | P1 (corrected N16) |
| P-UI-32 | P-UI | P1 | Product Decision Center responsive + measured row rendering | P2/C9 |
| P-UI-33 | P-UI | P1 | Central Actions responsive migration | P3 |
| P-UI-34 | P-UI | P2 | Analytics Dashboard + Daily Sales responsive migration | P5/P6 + chart subset |
| P-UI-35 | P-UI | P2 | Nivelacija analytics responsive migration | P7 |
| P-UI-36 | P-UI | P2 | Supplier Decision Hub + Shoe Type + Color responsive migration | P8 |
| P-UI-37 | P-UI | P2 | Article list + bounded long-tail cleanup | P9/P10 |
| P-UI-38 | P-UI | P2 | Responsive regression gate + CSS hygiene after migrations | T2/T3/T5, measured T4 UI subset |
| PERF18 | PERF | P1 | Reconcile Recharts initial preload and extend existing bundle graph guard | F7/T4 bundle subset |

## Ordering and collision policy

1. `P-UI-24` first: capture browser evidence before broad visual source changes.
2. `P-UI-25` foundation before page migrations.
3. `P-UI-26` and `P-UI-27` can follow when current frontend-owner collisions are clear.
4. Build shared primitives (`P-UI-28`, `P-UI-29`) as pilots before page-family migrations.
5. Prioritize store workflows (`P-UI-30`) and highest-value decision pages (`P-UI-31`..`P-UI-33`) before long-tail cleanup.
6. `P-UI-38` turns repeated evidence into regression gates only after selectors/contracts stabilize.
7. `PERF18` stays independently owned by PERF and must not be implemented as a CSS/UI side effect.
8. If an active RQ prompt touches the same frontend paths/business semantics, P-UI waits rather than merging correctness and presentation ownership.

## Stop conditions

A responsive prompt must stop as PARTIAL/BLOCKED instead of guessing when:

- changing layout requires changing analytics values, recommendation/trust semantics, filters/defaults, URL parameters, API shape or authorization;
- a browser/device assertion cannot be produced in the available environment;
- a new dependency is the only way forward but equivalent existing tooling has not been evaluated;
- mobile locale/decimal behavior would change persisted numeric meaning;
- card/table transformation would hide columns that are needed to understand a decision;
- an active higher-priority owner is editing the same paths.

## What remains unproven after this source re-review

- Real iOS Safari focus zoom/virtual keyboard behavior.
- Exact rendered header heights and panel overlap at each viewport.
- Whether each nested scroll area is a practical scroll trap.
- Product Decision Center frame rate / interaction cost for 1,200 rows.
- Chart label readability and tap-tooltip behavior on real devices.
- Lighthouse/Web Vitals thresholds; no new performance score gate is approved by this audit.
- Full long-tail coverage of every page/table.
