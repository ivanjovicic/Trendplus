Task ID: RQ601-RQ602-historical-validation
Queue: direct-user-request; existing RQ601/RQ602 status/evidence update only
Date: 2026-10-10
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / none
Main commit SHA: d395a628d8e14ba6f93d07c0e81d0bc5d4b6cd92
Main verification: passed - `origin/main` contains `f1e065d6e4fb41b39f5c28f7ee3800f64798e031`
Evidence state: synchronized
Ownership transfer: none

## What was done

- Fetched and verified current `origin/main` before inspection; base was `d8db106bcb43643ca4ddca908367651a9ca4d70c`.
- Checked working tree, active Codex threads, attached artifacts, branches, physical task locks and existing RQ601/RQ602 ownership. No duplicate task, PR, branch or active agent collision was found; unrelated untracked directories were preserved.
- Confirmed local Docker PostgreSQL `trendplus-postgres`, database `trendplus`, and completed Access batch #23.
- Executed `tools/purchase-cost-scale-reconciliation.sql` read-only on the real local database and recorded source/inbound/master/unknown cost coverage, scale ratios, stock-value scenarios and a precise 20-row owner worksheet.
- Executed the Access #23 fact audit read-only. Access receipt/line populations reconcile 5,530/5,530 and 67,092/67,092 with zero paired amount/quantity/price delta.
- Executed the existing `tools/markdown-pilot-measurement.sql` against a synthetic read-only runtime substitution populated from the real batch-23 history. Recorded 6,052 unique markdown candidates, 5,818 complete windows, and a descriptive 20 treated + 20 matched-control cohort.
- Compared Daily Sales, Supplier Sales and Shoe Type totals with an independent operational SQL oracle for `imported`, `existing` and `all` on `[2026-07-01, 2026-08-05)`.
- Confirmed and documented one separate InventoryStatus scope defect: `imported` is compared as a literal instead of the canonical `access` origin in `GetInventoryStatusHandler`. It is outside RQ601/RQ602 owned paths; no duplicate queue task or cross-owner code change was made.
- Updated the RQ601 worksheet, the durable historical validation report, the canonical RQ queue notes and the roadmap. Both RQ601 and RQ602 remain `PARTIAL`.

## Files changed

- `docs/qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md`
- `docs/qa/PURCHASE_COST_SCALE_RECONCILIATION_2026-10-09.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`
- `.ai/runs/2026-10-10-RQ601-RQ602-historical-validation-evidence.md`

## Validation run

- `git fetch origin main` and current-main/working-tree/branch/lock/agent collision inspection -> pass.
- Read-only PostgreSQL `DataImportBatches`/Access fact audit -> pass: batch #23 completed; 5,530/5,530 receipts; 67,092/67,092 lines; zero paired amount/quantity/price mismatch.
- `tools/purchase-cost-scale-reconciliation.sql` on `trendplus-postgres`/`trendplus` -> pass: 12,428 article rows returned; unknown costs remained `NULL`; no database write statements executed.
- RQ601 dimension coverage SQL -> pass: supplier, shoe type, store and month coverage recorded; largest unknown-cost group is store 551466791, 327 lines / 1,571,450 RSD.
- RQ602 historical dry-run -> pass: 6,052 candidate events, 5,818 complete windows, 20 treated + 20 matched controls; output retained descriptive/unknown-cost semantics.
- API vs independent SQL oracle for Daily/Supplier/Shoe Type totals on imported/existing/all -> pass: `1,833,020 / 359`, `480 / 5`, and `1,833,500 / 364` respectively.
- InventoryStatus live check -> expected failure evidence: imported scope returned zero status while the canonical `access` population exists; source-level handler mismatch recorded, no data was altered.
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --filter "FullyQualifiedName~PurchaseCostScaleReconciliationTests|FullyQualifiedName~MarkdownPilotMeasurementTests" --logger "console;verbosity=minimal"` -> pass: 2 passed, 0 failed, 0 skipped.
- `node scripts/check-agent-instructions.mjs --self-test; node scripts/check-agent-instructions.mjs` -> pass.
- `node scripts/check-prompt-queues.mjs --self-test; node scripts/check-prompt-queues.mjs` -> pass.
- `node scripts/check-planning-architecture.mjs --self-test; node scripts/check-planning-architecture.mjs` -> pass.
- `git diff --check` -> pass.

## Validation not run

- Full backend/frontend solution test suite -> not run; this was a read-only evidence/documentation run and the focused PostgreSQL fixture proof was the narrowest relevant check.
- Original owner calculation sample -> not available in the repository or local database; this is the explicit RQ601 acceptance blocker.
- Prospective markdown pilot / owner decision -> not run; historical retrospective evidence is not a pilot.
- Code repair for InventoryStatus -> not run; the finding crosses the RQ601/RQ602 owner boundary and was recorded without creating a duplicate task.
- Production database or production API mutation -> not run by design.

## Documentation impact

- Added a business-facing report with real numbers, dimension coverage, screen oracle comparison, the 20-item worksheet and trust verdicts.
- Synchronized RQ601/RQ602 completion notes and `MASTER_ROADMAP.md` without changing either status to `DONE` or registering a duplicate.

## What was missed

- RQ601 cannot certify the scale or RSD capital until the owner supplies the 20 original kalkulacija lines and dates/units.
- RQ602 cannot claim causal uplift or a markdown recommendation without an approved prospective/pilot design and outcome evidence.
- InventoryStatus still needs its owning repair and regression proof for the `imported` -> `access` scope mapping.

## Risks

- Legacy `NabavnaCena` scale remains suspicious; using it as certified capital would materially understate Access stock.
- The 501 unknown-cost Access retail lines carry 1,711,050 RSD of revenue and must not be assigned zero margin.
- The pre/post API marks the bounded August event window critical/blocked because the requested end date is before the 30-day post window matures and integrity evidence reports seven bounded drifts.
- Unrelated untracked workspace directories were left untouched.

## Post-close routing recovery

- not applicable - direct-user-request work; no formal queue claim was created or closed. Existing RQ601/RQ602 remain PARTIAL and no duplicate successor was registered.

## Next

- RQ601 owner: provide the 20 original kalkulacija records so the scale verdict can be certified.
- InventoryStatus owner: repair canonical `imported`/`access` mapping and add a real PostgreSQL regression test before trusting imported inventory capital.
- RQ602/product owner: approve a prospective matched pilot and use the existing read-only measurement query only after freshness/readiness gates are satisfied.
