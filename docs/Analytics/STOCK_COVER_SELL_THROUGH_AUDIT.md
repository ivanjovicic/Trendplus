# Stock Cover / Sell-through Audit

| Layer | Stock cover | Sell-through | Status | Gap |
| --- | --- | --- | --- | --- |
| Backend formula | `currentOnHandUnits / avgDailySalesUnits` u `InventorySignalCalculator.Calculate` | `soldUnits / (openingStockUnits + inboundUnits)` u `InventorySignalCalculator.Calculate` | OK | Formula ostaje nepromenjena; audit potvrđuje implementaciju. |
| DTO | `InventoryListItemDto.StockCoverDays`, `StockCoverStatus`, `StockCoverStatusLabel` | `SellThroughRatio`, `SellThroughStatus`, `SellThroughStatusLabel` | OK | DTO kontrakt je kompletan za signal sloj. |
| API response | `/api/analytics/inventory/list` vraća stock cover polja i labele | `/api/analytics/inventory/list` sada puni opening/inbound iz movement window statistike | OK | Pre audita je sell-through često završavao u `insufficient_data` jer su opening/inbound bili `null`. |
| Frontend table | `InventoryItemsTable` prikazuje `Pokrivenost zalihe` sa `Nedovoljno podataka` ili `Nije dostupno` za `null` numeriku | `InventoryItemsTable` prikazuje `Sell-through` bez fake `0%` kada denominator nije pouzdan | OK | Dodata je guard logika da blokirana preporuka i dalje nudi signal akciju. |
| KPI cards | `InventoryPage` KPI sekcija koristi stock cover statuse (`low_cover`, `out_of_stock_risk`, `slow_stock`, `no_velocity`, `insufficient_data`) | Sell-through KPI koristi canonical status vrednosti (`good`, `warning`, `critical`, `insufficient_data`) | OK | Nema razmimoilaženja status vrednosti između kartica i tabele. |
| Methodology | `analyticsMetricDefinitions.stockCoverDays` koristi `currentOnHandUnits / avgDailySalesUnits` | `analyticsMetricDefinitions.sellThrough` dokumentuje `soldUnits / (openingStockUnits + inboundUnits)` i blokadu na nepouzdan denominator | OK | Methodology pokriva signal putanju u inventory/products ekranima. |
| Action Queue | `low_cover`/`out_of_stock_risk` mapira na `REPLENISH`; `slow_stock`/`no_velocity` na `SLOW_STOCK_REVIEW` | `insufficient_data` ili `recommendationAllowed=false` mapiraju na `SIGNAL_REVIEW` | OK | Uvedena zaštita da blokirana preporuka ne kreira finalnu akciju. |
| Tests | `Api.Tests/InventorySignalCalculatorTests.cs` pokriva no-fake-zero i labele | Frontend specovi pokrivaju `Nedovoljno podataka`, `Nije dostupno`, KPI explain i `SIGNAL_REVIEW` mapiranje | OK | Dodate rupe za recommendation block i UTF-8 label check. |

## Formula Notes

- Stock cover ostaje `daysOfSupply = currentOnHandUnits / avgDailySalesUnits`.
- Sell-through ostaje `sellThrough = soldUnits / (openingStockUnits + inboundUnits)`.
- U inventory list putanji opening stock se aproksimira kao `currentOnHand - netMovementWindow`, gde je `netMovementWindow` zbir `Kolicina` iz `InventoryMovementFacts` za isti period.
- Inbound ulazi se računaju preko `TipPromeneConstants.UlazTypes` (`Ulaz robe`, `Prenos ulaz`, `Povrat kupca`).

## No Fake Zero Rules

- `openingStockUnits + inboundUnits <= 0` vraća `SellThroughRatio = null` i status `insufficient_data`.
- Nedostajući opening/inbound ulazi ne prikazuju `0%` sell-through u UI.
- `avgDailySalesUnits <= 0` uz pozitivan lager vraća `no_velocity` i `StockCoverDays = null`.
- `stock = 0` uz pozitivan velocity vraća `out_of_stock_risk`.
- `recommendationAllowed = false` degradira queue akciju na `SIGNAL_REVIEW`.
