# Backend CI queue recovery evidence

Task ID: bci-queue-recovery-2026-09-27
Queue: docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md
Date: 2026-09-27
Delivery target: main
Evidence state: synchronized

## What was found
- The canonical roadmap and 2026-09-26 BCI10 re-entry evidence say the broad backend suite is red and BCI10 is PARTIAL.
- The parent BCI queue and evidence addendum still said BCI10 DONE / Current READY none.
- Workflow run 36270728235, broad job 108484268134: 1477 total / 1398 passed / 41 failed / 38 skipped.
- RQ447's dedicated Supplier/Shoe Type certification job remained green and is not reopened.
- Later same-day local audit already repaired Daily Sales golden drift, worker catalog coverage and Supplier warning-label assertions.
- A stale Color cache-key v4 assertion remains on current main while the implementation is v5.
- SQL Server source-session deterministic tests and endpoint/provider/isolation families remain the main unclosed broad-suite groups.

## Routing repair
- BCI10: DONE -> PARTIAL.
- BCI11: READY, primary pointer, SQL Server source-session contract.
- BCI12: READY, parallel-safe, Color cache-key v5 contract.
- BCI13: WAITING, endpoint test-host DI/auth/rate-limit isolation.
- BCI14: WAITING, remaining PostgreSQL/provider/order-dependent full-suite isolation.
- MASTER_ROADMAP and the evidence addendum were synchronized.
- The historical analytics priority review no longer presents the old BCI10 DONE state as live routing.

## Branch/merge review
- Open pull requests: none at review time.
- Remote branches were cleaned during the review; only main and backup/mixed-local-changes-20260312-1845 remained.
- The backup branch is 2390 commits behind and has one divergent mixed commit touching old API/frontend/Python/debug/CV files. It is intentionally not merged because it is an archival mixed backup, not a reviewable current feature branch.

## Residual external gates
- RQ448: authenticated browser/API/deployment evidence.
- RQ454/STAB16: production read-only/provider access.
- RQ455: customer acceptance evidence.
- RQ319: unresolved Apply-vs-auto-apply product decision.

## Next
Execute BCI11 first. BCI12 may run in parallel in another workspace. Re-run the broad backend suite after both focused drifts are closed, then promote only the smallest evidenced BCI13/BCI14 residual family.
