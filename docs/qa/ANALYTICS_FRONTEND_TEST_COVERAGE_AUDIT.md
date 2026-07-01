# Analytics Frontend Test Coverage Audit

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Status: test harness hardening + component-level, page-level and MSW API-contract analytics coverage

## Goal

Improve analytics-screen test quality and coverage around the highest-risk user workflows:

- trust and data-quality context must not be hidden;
- fallback/gated/partial states must stay explicit;
- export/print must use the same rows, columns, filters and metadata visible on screen;
- inventory table actions must not accidentally open details or enqueue duplicate actions;
- supplier ranking table must preserve backend-sort/pagination contracts;
- Color Sales Stats must preserve filter/sort/detail snapshot semantics;
- Analytics Actions must preserve status/outcome evidence semantics;
- Data Quality must preserve no-fake-green health and context semantics;
- Executive Decision Board must preserve source-state, partial, expected-impact and retry semantics;
- analytics service functions must preserve API route/query/body contracts;
- persisted print/detail snapshots must be TTL-safe and resilient to malformed storage.

## Test infrastructure reviewed

Existing frontend setup:

- `Klijent/clientapp/package.json` already had `vitest`, `@testing-library/react`, `@testing-library/jest-dom`, `jsdom`, `msw` and `whatwg-fetch`.
- `Klijent/clientapp/vitest.config.ts` already configured `jsdom`, globals and `src/setupTests.ts`.
- Existing inventory guardrail tests already covered forecast signal text and store-aware forecast row matching.

Infrastructure improvement applied:

- `Klijent/clientapp/src/setupTests.ts`
- Commit: `dd1c90d7ba095c54aba8ed18cfc854b75a810bf2`

What changed:

- switched to `@testing-library/jest-dom/vitest` for Vitest-native matchers;
- MSW now fails on unhandled requests to avoid silent real-network assumptions;
- added stable `matchMedia`, `ResizeObserver`, `scrollIntoView` and `crypto.randomUUID` fallbacks;
- clears `localStorage` and `sessionStorage` after each test.

Script improvement applied:

- `Klijent/clientapp/package.json`
- Commits:
  - `32dc7b4d5989c292e8ad16f46691e295e36bd4bb`
  - `dad85625bb75de3d7b035fe15af71726282067ab`

Added/updated scripts:

- `npm run test:run`
- `npm run test:analytics`

`test:analytics` now includes `src/pages`, so page-level specs such as `ColorSalesStatsPage.spec.tsx`, `AnalyticsActionsPage.spec.tsx`, `DataQualityPage.spec.tsx` and `ExecutiveDecisionBoardPage.spec.tsx` are included in the targeted analytics run.

## Coverage added or improved

### AnalyticsTrustHeader

File:

- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsTrustHeader.spec.tsx`

Commits:

- `87bdee1fdb7e3f06b383c2f77d71a94d72c059ed`
- `b0d29f802181f733a18f398633b6f479a853bb57`

Coverage:

- renders mode, title, description, status and freshness;
- renders data source and period metadata;
- renders data-quality summary counts without inventing missing values;
- validates default links for data quality and refresh status;
- validates methodology link;
- validates live refresh copy;
- validates fallback message and dataset lineage;
- validates fallback message takes priority over gated message;
- validates partial/stale warning;
- validates gated recommendation state;
- validates missing summary state.

Why it matters:

- This shared component is used by many analytics pages. These tests protect the core “no fake confidence / no silent fallback” UX contract.

### AnalyticsTableToolbar

File:

- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsTableToolbar.spec.tsx`

Commits:

- `8710836fca7dc080f7d5b69bfb3ec5146626d536`
- `052981d0b9f485241ade5fc4093c681664775a61`

Coverage:

- print button stores the exact resolved analytics payload in local storage;
- payload includes only declared columns and visible table rows;
- filters and metadata are preserved;
- export menu shows PDF/Excel/CSV options with premium descriptions;
- sync Excel export calls `generateExport` with correct payload and options;
- PDF preview uses `requestPrintPreview`, not direct export;
- async export waits for completion before download.

Why it matters:

