# Backend CI Full-Suite Evidence - 2026-08-12

Date: 2026-08-12
Repo: `ivanjovicic/Trendplus`
Prompt: `BCI05` re-entry
Agent: Codex
Current local HEAD: `e2ebd1d0311617901587184c798f08e0335a5f60`

## Decision

`BCI05` is **PARTIAL**.
`BCI01` remains **PARTIAL**.

The program is no longer blocked on "commit/push missing" alone. The latest backend-equivalent GitHub Actions proof is a real red full-suite run, so the next actionable task is a focused isolation repair prompt (`BCI08`), not another evidence-only re-entry.

## Latest GitHub Actions proof

- Workflow: `Analytics Tests & Data Integrity`
- Workflow ID: `260581486`
- Run ID: `31575771867`
- Job ID: `94047422144`
- Head SHA: `9c5fb2c6a2254f364ad2247a133413709860bd69`
- Trigger: `push`
- Outcomes:
  - Restore: success
  - Build: success
  - Test: failure
  - Publish coverage summary: success
  - Upload test results and coverage: success
- Artifact:
  - `analytics-backend-test-results`
  - artifact ID `9133159015`

## TRX totals from the uploaded artifact

| Metric | Value |
|---|---|
| Total | 829 |
| Executed | 829 |
| Passed | 825 |
| Failed | 4 |

Failing tests:

1. `Api.Tests.DemoEnvironmentVerificationEndpointTests.DemoVerification_ReturnsUnsafe_WhenNoProofInputsArePresent`
2. `Api.Tests.InventoryListEndpointIntegrationTests.InventoryList_UncachedRouteMatchesSeededRowCountAndEmptyMeta`
3. `Api.Tests.AccessImportRunEndpointTests.PostRun_WhenStoragePreparationTimesOut_ReturnsGatewayTimeout`
4. `Api.Tests.AccessImportRunEndpointTests.PostRun_ReturnsAccepted_AndInvokesImportServiceOnce`

## First failure signatures

- Demo verification:
  - expected warning `connection_string_unavailable_or_unreadable`
  - actual warnings collection empty
- Inventory uncached route:
  - expected `200 OK`
  - actual `500 InternalServerError`
- Access import timeout path:
  - expected `504 GatewayTimeout`
  - actual `503 ServiceUnavailable`
- Access import accepted path:
  - expected `202 Accepted`
  - actual `503 ServiceUnavailable`

The job log also contains repeated missing-table noise around the same time window:

- `relation "PerformanceLogs" does not exist`
- `relation "InventoryMovementFacts" does not exist`

This supports a shared host/database isolation or order-dependence diagnosis over a single deterministic assertion bug.

## Local focused repro on current HEAD

Commands run:

```powershell
dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AccessImportRunEndpointTests.PostRun_WhenStoragePreparationTimesOut_ReturnsGatewayTimeout|FullyQualifiedName~AccessImportRunEndpointTests.PostRun_ReturnsAccepted_AndInvokesImportServiceOnce|FullyQualifiedName~DemoEnvironmentVerificationEndpointTests.DemoVerification_ReturnsUnsafe_WhenNoProofInputsArePresent"

dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~InventoryListEndpointIntegrationTests"
```

Results:

- targeted access-import + demo verification filter: `3 passed / 3 total`
- `InventoryListEndpointIntegrationTests`: `7 passed / 7 total`

## Backend-equivalence check

The current local `HEAD` is backend/workflow-equivalent to the latest red GHA commit:

```powershell
git diff --name-only 9c5fb2c6..HEAD -- Api Api.Tests Application Domain Infrastructure .github/workflows/analytics-tests.yml
```

Result: no output.

That means the current backend/test/workflow code has not changed since the latest red `analytics-tests` run. A new push with docs-only changes would not honestly count as a backend fix.

## Conclusion

- `BCI02` diagnostic behavior is confirmed on a real red run:
  - the primary failure remains the test step;
  - coverage summary and artifact upload succeed and do not invent a fake root cause.
- `BCI05` cannot be closed with green evidence.
- The next correct action is a focused full-suite isolation prompt (`BCI08`) covering:
  - access-import run endpoint integration host behavior,
  - demo verification environment proof state,
  - inventory uncached integration path,
  - and their shared test-host/database isolation.

## Next

1. Claim `BCI08`.
2. Reproduce the four-test family in focused and full-suite form.
3. Fix the smallest proven isolation/shared-state root cause.
4. Re-run `analytics-tests` on the resulting backend commit.
5. Re-enter `BCI05` only after the family is closed or narrowed with fresh proof.
