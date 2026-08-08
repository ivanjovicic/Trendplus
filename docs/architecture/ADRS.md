# Trendplus Architecture Decision Records

Updated: 2026-08-08  
Status: canonical ADR register

## ADR structure

New durable architecture decisions should be recorded here or, when they become large enough, split into `docs/architecture/adrs/ADR-NNN-short-title.md` and indexed here.

Each ADR must contain:

- **Status:** Proposed / Accepted / Superseded / Deprecated
- **Date**
- **Context**
- **Decision**
- **Consequences**
- **Alternatives considered**
- **Related roadmap/queue**

Do not use an ADR to track task status. Queues own execution state; ADRs own durable decisions.

---

## ADR-001 - Backend is the source of truth

**Status:** Accepted  
**Date:** 2026-08-08  
**Related:** RQ, DEX, DT, RL, GAI

### Context

Trendplus has multiple user-visible surfaces: API responses, dashboards, detail views, exports, reports and action workflows. If frontend helpers independently invent confidence, recommendation type, expected impact or evidence state, the same business fact can have different meanings across surfaces.

### Decision

The backend is authoritative for business semantics including:

- recommendation/action type;
- reason codes;
- confidence and confidence contributors;
- freshness/data-quality/evidence status;
- expected impact and its meaning;
- decision/outcome state;
- tenant/user authorization decisions.

The frontend may format, filter, navigate and explain authoritative fields, but it must not invent substitute business truth.

### Consequences

- contract changes require backend/tests first;
- frontend fallbacks cannot turn missing evidence into trusted values;
- DEX explanations must derive from backend decision/evidence contracts;
- AI may summarize authoritative data but is not the source of truth.

### Alternatives considered

Frontend-local scoring and recommendation logic were rejected because they create cross-surface drift and weak auditability.

---

## ADR-002 - One customer equals one deployment until shared-SaaS isolation is proven

**Status:** Accepted  
**Date:** 2026-08-08  
**Related:** MT, SEC, QDB

### Context

The current product can isolate customers safely by deployment/database/storage/cache scope. Shared SaaS requires trusted tenant resolution and tenant ownership across persistence, cache, workers, files, reports and connector configuration.

### Decision

Until the MT shared-SaaS release gate is complete, the supported customer isolation model is:

**one customer = one deployment/database/storage/cache operational scope.**

`StoreId`, `IDObjekat`, user ID, source connection ID, path or caller-provided tenant header are not substitutes for canonical tenant identity.

### Consequences

- pilot/first-customer work can proceed without unsafe pseudo-multitenancy;
- no two real customers may share a data plane merely because rows contain store/customer-like fields;
- QDB persistent connector profiles/mappings/checkpoints must become tenant-owned before shared SaaS use.

### Alternatives considered

Implicit tenant identity from store/source IDs was rejected as insufficient authorization/isolation.

---

## ADR-003 - Data connector architecture is read-only source ingestion into the existing Trendplus core

**Status:** Accepted  
**Date:** 2026-08-08  
**Related:** QDB

### Context

Customers may keep operational data in Access, SQL Server, PostgreSQL, MySQL/MariaDB, APIs or files. Trendplus already has a PostgreSQL/Npgsql internal database and an import/validation pipeline.

### Decision

External databases/files are **read-only source connectors** feeding provider-neutral records, mapping/validation and the existing Trendplus persistence/analytics pipeline.

The internal Trendplus database remains PostgreSQL/Npgsql for this roadmap.

Connectors must not execute arbitrary user SQL, write back to customer sources, or advance durable checkpoints before destination commit.

### Consequences

- source portability does not require multi-provider EF migrations;
- provider-specific SQL stays inside connectors;
- mapping is separate from provider mechanics;
- source credentials and checkpoints require explicit ownership/security.

### Alternatives considered

Turning `TrendplusDbContext` into a multi-provider internal database layer was rejected as a different and much larger portability problem.

