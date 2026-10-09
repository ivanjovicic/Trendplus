# Analytics next execution prompts — 2026-10-09

Owner: Analytics Reliability (RQ); canonical routing remains `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` and `MASTER_ROADMAP.md`.
Source base: `a9ea060e5a04f825fece1dbcb3b192d8290f40b7` and `.ai/runs/2026-10-09-queue-recovery-cursor-evidence.md`.
This is a newly requested owner-approved work-intake addendum, not evidence that an unobserved defect exists. Before claiming, reconcile against fresh main and canonical statuses, and register the selected prompt in the canonical RQ queue per its existing conventions. Do not revive DONE prompts or count auditing as an actual product fix.

## Decision and operating principles
- Priority sequence: exact-deploy freshness/schema/worker truth (STAB16); existing historical analytics correctness; inventory capital; markdown outcome; supplier decisions; external pilot.
- Existing imports remain valid for historical formula tests. Do not require new imports merely because datasets are old. Time-sensitive inventory or freshness recommendations must communicate staleness.
- Prefer existing independent raw-fact oracles, PostgreSQL integration fixtures and UI contracts. No shared mocks as both source and oracle. No invented product formulas, acceptance thresholds, causal impact or supplier authority.
- Never display error/missing cost/missing freshness as valid numeric zero. DUG/KOREKCIJA semantics and source-of-truth business decisions must follow already approved contracts.
- Keep new work local, bounded, isolated and reproducible. Never change production config, migrations, deployment, billing, auth or tenant policy without explicit scope.

## RQ598 — Analytics regression gap inventory and targeted test proposals
Status: READY (new owner-requested **audit-only** work; no product changes assumed)
Priority: P1; Dependencies: none; Owner: RQ
Scope: existing backend analytics test suites, independent SQL/raw-fact oracle tests, contract tests and frontend analytics tests; current analytics accuracy/value audits.
Task:
1. Fetch latest origin/main; check all current RQ statuses and active agent ownership. Map each shipped high-value metric/route to source, aggregation grain, approved formula/policy, test ID and independent oracle presence. Include Daily, Supplier, Shoe Type, Color, Supplier Footwear, Inventory, Pre/Post, Pre-Nivelacija, shift and selected action screens. Note export/query consistency.
2. Focus on reproducible blind spots: unknown ID vs display name, previous-only categories, negative/zero margins, missing costs, returns/adjustments, date and store filter parity, empty/missing/error/stale states, applied-vs-draft filters, top-N scope, rounding and denominators.
3. Do not claim defects from missing named tests if equivalent coverage exists. First search for coverage in current suites, including RQ597/BCI16 and RQ140. Classify each observation VERIFIED_GAP, COVERED, or NEEDS_AUTHORITY with exact file/line and risk.
4. For at most three VERIFIED_GAP cases, add deterministic independent tests only if demonstrably not already covered, with small unrelated patches and no formula or policy change. If no reproducible gaps: close audit with no speculative follow-ups.
Acceptance: source-to-test matrix; exact commands/results, zero unexpected skips, CI evidence where available; new production implementation only via separately registered narrowly scoped defect. Update canonical owner status/evidence and fresh post-close queue recovery.

## RQ599 — Historical-period reconciliation and cross-route identity checks
Status: WAITING (promote only if RQ598 documents a new reproducible gap)
Priority: P1; Owner: RQ; Ready after: RQ598's named VERIFIED_GAP not owned by existing RQ.
Scope: strictly the routes and existing facts named in the verified gap.
Task: build a replayable fixture from historical known-good data or sanitized independent seed; compare raw-fact sums with API totals, grouped rows, selected store/date/dataScope, and CSV/export where applicable; assert correct null-vs-zero and missing-cost treatment; include negative and boundary cases. Never infer freshness from age of source import; preserve historical correctness claims separately from live freshness claims.
Acceptance: failing regression before bounded fix, passing after; explicit origin/lineage of fixture; no denominator drift or policy reinterpretation.

## RQ600 — Actionable analytics value/eligibility acceptance matrix
Status: READY (new owner-requested planning/test-contract audit, no release or recommendation activation)
Priority: P1; Dependencies: none; Owner: RQ with product review
Scope: Product Decision, Inventory, pre/post markdown, Supplier decision surfaces, existing trust/quality/recommendation eligibility contracts and value roadmap.
Task:
1. Audit whether each existing action is supported by a precise metric grain (SKU/variant/store/time), source/coverage, freshness, known/unknown cost, reason codes and user-visible evidence link.
2. Separate historical report confidence from current replenishment/transfer/markdown decision eligibility. Flag stale data as ineligible for fresh-action claims, not necessarily unusable for historical comparison.
3. Produce a compact matrix: business decision, displayed KPI, source, denominator, exclusions, status (shipped/partial/gated), expected economic benefit hypothesis, instrumentation needed for measured impact, and blocker owner. Do not invent ROI or accept observed action count as success.
4. Reconcile against RQ530/RQ531/RQ559/RQ585/RQ592, STAB16, QDB07 and owner-decision gates; propose at most two new code tasks only on proven contract defects, and do not duplicate existing task IDs.
Acceptance: reviewed contract matrix with file references, small documentation changes only, explicit owner decisions and non-overlapping next tasks; fresh post-close routing.

## Existing gated tasks — do not duplicate
- STAB16 BLOCKED: authorized Render/Neon effective config, worker/run history, read-only DB/schema, exact production SHA and authenticated browser proof. RQ128, RQ137, RQ140, RQ592 inherit relevant live proof.
- RQ472 journal/watermark policy; RQ530 authoritative supplier buying metric sources; RQ531/RQ558/RQ559 price/model/sample decisions.
- RQ545 provider schema/role; QDB07 pilot UI authorization; MT02 identity; PERF16/SEC05 MT gates.
- RQ139 historical fake-zero residual was closed in RQ152; Pilot Intake refresh lineage uses LastSuccessfulRefreshAtUtc; do not reopen absent a specific new failure.
- RQ597 Color oracle, BCI16 and P-UI-01..54 are DONE; preserve test gains.

## Agent claim safety
Audit-only RQ598 and RQ600 are independent in purpose but inspect overlapping documents. Claim one at a time unless exact paths are partitioned before writing. A future agent must register the new prompt(s) into the existing canonical queue, use existing lock/claim protocol, and obtain fresh main before any edit. Never overwrite another active branch. Agent can report no uncovered test gaps; that is a legitimate completed audit, not grounds for speculative work.
