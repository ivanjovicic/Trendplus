# Analytics Cache Invalidation Audit

Datum: 2026-06-17
Repo: `ivanjovicic/Trendplus`
Scope: audit cache invalidation posle importa, refresh-a i report tokova, uz mali safe fix gde je rupa bila jasna

## Svrha

Analytics UI sada zavisi od freshness i trust signala. Ako import ili refresh prodju, a cache ostane star, operator moze da vidi zastarele preporuke, lazno star freshness signal ili stare report payload-e.

Ovaj audit mapira:

1. cache familije i key obrasce
2. dogadjaje koji moraju da invalidiraju cache
3. sta danas postoji
4. gde su rizici
5. koji mali fix je dodat

## Procitano

- `Infrastructure/Services/Caching/IAnalyticsCacheService.cs`
- `Infrastructure/Services/Caching/AnalyticsCachePolicy.cs`
- `Infrastructure/Services/Caching/AnalyticsCacheAdminService.cs`
- `Api/Services/AnalyticsRefreshStatusService.cs`
- `Api/Services/AccessImportService.cs`
- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `Api/Endpoints/DataQualityEndpoints.cs`
- `Api/Endpoints/AnalyticsReportsEndpoints.cs`
- `Api/Endpoints/AnalyticsActionsEndpoints.cs`
- `Workers/NightlyAnalyticsRefreshWorker.cs`
- `Workers/AnalyticsDataQualityHealthWorker.cs`
- `Workers/AnalyticsAggregationWorker.cs`
- `docs/ops/ANALYTICS_MONITORING_ALERTING.md`

Napomena:
- `docs/qa/STABLE_REPORT_URL_SMOKE.md` nije pronadjen u ovom branch stanju repoa.

## Cache familije i key obrasci

| Family | Prefix / key pattern | Glavne rute |
|---|---|---|
| dashboard | `analytics:summary:*`, `analytics:daily:*`, `analytics:category:*`, `analytics:gender:*`, `analytics:supplier:*`, `analytics:dashboard-*`, `analytics:dashboard-bootstrap:*`, `analytics:filters:*`, `analytics:validation:*` | `/api/analytics/cached/*`, `/api/analytics/cached/dashboard/bootstrap` |
| product-decision-center | `analytics:product-decision-center:*` | `/api/analytics/cached/product-decision-center` |
| supplier-decision-hub | `analytics:supplier-decision-hub:*` | `/api/analytics/suppliers/decision-hub/*` |
| inventory | `analytics:inventory:*`, `analytics:inventory-*`, `analytics:rebalance-suggestions:*` | cached inventory analytics endpoints |
| data-quality | `analytics:validation:*` plus data-quality payloadi sa TTL u cached endpoints | `/api/analytics/cached/validation/*` |
| reports | `analytics:analytics-report:*` uz report cache version token | `/api/analytics/reports/pilot-intake`, `/api/analytics/reports/supplier-decision` |
| pre-post | `analytics:pre-post:*` | pre/post analytics family in policy |
| pre-nivelacija-prioriteti | `analytics:pre-nivelacija-prioriteti:*` | `/api/analytics/pre-nivelacija-prioriteti` |
| action outcome summary | nema analytics cache key-ja | `/api/analytics/actions/outcomes/summary` |

## Detaljnije mapiranje po trazenom scope-u

### 1. Dashboard / bootstrap

- Key primeri:
  - `AnalyticsCacheKeys.DashboardBootstrap(...)`
  - `AnalyticsCacheKeys.SalesSummary(...)`
  - `AnalyticsCacheKeys.DailySales(...)`
  - `AnalyticsCacheKeys.CategoryData(...)`
  - `AnalyticsCacheKeys.GenderData(...)`
  - `AnalyticsCacheKeys.SupplierData(...)`
  - `AnalyticsCacheKeys.SupplierFilters(...)`
  - `AnalyticsCacheKeys.TransactionStats(...)`
  - `AnalyticsCacheKeys.DashboardAdvanced(...)`
  - `AnalyticsCacheKeys.ValidationCompleteness`
  - `AnalyticsCacheKeys.ValidationFreshness`
  - `AnalyticsCacheKeys.ValidationLostSales`
- Family:
  - `AnalyticsCachePolicy.DashboardFamily`
  - validation endpoints koriste `AnalyticsCachePolicy.DataQuality`, ali su ugradjeni u dashboard bootstrap payload

