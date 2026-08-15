# Retail Analytics Roadmap Architecture Review - 2026-08-15

## Request

Review the listed Trendplus roadmap and planning documents as a retail data-analytics architect. Compare the direction with mature retail analytics/planning patterns, identify gaps and excess scope without disrupting the current development direction, and improve the documents.

## What changed

- Added a single retail analytics product architecture to `MASTER_ROADMAP.md`:
  `source event -> canonical product context -> observed business fact -> trustworthy metric -> prioritized decision -> action -> measured outcome`.
- Defined existing-owner boundaries for product context, observed facts, decision-ready metrics, operational decisions and learning/planning. No new program or READY prompt was created.
- Strengthened business milestones so customer-facing decision, forecast, availability, optimization and lost-sales claims require declared grain, provenance, coverage, constraints and validation outcome.
- Added Decision Intelligence eligibility rules that prevent a score from becoming a replenish, transfer, markdown or price decision when context, feasibility or evidence is incomplete.
- Directed future Premium UI work toward consistent product/SKU -> variant/store -> evidence -> action -> outcome navigation only after backend contracts exist.
- Added data-truth coverage expectations to OBS, fact-grain/materialization expectations to PERF, and analytics-evidence minimization to SEC.
- Expanded Feature Lifecycle requirements for analytics, forecasting, optimization and decision-support work.
- Added a dated planning-audit addendum with repository findings, official-market references, deferred scope and owner routing.

## Key findings

- Preserve the existing differentiated direction: trustworthy retail facts -> explainable prioritized decision -> action -> measured outcome.
- Do not chase generic BI authoring, arbitrary SQL, generic product-master management, broad anomaly detection, premature elasticity models or a full enterprise planning suite.
- Existing foundations already cover many operational surfaces: product/variant dimensions, inventory signals, supplier analytics, reason codes, confidence, action/outcome lifecycle, Decision Timeline and measurement statistics.
- The depth-first remaining path is existing-owner work: cross-source product identity/hierarchy coverage, observed SKU/store/day inventory history (`RQ96`), forecast ownership (`RQ97`), baseline/backtesting (`RQ98`), narrow exception delivery, then controlled read-only scenarios.

## Validation

- `git diff --check` for the nine target documents: pass (only existing line-ending normalization warnings).
- `node scripts/check-prompt-queues.mjs`: pass, 260 tasks.
- `FEATURE_LIFECYCLE.md`: restored from current Git content after an interrupted write caused by a full system disk; final file is complete and contains the requested additions.

## Not changed

- No runtime code, schema, analytics formulas, queue status, READY prompt, deployment configuration or source connector behavior changed.
- No tracked `.tmp_dotnet` deletion is included in this documentation work. Those working-tree deletions resulted from the user-approved disk cleanup and remain intentionally unstaged.

## Risks and follow-up

- `node scripts/check-planning-architecture.mjs` currently fails because it expects exactly one DEX READY prompt while the current master roadmap and DEX queue state that there is no DEX READY prompt. This was observed after the documentation changes but is not caused by them; do not promote a task merely to satisfy the validator. Reconcile the validator expectation with canonical queue routing in a dedicated governance task.
- The planning rules deliberately reserve future work to existing owners. Before a new prompt is created, confirm the authoritative metric/data contract and current READY status on `main`.
