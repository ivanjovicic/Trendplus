# Analytics Reliability Prompt Queue — Supplier Audit Addendum

Date: 2026-09-30
Repo: ivanjovicic/Trendplus
Source audit: docs/ai/SUPPLIER_ANALYTICS_DEEP_AUDIT_PROMPTS_2026-09-30.md
Current READY prompt: RQ518
Additional READY prompts: RQ524, RQ525
Queue reconciliation 2026-09-30: RQ518 remains the primary deterministic P1 fix. RQ524 and RQ525 are independent, dependency-free P1 proof lanes and are READY in parallel. RQ524 builds/runs only the repository-local reconciliation pack and fixture evidence; production execution remains exclusively RQ454/STAB16. RQ519-RQ532 otherwise remain sequenced/owner-gated as declared below.

This addendum registers only non-duplicate Supplier follow-ups after second-pass source verification. Existing RQ474, RQ475 and RQ487 remain authoritative for overview error/readiness/query-cost work. RQ517 is already DONE for Daily Sales and is not reused.

## Registration and de-dup map

| Audit id | Queue owner |
|---|---|
| SA-F1 | RQ518 |
| SA-F2 | RQ519 |
| SA-F3 | RQ520 |
| SA-F4 corrected | RQ521 |
| SA-F5 | existing RQ474/RQ475; RQ525 for Analytics 015; security handoff only if deploy gap proven |
| SA-F6 | RQ522 |
| SA-F7 corrected | RQ523 |
| SA-P1 | RQ524 |
| SA-P2 | RQ525 |
| SA-P3 | RQ526 |
| SA-P4 | RQ527 |
| SA-P5 | RQ528 |
| SA-I1 | existing RQ474/RQ475 |
| SA-I2 | RQ529 |
| SA-E1 | RQ530 |
| SA-E2 | RQ531 |
| SA-E3 | RQ532 |

---

## RQ518 - Fix Supplier scorecard materialized-view capability detection

Status: IN_PROGRESS
Priority: P1
Type: backend/sql/tests
Feature family: supplier-scorecard-mv-capability
Parallel-safe: no
Owner: Analytics Reliability / Supplier Decision
Claimed: 2026-09-30 by ChatGPT connector workspace after exact-main refresh; no open PR or rq518 branch collision found. Local filesystem lock is unavailable in connector-only execution, so the canonical IN_PROGRESS state is the exclusive remote claim.

### Problem

Supplier Decision Hub checks materialized-view columns through information_schema.columns, while startup detects the same objects through PostgreSQL catalogs. This can yield MISSING_SCHEMA for an existing MV.

### Evidence

Current SupplierDecisionHubEndpoints uses information_schema.columns for mv_supplier_decision_score_cache variants; DatabaseInitializer uses pg_class relkind=m. This is a deterministic code defect, independent of the inherited live observation.

### Scope

Capability detection/readiness meta/tests only; no score formula, threshold, MV-data or production-schema changes.

### Read first

AGENTS.md; RQ401/RQ404/RQ475; SupplierDecisionSchemaSqlTests; Hub contract tests.

### Do

Use one pg_class/pg_namespace/pg_attribute helper plus pg_matviews.ispopulated; distinguish MISSING_OBJECT, MISSING_COLUMNS and NOT_POPULATED; reuse the same semantics in endpoint/startup; apply it to ML-score capability.

### Tests

Static regression forbidding information_schema for MVs; real-PostgreSQL cases when harness exists: populated, missing-column and WITH NO DATA; retain fail-closed missing-schema tests.

### Acceptance

Required populated MVs are never rejected as MISSING_SCHEMA; absent/incomplete/unpopulated objects remain explicitly blocked.

### Dependencies

None; RQ475 consumes the corrected readiness states.

---

## RQ519 - Make Supplier nivelacija schema lifecycle idempotent and preserve scorecard dependencies