### 2. Product decisions

- Key:
  - `AnalyticsCacheKeys.ProductDecisionCenter(...)`
- Family:
  - `AnalyticsCachePolicy.ProductDecisionCenterFamily`

### 3. Supplier scorecard / overview

- Key:
  - `AnalyticsCacheKeys.SupplierDecisionHubSummary(...)`
  - `AnalyticsCacheKeys.SupplierDecisionHubQuadrant(...)`
  - `AnalyticsCacheKeys.SupplierDecisionHubRanking(...)`
  - `AnalyticsCacheKeys.SupplierDecisionHubDetails(...)`
  - `AnalyticsCacheKeys.SupplierDecisionHubDataset(...)`
- Family:
  - `AnalyticsCachePolicy.SupplierDecisionHubFamily`

### 4. Inventory analytics

- Key:
  - `AnalyticsCacheKeys.Inventory(...)`
  - `AnalyticsCacheKeys.InventoryInsights(...)`
  - `AnalyticsCacheKeys.InventoryStoreComparison(...)`
  - `AnalyticsCacheKeys.InventoryForecast(...)`
  - `AnalyticsCacheKeys.InventorySizeCurve(...)`
  - `AnalyticsCacheKeys.RebalanceSuggestions(...)`
  - `AnalyticsCacheKeys.InventoryAlerts(...)`
  - `AnalyticsCacheKeys.InventoryDataset(...)`
- Family:
  - `AnalyticsCachePolicy.InventoryFamily`

### 5. Data quality

- Key:
  - `AnalyticsCacheKeys.ValidationCompleteness`
  - `AnalyticsCacheKeys.ValidationFreshness`
  - `AnalyticsCacheKeys.ValidationLostSales`
  - `AnalyticsCacheKeys.ValidationNegativeQty(...)`
- Napomena:
  - `/api/analytics/data-quality/*` health/list/trend nisu analytics-cacheovani kroz `IAnalyticsCacheService`
  - data quality report route jeste report-cacheovan kroz reports family

### 6. Reports

- Key:
  - `AnalyticsCacheKeys.SupplierDecisionReport(...)`
  - `AnalyticsCacheKeys.PilotIntakeReport(...)`
- Invalidation model:
  - report family clear
  - `AnalyticsCacheAdminService` bump-uje report version token
  - report endpoints citaju `GetReportCacheVersionAsync()` pre formiranja key-ja

### 7. Action outcome summary

- `AnalyticsActionItemService.GetOutcomeSummaryAsync(...)` ide direktno na servis
- Nije nadjen `IAnalyticsCacheService` sloj za outcome summary
- Zakljucak:
  - nema cache invalidation obaveze za ovaj endpoint
  - stale rizik ovde dolazi iz podataka, ne iz analytics cache-a

## Dogadjaji koji moraju da invalidiraju ili osveze cache

| Event | Family / payload koji mora da se osvezi | Trenutno stanje |
|---|---|---|
| import completed | dashboard, product decisions, supplier hub, inventory, data-quality validations, reports, pre-post, pre-nivelacija | postoji: `AccessImportService.ImportAsync` cisti `AnalyticsCachePolicy.CoreFamilies` |
| manual analytics refresh completed | isto kao gore | postoji indirektno: manual run trigeruje worker; `NightlyAnalyticsRefreshWorker` na uspehu cisti `CoreFamilies` |
| worker refresh completed | isto kao gore | postoji za `NightlyAnalyticsRefreshWorker`; `AnalyticsAggregationWorker` namerno ne radi broad clear |
| report generated/regenerated | report payload za isti stabilni query | report endpoint radi read-through cache populate; invalidacija zavisi od import/refresh/report-family clear |
| data quality recalculated | data-quality endpoints, pilot intake report, dashboard bootstrap i dashboard validations koji prikazuju freshness/trust | delomicno postojalo; dodat fix u ovom tasku |

## Stanje po event-u

### Import completed

Potvrdjeno:

- `AccessImportService` posle uspesnog importa poziva:
  - `AnalyticsCacheAdminService.ClearFamiliesAsync(AnalyticsCachePolicy.CoreFamilies, ...)`
- fallback bez admin servisa:
  - `RemoveByPrefixAsync(AnalyticsCacheKeys.Prefix, ...)`

Ocena:
- `PASS`

### Manual analytics refresh completed

Potvrdjeno:

