# 2026-09-28 Supplier report + Pilot intake audit evidence (grok)

- Date: 2026-09-28 (Europe/Belgrade)
- Requested by: Ivan Jovičić: „Analiziraj ekrane i podatke na njima do detalja `/analytics/supplier/report` i `/analytics/reports/pilot-intake` … napiši promptove u queue.“
- Scope: **audit only**. No product code was changed. Output: this run log, new prompts `RQ461`–`RQ468` in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`, addenda to `RQ137`, `RQ325`, `RQ79` (`docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_EXECUTIVE_DQ_ADDENDUM.md`) and `RQ46` (`docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_UI_TABLE_CHART_ADDENDUM.md`), and the `MASTER_ROADMAP.md` RQ row.
- Base: `main` = `origin/main` = `c5a1937f92cc363e03c4f067b4955875a531d515`. All `file:line` references below are at this commit.
- Delivery: local commits only (private index + CAS `update-ref`); nothing pushed.

## Existing coverage checked (referenced, not duplicated)

- `RQ137` (PARTIAL, period lineage incl. supplier report and pilot intake): its acceptance says no report may present query-generation time as the last successful refresh. Pilot intake still does this (addendum).
- `RQ79` (WAITING): pilot durable ratio `0.####` unit. Live evidence was added as an addendum.
- `RQ46` (WAITING): export trust metadata. Supplier/pilot CSV evidence was added as an addendum.
- `RQ325` (DONE): Operacije-only copy. A routing bullet sends the Supplier report / Pilot intake copy to `RQ468`.
- `RQ456` (DONE): canonical `DUG`/`KOREKCIJA` exclusion applies to Daily/Supplier/Shoe Type/Color only. It does not cover the supplier decision views or pilot counts (`RQ464`, `RQ467`).
- `RQ234`/`RQ235`/`RQ236`/`RQ249`/`RQ250`/`RQ458`/`RQ459` (DONE): supplier-decision filter fidelity, action gating, empty-state and parity work. `RQ236`'s "no zero-filled report" intent is violated again by the negotiation-pack zero fill (`RQ461`).
- `docs/ai/PRODUCTS_SUPPLIER_AUDIT_PROMPTS_2026-09-25.md` `PS08`: the live DB is missing the supplier decision MVs (`MISSING_SCHEMA`). This is confirmed live below and referenced from `RQ461`, not duplicated. `PS01`–`PS18` are still unregistered proposals.
- `docs/ai/SUPPLIER_DECISION_HUB_AUDIT_PROMPTS_2026-09-22.md` and `docs/qa/OPERATIONS_UNDOCUMENTED_FINDINGS_2026-09-25.md`: no overlap with the report-renderer or pilot-intake findings.
- Access control: the API has no authentication/authorization policy on these endpoints (`Program.cs` only calls `app.UseAuthorization()`; nothing calls `RequireAuthorization`). This is app-wide and owned by `docs/security/ANALYTICS_ACCESS_CONTROL_IMPLEMENTATION_PLAN.md` / STAB. No new prompt.
- Id collision note: this agent's earlier `RQ445`–`RQ449` ids were reused by a concurrent agent; that content was absorbed or renumbered as `RQ456`/`RQ457` (DONE). No `RQ445`/`RQ446` DONE sync was possible or needed in this run.

## Live verification (read-only GET, 2026-09-28 ~10:54–10:58 CEST)