- Export/print is a management/reporting workflow. These tests prevent export drift where printed/downloaded data differs from the screen.

### analyticsTableState

File:

- `Klijent/clientapp/src/services/__tests__/analyticsTableState.spec.ts`

Commits:

- `63d08663c5ac6595b8ddb8c852e1d9bd64f7a0c3`
- `eadcb8aab874a4020b736cb59e73dd90c8041a96`

Coverage:

- resolves export payload from declared columns only;
- supports column `getValue` functions;
- builds detail snapshots with highlighted currency/percent fields;
- stringifies booleans and nulls safely;
- persists print payloads;
- expires print payloads after TTL;
- persists and reads analytics detail snapshots;
- returns `null` for malformed detail snapshot JSON.

Why it matters:

- This service is the shared bridge between analytics tables, print/export and detail screens.

### InventoryItemsTable

File:

- `Klijent/clientapp/src/components/inventory/InventoryItemsTable.spec.tsx`

Commits:

- `77fffd165737e3d3fe113173591f46794fe22e22`
- `ce930c9df955e0f15fab4c3485022dc01bd71063`

Coverage:

- explicit insufficient-data signal text;
- readable stock-cover and sell-through labels;
- explain buttons for stock cover and sell-through;
- premium row count/header affordances;
- null metric renders as unavailable when not semantically insufficient-data;
- row click opens detail;
- keyboard enter opens detail;
- inline action button does not bubble into detail open;
- critical rows get explicit critical left accent;
- queued state disables duplicate action;
- slow-stock review button calls the right callback;
- pagination buttons are guarded at boundaries.

Why it matters:

- Inventory actions can create operational follow-ups. These tests prevent duplicate/accidental action behavior and preserve signal clarity.

### SupplierDecisionTable

File:

- `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDecisionTable.spec.tsx`

Commits:

- `dbbf9f5592ec3b314d0942586862fd0c3e3e73a3`
- `3db4d609715c0a3e8a80de0dc0f48249fe771286`
- `e7353c45bbfce42d11a4d53c5b3b7f0a3389eee9`

Coverage:

- renders premium ranking context and row-count summary;
- preserves shared toolbar integration;
- renders formatted supplier ranking rows;
- delegates sort requests to backend callback;
- row click selects supplier and opens detail;
- loading skeleton appears and disables pagination;
- empty state appears and guards pagination;
- pagination callbacks use correct previous/next page numbers.

Why it matters:

- Supplier ranking is backend-sorted/paginated. These tests prevent future UI refactors from accidentally turning it into misleading local behavior.

### ColorSalesStatsPage

File:

- `Klijent/clientapp/src/pages/ColorSalesStatsPage.spec.tsx`

Commits:

- `40f07df84d182114591ae5938525a5111357ef42`
- `ed95c5145d7cab6e7ce95e710452e4979e9e7b23`

Coverage:

- initial load renders the premium page shell, filters, KPI cards, chart/table sections and export context;
- API load uses a real date-range query shape without hard-coding unstable current-day values;
- data-quality notes stay visible when split/margin/unknown-color coverage is partial;
- invalid date ranges block a second analytics request;
- season and store filters are passed to the backend with exact query semantics;
- visible table sorting changes row order without changing export row count;
- expanding a color row shows decision detail metrics;
- opening full detail persists an analytics detail snapshot and navigates to the detail route.

Why it matters:

- Color Sales Stats is a full analytics screen with filters, KPI signals, sortable rows and detail snapshots. These tests protect the most important user-facing workflow after the premium UI pass.

### AnalyticsActionsPage

File:

- `Klijent/clientapp/src/pages/AnalyticsActionsPage.spec.tsx`

Commits:

- `a3f91cd7f917579408f3928f39998f319c3d0c04`
- `aba6ad96a77e87f42ca30cb60bb3e352dd89bc20`

Coverage:

- renders trust header, action KPIs, outcome summary warnings and action rows;
- query-string source filter initializes the list and keeps the Inventory shortcut visible;
- outcome-summary source bucket applies list filters;
- expanding details loads row evidence, notes and impact ledger;
- direct accept status sends the correct status update payload;
- pending outcome keeps measured-impact/date fields disabled and submits null measurement evidence;
- measured successful outcome parses decimal-comma impact values and submits measured date/note.

