# Analytics next-wave recovery and product-scope triage — 2026-10-14

Base GitHub origin/main: `6b515386e1851ca32342b117a5375d879815b187` (read-only verification before registration); open PRs: 0; no tracked `.ai/task-locks/` files at intake. GitHub cannot read another agent's uncommitted local worktree; READY is not a claim.
Deliverable: update canonical RQ post-import queue, MASTER_ROADMAP and RQ448 dependency note; register RQ662, RQ663, RQ664 READY without changing runtime. Do not assume fresh production state.

## Verified / actual progress

- `RQ648` DONE via PR #157; 110 non-main branch audit and no safe missing source delta. No old branch should be bulk merged.
- RQ657 supplier request cancellation is DONE but only discards late prior-scope responses; the scope-less `stores` array is still exposed while a new scope loads and retained after errors in `SupplierSalesStatsPage`. RQ662 owns missing scope-validity and retry/selection proof.
- Recent `DailySalesStatsPage` and `dailyShiftSummary` changes (`86691925`, `7f1c3fac`, `1b116e4e`) closed the most direct unproven shift KPI/export claims. Current quality panel counts only missing and partial, excluding unavailable, and top badge can be good if all source uncertainty is info. RQ663 owns the remaining source/quality parity without changing formulas.
- Exact implementation/frontend Actions `38557709870` for `1b116e4e` is SUCCESS. Backend Actions `38557522344` on RQ648 PR CI is SUCCESS. Earlier red guardrail/typecheck jobs are superseded by source-specific corrections, not evidence that production is certified.
- Operations RQ447 DONE, RQ453 DONE (isolated PG oracle/CI), RQ448 WAITING (authenticated deployed browser/API/export). Clientapp has Puppeteer devDependency but no dedicated browser e2e suite. RQ664 constructs a narrow reproducible local/CI component of this evidence without falsely marking RQ448 DONE.
- RQ585 DONE, RQ659/660/661 DONE. Marketplace/scraper/GenAI/tenancy not current product work. Main buyer job is decision-grade sales, supplier, inventory and markdown data.

## Nonterminal older candidates and accurate start gates

| Candidate | Status and decision |
| --- | --- |
| `STAB16` P0 BLOCKED | Requires authorized Render/deployed SHA, worker service and read-only DB evidence; GitHub CI cannot promote this production task |
| `RQ610` P1 BLOCKED | Code/local store row structural proof exists but no durable mutation generation across POS/import/manual/transfers; don't generate fake daily historical stock |
| `RQ601`, `RQ602`, `RQ606` PARTIAL | Original cost documents, mature source cohort, and original MDB exceptions respectively; no synthetic completion |
| `RQ530` PARTIAL | Supplier buying panel's snapshot part shipped; additional buying metrics lack source/period contracts despite RQ487 DONE |
| `RQ531` WAITING | Owner must approve scorecard thresholds/weights, not a developer default |
| `RQ506` WAITING | Snapshot-cost rollout trigger or proven enabled configuration absent |
| `RQ558`, `RQ559` WAITING | Mature treated/control cohort and size-run source/model proof absent; RQ532 negative evidence is not approval |
| `RQ448`, `RQ451`, `RQ452`, `RQ454`, `RQ455`, `RQ565`, `RQ566` WAITING | Browser/deploy/source/certificate/acceptance dependencies as named in Operations queue; RQ664 only adds a safe repo-local precursor |
| `BCI/Q/P-UI` | No independently READY repository correctness candidates in inspected live owner queues; their older routing headers are historical |
| `MT/GAI` | Outside the owner-approved analytics-first scope; no activation |

Inspected: main roadmap, AGENTS/protocol, all named Analytics Reliability RQ addenda including operations, supplier, nivelacija and post-import; SQL, BCI, STAB and P-UI queues; source code and focused specs for Supplier store discovery and Daily Sales quality/shift presentation, product playbook, and recent CI runs. External provider config/local private credentials were not inspected.

## Routing and ownership

Register three new *non-overlapping* eligible tasks. Primary is RQ662 (P1 Supplier filter); independent RQ663 (P1 Daily Sales); independent RQ664 (P2 test-only browser harness). These are READY but not IN_PROGRESS. Shared claim on fresh main, path/branch/PR check, then worktree and code. No task/owner locks created by this review.

Promotion rule: after RQ664, recheck RQ448 authenticated browser/API prerequisites. Do not collapse `WAITING` to DONE from fixture proof. RQ610/STAB16 remain blocked until a specifically stated external owner fact arrives, not merely until agents write more docs. Reopen source-policy decisions only with owner approval.
