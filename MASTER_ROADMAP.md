# Trendplus Master Roadmap

Updated: 2026-09-22
Repository: `ivanjovicic/Trendplus`
Status: canonical planning entry point

Owner audit 2026-09-22: under the user's direct Daily Sales by Shift screen/backend audit request, `RQ375` returned to `WAITING`, `RQ381` became the current RQ `READY` prompt for signed quantity/revenue semantics, and `RQ382`-`RQ384` were added as `WAITING` scope, shift-provenance and safe-error follow-ups. Daily Sales ASCII Serbian and residual English findings remain routed to `RQ306`/`RQ325`.
Owner audit 2026-09-22: under the user's direct Pre/Post Nivelacija screen/backend audit request, `RQ385` became the primary RQ `READY` prompt for request-scope/cache lineage, while `RQ386` and `RQ387` were added as `WAITING` cohort/denominator and runtime-payload/error-contract follow-ups. `RQ381` remains independently `READY`; Pre/Post ASCII Serbian and residual English/technical copy remain routed to `RQ306`/`RQ325`.
Owner audit 2026-09-22: under the user's direct Prioriteti nivelacije and Prodaja po boji artikla screen/backend audit request, `RQ388` became the primary Pre-Nivelacija `READY` prompt for global KPI/page-population parity and `RQ389` became the primary Color `READY` prompt for store/data-origin event lineage. `RQ390`-`RQ391` and `RQ392`-`RQ395` were added as `WAITING` scoring-window, runtime-schema, signed-numeric, weighted-margin, comparable-cohort and safe-error follow-ups. Shared Serbian/English copy remains routed to `RQ306`/`RQ325`.
Follow-up audit 2026-09-22: the Color review also added `RQ396`-`RQ400` as `WAITING` cache/freshness, generic-detail trust, identity, source-provenance and authoritative decision-score follow-ups. `RQ389`, `RQ390` and `RQ391` are DONE on current `main`; `RQ392` is now the primary RQ `READY` lane and the remaining follow-ups stay WAITING.
Owner completion 2026-09-22: `RQ390` was delivered directly to `main` in `c84a05fa838784db56e9ad78daf72d65cd804381`; its bounded UTC scoring window, signed-sales evidence, fail-closed denominators and cache/meta provenance are verified. `RQ391` was promoted to `READY` after its runtime-validation dependencies were verified DONE.
Owner completion 2026-09-22: `RQ391` was delivered directly to `main` in `d54da8aa77267faf2ad48878a1be9ee0934dd8a1`; complete Pre-Nivelacija decision payload validation and safe error handling are verified. `RQ392` was promoted to `READY` after its `RQ381` and `RQ288` dependencies were verified DONE.
Owner claim 2026-09-22: `RQ392` transitioned `READY -> IN_PROGRESS` in this workspace after dependency and collision checks; its implementation lock is local and not committed.
Owner completion 2026-09-22: `RQ392` was delivered directly to `main` in `7389d4b1896473e26c1fbf1abcb86e2fe89aa8ba`; signed Color amounts, nullable degraded denominators and fail-closed recommendations are verified. `RQ393` was then delivered directly to `main` in `3b635fe813e0e55f843236d55025c52c030e853d`; weighted Color margin baseline, nullable unknown-share evidence and separated cost-source coverage are verified. No dependency-complete current READY lane is declared in this queue. Run logs: `.ai/runs/2026-09-22-RQ392-evidence.md`, `.ai/runs/2026-09-22-direct-last-commits-audit-evidence.md`.
Owner audit 2026-09-22: under the user's direct Supplier Decision Hub screen/backend audit request, `RQ401` became the Supplier Decision `READY` prompt for cache-schema/effective-period compatibility, while `RQ402`-`RQ405` were added as `WAITING` detail, filter-parity, effective-period and localization follow-ups. Existing independent RQ READY lanes remain unchanged.
Owner completion 2026-09-22: `RQ401` was delivered directly to `main` in `da55fc1625d7f5db58fea56806f1e9613f57cb77` with per-window cache capability gates, required projection-column validation, conservative post-signal coverage handling, all-time evidence-column parity and accurate unavailable effective-period error reporting. `RQ402`/`RQ404` remain `WAITING` on this contract.
Follow-up audit correction 2026-09-22: RQ401 now also proves the required `vw_supplier_ml_latest_predictions` projection columns before enabling optional ML enrichment; incomplete view schema fails closed to the non-ML projection. Runtime commit: `07760b749b5b6c2b4d26aff78f7137d6c85becf9`; run log: `.ai/runs/2026-09-22-direct-other-latest-commits-audit-evidence.md`.
Owner promotion 2026-09-22: after `RQ401` reached DONE and its follow-up correction was delivered, `RQ402` moved `WAITING -> READY` as the next dependency-complete Supplier Decision detail-source prompt; `RQ403`-`RQ405` remain WAITING.
Owner completion 2026-09-22: `RQ402` was delivered directly to `main` in `99617cacb0b26ab52fcf39c52ae71bc5ea9a8ad2`; canonical Supplier Decision rich details, active-filter parity, trust/meta provenance and stale-response protection are now live. Run log: `.ai/runs/2026-09-22-RQ402-evidence.md`. `RQ403` was then delivered directly to `main` in `f87dd49c0ef7a43051178111510d9d5b190f43bd`; canonical filter URL state, controls, API/cache/detail/action/report parity and invalid-filter clearing are verified. Run log: `.ai/runs/2026-09-22-RQ403-evidence.md`. `RQ404`-`RQ405` remain WAITING.
Routing correction 2026-09-22: `RQ132` is now `BLOCKED` because its declared `STAB16` dependency remains BLOCKED; its previous unassigned `IN_PROGRESS` state had no active lock.
Owner promotion 2026-09-22: `RQ403` moved `WAITING -> READY` after `RQ401`/`RQ402` completion and collision checks; it is now the current RQ primary pointer.
Owner completion 2026-09-22: `RQ403` is DONE on current `main`; no next prompt was promoted in the same run. `RQ404`-`RQ405` remain WAITING behind their declared sequencing and owner boundaries.
Owner promotion 2026-09-22: after `RQ403` reached DONE, `RQ404` became the current RQ READY prompt because its declared dependency on `RQ401` and coordination with `RQ402` are satisfied; `RQ405` remains WAITING in the same exclusive Supplier Decision feature family.
Owner completion 2026-09-22: `RQ404` was delivered directly to `main` in `015e394d`; requested/effective/observed Supplier Decision period lineage is explicit across screen, details, reports/exports and action rationale, and backend response metadata preserves the same contract. Run log: `.ai/runs/2026-09-22-RQ404-evidence.md`.
Owner promotion 2026-09-22: after `RQ404` reached DONE and the declared `RQ405` dependency was satisfied, `RQ405` moved `WAITING -> READY` as the next dependency-complete Supplier Decision localization prompt; collision checks are clear.
Owner claim 2026-09-22: `RQ405` transitioned `READY -> IN_PROGRESS` in this workspace after refresh, dependency and collision checks; local runtime lock `.ai/task-locks/RQ405-codex.lock.md`.
Owner completion 2026-09-22: `RQ405` was delivered directly to `main` in `9bf8af87c11c0b8feb0efcd0f632d10d27f819ac`; Supplier Decision user-facing screen/report/export/error/empty/degraded terminology is Serbian and diacritics-safe, while contracts, reason codes and technical identifiers remain compatible. Run log: `.ai/runs/2026-09-22-RQ405-evidence.md`. No next prompt was promoted in this run.
Owner promotion 2026-09-22: after `RQ405` reached DONE, `RQ375` became the current RQ READY prompt as the first dependency-complete P1 Shoe Type aggregate/cost-quality contract; `RQ376`-`RQ377` remain sequenced behind its metric semantics.
Owner claim 2026-09-22: `RQ385` transitioned `READY -> IN_PROGRESS` in this workspace after refresh, dependency and collision checks; its implementation lock is local and not committed.
Owner completion 2026-09-22: `RQ385` delivered directly to `main` with implementation `bb6158f003bfd535a3211430970b30abbc761644`; current `origin/main` contains this SHA and the synchronized closure commits. Scoped Pre/Post fact queries, scope-isolated main/options caches, backend provenance and frontend fail-closed validation are delivered. Run log: `.ai/runs/2026-09-22-RQ385-evidence.md`.
Owner promotion 2026-09-22: after the user's explicit Daily Sales signed-contract fix request, `RQ381` transitioned `READY -> IN_PROGRESS` and was claimed in this workspace; its current pointer remains the RQ default while implementation and proof are in progress.
Owner completion 2026-09-22: `RQ381` was delivered on `main` with signed Daily Sales quantity/revenue contract alignment, negative supplier/`Ostali` reconciliation, net-zero evidence preservation and focused frontend proof; `RQ385` remains the current RQ READY prompt.
Owner promotion 2026-09-20: `RQ333` was explicitly promoted after completed `RQ332`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-20: `RQ333` was delivered on `main` with backend-only Pre/Post post revenue share projection; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-20: `RQ334` was explicitly promoted after completed `RQ333`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-20: `RQ334` was delivered on `main` with fail-closed supplier pre/post comparable article count presentation; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-20: `RQ335` was explicitly promoted after completed `RQ334`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-20: `RQ335` was delivered on `main` with visible Daily Sales previous-period failure and empty-baseline states; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-20: `RQ336` was explicitly promoted after completed `RQ335`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-20: `RQ336` was delivered on `main` with chronological Daily Sales trend/shift chart ordering independent of table sort; the RQ queue returned to no current READY prompt.
Planning intake 2026-09-20: the user requested a systemic Reliability Contract Layer; `RQ359`-`RQ367` were added to the analytics reliability queue for shared async lifecycle, invariant tests, guardrails, provenance, runtime validation, dataset projections, migration smoke, baseline debt and generated evidence.
Owner promotion 2026-09-20: under the user's instruction to claim the next prompt, `RQ359` transitioned `WAITING -> READY -> IN_PROGRESS` and was claimed in this workspace.
Owner completion 2026-09-20: `RQ359` delivered the shared reliable-query lifecycle hook and five analytics-page migrations; the queue returned to no current READY prompt.
Owner promotion 2026-09-20: after `RQ359` reached DONE, `RQ360` transitioned `WAITING -> READY -> IN_PROGRESS` and was claimed in this workspace.
Owner completion 2026-09-20: `RQ360` delivered the reusable analytics reliability contract test kit with five initial adopter adapters; the queue returned to no current READY prompt.
Owner promotion 2026-09-20: after `RQ360` reached DONE, `RQ366` transitioned `WAITING -> READY -> IN_PROGRESS` and was claimed in this workspace.
Owner completion 2026-09-20: `RQ366` delivered the non-growing analytics guardrail baseline with explicit baseline-only output and new-violation failure; the queue returned to no current READY prompt.
Owner promotion 2026-09-20: after `RQ366` reached DONE, `RQ361` transitioned `WAITING -> READY -> IN_PROGRESS` and was claimed in this workspace.
Owner completion 2026-09-20: `RQ361` delivered expanded anti-pattern detection and reviewed baseline coverage; the queue returned to no current READY prompt.
Owner promotion 2026-09-20: after `RQ361` reached DONE, `RQ362` transitioned `WAITING -> READY -> IN_PROGRESS` and was claimed in this workspace.
Owner completion 2026-09-20: `RQ362` delivered the backward-compatible authoritative-versus-derived metric provenance contract and fail-closed frontend mapping; the queue returned to no current READY prompt.
Owner promotion 2026-09-20: after `RQ362` reached DONE, `RQ363` transitioned `WAITING -> READY -> IN_PROGRESS` and was claimed in this workspace.
Owner completion 2026-09-20: `RQ363` delivered fail-closed frontend API-boundary validation for the selected critical analytics response surfaces; the queue returned to no current READY prompt.
Owner promotion 2026-09-20: after `RQ363` reached DONE, `RQ364` transitioned `WAITING -> READY -> IN_PROGRESS` and was claimed in this workspace.
Owner completion 2026-09-20: `RQ364` delivered typed dataset projections for Daily Sales and Pre-Nivelacija, preserving chart/table/export/detail/page versus global semantics; the queue returned to no current READY prompt.
Owner promotion 2026-09-20: after `RQ364` reached DONE, `RQ365` transitioned `WAITING -> READY -> IN_PROGRESS` and was claimed in this workspace.
Owner completion 2026-09-21: `RQ365` delivered the PostgreSQL migration/bootstrap lifecycle smoke, pgvector fixture dependency, serialized extension creation and fresh/repeat/restart assertions; CI remains residual risk by explicit user instruction not to wait.
Owner promotion 2026-09-21: after `RQ365` reached DONE, `RQ367` transitioned `WAITING -> READY -> IN_PROGRESS` and was claimed in this workspace.
Owner completion 2026-09-21: `RQ367` delivered machine-readable validation evidence generation, truthful Markdown rendering and exact-tip/ancestor `origin/main` verification; the RQ reliability sequence is complete.
Owner promotion 2026-09-21: under the user's explicit instruction to claim the next prompt, `RQ368` transitioned `WAITING -> READY -> IN_PROGRESS` as the first safe P1 Operacije inline-error-safety follow-up; the RQ queue is the canonical execution owner.
Owner completion 2026-09-21: `RQ368` delivered safe Pre/Post inline partial-failure error mapping and provider-exception regression coverage; the queue returned to no current READY prompt.
Owner promotion 2026-09-21: under the user's explicit instruction to claim the next prompt, `RQ369` transitioned `WAITING -> READY -> IN_PROGRESS` as the Inventory inline-error-safety follow-up; the RQ queue is the canonical execution owner.
Owner completion 2026-09-21: `RQ369` delivered safe Inventory inline error projections and focused component/page regressions; the queue returned to no current READY prompt.
Owner promotion 2026-09-21: under the user's explicit instruction to claim the next prompt, `RQ370` transitioned `WAITING -> READY -> IN_PROGRESS` as the Inventory secondary/detail request-cancellation follow-up; the RQ queue is the canonical execution owner.
Owner completion 2026-09-21: `RQ370` delivered shared lifecycle and per-effect AbortSignal propagation for Inventory secondary/detail reads with focused cancellation regressions; the queue returned to no current READY prompt.
Owner follow-up correction 2026-09-21: a post-delivery audit found and fixed the missing Inventory data-scope dependency on both size-curve effects, with a regression test proving scope changes abort the active request.
Owner promotion 2026-09-22: under the user's direct Inventory audit request, `RQ308` transitioned `WAITING -> READY` as the current Inventory period-selection and snapshot-provenance prompt; `RQ371` and `RQ372` were added as later `WAITING` follow-ups.
Owner promotion 2026-09-22: under the user's direct Sales by Supplier audit request, `RQ308` returned to `WAITING`, `RQ373` transitioned `WAITING -> READY` as the current Supplier Sales visible-scope/KPI parity prompt, and `RQ374` was added as a later `WAITING` detail trust-contract follow-up; `RQ306`/`RQ325` were narrowed/referenced for Supplier Sales localization without creating duplicate prompts.
Owner promotion 2026-09-22: under the user's direct Shoe Type Sales screen/backend audit request, `RQ373` returned to `WAITING`, `RQ375` transitioned `WAITING -> READY` as the current Shoe Type weighted-margin/cost-quality contract prompt, and `RQ376`/`RQ377` were added as later `WAITING` pre/post aggregate and detail trust-contract follow-ups; Shoe Type localization remains routed to `RQ306`/`RQ325`, with dead truncation behavior retained in `RQ329`.
Owner audit 2026-09-22: combined Supplier + Shoe Type value/correctness audit strengthened `RQ373`-`RQ377` and added `RQ378`-`RQ380`. The audit separates display population from decision reference cohort, replaces arithmetic-mean margin benchmarks with covered-revenue-weighted truth, requires runtime validation for decision payloads, and aligns total pre/post metrics to comparable cohorts. Waiting states now reflect dependencies/path ownership rather than a one-READY invariant.
Owner promotion 2026-09-21: under the user's explicit instruction to claim the next prompt, `RQ301` transitioned `WAITING -> READY -> IN_PROGRESS` as the next P1 Operacije Inventory localization slice; the RQ queue is the canonical execution owner.
Owner completion 2026-09-21: `RQ301` delivered Serbian product copy across the Inventory decision surface and child panels with 22 focused files / 85 passing tests; the queue returned to no current READY prompt.
Owner promotion 2026-09-21: under the user's explicit instruction to claim the next prompt, `RQ302` transitioned `WAITING -> READY -> IN_PROGRESS` as the next P1 Operacije route-smoke coverage slice; the RQ queue is the canonical execution owner.
Owner completion 2026-09-21: `RQ302` delivered smoke coverage for all eight Operacije route targets and supplier legacy redirects with 20 passing route tests; the queue returned to no current READY prompt.
Owner promotion 2026-09-21: under the user's explicit instruction to claim the next prompt, `RQ303` transitioned `WAITING -> READY -> IN_PROGRESS` as the next P1 Daily Sales localization slice; the RQ queue is the canonical execution owner.
Owner completion 2026-09-21: `RQ303` delivered Serbian Daily Sales mismatch indicators and localized reconciliation copy with focused regression coverage; the queue returned to no current READY prompt.
Owner promotion 2026-09-08: `RQ203` was explicitly promoted after completed `RQ202`; the RQ queue is the canonical execution owner.
Owner promotion 2026-09-08: `RQ204` was explicitly promoted after completed `RQ203`; the RQ queue is the canonical execution owner.
Owner promotion 2026-09-08: `RQ205` was explicitly promoted after completed `RQ204`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-08: `RQ205` was delivered on `main` as the frontend cache-invalidation follow-up; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-08: `RQ206` was explicitly promoted after completed `RQ205`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-08: `RQ206` was delivered on `main` as the refresh-status accuracy follow-up; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-08: `RQ207` was explicitly promoted after completed `RQ206`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-08: `RQ207` was delivered on `main` as the failed-refresh cache-safety follow-up; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-08: `RQ208` was explicitly promoted after completed `RQ207`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-08: `RQ208` was delivered on `main` as the Dashboard UTC period-divisor follow-up; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-08: `RQ209` was explicitly promoted after completed `RQ208`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-08: `RQ209` was delivered on `main` as the single-owner migration orchestration correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-08: `RQ210` was explicitly promoted after completed `RQ209`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-08: `RQ210` was delivered on `main` as the fail-closed startup advisory-lock timeout correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-08: `RQ211` was explicitly promoted after completed `RQ210`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-08: `RQ211` was delivered on `main` as the ordered critical startup migration correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-08: `RQ212` was explicitly promoted after completed `RQ211`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-08: `RQ212` was delivered on `main` as the fail-closed EF migration failure correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-09: `RQ213` was explicitly promoted after completed `RQ212`, transitioned to `READY` and claimed as `IN_PROGRESS`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-09: `RQ213` was delivered on `main` as the fail-closed sales-facts rollback correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-09: `RQ214` was explicitly promoted after completed `RQ213`, transitioned to `READY` and claimed as `IN_PROGRESS`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-09: `RQ214` was delivered on `main` as the seed-sales stock reconciliation correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-09: `RQ215` was explicitly promoted after completed `RQ214`, transitioned to `READY` and claimed as `IN_PROGRESS`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-09: `RQ215` was delivered on `main` as the atomic aggregate replacement correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-09: `RQ216` was explicitly promoted after completed `RQ215`, transitioned to `READY` and claimed as `IN_PROGRESS`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-09: `RQ216` was delivered on `main` as the aggregate-refresh cache-safety correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-09: `RQ217` was explicitly promoted after completed `RQ216`, transitioned to `READY` and claimed as `IN_PROGRESS`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-09: `RQ217` was delivered on `main` as the outbox row-locking and sales-line idempotency correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-09: `RQ218` was explicitly promoted after completed `RQ217`, transitioned to `READY` and claimed as `IN_PROGRESS`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-09: `RQ218` was delivered on `main` as the Access import analytics rollback-boundary correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-09: `RQ219` was explicitly promoted after completed `RQ218`, transitioned to `READY` and claimed as `IN_PROGRESS`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-09: `RQ219` was delivered on `main` as the fail-fast background-service host policy correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-10: `RQ220` was explicitly promoted after completed `RQ219`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-10: `RQ220` was delivered on `main` as the outbox dead-letter observability correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-10: `RQ221` was explicitly promoted after completed `RQ220`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-10: `RQ221` was delivered on `main` as the Insight Studio error-response sanitization correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-10: `RQ222` was explicitly promoted after completed `RQ221`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-10: `RQ222` was delivered on `main` as the dimensional aggregate orphan-line reconciliation correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-10: `RQ225` was explicitly promoted after completed `RQ224`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner promotion 2026-09-10: `RQ226` was explicitly promoted after completed `RQ225`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-10: `RQ226` was delivered on `main` with startup validation and explicit UTC schedule logging; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-10: `RQ227` was explicitly promoted after completed `RQ226`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-10: `RQ227` was delivered on `main` as the fail-closed deleted-row archive correction; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-10: `RQ228` was explicitly promoted after completed `RQ227`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-10: `RQ228` was delivered on `main` with shared Insight Studio v1/v2 period normalization and focused contract coverage; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-10: `RQ233` was explicitly promoted after completed `RQ228`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-10: `RQ233` was delivered on `main` with visible-scope supplier concentration denominators and focused UI regression coverage; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-11: `RQ234` was explicitly promoted after completed `RQ233`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-11: `RQ234` was delivered on `main` with canonical supplier-report filter serialization, fail-closed invalid-link handling and backend stable URL/payload parity; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-11: `RQ235` was explicitly promoted after completed `RQ234`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-11: `RQ235` was delivered on `main` with a backend-owned supplier report actionability gate across concrete negotiation actions; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-11: `RQ236` was explicitly promoted after completed `RQ235`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-11: `RQ236` was delivered on `main` with explicit supplier-report numeric availability states and fail-closed optional metric handling; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-11: `RQ237` was explicitly promoted after completed `RQ236`, repaired a stale same-owner dependency on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-11: `RQ237` was delivered on `main` with conservative inventory composite trust aggregation, explicit degraded-source lineage and confirmed-refresh handling; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-11: `RQ241` was explicitly promoted after completed `RQ237`, repaired a stale same-owner dependency on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-11: `RQ241` was delivered on `main` with fail-closed Dashboard custom-date validation, trusted-response preservation and focused request-gating coverage; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-11: `RQ242` was explicitly promoted after completed `RQ241`, repaired a stale same-owner dependency on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-11: `RQ242` was delivered on `main` with fail-closed Daily Sales supplier concentration reconciliation and focused contradictory-total coverage; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-11: `RQ238` was explicitly promoted after completed `RQ242`, repaired a stale same-owner dependency on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-11: `RQ238` was delivered on `main` with explicit Shoe Type coverage denominator semantics, unavailable-state protection and focused projection coverage; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-11: `RQ239` was explicitly promoted after completed `RQ238`, repaired a stale same-owner dependency on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS` and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-11: `RQ239` was delivered on `main` with fail-closed Executive fallback provenance, explicit generated/refresh timestamp precedence and focused fallback-card coverage; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-14: `RQ240` was explicitly promoted after completed `RQ239`, repaired the stale same-owner dependency note on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS`, and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ240` was delivered on `main` with shared finite/nullable inventory ratio projection and unavailable-state protection; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-14: `RQ243` was explicitly promoted after completed `RQ240`, repaired the stale same-owner dependency note on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS`, and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ243` was delivered on `main` with nullable supplier-footwear data-quality evidence, finite/complete frontend projection and fallback trust/export protection; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-14: `RQ244` was explicitly promoted after completed `RQ243`, repaired the stale same-owner dependency note on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS`, and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ244` was delivered on `main` with fail-closed Analytics Actions outcome-evidence classification, timestamp-only protection and finite measured-impact handling; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-14: `RQ245` was explicitly promoted after completed `RQ244`, repaired the stale same-owner dependency note on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS`, and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ245` was delivered on `main` with safe unknown Analytics Actions metadata labels across table/detail surfaces; the RQ queue returned to no current READY prompt.
Owner promotion 2026-09-14: `RQ246` was explicitly promoted after completed `RQ245`, repaired the stale same-owner dependency note on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS`, and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ246` was delivered on `main` with fail-closed legacy Pilot Intake preview period/provenance mapping and focused unknown/explicit metadata coverage; the queue returned to no current READY prompt.
Owner promotion 2026-09-14: `RQ247` was explicitly promoted after completed `RQ246`, repaired the stale same-owner dependency note on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS`, and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ247` was delivered on `main` with shared safe Serbian Pilot Intake readiness/import status and scope labels across card, report, copied summary and exports; the queue returned to no current READY prompt.
Owner promotion 2026-09-14: `RQ248` was explicitly promoted after completed `RQ247`, repaired the stale same-owner dependency note on already-DONE `RQ167`, transitioned `WAITING -> READY -> IN_PROGRESS`, and claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ248` was delivered on `main` with shared finite/null Pilot Intake impact projection, denominator-backed true-zero handling and cross-surface parity; the RQ queue advanced to `RQ249`.
Owner promotion 2026-09-14: `RQ249` was explicitly promoted after completed `RQ248`, repaired the stale same-owner dependency note on already-DONE `RQ169`, and transitioned `WAITING -> READY`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ249` was delivered on `main` with fail-closed Supplier Decision Hub detail and report-toolbar actionability gates; blocked, fallback, stale/partial and missing recommendation permission remain review-only.
Owner promotion 2026-09-14: `RQ250` was explicitly promoted after completed `RQ249`, repaired the stale dependency note on already-DONE `RQ169`, and transitioned `WAITING -> READY`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ250` was delivered on `main` with full-price-weighted Supplier Decision Hub/client-report/server-report margin contribution parity and fail-closed missing, partial and non-finite evidence handling; the RQ queue advanced to `RQ251`.
Owner promotion 2026-09-14: `RQ251` was explicitly promoted after completed `RQ250`, repaired its stale dependency note on already-DONE `RQ169`, and transitioned `WAITING -> READY`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ251` was delivered on `main` with shared safe Serbian labels for Inventory workflow action type/status/priority and scheduler frequency/format/run status across workflow, mail-scheduler and export-scheduler surfaces; the RQ queue advanced to `RQ252`.
Owner promotion 2026-09-14: `RQ252` was explicitly promoted after completed `RQ251`, repaired its stale dependency note on already-DONE `RQ169`, and transitioned `WAITING -> READY`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ252` was delivered on `main` with a shared fail-closed supplier-report recommendation gate across report actions, metadata, negotiation-pack rows, legacy rows and export payloads; the RQ queue advanced to `RQ253`.
Owner promotion 2026-09-14: `RQ253` was explicitly promoted after completed `RQ252`, repaired its stale dependency note on already-DONE `RQ169`, and transitioned `WAITING -> READY`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ253` was delivered on `main` with shared safe analytics error messaging; raw error codes and technical exception text are suppressed in the shared alert while Serbian guidance, correlation support metadata and error/empty/retry semantics remain intact.
Owner promotion 2026-09-14: `RQ254` was explicitly promoted after completed `RQ253`, repaired its stale dependency note on already-DONE `RQ169`, and transitioned `WAITING -> READY`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-14: `RQ254` was delivered on `main` with separate Product Decision Center generation and source-refresh provenance; direct and empty PDC responses keep unknown refresh nullable and Decision Board no longer treats query generation as source freshness.
Owner completion 2026-09-15: `RQ255` was delivered on `main` with nullable Product Decision Center stock evidence and fail-closed stock-dependent recommendation projections; the RQ queue advanced to `RQ256`.
Owner promotion 2026-09-15: `RQ256` was explicitly promoted after completed `RQ255`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ257`-`RQ262` were delivered as bounded analytics frontend correctness slices covering finite/null supplier values, shared trust/empty/refresh metadata, and executive KPI value-tone parity; the RQ queue advanced to `RQ263`.
Owner promotion 2026-09-15: `RQ263` was explicitly promoted after completed `RQ262`, repairing its stale same-owner dependency on already-DONE `RQ169`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ263` was delivered on `main` with explicit export/preview operation feedback, valid artifact and popup validation, safe failure messaging and retry-preserving incomplete states; the RQ queue advanced to `RQ264`.
Owner promotion 2026-09-15: `RQ264` was explicitly promoted after completed `RQ263`, repairing its stale same-owner dependency on already-DONE `RQ169`; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ264` was delivered on `main` with one shared finite/null normalization and display projection for analytics payloads, detail snapshots and generic print; measured finite zero remains visible and non-finite numeric evidence fails closed. The RQ queue advanced to `RQ270`.
Owner promotion 2026-09-15: `RQ270` was explicitly promoted after completed `RQ264`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ270` was delivered on PR #6 with unified page reload on global data-scope change; InventoryPage now listens for `trendplus:data-scope-changed` events and reloads all primary and signal panels as one coherent generation while preserving request-sequence guards and detail state per RQ203. The RQ queue advanced to `RQ271`.
Owner promotion 2026-09-15: `RQ271` was explicitly promoted after completed `RQ270`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ271` was delivered on PR #7 with confirmed whole-scope KPI contract and explicit scope documentation; all five balance KPI cards now clarify they represent whole-inventory totals not filtered by SKU search. The RQ queue advanced to `RQ272`.
Owner promotion 2026-09-15: `RQ272` was explicitly promoted after completed `RQ271`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ272` was delivered on PR #8 with removed rows fallback; total inventory value now uses only authoritative backend value, preventing false partial sums during pagination. The RQ queue advanced to `RQ273`.
Owner promotion 2026-09-15: `RQ273` was explicitly promoted after completed `RQ272`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ273` was delivered with explicit export/preview dataScope parity and documented current-stock snapshot semantics. The RQ queue advanced to `RQ274`.
Owner promotion 2026-09-15: `RQ274` was explicitly promoted after completed `RQ273`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ274` was delivered with nullable forecast restock `daysSinceMovement`, defer-while-detail-loading behavior and focused regression tests; the RQ queue advanced to `RQ275`.
Owner promotion 2026-09-15: `RQ275` was explicitly promoted after completed `RQ274`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ275` was delivered with inventory queue marker reset when source keys disappear; the RQ queue advanced to `RQ276`.
Owner promotion 2026-09-15: `RQ276` was explicitly promoted after completed `RQ275`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ276` was delivered with inventory exposure separated from central-queue expected impact; the RQ queue advanced to `RQ277`.
Owner promotion 2026-09-15: `RQ277` was explicitly promoted after completed `RQ276`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ277` was delivered with Supplier canonical/embedded composition deduplication and focused composition tests; the RQ queue advanced to `RQ278`.
Owner promotion 2026-09-15: `RQ278` was explicitly promoted after completed `RQ277`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ278` was delivered with explicit supplier-filter `dataScope` propagation and invalid-selection clearing; the RQ queue advanced to `RQ279`.
Owner promotion 2026-09-15: `RQ279` was explicitly promoted after completed `RQ278`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ279` was delivered with visibly stale supplier filter fallback handling and blocked selection; the RQ queue advanced to `RQ280`.
Owner promotion 2026-09-15: `RQ280` was explicitly promoted after completed `RQ279`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ280` was delivered with visible supplier previous-period comparison degradation and non-fabricated PoP growth; the RQ queue advanced to `RQ281`.
Owner promotion 2026-09-15: `RQ281` was explicitly promoted after completed `RQ280`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ281` was delivered with aligned embedded supplier freshness provenance and standalone/embedded trust parity; the RQ queue advanced to `RQ282`.
Owner promotion 2026-09-15: `RQ282` was explicitly promoted after completed `RQ281`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-15: `RQ282` was delivered with collision-safe null-ID supplier vendor keys and distinct detail snapshot IDs; the RQ queue has no current READY prompt.
Owner promotion 2026-09-16: `RQ283` was explicitly promoted after completed `RQ282`, transitioned `WAITING -> READY -> IN_PROGRESS`, and was claimed in this workspace; the RQ queue is the canonical execution owner.
Owner completion 2026-09-16: `RQ283` was delivered on `main` with explicit finite/zero/negative Shoe Type margin comparison states; the RQ queue advanced to `RQ284`.
Owner completion 2026-09-16: `RQ284` was delivered on `main` with preserved gated Shoe Type backend status identity and actionability cues; the RQ queue advanced to `RQ285`.
Owner completion 2026-09-16: `RQ285` was delivered on `main` with fail-closed shoe-type share/coverage percent validation and consolidated margin/status utilities; the RQ queue has no current READY prompt.
Owner completion 2026-09-16: `RQ286` was delivered with independent Color pre/post detail metric availability; the RQ queue has no current READY prompt.
Owner completion 2026-09-10: `RQ225` was delivered on `main` with stable process-lifetime snapshot-cost option consumption; the RQ queue returned to no current READY prompt.
Owner completion 2026-09-10: `RQ224` was delivered on `main` with fail-closed analytics DB connection resolution; the RQ queue returned to no current READY prompt.
Owner completion 2026-09-10: `RQ223` was delivered on `main` as the fail-closed invalid-foreign-key default correction; the RQ queue returned to no current READY prompt.

