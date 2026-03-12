# Supplier Decision Hub Analytics Migration Plan

## Cilj

Prebaciti Supplier Decision Hub sa operativne baze `trendplus` na analiticku bazu `analytics`, tako da:

- operativna baza ostane write model
- analytics baza postane jedini read model za supplier decisioning
- pre/post markdown analitika ostane u PostgreSQL view-evima
- API i frontend ne racunaju analitiku lokalno

## Trenutno stanje

- `SalesFacts`, `SalesLineFacts`, `ProductsDim`, `SuppliersDim`, `SeasonsDim`, `FootwearTypesDim` i `InventoryMovementFacts` vec postoje u `analytics`
- Supplier Decision Hub je do sada citao iz `trendplus`
- supplier quality signal je zavisio od operativnih `povracaj_*` tabela
- `mv_daily_sales_facts`, `vw_vendor_sales_nivelacija`, `vw_nivelacija_did` i `vw_supplier_*` su istorijski ziveli na `trendplus`

## Implementirano u ovoj etapi

1. Dodata je analytics-side tabela `ReturnFacts`.
2. Dodat je compatibility sloj u `analytics`:
   - `"Artikli"`
   - `"Dobavljaci"`
   - `"DnevnikPromena"`
   - `prodaja_zaglavlje`
   - `prodaja_stavke`
   - `povracaj_zaglavlje`
   - `povracaj_stavke`
   - `price_history`
3. Dodat je analytics-side `vw_vendor_sales_nivelacija`.
4. Analytics initializer sada:
   - kreira compatibility sloj
   - backfill-uje `SalesFacts`
   - backfill-uje `ReturnFacts`
   - kreira analytics-side materialized/view sloj za supplier decisioning
5. `SupplierDecisionHubEndpoints` su prevezani na `IAnalyticsDbContext`.
6. Nightly refresh je prosiren da osvezi i analytics-side materialized view-e.

## Sledece etape

1. Zameniti dupliranu scoring logiku u `SupplierDecisionHubEndpoints` direktnim citanjem iz `vw_supplier_decision_score`.
2. Dodati analytics-side stock snapshot ili inventory balance view, da `stock_before_markdown` ne ostane samo proxy.
3. Ojacati sync za `InventoryMovementFacts` sa append-only watermark modela na upsert/replay recent window model.
4. Normalizovati `Pol` kao eksplicitno analytics polje umesto oslanjanja na `ProductsDim.SubCategory`.
5. Prebaciti i ostale dashboard endpoint-e koji jos citaju `trendplus` na analytics read model.
6. Dugorocno: izdvojiti `analytics` na poseban Postgres instance ili read replica.

## Pravilo za naredne izmene

Sve nove supplier analytics metrike treba da se dodaju prvo u SQL view contract u `analytics`, pa tek onda da ih API izlozi, a frontend prikaze.
