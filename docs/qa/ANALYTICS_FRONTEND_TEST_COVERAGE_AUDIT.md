# Analytics Frontend Test Coverage Audit

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Status: test harness hardening + targeted frontend analytics coverage

## Goal

Improve analytics-screen test quality and coverage around the highest-risk user workflows:

- trust and data-quality context must not be hidden;
- fallback/gated/partial states must stay explicit;
- export/print must use the same rows, columns, filters and metadata visible on screen;
- inventory table actions must not accidentally open details or enqueue duplicate actions;
- supplier ranking table must preserve backend-sort/pagination contracts;
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
- Commit: `32dc7b4d5989c292e8ad16f46691e295e36bd4bb`

Added scripts:

- `npm run test:run`
- `npm run test:analytics`

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

## Commands

From `Klijent/clientapp`:

```bash
npm run test:analytics
npm run test:run
npm run check:analytics-guardrails
npm run build
```

## Remaining recommended coverage

### T-FE-01 - Page-level MSW tests for Color Sales Stats

Add page-level tests with mocked `getColorSalesStats` / `getStores` or MSW handlers:

- initial load shows filters, KPI cards, chart/table section and export toolbar;
- invalid date range shows explicit error;
- apply/reset filters preserve period/store/season semantics;
- table sort changes row order without changing source data;
- detail navigation saves analytics detail snapshot.

### T-FE-02 - Page-level tests for Analytics Actions outcome workflow

Add tests after RQ81/RQ86 semantics are stable:

- pending/not-measured states do not imply measured evidence;
- modal copy and disabled fields are evidence-first;
- status action buttons call correct API payloads;
- expanded details show ledger/notes without fake values.

### T-FE-03 - Data Quality table tests

Add tests after shared table migration or as page-local tests:

- health score states;
- top offender rows;
- returned-count vs total-count copy;
- no fake green when unknown/failed states are present.

### T-FE-04 - Decision Board aggregate page tests

Add page-level tests for:

- source states;
- product/supplier/blocker/action sections;
- expected impact display;
- action links;
- no fallback impact when expected impact is unavailable.

### T-FE-05 - Visual regression protocol

Implement or document screenshot tests for:

- analytics trust header;
- export modal;
- product decision table;
- inventory table;
- supplier ranking table;
- color sales screen;
- data quality table;
- actions/outcome table and modal.

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
