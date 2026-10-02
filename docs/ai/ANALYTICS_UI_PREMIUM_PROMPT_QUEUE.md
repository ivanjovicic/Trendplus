# Analytics UI Premium Prompt Queue

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none (P-UI-32 DONE; re-enter idle recovery)
Purpose: make shared backoffice and analytics navigation, controls, tables, responsive workflows and dashboard UX premium without mixing presentation work with analytics correctness fixes.

Use with:

- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/PROMPT_TOKEN_ECONOMY_AND_LINT.md`
- `docs/qa/ANALYTICS_UI_PREMIUM_AUDIT.md`

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| P-UI-05 | DONE | analytics-ui-visual-regression | Add screenshot/manual visual review protocol before broad visual refactors |
| P-UI-06 | DONE | global-command-header | Add full command/search/breadcrumb/notification header system |
| P-UI-01 | DONE | analytics-menu-ia | Redesign analytics menu information architecture |
| P-UI-02 | DONE | analytics-control-bar | Create shared premium analytics control bar |
| P-UI-03 | DONE | analytics-table-system | Standardize analytics table density, sticky headers, numeric alignment and trust metadata |
| P-UI-07 | DONE | supplier-analytics-table | Migrate supplier analytics tables to shared premium table system |
| P-UI-08 | DONE | inventory-control-surface | Consolidate inventory page filters/export/scheduler controls |
| P-UI-04 | DONE | analytics-command-center | Redesign analytics dashboard above-the-fold command center |
| P-UI-18 | DONE | legacy-analytics-modernization | Modernize SupplierFootwearAnalyticsPage chrome (TrustHeader + ControlBar + DataTable) |
| P-UI-19 | DONE | analytics-ui-regression-hardening | Verify recent React chrome migrations across shared analytics components and modernized pages |
| P-UI-20 | DONE | analytics-ui-trust-state-proof | Grouped ErrorState/EmptyState/TrustHeader proof on Daily/Color/ShoeType/Supplier/Actions pages |
| P-UI-21 | DONE | analytics-ui-empty-kpi-honesty | Hide KPI totals on empty success; use shared ErrorState on Actions list failure |
| P-UI-22 | DONE | analytics-ui-remaining-trust-chrome | Remaining decision pages empty/error chrome after P-UI-21 |
| P-UI-23 | WAITING | frontend-lint-baseline | Reduce lint errors in bounded, trust-sensitive slices without broad rewrites |
| P-UI-24 | DONE | responsive-ui-browser-baseline | Establish measured 320/375/768/1024/1280 browser evidence using existing Puppeteer |
| P-UI-25 | DONE | responsive-ui-foundation | Responsive type/control/input/focus foundation |
| P-UI-26 | WAITING | responsive-ui-shell | Compact mobile header and accessible drawer |
| P-UI-27 | WAITING | responsive-ui-primitives | Modal, InfoTip, tabs and touch-safe shared primitives |
| P-UI-28 | DONE | responsive-filter-bar | Responsive Inventory filter pilot with semantics frozen |
| P-UI-29 | DONE | responsive-analytics-table | Responsive AnalyticsDataTable pilot with column priority |
| P-UI-30 | WAITING | mobile-data-entry | Mobile sales/goods/nivelacija data-entry workflow |
| P-UI-31 | WAITING | supplier-overview-responsive | Supplier overview responsive migration |
| P-UI-32 | DONE | product-decision-responsive | Product Decision Center responsive + measured row rendering |
| P-UI-33 | WAITING | central-actions-responsive | Central Actions responsive migration |
| P-UI-34 | WAITING | analytics-overview-responsive | Dashboard and Daily Sales responsive migration |
| P-UI-35 | WAITING | nivelacija-responsive | Pre/Post and Pre-Nivelacija responsive migration |
| P-UI-36 | WAITING | supplier-segment-responsive | Supplier Hub, Shoe Type and Color responsive migration |
| P-UI-37 | WAITING | responsive-long-tail | Article List and bounded long-tail responsive cleanup |
| P-UI-38 | WAITING | responsive-ui-regression-gates | Responsive regression gates and bounded CSS hygiene |

---

## P-UI-05 - Analytics visual regression protocol

Status: DONE
Priority: P0
Type: docs/tests
Feature family: analytics-ui-visual-regression
Parallel-safe: yes
Owner: Cursor-Composer
Local lock: `.ai/task-locks/P-UI-05-cursor.lock.md` (removed after DONE)
Commit suggestion: `docs(ui): add analytics visual regression protocol`

### Why

Premium UI changes need rendered verification. GitHub connector code edits cannot prove that sidebar, global header, trust header, dashboard, export modal and tables look correct in dark/light themes.

### Scope only

- `docs/Frontend/` or `docs/qa/`
- optional Playwright/screenshot test files if the app already has a test harness

### Do

1. Add a visual review checklist or screenshot protocol for:
   - sidebar expanded/collapsed/mobile
   - global header desktop/tablet/mobile
   - analytics trust header in recommendation/signal/report modes
   - analytics dashboard overview
   - export toolbar menu/modal
   - product decision table
   - inventory table
   - supplier table
   - data quality table
2. Include dark and light theme expectations.
3. Include viewport matrix: mobile, tablet, desktop.
4. State exact validation command if automated, or manual screenshot evidence fields if not.

### Acceptance

- Future UI tasks have a repeatable way to verify visual regressions.

### Completion note

- Date: 2026-08-06
- Agent: Cursor-Composer
- Added: `docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md`, `docs/qa/ANALYTICS_UI_VISUAL_REVIEW_EVIDENCE_TEMPLATE.md`
- Also linked from `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md` and `docs/qa/ANALYTICS_UI_PREMIUM_AUDIT.md`
- Contract: light+dark × mobile/tablet/desktop; surfaces A/B/C (chrome, trust/dashboard, export/tables); route smoke is baseline only
- Automation: none in repo (vitest only); Playwright deferred with ID mapping noted
- Checks: docs-only; `node scripts/check-prompt-queues.mjs` after queue update
- Next: `P-UI-06` READY

---

## P-UI-06 - Global command header system

Status: DONE
Ready after: P-UI-05
Priority: P1
Type: frontend/design/tests
Feature family: global-command-header
Parallel-safe: no
Owner: Cursor
Local lock: `.ai/task-locks/P-UI-06-cursor.lock.md` (removed after DONE)
Commit suggestion: `feat(ui): add global command header system`

### Why

The global header now has premium styling and consistent status flags, but it still lacks a full premium application command model.

### Scope only

- `Klijent/clientapp/src/layout/components/HeaderStatus.tsx`
- optional new shared components under `Klijent/clientapp/src/layout/components/`
- optional route/nav helper extracted from `navConfig.ts`
- tests or visual protocol output

### Do not touch

- backend status polling semantics
- worker/Redis toggle API behavior
- analytics formulas
- route paths/aliases

### Do

1. Add or design a global command/search launcher for quickly opening pages/actions.
2. Add robust route-aware breadcrumbs for dynamic/detail routes beyond simple `NAV_GROUPS` matches.
3. Add a notification/action inbox concept for backend, worker, Redis and analytics warnings.
4. Add user/account/store context only if source data exists; otherwise leave a prepared slot, not fake data.
5. Verify desktop/tablet/mobile header behavior.

### Acceptance

- Header feels like a premium command center, not only a status strip.
- Existing status/toggle behaviors remain unchanged.

### Completion note

- Date: 2026-08-06
- Commit: not created; base HEAD `568f03c65891e96bf2c0f27592aeea96c2e58361`
- Changed files:
  - `Klijent/clientapp/src/layout/components/HeaderStatus.tsx`
  - `Klijent/clientapp/src/layout/components/headerNavigation.ts`
  - `Klijent/clientapp/src/layout/components/__tests__/HeaderStatus.spec.tsx`
  - `Klijent/clientapp/src/layout/components/__tests__/headerNavigation.spec.ts`
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
  - `.ai/runs/2026-08-06-P-UI-06-evidence.md`
- Checks:
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
  - `cd Klijent/clientapp && npm run test -- --run src/layout/components/__tests__/HeaderStatus.spec.tsx src/layout/components/__tests__/headerNavigation.spec.ts` - pass
- Notes:
  - Added route-aware breadcrumbs for dynamic detail paths, a searchable global command launcher, an inbox for backend/worker/Redis/analytics signals, and prepared context slots without fake user/store data.
  - Existing worker/Redis status toggles and theme link behavior were preserved.
  - Targeted tests still emit pre-existing React `act(...)` warnings from the shared flag components, but they pass.
- Remaining:
  - P-UI-01 `analytics-menu-ia`
  - Keep using the shared route helper if later header prompts need dynamic breadcrumb coverage.

---

## P-UI-01 - Analytics menu information architecture

Status: DONE
Ready after: P-UI-06
Priority: P1
Type: frontend/tests
Feature family: analytics-menu-ia
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-01-<agent>.lock.md` (removed after DONE)
Commit suggestion: `feat(ui): restructure analytics navigation`

### Why

The analytics sidebar group is currently a long flat list mixing executive surfaces, operational modules, old detail pages and support/report screens.

### Scope only

- `Klijent/clientapp/src/layout/navConfig.ts`
- `Klijent/clientapp/src/layout/components/Sidebar.tsx` only if nested/subgroup support is needed
- route smoke tests

### Do not touch

- route paths/aliases unless redirects are preserved
- page implementations
- analytics formulas

### Do

1. Propose IA groups:
   - Executive
   - Decisions
   - Operations
   - Data Quality
   - Reports / Legacy
2. Preserve all existing routes.
3. Add labels/badges for legacy/support screens.
4. Update route smoke tests if nav assumptions change.

### Acceptance

- Analytics navigation is easier to scan and still preserves route coverage.

### Completion note

- Date: 2026-08-06
- Commit: not created; base HEAD `568f03c65891e96bf2c0f27592aeea96c2e58361`
- Changed files:
  - `Klijent/clientapp/src/layout/navConfig.ts`
  - `Klijent/clientapp/src/layout/components/Sidebar.tsx`
  - `Klijent/clientapp/src/layout/components/headerNavigation.ts`
  - `Klijent/clientapp/src/layout/components/HeaderStatus.tsx`
  - `Klijent/clientapp/src/layout/__tests__/navConfig.spec.ts`
  - `Klijent/clientapp/src/layout/components/__tests__/Sidebar.spec.tsx`
  - `Klijent/clientapp/src/layout/components/__tests__/headerNavigation.spec.ts`
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
  - `.ai/runs/2026-08-06-P-UI-01-evidence.md`
- Checks:
  - `cd Klijent/clientapp && npm run test -- --run src/layout/__tests__/navConfig.spec.ts src/layout/components/__tests__/Sidebar.spec.tsx src/layout/components/__tests__/headerNavigation.spec.ts src/layout/components/__tests__/HeaderStatus.spec.tsx` - pass
  - `cd Klijent/clientapp && npm run build` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
- Notes:
  - Split analytics navigation into Executive, Decisions, Operations, Data Quality, and Reports / Legacy sections while preserving all routes.
  - Added `sidebarLabel` support so the sidebar shows the new IA while header breadcrumbs stay on the broader `Analitika` label.
  - Marked legacy/support screens with badges and kept route launcher grouping aligned to the sidebar IA.
  - Existing header inbox still shows analytics signal content via the new `analytics-*` group ids.
- Remaining:
  - P-UI-02 `analytics-control-bar`
  - Keep route-preserving smoke coverage in sync if any analytics route aliases are changed later.

---

## P-UI-02 - Shared analytics control bar

