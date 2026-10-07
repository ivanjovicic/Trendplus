# Analytics UI Premium Prompt Queue

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Current primary prompt: P-UI-46 (IN_PROGRESS; claimed 2026-10-07 after P-UI-42 completion). Other READY lane: `P-UI-52`.
Responsive re-audit registration 2026-10-04: `P-UI-39`..`P-UI-46` registered from `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md` (live Chromium viewport evidence + current-main code, second-pass verified); dated addenda on P-UI-31, P-UI-35, P-UI-36, P-UI-38 and RQ582.
Owner decision (Ivan, 2026-10-04 21:36): the seasonal carousel is shown only on the home page `/`. Recorded in `P-UI-45` (scope, Do, Tests, Acceptance); the open question in `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md` is closed.
UX/UI audit registration 2026-10-04: `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md` registered P-UI-47-P-UI-53 against the new canonical `docs/ai/ANALYTICS_DESIGN_SYSTEM.md`, after deduplication against P-UI-39-P-UI-46. P-UI-47 (theme tokens/contrast) and P-UI-49 (state taxonomy) are additional READY lanes. P-UI-48 waits for P-UI-40, P-UI-50 for RQ573+RQ574+P-UI-49, P-UI-51 for RQ570+P-UI-39, P-UI-52 for RQ553. The trust-strip desktop budget extends P-UI-43; the live Inventory overflow confirms P-UI-41. P-UI remains a supplemental lane.
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
| P-UI-23 | DONE | frontend-lint-baseline | Reduce lint errors in bounded, trust-sensitive slices without broad rewrites |
| P-UI-24 | DONE | responsive-ui-browser-baseline | Establish measured 320/375/768/1024/1280 browser evidence using existing Puppeteer |
| P-UI-25 | DONE | responsive-ui-foundation | Responsive type/control/input/focus foundation |
| P-UI-26 | DONE | responsive-ui-shell | Compact mobile header and accessible drawer |
| P-UI-27 | DONE | responsive-ui-primitives | Modal, InfoTip, tabs and touch-safe shared primitives |
| P-UI-28 | DONE | responsive-filter-bar | Responsive Inventory filter pilot with semantics frozen |
| P-UI-29 | DONE | responsive-analytics-table | Responsive AnalyticsDataTable pilot with column priority |
| P-UI-30 | DONE | mobile-data-entry | Mobile sales/goods/nivelacija data-entry workflow |
| P-UI-31 | DONE | supplier-overview-responsive | Supplier overview responsive migration |
| P-UI-32 | DONE | product-decision-responsive | Product Decision Center responsive + measured row rendering |
| P-UI-33 | DONE | central-actions-responsive | Central Actions responsive migration |
| P-UI-34 | DONE | analytics-overview-responsive | Dashboard and Daily Sales responsive migration |
| P-UI-35 | DONE | nivelacija-responsive | Pre/Post and Pre-Nivelacija responsive migration |
| P-UI-36 | DONE | supplier-segment-responsive | Supplier Hub, Shoe Type and Color responsive migration |
| P-UI-37 | DONE | responsive-long-tail | Article List and bounded long-tail responsive cleanup |
| P-UI-38 | WAITING | responsive-ui-regression-gates | Responsive regression gates and bounded CSS hygiene |
| P-UI-39 | DONE | analytics-control-bar-overflow | Shared control bar overflow-safe by default (phone viewport inflation, 1024 overflow) |
| P-UI-40 | DONE | app-shell-small-laptop | Single-row header and sidebar rail at 1024–1279px |
| P-UI-41 | DONE | report-inventory-intrinsic-overflow | Pilot intake tables and Inventory panels contained on phones |
| P-UI-42 | DONE | responsive-coarse-pointer-tablet | 16px/44px floor for coarse-pointer tablets |
| P-UI-43 | DONE | trust-header-mobile-compaction | Compact trust header on phones (after RQ569) |
| P-UI-44 | DONE | operations-wide-table-responsive | Sticky key column for Daily Sales and Inventory tables (after RQ569) |
| P-UI-45 | DONE | global-chrome-mobile | Serbian non-blocking request indicator; carousel only on home page (owner decision 2026-10-04); reduced motion |
| P-UI-46 | IN_PROGRESS | operational-long-tail-responsive | Operational/šifarnik list screens usable on phones |
| P-UI-47 | DONE | analytics-theme-token-contract | One theme-token source of truth; accessible status text; light-theme card fix; action tiers |
| P-UI-48 | DONE | global-header-ops-safety | Ops toggles out of the business header; confirmation; skip link (after P-UI-40) |
| P-UI-49 | DONE | analytics-state-taxonomy | Backend reason codes mapped into shared empty/error/loading states |
| P-UI-50 | BLOCKED | product-decision-hierarchy | Blocked KPIs show "—" + reason; row disclosure ARIA; copy (dependencies done; Product Decision page has uncommitted workspace edits) |
| P-UI-51 | DONE | decision-surface-controls | Board period/scope/URL state; unambiguous dates; history |
| P-UI-52 | READY | analytics-nav-ia-copy | Navigation labels/badges/canonical links and glossary sweep (after RQ553/RQ582) |
| P-UI-53 | DONE | analytics-chart-accessibility | Screen-reader names/summaries/table alternatives for analytics charts (after P-UI-44 path release) |

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

Status: DONE
Priority: P2
Type: frontend/tests/hygiene
Feature family: frontend-lint-baseline
Parallel-safe: yes, only when the selected files do not overlap another active prompt
Owner: Codex
Commit suggestion: `chore(ui): reduce lint errors in bounded slice`

### Problem

The frontend lint command still reports a legacy backlog even though typecheck, build and focused analytics guardrails pass. A broad formatting pass would create noise and could obscure product changes, so the backlog must be reduced in small evidence-backed slices.

### Evidence

- The exact pre-change `npm run lint` baseline for this run reported 108 errors and 226 warnings; the selected component had seven Fast Refresh errors and one unused-import warning.
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
- After this completed bounded slice, `Current READY` returns to `none`; another slice requires fresh idle recovery and a collision-safe evidence-backed claim.

### Dependencies

- P-UI-22 is DONE.
- This is a later hygiene follow-up and must not displace BCI/STAB/RQ work.

Owner promotion/claim 2026-10-02: after P-UI-37 delivery, fresh cross-program routing confirmed BCI has no READY/IN_PROGRESS prompt, STAB16 remains blocked on external provider evidence, RQ Current READY is none, and QDB/MT/GAI remain behind their named gates. `npm run lint` established 108 errors and 226 warnings; a structured diagnostic isolated seven Fast Refresh export errors and one unused-import warning in the shared Pilot Data Quality Intake Report component. No active RQ owner/lock/branch/PR overlaps this shared-component-only lint slice; prior RQ pilot-intake work is delivered. P-UI-23 moved WAITING -> READY -> IN_PROGRESS for the bounded component/helper extraction and its focused tests. Local lock `.ai/task-locks/P-UI-23-codex.lock.md` was removed before delivery.

Owner completion 2026-10-02: P-UI-23 moved IN_PROGRESS -> DONE. The shared report exports/trust-state helpers were moved unchanged into `pilotDataQualityIntakeReportHelpers.ts`; the unused `KpiExplainButton` import was removed and nearest tests now import pure helpers from the `.ts` module. Baseline for selected files: 7 errors / 1 warning; after: 0 errors / 0 warnings. Full lint baseline remains 108 errors / 226 warnings; no unrelated lint slices were changed. Focused specs pass 14/14, analytics guardrails/typecheck/build pass, and `git diff --check` passes. Implementation SHA `87e9deb7b9f7416b8cdd92be28ffb9ed7daa89ff` was pushed to and freshly verified on `origin/main`. Analytics Quality Gates run `37018019549` completed red at `Run analytics tests` (135 passed, 3 failed): `AnalyticsDashboard.tableSystem.spec.tsx`, `InventoryPage.queueStatus.spec.tsx`, and `InventoryPage.signalWindow.spec.tsx`. These specs and their owning page paths are outside P-UI-23; the queue-status failure is also present on earlier pre-P-UI-23 run `37015705322`. The two changed pilot-intake specs pass both locally and on the focused local run; no P-UI-23 regression is evidenced. Run log: `.ai/runs/2026-10-02-P-UI-23-evidence.md`. Evidence state: synchronized. P-UI-38 remains WAITING until P-UI-31/35/36 core migrations are DONE or an owner explicitly defers those slices.


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

Status: DONE
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

Owner promotion/claim 2026-10-02: after P-UI-34, idle recovery reconciled stale P-UI-26/P-UI-27 summary rows from WAITING to DONE using their synchronized completion notes/run logs. It also reconciled the RQ495 operations-accuracy summary row from IN_PROGRESS to DONE using `.ai/runs/2026-09-29-RQ495-evidence.md` and delivery SHA `d41ed2e77f7716a1095de1f2d588e3cb4ce0ca31`; its scope owns Supplier/Shoe Type analytics snapshot reads, not the P-UI-30 sales/goods/price-entry forms. P-UI-30 dependencies P-UI-25/P-UI-26 are DONE; no active RQ owner, lock, branch or open PR was found for the three form paths. P-UI-30 moved WAITING -> READY -> IN_PROGRESS. Local lock: `.ai/task-locks/P-UI-30-codex.lock.md`.

### Completion note

- Date: 2026-10-02
- Status: DONE
- Completion: Sales and Goods Receipt use readable, touch-sized phone controls; autocomplete stays within the visual viewport; the Sales mocked browser flow completes without changing validation or submitted numeric values.
- Changed files: see `.ai/runs/2026-10-02-P-UI-30-evidence.md`.
- Checks run: 3 focused suites / 3 tests, typecheck, build, strict responsive fixture matrix for `/prodaja`, `/unos-robe`, `/nivelacija` at 320/375/768 in light/dark, runner self-test, queue/instruction/planning validators, diff check.
- Checks not run: full frontend suite and analytics guardrails (scope does not change analytics); real iOS/iPad keyboard/zoom proof unavailable.
- Run log: `.ai/runs/2026-10-02-P-UI-30-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `d22e17f0e3f488286c7df933739a68305d78c669`
- Main verification: fresh fetch and ancestry check confirmed `origin/main` contains the implementation SHA; no Actions run was discoverable for it.
- Missed: none known.
- Follow-up: P-UI-33 (IN_PROGRESS)
- Residual risk: real-device iOS keyboard/zoom behavior remains unverified.
- Prompt defect / scope repair: stale P-UI-26/P-UI-27 summary rows and the RQ495 operations-accuracy row were reconciled from synchronized evidence before the claim; the RQ495 owner scope is analytics snapshot reads and does not overlap these form paths.

---

---

## P-UI-31 - Migrate Supplier overview to the responsive primitives

Status: DONE
Ready after: P-UI-47 DONE (P-UI-28/P-UI-29 are already DONE; adopt the canonical theme/action tokens in the same page pass)
Priority: P1
Type: frontend/page/tests
Feature family: supplier-overview-responsive
Parallel-safe: no
Owner: Codex
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

### Completion note

- Date: 2026-10-06
- Status: DONE
- Completion: Responsive Supplier overview delivered on `main`; phone filters collapse behind a contextual disclosure, controls meet 44px, desktop sticky behavior remains, and supplier tables use the shared scroll pattern.
- Changed files: SupplierConsolidated page/CSS/spec, SupplierSalesStats page/CSS, SupplierDecisionHub CSS, SupplierFootwearAnalytics CSS, `scripts/responsive_baseline.mjs`, and the P-UI-31 run log.
- Checks run: focused Supplier suites 91/91; typecheck; responsive fixture 30/30 across three themes and ten widths, zero root overflow/page errors, all scorecard filters reachable, phone targets >=44px and desktop sticky preserved; analytics guardrails; production build; responsive runner syntax; diff check.
- Checks not run: physical-device iOS/iPadOS Safari.
- Run log: `.ai/runs/2026-10-06-P-UI-31-evidence.md`
- Evidence state: synchronized.
- Delivery mode: direct-main
- Main commit SHA: `2170fae56c1439c17bea50467d51241714637df2`
- Main verification: fresh `origin/main` matched implementation SHA `2170fae56c1439c17bea50467d51241714637df2`.
- Missed: physical-device verification.
- Follow-up: P-UI-35 was claimed IN_PROGRESS after the mandatory post-close scan.
- Residual risk: responsive fixtures use synthetic filters and do not prove live Supplier API or Safari behavior; Analytics Quality Gates run `37394820689` passed on the implementation SHA.
- Post-close routing: fresh `origin/main` `19f9d94e07aab4cc754d46d5db49d38687820a49` scan of all 16 active owner files found no newly runnable RQ/SQL dependency and claimed dependency-complete P-UI-35; details are in the run log.
- Prompt defect / scope repair: initial supplier browser baseline captured the global loading screen and did not wait for Supplier controls; the route now waits for and captures the filter panel and exercises scorecard filters using synthetic filter responses.

---

### Addendum 2026-10-04 (responsive re-audit, live evidence)

- Live 360x780 (mobile emulation) on `/analytics/supplier?fromDate=2026-07-07&toDate=2026-08-05`: no document overflow (`innerWidth` 360). Filters stack one per row at 296px x 40px (`select`, `input`, `button.secondary`), below the 44px coarse-pointer target. The trust header is 749px tall before any supplier content.
- The deferral reason recorded on 2026-10-02 ("RQ487 remains the owner-gated Supplier overview query-cost path") is stale: RQ487 is DONE. RQ530 remains PARTIAL as a consumer, not a start gate. After the same-day UX audit, the deliberate sequencing gate is P-UI-47 so this page can adopt the canonical theme/action tokens once instead of being touched twice; perform a fresh active-owner/path collision check at claim time.
- The shared control-bar geometry fix is now `P-UI-39` (default overflow safety). This prompt still owns the Supplier-specific filter density/disclosure and table migration. Source: `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md`.

### Addendum 2026-10-04 (UX/UI audit; no status change)

- Apply `docs/ai/ANALYTICS_DESIGN_SYSTEM.md` §2 when migrating: replace the page-local `--dashboard-accent: var(--success)` / `--dashboard-accent-strong: var(--warning)` aliases and neon fallbacks (`#66ff7e`, `#8bff00`, `#8ad5a8`) in `SupplierSalesStatsPage.css`/`.tsx` with the P-UI-47 `--chart-series-*` and `--status-*` tokens (audit UX-013/UX-037). Fix the truncated period preset only if P-UI-51 has not already done so.
- P-UI-47 is now an explicit start dependency for this page migration; do not intentionally ship a residual page-token migration that would require a second immediate pass.
- Live 2026-10-04 (audit UX-046): "Pregled" (final recommendation) and "Skorkarta" (explicitly auxiliary signal) look like equal tabs. Mark the final tab as primary and label auxiliary tabs as supporting signals, using existing backend role/readiness fields only.

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

Owner promotion/claim 2026-10-02: after P-UI-32, idle recovery reconciled stale RQ468 summary status `READY -> DONE` from its synchronized completion note/run log; the canonical RQ READY pointer is `none`. P-UI-33 remains blocked on P-UI-27 and P-UI-30 on P-UI-26. P-UI-31 remains unsafe while Supplier RQ query-cost ownership is unresolved. P-UI-34 dependencies P-UI-25/P-UI-28 are DONE; no active Analytics Dashboard or Daily Sales RQ owner, lock, branch or open PR collision was found. RQ517 and the Daily Sales correctness contracts are DONE. P-UI-34 moved WAITING -> READY -> IN_PROGRESS for presentation-only Dashboard/Daily Sales work; preserve all metric, period, anomaly, store, export and trust semantics. Local lock: `.ai/task-locks/P-UI-34-codex.lock.md`.

---

## P-UI-33 - Migrate Central Actions to responsive filters, table and dialogs

Status: DONE
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

Owner promotion/claim 2026-10-02: P-UI-27's synchronized completion and P-UI-28/P-UI-29 delivery evidence satisfy all named dependencies. After P-UI-30 delivery, fresh RQ review found RQ47/RQ48 and adjacent RQ477-RQ482/RQ498-RQ500 action items WAITING, with no active Central Actions correctness owner, task lock, matching local/remote branch or open PR. P-UI-33 moved WAITING -> READY -> IN_PROGRESS as an independent presentation-only task. Local lock: `.ai/task-locks/P-UI-33-codex.lock.md`.

### Completion note

- Date: 2026-10-02
- Status: DONE
- Completion: Central Actions now uses the shared horizontally scrollable table and responsive modal primitives. Phone filters and controls meet the target size; status/reason/action columns remain reachable. Filter, URL/request, eligibility and status semantics are unchanged. A bounded page grid fixes root overflow.
- Changed files: `Klijent/clientapp/scripts/responsive_baseline.mjs`, `Klijent/clientapp/src/pages/AnalyticsActionsPage.css`, `Klijent/clientapp/src/pages/AnalyticsActionsPage.spec.tsx`, `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`, `MASTER_ROADMAP.md`, `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`, `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md`, `.ai/runs/2026-10-02-P-UI-33-evidence.md`.
- Checks run: 35 focused Actions tests; analytics guardrails/encoding/typecheck; production build; strict Actions fixture matrix at 320/375/768 in light/dark; `git diff --check`; fresh current-main ancestry verification.
- Checks not run: full frontend suite and physical mobile-device verification; see run log for scope/reasons.
- Run log: `.ai/runs/2026-10-02-P-UI-33-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `987ec671894b5b77642674674d466159dd1d8bad`
- Main verification: fresh `origin/main` equals the implementation SHA; no Actions run was returned for this commit at inspection.
- Missed: none known.
- Follow-up: P-UI-37 Article List responsive migration was promoted and claimed after confirming dependencies and path ownership.
- Residual risk: physical mobile keyboard/zoom behavior was not tested; unrelated seasonal-image fixture requests still log HTTP 503; build retains its existing Recharts chunk warning.
- Prompt defect / scope repair: the initial fixture omitted required `meta` envelopes and did not stub shared worker health, which obscured the route with errors. Corrected the deterministic fixture before final proof; no production API or business semantics changed.

Owner promotion/claim 2026-10-02: after P-UI-33 was verified DONE on fresh `origin/main`, idle recovery confirmed BCI has no READY/IN_PROGRESS task, STAB16 remains externally blocked, RQ Current READY is none, and QDB/MT/GAI retain their documented gates. P-UI-37 dependencies P-UI-25/27/29 are DONE; a fresh path/branch/PR review found no Article List paging/correctness owner or overlapping change. P-UI-37 moved WAITING -> READY -> IN_PROGRESS for the required Article List slice only; optional long-tail work remains out of scope. Local lock: `.ai/task-locks/P-UI-37-codex.lock.md`.

---

## P-UI-34 - Make Analytics Dashboard and Daily Sales responsive using measured chart/control rules

Status: DONE
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

### Completion note

- Date: 2026-10-02
- Status: DONE
- Completion: Dashboard and Daily Sales controls, KPI grids, charts and stock summary now size to their available content width. Metric, period, anomaly, store, export, data-quality and trust semantics are unchanged.
- Changed files: `Klijent/clientapp/scripts/responsive_baseline.mjs`, `Klijent/clientapp/src/components/analytics/AnalyticsDashboardCharts.tsx`, `Klijent/clientapp/src/pages/AnalyticsDashboard.css`, `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`, `Klijent/clientapp/src/pages/DailySalesStatsPage.css`, `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`, `MASTER_ROADMAP.md`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`, `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`, `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md`, `.ai/runs/2026-10-02-P-UI-34-evidence.md`.
- Checks run: 8 focused suites, 77/77 tests; analytics guardrails and typecheck; production build; four responsive fixture matrices; queue, instruction and planning validators; `git diff --check`.
- Checks not run: physical iOS/iPad Safari; no device/browser access was available. No Actions run was returned for the implementation SHA.
- Run log: `.ai/runs/2026-10-02-P-UI-34-evidence.md`
- Evidence state: synchronized after the closure note and run log are delivered to `main`.
- Delivery mode: direct-main
- Main commit SHA: `c2874c9a6ce70a7a1fc38ec6477021c721852847`
- Main verification: implementation SHA is an ancestor of freshly fetched `origin/main`.
- Missed: none known.
- Follow-up: re-enter canonical idle recovery; P-UI-26 and P-UI-27 remain WAITING behind their documented owner/dependency gates.
- Residual risk: real iOS/iPad Safari behavior was not verified; Vite still reports the existing Recharts chunk above 500 kB.
- Prompt defect / scope repair: idle recovery reconciled the stale RQ468 summary `READY -> DONE` from synchronized completion evidence. Responsive fixtures now use Dashboard/Daily Sales DTO field names and the Dashboard fixture explicitly opens its existing detailed-analysis disclosure; product data semantics and user interaction remain unchanged.

