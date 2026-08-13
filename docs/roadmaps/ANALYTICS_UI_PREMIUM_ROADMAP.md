# Trendplus Analytics UI Premium Roadmap

Updated: 2026-08-13
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

## Current direction

The existing program has already established shared visual-regression, global command/header, information architecture, control-bar and table-system foundations.

Current queue truth on 2026-08-13:

- `P-UI-17` is DONE: PreNivelacijaPriorityPage chrome modernization.
- `P-UI-18` is DONE: SupplierFootwearAnalyticsPage chrome modernization.
- `P-UI-19` is DONE: grouped React chrome regression hardening.
- `P-UI-20` is DONE: grouped ErrorState/EmptyState/TrustHeader proof on Daily/Color/ShoeType/Supplier/Actions pages.
- `P-UI-21` is READY: empty success without KPI totals and shared Actions ErrorState.
- `P-UI-22` is WAITING after P-UI-21.

The queue remains authoritative for exact task status and acceptance.

## Roadmap sequence

1. Preserve visual-regression evidence and route smoke as the safety baseline.
2. Finish migration of high-value analytics tables onto shared UI primitives.
3. Consolidate dense page-specific controls where doing so does not change request/filter semantics.
4. Improve dashboard/command-center hierarchy after correctness/trust states are stable.
5. Keep dark/light/mobile/tablet/desktop visual evidence for broad UI changes.
6. Re-evaluate remaining page-specific UI debt before creating more premium prompts.

## Dependencies

- RQ/backend contracts remain authoritative for analytics semantics.
- STAB release/access/security gates remain authoritative for production readiness.
- DEX may later provide deterministic explanation contracts; P-UI may render them but does not define them.
- OBS may later provide operational telemetry; P-UI may visualize it but does not invent SLI state.

## Milestone

**Premium analytics consistency:** important analytics pages share trustworthy controls/tables/navigation and preserve the same units, filters, trust metadata and empty/error semantics across the product.

## Non-goals

No broad visual rewrite, design-system replacement, business-logic migration to frontend, or parallel analytics formula work is authorized by this roadmap.