- API base found in the Vercel bundle: `https://trendplus-api.onrender.com`. `trendplus.fly.dev` hangs. Vercel `/api/*` returns SPA HTML.
- Supplier report `GET /api/analytics/reports/supplier-decision`:
  - The default request, and requests with windows 2026-06-30..09-27 (90 days), 2025-10-01..12-29 (historic 90 days), 2026-08-01..09-14 (45 days) and 2026-04-01..09-27 (180 days), all return HTTP 200 with `meta.success=false` and `MISSING_SCHEMA`. The message embeds the raw dataset code, e.g. „… za period all_time nije spreman …“ and „… 90d …“. The production report is unavailable (`PS08`).
  - 2026-06-01..06-30 (historic): the error report's period label is „Poslednjih 30 dana“.
  - `fromDate > toDate`: HTTP 400 ValidationProblem with English text "One or more validation errors occurred." / "fromDate must be earlier than or equal to toDate." The UI prefixes it with „Greska pri ucitavanju trajnog supplier report-a:“.
  - `dataScope=existing` (the live SQL path) returns success with 0 suppliers and `emptyReason=no_data_in_period`.
    - Without dates the effective period is 2026-04-01..2026-09-28 (180-day default lookback), but it is labelled „Celokupna istorija“/`all_time`, and `provenanceBasis=mv_supplier_decision_score_cache` even though no MV was read.
    - Payload row sections are `Header`, `Status`, `Upozorenja`, `supplier_negotiation_pack` (zero-filled: „Dobavljač: Nije određeno“, „Prihod 0“ …), `Preporučene akcije`, `Metodologija`.
    - Filters are raw `all`/`existing`/`false`. Metadata carries `dataFreshnessStatus`, booleans as `False`/`True`, ISO `O` timestamps and an empty `lastRefreshAtUtc`.
- Pilot intake `GET /api/analytics/reports/pilot-intake`:
  - Default period 2026-08-30..2026-09-28:
    - readiness 42 „Kritično — preporuke nisu bezbedne“;
    - 12422 articles, 0 sale items, 0 receipts;
    - missing cost 1087, blocked 1087;
    - `revenueWithoutCost` null (unit `ratio`);
    - 12422 without category (the source category is empty; data issue, not a code bug);
    - 656 ignored rows (latest batch 42, completed 2026-08-12; global, not period-filtered);
    - 12422 insufficient signals.
  - `lastRefreshAtUtc` equals `generatedAtUtc` to within about 0.1 s: query time is presented as refresh time.
  - Rows are ASCII/English („Naziv izvestaja“, „Scope“, „Readiness score“, „Ucitano“, „Racuni“, „Bez dobavljaca“, „Preporucene akcije“, „Batch id“, „Scope importa“, „n/a“). Actions are static even when the counts are 0.
  - 2026-07-14..2026-08-12: 195 sale lines, 19 receipts, observed 07-14..08-05; still 42/critical (insufficient signals 12326/12422); `revenueWithoutCost` still null because the health window is relative to now.
  - `scope=imported` returns numbers identical to `all`. `scope=bogus` is accepted and served as report `all`.
  - An invalid period returns HTTP 200 with `meta.error=invalid_period` and a Serbian message (correct).

## Tests run (read-only, worktree at `c5a1937f`)

- Vitest (focused): 7 files / 52 tests passed (`SupplierDecisionReport*`, `supplierDecisionReport*`, `PilotIntakeReportPage`, `PilotDataQualityIntakeReport` specs). They do not catch the defects: the fixtures use client-shaped rows, and the page spec mocks `PilotDataQualityIntakeReport`.
- `dotnet test Api.Tests` with filter `SupplierDecisionHubContractTests|SupplierDecisionSchemaSqlTests|PilotIntake|DataQualityEndpoints`: 72/72 passed (2026-09-28 11:00–11:01 CEST). `SupplierDecisionHubContractTests.cs:241-261` encodes a June 2026 range as a "30d" fallback, i.e. the length-only window mapping (`RQ463`).

## Screen 1 — `/analytics/supplier/report`

Route: `pages/SupplierDecisionReportPage.tsx` → `components/analytics/SupplierDecisionReport.tsx` (+ `SupplierDecisionReportActions.tsx`). Data: `GET /api/analytics/reports/supplier-decision` → `SupplierDecisionHubEndpoints.cs` (`BuildSupplierDecisionReportAsync` → precomputed MV path, `mv_supplier_decision_score_cache_{30d,90d,180d}` + `mv_supplier_markdown_dependency_cache`, or live SQL for category/gender/season/store/dataScope≠all/excludeOos).