This file is the single routing entry point for Trendplus planning. It does not replace detailed roadmaps, audits, queues, or historical evidence. It tells an agent which program owns a topic, what is currently runnable, what is blocked, what may run in parallel, and what milestone comes next.

## Canonical planning rule

1. Read `AGENTS.md` and `.github/copilot-instructions.md`.
2. Read `docs/ai/AGENT_START_HERE.md`.
3. Read this file.
4. Follow the owner queue named here.
5. Read only the target prompt plus its `Read first` documents.

If an older queue addendum, audit, status report, or completion note conflicts with this file and the current owner queue header, treat the older statement as historical evidence, not current routing.

### Parallel READY semantics

`Current READY` is the **primary/default READY** pointer used by a simple `next` selector. It is not an exclusive allowlist. A program may expose additional READY tasks when they are dependency-complete and collision-safe. Multiple active tasks in the same feature family require `Parallel-safe: yes` on every active task in that family; different feature families still must pass path, owner, lock and release-gate checks before concurrent claims.

## Existing program priority

The existing execution priority is preserved:

1. Backend CI Repair (`BCI`)
2. Stabilization / Release / Security (`STAB`)
3. Analytics Reliability (`RQ`)
4. remaining STAB work
5. remaining analytics correctness
6. Data Connector (`QDB`)
7. Multi-Tenant (`MT`)
8. GenAI (`GAI`)

