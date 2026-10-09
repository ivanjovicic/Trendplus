# Analytics regression gap inventory — RQ598

Date: 2026-10-09  
Owner: Analytics Reliability / Codex  
Registration/claim base: `origin/main` `052a64b05d001e1adeb7971562bf03c116f77622`  
Scope: repository-local audit and deterministic frontend regression proof; no production/runtime formula change.

## Verdict

The shipped analytics decision surfaces have current source-to-test and independent-oracle coverage for the named retail invariants. Two deterministic export/query identity gaps were verified and closed with focused frontend tests:

1. Daily Sales now proves that a paginated table still exports the complete sorted backend row set.
2. Shoe Type now proves that export receives the complete backend identity set, including Serbian names.

No backend defect or formula change was demonstrated. No new RQ599 prompt is registered: its promotion condition (a new reproducible uncovered defect after RQ598) was not met. Production certification, live freshness and business validation remain open operational/validation work owned by STAB16/RQ600 and the relevant release owners.

## Source-to-test matrix

| Surface | Source, grain and policy | Focused proof / independent oracle | Export/query parity | Classification |
|---|---|---|---|---|
| Daily Sales | `Api/Endpoints/DailySalesStatsEndpoints.cs:22`; `Api/Services/DailySalesStatsService.cs:47,71-83,471+,769`; receipt-line daily grain, half-open dates, signed returns and DUG/KOREKCIJA policy | `DailySalesStatsServiceTests.cs:388,457,514,608,672,721,766,810,878,924`; `DailySalesStatsIntegrationTests.cs:76,111,140,177,201,221,238,282`; `OperationsAnalyticsRawFactOracle.cs:131`; cross-screen invariant and certified metamorphic suites | `DailySalesStatsPage.premium.spec.tsx:363` now asserts 30 full sorted rows remain behind page 2 | COVERED; former export identity gap CLOSED |
| Supplier | `Api/Endpoints/AllEndpoints.cs:1112`; supplier aggregation and margin policy | supplier unit/integration suites, independent supplier/shoe oracle and raw-fact oracle; unknown IDs/names, missing cost, returns, scopes and denominators | supplier page export/detail specs and golden manifest | COVERED |
| Shoe Type | `Api/Endpoints/AllEndpoints.cs:2199`; shoe-type aggregation and margin policy | supplier/shoe independent oracle (`SupplierShoeTypeIndependentOracleIntegrationTests.cs:25,33,41,49,57`), integration filters/margins/scope and identity tests | `ShoeTypeSalesStatsPage.spec.tsx:171` asserts complete `Patike,Čizme` identity set | COVERED; former export identity gap CLOSED |
| Color | `Api/Endpoints/AllEndpoints.cs:3014`; color aggregation and source scope | `ColorSalesStatsIndependentOracleIntegrationTests.cs:34,57`; color page status/error/empty/stale/export/detail suites | page export/detail tests preserve backend status and rows | COVERED |
| Supplier Footwear / assortment | vendor nivelacija route `AllEndpoints.cs:3989`, pair route `5388`; scoped supplier/footwear aggregation | `AssortmentNivelacijaOracleTests.cs:32,81,134,158,196,214,267,362,380,418,467`; RQ140 pre/post consumer closure | vendor/pre-post export/detail and scope tests | COVERED |
| Inventory | `Api/Endpoints/InventoryEndpoints.cs:27,79,101,505,509,543,547,927,988,1182,1388,1606`; item/store grain, valuation and action policy | `InventoryValueCoverageTests`, `InventoryListEndpointIntegrationTests`, `InventorySnapshotContractTests`, `InventoryOperationsIntegrityProbeTests`, `InventoryActionDecisionPolicyTests` | export builds the same focused dataset as the list; snapshot export/error/empty/stale proofs | COVERED |
| Vendor Pre/Post | vendor/`PrePost` API response is authoritative for applied/draft, scope, margin and comparison semantics | vendor scope, malformed/nonfinite, previous-failure, identity, denominator, export/detail and status suites; `vendorSalesNivelacijaApi.scope.spec.ts` | export/detail reuse authoritative response rows | COVERED |
| Pre-Nivelacija | `PreNivelacijaPriorityEndpoints.cs:69,97,824,1446,1632,1655`; priority row/filter/paging/store grain | `PreNivelacijaPriorityOracleIntegrationTests.cs:34,109,167,198,236,368,703,723`; frontend invalid-filter, scope/store, identity and negative-value suites | export uses the same filtered rows (`...spec.tsx:1044`) | COVERED |
| Shift | daily shift summary uses bounded daily/shift grain | `dailyShiftSummary.spec.ts:26,34,43,55,67`; service shift-boundary/DST/unknown-time tests | summary is derived from the same daily response scope | COVERED |
| Action screens | `Api/Endpoints/AnalyticsActionsEndpoints.cs:14,18,90,148,223,303+,416`; action lifecycle/status/outcome contract | `AnalyticsActionItemServiceTests.cs` lifecycle/status/outcome/denominator/upsert/sourceKey tests; endpoint auth/shared-population; Product Decision Center and Inventory queue specs | queue/list/summary screens preserve error, empty and not-measured states | COVERED |

