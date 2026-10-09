# Analytics action eligibility and value contract matrix

Audit date: 2026-10-09  
Queue prompt: RQ600  
Feature family: `analytics-action-eligibility-value-contract`  
Scope: Product Decision, Inventory, pre/post markdown, Supplier decision and the shared Analytics Actions outcome ledger.

## Verdict

The four decision surfaces have a backend-owned eligibility contract. Each surface carries a distinct combination of recommendation eligibility, status/reason, data quality and period/freshness context; the frontend renders those fields and fails closed when eligibility is unknown or false. No new scoring model or runtime change is required by this audit.

Historical report confidence is kept separate from current-action eligibility. A historical pre/post result may remain useful as an as-of report, while current replenishment, transfer, clearance or markdown execution remains gated by the current surface's freshness, coverage and `recommendationAllowed` contract. Observed action counts and expected impact fields are not business-value proof: measured outcome evidence remains an open pilot obligation.

No causal ROI, release activation, production certification, supplier authority or business validation is claimed here.

## Reviewed contract matrix

| Surface / decision | Grain and source of truth | Eligibility and trust contract | Displayed KPI / evidence / exclusions | State and owner decision |
|---|---|---|---|---|
| Product Decision | Product × store decision row for the requested/effective period. Backend mapping is in `Api/Endpoints/CachedAnalyticsEndpoints.cs:7104-7228,7408-7545` and the decision-board mapping in `Api/Endpoints/DecisionBoardEndpoints.cs:557-593`. | Backend supplies `recommendationAllowed`, status, reason/evidence chain, confidence/reliability and nullable `expectedImpactRsd`. Insufficient freshness/coverage clears actionable fields; the UI does not recreate business scoring (`Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx:400-560,804-824,1997-2120`). | KPI and expected impact are shown only when the backend field is valid. Blocked rows become review signals; unknown money is not rendered as zero. Evidence is the row's backend evidence chain and trust panel. `RQ593` owns money-coverage correction; `RQ585` owns eligible Decision Pulse composition. | **Shipped / covered.** Current production freshness and live-source authority remain `STAB16`/`QDB07` gates. No duplicate code task.
| Inventory actions | SKU × store action generated from the bounded signal window, snapshot and dataset context. `Api/Dtos/InventoryExperienceDtos.cs:164-218`; workflow construction in `Api/Endpoints/InventoryEndpoints.cs:1401-1592`. | Suggestion carries nullable `RecommendationAllowed`, `SignalConfidencePct`, `SignalDataQualityStatus`, reason codes, `CostMissing`, nullable `EstimatedValue`, `EstimatedValueBasis`, `AsOfUtc`, signal window and horizon. Calculator is backend-owned (`Api/Endpoints/InventorySignalCalculator.cs:28-58`); UI maps blocked/insufficient signals to review (`Klijent/clientapp/src/pages/InventoryPage.tsx:348-409`). | Replenishment, markdown, clearance and transfer rows expose action type, quantity/value only with known evidence. Denominator is the bounded signal window and selected store scope; blocked cost/coverage is explicit. `RQ594` owns horizon/action semantics; `RQ596` owns cost-unknown coverage. | **Shipped / covered.** No new eligibility defect reproduced; no duplicate task. Production currentness remains an external gate.
| Pre/post markdown or nivelacija | Article/vendor/category cohort with explicit pre/post window, comparable/mature counts and scope. Backend recommendation/effect policy and response gates are in `Api/Endpoints/AllEndpoints.cs:5036,5294,5336-5517,8085-8362`; frontend preserves the backend status (`Klijent/clientapp/src/pages/ProdajaPrePostNivelacijePage.tsx:941-987,1042-1120,1651-1741`). | Historical effect status, confidence/reliability, quality, recommendation eligibility and reason codes are backend-owned. The page labels analyzed/comparable/truncated coverage and previous/fallback states; it does not calculate a replacement business status. | Report KPIs are revenue/quantity/margin and comparable coverage for the historical cohort. Incomplete or untrusted values are withheld; an “as-of” historical report is not a current action authorization. Outcome ledger is separate (`Api/Services/VendorSalesNivelacijaOutcomeLedgerService.cs:9-13,157`). | **Shipped for historical reporting; gated for measured business value.** `RQ140` owns repository-local report/export parity; `Q83`/`STAB16` own live schema/authority; `RQ592` owns the fresh pilot and outcome proof. No duplicate task.
| Supplier decision | Supplier × requested period × store/data scope. Backend trust/action report is in `Api/Endpoints/SupplierDecisionHubEndpoints.cs:1192-1212,1466-1494,1997-2010,2189-2249`; source-page gating is in `Api/Endpoints/AllEndpoints.cs:1820-1911,2024,2097`. | `recommendationAllowed`, freshness, data quality, confidence/reliability and reason are preserved from the backend. Report actions are blocked or demoted to signal/review when not allowed; unknown supplier/cost evidence is not promoted to an executable action (`Klijent/clientapp/src/pages/SupplierDecisionHubPage.tsx:545-634,1122-1211`). | Supplier sales/decision KPIs show scope, freshness and coverage. Denominator and missing-cost state remain visible; blocked rows use a review label and do not imply negotiated value. `RQ585` owns eligible owner-digest composition; existing Supplier queue owners retain source/quality work. | **Shipped / covered.** Production freshness and connector/read authority remain `STAB16`/`QDB07` gates. The former frontend status-overwrite concern is covered by current backend-status preservation; no new task.
| Shared action/outcome ledger | Action item lifecycle keyed by source/action identity, with expected impact and outcome fields. `Api/Endpoints/AnalyticsActionsEndpoints.cs:18-416`; `Infrastructure/Services/Analytics/AnalyticsActionItemService.cs`; focused tests in `Api.Tests/AnalyticsActionItemServiceTests.cs` and `Api.Tests/AnalyticsActionsEndpointsTests.cs:348`. | Counts, pending/not-measured/success outcomes and coverage are distinct. Population, denominator and smoke-fixture treatment were corrected by `RQ477`, `RQ478` and `RQ479`; the ledger does not turn observed actions into measured value. | `measuredSampleSize`, outcome coverage and outcome status are the instrumentation needed for a measured pilot. Current evidence is still insufficient for a causal or ROI claim; four old smoke actions are historical fixture evidence, not production value proof. | **Instrumentation shipped; business value unproven.** `RQ592`/`RL12`/`RQ557` own fresh action-to-outcome evidence and business validation. No duplicate task.