Status: WAITING
Ready after: RQ518 DONE
Priority: P1
Type: backend/sql/startup/tests
Feature family: supplier-nivelacija-schema-lifecycle
Parallel-safe: no
Owner: Analytics Reliability / Schema

### Problem

Startup can check readiness before scripts that replace dependent views. 014 Fix and 016 contain DROP ... CASCADE; Analytics 014 owns semantic columns; later rebuild can repair objects, so the lifecycle risk is real but live causality is unproven.

### Evidence

014 Fix replaces vw_vendor_sales_nivelacija with CASCADE; Analytics 014 defines semantic columns; 016 drops vw_nivelacija_did with CASCADE; readiness/history and later Supplier rebuild are separate phases. Scoped vendor-sales SQL also scans unbounded history.

### Scope

Startup ordering/idempotence/readiness re-check and behavior-preserving query bounding; no pre/post semantic change.

### Read first

RQ475/RQ518/RQ525; migration 014 Fix; Analytics 014; migrations 016/018/029; DatabaseInitializer.

### Do

Capture object/history state read-only; establish one canonical view owner; re-check after destructive scripts; deterministically rebuild dependencies before readiness is green; bound scoped sales_daily and avoid equivalent source work three times.

### Tests

Real PostgreSQL repeated-start and changed-history cases; semantic column/MVs survive restart; bounded query equals prior fixture output.

### Acceptance

Restart/hash/order cannot silently break assortment contract or scorecard chain; performance changes preserve values.

### Dependencies

RQ518 DONE; RQ525 is preferred proof and may develop in parallel.

---

## RQ520 - Separate Assortment price-change effect from Supplier PoP recommendation semantics

Status: WAITING
Ready after: RQ519 DONE
Priority: P1
Type: backend/frontend/sql/tests
Feature family: supplier-assortment-price-change-semantics
Parallel-safe: no
Owner: Analytics Reliability / Supplier

### Problem

Assortment feeds markdown pre/post change into the shared Supplier PoP recommendation input. It also lacks maturity, has inconsistent no-post/zero-baseline semantics and can aggregate Change over a different population than Pre/Post.

### Evidence

Current endpoint maps vendor pre/post fields to PopRevenueChangePct/PreviousPeriodRevenue; Analytics 014 uses fixed 30-day windows; endpoint percentage/vendor aggregation reproduces the mismatches.

### Scope

Assortment price-change effect semantics/labels only; no invented buying-action thresholds and no Overview PoP-engine change.

### Read first

RQ519; vendor-sales-nivelacija endpoint/view/scoped SQL; recommendation engine; assortment tests.

### Do

Use dedicated descriptive effect states; keep them non-actionable until owner approval; add elapsed/mature state; mature valid-baseline no-post -> zero; zero baseline -> unavailable/no_baseline; Change uses same comparable rows; margin benchmark uses documented weighted covered population.

### Tests

Mature/immature, no-post, zero-baseline, same-population and weighted-margin fixtures; frontend proves Overview PoP labels are not reused.

### Acceptance

No markdown effect is presented as ordinary PoP; no fake +100 baseline; Change equals Post minus Pre for declared comparable rows.

### Dependencies

RQ519 DONE; owner approval only if effect becomes actionable.

---

## RQ521 - Repair confirmed Supplier scorecard input bugs without changing model policy

Status: WAITING
Ready after: RQ518 DONE and RQ526 DONE
Priority: P2
Type: sql/backend/tests
Feature family: supplier-scorecard-input-correctness
Parallel-safe: no
Owner: Analytics Reliability / Supplier Decision

### Problem

Confirmed input defects: return rate divides by net units, sales_in_period requires current and sale-time supplier equality, and receipt population is inconsistent. Coverage gates, inventory penalty and confidence weights are policy questions, not this fix.

### Evidence

Windowed score SQL derives returns from negative units and denominator from signed/net units; it cross-checks current supplier with supplier_id_at_sale; full-price/nivelacija sources do not consistently apply canonical DUG/KOREKCIJA policy.

### Scope

