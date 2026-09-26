# Odluke o proizvodima i Pregled dobavljača — audit nalaza i fix promptovi

Datum: 2026-09-25

Queue owner: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` (promptovi iz ovog fajla još NISU upisani u queue — vidi „Registracija u queue“)

Queue: `direct-user-request` (lokalno, bez remote-a i bez cloud agenata)

Osnova: `main` @ `d8d3771f` (lokalno ispred `origin/main` za 3 commit-a)

Površina: `/analytics/products` („Odluke o proizvodima“) i `/analytics/supplier` („Pregled dobavljača“, tabovi overview / scorecard / assortment); rute `Klijent/clientapp/src/App.tsx:102-105`, meni `src/layout/navConfig.ts:138-139`, `src/routes/analyticsRouteDefinitions.ts:28-35`

## Sažetak

Kombinovani audit (statički read-only pregled koda + live UI pregled https://trendplus.vercel.app) pronašao je da ekran Odluke o proizvodima trenutno ne može da da nijednu izvršivu preporuku: `HasCompleteJournal` je hardkodovan na `false`, pa je svaki red „Blokirano — nedovoljno dokaza“ (potvrđeno i live). Provera statusa akcija šalje do 1.200 stavki, a backend prihvata najviše 1.000 (potvrđeno u kodu, live HTTP 400). Pretraga se ne šalje backendu (regresija `RQ200`). Marža proizvoda se deli ukupnim umesto cost-covered prihodom i koristi drugačiji izvor nabavne cene nego ekran dobavljača. Pravila MARKDOWN/DO_NOT_ORDER praktično su nedostižna.

Na Pregledu dobavljača live endpoint `supplier-sales-stats` vraća HTTP 503, a UI ne razlikuje grešku od praznog stanja. Preporuke dobavljača se tvrdo blokiraju kada nema pre/post nivelacija signala, a top-level `recommendationAllowed` je praktično uvek `false` čim postoji nepoznati dobavljač. Scorecard (90d) i Assortment tab pokazuju greške koje ukazuju na nedostajuću precomputed šemu u live analitičkoj bazi (hipoteza). Tu su i P2/P3 nalazi o imeniocima udela i PoP, URL stanju, layoutu, datumima/lokalizaciji, copy-ju i izvozu.

Ukupno 43 nalaza (C1-C33 iz koda, L1-L10 iz live UI-ja) grupisano je u 18 self-contained promptova `PS01`-`PS18`: 8 × P1, 7 × P2, 3 × P3. Hipoteze su eksplicitno označene sa **Hipoteza**.

## Ekrani

| Ekran | Ruta | Fajl |
|---|---|---|
| Odluke o proizvodima | `/analytics/products` | `pages/ProductDecisionCenterPage.tsx` → `GET /api/analytics/cached/products/decision-center` (`Api/Endpoints/CachedAnalyticsEndpoints.cs:1741-1820`, builder `5936-6403`) |
| Pregled dobavljača (shell) | `/analytics/supplier` | `pages/SupplierConsolidatedPage.tsx`, `useSupplierCanonicalState.ts`, `supplierSharedState.ts` |
| — tab overview | `/analytics/supplier?tab=overview` | `pages/SupplierSalesStatsPage.tsx` → `GET /api/analytics/supplier-sales-stats` (`Api/Endpoints/AllEndpoints.cs:1095-2091`) |
| — tab scorecard | `/analytics/supplier?tab=scorecard` | `pages/SupplierDecisionHubPage.tsx` → `Api/Endpoints/SupplierDecisionHubEndpoints.cs` |
| — tab assortment | `/analytics/supplier?tab=assortment` | `pages/SupplierFootwearAnalyticsPage.tsx` → vendor-sales-nivelacija (`AllEndpoints.cs:~3840+`) |

Ključni pomoćni fajlovi: `src/services/analyticsApi.ts`, `src/services/supplierSalesStatsApi.ts`, `src/utils/analyticsFormatters.ts`, `src/utils/dataScope.ts`, `Api/Endpoints/InventorySignalCalculator.cs`, `Application/Analytics/ProductDecisionReasoningHelper.cs`, `Application/Analytics/AnalyticsDecisionRecommendationEngine.cs`, `Application/Analytics/AnalyticsMarginPolicy.cs`, `Api/Endpoints/AnalyticsActionsEndpoints.cs`, `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`, `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`.

Napomena o arhitekturi: frontend je React + Vite SPA (Vercel hostuje samo statiku), backend je .NET minimal API + PostgreSQL. Next.js keširanje ne postoji; keš je serverski (`IAnalyticsCacheService`) plus klijentski memorijski keš (`analyticsApi.ts:209-261`).

## Mapa nalaza → prompt

| Nalaz | Kratko | Težina | Prompt |
|---|---|---|---|
| C1 | PDC `HasCompleteJournal=false` → sve preporuke blokirane | High | `PS02` |
| C2 | Dupli header/toolbar/KPI blok u PDC | Medium (proveriti live) | `PS10` |
| C3 | PDC pretraga se ne šalje backendu (regresija RQ200) | High | `PS05` |
| C4 | PDC marža deli ukupnim prihodom | High | `PS03` |
| C5 | PDC i Supplier koriste različit izvor troška i definiciju marže | Medium-High | `PS03` |
| C6 | Supplier preporuke tvrdo zavise od pre/post nivelacija signala | High | `PS07` |
| C7 | Top-level `recommendationAllowed` traži da i nepoznati dobavljač bude dozvoljen | Medium-High | `PS07` |
| C8 | MARKDOWN / stale DO_NOT_ORDER nedostižni; datum od „sada“ | High | `PS04` |
| C9 | REPLENISH/BOOST zahtevaju MinStock > current; MinStock 0 → nikad | Medium | `PS04` |
| C10 | Nema prethodne prodaje → trend null → INSUFFICIENT_DATA | Medium | `PS04` |
| C11 | KPI brojevi ignorišu blokadu; populacija; skrivena odsečenost | Medium | `PS09`, `PS05` |
| C12 | Kvalitet podataka nikad „Dobar“ | Medium | `PS02` |
| C13 | Action-status efekat šalje sve redove na svaku promenu, O(n²) | Medium | `PS01` |
| C14 | Teški PDC upiti | Medium | `PS14` |
| C15 | Fokusiran dobavljač ima udeo 100% | Medium | `PS11` |
| C16 | Klijentski PoP trend pristrasno naviše | Medium | `PS11` |
| C17 | Trust header se briše pri promeni dobavljača | Medium | `PS12` |
| C18 | Različit data scope i atribucija dobavljača između ekrana | Medium | `PS13` |
| C19 | Snapshot trošak uzima minimum | Medium | `PS03` |
| C20 | Teški supplier upit (grupisanje po timestamp-u) | Medium | `PS14` (+ `PS06` hipoteza) |
| C21 | Supplier keš 20 min bez stale signala na cache-hit | Medium | `PS14` |
| C22 | Engine summary/labeli na engleskom | Medium | `PS17` |
| C23 | Timezone u podrazumevanim datumima | Low | `PS16` |
| C24 | Side-effect u state updater-u pri sortiranju | Low | `PS10`, `PS18` |
| C25 | Null sortiran kao -9999 | Low | `PS10` |
| C26 | Izvoz: ratio kao procenat, sirovi enum-i, nesklad zaglavlja | Low | `PS17` |
| C27 | Nekonzistentne labele/prevodi | Low | `PS17` |
| C28 | Mrtav kod / beskorisni memo | Low | `PS18` |
| C29 | Nevalidan period u embedded modu i dalje šalje zahtev | Low | `PS12` |
| C30 | Supplier generički catch vraća `ex.Message` | Low | `PS06` |
| C31 | `getStores` ignoriše data scope stranice | Low | `PS12` |
| C32 | Rank bedževi po trenutnom sortu | Low | `PS18` |
| C33 | Sitno: slow-stock pravilo, margin 0 umesto null, queueMessage, timeline | Low | `PS04`, `PS03`, `PS10` |
| L1 | `POST /api/analytics/actions/status` → 400, dugo „učitavanje“ | Med/High | `PS01` |
| L2 | PDC filteri nisu u URL-u | Medium | `PS10` |
| L3 | PDC date input US format | Low/Med | `PS16` |
| L4 | 1.200 redova bez paginacije/virtualizacije | Medium | `PS05` |
| L5 | `supplier-sales-stats` → 503, greška = prazno | High | `PS06` |
| L6 | Scorecard 90d „nije spreman“ | High | `PS08` |
| L7 | Assortment prikazuje samo tehničku grešku ugovora | Medium | `PS08` |
| L8 | Horizontalni overflow na 1280 px | Medium | `PS15` |
| L9 | Supplier date input US format | Low/Med | `PS16` |
| L10 | Dupla opcija „Komision (Gospodska 6, N/A)“ | Low | `PS12` |

## Već u queue-u / povezano — ne duplirati

- `RQ200` (`DONE`, 2026-09-08) — tvrdio je da se pretraga šalje backendu; kod na `d8d3771f` to ne radi (`analyticsApi.ts:825-839`). `PS05` je regresija, ne novi zahtev; UI test iz `RQ200` mock-uje `getProductDecisionCenter`, pa ne vidi izgubljen parametar.
- `RQ373` (`DONE`) — ugovor display populacije za Supplier overview; `PS11` ga poštuje i samo dodaje ispravan imenilac za udeo/PoP.
- `RQ233` (`DONE`) — top-5 koncentracija; `PS11` ga ne ponavlja.
- `RQ128` (`WAITING`) — dokaz PDC actionability na produkciji; zavisi od `PS02`.
- `RQ143` (`WAITING`) — frontend ne sme da izmišlja odluke; `PS09` je konkretan slučaj.
- `RQ146` (`WAITING`) — dokaz šeme/runtime-a; `PS08` je konkretan live slučaj.
- `RQ148` (`WAITING`) — osnova merenja marže; `PS03` mora da se uskladi.
- `RQ140` (`PARTIAL`) — pre/post uporedivost; `PS07` menja samo gate politiku preporuka.
- `RQ325` (`WAITING`) — preostali engleski copy (Operacije); `PS17` koordinira, ne duplira.
- `RQ439` (`WAITING`) — trijaža PR #63 za Supplier Decision Hub; pre izmene `SupplierDecisionHubPage.tsx` u `PS08` proveriti preklapanje.
- `RQ440` (`READY`) — deljeni padajući spec-ovi; ne menjati ih bez dokazane regresije.
- `RQ441` / `RQ442` — u trenutku pisanja postoje samo kao necommit-ovane izmene drugog agenta u radnom stablu queue fajla (Daily Sales atribucija u vreme prodaje; half-open dnevni opsezi `T23:59:59Z` / `<=`). `PS13` i `PS16` ih ne dupliraju; proveriti konačne brojeve pre upotrebe.

## Registracija u queue

Presedan (`fb956bac`, Operacije audit) dodaje RQ sekcije i redove statusne tabele u kanonski queue, ažurira queue header i `MASTER_ROADMAP.md`. Ovde to namerno NIJE urađeno: u trenutku pisanja `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` i `MASTER_ROADMAP.md` imaju necommit-ovane izmene drugog, paralelnog agenta (`RQ441`, `RQ442`). Stage-ovanje tih fajlova bi commit-ovalo tuđi rad, a paralelna izmena rizikuje koliziju.

Oznake `PS01`-`PS18` su privremene. Pri registraciji dodeliti sledeće slobodne RQ brojeve istim redosledom, prepisati sekcije ispod u queue format i ne menjati, ne preuređivati i ne zatvarati postojeće stavke.

## Promptovi

Svaki prompt je samostalan i spreman za lepljenje coding agentu. Promptovi su na engleskom, kao sekcije kanonskog queue-a. Brojevi linija se odnose na `main` @ `d8d3771f`; agent mora da ih ponovo proveri pre izmene.

---

## PS01 - Keep Product Decision action-status lookups within the backend batch contract

Suggested status: READY
Priority: P1
Type: frontend/contract/tests
Feature family: pdc-action-status-batch
Parallel-safe: no (shares `ProductDecisionCenterPage.tsx` with `PS05`, `PS09`, `PS10`)
Owner: Analytics Reliability / Product Decision
Findings: C13, L1
Commit suggestion: `fix(analytics): batch product decision action status lookups`

### Problem

The Product Decision page (`/analytics/products`) asks for the action-queue status of every sorted row in a single request. The page loads `top: 1200` rows, but `POST /api/analytics/actions/status` rejects more than 1000 items with HTTP 400. On the live app the status probe returns 400 and the page ends in a degraded state. The same effect re-posts the entire list on every sort, filter or search keystroke, resets the queued state to `null` before each call (visible flicker) and matches results with an O(n²) loop.

### Evidence

- `Api/Endpoints/AnalyticsActionsEndpoints.cs:235-236` returns `BadRequest("items must contain at most 1000 entries")`.
- `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx:816` requests `top: 1200`; `:900-930` builds one lookup item per `sortedRows` entry (deduplicated only by `sourceType::sourceKey`) and posts all of them through `src/services/analyticsApi.ts:1591-1607`.
- `:966` depends on `sortedRows`, so any sort/filter/search change re-runs the lookup; `:926` sets `queuedActionKeys` to `null` before every call; `:939-942` calls `statuses.items.find` inside a loop over all rows.
- Live 2026-09-25: `/api/analytics/actions/status?dataScope=all` → HTTP 400. The page stayed in "Loading data / 1 request in progress" for more than 15 s, then showed "Vreme osveženja nije dostupno", "Prikaz može biti delimičan ili zastareo", "Nedovoljni signali: 1.200".
- Confirmed in code: 1,200 rows > 1,000-item cap → 400.
- **Hypothesis:** the >15 s wait is the decision-center request itself (see `PS14`), not the status probe; confirm in the browser network panel before attributing it.

### Scope

- `ProductDecisionCenterPage.tsx` status-lookup effect and `analyticsApi.ts` `getAnalyticsActionSourceStatuses`.
- Change the backend cap only if deliberately decided, with tests; do not change action write semantics.

### Read first

- `AGENTS.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `Api/Endpoints/AnalyticsActionsEndpoints.cs:225-259`
- `ProductDecisionCenterPage.tsx:900-966`, `src/pages/__tests__/ProductDecisionCenterPage.queueStatus.spec.tsx`