Status: DONE
Ready after: P-UI-05
Priority: P1
Type: frontend/component/tests
Feature family: analytics-control-bar
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/P-UI-02-codex.lock.md` (removed after DONE)
Commit suggestion: `feat(ui): add shared analytics control bar`

### Why

Date presets, refresh buttons, store/supplier filters, search controls and export controls are visually inconsistent across analytics pages.

### Scope only

- new shared component under `Klijent/clientapp/src/components/analytics/`
- migrate one page first, preferably `AnalyticsDashboard` or one smaller page
- tests if available

### Do

1. Create `AnalyticsControlBar` or equivalent.
2. Support title/description, filters, primary action, secondary actions and metadata chips.
3. Migrate only one page in the first prompt.
4. Leave follow-up prompts for other pages.

### Acceptance

- One analytics page uses a consistent premium control bar without breaking existing filters.

### Completion note

- Date: 2026-08-06
- Commit: not created; base HEAD `ad1d86bfd15253c93f09a27b2c305342ea770332`
- Changed files:
  - `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.tsx`
  - `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.css`
  - `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsControlBar.spec.tsx`
  - `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`
  - `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.controlBar.spec.tsx`
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
  - `.ai/runs/2026-08-06-P-UI-02-evidence.md`
- Checks:
  - `cd Klijent/clientapp && npm run build` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run test -- --run src/components/analytics/__tests__/AnalyticsControlBar.spec.tsx src/pages/__tests__/AnalyticsDashboard.controlBar.spec.tsx src/layout/components/__tests__/HeaderStatus.spec.tsx` - pass
- Notes:
  - Added a shared premium control bar with title/description, metadata chips, filter fields, and primary/secondary action slots.
  - Migrated `AnalyticsDashboard` to the shared surface for period, store, supplier, freshness context, and refresh actions while preserving existing dashboard fetch behavior.
  - Added targeted component and page tests; the dashboard test now scopes duplicate links to the new control bar and aligns `AbortSignal` with the browser test environment.
- Remaining:
  - P-UI-03 `analytics-table-system`

---

## P-UI-03 - Shared analytics table system

Status: DONE
Ready after: P-UI-05 and RQ57/RQ58 if inventory table is touched
Priority: P1
Type: frontend/component/tests
Feature family: analytics-table-system
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/P-UI-03-codex.lock.md` (removed after DONE)
Commit suggestion: `feat(ui): standardize analytics tables`

### Why

Analytics tables vary by page. Premium analytics needs consistent sticky headers, numeric alignment, density controls, truncation labels, empty states and export metadata.

### Scope only

- shared table style/component files
- migrate one table only in first prompt
- do not change business values or sorting semantics without a reliability prompt

### Do

1. Define shared table class/component.
2. Align numeric/currency/percent columns right.
3. Keep sticky header and horizontal scroll.
4. Add row count/truncation metadata near toolbar.
5. Prove export still uses the same row set.

### Acceptance

- One migrated analytics table looks premium and preserves data/export parity.

### Completion note

- Date: 2026-08-06
- Commit: not created; base HEAD `ad1d86bfd15253c93f09a27b2c305342ea770332`
- Changed files:
  - `Klijent/clientapp/src/components/analytics/AnalyticsDataTable.tsx`
  - `Klijent/clientapp/src/components/analytics/AnalyticsDataTable.css`
  - `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsDataTable.spec.tsx`
  - `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`
  - `Klijent/clientapp/src/pages/AnalyticsDashboard.css`
  - `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.tableSystem.spec.tsx`
  - `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.controlBar.spec.tsx`
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
  - `.ai/runs/2026-08-06-P-UI-03-evidence.md`
- Checks:
  - `cd Klijent/clientapp && npm run build` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run test -- --run src/components/analytics/__tests__/AnalyticsDataTable.spec.tsx src/components/analytics/__tests__/AnalyticsTableToolbar.spec.tsx src/pages/__tests__/AnalyticsDashboard.tableSystem.spec.tsx src/pages/__tests__/AnalyticsDashboard.controlBar.spec.tsx src/layout/components/__tests__/HeaderStatus.spec.tsx` - pass
  - `cd Klijent/clientapp && npm run check:encoding` - pass
- Notes:
  - Added a shared premium table surface with sticky headers, right-aligned numeric cells, shared metadata pills, and a reusable horizontal-scroll shell.
  - Migrated only the `AnalyticsDashboard` top-products table in this prompt and kept the existing `AnalyticsTableToolbar` export payload tied to the same `topRows` array as the rendered table.
  - Added a dashboard regression test that proves the rendered row count stays aligned with the export toolbar row count.
- Remaining:
  - P-UI-07 `supplier-analytics-table`

---

## P-UI-07 - Supplier analytics table migration

Status: DONE
Ready after: P-UI-03
Priority: P1
Type: frontend/component/tests
Feature family: supplier-analytics-table
Parallel-safe: no
Owner: Cursor
Local lock: removed after DONE
Commit suggestion: `feat(ui): migrate supplier analytics table`

### Why

Supplier decision/sales tables still use page-specific styles. Premium analytics should use one table contract for ranking, detail and export surfaces.

### Scope only

- one supplier analytics table first, preferably `SupplierDecisionTable`
- shared table component/style from P-UI-03
- tests or visual protocol output

### Do not touch

- backend pagination/sort semantics
- recommendation formulas
- export values

### Do

1. Migrate one supplier table to the shared premium table system.
2. Preserve row click/detail behavior.
3. Preserve `AnalyticsTableToolbar` payload.
4. Verify numeric alignment and sticky header.

### Acceptance

- One supplier analytics table matches the premium table system without changing data semantics.

### Completion note

- Date: 2026-08-09
- Agent: Cursor
- Changed files:
  - `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx`
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/SupplierDecisionHubPage.spec.tsx` - pass
  - `cd Klijent/clientapp && npm run build` - pass
- Notes:
  - Live supplier ranking table now uses `AnalyticsDataTable` with row-count metadata and the shared premium scroll/sticky shell.
  - `AnalyticsTableToolbar` payload, row click/detail behavior, and backend sort/export values were preserved.
  - The regression test proves the shared table shell is rendered and numeric columns keep the shared alignment class.
  - Remaining risk: no manual screenshot/pixel review was run in this session.
- Next: `P-UI-08` READY

---

## P-UI-08 - Inventory page control surface consolidation

Status: DONE
Ready after: P-UI-02 and RQ57/RQ58 if risk sort labels are touched
Priority: P1
Type: frontend/component/tests
Feature family: inventory-control-surface
Parallel-safe: no
Owner: Cursor
Local lock: removed after DONE
Commit suggestion: `feat(ui): consolidate inventory controls`

### Why

The inventory table panel is now more premium, but the page still has many separate filter, sort, export, print, scheduler and operations controls.

### Scope only

- `InventoryPage.tsx`
- existing inventory subcomponents only if necessary
- shared `AnalyticsControlBar` from P-UI-02

### Do not touch

- inventory API contracts
- risk calculation semantics
- forecast/rebalance/null-evidence logic

### Do

1. Group search, store, supplier, page size and sort controls into a premium control surface.
2. Keep export/print/scheduler controls visible but secondary.
3. Preserve all existing API calls and state.
4. Clearly label page-local risk sort if RQ57/RQ58 has not been completed.

### Acceptance

- Inventory controls are easier to scan and still behave identically.

### Completion note

- Date: 2026-08-09
- Agent: Cursor
- Changed files:
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/InventoryPage.queueStatus.spec.tsx`
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/InventoryPage.queueStatus.spec.tsx` - pass
  - `cd Klijent/clientapp && npm run build` - pass
- Notes:
  - Grouped inventory search, store, supplier, page-size and sort controls into `AnalyticsControlBar` so the page now reads like one premium control surface instead of scattered inputs.
  - Kept export/print/scheduler as the secondary section and preserved the existing inventory API/state flow.
  - Added a regression test for the shared control bar, explicit local risk-sort labels, and the central action queue link.
  - Remaining risk: no manual screenshot/pixel review was run in this session.
- Next: `P-UI-04` READY

---

## P-UI-04 - Dashboard command center redesign

Status: DONE
Ready after: P-UI-02 and P-UI-03
Priority: P2
Type: frontend/design/tests
Feature family: analytics-command-center
Parallel-safe: no
Owner: Cursor
Local lock: removed after DONE
Commit suggestion: `feat(ui): redesign analytics command center`

### Why

`AnalyticsDashboard` already has strong data and action concepts, but the above-the-fold area is dense. A premium command center should make the weekly decision path obvious.

### Scope only

- `AnalyticsDashboard.tsx`
- `AnalyticsDashboard.css`
- screenshot/visual protocol from P-UI-05

### Do not touch

- data loading/fetch contracts
- analytics formulas
- action queue semantics

### Do

1. Redesign above-the-fold as:
   - business KPI strip
   - this-week action cockpit
   - data trust/freshness panel
   - risk/loss highlights
2. Keep existing trust header and empty/error states.
3. Preserve all current links/actions.
4. Verify mobile/tablet/desktop.

### Acceptance

- The dashboard reads like a premium executive cockpit without changing analytics semantics.

### Completion note

- Date: 2026-08-09
- Agent: Cursor
- Changed files:
  - `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`
  - `Klijent/clientapp/src/pages/AnalyticsDashboard.css`
  - `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.controlBar.spec.tsx`
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
  - `.ai/task-locks/P-UI-04-cursor.lock.md` (removed after DONE)
- Checks:
  - `git diff --check` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/AnalyticsDashboard.controlBar.spec.tsx` - pass
  - `cd Klijent/clientapp && npm run build` - pass
- Notes:
  - Reworked the analytics dashboard above-the-fold into a command center with a premium hero, KPI strip, weekly action cockpit, trust/freshness panel, and risk/loss preview.
  - Preserved the existing data loading, trust header, refresh banner, empty/error behavior, and action links while making the top fold easier to scan.
  - Added a regression test for the command center, KPI strip, trust panel, and risk preview so the layout does not drift back to scattered blocks.
- Remaining risk: no manual screenshot/pixel review was run in this session.
- Next: none

---

## P-UI-16 - Pre-nivelacija priority: no fake reliability + empty/copy polish

Status: DONE
Ready after: P-UI-15 DONE
Priority: P1
Type: frontend/copy/ux/tests
Feature family: pre-nivelacija-priority-signal-copy
Parallel-safe: yes
Owner: Cursor
Local lock: `.ai/task-locks/P-UI-16-cursor.lock.md` (released on DONE)
Commit suggestion: `fix(ui): stop showing missing reliability as Nisko on pre-nivelacija priority`
Canonical detail: `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE_LEAST_IMPROVED_ADDENDUM.md` (P-UI-16)

### Problem

On Prioriteti pre-nivelacije, missing reliability is shown as **"Nisko"** in the table because null is coerced to `0` and the pill ignores `reliabilityAvailable`. Empty-state copy references a sales period this screen does not have. Several strings lack Serbian diacritics / use English chrome.

### Evidence

- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx` (~333–336, ~827–847, ~640–644, ~275, ~701, ~705, ~747, ~1016)
- Detail already correct (~928) via `RECOMMENDATION_SIGNAL_UNAVAILABLE`
- Audit: `.ai/runs/2026-08-11-P-UI-16-audit-promote.md`

### Scope

- Page TSX/CSS + focused tests for reliability/empty/copy only
- Out of scope: backend formulas, filter catalogs, ControlBar/DataTable migration (`P-UI-17`)

### Read first

- `AGENTS.md`, `docs/ai/PROMPT_QUEUE_PROTOCOL.md`, `docs/ai/ENCODING_AND_TEXT_SAFETY.md`
- Full prompt body in least-improved addendum (P-UI-16)

### Do

1. Unavailable reliability → unavailable label/pill (not Nisko).
2. Fix empty-state copy for SKU priority filters (no sales-period wording).
3. Fix listed diacritics / English toolbar title on this page.
4. Do not change recommendation status, scores, or API payloads.

### Tests

```powershell
cd Klijent/clientapp
npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx
```

Assert null reliability does not render “Nisko”.

### Acceptance

- No fake weak reliability; empty/copy polished; no API/formula changes.

### Dependencies

- P-UI-15 DONE; path-safe vs BCI/STAB/RQ exclusive work

### Completion note (2026-08-11)

- Unavailable reliability → “Nije dostupno” / `signal-na` (not “Nisko”); empty/copy polished; focused vitest 5/5.
- Next READY: `P-UI-18`

---

## P-UI-17 - PreNivelacijaPriorityPage chrome modernization

Status: DONE
Ready after: P-UI-16 DONE
Priority: P2
Type: frontend/design/tests
Feature family: legacy-analytics-modernization
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-17-codex.lock.md` (released on DONE)
Commit suggestion: `feat(ui): modernize PreNivelacijaPriorityPage chrome`
Canonical detail: `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE_LEAST_IMPROVED_ADDENDUM.md` (P-UI-17)

