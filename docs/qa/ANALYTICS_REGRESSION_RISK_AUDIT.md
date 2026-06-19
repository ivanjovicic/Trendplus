# Analytics Regression Risk Audit

Date: 2026-06-19 14:04:44 +02:00
Repo: `ivanjovicic/Trendplus`

## Scope

Audited:

- [docs/ai/ANALYTICS_STANDARDS.md](../ai/ANALYTICS_STANDARDS.md)
- [docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md](ANALYTICS_LIVE_SMOKE_RESULT.md)
- [ProductDecisionCenterPage.tsx](../../Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx)
- [ExecutiveDecisionBoardPage.tsx](../../Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx)
- [AnalyticsActionsPage.tsx](../../Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx)
- [PilotReadinessPage.tsx](../../Klijent/clientapp/src/pages/PilotReadinessPage.tsx)
- [InventoryPage.tsx](../../Klijent/clientapp/src/pages/InventoryPage.tsx)
- supplier analytics pages in `Klijent/clientapp/src/pages`
- shared analytics API / meta / error helpers in `Klijent/clientapp/src/services` and `Klijent/clientapp/src/utils`

Search focus:

- `|| 0`
- `?? 0`
- `catch` branches that clear lists
- empty-response success assumptions
- stale/partial states being ignored
- `warningCodes` being ignored

## Findings

| Surface | Risk pattern | Result | Action |
| --- | --- | --- | --- |
| [ProductDecisionCenterPage.tsx](../../Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx) | `catch` previously cleared store/supplier options with `[]` | Fixed in this task. The page now preserves the last known option list on transient failures instead of faking an empty filter set. | Tiny fix applied. |
| [SupplierConsolidatedPage.tsx](../../Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx) | `catch` previously cleared store/supplier options with `[]` | Fixed in this task. Existing options remain visible if refresh fails. | Tiny fix applied. |
| [SupplierFootwearAnalyticsPage.tsx](../../Klijent/clientapp/src/pages/SupplierFootwearAnalyticsPage.tsx) | `catch` previously cleared vendor options with `[]` | Fixed in this task. Existing options remain visible if refresh fails. | Tiny fix applied. |
| [SupplierSalesStatsPage.tsx](../../Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx) | `catch` previously cleared store options with `[]` | Fixed in this task. Existing options remain visible if refresh fails. | Tiny fix applied. |
| [InventoryPage.tsx](../../Klijent/clientapp/src/pages/InventoryPage.tsx) | `catch` previously cleared supplier options with `[]` | Fixed in this task. Existing options remain visible if refresh fails. | Tiny fix applied. |
| [SupplierDecisionHubPage.tsx](../../Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx) | `catch` previously cleared season options with `[]` | Fixed in this task. Existing options remain visible if refresh fails. | Tiny fix applied. |
| [ExecutiveDecisionBoardPage.tsx](../../Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx) | `warningCodes` ignored / stale ignored | No issue found. `warningCodes` are preserved, combined with `reasonCodes`, and rendered into cards/sections. Stale and partial states are surfaced through the board model. | No code change. |
| [AnalyticsActionsPage.tsx](../../Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx) | success assumed from empty response | No issue found. The page uses `meta`, summary warnings, and explicit empty/error states rather than treating empty data as success. | No code change. |
| [PilotReadinessPage.tsx](../../Klijent/clientapp/src/pages/PilotReadinessPage.tsx) | stale ignored / empty assumed success | No issue found in the audited paths. Readiness cards use explicit warning/blocked states and partial/error metadata. | No code change. |
| `?? 0` / `|| 0` usage across audited analytics pages | fake-zero concern | Mostly intentional display defaults for already-loaded numeric payloads or derived labels. I did not find a high-confidence fake-success bug in these audited surfaces. | Follow-up audit only. |

## Tiny Fix Applied

The only code change made from this audit was to stop clearing already loaded option lists on transient load failures. That keeps the UI honest by avoiding a fake empty filter state while still letting the main analytics surfaces remain usable.

## Remaining Follow-Ups

- Add visible warning banners when ancillary filter/list refreshes fail, so users know an option list may be stale instead of only preserving the previous data.
- Continue the broader numeric fallback review on other charts and derived panels that were not part of this audit pass.

## Verification

- `git diff --check` - pass
- `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
- `cd Klijent/clientapp && npm run build` - pass

