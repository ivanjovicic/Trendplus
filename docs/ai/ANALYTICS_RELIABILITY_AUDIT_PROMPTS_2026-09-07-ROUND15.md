# Analytics Reliability Audit Prompts: 2026-09-07 Round 15

Scope: direct-user-request audit of production analytics table, detail, print and export paths. Standalone Trend, forecast, Shopify/vendor integrations, Python/ML evaluation and test-only features were excluded.

## Audit method

- Applied the canonical repository, architecture, validation and queue rules already loaded for this direct request.
- Reviewed the queue through `RQ262` and de-duplicated against broad parity owner `RQ145`, historical RQ44/RQ46 work, numeric-state prompts and report/scheduler-specific prompts.
- Inspected `AnalyticsTableToolbar`, `exportApi`, `analyticsTableState`, `AnalyticsPrintPage`, `AnalyticsDetailPage`/`AnalyticsDetailView`, related types, nearest tests and Git history/blame.
- This is a static audit. No runtime behavior, document generation, browser download or print output was changed or claimed.

## New findings

| ID | Area | Evidence | Impact | Queue prompt |
|---|---|---|---|---|
| A15-1 | Export operation status | `AnalyticsTableToolbar.tsx:254-257,338-347` renders exception text with success tone/icon; `:211-250` accepts missing preview/download URLs as silent or successful states | Failed/incomplete exports can look successful and users can be told a document was downloaded when no artifact exists | `RQ263` |
| A15-2 | Shared numeric serialization | `analyticsTableState.ts:32-35,70-82,113-130,168-174` stringifies non-finite values in detail/metadata while JSON persistence/request serialization changes them to null | Table, detail, print and export can represent the same invalid value differently | `RQ264` |
| A15-3 | Generic print formatting | `AnalyticsPrintPage.tsx:93-136` prints metadata and cells directly with `String(...)`, ignoring column data type/format/locale | Currency, percent, date and unavailable states can differ from the screen and exported report | `RQ264` |

## De-duplication decisions

- Historical RQ44 fixed selected table/detail formatting but left non-finite raw-string fallback and generic print formatting unchanged.
- RQ46/broad metadata work does not own document operation success/failure presentation.
- `RQ145` remains the global parity owner; `RQ264` is the concrete shared serialization/print boundary needed before global parity can pass.
- `RQ196-RQ197` remain Inventory scheduling and row-cap owners; `RQ263` covers generic document operation truth only.
- `RQ263` and `RQ264` separate operation state from exported data value semantics.

## Queue result

- Added `RQ263` and `RQ264` as `WAITING` prompts to `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`.
- The canonical `READY` prompt remains `RQ169`; no queue item was claimed or promoted.
- Queue classification: `direct-user-request`.

## Required proof when executed

- Failing-first export tests for errors, timeout, poisoned/failed jobs, absent artifact URLs and blocked preview/download.
- Failing-first shared-output tests for null, genuine zero, `NaN`, `Infinity`, `-Infinity`, numeric strings and all declared data types.
- Exact table/detail/print/export/report parity fixture with period, scope, freshness, quality, fallback and recommendation metadata.
- Focused frontend tests, analytics guardrails, build, theme/no-console checks and `git diff --check`.