| Element | Source | Verdict | Evidence |
|---|---|---|---|
| Production availability | MV path | bug (env) | Live `MISSING_SCHEMA` for every window → `PS08` |
| Header „Dobavljač“ | row `Header/Dobavljač` expected; backend emits filter `supplier` | bug | component `:183-204`; backend `:1760` emits `all` or a numeric id |
| „Opseg podataka“, „Datum izveštaja“, „Poslednje osveženje“ | Header items not emitted by the backend | bug | component `:194-203`; backend rows `:1668-1742`; raw code / ISO / „-“ |
| Freshness badge | meta `dataFreshness` | bug | component `:206` reads `dataFreshness`; backend emits `dataFreshnessStatus` (`:1797`) |
| „Preporuke: dozvoljene/ograničene“ badge | meta `recommendationAllowed` | bug | `:281` compares `"True" === "true"`, so the badge always says „ograničene“; `:315` parses case-insensitively (inconsistent) |
| Traženi/Efektivni period chips | meta ISO | bug (format) | component `:288-293` shows raw ISO |
| Filter chips | payload filters | bug (copy) | `renderMetaChips :166`; raw `all`/`existing`/`false` (backend `:1758-1770`) |
| KPI cards (Prihod, Prodate jedinice, Maržni doprinos, Pouzdanost signala …) | `BuildSupplierDecisionReportKpis :1115-1139` | bug (format + basis) | `FormatReportValue :1903` gives invariant raw decimals. Revenue/units are the ±30-day markdown-window totals of marked-down articles, not period sales (029 `:369-371`, 018 `:141-175`) → `RQ464` |
| Upozorenja / Preporuke / Top / Rizik / Pojačaj / Smanji / Kvalitet podataka sections | expected names | bug | component `:235-244` expects „Upozorenje“, „Preporuke“, „Top artikli / dobavljači“, „Rizik zalihe“, „Pojačaj“, „Smanji“, „Kvalitet podataka“; backend emits „Upozorenja“, „Top dobavljači“, „Rizik“, „Preporučene akcije“ (`:1701`, `:1730`, `:1737`), so these sections are always empty |
| „Status“ / no-data message | backend `Status` rows | bug | never rendered. A successful empty report shows the zero-filled negotiation pack (`:1486-1530`) instead of an empty state (contradicts `RQ236`); page `:364` only handles a null payload |
| Snapshot confidence/reliability/reason codes/fallback reason | meta keys not emitted | bug | component `:210-217`, `:317-321` |
| Response `sections`/`kpis`/`warnings` | typed DTO | dead | ignored by the page (`:188-205`), which renders legacy rows only |
| Period / window mapping | `GetDecisionScoreWindowDays :2666`, `ResolveRequestedDataset :2688` | bug | length-only: any 31–90-day range, historic included, maps to 90d with no fallback while the MV is a rolling window from refresh (029 `:23-38`); overlap filter only (`:2933`); `BuildEffectivePeriodLabel :2709` returns „Poslednjih N dana“ for historic ranges → `RQ463` |
| No-date live path label | `TryCreateFilters :661-663` | bug | live-confirmed: 180-day lookback labelled „Celokupna istorija“ |
| `provenanceBasis` | `:1799`, `:2809` | bug | MV name even on the live SQL path |
| „Zavisnost od sniženja“, „Kapital/Lager u riziku“ | LEFT JOIN all-history `mv_supplier_markdown_dependency_cache` (`:2894`) | bug | all-history values mixed with windowed revenue → `RQ463` |
| Error report period label | `:928-939` | bug | requested-dataset label (live „Poslednjih 30 dana“ for June) |
| DUG/KOREKCIJA | 018/029/015 views + live SQL | decision | not excluded (RQ456 policy not applied) → `RQ464` |
| Missing cost | 018 `:163-175` COALESCE 0 | decision | inflates pre-markdown margin → `RQ464` |
| `return_rate` source | 029 `:358-362` `povracaj_zaglavlje.id_dobavljac` | decision/unverifiable | supplier returns vs customer returns → `RQ464` |
| Scoring model | MV `confidence_score*100` vs live 0–100 formula (`:3244-3325`); threshold `:2946` vs `:3463` | decision | toggling any live-only filter switches the model → `RQ464` |
| Store filter | `a."IDObjekat"` (`:3402`) | decision | article store, not the sale store → `RQ464` |
| Negotiation pack / „Finalni savet“ | `:1486-1530` | bug/decision | an all-supplier report silently uses the top-revenue supplier → `RQ465` |
| „Kandidat za rast“ / risk fallback | `:766-800` | bug | fallback labels suppliers without an EXPAND/risk code → `RQ465` |
| „Kapital u riziku“ insight | `:815` | bug (format) | raw `0.##` |
| Stable/action URLs | `:1009-1052` | bug | always add explicit dates, which turns all-time into windowed |
| „Dodaj u akcije“ | `SupplierDecisionReportActions.tsx:181-215` | bug | can create a negotiation action without a supplier → `RQ465` |
| „Otvori trajni izveštaj“ on the durable page | Actions | bug (P3) | navigates to the same URL |
| „Ponovo generiši izveštaj“ / „Vrati se“ | page `:352-353`, `:435-436` | bug (P3) | drops filters |
| Export error / status colour | page `:137`, `:393`; Actions `:330` | bug (P3) | `exportError` never cleared; errors use the success colour |
| `section` URL param, `scope ?? dataScope` | backend `:471` | dead/bug (P3) | `section` ignored; `scope` silently wins |
| Client date-order validation | page `:112-131` | bug (P3) | none; backend 400 shows English text |
| CSV export | `supplierDecisionReport.ts:622-634` | bug (P3) | comma separator, no BOM, UTC date in the name, no filters/trust → `RQ46` addendum |
| Summary/copy text | `supplierDecisionReport.ts:240-242`, `300`, `514-525`, `636-689`; backend `:966-967`, `:1078`, `:1396-1410`, `:2196-2236`; component `:535`; `analyticsApi.ts:1177` | bug (copy) | English/ASCII → `RQ468` |
| URL validation for invalid values, loading/error/retry, AnalyticsMetaError, RQ234 filter fidelity, RQ235/RQ249 action gating, browser-preview TTL banner | page/component | correct | code + focused Vitest |
| Access/permissions | API | unverifiable here / app-wide | no auth; owned by the security plan |

