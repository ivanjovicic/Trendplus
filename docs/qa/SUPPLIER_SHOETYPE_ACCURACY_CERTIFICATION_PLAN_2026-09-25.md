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
| Retail receipt population parity | Daily Sales excludes trimmed/case-insensitive DUG/KOREKCIJA; Supplier, Shoe Type and Color currently do not apply the same header predicate. | Confirmed inconsistency — RQ456 |
| Shoe Type identity / previous-only rows / margin semantics | Several backend/detail paths infer unknown from the label `Nepoznato`; rows originate only from current-period types; headline weighted margin currently uses a name-filtered known cohort. | Confirmed semantic gaps — RQ457 |
| Non-positive margin contribution share | Shoe Type chart already falls back to RSD, but detail formatting previously returned percentage shares for a negative total and 0% for 0/0. | Immediate fix delivered on main 2026-09-25; RQ457 keeps backend/detail/export parity |

## Semantic closure decisions — 2026-09-25

The certification review resolves the open business questions as follows:

1. **DUG/KOREKCIJA are not retail turnover.** Receipt numbers `DUG` and `KOREKCIJA`, normalized with trim + case-insensitive comparison, are excluded from certified retail-sales facts on Daily Sales, Supplier, Shoe Type and Color. They remain auditable as non-standard adjustment/debt documents and may be shown separately. Signed retail returns remain in the sales population.
2. **Margin-contribution share requires a strictly positive denominator.** If total margin contribution is zero or negative, row share is unavailable/N/A and the UI may show absolute RSD contributions. The Shoe Type frontend helper was hardened immediately so negative totals and 0/0 fail closed.
3. **Shoe Type unknown identity is ID/null-based.** Only `tipObuceId == null` is the unknown bucket. A non-null ID whose display name is blank or literally `Nepoznato` remains a known entity; the bad/missing label is a data-quality issue, not identity.
4. **Previous-period-only Shoe Types need rows.** The row population for PoP comparison is the union of current and previous identities. A type with previous sales and no current sales is represented with current revenue/units 0 and a -100% PoP change when the previous denominator is positive. Current-period margin/cost/recommendation fields must not be invented.
5. **Headline average margin is the full-population weighted aggregate.** The customer-facing `Prosečna marža` card uses current response `sum(marginContribution) / sum(costCoveredRevenue)` for all dimension buckets with covered cost, including the unknown bucket. A recommendation benchmark that intentionally excludes unknown IDs is a separate known-identity metric with separate provenance.

These decisions are certification prerequisites. RQ456 owns the cross-surface receipt population implementation; RQ457 owns Shoe Type identity, previous-only PoP and margin-denominator separation. Existing RQ447 and RQ449 keep their already-assigned certification meanings and are not renumbered.

## Deconfliction rules

- RQ407 remains the owner of the shared eight-route fixture and live HTTP arithmetic proof; certification prompts consume it and do not create another cross-screen seed.
- RQ411 remains the owner of immutable sale-time Supplier/Shoe Type attribution. RQ412 remains the owner of the independent raw SQL oracle. RQ413 remains the owner of runtime integrity states, bounded probes and fail-closed recommendation gating.
- RQ373-RQ380 and RQ375-RQ377 remain historical owners of Supplier Sales and Shoe Type population, margin, comparable-cohort, detail and trust contracts. RQ456/RQ457 are narrowly scoped residual semantic owners discovered by certification review; they do not reopen unrelated delivered work. RQ442 remains the owner of whole-day half-open implementation.
- OP2-01/04/05/14 are not promoted as product defects from static evidence. RQ447 is a runtime reproducer/closure task; a new fix prompt is justified only by a failing reproducer and must name the smallest existing owner boundary.
- Existing STAB16 production access/deploy work is not duplicated. RQ454 is the screen-specific reconciliation artifact after that gate is available.
- Existing export, document, CI and customer-readiness conventions are reused; new prompts bind them to the Supplier/Shoe Type certification evidence id.

## Prompt sequence

| Sequence | Prompt | Status | Output |
|---:|---|---|---|
| 1 | RQ445 | WAITING | canonical contract and bounded claim language |
| 2 | RQ456 | WAITING | one DUG/KOREKCIJA retail-sales population across Daily/Supplier/Shoe/Color/oracle |
| 3 | RQ457 | WAITING | Shoe Type ID/null identity, previous-only rows and margin semantics |
| 4 | RQ446 | WAITING | adversarial fixture and immutable expected manifest |
| 5 | RQ447 | WAITING | live oracle execution and OP2 runtime closure after semantic owners |
| 6 | RQ448 | WAITING | DB/API/browser/render/detail/export reconciliation artifact |
| 7 | RQ449 | WAITING | durable append-only integrity evidence history |
| 8 | RQ450 | WAITING | post-import probe lifecycle |
| 9 | RQ451 | WAITING | Verified/evidence surface on both screens |
| 10 | RQ452 | WAITING | evidence-backed PDF/HTML certificate |
| 11 | RQ453 | WAITING | non-skippable release certification CI |
| 12 | RQ454 | WAITING | production read-only reconciliation artifact |
| 13 | RQ455 | WAITING | customer acceptance/reconciliation pack |

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

This certification-plan update records the semantic decisions and queue routing. The narrow Shoe Type non-positive margin-share frontend defect was fixed directly on main with focused regression expectations; RQ456/RQ457 remain the owners of the broader backend/detail/export/oracle changes. No production data was mutated or accessed and no new certification prompt is marked DONE.
