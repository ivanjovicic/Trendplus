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
| P-UI-09 | DONE | analytics-actions-copy-outcome-ux | Polish action queue copy and outcome modal evidence UX |
| P-UI-10 | DONE | data-quality-table-migration | Migrate Data Quality issue/top-offender tables to shared premium table system |
| P-UI-11 | DONE | legacy-analytics-modernization | Modernize legacy supplier/shoe/color/daily/nivelacija analytics pages |
| P-UI-12 | DONE | legacy-analytics-modernization | Continue legacy page modernization (ShoeTypeSalesStatsPage) |
| P-UI-13 | DONE | legacy-analytics-modernization | Continue legacy page modernization (ColorSalesStatsPage) |
| P-UI-14 | DONE | legacy-analytics-modernization | Continue legacy page modernization (DailySalesStatsPage) |
| P-UI-15 | READY | legacy-analytics-modernization | Continue legacy page modernization (one page per run) |

---

## P-UI-09 - Analytics Actions copy and outcome UX refinement

Status: DONE
Ready after: P-UI-05 DONE; RQ81/RQ86 only if outcome *semantics* are touched (this prompt is copy/UX only — do not change backend outcome rules)
Priority: P1
Type: frontend/copy/ux/tests
Feature family: analytics-actions-copy-outcome-ux
Parallel-safe: yes, when no overlapping RQ action/outcome runtime task owns the same TSX paths
Owner: Cursor
Local lock: none
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

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
  - `Klijent/clientapp/src/pages/AnalyticsActionsPage.css`
  - `Klijent/clientapp/src/pages/__tests__/AnalyticsActionsPage.spec.tsx`
  - `Klijent/clientapp/src/pages/AnalyticsActionsPage.spec.tsx`
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE_LEAST_IMPROVED_ADDENDUM.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `npm run test -- --run src/pages/__tests__/AnalyticsActionsPage.spec.tsx src/pages/AnalyticsActionsPage.spec.tsx` - pass (22/22)
- Risks:
  - RQ81/RQ86 semantics unchanged; copy only clarifies locked/optional measured fields
- Next:
  - Current P-UI READY: `P-UI-10`

---

## P-UI-10 - Data Quality table migration

Status: DONE
Ready after: P-UI-03 and after relevant reliability prompts for health/top-count semantics
Priority: P1
Type: frontend/component/tests
Feature family: data-quality-table-migration
Parallel-safe: no
Owner: Cursor
Local lock: none
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

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `Klijent/clientapp/src/pages/DataQualityPage.tsx`
  - `Klijent/clientapp/src/pages/DataQualityPage.css`
  - `Klijent/clientapp/src/pages/DataQualityPage.spec.tsx`
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
  - `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE_LEAST_IMPROVED_ADDENDUM.md`
  - `MASTER_ROADMAP.md`
- Checks:
  - `npm run test -- --run src/pages/DataQualityPage.spec.tsx` - pass (5/5)
- Risks:
  - page-local `.data-quality-table` CSS remains for unused legacy selectors; visual QA via P-UI-05 if needed
- Next:
  - Current P-UI READY: `P-UI-11`

---

## P-UI-11 - Legacy analytics pages modernization

Status: DONE
Ready after: P-UI-02, P-UI-03, and P-UI-05
Priority: P2
Type: frontend/design/tests
Feature family: legacy-analytics-modernization
Parallel-safe: no
Owner: Cursor
Local lock: none
Commit suggestion: `feat(ui): modernize legacy analytics pages`
This run: `SupplierSalesStatsPage.tsx` only.

### Why

Several older analytics pages likely still use page-specific filters, tables, chart wrappers and control patterns. They should be modernized after the shared control/table systems exist.

