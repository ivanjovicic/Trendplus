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

## RQ446 adversarial appendix

The shared fixture now contains an out-of-window adversarial appendix for
Supplier/Shoe Type certification. The immutable expected-output corpus is
[`SUPPLIER_SHOETYPE_ADVERSARIAL_GOLDEN_MANIFEST_2026-09-26.json`](SUPPLIER_SHOETYPE_ADVERSARIAL_GOLDEN_MANIFEST_2026-09-26.json);
its SHA-256 sidecar is
[`SUPPLIER_SHOETYPE_ADVERSARIAL_GOLDEN_MANIFEST_2026-09-26.sha256`](SUPPLIER_SHOETYPE_ADVERSARIAL_GOLDEN_MANIFEST_2026-09-26.sha256).
The original July RQ407 totals above remain unchanged. RQ446 cases explicitly
cover fractional boundaries, signed returns, mixed-case/whitespace
`DUG`/`KOREKCIJA`, store/origin scope, known labels versus null identity,
previous-only Shoe Type rows, cost coverage, top-N unknown, master mutation,
duplicate replay and cache evidence states.