## Screen 2 — `/analytics/reports/pilot-intake`

Route: `pages/PilotIntakeReportPage.tsx` → `components/analytics/PilotDataQualityIntakeReport.tsx`. Data: `GET /api/analytics/reports/pilot-intake` → `DataQualityEndpoints.cs` (`BuildPilotDataQualityIntakeReportAsync :502-712`, trendDb articles/sales/import batches + `AnalyticsDataQualityHealthService`).

| Element | Source | Verdict | Evidence |
|---|---|---|---|
| Whole report body (readiness, KPIs, sections, actions, CSV/PDF/XLSX, copy) | page passes `report={null}` (`PilotIntakeReportPage.tsx:397-404`) | **bug P1** | the component returns the empty state whenever `!report` (`:347-356`), so the durable page always shows „Pilot intake izveštaj nema dovoljno podataka“. Broken since `8006a4a6` (2026-05-25); the page spec mocks the component |
| Trust header freshness | page `:370-371` | bug | `lastRefreshAtUtc` comes from the report (query-time fallback), status from refresh-status (mixed sources) |
| `lastRefreshAtUtc` | `DataQualityEndpoints.cs:655` | bug | `?? health.GeneratedAtUtc` presents query time as refresh (live-confirmed; violates the `RQ137` acceptance) |
| Readiness score / label | `ResolveReadiness :1515-1535` | decision | `insufficientSignalCount` = all articles unsold in the period (`:592-594`) forces „Kritično“ at ≥75%; contradicts the methodology bands and double-penalizes → `RQ467` |
| „Blokirane preporuke“ | `:649` | decision | sums overlapping sets of mixed units (articles + sale lines) → `RQ467` |
| „Nula/negativna cena“ | `:590` | decision | counts signed return lines → `RQ467` |
| „Prihod bez cene“ + health penalties | health `CaptureAsync(lookbackDays, …)` `:514`; `DataQualitySalesWindow.Resolve` (health service `:109`) | bug | window relative to now, not the requested period (live null for July) → `RQ466` |
| Scope | `:375-377`, `:514` | bug | `dataScope` ignored for trendDb counts; scope not validated (live `imported` = `all`, `bogus` accepted) → `RQ466` |
| Preporučene akcije | static `PilotIntakeRecommendations :18-26` | bug | shown regardless of counts → `RQ466` |
| Empty meta message | `:657-663` | bug | „nema import batch u periodu“ although batches are not period-filtered → `RQ466` |
| Errors | `catch (Exception)` `:445`, `:314` | bug | swallowed without logging → `RQ466` |
| „Ponovo generiši report“ | page `:332`, `:394` | bug | returns the cached report (HeavyAnalytics 20 min) → `RQ466`/`RQ462` |
| Prodavnice | `:534` | bug (minor) | counts all `StoresDim`, not the stores in the period/filter |
| Store filter | `:518-545` | decision | article `IDObjekat` for articles, header `IDObjekat` for sales → `RQ467` |
| Missing cost | `:562` `NabavnaCena` only | decision | health uses `ps.NabavnaCena ?? a.NabavnaCena` → `RQ467` |
| Default period | `TryResolveIntakePeriod :1600-1617` | decision | last 30 UTC days, unrelated to import coverage → `RQ467` |
| DUG/KOREKCIJA | sales counts | decision | no policy → `RQ467` |
| Durable ratio `0.####`, `n/a`, unit `ratio` | durable rows | bug | → `RQ79` addendum |
| KPI note duplicate „Status/Oznaka spremnosti“ | component `:68-69`, `:126-127` | bug (P3) | duplicate row → `RQ462` |
| Durable sections „N redova“ | component `:422` | bug (P3) | meaningless row counts → `RQ462` |
| Dead helpers | component `:180`, `:191`, `:208` | dead | → `RQ462` |
| CSV filename/BOM/metadata | component `:300` (`toLocaleDateString("sr-RS")`) | bug (P3) | „pilot-intake-28. 9. 2026..csv“, no BOM, no period/scope → `RQ46` addendum / `RQ462` |
| Copy | page `:331-332`, `:351`, `:374`, `:393-394`; component; backend rows/actions; `analyticsApi.ts:1150-1154`, `:1200` | bug (copy) | English/ASCII → `RQ468` |
| Period validation (both dates, format, order → `invalid_period`), half-open sales window, cache keyed by filters, retry buttons, invalid-period UI | backend/page | correct | code + live |
| „Bez kategorije“ 12422 | article data | correct (data issue) | the source category is empty |