### Do

1. Split lookups into chunks no larger than the backend cap (one shared constant, not a scattered magic number) or restrict the lookup to rows actually rendered once `PS05` pagination exists; merge chunk results.
2. Key the effect on a stable signature of the `(sourceType, sourceKey)` set instead of the `sortedRows` array identity; a pure re-sort must not trigger a request.
3. Replace the per-row `find` with a `Map` keyed by `sourceType::sourceKey`.
4. Keep the previous queued state while a refresh is in flight; on failure, warn only for the affected rows/chunk.
5. Verify which UI banner is driven by the status-probe failure; a queue-status failure must not be presented as analytics data freshness/partiality.

### Tests

- 1,200-row fixture: no request exceeds the cap and every row receives its status.
- Re-sorting does not issue a new status request; filter change issues at most one lookup per changed key set.
- One chunk returns 400 → warning scoped to that chunk; other rows keep their status.
- Existing backend contract: 1,000 items accepted, 1,001 rejected.

### Acceptance

- With 1,200 rows the status probe never returns 400.
- Sorting does not re-post; queued badges do not flicker.
- The status-probe failure is explained as such and does not mark the dataset stale.

### Dependencies

- None blocking. Sequence with `PS05` and `PS10` (same file).

---

## PS02 - Unblock Product Decision recommendations: replace the hard-coded journal gate with proven evidence

Suggested status: WAITING (owner decision on the fail-closed policy)
Priority: P1
Type: backend/contract/tests
Feature family: pdc-sell-through-journal-gate
Parallel-safe: no (`CachedAnalyticsEndpoints.cs`, `InventorySignalCalculator.cs` shared with `PS03`, `PS04`, `PS14`)
Owner: Analytics Reliability / Product Decision
Findings: C1, C12; live: every row blocked
Commit suggestion: `fix(analytics): gate product decisions on proven journal completeness`

### Problem

`HasCompleteJournal` is hard-coded to `false`, so opening stock is never available and sell-through is always `insufficient_data`. `RecommendationAllowed` is therefore `false` for 100% of rows, and confidence and impact are nulled. The screen cannot produce a single actionable recommendation, yet the journal query still runs. The "Dobar" data-quality state is unreachable.

### Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:8155` sets `HasCompleteJournal = false`; the comment at `8110-8115` documents the intentional fail-closed choice.
- `:6217-6219` opening stock null → `Api/Endpoints/InventorySignalCalculator.cs:145-149` `insufficient_data` → `:68-76` `ResolveRecommendationAllowed` false → `CachedAnalyticsEndpoints.cs:6618-6631` confidence/impact nulled.
- `:6239-6245` effective data quality can never be `good` because reliable sell-through inputs are never available, so the "Dobar" filter is always empty; `:6260-6263` adds `opening_stock_unavailable` to every row.
- UI: every queue action becomes `signal_check` (`ProductDecisionCenterPage.tsx:627`); KPI "SKU sa dobrim obrtom" is always 0; the Obrt column is always N/A.
- Live 2026-09-25: all 1,200 rows show "Blokirano — nedovoljno dokaza", for example "8508 1589 Ž.Cipela, Neta, 0 RSD, 0 kom", confidence 15%.

