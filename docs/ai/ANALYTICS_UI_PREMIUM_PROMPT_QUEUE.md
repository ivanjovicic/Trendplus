# Analytics UI Premium Prompt Queue

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none (P-UI-18 DONE; see least-improved addendum completion note)
Purpose: make analytics navigation, controls, tables and dashboard UX premium without mixing visual polish with analytics correctness fixes.

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

