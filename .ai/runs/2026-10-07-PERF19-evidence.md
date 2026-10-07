Task ID: PERF19
Queue: docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md
Date: 2026-10-07
Agent/tool: Codex
Delivery target: main
Working branch / PR: codex/perf19-decision-board-composition / none
Main commit SHA: pending
Main verification: pending
Evidence state: pending
Ownership transfer: none

## What was done
- Added opt-in server-side timings for the seven Decision Board contributors and Board composition, plus bounded sample identifiers and a compact total/source-state log. Profiling is off by default unless config or `profileSections=true` opts in.
- Added a full HTTP performance harness on the fixed July 7–August 6 PostgreSQL operations fixture, with cold-process/cold-cache and 20 warm requests using the in-memory analytics cache.
- Measured cold-process/cold-cache HTTP at 3,351.94 ms; first server composition was 2,064.89 ms. Warm N=20 HTTP p50/p95 was 343.05/653.56 ms (min 81.06, max 760.88), below the existing 2 s warm p95 target.
- Warm contributor p50/p95 in ms: Product Decision 71.09/174.07; inventory insights 22.12/53.22; inventory workflow 46.86/83.64; supplier summary 41.10/148.65; Actions 47.59/107.78; refresh status 41.15/101.97; Data Quality 31.30/82.64; composition 0.54/3.57. Supplier returned `failed` in all 20 samples because the fixture has no 90-day supplier decision dataset; failures remain fail-closed.
- Cache logs showed cold misses and warm hits. Stable Board sections/cards/order/counts/warnings/source states/metrics matched across all 20 warm responses; generated timestamps were excluded from parity comparison.
- Source states were `product-decision-center:critical`, `inventory-workflow:warning`, `supplier-decision-hub:unknown`, `analytics-actions:good`, `action-outcome-summary:unknown`, `refresh-status:unknown`, `data-quality-health:warning`. This is degraded local fixture evidence, not a claim of healthy deployed sources.
- No code optimization was justified by the local warm measurements. The retained production observation (~17.3 s) is still unexplained and is routed to STAB16 for provider/database/network evidence.

## Files changed
- `Api/Endpoints/DecisionBoardEndpoints.cs`
- `Api.Tests/DecisionBoardEndpointsTests.cs`
- `Api.Tests/OperationsAnalyticsAllRoutesIntegrationTests.cs`
- `Api.Tests/Api.Tests.csproj`
- `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`
- `docs/roadmaps/PERFORMANCE_ROADMAP.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-07-PERF19-evidence.md`

## Validation run
- `dotnet restore Api.Tests/Api.Tests.csproj --artifacts-path F:\codex-perf19-artifacts` -> pass.
- `dotnet test Api.Tests/Api.Tests.csproj --artifacts-path F:\codex-perf19-artifacts --configuration Release --no-restore --filter "FullyQualifiedName~DecisionBoardHttp_ProfilesColdWarmAndPreservesBusinessPayload"` -> pass, HTTP harness 1/1; API and test projects compiled. Build emitted existing analyzer warnings.
- `dotnet test Api.Tests/Api.Tests.csproj --artifacts-path F:\codex-perf19-artifacts --configuration Release --no-restore --no-build --filter "FullyQualifiedName~DecisionBoardEndpointsTests"` -> pass, 45/45.
- `git diff --check` -> pass.
- `node scripts/check-agent-instructions.mjs --self-test` -> pass.
- `node scripts/check-agent-instructions.mjs` -> fail on five pre-existing consistency markers in unchanged `REPO_AI_README.md`, `AGENTS_QUEUE_ADDENDUM.md`, `AGENT_RUN_EVIDENCE_STANDARD.md` and `.ai/RUN_LOG_TEMPLATE.md`; none are in PERF19 scope.
- `node scripts/check-prompt-queues.mjs --self-test` and `node scripts/check-prompt-queues.mjs` -> pass (710 tasks).
- `node scripts/check-planning-architecture.mjs --self-test` and `node scripts/check-planning-architecture.mjs` -> pass (80 planning tasks).

## Validation not run
- Deployed/provider-side p50/p95 and current production contributor logs -> not run; this remains STAB16 external/provider evidence.
- Full backend suite -> not run; focused endpoint and integration checks cover the changed behavior.
- Current-main Actions classification -> pending main delivery; inspect only if an associated current-main run is discoverable after push.

## Documentation impact
- Updated the owning platform queue, performance roadmap and master routing row with local measurement and the deployed-vs-local residual.

## What was missed
- The HTTP fixture is the existing fixed July operations fixture, not production data. Its product/inventory/data-quality states are degraded, and its supplier 90-day dataset is absent. No production claim or optimization was made.

## Risks
- Production's retained ~17.3 s measurement is far above local warm p95; contributor attribution requires STAB16 provider/database/network evidence.
- Cold HTTP includes host startup (3.35 s); server composition was 2.06 s. Warm endpoint samples are the comparison to the existing p95 budget.
- Agent instruction validator still reports five unrelated existing consistency-marker failures listed above.

## Post-close routing recovery
- Pending final delivery. After delivery, refresh `origin/main`, scan the full active RQ/SQL/P-UI plus program queue/addendum set, search PERF19 and changed dependencies, and re-evaluate every non-terminal candidate. Do not infer `none` from the prior recovery.

## Next
- Pending post-close routing recovery; STAB16 owns deployed/provider evidence for the remaining production discrepancy.
