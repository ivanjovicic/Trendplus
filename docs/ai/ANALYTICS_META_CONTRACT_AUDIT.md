# Analytics Meta Contract Audit

Last updated: 2026-05-24
Scope: core analytics families used by `/analytics`, `/analytics/products`, `/analytics/supplier`, `/analytics/inventory`, `/analytics/data-quality`, plus nivelacija flows.

## Coverage Table

| Endpoint family | Success meta | Empty meta | Warning/partial meta | Error meta | CorrelationId | Frontend meta-aware | Fake-zero guarded | Notes |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Cached dashboard/bootstrap | Yes | Partial | Partial | Yes | Partial | Partial | Yes | `CachedAnalyticsEndpoints.cs:1329` confirms meta on route; explicit empty/warning/correlation contract should be re-verified for all branches. |
| Cached products/decision-center | Yes | Partial | Partial | Yes | Partial | Partial | Yes | `CachedAnalyticsEndpoints.cs:1175` confirms error-meta branch; empty/warning/correlation behavior should be validated across no-data and fallback paths. |
| Supplier decision hub (summary/ranking) | Yes | Yes | Yes | Yes | Yes | Partial | Yes | `SupplierDecisionHubEndpoints.cs:100`, `:207` confirm meta + correlation propagation; frontend parity should be re-checked on all tabs/report export. |
| Inventory family | Yes | Partial | Partial | Yes | Partial | Partial | Yes | `InventoryEndpoints.cs:48`, `:394` use `AnalyticsResponseMetaFactory`; verify every route in family returns consistent empty/partial/correlation metadata. |
| Data quality family | Yes | Yes | Partial | Yes | Partial | Partial | Yes | `DataQualityEndpoints.cs:19`, `:179` are meta-aware; warning/partial and correlation consistency across all data-quality routes should be validated. |
| Pre/post nivelacija | Yes | Partial | Partial | Partial | Partial | Yes | Yes | `AllEndpoints.cs:3150`, `:3995`, `:6476`, `:6527` + `VendorSalesNivelacijaModels.cs:170` expose meta field; branch-level error/warning consistency still needs hard verification. |
| Pre-nivelacija prioriteti | Yes | Partial | Partial | Yes | Yes | Yes | Yes | `PreNivelacijaPriorityEndpoints.cs:44`, `:531` confirm error meta + correlation; full empty/warning coverage should be validated for all filter combinations. |

## Partial/No Follow-up

### Cached dashboard/bootstrap (P0)
- Missing: prove `empty meta`, `warning/partial meta`, and `correlationId` on every non-happy path.
- Verify file(s): `Api/Endpoints/CachedAnalyticsEndpoints.cs` (all dashboard bootstrap branches).

### Cached products/decision-center (P0)
- Missing: standardized empty/no-data metadata and warning/partial metadata on fallback/insufficient-data paths, plus correlation on all errors.
- Verify file(s): `Api/Endpoints/CachedAnalyticsEndpoints.cs` (decision-center route and helpers).

### Supplier decision hub frontend parity (P1)
- Missing: confirm all supplier UI surfaces (summary, ranking, scorecard/report) consume meta uniformly via shared helpers.
- Verify file(s): `Klijent/clientapp/src/services/supplierDecisionHubApi.ts`, supplier pages and report components.

### Inventory family breadth (P1)
- Missing: guarantee non-core inventory analytics endpoints match same meta contract.
- Verify file(s): `Api/Endpoints/InventoryEndpoints.cs`, `Klijent/clientapp/src/services/analyticsApi.ts` inventory calls.

### Data-quality warning/correlation consistency (P1)
- Missing: enforce partial/warning semantics and correlationId on all failure branches.
- Verify file(s): `Api/Endpoints/DataQualityEndpoints.cs`, data-quality frontend service/page wiring.

### Pre/post nivelacija branch consistency (P1)
- Missing: ensure every response path sets full meta (including error + correlation + partial semantics), not only model-level `Meta` availability.
- Verify file(s): `Api/Endpoints/AllEndpoints.cs`, `Api/Models/VendorSalesNivelacijaModels.cs`.

### Pre-nivelacija prioriteti empty/partial coverage (P2)
- Missing: confirm empty/no-data and partial semantics are emitted for edge filter windows, not only errors.
- Verify file(s): `Api/Endpoints/PreNivelacijaPriorityEndpoints.cs`, `Klijent/clientapp/src/services/preNivelacijaApi.ts`.

## Contract Rule

- Backend greška nikad ne sme biti predstavljena kao `0 RSD` uspeh.
- `empty dataset` nije error: treba vratiti `success=true` + odgovarajući `emptyReason`/`insufficient_data`.
- `insufficient_data` nije finalna preporuka.
- `warning/partial` mora biti vidljiv u UI (banner/badge/notice), ne tih fallback.

## Frontend Rule

- Servisi treba da koriste shared meta helper (`analyticsResponseMeta` util pattern) za error/empty/warning tumačenje.
- Page fajlovi ne treba da uvode ad-hoc parsiranje meta ugovora mimo shared helpera/komponenti (`AnalyticsErrorState`, `AnalyticsEmptyState`, `AnalyticsTrustHeader`, refresh/status banner).

## Regression Checklist (pre merge)

1. `dotnet build`
2. Relevantni `dotnet test` paketi za dirnute endpoint porodice
3. `npm run check:analytics-guardrails` (iz `Klijent/clientapp`)
4. `npm run build` (iz `Klijent/clientapp`)
5. Manual smoke:
   - `/analytics`
   - `/analytics/products`
   - `/analytics/supplier`
   - `/analytics/inventory`
   - `/analytics/data-quality`

## Known Gaps

- Full `dotnet test` suite nije uvek pokrenut u svakom patch-u.
- Lokalne nepovezane izmene u radnom stablu mogu uticati na audit signal.
- Endpointi van core analytics porodica nisu nužno pokriveni ovim auditom.

