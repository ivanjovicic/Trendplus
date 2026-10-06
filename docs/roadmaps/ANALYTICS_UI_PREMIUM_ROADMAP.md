# Trendplus Analytics UI Premium Roadmap

Updated: 2026-10-06
Status: existing UI program routing companion; implementation remains owned by the existing queue
Owner queue: `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
Evidence/audit: `docs/qa/ANALYTICS_UI_PREMIUM_AUDIT.md`

## Purpose

This document gives the already-active Premium UI queue an explicit roadmap owner so it is not orphaned by the consolidated planning architecture. It intentionally does not duplicate the queue's implementation prompts or the existing UI audit.

## Product boundary

The P-UI program improves navigation, shared controls, table systems, command-center presentation and other premium analytics UX **without changing analytics business truth**.

P-UI must not:

- invent recommendation, confidence, freshness or reason semantics;
- repair SQL/backend analytics correctness through visual fallbacks;
- turn missing/unknown/stale/partial evidence into a healthy-looking state;
- displace higher-priority BCI/STAB/RQ correctness work merely because a UI task is locally READY.

## Product-led analytics rule

The next UI improvements should make the existing retail decision flow easier to follow, not create additional visual destinations for the same signal. A user should be able to move from a prioritized exception to the same product/SKU, variant, store, supplier, period and trust context without losing the decision grain.

When a future backend contract supports it, P-UI may render a consistent drill path:

`portfolio/exception -> product or SKU -> variant/store context -> evidence -> recommended action -> recorded outcome`

This is a navigation and presentation rule, not permission to invent a Product 360 API, aggregate variants in the browser, infer parent-child relationships, or make a local recommendation. The authoritative identity, hierarchy, metric scope, provenance and action eligibility remain backend contracts.

## Current direction

The existing program has already established shared visual-regression, global command/header, information architecture, control-bar and table-system foundations.

Current queue truth on 2026-10-04:

- `P-UI-17` is DONE: PreNivelacijaPriorityPage chrome modernization.
- `P-UI-18` is DONE: SupplierFootwearAnalyticsPage chrome modernization.
- `P-UI-19` is DONE: grouped React chrome regression hardening.
- `P-UI-20` is DONE: grouped ErrorState/EmptyState/TrustHeader proof on Daily/Color/ShoeType/Supplier/Actions pages.
- `P-UI-21` is DONE: empty success without KPI totals and shared Actions ErrorState.
- `P-UI-22` is DONE: remaining decision-page empty/error chrome.
- `P-UI-24`..`P-UI-29` are DONE; measured baseline, responsive foundation, Inventory filter and Color Sales table pilots are delivered.
- `P-UI-32` is DONE on `main` (`3c15311541dca7dad39824ca207cdb3e63222e3b`): Product Decision Center is responsive with measured progressive rendering that keeps its complete result set available.
- `P-UI-34` is DONE on `main` at `c2874c9a6ce70a7a1fc38ec6477021c721852847`: Dashboard and Daily Sales have 0/5 root-overflow observations in both themes across 320/375/768/1024/1280, with unchanged analytics values and trust semantics.
- `P-UI-26` and `P-UI-27` are DONE, reconciled from their synchronized prompt evidence.
- `P-UI-30` is DONE on `main` at `d22e17f0e3f488286c7df933739a68305d78c669`: Sales/Goods Receipt controls meet the phone text/target contract, search panels track the visual viewport, and the mock Sales flow plus 320/375/768 light/dark geometry passed.
- `P-UI-33` is DONE on `main` at `987ec671894b5b77642674674d466159dd1d8bad`: Central Actions uses the shared responsive table/modal patterns; its 320/375/768 light/dark matrix passed with 0 root overflow and the status/outcome dialog workflow reachable.
- `P-UI-37` is DONE on `main` at `46f9587f6b2424e1238bac7b76be7a87c2ea5c6b`: Article List passes the 320/375/768 light/dark fixture matrix with 0 root overflow and keeps server paging/sort semantics unchanged.
- `P-UI-23` is DONE on `main` at `87e9deb7b9f7416b8cdd92be28ffb9ed7daa89ff`: the selected shared Pilot Data Quality Intake component slice went from 7 errors / 1 warning to 0 / 0; global lint baseline remains 108 / 226.
- Prompt-maintenance update 2026-10-06: the theme audits `cc57cace` + `2d675b60` are routed into the existing queue rather than a new mega-prompt. P-UI-38 owns residual theme/CSS regression debt; P-UI-52 owns the bounded Serbian display-copy sweep; P-UI-53 stays serialized behind P-UI-44 for Daily/Inventory path overlap.
- Responsive re-audit + same-day UX/UI reconciliation registered `P-UI-39`..`P-UI-53` and the canonical `docs/ai/ANALYTICS_DESIGN_SYSTEM.md`. Current collision-safe READY lanes are `P-UI-39` (primary), `P-UI-40`, `P-UI-41`, `P-UI-47` and `P-UI-49`.
- Sequencing is explicit rather than blanket-blocked by unrelated RQ statuses: `P-UI-31` waits for `P-UI-47`; `P-UI-35`/`P-UI-36` wait for `P-UI-39` + `P-UI-47` plus a fresh active-owner/path collision check; `P-UI-42` waits for `P-UI-39` + `P-UI-40` + `P-UI-47` + `P-UI-48`; `P-UI-45` waits for `P-UI-40` + `P-UI-48` because all touch shell/`AppLayout.tsx`; `P-UI-43`/`P-UI-44` remain after `RQ569`; `P-UI-46` follows `P-UI-42`; `P-UI-53` follows `P-UI-47` plus the chart-heavy page migrations `P-UI-31/35/36` **and** release/deferral of overlapping `P-UI-44` Daily/Inventory paths (or a freshly proven disjoint chart-only split); `P-UI-38` is the final responsive/theme/a11y regression gate and consumes P-UI-53.
- Portrait-tablet layout is currently stable against root overflow, but touch ergonomics is not yet acceptable: shared/page-local controls still miss the internal 16px form-text / 44px target contract. `P-UI-42` therefore targets coarse-pointer and hybrid touch capability, not a width-only device class.
- Recharts initial-preload/bundle graph is not P-UI-owned; it is routed to `PERF18`.

The queue remains authoritative for exact task status and acceptance. P-UI remains supplemental and must not displace higher-priority RQ/SQL correctness work.

Chart accessibility is an explicit program owner now: `P-UI-53` provides accessible names plus summary/table alternatives for analytics charts without deriving new business metrics; `P-UI-38` later ratchets that invariant.

## Roadmap sequence

1. Preserve visual-regression evidence and route smoke as the safety baseline.
2. Finish migration of high-value analytics tables onto shared UI primitives.
3. Consolidate dense page-specific controls where doing so does not change request/filter semantics.
4. Improve dashboard/command-center hierarchy after correctness/trust states are stable.
5. When an owner contract exists, make product/SKU-to-variant/store/evidence/action navigation consistent across product, inventory and supplier surfaces.
6. Keep dark/light/mobile/tablet/desktop visual evidence for broad UI changes.
7. Use the responsive execution sequence from `P-UI-24` onward: measure first, establish shared type/control/shell/primitives, then migrate bounded page families.
8. Prefer existing Puppeteer/browser and Node-guard tooling before adding new UI test frameworks.
9. Keep performance/loading-graph ownership in PERF; P-UI may consume its evidence but does not redefine bundle budgets.
10. Re-evaluate remaining page-specific UI debt before creating more premium prompts.

## Dependencies

- RQ/backend contracts remain authoritative for analytics semantics.
- STAB release/access/security gates remain authoritative for production readiness.
- DEX may later provide deterministic explanation contracts; P-UI may render them but does not define them.
- OBS may later provide operational telemetry; P-UI may visualize it but does not invent SLI state.

## Milestone

**Premium analytics consistency and responsive operation:** important backoffice/analytics pages share trustworthy controls/tables/navigation, remain usable across 320/375/768/1024/1280 layouts, and preserve the same units, filters, trust metadata and empty/error semantics across the product.

## Non-goals

No broad visual rewrite, design-system replacement, business-logic migration to frontend, parallel analytics formula work, generic self-service dashboard builder, or client-side product-hierarchy reconstruction is authorized by this roadmap.


## Responsive audit integration — 2026-10-01

Canonical source: `docs/ai/RESPONSIVE_UI_AUDIT_PROMPTS_2026-10-01.md`.

The audit deliberately does **not** authorize a repository-wide visual rewrite. Its execution model is:

`P-UI-24 measured browser baseline -> P-UI-25 foundation -> P-UI-26/27 shell+primitives -> P-UI-28/29 filter+table pilots -> P-UI-30..37 bounded page families -> P-UI-38 stable regression gates`.

Any page-family prompt that collides with an active RQ correctness owner waits. Responsive work never invents client-side analytics truth to make a layout easier.

## Responsive + UX reconciliation — 2026-10-04

The 2026-10-04 live responsive measurements remain valid, but routing was recomputed after the same-day UX/UI audit landed. READY means dependency-complete **and path-collision-safe**: two prompts that both edit `AppLayout.tsx` are not parallel merely because their feature-family names differ. WAITING RQ prompts are not start blockers by status alone; only a declared semantic dependency or a verified active owner/path collision blocks a P-UI claim.

Canonical evidence: `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md`, `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`, and `.ai/runs/2026-10-04-responsive-audit-review-corrections-evidence.md`.