### Scope

- PDC builder, `InventorySignalCalculator`, confidence profile and their tests.
- No schema migration without explicit owner approval; no frontend re-creation of decision logic.

### Read first

- `AGENTS.md` (sections 10.3 and 13 "Product Decision")
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `CachedAnalyticsEndpoints.cs:6200-6300`, `6586-6695`, `8093-8160`
- `InventorySignalCalculator.cs`, `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`
- `RQ128`, `RQ255`

### Do

1. Record the owner decision. Option (a): add a durable journal-completeness watermark (for example the last fully journaled date per store covering the requested period) and set `HasCompleteJournal` true only when proven. Option (b): make sell-through an optional signal, so recommendations that do not depend on it (REPLENISH on stock gap plus velocity, DO_NOT_ORDER on stale stock) may be allowed with an explicit reason code and reduced confidence.
2. Implement the chosen option; do not run the journal query when its result cannot be used.
3. Make `good` data quality reachable when inputs are proven; otherwise disable or hide the "Dobar" filter with an explanation.
4. Keep a per-row "why blocked" explanation built from concrete reason codes mapped to Serbian copy.

### Tests

- Complete-journal fixture → sell-through computed, recommendation allowed.
- Incomplete journal → blocked with the exact reason code.
- For option (b): REPLENISH allowed without sell-through, with the reduced-confidence reason code.
- `good` data quality reachable on a fully proven fixture.

### Acceptance

- With proven inputs, at least one fixture row per recommendation type is allowed.
- Blocked rows name the exact missing evidence.
- No wasted journal query when its result is ignored.

### Dependencies

- Owner decision (step 1). `RQ128` consumes the result; `PS04` follows this prompt.

---

## PS03 - One margin basis and cost source for Product Decision and Supplier overview

Suggested status: READY
Priority: P1
Type: backend/contract/tests
Feature family: margin-basis-parity
Parallel-safe: no (`CachedAnalyticsEndpoints.cs` with `PS02`/`PS04`/`PS14`; `AllEndpoints.cs` with `PS06`/`PS07`)
Owner: Analytics Reliability / Margin
Findings: C4, C5, C19, C33 (margin 0 instead of null)
Commit suggestion: `fix(analytics): align product and supplier margin basis`

### Problem

- The PDC margin % divides the margin contribution of cost-covered lines only by total revenue (covered plus uncovered), so margin is understated whenever cost coverage is below 100%.
- PDC uses `ps.NabavnaCena ?? a.NabavnaCena` with `.HasValue`: a cost of 0 counts as "covered" (100% margin), and `Artikli.NabavnaCenaDin` is ignored.
- The Supplier overview uses `AnalyticsMarginPolicy`: sale line → snapshot → `NabavnaCenaDin` → `NabavnaCena`, cost must be > 0, margin = MC / covered revenue.
- The same product or supplier therefore gets different margins on the two screens, and the PDC thresholds (good ≥22%, low <10%) fire on distorted values.
- The supplier snapshot cost takes the minimum per article, understating cost.
- The supplier margin is `0` instead of unavailable when there is no covered revenue.

### Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:6043-6050` (cost chain with `.HasValue`), `:6118` (`marginContribution / revenue`), `:5991` (`UnitCost = a.NabavnaCena`, also used for slow-stock capital at `:6208-6212`).
- `Application/Analytics/AnalyticsMarginPolicy.cs:107-112` (MC / `RevenueWithCost`, returns `0d` when nothing is covered), `:202-245` (cost chain, `IsReliableCost` requires > 0).
- `Domain/Model/Artikli.cs:16-17` declares both `NabavnaCena` and `NabavnaCenaDin`; the supplier path passes `a.NabavnaCenaDin` as `ProductCostRsd` (`AllEndpoints.cs:1347`, `:1357`).
- **Hypothesis:** `NabavnaCena` may be a legacy or non-RSD value; confirm with the data owner before fixing precedence.
- `Api/Endpoints/AllEndpoints.cs:1251` uses `g.Min(s => s.ResolvedUnitCost)` for the snapshot cost.
- `Application/Analytics/ProductDecisionReasoningHelper.cs:89-90` margin thresholds.

### Scope

- PDC sales aggregate and margin, supplier snapshot cost selection, nullable margin in the supplier DTO/UI.
- Reuse `AnalyticsMarginPolicy` / `MarginAccumulator`; no new cost schema.

### Read first

- `AGENTS.md` (10.1 no fake zero)
- `Application/Analytics/AnalyticsMarginPolicy.cs`
- `CachedAnalyticsEndpoints.cs:6025-6125`, `AllEndpoints.cs:1240-1260`, `1330-1360`
- `RQ148`, `RQ256`

### Do

1. PDC: accumulate cost with the shared policy (same chain, > 0 rule); `marginPct = marginContribution / costCoveredRevenue`; null when nothing is covered; expose coverage.
2. Supplier snapshot cost: replace `Min` with a documented deterministic choice (for example latest effective snapshot, or revenue-weighted) and record it in provenance.
3. Return null (not 0) margin when coverage is zero; the UI renders N/A through the shared formatter.
4. Add a cross-route parity check: same supplier, period and scope → same margin basis on both screens.

### Tests

- Mixed-coverage fixture: PDC margin equals the covered-basis margin.
- A zero-cost line is not treated as covered.
- `NabavnaCenaDin` precedence (once confirmed).
- Supplier margin null without coverage; snapshot selection test.
- Cross-route parity.

### Acceptance

- Both screens show the same margin for the same population, and margin with missing cost is never a fake 0 or a fake 100%.

### Dependencies

- Align with `RQ148`. `PS04` re-evaluates thresholds after this.

---

## PS04 - Make Product Decision rules reachable and period-consistent

Suggested status: WAITING (after `PS02` and `PS03`; threshold values need owner sign-off)
Priority: P1
Type: backend/tests
Feature family: pdc-decision-rule-reachability
Parallel-safe: no (`ProductDecisionReasoningHelper.cs`, `CachedAnalyticsEndpoints.cs`)
Owner: Analytics Reliability / Product Decision
Findings: C8, C9, C10, C33 (slow-stock rule)
Commit suggestion: `fix(analytics): make product decision rules reachable`

### Problem

- MARKDOWN and the stale branch of DO_NOT_ORDER require `unitsSold >= 3` in the period AND `daysSinceLastSale >= 45`. In a 30-day period a sale inside the period means the last sale was less than 30 days ago, so these rules are unreachable. Dead stock with 0 sales becomes INSUFFICIENT_DATA.
- `daysSinceLastSale` is measured from now, not from the period end, and the last-sale query has no date bound, so historical custom periods are inconsistent.
- REPLENISH/BOOST require `StockGap > 0` (current < minimum). With `MinStock = 0` an out-of-stock bestseller is never REPLENISH, and the lost-sales estimate is 0.
- When the previous period has no sales, `trendPct` is null, which forces INSUFFICIENT_DATA; new hot products never get REPLENISH/BOOST.
- Slow-stock capital uses velocity < 0.15/day and `UnitCost ?? 0`, so a missing cost yields 0 capital. The threshold may flag most size-level footwear SKUs.

### Evidence

- `Application/Analytics/ProductDecisionReasoningHelper.cs:76`, `:81-82`, `:93`, `:96-100`, `:102`, `:105`.
- `Api/Endpoints/CachedAnalyticsEndpoints.cs:6074-6090` (last sale without a period bound), `:91-110` `CalculateLostSalesEstimate` (`:99` returns 0 when `minimumStock <= 0`), `:6208-6212` slow-stock capital.

### Scope

- Decision rules, last-sale window, lost-sales and slow-stock computations and their tests. No UI decision logic.

### Read first

- `AGENTS.md`
- `ProductDecisionReasoningHelper.cs`, `CachedAnalyticsEndpoints.cs:6060-6260`
- `Api.Tests/AnalyticsDecisionRecommendationEngineTests.cs`, `Api.Tests/AnalyticsProductDecisionConfidenceTests.cs`

### Do

