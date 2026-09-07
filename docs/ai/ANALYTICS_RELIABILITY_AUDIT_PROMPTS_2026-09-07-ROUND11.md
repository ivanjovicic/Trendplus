# Analytics Reliability Audit Prompts - 2026-09-07 Round 11

Queue: direct-user-request
Scope: production analytics only; Trend, forecast, Shopify, vendor integrations and standalone test functionality excluded.
Status: audit completed; one new prompt added and prior trust/action findings rechecked.

## Audit focus

This pass followed the shared error-state path across the main analytics decision surfaces and compared it with existing queue ownership before adding work. The review covered source, nearest tests and Git history, with particular attention to raw backend codes, safe Serbian messaging and error-versus-empty behavior.

Routes/surfaces reviewed:

- `/analytics`, `/analytics/products`, `/analytics/supplier`, `/analytics/inventory`
- `/analytics/actions`, `/analytics/decision-board`, `/analytics/data-quality`, `/analytics/reports`
- supplier report/export and pre/post nivelacija error surfaces that reuse the shared component

## Findings

### RQ253 - Shared analytics error state exposes raw backend codes

Status: `WAITING`, Priority: `P1`, Owner: Analytics Frontend Foundations.

`AnalyticsErrorState` displays `errorCode` verbatim in the visible alert. Because the same component is used by the core analytics pages and report/nivelacija consumers, an internal or future backend code can become user-facing text. The repository has focused raw-code coverage for Analytics Actions (`RQ151`), but no shared-component regression test for known, unknown or malformed codes.

Evidence:

- `Klijent/clientapp/src/components/analytics/AnalyticsErrorState.tsx:31-50` renders `Šifra greške: {errorCode}` directly.
- The component is consumed by the dashboard, products, supplier, inventory, actions, Decision Board, Data Quality, report, readiness and pre/post pages.
- Existing page tests mostly mock the component; no focused component test asserts that an error code cannot leak into visible user copy.
- Git history attributes the behavior to `c7b81d060`; subsequent refresh/action hardening added correlation support but did not remove the raw-code presentation.

Required implementation and proof are defined in `RQ253` in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`.

### Rechecked existing findings

- `RQ252` remains valid: supplier report action generation still has an explicit-false-only trust guard and needs backend fail-closed handling for missing trust metadata.
- `RQ251` remains valid: Inventory workflow/scheduler raw operational labels are already covered and were not duplicated.
- `RQ233` and `RQ144` remain existing owners for supplier concentration scope and Data Quality denominator/null handling; their findings were not recreated.

## Coverage matrix for this pass

| Surface | Shared error owner | Endpoint/DTO boundary | Empty/error distinction | Raw-code risk | Recommendation impact | Result |
|---|---|---|---|---|---|---|
| Dashboard | `AnalyticsErrorState` | cached dashboard responses | page has error state | shared component | no decision shown in error state | covered by `RQ253` |
| Products | `AnalyticsErrorState` | product decision response | page-specific error state | shared component | backend gate remains authoritative | covered by `RQ253` |
| Supplier | `AnalyticsErrorState` | supplier decision/report responses | supplier error/empty paths | shared component plus `RQ252` | `RQ252` remains open | `RQ253` + `RQ252` |
| Inventory | `AnalyticsErrorState` | inventory balance/list/signal responses | inventory error/empty paths | shared component plus `RQ251` labels | `RQ178` remains owner | `RQ253` + existing prompts |
| Actions | `AnalyticsErrorState` | actions/outcome responses | action summary error/empty | `RQ151` covers action metadata, shared alert remains open | backend action contract | `RQ253` + `RQ151` |
| Decision Board | `AnalyticsErrorState` | decision-board DTO/projector | board error/empty paths | shared component | board gate remains backend-owned | covered by `RQ253` |
| Data Quality | `AnalyticsErrorState` | issue/health/intake responses | empty vs error path exists | shared component plus `RQ144` | not an action surface | `RQ253` + `RQ144` |
| Reports/pre-post | `AnalyticsErrorState` | report and pre/post endpoints | report error/empty paths | shared component | report action gate remains `RQ235`/`RQ252` | `RQ253` + existing prompts |

For `RQ253`, period, scope, freshness and data-quality metadata remain owned by each endpoint contract. The defect is the shared error presentation layer: it can reveal an internal code instead of a safe explanation, while the underlying error/empty distinction must remain intact.

## Prompt hygiene

- Added only `RQ253`; no duplicate was created for `RQ151`, `RQ252` or `RQ251`.
- `RQ253` remains `WAITING` because the canonical queue still keeps `RQ169` as the current `READY` item.
- No production code was changed; only the canonical queue and audit evidence were updated.