The existing Premium UI program (`P-UI`) is a supplemental presentation lane. Its current task may run only when path-safe and must never displace the priority chain above or repair analytics correctness through frontend invention.

A historical task ID does not become READY merely because it appears in this priority list. Always use the current queue status. If a higher-priority program has no READY task, do not invent one; use its documented blocker or owner-gated promotion rule.

The new DEX/RL/DT/PERF/OBS/SEC programs are future planning lanes. Their first READY prompts are planning/contract tasks only. They do not authorize lower-priority runtime implementation ahead of the existing priority chain.

## Program routing matrix

Owner promotion 2026-09-08: RQ199 was explicitly promoted and completed for Pre-nivelacija DataScope filtering and cache isolation; the RQ queue remains the canonical execution owner.
Owner promotion 2026-09-08: RQ200 was explicitly promoted and completed for backend-first Product Decision Center search before the top-row limit; the RQ queue remains the canonical execution owner.
Owner promotion 2026-09-08: RQ201 was explicitly promoted for Daily Sales chart/table sort parity; the RQ queue remains the canonical execution owner.
Owner promotion 2026-09-08: RQ201 was explicitly promoted and completed for Daily Sales chart/table sort parity; the RQ queue remains the canonical execution owner.
Owner promotion 2026-09-08: RQ202 was explicitly promoted and completed for timezone-safe Daily Sales date sorting; the RQ queue remains the canonical execution owner.

