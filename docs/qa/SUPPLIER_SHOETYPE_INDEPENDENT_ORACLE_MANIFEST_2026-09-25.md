# Supplier / Shoe Type independent raw-fact oracle (RQ412)

Date: 2026-09-25  
Fixture: `Api.Tests/Fixtures/operations-analytics-all-routes-seed.sql` (shared RQ407 seed)  
Oracle owner: `Api.Tests/Analytics/SupplierShoeTypeRawFactOracle.cs`  
Integration proof: `Api.Tests/SupplierShoeTypeIndependentOracleIntegrationTests.cs`

## Semantics

- Population: `prodaja_stavke` joined to `prodaja_zaglavlje` and `"Artikli"`.
- Period: inclusive `datum_prodaje` between query `fromDate` and `toDate` (UTC midnight bounds).
- Store: optional `id_objekat` on sale header.
- Data scope: article `"DataOrigin"` (`all` / `imported=access` / `existing`).
- Supplier bucket: `supplier_id_at_sale` (NULL = unknown).
- Shoe Type bucket: `shoe_type_id_at_sale` (NULL = unknown).
- Revenue: signed `kolicina * cena`; quantity: signed `kolicina`.
- Oracle deliberately does **not** read current `"Artikli"."IDDobavljac"` / `"IDTipObuce"` for grouping.

## Expected totals (all scope, 2026-07-01 .. 2026-07-07)

| Metric | Value |
| --- | ---: |
| Sale lines | 4 |
| Signed quantity | 5 |
| Revenue (RSD) | 540 |

### Supplier buckets

| supplier_id_at_sale | Units | Revenue |
| --- | ---: | ---: |
| 1 | 2 | 200 |
| 2 | 2 | 260 |
| NULL | 1 | 80 |

### Shoe Type buckets

| shoe_type_id_at_sale | Units | Revenue |
| --- | ---: | ---: |
| 1 | 2 | 200 |
| 2 | 2 | 260 |
| NULL | 1 | 80 |

## Scoped expectations

| Filter | Units | Revenue |
| --- | ---: | ---: |
| `dataScope=imported` | 2 | 260 |
| `dataScope=existing`, `storeId=1` | 3 | 280 |

## Mutation invariant

After updating `"Artikli"."IDDobavljac"` / `"IDTipObuce"` for `OPS-101`, endpoint totals must still match the oracle (attribution frozen on sale lines).
