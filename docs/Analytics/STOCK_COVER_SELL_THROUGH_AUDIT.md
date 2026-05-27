# Stock Cover / Sell-through Audit

Datum: 2026-05-27  
Repo: ivanjovicic/Trendplus

## End-to-end tabela

| Layer | Stock cover | Sell-through | Status | Gap | Next action |
|---|---|---|---|---|---|
| Backend formula | `daysOfSupply = currentOnHandUnits / avgDailySalesUnits` u `InventorySignalCalculator.Calculate` | `sellThrough = soldUnits / (openingStockUnits + inboundUnits)` u `InventorySignalCalculator.Calculate` | OK | Nema funkcionalnog gapa u kalkulatoru. | Odrzavati test pokrice za edge-case grane (`no_velocity`, `out_of_stock_risk`, `insufficient_data`). |
| DTO/API | `InventoryListItemDto.StockCoverDays/Status/StatusLabel` i `ProductDecisionCenterRowDto.StockCover*` popunjeni iz backend signala | `InventoryListItemDto.SellThrough*` i `ProductDecisionCenterRowDto.SellThrough*` popunjeni iz backend signala | OK | Pre hardening-a je cached putanja slala `opening/inbound = null` pa je sell-through cesto bio `insufficient_data`. | Zadrzati movement-window unos i u cached inventory i u product decision putanji. |
| Frontend table | `InventoryItemsTable` prikazuje "Pokrivenost zalihe" + tekstualni status | `InventoryItemsTable` prikazuje "Sell-through" + tekstualni status | OK | Nema fake `0` prikaza za null signal. | Odrzavati render fallback: `Nedovoljno podataka` za `insufficient_data`, `Nije dostupno` za ostale `null` slucajeve. |
| KPI cards | `InventoryPage` i `ProductDecisionCenterPage` broje status-e po backend vrednostima (`low_cover`, `out_of_stock_risk`, `slow_stock`, `no_velocity`, `insufficient_data`) | KPI za sell-through koristi backend `sellThroughStatus` i ne racuna ratio lokalno | OK | KPI agregati su UI agregacija backend statusa, bez izmisljanja signala. | Ostaviti backend kao source-of-truth za status/label/reasonCodes. |
| Methodology | `analyticsMetricDefinitions.stockCoverDays` postoji i dokumentuje formulu + blokade | `analyticsMetricDefinitions.sellThrough` postoji i dokumentuje formulu + denominator blokade | OK | Nema. | Odrzavati uskladjenost sa kalkulatorom pri promeni formule/threshold-a. |
| Action Queue | `low_cover`/`out_of_stock_risk` -> `REPLENISH`; `slow_stock`/`no_velocity` -> `SLOW_STOCK_REVIEW` | `insufficient_data` ili `recommendationAllowed=false` -> `SIGNAL_REVIEW` | OK | Nema. | Odrzavati mapiranje u `InventoryPage` i `ProductDecisionCenterPage` testovima. |
| Tests | Backend testovi pokrivaju stock cover/sell-through formule, labele i insufficient_data grane | Frontend testovi pokrivaju prikaz `Nedovoljno podataka`/`Nije dostupno` i queue mapiranje | OK | Nema aktivnog regresionog gapa u ovom pass-u. | Nastaviti ciljane testove za signal i action mapiranje pri svakoj izmeni. |

## Potvrda formula

- Stock cover: `daysOfSupply = currentOnHandUnits / avgDailySalesUnits`
- Sell-through: `sellThrough = soldUnits / (openingStockUnits + inboundUnits)`

Cached i non-cached API putanje sada koriste movement-window ulaze za `openingStockUnits` i `inboundUnits` (umesto hardcoded `null`), pa sell-through vise nije sistemski degradiran na `insufficient_data` kada postoje validni podaci.

## No-fake-zero pravila

- `openingStockUnits + inboundUnits <= 0` -> `SellThroughRatio = null`, `SellThroughStatus = insufficient_data`.
- Missing opening/inbound ulazi -> `insufficient_data` (bez fallback `0%`).
- `avgDailySalesUnits <= 0` uz pozitivan lager -> `no_velocity`.
- `stock = 0` uz pozitivan velocity -> `out_of_stock_risk`.
- Frontend prikaz:
  - `insufficient_data` + null vrednost -> `Nedovoljno podataka`
  - ostali null slucajevi -> `Nije dostupno`

## UI copy potvrda

- Kolone: `Pokrivenost zalihe`, `Sell-through`, `Signal`.
- Tekstualni status je vidljiv (status nije oslonjen samo na boju).

## Methodology potvrda

- `analyticsMetricDefinitions` sadrzi:
  - `stockCoverDays`
  - `sellThrough`

## Action Queue mapiranje potvrda

- `low_cover` i `out_of_stock_risk` -> `REPLENISH`
- `slow_stock` i `no_velocity` -> `SLOW_STOCK_REVIEW`
- `insufficient_data` ili `recommendationAllowed=false` -> `SIGNAL_REVIEW`
