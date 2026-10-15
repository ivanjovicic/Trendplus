# Trendplus — analytics next execution brief and agent prompts (2026-10-15)

**Scope:** current, source-backed work orders for existing RQ/STAB/Access owners. **This is not a new canonical queue, new RQ numbering, claim reservation, READY promotion or independent product roadmap.** Formal statuses are only in `MASTER_ROADMAP.md`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` and the existing STAB/BCI queues. Read `AGENTS.md`, `.github/copilot-instructions.md`, `docs/ai/AGENT_START_HERE.md` and `docs/ai/PROMPT_QUEUE_PROTOCOL.md` before running any of these. Inspect fresh `origin/main`, current PR heads, claims and dependencies immediately before editing.

Requested 2026-10-15. Revalidated GitHub repository after `main` advanced from `6ff22b7c` to `a85191c9540914a0680c1fc4ec6d9d44c7261315`; the comparison has **two evidence-only commits** affecting `.ai/runs/2026-10-15-RQ667-evidence.md`, no analytics runtime source changes. Re-check before execution; GitHub state changes independently.

## Business objective and scope boundaries

Existing retailer workflow: **dated sales → store/supplier/profit context → capital tied in inventory → bounded markdown decision → action/disposition → observed outcome**. No new dashboard/scorer/AI/marketplace/scraper features; no cosmetic P3 cleanup or blanket ESLint fixes. Max **two path-disjoint engineering tasks** in flight, plus external/operator evidence gathering. Reuse shipped RQ585 digest, RQ659–RQ668 UX/identity work, RQ605 scope semantics, RQ598–600 coverage/value audits and RQ664 browser/CSV fixture proof. Do not register or reopen them.

Historical correctness and current-action eligibility are **different** gates. The 2026-10-10 batch #23 read-only oracle reconciled 5,530 Access sale headers and 67,092 lines to SalesFacts/SalesLineFacts with zero paired quantity/price/amount delta; it does **not** prove two-source lineage, basket semantics, live Render state or fresh ordering decisions. For 66,233 retail Access lines, 501 have unknown cost, representing 1,711,050 RSD sales; numeric cost elsewhere is not automatically source-confirmed. Last **dated** live source horizon from 2026-10-07 was 2026-08-05; no fresh live verification in this review. RQ610 still lacks a verified SKU×store mutation generation. An absent operator session is **NOT RUN** and a financial outcome remains **NOT MEASURED**.

### What was actually inspected

- GitHub `main` and open PR #163, #169, #171, #172, their diffs/metadata and Actions logs; `main` backend run `38712103016` SUCCESS (2,132 pass / 43 existing skips), `main` frontend run `38712102729` FAILURE on Inventory period control race; PR #172 exact-head backend+quality workflows SUCCESS; PR #163 exact-head backend `38723524869` SUCCESS (2,143 pass / 43 skips), quality `38723524551` SUCCESS.
- PR #171 exact-head `38717335842`: **1,252 frontend tests passed**, then analytics guardrail FAILED with two line-shifted findings, `InventoryPage.tsx:1068 fake_zero_fallback` and `PreNivelacijaPriorityPage.tsx:746 decisionScore_assign`, plus corresponding removed-baseline entries (old lines 1061 and 745). This is a **guardrail result**, not proof of newly introduced fake-zero decision behavior. Its backend `38717335957` SUCCEEDED.
- Read code for `AnalyticsDashboard`, `DailySalesStatsPage`, `InventoryPage`, `SupplierSalesStatsPage`, `SupplierDecisionHubPage`, `ProductDecisionCenterPage`, `PreNivelacijaPriorityPage`, `ProdajaPrePostNivelacijePage`, `DataQualityPage`, `AnalyticsActionsPage` and `DecisionPulsePage`; read canonical product scopes, agent routing/protocol, 2026-10-14 frontend audit, 2026-10-13 bug review, 2026-10-10 SQL population evidence and product playbook. **No authenticated deployed browser/screenshots/Render worker logs/new Neon SQL results were obtained.** Code-level UX assessment is not a production browser certification.
- Existing queue post-import header explicitly says `Current READY: none`; RQ666/RQ667 are DONE, RQ669–671 WAITING, STAB16/RQ448/RQ565/RQ601/RQ610/RQ592 have named external/provenance gates. This brief does not override that routing.

## Screen-by-screen decision assessment (current code + dated proofs, not new live render)

| Existing surface | What it currently supports | Remaining decision gate / target verification |
|---|---|---|
| `/analytics` Dashboard + `/analytics/daily-sales` | Dated sales/trend/table and shift/supplier views; RQ668 fixes incomplete 7-day momentum windows; RQ663 shift evidence already DONE | Prove only changed post-import source periods / returns / exports; don't re-run general audit; show last observed source horizon, not 30-day default as proof of currentness |
| `/analytics/supplier`, supplier report, shoe-type/color | Descriptive supplier/type and known-cost projections, sorted/exported rows; existing independent parity tests | Source-confirmed cost and category/variant coverage before asserting reliable margin/rank; stale-snapshot filter isolation under PR #171 |
| `/analytics/inventory` | Latest available SKU/store snapshot, scoped filtering and actions (RQ607, RQ667 DONE) | No historical stock-as-of or certified capital valuation while RQ610 and RQ601 are open; verify PR #171 filter-context isolation and transfer commit cache invalidation |
| `/analytics/products` | Backend-governed recommendation and action identity; RQ660/RQ666 DONE | Action-eligible only where provenance/currentness passes; no frontend promotion of `FIX_DATA` / `insufficient_data` to monetary opportunity |
| `/analytics/pre-nivelacija-prioriteti`, `/analytics/nivelacije-pre-post` | Candidate/observed descriptive pre/post data with source/quality warnings | RQ602 historical store/day coverage and RQ592 prospective proof; never causal uplift from naïve pre/post |
| `/analytics/data-quality` | Missing/unknown dimensions, costs and provenance evidence; RQ651 fake-zero fix already owned/delivered | Confirm exact source and explicit unknown denominator where financial action depends on it; no zero-for-null substitution |
| `/analytics/decision-pulse`, `/analytics/actions` | Shipped RQ585 digest, action/disposition/outcome UI | Actual operator observation and measured action coverage; do not rebuild a digest or invent outcome |
| Shared `useReliableAnalyticsQuery`, transfer mutation/cache | PR #171 proposes context-bound snapshots and post-commit invalidation | Inspect stale-before-effect render, same-context failed-refresh fallback, commit failure/rollback and actual invalidation after confirm/complete/cancel |

Large page components (`AnalyticsDashboard` ~3,120 lines, `SupplierSalesStatsPage` ~3,115, `DailySalesStatsPage` ~2,757, `ProductDecisionCenterPage` ~2,716, `InventoryPage` ~2,134) create maintenance cost, but **file size alone is not a verified P0**. Extract only when bounded bug/coverage/performance evidence supports it.

## Prioritized execution sequence

| Lane | Work order / existing owner | Status for this brief | Why now | Depends on |
|---|---|---|---|---|
| 1 | **A — Close existing analytics cache/filter CI safely**, PR #171 / #172 | REPO-LOCAL, existing open PRs; no new RQ | Prevent stale/wrong-filter inventory and recommendation views; remove current CI failure | Exact current PR diff, path/owner collision; regression and guardrails |
| 2 | **B — Access sale/source identity and independent oracle**, PR #163, issues #173/#174 | P0 code partial; source-namespace **EXTERNALLY GATED** | Wrong sale parent/source can corrupt downstream analytics | Stable operator-owned source namespace, read-only reconciliation, all FK paths |
| 3 | **C — Deployed source freshness/worker/readiness**, STAB16/RQ448/RQ565 | EXTERNALLY GATED | Without deployed identity and source freshness, historical history cannot be marketed as current action | Authorized Render/Neon/provider read-only access |
| 4 | **D — Original purchase-cost evidence**, RQ601 | OWNER SOURCE GATED | Unknown/estimated capital and margins must not become confirmed money | Original 20 calculation lines with unit/currency/tax proof |
| 5 | **E — Inventory stock mutation generation**, RQ610 | CODE PROBE ALLOWED; full implementation SOURCE GATED | Current SKU snapshot != historical stock flow | Proven store×SKU mutation event stream, idempotence and coverage |
| 6 | **F — Historical markdown and first observable outcome**, RQ602 then RQ592 | HISTORICAL READ-ONLY; prospective gate WAITING | Separate descriptive change from attributable financial outcome | Source/day coverage, eligible controls, real current source/action authority |
| 7 | **G — One observed operator workflow**, existing `OP-PREP` + pilot runbook | PREP READY; real observation HUMAN GATED | Only real operator use determines next product feature | Willing operator, consent, dated verified read-only period |

**Policy:** Work orders A and the repository-only part of E/B can be disjoint after claim review; only two engineering items simultaneously. External evidence C/D/G may proceed independently. Don't keep reopening PR/queue/log audits when the next actual input is a named operator/source artefact.

---

## Prompt A — repair and consolidate the active cache/filter branch

**Owner:** existing PR #171; coordinate overlapping PR #172. **Mode:** immediate repo-local review, no new task ID. **Paths:** `Klijent/clientapp/src/hooks/useReliableAnalyticsQuery.ts`, `InventoryPage.tsx`, `PreNivelacijaPriorityPage.tsx`, Color/Shoe/PrePost pages, `Api/Services/TransferService.cs`, cache-policy/tests and relevant guardrail baseline.

> Fetch origin/main and both PR heads. Establish whether #171 already contains #172's exact Inventory `findByLabelText` fix and do not merge duplicate/conflicting patches twice. Reproduce exact-head #171 CI failure: 1,252 frontend tests pass but `check:analytics-guardrails` calls out line-shifted `fake_zero_fallback` and `decisionScore_assign`. Inspect source semantics and rule implementation: prove whether these are genuinely new violations or stale line-number baselines, then make the **smallest** correction. Do not blindly add exceptions or disable checks. Add/verify three fail-first contracts for the shared hook and screens: (1) switching `dataScope`/store/period/search/page hides the previous context's rows, totals and CSV until new data; (2) failed reload in the **same** context may show explicit stale data without false currentness; (3) after transfer confirm/complete/cancel, invalidate the correct analytics cache families only after committed business mutation, and a failed transaction must not invalidate/claim success. Verify the runtime `IServiceProvider` injection/invalidation wiring, Redis/memory parity where supported and cancellation-safe post-commit behavior. Run focused tests, `npm run test:analytics`, `npm run check:analytics-guardrails`, `npm run typecheck`, frontend build, focused transfer/cache PostgreSQL tests, full CI on the **exact final SHA**. Ensure PR #171 and PR #172 are reconciled safely; record which one supersedes the other only after verification. No production mutation, no fake-green rebaseline and no unrelated UI restyling. Update the original PR/evidence, not a new large audit.

**Acceptance:** 0 new rule violations; scenario-specific tests fail on old behavior and pass on fix; identical filters preserve only explicitly marked same-context stale fallback; changed filters never expose prior source, export or action; relevant exact-head CI green; no duplicate competing PR patch. **STOP** if unproved transfer/Redis backend requires a new operational policy.

## Prompt B — Access identity P0, independent historical integrity oracle

**Owner:** issues #173/#174 and draft PR #163; **do not merge this draft merely because exact-head CI is green.** **Mode:** read-only reconciliation + locally disposable PostgreSQL; source namespace still operator-gated.

> Read PR #163, issue #173/#174, batch #23 audit, `Api/Services/AccessImportService.cs`, `TrendplusDbContext`, sale/return/article/journal import paths and their tests. Enumerate **source DB identity, source table, source row, target row, parent FK and analytic sale/line fact lineage** separately, including POS/existing/imported. Confirm real Access source primary-key availability before assuming line IDs. Define durable **operator-owned source namespace** (not filename/random upload key), API validation and safe schema/upsert upgrade with a read-only legacy dry-run report. For 5,530 source headers and 67,092 imported lines in dated batch #23, classify exact/missing/ambiguous and compute independently, not by calling importer projections, the operational→SalesFacts/SalesLineFacts row count, signed quantity, price and money oracle for `imported`, `existing`, `all` and matched store/periods. Do not conflate receipt grain with customer basket grain; exclude DUG/KOREKCIJA and preserve returns. Test two independent Access sources with identical numeric IDs, POS collision, direct/line-derived/Dnevnik strategies, source line without stable ID, retry/full/incremental, concurrent importer/POS, rollback at flush/cursor/analytics and downstream non-sale FKs. Do not silently reparent, re-key, duplicate or repair legacy rows on receipt/date/price heuristics. Keep permanent code and data mutations separate from the **read-only** reconciliation. Record exact source input required; if unavailable, leave P0 PARTIAL with reproducible test scaffolding and a short blocked decision rather than merging.

**Acceptance:** independent SQL oracle with before/after and negative control; zero unexpected skipped PostgreSQL cases; documented migration/up/down safety and real source namespace; all source→target/FK cases proved. No production backfill or deployment without separate approval.

## Prompt C — deployed truth (existing STAB16 / RQ448 / RQ565)

**Owner:** STAB; **requires authorized provider access**. **Mode:** read-only only.

> Fetch current origin/main and actual deployed `/api/runtime/version` SHA, Render **web and separate worker** config/state, Vercel deployment identity, Neon DB/schema/migration fingerprints, last durable Access import and analytics refresh/worker records, materialized views and real source sales horizon. Do not infer separate worker health from `workersEnabled=false` in the web process. For one documented `dataScope`, store and bounded period, compare **read-only database oracle → deployed API → deployed browser → actual CSV export**, including warning/meta, return sign, partial/unknown, cache generation and source horizon. Mark each unavailable layer UNVERIFIED, not PASS. If missing access, provide one minimal checklist of specific requested permissions and stop. No credentials in public evidence, no production write or deploy. Keep release STAB queue status truthful and do not require a new import merely to validate historical arithmetic.

**Acceptance:** actual environment and SHA/time in evidence, source freshness + analytics schema/refresh and browser/export parity with precise PASS/FAIL/UNVERIFIED per layer; production-current recommendations only when all relevant sources are current and eligible.

## Prompt D — purchase-cost authority (existing RQ601; not another scale heuristic)

**Owner:** RQ601 + retailer source owner. **Mode:** external document collection and authorized analysis; no guessed conversion.

> Use the **already prepared 20-item** source checklist in `docs/qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md`. Do not select another 20 SKUs or rerun the batch import. For each actually supplied original inbound calculation, verify article/variant, source row/date, supplier, unit and quantity per pair, source currency, FX, transport/discount, VAT basis and historical RSD net purchase cost **against the same sales/source key**. Recompute discrepancy distribution across existing master `NabavnaCena`, `NabavnaCenaDin`, sale-line cost and original; retain `unknown` until verified. Check whether 501 unknown-cost sale lines / 1,711,050 RSD revenue materially change displayed margin/capital/decision eligibility for the documented cohort. Never rewrite historical cost, replace NULL with 0, claim a scale multiplier from ratio alone or publish private source invoices in GitHub.

**Acceptance:** sample-level provenance ledger with source evidence references (without private documents), one explicit decision per cost source, confirmed versus estimated versus unknown UI/API treatment and reproducible margin denominator/covered-revenue checks. If originals absent: PARTIAL, named missing inputs and no price-model change.

## Prompt E — prove inventory movement history before expanding capital KPIs (RQ610)

**Owner:** RQ610. **Mode:** code/source qualification first, not new speculative metric.

> Trace SKU×store on-hand changes from Access import, manual article edit, sales/POS, returns, transfers (confirm/complete/cancel) and `DnevnikPromena`; identify event timestamp, sign, origin, duplicate/replay key, store and sequence/generation. Contrast live `Artikli.Kolicina`, newest observed snapshot and existing daily capture with true historical as-of coverage. For each pathway, make a negative PostgreSQL test for concurrent writers, replay, delayed/out-of-order sync and missing movement. Clearly identify whether a trustworthy monotonic stock-change generation can be calculated from existing source events; if not, specify **exact missing producer fields**, no fabricated history and no new production migration. Require that Inventory, Product Decision and supplier-capital views keep `latest snapshot` and unknown-value labels while evidence is absent. Do not reactivate historical turns/GMROI/OOS lost-sales recommendation on a mere snapshot.

**Acceptance:** per-producer read/write/source matrix and either proven end-to-end generation with disposable PG tests or explicit SOURCE GATED blocker. No retroactive stock as-of, arbitrary capital, or automatic order.

## Prompt F — descriptive markdown now; causal/outcome later (RQ602 → RQ592)

**Owner:** RQ602/RQ592. **Mode:** read-only retrospective before real prospective pilot; no price changes.

> Reuse the existing frozen 20-treated/20-control historical dry-run and `tools/markdown-pilot-measurement.sql`; independently verify SKU×store×business-day event/source coverage, [start,end) period bounds, signed returns, DUG/KOREKCIJA exclusions, control maturity, cost provenance and inventory availability. Mark unmatched/partial/unstable windows `insufficient_data` instead of reporting recovered profit. Check `ProdajaPrePostNivelacijePage`, `PreNivelacijaPriorityPage` and exports against backend eligible/status/meta fields. Only after STAB16 current source, owner decision and preregistration does RQ592 define prospectively measured outcome, action date, actual price execution proof, post period and valid controls. No claim of causal uplift from historical 20/20 percentages.

**Acceptance:** same population and limits in SQL/API/table/export; explicit descriptive vs measured flags, robust insufficient-signal tests and one owner-gated preregistration trigger. No new forecast/elasticity score.

## Prompt G — a real operator session using existing screens, not a new UI backlog

**Owner:** business/product owner; repo-only preparation can be delegated, **user observation needs a real consenting person**. **Mode:** `OP-PREP` from `docs/pilot/RETAIL_OPERATOR_VALUE_SESSION_2026-10-14.md`.

> Prepare a one-page, dated and **read-only** walkthrough using existing `/analytics/daily-sales`, `/analytics/supplier`, `/analytics/inventory`, `/analytics/pre-nivelacija-prioriteti`, `/analytics/decision-pulse` and `/analytics/actions`. Choose one **verified historical** store/scope/period only if backed by exact source evidence; where missing, mark `NOT VERIFIED`. Ask a real footwear operator to attempt independently: reconcile sales and export; identify supplier decision and cost limits; identify stock/markdown candidate or explain correct block. Record completion (0–3 rubric), time-to-evidence, three objections, actual disposition and next step versus POS/Excel. Document consent and anonymize findings; mark NOT RUN until observed. **Only if a real recurring friction is demonstrated** register one bounded P1 under an existing RQ owner with failing-first test; otherwise no new code. Don't claim owner acceptance, real money saved or an outcome from synthetic runbook.

**Acceptance:** completed real session evidence or honest NOT RUN state; no generic redesign; clear go/no-go for one bounded read-only pilot.

## Verification and collision contract for all prompt executions

1. Resolve exact latest `main`, open PR heads and current owner queue/locks; no duplicate task claim, no rebasing/force-pushing another agent's head and no refresh of deprecated DONE prompts.
2. Evidence ladder: current code → focused negative test → disposable PostgreSQL independent oracle when money/identity is involved → affected UI/screenshot/CSV/browser proof (if authorized) → exact-head Actions. Distinguish test passes from source reconciliation, actual operator decisions and measured financial results.
3. For the 43 known backend skips, classify by the existing BCI17 test inventory; do not present `43 skipped` as zero skips or indiscriminately change test tags. Vercel free-tier rate-limit status is not product correctness; check required CI independently.
4. Stop on unverified source prices, namespace, historical stock generation, production credentials, direct data mutation/deploy or causal policy; report the **minimal named input** needed. Separate branch/PR when ownership/path/feature families collide.
5. Update just the owning queue/task and one short run log **if status or material evidence changes**. No additional audit cycles, duplicative dashboards or large cosmetic cleanup. On completion verify SHA on main and cite exact CI checks; when safety gates fail leave a draft and explain why.

## Candidate next-cycle milestone, not automatic implementation authorization

1. **Engineering:** settle PR #171 and #172 overlap after fixing verified guardrail/source-context issues. Independently advance P0 Access read-only namespace/oracle work only while current data does not require destructive repair.
2. **Operator/source:** obtain provider/deployment read-only proof (STAB16), 20 authoritative calculation rows (RQ601), and one real operator (OP-PREP).
3. **Then:** prove stock-mutation generation (RQ610) and first bounded current-action outcome with RQ592 preregistration. Until then, position Trendplus as a **dated, reconciled read-only retail analysis**, not current inventory autopilot or profit optimizer.

**External references:** [PR #163](https://github.com/ivanjovicic/Trendplus/pull/163), [PR #169](https://github.com/ivanjovicic/Trendplus/pull/169), [PR #171](https://github.com/ivanjovicic/Trendplus/pull/171), [PR #172](https://github.com/ivanjovicic/Trendplus/pull/172), [Access source namespace issue #173](https://github.com/ivanjovicic/Trendplus/issues/173), [other entities issue #174](https://github.com/ivanjovicic/Trendplus/issues/174).

**Proof limitations:** source checks are dated; no new real UI render, production DB access, business operator or original cost document was available. Future agents must verify latest remote state, not rely on this snapshot's statuses.