Only proven input/population correctness; no thresholds, weights, cutoffs, inventory normalization or cost-fallback policy.

### Read first

RQ445; RQ473; RQ526; 018/029 Supplier SQL.

### Do

Return rate uses gross positive sold units and missing stays unavailable/neutral; use canonical sale-time attribution; bound sales evidence to documented dates; apply canonical receipt exclusions; fix suspected double-return subtraction only if oracle proves it.

### Tests

Oracle cases for returns, supplier change, receipt exclusion and period boundary; update SQL guards.

### Acceptance

Oracle/MV agree on corrected inputs without changing owner-approved model policy.

### Dependencies

RQ518 DONE and RQ526 DONE; RQ445/RQ473 remain authority.

---

## RQ522 - Declare Supplier cross-tab basis and align cheap canonical mismatches

Status: WAITING
Ready after: RQ519 and RQ521 DONE
Priority: P2
Type: backend/frontend/contract/tests
Feature family: supplier-cross-tab-basis
Parallel-safe: no
Owner: Analytics Reliability / Supplier

### Problem

Overview, Scorecard and Assortment differ in attribution, cost, receipt population, cohort and period semantics. Some differences can be valid, but they are not consistently disclosed.

### Evidence

Overview uses sale-time/snapshot-aware basis; scorecard/assortment use markdown-event cohorts with different attribution/cost; assortment filters event date while sales windows extend around it.

### Scope

Machine-readable/backend basis/provenance metadata plus low-risk canonical alignment; no silent cohort or attribution rewrite. Visible cross-tab metric naming, percentage-point units and report/export labels remain owned by RQ498 and must consume this basis rather than being duplicated here.

### Read first

RQ445/RQ473/RQ498/RQ519/RQ521; shared context/provenance contracts; Supplier shell/endpoints.

### Do

Add basis metadata and 'Kako se broji'; collapse unresolved supplier ids into one unknown bucket with diagnostic count; reuse existing business timezone contract or document UTC; check nivelacija store grain read-only; record rather than auto-apply broad attribution changes.

### Tests

Basis contract, unknown-bucket, timezone and store-grain fixtures.

### Acceptance

Every tab states its basis; remaining differences are explicit and explainable.

### Dependencies

RQ519 and RQ521 DONE; RQ445/RQ473 prior authority.

---

## RQ523 - Fix Supplier frontend residuals without redefining metric policy

Status: WAITING
Ready after: RQ520 and RQ522 DONE
Priority: P3
Type: frontend/copy/tests
Feature family: supplier-frontend-residuals
Parallel-safe: no
Owner: Analytics UI

### Problem

Focused Supplier share is recomputed over one visible row, revenue rank badges also appear for ascending sort, assortment chips can show draft/raw state, and category is not URL-persistent. C22 engine-English claim is stale; RQ488 fixed it. Shared pending/critical/gated trust semantics are owned by RQ499; visible cross-tab basis/unit wording is owned by RQ498.

### Evidence

Current display projection runs after focused filtering; rank is index+1 whenever sort field is revenue regardless of direction; assortment applied-state handling is inconsistent. AnalyticsTrustHeader trust-state wording remains evidence for RQ499, not this prompt.

### Scope

Presentation/state residuals only. No recommendation-code, shared trust-state semantics, cross-tab basis/unit naming or signed margin-share denominator change; those remain RQ499, RQ498 and RQ531 as applicable.

### Read first

RQ476/RQ488/RQ498/RQ499; Supplier overview/assortment specs.

### Do

Preserve backend whole-population share when focused; badges only for descending revenue; chips use activeFilters and Serbian enum labels; persist category. Do not redefine shared trust-state copy or the signed margin-contribution denominator here; those remain RQ499 and RQ498/RQ531 respectively.

### Tests

Focused share, asc/desc rank, applied-filter chip and category deep-link tests.

### Acceptance

No focused 100% artifact, inverse badge, raw enum/jargon or silent denominator change.

### Dependencies

RQ520, RQ522 and RQ498 DONE.

