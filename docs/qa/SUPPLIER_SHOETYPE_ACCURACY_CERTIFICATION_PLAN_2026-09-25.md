# Supplier / Shoe Type Accuracy Certification Plan

Date: 2026-09-25  
Repository: ivanjovicic/Trendplus  
Status: canonical plan for the certification follow-up; not a product implementation and not a claim that either screen is already certified.

## Purpose

Define the evidence required before Trendplus may make a defensible, bounded accuracy claim for Prodaja po dobavljačima and Prodaja po vrsti obuće. The claim is scoped to a tenant/store, requested and effective period, data scope, application build, schema and contract version. It must never be presented as an unconditional claim about every future source record.

Preferred customer wording:

> For the certified dataset, period, store and application build, Supplier and Shoe Type sales totals and buckets were independently reconciled with the source sale lines with zero unexplained deltas; unknown, estimated and cost-limited portions are shown explicitly.

## Current-main comparison

| Desired layer | Current evidence on main | Verdict / owner |
|---|---|---|
| Formal accuracy contract | RQ411/RQ412 manifests define attribution/oracle facts, but no single contract binds API, UI, export, trust, cost qualification and claim language. | Missing — RQ445 |
| Live RQ412 oracle execution | Four PostgreSQL oracle cases are implemented but the evidence says they were not run in the recorded environment. | Missing proof — RQ447 |
| Adversarial golden dataset | RQ407 has a strong shared seed and RQ412 has expected totals, but the full boundary/duplicate/mutation/cost adversarial corpus is not an immutable certification pack. | Missing — RQ446 |
| OP2-01/04/05/14 reproducers | Second-pass classification keeps these as potential and records no failing runtime assertion. | Missing runtime closure — RQ447; do not reopen statically |
| Backend to browser to render/export reconciliation | RQ407 proves live PostgreSQL HTTP routes and frontend focused tests; its evidence explicitly says deployed browser request-to-render capture was not run. | Missing — RQ448 |
| Persistent integrity evidence/history | RQ413 exposes a process/runtime snapshot and evidence id; it does not persist an append-only run history across restart. | Missing — RQ449 |
| Post-import immediate probe | RQ413 marks unverified on cache clear and has worker/on-demand probes; immediate Access-import execution is explicitly listed as missed. | Missing — RQ450 |
| Frontend Verified/evidence UI | Backend operationsIntegrity metadata exists, but the Supplier and Shoe Type pages do not provide the complete customer-inspectable proof surface. | Missing — RQ451 |
| Accuracy certificate | No Supplier/Shoe Type certificate artifact is present. | Missing — RQ452 |
| Non-skippable certification CI | RQ412 integration tests are opt-in and the recorded run could not execute them; no dedicated non-skip certification gate is evidenced. | Missing — RQ453 |
| Production read-only reconciliation | STAB16 and older audits identify the general missing access/browser proof; no Supplier/Shoe Type-specific dated artifact exists. | Missing / externally gated — RQ454 |
| Customer acceptance evidence | No fixed-window customer reconciliation/sign-off pack exists for these two screens. | Missing — RQ455 |

## Deconfliction rules

- RQ407 remains the owner of the shared eight-route fixture and live HTTP arithmetic proof; certification prompts consume it and do not create another cross-screen seed.
- RQ411 remains the owner of immutable sale-time Supplier/Shoe Type attribution. RQ412 remains the owner of the independent raw SQL oracle. RQ413 remains the owner of runtime integrity states, bounded probes and fail-closed recommendation gating.
- RQ373-RQ380 and RQ375-RQ377 remain the owners of Supplier Sales and Shoe Type population, margin, comparable-cohort, detail and trust contracts. RQ442 remains the owner of whole-day half-open implementation.
- OP2-01/04/05/14 are not promoted as product defects from static evidence. RQ447 is a runtime reproducer/closure task; a new fix prompt is justified only by a failing reproducer and must name the smallest existing owner boundary.
- Existing STAB16 production access/deploy work is not duplicated. RQ454 is the screen-specific reconciliation artifact after that gate is available.
- Existing export, document, CI and customer-readiness conventions are reused; new prompts bind them to the Supplier/Shoe Type certification evidence id.

## Prompt sequence

| Sequence | Prompt | Status | Output |
|---:|---|---|---|
| 1 | RQ445 | WAITING | canonical contract and bounded claim language |
| 2 | RQ446 | WAITING | adversarial fixture and immutable expected manifest |
| 3 | RQ447 | WAITING | live oracle execution and OP2 runtime closure |
| 4 | RQ448 | WAITING | DB/API/browser/render/detail/export reconciliation artifact |
| 5 | RQ449 | WAITING | durable append-only integrity evidence history |
| 6 | RQ450 | WAITING | post-import probe lifecycle |
| 7 | RQ451 | WAITING | Verified/evidence surface on both screens |
| 8 | RQ452 | WAITING | evidence-backed PDF/HTML certificate |
| 9 | RQ453 | WAITING | non-skippable release certification CI |
| 10 | RQ454 | WAITING | production read-only reconciliation artifact |
| 11 | RQ455 | WAITING | customer acceptance/reconciliation pack |

## Certification definition

A Supplier/Shoe Type result may be labelled VERIFIED only when all required layers for the claim are executed on the same contract/fixture/build/schema/evidence scope:

1. independent raw-fact oracle and API agree with zero unexplained deltas;
2. requested/effective period, store and data scope are explicit;
3. browser-rendered KPI/table/detail and export agree with the API/oracle;
4. no required test is skipped, unavailable or stale;
5. unknown attribution and cost coverage are visible and qualified;
6. integrity evidence is durable and traceable by evidence id;
7. production/customer evidence is present when the claim is presented as production or customer-certified.

If any required layer is missing, the honest state is UNVERIFIED, DEGRADED, DRIFT_DETECTED or NOT_RUN, not a green accuracy claim.

## Delivery boundary

This planning update changes documentation and queue routing only. It does not implement product code, migrate data, access production, run customer acceptance or mark any new prompt DONE.
