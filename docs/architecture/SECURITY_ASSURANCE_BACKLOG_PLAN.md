# Post-STAB Security Assurance Backlog Plan

Status: SEC02 planning document
Date: 2026-08-11
Ownership map: `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md`
Roadmap: `docs/roadmaps/SECURITY_EVOLUTION_ROADMAP.md`
Related: `docs/security/TENANT_SAFETY_CHECKLIST.md`, `docs/security/GENAI_SECURITY_AND_DATA_BOUNDARIES.md`, `docs/architecture/OBSERVABILITY_SLI_CATALOG.md`

## Purpose

Turn SEC01 orphans into a **bounded, non-duplicative** assurance backlog for Security Evolution (SEC).

This document is planning only. It does **not**:

- implement runtime authz, IdP, RBAC, or secret scanning;
- queue STAB follow-ups as SEC work;
- bypass MT shared-SaaS or GAI GenAI gates;
- claim PASS from docs-only evidence.

## Non-goals

- no parallel STAB03–STAB09 defect prompts
- no TenantId / membership redesign (MT)
- no connector credential feature work (QDB)
- no GenAI product implementation (GAI)
- no OBS dashboard product (OBS owns instrumentation shape; SEC consumes signals)
- no real secrets or customer payloads in evidence packs

## Ownership filter (must stay true)

| Surface from SEC01 | Allowed in SEC backlog? | Why |
|---|---|---|
| S1–S3, S6–S9, S11–S12 pilot authz / edge / files / workers / health / backup / logs | **No** (STAB primary) | Track only if STAB explicitly defers |
| S4 tenant isolation | **No** (MT) | SEC-7 consumes MT gate |
| S5 connector credentials | **No** (QDB) | SEC-2 inventories org-wide; QDB owns adapters |
| S10 GenAI | **No** (GAI) | SEC-8 is checklist after GAI |
| S13 correlation | **No** (OBS) | SEC-6 uses IDs; does not redefine them |
| S14 supply-chain | **Yes** | SEC-3 |
| S15 retention / offboarding assurance | **Yes** | SEC-4 (with MT collaborator) |
| S16 incident ownership | **Yes** | SEC-6 |
| Admin key rotation / emergency access runbook | **Yes** | SEC-2 (orphan from SEC01) |

## STAB deferral watchlist (not SEC prompts)

Keep visible; do **not** invent SEC duplicates. Prefer STAB follow-up queue entries:

| Watch ID | Gap | Preferred owner action |
|---|---|---|
| W-STAB-import | Sensitive import batch/cleanup/archive reads | Queue STAB follow-up |
| W-STAB-logs | `/api/logs*` read gating | Queue STAB follow-up |
| W-STAB-docs | Document/export header-role trust | Queue STAB follow-up |

If STAB records **accepted residual risk** with date/owner, a later SEC assurance slice may *verify* the residual — still citing the STAB acceptance note, not reopening the defect as SEC-owned.

## Handoff gates before SEC *runtime* work

Planning (this document) may proceed now. **Runtime** SEC prompts require SEC01 handoff criteria:

1. STAB P0 pilot security for the active release is DONE or residual risk accepted in writing.
2. No open STAB READY/IN_PROGRESS owns the same surface.
3. Shared-SaaS claims still require MT gate — SEC does not bypass.
4. Customer GenAI still requires GAI gates — SEC does not bypass.
5. Prompt cites orphan/slice ID from this plan and the ownership map.

Current posture (2026-08-11): STAB `Current READY: none` with completed STAB01–STAB09 lane, but watchlist gaps above remain **unqueued**. Treat them as STAB residual, not SEC READY for implementation.

## Prioritized SEC slices

| Rank | Slice | Roadmap | Pilot / assurance value | Runtime earliest |
|---:|---|---|---|---|
| 1 | **S2-1** Privileged secrets & emergency-access assurance | SEC-2 | Prevents silent key sprawl after pilot | After STAB residual acceptance *or* docs-only runbook first |
| 2 | **S2-2** Dependency / supply-chain policy | SEC-3 | Orphan; no other owner | Docs/policy anytime; CI wiring later with BCI |
| 3 | **S2-3** Retention / classification / offboarding assurance | SEC-4 | Orphan beyond MT09 | After MT09 contracts exist for tenant delete |
| 4 | **S2-4** Security observability & incident ownership | SEC-6 | Continuous assurance orphan | After OBS O2-1/O2-6 unknown≠green |
| 5 | **S2-5** Authorization assurance matrix (consume-only) | SEC-5 | Regression harness for post-STAB | After STAB watchlist closed or accepted |
| 6 | **S2-6** SaaS security gate checklist | SEC-7 | Blocks false multi-tenant claims | After MT gate |
| 7 | **S2-7** AI security gate checklist | SEC-8 | Blocks premature GenAI launch | After GAI gates |

## Slice definitions

### S2-1 — Privileged secrets and emergency access (SEC-2)

**Owns:** Admin API-key / deployment-secret **assurance** (inventory, rotation expectation, emergency access, audit of who can mint), not the STAB authz defect itself.

**Deliverables (future prompts):**

1. Inventory of privileged secret *classes* (Admin key, DB connection, storage, cache, connector SecretReference) — names/types only, no values.
2. Rotation / revoke expectations per class (who, when, blast radius).
3. Emergency access runbook: break-glass steps, dual-control where practical, post-incident revoke.
4. Negative evidence checklist: missing key fails closed (already STAB); rotation leaves old key dead.

**Evidence gaps before runtime PASS:**

- written inventory reviewed against current `AdminAccessControl` / config keys;
- dry-run rotation in a non-prod dedicated deploy;
- no secret material in git or evidence markdown.