| Program | Owner queue / roadmap | Current READY | Blocked by / current truth | Parallel-safe planning | Next milestone |
|---|---|---|---|---|---|
| BCI | `MASTER_ROADMAP.md` / `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md` + `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md` | none | Historical `BCI01`/`BCI05`/`BCI06` remain DONE, and `BCI10` is DONE after re-closing the backend suite with the test-host checkpoint-sync registration fix. | No | queue complete unless a new red current-main run appears |
| STAB | `MASTER_ROADMAP.md` / `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` | none | A same-day 2026-08-27 API-only recheck confirmed the canonical Render runtime SHA `6ecbfa67a7304c3cbeeb71755a35255e766c8e24` is contained in current `main`, but refresh workers are still unregistered and direct read-only reconciliation/browser proof is still missing. Current operator evidence also reports Render web SHA `d38aafd405a9213a279bb76664cde4bf69ddf83b`, no worker service, and Neon storage `0.54/0.5 GB`. `STAB16` remains BLOCKED on provider worker access, read-only audit connection and Neon storage capacity; GenAI remains BLOCKED by the core-pilot/release gate. | No | storage triage, then restore worker/reconciliation proof through STAB16 |
| RQ | `MASTER_ROADMAP.md` / `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md` + `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` + active addenda | RQ375 | `RQ381` is DONE on current `main` for the Daily Sales signed-quantity/revenue contract. `RQ385` is DONE on current `main` for Pre/Post request-scope/cache lineage; `RQ386`-`RQ387` are later Pre/Post cohort/denominator and runtime-payload/error-contract follow-ups. `RQ388`, `RQ389`, `RQ390`, `RQ391`, `RQ392` and `RQ393` are DONE on current `main` for Pre-Nivelacija population, Color scope/event lineage, Pre-Nivelacija scoring-window/runtime-schema and Color signed-sales/cost-quality/weighted-margin contracts; `RQ394`-`RQ400` remain WAITING Color follow-ups. `RQ401`, `RQ402`, `RQ403`, `RQ404` and `RQ405` are DONE on current `main` for Supplier Decision cache/effective-period, rich detail-source, filter parity, requested/effective/observed period contracts and Serbian localization. `RQ375` is READY for the Shoe Type weighted-margin/cost-quality contract; `RQ376`-`RQ377` remain sequenced behind it, `RQ378`-`RQ380` behind Supplier Sales ownership dependencies, and `RQ132` is BLOCKED behind STAB16. Daily Sales, Pre/Post, Pre-Nivelacija, Color and Supplier Decision localization remains routed to `RQ306`/`RQ325`; `RQ327` remains the URL-sort follow-up. RQ365 CI residual risk and RQ367 skipped dotnet proof are recorded in their evidence. `RQ139`/`RQ140` remain PARTIAL on live STAB16 proof. Keep `RQ141`/`RQ145`/`RQ146` and live gates behind dependencies/STAB16. | Selected docs/tests only | execute RQ375, then promote the next safe prompt |
| P-UI | `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` + least-improved addendum + `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md` | none | P-UI-21 DONE. P-UI-22 DONE. Queue complete. | Yes | queue complete |
| QDB | `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` + `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md` | none (`QDB09` is DONE after the 2026-08-22 delivery) | `QDB01`-`QDB06` DONE. Durable checkpoints are `ConnectionId + MappingProfileId + SourceStream` with `TenantScope=n/a_dedicated`, and SQL Server end-to-end checkpoint application plus production caller proof are now delivered. `QDB07` stays WAITING after `QDB09` plus release gates. | Docs/tests when paths clear | SQL Server e2e through the checkpoint engine, then QDB07 |
| MT | `docs/ai/MULTITENANCY_PROMPT_QUEUE.md` + `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md` | none (`MT01` DONE) | `MT02` WAITING on owner approval of identity/membership source or single-tenant API-key binding. | Contract docs when paths clear | Owner decision -> MT02 |
| GAI | `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md` + `docs/ai/GENAI_COPILOT_ROADMAP.md` | none | Blocked by current core-pilot/release evidence | Planning/audit only | Core pilot ready, then explicit GenAI entry gate |
| DEX | `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md` + `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` | none | `DEX19` DONE = Executive Board explainability runtime. `DEX20` DONE = cross-family alternatives contract on main. | Yes, docs/contracts when path-safe | Alternatives contract on main, then optional runtime reuse |
| RL | same queue/roadmap as DEX | none | `RL10` DONE = Slice 4 advisory calibration contract. `RL11` DONE = advisory calibration runtime gate contract. `RL12` is WAITING for a causal outcome-comparison planning gate. Duplicate `RL08` remains OBSOLETE. | Yes, docs/contracts when path-safe | keep RL12 WAITING until its named analytics evidence dependencies and explicit owner promotion |
| DT | same queue/roadmap as DEX | none | `DT08` DONE = Slice-5 hardening. `DT09` DONE = first-class timestamp contract on main. `DT10` DONE = derived-clock honesty on main. | Yes, docs/contracts when path-safe | DT queue complete |
| PERF | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/PERFORMANCE_ROADMAP.md` | none | `PERF17` DONE = measured frontend bundle baseline and guardrail; `PERF16` remains BLOCKED until `MT10` or an owner-recorded shared-SaaS gate. PERF15 DONE = D8 stays MT-owned and `n/a_dedicated`. | Yes, docs/contracts when paths clear | D8 reopen after MT fixtures |
| OBS | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/OBSERVABILITY_ROADMAP.md` | none (`OBS11` DONE) | `OBS01`-`OBS11` DONE. Operational dashboard panel inventory / correlation contract frozen. | Yes, docs/contracts | queue complete |
| SEC | `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` + `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md` | none | `SEC08` DONE = reproducible frontend audit gates for both workspace lockfiles; `SEC07` DONE = clientapp production npm audit is 0. `SEC05` waits on MT09 or an explicitly approved interim dedicated-deploy offboarding scope. | Yes, scoped dependency/security work when paths clear | SEC05 after MT09 |

