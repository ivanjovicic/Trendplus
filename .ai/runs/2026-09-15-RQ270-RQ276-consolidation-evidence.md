# RQ270–RQ276 evening consolidation

## Interpreted outcome
Review all evening Inventory prompt deliveries as one integrated change set, repair cross-branch conflicts and semantic gaps, and push a locally merged branch.

## Owner
Inventory analytics frontend/API contracts plus queue delivery records.

## Integrated branches
- `cursor/rq270-inventory-scope-reload-c753`
- `cursor/rq271-inventory-kpi-search-scope-c753`
- `cursor/rq272-inventory-total-value-c753`
- `cursor/rq273-inventory-report-parity-c753`
- `cursor/rq274-inventory-forecast-age-c753`
- `cursor/rq275-inventory-queue-empty-reset-c753`
- `cursor/rq276-inventory-impact-semantics-c753`

## Review repairs
- Scope-change handling now both refreshes the captured `dataScope` used by export and increments the unified reload generation.
- Second review adds the scope to refresh-generation predicates so operational and signal panels cannot retain the previous scope.
- Queue ledger summary/statuses are consistent for RQ270–RQ276.
- KPI copy now states that store, supplier and data-scope filters apply while text search does not.
- Backend workflow DTOs preserve `CostMissing` and an explicit `EstimatedValueBasis`; transfer cost is no longer labeled as current-stock exposure.
- Inventory Decision Board cards no longer rank or display exposure/action cost as expected impact.
- Export and print preview reject unsupported explicit data scopes instead of silently broadening to `all`.
- RQ276 distinguishes backend current-stock exposure from transfer/forecast suggested-action cost; neither is serialized as expected business impact.
- Workflow value labels follow the explicit value basis.
- Three pre-existing frontend full-suite assertions were aligned with the established safe `N/A` and mapped empty-reason contracts.
- Full-suite rerun exposed duplicate scope refreshes; the event handler now performs exactly one state-generation update while still refreshing on same-scope events.
- The stale preset-range test now follows inclusive 30/90/180/365-day semantics owned by `getAnalyticsPeriodPresetRange`.
- The existing npm-generated package-lock metadata normalization is retained in a separate commit per the request to push all local changes.

## Validation
- Focused second-review regression: 7 files, 49 tests passed.
- Complete frontend suite after final repair: 134 files, 801 tests passed.
- Frontend production build after second review: passed; existing Recharts chunk-size warning only.
- Prompt queue/planning validators: initial queue check found four missing strict RQ273 completion fields; repaired; queue check, queue self-test, planning check and planning self-test all passed.
- Backend build: not run because `dotnet` is unavailable in this environment.
- `git diff --check`: passed before the review commit.
- PR CI before the second review: frontend failed three stale safe-output assertions; backend had broad shared-database/schema failures including missing `StoresDim`. The frontend assertions were repaired; the backend-specific DTO/Decision Board changes await CI because local .NET is unavailable.

## Delivery
- Consolidation branch: `cursor/evening-inventory-prompts-consolidation-c753`
- Base: `main`
