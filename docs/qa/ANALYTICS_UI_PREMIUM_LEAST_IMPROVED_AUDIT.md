# Analytics UI Premium Least-Improved Audit

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Status: follow-up UI audit + scoped runtime improvements

## Purpose

This pass targets analytics screens that were least improved by the previous premium UI work.

Previous work already improved:

- global sidebar
- global header/status bar
- shared `AnalyticsTrustHeader`
- shared `AnalyticsTableToolbar`
- Product Decision Center CSS
- central Inventory items table

This pass focuses on remaining table/workflow-heavy surfaces that still looked closer to utility/admin UI.

## Screens reviewed

### Analytics Actions / Outcome workflow

Files reviewed:

- `Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx`
- `Klijent/clientapp/src/pages/AnalyticsActionsPage.css`

Main controls/tables:

- KPI bar for action counts
- outcome summary panel
- breakdown cards by source/priority/data quality/outcome status
- active summary filters
- status/priority/source/data-quality/search filters
- action queue table
- status/action buttons
- expandable detail rows
- outcome/status modals
- impact ledger and notes timeline

Finding:

- This was the least premium screen: many plain Bootstrap-like colors, small radius, flat table, plain modals and old-style badges.
- It is also a high-trust screen, so the UI should make outcome/evidence state easier to scan.

Runtime improvement applied:

- `Klijent/clientapp/src/pages/AnalyticsActionsPage.css`
- Commit: `cc14194349dbc7131b4d4ddd1f4e9c118d399f31`

What changed:

- premium page/header/card/table surfaces
- premium KPI cards
- premium outcome summary/breakdown cards
- framed filter bar
- sticky premium action table header
- rounded status/data-quality/outcome badges
- rounded action buttons
- premium expanded detail cards
- premium modal backdrop and modal panel
- improved mobile wrapping for summary/filter/table helpers

What was not changed:

- action/outcome semantics
- measured impact logic
- modal validation logic
- Serbian copy inside `AnalyticsActionsPage.tsx`

Reason:

- Copy/diacritic cleanup inside the large TSX file should be a separate targeted prompt to avoid mixing copy and visual CSS changes.

### Supplier Decision ranking table

Files reviewed:

- `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDecisionTable.tsx`
- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.css`
- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.css`

Main controls/tables:

- backend sort buttons
- backend pagination
- supplier ranking table
- recommendation pill
- shared export toolbar
- row click/detail behavior

Finding:

- Supplier surfaces already had some dark/premium styling, but `SupplierDecisionTable` itself looked less aligned with the newer Product/Inventory table treatment.

Runtime improvement applied:

- `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDecisionTable.tsx`
- Commit: `84723fc6958038fe17ce0aee09e56b6df3b3ffd9`

What changed:

- premium table panel wrapper
- overline/header hierarchy
- row-count chip
- premium table shell
- stronger uppercase sortable headers
- numeric right alignment
- clearer row hover state
- premium pagination controls
- preserved shared `AnalyticsTableToolbar`
- preserved backend sort/pagination behavior

What was not changed:

- supplier decision formulas
- backend sorting and pagination
- export payload values
- detail/drill-down behavior

## Screens still under-improved

### Data Quality detail tables

Already decent, but still needs a shared table-system migration after `P-UI-03`.

Risk:

- UI work must not hide known reliability issues: health status, top-offender total count, and insufficient-data semantics.

### Legacy supplier/shoe/color analytics pages

Examples:

- `SupplierSalesStatsPage.tsx`
- `ShoeTypeSalesStatsPage.tsx`
- `ColorSalesStatsPage.tsx`
- `DailySalesStatsPage.tsx`
- `ProdajaPrePostNivelacijePage.tsx`

Risk:

- They likely still use page-specific CSS and older chart/table/control patterns.
- They should be migrated after screenshot protocol and shared control/table systems exist.

### Analytics Actions copy and outcome modal UX

Known visible issues left intentionally out of this CSS patch:

- some Serbian copy still lacks diacritics inside TSX strings;
- outcome modal needs better evidence-first UX;
- pending/not-measured field disabling should be handled with reliability prompt work, not only styling.

## Queue recommendations

Add or keep these prompts:

1. `P-UI-05` visual regression protocol remains first.
2. `P-UI-03` shared analytics table system should precede broad table migration.
3. `P-UI-09` Analytics Actions copy/outcome UX refinement.
4. `P-UI-10` Data Quality table migration to shared table system.
5. `P-UI-11` Legacy analytics pages modernization.

## Validation status

Tests were not run in this GitHub connector session.

Manual visual verification is required for:

- Analytics Actions table and modals
- Supplier Decision ranking table
- dark/light themes
- mobile/tablet/desktop layouts
