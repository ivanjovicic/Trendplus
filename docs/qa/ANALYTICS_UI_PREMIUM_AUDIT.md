# Analytics UI Premium Audit

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Status: UI audit + scoped runtime improvements

## Scope

Reviewed primary analytics UX surfaces:

- global app shell and sidebar navigation
- global header/status bar visible on all pages
- analytics route definitions
- analytics dashboard cockpit
- shared analytics trust header
- shared analytics table toolbar/export modal
- product decision controls/table
- inventory analytics table
- data quality controls/table
- supplier decision table patterns
- table/export affordances

Reviewed files:

- `Klijent/clientapp/src/layout/components/Sidebar.tsx`
- `Klijent/clientapp/src/layout/components/HeaderStatus.tsx`
- `Klijent/clientapp/src/layout/navConfig.ts`
- `Klijent/clientapp/src/routes/analyticsRouteDefinitions.ts`
- `Klijent/clientapp/src/App.tsx`
- `Klijent/clientapp/src/components/ApiPingFlag.tsx`
- `Klijent/clientapp/src/components/WorkerControlFlag.tsx`
- `Klijent/clientapp/src/components/RedisToggleFlag.tsx`
- `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader.tsx`
- `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader.css`
- `Klijent/clientapp/src/components/analytics/AnalyticsTableToolbar.tsx`
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`
- `Klijent/clientapp/src/pages/AnalyticsDashboard.css`
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.css`
- `Klijent/clientapp/src/pages/DataQualityPage.tsx`
- `Klijent/clientapp/src/pages/DataQualityPage.css`
- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/components/inventory/InventoryItemsTable.tsx`
- `Klijent/clientapp/src/components/supplierDecisionHub/SupplierDecisionTable.tsx`

## What was improved now

### UI-F01 - Premium navigation shell

File changed:

- `Klijent/clientapp/src/layout/components/Sidebar.tsx`

Before:

- Sidebar was functional but visually flat.
- Active group/item state was mostly ring/background only.
- Header did not explain product context.
- Collapsed button had typo `Raspiri meni`.

Now:

- Premium gradient sidebar shell.
- Stronger brand/header hierarchy.
- Group icons live in framed icon wells.
- Active group gets stronger border/shadow.
- Active item gets a left rail indicator.
- Group headers show item count.
- Mobile overlay uses stronger backdrop blur.
- Collapsed action label fixed to `Raširi meni`.

Commit:

- `873f61525d0a4b9ef0a72fd1bd8b25c60257ca19`

### UI-F02 - Premium shared analytics table/export toolbar

File changed:

- `Klijent/clientapp/src/components/analytics/AnalyticsTableToolbar.tsx`

Before:

- Export toolbar was compact but looked utility-like.
- Export menu had short labels only.
- Modal copy did not clearly explain table/filter/metadata parity.
- Some Serbian text lacked diacritics.

Now:

- Toolbar is a framed premium control bar.
- Export menu has icons and descriptive labels for PDF/Excel/CSV.
- Row count is shown as a compact trust chip.
- Success/status message is a compact status pill.
- Export modal has a trust explainer strip.
- Serbian copy is corrected: `Čekam`, `završen`, `Uključi`, `generišu`, `Otkaži`, `Generišem`.

Commit:

- `a0d4a6328d5abea246a63ceadec015b47c9ef99a`

### UI-F03 - Premium global header/status bar

Files changed:

- `Klijent/clientapp/src/layout/components/HeaderStatus.tsx`
- `Klijent/clientapp/src/components/ApiPingFlag.tsx`
- `Klijent/clientapp/src/components/WorkerControlFlag.tsx`
- `Klijent/clientapp/src/components/RedisToggleFlag.tsx`

Before:

- Header was functional but looked like a developer control strip.
- Current page context was not visible in the header.
- Backend, API ping, workers and Redis controls used mixed visual styles.
- Several Serbian labels lacked diacritics.
- On smaller widths, controls could compete with page title and actions.

Now:

- Header is a premium sticky command/status bar.
- It derives current group and current page from `NAV_GROUPS` and `location.pathname`.
- Page context appears as breadcrumb-like group → page text.
- Backend state is a compact status pill with tone, icon and last-check tooltip.
- API ping, workers and Redis controls have consistent premium pill/button styling.
- System controls collapse into a secondary horizontal strip below the header on smaller screens.
- Data scope selector is framed as a data view control.
- Theme and refresh buttons have consistent rounded premium styling.
- Serbian copy is corrected: `Postojeći`, `Osveži`, `učitavanje`, `isključeni`, `uključen`, `greška`.

Commits:

- `a49a966f73d851245772807fff751b6c767f2dfc`
- `7dd6d89190d11600dcd714bfd5b4d111e725d092`
- `cf743b39457e1c6978ea42dd0b7fc59540d5b585`
- `5da4ab031389afbda1ea0ac7371d9d1be8c37a7b`

### UI-F04 - Premium shared analytics trust header

File changed:

- `Klijent/clientapp/src/components/analytics/AnalyticsTrustHeader.css`

Before:

- Trust header was correct and useful, but visually closer to a plain Bootstrap/card layout.
- Status and freshness chips used hard-coded light colors and did not feel integrated with the dark premium app theme.
- Metadata cards and footer links were functional but not visually elevated.

Now:

- Trust header has premium gradient/radial surface, stronger shadow and larger radius.
- Title, overline, metadata cards, status chips, freshness chips and footer links are redesigned with theme-aware tokens.
- Status chip includes a dot indicator via CSS.
- Summary chips/cards have stronger information hierarchy.
- Mobile layout keeps meta cards stacked and status full-width.

Commit:

- `7b9ddfd9aa31a26305d25defeb7951c018367a7b`

### UI-F05 - Premium Product Decision Center screen

File changed:

- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.css`

