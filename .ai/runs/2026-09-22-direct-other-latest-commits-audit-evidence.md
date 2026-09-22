Task ID: direct-other-latest-commits-audit
Queue: direct-user-request
Date: 2026-09-22
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct-main
Main commit SHA: 07760b749b5b6c2b4d26aff78f7137d6c85becf9
Main verification: passed - pushed `main` and verified `origin/main` contains `07760b749b5b6c2b4d26aff78f7137d6c85becf9` before documentation closure.
Evidence state: synchronized

## What was done
- Audited the remaining recent Supplier Decision/Pre-Post delivery commits not covered by the earlier Color and Pre-Nivelacija audit, with emphasis on the RQ401 cache-schema contract.
- Confirmed and fixed a same-owner RQ401 gap: the precomputed capability probe checked only that `vw_supplier_ml_latest_predictions` existed, not that its five consumed columns existed.
- Added a fail-closed capability gate for `supplier_id`, `top_feature_1`, `top_feature_2`, `top_feature_3` and `explanation_text`. An incomplete optional ML view now disables only the enrichment join instead of allowing generated SQL to fail.
- Added a focused source-contract regression assertion for the view relation and required-column probe.

## Files changed
- Api/Endpoints/SupplierDecisionHubEndpoints.cs
- Api.Tests/SupplierDecisionSchemaSqlTests.cs
- docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
- MASTER_ROADMAP.md
- .ai/runs/2026-09-22-direct-other-latest-commits-audit-evidence.md

## Validation run
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~SupplierDecisionPrecomputedCapabilitiesGateEachSelectedWindowAndRequiredColumns|FullyQualifiedName~SupplierDecisionSchemaSqlTests.SupplierDecisionLiveQueryDoesNotRequireOptionalMlPredictionTable|FullyQualifiedName~SupplierDecisionSchemaSqlTests.SupplierDecisionLiveAndPrecomputedSqlPreservePostObservationState"` -> pass, 3/3.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter "FullyQualifiedName~SupplierDecisionSchemaSqlTests|FullyQualifiedName~SupplierDecisionHubContractTests"` -> fail, 51 passed / 1 failed; the failure is an unrelated pre-existing `VendorSalesNivelacijaEndpointFailsClosedForMissingComparabilityEvidence` assertion expecting 3 while the current source exposes 2.
- `git diff --check` -> pass.
- `git push origin main` -> pass for runtime commit `07760b749b5b6c2b4d26aff78f7137d6c85becf9`.
- `git ls-remote origin refs/heads/main` -> pass; remote SHA matched runtime SHA before documentation closure.

## Validation not run
- Full backend/frontend suites -> not run; the changed contract is covered by focused backend tests.
- Live PostgreSQL schema-drift replay -> not run; no live database fixture was available.
- Remote CI result -> not inspected.

## Documentation impact
- Added a follow-up correction note to the RQ401 queue completion and the master roadmap.
- Recorded the exact runtime delivery SHA and the residual unrelated focused-suite failure.

## What was missed
- RQ402, RQ403, RQ404 and RQ405 remain WAITING and were not silently expanded into this same-owner correction.
- The unrelated Vendor Sales test failure was not changed.

## Risks
- The view-column probe is source-contract/static-test verified but not replayed against a live PostgreSQL schema with deliberate column drift.
- Existing analyzer warnings and remote CI state remain outside this run's proof.

## Next
- RQ402 remains the next Supplier Decision detail-source follow-up; RQ403-RQ405 remain under their named dependencies.