### Problem

Page still uses local `pnp-decision-filters` / table wrap; tooltip hardcodes trend hex colors.

### Evidence

- `PreNivelacijaPriorityPage.tsx` (~575 filters; ~120–123 hardcoded tooltip colors)
- No AnalyticsControlBar/DataTable on this page; ProdajaPrePostNivelacije already migrated (P-UI-15)

### Scope

- ControlBar + DataTable migration + theme-token tooltip colors + focused tests
- Out of scope: inventing filter catalogs; redoing P-UI-16

### Read first

- Premium queue + least-improved addendum P-UI-17; prior one-page migrations

### Do

1. Confirm TrustHeader.
2. Migrate filters → AnalyticsControlBar.
3. Migrate priority table → AnalyticsDataTable.
4. Replace hardcoded tooltip colors with CSS variables.
5. Keep chart/recommendation semantics; preserve P-UI-16 behavior.

### Tests

```powershell
cd Klijent/clientapp
npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx
```

### Acceptance

- Shared chrome without semantic drift; theme tokens for tooltip; P-UI-16 still green.

### Dependencies

- P-UI-16 DONE; promote to READY only after that (one READY per P-UI program)

### Completion note

- Date: 2026-08-11
- Agent: codex
- Commit: `1d0561e`
- Changed files:
  - `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
  - `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.css`
  - `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`
- Checks:
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
- Notes:
  - Migrated the page to shared `AnalyticsControlBar` and `AnalyticsDataTable` chrome.
  - Replaced tooltip hardcoded trend colors with theme tokens.
  - Kept P-UI-16 reliability semantics intact.
- Remaining:
  - none

---

## P-UI-18 - SupplierFootwearAnalyticsPage chrome modernization

Status: DONE
Ready after: P-UI-17 DONE
Priority: P2
Type: frontend/design/tests
Feature family: legacy-analytics-modernization
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-18-codex.lock.md` (released on DONE)
Commit suggestion: `feat(ui): modernize SupplierFootwearAnalyticsPage chrome`
Canonical detail: `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE_LEAST_IMPROVED_ADDENDUM.md` (P-UI-18)

### Problem

`SupplierFootwearAnalyticsPage` still uses page-local `sf-decision-filters` and `sf-decision-table-wrap` instead of shared `AnalyticsControlBar` and `AnalyticsDataTable`. It also needs an `AnalyticsTrustHeader` pattern that stays compatible with the embedded `SupplierConsolidatedPage` flow.

### Evidence

- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`
- No `AnalyticsControlBar`, `AnalyticsDataTable` or `AnalyticsTrustHeader` imports on this page
- `SupplierConsolidatedPage` embeds this page with `sharedFilters`

### Scope

- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`
- `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.css`
- focused tests under `Klijent/clientapp/src/pages/__tests__/`

### Read first

- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE_LEAST_IMPROVED_ADDENDUM.md`
- `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`

### Do

1. Add or confirm `AnalyticsTrustHeader` in full-page mode while keeping embedded mode compatible.
2. Migrate filters to `AnalyticsControlBar`.
3. Migrate the supplier priority table to `AnalyticsDataTable`.
4. Preserve embedded/sharedFilters behavior used by `SupplierConsolidatedPage`.
5. Keep chart and recommendation semantics unchanged.
6. Add or update focused tests for the page chrome and embedded wrapper if needed.

### Tests

```powershell
cd Klijent/clientapp
npm run test -- --run src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx src/pages/__tests__/SupplierConsolidatedPage.spec.tsx
```

### Acceptance

- Supplier footwear analytics uses shared premium chrome without breaking embedded/sharedFilters behavior.
- Charts and recommendation semantics stay intact.

### Dependencies

- `P-UI-17` DONE
- Path-safe vs higher-priority BCI/STAB/RQ exclusive work

### Completion note

- Date: 2026-08-11
- Agent: codex
- Commit: `2fa16a5`
- Changed files:
  - `Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx`
  - `Klijent/clientapp/src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx`
- Checks:
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
- Notes:
  - Added shared `AnalyticsTrustHeader`, `AnalyticsControlBar`, and `AnalyticsDataTable` chrome.
  - Kept the embedded `SupplierConsolidatedPage` trust metadata path intact.
- Remaining:
  - none

---

## P-UI-19 - Analytics React chrome regression hardening

Status: DONE
Ready after: P-UI-18 DONE
Priority: P2
Type: frontend/tests/ux-regression
Feature family: analytics-ui-regression-hardening
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-19-<agent>.lock.md`
Commit suggestion: `test(ui): harden analytics chrome regression coverage`

### Problem

Recent React commits modernized legacy analytics pages and shared chrome, but the queue has no follow-up that proves the migrated pages still behave consistently as a group. The risk is not a known broken page; it is silent drift across `AnalyticsTrustHeader`, `AnalyticsControlBar`, `AnalyticsDataTable`, embedded supplier flows, and older analytics routes.

### Evidence

- Recent React commits include `P-UI-16`/`P-UI-17`/`P-UI-18` work on `PreNivelacijaPriorityPage` and `SupplierFootwearAnalyticsPage`.
- Shared chrome tests exist for `AnalyticsControlBar`, `AnalyticsDataTable`, `AnalyticsTrustHeader`, `HeaderStatus`, `Sidebar`, and the modernized page specs.
- `P-UI-18` completion notes only the focused supplier footwear page test, guardrails and build. It does not record a grouped regression pass across the shared chrome and both recently migrated page families.

### Scope

- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsControlBar.spec.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsDataTable.spec.tsx`
- `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsTrustHeader.spec.tsx`
- `Klijent/clientapp/src/layout/components/__tests__/HeaderStatus.spec.tsx`
- `Klijent/clientapp/src/layout/components/__tests__/Sidebar.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierConsolidatedPage.spec.tsx`
- affected page/component files only if a real regression is reproduced

### Do Not Touch

- analytics formulas, score semantics or API payloads
- backend routes
- broad visual redesign beyond fixing reproduced regressions
- unrelated premium UI pages that are already covered by their own prompts

### Do

1. Run the grouped React regression suite for the shared analytics chrome and the two latest migrated page families.
2. Record whether existing `act(...)` warnings still appear and whether they are harmless, newly introduced or actionable.
3. If a test fails or a reproducible UI regression is found, make the smallest page/component fix and add/adjust focused assertions.
4. Verify embedded `SupplierConsolidatedPage` still preserves shared filter behavior after `SupplierFootwearAnalyticsPage` modernization.
5. Keep the completion note tied to a durable run log under `.ai/runs/`.

### Tests

```powershell
cd Klijent/clientapp
npm run test -- --run src/components/analytics/__tests__/AnalyticsControlBar.spec.tsx src/components/analytics/__tests__/AnalyticsDataTable.spec.tsx src/components/analytics/__tests__/AnalyticsTrustHeader.spec.tsx src/layout/components/__tests__/HeaderStatus.spec.tsx src/layout/components/__tests__/Sidebar.spec.tsx src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx src/pages/__tests__/SupplierConsolidatedPage.spec.tsx
npm run check:analytics-guardrails
npm run build
```

### Acceptance

- The latest React chrome migrations have a grouped regression evidence note.
- No shared chrome regression is left untriaged.
- Any remaining warnings are explicitly classified with risk and follow-up owner.
- Completion note references the exact run log path.

### Dependencies

- Path-safe vs higher-priority BCI/STAB/RQ/QDB runtime work.
- Do not promote another P-UI prompt until this one is DONE or explicitly demoted by the owner.

### Completion note

- Date: 2026-08-13
- Agent: Codex
- Status: DONE
- Completion: Ran the grouped React regression suite for shared analytics chrome and the latest migrated page families, then verified analytics guardrails, production build, and queue/planning governance checks.
- Changed files:
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
  - `.ai/runs/2026-08-13-P-UI-19-evidence.md`
- Contract/runtime behavior changed:
  - none
- Checks run:
  - `cd Klijent/clientapp && npm run test -- --run src/components/analytics/__tests__/AnalyticsControlBar.spec.tsx src/components/analytics/__tests__/AnalyticsDataTable.spec.tsx src/components/analytics/__tests__/AnalyticsTrustHeader.spec.tsx src/layout/components/__tests__/HeaderStatus.spec.tsx src/layout/components/__tests__/Sidebar.spec.tsx src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/pages/__tests__/SupplierFootwearAnalyticsPage.spec.tsx src/pages/__tests__/SupplierConsolidatedPage.spec.tsx` - pass
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp && npm run build` - pass
  - `node scripts/check-agent-instructions.mjs --self-test` - pass
  - `node scripts/check-agent-instructions.mjs` - pass
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
  - `node scripts/check-prompt-queues.mjs` - pass
  - `node scripts/check-planning-architecture.mjs --self-test` - pass
  - `node scripts/check-planning-architecture.mjs` - pass
- Checks not run:
  - none
- Run log: `.ai/runs/2026-08-13-P-UI-19-evidence.md`
- Delivery mode: main
- Main commit SHA: `8dc3dbdfb9b344b93df7e1919c8598e9c40a0f27`
- Main verification: `git ls-remote origin refs/heads/main -> 8dc3dbdfb9b344b93df7e1919c8598e9c40a0f27`
- Missed:
  - no reproducible UI regression was found, so no component/page fix was needed
- Follow-up:
  - `P-UI-20`
- Residual risk:
  - existing `act(...)` warnings remain in `HeaderStatus.spec.tsx` for `RedisToggleFlag` and `WorkerControlFlag`
- Next:
  - `P-UI-20`
- Prompt defect / scope repair:
  - none; the grouped regression prompt executed as written and produced evidence-only completion

---

## P-UI-20 - Grouped analytics trust-state proof

Status: DONE
Ready after: P-UI-19 DONE
Priority: P2
Type: frontend/tests
Feature family: analytics-ui-trust-state-proof
Parallel-safe: yes
Owner: unassigned
Local lock: removed after DONE
Commit suggestion: `test(ui): lock error empty and trust states on stats pages`

### Problem

Daily Sales, Color, Shoe Type, Supplier sales and Actions already use `AnalyticsErrorState`, `AnalyticsEmptyState` and `AnalyticsTrustHeader`, but the page specs mostly cover happy-path chrome. An error that still shows KPI zeros, or an empty period that looks like a crash, is a trust failure, not a visual polish issue. P-UI may prove presentation of backend trust states; it must not invent recommendation or confidence truth.

### Evidence

- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/ColorSalesStatsPage.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/ColorSalesStatsPage.premium.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/AnalyticsActionsPage.spec.tsx`
- `docs/ai/ANALYTICS_TEST_STRATEGY.md`

### Scope

- the spec files listed in Evidence
- the five pages only if a spec reproduces a trust-state display bug
- shared `AnalyticsErrorState` / `AnalyticsEmptyState` / `AnalyticsTrustHeader` only if a reproduced bug is in the shared component

### Do Not Touch

- analytics formulas, score semantics or API payloads
- backend routes
- local Visoko/Srednje/Nisko scoring bands
- P-UI-19 chrome regression files unless a shared component bug is reproduced
- converting lazy routes to eager imports

### Read first

- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/ai/FRONTEND_UX_STANDARDS.md`
- `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md` only if a route smoke assertion is required
- the existing specs listed in Evidence

### Do

1. For Daily Sales, Color, Shoe Type and Supplier sales: add or keep a proof that API error renders `AnalyticsErrorState` / `role=alert` and does not render the main KPI block as trusted zeros.
2. For the same four pages: add or keep a proof that successful empty (`success=true`, `emptyReason` / no rows) renders `AnalyticsEmptyState`, not `AnalyticsErrorState`.
3. Prove `AnalyticsTrustHeader` is actually mounted on those pages. If a premium spec currently mocks the header to `null`, add a sibling assertion with the real header or a dedicated trust-state spec; do not leave TrustHeader coverage as a mock-only pass.
4. For Analytics Actions: prove list/summary error is a user-facing error state without fake measured impact, and empty measured summary is empty rather than error.
5. Do not snapshot entire pages. Name the failure mode in the test title (`error hides KPI`, `empty is not error`).