**Dependencies:** STAB auth boundary contracts; QDB for connector secret *storage* rules; do not redefine QDB adapters.

**Not in scope:** External IdP selection; RBAC rewrite.

### S2-2 — Dependency and supply-chain posture (SEC-3)

**Owns:** Org-wide policy for vulnerable/abandoned packages and release evidence.

**Deliverables:**

1. Scanning policy: which ecosystems (.NET, npm, optional Python), frequency, severity fail rules.
2. Ownership of advisory triage (SEC + BCI for CI wiring).
3. Abandoned/unavailable package handling (pin, replace, or accepted risk note).
4. Optional: container/base-image ownership when containers are in scope.

**Evidence gaps:**

- documented fail thresholds;
- at least one reproducible scan command or CI job name (BCI-owned wiring);
- no claim of “secure supply chain” without scan output retained outside secrets.

**Dependencies:** BCI for CI integration; PERF not required.

**Not in scope:** Broad dependency upgrades as drive-by SEC work.

### S2-3 — Data protection and retention assurance (SEC-4)

**Owns:** Cross-cutting classification, retention, deletion, and offboarding **assurance** after product owners define storage.

**Deliverables:**

1. Data classes aligned with GAI doc + ops (customer source, analytics aggregates, decisions/actions, reports/exports, logs).
2. Retention/deletion expectations per class for dedicated-deploy mode.
3. Offboarding checklist that *consumes* MT09 when shared SaaS exists; until then, dedicated-deploy wipe/restore scope.
4. Backup/restore protection expectations (links STAB07 / MT09; does not replace them).

**Evidence gaps:**

- classification table with owner per class;
- delete/offboard steps that fail closed if scope unknown;
- AI provider retention called out as blocked until GAI policy approved.

**Dependencies:** MT09 for tenant lifecycle; GAI for provider retention; STAB for backup rehearsal evidence.

### S2-4 — Security observability and incident response (SEC-6)

**Owns:** Continuous security incident ownership and safe audit signals — not authz implementation.

**Deliverables:**

1. Security-relevant event catalog (authz deny, admin key reject, privileged action success/fail) without secret bodies.
2. Incident classes + runbook owners (SEC primary; STAB for pilot authz; OBS for signal plumbing).
3. Correlation usage rules: diagnose only; never authorize (OBS01).
4. Evidence retention for supported deployments (dedicated vs future SaaS).

**Evidence gaps:**

- events exist or explicitly `unknown` (not green);
- runbook names owner + severity;
- OBS unknown≠green respected (O2-6).

**Dependencies:** OBS instrumentation slices; STAB log redaction.

### S2-5 — Authorization assurance matrix (SEC-5)

**Owns:** Cross-route **assurance tests** that consume STAB/MT contracts.

**Deliverables:**

1. Route-family matrix: anonymous / admin-key / future membership → expected 401/403/200.
2. Negative tests for opaque IDs (batch, document, export) without inventing TenantId early.
3. Worker/outbox privileged action checks linked to existing admin gates.

**Evidence gaps:** automated negatives; missing matrix row = BLOCKED not PASS.

**Dependencies:** STAB watchlist closed or accepted; MT before any shared-tenant row.

**Not in scope:** New identity model.

### S2-6 — SaaS security gate (SEC-7)

Checklist gate only after MT isolation evidence. Must include:

- MT gate passed;
- tenant-owned connector/storage/job/cache paths verified (MT + QDB);
- privileged boundaries reviewed (STAB + S2-1);
- backup/restore + offboarding proven (STAB/MT + S2-3);
- security alerts + incident ownership exist (S2-4);
- no known untested cross-tenant path.

Missing item → gate **BLOCKED**.

### S2-7 — AI security gate (SEC-8)

Checklist gate only after GAI implementation gates. Must include items from `GENAI_SECURITY_AND_DATA_BOUNDARIES.md` plus:

- retrieval/tool authorization proven;
- prompt/tool logs safe;
- provider secrets isolated;
- AI-disabled mode fully supported;
- no browser→provider.

SEC does not implement GenAI features.

## Minimal first *planning* follow-up after SEC02

**Recommended next SEC queue item (when promoting):** docs/runbook for **S2-1** (privileged secret classes + rotation/emergency access) — still docs-only unless STAB residual is accepted and a runtime prompt is explicitly queued.

**Recommended first *CI-adjacent* SEC item:** **S2-2** policy + BCI collaboration note (scan command/severity), because it does not compete with STAB authz surfaces.

Do not promote S2-5/S2-6/S2-7 while STAB watchlist or MT/GAI gates are open for the claimed surface.

## Future prompt template (required fields)

Every future SEC runtime/planning prompt must include:

- `Slice:` S2-x
- `Orphan/map IDs:` from SEC01
- `Primary owner:` SEC (collaborators named)
- `Not duplicating:` explicit STAB/MT/QDB/GAI/OBS IDs
- `Evidence gap:` what is unknown today
- `Fail-closed rule:` what happens when evidence is missing
- `Dependencies:` exact gates

## Explicit non-duplicates

| Do not create | Already owned by |
|---|---|
| “Fix Admin API key auth” | STAB |
| “Add TenantId everywhere” | MT |
| “Secure connector passwords in UI” | QDB |
| “Build RAG with safe prompts” | GAI |
| “Add correlation ID middleware product” | OBS |
| “Green CI / Docker suite” | BCI |

## Acceptance (SEC02)

- SEC-owned backlog is prioritized and sliced (S2-1…S2-7).
- STAB/MT/GAI/QDB/OBS boundaries remain explicit; watchlist is not re-homed to SEC.
- Each slice names evidence gaps and fail-closed behavior.
- No runtime security change in this prompt.
