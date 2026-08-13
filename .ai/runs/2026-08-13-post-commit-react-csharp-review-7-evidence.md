Task ID: post-commit-react-csharp-review-7
Queue: none (direct user request)
Date: 2026-08-13
Agent/tool: Cursor
Model: unknown-not-exposed
Delivery target: main
Main commit SHA: pending-push
Main verification: pending-push

## What was done
- Reviewed leftover React/C# gaps after `7b758d5`: Daily Sales chrome `5997fc0` still used an in-table empty row instead of `AnalyticsEmptyState`; Actions polish `8bfa7c3` still dumped raw `REPLENISH` / `replenish` into the list and ledger details.
- Daily Sales empty success now uses shared empty state, including a jump-to-available-data action when the backend reports a data window, and no longer duplicates the old no-data banner.
- Analytics Actions maps recommendation status/type to operator labels (`Dopuni`) so raw family codes are not shown.

## Files changed
- Klijent/clientapp/src/pages/DailySalesStatsPage.tsx
- Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx
- Klijent/clientapp/src/pages/AnalyticsActionsPage.tsx
- Klijent/clientapp/src/pages/AnalyticsActionsPage.spec.tsx
- .ai/runs/2026-08-13-post-commit-react-csharp-review-7-evidence.md

## Validation run
- cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx src/pages/AnalyticsActionsPage.spec.tsx -> pass (10/10)
- cd Klijent/clientapp && npm run check:analytics-guardrails -> pass

## Validation not run
- npm run build -> not run - types covered by analytics guardrails typecheck
- dotnet test -> not run - no C# runtime change

## What was missed
- Prodaja pre/post still uses local 70/40 bands for available reliability so `Nisko poverenje` keeps working.
- Analytics Actions ledger still shows some raw driver/warning codes and English snapshot prose when the backend stores them that way.
- Unused `.daily-sales-no-data-banner` CSS remains after the banner was replaced by EmptyState.

## Risks
- Empty Daily Sales still renders KPI zeros under the empty state, matching Color/ShoeType siblings (empty is not error).
- Unknown recommendation types fall back to underscored-to-spaced text.

## Next
- Push to origin/main.