- manual refresh ide preko worker runtime policy `RequestManualRunAsync(...)`
- `NightlyAnalyticsRefreshWorker` na uspehu poziva:
  - `ClearFamiliesAsync(AnalyticsCachePolicy.CoreFamilies, ...)`

Ocena:
- `PASS`

### Worker refresh completed

Potvrdjeno:

- `NightlyAnalyticsRefreshWorker`: invalidira core families
- `AnalyticsDataQualityHealthWorker`: invalidira specificne familije vezane za data quality
- `AnalyticsAggregationWorker`: svesno ne radi broad purge, oslanja se na TTL

Ocena:
- `PASS` za nightly worker
- `PARTIAL` za aggregation worker, ali deluje namerno i dokumentovano u kodu

### Report generated / regenerated

Potvrdjeno:

- report endpoints cache-uju gotov report payload
- key ukljucuje `reportCacheVersion`
- report-family clear bump-uje version token

Ocena:
- `PASS`

Komentar:
- "generate" sam po sebi ne invalidira nista, vec puni cache za trazeni stabilni query
- pravi invalidation okidaci su import, refresh i report-family clear

### Data quality recalculated

Pre izmene:

- `AnalyticsDataQualityHealthWorker` je cistio:
  - `data-quality`
  - `reports`
- nije cistio:
  - `dashboard`

Zasto je to problem:

- dashboard bootstrap payload ugradjuje:
  - `ValidationCompleteness`
  - `ValidationFreshness`
  - `ValidationLostSales`
  - executive freshness/data-quality summary
- iako validation kljucevi imaju svoju TTL logiku, `/api/analytics/cached/dashboard/bootstrap` je zasebno cacheovan
- zato je dashboard mogao da vrati stari bootstrap payload i posle uspesnog data-quality refresh-a

Ocena:
- `FAIL` pre fix-a
- `PASS` posle fix-a

## Mali fix dodat u ovom tasku

Izmena:
- `Workers/AnalyticsDataQualityHealthWorker.cs`

Promena:
- worker sada invalidira i `AnalyticsCachePolicy.DashboardFamily`
- zadrzava postojece invalidacije za:
  - `AnalyticsCachePolicy.DataQualityFamily`
  - `AnalyticsCachePolicy.ReportsFamily`

Efekat:
- dashboard bootstrap i dashboard trust/freshness sekcije vise ne cekaju TTL posle data-quality recalculation-a
- pilot readiness i report trust sloj dobijaju sveziji signal odmah posle uspesnog worker run-a

## Gaps i prioritet

### P1: data-quality worker mora da obori dashboard bootstrap

- Status:
  - reseno u ovom tasku

### P2: batch delete sa `includeAnalytics=false` ne invalidira analytics cache

Posmatranje:

- `DeleteBatchAsync(...)` uvek brise trendplus Access-origin podatke
- ali cache invalidation radi samo kada je `includeAnalytics == true`

Rizik:

- dashboard freshness/data-quality/inventory signali mogu kratko ostati stari posle batch delete operacije koja dira raw podatke ali ne trazi analytics cleanup

Ocena:
- `MEDIUM`

Preporuka:
- sledeci mali follow-up moze da invalidira makar `dashboard`, `inventory` i `data-quality` familije i kada je `includeAnalytics=false`

### P3: `AnalyticsAggregationWorker` oslanja se na TTL umesto invalidacije

Posmatranje:

- worker osvezava pre-agregirane summary tabele na 5 minuta
- kod eksplicitno izbegava broad cache purge zbog cold-start troska

Rizik:

- postoji kontrolisan stale window do TTL-a za dashboard summary sekcije

Ocena:
- `LOW`

Preporuka:
- ne menjati bez merenja performansi
- ako se pojavi incident, razmotriti usko targetiranu invalidaciju samo summary/dashboard familije

## Zakljucak

Glavni trust tokovi imaju dobar osnov:

- import completion invalidira core analytics cache
- nightly/manual analytics refresh invalidira core analytics cache
- reports koriste versioned cache key model
- action outcome summary nije cacheovan pa nema fake stale cache sloj

Najjasnija rupa bila je data-quality worker:

- data-quality recalc je menjao trust signal
- report cache i data-quality cache su se cistili
- ali dashboard bootstrap je mogao da ostane zastareo

To je sada zatvoreno dodavanjem dashboard family invalidacije.
