# Shoe Type Sales screen audit — 2026-09-25 (grok)

Task ID: shoe-type-sales-screen-audit-2026-09-25
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-25
Agent/tool: Grok (executor, Ivan's local worktree)
Delivery target: local `main` only (owner instruction: local commits, never push)
Main commit SHA: pending - local commits on `main` (parent `e49628db`); not pushed
Main verification: pending - not pushed
Evidence state: pending

## Scope

„Prodaja po tipu obuće“, route `/analytics/shoe-type-sales-stats` (`App.tsx:106`, `routes/analyticsRouteDefinitions.ts:46`, nav `layout/navConfig.ts:168`). No legacy redirect leads into this route; the only outbound deep link is the generic detail `/analitika/shoe-type-sales-stats/<id>` (`ShoeTypeSalesStatsPage.tsx:760-787`). Sources read at `e49628db`: `ShoeTypeSalesStatsPage.tsx` (all 1681 lines), `services/shoeTypeSalesStatsApi.ts`, `utils/shoeType*.ts`, `utils/analyticsFormatters.ts`, `components/analytics/AnalyticsTrustHeader.tsx`, `Api/Endpoints/AllEndpoints.cs:2093-2815` (endpoint), `Api/Services/AnalyticsDetailReadService.cs` (previous-range builder), `Api/Services/DailySalesStatsService.cs` (cross-screen reconciliation), `Api/Services/AccessImportService.cs:10100-10115` (sale timestamp kind).

Existing coverage checked first and referenced, not duplicated: `.ai/runs/2026-09-22-shoe-type-sales-audit-evidence.md` (RQ375-RQ377, DONE), `.ai/runs/2026-09-22-supplier-shoe-type-value-correctness-audit-evidence.md`, `docs/qa/SUPPLIER_SHOETYPE_INDEPENDENT_ORACLE_MANIFEST_2026-09-25.md` (RQ412 raw-fact oracle), `docs/qa/OPERATIONS_UNDOCUMENTED_FINDINGS_2026-09-25.md` (RQ441/RQ442), `docs/ai/OPERATIONS_AUDIT_PROMPTS_2026-09-{18,21,23,25}.md` (RQ307, RQ318-RQ321, RQ325, RQ329, RQ337, RQ340, RQ345, RQ346, RQ350, RQ358, RQ435, RQ436).

## Live verification

Not available. `https://trendplus.vercel.app/api/analytics/shoe-type-sales-stats` returns the SPA HTML (HTTP 200 `text/html`), no local API answers on `127.0.0.1:5000/5080/7000/8080`, and there is no usable local PostgreSQL login. Every verdict below is verified by code and tests only; no production numbers are quoted.

## Backend population (all indicators)

- Rows: `prodaja_stavke` ⋈ `prodaja_zaglavlje` ⋈ `Artikli`, `DatumProdaje >= fromUtc && <= toUtc` (`AllEndpoints.cs:2305-2313`), optional `IDObjekat`, data scope by `Artikli.DataOrigin` (`:2144-2154`, `:2312-2313`).
- Bucket: sale-time `ShoeTypeIdAtSale` (`:2324`, `:2375-2376`); name from `TipoviObuce`, missing id/blank name → „Nepoznato“ (`:2265-2270`, `:2420-2422`); unknown is decided by name (`:2506-2508`, `:2563`).
- Revenue `Σ Kolicina × Cena` (signed, returns net out), quantity `Σ Kolicina` (`:2317`, `:2338-2339`); `Cena` is the stored line unit price, the same basis as Supplier and Daily Sales.
- Season override replaces the requested dates with `[DatumOd 00:00Z, DatumDo+1d − 1 tick]` (`:2156-2170`); `sezone` are serialized as UTC midnight (`:2367-2368`), so the page's `toDateOnly` season mapping does not shift a day.
- Previous period: `BuildComparablePreviousRange` (`:2121-2139`), inclusive-tick duration; totals previous revenue sums all previous buckets, including types without current sales (`:2279-2302`, `:2711-2717`).

## Indicator table

| # | Indicator | Formula / source | Verdict |
|---|---|---|---|
| 1 | KPI „Ukupan promet“ | `totals.ukupanPromet` = Σ rows `Math.Round(Σ Kolicina×Cena, 2)` incl. „Nepoznato“ (`AllEndpoints.cs:2433`, `:2499`, `:2675`; page `:1068-1071`) | Correct for the population. Caveats: inclusive `23:59:59Z` end (`RQ442`); `DUG`/`KOREKCIJA` receipts included while Daily Sales excludes them (`RQ447`). |
| 2 | KPI „Ukupno prodato“ | `totals.ukupnaKolicina` = Σ signed `Kolicina` (`:2338`, `:2695`; page `:1072-1075`) | Correct; same caveats as #1. |
| 3 | KPI „Ukupna nabavna vrednost“ | Σ `MarginAccumulator.TotalCost` (`:2403-2412`, `:2448`, `:2677`) | Correct; tooltip says cost only for revenue with cost. |
| 4 | KPI „Ukupan maržni doprinos“ + quality badge | Σ row MC incl. unknown (`:2676`); badge `MarginQualityClassifier.Classify` on total coverage (`:2655-2671`, `:2688-2691`) | Correct. |
| 5 | KPI „Prosečna marža“ | `ResolveWeightedMarginPct` over known types only (`:2542-2547`, `:2678`; RQ375 contract) | Value correct per contract; tooltip claimed the full total MC basis → fixed in `RQ446`. |
| 6 | KPI „PoP trend prometa“ | `(totalRevenue − previousPeriodRevenue) / previousPeriodRevenue × 100`, null when base ≤ 0 (`:2715-2717`; page `:560`, `:1102-1105`) | RQ443 pattern absent (previous-only types are in the base). **Defect:** with the page's `T23:59:59Z` end the previous window starts at `00:00:00.9999999` of its first day, so midnight-stamped sales of that day are excluded (see H1) → routed to `RQ442` addendum. |
| 7 | Trust header „Period“ | `periodFrom/periodTo` = raw `data.fromDate/toDate` (page `:973-974`) formatted in local time (`analyticsFormatters.ts:61-70`) | **Defect:** end `…T23:59:59Z` renders as the next day in Europe/Belgrade (RQ444 pattern) → fixed in `RQ445`. |
| 8 | Trust header source / status | `Sales facts analytics (scope: …)` (page `:977`); status `meta.dataQualityStatus ?? headerDataQualityStatus` (`:680-697`) | Status logic correct; English source string and raw scope already in `RQ325`. |
| 9 | Quality notes | backend `dataQuality.*SharePct`, all over total revenue incl. unknown (`:2510-2540`; page `:640-678`) | Correct. |
| 10 | Concentration chart | backend `sharePct = row / totalRevenue × 100` (`:2555-2558`), top 6 + „Ostali“ = Σ remaining valid shares (page `:562-581`) | Numbers correct; top-N + Ostali = 100 % ± rounding while all shares are in [0,100]; rows with negative revenue are dropped (shown as N/A). Legend/tooltip showed the raw key `sharePct` → fixed in `RQ446`. |
| 11 | Margin comparison chart | top 8 by revenue; share mode when total MC > 0, value mode otherwise (`utils/shoeTypeMarginComparison.ts:69-103`; page `:583-590`, `:1143-1204`) | Correct. |
| 12 | Column „Pokriće artikala %“ | `brojArtikalaSaNivelacijom / brojArtikalaUkupno` (page `:469-472`; backend `:2435-2436`, nivelacija up to `toUtc`, store rows plus global rows `:2251-2263`) | Correct; column placed before the name (layout only). |
| 13 | Columns Promet / Količina / Nabavna / MD | row fields (`:2433-2448`) | Correct. |
| 14 | Column „Udeo u prometu“ | backend `sharePct`; InfoTip „svih prikazanih tipova“ | Correct (unknown is a displayed row and part of the denominator). |
| 15 | Column „Marža %“ + „marža“ pill | `MarginPct` only when `RevenueWithCost > 0` (`:2444-2446`) | Correct; detail uses signed format while the table does not (`RQ448`). |
| 16 | Column „PoP trend“ | per type, null when previous ≤ 0; „Novo“ when previous ≤ 0 and current > 0 (`:2473-2475`; page `:282-304`) | Formula correct; affected by H1; „Novo“ text is wrong for a negative previous base (`RQ448`). |
| 17 | Column „Uticaj nivelacije“ | comparable-cohort split policy (`:2413-2419`, `:2479`; RQ376) | Correct. |
| 18 | Column „Preporuka“ + reason chip | `AnalyticsDecisionRecommendationEngine` with weighted known baseline (`:2552-2587`) | Correct per RQ375/RQ377. |
| 19 | Rank badges #1-#3 | index in the current sort (page `:1371-1381`); default sort is status | Misleading when not sorted by revenue (`RQ448`). |
| 20 | Detail „Udeo u maržnom doprinosu“ | `MC / totalMC × 100`, kept for a negative total (`utils/shoeTypeMarginComparison.ts:37-53`, deliberately tested) | With mixed signs the sign inverts (+500 / −1 000 → −50 %) → decision `RQ449`. |
| 21 | Detail „Udeo u količini“ | `resolveShoeTypeQuantitySharePct` (`shoeTypePercentRange.ts:36-41`) | Correct (N/A for negative/over-total). |
| 22 | Detail previous revenue/units, PoP units | row fields (`:2467-2478`) | Correct; affected by H1. |
| 23 | Detail cost-quality cards | row coverage fields (`:2449-2456`) | Correct; ASCII copy at `:1602` (`RQ325`). |
| 24 | Toolbar metadata / status counts | all rows incl. unknown (page `:594-601`, `:719-758`) | Correct. |
| 25 | Table rows vs totals | every current bucket is a row; previous-only types have no row | Totals reconcile with rows for the current period; previous-only types are invisible in the table while counted in the KPI base (`RQ449`). |
| 26 | Cross-screen totals | Supplier Sales uses the same joins/filters/formula (bucket `SupplierIdAtSale`) → same totals for the same filters; Daily Sales uses `[from, nextDay)` and excludes `DUG`/`KOREKCIJA` receipts (`DailySalesStatsService.cs:24-25`, `:48-57`, `:232`) | Not reconcilable with Daily Sales until `RQ442` and `RQ447`. |

## Filters, URL state, UI

- Presets 30/90/180/365 days and date inputs clear `sezonaId` (page `:789-797`, `:898-926`), so the RQ444 sticky-season pattern does not exist here. Store change keeps the season (`:951-955`).
- Only the sort is in the URL (RQ350); period, season and store are lost on reload or share → existing `RQ318`. Store-list failure silently shows an empty list (`:417-427`) → existing `RQ321`. Dead truncation label (`:1228-1231`) → existing `RQ329`.
- Loading overlay, stale warning, blocking error with retry and empty/out-of-window hints are present (`:1012-1040`, `:613-638`); errors are allowlisted (RQ435).
- Sorting: nulls sort as `-9999`/`-1` and „Nepoznato“ is not pinned last (`:499-531`) → `RQ448`.
- Copy: `:1027` „ucitane“, `:1602` „troska … troskovi … ukljuceni“, sort markers `^`/`v` (`:219-222`) → `RQ325` addendum (raw scope codes and `Sales facts analytics` are already listed there).

## Bugs by severity

- **H1 (High, routed):** previous-period window drops the first day's midnight-stamped sales. The page sends `toDate=…T23:59:59Z` (`:178-182`), so `inclusiveDurationTicks` is N days − 1 s + 1 tick (`AllEndpoints.cs:2130`) and `previousFromUtc` becomes `(from − N days) 00:00:00.9999999` (`:2136-2137`). Access-imported sales are stored with `DateTimeKind.Utc` date values (`AccessImportService.cs:10106-10112`), i.e. at `00:00:00Z`, so for date-only sales the previous period covers N − 1 days: the 30-day preset compares 30 days with 29 (PoP biased upward by about 3.4 points at flat sales), and types that sold only on that day show „Novo“. The same builder exists for Supplier (`:1123-1141`), Color (`:2861-2879`) and the detail service (`AnalyticsDetailReadService.cs:677-695`). Owner: `RQ442` (addendum with this counterexample).
- **M1 (Medium, fixed `RQ445`):** trust header end date +1 day in UTC+ zones.
- **M2 (Medium, routed `RQ447`, needs decision):** Shoe Type/Supplier/Color include `DUG`/`KOREKCIJA` receipts that Daily Sales excludes.
- **L1 (Low, fixed `RQ446`):** concentration legend/tooltip raw key `sharePct`; „Prosečna marža“ tooltip omitted the known-types-only basis.
- **L2 (Low, routed `RQ449`, needs decision):** margin-contribution share sign with a negative total; previous-only types invisible; unknown identity (orphan ids and a real type literally named „Nepoznato“ merge into unknown by name).
- **L3 (Low, routed `RQ448`):** rank badges follow the current sort; nulls/unknown ordering; „Novo“ for a negative base; signed vs unsigned Marža %.
- Existing owners confirmed, not duplicated: `RQ442` (end boundary), `RQ318` (URL state), `RQ321` (store failure), `RQ329` (truncation label), `RQ325` (copy).

## Fixes

- `RQ445` — trust header receives calendar dates of the effective range (`toDateOnly`), falling back to the requested filters.
- `RQ446` — concentration series named „Udeo u prometu %“; „Prosečna marža“ tooltip states that the „Nepoznato“ row is excluded.

## Validation

See the `RQ445`/`RQ446` run logs and completion notes. Focused baseline before changes: 5 files / 80 tests passed.