### Tests

```powershell
cd Klijent/clientapp
npm run test -- --run src/pages/__tests__/DailySalesStatsPage.spec.tsx src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx src/pages/__tests__/ColorSalesStatsPage.spec.tsx src/pages/__tests__/ColorSalesStatsPage.premium.spec.tsx src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/AnalyticsActionsPage.spec.tsx
npm run check:analytics-guardrails
```

### Acceptance

- Each named page family has error-without-KPI-zeros and empty-is-not-error coverage, or an explicit proof the existing spec already locks it.
- TrustHeader is proven as a real mount, not only a mocked import.
- No new frontend scoring threshold is introduced.
- Completion note references `.ai/runs/<date>-P-UI-20-evidence.md`.

### Dependencies

- `P-UI-19` DONE or owner explicitly serializes this first
- Path-safe vs `RQ104`; P-UI-20 owns stats-page trust chrome, RQ104 owns decision-page backend-field display
- Do not displace a higher-priority exclusive READY task; `QDB06` is WAITING on owner migration approval

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: grouped error-without-KPI-zeros, empty-is-not-error, and real TrustHeader proofs landed for Daily/Color/ShoeType/Supplier/Actions; Actions list error no longer looks like empty
- Changed files:
  - Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx
  - Klijent/clientapp/src/pages/__tests__/ColorSalesStatsPage.spec.tsx
  - Klijent/clientapp/src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx
  - Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx
  - Klijent/clientapp/src/pages/__tests__/AnalyticsActionsPage.spec.tsx
  - Klijent/clientapp/src/pages/__tests__/analyticsTrustStateProof.spec.tsx
  - docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md
  - MASTER_ROADMAP.md
  - docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md
  - docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md
  - .ai/runs/2026-08-13-P-UI-20-evidence.md
- Contract/runtime behavior changed:
  - Analytics Actions list failure is `role=alert` and does not render the "Nema akcija" empty copy
- Checks run:
  - `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/DailySalesStatsPage.spec.tsx src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx src/pages/__tests__/ColorSalesStatsPage.spec.tsx src/pages/__tests__/ColorSalesStatsPage.premium.spec.tsx src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/AnalyticsActionsPage.spec.tsx src/pages/__tests__/analyticsTrustStateProof.spec.tsx` - pass (46)
  - `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
- Checks not run:
  - npm run build - typecheck already ran via guardrails
  - full Vitest suite - pre-existing failures outside this prompt
- Run log: .ai/runs/2026-08-13-P-UI-20-evidence.md
- Delivery mode: direct-main
- Main commit SHA: acc8943e5b91f2b4a97c7f947b81648406bd0f53
- Main verification: git rev-parse origin/main -> 405d27b46f054dad94ba150ff33fe21cfc8e5ea5; work SHA acc8943e5b91f2b4a97c7f947b81648406bd0f53 is an ancestor
- Missed: empty success on Color/Shoe/Supplier can still show KPI totals beside EmptyState
- Follow-up: `P-UI-21`
- Residual risk: Actions list error uses a local alert banner instead of shared AnalyticsErrorState
- Next: `P-UI-21`
- Prompt defect / scope repair: dedicated `analyticsTrustStateProof.spec.tsx` added because premium/Actions specs mock TrustHeader

---

## P-UI-21 - Empty success without KPI totals and shared Actions error state

Status: DONE
Ready after: P-UI-20 DONE
Priority: P2
Type: frontend/tests
Feature family: analytics-ui-empty-kpi-honesty
Parallel-safe: yes, when RQ100 is not touching the same TSX files
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-21-codex.lock.md` (removed after DONE)
Commit suggestion: `fix(ui): hide empty-success KPIs and share Actions error state`

### Problem

P-UI-20 locked error-without-KPI-zeros, but Color, Shoe Type and Supplier empty success can still render KPI totals beside EmptyState. Analytics Actions list failure still uses a local `role=alert` banner instead of shared `AnalyticsErrorState`.

### Evidence

- `.ai/runs/2026-08-13-P-UI-20-evidence.md`
- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
- `Klijent/clientapp/src/pages/__tests__/analyticsTrustStateProof.spec.tsx`

### Scope

- the pages listed in Evidence;
- their focused specs;
- reuse `AnalyticsErrorState` / `AnalyticsEmptyState` only; no new recommendation logic.

### Read first

- P-UI-20 completion note
- `docs/ai/FRONTEND_UX_STANDARDS.md`
- `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md`

### Do

1. Hide the main KPI block on successful empty Color/Shoe/Supplier payloads.
2. Route Analytics Actions list failure through shared `AnalyticsErrorState` if the page host allows it without breaking routing tests.
3. Keep empty as `role=status` and error as `role=alert`.
4. Do not invent backend emptyReason or confidence.

### Tests

```powershell
cd Klijent/clientapp
npm run test -- --run src/pages/__tests__/analyticsTrustStateProof.spec.tsx src/pages/__tests__/ColorSalesStatsPage.spec.tsx src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/AnalyticsActionsPage.spec.tsx
npm run check:analytics-guardrails
```

### Acceptance

- empty success no longer shows trusted KPI totals beside EmptyState on the named pages;
- Actions list error does not fall through to "Nema akcija";
- no frontend-invented recommendation or confidence.

### Dependencies

- P-UI-20 DONE.

### Completion

- Run log: .ai/runs/2026-08-14-P-UI-21-evidence.md
- Checks run:
  - `cd Klijent/clientapp; npm run test -- --run src/pages/__tests__/analyticsTrustStateProof.spec.tsx src/pages/__tests__/ColorSalesStatsPage.spec.tsx src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx src/pages/__tests__/AnalyticsActionsPage.spec.tsx` - pass
  - `cd Klijent/clientapp; npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp; npm run build` - pass

---

## P-UI-22 - Remaining decision-page empty and error chrome

Status: DONE
Ready after: P-UI-21 DONE
Priority: P2
Type: frontend/tests
Feature family: analytics-ui-remaining-trust-chrome
Parallel-safe: yes, when RQ104 is not rewriting the same pages
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-22-codex.lock.md` (removed after DONE)
Commit suggestion: `test(ui): lock remaining decision page empty error chrome`

### Problem

After P-UI-21, Executive Decision Board, Product Decision Center, Inventory and Pre-nivelacija may still mix empty success with KPI-like numbers or skip shared ErrorState/EmptyState.

### Evidence

- `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`

### Scope

- the pages listed in Evidence and their nearest specs;
- presentation of backend trust states only.

### Read first

- P-UI-21
- `docs/ai/FRONTEND_UX_STANDARDS.md`

### Do

1. Prove error hides KPI zeros and empty uses EmptyState on the remaining high-value decision pages.
2. Reuse shared trust components; do not add page-local formatters.
3. Stop if a missing emptyReason requires a backend contract change; hand that to RQ.

### Tests

- focused non-watch Vitest for the touched pages;
- `npm run check:analytics-guardrails` if analytics pages change.

### Acceptance

- remaining named decision pages have error/empty proofs;
- no backend scoring is invented in the client.

### Dependencies

- P-UI-21 DONE.

### Completion note

- Date: 2026-08-14
- Status: DONE
- Completion: 100%
- Changed files: Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx; Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx; Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx; Klijent/clientapp/src/pages/__tests__/ExecutiveDecisionBoardPage.emptyState.spec.tsx; Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx; Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx; docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md; docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md; MASTER_ROADMAP.md
- Checks run: `cd Klijent/clientapp; npm run test -- --run src/pages/__tests__/ExecutiveDecisionBoardPage.emptyState.spec.tsx src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx` pass; `cd Klijent/clientapp; npm run check:analytics-guardrails` pass; `node scripts/check-planning-architecture.mjs` pass; `node scripts/check-prompt-queues.mjs` pass; `git diff --check` pass
- Checks not run: `cd Klijent/clientapp; npm run build` not run because focused tests plus guardrails covered the touched pages; dotnet build/test - frontend-only change
- Run log: .ai/runs/2026-08-14-P-UI-22-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 2ce7047ca16cd4629fa059df8b93458b6c739eb1
- Main verification: git rev-parse origin/main -> 2ce7047ca16cd4629fa059df8b93458b6c739eb1
- Missed: Inventory did not need code changes because it already had shared empty/error chrome and existing tests
- Follow-up: DEX18 Executive Board explainability reuse contract
- Residual risk: Remaining analytics pages outside this prompt still rely on their existing trust-state coverage
- Prompt defect / scope repair: locked the remaining decision-page empty/error chrome with shared trust components and no page-local formatters

### Completion

- Run log: .ai/runs/2026-08-14-P-UI-22-evidence.md
- Checks run:
  - `cd Klijent/clientapp; npm run test -- --run src/pages/__tests__/ExecutiveDecisionBoardPage.emptyState.spec.tsx src/pages/__tests__/ProductDecisionCenterPage.actionStatusFallback.spec.tsx src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx` - pass
  - `cd Klijent/clientapp; npm run check:analytics-guardrails` - pass
  - `cd Klijent/clientapp; npm run build` - pass
- Main commit SHA: 0a703f78f159acf8904f77876294f91b2cf55338
- Main verification: git rev-parse HEAD -> 0a703f78f159acf8904f77876294f91b2cf55338
- Next: none

---

## P-UI-23 - Reduce frontend lint errors in bounded trust-sensitive slices

Status: WAITING
Priority: P2
Type: frontend/tests/hygiene
Feature family: frontend-lint-baseline
Parallel-safe: yes, only when the selected files do not overlap another active prompt
Owner: Codex
Commit suggestion: `chore(ui): reduce lint errors in bounded slice`

### Problem

The frontend lint command still reports a legacy backlog even though typecheck, build and focused analytics guardrails pass. A broad formatting pass would create noise and could obscure product changes, so the backlog must be reduced in small evidence-backed slices.

### Evidence

- The current lint run reports approximately 37 errors and 167 warnings.
- The dependency/build cleanup is already complete and must not be mixed with an unrelated repository-wide lint rewrite.
- Analytics correctness remains backend-owned; lint cleanup must not recreate scoring or fallback logic in the client.

### Scope

- one selected set of frontend files from the current lint report;
- the nearest focused test files when a lint repair changes executable behavior;
- no generated files, unrelated formatting, analytics formulas or global ESLint policy changes unless the selected error proves the policy is wrong.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/PROMPT_TOKEN_ECONOMY_AND_LINT.md`
- the current lint configuration and package scripts
- the exact lint output for the selected slice

### Do

1. Freeze a baseline with the exact command, file list and error/warning counts.
2. Select one coherent file family, preferably a trust-state or shared analytics component.
3. Fix errors with behavior-preserving changes; remove warnings only when the ownership and intent are clear.
4. Do not add blanket disables, weaken rules, or reformat unrelated files.
5. Record residual warnings and create a later slice rather than expanding scope.

### Tests

- lint on the selected files;
- nearest focused Vitest suite;
- `npm run typecheck`;
- `npm run build`;
- `npm run check:analytics-guardrails` when analytics surfaces are touched;
- `git diff --check`.

### Acceptance

- The selected files have zero new lint errors and no unexplained warning increase.
- Existing UI behavior and analytics trust semantics are unchanged unless a test proves the prior behavior was invalid.
- The final note reports before/after counts and the next bounded slice.
- The queue remains `Current READY: none` until explicitly promoted.

### Dependencies

- P-UI-22 is DONE.
- This is a later hygiene follow-up and must not displace BCI/STAB/RQ work.


---

# Responsive UI audit follow-up (2026-10-01)

Source: `docs/ai/RESPONSIVE_UI_AUDIT_PROMPTS_2026-10-01.md`  
Verified base: `5257014a3bb6925cb13d6bffdb47affb41437c3d`

These prompts are presentation/browser-proof work only. They must not change analytics formulas, recommendation/trust semantics, filter meaning/defaults, URL contracts, API shapes or authorization. `PERF18` separately owns the Recharts initial-preload regression.

## P-UI-24 - Establish a measured responsive browser baseline with the existing toolchain