## Historical confidence versus current eligibility

The audit uses this rule consistently:

| Evidence state | Historical report | Current executable action |
|---|---|---|
| Fresh, sufficient and allowed | May be displayed with the backend's confidence and reason | Eligible only when backend `recommendationAllowed=true` and the surface's scope/quality gates pass |
| Stale but internally valid | May remain visible as an explicitly dated/as-of historical result | Not eligible for a fresh-action claim; the surface must warn, demote or block |
| Partial, unknown cost/coverage, fallback or error | Show the established warning/empty/error semantics; do not substitute zero | Block or emit review signal; keep nullable value/eligibility fields unknown |
| Observed action with no measured outcome | Counts/lifecycle are reportable as instrumentation | Not business value, ROI or causal impact proof |

The implementation matches this rule across the reviewed surfaces: Product Decision and Inventory gate action creation; Supplier reports demote blocked actions; pre/post preserves historical effect status and coverage without silently becoming a current-action recommendation.

## Owner reconciliation and next tasks

No proven new contract defect was found, so RQ600 creates no follow-up code prompt. Existing ownership is non-overlapping:

- `STAB16` / `QDB07`: production provider, freshness, read-only authority and certification gates.
- `RQ593`: Product Decision unknown-money coverage.
- `RQ594` / `RQ596`: Inventory horizon/action semantics and cost-unknown handling.
- `RQ477` / `RQ478` / `RQ479`: Actions population, measurement denominator and smoke-fixture quarantine.
- `RQ585`: eligible cross-surface owner digest composition.
- `RQ140` / `Q83`: pre/post report/export parity and live schema/authority.
- `RQ592` / `RL12` / `RQ557`: fresh pilot, action-to-outcome instrumentation and business validation.

These are existing owners rather than newly inferred work. This audit does not activate recommendations, alter production configuration or infer an economic benefit.

## Evidence references

- `docs/ai/ANALYTICS_DESIGN_SYSTEM.md:9,75-104`
- `docs/ai/ANALYTICS_BUSINESS_GLOSSARY.md:51,84,96`
- `docs/product/TRENDPLUS_PRODUCT_ANALYTICS_VALUE_AUDIT_2026-10-07.md:7-9,29-35,218`
- `docs/qa/ACTIONS_DECISION_PULSE_SUPPLIER_SCORECARD_LIVE_AUDIT_2026-09-28.md:22-27,33-38,74-80`
- Focused backend/frontend tests named in the matrix and the RQ477/RQ478/RQ479/RQ585/RQ592 completion notes.
