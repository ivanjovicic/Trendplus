# Analytics UI Premium Prompt Queue

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none (queue complete)
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
| P-UI-19 | DONE | analytics-ui-regression-hardening | Verify recent React chrome migrations across shared analytics components and modernized pages |
| P-UI-20 | DONE | analytics-ui-trust-state-proof | Grouped ErrorState/EmptyState/TrustHeader proof on Daily/Color/ShoeType/Supplier/Actions pages |
| P-UI-21 | DONE | analytics-ui-empty-kpi-honesty | Hide KPI totals on empty success; use shared ErrorState on Actions list failure |
| P-UI-22 | DONE | analytics-ui-remaining-trust-chrome | Remaining decision pages empty/error chrome after P-UI-21 |

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