---

## RQ524 - Add read-only Supplier analytics reconciliation pack

Status: IN_PROGRESS
Claimed: 2026-09-30 by ChatGPT connector workspace after exact-main refresh; no rq524 branch/search collision found. Connector-only execution cannot create a filesystem lock, so this canonical IN_PROGRESS state is the exclusive remote claim.
Priority: P1
Type: sql/qa/evidence
Feature family: supplier-reconciliation-evidence
Parallel-safe: yes
Owner: Analytics Reliability / QA

### Problem

The live audit could not validate Supplier numbers, so source review cannot prove remaining data/deploy hypotheses.

### Evidence

Overview has strong fixtures, but scorecard/assortment lack equivalent live/read-only reconciliation and cross-tab basis proof.

### Scope

Read-only scripts/evidence only. Repository-local fixture execution is in scope; production/replica execution is explicitly out of scope and remains RQ454/STAB16. No writes.

### Read first

Existing check_supplier_sales_stats.sql; RQ407/RQ447/RQ454; RQ518-RQ523; context/receipt/cost policies.

### Do

Add parameterized checks for Supplier sum, unknown split, previous-only suppliers, cost coverage, attribution drift, DUG/KOREKCIJA share, MV health/refresh/capability, vendor-sales columns/startup history, assortment comparable totals, scorecard-vs-overview explained delta, nivelacija store grain, immature/no-post events, and NabavnaCenaDin vs NabavnaCena unit sanity.

### Tests

Run against deterministic fixture and prove the pack is read-only. Do not run it against production here; RQ454 consumes the reusable pack when STAB16 grants the approved production gate.

### Acceptance

Every check has pass/fail/explained result and failures map to an owner; hypotheses stay hypotheses without evidence.

### Dependencies

None for repository-local fixture work. RQ454/STAB16 exclusively own production/replica reconciliation execution.

---

## RQ525 - Add real-PostgreSQL Supplier schema readiness harness

Status: READY
Priority: P1
Type: tests/infrastructure
Feature family: supplier-schema-readiness-tests
Parallel-safe: yes
Owner: Analytics Reliability / Schema

### Problem

Static SQL tests did not catch MV capability or startup-order/CASCADE risks.

### Evidence

N01 is catalog-level; 014/016/018/029 require real PostgreSQL. Analytics 015 contains invalid/transaction-sensitive SQL but current startup does not execute that optional overlay.

### Scope

Test infrastructure/schema proof only; do not wire Analytics 015 into startup to make tests pass.

### Read first

RQ518/RQ519; current Testcontainers/integration setup; 013/014/016/018/029 and Analytics 014/015.

### Do

Apply Supplier startup sequence to seeded PostgreSQL; prove MV capability, repeated-start idempotence, semantic-column survival and refresh prerequisites; validate Analytics 015 in an isolated explicit test and preserve its current non-startup status.

### Tests

Harness plus focused schema/queue tests.

### Acceptance

Reproduces pre-fix MV issue, protects lifecycle after fixes and makes optional-015 validity explicit.

### Dependencies

None; coordinate with RQ518/RQ519 paths.

---

## RQ526 - Add independent Supplier scorecard oracle and golden fixture

Status: WAITING
Ready after: RQ525 DONE
Priority: P2
Type: tests/sql
Feature family: supplier-scorecard-oracle
Parallel-safe: yes
Owner: Analytics Reliability / Supplier Decision

### Problem

Scorecard contract/static tests do not independently prove formula inputs, rankings or recommendation output.

### Evidence

Audit found confirmed input bugs plus model-policy choices whose effects cannot be safely judged from source alone.

### Scope

Independent oracle/golden evidence; matching SQL does not approve disputed policy.

### Read first

RQ521/RQ531; 018/029 SQL; current scorecard tests.

### Do

Seed hand-computable suppliers for sell-through, margin, markdown, stock, returns, supplier change, DUG/KOREKCIJA, missing cost and first-markdown edges; implement independent C# oracle; test monotonicity/clamping and unrelated-supplier percentile effects.

