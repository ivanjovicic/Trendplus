# Analytics Reliability Next-Wave Audit — 2026-09-29

Repo: `ivanjovicic/Trendplus`  
Inspected current-main baseline: `34028b2272c6da763909de14750a4f7281cf4128`  
Purpose: identify the highest-value reliability work **beyond already-planned screen fixes** and register non-duplicate follow-ups for analytics that can be trusted operationally and commercially.

## Executive conclusion

Trendplus already has many strong local reliability mechanisms. The remaining risk is that they are not yet enforced as one end-to-end trust system across every Tier-1 analytics surface.

The next maturity step is not another large wave of isolated KPI fixes. It is to make six properties systematic:

1. **Context identity** — two values are comparable only when period, scope, store/supplier, source generation, population and row-limit semantics match.
2. **Metric authority** — every decision-relevant KPI has a machine-readable source/provenance/denominator/limitation contract, not only methodology copy.
3. **Runtime boundary validation** — malformed or semantically impossible API payloads fail closed before reaching a business screen.
4. **Cross-surface differential proof** — equal contexts reconcile across Dashboard, Product, Supplier, Data Quality and other consumers.
5. **Continuous runtime integrity** — imports, refreshes, cache invalidation and schema drift can invalidate trust and create durable evidence.
6. **Closed-loop value evidence** — recommendation quality is evaluated from executed and measured outcomes without calling association causal impact.

This audit registers `RQ509`–`RQ515` for the first five layers and decision-readiness presentation. The existing Actions/Outcome and Decision Intelligence/RL contracts remain the owner of the sixth layer; they are not duplicated here.

## What is already strong and should be reused

### Shared reliability foundations

- `RQ359` — shared reliable async query lifecycle.
- `RQ360` — reusable analytics reliability contract test kit.
- `RQ361` / `RQ366` — static analytics guardrails with a non-growing reviewed baseline.
- `RQ362` — shared metric provenance vocabulary.
- `RQ363` — Zod/API-boundary response validation on selected critical analytics clients.
- `RQ364` — explicit dataset projections for canonical/table/chart/export/global data.
- `RQ365` — PostgreSQL migration/bootstrap lifecycle smoke.
- `RQ367` — machine-readable validation evidence before Markdown.

These should be extended, not replaced.

### Analytics semantic foundations

- `RQ137` / `RQ141` define period/scope/freshness lineage and the full route lineage matrix.
- `RQ143` keeps decisions/ranking backend-owned.
- `RQ144` protects Data Quality denominator truth.
- `RQ145` established cross-surface presentation parity principles.
- `RQ146` protects schema/migration/refresh failure semantics.
- `RQ147`–`RQ149` define metric evidence tiers, sales/margin basis and inventory economic evidence.
- Recent Operations work (`RQ441`–`RQ504`) significantly hardens sale attribution, signed-return semantics, dataScope, exact cost sources, Shoe Type/Color/Daily/Pre-Nivelacija correctness and related UX.

### Runtime drift/certification foundations

- `RQ413` implements bounded Operations integrity probing for Supplier/Shoe Type.
- `RQ449` persists append-only integrity evidence.
- `RQ450` triggers bounded integrity proof after Access import.
- `RQ451` exposes evidence status.
- `RQ448`, `RQ452`–`RQ455` remain the dedicated Supplier/Shoe Type browser/certificate/CI/production/customer-certification chain.

The new work must generalize these patterns where appropriate, not create a second Supplier/Shoe oracle.

## Confirmed systemic gaps

### F1 — There is no canonical machine-comparable analytics context identity

`RQ141` maps requested/effective/observed period, scope, freshness and cache ownership, but comparable endpoints still do not share one stable context fingerprint that can prove two numbers belong to the same population and source generation.

The planning-only `PROD-AN-11` already identified this need. It should now become an executable reliability contract rather than remain a historical backlog item.

**Risk:** Dashboard, Product, Supplier, Data Quality or reports can each be internally correct yet still disagree because they were computed from different periods, source versions, row caps or refresh generations.

**Owner:** `RQ509`.

### F2 — Metric provenance infrastructure exists but coverage is not mandatory

`AnalyticsMetricProvenanceDto` and `AnalyticsResponseMetaDto.MetricProvenance` exist, and frontend helpers/tests can consume them. A current-main source search did not find broad production endpoint population of that map; the visible usages are primarily the contract/factory/tests plus frontend consumers/fixtures.

`RQ147` defines the richer evidence concept, but there is no current coverage gate proving every Tier-1 KPI actually emits machine-readable authority, unit, denominator and limitation data.

**Risk:** methodology text can be correct while a runtime value lacks enough evidence to establish its decision use.

**Owner:** `RQ510`.

### F3 — Runtime response validation is good but not demonstrably complete for Tier-1 surfaces

`RQ363` added strong Zod boundary validation and current schemas cover several Operations/Supplier/Inventory families. The reviewed code does not establish a simple, enforced manifest proving that every Tier-1 analytics client — including composed decision/report surfaces — passes through an explicit runtime schema.

**Risk:** a new or less-migrated endpoint can bypass the boundary and reintroduce non-finite numbers, impossible counts, malformed dates or contradictory trust metadata.

**Owner:** `RQ511`.

### F4 — The reusable reliability test kit covers only a subset of critical screens

The current `analyticsReliabilityContract.spec.ts` registers adapters for:

- Pre/Post
- Shoe Type
- Color
- Daily Sales
- Inventory

