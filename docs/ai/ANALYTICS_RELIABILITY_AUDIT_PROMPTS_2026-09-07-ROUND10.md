# Analytics Reliability Audit Prompts - 2026-09-07 Round 10

Queue: direct-user-request
Scope: production analytics only; Trend, forecast, Shopify, vendor integrations and standalone test functionality excluded.
Status: audit completed; one new prompt added, one prior prompt revalidated.

## Audit focus

This pass rechecked the active analytics decision surfaces and their backend report/action boundaries after the previous `RQ249-RQ251` additions. The audit followed the repository authority documents and checked source, nearest tests and Git history before creating a prompt.

Routes and surfaces reviewed:

- `/analytics`, `/analytics/products`, `/analytics/supplier`, `/analytics/inventory`
- `/analytics/actions`, `/analytics/decision-board`, `/analytics/data-quality`, `/analytics/reports`
- supplier report/export and Inventory workflow/scheduler surfaces that feed those routes

## Findings

### RQ252 - Backend supplier report trust gate fails open when metadata is absent

Status: `WAITING`, Priority: `P1`, Owner: Supplier Analytics.

The backend report action builder receives nullable `ScorecardTrustMetadata` but blocks only the explicit false case. With non-empty data and missing trust metadata, concrete actions can still be emitted. The metadata builder uses the same pattern, so missing evidence is not consistently represented as blocked/insufficient. The focused tests cover explicit false, fallback and empty data, but not this missing-metadata branch.

Evidence:

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs:1076-1131` builds concrete report actions after the guard `!hasData || trust is { RecommendationAllowed: false }`.
- `Api/Endpoints/SupplierDecisionHubEndpoints.cs:1847-1855` treats only explicit false as `recommendationGated`.
- `Api.Tests/AnalyticsReportsContractTests.cs` and `Api.Tests/SupplierNegotiationPackReportTests.cs` have no non-empty `trust == null` fail-closed assertion.
- Git history shows the action builder in `8006a4a6`; trust hardening commits `569705f1`, `29a5943a` and `e4d53a618` did not add the nullable-trust branch.

Required proof is defined in queue prompt `RQ252` in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`.

### RQ251 - Confirmed existing, not duplicated

The Inventory workflow/scheduler raw status leak remains a valid open finding, but it is already fully represented by `RQ251`. `ActionWorkflowPanel` and `MailSchedulerPanel` still render raw operational tokens, while existing tone helpers do not provide safe user labels. No duplicate prompt was created.

## Coverage matrix for this pass

| Surface | React/page | API/endpoint | Backend/contract | Period/scope | Freshness/quality | Empty/partial/error | Recommendation gate | Result |
|---|---|---|---|---|---|---|---|---|
| Dashboard | `AnalyticsDashboard.tsx` | dashboard bootstrap/advanced | cached analytics endpoints | present through shared meta; ratio null handling already owned by existing prompts | shared trust metadata | existing guardrails and `RQ240` | existing dashboard contract | no new distinct finding |
| Products | product decision surfaces | product decision client/endpoints | product decision DTO/services | existing lineage contract | existing quality metadata | existing product guardrails | backend-owned | no new distinct finding |
| Supplier Hub | `SupplierDecisionHubPage.tsx` | supplier decision endpoints | supplier scorecard contract | existing requested/effective/observed fields | trust metadata | `RQ249`, `RQ250` and existing tests | `RQ249` | rechecked; no duplicate |
| Supplier report | `SupplierDecisionReportPage.tsx` and report builder | supplier decision report | `SupplierDecisionHubEndpoints` report response | report period and observed period present | nullable trust path inconsistent | explicit false/empty covered, missing trust absent | `RQ252` | new finding |
| Inventory | `InventoryPage.tsx`, workflow/scheduler panels | inventory/workflow/scheduler endpoints | inventory workflow DTOs | existing inventory contract | status labels unsafe | `RQ251` | `RQ178` | existing `RQ251` confirmed |
| Actions | `AnalyticsActionsPage.tsx` | actions endpoints | action outcome contract | existing action metadata | existing quality projection | existing state handling | backend-owned | no new distinct finding |
| Decision board | `ExecutiveDecisionBoardPage.tsx` | decision-board endpoint | decision board DTO/projector | existing lineage | existing trust block | existing warning/error path | existing gate | no new distinct finding |
| Data quality | `DataQualityPage.tsx` | data-quality endpoints | issue/refresh contracts | filter-dependent | status projection exists | empty/error distinction exists | not an action surface | no new distinct finding |
| Reports/exports | shared report/export components | report endpoints | report payload/legacy rows | parity owned by `RQ145` | warnings/meta present | supplier action branch found | `RQ252` plus `RQ235` | new backend boundary finding |

Matrix interpretation for the new finding: requested period, effective period, observed period, data scope, generation time and last successful refresh are already represented in the supplier report contract, but the missing trust case does not safely confirm freshness/quality or recommendation permission. Until `RQ252` is implemented, the confirmation for `Freshness status`, `Data quality status`, `Recommendation allowed` and `Reason limitation` is therefore **not confirmed** for that branch.

## Prompt hygiene

- Added only `RQ252`; no duplicate was created for the already-open `RQ251` issue.
- `RQ252` remains `WAITING` because the canonical queue still has `RQ169` as the current `READY` prompt.
- No production code was changed in this audit; only queue and durable audit/evidence documentation were added.

