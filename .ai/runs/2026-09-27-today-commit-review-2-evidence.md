# 2026-09-27 — Today commit review, part 2 (evidence)

- Queue: direct-user-request ("Analiziraj ostale danasnje komitove detaljno i popravi/unapredi")
- Owner: frontend analytics (Operacije pages, Inventory summary) + backend detail labels (`AnalyticsDetailReadService`, RQ325 scope)
- Base: `origin/main` c802592f, isolated worktree `Trendplus2-review2`, branch `review-today-2`

## Commits reviewed

| Commit | Prompt | Verdict |
|---|---|---|
| 95632a21 | RQ321 store-filter load failures | Notice/retry correct. Combined with RQ318, an unverified URL `storeId` stayed applied on Color/Shoe Type/Pre-Post after a store-list failure, while the notice claimed all-store data and the select was locked. Fixed. |
| 074e7b96 / a92f2709 | Daily fail-closed store filter | Correct intent, but `setSearchParams` in the effect deps re-ran store discovery on every URL change. Fixed with a ref. |
| ff85759e | RQ322 Inventory store bootstrap | OK. |
| 0dc0dae3 / d3054028 | RQ323 stale secondary panels | Panels clear correctly. `DecisionSummaryBar` still showed workflow-derived `0` while the workflow was refetching (balance kept, so no skeleton) or never loaded. That is a fake zero. Fixed. |
| 26ccc529 | RQ316 empty reasons | OK: both surfaces use `getAnalyticsMetaMessage`. |
| 86e12599 | RQ317 Pre/Post focus URL | OK. |
| 3ddd4fb5 | RQ318 list filters URL | OK apart from the store interaction above. Draft date edits are re-synced to the URL on sort clicks (RQ320 territory, not changed). |
| 4ecea4b1 | RQ325 residual copy | Frontend OK. Backend Color/aggregated detail labels still exposed `Insufficient data`, `impact`, `snapshot`/`fallback` and ASCII `marza`. Fixed. |
| fdac63ba | CI timeouts | OK. |
| 72073d2e, e7bf0272, e175de5e, 66628eb4, 421c3775, d4e382fb, c3172632 | test stabilisation | OK (tests only). |

Queue defects: the RQ321 section carried a misplaced RQ327 completion note with a wrong SHA (`dc20d7754f…`; the real SHA is `dc20d775ca3c…`). It has been removed. RQ324 was DONE without a completion note, so the note was restored from its run log.

## Changes

- `ColorSalesStatsPage.tsx`, `ShoeTypeSalesStatsPage.tsx`, `ProdajaPrePostNivelacijePage.tsx`: on store-list failure, remove the unverified `storeId` from the URL. The URL sync effect then resets draft and active filters to all stores.
- These three pages plus `DailySalesStatsPage.tsx`: store effect depends only on `storesReloadNonce`. `setSearchParams` is read through a ref.
- `DecisionSummaryBar.tsx`: workflow counts are shown only for a loaded workflow. Otherwise the bar shows `…` while loading and `Nije dostupno` when absent. `InventoryPage.tsx` passes the plain `loading` flag, and the component keeps its own skeleton condition.
- `Api/Services/AnalyticsDetailReadService.cs`: Serbian labels and note text aligned with the existing Supplier/Shoe Type localization map.
- Tests:
  - A store fail-closed spec each for Color, Shoe Type and Pre/Post. The Color spec also asserts a single store request; it failed before the ref fix.
  - `DecisionSummaryBar` specs: measured zero with a loaded workflow, not loaded, and refetch placeholder.
  - New `Api.Tests/AnalyticsDetailLocalizedLabelTests.cs` source-scan test.
- Guardrail baseline: Daily `refetch_setData_null` line 875 → 877 (line shift only).
- Queue: RQ321/RQ323/RQ325 review notes, removal of the misplaced note, RQ324 completion note.

## Validation

- Focused Vitest:
  - Color/Shoe Type/Pre-Post/Daily specs 76/76.
  - `DecisionSummaryBar` + all `InventoryPage.*` specs 91/91.
- `npm run check:analytics-guardrails`: OK (encoding, self-test, baseline-only, `tsc -b`).
- `npm run build`: OK.
- `node scripts/check-prompt-queues.mjs`: OK. `git diff --check`: OK.
- Not run: local `dotnet test` / backend build. Drive C: ran out of space (`ENOSPC`) during the first attempt, and after cleanup only about 0.9 GB was free, which is not enough for a fresh-worktree .NET build. The backend change is string literals plus a source-scan test that mirrors `VendorSalesNivelacijaSafeErrorTests`. Remote backend CI must be inspected.
- A Daily-only regression test for the store re-fetch was discarded because the `getStores(true)` cache made it pass before the fix, so it proved nothing.

## Not done / risks

- Pre/Post keeps a URL `vendorId` applied after a vendor-list failure. The warning is visible and the last known list is preserved. No change was made.
- RQ318 draft date inputs re-sync to the URL on sort clicks, so unapplied draft edits are lost (RQ319/RQ320 product decision).
- 4 stale guardrail baseline entries remain (see part 1 evidence).

## Delivery

- Implementation SHA `8c3693908ec93c3fb74625f3068e3e96def93f73`, pushed directly to `main` (no upstream delta at rebase). `git merge-base --is-ancestor 8c369390 origin/main` returned 0.
