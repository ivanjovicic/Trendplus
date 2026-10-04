Task ID: analytics-owner-decisions-2026-10-04
Queue: Analytics Reliability (RQ)
Date: 2026-10-04
Agent/tool: ChatGPT
Delivery target: main
Evidence state: synchronized

## Recovery base

- Re-audit registration commit: `c8a9115eecaab31648bd8d68751b3cd758036cff`.
- Main advanced before this decision pass to `37acd8031a8d3bbcafb08d7bc2bfb53f40a35acd` (Shoe Type legacy-payload compatibility repair); that commit was reviewed before writes.
- The direct Shoe Type change did not alter RQ570-RQ585 semantics, but it exposed one stale Pre-Nivelacija cache-key test assertion (`v10` vs current `v11`), repaired in this pass.

## Owner decisions recorded

- RQ570: undated decision period = last 30 calendar days ending at observed sale horizon for the active store/dataScope; explicit dates unchanged; beyond-horizon comparison unavailable.
- RQ574: `TipObuce` authoritative; missing category/pol non-blocking when required type exists; recommendation-specific rather than global missing-dimension gates. Promoted to READY.
- RQ576: valuation = last reliable positive inbound cost, then labelled estimated last sale-line cost; no unverified master-cost scale and no missing-as-zero. Aging uses real inbound lineage only; last sale is never inventory age.
- RQ577: `ProdajaZaglavlje` is treated as an aggregation document, not a proven customer receipt; basket metrics gated.
- RQ582: Insight Studio/legacy Advanced hidden from main navigation behind Experimental flag until certified; preserve legacy acceptance traceability.
- RQ583: 48h warning / 168h critical durable-import SLA; unknown is not green; optional max-daily owner notification only after opt-in.
- RQ556/RQ571: retail decision stores = Trend PLUS 1/2; STARO/Magacin/Komision*/Objekat 20828 separated from retail markdown; Oprema separated; recommendationAllowed=false cannot enter highlightNow. Executable hygiene moved to RQ571; RQ556 still waits only on v9 weights.

## Routing corrections

- Primary remains RQ569.
- Additional claimable lanes: RQ553, RQ574, RQ578, RQ580, RQ581.
- RQ570/RQ576/RQ583 wait only for RQ569.
- RQ577 waits for RQ574 due shared Dashboard/bootstrap ownership.
- RQ582 waits for RQ581 to serialize the Insight Studio family.
- RQ573 waits for RQ569 + RQ574; RQ572 consumes RQ570 + RQ573 rather than duplicating their contracts.
- RQ575 waits for RQ569 + RQ574.
- RQ571 owns the resolved store/Oprema/action-queue hygiene as well as horizon anchoring.

## Additional repair

- `Api.Tests/AnalyticsScreenCacheKeyContractTests.cs`: synchronized the stale Pre-Nivelacija cache key assertion from `v10` to the current implementation contract `v11`.
- `.ai/runs/2026-10-04-shoe-type-production-contract-drift-evidence.md`: synchronized main-delivery evidence for `37acd803`.

## Validation

- Documentation changes are contract/routing only; no analytics business formula was changed.
- The cache-key test repair is a one-line assertion sync to the already-current `AnalyticsCacheKeys.PreNivelacijaPriorityBase` v11 implementation.
- Full runtime/test suite was not executed in this connector-only pass. Agents executing READY prompts must run the focused validations declared by each prompt.
