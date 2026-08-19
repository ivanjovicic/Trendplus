# Analytics Test Strategy

Updated: 2026-08-13
Repo: `ivanjovicic/Trendplus`
Status: canonical how-to for analytics tests; does not create a new program

Use this file when writing or executing analytics tests. Live work still belongs to the owner queue named by `MASTER_ROADMAP.md`.

## What is worth testing

Trendplus sells decision support, not screen coverage. A useful test proves one of:

- šta se prodaje, za koji period i koji opseg
- gde je stvarna marža, a gde je cost/value unknown
- gde je mrtav lager / OOS rizik / dopuna
- koji dobavljač zaslužuje fokus
- kojim podacima ne treba verovati
- koju akciju uraditi ove nedelje, sa razlogom i pouzdanošću

A test that only asserts chrome, CSS class names, or snapshot of an entire page is lower value unless it locks a named trust failure.

## Proof ladder

Use the smallest proof that can fail for the named risk. Do not start with a browser suite.

```text
1. Backend contract / in-memory HTTP integration
   Fake zero, empty vs error, period bounds, recommendation/impact, dataScope leak.
2. Focused handler/projection unit test
   Reader EOF, lifecycle eligibility, denominator math, reason codes.
3. Vitest display contract
   Page shows backend status/label/reason; ErrorState hides KPI zeros; empty is not error.
4. Analytics guardrails / encoding
   Shared formatters, trust/error/empty imports, mojibake.
5. Browser / URL refresh
   Only when acceptance is routing, refresh, or deployed loading behavior.
```

Existing gold-standard hosts:

- `Api.Tests/CachedAnalyticsCriticalEndpointsIntegrationTests.cs`
- `Api.Tests/DailySalesStatsIntegrationTests.cs`
- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs`
- `Api.Tests/DecisionBoardEndpointsTests.cs`
- `Api.Tests/InventorySnapshotContractTests.cs`
- `Api.Tests/AnalyticsActionItemServiceTests.cs`
- page specs under `Klijent/clientapp/src/pages/**/*.spec.tsx`

Extend these before creating a new test host.

## Named failure modes that must have a counterexample

Every new analytics test file should include at least one of:

| Failure | Required proof |
|---|---|
| Fake zero | Backend error/unavailable is `meta.success=false` or Problem, never a trusted `0 RSD` / `0 kom` with `success=true` and no emptyReason. |
| Empty vs error | Successful empty period has `success=true`, `emptyReason`, and `dataQualityStatus=insufficient_data` when there is no signal. Frontend uses `AnalyticsEmptyState`, not `AnalyticsErrorState`. |
| Hidden fallback | Lost-sales / previous-period / cache fallback is labeled; `FIX_DATA` and `INSUFFICIENT_DATA` do not inherit another row's expected impact. |
| Wrong period | `toDate` is whole-day; current and previous windows do not overlap; store/supplier filter does not leak other entities. |
| Invented recommendation | Backend owns `recommendationStatus`, `label`, `reason`/`reasonCodes`, `confidence`/`reliability`, `dataQualityStatus`. Frontend does not apply local 70/40 bands as business truth. |
| Fake measured | `not_measured` / missing outcome has no measured timestamp; acceptance is not success; learning eligibility requires executed + measured evidence. |

If the test cannot name which row in this table it protects, it is probably the wrong test.

## What not to add

- Page-wide snapshots that break on copy edits.
- Duplicate local formatters or tests of `fmtRsd` already covered in `analyticsFormatters` specs.
- Frontend tests that lock in local scoring thresholds as the product contract.
- Playwright flows that re-prove a contract already covered by WebApplicationFactory + Vitest.
- Broad `dotnet test` of the whole solution as the only proof of a single endpoint change.

## Commands

Choose through `docs/ai/VALIDATION_SELECTOR.md`. Typical owners:

```powershell
dotnet test .\Api.Tests\Api.Tests.csproj --filter "FullyQualifiedName~<TestClass>"
```

```powershell
cd Klijent/clientapp
npm run test -- --run <path-to-spec>
npm run check:analytics-guardrails
```

Record skipped checks with a real reason. A compiling test is not passing runtime proof.

## Queue mapping

| Risk family | Owner program | First WAITING prompt |
|---|---|---|
| Decision / impact / recommendation authority | RQ | `RQ100` |
| Inventory null evidence / dead stock / replenish counts | RQ | `RQ101` |
| Sales period, empty, scope leak | RQ | `RQ102` |
| Action outcome / learning eligibility | RQ | `RQ103` |
| Frontend displays backend truth, no invented scores | RQ | `RQ104` DONE |
| Operational fallback must not look trusted | RQ | `RQ105` DONE |
| Shared ErrorState/EmptyState/TrustHeader on stats pages | P-UI | `P-UI-20` |

Do not start these while a higher-priority READY task owns overlapping paths, unless the prompt is explicitly marked parallel-safe and path-clear. Current execution is `RQ96`. `QDB06` is DONE.

Live prompt text: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_TEST_HARDENING_ADDENDUM.md` and `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md`.
