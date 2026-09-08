# RQ186 evidence

Task ID: RQ186
Queue: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
Date: 2026-09-08
Agent/tool: Codex
Delivery target: `main`
Working branch / PR: `codex/rq186-pdc-lost-sales-20260908` / local merge, no PR
Main commit SHA: `498553a0989c099cc1d38fda53098a894e8904ca`
Main verification: pending push verification
Evidence state: pending

## What was done

RQ186 was explicitly promoted because the queue had no current READY prompt, then claimed with the local task lock. The Product Decision Center lost-sales estimate now incorporates demand velocity and an explicit 14-day impact window. The calculation is:

`velocityUnitsPerDay * 14 * averageUnitPrice * min(1, stockGap / minimumStock)`

Invalid or non-positive gap, minimum stock, velocity, price or impact-window inputs return a true zero. This makes equal stock gaps rank fast movers above slow movers while preventing the estimate from exceeding the projected demand value when the stock shortfall is larger than minimum stock. The DTO XML documentation and frontend metric methodology describe the estimate as modeled RSD exposure, not booked sales.

## Files changed

- `Api/Endpoints/CachedAnalyticsEndpoints.cs` — shared PDC lost-sales calculation and DTO semantics.
- `Api.Tests/ProductDecisionLostSalesTests.cs` — velocity ranking, shortfall-ratio/window arithmetic and true-zero tests.
- `Api.Tests/ProductDecisionCenterBuilderIntegrationTests.cs` — updated PDC summary/row expectations for the new contract.
- `Klijent/clientapp/src/utils/analyticsMetricDefinitions.ts` — explicit frontend formula and limitation.
- `Klijent/clientapp/src/utils/__tests__/analyticsMetricDefinitions.spec.ts` — methodology regression proof.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` — promotion, completion state and delivery evidence.

## Validation run

- `node scripts/check-agent-instructions.mjs --self-test` — passed.
- `node scripts/check-prompt-queues.mjs --self-test` — passed.
- `node scripts/check-planning-architecture.mjs --self-test` — passed.
- `node scripts/check-agent-instructions.mjs` — passed.
- `node scripts/check-prompt-queues.mjs` — passed (403 tasks).
- `node scripts/check-planning-architecture.mjs` — passed (78 tasks).
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~ProductDecisionLostSalesTests --no-build` — passed, 3/3.
- `dotnet test Api.Tests/Api.Tests.csproj --filter FullyQualifiedName~ProductDecisionCenterBuilderIntegrationTests --no-build` — passed, 4/4.
- `npm run test -- --run src/utils/__tests__/analyticsMetricDefinitions.spec.ts` — passed, 7/7.
- `npm run check:analytics-guardrails` — passed, including the frontend guardrails and typecheck.
- `npm run build` — passed; TypeScript project build and Vite production bundle completed.
- `dotnet build Api.Tests/Api.Tests.csproj --configuration Release --no-restore` — passed, 0 errors; existing warnings remain.
- Release focused backend tests for the two RQ186 suites — passed, 7/7.
- `git diff --check` — passed.

The first parallel attempt at the new backend unit test hit an environment/tooling file lock on `Domain.dll` held by the Security Engine Service. The same focused unit test passed on the sequential no-build rerun; no product change was made for that environmental failure.

## Validation not run

- Live PostgreSQL, browser and production endpoint proof were not run.
- Full backend and frontend suites were not run; the focused contract path and mapped guardrails were used.
- No production data, schema or external service was changed.

## Analytics safety gate

- Surface: Product Decision Center lost-sales estimate, summary, evidence and action consumers.
- Source of truth: backend PDC calculation.
- Contract changed: yes.
- Old contract: `stockGap * avgUnitPrice`.
- New contract: 14-day projected demand value (`velocityUnitsPerDay * 14 * avgUnitPrice`) weighted by `min(1, stockGap / minimumStock)`, zero for insufficient inputs.
- Unit: RSD modeled exposure, not booked sales.
- Numerator: units/day velocity * impact-window days * average price, weighted by shortfall ratio.
- Denominator: period calendar days for velocity; minimum stock for shortfall ratio.
- True zero: no stock gap or no positive demand/price returns 0.
- Missing/unknown: invalid/missing inputs return 0 in the current PDC contract; existing RQ03 unavailable-source semantics remain separate and untouched.
- No-baseline: not applicable.
- Freshness/fallback: unchanged; no new fallback.
- DataScope/store/search filters: existing filters unchanged; formula is applied per already-filtered PDC row.
- User-visible surfaces: PDC summary/rows/evidence, Decision Board consumers and methodology panel.
- Export/detail/action payload affected? yes, existing DTO/payload values change; no field rename.
- Tests proving true-zero vs unknown: helper zero-input tests; RQ03 source status remains separate.
- Tests proving table/detail/export/action parity: existing DTO/payload path retained; no field rename.
- Stop condition hit? no.

## Documentation impact

The queue records the explicit promotion, formula decision, validation and delivery evidence. The frontend metric registry now states the velocity-weighted formula, its minimum-stock guard and modeled-data limitation.

## What was missed

Live data and full-suite validation remain outside this local focused run. The queue prompt's example formula was dimensionally ambiguous, so the same-owner implementation uses the explicit 14-day projected-demand plus shortfall-ratio contract described above.

## Risks

The estimate remains a modeled exposure and depends on the quality of the existing velocity, price and minimum-stock evidence. Underlying live-data freshness and RQ03 unavailable-source semantics were not redefined in this slice.

## Next

After commit and local merge, push `main`, verify the exact merge SHA is contained in `origin/main`, synchronize this evidence with the final remote SHA and leave RQ187 WAITING.