Status: DONE
Claimed: 2026-10-01 by ChatGPT on `cursor/p-ui-24-responsive-baseline-51d0`; completed on the same branch; local lock removed after delivery.
Priority: P1
Type: frontend/tests/evidence
Feature family: responsive-ui-browser-baseline
Parallel-safe: yes, while no active prompt owns the same browser-test/docs paths
Owner: unassigned
Commit suggestion: `test(ui): add responsive browser baseline`

### Problem

The responsive audit contains several high-confidence source findings but also runtime hypotheses about header height, modal overflow, scroll traps and touch ergonomics. Registering layout fixes before a rendered baseline risks optimizing imagined behavior and makes regression review subjective.

### Evidence

- `docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md` already defines a manual 375/768/1280 visual review contract.
- `Klijent/clientapp/package.json` already includes `puppeteer` and `puppeteer-core`.
- `Klijent/clientapp/scripts/perf08_frontend_render.mjs` demonstrates headless Puppeteer use.
- No dedicated responsive screenshot/geometry suite exists.
- The source audit marks several findings as browser/device pending.

### Scope

- `Klijent/clientapp/scripts/` or a narrow existing frontend test-support path;
- `docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md`;
- `docs/qa/` evidence/template files if needed;
- package scripts only when needed to expose the new check;
- no production component/CSS redesign in this prompt.

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md`
- `docs/ai/RESPONSIVE_UI_AUDIT_PROMPTS_2026-10-01.md`
- `Klijent/clientapp/scripts/perf08_frontend_render.mjs`
- `Klijent/clientapp/package.json`

### Do

1. Reuse Puppeteer unless current evidence proves it cannot meet the required Chromium geometry/screenshot checks; do not add Playwright by default.
2. Add a deterministic responsive audit runner for 320, 375, 768, 1024 and 1280 widths.
3. Start with a bounded representative matrix: app shell, `/prodaja`, `/analytics`, `/analytics/supplier`, `/analytics/products`, `/analytics/actions`, plus one nivelacija page. Expand only when the fixture/backend mode is deterministic.
4. Record per route: root/document overflow, header bounding box, visible form-control font sizes, relevant modal/table/filter bounding boxes, console/page errors and screenshot path.
5. Support light/dark capture for shared shell/primitives. Keep screenshots local/CI artifacts; never commit real customer metrics.
6. Clearly distinguish fixture/mock mode, connected-local mode and real-device evidence. Do not present Chromium emulation as iOS Safari proof.
7. Emit a machine-readable JSON baseline plus a concise Markdown evidence summary.
8. Seed one intentional overflow fixture/test condition and prove the geometry assertion fails.

### Tests

- `npm run typecheck`
- focused Vitest tests for any helper logic
- the new Puppeteer responsive command at the bounded route matrix
- `npm run check:analytics-guardrails` if analytics test support is touched
- `git diff --check`
- queue/planning validators if queue/docs are updated

### Acceptance

- One command produces reproducible viewport evidence for 320/375/768/1024/1280 without requiring a new browser framework.
- The output distinguishes observed failures from source hypotheses.
- An intentional overflow regression fails the check.
- No production UI behavior or analytics semantics change.
- Real iOS/iPad evidence remains explicitly pending unless it was actually captured.

### Dependencies

- `P-UI-05` is DONE and supplies the existing visual-review contract.
- Path collision check against current RQ/frontend work is required before claim.
- This lower-priority P-UI task may run only when it does not displace active BCI/STAB/RQ correctness work.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: 100%
- Changed files: `Klijent/clientapp/scripts/responsive_baseline.mjs`; `Klijent/clientapp/package.json`; `docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md`; `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`; `.ai/runs/2026-10-01-P-UI-24-evidence.md`; `MASTER_ROADMAP.md`
- Contract/runtime behavior changed: added a deterministic Puppeteer fixture/connected-local responsive baseline command; no production UI, analytics semantics or API contract changes
- Checks run: `npm run responsive:baseline -- --self-test` pass; bounded fixture matrix pass for 70 route/theme/viewport combinations with 0 page errors and 4 recorded root-overflow observations; `node --check scripts/responsive_baseline.mjs` pass; `npm run typecheck` pass; `npm run check:analytics-guardrails` pass; `npm run build` pass; queue/planning validators and `git diff --check` pass
- Checks not run: real iOS/iPad Safari and coarse-pointer device capture not run; Chromium evidence cannot prove device behavior
- Run log: `.ai/runs/2026-10-01-P-UI-24-evidence.md`
- Delivery mode: direct-main
- Main commit SHA: `007e26c64ff5dbea16cd434ab1e02fc50e21642b`
- Main verification: `origin/main` contains `007e26c64ff5dbea16cd434ab1e02fc50e21642b`
- Evidence state: synchronized
- Missed: 4 observed root-overflow cases remain as baseline findings on `/analytics/products` at 320/375px in both themes; they are intentionally not fixed by this measurement prompt
- Follow-up: promote/claim `P-UI-25` only after a fresh collision check against active frontend owners
- Residual risk: Chromium/Puppeteer evidence does not prove iOS/iPad Safari keyboard, zoom or coarse-pointer behavior
- Next: promote/claim `P-UI-25` only after a fresh collision check against active frontend owners

---

## P-UI-25 - Introduce responsive type/control/input/focus foundations without desktop churn

Status: DONE
Ready after: P-UI-24 baseline
Priority: P1
Type: frontend/css/tests
Feature family: responsive-ui-foundation
Parallel-safe: no
Owner: ChatGPT
Claimed: 2026-10-01 by ChatGPT on `cursor/p-ui-25-responsive-foundation-51d0`; completed on the same branch; local lock removed after delivery.
Commit suggestion: `feat(ui): add responsive control and type foundations`

### Problem

Current tokens and shared forms are desktop-dense: body/small tokens are below the desired phone scale, `.form-control` is 40px, compact controls are 34px and `.btn` is 38px. Many page fields use 13–14px text. Broad page-by-page fixes would duplicate policy and cause inconsistent coarse-pointer behavior.

### Evidence

- `src/styles/themes.css` defines `--font-size-base: .9rem`, `--font-size-sm: .78rem`, `--font-size-xs: .72rem`, `--size-control: 2.5rem`.
- `src/styles/forms.css` defines 40px controls, 34px compact controls and 38px buttons.
- `AnalyticsControlBar.css` fields are 13px/42px.
- Data-entry forms use `text-sm`; focus suppression exists in shared CSS/Tailwind call sites.
- P-UI-24 supplies actual computed-size/overflow evidence before the global change.

### Scope

- `Klijent/clientapp/src/styles/themes.css`
- `Klijent/clientapp/src/styles/forms.css`
- `Klijent/clientapp/src/tailwind.css`
- the smallest shared helper/test files needed to prove the foundation
- no page-family migration in this prompt

### Read first

- `P-UI-24` evidence
- `docs/ai/RESPONSIVE_UI_AUDIT_PROMPTS_2026-10-01.md`
- the three scoped style files
- Tailwind v4 configuration/import path
- existing focus-visible patterns in shared analytics components

### Do

1. Define one documented responsive token source for body/input/label/meta/table text, control height and coarse-pointer target.
2. Use 16px as the phone form-control text contract and 44px as the Trendplus coarse-pointer primary-control target; do not describe either as a universal browser/WCAG law.
3. Keep desktop/fine-pointer density close to current behavior; avoid a repository-wide typography jump.
4. Add a global `:focus-visible` baseline, then remove/override only bare focus suppression that defeats visible focus in scoped shared styles.
5. Make cascade/layer ownership explicit so page CSS cannot silently shrink phone inputs below the contract.
6. Do not mass-convert every hard-coded font size or media query in this prompt.

### Tests

- P-UI-24 geometry/computed-style runner on representative routes
- existing shared-form/component Vitest suites
- `npm run typecheck`
- `npm run build`
- `npm run check:analytics-guardrails`
- `git diff --check`

### Acceptance

- At 375px, representative visible text inputs/selects/textarea compute to at least 16px and shared primary controls hit the intended 44px coarse-pointer target.
- At 1024/1280 with a fine pointer, shared desktop density remains visually close to baseline with reviewed deltas.
- Every keyboard-focusable shared primitive in scope has a visible focus indicator.
- No `maximum-scale=1` or `user-scalable=no` accessibility regression is introduced.
- No business semantics change.

### Dependencies

- `P-UI-24` baseline is DONE.
- Path collision check is required before editing global styles.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: 100%
- Changed files: `Klijent/clientapp/src/styles/themes.css`; `Klijent/clientapp/src/styles/forms.css`; `Klijent/clientapp/src/tailwind.css`; `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-01-P-UI-25-evidence.md`
- Contract/runtime behavior changed: shared phone text controls now use the 16px product contract, native phone buttons use the 44px coarse-pointer target, and keyboard focus has a visible theme-token ring; no analytics/API/business semantics changed
- Checks run: shared interaction tests 9/9; `npm run typecheck`; `npm run check:analytics-guardrails`; `npm run build`; P-UI-24 fixture matrix 70/70 with 0 page errors and 4 baseline overflow observations; phone computed-style proof; `git diff --check`
- Checks not run: real iOS/iPad Safari and physical coarse-pointer device proof
- Run log: `.ai/runs/2026-10-01-P-UI-25-evidence.md`
- Delivery mode: direct-main
- Main commit SHA: `063d40877e3cc3ddfd650d151d14412fc812349c`
- Main verification: `origin/main` contains `063d40877e3cc3ddfd650d151d14412fc812349c`; implementation and evidence commits are ancestors
- Evidence state: synchronized
- Missed: page-family migrations, mobile drawer behavior and shared primitive migrations remain later P-UI prompts; the existing four Products-page overflow observations remain unfixed baseline findings
- Follow-up: promote P-UI-26 or P-UI-27 only after a fresh collision check
- Residual risk: global phone button sizing may increase mobile header height; P-UI-26 owns measured header/drawer remediation
- Next: re-enter canonical idle recovery for P-UI-26/P-UI-27

---

## P-UI-26 - Compact the mobile header and make the mobile drawer an accessible dialog

Status: DONE
Ready after: P-UI-24 and P-UI-25
Priority: P1
Type: frontend/layout/a11y/tests
Feature family: responsive-ui-shell
Parallel-safe: no
Owner: unassigned
Commit suggestion: `feat(layout): harden responsive app shell`

### Problem

The sticky header currently wraps many actions plus a second system-control strip, while the mobile drawer is a fixed overlay without dialog semantics, Esc handling, focus containment or scroll lock. Source proves the structural risk; P-UI-24 must supply rendered height/overflow evidence.

### Evidence

- `HeaderStatus.tsx` uses sticky positioning, `flex-wrap`, many action controls and an `xl:hidden` system strip.
- Header scope select/buttons are small by the responsive target.
- `Sidebar.tsx` mobile overlay has no `role=dialog`/`aria-modal`, focus trap, Esc close or body scroll lock; width is `w-80`.
- The existing `Modal.tsx` already demonstrates Esc/focus return/body-scroll-lock behavior that may be reused conceptually.

### Scope

- `src/layout/components/HeaderStatus.tsx`
- `src/layout/components/Sidebar.tsx`
- `src/layout/AppLayout.tsx`
- nearest layout tests/helpers/styles
- no backend status/polling or nav-route semantic changes

### Read first

- P-UI-24 baseline
- P-UI-25 token contract
- existing `HeaderStatus`/`Sidebar` tests
- `Modal.tsx` accessibility behavior
- `navConfig.ts`

### Do

1. Use the measured baseline to define compact mobile/tablet header modes; do not hard-code a 56px/64px budget unless the evidence confirms it is practical.
2. Preserve page title, navigation entry, backend status, notification access and all existing actions; move secondary actions into an accessible overflow surface instead of deleting them.
3. Make command/inbox/context panels viewport-bounded and keyboard/touch usable on small screens.
4. Make the mobile nav a proper modal dialog: semantics, Esc, focus containment, focus return, backdrop close and body scroll lock.
5. Use dynamic viewport units with fallback and safe-area padding where the drawer/sheet reaches viewport edges.
6. Keep persistent desktop sidebar behavior and existing route grouping unchanged.

### Tests

- focused `HeaderStatus` and `Sidebar` Vitest/RTL suites
- P-UI-24 320/375/768/1024/1280 shell captures
- keyboard order/focus return assertions
- root-overflow assertion
- `npm run typecheck`, `npm run build`, `git diff --check`

### Acceptance

- No root horizontal overflow in the shell matrix.
- Mobile/tablet header height and wrapping match the reviewed P-UI-24 target rather than an unmeasured arbitrary threshold.
- Every existing header action remains reachable.
- Drawer passes dialog keyboard behavior and background scrolling is locked while open.
- Desktop navigation/header functionality is unchanged.

### Dependencies

- P-UI-24 and P-UI-25 DONE.
- Do not overlap another active owner in header/sidebar paths.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: 100%
- Changed files: `Klijent/clientapp/src/hooks/useDialogA11y.ts`; `Klijent/clientapp/src/layout/AppLayout.tsx`; `Klijent/clientapp/src/layout/components/HeaderStatus.tsx`; `Klijent/clientapp/src/layout/components/Sidebar.tsx`; layout tests; `Klijent/clientapp/scripts/responsive_baseline.mjs`; queue/roadmap/evidence docs
- Contract/runtime behavior changed: mobile navigation is an accessible dialog with focus trap/scroll lock; compact mobile header keeps all actions via **Više** overflow; narrow command panels are viewport-bounded sheets; desktop shell unchanged; no analytics/API semantics changed
- Checks run: HeaderStatus/Sidebar tests 9/9; `npm run typecheck`; `npm run build`; app-shell root-overflow geometry 5/5 viewports; `git diff --check`
- Checks not run: full 70-shot PNG responsive matrix (screenshot protocol timeouts); real iOS/iPad Safari
- Run log: `.ai/runs/2026-10-01-P-UI-26-evidence.md`
- Delivery mode: direct-main
- Main commit SHA: `cb57deb02a6898d17a145f2fb847eadc1ac9fd8a`
- Main verification: `origin/main` contains `cb57deb02a6898d17a145f2fb847eadc1ac9fd8a`; implementation commit `dd40e1f8` is an ancestor
- Evidence state: synchronized
- Missed: P-UI-27 primitives and later page-family migrations remain queued
- Follow-up: promote P-UI-27 after fresh collision check
- Residual risk: full-page Puppeteer captures remain environment-sensitive
- Next: idle recovery for P-UI-27

---

## P-UI-27 - Make Modal, InfoTip, tabs and shared compact controls touch-safe

Status: DONE
Ready after: P-UI-25
Priority: P1
Type: frontend/component/a11y/tests
Feature family: responsive-ui-primitives
Parallel-safe: no
Owner: unassigned
Commit suggestion: `fix(ui): harden responsive shared primitives`

### Problem

Several shared primitives have viewport/touch problems: `Modal` has fixed minimum widths, `InfoTip` is an ~18px `span role=button` that closes on every scroll, and tabs/chips are implemented ad hoc with small targets. Fixing each page separately would multiply behavior.

### Evidence

- `Modal.tsx` has min widths 320/400/600; existing Esc/focus-trap/body-scroll-lock behavior is already correct and must be preserved.
- `.modal-content` has no viewport width cap; toast has `min-width:300px`.
- `InfoTip` is a focusable span, ~18px, and attaches a capture scroll listener that calls `hide`.
- Multiple pages use page-specific compact tabs/chips.

### Scope

- `src/components/Modal.tsx`
- modal/toast CSS in `src/tailwind.css`
- `src/components/ui/InfoTip.*`
- one shared tabs/segmented primitive only if two or more current call sites can adopt it without semantic changes
- focused primitive tests

### Read first

- P-UI-25 contract
- existing Modal and InfoTip code/tests
- current tab call sites named by the responsive audit
- ARIA patterns already used in the repo

### Do

1. Cap modal width to viewport; use a phone sheet only when content benefits from it, not as a mandatory visual rewrite.
2. Preserve existing Esc, focus trap, scroll lock and focus return.
3. Replace the InfoTip pseudo-button with a real button and enlarge the hit area while keeping the visual icon compact.
4. On touch/coarse pointer, open help on tap and close on outside tap/Esc/second tap; do not dismiss solely because the page scrolls.
5. If shared tabs are introduced, preserve active state/URL behavior and implement tab semantics/keyboard behavior; otherwise harden the existing bounded call sites without adding another abstraction.
6. Make toast width safe at 320px.

### Tests

- primitive RTL/Vitest suites for modal and InfoTip
- touch/click and keyboard behavior
- P-UI-24 geometry on one modal/help route at 320/375/768
- `npm run typecheck`, `npm run build`, `git diff --check`

### Acceptance

- md/lg modal never exceeds viewport width at 320/375 and footer/close controls stay reachable.
- InfoTip uses a native button and meets the project coarse-pointer target without visually inflating the icon.
- Existing modal accessibility behavior remains green.
- No information-bearing interaction becomes hover-only on touch.

### Dependencies

- P-UI-25 DONE.
- Page-specific migrations may follow separately.

### Completion note

- Date: 2026-10-01
- Status: DONE
- Completion: 100%
- Changed files: `Modal.tsx`; `tailwind.css` modal/toast helpers; `InfoTip.tsx`/`.css`/tests; `AnalyticsAccessibility.spec.tsx`; queue/roadmap/evidence
- Contract/runtime behavior changed: modal/toast respect viewport width; InfoTip is a native button with coarse-pointer touch semantics; no analytics/API semantics changed
- Checks run: InfoTip + Modal accessibility tests 11/11; `npm run typecheck`; `npm run build`; `git diff --check`
- Checks not run: full P-UI-24 PNG matrix; real device Safari
- Run log: `.ai/runs/2026-10-01-P-UI-27-evidence.md`
- Delivery mode: direct-main
- Main commit SHA: `d41ef0d7725b0a6554f750597fba8637c129ee61`
- Main verification: `origin/main` contains `d41ef0d7725b0a6554f750597fba8637c129ee61`
- Evidence state: synchronized
- Missed: shared tabs primitive and page-level tab migrations deferred
- Follow-up: promote P-UI-28 after collision check
- Residual risk: full modal/help screenshot matrix was not rerun, and real iOS/iPad Safari behavior remains unverified; see the run log.
- Next: idle recovery for P-UI-28

---

Owner promotion/claim 2026-10-02: canonical idle recovery found no claimable BCI/STAB/RQ/QDB/MT/GAI work; remaining higher-priority RQ/STAB items require provider access or owner decisions. P-UI-24 and P-UI-25 are DONE. Fresh collision check found no P-UI-28 lock, branch or open PR; InventoryPage's RQ427 ownership is DONE, and no current RQ prompt owns its filter layout. P-UI-28 moved WAITING -> READY -> IN_PROGRESS with InventoryPage as the one pilot; AnalyticsControlBar filter values, defaults, URL state and request mapping remain frozen. Local lock: `.ai/task-locks/P-UI-28-codex.lock.md`.

## P-UI-28 - Build a responsive FilterBar pilot without changing filter semantics

Status: DONE
Claimed: 2026-10-02 by Codex after fresh path/branch/PR/lock collision check; pilot is InventoryPage.
Ready after: P-UI-24 and P-UI-25
Priority: P1
Type: frontend/component/tests
Feature family: responsive-filter-bar
Parallel-safe: no
Owner: unassigned
Commit suggestion: `feat(ui): pilot responsive filter bar`

### Problem

Analytics pages duplicate compact 13px decision-field/filter CSS. On narrow screens filters often become long vertical stacks, and fixes do not propagate. A mass migration would be high-risk because filter defaults, URL state and request semantics are correctness-sensitive.

### Evidence

- `AnalyticsControlBar.css` owns a shared 4/2/1-column control pattern but remains 13px.
- Multiple decision pages duplicate field CSS.
- Supplier filters already become non-sticky below 900px; the remaining issue is density/one-column length at <=768px, not a mobile sticky bug.

### Completion note

- Date: 2026-10-02
- Status: DONE
- Completion: delivered the InventoryPage responsive filter pilot. A mobile summary and accessible disclosure shorten the filter stack; pilot-only auto-fit sizing prevents control overflow. Filter values/defaults, URL state and request behavior remain unchanged.
- Changed files: `AnalyticsControlBar.tsx`, `AnalyticsControlBar.css`, `InventoryPage.tsx`, `AnalyticsControlBar.spec.tsx`, `responsive_baseline.mjs`, and the P-UI queue/roadmap/evidence files.
- Checks run: focused component/recovery tests 10/10; Inventory period/URL suite passed; one combined queue-status assertion timed out but its isolated rerun passed; typecheck, build, analytics guardrails, five-width light viewport baseline (0/5 root overflow; 0 page errors), queue/planning/instruction validators and `git diff --check` passed.
- Checks not run: dark-theme and full-page Inventory browser captures (prior Puppeteer timeout); full frontend test suite (focused owner coverage was selected).
- Run log: `.ai/runs/2026-10-02-P-UI-28-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `512f276a92f5cef539608f77a768c43cef286f71`
- Main verification: fresh fetch confirmed `origin/main` contains the implementation SHA.
- Prompt defect / scope repair: responsive baseline runner lacked Inventory route isolation and viewport-only capture; targeted options were added to make browser evidence reliable.
- Residual risk: dark-theme/full-page screenshot capture timed out in prior attempts; the final light-theme viewport-only matrix passed. Build retains its existing large `recharts` chunk warning.
- Missed: no product behavior or filter semantics known to be missing.
- Follow-up: canonical idle recovery and collision check for P-UI-29.
- Next: canonical idle recovery and collision check for P-UI-29.