### Candidate pages

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx` (**DONE this run**)
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

### Completion note

- Date: 2026-08-11
- Agent: Cursor
- Changed files:
  - `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx`
  - `Klijent/clientapp/src/pages/SupplierSalesStatsPage.css`
  - `Klijent/clientapp/src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx`
  - queue/MASTER docs
- Checks:
  - `npm run test -- --run src/pages/__tests__/SupplierSalesStatsPage.premium.spec.tsx` - pass (1/1)
- Next:
  - Current P-UI READY: `P-UI-12`

---

## P-UI-12 - Continue legacy analytics page modernization

Status: DONE
Ready after: P-UI-11 DONE
Priority: P2
Type: frontend/design/tests
Feature family: legacy-analytics-modernization
Parallel-safe: no
Owner: Cursor
Local lock: `.ai/task-locks/P-UI-12-cursor.lock.md` (released on DONE)
Commit suggestion: `feat(ui): modernize ShoeTypeSalesStatsPage chrome`
Promotion note: 2026-08-11 — remaining candidates after SupplierSalesStatsPage.
This run: `ShoeTypeSalesStatsPage.tsx` only.

### Why

P-UI-11 modernized one page. Remaining legacy analytics pages still use page-local filter/table chrome.

### Candidate pages (pick one)

- ~~`Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.tsx`~~ (DONE this run)
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
2. Confirm `AnalyticsTrustHeader`.
3. Migrate filters to `AnalyticsControlBar`.
4. Migrate tables to `AnalyticsDataTable`.
5. Keep chart semantics unchanged.
6. Add/update focused tests.

### Acceptance

- One more legacy page matches premium analytics chrome without semantic drift.

### Completion note (2026-08-11)

- Modernized `ShoeTypeSalesStatsPage.tsx`: TrustHeader (diacritics), filters → `AnalyticsControlBar`, priority table → `AnalyticsDataTable` + toolbar, numeric cols → `analytics-data-table__numeric`.
- Charts / recommendation semantics unchanged.
- Checks:
  - `npm run test -- --run src/pages/__tests__/ShoeTypeSalesStatsPage.premium.spec.tsx src/pages/ShoeTypeSalesStatsPage.spec.tsx` - pass (2/2)
- Next:
  - Current P-UI READY: `P-UI-13`

---

## P-UI-13 - Continue legacy analytics page modernization

Status: DONE
Ready after: P-UI-12 DONE
Priority: P2
Type: frontend/design/tests
Feature family: legacy-analytics-modernization
Parallel-safe: no
Owner: Cursor
Local lock: `.ai/task-locks/P-UI-13-cursor.lock.md`
Commit suggestion: `feat(ui): modernize legacy analytics page`
Promotion note: 2026-08-11 — remaining candidates after ShoeTypeSalesStatsPage.
This run: `ColorSalesStatsPage.tsx` only.

### Why

P-UI-13 is done. Remaining legacy analytics pages still use page-local filter/table chrome.

### Candidate pages (pick one)

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
2. Confirm `AnalyticsTrustHeader`.
3. Migrate filters to `AnalyticsControlBar`.
4. Migrate tables to `AnalyticsDataTable`.
5. Keep chart semantics unchanged.
6. Add/update focused tests.

### Acceptance

- One more legacy page matches premium analytics chrome without semantic drift.

### Completion note (2026-08-11)

- Modernized `ColorSalesStatsPage.tsx`: `AnalyticsTrustHeader`, `AnalyticsControlBar`, `AnalyticsDataTable`, `AnalyticsErrorState`, and `AnalyticsEmptyState` now replace page-local chrome.
- Kept chart semantics, recommendations, and export payload behavior unchanged.
- Added focused regression coverage for premium chrome, filter interactions, sorting, empty/error states, and detail snapshot behavior.
- Checks:
  - `npm run test -- --run src/pages/__tests__/ColorSalesStatsPage.spec.tsx` - pass
  - `npm run check:analytics-guardrails` - pass
  - `npm run build` - pass
- Checks not run:
  - `dotnet build`
  - `dotnet test`
- Remaining risk:
  - Legacy copy in some touched strings still needs broader encoding cleanup outside this prompt scope.
- Next:
  - Current P-UI READY: `P-UI-14`

---

## P-UI-14 - Continue legacy analytics page modernization

Status: DONE
Ready after: P-UI-13 DONE
Priority: P2
Type: frontend/design/tests
Feature family: legacy-analytics-modernization
Parallel-safe: no
Owner: Cursor
Local lock: `.ai/task-locks/P-UI-14-cursor.lock.md` (released on DONE)
Commit suggestion: `feat(ui): modernize DailySalesStatsPage chrome`
Promotion note: 2026-08-11 — remaining candidates after ColorSalesStatsPage.
This run: `DailySalesStatsPage.tsx` only.

### Why

P-UI-13 modernized Color. Remaining legacy analytics pages still use page-local filter/table chrome.

### Candidate pages (pick one)

- ~~`Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`~~ (DONE this run)
- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`

### Do not touch

- analytics formulas
- backend endpoints
- chart data contracts
- export values

### Do

1. Pick one page per run.
2. Confirm `AnalyticsTrustHeader`.
3. Migrate filters to `AnalyticsControlBar`.
4. Migrate tables to `AnalyticsDataTable`.
5. Keep chart semantics unchanged.
6. Add/update focused tests.

### Acceptance

- One more legacy page matches premium analytics chrome without semantic drift.

### Completion note (2026-08-11)

- Modernized `DailySalesStatsPage.tsx`: TrustHeader, filters → `AnalyticsControlBar` (Primeni/Reset), day table → `AnalyticsDataTable` + toolbar, numeric cols → `analytics-data-table__numeric`.
- Charts / export blank print / apply-filter semantics unchanged.
- Checks:
  - `npm run test -- --run src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx src/pages/__tests__/DailySalesStatsPage.spec.tsx` - pass (3/3)
- Next:
  - Current P-UI READY: `P-UI-15`

---

## P-UI-15 - Continue legacy analytics page modernization

Status: READY
Ready after: P-UI-14 DONE
Priority: P2
Type: frontend/design/tests
Feature family: legacy-analytics-modernization
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/P-UI-15-<agent>.lock.md`
Commit suggestion: `feat(ui): modernize legacy analytics page`
Promotion note: 2026-08-11 — remaining candidates after DailySalesStatsPage.
Recommended first: `ProdajaPrePostNivelacijePage.tsx`

### Why

P-UI-14 modernized Daily. Remaining legacy analytics pages still use page-local filter/table chrome.

### Candidate pages (pick one)

- `Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`

### Do not touch

- analytics formulas
- backend endpoints
- chart data contracts
- export values

### Do

1. Pick one page per run.
2. Confirm `AnalyticsTrustHeader`.
3. Migrate filters to `AnalyticsControlBar`.
4. Migrate tables to `AnalyticsDataTable`.
5. Keep chart semantics unchanged.
6. Add/update focused tests.

### Acceptance

- One more legacy page matches premium analytics chrome without semantic drift.
