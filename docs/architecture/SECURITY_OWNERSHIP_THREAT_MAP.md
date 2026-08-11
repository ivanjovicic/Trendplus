# Security Ownership and Threat Map

Status: authoritative SEC01 reconciliation  
Date: 2026-08-11  
Roadmap: `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`  
Related:

- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`
- `docs/security/TENANT_SAFETY_CHECKLIST.md`
- `docs/security/GENAI_SECURITY_AND_DATA_BOUNDARIES.md`
- `docs/architecture/MULTITENANCY_ARCHITECTURE_ROADMAP.md`
- `docs/architecture/DATA_SOURCE_CONNECTOR_ROADMAP.md`
- `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`

## Purpose

Reconcile who owns which security surface so SEC does not duplicate STAB, MT, QDB, GAI or OBS.

Rules:

1. Exactly one **primary owner** per surface for the next real defect.
2. **Collaborators** may constrain the owner but do not take the defect.
3. Missing runtime evidence stays **unknown/BLOCKED**, never PASS.
4. No secrets or customer payloads in this document.

## Ownership vocabulary

| Owner | Owns |
|---|---|
| **STAB** | Current pilot/release security defects and evidence (auth boundary, admin key, edge, backup/restore rehearsal, release gate) |
| **MT** | Shared-SaaS tenant identity, membership, isolation of DB/cache/jobs/storage/docs |
| **QDB** | Source connector behavior, credential handling for imports, mapping/checkpoint identity |
| **GAI** | GenAI/RAG/provider/tool/data-boundary gates |
| **OBS** | Safe correlation/SLI catalogs and security-relevant telemetry *shape* (not authz) |
| **SEC** | Cross-cutting post-STAB assurance, threat-model maintenance, orphans not owned above |
| **accepted/n/a** | Explicitly deferred or not applicable in current single-customer pilot mode |

## Surface / threat / owner matrix

| ID | Surface | Primary threat | Primary owner | Collaborators | Current evidence posture | Notes |
|---|---|---|---|---|---|---|
| S1 | Identities / roles / authn | Spoofed identity; assumed Admin role without IdP | **STAB** | MT (later membership) | PARTIAL / unknown for production IdP | Phase 1 = Admin API key; no `AddAuthentication` pipeline |
| S2 | Admin API key / privileged ops | Key leak; over-broad admin surface | **STAB** | SEC (rotation assurance later) | PARTIAL | Dedicated-deploy only; rotation runbook orphan → SEC02 candidate |
| S3 | Edge / proxy / diagnostics | Info leak; open diagnostics | **STAB** | OBS | PARTIAL until live smoke | STAB05 contract |
| S4 | Tenant isolation / membership | Cross-customer data access | **MT** | STAB (pilot dedicated deploy) | **NOT READY** shared SaaS | One deployment per customer until MT gate |
| S5 | Source connector credentials | Secret in logs/UI; shared creds | **QDB** | MT (tenant-owned profiles) | docs/contracts; runtime varies | Never log real credentials |
| S6 | Import files / storage / batches | Unauthenticated sensitive reads | **STAB** | QDB, MT | **GAP** (STAB03 follow-up not queued) | Primary stays STAB — do not invent parallel SEC prompt |
| S7 | Documents / exports / reports | Header-role trust; cross-customer files | **STAB** | MT (`MT08`) | **GAP** (STAB03 follow-up) | Same: STAB primary until closed or accepted |
| S8 | Workers / outbox / jobs | Wrong-customer processing; public worker APIs | **STAB** | MT (`MT07`) | PARTIAL | Admin gates STAB; tenant job ownership MT |
| S9 | Public health / version | Over-disclosure | **STAB** | OBS | PARTIAL | Must not create fake tenant |
| S10 | GenAI / RAG / tools / providers | Over-agency; data exfil; browser→provider | **GAI** | SEC (AI security gate checklist) | BLOCKED by release/pilot evidence | `GENAI_SECURITY_AND_DATA_BOUNDARIES.md` |
| S11 | Backup / restore | Restore to wrong scope; unproven | **STAB** | MT (`MT09`), SEC (assurance) | PARTIAL (STAB07) | Tenant-level restore unproven |
| S12 | Logging / redaction | Secret leakage in logs | **STAB** | OBS, MT | PARTIAL | `/api/logs*` gating gap → STAB follow-up |
| S13 | Correlation IDs | Misuse as auth/tenant | **OBS** | STAB | Catalogued in OBS01 | IDs diagnose flows only |
| S14 | Supply-chain / dependencies | Vulnerable/abandoned packages | **SEC** | BCI/PERF (build) | **ORPHAN → SEC** | No STAB/QDB owner; SEC-3 |
| S15 | Retention / offboarding / classification | Over-retention; wrong delete scope | **SEC** | MT, GAI, STAB | **ORPHAN → SEC** | SEC-4 after pilot stable |
| S16 | Security incident response | No owner for continuous assurance | **SEC** | OBS, STAB | **ORPHAN → SEC** | SEC-6 post-handoff |

## Threat themes (reconciled, not duplicated)

### A. Pilot authentication boundary (STAB)

- Admin API key gates privileged operational reads/writes.
- Missing/wrong key fails closed.
- Role claims without IdP must not be treated as production identity.

Canonical: STAB03/STAB04 audits and `AdminAccessControl`.

### B. Dedicated-deploy isolation (STAB + ops)

Until MT shared-SaaS gate:

- one DB / storage / cache scope per customer;
- Admin key bound to that deployment only.

Canonical: `TENANT_SAFETY_CHECKLIST.md`.

### C. Shared-SaaS isolation (MT)

- Server-resolved `TenantId` + membership;
- no caller header as authority;
- tenant-owned cache/jobs/files/connectors.

Canonical: MT roadmap/queue. SEC consumes MT gate results; does not redefine TenantId.

### D. Connector secrets (QDB)

- Credentials via SecretReference / env — never UI plaintext logs;
- provider adapters read-only unless future ADR.

Canonical: QDB roadmap/contract. SEC does not own connector feature work.

### E. GenAI boundaries (GAI)

- Model untrusted for authz/writes;
- no browser→provider;
- data classes blocked until approved.

Canonical: `GENAI_SECURITY_AND_DATA_BOUNDARIES.md`. SEC-8 is a gate checklist after GAI, not a second GenAI queue.

### F. Observability safety (OBS)

- Unknown telemetry ≠ green;
- correlation not auth;
- no sensitive payloads in metrics.

Canonical: `OBSERVABILITY_SLI_CATALOG.md`.

## Historical duplication pointers

| Historical / overlapping description | Point to current owner |
|---|---|
| Old “security” notes inside analytics reliability audits | RQ owns analytics honesty; STAB owns authz defects |
| Tenant checklist vs STAB auth plan | Pilot auth = STAB; SaaS isolation = MT |
| GenAI security doc vs SEC-8 | GAI owns implementation gates; SEC owns cross-cutting AI security gate evidence after GAI |
| OBS correlation vs STAB redaction | OBS catalog; STAB secret redaction in logs/admin |
| QDB credential rules vs SEC-2 secrets | QDB owns connector secrets; SEC owns org-wide secrets assurance inventory later |

Do not delete historical evidence; label it historical when it conflicts with this map.

## True orphan risks (SEC-relevant)

These lack a **queued** owner prompt today. Primary product owner for active defects remains as in the matrix; SEC backlog only for cross-cutting assurance after STAB acceptance:

| Orphan | Prefer next action | SEC role |
|---|---|---|
| Import batch/cleanup/archive sensitive reads (STAB03 follow-up) | Queue STAB follow-up (not SEC duplicate) | Track only if STAB explicitly defers |
| `/api/logs*` read gating | Queue STAB follow-up | Track if deferred |
| Document/export header-role trust | Queue STAB follow-up | Track if deferred |
| Admin API-key rotation / emergency access runbook | SEC02 secrets/privileged assurance | **SEC** |
| Dependency/supply-chain scanning policy | SEC-3 | **SEC** |
| Data retention / offboarding policy beyond MT09 | SEC-4 | **SEC** (with MT) |
| Continuous security incident ownership | SEC-6 | **SEC** (with OBS) |
| External IdP selection | Product/architecture decision; then STAB/MT | **accepted/n/a** until decided |

## STAB → SEC handoff criteria

SEC may take cross-cutting assurance work only when **all** are true:

1. STAB P0 pilot security tasks for the active release are DONE or explicitly accepted with residual risk recorded.
2. No open STAB READY/IN_PROGRESS task owns the same surface.
3. Shared-SaaS claims (if any) still require MT gate — SEC does not bypass MT.
4. GenAI customer features still require GAI gates — SEC does not bypass GAI.
5. The proposed SEC prompt cites this map’s orphan ID and does not recreate STAB03–STAB09 scope.

Until then, open pilot authz gaps remain **STAB** (or newly queued STAB follow-ups), not SEC.

## Evidence principles (enforced)

- Docs-only ≠ runtime PASS.
- Unknown evidence → BLOCKED/WARN.
- No real secrets in commits or SEC evidence packs.
- IDs (correlation, batch, tenant slug) are not authorization.

## Acceptance (SEC01)

- current ownership/threat map exists;
- every listed surface has one primary owner;
- SEC future work is limited to genuine cross-cutting orphans / post-STAB assurance;
- no runtime security change was made.
