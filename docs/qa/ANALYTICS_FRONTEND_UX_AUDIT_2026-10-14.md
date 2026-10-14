# Analytics frontend UX audit - 2026-10-14

Base: `origin/main` `a159d579`. Scope: `Klijent/clientapp` analytics pages and shared analytics/inventory/supplier components (117 files). Method: targeted static scans (English labels, locale-less or `en-US` formatting, visible `toFixed`, mouse-only click targets, `key={index}`, `target=_blank` without `rel`, date parsing), manual review of hits, and de-duplication against P-UI-*, RQ651-RQ657 and the 2026-10-04 UX/responsive audits.

## Fixed now (RQ668)

| Finding | Evidence | Fix |
|---|---|---|
| Dashboard momentum/elasticity used an incomplete previous week (e.g. 10 days of data compared 7 days against 3) | `AnalyticsDashboard.tsx:157-178` | Requires two full 7-row windows, otherwise `Nije dostupno` |
| Unknown momentum shown with green "up" tone | `AnalyticsDashboard.tsx:2742-2746` | Neutral `trend` class when unknown |
| English `Reset` button | `SupplierConsolidatedPage.tsx:516` | `Resetuj filtere` |
| Coverage ratio with dot decimal (`1.50x`) | `SKUDetailModal.tsx:136` | `sr-RS` (`1,50x`) |
| Insight Studio quadrant filters mouse-only | `InsightStudioPage.tsx:1211-1230` | `button` + `aria-pressed` |

## Registered prompts

- **RQ669** (WAITING, P3): Insight Studio residual a11y/i18n/format; waits for the owner reachability decision.
- **RQ670** (READY, P3): `sr-RS` number formatting and residual English labels on operational/admin screens.
- **RQ671** (READY, P3): 120 ESLint errors on main, lint not gated in CI.

## Checked and not a defect

- Modal/drawer backdrops with `onClick` (`KpiExplainButton.tsx:70`, `AnalyticsDetails.tsx:557`, `SupplierDetailDrawer.tsx:118`, `LogsPage.tsx:499`) already have a keyboard close path. `ProductDecisionCenterPage.tsx:2031` row click has a disclosure button (`:2089`, P-UI-50).
- `target="_blank"` links already carry `rel` on the following line.
- `new Date(fromDate) > new Date(toDate)` range checks compare ISO date strings in the same zone, so they are correct.
- `Intl.DateTimeFormat("en-US", { timeZone: "Europe/Belgrade" })` in `ProductDecisionCenterPage.tsx:250` and `inventoryUtils.ts:669` is used only for `formatToParts`, not display.
- `toFixed` in CSV export rows (`AnalyticsDashboard.tsx:1564-1568`) is intentional machine format.
- `key={index}` hits are static skeletons/chart cells without reordering.

## Already owned elsewhere

- Swallowed loader errors, fake-zero UI and request races: RQ655, RQ656, RQ657.
- Responsive/phone layout and small text: P-UI-38/39/41/46 and the 2026-10-04 responsive re-audit.

## Verification

- Focused Vitest 47/47; `npm run test:analytics` 161 files / 1225 tests passed.
- `npm run typecheck`, `npm run build`, `check:analytics-guardrails` (+ self-test), `check:ui-ratchets`, `check:analytics-chart-accessibility`, `check:encoding` passed.
- ESLint: no new errors on changed lines; the touched files carry 14 pre-existing errors (RQ671).