### Scope

- shared filter/control component files
- exactly one low-conflict analytics page as the pilot
- its focused tests
- no change to filter names/defaults/URL params/request mapping

### Read first

- P-UI-24/25 evidence
- existing `AnalyticsControlBar`
- URL/filter tests for the selected pilot
- relevant RQ contract for that page if it defines store/period/supplier semantics

### Do

1. Extend or wrap the existing control-bar/filter primitives; do not create a parallel competing control system.
2. Add a compact mobile summary + accessible filter disclosure/sheet only if baseline evidence shows the inline stack is materially harmful.
3. Keep all filters, active-count truth, Apply/Reset semantics and URL state identical.
4. Use responsive auto-fit/content-aware layout on tablet/desktop; do not force exactly two columns if available width proves another layout better.
5. Migrate one pilot page and leave subsequent page migrations to P-UI-31..36.

### Tests

- existing URL/filter request tests for the pilot
- shared component tests
- P-UI-24 geometry/screenshot diff
- `npm run typecheck`, `npm run build`, analytics guardrails, `git diff --check`

### Acceptance

- The pilot has materially shorter/clearer phone filter chrome with no root overflow.
- Filter values/defaults/request payload/URL behavior are byte-for-byte or semantically equivalent to baseline.
- No new filter system duplicates `AnalyticsControlBar`.
- Tablet/desktop behavior remains usable.

### Dependencies

- P-UI-24 and P-UI-25 DONE.
- Select a pilot whose paths are not owned by a current RQ prompt.

---

Owner promotion/claim 2026-10-02: after P-UI-28 delivery, canonical idle recovery selected P-UI-29. Collision check found no matching local task lock, branch or open PR, and no current READY/IN_PROGRESS RQ prompt owns AnalyticsDataTable or ColorSalesStatsPage. ColorSalesStatsPage is the single pilot because existing tests cover URL sort, visible sorting versus export population, and detail navigation. Shared callers remain outside this migration; only the chosen pilot opts into responsive behavior.

Owner promotion/claim 2026-10-02: after P-UI-29 delivery, canonical idle recovery found no claimable higher-priority BCI/STAB/RQ/QDB/MT/GAI prompt. P-UI-30 remains dependency-blocked on P-UI-26. P-UI-31 is deferred because RQ530 is PARTIAL and RQ487 remains the owner-gated Supplier overview query-cost path. P-UI-32 dependencies P-UI-24/P-UI-29 are DONE; RQ485/RQ488 Product Decision frontend work is DONE, while RQ472/RQ487 backend/contract scopes remain untouched. No P-UI-32 lock, branch or open PR existed. P-UI-32 moved WAITING -> READY -> IN_PROGRESS as a presentation/rendering-only task; preserve all rows and decision/action/export/sort semantics. Local lock: `.ai/task-locks/P-UI-32-codex.lock.md`.