Completion 2026-10-02: Implemented in `c2874c9a6ce70a7a1fc38ec6477021c721852847`; fresh `origin/main` contains the SHA. Before: Dashboard overflowed by 117px at 1280px; Daily Sales overflowed by 8px at 320px and 87px at 1024px. After: both routes recorded 0/5 root-overflow observations in light and dark themes at 320/375/768/1024/1280, with 0 browser page errors. The runner mounted 7 Dashboard and 4 Daily Sales chart containers and recorded chart heights from 280–340px and 280–358px respectively. Focused page tests passed 77/77. Run log: `.ai/runs/2026-10-02-P-UI-34-evidence.md`; Evidence state: synchronized.

---

## P-UI-35 - Migrate Pre/Post and Pre-Nivelacija analytics to responsive primitives

Status: DONE
Ready after: P-UI-39 DONE AND P-UI-47 DONE (P-UI-27/P-UI-28/P-UI-29 are already DONE); then re-check active Nivelacija owners
Priority: P2
Type: frontend/pages/tests
Feature family: nivelacija-responsive
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/P-UI-35-codex.lock.md`
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

### Addendum 2026-10-04 (responsive re-audit, live evidence)

- Live 360 (mobile emulation, dated data): `/analytics/nivelacije-pre-post` inflates the layout viewport to 433px through `label.analytics-control-bar__field` (380px, long supplier/store options). `/analytics/pre-nivelacija-prioriteti` at 1024 with the sidebar open overflows by 104px (4-column control-bar grid). The geometry part is now owned by `P-UI-39`. Do not duplicate it here.
- Still owned here: the Pre-Nivelacija table (956px, 10 columns, 60 rows in a 268px scroller, first column `position: static`, 12px cells); the trust header 1287px plus control bar 1081px before the first KPI at 4056px on 360 (the trust-header compaction itself is `P-UI-43`); the Pre/Post control bar is 963px tall on 360.
- All P-UI-27/28/29 dependencies are DONE. The real sequencing gates are P-UI-39 (shared control-bar geometry required for no-overflow acceptance) and P-UI-47 (canonical theme/status tokens required by the same-day UX addendum). RQ552/RQ556/RQ571 do not block while merely WAITING. RQ553 blocks only if it is actually active on the same page paths when this prompt becomes otherwise READY. Source: `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md`.

### Addendum 2026-10-04 (UX/UI audit; no status change)

- When migrating Pre/Post and Pre-Nivelacija, replace page-local status-as-brand aliases in `ProdajaPrePostNivelacijePage.css` and the hardcoded status colours in `PreNivelacijaPriorityPage.tsx:246` with P-UI-47 tokens (audit UX-013). Trust-header density belongs to P-UI-43, not this prompt.

### Completion note

- Date: 2026-10-06
- Status: DONE
- Completion: Pre/Post and Pre-Nivelacija now use the shared responsive table behavior for wide evidence tables, phone focus controls meet the 44px target, and status colors follow the P-UI-47 semantic tokens. Analytics scoring, event, trust and recommendation behavior did not change.
- Changed files: both page components/styles and Pre/Post spec; `scripts/responsive_baseline.mjs`; P-UI-35 run log.
- Contract/runtime behavior changed: presentation only; no API, analytics or export contract changes.
- Checks run: focused suites 114/114; 60-case three-theme viewport matrix with 0 root overflow and 0 page errors; analytics guardrails/typecheck; production build; responsive runner syntax; diff check.
- Checks not run: real iOS/iPadOS Safari.
- Run log: `.ai/runs/2026-10-06-P-UI-35-evidence.md`
- Evidence state: synchronized.
- Delivery mode: direct-main
- Main commit SHA: `cee0665c0d67fe8f1f9cbefd4cd5aedd01ab9321`
- Main verification: current `origin/main` `e7f9bc47325348d5f7ad202e9850926c91df95a5` contains the implementation SHA.
- Missed: physical-device Safari proof; populated PPN API responses are not part of the responsive fixture.
- Follow-up: P-UI-36 claimed after the mandatory full active-queue post-close cascade.
- Residual risk: the Pre/Post fixture uses its empty/error state because the runner does not stub all PPN data endpoints; populated behavior is covered by page tests. GitHub Analytics Quality Gates run `37396627516` succeeded on the implementation SHA; Vercel status was pending.
- Post-close routing: recovery base `origin/main` `e7f9bc47325348d5f7ad202e9850926c91df95a5`; the full 16-file active queue/addendum set was scanned. No RQ/SQL dependency became newly runnable; P-UI-53 now has P-UI-35 satisfied but still waits on P-UI-36. P-UI-36 was dependency-complete and collision-safe and was claimed as the current P2 successor.
- Next: P-UI-36 - Supplier Hub, Shoe Type and Color responsive migration.
- Prompt defect / scope repair: the PPN select shortens the visible long-store label, so the responsive runner now checks its unique 36-character prefix rather than requiring an exact full-label match.

## P-UI-36 - Migrate Supplier Decision Hub, Shoe Type and Color analytics to responsive primitives

Status: DONE
Ready after: P-UI-39 DONE AND P-UI-47 DONE (P-UI-28/P-UI-29 are already DONE); then perform a fresh active-owner/path collision check
Priority: P2
Type: frontend/pages/tests
Feature family: supplier-segment-responsive
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/P-UI-36-codex.lock.md`
Commit suggestion: `feat(ui): harden supplier segment responsive views`

Owner claim 2026-10-06: P-UI-35 DONE was verified on `origin/main` `e7f9bc47325348d5f7ad202e9850926c91df95a5`. Full post-close review of the 16 active queue/addendum files found no newly runnable RQ/SQL dependency or higher-priority global repo-local lane. P-UI-36 is dependency-complete (P-UI-39/47/28/29 DONE), with no active Supplier/segment RQ owner, matching lock, branch or open PR. Claimed READY -> IN_PROGRESS. P-UI-43 trust-header density remains outside this claim. Local lock: `.ai/task-locks/P-UI-36-codex.lock.md`; recovery evidence: `.ai/runs/2026-10-06-P-UI-35-evidence.md`.

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

### Addendum 2026-10-04 (responsive re-audit, live evidence)

- Live 360 (mobile emulation, dated data): Color inflates `innerWidth` to 426px and Shoe Type to 433px, both through the control-bar select width. At 1024 with the sidebar open, both overflow by 106px. Owned now by `P-UI-39`.
- Still owned here: the Shoe Type table (1730px, 10 columns in a 260px scroller at 360, first column static, 12px); the Shoe Type trust header 1436px plus control bar 815px before the first KPI at 2403px (compaction in `P-UI-43`); the Supplier Decision Hub/Color table priority columns.
- P-UI-28/29 are DONE. The real sequencing gates are P-UI-39 (shared overflow geometry) and P-UI-47 (canonical chart/status tokens now required by this page migration). RQ575 is WAITING and RQ580 is backend label-helper work; neither is a blocker by status alone. Only a fresh active owner/path collision blocks claim after those P-UI dependencies. Source: `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md`.

### Addendum 2026-10-04 (UX/UI audit; no status change)

- Replace `--dashboard-*` status aliases and neon fallbacks in `ShoeTypeSalesStatsPage.css`/`.tsx` (`:172-184`) and the Supplier Hub/Color page CSS with P-UI-47 chart/status tokens (audit UX-013/UX-037).

### Completion note

- Date: 2026-10-06
- Status: DONE
- Completion: shared empty-state surfaces follow the active theme; Supplier Hub, Shoe Type and Color status/chart colors use canonical theme tokens. The named pages retain the shared responsive table/chart primitives and no root overflow was observed in the tested route matrix.
- Changed files: `AnalyticsEmptyState.css` and spec; `ShoeTypeSalesStatsPage.css`/`.tsx`; `ColorSalesStatsPage.css`; `SupplierDecisionHubPage.css`; P-UI-36 run log and queue/roadmap evidence.
- Checks run: focused page/component suites 123/123; responsive Chromium fixture matrix 60/60 with zero overflow/errors; three-theme computed background check; analytics guardrails/typecheck; production build; `git diff --check`.
- Checks not run: populated Supplier Hub browser rendering (local backend returned HTTP 500); real iOS/iPadOS Safari.
- Run log: `.ai/runs/2026-10-06-P-UI-36-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `3d6c538d4b60508accea3c5942fdf8a1f2a303df`
- Main verification: fresh `origin/main` contains `3d6c538d4b60508accea3c5942fdf8a1f2a303df`
- Missed: populated Supplier Hub chart/table visual check remains unavailable while local backend returns HTTP 500.
- Follow-up: P-UI-43 - compact the trust header on phones.
- Residual risk: no populated live Hub browser evidence; focused Supplier Hub component tests passed.
- Post-close routing: full 16-file cascades at `3d6c538d4b60508accea3c5942fdf8a1f2a303df` and `7b6cac1573ae7a3fbb8a84ed84599b7446cbfec1` selected and reconfirmed P-UI-43 as current primary.
- Prompt defect / scope repair: none.

## P-UI-37 - Finish responsive Article List and bounded long-tail surfaces

Status: DONE
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

### Completion note

- Date: 2026-10-02
- Status: DONE
- Completion: Article List pagination/filter controls meet the phone target contract, all columns remain reachable in the shared keyboard-scrollable table, and sort controls are keyboard accessible. Server paging/filter/sort semantics remain unchanged.
- Changed files: `Klijent/clientapp/scripts/responsive_baseline.mjs`, `Klijent/clientapp/src/pages/ArtikliListPage.tsx`, `Klijent/clientapp/src/pages/ArtikliListPage.spec.tsx`, `MASTER_ROADMAP.md`, `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`, `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md`, `.ai/runs/2026-10-02-P-UI-37-evidence.md`.
- Checks run: Article List focused tests 2/2; analytics guardrails/encoding/typecheck; production build; strict 320/375/768 light/dark browser matrix; `git diff --check`; governance validators; fresh current-main ancestry verification.
- Checks not run: full frontend suite and physical mobile-device verification. GitHub Actions discovery returned HTTP 503.
- Run log: `.ai/runs/2026-10-02-P-UI-37-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `46f9587f6b2424e1238bac7b76be7a87c2ea5c6b`
- Main verification: fresh `origin/main` equals the implementation SHA.
- Missed: no required acceptance item; optional long-tail pages were not included.
- Follow-up: P-UI-23 is promoted and claimed for a bounded shared analytics component lint slice.
- Residual risk: physical-device keyboard/zoom behavior and GitHub Actions status are unavailable; unrelated seasonal-image fixture requests log HTTP 503; existing Recharts build warning remains.
- Prompt defect / scope repair: none.

---

## P-UI-38 - Turn proven responsive invariants into regression gates and remove bounded CSS debt

Status: WAITING
Ready after: all non-gate UI migrations `P-UI-31`, `P-UI-35`, `P-UI-36` and `P-UI-39`..`P-UI-53` are DONE or explicitly deferred; this prompt is the final responsive/theme/a11y regression gate
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
- `npm run check:analytics-chart-accessibility` and `node scripts/check-analytics-chart-accessibility.mjs --self-test`
- any new Node guard self-test
- governance validators
- `git diff --check`

### Acceptance

- A seeded root-overflow/input-font/dialog regression fails deterministically.
- Every active production chart call site has a named, described chart frame and Recharts keyboard navigation; discoverable equivalent tables are linked, while quarantined exceptions have a reason and adjacent text proof.
- The gate is reproducible locally and in CI without real customer data.
- No new dependency/tool is added without a documented gap and rationale.
- Breakpoint/static checks prevent known regressions without banning valid content-driven layouts.

### Dependencies

- All named responsive/UX migrations above are DONE or explicitly deferred, including P-UI-53 chart accessibility.
- PERF18 separately owns bundle/preload performance gating.
- Physical-device proof is release evidence, not a CI prerequisite: before claiming "real-device certified", record one iPhone Safari and one iPad Safari manual pass using `ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md`; if hardware is unavailable, state that limitation explicitly rather than inferring parity from Chromium emulation.

### Addendum 2026-10-04 (responsive re-audit, live evidence)

