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
| P-UI-15 | DONE | legacy-analytics-modernization | Continue legacy page modernization (ProdajaPrePostNivelacijePage) |
| P-UI-16 | DONE | pre-nivelacija-priority-signal-copy | Fix unavailable reliability shown as Nisko + empty/copy polish |
| P-UI-17 | DONE | legacy-analytics-modernization | Modernize PreNivelacijaPriorityPage chrome (ControlBar + DataTable) |

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

Status: DONE
Ready after: P-UI-14 DONE
Priority: P2
Type: frontend/design/tests
Feature family: legacy-analytics-modernization
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/P-UI-15-codex.lock.md` (removed after DONE)
Commit suggestion: `feat(ui): modernize ProdajaPrePostNivelacijePage chrome`
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

### Completion note (2026-08-11)

- Modernized `ProdajaPrePostNivelacijePage.tsx`: added shared `AnalyticsControlBar` for period, dobavljač, kategorija and objekat filters; wrapped the priority table in `AnalyticsDataTable`; kept the trust header, table semantics, focus chips and decision details intact.
- Added focused regression coverage for the shared control bar, data-table wrapper and the existing scope-lineage behavior.
- Checks:
  - `npm run test -- --run src/pages/ProdajaPrePostNivelacijePage.spec.tsx` - pass
  - `npm run check:analytics-guardrails` - pass
  - `npm run build` - pass
- Remaining risk:
  - React test suite still emits a pre-existing `act(...)` warning in the data-scope change case.
- Next:
  - Current P-UI READY: `P-UI-18`

---

## P-UI-16 - Pre-nivelacija priority: no fake reliability + empty/copy polish

Status: DONE
Ready after: P-UI-15 DONE
Priority: P1
Type: frontend/copy/ux/tests
Feature family: pre-nivelacija-priority-signal-copy
Parallel-safe: yes, when paths clear (`PreNivelacijaPriorityPage*`)
Owner: Cursor
Local lock: `.ai/task-locks/P-UI-16-cursor.lock.md` (released on DONE)
Commit suggestion: `fix(ui): stop showing missing reliability as Nisko on pre-nivelacija priority`
Promotion note: 2026-08-11 — defect audit of `PreNivelacijaPriorityPage` after P-UI-15 left that page unmodernized.

### Problem

On **Prioriteti pre-nivelacije**, missing reliability is coerced to `0` and the priority table renders the pill **"Nisko"** even when `reliabilityAvailable === false`. Detail already shows unavailable correctly. Empty-state copy talks about “prodaja u periodu” / “proširite period” on a screen without a date period. Several Serbian strings lack diacritics / mix English chrome.

This is presentation of an existing availability flag — not inventing backend reliability.

### Evidence

- Null reliability → `0` while keeping `reliabilityAvailable`:
  - `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx` (~333–336)
- Table pill buckets only on numeric `%` (so `0` → “Nisko”), ignores availability:
  - same file (~827–847)
- Detail already uses unavailable correctly:
  - same file (~928) via `RECOMMENDATION_SIGNAL_UNAVAILABLE`
- Empty copy wrong for this screen (`periodFrom`/`periodTo` are null on TrustHeader):
  - same file (~640–644): “Promenite filtere ili proširite period.” / “Nije bilo prodaje u izabranom periodu.”
- Copy/encoding leftovers:
  - ~275 `ucitavanju`
  - ~701 `snize`
  - ~705 `moze`
  - ~747 toolbar title `Pre-nivelacija decision support`
  - ~1016 InfoTip without diacritics (`rasporedjeni`, `Pojacaj`, `Zadrzi`, `pomocnim`)

### Scope

In scope:

- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.css` only if a new unavailable pill class is required
- `Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx` (or a focused premium/signal sibling)

Out of scope:

- backend recommendation/reliability formulas or API contracts
- season/footwear filter catalog completeness (paginated candidates only — needs RQ/backend; do not invent catalog)
- full ControlBar/DataTable migration (owned by `P-UI-17`)
- chart color token cleanup (owned by `P-UI-17`)

### Read first