1. Separate zero-sales dead stock (stock > 0, no sale for N days measured from the period end) into its own MARKDOWN/DO_NOT_ORDER path with its own reason code; keep low-velocity sellers separate.
2. Measure `daysSinceLastSale` relative to the period end and bound the last-sale query to `< periodToExclusive`.
3. When `MinStock` is 0 or null, use a velocity-based cover target, or emit an explicit "minimum stock not configured" reason instead of silently never recommending REPLENISH. Return the lost-sales estimate as unavailable (null), not 0.
4. No-baseline trend → explicit "new product" path (for example REPLENISH allowed on velocity plus stock, with reduced confidence) instead of INSUFFICIENT_DATA.
5. Slow-stock capital: unavailable when cost is missing (count those separately); document the threshold and the aggregation level (size vs model) after the owner decides.

### Tests

- Table-driven helper tests proving every status is reachable with a realistic 30-day fixture.
- Historical custom period test (no dependency on `DateTime.UtcNow` when a period end exists).
- `MinStock = 0` case; new-product case; missing-cost slow-stock case.

### Acceptance

- Every recommendation status is reachable in a 30-day window, and no rule depends on the current clock when a period end exists.

### Dependencies

- `PS02`, `PS03`; owner sign-off on thresholds.

---

## PS05 - Restore server-side Product Decision search and make the row cap visible and navigable

Suggested status: READY
Priority: P1
Type: frontend/api-client/tests
Feature family: pdc-search-cap-pagination
Parallel-safe: no (`ProductDecisionCenterPage.tsx`)
Owner: Analytics Reliability / Product Decision
Findings: C3 (regression of `RQ200`), C11 (truncation part), L4
Commit suggestion: `fix(analytics): send product decision search and paginate rows`

### Problem

- `getProductDecisionCenter` drops the `search` option, so search only filters the returned top-1,200 rows and products outside the cap cannot be found.
- Every keystroke sets `loading`, which unmounts the table.
- All 1,200 rows render at once with no pagination or virtualization.
- The truncation is invisible: `TotalRows` is the returned count, and `AnalyzedRows`/`IgnoredRowsCount` are not shown.

### Evidence

- `Klijent/clientapp/src/services/analyticsApi.ts:825-839`: the options type has no `search` and no `search` param is appended.
- `ProductDecisionCenterPage.tsx:818` passes `search` through a conditional spread (not type-checked); `:850` `loadData` depends on `search` without debounce; `:1649` `!loading &&` unmounts the table.
- Backend supports search: `CachedAnalyticsEndpoints.cs:1754`, `:5974-5977`, cache key `:1768`.
- The `RQ200` completion note (2026-09-08) claims search is forwarded, but its UI regression mocks `getProductDecisionCenter`, so the dropped URL parameter is not caught.
- `CachedAnalyticsEndpoints.cs:6400` `TotalRows` = returned rows, shown as "Ukupno redova" (`ProductDecisionCenterPage.tsx:1030`).
- Live 2026-09-25: 1,200 rows rendered with no pagination.

### Scope

- `analyticsApi.ts` PDC client, PDC page loading/pagination/summary label, tests. No backend contract change needed for search.

### Read first

- `AGENTS.md`
- `RQ200` section and `.ai/runs/2026-09-08-RQ200-pdc-search-evidence.md`
- `ProductDecisionCenterPage.tsx:805-900`, `1600-1700`

### Do

1. Add `search` to the options type and append it as a URL parameter; add a client test that asserts the actual request URL.
2. Debounce search (about 300 ms) and keep the previous table visible while loading (inline loading indicator, no unmount).
3. Add pagination or virtualization; reset to page 1 on filter/search change; keep sort across pages.
4. Show "Prikazano X od Y analiziranih" from `AnalyzedRows`/`IgnoredRowsCount` and rename "Ukupno redova" accordingly.

### Tests

- Request URL contains `search`; a tail SKU beyond the top 1,200 is found.
- Typing does not unmount the table.
- Pagination boundaries (first/last page, off-by-one, page reset on filter change).

### Acceptance

- Any product in the analyzed population is findable, the table stays mounted while typing, and the cap is visible to the user.

### Dependencies

- `PS01` (status lookup can then follow the visible page); sequence with `PS09`, `PS10`.

---

## PS06 - Supplier overview: diagnose the live 503 and separate failure from empty

Suggested status: READY
Priority: P1
Type: backend/frontend/tests
Feature family: supplier-sales-stats-availability
Parallel-safe: no (`AllEndpoints.cs` with `PS03`, `PS07`, `PS14`)
Owner: Analytics Reliability / Supplier
Findings: L5, C30 (C20 as likely cause, owned by `PS14`)
Commit suggestion: `fix(analytics): distinguish supplier stats failure from empty state`

### Problem

The live `GET /api/analytics/supplier-sales-stats` returns HTTP 503 and the page shows "Podaci trenutno nisu dostupni" without distinguishing a failure from an empty dataset. The generic catch returns `ex.Message` to the client.

### Evidence

- `Api/Endpoints/AllEndpoints.cs:2035-2070`: 503 is returned only for (a) `OperationCanceledException` when the request token is cancelled (client abort/timeout), (b) `TaskCanceledException` (DB command timeout or connection drop), (c) `NpgsqlException`.
- The rate limiter rejects with 429 (`Api/Program.cs:766-768`), so it is not the source.
- `:2083-2086` generic catch returns `detail: ex.Message` with 500.
- Titles "Zahtjev otkazan" (`:2047`, `:2057`) use ijekavian spelling, unlike the rest of the app.
- **Hypothesis:** the heavy supplier query (per-sale-line grouping at `:1350-1360`, unbounded `prvaNivelacijaPoArtiklu` at `:1319-1331`) exceeds the DB command timeout or the client/proxy timeout for 30/90-day windows. Confirm from API logs ("Supplier-sales-stats cancelled ..." / "DB error ...") before optimizing.

### Scope

- Supplier stats endpoint error mapping, `supplierSalesStatsApi.ts`, `SupplierSalesStatsPage.tsx` error/empty rendering, tests. Query optimization belongs to `PS14`.

### Read first

- `AGENTS.md` (10.1, 10.2, section 11)
- `AllEndpoints.cs:1095-1230`, `2030-2091`
- `Api/Dtos/AnalyticsResponseMetaFactory.cs`
- `SupplierSalesStatsPage.tsx` error/empty branches

### Do

1. Inspect the API logs for the live request and record which branch produced the 503.
2. Return problem details with an error code and correlation id through the established pattern; use safe Serbian ekavian copy ("Zahtev ..."); never return `ex.Message`.
3. Frontend: a distinct error state (retry plus correlation id), a distinct successful-empty state (`emptyReason`) and a distinct degraded state; never show empty copy for a 5xx.
4. If a timeout is proven, hand the optimization to `PS14`; add a guard here only if it is the owning fix.

### Tests

- Endpoint tests for cancellation/timeout/Npgsql mapping with no `ex.Message` leak.
- Page tests: 503 → error with retry; successful empty 200 → empty state.

### Acceptance

- The user can tell "error, try again" from "no data for this period", and no internal exception text reaches the client.

### Dependencies

- `PS14` for performance.

---

## PS07 - Supplier recommendation gates: stop hard-blocking on nivelacija evidence and the unknown bucket

Suggested status: WAITING (owner decision on gate policy)
Priority: P1
Type: backend/contract/tests
Feature family: supplier-recommendation-gates
Parallel-safe: no (`AllEndpoints.cs`, `AnalyticsDecisionRecommendationEngine.cs`)
Owner: Analytics Reliability / Supplier
Findings: C6, C7
Commit suggestion: `fix(analytics): relax supplier recommendation evidence gates`

### Problem

- Every supplier recommendation is downgraded to `insufficient_data` / not allowed unless the supplier has both pre/post nivelacija revenue and units impact. Period-over-period and margin decisions do not require a price change, so this blocks most suppliers.
- Missing split coverage also forces `insufficient_data`.
- The gate overwrites the unknown supplier's `do_not_trust` with `insufficient_data`.
- The response-level `recommendationAllowed` requires every supplier, including the unknown bucket (always `do_not_trust`), to be allowed, so it is false whenever an unknown bucket exists and the trust header stays in warning.

### Evidence

- `Api/Endpoints/AllEndpoints.cs:1799-1804` `hasComparableNivelacijaSignal` requires both `prePostNivelacijaRevenueImpactPct` and `prePostNivelacijaUnitsImpactPct`.
- `Application/Analytics/AnalyticsDecisionRecommendationEngine.cs:32-55` `ApplyComparableSignalGate` overwrites any status; `:177-181` missing split coverage → `insufficient_data`; `:172-174` unknown → `do_not_trust`; `:105-106` allowed only for non-insufficient/non-do_not_trust and non-critical.
- `AllEndpoints.cs:1992-1994` top-level `recommendationAllowed = ... All(x => x.recommendation.recommendationAllowed)`; the cohort declares `includesUnknown = true` (`:1995-1999`).
- **Hypothesis:** most suppliers in a 30-day window lack pre/post evidence, so most rows show "Nedovoljno podataka"; measure the share on live data.

