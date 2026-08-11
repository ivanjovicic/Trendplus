# Privileged Secrets Assurance

Status: authoritative SEC03 assurance doc
Date: 2026-08-11
Backlog slice: `S2-1` in `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`
Roadmap: `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
Ownership map: `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md`

## Purpose

Define the privileged secret classes that Trendplus must track for post-STAB assurance, plus the rotation, revoke and emergency-access expectations that should exist before any runtime security prompt tries to claim readiness.

This document is assurance-only. It does **not**:

- implement authentication, authorization or IdP changes;
- change the admin gate behavior;
- add secret scanning or runtime policy enforcement;
- own connector feature work that belongs to QDB;
- store or reveal real secrets.

## Ownership boundary

SEC owns the assurance inventory and the evidence checklist.

STAB remains the primary owner of current pilot authz defects and admin gate behavior.
QDB remains the primary owner of connector credential feature behavior.
MT remains the primary owner of tenant membership/isolation.
GAI remains the primary owner of GenAI provider/tool boundaries.

SEC must only describe and verify the orphaned assurance layer. It must not recreate those other queues.

## Secret classes

| Secret class | Primary owner | SEC assurance focus | Fail-closed rule |
|---|---|---|---|
| Admin API key / privileged deploy secret | STAB | Inventory the class, rotation expectation and break-glass evidence | Missing or invalid key stays denied |
| DB connection secret | STAB / platform ops | Confirm where the secret is referenced and how it is rotated or revoked | Missing rotation evidence stays unknown |
| Storage secret / object-store credential | STAB / platform ops | Record where exports/files would rely on it and how access is revoked | No confirmed secret path means no PASS |
| Cache secret / cache credential | STAB / platform ops | Note whether the cache is shared or scoped and how revoke happens | Unknown cache scope is not green |
| Connector SecretReference | QDB | Record the reference model only; never the raw value | Plaintext credential handling is blocked |

## Rotation and revoke expectations

Every privileged secret class should answer the same questions:

1. Who can mint or rotate it?
2. Who can revoke it?
3. What is the expected blast radius of revocation?
4. What is the post-incident cleanup step?
5. What evidence proves the old secret no longer works?

Minimum assurance expectations:

- rotation is documented before runtime claims are made;
- revoke steps are explicit and reversible only through a new mint;
- emergency access uses the smallest viable blast radius;
- post-incident revoke is part of the written runbook;
- secret values never appear in git, logs or evidence markdown.

## Emergency-access expectations

Break-glass access is a support mechanism, not a security exception.

Required expectations:

- one named emergency path per privileged class where applicable;
- a clear owner for who can mint the emergency credential;
- audit notes for when and why the emergency path was used;
- a revoke/rotate follow-up after every use;
- no assumption that emergency access equals production identity.

## Evidence gaps

The following gaps remain open until a later runtime or dry-run prompt closes them:

- no live rotation proof for the privileged classes exists in this doc;
- no non-prod rotation rehearsal is attached here;
- STAB authz defects remain STAB-owned;
- QDB connector secret handling remains QDB-owned;
- external IdP selection remains a separate product/architecture decision.

## Validation rules

- no secret values or connection strings in this document;
- each class must retain one primary owner;
- missing evidence stays unknown or BLOCKED, never PASS;
- this doc may cite STAB/QDB/MT/GAI but must not rename their ownership;
- docs-only assurance is acceptable here, but runtime claims are not.

## Acceptance

- privileged secret classes are listed with owner boundaries;
- rotation, revoke and emergency-access expectations are explicit;
- the doc provides a stable citation for future SEC runtime prompts;
- no runtime security behavior changed.