### Tests

Real-PostgreSQL oracle comparison plus property/counterexample tests.

### Acceptance

Every component is independently reproducible; input bugs fail before/pass after; policy choices remain explicit.

### Dependencies

RQ525 DONE.

---

## RQ527 - Add Assortment pre/post oracle and golden fixture

Status: WAITING
Ready after: RQ519 DONE and RQ525 DONE
Priority: P2
Type: tests/sql
Feature family: supplier-assortment-oracle
Parallel-safe: yes
Owner: Analytics Reliability / Supplier

### Problem

Assortment lacks an independent oracle for event windows, maturity, no-post/zero-baseline and vendor aggregation.

### Evidence

Current tests do not prove N04-N09/N23/N24 aggregate semantics.

### Scope

Fixture/oracle evidence only.

### Read first

RQ519/RQ520; vendor-sales-nivelacija SQL/policy tests.

### Do

Seed mature/immature, no-post, zero-baseline, repeated-event, price-increase, store-vs-chain and partially comparable cases; independently compute windows/aggregates; compare bounded/unbounded source equivalence.

### Tests

Golden JSON plus real-PostgreSQL comparison.

### Acceptance

Totals match declared population; Change equals Post-Pre for comparable rows; maturity/baseline states explicit; bounded SQL value-equivalent.

### Dependencies

RQ519 DONE and RQ525 DONE.

---

## RQ528 - Add Supplier cross-tab parity contract

Status: WAITING
Ready after: RQ522 DONE
Priority: P2
Type: tests/contract
Feature family: supplier-cross-tab-parity
Parallel-safe: yes
Owner: Analytics Reliability / Supplier

### Problem

No automated contract prevents silent basis drift among Overview, Scorecard and Assortment.

### Evidence

Audit confirmed attribution, cost, receipt, cohort and period differences.

### Scope

Cross-tab reconciliation only; intentional differences allowed when metadata fully explains them.

### Read first

RQ522; shared context/evidence contracts.

### Do

Call all three surfaces on one fixture; assert equality where basis is equal and exact explained deltas where it differs; assert consistent supplier identity.

### Tests

Focused cross-tab fixture suite.

### Acceptance

Silent basis changes fail; intentional differences remain machine-readable/explainable.

### Dependencies

RQ522 DONE.

---

## RQ529 - Fix Supplier accessibility, date formatting and terminology help

Status: WAITING
Ready after: RQ523 DONE and RQ499 DONE
Priority: P3
Type: frontend/a11y/copy/tests
Feature family: supplier-a11y-formatting
Parallel-safe: no
Owner: Analytics UI

### Problem

Supplier shell lacks one H1, tab semantics are inconsistent, sortable headers need aria-sort, native date display is locale-dependent, duplicate store labels can remain ambiguous and metric terminology needs accessible help. Shared trust-state severity/pending/gated wording is explicitly outside this prompt and remains RQ499.

### Evidence

Current shell uses H2; tab buttons combine aria-selected/aria-current without tab roles; audit observed locale/label ambiguity. Shared trust-header state wording is handled by RQ499.

### Scope

Accessibility/date/terminology-help formatting only; preset alignment is owner decision if it changes filters. Do not change trust-state meaning or severity copy.

### Read first

Supplier shell/page specs; RQ499/RQ523; shared accessibility patterns.

### Do

Add coherent heading/tab or link semantics, aria-sort/direction, Serbian date display, stable duplicate-store disambiguation and glossary InfoTips; get owner decision before changing canonical presets. Do not alter pending/critical/recommendation trust semantics here.

### Tests

RTL/axe-style tab/sort, date-format and duplicate-label tests.

### Acceptance

Keyboard/screen-reader semantics coherent, dates stable Serbian, jargon explained.

### Dependencies

RQ523 DONE and RQ499 DONE.

---

## RQ530 - Add Supplier buying-value panel with independently proven metrics

