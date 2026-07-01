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
- shared analytics table toolbar/export modal
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
- `Klijent/clientapp/src/components/analytics/AnalyticsTableToolbar.tsx`
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`
- `Klijent/clientapp/src/pages/AnalyticsDashboard.css`

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

### Shared table/export controls

- Source: `AnalyticsTableToolbar.tsx`.
- Used by analytics tables to export/print.
- Improved now because it multiplies across multiple pages.
- Remaining risk: not all analytics pages use this shared toolbar; some pages still have custom export buttons.

### Tables

- Source patterns:
  - `top-table` in `AnalyticsDashboard.css`
  - page-specific tables in supplier, product, inventory, actions, data quality pages
  - shared export payloads via `AnalyticsTableToolbar`
- Current risk: table density, sticky header, numeric alignment, trust metadata and export parity are not standardized across all analytics tables.

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

- Add Playwright or story/screenshot checklist for sidebar, global header, analytics dashboard, toolbar modal and key table pages in dark/light themes.

### P-UI-06 - Global command header system

Problem:

- Header now has premium styling, but the application still lacks full top-level command UX.

Recommendation:

- Add global command/search launcher.
- Add route-aware breadcrumbs for dynamic/detail routes.
- Add notification/action inbox for worker/backend/analytics warnings.
- Add user/account/store context if authentication/account context is added.

## Recommended execution order

1. P-UI-05 screenshot/regression harness or manual screenshot protocol.
2. P-UI-06 global command header system design.
3. P-UI-01 menu IA, because it affects analytics navigation first impression.
4. P-UI-02 shared control bar.
5. P-UI-03 shared analytics table system.
6. P-UI-04 command center redesign.

## Validation status

Tests were not run in this GitHub connector session.

Manual visual verification is still required before calling the UI premium work complete.
