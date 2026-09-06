# Analytics Audit Round 5

Date: 2026-09-06
Repo: `ivanjovicic/Trendplus`
Queue state before sync: `RQ167` remained the existing `READY` prompt. No active claim was changed.

## Scope

This pass revisited the in-scope analytics decision surfaces and their nearest contracts, with emphasis on Dashboard period input boundaries, Daily Sales supplier concentration, numeric-state preservation and parity. Forecast-only, Trend Models, Shopify, scrapers and unrelated test functionality were excluded.

## Confirmed New Findings

| Prompt | Surface | Confirmed defect | User risk |
|---|---|---|---|
| RQ241 | `/analytics` custom period | `parseInputDate` substitutes the current time for malformed or empty `datetime-local` input. Range validation and selected-day calculation can therefore accept invalid input and the page can still request bootstrap data. | A user can receive a response for an invalid/ambiguous period or see old data beside a misleading period state. |
| RQ242 | Daily Sales supplier concentration | The page selects `Math.max` between the full-period total and the `TopN` supplier sum, then creates `Ostali` and shares from the synthetic basis. Contradictory totals are hidden instead of degraded/unavailable. | Concentration, Top 3/5 share and supplier distribution can be numerically plausible but false, especially with returns, negative rows, partial data or scope mismatch. |

## Coverage Repair

`RQ240` was expanded rather than duplicated: the same nullable OOS/low-stock-to-ratio coercion exists in `AnalyticsDashboard.tsx` as well as `AnalyticsDetails.tsx`. The prompt now owns the shared Details/Dashboard projection and parity tests.

## Evidence Map

- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:206-209` maps invalid date text to `new Date()`.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:645-653` uses that fallback for range validity and selected-day count.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:713-718` blocks only inverted ranges before calling `getDashboardBootstrap`.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:1071-1091` permits empty custom `datetime-local` values during editing.
- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx:694-702` selects `Math.max` as concentration denominator.
- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx:714-760` derives `Ostali`, Top 3/5 share and suppliers-to-80% from that selected basis.
- `Api/Models/DailySalesStatsDto.cs:5-16` exposes `TopN` and `TopSuppliers` with period metadata.
- `Api/Services/DailySalesStatsService.cs:400-438` truncates supplier ranking with `Take(topN)`.
- `Api/Services/DailySalesStatsService.cs:455-460` warns only for per-day top/others mismatch, not cross-period concentration inconsistency.
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx:824-832` repeats nullable inventory count coercion; `:2275-2285` renders the affected KPI cards.

## Existing Coverage Checked

- `DailySalesStatsPage.numericState.spec.ts`, `DailySalesStatsPage.spec.tsx` and `DailySalesStatsServiceTests.cs` cover normal reconciliation, empty/numeric states and per-day reconciliation, but not contradictory cross-period concentration totals.
- `AnalyticsDashboard.controlBar.spec.tsx`, `AnalyticsDashboard.integration.spec.tsx` and `AnalyticsDashboard.tableSystem.spec.tsx` cover normal controls/ranges and table behavior, but not empty/malformed custom date input before request dispatch.
- Recent history was checked for `AnalyticsDashboard.tsx`, `DailySalesStatsPage.tsx` and `DailySalesStatsService.cs`; prior fixes `RQ154`, `RQ155`, `RQ201`, `RQ202` and the broader period owners do not cover these two concrete boundaries.

## Non-Duplicates

- `RQ208` remains DST-safe valid-period day counting; `RQ241` is invalid input rejection.
- `RQ233` remains Supplier Sales denominator scope; `RQ242` is Daily Sales TopN/full-total reconciliation.
- `RQ240` now covers both Details and Dashboard rather than creating a second nullable inventory ratio prompt.
- No new prompt was created for missing/zero chart buckets because the inspected Dashboard bucket fill represents time bins with true zero observations and did not meet the evidence threshold for a confirmed defect.

## Queue Result

- Added `RQ241` and `RQ242` as `WAITING` with failing-first tests, bounded scope, dependencies and acceptance criteria.
- Preserved `RQ167 READY`.
- No prompt was claimed, promoted or marked complete.

## Validation and Delivery Truth

- `node scripts/check-prompt-queues.mjs` passed: 381 tasks.
- `git diff --check` passed.
- Runtime tests, analytics guardrails, backend/frontend builds, live refresh/schema/404 checks and browser console/theme/chart smoke were not run because this round changes only queue/audit documentation.
- No production code or tests were changed; no commit or push was performed.

## Residual Risk

`RQ240`, `RQ241` and `RQ242` are documented implementation prompts, not fixes. The affected screens remain unproven until the queue owners add failing-first regression tests and complete backend/frontend parity validation.
