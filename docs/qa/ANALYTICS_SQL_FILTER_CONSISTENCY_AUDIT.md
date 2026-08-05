# Analytics SQL Filter Consistency Audit

Date: 2026-08-05
Repo: `ivanjovicic/Trendplus`
Prompt: Q81
Status: docs/tests audit only

## Goal

Map how `dataScope`, `storeId`, and `supplierId` are interpreted across the main analytics SQL helpers before any runtime rewrite.

## What was checked

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- `Api/Endpoints/DecisionBoardEndpoints.cs`
- `Api/Endpoints/InventoryEndpoints.cs`
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `Api/Endpoints/AllEndpoints.cs`
- `docs/qa/ANALYTICS_DATASCOPE_CONSISTENCY_AUDIT.md`

## Shared baseline

`NormalizeDataScope` is stable across the audited helpers:

- null, blank, and unknown values collapse to `all`
- `imported` means `DataOrigin == "access"`
- `existing` means `DataOrigin == "existing" OR NULL OR ""`
- `all` means no origin predicate

That part is consistent.

## Findings

### 1. Product Decision Center is dual-origin by design

`BuildProductDecisionCenterAsync` scopes article eligibility by article origin, but sales and previous-period revenue by sale-header origin.

Observed behavior:

- article rows use `Artikli.DataOrigin`
- current and previous sales use `ProdajaZaglavlja.DataOrigin`
- last-sale lookup uses the same sale-header gate

Risk:

- the response mixes article-origin and sale-header-origin semantics in the same payload
- that is acceptable only if the contract stays explicit

### 2. Lost-sales validation is safer, but validation still defaults to the widest scope

`GetLostSalesSnapshotAsync` prefers the trusted view when the request is `all` and no store or supplier is requested.

Observed behavior:

- trusted view path is only used for the broadest request shape
- fallback SQL applies both article and sale-header scope predicates
- `validation/lost-sales` still defaults to `dataScope=all`

Risk:

- a scoped UI can still receive a broad validation result unless the caller passes scope intentionally
- the endpoint is conservative, but request-lineage still needs to be visible in docs/UI

### 3. Inventory insight and workflow paths do not accept `dataScope`

`InventoryEndpoints.GetInventoryInsightsAsync` and the linked workflow helpers accept `storeId` and `supplierId`, but not `dataScope`.

Observed behavior:

- `DecisionBoardEndpoints` forwards `dataScope` to Product Decision Center and supplier filters
- the same Decision Board call does not forward `dataScope` into inventory insights/workflow

Risk:

- the same board request can show differently scoped product/supplier data next to inventory data that is effectively forced to the default scope
- this is a cross-surface consistency issue, not a formula issue

### 4. Supplier decision hub filters article eligibility, not sale-header scope

`BuildRowFilters` in `SupplierDecisionHubEndpoints` applies `dataScope` to `Artikli.DataOrigin`, then layers `storeId` and `supplierId` filters.

Observed behavior:

- supplier rows are article-gated first
- the filter builder does not switch to sale-header `DataOrigin`
- downstream queries reuse the same filter builder, so the contract is article-centric

Risk:

- supplier decision numbers can be read as sales-header scoped when they are actually article-eligibility scoped
- that is fine only if the UI and docs keep the distinction explicit

### 5. Supplier-sales-stats uses store/scope differently from other sales helpers

The supplier-sales-stats route in `AllEndpoints.cs` uses `dataScope` in the cache key and in its query path, but the supporting helpers still behave differently from the product/dashboard helpers.

Observed behavior:

- `GetSalesDataWindowAsync` is keyed by `storeId` and `dataScope`, not supplier
- the period rows in the supplier-sales-stats path use article origin gates in the aggregation steps
- that is different from the header-scoped sales helpers used by the dashboard and Product Decision Center

Risk:

- the same `dataScope` label does not mean the same thing everywhere
- this is the main reason the filter contract needs to be documented instead of inferred

## Summary matrix

| Surface | Main scope rule | Notes |
|---|---|---|
| Product Decision Center | Dual-origin | Article eligibility uses article origin, sales use sale-header origin |
| Lost-sales snapshot | Dual-path | Trusted view for broad requests, fallback uses article + sale-header gates |
| Inventory insights/workflow | Store/supplier only | No `dataScope` parameter today |
| Supplier decision hub | Article-centric | `dataScope` applies to article eligibility |
| Supplier-sales-stats | Mixed helper contract | `dataScope` is present, but the helper stack is not identical to dashboard sales helpers |

## Follow-up prompts

- RQ05-F2 for inventory and Decision Board forced-all vs article-scoped meta
- RQ05-F1 for Product Decision Center dual-origin alignment or explicit contract tests
- RQ05-F4 for lost-sales validation request-scope handling
- new SQL contract prompt for supplier-sales-stats helper parity if we want to unify the sales-header rules later

## Non-goals

- no SQL formula rewrite
- no frontend routing change
- no action write security change
- no runtime behavior change in this audit