Owner refill 2026-08-13 sequential backlog of 15 prompts is complete. Owner refill 2026-08-20 promoted/executed `DT09` + `DEX20` + `OBS11` + `STAB13` as docs DONE on main (`docs/planning/QUEUE_REFILL_2026-08-20.md`). Audit follow-up delivery on 2026-08-22 closed `BCI10`, `STAB14`, `STAB15`, `RQ108`, `RQ109`, and `QDB09` on current `main`. `RQ110`, `RQ111`, `RQ112`, `RQ113`, `RQ114`, `RQ115`, `RQ116`, `RQ117`, `RQ118`, `RQ119`, `RQ120`, `RQ121`, `RQ122`, `RQ123`, `RQ124`, `RQ127`, `RQ129`, `RQ134`, `RQ135`, and `RQ63` are DONE; BCI and QDB have no current READY prompt. The first 2026-08-27 production audit created `STAB16` BLOCKED and `RQ128` WAITING; a same-day API-only queue audit then confirmed Render runtime `6ecbfa67a7304c3cbeeb71755a35255e766c8e24` is contained in current `main`, repaired stale STAB/RQ routing truth, and promoted `RQ129` as the live Decision Board fake-confidence cleanup follow-up, which is now landed on `main`. Planning sync on 2026-08-28 added `RQ130` and `RQ131` as later WAITING vendor baseline/trend-truth follow-ups in the active cross-surface addendum without changing the current RQ READY state. The 2026-09-01 cache-invalidation review closed `RQ134` as the supplier-summary freshness follow-up after aggregate refresh and closed `RQ135` as the trust-bearing cache parity follow-up after data-quality snapshot refresh, returning the current RQ READY state to `none`. On 2026-09-02, `PERF17` was explicitly promoted and completed as a parallel-safe frontend bundle measurement/guardrail run; the PERF queue returned to `none`. On 2026-09-02, `SEC08` was explicitly promoted and completed as a parallel-safe reproducible frontend dependency-audit gate; the SEC queue returned to `none`. `QDB07` remains WAITING for release gates. `DT10`, `RQ107`, and `RL11` are DONE. `PERF16` stays BLOCKED on `MT10`. On 2026-09-07, `RQ178`, `RQ179`, `RQ180`, `RQ181`, `RQ182`, and `RQ183` were completed as bounded local analytics contract slices; the RQ queue currently has no READY prompt. Do not promote `MT02`, `GAI01`, or `SEC05` without their named gates.
Owner refill 2026-08-13 sequential backlog of 15 prompts is complete. Owner refill 2026-08-20 promoted/executed `DT09` + `DEX20` + `OBS11` + `STAB13` as docs DONE on main (`docs/planning/QUEUE_REFILL_2026-08-20.md`). Audit follow-up delivery on 2026-08-22 closed `BCI10`, `STAB14`, `STAB15`, `RQ108`, `RQ109`, and `QDB09` on current `main`. `RQ110`, `RQ111`, `RQ112`, `RQ113`, `RQ114`, `RQ115`, `RQ116`, `RQ117`, `RQ118`, `RQ119`, `RQ120`, `RQ121`, `RQ122`, `RQ123`, `RQ124`, `RQ127`, `RQ129`, `RQ134`, `RQ135`, and `RQ63` are DONE; BCI and QDB have no current READY prompt. The first 2026-08-27 production audit created `STAB16` BLOCKED and `RQ128` WAITING; a same-day API-only queue audit then confirmed Render runtime `6ecbfa67a7304c3cbeeb71755a35255e766c8e24` is contained in current `main`, repaired stale STAB/RQ routing truth, and promoted `RQ129` as the live Decision Board fake-confidence cleanup follow-up, which is now landed on `main`. Planning sync on 2026-08-28 added `RQ130` and `RQ131` as later WAITING cross-surface vendor baseline/trend-truth follow-ups in the active cross-surface addendum without changing the current RQ READY state. The 2026-09-01 cache-invalidation review closed `RQ134` as the supplier-summary freshness follow-up after aggregate refresh and closed `RQ135` as the trust-bearing cache parity follow-up after data-quality snapshot refresh, returning the current RQ READY state to `none`. On 2026-09-02, `PERF17` was explicitly promoted and completed as a parallel-safe frontend bundle measurement/guardrail run; the PERF queue returned to `none`. On 2026-09-02, `SEC08` was explicitly promoted and completed as a parallel-safe reproducible frontend dependency-audit gate; the SEC queue returned to `none`. `QDB07` remains WAITING for release gates. `DT10`, `RQ107`, and `RL11` are DONE. `PERF16` stays BLOCKED on `MT10`. On 2026-09-05, owner-promoted `RQ139` and SQL-owner `Q83` as independently runnable current prompts with disjoint scopes; `RQ140`-`RQ146` remain WAITING behind their declared dependencies. Do not promote `MT02`, `GAI01`, or `SEC05` without their named gates.

