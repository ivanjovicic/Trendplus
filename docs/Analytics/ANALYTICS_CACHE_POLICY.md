# Analytics Cache Policy

## Scope
Ovaj dokument mapira trenutnu cache politiku za analytics rute kako bismo izbegli nekonzistentne rezultate tokom importa, refresh-a i deploy-a.

| Cache family | Endpoint(s) | Key inputs | TTL | Invalidated by | Stale warning? | Notes |
|---|---|---|---|---|---|---|
| `dashboard` | `/api/analytics/cached/dashboard/bootstrap`, `/api/analytics/cached/sales/summary`, `/api/analytics/cached/sales/top-products` | period (`fromDate`,`toDate`), `storeId`, `supplierId`, `dataScope` | `AnalyticsCachePolicy.DashboardBootstrap.Ttl` = 2 min | `AnalyticsCachePolicy.CoreFamilies` clear (import completed, nightly refresh), manual `/api/analytics/cached/cache/invalidate` (`dashboard`/`all`) | Yes (`ApplyStaleCacheWarning`, stale after 1 min) | Bootstrap sadrži pod-sekcije sa internim TTL-ovima (medium/short/long aliasi u kodu). |
| `product-decision-center` | `/api/analytics/cached/products/decision-center` | period, `storeId`, `supplierId`, `top`, `dataScope` | `AnalyticsCachePolicy.ProductDecisionCenter.Ttl` = 4 min | `CoreFamilies`, manual invalidate (`product-decision-center`/`all`) | Yes (`ApplyStaleCacheWarning`, stale after 2 min) | Težak endpoint; cache hit/miss logovan preko policy helper-a. |
| `supplier-decision-hub` | `/api/analytics/suppliers/decision-hub/*` i supplier durable report cache | period, category/gender/season, supplier/store, `dataScope`, paging/sort, report version | `CacheExpiration.HeavyAnalytics` = 20 min | `CoreFamilies`, manual invalidate (`supplier-decision-hub`/`all`), report version bump pri report cache clear | No (nema `ApplyStaleCacheWarning`) | Endpoint je van `CachedAnalyticsEndpoints`, ali je deo iste analytics cache topologije. |
| `supplier-scorecard` | `/api/analytics/advanced/supplier-scorecard` (InsightStudio), scorecard sekcija u `/api/analytics/suppliers/decision-hub/ranking` (trustMetadata) | period (`fromDate`,`toDate`), `storeId`, `dataScope` | deo `CacheExpiration.HeavyAnalytics` u ranking; InsightStudio endpoint nema poseban server-side cache | `CoreFamilies` (nightly/import clear) | No (nema stale warning na scorecardMeta; UI prikazuje `Pomoćni signal` banner ako je `usedFallback=true`) | **Kritično za tihi fallback**: `requestedDataset` vs `effectiveDataset` moraju biti različiti samo ako `usedFallback=true`. Ako `recommendationAllowed=false`, UI prikazuje Pomoćni signal, ne finalnu preporuku. |
| `inventory` | `/api/analytics/cached/inventory/status`, `/balance`, `/list`, `/insights`, `/store-comparison`, `/forecast`, `/size-curve`, `/rebalance-suggestions`, `/alerts` | filteri po store/supplier, search/sort, compare stores, sku/size/top | `AnalyticsCachePolicy.Inventory.Ttl` = 2 min | `CoreFamilies`, manual invalidate (`inventory`/`all`) | Delimično (policy warning nije svuda primenjen) | `status/list/insights` su najkritičniji za konzistentnost tokom importa i noćnog refresh-a. |
| `data-quality` | `/api/analytics/cached/validation/*` (+ durable pilot report cache u DataQuality endpoints) | validation tip, period (`negative-qty`), report filters + report version | `AnalyticsCachePolicy.DataQuality.Ttl` = 3 min (validation), `CacheExpiration.HeavyAnalytics` za durable report | Data quality worker clear (`data-quality`,`reports`), `CoreFamilies`, manual invalidate (`data-quality`/`all`) | Uglavnom No (validation endpointi ne koriste stale meta helper) | Data quality worker je kanonski invalidation signal za ovu familiju. |
| `pre-post` | `/api/analytics/vendor-sales-nivelacija` (AllEndpoints) | vendor/date/category/filter kombinacije | `CacheExpiration.HeavyAnalytics` = 20 min | `CoreFamilies`, manual invalidate (`pre-post`/`all`) | No | Route je van `CachedAnalyticsEndpoints`; mapirana radi kompletne politike. |
| `pre-nivelacija-prioriteti` | `/api/analytics/pre-nivelacija-prioriteti` | supplier/season/footwear/stock/no-sale/min score/margin, pagination | `CacheExpiration.HeavyAnalytics` za base entry | `CoreFamilies`, manual invalidate (`pre-nivelacija-prioriteti`/`all`) | No | Endpoint ima base cache + page slicing; invalidacija mora ostati sinhronizovana sa import/refresh ciklusom. |
| `reports` | supplier decision report, pilot intake report durable artifacts | canonical report filters + `reportCacheVersion` | `CacheExpiration.HeavyAnalytics` (report payload), report version token | report invalidate (`reports`/`all`) bumpuje version, data-quality worker, nightly/import clear | No | Report cache koristi version token za sigurnije “global invalidate” ponašanje. |

