Task ID: post-commit-react-csharp-review-6
Queue: none (direct user request)
Date: 2026-08-13
Agent/tool: Cursor
Model: unknown-not-exposed
Delivery target: main
Main commit SHA: pending-push
Main verification: pending-push

## What was done
- Reviewed leftover React/C# gaps from earlier unreviewed commits after `08d8897`: Decision Timeline `117dbda`/`e2aa93a` still dumped `Porodica: REPLENISH` from `scopeExplanation`; Daily Sales chrome `5997fc0` still used a raw error div; Pre-nivelacija `1d0561e`/`d6eadf4` still invented Visoko/Srednje/Nisko bands; Prodaja pre/post still treated missing reliability as `Nisko`.
- Product Decision Center now composes timeline scope from structured `sourceKey` / `recommendationType` / period fields, with `Dopuni` instead of raw `REPLENISH`. Backend `scopeExplanation` stays for audit.
- Daily Sales failures use `AnalyticsErrorState` with retry and hide KPI/table chrome on error.
- Pre-nivelacija shows backend `fmtPct` for available reliability. Prodaja pre/post no longer maps omitted reliability onto a weak `Nisko` band.

## Files changed
- Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx
- Klijent/clientapp/src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx
- Klijent/clientapp/src/pages/DailySalesStatsPage.tsx
- Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx
- Klijent/clientapp/src/pages/PreNivelacijaPriorityPage.tsx
- Klijent/clientapp/src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx
- Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx
- Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.spec.tsx
- .ai/runs/2026-08-13-post-commit-react-csharp-review-6-evidence.md

## Validation run
- cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/ProductDecisionCenterPage.confidence.spec.tsx src/pages/__tests__/PreNivelacijaPriorityPage.spec.tsx src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx src/pages/ProdajaPrePostNivelacijePage.spec.tsx -> pass (27/27)
- cd Klijent/clientapp && npm run check:analytics-guardrails -> pass

## Validation not run
- npm run build -> not run - types covered by analytics guardrails typecheck
- dotnet test -> not run - no C# runtime change

## What was missed
- Prodaja pre/post still uses local 70/40 bands for available reliability so the existing `Nisko poverenje` filter keeps working.
- Daily Sales empty table still uses an in-table "Nema podataka" row instead of `AnalyticsEmptyState`.
- Backend timeline `ScopeExplanation` and English gap `message` strings are unchanged; UI no longer displays those raw strings.

## Risks
- Pre-nivelacija reliability pills no longer use green/amber/red bands; operators now see the percent itself.
- Missing reliability on Prodaja pre/post is a `watch` tone, so those rows are not counted in the local `Nisko poverenje` filter.

## Next
- Push to origin/main.