| # | ID | Status | Program |
|---|---|---|---|
| 1 | `RQ100` | DONE | RQ |
| 2 | `RQ101` | DONE | RQ |
| 3 | `RQ102` | DONE | RQ |
| 4 | `RQ103` | DONE | RQ |
| 5 | `RQ104` | DONE | RQ |
| 6 | `RQ105` | DONE | RQ |
| 7 | `P-UI-21` | DONE | P-UI |
| 8 | `P-UI-22` | DONE | P-UI |
| 9 | `DEX18` | DONE | DEX |
| 10 | `RL07` | DONE | RL |
| 11 | `DT07` | DONE | DT |
| 12 | `PERF15` | DONE | PERF |
| 13 | `OBS08` | DONE | OBS |
| 14 | `OBS09` | DONE | OBS |
| 15 | `SEC07` | DONE | SEC |

## Product and process documents

- Product direction: `docs/product/PRODUCT_VISION.md`
- Feature flow: `docs/planning/FEATURE_LIFECYCLE.md`
- Business milestones: `docs/roadmaps/BUSINESS_ROADMAP.md`
- Architecture decisions: `docs/architecture/ADRS.md`
- Planning consolidation evidence: `docs/planning/PLANNING_CONSOLIDATION_AUDIT_2026-08-08.md`
- Latest prompt/commit implementation audit: `docs/qa/PROMPT_IMPLEMENTATION_AUDIT_2026-08-10.md`
- Current retail analytics market/capability gap audit: `docs/qa/RETAIL_ANALYTICS_COMPETITIVE_GAP_AUDIT_2026-08-12.md`