## P-UI-29 - Add a responsive AnalyticsDataTable pilot with explicit column priority

Status: DONE
Claimed: 2026-10-02 by Codex after fresh shared-component/page collision check; pilot is ColorSalesStatsPage.
Ready after: P-UI-24 and P-UI-25
Priority: P1
Type: frontend/component/tests
Feature family: responsive-analytics-table
Parallel-safe: no
Owner: unassigned
Commit suggestion: `feat(analytics-ui): pilot responsive data table`

### Problem

`AnalyticsDataTable` standardizes chrome but remains horizontal-scroll-only with 760px minimum width and 12px cells. Many decision pages still use wider page-owned tables. Migrating all tables at once would risk hiding decision context, sorting or row actions.

### Evidence

- `AnalyticsDataTable.css` has `overflow-x:auto`, `min-width:760px`, 12px cells.
- Important page tables range roughly 880–1400px.
- Existing P-UI table work explicitly preserves data/export semantics.

### Scope

- `AnalyticsDataTable.*`
- one representative table whose export/sort/detail behavior is covered by tests
- focused tests/styles
- no data fetching, sorting, export-row-set or metric changes

### Read first

- P-UI-24/25 evidence
- current `AnalyticsDataTable` tests/call sites
- selected page's sort/export/detail tests
- P-UI-03/P-UI-07 completion notes

### Do

1. Add explicit responsive column metadata/priority only if the selected page can map it without losing meaning.
2. At phone width, prefer a card/stacked representation when it remains semantically clear; otherwise retain contained horizontal scroll with strong affordance rather than hiding required columns.
3. At tablet width, evaluate sticky key column/edge affordance from measured behavior.
4. Preserve desktop column order, numeric semantics, sorting, row actions and export row set.
5. Do not migrate every page in this prompt.

### Tests

- component unit tests for responsive representation
- selected page sort/export/detail tests
- P-UI-24 root/internal overflow and screenshot evidence
- `npm run typecheck`, build, analytics guardrails, `git diff --check`

### Acceptance

- The pilot has no document-level horizontal overflow at 320/375.
- All decision-relevant fields remain reachable; no hidden field changes interpretation.
- Sort/export/row-action semantics remain identical.
- Desktop table parity is reviewed against baseline.

### Dependencies

- P-UI-24 and P-UI-25 DONE.
- Page migrations remain separate prompts.

### Completion note