- `AGENTS.md` (no fake zero / unknown ≠ weak)
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md` (missing evidence must not look trusted)
- `docs/ai/ENCODING_AND_TEXT_SAFETY.md`
- Existing shared helpers: `RECOMMENDATION_SIGNAL_UNAVAILABLE`, detail-panel pattern on this page

### Do

1. When `!row.reliabilityAvailable`, render unavailable (same semantics as detail / `RECOMMENDATION_SIGNAL_UNAVAILABLE`), not Visoko/Srednje/Nisko.
2. Keep Visoko/Srednje/Nisko buckets only when reliability is available.
3. Replace empty-state messages so they match SKU priority filters (supplier/focus/season/footwear), not sales period.
4. Fix the listed Serbian diacritics / English toolbar title on this page only.
5. Do not change recommendation status, scores, or API payloads.

### Tests

```powershell
cd Klijent/clientapp
npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx
```

Add/extend a case where `recommendation.reliabilityPct` (and row `reliabilityPct` if used) is null/absent and assert the table does **not** show “Nisko”.

### Acceptance

- Unavailable reliability never appears as “Nisko” / weak pill in the priority table.
- Available reliability still maps to Visoko/Srednje/Nisko unchanged.
- Empty-state copy no longer references sales period / expanding period.
- Listed diacritic/English chrome strings on this page are fixed (UTF-8, no mojibake).
- No backend/API/formula changes.

### Dependencies

- `P-UI-15` DONE (satisfied)
- Path-safe vs higher-priority BCI/STAB/RQ exclusive work

### Completion note (2026-08-11)

- Confirmed/finished `reliabilitySignalDisplay`: unavailable → “Nije dostupno” + `signal-na` (not “Nisko”).
- Empty-state titles/messages tied to SKU priority filters (no sales-period wording).
- Diacritic polish on KPI data-note / queue InfoTip; toolbar title already Serbian.
- Deduped `signal-na` CSS to theme-token rule.
- Checks:
  - `npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx` - pass (5/5)
- Next:
  - Current P-UI READY: `P-UI-17`

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
Promotion note: 2026-08-11 — promoted after P-UI-16 DONE; last remaining least-improved legacy candidate.

### Problem

`PreNivelacijaPriorityPage` still uses page-local filter chrome (`pnp-decision-filters`) and a page-local table wrap instead of shared `AnalyticsControlBar` / `AnalyticsDataTable`. Chart tooltip hardcodes `#ef4444` / `#16a34a` instead of theme tokens.

### Evidence

- Local filters: `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx` (~575 `pnp-decision-filters`)
- No `AnalyticsControlBar` / `AnalyticsDataTable` imports on this page
- Hardcoded tooltip colors: same file (~120–123)
- Contrast: `ProdajaPrePostNivelacijePage` already migrated in P-UI-15

### Scope

In scope:

- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx`
- `Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.css`
- focused tests under `Klijent/clientapp/src/pages/__tests__/`

Out of scope:

- analytics formulas / backend endpoints / export values
- inventing season/footwear filter catalogs (still RQ if full fix needed)
- redoing P-UI-16 signal/copy work (must already be DONE)

### Read first

- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`
- Recent one-page migrations: Supplier / ShoeType / Color / Daily / ProdajaPrePostNivelacije patterns
- `P-UI-16` completion note (signal semantics must stay)

### Do

1. Confirm `AnalyticsTrustHeader` remains.
2. Migrate filters to `AnalyticsControlBar`.
3. Migrate primary priority table to `AnalyticsDataTable` (+ existing toolbar if present).
4. Replace hardcoded tooltip trend colors with CSS variables / theme tokens.
5. Keep chart data contracts and recommendation semantics unchanged.
6. Add/update focused tests for control bar + data table presence.

### Tests

```powershell
cd Klijent/clientapp
npm run test -- --run src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx
```

Prefer a dedicated premium chrome assertion (control bar + data table test ids) without weakening P-UI-16 reliability coverage.

### Acceptance

- Page uses shared ControlBar + DataTable chrome without semantic drift.
- Tooltip colors use theme tokens (no hardcoded red/green hex for trend).
- P-UI-16 unavailable-reliability behavior remains green.

### Dependencies

- P-UI-16 DONE (satisfied)

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
  - Shared `AnalyticsControlBar` / `AnalyticsDataTable` chrome now wraps the page filters and primary table.
  - Tooltip trend colors now use theme tokens instead of hardcoded red/green hex values.
  - P-UI-16 unavailable-reliability behavior remains intact.
- Remaining:
  - none
- Path-safe vs higher-priority BCI/STAB/RQ exclusive work