## Decision Intelligence boundary

Decision Intelligence is not a synonym for analytics. Analytics describes and measures the business. Decision Intelligence links evidence to a recommended decision, exposes why that decision was made, records alternatives, tracks what happened after action, and learns from outcomes.

The deterministic order is:

`evidence -> decision -> explanation -> alternatives -> action -> execution -> outcome -> learning`

No AI dependency is required for DEX, RL, or DT. LLMs may later explain already-authoritative evidence, but they must not become the source of truth for confidence, recommendation, outcome, or decision history.

## Milestone routing

| Milestone | Must be satisfied primarily by |
|---|---|
| Pilot Ready | BCI, STAB, RQ, OBS evidence |
| First Customer | STAB, QDB, OBS, SEC, deterministic DEX foundations |
| 10 Customers | QDB, MT staged isolation, PERF, OBS, SEC |
| 50 Customers | MT shared-SaaS gates, PERF scalability, OBS SLA evidence, SEC operational hardening, RL/DT evidence |
| SaaS Ready | MT release gate + tenant-owned QDB persistence + PERF/OBS/SEC gates |
| AI Ready | Core pilot ready + GAI security/evaluation gate + tenant boundaries where applicable; deterministic decision evidence remains authoritative |

Detailed milestone acceptance belongs in `docs/roadmaps/BUSINESS_ROADMAP.md`.

