# Trendplus Analytics UI Premium Roadmap

Updated: 2026-10-02
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

Current queue truth on 2026-10-02:

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
- `P-UI-23` is IN_PROGRESS for the bounded shared Pilot Data Quality Intake component lint slice; the current global baseline is 108 errors / 226 warnings.
- `P-UI-31` and `P-UI-35`..`P-UI-38` remain WAITING behind named dependencies or current-owner collision checks; the live queue is authoritative for their exact status.
- Recharts initial-preload/bundle graph is not P-UI-owned; it is routed to `PERF18`.

The queue remains authoritative for exact task status and acceptance. P-UI remains supplemental and must not displace higher-priority RQ/SQL correctness work.

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
