# Analytics Calculation Recheck - 2026-09-06

## Outcome

This is a second, bounded recheck of the previous analytics reliability work. The earlier stability audit created `RQ157`-`RQ161`, but those prompts are still `WAITING` and their runtime fixes were not executed. This pass does not duplicate them. It covers only newly evidenced analytical calculation/state risks and excludes trend, Shopify, forecast and test-only feature work.

The canonical queue keeps exactly one `READY` prompt (`RQ154`). The five new prompts are recorded as `WAITING` in executable order so the queue protocol remains valid; they are not claimed or marked complete by this documentation-only recheck.

## New findings

| Finding | Area | Evidence | User risk | Queue prompt |
|---|---|---|---|---|
| N1 | Inventory sell-through | `InventorySignalCalculator.CalculateSellThrough` blocks only when both opening stock and inbound are null, then coalesces each missing component to zero (`Api/Endpoints/InventorySignalCalculator.cs:137-151`). Existing tests cover both missing, not exactly one missing. | A partial denominator can produce a plausible ratio/status and feed an inventory decision. | `RQ162` |
| N2 | Supplier post-nivelacija | Direct supplier SQL coalesces an unmatched post observation to zero (`Api/Endpoints/SupplierDecisionHubEndpoints.cs:2794-2816`), then uses it in shares, dependency, dead-stock, confidence and recommendation branches (`2829-2867`, `2936-2979`, `3011-3070`). | No post evidence can look like measured zero and support a trusted supplier recommendation. | `RQ163` |
| N3 | Pre-nivelacija margin | The candidate builder coalesces purchase cost to zero and treats `PurchasePrice >= 0` as complete (`Api/Endpoints/PreNivelacijaPriorityEndpoints.cs:258-266`). Existing cost policy treats null/non-positive cost as missing. | Missing/zero cost can generate a 100% margin and influence score/scenarios. | `RQ164` |
| N4 | Data Quality period/scope | `TopOffendersSql` (`Infrastructure/Services/AnalyticsDataQualityHealthService.cs:17-29`) and the issues handler (`Application/Analytics/Queries/GetDataQualityIssues/GetDataQualityIssuesHandler.cs:31-43`) have only a lower date bound. `CaptureAsync` uses a date bound but applies `dataScope` through article origin (`...HealthService.cs:124-147`) while the documented offender rule is sale-header origin. | Future sales and mismatched populations can change health percentages and offender impact for the same named window/scope. | `RQ165` |
| N5 | Action timeline period | Both the projection (`Infrastructure/Services/Analytics/AnalyticsActionTimelineFilterProjection.cs:23-42`) and product-decision timeline path (`Api/Endpoints/CachedAnalyticsEndpoints.cs:5376-5392`) silently swap reversed dates. | A valid-looking timeline/export can be returned for a period different from the requested one. | `RQ166` |

## What was rechecked and not reopened

- `RQ157` remains the owner for Product Decision baseline, margin and coverage unknown-state behavior.
- `RQ158` remains the owner for null inventory quantity/minimum semantics; N1 is specifically the sell-through denominator contract.
- `RQ159` and `RQ160` remain the owners for inventory summary arithmetic and synthetic health-series behavior.
- `RQ161` remains the owner for the `AnalyticsDetails` page period/trend state; N5 is the action-timeline projection and export path.
- `RQ140` remains the owner for broad pre/post causal comparability; N2 is a narrower missing post-observation state bug in supplier decision SQL.
- `RQ148` remains the owner for broad sales/margin measurement basis; N3 is the pre-nivelacija endpoint's concrete completeness check.
- `RQ05`, `RQ06`, `RQ118`, `RQ135` and `RQ144` remain the owners for Data Quality scope, refresh and denominator contracts; N4 is the residual upper-bound and cross-surface population consistency check.

## Existing tests reviewed

- `Api.Tests/InventorySignalCalculatorTests.cs`: both-null denominator, zero denominator and normal sell-through are covered; one-null denominator is not.
- `Api.Tests/SupplierDecisionHubContractTests.cs` and `Api.Tests/SupplierDecisionSchemaSqlTests.cs`: supplier DTO/cache contracts and cache coverage are covered; absent post observation versus measured zero through the direct query is not.
- `Api.Tests/PreNivelacijaScoringServiceTests.cs` and `Api.Tests/PreNivelacijaQueryFailureMetaTests.cs`: service scenarios and query failure metadata are covered; endpoint `PurchasePrice=0` completeness is not.
- `Api.Tests/AnalyticsDataQualityHealthServiceTests.cs` and `Api.Tests/DataQualityIssuesHandlerTests.cs`: basic scope and health cases are covered; future-date exclusion and health/offender population parity are not.
- `Api.Tests/AnalyticsActionTimelineFilterProjectionTests.cs` and `Api.Tests/DecisionTimelineExportProjectionTests.cs`: valid/outside-period and export parity are covered; reversed-period rejection is not.

## Git history reviewed

The concrete file history was inspected before writing the prompts. Relevant prior fixes include:

- Inventory: `e4b71276`, `775ad2a7` (`feat(inventory): add stock cover and sell-through signals`), plus `c8279378` pending analytics/inventory hardening.
- Supplier decision: `29a5943a` (`fix(analytics): harden trust metadata and decision surfaces`), `4c8844b9` freshness/empty indicators, `1a78a0d9` supplier delivery repair and `569705f1` recommendation trust.
- Pre-nivelacija: `a84d8a42` (`fix(analytics): enforce trusted nivelacija and parity contracts`) and `da18187c` preservation of unknown numeric evidence.
- Data Quality: `6ce1591d` scope/readiness alignment and `ae8835e8` sales-window scope repair, with earlier denominator hardening in `5b8547b0` and `c5e6ce68`.
- Timeline: `29a5943a` trust metadata/decision surfaces and `69511be0` incomplete-evidence hardening.

These histories explain why the prompts are narrow follow-ups rather than a second broad reliability rewrite. No reviewed commit proves the five specific counterexamples above.

## Queue/delivery truth

- `RQ154` remains the only `READY` task by protocol.
- `RQ162`-`RQ166` are new `WAITING` prompts, not completed fixes and not claimed work.
- No backend or frontend runtime code was changed in this recheck.
- No live database, refresh, browser console or runtime build proof is claimed.