## Findings → prompts

| Prompt | Status | Summary |
|---|---|---|
| `RQ461` | READY | Supplier report durable renderer ↔ backend payload contract (sections, header, metadata keys, bool parsing, formatting, empty state) |
| `RQ462` | READY | Pilot intake durable page actually renders the report; dead helpers; trust header; real component spec |
| `RQ466` | READY | Pilot intake backend scope/period/freshness truth, conditional actions, logging, honest empty message and regenerate |
| `RQ463` | WAITING | Supplier report requested-window truth (length-only mapping, historic ranges, no-date label, provenance, markdown/capital window) |
| `RQ464` | WAITING (decision) | Supplier report metric basis vs Supplier overview, DUG/KOREKCIJA, missing cost, return source, single scoring model, store semantics |
| `RQ465` | WAITING | Supplier report actions/negotiation pack/fallback labels and P3 page bugs |
| `RQ467` | WAITING (decision) | Pilot readiness score semantics and default period |
| `RQ468` | WAITING | Serbian copy for both screens (frontend + backend + `analyticsApi.ts`) |
| `RQ137` addendum | PARTIAL | pilot query-time refresh fallback; supplier length-based and all-time labels |
| `RQ79` addendum | WAITING | live ratio/`n/a`/`ratio` unit evidence |
| `RQ46` addendum | WAITING | supplier/pilot CSV metadata/BOM |
| `RQ325` routing bullet | DONE | non-Operacije copy → `RQ468` |

## Owner decisions for Ivan

1. `RQ463`: recompute the requested historic window (new SQL/MV work) or fail closed (label as the rolling window and block recommendations).
2. `RQ464`: the revenue/units/margin basis of the Supplier report vs the Supplier overview, `DUG`/`KOREKCIJA`, missing-cost treatment, the return source and one scoring model.
3. `RQ465`: what an all-supplier report shows in place of a single-supplier negotiation pack.
4. `RQ467`: the pilot readiness definition of an insufficient signal, the blocked-count basis and the default period anchoring.
5. Register `PS01`–`PS18` (especially `PS08`: the production DB lacks the supplier decision MVs, so the Supplier report is unavailable in production).
6. API authentication is app-wide (security plan owner).

## Not changed

- No product code, tests or schema were modified. No push. Worktree `Trendplus2-grok` was only refreshed to `c5a1937f` and used for read-only test runs.
