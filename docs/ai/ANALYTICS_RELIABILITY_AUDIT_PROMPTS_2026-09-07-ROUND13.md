# Analytics Reliability Audit Prompts: 2026-09-07 Round 13

Scope: direct-user-request audit of production analytics decision surfaces. Standalone Trend, forecast, Shopify/vendor integrations, Python/ML evaluation and test-only features were excluded.

## Audit method

- Read `AGENTS.md`, `docs/ai/ARCHITECTURE_BOUNDARIES.md`, `docs/ai/VALIDATION_SELECTOR.md` and `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.
- Reviewed the current queue and prior audit receipts through Round 12; existing prompts `RQ145`, `RQ151`, `RQ176`, `RQ187`, `RQ191`, `RQ245`, `RQ253` and `RQ254-RQ259` were treated as owners and not duplicated.
- Inspected the shared React trust boundaries, representative Dashboard, Actions, Data Quality, Daily Sales, Shoe Type, Supplier, PDC, pre/post and report call sites, nearest component/page tests and Git history for `AnalyticsTrustHeader.tsx` and `AnalyticsEmptyState.tsx`.
- This is a static audit. No production behavior was changed and no live browser, database refresh or runtime endpoint proof was claimed.

## New findings

| ID | Area | Evidence | Impact | Queue prompt |
|---|---|---|---|---|
| A13-1 | Shared trust metadata | `AnalyticsTrustHeader.tsx:99-105` and `:114-120` accept/display non-finite summary numbers; `:180-182` renders `refreshCurrentStep`; `:226-232` renders `fallbackReasonCode` verbatim | Internal tokens and `NaN`/`Infinity` can appear in user-facing trust context, making degraded evidence look measured or exposing backend vocabulary | `RQ258` |
| A13-2 | Trust-header mode contract | `AnalyticsTrustHeader.tsx:167-169` gates every mode when `recommendationAllowed !== true`; report/signal call sites omit the field | Signal/report screens can claim a recommendation is gated even though no recommendation decision applies | `RQ259` |
| A13-3 | Freshness adapter | `AnalyticsTrustHeader.tsx:70-76` exact-matches lowercase tokens and does not trim/case-normalize | `STALE` or whitespace-variant stale metadata becomes `unknown`, hiding a real degraded state | `RQ259` |
| A13-4 | Shared empty state | `AnalyticsEmptyState.tsx:57-58,93-94` renders raw reason codes; core pages pass `meta.emptyReason` before the safe mapped message; default “Proširi period” has no action handler/link | Backend vocabulary can leak into empty UI and a suggested next step is displayed as an unusable action | `RQ260` |

## De-duplication decisions

- `RQ253` covers raw error codes in `AnalyticsErrorState`; `RQ258` is limited to the separate trust-header fallback and refresh metadata boundary.
- `RQ151` and `RQ245` cover Analytics Actions code mapping; this audit found the shared header still bypasses any page-level mapping.
- `RQ191` covers shared confidence/reliability percentage range behavior; `RQ258` covers the distinct data-quality summary-count renderer.
- `RQ176` and `RQ187` own freshness source truth for inventory/cache paths; `RQ259` only normalizes the shared frontend display token and does not create freshness.
- `RQ260` is a shared empty-state boundary finding; it does not replace `RQ169` readiness semantics or `RQ145` broad parity ownership.
- Existing PDC/supplier findings `RQ254-RQ257` were not repeated.

## Queue result

- Added `RQ258`, `RQ259` and `RQ260` as `WAITING` prompts to `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`.
- The canonical `READY` prompt remains `RQ169`; no prompt was promoted because the user asked for audit and new prompts, not execution.
- Queue routing remains governed by the existing protocol; this request is recorded as `Queue: direct-user-request`.

## Required proof when executed

- Add failing-first component/page tests for raw-token safety, finite summary counts, mode-specific gating and freshness variants.
- Verify no raw code, `NaN`, `Infinity` or `-Infinity` appears in visible trust text.
- Verify signal/report surfaces do not show an invented recommendation gate, while recommendation surfaces preserve backend gating.
- Run focused frontend tests, analytics guardrails, frontend build and `git diff --check`; add browser console evidence if the runtime surface is exercised.
