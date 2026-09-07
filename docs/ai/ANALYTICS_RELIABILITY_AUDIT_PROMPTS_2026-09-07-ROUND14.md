# Analytics Reliability Audit Prompts: 2026-09-07 Round 14

Scope: direct-user-request audit of production analytics shared refresh and executive KPI surfaces. Standalone Trend, forecast, Shopify/vendor integrations, Python/ML evaluation and test-only features were excluded.

## Audit method

- Reused the canonical repository, architecture, validation and queue rules already read for this direct request.
- Reviewed the current analytics reliability queue through `RQ260` and de-duplicated against refresh/freshness owners `RQ141`, `RQ145`, `RQ146`, `RQ176`, `RQ187`, workflow-label owner `RQ251`, numeric owners `RQ167`/`RQ191` and shared trust prompts `RQ258-RQ260`.
- Inspected `AnalyticsRefreshStatusService`, refresh DTO/endpoint, `AnalyticsRefreshStatusBanner`, `ExecutiveKpiRow`, Dashboard wiring, nearest backend/frontend tests and Git history/blame.
- This is a static audit. No runtime behavior, database state, browser console or live refresh was changed or claimed.

## New findings

| ID | Area | Evidence | Impact | Queue prompt |
|---|---|---|---|---|
| A14-1 | Refresh duration contract | `AnalyticsRefreshStatusService.cs:210-214` uses `DefaultIfEmpty().Max()` for nullable job durations | No measured duration becomes `0`, and the UI can present fabricated `0 s` when an attempt exists | `RQ261` |
| A14-2 | Refresh banner degraded boundary | `AnalyticsRefreshStatusBanner.tsx:59` assumes `jobs` exists; `:57,83-147` renders process, step, errors and relation names directly; duration lacks finite validation | Partial payload can crash the trust surface, while internal operational details and non-finite values can reach users | `RQ261` |
| A14-3 | Executive KPI value/tone parity | `ExecutiveKpiRow.tsx:55-60` always marks Revenue good; `:63-80` classifies non-finite positive values; `:42-43` flattens insufficient readiness to neutral | Unavailable metrics can look healthy and visual trust can contradict displayed text | `RQ262` |

## De-duplication decisions

- `RQ187` owns cache-created time versus source refresh time; `RQ261` owns the separate refresh-duration aggregation and banner boundary.
- `RQ251` owns Inventory workflow/scheduler labels; `RQ261` owns the shared analytics refresh-status banner.
- `RQ258-RQ260` own trust-header and empty-state components, not `AnalyticsRefreshStatusBanner` or executive KPI cards.
- `RQ167` owns failed backend KPI responses and `RQ191` shared numeric formatting; `RQ262` owns value/tone/accessibility parity after those values reach the executive row.
- No standalone forecast/Trend/Shopify/Python work was added.

## Queue result

- Added `RQ261` and `RQ262` as `WAITING` prompts to `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`.
- The canonical `READY` prompt remains `RQ169`; this audit did not claim or promote queue work.
- Queue classification: `direct-user-request`.

## Required proof when executed

- Add failing-first backend/frontend tests for missing versus genuine-zero refresh duration and partial refresh payloads.
- Prove safe refresh copy with correlation support and no raw operational identifiers in ordinary analytics UI.
- Add executive KPI tests for null, genuine zero, non-finite values, partial/fallback/stale/unknown/insufficient states and value/tone/accessibility parity.
- Run focused tests, analytics guardrails, relevant builds, theme checks, no-console checks and `git diff --check`.
