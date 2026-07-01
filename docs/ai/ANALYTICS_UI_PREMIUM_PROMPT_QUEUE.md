# Analytics UI Premium Prompt Queue

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Current READY prompt: P-UI-05
Purpose: make analytics navigation, controls, tables and dashboard UX premium without mixing visual polish with analytics correctness fixes.

Use with:

- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/PROMPT_TOKEN_ECONOMY_AND_LINT.md`
- `docs/qa/ANALYTICS_UI_PREMIUM_AUDIT.md`

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| P-UI-05 | READY | analytics-ui-visual-regression | Add screenshot/manual visual review protocol before broad visual refactors |
| P-UI-06 | WAITING | global-command-header | Add full command/search/breadcrumb/notification header system |
| P-UI-01 | WAITING | analytics-menu-ia | Redesign analytics menu information architecture |
| P-UI-02 | WAITING | analytics-control-bar | Create shared premium analytics control bar |
| P-UI-03 | WAITING | analytics-table-system | Standardize analytics table density, sticky headers, numeric alignment and trust metadata |
| P-UI-04 | WAITING | analytics-command-center | Redesign analytics dashboard above-the-fold command center |

---

## P-UI-05 - Analytics visual regression protocol

Status: READY
Priority: P0
Type: docs/tests
Feature family: analytics-ui-visual-regression
Parallel-safe: yes
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-05-<agent>.lock.md`
Commit suggestion: `docs(ui): add analytics visual regression protocol`

### Why

Premium UI changes need rendered verification. GitHub connector code edits cannot prove that sidebar, global header, dashboard, export modal and tables look correct in dark/light themes.

### Scope only

- `docs/Frontend/` or `docs/qa/`
- optional Playwright/screenshot test files if the app already has a test harness

### Do

1. Add a visual review checklist or screenshot protocol for:
   - sidebar expanded/collapsed/mobile
   - global header desktop/tablet/mobile
   - analytics dashboard overview
   - export toolbar menu/modal
   - inventory table
   - supplier/product table
   - data quality table
2. Include dark and light theme expectations.
3. Include viewport matrix: mobile, tablet, desktop.
4. State exact validation command if automated, or manual screenshot evidence fields if not.

### Acceptance

- Future UI tasks have a repeatable way to verify visual regressions.

---

## P-UI-06 - Global command header system

Status: WAITING
Ready after: P-UI-05
Priority: P1
Type: frontend/design/tests
Feature family: global-command-header
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-06-<agent>.lock.md`
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

---

## P-UI-01 - Analytics menu information architecture

Status: WAITING
Ready after: P-UI-05
Priority: P1
Type: frontend/tests
Feature family: analytics-menu-ia
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-01-<agent>.lock.md`
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

---

## P-UI-02 - Shared analytics control bar

Status: WAITING
Ready after: P-UI-05
Priority: P1
Type: frontend/component/tests
Feature family: analytics-control-bar
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-02-<agent>.lock.md`
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

---

## P-UI-03 - Shared analytics table system

Status: WAITING
Ready after: P-UI-05 and RQ57/RQ58 if inventory table is touched
Priority: P1
Type: frontend/component/tests
Feature family: analytics-table-system
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-03-<agent>.lock.md`
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

---

## P-UI-04 - Dashboard command center redesign

Status: WAITING
Ready after: P-UI-02 and P-UI-03
Priority: P2
Type: frontend/design/tests
Feature family: analytics-command-center
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-04-<agent>.lock.md`
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
