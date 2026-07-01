# Analytics UI Premium Prompt Queue - Least-Improved Addendum

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Status: queue addendum
Use with:

- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- `docs/qa/ANALYTICS_UI_PREMIUM_LEAST_IMPROVED_AUDIT.md`
- `docs/ai/PROMPT_TOKEN_ECONOMY_AND_LINT.md`

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| P-UI-09 | WAITING | analytics-actions-copy-outcome-ux | Polish action queue copy and outcome modal evidence UX |
| P-UI-10 | WAITING | data-quality-table-migration | Migrate Data Quality issue/top-offender tables to shared premium table system |
| P-UI-11 | WAITING | legacy-analytics-modernization | Modernize legacy supplier/shoe/color/daily/nivelacija analytics pages |

---

## P-UI-09 - Analytics Actions copy and outcome UX refinement

Status: WAITING
Ready after: P-UI-05 and RQ81/RQ86 if outcome semantics are touched
Priority: P1
Type: frontend/copy/ux/tests
Feature family: analytics-actions-copy-outcome-ux
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-09-<agent>.lock.md`
Commit suggestion: `feat(ui): refine analytics actions outcome ux`

### Why

`AnalyticsActionsPage.css` now has a premium visual treatment, but the large TSX still contains some old copy and the outcome modal needs a clearer evidence-first workflow.

### Scope only

- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.css` only for minor follow-up styling
- existing tests if available

### Do not touch

- backend outcome semantics
- measured impact calculation
- action/outcome API contracts
- RQ81/RQ86 logic unless those prompts are explicitly included

### Do

1. Fix Serbian UI copy and diacritics in visible strings, for example `Dobavljaci`, `Zavrsi`, `ocekivanja`, `Preporucena`.
2. Improve outcome modal explanatory copy so users understand when measured impact/date are required or intentionally unavailable.
3. Make pending/not-measured states visually clear without changing data semantics.
4. Keep current validation rules unless RQ81/RQ86 is included.
5. Add or update focused tests if existing test harness covers the page.

### Acceptance

- Action queue UI reads professionally in Serbian.
- Outcome modal is easier to use and does not imply fake measurement evidence.
- No backend semantics are changed.

---

## P-UI-10 - Data Quality table migration

Status: WAITING
Ready after: P-UI-03 and after relevant reliability prompts for health/top-count semantics
Priority: P1
Type: frontend/component/tests
Feature family: data-quality-table-migration
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-10-<agent>.lock.md`
Commit suggestion: `feat(ui): migrate data quality tables`

### Why

Data Quality already has decent page styling, but issue and top-offender tables still use page-local CSS. They should use the shared analytics table system once available.

### Scope only

- `Klijent/clientapp/src/pages/DataQualityPage.tsx`
- `Klijent/clientapp/src/pages/DataQualityPage.css`
- shared table component/style from P-UI-03

### Do not touch

- health status semantics
- top-offender count semantics
- data quality API contracts
- missing/unknown/fake-green logic

### Do

1. Migrate one Data Quality table first.
2. Preserve numeric alignment for revenue, impact and percentages.
3. Keep issue tabs and view tabs unchanged.
4. Preserve export payload/filter metadata.
5. Clearly label returned-count vs total-count if backend total is unavailable.

### Acceptance

- Data Quality table matches the premium table system without hiding reliability warnings.

---

## P-UI-11 - Legacy analytics pages modernization

Status: WAITING
Ready after: P-UI-02, P-UI-03, and P-UI-05
Priority: P2
Type: frontend/design/tests
Feature family: legacy-analytics-modernization
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-11-<agent>.lock.md`
Commit suggestion: `feat(ui): modernize legacy analytics pages`

### Why

Several older analytics pages likely still use page-specific filters, tables, chart wrappers and control patterns. They should be modernized after the shared control/table systems exist.

### Candidate pages

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ColorSalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`

### Do not touch

- analytics formulas
- backend endpoints
- chart data contracts
- export values

### Do

1. Pick one page per run.
2. Add/confirm `AnalyticsTrustHeader` if missing.
3. Migrate page controls to `AnalyticsControlBar` if available.
4. Migrate tables to shared analytics table system if available.
5. Keep chart semantics unchanged.
6. Verify mobile/tablet/desktop via P-UI-05 protocol.

### Acceptance

- One legacy analytics page matches the premium analytics design language and preserves analytics semantics.