- The existing `responsive:baseline` runner (`Klijent/clientapp/scripts/responsive_baseline.mjs:8-22`, 13 route entries, fixture mode) did not catch the live phone layout-viewport inflation on Color/Shoe Type/Pre-Post/Pilot intake/Inventory or the 1024px overflow (P-UI-39/P-UI-41). When this gate is built, add these invariants:
  1. in mobile emulation, `window.innerWidth === viewport width` (document `scrollWidth` alone misses Chrome's layout-viewport growth);
  2. 1024x768 with the sidebar expanded;
  3. fixture option labels of at least 60 characters;
  4. a sticky-header height budget (≤ 88px at ≥1024, see P-UI-40);
  5. the missing routes `/analytics/shoe-type-sales-stats`, `/analytics/pre-nivelacija-prioriteti`, `/analytics/data-quality`, `/analytics/decision-board`, `/analytics/decision-pulse`, `/analytics/pilot-readiness`, `/analytics/reports/pilot-intake`, `/analytics/supplier/report`, `/logs` and the šifarnik list pages from P-UI-46.
- The carousel reduced-motion item in Do step 2 is implemented by `P-UI-45`. This gate only needs to assert it. Source: `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md`.

### Addendum 2026-10-04 (UX/UI audit; no status change)

- Extend the gate scope with a theme/a11y ratchet (audit UX-039, design system §12): count per file hex/`rgb()` literals in analytics pages, raw Tailwind palette classes (`amber|red|green|…-NNN`), `text-[9|10|11px]`, `color: var(--warning|success|error)` used as text, and `onClick` on `tr/div/span` without `role`+`tabIndex`. The 2026-10-04 audit baselines (e.g. Insight Studio 50 hex / 34 inline / 58 tiny text) are the initial allowlist; counts may only decrease.
- The theme-contrast unit test added by P-UI-47 becomes part of the gate.
- P-UI-53 supplies the chart semantics; this gate adds a deterministic check that chart regions have an accessible name plus either a textual summary or a discoverable table alternative, and that seeded missing-a11y fixtures fail.
- If the responsive migrations remain blocked, the owner may split this ratchet into its own prompt with a fresh collision check; do not duplicate it elsewhere.

### Addendum 2026-10-06 (post-theme-audit residual hardening; no status change)

Independent review of the theme delivery `cc57cace76e6c737e10dc0cb50d5ea5e296885c4` and follow-up `2d675b60fd9ac5d6943413b06b0d3b9a11b4f549` closed immediate Tailwind-v4 mapping/light-theme regressions, but also identified residual debt that belongs here rather than in a new overlapping prompt:

- Treat `var(--theme-color-*, #fallback)` / `var(--theme-color-rgba-*, rgba(...))` as **pseudo-token debt**, not as a target for a blind repository-wide rewrite. Classify each audited occurrence as semantic theme value, chart/business semantic value, justified fixed decorative value, or dead/legacy pseudo-token. Prioritize shared components, analytics, Inventory, forms and other critical surfaces; ratchet counts down per file rather than requiring zero literals globally.
- `inventory-dark` must either meet the same supported-theme contrast contract as the other selectable themes or be explicitly treated as a compatibility/legacy alias with a safe preference migration. Do not remove or silently remap a persisted theme value. In particular re-check links/accent text, `text-on-primary`, status fill/text pairs, selected tabs, controls and focus states against their **actual** backgrounds.
- Tailwind v4 semantic-colour correctness is a gate: every semantic colour utility actually used by production TSX, including opacity variants such as `bg-muted/20`, `border-muted/30`, `ring-*`, `divide-*`, `fill-*`, `stroke-*`, `from/via/to-*`, must either resolve through `@theme inline` to an active runtime token or be replaced with a documented canonical helper. The current build does not get colour mappings merely from `tailwind.config.js`; verify any future claim that it does before relying on that file.
- Add a bounded guard against new fixed `text-white`, `bg-white`, `text-black`, `bg-black`, `border-white` or `border-black` on **theme-aware** shared/critical surfaces. Keep explicit exceptions for print, photo overlays and other truly fixed backgrounds. Do not create a global ban that rejects valid fixed-colour UI.
- Clean duplicate token declarations, self/nested fallback chains and aliases that resolve only to an undefined variable plus literal fallback where the completed migrations prove them redundant. Preserve computed values; this is hygiene, not a palette redesign.
- Consume the existing `ThemeContext.tokens.spec.ts` and `themeTokenUsage.spec.ts` protections, including the `muted` opacity mapping and theme-aware fixed-white regression checks added after the audit. Promote these to stable CI ratchets instead of re-implementing parallel tests.
- Do not reopen `P-UI-43`, `P-UI-52` or `P-UI-53` scope here. P-UI-38 only consumes their finished contracts and adds deterministic regression gates after those tasks are DONE or explicitly deferred.

Additional acceptance for this residual scope:
1. No known unsafe theme-dependent literal/pseudo-token remains on the audited shared and critical surfaces; justified fixed colours are documented rather than hidden behind fake tokens.
2. `inventory-dark` has a documented supported/legacy decision and no known serious contrast regression on its audited surfaces.
3. A seeded missing semantic Tailwind mapping and a seeded fixed-white-on-theme-surface regression fail deterministically.
4. The result is a bounded, reviewable diff; a mass formatting/search-replace sweep of ~thousands of fallbacks is explicitly out of scope.

## Responsive re-audit registration 2026-10-04

Source: `docs/qa/RESPONSIVE_REAUDIT_2026-10-04.md`; evidence: `.ai/runs/2026-10-04-responsive-reaudit-evidence.md`. Base `origin/main` `6a2a23b3`. Live proof: headless Chrome (Playwright-core, Chromium emulation, `isMobile`/`hasTouch` for phone and tablet profiles) against `https://trendplus.vercel.app` on 2026-10-04 21:10–21:40 (Europe/Belgrade), viewports 360x780, 390x844, 768x1024, 1024x768, 1280x800, plus dated URLs `fromDate=2026-07-07&toDate=2026-08-05` so tables/charts render with real rows. The deployed Vercel build SHA was not readable from the UI, so every live finding is also tied to the current-main source line that produces it. Real iOS/iPadOS Safari is still unproven (Chromium emulation only). De-duplicated against P-UI-01..P-UI-38, PERF18, `docs/ai/RESPONSIVE_UI_AUDIT_PROMPTS_2026-10-01.md` and the RQ queues. Work already owned by P-UI-31/P-UI-35/P-UI-36/P-UI-38 is recorded as dated addenda on those prompts, not as new prompts.

## P-UI-39 - Make the shared AnalyticsControlBar overflow-safe by default (phone layout-viewport inflation and 1024px overflow)

Status: DONE
Ready after: none
Priority: P1
Type: frontend/css/tests
Feature family: analytics-control-bar-overflow
Parallel-safe: yes
Owner: unassigned
Commit suggestion: `fix(ui): make analytics control bar fields shrink inside narrow layouts`

### Problem

Only three pages opt into the overflow-safe filter grid (`responsiveFilterLayout`: Dashboard, Daily Sales, Inventory). On every other `AnalyticsControlBar` page, the default grid and the native `<select>` keep their intrinsic width. A native select is as wide as its longest option, so long Serbian labels push the whole page wider than the screen. Examples: store `Komision (Gospodska 6, N/A) [I…`, season `Proleće-leto 2021`, supplier `"Tim Tod"doo`. On a phone this inflates the layout viewport. Chrome renders the page about 17% zoomed out, or with sideways panning. On a 1024px laptop or tablet in landscape with the sidebar open, the page gets a horizontal scrollbar.

### Evidence

- Live, 360x780 phone emulation with data: `window.innerWidth` becomes 426 on Color, 433 on Shoe Type and 433 on Pre/Post. It should be 360. The widest non-scrolling element on each is `label.analytics-control-bar__field` (380px wide) containing the Objekat/Sezona/Dobavljač select.
- Live, 1024x768 with the sidebar open: `documentElement.scrollWidth - innerWidth` = 106 on Color, 106 on Shoe Type and 104 on Pre-Nivelacija. The offender is the fourth `select` (180px) of `.analytics-control-bar__fields`, ending at x≈1130.
- `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.css:153-158`: `.analytics-control-bar__fields { grid-template-columns: repeat(4, minmax(180px, 1fr)) }`. At 1024 the content column is 704px minus padding, which is less than 4×180 plus gaps.
- `AnalyticsControlBar.css:160-163` (field has no `min-width: 0`) and `:174-183` (select/input have no `width`/`min-width`).
- `AnalyticsControlBar.css:192-203`: the safe rules (`minmax(min(100%,180px),1fr)`, `min-width:0`, `width:100%`) exist only under `.analytics-control-bar--responsive-pilot`.
- `AnalyticsControlBar.css:211-215` (`@media (max-width:960px)` still uses `repeat(2, minmax(180px,1fr))`) and `:227-230` (`<=640px` uses `1fr`, i.e. `minmax(auto,1fr)`).
- `AnalyticsControlBar.tsx:89,139`: `responsiveFilterLayout = false` by default. Pages without the opt-in: `ColorSalesStatsPage`, `ShoeTypeSalesStatsPage`, `ProdajaPrePostNivelacijePage`, `PreNivelacijaPriorityPage`, `SupplierSalesStatsPage`, `SupplierFootwearAnalyticsPage`.

### Scope

- `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.css`
- `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.tsx` only if a class hook is needed
- its nearest spec under `components/analytics/__tests__/`
- `Klijent/clientapp/scripts/responsive_baseline.mjs` only to add a regression case for this fix
- No page files, no filter semantics, no URL parameters, no option text changes

### Read first

- P-UI-28 completion evidence (filter pilot) and `docs/ai/RESPONSIVE_UI_AUDIT_PROMPTS_2026-10-01.md`
- `AnalyticsControlBar.css` and `.tsx`
- `docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md`

### Do

1. Make the base (non-pilot) grid overflow-safe: `grid-template-columns: repeat(auto-fit, minmax(min(100%, 180px), 1fr))` or an equivalent. Fields get `min-width: 0`. `select`/`input` get `box-sizing: border-box; width: 100%; min-width: 0; max-width: 100%`. Wide fields must never span more tracks than exist.
2. Keep the existing pilot-only behaviour (mobile filter summary/disclosure) opt-in. Only the geometry safety becomes default.
3. Long option text must truncate inside the closed select, never widen the page. Do not shorten or rename the option labels.
4. Keep the desktop layout at 1280 visually equivalent (4 columns when space allows).

### Tests

- Vitest: base control bar renders fields with the overflow-safe class/geometry contract. Use a jsdom style assertion or a class contract, not pixels.
- Responsive runner (existing Puppeteer `responsive:baseline`), with a fixture option label of at least 60 characters: at 360 (mobile emulation) `window.innerWidth === 360`, and at 360/390/768/1024 (sidebar open)/1280 the document has no horizontal overflow on Color, Shoe Type, Pre/Post and Pre-Nivelacija.
- `npm run typecheck`, `npm run build`, analytics guardrails, governance validators, `git diff --check`.

### Acceptance

- On a phone, Color/Shoe Type/Pre-Post/Pre-Nivelacija keep `innerWidth == viewport width` (no zoom-out), even with the longest real store/season/supplier names.
- No document-level horizontal overflow at 1024 with the sidebar open on any control-bar page.
- Filter values, defaults, URL state and option text are unchanged.

### Dependencies

- None. P-UI-35/P-UI-36/P-UI-31 later migrate those pages to the full responsive pilot and must not be reverted by this change.

### Claim note 2026-10-06

- RQ588 is DONE and fresh post-close recovery at `origin/main` `4762916deaf9701d21ae46c5d4f81ffa5debe094` found no dependency-complete RQ successor. Q69's stale PARTIAL status was reconciled to its already evidenced DONE state; Q70-Q83 are DONE.
- BCI has no READY or IN_PROGRESS prompt; STAB16 remains provider/deployed-proof gated; QDB/MT/GAI have no READY execution lane. P-UI-39 is the primary collision-safe P1 candidate. No matching lock, branch or open PR exists. Its owned `AnalyticsControlBar` files/specs are separate from the just-completed `AnalyticsEmptyState.css` fix.
- Local lock: `.ai/task-locks/P-UI-39-codex.lock.md`.

### Completion note

- Date: 2026-10-06
- Status: DONE
- Completion: Shared filter fields shrink within their grid tracks by default; long store labels no longer inflate the viewport or create document-level horizontal overflow.
- Changed files: `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.css`; `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.tsx`; `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsControlBar.spec.tsx`; `Klijent/clientapp/scripts/responsive_baseline.mjs`; queue and roadmap evidence.
- Checks run: focused Vitest 3/3; responsive fixture matrix 56/56 across four routes, two themes and seven widths (0 root overflow observations, 0 page errors); `npm run typecheck`; `npm run build`; analytics guardrails; governance validators; `git diff --check`.
- Checks not run: full analytics test suite; not required by the focused acceptance.
- Run log: `.ai/runs/2026-10-06-P-UI-39-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `49548a92985cc72d4aee8e5cd392a8c35dbb54d4`
- Main verification: pushed to `origin/main`; fresh fetch confirmed `HEAD == origin/main == 49548a92985cc72d4aee8e5cd392a8c35dbb54d4`.
- Missed: none known.
- Follow-up: determine the next collision-safe READY task during mandatory post-close routing recovery.
- Residual risk: analytics guardrails report 39 pre-existing baseline violations and 0 removed; build reports the existing large-chunk warning.
- Post-close routing: pending recovery scan from the post-close `origin/main` SHA.
- Prompt defect / scope repair: none.
- Run log: `.ai/runs/2026-10-06-P-UI-39-evidence.md`
- Evidence state: pending

## P-UI-40 - Small-laptop shell (1024–1279px): single-row header and space-saving sidebar

Status: DONE
Ready after: none
Priority: P1
Type: frontend/layout/tests
Feature family: app-shell-small-laptop
Parallel-safe: yes
Owner: unassigned
Commit suggestion: `feat(layout): compact header and sidebar rail for small laptops`

### Problem

From 1024px up, the shell switches to the full desktop mode: a fixed 320px sidebar is expanded by default, and the header shows every action at once and wraps. On a 1024px tablet in landscape or a small laptop, the sticky header is 3 rows tall and permanently covers 22–23% of the screen. The content column shrinks to about 704px. That column width is the direct cause of the control-bar overflow (P-UI-39) and of very narrow tables (Daily table scroller 641px, Product Decision 672px, Pre-Nivelacija 612px). The collapsed state is not remembered between visits.

### Evidence

- Live header height (sticky): 177px at 1024x768 and 166px at 1280x800 on every route. Compare 64px at 768 and 80px at 360. The screenshot at 1024 shows three rows: title/backend chip; Komande, Obaveštenja, Kontekst, Prikaz Sve; Prikaz select, Teme, Osveži.
- Live sidebar at 1024: `aside.w-80` = 320px visible, so content is 704px.
- `Klijent/clientapp/src/layout/components/HeaderStatus.tsx:400-401`: sticky header, `lg:flex-wrap lg:gap-3`. `:439` system strip `lg:flex`. `:457` action group `ml-auto hidden flex-wrap … lg:flex`. `:371-375`: the compact mode is tied to `max-width: 1023px`.
- `Klijent/clientapp/src/layout/components/Sidebar.tsx:90` (`w-80`), `:219` (`lg:w-14` collapsed rail), `:231` (`hidden lg:block lg:sticky lg:top-0 lg:h-screen`).
- `Klijent/clientapp/src/layout/AppLayout.tsx:13`: `useState(false)` for `sidebarCollapsed`. No persistence and no width-based default.

### Scope

- `layout/components/HeaderStatus.tsx`, `layout/components/Sidebar.tsx`, `layout/AppLayout.tsx` and their tests
- No navigation IA change (navConfig labels/groups unchanged) and no removal of any header action

### Read first

- P-UI-26 completion evidence (mobile shell/drawer, which must not regress)
- `HeaderStatus.tsx`, `Sidebar.tsx`, `AppLayout.tsx`, `layout/__tests__`, `layout/components/__tests__`

### Do

1. Between 1024 and 1279px, keep the header to one row (target ≤ 88px). Move the secondary actions (Kontekst, Prikaz, Teme, Obaveštenja list) into one accessible "Više" menu. Do not remove them. At ≥1280, show the full action set only if it fits in one row. Otherwise use the same overflow menu.
2. Between 1024 and 1279px, default the sidebar to the existing 56px rail (`lg:w-14`), with the expanded panel available on demand. Persist the user's explicit collapse/expand choice (localStorage key, SSR/jsdom safe). An explicit user choice beats the width default.
3. Keep the <1024 drawer behaviour from P-UI-26 unchanged: dialog, focus trap, Esc, scroll lock.
4. Keep keyboard order, `aria-expanded`, focus-visible and the skip-to-content behaviour if present.

### Tests

- Vitest: default rail at 1100px width (matchMedia mock), persisted choice wins, overflow menu exposes every action, Esc/focus behaviour on the menu.
- Responsive runner at 1024x768 and 1280x800: header height ≤ 88px, content column ≥ 900px at 1024 with the rail, no document overflow; 360/768 header heights unchanged (±4px).
- typecheck, build, guardrails, governance validators, `git diff --check`.

### Acceptance

- At 1024 and 1280 the sticky header uses at most about 11% of the viewport height. The analytics content column is at least 900px at 1024 with the rail. At 1280 preserve the intended desktop sidebar policy; do not claim a full-width content column while the 320px sidebar is expanded, but require a single-row/overflow-menu-safe header and no root overflow.
- Every header action stays reachable by mouse, touch and keyboard.
- Mobile/tablet (<1024) shell behaviour is unchanged.

### Dependencies

- None. RQ583 (owner-approved global warning/critical stale-data banner on every analytics route, WAITING) will add a banner to the shell. The banner is outside the one-row header budget but must stay a single line (≤ 48px) at ≥1024, and whichever lands second rebases and re-measures.

### Claim note 2026-10-06

- P-UI-39 is DONE on main; post-close recovery at `origin/main` `ae9a7e5f25830776a7b70216483a2bbac11d7d57` found no higher-priority READY RQ/SQL lane. The active RQ partials remain evidence/provider gated; SQL queue has no READY prompt.
- P-UI-40 is the next collision-safe P1 lane. No matching local lock, branch or open PR exists. Its HeaderStatus/Sidebar/AppLayout paths are separate from READY P-UI-41 analytics page paths and P-UI-47/49 theme/state paths.
- Local lock: `.ai/task-locks/P-UI-40-codex.lock.md`.

### Completion note

- Date: 2026-10-06
- Status: DONE
- Completion: The shell defaults to the 56px sidebar rail at 1024–1279px, stores the user's explicit collapse/expand choice, and keeps the header in one row with all actions reachable from the accessible “Više” dialog when the full action group does not fit.
- Changed files: `Klijent/clientapp/src/layout/AppLayout.tsx`; `Klijent/clientapp/src/layout/AppLayout.spec.tsx`; `Klijent/clientapp/src/layout/components/HeaderStatus.tsx`; `Klijent/clientapp/src/layout/components/Sidebar.tsx`; `Klijent/clientapp/src/layout/components/__tests__/HeaderStatus.spec.tsx`; `Klijent/clientapp/src/layout/components/__tests__/Sidebar.spec.tsx`; `Klijent/clientapp/scripts/responsive_baseline.mjs`; queue and roadmap evidence.
- Checks run: focused shell Vitest 15/15; responsive shell matrix 20/20 across two themes and ten widths (0 root overflow, 0 page errors); at 1024×768 header 67.5px/main 968px; at 1280×800 header 67.5px; at 360/768 header heights match pre-change; `npm run typecheck`; `npm run build`; analytics guardrails; governance validators; `git diff --check`.
- Checks not run: full analytics Vitest suite locally; focused shell tests plus browser geometry cover the changed shell contract.
- Run log: `.ai/runs/2026-10-06-P-UI-40-evidence.md`
- Evidence state: pending post-close routing recovery
- Delivery mode: direct-main
- Main commit SHA: `3c7a84b3099392dc94209eb46e3bf3bafc0dfc4f`
- Main verification: fresh fetch after closure confirmed `origin/main` at `72f85d81fda2626f02d15a66837760a8b1997fb3`, which contains implementation `3c7a84b3099392dc94209eb46e3bf3bafc0dfc4f`.
- Missed: none known.
- Follow-up: P-UI-48 is now IN_PROGRESS after P-UI-40 satisfied its explicit dependency.
- Residual risk: the 2400px breakpoint exposes the full action cluster only when it fits; GitHub Analytics Quality Gates run `37384995175` is green on this SHA; the existing build chunk-size warning and 39 guardrail baseline findings remain.
- Post-close routing: post-close scan at `origin/main` `72f85d81fda2626f02d15a66837760a8b1997fb3` covered the full 16-file active RQ/SQL/UI queue/addendum set, including `MASTER_ROADMAP.md`. P-UI-48 was promoted WAITING -> READY -> IN_PROGRESS because P-UI-40 was DONE and no conflicting lock, branch or open PR remained. P-UI-41/P-UI-47/P-UI-49/P-UI-51/P-UI-52 remain independent READY lanes; P-UI-45/P-UI-42 remain WAITING on P-UI-48; P-UI-31/P-UI-35/P-UI-36 remain WAITING on P-UI-47; P-UI-43/P-UI-44 remain behind RQ569. RQ current READY and SQL current READY are none; higher-priority BCI/QDB/MT/GAI execution lanes remain none and STAB16 remains provider/deployment gated. No higher-priority runnable successor was found.
- Prompt defect / scope repair: none.
- Run log: `.ai/runs/2026-10-06-P-UI-40-evidence.md`
- Evidence state: synchronized

## P-UI-41 - Stop intrinsic-width overflow in the Pilot intake report and Inventory insight panels on phones

Status: DONE
Ready after: none
Priority: P1
Type: frontend/css/tests
Feature family: report-inventory-intrinsic-overflow
Parallel-safe: yes
Owner: Codex
Commit suggestion: `fix(ui): contain wide report tables and inventory panels on phones`

### Problem

Two screens widen the phone layout viewport for reasons unrelated to the control bar:
- The Pilot intake report renders its "durable" tables with classes that have no CSS at all: no scroll wrapper, no width rules. The 406–417px tables push every card on the page to 417px.
- Inventory insight panels are grid items without `min-width: 0`, so their content forces 415px cards.

### Evidence

- Live 360x780 mobile emulation: `/analytics/reports/pilot-intake` has `innerWidth` 433. The widest elements are `section.analytics-trust-header`, `section.analytics-refresh-banner`, `div.pirp-actions` and `section.pilot-intake-card`, all 417px. The tables measure 406/309/417/417/417px with no scroll container. The same happens at 390 (`innerWidth` 433), and also without dated data (first pass).
- Live 360: `/analytics/inventory` has `innerWidth` 431. The widest elements are `div.rounded-[28px]` "Zastarelost i obrt zalihe" and "ABC segmentacija kapitala" (415px), then "Rizik i prioriteti"/"Vrednost po dobavljaču" (378px) and `div.recharts-wrapper` (336px, ends at 373).
- `Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.tsx:220-221`: `pilot-intake-durable-table-wrap` / `pilot-intake-durable-table`. A search of all `*.css` finds no rule for either class.
- `Klijent/clientapp/src/components/inventory/InventoryInsightPanels.tsx:66` (`grid gap-5 xl:grid-cols-[1.05fr_0.95fr]`), `:67` (panel without `min-w-0`), `:84` and `:151` (inner grids).

### Scope

- `components/analytics/PilotDataQualityIntakeReport.tsx` and `.css`
- `pages/PilotIntakeReportPage.css` for the report page's own min-content grid track; do not edit `AnalyticsTrustHeader.*`
- `components/inventory/InventoryInsightPanels.tsx` (and `InventoryPriorityPanels.tsx` only if the same pattern is proven there)
- the responsive baseline route/fixture in `Klijent/clientapp/scripts/responsive_baseline.mjs` because the existing runner had no Pilot intake route for this prompt's named viewport acceptance
- nearest tests
- Not `InventoryPage.tsx` and not `AnalyticsTrustHeader.*` (RQ569 owns the trust-header/page trust props)

### Read first

- P-UI-29 table pilot (scroll hint, sticky first column) and `AnalyticsDataTable.css`
- RQ466/RQ467 pilot intake contracts (no content/semantics change)

### Do

1. Give `pilot-intake-durable-table-wrap` a contained horizontal scroll (`overflow-x:auto`, keyboard-focusable region with label, `overscroll-behavior-inline: contain`). Give the table readable cell padding and numeric alignment, consistent with `AnalyticsDataTable`. Keep the print styles working (`@media print` in `PilotDataQualityIntakeReport.css:349`).
2. Add `min-w-0` (or `minmax(0,1fr)` tracks) to Inventory insight panel grid items and inner cards. Let long words/numbers wrap or truncate with a title. The chart container must respect its parent width.
3. Do not hide any column or value.

### Tests

- Vitest: the durable table renders inside a labelled scroll region. Inventory panels carry the min-width contract class.
- Responsive runner: at 360/390 (mobile emulation) `innerWidth === viewport` on `/analytics/reports/pilot-intake` and `/analytics/inventory` with data, and no document overflow at 768/1024/1280.
- Print smoke test of the pilot intake report (existing print CSS still applies).
- typecheck, build, guardrails, governance validators, `git diff --check`.

### Acceptance

- Neither page zooms out or pans sideways on a phone. Wide tables scroll inside their own region with a visible affordance.
- No report/inventory value, label or order changes.

### Dependencies

- None. RQ576 (inventory valuation/aging) is DONE on `main` at `e5e2dc83bf73eee8b34f0b7e3565afa272625535`; the RQ576-owned page/KPI paths were verified separate from `InventoryInsightPanels.tsx`.

Owner promotion/claim 2026-10-06: refreshed `origin/main` at implementation base `575af2ab3f48eb9668477b9b95b90928b4a61aec`; confirmed RQ576 is DONE and its former P-UI-41 Inventory-path collision is released. No matching P-UI-41 lock, branch or open PR exists. P-UI-41 moved READY -> IN_PROGRESS for the scoped Pilot intake/Inventory overflow fix; local lock `.ai/task-locks/P-UI-41-codex.lock.md`.

Owner release 2026-10-06: P-UI-41 was returned to READY before implementation while the same workspace resumed P-UI-48 for its in-scope CI test correction. The P-UI-41 local lock was removed; RQ576's path collision remains clear.

Owner promotion/claim 2026-10-06: post-close recovery refreshed `origin/main` to `858ade29e8c5a95315ce94c3d870ba71f4746d06`, verified RQ576 DONE on `e5e2dc83bf73eee8b34f0b7e3565afa272625535`, and found no matching P-UI-41 lock, branch or open PR. P-UI-41 moved READY -> IN_PROGRESS for the Pilot intake/Inventory phone-overflow scope; local lock `.ai/task-locks/P-UI-41-codex.lock.md`.

Prompt repair 2026-10-06: the shared responsive baseline route set had no Pilot intake entry, so its own acceptance could not measure the named route in fixture mode. Added a bounded Pilot intake route/response fixture and print-media smoke assertion to the existing runner; this creates synthetic presentation evidence only and does not change report semantics. A 360px fixture measurement then proved `.pilot-intake-report-page` was allowing an intrinsic-width child to expand its grid track; added `min-width: 0` to that report-owned wrapper and durable sections, without changing the RQ569-owned trust header.

Owner completion 2026-10-06: P-UI-41 is DONE. Durable Pilot intake tables now use labeled keyboard-focusable horizontal scrolling with readable numeric alignment and print behavior; Inventory insight panels/cards can shrink without hiding values, with full labels available by title. The report wrapper's min-width repair and responsive runner's synthetic Pilot fixture were required by observed viewport evidence and are documented above. Focused Vitest passed 15/15; analytics guardrails, typecheck, production build, governance validators and `git diff --check` passed. Strict responsive fixture passed 40/40 route/viewport/theme cases with zero page overflow/errors; Pilot print smoke passed 20/20. Implementation SHA `cf9ba0428387d9c1e962e3dad77de13cb37eb5eb` is freshly verified on `origin/main`; Analytics Quality Gates run `37389415426` and Planning Governance run `37389415620` passed on that SHA. The full 16-file post-close cascade claimed P-UI-47; run log `.ai/runs/2026-10-06-P-UI-41-evidence.md`; Evidence state: synchronized.

Owner routing correction 2026-10-06: after P-UI-48 closure SHA `9663b4b8be7624be47dd4585ffd40dbb34a40cd6`, Analytics Quality Gates run `37386823059` failed the broad ConfigurationPage refresh-button count against the two independent settings refresh controls. P-UI-48 resumed IN_PROGRESS to correct the assertion to the worker control's accessible name. P-UI-41 returned to READY before implementation so this workspace has one active claim; its RQ576 collision remains clear.

### Addendum 2026-10-04 (UX/UI audit live evidence; no scope change)

- The parallel live browser audit independently confirmed the Inventory overflow at 390×844 (capture 431px wide, horizontal scrollbar; audit UX-042). No separate prompt was registered.

## P-UI-42 - Extend the touch-size and 16px input floor to coarse-pointer tablets and hybrid touch devices

Status: DONE
Ready after: P-UI-39 DONE, P-UI-40 DONE, P-UI-47 DONE, P-UI-48 DONE, P-UI-43 DONE and P-UI-51 DONE (shared trust-header/control-bar/theme/header ownership)
Priority: P2
Type: frontend/css/tests
Feature family: responsive-coarse-pointer-tablet
Parallel-safe: no
Owner: Codex (Analytics Frontend / Responsive UI)
Owned paths checked: `tailwind.css`, `styles/themes.css`, `styles/forms.css`, `AnalyticsControlBar.css`, shared UI/header class contracts, and focused responsive/browser tests.
Commit suggestion: `feat(ui): apply touch and input-size floor to coarse-pointer tablets`

### Problem

The P-UI-25 foundation applies the 16px form-control font and 44px button height by viewport width only, below 760px (`max-width: 759px`). Nothing is keyed to `pointer: coarse` except a data-entry search-panel tweak. The 2026-10-01 responsive contract also requires ≥16px text and 44px targets on coarse-pointer tablets. On a 768–1023px touch tablet, analytics filters are still 12–14px and most buttons, chips and links are 26–42px. That risks iPad Safari focus-zoom and causes mis-taps.

### Evidence

- Live 768x1024 with `hasTouch`: form-field font is 13px on `/analytics`, Inventory, Actions, Color, Shoe Type and Daily; 12.16px on Product Decision; 14px on `/prodaja`. At 360 the same fields are 16px.
- Live 768: targets below 44px include the header menu button 35x35, the "Više" button 70x34, breadcrumb links 17px tall, trust-header footer links 34px, `kpi-explain-button` 116x17, Inventory toolbar buttons 17x36, `/prodaja` chips 94x27 and 112x30 with inputs 38px, and `/dobavljaci` row actions 71x26 and 67x26 (237 of 237 targets small).
- `Klijent/clientapp/src/tailwind.css:604-622` (`@media (max-width: 759px)`: global 16px control font with `!important` and 44px `button` min-height), `Klijent/clientapp/src/styles/forms.css:544` and `:565` (`max-width: 759px` form/data-entry floors), `:578` (the only coarse-pointer rule, a search-panel layout tweak), `AnalyticsControlBar.css:182` (13px), `layout/components/HeaderStatus.tsx:406` (menu button `p-2`), `:544` ("Više" `py-2 text-xs`).
- Contract: `docs/ai/RESPONSIVE_UI_AUDIT_PROMPTS_2026-10-01.md` responsive design table ("Form controls | Tablet: text >=16px on coarse pointer; target 44px").

### Scope

- Foundation CSS (`tailwind.css`, `styles/themes.css` tokens, `styles/forms.css`), `AnalyticsControlBar.css`, and header/shared button primitives (`components/ui/*`, `HeaderStatus.tsx` class names only)
- No per-page redesign, and no change for fine-pointer desktops

### Read first

- P-UI-25 and P-UI-27 completion evidence; the responsive design contract table

### Do

1. Add a capability-based touch layer using `any-pointer: coarse` (or an equivalent hybrid-safe strategy), not viewport width alone, so touch-capable tablets and hybrid devices get form-control text ≥16px and shared interactive targets ≥44x44 via the existing tokens (`--size-touch-target`). Inline text links inside paragraphs are documented exceptions. Fine-pointer-only desktops keep their dense layout.
2. Apply it to the shared primitives first (control bar fields/actions, header buttons, KPI explain button, trust-header links, table toolbar buttons, chips). Record the remaining page-local offenders as a list in the run log for P-UI-46. Do not fix them here.
3. Keep fine-pointer desktop density unchanged.

### Tests

- Responsive runner with a `hasTouch` 768 and 1024 profile: on the listed routes no shared-primitive target is below 44px and no text field is below 16px. A fine-pointer 1280 profile stays unchanged within ±2px.
- Vitest for any primitive class contract changes; typecheck, build, guardrails, governance validators, `git diff --check`.

### Acceptance

- Coarse-pointer tablets meet the documented 16px/44px contract on shared primitives, and desktop density is unchanged.

### Dependencies

- P-UI-39, P-UI-40, P-UI-47 and P-UI-48 own overlapping shared CSS/header/token surfaces and land first. P-UI-46 consumes the leftover list.

### Owner claim 2026-10-07

- After P-UI-51 DONE, refreshed `origin/main` to `68731e8269ac4ab064fbe17fefec964c8c7d3f3e` and scanned all 15 active RQ/SQL/P-UI queue files plus `MASTER_ROADMAP.md`.
- Verified P-UI-39/P-UI-40/P-UI-43/P-UI-47/P-UI-48/P-UI-51 DONE; P-UI-42 has no matching local/remote branch, open PR or active task lock. P-UI-52 remains independently READY and owns nav/copy paths, not the tablet shared CSS paths.
- Higher-priority RQ/SQL current READY pointers remain none; RQ139's derived builder residual is already addressed by DONE RQ152 and RQ153 is DONE. The remaining RQ PARTIAL items are broad cross-surface or live/runtime acceptance. STAB16 remains provider/deployment gated; the current roadmap exposes no higher-priority BCI/QDB/MT/GAI READY execution candidate.
- Promoted WAITING -> READY -> IN_PROGRESS and claimed on `codex/p-ui-42-coarse-pointer-tablet` from refreshed `origin/main` `68731e8269ac4ab064fbe17fefec964c8c7d3f3e`.

### Completion note

- Date: 2026-10-07
- Status: DONE
- Completion: Coarse-pointer and hybrid devices now receive the shared 16px field and 44x44 shared-target floors. The responsive runner verifies 768/1024 touch profiles and reports remaining page-local links for P-UI-46.
- Changed files: `Klijent/clientapp/src/tailwind.css`; `Klijent/clientapp/src/styles/forms.css`; `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.css`; `Klijent/clientapp/src/components/analytics/AnalyticsDataTable.css`; `Klijent/clientapp/src/components/analytics/AnalyticsEmptyState.css`; `Klijent/clientapp/src/components/analytics/AnalyticsRefreshStatusBanner.css`; `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader.css`; `Klijent/clientapp/src/components/analytics/MetricMethodologyPanel.css`; `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsEmptyState.spec.tsx`; `Klijent/clientapp/scripts/responsive_baseline.mjs`.
- Contract/runtime behavior changed: `any-pointer: coarse` applies shared touch floors using existing tokens; fine-pointer layouts do not receive these rules. Empty-state, refresh, trust-header, KPI explanation, table toolbar and control-bar actions are covered.
- Checks run: 8 focused Vitest files / 65 tests passed; `npm run responsive:baseline -- --self-test` passed; responsive fixture profiles passed on 9 routes at both 768px and 1024px (zero shared undersized targets, zero root overflow, zero page errors); fine-pointer 1280px profile passed on 8 routes with 0px maximum change in audited control font/width/height; analytics guardrails/encoding/typecheck passed (39 existing findings, zero new); production build passed; `git diff --check` passed.
- Checks not run: full Vitest suite; physical iPad/Safari and other device/browser evidence. Chromium touch emulation is the available deterministic runner.
- Run log: `.ai/runs/2026-10-07-P-UI-42-evidence.md`.
- Evidence state: synchronized.
- Delivery mode: direct-main.
- Main commit SHA: `6313e1d5ff20023d814c68f6ac8422b710d58043`.
- Main verification: fresh `origin/main` at `6313e1d5ff20023d814c68f6ac8422b710d58043`; `git merge-base --is-ancestor 6313e1d5 origin/main` passed.
- Missed: smaller page-local links remain for the owning P-UI-46 follow-up. Their exact selectors/labels and measured sizes are listed in the run log.
- Follow-up: P-UI-46 consumes the remaining page-local offender list. P-UI-52 remains an independent READY lane.
- Residual risk: local responsive Chromium fixture evidence does not replace physical-device Safari evidence; existing Recharts chunk-size advisory remains.
- Post-close routing: full post-delivery scan and P-UI-46 successor claim are recorded in `.ai/runs/2026-10-07-P-UI-42-evidence.md`.
- Prompt defect / scope repair: no change to task boundaries; applied the common foundation to two shared refresh/empty-state links after the live shared-control audit exposed them.

## P-UI-43 - Compact the trust header on phones so data appears in the first screen

Status: DONE
Ready after: RQ569 DONE (RQ569 changes `AnalyticsTrustHeader` to show the observed source horizon)
Priority: P2
Type: frontend/ux/tests
Feature family: trust-header-mobile-compaction
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/P-UI-43-codex.lock.md`
Commit suggestion: `feat(ui): compact analytics trust header on phones`

Owner claim 2026-10-06: P-UI-36 DONE was delivered and verified on `origin/main` `3d6c538d4b60508accea3c5942fdf8a1f2a303df`. Post-close recovery scanned the full 16-file active RQ/SQL/UI queue set; RQ569 is DONE and no active trust-header owner, lock, matching branch or open PR conflicts with `AnalyticsTrustHeader`. P-UI-43 was the primary collision-safe READY lane and moved READY -> IN_PROGRESS. Local lock: `.ai/task-locks/P-UI-43-codex.lock.md`; recovery evidence: `.ai/runs/2026-10-06-P-UI-36-evidence.md`.

### Problem

Every analytics screen starts with `AnalyticsTrustHeader`. On a phone it stacks the period, last refresh, data source, gating text, the methodology note and the footer links as full-width cards. Next comes the control bar, also tall. The owner has to scroll 2–5 phone screens before seeing the first KPI. The trust information is required, but it should be summarised first and expanded on demand.

### Evidence

Live 360x780 mobile, dated data:

| Route | Trust header height | Control bar | First KPI section top |
|---|---|---|---|
| Shoe Type | 1436px | 815px | 2403px (`shoetype-decision-kpis`) |
| Pre-Nivelacija | 1287px | 1081px | 4056px (`pnp-decision-kpis`) |
| Daily Sales | 1139px | 498px | 1777px |
| Inventory | 1341px | – | – |
| Color | 1036px | 815px | – |
| Pre/Post | 928px | 963px | – |
| Product Decision | 855px | – | 1402px |
| Supplier | 749px | – | – |
| Pilot intake | 649px | – | 1711px (first table) |

- At 1280 the header is still 356–919px tall.
- Code: `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader.tsx:292` (single `section` with all blocks always rendered); `AnalyticsTrustHeader.css:409` has only one breakpoint, `max-width: 720px`.

### Scope

- `components/analytics/AnalyticsTrustHeader.tsx` / `.css` and tests
- Page files only if a prop is needed to mark the primary status (prefer no page edits)
- No change to trust/readiness semantics, labels or which facts exist

### Read first

- RQ567/RQ569 trust-header contracts (after RQ569 lands), P-UI-20/P-UI-22 trust-state proofs

### Do

1. Below 640px, render a compact summary row: readiness status chip, effective period, observed horizon/freshness (from RQ569), and a "Detalji pouzdanosti" disclosure (`button` with `aria-expanded`/`aria-controls`) holding the remaining blocks and links.
2. A blocked/critical readiness state must stay visible in the summary. It must never be hidden behind the disclosure.
3. Between 640 and 1023px, use a two-column layout. At ≥1024, keep the current layout but cap the vertical padding.

### Tests

- Vitest: the summary always shows the readiness chip/period/horizon; the disclosure toggles the details; critical state stays visible when collapsed; existing trust-state specs still pass.
- Responsive runner at 360: trust header ≤ 260px collapsed on the routes above; the first KPI section starts within 1.5 viewport heights on Daily/Product Decision.
- typecheck, build, guardrails, governance validators, `git diff --check`.

### Acceptance

- On a phone, the first KPI or table is reachable in at most about 1.5 screens on the main analytics routes, and every trust fact is still one tap away.

### Dependencies

- RQ569 (same component). Later RQ570/RQ583 banners must use the summary row.

### Addendum 2026-10-04 (UX/UI audit; scope extension, no status change)

The UX/UI audit (`docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`, UX-003/UX-004/UX-005/UX-036/UX-038) found the same problem on desktop, so this prompt is extended rather than duplicated:
- Desktop budget: at 1280×800 the first KPI is above the fold on Dashboard, Product Decision, Supplier and Pre-Nivelacija (live: the trust header fills the first viewport in all six themes). The strip is at most 56px on desktop.
- Evidence disclosure: `pre_nivelacija.evidence`, `bounded_probe:…`, `sha256:…`, ISO UTC timestamps with seven fractional digits and raw readiness codes (`decision_readiness_unavailable`, `no_shoe_type_sales`, `color_is_supporting_signal`, `daily_sales.actionability_not_assessed`, `unverified`) move into a keyboard-accessible "Detalji" disclosure; the default view shows labels and Europe/Belgrade `dd.MM.yyyy HH:mm` times (`AnalyticsTrustHeader.tsx:327-328,371`).
- Copy: "Preporuka je gated" → Serbian (design system §8); label the "Ignorisani redovi" counter with its backend meaning; warnings use `role="status"` instead of `role="note"` (`:395`, `:399-403`).
- One `h1`: the trust header renders no title when the page already has one (live: "Odluke o proizvodima" three times).
- Consume RQ569 fields exactly as delivered; compute nothing in the browser. Design system: `docs/ai/ANALYTICS_DESIGN_SYSTEM.md` §3, §5.
- Extra tests: evidence IDs are absent from the default render but reachable by keyboard; Belgrade time format; `role="status"`; desktop fold check in `responsive:baseline`.
- Data Quality trust-state follow-up from the 2026-10-06 hierarchy audit: reproduce the browser fixture before changing semantics. The current `DataQualityPage` clears `health`/intake state on rejected requests and an **all trust sources failed** path must render quality status unavailable, never `good`. Add/retain regression proof for that invariant. If the health/readiness source succeeds as `good` while another decision-relevant Data Quality request fails, the header may keep the backend quality classification but must expose the availability/partial failure prominently enough that the page cannot read as fully verified/healthy. Do not silently convert transport failure into a business `critical` score.

### Completion note

- Date: 2026-10-06
- Status: DONE
- Completion: Mobile trust header is condensed, evidence and supporting facts remain keyboard-accessible in details, desktop first-KPI placement is measured, and Data Quality availability failures remain visibly partial/unavailable without fabricating a quality score.
- Changed files: `Klijent/clientapp/scripts/responsive_baseline.mjs`; `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader.css`; `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader.tsx`; `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsTrustHeader.spec.tsx`; `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`; `Klijent/clientapp/src/pages/DataQualityPage.tsx`; `Klijent/clientapp/src/pages/DataQualityPage.spec.tsx`; `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-06-P-UI-43-evidence.md`.
- Checks run: focused Vitest 68/68; analytics guardrails; production build; responsive browser fixtures at 360px, 768px and 1280×800; governance self-tests and validators; `git diff --check` (all passed).
- Checks not run: full frontend suite not run; no task acceptance required it. Relevant Analytics Quality Gates run `37522392701` was in progress on implementation SHA at inspection.
- Run log: `.ai/runs/2026-10-06-P-UI-43-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `1b6856051fa1fb4e759ac4564cbf13cb3e779976`
- Main verification: fresh fetch at `3a84254360400a3b149a0caa6f446057328718ed` confirmed `origin/main` contains implementation SHA `1b6856051fa1fb4e759ac4564cbf13cb3e779976` and the closure commit.
- Missed: none known
- Follow-up: P-UI-45 is the next primary READY lane; P-UI-49, P-UI-51 and P-UI-52 remain independent READY lanes. P-UI-42 remains WAITING on P-UI-51.
- Residual risk: Analytics Quality Gates was still in progress when inspected; existing build emitted the repository's Recharts chunk-size warning.
- Post-close routing: full 16-file recovery recorded in `.ai/runs/2026-10-06-P-UI-43-evidence.md`; P-UI-45 selected as successor, unclaimed.
- Prompt defect / scope repair: extended the task to cover the dated Data Quality availability failure contract; the browser keeps backend quality classification while surfacing failed-source availability.

## P-UI-44 - Wide Operations tables: sticky key column and scroll affordance for Daily Sales and Inventory items

Status: DONE
Ready after: RQ569 DONE (touches `DailySalesStatsPage.tsx` / `InventoryPage.tsx` trust wiring)
Priority: P2
Type: frontend/tests
Feature family: operations-wide-table-responsive
Parallel-safe: no
Owner: unassigned
Commit suggestion: `feat(ui): sticky key column for wide operations tables`

### Problem

The P-UI-29 table pilot (sticky first column, labelled scroll region, scroll hint) is enabled only on Article List, Actions and Color. The two widest Operations tables still scroll with nothing to anchor them: Daily Sales (21 columns, 1795px) and Inventory items (17 columns, 1323px). On a phone the date or article column scrolls away, so a number cannot be matched to its row.

### Evidence

- Live 360 with dated data: Daily table 1795px wide, 21 columns, 30 rows, in a 297px scroller; first cell `position: static`; 12px cells. Inventory items table 1323px wide, 17 columns, 50 rows, in a 284px scroller; first cell static.
- Live 1024: Daily scroller 641px, Inventory 628px. Product Decision is sticky only ≤900px (static at 1024 with 1442px width in a 672px scroller).
- `Klijent/clientapp/src/components/analytics/AnalyticsDataTable.tsx:8,22,28` (`responsivePilot` opt-in); only `ArtikliListPage.tsx:487`, `AnalyticsActionsPage.tsx:1297` and `ColorSalesStatsPage.tsx:1219` pass it.
- `AnalyticsDataTable.css:125-160`: sticky first column only inside `@media (max-width: 900px)`.
- `Klijent/clientapp/src/components/inventory/InventoryItemsTable.tsx` is a separate raw `<table>` without the pilot behaviour.

### Scope

- `DailySalesStatsPage.tsx` (table call site only), `components/inventory/InventoryItemsTable.tsx`, `AnalyticsDataTable.css` (sticky threshold up to 1279px when the table overflows)
- Not Shoe Type/Pre-Nivelacija/Product Decision tables (owned by P-UI-35/P-UI-36/P-UI-32)

### Read first

- P-UI-29 and P-UI-34 completion evidence; P-UI-37 Article List example

### Do

1. Enable the pilot table behaviour on the Daily table. Give Inventory items the equivalent: labelled focusable scroll region, sticky date/article column, scroll hint.
2. Apply the sticky key column whenever the table actually overflows its scroller, not only at ≤900px. Add a container-width check or a class toggled by a ResizeObserver.
3. Keep every column, sort and export unchanged.

### Tests

- Vitest: pilot props/classes present on both tables; sticky column class applied when overflow is detected (mocked ResizeObserver).
- Responsive runner: Daily and Inventory at 360/768/1024 keep the first column visible after scrolling the region to its end (`getBoundingClientRect().left` of the first cell ≥ scroller left).
- typecheck, build, guardrails, governance validators, `git diff --check`.

### Acceptance

- On a phone, tablet and 1024 laptop, the key column of the Daily and Inventory tables stays visible while scrolling horizontally. No data or column changes.

### Dependencies

- RQ569 (same page files).

### Claim note 2026-10-06

- RQ569 is DONE on current `origin/main` `1f8356eaa799514e281b78e2fe59f45725d620ef`.
- The P-UI-43 trust-header owner is active but uses separate trust-header paths. No P-UI-44 lock, matching branch, or open PR was found. User's existing dirty edits in the other checkout do not touch the P-UI-44 scope.
- P-UI-44 moved READY -> IN_PROGRESS on `codex/p-ui-44-operations-wide-table-responsive`. Local lock: `.ai/task-locks/P-UI-44-codex.lock.md`.
- Scope repair: the prompt's requested ResizeObserver overflow class requires the shared measuring hook in `AnalyticsDataTable.tsx`; `setupTests.ts` also needed its ResizeObserver polyfill to be configurable so the required observer mock can be installed. Both remain inside the same table owner.

### Completion note

- Date: 2026-10-06
- Status: DONE
- Completion: Daily Sales opts into the existing accessible table pilot. Daily Sales and Inventory measure actual horizontal overflow with `ResizeObserver`; under 1280px, their key column stays pinned only while the table overflows. Inventory now has a keyboard-focusable, labelled scroll region and a visible scroll hint.
- Changed files: `Klijent/clientapp/src/components/analytics/AnalyticsDataTable.css`; `Klijent/clientapp/src/components/analytics/AnalyticsDataTable.tsx`; `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsDataTable.spec.tsx`; `Klijent/clientapp/src/components/inventory/InventoryItemsTable.tsx`; `Klijent/clientapp/src/components/inventory/InventoryItemsTable.spec.tsx`; `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`; `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx`; `Klijent/clientapp/src/setupTests.ts`; `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`; `MASTER_ROADMAP.md`; `.ai/runs/2026-10-06-P-UI-44-evidence.md`
- Contract/runtime behavior changed: no API, analytics value, sorting, or export contract changed; the UI measures scroll-width overflow and keeps the first table column visible in the affected responsive layout.
- Checks run: focused Vitest 3 files / 36 tests passed; `npm run check:analytics-guardrails` (encoding, guardrail scan and typecheck) passed; `npm run build` passed; synthetic Chromium checks at 360/768/1024 for Daily Sales and Inventory passed the overflow, sticky-style and post-scroll header/cell visibility assertions; all six queue/planning validators and `git diff --check` passed.
- Checks not run: full frontend suite and physical iOS/iPad verification. Current-main Actions run `37513716654` for `7fc26c93` completed successfully after implementation delivery.
- Run log: `.ai/runs/2026-10-06-P-UI-44-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `7fc26c93bf67b8d4c5951510812f14c47bb29ca3`
- Main verification: fresh `git fetch origin main`; `origin/main` equals `7fc26c93bf67b8d4c5951510812f14c47bb29ca3`; implementation SHA is present on `origin/main`.
- Missed: none within P-UI-44 acceptance.
- Follow-up: P-UI-53 is READY after its dependency and path collision review; P-UI-43 remains the primary in-progress pointer.
- Post-close routing: P-UI-53 promoted WAITING -> READY, unclaimed, after the 16-file cascade refreshed at `a3ebc6dbed48fefd159153ae52ef637288d424d2`.
- Residual risk: the 360px Inventory synthetic fixture has one page-level horizontal-overflow observation outside the table region; the table itself stayed clipped to its scroll region and its key cells remained visible. Physical touch-device behavior is not verified. GitHub Actions run `37513716654` for the implementation SHA completed successfully.
- Next: P-UI-43 remains the primary IN_PROGRESS pointer; P-UI-53 is the secondary READY lane and remains unclaimed.
- Prompt defect / scope repair: added the shared ResizeObserver hook to `AnalyticsDataTable.tsx` and made the test polyfill configurable in `setupTests.ts`; both changes were required by the prompt's mocked observer and conditional overflow acceptance.

## P-UI-45 - Global chrome on phones: Serbian, non-blocking request indicator and calmer seasonal carousel

Status: DONE
Ready after: P-UI-40 DONE AND P-UI-48 DONE (`AppLayout.tsx` is shared ownership; P1 shell/safety changes land first)
Priority: P2
Type: frontend/a11y/tests
Feature family: global-chrome-mobile
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/P-UI-45-codex.lock.md`
Owner decision (Ivan, 2026-10-04 21:36): the seasonal image carousel is shown **only on the home page** (`/`). It is removed from every other route, including all analytics, data-entry, šifarnik and admin screens. Reduced-motion behaviour still applies on the home page.
Commit suggestion: `fix(ui): localize request indicator, show seasonal carousel only on home`

Owner claim 2026-10-06: refreshed `origin/main` at `9b33ef73ca7b8284675013870c4ccc6ba382d4a0`; P-UI-40 and P-UI-48 are DONE, no matching P-UI-45 branch/lock/open PR exists, and the owning files have no active path owner. P-UI-45 moved READY -> IN_PROGRESS. Existing implementation already covers Serbian copy, reduced-motion timer guard, home-only carousel mount and coarse-pointer 44px targets; this claim focuses on the remaining mobile no-overlap presentation, CSS reduced-motion scroll behavior and full named-route proof. Local lock: `.ai/task-locks/P-UI-45-codex.lock.md`.

### Completion note

- Date: 2026-10-06
- Status: DONE
- Completion: The mobile request indicator now flows above page content as a compact Serbian progress strip, seasonal carousel reduced-motion scroll uses `auto`, and route/browser proofs cover the named non-home paths and coarse-pointer targets.
- Changed files: `Klijent/clientapp/scripts/responsive_baseline.mjs`; `Klijent/clientapp/src/components/trendshoes/SeasonalImageCarousel.spec.tsx`; `Klijent/clientapp/src/imagecarousel.css`; `Klijent/clientapp/src/pages/HomePage.carousel.spec.tsx`; `Klijent/clientapp/src/skeleton.css`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ACTION_OUTCOME_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ADVANCED_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_LEGACY_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_OPERATIONS_ACCURACY_ADDENDUM.md`; `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`; `MASTER_ROADMAP.md`; `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`; `.ai/runs/2026-10-06-P-UI-45-evidence.md`.
- Checks run: focused Vitest 17/17; `npm run typecheck`; `npm run check:analytics-guardrails` (39 known baseline violations, none removed); production build; 360px responsive fixture; governance validators; `git diff --check` (all passed).
- Checks not run: full frontend suite not run; no task acceptance required it. No GitHub Actions run was discoverable for implementation SHA at inspection.
- Run log: `.ai/runs/2026-10-06-P-UI-45-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `a1a51aa0bd8987165a7f55073fa203c1367dfc14`
- Main verification: fresh `origin/main` at post-close recovery SHA `33e35bb11fcb9908a7ddb89e459095955fc5771a` contains implementation SHA `a1a51aa0bd8987165a7f55073fa203c1367dfc14` and closure commit `33e35bb11fcb9908a7ddb89e459095955fc5771a`.
- Missed: none known
- Follow-up: P-UI-49 is the next primary READY lane; P-UI-51 and P-UI-52 remain independent READY lanes.
- Residual risk: the responsive fixture returns expected 503 responses for non-modeled unrelated endpoints and records a seasonal-image request abort when the browser page closes; it reports zero page errors and the strict route/viewport assertions pass. Full iOS Safari behavior is not proven by Chromium emulation.
- Post-close routing: full 16-file recovery at `33e35bb11fcb9908a7ddb89e459095955fc5771a` is recorded in `.ai/runs/2026-10-06-P-UI-45-evidence.md`; P-UI-49 is selected as the next primary READY lane, unclaimed.
- Prompt defect / scope repair: no scope repair; existing Serbian copy, reduced-motion timer guard, home-only mount and coarse-pointer sizing were already present and were verified. The prompt's stale English-copy evidence was reconciled to current code.

### Problem

Two always-mounted chrome elements degrade every screen on phones:
- The global request indicator is English ("Loading data", "1 request in progress"). It sits as a card over the bottom of the content on 360px screens, covering the text under it while the slow analytics endpoints load (10–18s).
- The seasonal image carousel is mounted under every route, including analytics and data entry. It auto-scrolls every 4s with no reduced-motion check, and its nav buttons are 36px wide.

### Evidence

- Live 360 screenshot of `/analytics/products`: a bottom-right card "Loading data / 1 request in progress" overlaps the trust-header text.
- `Klijent/clientapp/src/components/GlobalRequestSpinner.tsx:26-29`: English label and copy.
- Live 360: `button.carousel-nav-btn.left/right` 36x44 on `/analytics/products`, `/prodaja` and `/dobavljaci`.
- `Klijent/clientapp/src/components/trendshoes/SeasonalImageCarousel.tsx:74-79` (`setInterval(… scrollBy(200) …, 4000)`) and `:88-93` (`scrollBy({ … behavior: "smooth" })`). No `prefers-reduced-motion` check exists anywhere in the component (repo-wide matchMedia reduced-motion appears only in `ShoeTypeSalesStatsPage.tsx:206` and `SupplierSalesStatsPage.tsx:294`).
- `Klijent/clientapp/src/layout/AppLayout.tsx:42-44`: the carousel is rendered for every route. This mount point is out of scope here.

### Scope

- `components/GlobalRequestSpinner.tsx` (+ its CSS), `components/trendshoes/SeasonalImageCarousel.tsx`, `imagecarousel.css`, nearest tests
- `layout/AppLayout.tsx` is limited to removing the global carousel mount (`AppLayout.tsx:6` import and the `<section className="w-full pb-5">` wrapper at `:42-44`). No other shell change: P-UI-40 owns the rest of `AppLayout.tsx`.
- `pages/HomePage.tsx`: mount the carousel there (the only route that renders it, per the owner decision).

### Do

1. Translate the indicator: "Učitavanje podataka" and "{n} zahtev(a) u toku", with correct Serbian plural forms (1 zahtev, 2–4 zahteva, 5+ zahteva). Below 640px, render it as a slim top progress bar or a compact pill that does not cover content (respect the safe-area insets). Keep `aria-live="polite"`.
2. Carousel: do not auto-scroll when `prefers-reduced-motion: reduce`, and use `behavior: "auto"` instead of smooth. Pause on focus/hover/touch. Nav buttons ≥44x44 on coarse pointers.
3. Keep the visible order of images and the modal unchanged.
4. Owner decision 2026-10-04: render `SeasonalImageCarousel` only from `HomePage` (route `/`). Remove the global mount and its wrapper from `AppLayout.tsx`, so no other route renders the carousel, its wrapper spacing, or its image request. Do not use a route-name check inside the shared layout as a substitute.

### Tests

- Vitest: Serbian copy and plural forms; reduced-motion mock stops the interval; nav buttons have a min-size class.
- Vitest (routing, owner decision): rendering the app at `/` shows the carousel (e.g. `carousel-strip` / its test id). Rendering `/analytics`, `/analytics/products`, `/prodaja`, `/unos-robe`, `/nivelacija` and `/dobavljaci` does not render it, and no seasonal-image request is made on those routes (mocked fetch not called).
- Vitest: on `/` with `prefers-reduced-motion: reduce` mocked, the carousel still renders but starts no auto-scroll interval.
- Responsive runner at 360: indicator does not intersect the main content's first 200px when shown; carousel nav ≥44px.
- typecheck, build, guardrails, governance validators, `git diff --check`.

### Acceptance

- No English loading text remains. On phones the loading indicator never covers content. Reduced-motion users get no automatic carousel movement.
- The seasonal carousel appears only on the home page `/`. No analytics, data-entry, šifarnik or admin route renders it or loads its images. On `/` it respects reduced motion.

### Dependencies

- P-UI-40 and P-UI-48 land first because both own `AppLayout.tsx`/shell behavior at higher priority. This is a path-collision sequence, not a business-semantic dependency. Owner decision recorded 2026-10-04 (carousel only on home page); after those owners land, remove only the global carousel mount and preserve their shell/skip-link behavior.

## P-UI-46 - Operational and šifarnik screens: phone-usable lists, actions and paging

Status: IN_PROGRESS
Ready after: P-UI-42 DONE (coarse-pointer target foundation)
Priority: P3
Type: frontend/tests
Feature family: operational-long-tail-responsive
Parallel-safe: no
Owner: Codex (Analytics Frontend / Responsive UI)
Local lock: `.ai/task-locks/P-UI-46-codex.lock.md`
Commit suggestion: `feat(ui): make operational list screens usable on phones`

### Problem

The store/back-office list screens were outside P-UI-30/P-UI-37. They render full raw tables and every row's actions with no paging or search-first layout. Phones get very long pages with 26–34px action buttons. The Logs toolbar overflows at 1024.

### Evidence

- Live page heights at 360: `/dobavljaci` 8940px (237 targets, all small at 768: row actions 71x26/67x26); `/nivelacije` 6174px (9-column table 822px in a 294px scroller); `/dnevnik-promena` 5177px (11 columns, 961px in 294px); `/artikli/lista` 5093px (handled by P-UI-37).
- Live 1024: `/logs` document overflow 81px from `button.button-big.button-danger` / `button-secondary` (right edge 1105/1030).
- Raw tables: `pages/DobavljaciPage.tsx`, `NivelacijePage.tsx`, `DnevnikPromenaPage.tsx`, `SezonaPage.tsx`, `PovracajPage.tsx`, `LogsPage.tsx`, `ConfigurationPage.tsx` (`ConfigurationPage.css` `min-width: 1120px`), `components/transfers/TransferItemsTable.tsx`, `components/WorkersPanel.css` (`min-width: 1100px`).

### Scope

- The listed pages/components only, one page family per commit. No API, paging contract or permission changes. If server paging is missing, use client-side progressive rendering ("Prikaži još") and record the gap.

### Read first

- P-UI-37 Article List delivery (pattern to reuse), P-UI-29 table pilot, the leftover-offender list from P-UI-42's run log

### Do

1. For each list: search/filter first; card or priority-column layout below 640px when the columns are not needed for comparison, otherwise the pilot scroll table with a sticky key column; row actions in a 44px overflow menu on coarse pointers.
2. Progressive rendering (e.g. 50 rows plus "Prikaži još") where more than 100 rows render at once.
3. Fix the Logs toolbar wrap at 1024.

### Tests

- Per page: Vitest for the layout switch and action menu. Responsive runner at 360/768/1024: no document overflow, row actions ≥44px on coarse pointers, `/dobavljaci` first screen shows search plus at least 5 rows.
- typecheck, build, guardrails, governance validators, `git diff --check`.

### Acceptance

- Every listed screen is usable one-handed on a phone without sideways page panning. Data and actions are unchanged.

### Dependencies

- P-UI-42. Insight Studio (67 sub-12px text elements at 360) is excluded: the RQ582 owner decision (2026-10-04) hides it behind the `Eksperimentalno` flag, and responsive work is required only before any re-exposure.

### Owner claim 2026-10-07

- After P-UI-42 DONE, refreshed `origin/main` to `c755b79a26e41834ad98cfeb1ee2e95e2670b0cc` and scanned the 15 active RQ/SQL/P-UI queue/addendum files plus `MASTER_ROADMAP.md`.
- P-UI-42's P-UI-46 dependency is satisfied; no matching local/remote branch, open PR or local lock exists. The run log supplies the requested coarse-pointer residual link inventory.
- P-UI-52 remains READY on navigation/configuration and copy sweep paths. P-UI-46 will edit only its listed operational page families; any P-UI-52 sweep must skip these active owner paths. No owner collision was found.
- Higher-priority RQ/SQL queues remain without READY work; STAB16 remains provider/deployment gated and BCI/QDB/MT/GAI expose no higher-priority runnable candidate in the current roadmap.
- Promoted WAITING -> READY -> IN_PROGRESS and claimed on `codex/p-ui-46-operational-mobile-lists` from refreshed `origin/main` `c755b79a26e41834ad98cfeb1ee2e95e2670b0cc`.

---

## P-UI-47 - Make one theme-token source of truth and repair light-theme status contrast

Status: DONE
Ready after: none (registered 2026-10-04 from `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`)
Priority: P1
Type: frontend/css/tests
Feature family: analytics-theme-token-contract
Parallel-safe: yes (disjoint from READY P-UI-39/P-UI-40/P-UI-41, from P-UI-49 and from all READY RQ paths)
Owner: Codex (Analytics Frontend / Design System)
Owned paths: `Klijent/clientapp/src/context/ThemeContext.tsx`, `Klijent/clientapp/src/styles/themes.css`, `Klijent/clientapp/src/styles/themeTokens.ts`, the token blocks in `Klijent/clientapp/src/tailwind.css` and `Klijent/clientapp/src/styles/analytics-system.css`, new token/contrast tests
Avoid paths: `AnalyticsControlBar.css` (P-UI-39), `HeaderStatus.tsx`/`Sidebar.tsx`/`AppLayout.tsx` (P-UI-40), the `tailwind.css` `@media (max-width: 759px)` floor block (P-UI-42), `AnalyticsTrustHeader*` (RQ569), nivelacija pages/CSS (RQ553, P-UI-35), Supplier/Shoe Type/Color page CSS (P-UI-31/P-UI-36), `AnalyticsEmptyState*`/`AnalyticsErrorState*` (P-UI-49)
Commit suggestion: `fix(ui): single theme token source and accessible status text`

### Problem

Theme colours come from three competing sources: `ThemeContext.tsx` inline variables, `themes.css` `:root`/`[data-theme]` blocks and a dark-valued `:root` block in `tailwind.css`. `themeTokens.ts` references `--c-*` variables that are not defined in `themes.css`. In the light and soft-gray themes, status colours used as text fail WCAG contrast: warning 2.15:1 on white and 1.47:1 on the soft-gray background, success 2.54/1.73, `--error-text` (#fecaca) 1.45. `COMMON_VARS` sets `--surface-elevated-light: #1f2430` and `--surface-elevated-dark: #0f1116` for every theme, so `.card-theme` (used by `InventoryPageShell`) fades to near-black under dark text (1.15:1) in light themes. Page-local palettes alias `--success`/`--warning` as brand/chart colours.

### Evidence

- Audit findings UX-010, UX-011, UX-012, UX-013, UX-037 (`docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md` §5, §10).
- `context/ThemeContext.tsx:12-180` (`COMMON_VARS` lines 68-69); `styles/themes.css:1-140`, `:248`; `tailwind.css:345-389`, `:414+`; `components/inventory/InventoryPageShell.tsx:21`.
- 50 CSS rules use `color: var(--warning|success|error)` as text.
- Live 2026-10-04: green status text in the soft-gray header is barely legible (`/workspace/ux-audit/*_Meka-siva_desktop.png`; screenshots are box-local evidence).

### Scope

- Token definitions and their tests only. Add `--status-{success,warning,error,info}-{fill,border,text}` and `--chart-series-1..8`, `--chart-axis`, `--chart-grid`, `--chart-tooltip-bg/text`, `--chart-positive/negative` for all six themes.
- Make `ThemeContext.tsx` canonical. `themes.css` stays a no-JS fallback with a parity test; the `tailwind.css` token block only maps names.
- Fix `--surface-elevated-light/-dark` and `--error-text` per theme.
- Keep the old variable names as aliases so pages keep working; page migrations belong to P-UI-31/35/36 and later owners.
- Define action-tier tokens and shared classes (primary / secondary / tertiary / link / destructive) in `analytics-system.css` so retry, export, print, navigation links and workflow actions stop sharing one pill style (audit UX-045, live). Page adoption stays with the page owners.
- No analytics semantics, no API change, no page-level CSS rewrite.

### Read first

- `docs/ai/ANALYTICS_DESIGN_SYSTEM.md` §2, §9, §11
- `docs/ai/FRONTEND_UX_STANDARDS.md`
- P-UI-25 completion note (focus/phone foundation)
- `docs/Frontend/ANALYTICS_VISUAL_REGRESSION_PROTOCOL.md`

### Do

1. Inventory every colour variable defined in the three sources and record the effective value per theme in the run log.
2. Make `ThemeContext.tsx` the single definition; remove conflicting colour values from the `tailwind.css` `:root` block, or alias them, without changing dark-theme rendering.
3. Add the status and chart token sets for all six themes, meeting design system §2.2 contrast rules.
4. Point `--error-text`, `--warning-text`, `--accent-text` and `--muted` at accessible per-theme values; fix `--surface-elevated-light/-dark` in light themes.
5. Replace dark neon fallbacks in shared chart constants only where they live in the owned paths; leave page constants to the page owners and list them in the run log.
6. Fix `themeTokens.ts` to reference defined variables.

### Tests

- New Vitest: for each theme, WCAG contrast of text/surface pairs and `--status-*-text` against `-fill` and `--surface-elevated` is at least 4.5:1, and chart axis/grid at least 3:1.
- Parity test: the `themes.css` fallback equals `ThemeContext` values for the shared tokens.
- `npm run check:analytics-guardrails`, `npm run typecheck`, `npm run build`, focused Vitest, `npm run responsive:baseline` for light/soft-gray/dark on Dashboard and an InventoryPageShell page.
- Governance validators; `git diff --check`.

### Acceptance

- Each theme has exactly one authoritative definition per colour token.
- All status text tokens meet 4.5:1 in all six themes; `.card-theme` stays light in light themes.
- Dark themes are visually unchanged in the responsive baseline screenshots (documented diff).
- No business value, status or threshold changed.

### Dependencies

- None blocking. P-UI-38 later turns the contrast and token rules into a ratchet. P-UI-31/35/36 migrate page-local `--dashboard-*` palettes to the new tokens.

### Completion note

- Date: 2026-10-06; Agent: Codex.
- Status: DONE
- Completion: `ThemeContext.tsx` is the runtime token source and `themes.css` is the no-JS parity fallback; duplicate Tailwind color definitions and undefined `--c-*` aliases were removed. Six themes now have contrast-tested status/chart tokens and action tiers. Light-theme insufficient-data/status backgrounds follow the selected theme, while existing dark soft-status surfaces are preserved.
- Changed files: `Klijent/clientapp/src/context/ThemeContext.tsx`; `Klijent/clientapp/src/context/__tests__/ThemeContext.tokens.spec.ts`; `Klijent/clientapp/src/styles/themes.css`; `Klijent/clientapp/src/styles/themeTokens.ts`; `Klijent/clientapp/src/styles/analytics-system.css`; `Klijent/clientapp/src/tailwind.css`; `Klijent/clientapp/scripts/responsive_baseline.mjs`; queue/master/run-log evidence.
- Checks run: focused theme/empty-state tests 11/11; analytics guardrails/typecheck; production build; responsive fixture 60/60 across Dashboard/Inventory, light/soft-gray/dark and ten viewports (zero root overflow/page errors); responsive runner self-test; agent-instruction, prompt-queue and planning-architecture governance validators/self-tests; `git diff --check`; Planning Governance run `37392543043` and Analytics Quality Gates run `37392543144` passed on implementation SHA.
- Checks not run: full frontend test suite; manual production-browser visual comparison.
- Run log: `.ai/runs/2026-10-06-P-UI-47-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `61dbcd7f1354dea30afa7210e4fbbe7ee83be839`
- Main verification: fresh fetch confirmed `origin/main` exactly at `61dbcd7f1354dea30afa7210e4fbbe7ee83be839`.
- Missed: none known.
- Follow-up: P-UI-31 is claimed IN_PROGRESS after the post-close dependency/path scan; P-UI-35/36/43/44 are READY.
- Residual risk: the existing Recharts chunk-size warning remains; responsive evidence uses local fixtures and does not establish deployed runtime behavior.

---

## P-UI-48 - Remove one-click ops toggles from the business header and require confirmation

Status: DONE
Ready after: P-UI-40 is DONE (same `HeaderStatus.tsx`/`AppLayout.tsx` files; registered 2026-10-04 from `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`)
Priority: P1
Type: frontend/tests
Feature family: global-header-ops-safety
Parallel-safe: no (exclusive shared header/layout ownership; P-UI-40 is DONE)
Owner: Codex
Owned paths: `Klijent/clientapp/src/layout/components/HeaderStatus.tsx` (+ spec), `Klijent/clientapp/src/components/WorkerControlFlag.tsx` (+ focused tests), `RedisToggleFlag.tsx` (+ focused tests), `ApiPingFlag.tsx`, new shared `AdminActionConfirmModal.tsx`, `Klijent/clientapp/src/pages/ConfigurationPage.tsx`, `Klijent/clientapp/src/services/workersApi.ts`, `Klijent/clientapp/src/layout/AppLayout.tsx` (+ focused spec; skip link and `main` landmark only)
Avoid paths: backend authorization code (report gaps, do not change here), analytics pages
Commit suggestion: `fix(ui): move ops toggles out of the business header`

### Problem

At widths of 1280px and above, every business screen shows "API ON [Stop]", "Workeri 0/1 [Stop]" and "Redis: isključen [Start]" in the global header, but these controls do **not** have the same semantics:
- worker enable/disable and Redis toggle are backend write operations protected server-side by `AdminAccessControl` / `X-Admin-Key`;
- "API Stop" only pauses the SPA's periodic backend ping in the current browser through `PingControlContext`; it does not stop the API service.

The UX defect is therefore global placement, misleading action naming and missing consequence/confirmation for backend writes — not proof that every anonymous user can shut down the backend.

### Evidence

- Audit UX-002 / DS-3. Live on all 2026-10-04 screenshots.
- `WorkerControlFlag.tsx` calls worker control writes; `RedisToggleFlag.tsx` calls `POST /api/redis/toggle`; `ApiPingFlag.tsx` only calls the local `toggleApiPing` context action.
- `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md`: worker start/stop/control and `POST /api/redis/toggle` use `AdminAccessControl`; production has API-key admin mode rather than a general frontend role pipeline.

### Scope

- The business header keeps passive read-only status (backend online, workers n/m, cache) with a link to the admin/observability surface.
- Worker/Redis backend write actions move to the admin/observability surface and require an accessible confirmation explaining the server-side effect. Preserve the existing `AdminAccessControl`; do not weaken, replace or simulate authorization in the frontend.
- The API-ping switch is **not** an admin backend action. Move it to diagnostics/preferences as a browser-local control and label it explicitly ("Pauziraj proveru API-ja u ovom pregledaču" / equivalent). It does not require destructive-action confirmation, but must never imply that the API service is stopped.
- Action visibility follows actual capability/credential availability. If the current client cannot legitimately supply the required admin credential, show status only and record the existing STAB/SEC/admin-client gap; do not invent client-side roles.

### Read first

- `docs/ai/ANALYTICS_DESIGN_SYSTEM.md` §6.1
- P-UI-06 and P-UI-26 completion notes (header and drawer)
- `HeaderStatus.spec.tsx`

### Do

1. Split each flag into passive status and action/pref control.
2. Render only passive operational status in `HeaderStatus` (desktop and mobile drawer).
3. Host worker/Redis writes on the admin/observability page with the shared `Modal` confirmation and existing admin-credential flow. If no usable credential flow exists on that surface, render them status-only and record the gap; do not create an insecure client-side bypass.
4. Move the API-ping switch to diagnostics/preferences as an explicitly browser-local preference. Test that changing it affects polling state only and never calls a backend "stop API" route.
5. Verify and record the existing server-side auth boundary: worker/Redis writes remain `AdminAccessControl`; no backend auth code changes are authorized here.
6. Fix the existing `act(...)` warnings in `HeaderStatus.spec.tsx` while touching it.
7. Add a visible-on-focus "Preskoči na sadržaj" skip link as the first tab stop, targeting the page `main` landmark; live keyboard testing on 2026-10-04 found the first 15 tab stops on shell, notification and sidebar controls before any page content (audit UX-044).
8. Give the truncated mobile breadcrumb/title an accessible full label and rename the unlabeled "Više" control descriptively (live: "Trendplus pre…", "Odluke o proi…"; audit UX-050).

### Tests

- RTL: the header contains no Stop/Start buttons; the status remains visible and accessible.
- RTL: worker/Redis admin writes require confirmation; cancel does nothing; confirm calls the existing client once only when the legitimate admin capability/credential is available.
- RTL: backend write actions are hidden/disabled when capability or credential is unavailable; the browser-local API-ping preference remains clearly local and does not call a backend write.
- RTL: the first Tab focuses the skip link, and activating it moves focus to `main`.
- `npm run check:analytics-guardrails`, `npm run typecheck`, `npm run build`, focused Vitest; responsive header check at 1280/768/375.
- Governance validators; `git diff --check`.

### Acceptance

- No business screen offers backend operational write controls.
- Worker/Redis writes require confirmation and retain server-side admin authorization.
- The API-ping preference is labelled as local to the current browser and cannot be mistaken for stopping the backend service.
- Any missing legitimate admin-client credential flow is documented as a follow-up, not bypassed.

### Dependencies

- P-UI-40 DONE first (same `HeaderStatus.tsx`/`AppLayout.tsx` ownership). Backend authorization remains STAB/SEC-owned; this prompt consumes the existing boundary and must not weaken it.

### Completion note

- Date: 2026-10-06
- Status: DONE
- Completion: The business header now exposes passive worker/Redis status only and links to admin operations. Worker/Redis writes are available on Configuration only through consequence confirmation and an `X-Admin-Key` input; API polling is clearly browser-local. A keyboard skip link and full breadcrumb/control labels improve shell access.
- Changed files: `Klijent/clientapp/src/components/AdminActionConfirmModal.tsx`; `ApiPingFlag.tsx`; `RedisToggleFlag.tsx`; `WorkerControlFlag.tsx`; `components/__tests__/OperationalFlags.spec.tsx`; `layout/AppLayout.tsx`/`.spec.tsx`; `layout/components/HeaderStatus.tsx` and `__tests__/HeaderStatus.spec.tsx`; `pages/ConfigurationPage.tsx`; `services/workersApi.ts` and `services/__tests__/workersApi.spec.ts`; UI queue, master router, RQ supplemental pointers, P-UI-40 evidence and P-UI-48 run log.
- Checks run: 17 focused tests; analytics guardrails and typecheck; production build; responsive app-shell 20/20 (two themes × ten widths, zero overflow/page errors); all governance validators/self-tests; `git diff --check`.
- Checks not run: full local analytics suite; live production admin-key acceptance.
- Run log: `.ai/runs/2026-10-06-P-UI-48-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `575af2ab3f48eb9668477b9b95b90928b4a61aec`
- Main verification: pushed to origin/main and fresh fetch confirmed `HEAD == origin/main == 575af2ab3f48eb9668477b9b95b90928b4a61aec`.
- Missed: none known.
- Follow-up: sync the mandatory post-close routing recovery.
- Residual risk: production admin-key availability is not established locally; server-side `AdminAccessControl` remains unchanged and authoritative. GitHub Analytics Quality Gates run `37386823059` was in progress at close preparation.
- Post-close routing: recovery scan from the terminal closure SHA will be recorded in the run log before final evidence synchronization.

### Post-delivery regression follow-up

- Date: 2026-10-06
- Current status: DONE after local regression proof and main delivery
- Trigger: GitHub Analytics Quality Gates run `37386823059` on implementation SHA `575af2ab3f48eb9668477b9b95b90928b4a61aec` failed `ConfigurationPage.spec.tsx` because the test counted both the worker refresh and the separate Configuration refresh by a shared text regex. The worker-specific accessible control remains unique.
- Follow-up: assertion now names `Osveži status workera`; focused ConfigurationPage spec passes 5/5 locally. The same CI run's `PreNivelacijaPriorityPage` period-fixture failure (`unknown|unknown`) passes in a local isolated rerun (1/1; 55 skipped) and is recorded as a remote-run discrepancy.
- Run log: `.ai/runs/2026-10-06-P-UI-48-evidence.md`
- Evidence state: pending

### Follow-up completion note

- Date: 2026-10-06
- Status: DONE
- Completion: changed the ConfigurationPage regression assertion from a broad refresh-text count to the unique worker refresh accessible name, preserving the separate Configuration refresh control.
- Changed files: `Klijent/clientapp/src/pages/__tests__/ConfigurationPage.spec.tsx`; this queue, `MASTER_ROADMAP.md`, five RQ supplemental pointers and `.ai/runs/2026-10-06-P-UI-48-evidence.md`.
- Checks run: focused ConfigurationPage tests 5/5; isolated prior CI PreNivelacija failure 1/1; queue, agent-instruction and planning validators; `git diff --check`.
- Checks not run: full analytics suite; Analytics Quality Gates `37387704537` remains in progress on the correction SHA.
- Run log: `.ai/runs/2026-10-06-P-UI-48-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `1460987fb726cf7f70687050c3cb518b05d5c8fd`
- Main verification: pushed to origin/main; fresh fetch confirmed `HEAD == origin/main == 1460987fb726cf7f70687050c3cb518b05d5c8fd`.
- Post-close routing: refreshed `origin/main` at `858ade29e8c5a95315ce94c3d870ba71f4746d06`; full 16-file cascade promoted and claimed P-UI-41. See the run log.
- Residual: correction-SHA Analytics Quality Gates run `37387704537` was in progress at the post-close scan; Planning Governance run `37387704648` is green. The earlier remote-only PreNivelacija test discrepancy passed locally in isolation.
- Prompt defect / scope repair: added a shared confirmation modal and listed its explicit frontend/test/service files in Owned paths; reused the existing `X-Admin-Key` contract without changing server authorization.

---

## P-UI-49 - Map backend reason codes into one shared empty/error state taxonomy

Status: DONE
Ready after: none (registered 2026-10-04 from `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`)
Priority: P2
Type: frontend/tests
Feature family: analytics-state-taxonomy
Parallel-safe: yes (disjoint from READY P-UI-39/P-UI-40/P-UI-41/P-UI-47 and READY RQ paths)
Owner: unassigned (Analytics Frontend / Design System)
Owned paths: `Klijent/clientapp/src/components/analytics/AnalyticsEmptyState.tsx/.css`, `AnalyticsErrorState.tsx/.css`, new `Klijent/clientapp/src/utils/analyticsStateTaxonomy.ts`, their tests
Avoid paths: `AnalyticsTrustHeader*` (RQ569), page files (adopt per page through page owners), backend
Commit suggestion: `feat(ui): shared analytics state taxonomy from backend reason codes`

### Problem

`AnalyticsEmptyState` knows only `no_data`, `insufficient_data` and `filtered_out`; `AnalyticsErrorState` has no kind. The backend already returns reasons such as source-horizon limits, `MISSING_OBJECT`/`contract_missing`, suppressed candidates and readiness blocks. The UI renders these as a generic "Nema podataka", so users read "no sales" when the real cause is stale data or an unready source.

### Evidence

- Audit UX-016, §6. `components/analytics/AnalyticsEmptyState.tsx:21-41`; `AnalyticsErrorState.tsx`.
- Live-API 2026-10-04: beyond-horizon empty periods, Supplier report `MISSING_OBJECT`, Pre/Post `contract_missing`, Color 100% unknown (`docs/qa/ANALYTICS_REAUDIT_2026-10-04.md`).

### Scope

- One mapping table from backend code to tone, title, message and action, following design system §7. Backward-compatible props.
- Map only codes the backend emits today (inventory them in the run log). Reserve names for the RQ570 (`beyond_source_horizon`) and RQ575 (`source_dimension_not_populated`) codes; render them only when the backend sends them.
- An unknown code renders `unknown_code` with the raw code inside a details disclosure.
- Add `schema_mismatch` for `AnalyticsResponseValidationError` (today rendered as "<context> response nije u očekivanom formatu", live on Supplier overview and Inventory action proposals on 2026-10-04, audit UX-047). Serbian copy, retry, correlation ID with a copy button. The root cause stays with RQ565.
- Add `backend_unreachable` for network failures (live raw "Failed to fetch" on Pilot Readiness, Decision Board, Decision Pulse and Actions on 2026-10-04): Serbian copy and retry; never show the browser error text as the main message.
- Add `loading_slow`: after a fixed delay, the loading state explains a possible backend cold start and offers retry/cancel instead of spinning forever (live: Supplier report stayed on "Učitavam trajni izveštaj…", audit UX-049). Reuse `BackendWakeupNotice` semantics; no new backend status is inferred.
- No page adoption beyond one pilot page whose owner is not active (record which one); other pages adopt through their owners.

### Read first

- `docs/ai/ANALYTICS_DESIGN_SYSTEM.md` §7, §8
- `utils/analyticsErrorMessages.ts`, `AnalyticsResponseMeta` types
- P-UI-20/P-UI-21/P-UI-22 completion notes

### Do

1. Inventory backend reason, warning and error codes reachable by analytics clients.
2. Implement `analyticsStateTaxonomy.ts` with an exhaustive typed map and Serbian copy.
3. Extend both components to accept `code` and `meta` and resolve through the taxonomy; keep the existing variants as aliases.
4. Keep error states free of KPI zeros and always offer retry for retryable errors (with correlation ID when present).

### Tests

- Unit: every mapped code resolves; an unknown code resolves to `unknown_code`; no code is derived from row counts when a backend reason exists.
- RTL: retry is present for retryable errors; the correlation ID is shown; the details disclosure is keyboard accessible.
- `npm run check:analytics-guardrails`, `npm run typecheck`, focused Vitest.
- Governance validators; `git diff --check`.

### Acceptance

- The two shared components can express every state in design system §7 without page-local markup.
- No state is inferred by the frontend when a backend code exists.

### Dependencies

- None blocking. RQ570/RQ575/RQ583 add or emit codes later; P-UI-50 and the page owners consume the taxonomy.

### Addendum 2026-10-04 (next-wave audit; evidence only, no scope change)

- The four live "Failed to fetch" observations used as `backend_unreachable` evidence coincide with a production redeploy at 21:38:36 CEST (ready at 21:39:00); at 22:50 the same endpoints returned 200. The taxonomy row stays valid because restarts and cold starts recur; the copy should say the server is restarting or temporarily unreachable and offer retry, not imply a data error. Source: `docs/qa/ANALYTICS_RELIABILITY_VALUE_NEXT_WAVE_AUDIT_2026-10-04.md` F6.


---

### Completion note

- Date: 2026-10-07
- Status: DONE
- Completion: Added the shared typed analytics state taxonomy for backend empty, warning, loading and error reasons; extended the shared empty/error components with backward-compatible code/meta props, safe unknown-code details, retry/correlation actions and delayed-loading recovery; adopted it on the Supplier Footwear pilot page.
- Changed files: See `.ai/runs/2026-10-07-P-UI-49-evidence.md` (16 clientapp source/test paths plus guardrail and owner-evidence files).
- Contract/runtime behavior changed: Centralized Serbian state copy and reason-code handling. Unknown codes remain neutral and disclosed; row counts do not infer backend state; only explicit backend horizon/dimension codes activate those states. Cancelling slow-load recovery cancels the pilot page request.
- Checks run: Focused tests 192/192; analytics guardrails (39 existing baseline findings, zero new); typecheck/build; queue, instruction and planning governance validators; diff checks. Exact commands and the remote run classification are in the run log.
- Checks not run: Full frontend suite locally; live/browser API smoke. Remote Analytics Quality Gates run `37592911311` on correction SHA `1814be27d6d94375ce32f74f816371f3a21b590c` passed 150/153 specs and remains red only on three duplicate trust-header assertions in unchanged P-UI-43-owned surfaces; Planning Governance run `37593471184` passed on final synchronized evidence SHA `51ba47f426832ee4d262b6651ade5dd6508cd121`.
- Run log: `.ai/runs/2026-10-07-P-UI-49-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `1814be27d6d94375ce32f74f816371f3a21b590c`
- Main verification: refreshed `origin/main` at `194df3081cd4013b4320e77ffe34329e01547ff6`; it contains the implementation and compatibility correction.
- Missed: No additional page families adopted the taxonomy; adoption remains with their owning prompts.
- Follow-up: P-UI-50 consumes the taxonomy after its page path is released; P-UI-51 is the current next READY lane.
- Residual risk: Analytics Quality Gates run `37592911311` remains red on three unrelated trust-header duplicate-text assertions; Recharts chunk warning (>500 kB) remains.
- Next: P-UI-51 READY, unclaimed; P-UI-50 BLOCKED on the existing Product Decision page edit.
- Prompt defect / scope repair: Retargeted three shifted guardrail baseline line references without adding exemptions. Preserved legacy prose `emptyReason` values while reserving taxonomy disclosure for machine reason codes; updated shared-consumer test expectations.
- Post-close routing: Full 16-file cascade at post-close base `origin/main` `194df3081cd4013b4320e77ffe34329e01547ff6`; P-UI-51 remains the primary unclaimed READY candidate, P-UI-50 is blocked by an active uncommitted edit to its owned page path. Details: `.ai/runs/2026-10-07-P-UI-49-evidence.md`.

## P-UI-50 - Product Decision information hierarchy: blocked KPIs, row disclosure and copy

Status: BLOCKED
Ready after: RQ573 and RQ574 are DONE (same page/contract) and P-UI-49 is DONE
Priority: P2
Type: frontend/tests
Feature family: product-decision-hierarchy
Parallel-safe: no (`ProductDecisionCenterPage.tsx` is shared with RQ573/RQ574)
Owner: unassigned (Analytics Frontend)
Owned paths: `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx/.css` and its specs
Avoid paths: backend Product Decision endpoints and reasoning helpers (RQ573/RQ574)
Commit suggestion: `feat(ui): honest Product Decision hierarchy when recommendations are blocked`

### Problem

When the backend blocks recommendations, the KPI row still shows "Za dopunu 0 · Za pojačanje 0 · Za sniženje 0 · Ne naručivati 0", which reads as "nothing to do". Estimates show "N/A". The table row expands via `<tr onClick>` and a "Zašto?" button without `aria-expanded`. "N/A" appears 18 times; the export button is disabled without a reason; the population note does not say how many rows are shown out of the total.

### Evidence

- Audit UX-007, UX-008 (UX part), UX-023, UX-006; live 2026-10-04 Product Decision screenshots in all themes.
- `pages/ProductDecisionCenterPage.tsx:1509-1544`, `:1843`, `:1898`.

### Scope

- When backend readiness or `recommendationAllowed` blocks an action family, its KPI shows "—" plus the backend reason (via the P-UI-49 taxonomy); real zeros stay zeros only when the backend marks the family as allowed.
- Use backend totals for "Prikazano N od M" (added by RQ573); do not count in the browser.
- Add `aria-expanded`/`aria-controls` to "Zašto?"; keep the row click as a secondary path.
- Replace "N/A" with "Nije dostupno"; explain the disabled export.
- Visually distinguish "Zašto?" (row reasoning) from "Kako je izračunato?" (metric methodology), and make "Dodaj u proveru" read as a workflow action with the P-UI-47 action tiers (live). Reduce the 11-card KPI strip to the golden-screen 4–5 plus a disclosure. Show methodology enums such as `REPLENISH` as labels.
- Apply the layout from audit §11.3.

### Read first

- RQ573, RQ574, RQ483 completion notes
- `docs/ai/ANALYTICS_DESIGN_SYSTEM.md` §1, §3, §6, §7, §8
- P-UI-32 completion note

### Do

1. Bind each KPI to its backend allowed/blocked field; render the three value states.
2. Add the population line from backend totals.
3. Fix the disclosure ARIA and copy.
4. Rearrange to the golden-screen order without changing data fetching.

### Tests

- RTL: blocked family shows "—" with reason, never "0"; allowed family with backend zero shows "0"; the population line uses backend totals; `aria-expanded` toggles.
- `npm run check:analytics-guardrails`, `npm run typecheck`, focused Vitest, responsive baseline for `/analytics/products`.
- Governance validators; `git diff --check`.

### Acceptance

- No KPI can imply "nothing to do" while the backend blocks the recommendation.
- Keyboard users can open every "Zašto?" panel with a correct ARIA state.

### Dependencies

- RQ573, RQ574 (same page/contract); P-UI-49 (taxonomy).

### Completion note

- Date: 2026-10-07
- Status: BLOCKED
- Completion: Post-close recovery verified RQ573, RQ574 and P-UI-49 DONE. P-UI-50 was not claimed or implemented because `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx` has an uncommitted change in the shared primary checkout; the edit's owner/intent is not recorded, so the owned path is not safe to take.
- Changed files: Queue routing metadata only; no P-UI-50 product files changed.
- Contract/runtime behavior changed: none.
- Checks run: fresh main/dependency review; no matching P-UI-50/P-UI-51 branch, lock or open PR; confirmed the existing working-tree change directly touches P-UI-50's owned page path.
- Checks not run: P-UI-50 implementation tests; the prompt remains unclaimed.
- Run log: `.ai/runs/2026-10-07-P-UI-49-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `194df3081cd4013b4320e77ffe34329e01547ff6`
- Main verification: passed - refreshed `origin/main` at `194df3081cd4013b4320e77ffe34329e01547ff6` contains the BLOCKED routing record and P-UI-49 delivery.
- Missed: P-UI-50 remains unimplemented.
- Follow-up: Re-evaluate the page path after the existing checkout edit is delivered or cleared; then promote to READY if collision-free.
- Residual risk: Owner/intent for the existing uncommitted Product Decision page edit is unknown.
- Next: P-UI-51 remains the primary READY, unclaimed prompt.
- Prompt defect / scope repair: none.

---

## P-UI-51 - Decision-surface controls: Executive Board period/scope/URL state and date clarity

Status: DONE
Claimed: 2026-10-07 by Codex after fresh origin/main, dependency, lock, branch, PR and path collision checks.
Ready after: RQ570 and P-UI-39 are DONE (horizon-anchored default periods; P-UI-39 owns `AnalyticsControlBar.css` overflow); must not run while RQ319/RQ320 are IN_PROGRESS (shared `AnalyticsControlBar`)
Priority: P2
Type: frontend/tests
Feature family: decision-surface-controls
Parallel-safe: no (shared control bar)
Owner: Codex (Analytics Frontend)
Owned paths: `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx` (controls/URL only), `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx` (filter URL/history only), `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.tsx/.css`, `Klijent/clientapp/src/utils/analyticsFormatters.ts`, and focused specs for those owners
Avoid paths: Decision Board backend and scoring; Decision Pulse page (RQ481/RQ482)
Commit suggestion: `feat(ui): board scope controls and unambiguous dates`

### Problem

The Executive Board has no period, store or data-scope control and no URL state; its period is fixed by the backend (180 days from today), so it cannot be aligned with other screens or shared. Across the control bar, native date inputs follow the browser locale (live: `09/05/2026` next to "5. 9. 2026." in the trust header), and the preset label is truncated ("Poslednjih 3…").

### Evidence

- Audit UX-017, UX-019, UX-020; live Supplier screenshots 2026-10-04; `ExecutiveDecisionBoardPage.tsx` has no `<select>`/`<input>` controls; live-API Board period 180 days.

### Scope

- Board: show and allow changing the period/store/dataScope through parameters the backend already accepts (verify; if a parameter is not accepted, display only and record a backend follow-up). Persist them in the URL.
- Control bar: a `dd.MM.yyyy` echo next to native date inputs; verify preset labels no longer truncate after P-UI-39 and fix only the residual.
- URL/history rule (design system §6.3): `/analytics` (Dashboard) must encode custom dates in the URL like the other screens (live: it does not). Applied filter changes push one history entry so browser Back restores the previous applied state; typing in search does not (live: Back did not restore the Product filter, audit UX-043).
- Use RQ570 defaults as delivered; never compute a horizon in the browser.

### Read first

- RQ570, RQ481 and RQ319/RQ320 prompts
- `docs/ai/ANALYTICS_DESIGN_SYSTEM.md` §6.3

### Do

1. Inventory Board endpoint parameters; wire the supported ones to controls and the URL.
2. Add the date echo and fix preset label width in `AnalyticsControlBar`.
3. Keep Apply semantics consistent with the design system §6.3 rule.

### Tests

- RTL: Board URL round-trips period/store/scope; unsupported parameters are display-only.
- RTL: Dashboard custom dates round-trip in URL; Apply adds one history entry and Back restores applied filters; in-table search does not change history.
- RTL: the date echo renders `dd.MM.yyyy` regardless of browser locale; preset labels remain readable at 1280/768/375.
- `npm run check:analytics-guardrails`, `npm run typecheck`, focused Vitest; governance validators; `git diff --check`.

### Acceptance

- A Board view can be shared by URL and matches the period shown on other screens.
- No ambiguous month/day dates on analytics controls.

### Dependencies

- RQ570 (default period). Coordinate with RQ319/RQ320 (shared control bar).

### Promotion note 2026-10-06

- Promoted WAITING -> READY after RQ570 and P-UI-39 were verified DONE on main. RQ319/RQ320 remain WAITING, so their explicit IN_PROGRESS serialization gate is clear. P-UI-40 owns only shell layout paths and does not collide with this prompt's shared control-bar/page paths.

### Scope clarification 2026-10-07

- The URL/history acceptance explicitly names `/analytics` Dashboard custom dates, but its page and focused test were missing from the original owned-path list. Add only `AnalyticsDashboard.tsx` filter URL/history behavior and its focused spec; preserve backend-owned horizon defaults and existing filter semantics. Date echo formatting is shared through `analyticsFormatters.ts`; do not expand into broader dashboard redesign or alter business queries.

Owner claim 2026-10-07: refreshed `origin/main` at `a330671f09e557c6626895e3e15bad452695d683`; verified RQ570/P-UI-39 DONE, RQ319/RQ320 WAITING, and no matching P-UI-51 lock, branch or open PR. Higher-priority BCI/RQ/SQL queues have no READY task; STAB16 remains provider/deployment blocked. Claimed READY -> IN_PROGRESS on `codex/p-ui-51-decision-surface-controls` from the refreshed main SHA. Local lock: `.ai/task-locks/P-UI-51-codex.lock.md`.

### Completion note

- Date: 2026-10-07
- Status: DONE
- Completion: Board period/store/dataScope controls round-trip through URL state; Dashboard applied filters use one history entry and Back restores them; date echoes are locale-independent `dd.MM.yyyy`.
- Changed files: `Klijent/clientapp/scripts/known-guardrail-baseline.json`; `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.css`; `Klijent/clientapp/src/components/analytics/AnalyticsControlBar.tsx`; `Klijent/clientapp/src/components/analytics/__tests__/AnalyticsControlBar.spec.tsx`; `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`; `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.spec.tsx`; `Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx`; `Klijent/clientapp/src/pages/__tests__/AnalyticsDashboard.controlBar.spec.tsx`; `Klijent/clientapp/src/utils/analyticsFormatters.ts`; `Klijent/clientapp/src/utils/__tests__/analyticsformatters.spec.ts`; `MASTER_ROADMAP.md`; `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`.
- Contract/runtime behavior changed: only supported backend Board filters are applied; backend default period remains authoritative; Dashboard query semantics are unchanged and browser history tracks applied filters only.
- Checks run: focused Vitest 4 files / 35 tests passed; `npm run typecheck`; `npm run check:analytics-guardrails` (39 known findings, 0 new); `npm run build`; agent-instruction, prompt-queue and planning-architecture self-tests/checks; `git diff --check` all passed.
- Checks not run: full Vitest suite and browser viewport/visual verification; no browser harness was used. Relevant current-main Actions run on the delivered SHA was not visible when queried; latest red Analytics Quality Gates run `35354567260` targets older SHA `b57a6383` and predates this change.
- Run log: `.ai/runs/2026-10-07-P-UI-51-evidence.md`.
- Evidence state: synchronized.
- Delivery mode: direct-main.
- Main commit SHA: `6de421712d43f52aa21e4751e3c1c03271cc2b31`.
- Main verification: fresh `origin/main` at `6de421712d43f52aa21e4751e3c1c03271cc2b31`; `git merge-base --is-ancestor 6de421712d43f52aa21e4751e3c1c03271 origin/main` passed.
- Missed: no in-table search control exists on the Dashboard; its filter changes are history-tracked without changing search behavior.
- Follow-up: post-close recovery promotes P-UI-42 after verifying all explicit dependencies.
- Residual risk: full browser visual verification was not run; existing Recharts chunk-size advisory remains.
- Post-close routing: recovery details and successor claim are recorded in `.ai/runs/2026-10-07-P-UI-51-evidence.md`.
- Prompt defect / scope repair: added `AnalyticsDashboard.tsx` and its focused test to owned paths because the URL/history acceptance explicitly covered `/analytics`; clarified no Dashboard in-table search surface exists.

---

## P-UI-52 - Analytics navigation IA and user-facing glossary sweep

Progress note 2026-10-05: presentation/UX audits already replaced many user-facing `N/A` strings with `Nije dostupno` and documented canonical terms in `docs/ai/ANALYTICS_BUSINESS_GLOSSARY.md`. **P-UI-52 stays READY** — nav/group label reconciliation, badge cleanup and canonical route `to` targets in `navConfig.ts` remain executable scope.

Status: READY
Ready after: RQ553 is DONE and RQ582 has released its exclusive navigation entry/visibility work; RQ589 is backend-only and does not own `navConfig.ts`
Priority: P2
Type: frontend/copy/tests
Feature family: analytics-nav-ia-copy
Parallel-safe: no (`navConfig.ts`)
Owner: unassigned (Frontend Shell / Analytics UX)
Owned paths: `Klijent/clientapp/src/layout/navConfig.ts` (+ nav specs), `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts` (labels only), the "N/A" fallbacks in `AnalyticsActionsPage.tsx` and `SupplierFootwearAnalyticsPage.tsx`, plus copy-only edits in otherwise unowned frontend files found by the bounded user-facing-text sweep after a fresh collision check
Avoid paths: `GlobalRequestSpinner.tsx` and the carousel (P-UI-45), header/breadcrumb (P-UI-40/P-UI-48), routes and redirects themselves (RQ507 legacy contract), Insight Studio entry (RQ582), RQ555 Actions logic
Commit suggestion: `fix(ui): consistent analytics navigation labels and glossary`

### Problem

Five sidebar groups share `label: "Analitika"`. Internal badges (P0, Ops, DQ, Archive, Ready, Board, Hub, Task, Lab) mostly use the warning tone and do not describe a state. "Pilot spremnost" carries a static "Ready" badge while readiness is 67/blocked. Nav labels differ from route definitions and page titles ("Prodaja po smeni i dobavljačima" vs "Dnevna prodaja", "Prioriteti nivelacije" vs "Prioriteti Pre-Nivelacije", "Pre/Posle" vs "Pre/Post"). The nav links to the redirect alias `/analytics/supplier-decision-hub`. "N/A" remains in Actions (14) and Supplier Footwear (11).

### Evidence

- Audit UX-021, UX-022, UX-006; live sidebar on all 2026-10-04 screenshots; `layout/navConfig.ts:108-220`; `routes/analyticsRouteDefinitions.ts`.

### Scope

- One label per destination, shared by nav, route definition and page `h1` (owner-approved naming recorded in the run log).
- Remove static status-like badges; keep only descriptive, non-status markers where the owner asks for them.
- Point nav items to canonical routes; keep redirects working.
- Replace "N/A" in the owned files with "Nije dostupno".
- Restore Serbian diacritics in shell/global strings seen live ("Prosiri", "Pokusaj ponovo", "Osvezi", "Greska pri ucitavanju", "jos nije dostupan", "Pojacaj"; audit UX-048). Extend this only to a bounded user-facing sweep for obvious display strings such as `nacin`, `najvise`, `velicina`, `potraznja`, `kriticno` and equivalent ASCII-only Serbian copy. Do **not** change API/error codes, enum values, route segments, JSON keys, telemetry identifiers, DB values or fixture contract values. If the string lives in a page owned by an active prompt, hand it to that owner and list it in the run log.

### Read first

- RQ507 completion note (legacy alias contract), RQ553, RQ582
- `docs/ai/ANALYTICS_DESIGN_SYSTEM.md` §6.1, §8

### Do

1. Build a label table (nav / route definition / page title) and reconcile it.
2. Update `navConfig.ts` items, group labels and badges.
3. Replace redirect-alias links with canonical routes.
4. Sweep "N/A" in the owned files.
5. Run the bounded user-facing Serbian copy/diacritics sweep across disjoint frontend files; update only obvious display copy and preserve all machine-readable identifiers/contracts.
6. Record every skipped active-owner path as a hand-off instead of editing through another task.

### Tests

- Nav spec: every nav `to` is a canonical (non-redirect) route; labels equal route-definition labels.
- Existing redirect/smoke route tests still pass.
- `npm run check:encoding`, `npm run check:analytics-guardrails`, `npm run typecheck`, focused Vitest; governance validators; `git diff --check`.

### Acceptance

- No nav badge implies a status the backend does not report.
- One name per screen across nav, route definitions and page titles.
- No known ASCII-only Serbian typo/diacritic defect remains in the audited owned/disjoint user-facing copy; machine-readable contracts are unchanged.

### Dependencies

- RQ553 (same file) and RQ582 (exclusive Insight Studio navigation entry) are DONE. RQ589 owns only Advanced/V2 backend certification. P-UI remains supplemental and must not displace READY/IN_PROGRESS RQ work.


## P-UI-53 - Analytics chart accessibility contract and screen-reader alternatives

Status: DONE
Ready after: P-UI-47 DONE AND P-UI-31/P-UI-35/P-UI-36 DONE or explicitly deferred AND P-UI-44 DONE or explicitly deferred; an owner may only split a demonstrably disjoint chart-only slice earlier after a fresh path/owner/lock/PR collision check
Priority: P2
Type: frontend/a11y/tests
Feature family: analytics-chart-accessibility
Parallel-safe: no while a page-family owner is editing the same chart files
Owner: Codex (Analytics Frontend / Accessibility)
Local lock: `.ai/task-locks/P-UI-53-codex.lock.md` (removed after DONE)
Historical routing note 2026-10-06 (before P-UI-44 completion): P-UI-47/P-UI-31/P-UI-35/P-UI-36 were DONE, but P-UI-53 remained WAITING while READY P-UI-44 owned overlapping Daily Sales/Inventory page/table paths. The serialization dependency is now complete.
Post-close promotion 2026-10-06: `P-UI-44` is DONE on `origin/main` `7fc26c93bf67b8d4c5951510812f14c47bb29ca3`. The fresh 16-file cascade verified P-UI-47/P-UI-31/P-UI-35/P-UI-36/P-UI-44 dependencies DONE. Reconciled the stale P-UI-35 detail status from IN_PROGRESS to DONE using synchronized completion evidence and current `origin/main` `e7f9bc47325348d5f7ad202e9850926c91df95a5`. No P-UI-53 lock, matching local/remote branch or open PR exists. Active owners are path-disjoint: P-UI-43 owns `AnalyticsTrustHeader`; P-UI-45 owns global chrome; P-UI-49 owns empty/error taxonomy; P-UI-51 owns Executive Board controls/shared control bar; P-UI-52's copy sweep must hand off active page-family paths. Promoted WAITING -> READY, unclaimed.
Owner claim 2026-10-06: refreshed `origin/main` at `2a5f4f072db2ea020dd5d29248ee82f219012824`; verified P-UI-44 and all named responsive/theme dependencies DONE. No P-UI-53 branch or open PR exists. The active P-UI-43 lock owns only `AnalyticsTrustHeader` and focused tests; P-UI-53 owns chart wrappers/page chart regions. P-UI-45 global chrome, P-UI-49 empty/error taxonomy, P-UI-51 controls and P-UI-52 navigation/copy have separate ownership. P-UI-53 moved READY -> IN_PROGRESS on `codex/p-ui-53-chart-accessibility`. Local lock: `.ai/task-locks/P-UI-53-codex.lock.md`.

### Completion note

- Date: 2026-10-06
- Status: DONE
- Completion: Added a shared name/summary/table alternative contract and keyboard navigation to all 25 in-scope production analytics charts; added static coverage/self-test and updated the downstream P-UI-38 invariant.
- Changed files: See `.ai/runs/2026-10-06-P-UI-53-evidence.md` (27 implementation, test and owner-evidence paths).
- Contract/runtime behavior changed: Accessibility semantics only; existing business values and recommendation logic are unchanged.
- Checks run: focused suites 139/139; chart coverage (25) and negative self-test; typecheck; encoding/analytics guardrails; build; governance validators; diff check. Exact commands are in the run log.
- Checks not run: full frontend suite and visual screen-reader session; see run log.
- Run log: `.ai/runs/2026-10-06-P-UI-53-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `c6c19d83e9de19c00af623df426e1b1cecaf52d7`
- Main verification: refreshed `origin/main` at `83ead40f15ab7923c1e2f9aad9ff3a58ba78337f` contains implementation commit `c6c19d83e9de19c00af623df426e1b1cecaf52d7`.
- Missed: eight Insight Studio sparklines remain quarantined under their upstream prompt and have a documented label/value static check.
- Follow-up: P-UI-38 consumes the chart accessibility checker after remaining UI migrations are complete.
- Residual risk: existing Vite Recharts chunk-size warning (>500 kB); no visual screen-reader session was available.
- Next: P-UI-43 remains the primary IN_PROGRESS pointer; P-UI-45/P-UI-49/P-UI-51/P-UI-52 remain existing independent READY lanes.
- Prompt defect / scope repair: clarified P-UI-38 acceptance to consume the stable chart accessibility invariant generated by this prompt; guardrail baseline line offsets were refreshed after import insertions without adding exemptions.
- Post-close routing: full 16-file cascade at recovery base `origin/main` `83ead40f15ab7923c1e2f9aad9ff3a58ba78337f` searched P-UI-53 and re-evaluated non-terminal UI prompts after evidence synchronization. P-UI-38 now has P-UI-53 DONE but still waits for remaining migrations; no dependent became newly runnable and none was promoted. Existing current pointer/READY lanes remain unchanged. Details: `.ai/runs/2026-10-06-P-UI-53-evidence.md`.
- Remote CI: Analytics Quality Gates run `37519116615` is in progress on delivered SHA `c6c19d83e9de19c00af623df426e1b1cecaf52d7`.

Commit suggestion: `feat(ui): add accessible analytics chart contract`

### Problem

The design system already says every chart needs a textual summary or table alternative, but the repository has no shared chart accessibility contract. Repo-wide inspection finds no Recharts `accessibilityLayer` usage and no common `aria-describedby`/summary wrapper. The Data Quality custom SVG is an isolated positive example with `role="img"` and `aria-label`; Dashboard, Daily, Supplier, Shoe Type, Color, Inventory, Pre-Nivelacija, Pre/Post and Analytics Details charts rely on visual rendering without one consistent screen-reader path.

### Evidence

- UX-051 in `docs/ai/ANALYTICS_UX_UI_AUDIT_2026-10-04.md`.
- Recharts chart call sites across `AnalyticsDashboardCharts.tsx`, `DailySalesStatsPage.tsx`, `SupplierSalesStatsPage.tsx`, `ShoeTypeSalesStatsPage.tsx`, `ColorSalesStatsPage.tsx`, Inventory panels, Pre-Nivelacija/Pre-Post and `AnalyticsDetails.tsx`.
- Positive control: `DataQualityPage.tsx` custom SVG exposes `role="img"` + `aria-label`.

### Scope

- Add one shared chart accessibility helper/frame under `components/analytics` plus focused tests.
- Migrate the named chart families after their responsive/theme page owners and the overlapping P-UI-44 Daily/Inventory table owner are finished or explicitly deferred.
- No chart data, aggregation, ranking, recommendation, threshold or backend contract changes.
- Insight Studio is excluded while RQ582 keeps it experimental/hidden; accessibility becomes a re-exposure prerequisite.

### Read first

- `docs/ai/ANALYTICS_DESIGN_SYSTEM.md` §4 and §9.
- P-UI-47 chart/status tokens.
- P-UI-31/P-UI-35/P-UI-36 completion evidence.
- P-UI-44 completion/deferral evidence before touching Daily Sales or Inventory page/table paths.
- Existing chart/table parity contracts; a textual alternative must use the same already-rendered dataset.

### Do

1. Define a shared chart frame/helper with an accessible name and optional `aria-describedby` summary id. Use Recharts' supported accessibility mechanism where it materially helps, but do not depend on SVG internals that are unstable across versions.
2. Every meaningful chart gets a short Serbian text summary generated only from the same chart-series projection already displayed. Do not calculate a new KPI, recommendation or business status for the summary.
3. When a semantically equivalent data table already exists, expose a clear relationship/link ("Prikaži tabelu podataka") instead of duplicating every point in hidden text.
4. Interactive chart controls/legends/tooltips must be keyboard reachable when they carry information not otherwise available. Purely decorative chart elements are hidden from assistive tech.
5. Preserve color-independent meaning: legend/series labels and status text remain understandable without color.
6. Document exceptions for decorative mini-sparklines and prove they have an adjacent textual value.

### Tests

- Shared helper RTL/Vitest: accessible name present; summary relation resolves; decorative mode is hidden; table-alternative link is keyboard reachable.
- Representative chart tests for Dashboard, Daily, Supplier/Shoe Type/Color and one Nivelacija/Inventory family.
- Static/Node check: named production chart call sites cannot silently regress to an unlabeled chart wrapper unless allowlisted with rationale.
- `npm run check:analytics-guardrails`, `npm run typecheck`, focused Vitest, build, governance validators, `git diff --check`.

### Acceptance

- Every in-scope analytics chart has an accessible name and either a concise textual summary or a discoverable equivalent table.
- Screen-reader users can recover the chart's decision-relevant meaning without relying on color or hover-only tooltips.
- No new business value is computed by the accessibility layer.
- P-UI-38 receives a stable invariant it can ratchet in CI.

### Dependencies

- P-UI-47 first for canonical chart/status tokens.
- P-UI-31/P-UI-35/P-UI-36 first, or explicit owner deferral, to avoid broad same-page collisions.
- P-UI-44 first, or explicit deferral, before P-UI-53 edits overlapping Daily Sales/Inventory page/table paths; a disjoint split requires a fresh collision proof.
- P-UI-38 consumes this prompt and remains the final gate.

Owner claim 2026-10-06: after P-UI-41 DONE was freshly verified on origin/main cf9ba0428387d9c1e962e3dad77de13cb37eb5eb, a full 16-file active RQ/SQL/P-UI/MASTER cascade found no newly runnable RQ dependency and no P-UI dependent on P-UI-41. Current primary RQ READY is none; BCI/STAB/QDB/MT/GAI have no higher-priority repository-local READY execution lane. P-UI-47 is dependency-complete and explicitly parallel-safe with current READY paths; no active lock, matching branch or open PR was found. P-UI-47 moved READY -> IN_PROGRESS. Local lock: .ai/task-locks/P-UI-47-codex.lock.md. P-UI-41 run log: .ai/runs/2026-10-06-P-UI-41-evidence.md; evidence synchronized.

Owner claim 2026-10-06: after P-UI-47 DONE was verified on `origin/main` `88e388c3718b7ea006d79df949b2eaf20b207c72`, a full 16-file active RQ/SQL/P-UI/MASTER cascade found no RQ/SQL dependent and no higher-priority global queue lane. P-UI-31's P-UI-47 dependency was satisfied; no active owner, lock, matching branch or open PR conflicted with Supplier overview paths. P-UI-31 moved WAITING -> READY -> IN_PROGRESS as the highest-priority safe P1 successor. P-UI-35/P-UI-36/P-UI-43/P-UI-44 were promoted to READY. P-UI-42 remains WAITING behind P-UI-43 and P-UI-51 shared paths. Local lock: `.ai/task-locks/P-UI-31-codex.lock.md`. P-UI-47 run log: `.ai/runs/2026-10-06-P-UI-47-evidence.md`; evidence synchronized.

Owner promotion/claim 2026-10-06: after P-UI-31 DONE was delivered and `origin/main` refreshed to `19f9d94e07aab4cc754d46d5db49d38687820a49`, a full 16-file active RQ/SQL/P-UI/MASTER cascade searched P-UI-31 and re-evaluated the P-UI non-terminal candidates. No RQ/SQL dependency became newly runnable; BCI/STAB/RQ/QDB/MT/GAI exposed no higher-priority repo-local candidate. P-UI-35 was READY with P-UI-39/P-UI-47/P-UI-28/P-UI-29 DONE; RQ552/RQ553/RQ571 are DONE, RQ556 remains owner-gated and non-blocking for this presentation-only scope, and no active Nivelacija owner, matching lock, branch or PR remained. P-UI-35 moved READY -> IN_PROGRESS. Local lock: `.ai/task-locks/P-UI-35-codex.lock.md`; P-UI-31 evidence: `.ai/runs/2026-10-06-P-UI-31-evidence.md`.