- Date: 2026-10-02
- Status: DONE
- Completion: shipped the responsive `AnalyticsDataTable` Color Sales pilot. The complete table remains reachable through contained horizontal scrolling; the `Boja` key column stays visible, and keyboard users can focus the scroll region. No table columns, ordering, data, sort, export or detail behavior changed.
- Changed files: `AnalyticsDataTable.tsx`, `AnalyticsDataTable.css`, `ColorSalesStatsPage.tsx`, the focused component/page specs, and `responsive_baseline.mjs` synthetic route fixture/capture support.
- Checks run: component/page tests 25/25; five-width light browser baseline with 0 page errors and no root overflow at 320/375; typecheck, build, analytics guardrails, governance validators and `git diff --check` passed.
- Checks not run: full frontend suite, dark theme/full-page browser captures and device Safari proof; see run log for reasons.
- Run log: `.ai/runs/2026-10-02-P-UI-29-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `fee2c165233cc985c51365c4de3ce0ca0edb96b4`
- Main verification: fresh fetch confirmed `origin/main` contains the implementation SHA and equals `HEAD` at merge SHA `c13e1660719fbdc9f908e509a327994ca1e52d2d`.
- Prompt defect / scope repair: the P-UI-24 runner did not render the Color Sales table in fail-closed fixture mode, so deterministic synthetic layout data and a table capture selector were added without loading customer data.
- Missed: none known within the table scope; a pre-existing 1024px overflow from the `Sve sezone` filter select is recorded separately in the run log.
- Residual risk: the unchanged 1024px filter-select overflow remains; current-main Analytics Quality Gates were still in progress when inspected.
- Follow-up: canonical idle recovery for the next dependency-complete, path-safe UI prompt.
- Next: re-enter canonical idle recovery.

---

## P-UI-30 - Harden mobile data-entry workflows for sales, goods receipt and price changes

Status: WAITING
Ready after: P-UI-25 and P-UI-26
Priority: P1
Type: frontend/workflow/tests
Feature family: mobile-data-entry
Parallel-safe: no
Owner: unassigned
Commit suggestion: `feat(ui): improve mobile data entry ergonomics`

### Problem

Store workflows are high-frequency and currently use small labels/fields/quick chips, desktop shortcut hints and number inputs without an explicit mobile keyboard/locale strategy. A generic analytics redesign must not delay these operational paths.

### Evidence

- `CreateProdajaForm.tsx` and `UnosRobeForm.tsx` use `text-sm` fields, `text-xs` labels and ~11px suggestion chips.
- Numeric quantity/price inputs use `type=number`.
- `NivelacijaCenaPage` uses shared 38px buttons and number fields.
- Ctrl+Enter hints appear regardless of pointer/device.

### Scope

- `/prodaja` form/components
- `/unos-robe` form/components
- `/nivelacija` price-entry surface
- nearest shared form styles/tests
- no API/validation/business-rule or persisted numeric-semantic changes

### Read first

- P-UI-25/26 evidence
- form tests and validation helpers
- Serbian decimal parsing/formatting helpers if any
- relevant backend request DTO expectations

### Do

1. Apply the shared 16px/44px mobile control contract and visible focus.
2. Make suggestion/action rows touch-sized and keep search results within the usable viewport when the keyboard is open.
3. Hide keyboard-shortcut hints on coarse-pointer-only contexts without removing shortcuts for keyboard users.
4. Improve primary save/continue reachability based on baseline evidence; a sticky action bar is optional, not mandatory.
5. Do not switch `type=number` to free text or add decimal-comma parsing unless an existing parser/DTO contract is proven and regression-tested.
6. Preserve validation messages and submit behavior exactly.

### Tests

- existing sales/goods/nivelacija form suites
- P-UI browser flow: fill receipt/search/add/edit/save or stop at mocked submit boundary
- 320/375/768 geometry
- `npm run typecheck`, build, `git diff --check`

### Acceptance

- Core workflow controls are readable/touchable at phone width with no root overflow.
- No mobile-specific change alters submitted numeric values or validation rules.
- Keyboard shortcuts still work on keyboard-capable contexts.
- Real iOS zoom/keyboard behavior is recorded as device evidence if available; otherwise remains residual risk.

### Dependencies

- P-UI-25 and P-UI-26 DONE.
- No active owner collision in the data-entry paths.

---

## P-UI-31 - Migrate Supplier overview to the responsive primitives

Status: WAITING
Ready after: P-UI-28 and P-UI-29
Priority: P1
Type: frontend/page/tests
Feature family: supplier-overview-responsive
Parallel-safe: no
Owner: unassigned
Commit suggestion: `feat(ui): make supplier overview responsive`

### Problem

Supplier overview has a dense filter surface and wide tables. The original audit incorrectly claimed the mobile filter block remains sticky; current CSS already makes it static below 900px. The real phone issue is an 11-field one-column stack at <=768px plus controls that shrink to 2.2rem.

### Evidence

- `SupplierConsolidatedPage.css`: sticky on wide screens, `position:static` at <=900px, one-column at <=768px, `min-height:2.2rem`.
- Supplier-related tables remain wide/page-specific.
- Shared responsive FilterBar/DataTable pilots are supplied by P-UI-28/29.

### Scope

- Supplier overview page/component/styles/tests
- adoption of P-UI-28/29 primitives where semantics fit
- no supplier scoring, store filter meaning, analytics values or request changes

### Read first

- P-UI-28/P-UI-29 contracts
- Supplier page tests and current RQ supplier contracts
- current source around filter/store/supplier URL state

### Do

1. Replace the long phone filter stack with the proven responsive filter pattern while preserving every filter.
2. Remove the phone control shrink below the shared control target.
3. Migrate bounded supplier tables to the responsive table pattern; preserve required ranking/detail fields.
4. Verify tabs/actions/help controls meet shared primitive contracts.
5. Preserve desktop sticky-filter behavior unless browser evidence shows it causes overlap.

### Tests

- Supplier page/filter/request tests
- table sort/export/detail tests where present
- P-UI browser matrix
- typecheck/build/analytics guardrails/diff check

### Acceptance

- No false claim remains about a sticky mobile filter stack.
- Phone filter interaction is shorter/clearer and all existing filters remain reachable.
- Supplier values, ordering, store/scope semantics and exports are unchanged.
- No root overflow at 320/375.

### Dependencies

- P-UI-28 and P-UI-29 DONE.
- Must avoid active Supplier RQ path/semantic ownership.

---

## P-UI-32 - Make Product Decision Center responsive and measure 1,200-row rendering before optimizing

Status: DONE
Ready after: P-UI-24 and P-UI-29
Priority: P1
Type: frontend/page/perf-tests
Feature family: product-decision-responsive
Parallel-safe: no
Owner: unassigned
Commit suggestion: `feat(ui): harden product decision responsive table`

### Problem

Product Decision Center uses a 1400px table, requests up to 1,200 rows and renders `sortedRows.map(...)`. The source proves a large DOM is possible, but it does not prove that virtualization is required or which strategy is safest.

### Evidence

- `ProductDecisionCenterPage.tsx` calls `getProductDecisionCenter(... top:1200 ...)`.
- The table maps all `sortedRows`.
- `ProductDecisionCenterPage.css` sets `min-width:1400px`.
- No virtualization dependency exists.

### Scope

- Product Decision Center presentation/rendering only
- shared table primitive if applicable
- a bounded measurement script/test using existing Puppeteer
- no recommendation, sorting meaning, action eligibility, pagination API or analytics contract changes

### Read first

- P-UI-24/P-UI-29 evidence
- Product Decision Center tests and RQ contracts
- existing performance measurement conventions

### Do

1. Capture row-count, DOM-node, useful-render and interaction evidence at representative row counts before selecting an optimization.
2. Make the table/card layout responsive while preserving every decision-critical field/action.
3. If measurement justifies optimization, prefer the smallest strategy compatible with current API: progressive rendering, client windowing or a separately owned server-pagination follow-up. Do not add `@tanstack/react-virtual` by default.
4. Preserve sorting/action/export semantics and current 1,200-row completeness unless a backend-owned prompt changes it.
5. Record before/after measurement.

### Tests

- Product Decision focused suites
- Puppeteer render/interaction measurement
- responsive geometry
- typecheck/build/analytics guardrails/diff check

### Acceptance

- Phone layout has no root overflow and all decision-critical information remains reachable.
- Any performance optimization is justified by recorded before/after evidence.
- No row silently disappears from the current result contract.
- No new virtualization dependency is added without measured need.

### Dependencies

- P-UI-24 and P-UI-29 DONE.
- Active Product/RQ owner collision check required.

Completion 2026-10-02: Delivered to `main` in implementation commit `3c15311541dca7dad39824ca207cdb3e63222e3b`. The Product Decision table is keyboard-focusable and horizontally contained on narrow screens, its first column remains visible while scrolling, and the period notes no longer force root overflow. A measured 1,200-row response mounts 50 rows initially with an explicit continuation control; all filtered/sorted rows remain in the shared export toolbar and can be revealed in 50-row steps. Before/after at 375px: DOM 41,408 -> 2,311; sort interaction 3,199.7 ms -> 223.8 ms; responsive light/dark matrix is 0 overflow across 320/375/768/1024/1280. RQ decision semantics and action/export behavior remain unchanged. Focused Product Decision suites: 41/41; typecheck, analytics guardrails, build, browser matrix, governance validators and diff check pass. Current-main Actions: Analytics Quality Gates run `37008756835` and Planning Governance run `37008756713` were `in_progress` on implementation SHA at inspection. Run log: `.ai/runs/2026-10-02-P-UI-32-evidence.md`; Evidence state: synchronized.

---

## P-UI-33 - Migrate Central Actions to responsive filters, table and dialogs

Status: WAITING
Ready after: P-UI-27, P-UI-28 and P-UI-29
Priority: P1
Type: frontend/page/tests
Feature family: central-actions-responsive
Parallel-safe: no
Owner: unassigned
Commit suggestion: `feat(ui): make central actions responsive`

### Problem

Central Actions combines compact filters, a wide actions table and page-specific dialog sizing. It should consume the proven shared primitives rather than receive a standalone visual rewrite.

### Evidence

- Responsive source audit identifies wide table/filter/modal paths on `AnalyticsActionsPage`.
- Shared primitive owners are P-UI-27/28/29.

### Scope

- Central Actions page/styles/tests
- adoption of shared primitives
- no queue/action status, actionability, trust reason, filter or API semantic changes

### Read first

- P-UI-27/28/29
- Actions page tests
- current RQ action/trust contracts

### Do

1. Migrate filters without changing defaults/URL/request mapping.
2. Migrate the action table with all status/reason/action fields reachable on phone.
3. Use the responsive shared modal/dialog behavior.
4. Keep destructive/confirm actions explicit and keyboard/touch accessible.
5. Preserve desktop workflow.

### Tests

- Actions focused suites
- request/filter/action-state regression tests
- responsive browser matrix
- typecheck/build/analytics guardrails/diff check

### Acceptance

- Phone workflow can filter, inspect and act without root overflow.
- Action semantics, eligibility and statuses are unchanged.
- Dialog content/actions remain fully reachable at 320/375.

### Dependencies

- P-UI-27/28/29 DONE.
- No active action-queue correctness owner on the same paths.

---

## P-UI-34 - Make Analytics Dashboard and Daily Sales responsive using measured chart/control rules

Status: WAITING
Ready after: P-UI-25 and P-UI-28
Priority: P2
Type: frontend/pages/tests
Feature family: analytics-overview-responsive
Parallel-safe: no
Owner: unassigned
Commit suggestion: `feat(ui): harden analytics overview responsive layout`

### Problem

Dashboard and Daily Sales use compact controls and desktop-oriented grids. Dashboard KPI cards collapse directly to one column below 920px; several charts have fixed geometry. These are layout issues only; metric semantics are RQ-owned.

### Evidence

- `AnalyticsDashboard.css` uses 5-column KPIs, 3 at <=1260px, then 1 at <=920px.
- Dashboard/Daily controls include sub-16px text by the responsive design target.
- Daily Sales has multiple legacy breakpoint values and already contains useful mobile card patterns for some content.

### Scope

- Analytics Dashboard and Daily Sales presentation/styles
- shared chart layout helper only if reused by both pages
- no metric, period, anomaly, store, export or trust semantic changes

### Read first

- P-UI-24/25/28 evidence
- page tests and analytics contracts
- existing chart components/ResponsiveContainer usage

### Do

1. Use content width, not a hard-coded “2-up always”, to choose phone/tablet KPI columns.
2. Apply shared control/filter sizing.
3. Make chart axis/height/legend behavior responsive from measured container width; preserve full labels via tooltip/accessibility text where truncation is necessary.
4. Preserve existing anomaly card behavior and data series.
5. Normalize only breakpoint rules touched by this work; do not mass-rewrite unrelated CSS.

### Tests

- Dashboard and Daily Sales focused suites
- screenshot/geometry evidence at reference widths
- chart/data-series assertions
- typecheck/build/analytics guardrails/diff check

### Acceptance

- First phone viewport exposes period/context and useful KPI content without unreadable compression.
- Charts retain the same data/series and remain readable at 375/768.
- No metric/filter/export behavior changes.

### Dependencies

- P-UI-25 and P-UI-28 DONE.
- Current analytics correctness owner collision check required.

---

## P-UI-35 - Migrate Pre/Post and Pre-Nivelacija analytics to responsive primitives

Status: WAITING
Ready after: P-UI-27, P-UI-28 and P-UI-29
Priority: P2
Type: frontend/pages/tests
Feature family: nivelacija-responsive
Parallel-safe: no
Owner: unassigned
Commit suggestion: `feat(ui): make nivelacija analytics responsive`

### Problem

The nivelacija analytics pages combine small fields/tabs, wide tables, large fixed inner scroll regions and desktop-first grids. They also sit next to active correctness contracts, so presentation changes must be especially strict about ownership.

### Evidence

- Source audit identifies compact controls, ~620px table scroll containers and grid minimums on Pre/Post and Pre-Nivelacija pages.
- Existing RQ/NV work owns score/event/trust/data semantics.

### Scope

- responsive presentation for the two named pages
- adoption of P-UI shared primitives
- no scoring, event definition, trust, scenario, filter, export or recommendation semantics

### Read first

- P-UI-27/28/29
- current nivelacija RQ/NV queue and tests
- page-specific tests and export/detail behavior

### Do

1. Migrate small tabs/help/filter controls to shared responsive behavior.
2. Make table/detail layout phone-safe without hiding score/action/evidence context.
3. Remove nested scroll only when browser evidence proves the replacement is better; otherwise contain it with clear affordance.
4. Collapse secondary scenario/detail panels through accessible disclosure where useful.
5. Re-run correctness-facing page tests to prove presentation-only behavior.

### Tests

- focused nivelacija page suites
- responsive browser evidence
- analytics guardrails/typecheck/build/diff check

### Acceptance

- No root overflow at 320/375.
- Score/action/stock/evidence fields remain reachable and semantically identical.
- No active NV/RQ acceptance is weakened or duplicated by UI logic.

### Dependencies

- P-UI-27/28/29 DONE.
- Do not claim while an active nivelacija RQ owner edits the same paths.

---

## P-UI-36 - Migrate Supplier Decision Hub, Shoe Type and Color analytics to responsive primitives

Status: WAITING
Ready after: P-UI-28 and P-UI-29
Priority: P2
Type: frontend/pages/tests
Feature family: supplier-segment-responsive
Parallel-safe: no
Owner: unassigned
Commit suggestion: `feat(ui): harden supplier segment responsive views`

### Problem

The Hub/Shoe Type/Color surfaces contain wide tables, desktop-oriented multi-column panels and chart geometry that can crowd narrow screens. They must reuse the same responsive primitives while preserving recent analytics truth hardening.

### Evidence

- Source audit identifies ~980/1020px tables, wide panel minimums and category-axis width on these pages.
- Current RQ work recently hardened supporting-signal/status semantics.

### Scope

- responsive presentation on the named three surfaces
- optional shared chart layout helper
- no score/status/reason/store/supplier/category semantic changes

### Read first

- P-UI-28/29
- current Supplier/Shoe Type/Color RQ contracts and tests
- page-specific charts/table tests

### Do

1. Adopt responsive filter/table primitives where compatible.
2. Stack Hub panels when measured width cannot sustain both columns.
3. Make chart container/axis/tooltip behavior adapt to width without changing series/data.
4. Preserve all supporting-signal/trust/status explanations.
5. Keep desktop presentation close to baseline.

### Tests

- focused page suites
- chart/data assertions
- responsive browser matrix
- analytics guardrails/typecheck/build/diff check

### Acceptance

- Named pages are usable at 375/768 without root overflow.
- Chart/table changes do not alter values or status/reason semantics.
- Desktop parity is reviewed.

### Dependencies

- P-UI-28 and P-UI-29 DONE.
- Avoid current Supplier/segment RQ path collisions.

---

## P-UI-37 - Finish responsive Article List and bounded long-tail surfaces

Status: WAITING
Ready after: P-UI-25, P-UI-27 and P-UI-29
Priority: P2
Type: frontend/pages/tests
Feature family: responsive-long-tail
Parallel-safe: yes only for explicitly disjoint page slices
Owner: unassigned
Commit suggestion: `fix(ui): close bounded responsive long-tail gaps`

### Problem

Article List has small pagination/input controls and a table without an explicit phone priority strategy. Additional admin/observability/legacy pages have wide grids/tables, but not all warrant card redesign.

### Evidence

- `ArtikliListPage` uses small pagination buttons and a 12px page input/select.
- Long-tail admin/observability tables have large minimum widths.
- Responsive audit did not deeply inspect every long-tail table, so broad redesign would exceed evidence.

### Scope

- Article List as the required slice
- one additional disjoint long-tail page family per execution only when measured by P-UI-24/P-UI-38
- no API paging, admin operation, observability meaning or scraper behavior changes

### Read first

- P-UI-25/27/29
- Article List tests/API paging contract
- selected long-tail page tests

### Do

1. Make Article List pagination/filter controls meet responsive shared contracts.
2. Choose card/sticky/contained-scroll table behavior from actual column semantics.
3. For admin/observability, contained horizontal scroll is acceptable when card conversion would reduce operator clarity; ensure the document itself does not overflow.
4. Keep each extra long-tail slice separately evidenced; do not turn this into a repository-wide CSS pass.

### Tests

- Article List focused tests
- selected page tests
- browser geometry evidence
- typecheck/build/diff check

### Acceptance

- Article List is phone-usable without changing API paging.
- Each additional long-tail slice has an explicit before/after evidence entry.
- No unreviewed broad CSS rewrite is introduced.

### Dependencies

- P-UI-25/27/29 DONE.
- Execute disjoint slices only after collision checks.

---

## P-UI-38 - Turn proven responsive invariants into regression gates and remove bounded CSS debt

Status: WAITING
Ready after: P-UI-30 through P-UI-37 core migrations or explicit owner decision that remaining slices are deferred
Priority: P2
Type: frontend/tests/tooling/css-hygiene
Feature family: responsive-ui-regression-gates
Parallel-safe: no
Owner: unassigned
Commit suggestion: `test(ui): enforce responsive regression contracts`

### Problem

The original audit proposed Playwright, axe, Lighthouse and Stylelint simultaneously with hard-coded thresholds. That would add tooling and unstable gates before the UI contracts are measured. After the migrations, the repeated invariants should become deterministic checks using the smallest existing toolchain.

### Evidence

- P-UI-24 supplies Puppeteer geometry/screenshot evidence.
- Existing Node guard scripts are the repo pattern for deterministic static checks.
- Existing `check:bundle-budget` owns bundle size; PERF18 owns preload graph.
- No Stylelint/Playwright/axe/LHCI dependency currently exists.

### Scope

- existing Puppeteer responsive runner
- focused Node guard scripts/package commands/CI wiring when stable
- bounded CSS cleanup revealed by the completed migrations
- no new test framework unless a demonstrated gap cannot be closed with existing tooling

### Read first

- P-UI-24 and completed migration evidence
- existing package scripts/CI
- `check-bundle-budget.mjs`
- `PROMPT_QUEUE_PROTOCOL.md`

### Do

1. Promote only stable invariants to gates: root overflow, mobile form font floor, selected coarse-pointer target rules with documented exceptions, dialog viewport containment, and named shell/table invariants.
2. Test reduced motion behaviorally: non-essential autoplay/long-running movement stops; do not require zero browser animations globally.
3. Add keyboard/focus assertions for shared dialog/drawer/help primitives.
4. Add a small CSS/static guard only for patterns proven harmful; allow documented content-driven breakpoints/container queries.
5. Do not introduce Lighthouse score gates without a measured environment baseline. Performance score ownership remains PERF.
6. Do not duplicate the existing bundle-size guard; consume PERF18 results if preload graph becomes a gate.

### Tests

- responsive runner self-test/negative fixtures
- relevant Vitest suites
- typecheck/build/analytics guardrails
- any new Node guard self-test
- governance validators
- `git diff --check`

### Acceptance

- A seeded root-overflow/input-font/dialog regression fails deterministically.
- The gate is reproducible locally and in CI without real customer data.
- No new dependency/tool is added without a documented gap and rationale.
- Breakpoint/static checks prevent known regressions without banning valid content-driven layouts.

### Dependencies

- Core responsive migrations are DONE or explicitly deferred.
- PERF18 separately owns bundle/preload performance gating.
