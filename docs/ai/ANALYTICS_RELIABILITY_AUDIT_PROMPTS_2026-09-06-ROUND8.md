# Analytics Reliability Audit Prompts - Round 8

Date: 2026-09-06
Repository: `C:\Users\Ivan\source\repos\Trendplus2`
Scope: production analytics surfaces only; no standalone Trend, forecast, Shopify, vendor integration or test-only functionality.
Queue mode: `direct-user-request`; no prompt was claimed and no existing lock was changed.

## Audit Result

This round found two new, concrete gaps on the Supplier Decision Hub family. Both are written as `WAITING` prompts in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`:

| Prompt | Surface | New gap | Primary owner |
|---|---|---|---|
| `RQ249` | `/analytics/supplier` / Supplier Decision Hub detail | A blocked recommendation still exposes `Dodaj u akcije` and writes a `signal_check` action. | Frontend actionability boundary, coordinated with central action contract |
| `RQ250` | Supplier Decision Hub, client report and server supplier report | Margin contribution uses two different formulas: Hub/client `revenue * preMarkdownMarginPct`; server report additionally multiplies by `fullPriceRevenueShare`. | Backend metric contract and all consumers |

## Evidence Map

### RQ249: blocked action CTA/write

- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx:375` resolves `recommendationAllowed` from backend trust metadata.
- `SupplierDecisionHubPage.tsx:764-802` still creates a `signal_check` action and calls `upsertAnalyticsAction` when that value is false.
- `SupplierDecisionHubPage.tsx:1248-1264` always renders the selected-row `Dodaj u akcije` button.
- `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.spec.tsx:231-268` covers the blocked banner but not the detail CTA or write prohibition.
- `8fb20b11` introduced the Hub action path; `29a5943a` hardened trust presentation but did not close this CTA/write boundary.

### RQ250: margin formula parity

- `Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx:391-395` derives row margin contribution without full-price weighting.
- `SupplierDecisionHubPage.tsx:442-448` aggregates the Hub KPI from that derived value; `:1079-1088` documents the same local formula.
- `Klijent/clientapp/src/services/supplierDecisionReport.ts:105-113` passes the frontend total into the client report payload.
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs:1054-1067` computes the server report KPI with an additional `FullPriceRevenueShare` factor.
- `Klijent/clientapp/src/pages/__tests__/SupplierDecisionHubPage.percentExport.spec.ts:57-98` tests percent units only, not this formula or cross-report equality.
- `d000e349b` introduced the Hub derivation and `8006a4a6` introduced the server report formula; later trust changes did not reconcile them.

## Trust Matrix for Newly Found Paths

| Stavka | RQ249: Supplier Decision Hub action | RQ250: Supplier margin KPI/report |
|---|---|---|
| Potvrđeno | `recommendationAllowed` is backend metadata; blocked CTA/write path exists. | Two distinct formulas are present in production code. |
| Traženi period | Hub filters are sent to summary/ranking. | Hub/client report and server report accept the same filter family, but parity is not proven. |
| Efektivni period | Exposed by `trustMetadata.effectiveFrom/To` and dataset. | Must be preserved by the authoritative metric payload. |
| Posmatrani period | Available through summary/report lineage where supplied. | Must be identical across Hub and report projections. |
| Data scope | Hub includes `dataScope` in filters/action source key. | Server and client report need an explicit parity assertion for the same scope. |
| Vreme generisanja | Client report has a separate generated timestamp. | Must not be used as refresh time or as metric provenance. |
| Poslednji uspešan refresh | Resolved from refresh status/trust metadata. | Same refresh metadata must accompany both report paths. |
| Freshness status | Hub displays refresh/trust state, but blocked CTA ignores it as an action gate. | Formula parity must remain valid for fresh, stale and partial states. |
| Data quality status | Blocked state is visible as insufficient/helper signal. | Missing cost/coverage and non-finite values need an explicit metric state. |
| Empty/partial/error stanje | Empty/error rendering exists; blocked detail action is not gated. | Existing report tests do not prove empty/partial/error formula parity. |
| Recommendation allowed | `false` still leads to visible CTA and queue write. | Metric mismatch can leak into recommendation/report interpretation even though recommendation status is backend-owned. |
| Razlog ograničenja | Helper copy exists, but action affordance contradicts it. | No single backend-owned explanation states whether the metric is all-sales or full-price weighted. |

## Existing Owners Checked

- `RQ181` is limited to blocked Executive Decision Board action cards.
- `RQ235` is limited to concrete negotiation actions in the supplier report builder.
- `RQ178` owns inventory snapshot actionability.
- `RQ233` owns Supplier Sales Stats denominator scope, not the Supplier Decision Hub ranking/report formula.
- `RQ145` remains the broad parity gate; `RQ249` and `RQ250` are concrete reproductions that can be executed without duplicating its entire matrix.
- `RQ139`, `RQ148` and `RQ236` remain numeric/measurement-basis dependencies; the new prompts explicitly coordinate with them.

## Queue Change

- Added `RQ249` and `RQ250` as `WAITING`.
- Preserved the existing `RQ169` `READY` marker.
- No prompt was promoted, claimed or marked complete.
- No production code was changed in this audit.

## Residual Risk

The queue still contains broader runtime/schema/refresh/browser-proof work. This round is static repository evidence only; it does not claim live database, migration, refresh, deployed endpoint, browser theme or console proof.