Before:

- Header/filter/table sections were clear but visually utilitarian.
- Table density was high and the sticky header lacked a premium hierarchy.
- Pills/buttons were inconsistent and small.

Now:

- Header, filter surface, message states and table shell use premium surfaces, gradients and shadows.
- KPI cards, filter fields and table headers have stronger hierarchy.
- Table uses separated borders, sticky premium header, clearer hover state and more readable spacing.
- Recommendation, data-quality and confidence pills have consistent borders/backgrounds.
- Queue/reason buttons use rounded premium styling.

Commit:

- `48706e0d12551fc3aabcb0b7085323cf1d4a678e`

### UI-F06 - Premium Inventory items table

File changed:

- `Klijent/clientapp/src/components/inventory/InventoryItemsTable.tsx`

Before:

- Inventory table was functional but dense.
- Header had hard `text-white` usage and several labels lacked diacritics.
- Row severity left border used the same border color for all stock states.
- Pagination/buttons were functional but not visually aligned with the premium header/sidebar work.

Now:

- Table panel has premium gradient surface, larger radius and shadow.
- Header now includes an overline and a trust-style row count chip.
- Table uses separated border layout, stronger uppercase headers and cleaner row borders.
- Critical/warning/good rows now get different left accent colors.
- Action buttons are rounded premium pills.
- Serbian copy is corrected: `Količina`, `Dobavljač`, `Učitavam`, `sledeću`, `Sledeća`.

Commit:

- `e8a67b988528e44b9b2f99bee9d336432d82b920`

## Main controls and tables identified

### Global top bar

- Source: `HeaderStatus.tsx` through `AppLayout`.
- Visible on all non-print pages.
- Main controls:
  - mobile menu button
  - backend status
  - API ping toggle
  - worker status/toggle/refresh
  - Redis status/toggle
  - data scope selector
  - theme settings link
  - global refresh
- Current remaining risk: header still lacks full command-search, notification center, breadcrumbs for routes outside nav config and user/account area.

### Navigation / menu

- Source: `navConfig.ts`.
- Analytics group currently has many items in one long flat list:
  - Trendplus pregled
  - Pilot spremnost
  - Izvršni board
  - Odluke o proizvodima
  - Pregled dobavljača
  - Zalihe i dopuna
  - Kvalitet podataka
  - Centralne akcije
  - several legacy/detail analytics pages
- Risk: premium users need clearer hierarchy: Executive, Decisions, Operations, Data Quality, Reports/Legacy.

### Shared analytics trust header

- Source: `AnalyticsTrustHeader.tsx` + `AnalyticsTrustHeader.css`.
- Used as the top context/trust component on multiple analytics pages.
- Main controls/info:
  - mode label: recommendation/signal/report
  - page title and description
  - data-quality status
  - period
  - last refresh and freshness
  - data source
  - dataset/fallback/gated/partial warnings
  - data-quality summary chips
  - links to data quality, refresh status and methodology
- Improved now because it multiplies across multiple analytics screens.

### Analytics dashboard

- Source: `AnalyticsDashboard.tsx` + `AnalyticsDashboard.css`.
- Main sections:
  - trust header
  - refresh status banner
  - KPI row
  - weekly decision cockpit
  - action queue controls
  - data quality/readiness panels
  - charts and advanced tables
- Current risk: page is powerful but dense. It needs stronger above-the-fold command hierarchy and consistent control group styling.

### Product Decision Center

- Source: `ProductDecisionCenterPage.tsx` + `ProductDecisionCenterPage.css`.
- Main controls/tables:
  - period/date filters
  - store/supplier filters
  - recommendation/data-quality filters
  - KPI cards
  - sortable product decision table
  - recommendation/data-quality/confidence pills
  - reason expansion/action queue controls
- Improved now at CSS/layout level. Business logic and recommendation semantics were not touched.

### Inventory analytics