Status: WAITING
Ready after: RQ522 DONE, RQ500 DONE, RQ524 evidence available, and RQ474/RQ487 make Overview reliably usable
Priority: P2
Type: backend/frontend/product/tests
Feature family: supplier-buying-panel
Parallel-safe: no
Owner: Analytics Product / Supplier

### Problem

Current surfaces describe sales/markdown behavior but omit several buyer decisions such as stock cover, return burden, margin trend and article winners/losers.

### Evidence

This is an enhancement gap, not a current-number bug; PO/lead-time sources are not yet proven.

### Scope

Read-only source discovery first; only metrics with authoritative source/formula.

### Read first

RQ522/RQ524; Inventory/Supplier contracts; purchase/receipt entities.

### Do

Discover PO/receipt/lead-time sources; add proven stock units/value, days cover, sell-through, gross return rate, equal-period margin trend, top/bottom articles and aged stock with basis/coverage; PO/lead time only if authoritative, otherwise unavailable reason.

### Tests

Fixture endpoint tests per metric, reconciliation and detail-drawer UI tests.

### Acceptance

Every metric has source/formula/unit/coverage/missing behavior and independent proof.

### Dependencies

RQ522 DONE, RQ500 DONE and RQ524 evidence; RQ474/RQ487 make Overview usable.

---

## RQ531 - Govern and explain Supplier scorecard model weights and thresholds

Status: WAITING
Ready after: RQ526 DONE and product owner approves model-policy decisions
Priority: P2
Type: product/sql/frontend/docs/tests
Feature family: supplier-scorecard-model-governance
Parallel-safe: no
Owner: Analytics Product / Supplier Decision

### Problem

Scorecard uses relative percentile ranks, absolute-stock exposure, strict coverage gates, clamping and hard-coded confidence/recommendation thresholds. These are implementation facts, not automatically bugs.

### Evidence

029 contains percentile ranks, gates, inventory-value rank, confidence weights and thresholds; endpoint has page-level coverage gate. First audit mixed some policy choices into bug-fix scope.

### Scope

Model documentation, owner decision, formula versioning/explainability; no unapproved weight/threshold change.

### Read first

RQ521/RQ526; current scorecard formula SQL/UI.

### Do

Document every component/range/weight/penalty/gate/cutoff with rationale or owner-needed marker; expose contributions/rank population/clamping/formula version; owner approves or changes coverage gate, inventory normalization, confidence weights, cutoffs and period-scaled samples; make signed margin-share denominator explicit; label current-stock inputs honestly.

### Tests

Update RQ526 oracle to approved version plus explainability/version API/UI tests.

### Acceptance

Every model policy is versioned, explainable, owner-approved and oracle-matched.

### Dependencies

RQ526 DONE and explicit product-owner decision.

---

## RQ532 - Add Supplier size-curve and controlled markdown effectiveness evidence

Status: WAITING
Ready after: RQ520, RQ527 and RQ500 DONE
Priority: P3
Type: backend/frontend/product/tests
Feature family: supplier-assortment-size-curve
Parallel-safe: no
Owner: Analytics Product / Supplier

### Problem

Assortment lacks size-curve evidence and controlled markdown-effect view separating simple pre/post from causal control comparison.

### Evidence

Repository has DiD/control lineage, but footwear-size source availability at article/sale grain must be proven.

### Scope

Source discovery plus additive evidence; no causal claim from raw pre/post.

### Read first

RQ520/RQ527; nivelacija DiD/control contracts; article/sale schemas.

### Do

Prove size availability first; if absent stop with explicit unavailable evidence. If present calculate sold/received/on-hand by size per supplier x footwear type. For mature events expose controlled uplift only through approved DiD/control contract with population/maturity/confidence metadata.

### Tests

Fixture size-curve and controlled-effect tests with maturity/control counterexamples.

### Acceptance

Every insight states population, maturity/control basis; raw pre/post is never presented as causal uplift.

### Dependencies

RQ520, RQ527 and RQ500 DONE.