Why it matters:

- Action Queue is the operational bridge between analytics recommendations and real outcomes. These tests protect the evidence-first workflow and prevent accidental fake measured-impact states.

### DataQualityPage

File:

- `Klijent/clientapp/src/pages/DataQualityPage.spec.tsx`

Commits:

- `9dcc0805d6fd9ae6d3e3e3e92fa23db2a3f21562`
- `7deb028a74bba30499d1949bfddf8f9453976069`

Coverage:

- renders warning health, issue table, top offenders, trend and export context;
- keeps warning/no-fake-green status visible through the mocked trust header;
- preserves context query parameters from originating analytics tables;
- passes `dataScope` through issue, health and top-offender API calls;
- issue tabs and search form update URL-driven filters;
- intake view keeps read-only report context and calls both legacy and durable intake report loaders.

Why it matters:

- Data Quality is the guardrail screen that tells the rest of analytics when recommendations are unsafe. These tests protect the page from silently dropping warnings or context.

### ExecutiveDecisionBoardPage

Files:

- `Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`
- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.spec.tsx`

Commits:

- existing model tests already present in `src/pages/__tests__/ExecutiveDecisionBoardPage.spec.ts`
- `3f4f3a82a04244d28071279f87722b08a4ea8928`
- `3e6ad5bc245d866a396c1aa904976db6680f1c4a`

Coverage:

- model-level tests cover aggregate-to-board mapping, missing expected impact, partial source states and null payload behavior;
- page-level tests render metrics, sections, product/blocker/action/outcome cards and action links;
- page-level tests keep partial source warnings visible;
- missing expected impact renders as `Nije dostupno`, not fake `0 RSD`;
- empty aggregate shows explicit empty state and keeps empty reason in the trust header;
- failed aggregate load shows error state and retry re-loads the board.

Why it matters:

- Executive Decision Board is the combined decision surface. These tests protect the page from turning partial, warning or unavailable impact data into overconfident executive decisions.

### analytics API contract requests

File:

- `Klijent/clientapp/src/services/__tests__/analyticsApi.contract.spec.ts`

Commit:

- `415fe176d140bd29c45e9925cd1188cf5238e7f6`

Coverage:

- Decision Board aggregate route and query parameters, including default `dataScope=all`;
- Color Sales Stats route and query parameters, including explicit `dataScope=imported`;
- Analytics Actions list filters and outcome summary filters;
- Analytics Action outcome PATCH body, especially pending/null measured evidence;
- Data Quality issue list and top-offender routes with explicit scope.

Why it matters:

- Page-level tests mock services directly. These MSW tests protect the service contracts underneath, so route/query/body drift is caught before UI tests become misleading.

## Commands

From `Klijent/clientapp`:

```bash
npm run test:analytics
npm run test:run
npm run check:analytics-guardrails
npm run build
```

## Remaining recommended coverage

### T-FE-05 - Visual regression protocol

Implement or document screenshot tests for:

- analytics trust header;
- export modal;
- product decision table;
- inventory table;
- supplier ranking table;
- decision board cards;
- color sales screen;
- data quality table;
- actions/outcome table and modal.

### T-FE-07 - Accessibility-focused analytics pass

Add targeted accessibility tests for:

- keyboard navigation through analytics tables and card grids;
- focus trapping/return behavior in export and outcome modals;
- accessible names for card CTAs and chart summaries;
- empty/error/partial states announced with `role="status"` or appropriate landmarks.

### T-FE-08 - True end-to-end smoke

Add browser-level smoke tests after route/data seeds are stable:

- open `/analytics/decision-board`;
- drill into source/action links;
- verify no uncaught console errors;
- verify screenshot snapshots for light/dark themes.

## Validation status

Tests were not run in this GitHub connector session.

The new tests are designed to run with:

```bash
cd Klijent/clientapp
npm run test:analytics
```

Before merging, also run:

```bash
npm run check:analytics-guardrails
npm run build
```