- Source: `InventoryPage.tsx` + `InventoryItemsTable.tsx` and inventory subcomponents.
- Main controls/tables:
  - search and filters
  - store/supplier/page-size/sort controls
  - KPI cards
  - inventory items table
  - forecast, alerts, rebalance, size curve and action workflow panels
  - export/scheduler/print controls
- Improved now for the central inventory items table. Broader inventory page control system remains queue work.

### Data Quality

- Source: `DataQualityPage.tsx` + `DataQualityPage.css`.
- Main controls/tables:
  - view tabs: issues/intake
  - issue tabs
  - health score card
  - top offender table
  - issue list table
  - search/sort/page-size controls
  - pilot intake report panels
- Existing CSS is already closer to premium, but has known reliability/UI queue risks around health status and top-count semantics.

### Supplier Decision / supplier analytics tables

- Source examples: `SupplierDecisionTable.tsx`, `SupplierDecisionHubPage.tsx`, `SupplierSalesStatsPage.tsx`.
- Main controls/tables:
  - backend pagination/sort
  - supplier ranking table
  - recommendation/status pills
  - shared export toolbar
  - detail drill-down
- Remaining risk: supplier-specific table styling should be migrated into a shared analytics table system instead of individual CSS.

### Shared table/export controls

- Source: `AnalyticsTableToolbar.tsx`.
- Used by analytics tables to export/print.
- Improved because it multiplies across multiple pages.
- Remaining risk: not all analytics pages use this shared toolbar; some pages still have custom export buttons.

## What still needs queue work

### P-UI-01 - Analytics menu information architecture

Problem:

- The analytics menu is a long flat list.
- Some entries are core decision surfaces while others are legacy/detail/smoke/support pages.
- Users need a premium IA: Executive, Decisions, Operations, Data Quality, Reports/Legacy.

Recommendation:

- Add support for subgroups/sections inside nav groups or split analytics into multiple nav groups.
- Keep route aliases stable.
- Add smoke tests for all routes after refactor.

### P-UI-02 - Analytics page control system

Problem:

- Filter bars, date presets, search, store/supplier selectors and refresh/export controls differ between pages.

Recommendation:

- Create a shared `AnalyticsControlBar` with consistent premium styling and metadata chips.
- Migrate pages incrementally.

### P-UI-03 - Analytics table system

Problem:

- Tables use page-specific CSS/classes.
- Numeric alignment, sticky headers, density, empty states and export metadata are inconsistent.

Recommendation:

- Create shared table styles/components for analytics tables.
- Include numeric right alignment, compact density, sticky header, visible row/total/truncation labels, trust metadata and export parity.

### P-UI-04 - Dashboard command center redesign

Problem:

- Analytics dashboard contains the right concepts but is visually dense.
- Executive decision, data trust and operational actions should be more clearly prioritized above the fold.

Recommendation:

- Redesign above-the-fold layout as a command center:
  - left: business outcome KPIs
  - center: this week's actions
  - right: data trust / freshness
  - bottom: risk/loss highlights

### P-UI-05 - Visual regression / screenshot review

Problem:

- Current GitHub connector changes cannot verify actual rendered pixels.

Recommendation:

- Add Playwright or story/screenshot checklist for sidebar, global header, analytics dashboard, trust header, product decision, inventory, toolbar modal and key table pages in dark/light themes.

### P-UI-06 - Global command header system

Problem:

- Header now has premium styling, but the application still lacks full top-level command UX.

Recommendation:

- Add global command/search launcher.
- Add route-aware breadcrumbs for dynamic/detail routes.
- Add notification/action inbox for worker/backend/analytics warnings.
- Add user/account/store context if authentication/account context is added.

### P-UI-07 - Supplier analytics table migration

Problem:

- Supplier decision/sales tables still use page-specific table styles.
- Ranking, detail and export parity should share the same premium table contract as product/inventory/data-quality tables.

Recommendation:

- Migrate one supplier analytics table to the shared table system after P-UI-03.
- Keep backend sort/pagination semantics unchanged.

### P-UI-08 - Inventory page control surface consolidation

Problem:

- Inventory has many controls and operational panels. The central table now looks better, but page-level filters/export/scheduler/sort controls still need one premium command surface.

Recommendation:

- Use the future `AnalyticsControlBar` for search, store/supplier, page size, sort, export and report scheduling.
- Migrate in small steps and preserve existing API calls.

## Recommended execution order

1. P-UI-05 screenshot/regression harness or manual screenshot protocol.
2. P-UI-06 global command header system design.
3. P-UI-01 menu IA, because it affects analytics navigation first impression.
4. P-UI-02 shared control bar.
5. P-UI-03 shared analytics table system.
6. P-UI-07 supplier analytics table migration.
7. P-UI-08 inventory page control surface consolidation.
8. P-UI-04 command center redesign.

## Validation status

Tests were not run in this GitHub connector session.

Manual visual verification is still required before calling the UI premium work complete.