## Adversarial checklist

| Case | Result and evidence |
|---|---|
| Unknown ID vs display name | COVERED by Daily service unknown/negative/dangling attribution tests, supplier identity specs and Pre-Nivelacija identity fixtures. |
| Previous-only categories | COVERED by `OperationsAnalyticsAllRoutesIntegrationTests.cs:205-206`, certified metamorphic previous-only test (`AnalyticsCertifiedRetailMetamorphicTests.cs:167`) and supplier/shoe golden manifest. |
| Negative/zero margins | COVERED by `AnalyticsMarginPolicyTests.cs:118,139,160,196,213,258,276` and supplier unit tests for negative, zero and non-finite denominators. |
| Missing costs | COVERED by supplier tests, `InventoryValueCoverageTests` and certified missing-cost metamorphic proof (`...:180`). |
| Returns/adjustments | COVERED by Daily DUG/KOREKCIJA and signed-return tests (`DailySalesStatsServiceTests.cs:457,514`) plus metamorphic tests (`...:88,120`). |
| Date/store parity | COVERED by Daily half-open/adjacent-day/store integration tests, raw oracle half-open SQL, and scope/store suites across vendor, inventory and Pre-Nivelacija. |
| Empty, missing, error, stale | COVERED by Daily empty contract, Color status suites, Pre/Post failure/empty/stale suites, Inventory snapshot contracts and action not-measured specs. |
| Applied vs draft filters | COVERED by `analyticsFilterDraft.spec.ts:19`, Color/Shoe page specs and Pre-Nivelacija invalid-draft tests. |
| Top-N, counts and denominators | COVERED by Daily top-N integration, Product Decision Center limit tests, supplier residual/top-5 tests and Data Quality returned-vs-total proofs. |
| Rounding | COVERED by certified rounding proof (`AnalyticsCertifiedRetailMetamorphicTests.cs:243`) and the shared frontend formatters. |
| Production/live freshness and business certification | NEEDS_AUTHORITY; not a repository regression gap. STAB16 remains the production/read-only authority lane and business validation remains open. |

## Tests added

- `Klijent/clientapp/src/pages/__tests__/DailySalesStatsPage.premium.spec.tsx:363` adds a toolbar probe and asserts the full 30-row sorted export set remains unchanged while the visible table paginates.
- `Klijent/clientapp/src/pages/ShoeTypeSalesStatsPage.spec.tsx:171` adds a deterministic two-row backend fixture and asserts both shoe-type identities reach the export toolbar.

Both tests are independent of production implementation details beyond the existing toolbar contract, deterministic, and fail if the export/query path is accidentally changed to the visible page slice or loses row identity.

## Follow-up decision

RQ599 remains unregistered because RQ598 produced no open reproducible defect. RQ600 remains independently READY for the action-eligibility/value-contract audit. Production certification and business validation are explicitly not represented as completed by this audit.