## Competitive capability priority

The current market-gap audit confirms that Trendplus should keep its explainable retail-decision positioning instead of chasing generic BI feature parity. Depth-first priorities after the active release gate are:

`release truth -> source adaptability -> observed historical inventory -> exception/digest delivery -> validated forecasting -> controlled scenarios -> shared SaaS/AI later`

The audit is planning evidence only. It does not create a competing READY queue. New work must still map to an existing owner first.

## Retail analytics product architecture

Trendplus should be judged by the quality of a retail decision at a declared grain, not by the number of charts. The durable product path is:

`source event -> canonical product context -> observed business fact -> trustworthy metric -> prioritized decision -> action -> measured outcome`

The following capability layers are sequential. A later layer may use an earlier one, but must not silently compensate for its absence.

| Layer | Current direction | Owner when work is proposed | Boundary |
|---|---|---|---|
| Product context | Stable SKU/product identity, variant, hierarchy, supplier, store, season and unit semantics across a source mapping | QDB + RQ | Do not create a second product-master system or guess a hierarchy from labels. |
| Observed retail facts | Sales, returns, price events and observed SKU/store/day inventory with source/proxy provenance | RQ + QDB | Reconstructed stock and missing history remain explicitly distinct from observed facts. |
| Decision-ready metrics | Availability, margin, sell-through, stock age, demand variability and forecast measures with declared denominators | RQ | A metric is not decision-ready until its scope, units, freshness and exclusions are visible. |
| Operational decisions | Replenish, transfer, hold, markdown or investigate with eligibility, feasibility, confidence and alternatives | DEX + RQ | A recommendation is not an ERP command and must not write back to a customer source by default. |
| Learning and planning | Outcome measurement, forecast scorecards, controlled scenarios and calibration | RL + DT + RQ | No adaptive policy, elasticity claim or scenario result without measured baseline and uncertainty. |

Product identity/hierarchy, observed inventory history, forecast provenance/backtesting and exception delivery are not parallel "dashboard ideas". They are the data and operating foundations that make the existing product, inventory, supplier and decision surfaces more useful.

Before a future prompt makes a retail metric or recommendation customer-facing, it must declare: decision grain (for example SKU/store/day), source and transformation provenance, population/denominator, known exclusions, freshness/coverage, action constraints, and the outcome that could validate value. The concrete prompt stays in its existing owner queue.

## Historical/current separation

- `docs/ai/NEXT_PROMPT_QUEUE.md` is a historical ledger and is never a current router.
- Dated QA/audit documents remain immutable evidence snapshots unless a document explicitly declares itself current.
- Addendum prose such as "main queue READY RQ01" or old "next READY" completion notes is historical when it conflicts with the current queue header and this master roadmap.
- Do not delete historical evidence to make routing look clean. Add a current pointer or archive classification instead.

## Governance checks

Before claiming planning consolidation complete, run:

```text
node scripts/check-prompt-queues.mjs
node scripts/check-prompt-queues.mjs --self-test
node scripts/check-planning-architecture.mjs
node scripts/check-planning-architecture.mjs --self-test
```

The planning validator owns the new master/roadmap/queue linkage. The prompt validator owns active execution queues, including BCI and its evidence addendum, plus the legacy/current queues it inventories.

## Change rule

When a feature is proposed:

1. map it to an existing program first;
2. create a new program only when ownership is genuinely different;
3. update the roadmap before creating implementation work;
4. expose one `Current READY` primary/default pointer per program for deterministic routing, while allowing additional READY prompts that are independently runnable;
5. keep dependent or collision-prone prompts WAITING; independent prompts may also be READY when feature-family, path, dependency and gate checks prove that concurrent claims are safe;
6. update this file only when ownership, current READY, blocking relationship, or next milestone changes.

Do not copy implementation detail into this file. The owner queue is the implementation contract.