## Dodatne familije (implementirano)

| Cache family | Endpoint(s) | Key inputs | TTL | Invalidated by | Notes |
|---|---|---|---|---|---|
| `supplier-sales` (alias `supplier-sales-stats`) | supplier sales stats ruta (AllEndpoints) | period, store, supplier, `dataScope` | po policy-ju familije | `CoreFamilies`, manual invalidate | Uključeno u `AnalyticsDataQualityHealthWorker`. |
| `shoe-type-sales` (alias `shoe-type-sales-stats`) | shoe-type sales stats ruta (AllEndpoints) | period, store, filteri, `dataScope`, snapshot, `integrityEvidenceId` (ključ v6) | po policy-ju familije | `CoreFamilies`, manual invalidate | Premešteno sa lokalnog `IMemoryCache` na `IAnalyticsCacheService`; integrity evidence u ključu sprečava mešanje snapshot/integrity konteksta. |
| `color-sales` (alias `color-sales-stats`) | color sales stats | period, store, supplier, `dataScope` | po policy-ju familije | `CoreFamilies`, manual invalidate | Prefiks `analytics:color-sales-stats:`. |

## Family -> prefix pravila (implementirano)
- Mapiranje je eksplicitno (`AnalyticsCachePolicy.FamilyPrefixes` + aliasi); `ResolveFamilyPrefixes` baca grešku za nepoznatu familiju.
- `all` (i prazna vrednost na API ulazu) ostaje dozvoljeno i invalidira sve.
- `/api/analytics/cached/cache/invalidate` za nepoznatu familiju vraća `400 unknown_cache_family` (ranije je nepoznata familija brisala sve).

## Mutacije -> invalidacija (implementirano, posle commit-a)
| Mutacija | Familije | Gde |
|---|---|---|
| Access import (uspešan) | `CoreFamilies` | `AccessImportService`, posle commit-a |
| Access import delete-batch | `CoreFamilies` | `AccessImportService` |
| POS prodaja / povraćaj POST/PUT | `CoreFamilies` | `AllEndpoints` -> `AnalyticsCacheMutationInvalidation.InvalidateAfterCommitAsync` |
| Artikli POST/PUT | `CoreFamilies` | isto |
| Transfer confirm/complete/cancel | `CoreFamilies` | `TransferService`, posle commit-a (promene rezervacija, zaliha i kretanja) |
| Snapshot activate/deactivate | `CoreFamilies` | `AnalyticsSnapshotEndpoints` |
| Nightly refresh | `CoreFamilies` | `NightlyAnalyticsRefreshWorker` |
| Data quality worker | `dashboard`, `product-decision-center`, `supplier-decision-hub`, `inventory`, `data-quality`, `color-sales`, `supplier-sales`, `shoe-type-sales`, `reports` | `AnalyticsDataQualityHealthWorker` |

Invalidacija posle mutacije je best-effort: neuspeh se loguje i ne obara već commit-ovanu mutaciju. Transfer draft create/update ne menja stanje zaliha; potvrda, završetak i otkazivanje sada invalidiraju cache. Nivelacija zadržava postojeće ponašanje (nije menjano u ovom prolazu).

## Frontend snapshot i Memory TTL (post-merge dopuna)
- `useReliableAnalyticsQuery` koristi eksplicitni `contextKey` na Inventory, Color Sales, Shoe Type Sales, Pre/Post i Pre-Nivelacija Priority ekranima. Promena primenjenih poslovnih filtera odmah skriva prethodni snapshot (uključujući tabele i izvoz); neuspešan refetch **istog konteksta** sme zadržati prethodni podatak uz stale upozorenje.
- In-memory L1 poštuje deklarisani apsolutni TTL bez dodatnog dvominutnog sliding cap-a. Hybrid L2→L1 i dalje preuzima isključivo preostali TTL.

## Invalidation TODO (preostalo)
1. Cross-instance stampede zaštita (lock je po instanci).
2. Produkcija trenutno radi sa `UseRedis=false` (render.yaml), tj. in-memory po instanci; generacije su spremne za Redis kada se uključi.
3. Potvrditi da zaključavanje i verzionisanje reporta zadovoljavaju stvarni multi-instance runtime pre uključivanja Redis-a.