---

## ADR-004 - Recommendation principles are deterministic, evidence-backed and auditable

**Status:** Accepted  
**Date:** 2026-08-08  
**Related:** RQ, DEX, RL, DT

### Context

A recommendation affects real inventory, supplier or commercial decisions. A recommendation that cannot explain its evidence, confidence and constraints is difficult to trust and impossible to learn from safely.

### Decision

Important recommendations must evolve toward a deterministic contract containing:

- recommendation type/action;
- stable reason codes;
- supporting evidence;
- confidence and its contributors;
- expected impact only when defensible;
- constraints/blockers;
- alternatives where meaningful;
- freshness/data-quality state.

Outcome learning may adjust later confidence/ranking only through explicit measured evidence and testable calibration.

### Consequences

- acceptance/clicks alone are not learning evidence;
- alternatives and evidence snapshots become first-class Decision Intelligence concepts;
- LLM output cannot replace the deterministic recommendation contract.

### Alternatives considered

Opaque recommendation scoring and AI-generated recommendations without deterministic evidence were rejected for the core decision engine.

---

## ADR-005 - No fake zero

**Status:** Accepted  
**Date:** 2026-08-08  
**Related:** RQ, DEX, OBS

### Context

Missing or unavailable numeric evidence can be misread as a legitimate zero and lead to wrong business decisions.

### Decision

Missing, unavailable or uncomputed evidence must never be silently represented as a trustworthy numeric zero.

Use nullability and/or explicit source/evidence status. Tests must distinguish true zero from unknown/unavailable when a feature can encounter both.

### Consequences

- API/UI/export/action surfaces must preserve the distinction;
- caches/fallbacks must not erase missing-evidence state;
- Decision Intelligence evidence chains must retain unknown values.

### Alternatives considered

Universal numeric defaults were rejected because they create false certainty.

---

## ADR-006 - No fake green

**Status:** Accepted  
**Date:** 2026-08-08  
**Related:** RQ, STAB, OBS

### Context

Missing, stale, partial, fallback or insufficient evidence can look safe when default statuses become `good`, `healthy`, `fresh`, `normal`, `measured` or equivalent green UI states.

### Decision

Unknown or insufficient evidence must remain explicitly non-green. Health/readiness, analytics trust and business status may be green only when the required evidence exists and satisfies the declared contract.

### Consequences

- absent telemetry is not healthy telemetry;
- stale/partial analytics must stay visible;
- release evidence cannot infer PASS from lack of errors alone;
- outcome state cannot be called measured without measurement evidence.

### Alternatives considered

Optimistic defaults were rejected because they hide risk and make operations/support less trustworthy.

---

## ADR-007 - Decision Intelligence is a deterministic product layer before AI

**Status:** Accepted  
**Date:** 2026-08-08  
**Related:** DEX, RL, DT, GAI

### Context

Trendplus already contains recommendations, actions and analytics evidence. The next product-value layer is explainable decision support and outcome learning. This should not depend on LLM availability or generate a second source of truth.

### Decision

Decision Intelligence will evolve in this order:

`evidence -> decision -> explanation -> alternatives -> action -> execution -> outcome -> learning`

DEX, RL and DT are deterministic programs. GenAI is optional and downstream. AI may provide cited natural-language exploration/explanation after GAI gates, but authoritative decisions, confidence, evidence, permissions and outcome statistics remain in Trendplus contracts.

### Consequences

- core Decision Intelligence works with AI disabled;
- GAI can consume stable decision/evidence APIs rather than inventing them;
- evaluation of AI explanations can compare against deterministic evidence.

### Alternatives considered

An LLM-first decision engine was rejected because it would couple product correctness to model behavior and make evidence/authorization harder to prove.

## ADR governance

When a future roadmap changes one of these decisions, create a superseding ADR rather than silently editing the historical decision into a different meaning. Minor clarifications that do not change the decision may update this register with a dated note.