### Scope

- Engine gate policy, supplier endpoint aggregation of the top-level flag, trust meta, tests. No frontend scoring.

### Read first

- `AGENTS.md` (sections 10.3 and 13 "Supplier Scorecard")
- `AnalyticsDecisionRecommendationEngine.cs`, `Api.Tests/AnalyticsDecisionRecommendationEngineTests.cs`
- `AllEndpoints.cs:1750-1810`, `1985-2000`
- `RQ140`

### Do

1. Owner decision: treat the nivelacija signal as optional evidence (reason code plus confidence adjustment), not a hard gate for PoP/margin decisions; missing split coverage becomes a reason code, not `insufficient_data`.
2. Never let the gate overwrite `do_not_trust` for the unknown entity.
3. Compute the response-level flag over known suppliers and handle the unknown bucket through the existing unknown-share thresholds (`unknown_heavy_dataset` 15%, critical 25%).
4. Update the trust header meta to match.

### Tests

- A supplier without nivelacija evidence but with good coverage and a baseline → `maintain` / `increase_focus` allowed with the reason code.
- Unknown stays `do_not_trust`.
- Top-level allowed with a small unknown share, not allowed with a heavy one.

### Acceptance

- Suppliers with sufficient sales/margin evidence receive allowed recommendations, and the trust header reflects known-supplier readiness.

### Dependencies

- Owner decision; sequence with `PS06` (same endpoint file).

---

## PS08 - Supplier Scorecard and Assortment tabs: missing precomputed analytics schema on the live database

Suggested status: READY (diagnosis first)
Priority: P1
Type: ops-diagnosis/backend/frontend/tests
Feature family: supplier-hub-schema-readiness
Parallel-safe: yes for diagnosis; frontend part shares `SupplierDecisionHubPage.tsx` / `SupplierFootwearAnalyticsPage.tsx`
Owner: Analytics Reliability / Supplier Hub
Findings: L6, L7
Commit suggestion: `fix(analytics): explain supplier hub schema readiness states`

### Problem

- The Scorecard tab for 90 days shows only "Skup podataka odluke dobavljača za period 90d nije spreman za traženi period" and no data.
- The Assortment tab shows only the inline technical sentence "Pre/post nivelacija nema potvrđen ugovor za prihodnu promenu." with no heading, explanation or retry.
- Both messages come from missing precomputed database objects, not from missing sales.

