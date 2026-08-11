# Trendplus Security Evolution Roadmap

Updated: 2026-08-08  
Status: future security roadmap; current pilot remediation remains owned by STAB  
Owner queue: `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` (`SEC`)

## Purpose

This roadmap exists because long-term security evolution is broader than the current stabilization queue, while still avoiding duplicate ownership.

### Ownership boundary

- `STAB` owns current pilot/release security defects and evidence.
- `MT` owns tenant identity, authorization/isolation and tenant-owned resources for shared SaaS.
- `GAI` owns GenAI/RAG/provider/tool security boundaries.
- `SEC` owns cross-cutting post-STAB security evolution, threat-model maintenance, assurance evidence and gaps that do not already belong to one of those programs.

SEC must not recreate an active STAB, MT or GAI prompt.

## Roadmap

### SEC-1 - Ownership and threat-model reconciliation

Create one current security ownership/threat map covering:

- identities and roles;
- admin compatibility credentials;
- customer/source credentials;
- tenant boundaries;
- import/files/documents/exports;
- workers/outbox/background jobs;
- public operational endpoints;
- connector profiles/mappings/checkpoints;
- future AI retrieval/tools/providers.

The output must link existing STAB/MT/GAI work rather than copying it.

**Status:** SEC01 complete - `docs/architecture/SECURITY_OWNERSHIP_THREAT_MAP.md`.
Pilot authz gaps remain STAB-owned; SEC owns only cross-cutting post-STAB orphans (supply-chain, retention assurance, incident ownership, key-rotation runbook).

### SEC-2 - Secrets and privileged operations assurance

- secret sources and rotation expectations;
- redaction/logging tests;
- admin/privileged route inventory;
- least-privilege service bindings;
- source-connector credential storage/usage;
- emergency access and audit expectations.

**Status:** SEC03 complete - `docs/architecture/PRIVILEGED_SECRETS_ASSURANCE.md`.
**Planning:** prioritized as slice **S2-1** in `docs/architecture/SECURITY_ASSURANCE_BACKLOG_PLAN.md`. QDB remains primary for connector credential *features*.

### SEC-3 - Dependency and supply-chain posture

- dependency vulnerability scanning policy;
- container/base-image ownership where used;
- JavaScript/.NET/Python dependency update strategy;
- signed/reproducible release evidence where practical;
- handling of abandoned/unavailable packages.

**Planning:** slice **S2-2**; CI wiring collaborates with BCI. Current queue prompt: `SEC04` READY after SEC03.

### SEC-4 - Data protection and retention

- classification of customer/source/analytics/decision data;
- retention/deletion expectations;
- backup/restore protection;
- generated reports/files;
- tenant/customer offboarding;
- AI data retention/provider policy when enabled.

**Planning:** slice **S2-3**; consumes MT09 / GAI retention, does not replace them.

### SEC-5 - Authorization assurance

- route-family authorization matrix;
- 401/403 negative tests;
- tenant authorization vs opaque resource ID tests;
- background-job authorization/binding;
- export/document access;
- privileged operational actions.

This phase consumes MT and STAB contracts; it does not invent a parallel identity model.

**Planning:** slice **S2-5**; blocked on STAB watchlist close/accept for overlapping surfaces.

### SEC-6 - Security observability and incident response

- security-relevant audit events;
- failed/forbidden access signals without secret leakage;
- correlation identifiers;
- incident classification/runbooks;
- customer/tenant impact assessment;
- evidence retention appropriate to supported deployments.

**Planning:** slice **S2-4**; consumes OBS unknown≠green rules.

### SEC-7 - SaaS security gate

Before shared SaaS claims:

- MT isolation gate passed;
- tenant-owned connector/storage/job/cache paths verified;
- privileged operation boundaries reviewed;
- backup/restore and offboarding proven;
- operational security alerts and incident ownership exist;
- no known cross-tenant data path remains untested.

**Planning:** slice **S2-6** checklist only after MT gate.

### SEC-8 - AI security gate

Before customer-facing GenAI:

- GAI data-boundary/evaluation gates current;
- retrieval and tool authorization proven;
- prompt/tool logs safe;
- provider secrets isolated;
- no arbitrary SQL/write tools without separate approval design;
- AI disabled mode remains fully supported.

**Planning:** slice **S2-7** checklist only after GAI gates.

## Evidence principles

- missing security evidence is not PASS;
- docs-only claims cannot prove runtime authorization;
- no real secrets/customer payloads in committed evidence;
- threat-model updates should link exact owner prompts/tests;
- security checks should fail closed where identity/scope is unresolved.

## Dependencies

- current STAB queue and release evidence;
- MT architecture/queue for shared SaaS;
- QDB for source credentials and connector persistence;
- OBS for safe audit/incident signals;
- GAI for AI-specific boundaries.

## Non-goals

SEC does not replace STAB, MT or GAI; it does not authorize a broad RBAC rewrite, shared SaaS before tenant gates, or a new external identity provider without a separate product/architecture decision.
