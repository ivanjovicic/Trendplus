# Agent Start Here

Updated: 2026-08-13

Read this after `AGENTS.md` and `.github/copilot-instructions.md`.

## Repo mission

Trendplus is a retail decision-support product, not a dashboard collection.

- Analytics must help operators decide what to do.
- Important decisions must expose trustworthy evidence, confidence, reason and next action.
- Empty is not error; unknown is not zero.
- Stale, partial, fallback or insufficient evidence must stay visible.
- Backend business semantics are authoritative.
- Frontend presents and navigates; it does not invent recommendation/confidence truth.
- Pilot safety and deterministic evidence outrank flashy UI or AI.
- Small evidence-backed changes beat broad rewrites.

## Default execution stance

- Assume the user may be offline after assigning the task.
- A direct repository request authorizes ordinary reversible work in Trendplus.
- Do not invent a queue claim or rewrite planning docs for a direct request unless the task is itself about planning/governance.
- Do not pause for routine choices like whether to inspect source, add the nearest focused proof, update the mapped owner doc or verify delivery on `main`.
- Ask only for material business authority, privacy/security/secret decisions, destructive data/schema consequences, production impact, external cost or irreversible actions outside the repo.
- Resolve non-material ambiguity by choosing the smallest same-owner reversible option and recording the assumption.

## Canonical planning read order

1. `AGENTS.md`
2. `.github/copilot-instructions.md`
3. `docs/ai/AGENT_START_HERE.md`
4. `MASTER_ROADMAP.md`
5. the roadmap and owner queue named by the master roadmap
6. only the target prompt's `Read first` documents and scoped source/tests

`MASTER_ROADMAP.md` is the cross-program planning router. Do not choose work from an old addendum completion note, historical audit, or stale "next READY" sentence when it conflicts with the master roadmap and current owner-queue header.

Useful standards:

- `docs/ai/CODEX_TASK_CHECKLIST.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
- `docs/ai/ENCODING_AND_TEXT_SAFETY.md`
- `docs/ai/COMMON_FAILURES_AND_FIXES.md`
- `docs/ai/ANALYTICS_STANDARDS.md`
- `docs/ai/ANALYTICS_TEST_STRATEGY.md`
- `docs/ai/BACKEND_STANDARDS.md`
- `docs/ai/FRONTEND_UX_STANDARDS.md`
- `.ai/RUN_LOG_TEMPLATE.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/VALIDATION_SELECTOR.md`
- `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`

## Program ownership

Use `MASTER_ROADMAP.md` for current READY/blocked/parallel-safe truth. The owner families are:

- Backend CI Repair -> `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`
- Stabilization / Release / current pilot Security -> `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`
- Analytics correctness -> `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md` and its named source queue
- Premium analytics UI -> `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` with `docs/roadmaps/ANALYTICS_UI_PREMIUM_ROADMAP.md`
- Data-source connectors -> `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md`
- Multi-tenancy/shared SaaS isolation -> `docs/ai/MULTITENANCY_PROMPT_QUEUE.md`
- GenAI/RAG/LLM -> `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md`
- Decision Explainability / Recommendation Learning / Decision Timeline -> `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md`
- Performance / Observability / long-term Security Evolution -> `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md`

Do not create another queue when one of these already owns the feature family.

## Priority rule

Preserve the existing program priority declared in `MASTER_ROADMAP.md`. A local `P0` or READY in a future planning queue does not automatically outrank an existing higher-priority program.

Premium UI is a supplemental presentation lane. It may run when path-safe, but it must not displace the main BCI/STAB/RQ/QDB/MT/GAI priority chain or implement business truth in frontend code.

A task may run in parallel only when:

- its queue explicitly marks it parallel-safe;
- it does not overlap the higher-priority task's paths/feature family;
- it does not weaken or bypass the higher-priority gate;
- it is not using planning READY as permission for runtime implementation.

If a higher-priority program has no current READY task, do not resurrect an old DONE/PARTIAL task or infer readiness from historical prose. Follow that program's documented blocker/promotion rule.

## Historical/current separation

- `docs/ai/NEXT_PROMPT_QUEUE.md` is a historical ledger.
- Dated QA/audit/release files are evidence snapshots, not live routers unless explicitly declared current.
- Older addenda may contain stale `Main queue READY ...` or `Next: ... READY` text from the date they were written. Those lines are historical when the current queue header/master roadmap says otherwise.
- Never delete historical evidence merely to remove a conflict. Fix the current canonical routing layer or archive/label the old source.

## Product/architecture direction

Read these when the task changes product direction rather than just implementing an accepted contract:

- `docs/product/PRODUCT_VISION.md`
- `docs/planning/FEATURE_LIFECYCLE.md`
- `docs/architecture/ADRS.md`
- `docs/roadmaps/BUSINESS_ROADMAP.md`

Decision Intelligence is deterministic before AI. DEX/RL/DT own explanation, decision history and outcome learning. GAI may later summarize/cite authoritative decision evidence; it does not become the source of truth.

The safe current customer-isolation fallback remains one deployment/database/storage/cache scope per customer until the shared-SaaS MT release gate passes. `StoreId`, `IDObjekat`, user ID, source connection ID, path or caller-provided header is not tenant authority.

The connector program keeps Trendplus internal storage on PostgreSQL/Npgsql and treats external Access/SQL Server/PostgreSQL/MySQL/API/file systems as read-only import sources unless a future ADR explicitly changes that decision.

## The ten non-negotiables

1. **No fake zero.** Missing/unavailable evidence must not silently become trusted `0`.
2. **No fake green.** Missing/stale/partial/fallback/insufficient evidence is not healthy/fresh/normal/measured.
3. **Backend source of truth.** Backend owns recommendation, confidence, reason, expected-impact and evidence semantics.
4. **No frontend-invented confidence/recommendations.** UI helpers must not create substitute business rules.
5. **Impact vocabulary stays strict.** `expectedImpactRsd` means actionable expected impact; context/exposure/value use different fields.
6. **Units are explicit.** Ratios and percent units are not interchangeable.
7. **Counts are explicit.** Returned/visible rows are not automatically total matching rows.
8. **Date ranges are explicit.** Date-only UI filters normally use half-open whole-day semantics unless the contract says otherwise.
9. **Surface parity is required.** API/table/chart/detail/export/report/action agree or explain the difference.
10. **UTF-8/no mojibake.** Preserve Serbian Latin characters and isolate text cleanup from logic changes.

## Tenant safety gate

Before changing tenant-sensitive behavior, record:

```md
Tenant safety gate:
- Tenant-owned or platform-global:
- Canonical TenantId source:
- Membership/authorization source:
- Missing tenant behavior:
- Mismatched tenant behavior:
- EF/raw SQL paths affected:
- Cache keys/invalidation affected:
- Jobs/outbox/imports affected:
- Documents/storage/exports affected:
- Two-tenant negative tests:
- Migration/backfill needed:
- Dedicated-deployment compatibility:
- Stop condition hit? no / details
```

If a tenant-owned line cannot be answered, narrow to docs/contracts/tests or mark BLOCKED/PARTIAL.

## Analytics safety gate

Before changing analytics behavior, record:

```md
Analytics safety gate:
- Source of truth:
- Contract changed? yes/no
- Unit/denominator:
- True zero case:
- Missing/unknown case:
- No-baseline case:
- Freshness/fallback case:
- Surfaces affected:
- Tests proving table/detail/export/action parity:
- Stop condition hit? no / details
```

If a line cannot be answered, do not guess the runtime contract.

## Direct task workflow

1. Record the work as a direct user request; do not invent a queue claim.
2. Identify the owning source-of-truth service/DTO/endpoint/context and affected architecture layer.
3. Find shared helpers/contracts and the nearest focused proof before creating new ones.
4. Run the tenant/analytics safety gate where relevant.
5. Make the smallest same-owner reversible patch.
6. Select validation through `docs/ai/VALIDATION_SELECTOR.md` and verify delivery on `main` when files changed.
7. Update planning only when the request changes routing/current READY/blocker/milestone truth.

## Queue task workflow

1. Resolve owner program from `MASTER_ROADMAP.md`.
2. Verify the current READY pointer in that owner queue.
3. Confirm no other program owns the same feature family.
4. Identify source-of-truth service/DTO/endpoint/context.
5. Find shared helpers/contracts before creating new ones.
6. Find existing tests and route/surface coverage.
7. Run tenant/analytics safety gate where relevant.
8. Make the smallest scoped patch, including a recorded same-owner mechanical prompt repair when required by acceptance.
9. Select and run exact proof through `docs/ai/VALIDATION_SELECTOR.md`.
10. Update queue status/evidence and master roadmap only if routing/current READY/blocker/milestone truth changed.

## Stop rules

Stop and report rather than expanding scope when:

- source of truth is unclear;
- correct business contract is unclear;
- tenant source/membership authority is unclear;
- caller-controlled tenant identity would become authoritative;
- unresolved tenant would fall back to a default tenant;
- a missing value would become zero/good/fresh/normal/measured;
- frontend would invent backend business semantics;
- table is fixed while detail/export/action remains inconsistent;
- source connector work would expose credentials, arbitrary SQL, write-back or premature checkpoints;
- tenant-owned work lacks a two-tenant negative-test plan;
- a task spills into unrelated modules/programs;
- the same check fails twice without new evidence;
- mojibake cleanup is turning into a mixed logic/text change.

## Queue and planning validation

Run before queue/planning cleanup is called complete:

```text
node scripts/check-agent-instructions.mjs --self-test
node scripts/check-agent-instructions.mjs
node scripts/check-prompt-queues.mjs --self-test
node scripts/check-prompt-queues.mjs
node scripts/check-planning-architecture.mjs --self-test
node scripts/check-planning-architecture.mjs
```

## Final report format

```text
Changed:
- ...

Checks:
- ...

Not done:
- ...

Risks:
- ...

Next:
- ...
```

## Quick reminders

- Do not bypass lazy/Suspense routing just to satisfy tests.
- Do not bypass shared formatters/analytics meta helpers.
- Do not hide stale/partial/fallback/insufficient states.
- Do not treat StoreId/user/source/path/header as tenant authority.
- Do not authorize tenant resources by opaque ID/path alone.
- Do not use process-global mutable tenant state.
- Do not let action/outcome summaries call something measured without measurement evidence.
- Do not let reports/exports silently use different units from on-screen values.
- Do not turn a roadmap idea directly into runtime code; follow `docs/planning/FEATURE_LIFECYCLE.md`.