### Evidence

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs:2144-2153` and `:2857-2864` throw `MISSING_SCHEMA` when `HasDecisionScoreCacheForWindow(90)` is false. `:2474-2479` requires `HasDecisionScoreCache90d && DecisionScoreCache90dHasRequiredColumns`; MV name `mv_supplier_decision_score_cache_90d` (`:2728-2733`). Window mapping `:2654-2663`: explicit range ≤90 days → 90, ≤180 → 180, otherwise default.
- `:2863` copy is ASCII ("dobavljaca", "trazeni").
- `Api/Endpoints/AllEndpoints.cs:3845-3866`: when view `vw_vendor_sales_nivelacija` lacks column `change_percent_revenue_semantic`, the endpoint returns 200 with meta error `vendor_sales_nivelacija_contract_missing` and that sentence.
- **Hypothesis:** the live analytics DB is behind the analytics migrations/refresh (the 90d MV is missing or lacks required columns; the view lacks the semantic column). Not verified; this audit had no DB access.

### Scope

- Read-only verification of live analytics schema vs migration scripts; UI mapping of the two codes; ASCII copy fix.
- No production DB writes or migrations without explicit owner approval.

### Read first

- `AGENTS.md` (sections 11, 13, 14)
- `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`
- `SupplierDecisionHubEndpoints.cs:2139-2160`, `2460-2480`, `2650-2740`, `2855-2870`
- `AllEndpoints.cs:3830-3870`
- `RQ146`, `RQ439`

### Do

1. Verify existence and columns of `mv_supplier_decision_score_cache_90d` and `vw_vendor_sales_nivelacija.change_percent_revenue_semantic` on the live analytics DB against the migration scripts; record which migration/refresh is missing and hand the apply step to the owner.
2. UI: map `MISSING_SCHEMA` and `vendor_sales_nivelacija_contract_missing` to an explained state (heading, what it means, what still works, retry, admin link `/admin/configuration?panel=workers`) instead of a bare technical sentence.
3. Optionally offer the nearest available window with an explicit effective-period label (never a silent fallback).
4. Fix the ASCII copy at `SupplierDecisionHubEndpoints.cs:2863`.

### Tests

- Page tests for both codes (explained state plus retry).
- Backend capability test: missing MV → `MISSING_SCHEMA` with safe copy.

### Acceptance

- Both tabs explain why data is unavailable and what to do. After the owner applies the missing schema, 90d Scorecard and Assortment load.

### Dependencies

- `RQ146`; check `RQ439` overlap before editing `SupplierDecisionHubPage.tsx`.

---

## PS09 - Product Decision KPI semantics must match row actionability and population

Suggested status: WAITING (after `PS05`, same file)
Priority: P2
Type: frontend/backend-summary/tests
Feature family: pdc-kpi-semantics
Parallel-safe: no (`ProductDecisionCenterPage.tsx`)
Owner: Analytics Reliability / Product Decision
Findings: C11 (KPI part)
Commit suggestion: `fix(analytics): align product decision KPIs with actionability`

### Problem

- The page counts REPLENISH/BOOST/MARKDOWN/DO_NOT_ORDER by `recommendationStatus` and ignores `recommendationAllowed`/blocked, so the KPIs contradict the "Blokirano" rows.
- "Rizik pokrivenosti" includes `insufficient_data`.
- Counts cover only the returned top-1,200 rows while the money KPIs cover all analyzed rows.

### Evidence

- `ProductDecisionCenterPage.tsx:977-998` (client counts), `:987` (coverage risk includes `insufficient_data`).
- `Api/Endpoints/CachedAnalyticsEndpoints.cs:6357-6360` money KPIs over analyzed rows; `:6377-6403` summary; `:6400` `TotalRows`.

### Scope

- PDC summary contract (prefer backend-owned counts) and KPI rendering; tests.

### Read first

- `AGENTS.md` (10.3)
- `RQ143`
- `CachedAnalyticsEndpoints.cs:6340-6403`

### Do

1. Compute KPI counts on the backend over the analyzed population, split into allowed and blocked; the frontend renders them.
2. Exclude `insufficient_data` from "Rizik pokrivenosti" or label it explicitly.
3. Label every KPI with its population (analyzed vs shown).

### Tests

- Fixture with blocked and allowed rows → KPI counts match row states.
- Population label present; no client re-computation of decisions.

### Acceptance

- KPIs never claim actionable counts that the rows show as blocked.

### Dependencies

- `PS02` (actionability), `PS05` (same file).

---

## PS10 - Product Decision page hygiene: duplicate header block, URL state, sorting, small state bugs

Suggested status: READY
Priority: P2
Type: frontend/tests
Feature family: pdc-page-state-hygiene
Parallel-safe: no (`ProductDecisionCenterPage.tsx`)
Owner: Analytics Reliability / Product Decision
Findings: C2, C24 (PDC part), C25, C33 (queueMessage, timeline), L2
Commit suggestion: `fix(analytics): clean up product decision page state`

### Problem and evidence

- C2: `ProductDecisionCenterPage.tsx:1290-1369` and `:1488-1567` render the same meta warning, `<header>` with `<h1>Odluke o proizvodima</h1>` plus `AnalyticsTableToolbar`, and the KPI grid. Both blocks are unconditional; only the KPI grids are gated by `hideKpiChrome` (`:975`, `:1311`, `:1509`). The duplication has been on `origin/main` since `b5280aaa` (2026-08-28). The live tester did not report a duplicated header, so **verify the deployed DOM first**: the deployment may predate the change, or the duplicate may have been missed while KPI grids were hidden by the insufficient state.
- L2: period, search and sort are not in the URL. Live: selecting 90 days leaves the URL at `/analytics/products`, and reopening resets to 30 days. The Supplier page already persists state in the URL (`useSupplierCanonicalState.ts`).
- C24: `setSortDir` is called inside the `setSortField` updater (`:1042-1051`); StrictMode double-invokes updaters in dev, so the toggle cancels out. Header-click sort combinations missing from the "Sortiranje" select (`:1460-1474`) leave the select unmatched.
- C25: nulls sort as `-9999` (`:883-887`) and mix with real negative values.
- C33: `queueMessage` is never cleared; the timeline family filter state is shared across rows with no active styling; the timeline request does not pass `storeId`/`supplierId`.

### Scope

- `ProductDecisionCenterPage.tsx` (+ CSS) and its specs. No backend change.

### Read first

- `AGENTS.md` (section 12, routing guardrails)
- `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md`
- `useSupplierCanonicalState.ts` (URL-state pattern)

### Do

1. Remove the duplicate header/toolbar/KPI/warning block (keep one, placed consistently under the trust header).
2. Sync period preset, from/to, search, sort, page and filters with the URL (validated on read, no history spam while typing).
3. Make the sort handler pure (compute next field and direction outside updaters) and include every reachable combination in the select or show a neutral "custom" value.
4. Sort nulls last in both directions.
5. Clear `queueMessage` after a timeout or on filter change; keep timeline filter state per row with active styling; pass `storeId`/`supplierId` to the timeline request.

### Tests

- Exactly one `h1` and one toolbar.
- URL round-trip (set 90 days → reload → still 90 days).
- Sort toggles correctly under StrictMode; nulls last; timeline request params.

### Acceptance

- One header; state survives reload and sharing; sorting is deterministic.

### Dependencies

- Sequence with `PS01`, `PS05`, `PS09`.

---

## PS11 - Supplier overview: share and PoP must use the correct denominator

Suggested status: READY
Priority: P2
Type: frontend/backend-totals/tests
Feature family: supplier-overview-denominators
Parallel-safe: no (`SupplierSalesStatsPage.tsx` with `PS12`, `PS18`)
Owner: Analytics Reliability / Supplier
Findings: C15, C16
Commit suggestion: `fix(analytics): keep supplier share and PoP denominators honest`

### Problem

- Share is recomputed over the filtered rows only, so a focused supplier (dropdown, or the PDC link `/analytics/supplier?supplierId=X`) shows 100% share, top-5 share becomes 100%, and the mismatch flags are wrong.
- "Ukupan PoP trend" is computed client-side from the listed suppliers' previous-period revenue. Suppliers that sold in the previous period but not the current one are missing from the list, so the trend is biased upward.

### Evidence

- `Klijent/clientapp/src/pages/SupplierSalesStatsPage.tsx:986-999` and `buildSupplierSalesDisplayProjection` `:681-706` (share recompute); PDC link at `ProductDecisionCenterPage.tsx:541`.
- PoP: `SupplierSalesStatsPage.tsx:660-666`, `:715`, `:1821`; rows are grouped from current-period lines (`Api/Endpoints/AllEndpoints.cs:1430`); the backend `totals.popRevenueChangePct` (`:1941`) is correct but unused.
- `RQ373` (DONE) intentionally made visible KPIs follow the display population. This prompt keeps that contract but gives share-of-total an explicit reference denominator.

### Scope

- Supplier overview projection, labels, and (if needed) backend scoped totals including previous-only suppliers; tests.

### Read first

- `AGENTS.md`
- `RQ373`, `RQ233`
- `SupplierSalesStatsPage.tsx:640-720`, `980-1000`, `1810-1830`

### Do

1. Share columns use the backend `sharePct` (vs all suppliers) with an explicit label; in focus mode never show a trivial 100%.
2. Hide or explicitly label top-5 concentration in focus mode.
3. Unfocused PoP uses backend `totals.popRevenueChangePct`; a filtered/focused PoP uses backend-scoped totals that include previous-only suppliers.

### Tests

- Focus on one supplier → share equals the backend global share, not 100%.
- Fixture with a previous-only supplier → PoP equals the backend total.

### Acceptance

- Share and PoP are mathematically correct for the labeled population.

### Dependencies

- None blocking; sequence with `PS12`, `PS18`.

---

## PS12 - Supplier page shell: trust header, date validation, store list and duplicate stores

Suggested status: READY
Priority: P2
Type: frontend/tests
Feature family: supplier-shell-state
Parallel-safe: no (`SupplierConsolidatedPage.tsx`, `SupplierSalesStatsPage.tsx`)
Owner: Analytics Reliability / Supplier
Findings: C17, C29, C31, L10
Commit suggestion: `fix(analytics): stabilize supplier shell trust and filter state`

### Problem and evidence

- C17: `SupplierConsolidatedPage.tsx:247-255` resets `trustPayload` on any filter change, but the child re-emits it only when its own effect dependencies change (`SupplierSalesStatsPage.tsx:1212-1247`). Switching supplier A→B leaves the header at defaults ("Pouzdanost nije potvrđena"); on a date change the child re-emits the old response's metadata while new data loads.
- C29: with an invalid date range both the parent (`SupplierConsolidatedPage.tsx:456`) and the child (`SupplierSalesStatsPage.tsx:1703`) show the error. The child still sends the request because the shared-filter sync (`:853-887`) bypasses `commitFilters` validation (`:1465`), and the backend returns 400 (`AllEndpoints.cs:1184-1192`).
- C31: `getStores` (`analyticsApi.ts:986`) uses the localStorage data scope (`dataScope.ts` `appendDataScopeToParams`), not the page scope, although `SupplierConsolidatedPage.tsx:189-199` refetches on scope change.
- L10: live, the object dropdown shows two identical "Komision (Gospodska 6, N/A)" options. Options are keyed by `storeId` (`SupplierConsolidatedPage.tsx:350`), so these are distinct store records with identical labels. **Hypothesis:** duplicate store master rows, or `buildStoreLabel` (`components/inventory/inventoryUtils.ts:617`) omits a distinguishing field; check the data.

### Scope

- Supplier shell/overview state handling, stores fetch, store label; tests. No backend change unless the store duplicate is a data issue (report to the data owner).

### Read first

- `AGENTS.md`
- `SupplierConsolidatedPage.tsx`, `useSupplierCanonicalState.ts`, `supplierSharedState.ts`
- `RQ321` (store-filter failures)

### Do

1. Key the trust payload by request identity: show a pending state while loading and emit on each settled response; never show stale metadata for new filters.
2. One validation owner for the date range; no request on an invalid range; a single error message.
3. Pass the page data scope to `getStores`.
4. Disambiguate duplicate store labels (for example append the store code/id) and record duplicate store rows for the data owner.

### Tests

- Switching supplier updates the trust header after the response settles.
- Invalid range → no request, one error.
- `getStores` receives the page scope; duplicate labels are disambiguated.

### Acceptance

- The trust header always describes the currently shown data, and invalid input never hits the backend.

### Dependencies

- Sequence with `PS11`, `PS18`.

---

## PS13 - Align data scope and supplier attribution between Product Decision and Supplier overview

Suggested status: WAITING (owner decision: sale-time vs current-master attribution)
Priority: P2
Type: backend/contract/tests
Feature family: pdc-supplier-scope-attribution-parity
Parallel-safe: no (`CachedAnalyticsEndpoints.cs`, `AllEndpoints.cs`)
Owner: Analytics Reliability / Cross-surface
Findings: C18
Commit suggestion: `fix(analytics): align product and supplier scope attribution`

### Problem

- The data scope is applied differently: PDC filters sales by the sale header's `DataOrigin`, while Supplier stats filter by the article's `DataOrigin`.
- Supplier attribution differs too: PDC uses the current article-master supplier; Supplier stats use the sale-time `SupplierIdAtSale`.
- The PDC → Supplier link can therefore point to a supplier that does not exist in the Supplier stats, and the shell silently clears the selection.

### Evidence

- `Api/Endpoints/CachedAnalyticsEndpoints.cs:6034-6035` (`pz.DataOrigin`), `:5972-5973` (`a.DataOrigin` on articles), `:5971`, `:6033` (`a.IDDobavljac`).
- `Api/Endpoints/AllEndpoints.cs:1290-1291`, `:1340-1341` (`a.DataOrigin`), `:1292`, `:1352` (`ps.SupplierIdAtSale`).
- `ProductDecisionCenterPage.tsx:541` (link); `SupplierConsolidatedPage.tsx:226-232` (silent clear).

### Scope

- Scope/attribution predicates in both builders, link handling, tests. No schema change.

### Read first

- `AGENTS.md`
- `RQ411`, `RQ412`; `RQ441` (Daily Sales counterpart; uncommitted in the working tree at the time of writing)

### Do

1. Document the canonical scope and attribution rule (owner decision) and apply it in the PDC supplier filter/grouping, or label the difference explicitly.
2. Unknown `supplierId` in the URL → visible message ("Dobavljač nije u izabranom periodu/opsegu") instead of a silent clear.

### Tests

- Master-mutation fixture: after changing `Artikli.IDDobavljac`, both screens attribute historical sales consistently.
- Scope parity for `all`/`existing`/`imported`; unknown-supplier link message.

### Acceptance

- The same product/period/scope lands under the same supplier on both screens.

### Dependencies

- Owner decision; coordinate with `RQ441`.

---

## PS14 - Performance and cache freshness for the Product Decision and Supplier endpoints

Suggested status: READY
Priority: P2
Type: backend/performance/tests
Feature family: pdc-supplier-query-performance
Parallel-safe: no (`CachedAnalyticsEndpoints.cs`, `AllEndpoints.cs`)
Owner: Analytics Reliability / Performance
Findings: C14, C20, C21
Commit suggestion: `perf(analytics): bound product and supplier decision queries`

### Problem and evidence

- PDC loads all matching articles and sends the full id list through `articleIds.Contains` (`CachedAnalyticsEndpoints.cs:5964-6029`). The last-sale query scans the entire history (`:6074-6090`), the journal query is wasted (`:8093-8160`, see `PS02`), and the cache key includes the raw search string (`:1768`).
- The supplier stats query groups by the full `DatumProdaje` timestamp plus cost fields (`AllEndpoints.cs:1350-1360`), roughly one materialized row per sale line; `prvaNivelacijaPoArtiklu` (`:1319-1331`) scans all history without an article filter.
- The supplier cache is `HeavyAnalytics` 20 min (`:2007`, `Infrastructure/Services/Caching/IAnalyticsCacheService.cs:500`). The cache-hit path returns raw JSON without a stale/age flag or correlation id (`AllEndpoints.cs:1228`), and `blockOperationsDecisionSignals` is baked into the cached payload (`:1762`, `:1804`).

### Scope

- Query shape, bounds, cache metadata; behavior-preserving (same results) with tests. No schema/index migration without approval (propose it separately if needed).

### Read first

- `AGENTS.md`
- `docs/ANALYTICS_PERFORMANCE_SPRINT_PLAN.md`
- `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`, `IAnalyticsCacheService.cs`

### Do

1. PDC: filter via joins/subqueries instead of huge `IN` lists; bound the last-sale query to the period end and the needed articles; normalize search in the cache key.
2. Supplier: aggregate in SQL at the needed grain (day/article) instead of per timestamp; restrict the nivelacija lookup to relevant articles.
3. Add age/stale metadata and a correlation id on cache hits; evaluate decision-block flags at read time, not inside the cached payload.
4. Measure before/after (timing logs or `EXPLAIN ANALYZE` on realistic data) and record the numbers.

### Tests

- Builder integration tests prove identical results before and after.
- Cache-hit metadata test (age, stale flag, correlation id).

### Acceptance

- Measured latency reduction on 30/90-day windows with unchanged numbers, and cache hits disclose their age.

### Dependencies

- `PS06` uses this result; `PS02` removes the journal query.

---

## PS15 - Supplier page filter layout at 1280 px

Suggested status: READY
Priority: P2
Type: frontend/css
Feature family: supplier-filter-layout
Parallel-safe: yes (CSS; minor JSX in `SupplierConsolidatedPage.tsx`)
Owner: Analytics UI
Findings: L8
Commit suggestion: `fix(ui): keep supplier filters within 1280px`

### Problem and evidence

- Live 2026-09-25 at 1280 px width: horizontal page overflow; filter values are clipped ("Poslednjih ...", "Svi dobavlja...") and the object dropdown overflows.
- The filter bar is in `SupplierConsolidatedPage.tsx` (about `:310-410`) with styles in `SupplierConsolidatedPage.css`. The CSS was not inspected in detail during the audit.

### Scope

- Supplier filter bar layout only; use existing theme tokens; do not change global theme defaults.

### Read first

- `AGENTS.md` (10.4)
- `SupplierConsolidatedPage.tsx`, `SupplierConsolidatedPage.css`

### Do

1. Let filters wrap (grid/flex with sensible min widths) and remove page-level horizontal scroll at 1280 px and 1024 px.
2. Provide full text for truncated options (`title` or wider control).
3. Constrain the object dropdown width.

### Tests

- A DOM/visual check at 1280 px and 1024 px (Playwright screenshot if available; otherwise attach manual evidence).

### Acceptance

- No horizontal overflow at 1280 px, and filter values are readable.

### Dependencies

- None.

---

## PS16 - Dates and locale on both screens

Suggested status: READY
Priority: P3
Type: frontend/tests
Feature family: pdc-supplier-date-locale
Parallel-safe: no (`ProductDecisionCenterPage.tsx`, `SupplierSalesStatsPage.tsx`)
Owner: Analytics UI
Findings: C23, L3, L9
Commit suggestion: `fix(analytics): local date defaults and Serbian date display`

### Problem and evidence

- `ProductDecisionCenterPage.tsx:228-246` `toDateInputValue` uses `toISOString()` (UTC); between 00:00 and 02:00 Belgrade time the default "to" date is yesterday.
- `SupplierSalesStatsPage.tsx:189-193` `toDateOnly` shifts datetime strings without `Z`.
- Native `<input type="date">` shows the browser-locale format (live: `06/28/2026` on Products, `08/27/2026` on Supplier), while the trust header uses Serbian "27. 8. 2026. - 25. 9. 2026.".
- The `T23:59:59Z` end serialization and inclusive `<=` in `SupplierSalesStatsPage.tsx:182-187` belong to `RQ442` (uncommitted in the working tree at the time of writing); do not duplicate.
- **Hypothesis:** the storage basis of `DatumProdaje` (UTC vs local) is unclear (the Access import uses `DT(...)` or a `UtcNow` fallback, `AccessImportService` around `:4689`); verify before changing server-side boundaries.

### Scope

- Frontend date helpers and date display only.

### Read first

- `AGENTS.md` (10.4)
- `src/utils/analyticsFormatters.ts`

### Do

1. Use one shared local-date helper for defaults and date-only parsing.
2. Show dates in Serbian format (for example a formatted label next to the native input, or a validated `dd.MM.yyyy` input); note that `lang="sr-RS"` alone does not change Chrome's native picker format.
3. Document the `DatumProdaje` storage basis in the helper comment once verified.

### Tests

- Fake timers at 00:30 Europe/Belgrade → default "to" is today.
- `toDateOnly` with and without `Z`; date display format test.

### Acceptance

- Defaults are correct around midnight, and users see Serbian date formatting consistently.

### Dependencies

- `RQ442` owns server-side whole-day boundaries.

---

## PS17 - Serbian copy, labels and export values on both screens

Suggested status: READY
Priority: P3
Type: frontend/backend-copy/tests
Feature family: pdc-supplier-copy-export
Parallel-safe: no (touches many files; do last or split per page)
Owner: Analytics UI / Copy
Findings: C22, C26, C27, plus ASCII copy observed during the audit
Commit suggestion: `fix(analytics): Serbian copy and export values for product and supplier`

### Problem and evidence

- C22: engine summaries/labels are English (`Application/Analytics/AnalyticsDecisionRecommendationEngine.cs:237-279`, e.g. "Strong PoP trend...", "Increase focus") while the gate summary (`:50`) is Serbian; wherever these are surfaced the text is mixed.
- C26 export:
  - PDC `sellThroughRatio` is declared as a percent column but holds a 0-1 ratio (`ProductDecisionCenterPage.tsx:219`), so it exports "0,45%".
  - The Supplier export `status` is a raw enum (`SupplierSalesStatsPage.tsx:133`), and the PDC export `dataQualityStatus` is raw too.
  - The export header "Kvalitet ulaza" differs from the UI's "Kvalitet podataka".
- C27 labels:
  - The backend "Dobar sell-through" / "Upozorenje sell-through" (`InventorySignalCalculator.cs:243-245`) overrides the client "Dobar obrt zalihe" (`ProductDecisionCenterPage.tsx:1731`); stock turnover and sell-through are different concepts.
  - "Ne naručuj" (backend) vs "Ne naručivati" (UI).
  - "Low signal" (`SupplierSalesStatsPage.tsx:410`, `:484`) and "decision preporuke" (`:1926`) are not translated.
  - The supplier page title/h2 "Dobavljači" (`SupplierConsolidatedPage.tsx:260`, `:308`) differs from the nav's "Pregled dobavljača", and there is no h1.
  - `dataQualityLabels` lacks `critical` (`:48-54`), and `trustToneClass` maps critical to warning (`:161-168`).
- ASCII/no-diacritics copy:
  - `ProductDecisionCenterPage.tsx:1273` ("pojacanje, pracenje"), `:1282` ("odlucivanje"), `:1292` ("delimicni ... osvezavanja"), `:1257` ("vec"), KPI labels `:1319-1331` ("pojacanje", "snizenje", "narucivati").
  - `analyticsApi.ts:844` ("Greska pri ucitavanju Product Decision Center pregleda").
  - `SupplierConsolidatedPage.tsx:322` ("Prilagodeno"), `:340` ("Postojeci"), `:368` ("Svi dobavljaci"), `:403` ("Zensko").
  - `SupplierDecisionHubEndpoints.cs:2863` (also listed in `PS08`).

### Scope

- User-visible copy, label maps and export column types on these two screens. Preserve UTF-8.
- Coordinate with `RQ325` (Operacije copy owner); do not rewrite unrelated screens.

### Read first

- `AGENTS.md` (10.4, section 12)
- `docs/ai/ENCODING_AND_TEXT_SAFETY.md`
- `src/utils/analyticsFormatters.ts`, `src/services/analyticsTableState.ts` (percent columns expect percent units)

### Do

1. Map backend statuses/labels to Serbian in the established label maps; keep backend codes machine-readable.
2. Fix diacritics in the listed strings.
3. Export: convert the ratio to percent units (or declare a ratio type), map enums to labels, and use one header name.
4. Use "Obrt zalihe" vs "Sell-through" consistently with the metric definition; add an h1 and align the title with the nav; add a `critical` label and tone.

### Tests

- `npm run check:analytics-guardrails`; export tests for percent and enum mapping; label map tests.

### Acceptance

- No English, raw enum or ASCII-substituted copy on either screen or in their exports.

### Dependencies

- Do after functional prompts touching the same files, or split per page.

---

## PS18 - Supplier overview hygiene: sort handler, dead code, rank badges

Suggested status: READY
Priority: P3
Type: frontend/tests
Feature family: supplier-overview-hygiene
Parallel-safe: no (`SupplierSalesStatsPage.tsx`)
Owner: Analytics UI
Findings: C24 (supplier part), C28, C32
Commit suggestion: `fix(analytics): supplier overview sort and badge hygiene`

### Problem and evidence

- `SupplierSalesStatsPage.tsx:1489-1499`: `setSortDir` is called inside the `setSortField` updater; under StrictMode the toggle cancels out in dev.
- `:293-304` `displaySignalLabel` is never used; the `controlBarFields` `useMemo` (`:1634-1645`) depends on functions recreated every render, so the memo never hits.
- `:2078-2106`: rank badges #1-3 are colored gold/silver/bronze by the current sort order, even when sorted alphabetically.

### Scope

- `SupplierSalesStatsPage.tsx` and its specs.

### Read first

- `AGENTS.md`

### Do

1. Make the sort handler pure.
2. Remove dead code; stabilize callbacks or drop the memo.
3. Show rank badges only for the revenue rank (backend order) or hide them on non-metric sorts.

### Tests

- Sort toggle under StrictMode; badges absent or stable on alphabetical sort.

### Acceptance

- Deterministic sorting and badges that always mean revenue rank.

### Dependencies

- Sequence with `PS11`, `PS12`.

---

## Provere tokom audita

- Statički audit koda urađen je read-only (čitanje fajlova, `git status`/`git log`/`git grep`/`git blame`), bez izmena, build-a i testova.
- Live UI audit https://trendplus.vercel.app radio je drugi worker (browser); nalazi `L1`-`L10` su preuzeti iz njegovog izveštaja, a uzroci su potvrđeni ili označeni kao hipoteze u kodu (`L1` potvrđen: 1.200 > 1.000; `L5`, `L6`, `L7`, `L10` hipoteze).
- Nije rađen pristup live bazi, API logovima ni Vercel deploy metapodacima.

## Šta nije menjano

- Nije menjan runtime kod, testovi, backend ugovor, baza ni produkcioni podaci.
- Nije menjan kanonski queue ni `MASTER_ROADMAP.md` (razlog u „Registracija u queue“).
- Promene su lokalno commit-ovane na `main` (bez push-a), po zahtevu korisnika.

## Stanje posle audita

| Prompt | Predloženi status | Prioritet | Ekran | Namena |
|---|---|---|---|---|
| `PS01` | `READY` | P1 | Odluke o proizvodima | action-status batch ≤ backend limit, bez re-posta na sort |
| `PS02` | `WAITING` (odluka vlasnika) | P1 | Odluke o proizvodima | dokazana kompletnost dnevnika umesto hardkodovanog `false` |
| `PS03` | `READY` | P1 | oba | ista osnova marže i izvor troška |
| `PS04` | `WAITING` | P1 | Odluke o proizvodima | dostižna pravila odluka, merenje od kraja perioda |
| `PS05` | `READY` | P1 | Odluke o proizvodima | serverska pretraga, paginacija, vidljiv limit |
| `PS06` | `READY` | P1 | Pregled dobavljača | dijagnoza 503, greška ≠ prazno, bez `ex.Message` |
| `PS07` | `WAITING` (odluka vlasnika) | P1 | Pregled dobavljača | gate preporuka bez tvrde nivelacija zavisnosti |
| `PS08` | `READY` (dijagnoza) | P1 | Scorecard / Assortment | nedostajuća precomputed šema, objašnjena stanja |
| `PS09` | `WAITING` | P2 | Odluke o proizvodima | KPI usklađeni sa blokadom i populacijom |
| `PS10` | `READY` | P2 | Odluke o proizvodima | dupli blok, URL stanje, sortiranje, sitni state bagovi |
| `PS11` | `READY` | P2 | Pregled dobavljača | imenioci udela i PoP |
| `PS12` | `READY` | P2 | Pregled dobavljača | trust header, validacija perioda, prodavnice |
| `PS13` | `WAITING` (odluka vlasnika) | P2 | oba | scope i atribucija dobavljača |
| `PS14` | `READY` | P2 | oba | performanse upita i svežina keša |
| `PS15` | `READY` | P2 | Pregled dobavljača | layout filtera na 1280 px |
| `PS16` | `READY` | P3 | oba | lokalni datumi i srpski format |
| `PS17` | `READY` | P3 | oba | srpski copy, labele, izvoz |
| `PS18` | `READY` | P3 | Pregled dobavljača | sortiranje, mrtav kod, rank bedževi |

---

## Dodatak 2026-09-25 — audit „Prodaja po dobavljačima“ (grok)

Izvor: `.ai/runs/2026-09-25-supplier-sales-overview-audit-evidence.md` (tabela svih pokazatelja sa file:line dokazima). Brojevi linija se odnose na `main` @ `53f66cfa`. Ovaj dodatak ne menja postojeće `PS` sekcije; registrar ga uzima u obzir pri prepisu u kanonski queue.

- `RQ443` (registrovan u queue-u, `PARTIAL`, lokalni commit `2150944b`) isporučio je deo `PS11` koraka 3: nefokusirani „Ukupan PoP trend“ koristi backend `totals.previousPeriodRevenue`, a prikaz samo poznatih dobavljača je N/A. Pri registraciji `PS11` zadržati korake 1-2 i drugi deo koraka 3 (backend prethodni zbir za fokusiran/poznat skup, uključujući dobavljače bez prodaje u tekućem periodu), bez ponavljanja isporučenog dela.
- `PS11` dopuna (odluka vlasnika): grafikon „Koncentracija prometa“ i KPI „Udeo top 5 dobavljača“ dele promet vidljivih **poznatih** dobavljača (`SupplierSalesStatsPage.tsx:197-225`, `:1126`, `:1150`), a kolona „Udeo u prometu“ i grafikon poređenja dele sve vidljive redove uključujući nepoznate (`:1155`, `:1980`), pod istom legendom „Udeo u prometu %“; InfoTip grafikona (`:1927`) kaže „ukupnog prometa“. Dobavljač sa negativnim neto prometom (samo povraćaji) može podići Top 5 iznad 100% i sakriti „Ostale“. Potrebno: jedan imenilac sa jasnom labelom i pravilo za negativne redove.
- `PS11`/`PS17` dopuna: InfoTip „Ukupan promet“ (`:1858`) kaže „svih dobavljača“, a bedž kvaliteta marže (`:1877`, `data.totals.marginQuality*`) opisuje ceo odgovor i kada je prikazan fokusirani dobavljač.
- `RQ444` (registrovan, `PARTIAL`, lokalni commit `53f66cfa`) pokriva lepljivi legacy `sezonaId`, prikaz prozora podataka kao perioda u kartici „Period i filteri“ i +1 dan krajnjeg datuma u zaglavlju; to nije deo `PS12` ni `PS16`.
- Preostali ASCII/engleski copy ovog ekrana dodat je u `RQ325` (dodatak 2026-09-25, Supplier overview); `PS17` ga ne treba ponavljati.