Dashboard, Product Decision Center, canonical Supplier, Executive Decision Board, Data Quality, Actions, Decision Pulse and durable reports are not represented in that shared contract suite.

**Risk:** identical failure classes are prevented on five screens but can regress on another high-value screen.

**Owner:** `RQ512`.

### F5 — Continuous integrity probing is narrow

Trendplus already has a good Operations integrity pattern: bounded probe, registry, durable evidence, startup/cache/import triggers and explicit verified/degraded/drift states. Today that proof is centered on Supplier/Shoe Type.

There is no equivalent general cross-family runtime integrity contract for other high-value families such as canonical sales/dashboard totals, inventory identities, Data Quality denominator consistency or Decision Board contributor composition.

**Risk:** deterministic tests remain green while a changed import, refresh, cache or deployed schema causes a silent production divergence outside Supplier/Shoe Type.

**Owner:** `RQ513`.

### F6 — Production readiness status can become stale while still looking authoritative

`docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md` is dated 2026-06-19 and references an old review HEAD, yet it still contains a broad “Ready with warnings” verdict. September work and current live/runtime gates have changed materially since then.

The repository has machine-readable validation evidence (`RQ367`) but the readiness verdict is not automatically bound to exact current/deployed SHA, evidence age and unresolved runtime gates.

**Risk:** an old PASS/WARN statement survives after code, schema or deployment truth changes.

**Owner:** `RQ514`.

### F7 — Data Quality and “decision readiness” are not the same thing

A general data-quality state can be healthy while a specific decision lacks cost, identity, inventory, comparable-window or freshness evidence. Existing components already expose many of these blockers individually, but there is no uniform screen-level readiness contract saying “this screen is safe for recommendation / signal only / blocked, and why”.

This should **not** become another arbitrary 0–100 score. It should be a backend-owned state derived from existing evidence and blockers.

**Risk:** users can over-trust a locally attractive KPI because the global data-quality status looks acceptable even when the decision-specific evidence is incomplete.

**Owner:** `RQ515`.

## What should NOT become new prompts

The following work already has owners and should be completed through them:

- Color signed-share decision: `RQ501`.
- Color/Shoe Type authority and navigation: `RQ505`, `RQ507`, then docs `RQ508`.
- Supplier cross-tab semantics and readiness: `RQ474`–`RQ476`, `RQ498`–`RQ500`.
- Pre/Post live SQL/view and activity-vs-data-coverage truth: `Q83`, `RQ491`, `RQ140`.
- Actions population/measurement/smoke data: `RQ477`–`RQ479`.
- Decision Pulse partial state/scope/provenance: `RQ480`–`RQ482`.
- Supplier/Shoe Type browser-to-export certification: `RQ448`, `RQ452`–`RQ455`.
- Snapshot rollout parity: `RQ506`.
- Causal recommendation learning/calibration: existing RL/Decision Intelligence contracts; do not infer causal uplift from before/after movement.

## Recommended execution sequence

### Phase A — establish one truth identity

1. `RQ509` — canonical analytics context fingerprint + differential reconciliation.
2. `RQ510` — mandatory metric provenance/evidence coverage for Tier-1 KPIs.

These two provide the identity and semantic contract every later gate can compare.

### Phase B — make regressions difficult to reintroduce

3. `RQ511` — complete Tier-1 API-boundary runtime schema coverage.
4. `RQ512` — extend the shared reliability contract suite to all Tier-1 screens.

These are comparatively cheap and give high regression-prevention value.

### Phase C — prove production data keeps matching the contracts

5. `RQ513` — generalize durable runtime integrity/drift checks beyond Supplier/Shoe Type.
6. `RQ514` — make production-readiness evidence exact-SHA-bound, age-aware and self-expiring.

This is the transition from “tests say it should be right” to “the deployed system keeps proving that it is right”.

### Phase D — maximize decision usefulness

7. `RQ515` — screen-level decision-readiness state and repair guidance.
8. Execute existing `RQ477`–`RQ482` and Supplier readiness tasks when dependencies allow.
9. Use the existing Actions/Outcome + RL/Decision Intelligence lifecycle for recommendation -> execution -> measured outcome -> learning.

Do not expand ML/forecast sophistication before the trust and outcome evidence layers are stable.

## Suggested reliability scorecard for Trendplus itself

Track these as engineering/product reliability indicators, not as customer-facing recommendation scores:

- Tier-1 endpoints with canonical context fingerprint: target 100%.
- Tier-1 displayed decision KPIs with machine-readable metric provenance/evidence: target 100%.
- Tier-1 API clients with runtime schema validation or reviewed documented exception: target 100%.
- Tier-1 screens enrolled in the shared reliability contract suite: target 100%.
- Cross-surface reconciliation: zero unexplained deltas when context fingerprints are equal.
- Live integrity state: no decision surface may show Verified after its evidence expires or a relevant import/refresh/cache/schema generation changes.
- Production readiness: every PASS bound to exact app/deployed/schema/contract versions and a freshness policy.
- Action value: measured-outcome coverage and impact sample size come from the existing outcome owner; missing evidence remains unavailable, never zero.
- Causal claims: none without the existing comparable-control/baseline gate.

## Product principle

The target is not “100% accuracy” as an unsupported blanket claim. The stronger product promise is:

> For every important number, Trendplus can show which population produced it, how it was calculated, how fresh the evidence is, whether the value is directly observed/derived/modelled, whether it is safe for a decision, and whether independent/runtime checks still agree.

That is the practical foundation for analytics users can trust.
