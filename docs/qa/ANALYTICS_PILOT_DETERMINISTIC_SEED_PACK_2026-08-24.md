# Analytics pilot deterministic seed pack — 2026-08-24

Scope: `RQ114`
Pack ID: `pilot-analytics-proof-pack-v1`

Machine-readable mirror:

- `Api.Tests/PilotAnalyticsSeedPack.cs`

This manifest names the reusable deterministic proof basis that later analytics reliability prompts can cite without cloning ad hoc fixture data.

## Canonical included families

| Family | Authoritative basis | Requested period / scope | Expected outputs | Allowed explicit states |
|---|---|---|---|---|
| Product Decision Center | `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.SeedDecisionDataAsync` | `2026-05-21` through `2026-06-19`, `storeId=1`, `supplierId=null`, `dataScope=all` | row `101` stays `REPLENISH` with `expectedImpactRsd=500`; row `102` stays `FIX_DATA` with critical blockers; summary remains `1 replenish / 1 bad-data`; unknown store returns explicit empty success meta; freshness is intentionally historical and may surface as stale | `no_rows_for_period`, `insufficient_data`, stale freshness |
| Supplier Decision / Sales | `Api.Tests/Fixtures/supplier-sales-stats-seed.sql` + `Api.Tests/SupplierDecisionHubContractTests.cs` | `2026-02-15` through `2026-03-15`, `sezonaId=1`, `dataScope=all` | Supplier A remains the dominant deterministic row in the integration fixture; unknown supplier normalizes into `Nepoznato`; missing cost metadata stays visible; repeated identical requests stay deterministic | explicit empty dataset, warning/partial trust state, unavailable report |
| Inventory | `Api.Tests/InventoryListEndpointIntegrationTests.Seed` | cached and uncached inventory list/detail/insights routes | `OOS-101` stays out-of-stock risk with recommendation allowed; `EMPTY-104` stays `insufficient_data` with recommendation blocked; sort/pagination stay deterministic; empty search stays explicit success | explicit empty success, `insufficient_data`, unavailable dependency, partial/warning |
| Analytics Actions | `Api.Tests/AnalyticsActionsEndpointsTests.AnalyticsActionsTestHost.SeedActionAsync` | `/api/analytics/actions` and action status/outcome flows | admin key remains required for protected mutations; ledger snapshot records preserve source recommendation metadata; status/outcome patches round-trip through the store | explicit empty list, unavailable source state, warning state for partial/outcome gaps |
| Decision Board | `Api.Tests/DecisionBoardEndpointsTests.CreateProductRow` plus the product-decision seed pack | `2026-06-19` snapshot | `expectedImpactRsd` stays authoritative when present; blocked statuses keep lost sales off impact; insufficient data stays a truthful empty-style board state | section-level no-signals, honest blocker/warning, explicit unavailable aggregate state |
| Pilot Intake / Readiness | `Api.Tests/AnalyticsReportsContractTests.CreatePilotIntakeReport` | pilot intake report window used by the readiness page | ready datasets stay success and populate KPIs; below-threshold readiness disables recommendation but stays visible; no-import stays an explicit empty success | explicit empty success, `insufficient_data`, warning/degraded report state |

## Excluded for now

Dashboard is intentionally not listed as a canonical seeded-non-empty family in this pack. The current evidence still treats that surface as route/meta/smoke-driven rather than a separately isolated seeded proof; that remains the follow-up tracked by `RQ115`.

## Notes

- The pack is deterministic: it uses fixed dates and stable IDs instead of `DateTime.UtcNow` or other environment-dependent values.
- Inventory is the intentional exception for freshness-sensitive smoke behavior: it keeps a runtime-relative base timestamp so the same seed shape still exercises the out-of-stock and sell-through signal path.
- The manifest intentionally preserves explicit empty/degraded states instead of forcing every family to look non-empty.
- Later prompts should reuse this pack as the authoritative proof basis rather than cloning new fixtures for the same pilot families.
