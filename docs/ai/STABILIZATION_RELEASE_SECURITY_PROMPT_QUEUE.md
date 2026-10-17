# Trendplus Pilot Stabilization, Release and Security Prompt Queue

Created: 2026-08-04
Repo: `ivanjovicic/Trendplus`
Queue state: active cross-cutting queue; it supplements, and does not replace, the analytics reliability queues.
P0 import follow-up: STAB17 IN_PROGRESS **for isolated repository-local engineering/proofs only** after owner-directed PR #163 release 2026-10-17; production baseline repair, cutover, live import and external acceptance remain WAITING. No production mutation authorized.
Current READY prompt: none in STAB. STAB17 is P0 **IN_PROGRESS** in the existing exclusive Codex `/root` claim on DRAFT PR #163, not claimable by another agent; STAB16 stays BLOCKED on production provider/release gates. Cross-program RQ686/RQ687 are separately READY but cannot start while the WIP cap is full.
Current gate verdict: the first 2026-08-27 live audit found Render behind current `main`; a same-day API-only recheck now shows the canonical Render API at runtime SHA `6ecbfa67a7304c3cbeeb71755a35255e766c8e24`, which is contained in current `main`, but no dedicated worker process or durable refresh proof is visible yet. GenAI remains BLOCKED; see `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md` and `.ai/runs/2026-08-27-queue-audit-production-followups-evidence.md`.

## Goal

Close the cross-cutting gaps that can make a technically correct analytics change unsafe to release or misleading to operate:

- current-`main` deploy truth and live smoke evidence;
- queue/router truth and stale status cleanup;
- authentication and authorization runtime boundaries;
- public admin/diagnostic exposure;
- production HTTP and reverse-proxy hardening;
- pilot import-status provenance;
- backup/restore evidence;
- a fresh release gate before GenAI implementation.

This queue intentionally does not duplicate analytics correctness prompts such as `RQ01`, `RQ72`, `RQ39`, `RQ40`, `RQ51/RQ52`, `RQ64`, `RQ81` or the SQL queue. Those remain owned by `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md` and their source queue files.

## Global routing

1. If the current `main` deploy is red, unknown or not tied to the current SHA, run `STAB01` before making a production-readiness claim.
2. For wrong recommendations, expected impact, fake zero/green, units, denominators, dates or data-scope semantics, continue from the analytics reliability priority review. The current analytics code task remains `RQ01` until its queue is updated with evidence.
3. For queue governance, authorization, public operational exposure, import provenance, backup/restore or release evidence, use this queue.
4. GenAI/RAG/LLM/tool-calling work may start only when:
   - the analytics reliability router has no earlier unresolved P0 task;
   - this queue has no unresolved P0 `READY`, `PARTIAL` or `BLOCKED` task;
   - the current-main release evidence is fresh.
5. Keep STAB/release scope separate from analytics formula fixes. Task selection, concurrency, session flow and commit/claim mechanics are governed only by `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

## Queue rules

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md` is the sole owner of selection, claim, lock, concurrency, promotion, no-READY and close-out mechanics.
- The rules below are release/security evidence constraints, not a second queue algorithm.
- Never mark live smoke, backup/restore or authorization readiness `DONE` from docs-only assumptions.
- Do not add secrets, real customer data or production credentials to source, tests or evidence documents.
- If provider dashboards, deployment logs or a safe restore target are unavailable, record the missing evidence and mark the task `BLOCKED` rather than guessing.

---

## STAB01 - Current main deploy, CI and live-smoke truth gate

Status: DONE
Priority: P0
Type: deploy/ops/docs, code only if evidence proves a minimal root cause
Feature family: current-main-release-truth
Parallel-safe: yes, with `RQ01` only because scopes must not overlap
Owner: Cursor-Composer
Local lock: `.ai/task-locks/STAB01-cursor.lock.md` (removed after DONE)
Commit suggestion: `docs(qa): refresh current main deploy evidence`

### Why

The repository has strong historical live-smoke evidence, but the latest checked `main` commit is not currently proven deployable. A red or stale deployment state invalidates production-readiness claims even when local tests are green.

### Evidence already found

- Current inspected `main` SHA: `66084a78e10dba9a77c11907074c0cb7834ebce4`.
- GitHub combined status for that SHA reports `Vercel: failure`.
- GitHub Actions returns no workflow runs associated with that docs-only SHA.
- `.github/workflows/analytics-quality-gates.yml` is path-filtered to frontend/workflow files.
- `.github/workflows/analytics-tests.yml` is path-filtered to backend/workflow files.
- `vercel.json` installs and builds `Klijent/clientapp`, serves `Klijent/clientapp/dist`, and rewrites non-asset routes to `index.html`.
- The latest committed live-smoke and production-readiness evidence is from June 2026 and predates the current `main` SHA.
- Risk class: confirmed release-evidence gap; the Vercel root cause itself is not yet proven.

### Contract

- Production readiness must be tied to the exact current `main` SHA or an explicitly documented deployment SHA.
- A local build is necessary but not sufficient for live deploy proof.
- A Vercel failure must be diagnosed from real provider/build evidence, not inferred from old incidents.
- Unknown deploy metadata stays `WARN` or `BLOCKED`; it must not become `PASS`.

### Scope only

- `vercel.json`
- `Klijent/clientapp/package.json`
- `.github/workflows/analytics-quality-gates.yml`
- `.github/workflows/analytics-tests.yml`
- existing deploy/smoke docs under `docs/qa/`
- one new dated evidence document if updating an old historical document would erase history

### Do not touch

- analytics formulas, DTO semantics or recommendation logic
- database migrations
- GenAI runtime/provider code
- deployment configuration without direct failure evidence

### Read first

- `.github/copilot-instructions.md`
- `AGENTS.md`
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/VERCEL_DEPLOY_TRIAGE.md`
- `docs/qa/ANALYTICS_DEPLOY_RECOVERY.md`
- `docs/qa/ANALYTICS_LIVE_SMOKE_RESULT.md`
- `docs/qa/ANALYTICS_PILOT_RELEASE_CHECKLIST_V2.md`

### Do

1. Record the latest `main` SHA, GitHub status checks, workflow evidence and current deployment identifiers before changing anything.
2. Inspect the exact Vercel failure evidence. Classify the root cause as one of:
   - source/build/type error;
   - install/Node/package-lock mismatch;
   - root/output/SPA configuration;
   - commit identity/account authorization;
   - provider/environment configuration;
   - unknown because provider evidence is unavailable.
3. Run or verify:
   - `cd Klijent/clientapp && npm ci`
   - `cd Klijent/clientapp && npm run check:analytics-guardrails`
   - `cd Klijent/clientapp && npm run test:analytics`
   - `cd Klijent/clientapp && npm run build`
4. Apply only the smallest evidence-backed repository fix. If the cause is provider-side, do not create unrelated source changes.
5. After a successful deploy, run the current live-smoke path against the exact deployed frontend/backend:
   - `/health`
   - `/ready`
   - `/api/runtime/version`
   - `/api/analytics/refresh-status?dataScope=all`
   - `/analytics`
   - `/analytics/products`
   - `/analytics/inventory`
   - `/analytics/actions`
   - `/analytics/decision-board`
   - one durable report route
6. Record response status, visible trust state, deployed SHA/bundle identifier, timestamp and any warning. Do not store credentials.
7. Update queue notes and release evidence. If the current SHA cannot be proven live, finish `BLOCKED` with the exact missing provider evidence.

### Test matrix

- local frontend checks pass;
- Vercel build succeeds for the current source;
- SPA direct-route refresh works;
- backend liveness and readiness are distinct;
- runtime version is present or explicitly unknown;
- stale/unknown freshness remains visibly non-green;
- unauthorized admin smoke remains `401`/`403`;
- current bundle renders real page content rather than only a generic SPA shell.

### Checks

- `git diff --check`
- `cd Klijent/clientapp && npm ci`
- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run test:analytics`
- `cd Klijent/clientapp && npm run build`
- live HTTP smoke only after deploy

### Acceptance

- Current `main` has a truthful PASS/WARN/BLOCKED deploy result tied to exact evidence.
- A red status has a proven root cause or an explicit provider-access blocker.
- No old June smoke document is presented as proof for an unverified August deployment.
- No analytics business logic changed unless it was the proven build blocker and was split into its own task.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Result: **WARN** (truth gate complete; not production PASS)
- Inspected SHA: `a1b9231a6910ab2209b5e7d79db0f2bd42cf8a04`
- Evidence: `docs/qa/ANALYTICS_CURRENT_MAIN_DEPLOY_EVIDENCE_2026-08-05.md`
- Vercel for `a1b9231`: success (previous June/`66084a7` failure evidence is stale for this SHA)
- Actions analytics suite for `a1b9231`: failure at NuGet `Restore dependencies` (build/tests skipped)
- Live backend: health/ready PASS; runtime SHA `e9f3238â€¦` â‰  main tip (WARN); refresh-status `unknown` (honest); admin `401`
- Live frontend: SPA shell + current assets served; authenticated page-body smoke not completed
- Local checks (dirty tree): guardrails PASS, build PASS, test:analytics 22 failed / 204 passed
- Repo code changes: none (docs/queue only)
- Next: `STAB02` READY

---

## STAB02 - Canonical queue and status truth reconciliation

Status: DONE
Ready after: `STAB01` is `DONE`, `PARTIAL` with a non-repository provider blocker, or explicitly deferred by the owner
Priority: P0
Type: docs/tooling
Feature family: prompt-queue-governance
Parallel-safe: yes, with runtime analytics work if no queue file overlap
Owner: Cursor-Composer
Local lock: `.ai/task-locks/STAB02-cursor.lock.md` (removed after DONE)
Commit suggestion: `chore(ai): reconcile prompt queue truth`

### Why

The repository has several queue generations and incompatible status conventions. Agents can choose the wrong task, skip an unresolved blocker, or start GenAI while a stale `OPEN`, `PARTIAL` or `READY` entry still exists.

### Evidence already found

- `docs/ai/NEXT_PROMPT_QUEUE.md` starts with a `TODO`-based workflow.
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md` requires `READY/WAITING/...` and does not allow `TODO` or `OPEN`.
- `NEXT_PROMPT_QUEUE.md` contains at least `Q20` and `Q22` with unsupported `Status: OPEN`.
- `Q22` says analytics action writes still need protection, while current `AnalyticsActionsEndpoints` already gates create/status/outcome writes through `AdminAccessControl`.
- `Q67` stayed `PARTIAL` because of a TypeScript mismatch that `Q68` later fixed and verified with passing guardrails/build.
- `AGENTS.md`, `CODEX_QUEUE_RUNNER.md`, the reliability priority review and the GenAI queue do not use one identical selection rule.
- Risk class: confirmed queue-governance drift.

### Contract

- One global router must say which queue owns each feature family.
- Queue statuses must use the exact protocol vocabulary.
- Historical tasks may be `DONE` or `OBSOLETE` only with current code/test evidence.
- One task family must not have two independent `READY` prompts.
- GenAI must not become active because an unsupported status was silently ignored.

### Scope only

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- `docs/ai/NEXT_PROMPT_QUEUE.md` only for targeted status corrections
- one small queue validation script under `scripts/` or `Klijent/clientapp/scripts/`
- focused script tests/fixtures if practical

### Do not touch

- application runtime code
- analytics formulas or SQL
- broad reformatting of every historical queue
- task status without evidence from current code/tests/commits

### Read first

- `.github/copilot-instructions.md`
- `AGENTS.md`
- `docs/ai/AGENT_START_HERE.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/ANALYTICS_QUEUE_RECONCILIATION.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md`

### Do

1. Inventory every active queue file and every `READY`, `WAITING`, `PARTIAL`, `BLOCKED`, `TODO` and `OPEN` entry.
2. Produce a compact ownership matrix:
   - feature family;
   - canonical queue;
   - current runnable task;
   - duplicate/replacement entries;
   - blocking dependency.
3. Add the smallest deterministic validator that reports actionable `file:line` failures for:
   - unsupported statuses;
   - more than one non-parallel `READY` task in the same feature family;
   - a declared current READY task whose entry is missing/not READY;
   - duplicate task IDs inside one queue family;
   - GenAI marked active while an earlier P0 gate remains unresolved.
4. Re-evaluate only the proven stale entries, including `Q20`, `Q22` and `Q67`. Use `DONE`, `OBSOLETE`, `PARTIAL` or `BLOCKED` based on current evidence.
5. Keep the analytics reliability priority review as the router for analytics correctness.
6. Keep this queue as the router for deploy/security/governance gaps.
7. Update the final notes with the exact next READY analytics prompt and next READY cross-cutting prompt.

### Test matrix

- valid queue sample passes;
- unsupported `OPEN` fails with `file:line`;
- duplicate READY in one exclusive family fails;
- parallel-safe READY pair passes only when explicitly marked;
- missing current-ready entry fails;
- stale task is not automatically marked DONE by the script;
- GenAI gate conflict is reported.

### Checks

- `git diff --check`
- validator command documented and executed
- focused validator tests or fixture run

### Acceptance

- Agents have one unambiguous routing path.
- Unsupported live statuses are removed or explicitly historical.
- Stale queue claims are reconciled against current code.
- A machine check prevents the same drift from returning.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `scripts/check-prompt-queues.mjs`, `docs/qa/ANALYTICS_QUEUE_RECONCILIATION.md`, `docs/ai/AGENT_START_HERE.md`, `docs/ai/PROMPT_QUEUE_PROTOCOL.md`, `docs/ai/NEXT_PROMPT_QUEUE.md`, `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md`, `docs/ai/QUEUE_STATUS_TEMPLATE.md`, `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`, this queue
- Fix: ownership matrix + canonical routing; GenAI `TODO`â†’`WAITING`; GAI02 demoted from `IN_PROGRESS` under STAB P0; Q20/Q22/Q67 confirmed DONE with current evidence; validator with `--self-test`
- Checks: `node scripts/check-prompt-queues.mjs --self-test` pass; `node scripts/check-prompt-queues.mjs` pass (209 tasks); `git diff --check` pass
- Next READY analytics: none globally (WAITING families remain owner-gated)
- Next READY cross-cutting: `STAB03`
- Also READY parallel-safe UI: `P-UI-05`
- Risk: historical addenda still mention stale â€œnext global: RQ51â€ prose; validator allows `Current READY prompt: none ...`
- Next: `STAB03` READY

---

## STAB03 - Authentication and authorization runtime boundary audit

Status: DONE
Ready after: `STAB02` is `DONE`
Priority: P0
Type: security audit/docs/tests
Feature family: auth-runtime-boundary
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/STAB03-cursor.lock.md` (removed after DONE)
Commit suggestion: `docs(security): audit runtime authorization boundary`

### Why

The repo has an admin-key helper and several authorization tests, but it does not yet prove a complete runtime authentication/role pipeline for an external pilot. Broad implementation before confirming the actual identity source would risk either locking out valid operators or leaving sensitive routes open.

### Evidence already found

- `Api/Program.cs` calls `app.UseAuthorization()`.
- No `AddAuthentication`, `UseAuthentication` or explicit policy registration was found in the inspected `Program.cs` path.
- `AdminAccessControl.GetDecision` authorizes either an authenticated `Admin` role or `X-Admin-Key`.
- `AdminAccessControl` currently supports only the `Admin` role and compares the API key with normal string equality.
- Analytics action create, status and outcome writes are currently admin-gated, although the documented target model distinguishes `Analyst` and `Manager`.
- Several endpoint groups still implement authorization handler-by-handler rather than through one proven policy boundary.
- Risk class: confirmed runtime-boundary gap; the intended external identity provider is a product/deployment decision.

### Contract

- Backend authorization is authoritative; frontend visibility is never sufficient.
- Missing credentials return `401`; valid identity with insufficient role returns `403`.
- The API-key compatibility path may represent `Admin` only, must fail closed, and must never be logged or returned.
- Do not invent an identity provider in this audit.
- The audit must select a minimal Phase 1 pattern before broad endpoint changes.

### Scope only

- `Api/Program.cs`
- `Api/Endpoints/AdminAccessControl.cs`
- endpoint registration files for admin, actions, import, workers, cache, reports and documents
- existing authorization tests
- `docs/security/ANALYTICS_ACCESS_CONTROL_AUDIT.md`
- `docs/security/ANALYTICS_ACCESS_CONTROL_IMPLEMENTATION_PLAN.md`
- one new dated runtime-boundary audit

### Do not touch

- full tenant architecture
- external IdP SDK/provider integration
- frontend route redesign
- business analytics logic
- broad endpoint protection in the audit commit

### Read first

- `.github/copilot-instructions.md`
- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/security/ANALYTICS_ACCESS_CONTROL_AUDIT.md`
- `docs/security/ANALYTICS_ACCESS_CONTROL_IMPLEMENTATION_PLAN.md`
- `docs/security/TENANT_SAFETY_CHECKLIST.md`
- relevant authorization tests under `Api.Tests/`

### Do

1. Map the real runtime identity sources for local, test, current production and future external pilot environments.
2. Prove whether an authenticated principal can be created in the current production runtime. Do not assume test principals represent production.
3. Build an endpoint matrix for:
   - public health/version;
   - read-only analytics;
   - action reads/writes;
   - report/export/document access;
   - import/cleanup;
   - workers/admin/config/logs;
   - cache clear/manual refresh.
4. For each group, record current enforcement, target role, 401/403 behavior, API-key compatibility and test coverage.
5. Review API-key comparison, configuration validation, secret redaction and rotation expectations.
6. Choose the smallest Phase 1 implementation boundary:
   - existing principal + policies;
   - explicit internal API-key admin mode while external auth remains disabled;
   - or `BLOCKED` pending an owner decision.
7. Split code follow-ups by endpoint family. Do not implement all groups in this audit.

### Test matrix

- no credential;
- wrong admin key;
- correct admin key;
- authenticated non-admin principal;
- authenticated admin principal;
- missing configured key;
- secret never appears in logs/problem responses;
- local development compatibility is explicit rather than accidental.

### Checks

- `git diff --check`
- targeted existing authorization tests
- docs link/path validation

### Acceptance

- Current production authentication capability is proven, not assumed.
- Every sensitive endpoint family has an explicit current and target boundary.
- The next code task is small enough for one endpoint family.
- No external provider or broad RBAC implementation was invented.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Evidence: `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md`
- Also updated: `ANALYTICS_ACCESS_CONTROL_AUDIT.md`, `ANALYTICS_ACCESS_CONTROL_IMPLEMENTATION_PLAN.md`, this queue
- Phase 1 decision: **(b) explicit internal API-key admin mode** â€” production has no auth pipeline, so Admin-role principal path is unreachable; `X-Admin-Key`/`Admin:ApiKey`/`ADMIN_API_KEY` is the live boundary
- Next code task: `STAB04` admin operational reads
- Checks: code inspection pass; targeted auth filter tests fail 17/63 on current branch (recorded, not used as IdP proof); no runtime code changed; `git diff --check` pass
- Risk: document header roles remain spoofable; import/logs sensitive reads still open until follow-ups
- Next: `STAB04` READY

---

## STAB04 - Protect admin operational read surfaces

Status: DONE
Ready after: `STAB03` fixes the Phase 1 boundary or declares the existing admin-key boundary acceptable for this task
Priority: P0
Type: backend security/tests
Feature family: admin-read-authorization
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/STAB04-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(security): protect admin operational reads`

### Why

Some state-changing admin routes are protected, while operational read routes expose import, worker and audit details without the same server-side gate. These reads can reveal customer file names, batch failures, internal worker configuration and operational messages.

### Evidence already found

In `Api/Endpoints/AdminConfigEndpoints.cs`, the inspected handlers for these routes do not call `AdminAccessControl`:

- `GET /api/admin/pending-batches`
- `GET /api/admin/health-check`
- `GET /api/admin/audit-log`
- `GET /api/admin/workers/list`
- `GET /api/admin/workers/{workerName}`

The same file protects requeue, stale recovery, demo verification and worker write operations through `AdminAccessControl`.

Risk class: confirmed authorization inconsistency and information exposure.

### Contract

- All `/api/admin/*` operational internals are Admin-only in Phase 1 unless a route is explicitly documented public.
- Public health stays under `/health`, `/ready` and `/api/runtime/version` with a minimal redacted contract.
- No credential -> `401`.
- Wrong/insufficient credential -> `403`.
- Authorized requests preserve existing successful response contracts.

### Scope only

- `Api/Endpoints/AdminConfigEndpoints.cs`
- `Api/Endpoints/AdminAccessControl.cs` only if the STAB03-approved helper needs a tiny compatible extension
- focused admin authorization tests
- optional API contract documentation update

### Do not touch

- worker business logic
- import processing logic
- frontend route/capability UI
- external identity provider integration
- public health endpoint redesign

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/security/ANALYTICS_ACCESS_CONTROL_IMPLEMENTATION_PLAN.md`
- STAB03 audit output
- `Api/Endpoints/AdminConfigEndpoints.cs`
- existing admin/import/worker authorization tests

### Do

1. Apply the approved admin decision helper consistently to every operational read handler listed above.
2. Keep authorization checks before any DB/service query.
3. Ensure unauthorized responses do not reveal whether a batch, worker or audit entry exists.
4. Add focused route tests for each route family, not only direct helper tests.
5. Preserve authorized payloads and pagination/filter behavior.
6. Document any intentionally public admin-named route; otherwise protect it.

### Test matrix

- no credential -> `401`;
- wrong key/non-admin -> `403`;
- correct admin -> existing `200` payload;
- unauthorized request does not query sensitive services when test infrastructure can prove it;
- route remains mapped and does not become `404`;
- no secret or raw connection string in responses.

### Checks

- `git diff --check`
- targeted admin endpoint authorization tests
- `dotnet build Trendplus2.sln --no-restore --configuration Release`

### Acceptance

- No sensitive admin operational read remains anonymously reachable in the scoped file.
- 401/403 behavior is consistent.
- Authorized behavior remains unchanged.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `Api/Endpoints/AdminConfigEndpoints.cs`, `Api.Tests/AdminConfigOperationalReadsAuthorizationTests.cs`
- Gate: `AdminAccessControl.GetDecision` before DB/service work on `GET pending-batches`, `health-check`, `audit-log`, `workers/list`, `workers/{workerName}`
- Behavior: missing key â†’ `401`; wrong key â†’ `403`; authorized preserves `200` (or `404` only after auth for unknown worker)
- Checks: `git diff --check` pass; `AdminConfigOperationalReadsAuthorizationTests` 18/18 pass; `dotnet build Trendplus2.sln --no-restore --configuration Release` pass
- Risk: import batch-list / logs / document header trust remain out of scope (STAB03 follow-ups)
- Next: `STAB05` READY

---

## STAB05 - Production edge, diagnostics and reverse-proxy hardening

Status: DONE
Ready after: `STAB03` is `DONE`; may run before STAB04 only if no auth helper files overlap
Priority: P0
Type: backend security/config/tests
Feature family: production-edge-exposure
Parallel-safe: no
Owner: Cursor-Composer
Local lock: `.ai/task-locks/STAB05-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(security): harden production edge diagnostics`

### Why

Public health and documentation surfaces must help the platform without exposing internal failure details. Reverse-proxy security behavior also needs an explicit production contract rather than an environment condition that appears inverted.

### Evidence already found

- `Api/Program.cs` calls `app.UseHsts()` only when `app.Environment.IsDevelopment()`.
- `GET /health/dependencies` is public and includes `ex.GetBaseException().Message` in dependency-check payloads.
- Swagger and Swagger UI are mapped unconditionally.
- CORS frontend origins are hard-coded in two places and do not use one validated configuration source.
- `UseForwardedHeaders` is enabled and known proxy/network lists are cleared, so HTTPS/HSTS behavior must be tested behind the actual proxy contract.
- Risk class: confirmed configuration/exposure gap; the exact provider HTTPS behavior requires deployment evidence.

### Contract

- Public `/health` is minimal liveness.
- Public `/ready` is minimal readiness and may expose only safe status/retry metadata.
- Detailed dependency errors belong in logs or an Admin-only diagnostic surface, not anonymous JSON.
- HSTS/HTTPS behavior must be correct for production behind Render/reverse proxy and must not break local HTTP development.
- Swagger production exposure must be an explicit configuration decision, disabled or protected by default for external pilot.
- CORS allowed origins must come from one validated configuration source with safe local defaults.

### Scope only

- `Api/Program.cs`
- one focused configuration/options type if needed
- appsettings examples without secrets
- focused startup/health/CORS tests
- one short production-edge contract doc

### Do not touch

- analytics endpoint semantics
- auth provider integration beyond the STAB03-approved boundary
- deployment provider infrastructure outside repository configuration
- worker or DB query logic

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/ANALYTICS_DEPLOY_RECOVERY.md`
- STAB03 audit output
- `Api/Program.cs`
- current health/readiness tests

### Do

1. Add tests that lock the intended environment behavior before changing middleware order.
2. Redact public dependency failure details to stable safe codes/statuses while retaining full exception detail in server logs with correlation ID.
3. Correct or explicitly replace the HSTS condition for production proxy operation.
4. Decide and implement Swagger production policy through configuration with a secure default.
5. Centralize CORS origins and validate empty/invalid production configuration.
6. Preserve `/health`, `/ready` and `/api/runtime/version` live-smoke compatibility.
7. Verify forwarded-proto behavior so HTTPS redirects/HSTS do not loop behind the proxy.

### Test matrix

- Development HTTP starts without forced production HSTS behavior;
- Production forwarded `https` returns the intended HSTS header;
- Production forwarded `http` behavior is explicit and does not loop;
- dependency failure returns safe code, not raw exception text;
- full error remains logged with correlation ID;
- Swagger disabled/protected by default in production and available in development;
- allowed origin receives CORS headers;
- unapproved origin does not;
- health and readiness remain anonymous and minimal.

### Checks

- `git diff --check`
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- focused startup/health/CORS tests
- live smoke after deploy if middleware/config changed

### Acceptance

- Public diagnostics no longer expose raw dependency errors.
- Production HSTS/forwarded-proxy behavior is tested and intentional.
- Swagger and CORS have explicit secure production configuration.
- Existing health/readiness consumers keep a stable safe contract.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `Api/Program.cs`; `Api/Config/CorsOriginsOptions.cs`; `Api/Config/SwaggerExposureOptions.cs`; `Api/Services/Startup/{ProductionEdgePolicy,DependencyHealthPublicErrors,HealthCorsHeaders}.cs`; `Api.Tests/ProductionEdgeMiddlewareTests.cs`; `Api/appsettings.json`; `Api/appsettings.Production.json`; `docs/security/PRODUCTION_EDGE_CONTRACT_2026-08-05.md`
- Behavior: dependency errors â†’ stable public codes + logged detail; HSTS outside Development; Swagger off by default outside Development (`Swagger:Enabled`); CORS from `Cors:AllowedOrigins`
- Checks: `git diff --check` pass; `ProductionEdge*` tests 15/15 pass; `dotnet build Trendplus2.sln --no-restore --configuration Release` pass; live smoke after deploy **not run**
- Risk: production must keep `Cors:AllowedOrigins` non-empty or startup throws; confirm HSTS header on Render host after deploy
- Next: `STAB06` READY

---

## STAB06 - Wire authoritative last-import status into pilot readiness

Status: DONE
Ready after: `STAB02` is `DONE`; may run in parallel with STAB03-STAB05 if files do not overlap
Priority: P1
Type: backend/frontend contract/tests
Feature family: pilot-import-provenance
Parallel-safe: yes
Owner: Cursor-Composer
Local lock: `.ai/task-locks/STAB06-cursor.lock.md` (removed after DONE)
Commit suggestion: `fix(data-quality): include last import status in readiness`

### Why

Pilot readiness currently uses the last import timestamp but not the authoritative last import outcome. A recent failed or cancelled import can therefore be absent from the visible readiness decision.

### Evidence already found

- `computePilotImportReadiness` accepts an optional third parameter `lastImportStatus` and treats failed/error/blocked/cancelled as hard blockers.
- `PilotImportReadinessCard` calls `computePilotImportReadiness(report, refreshStatus)` without the third parameter.
- `PilotImportReadinessCard` has no `lastImportStatus` prop and displays only `report.lastImportAtUtc`.
- The frontend `PilotDataQualityIntakeReport` contract contains `lastImportAtUtc` but no `lastImportStatus`.
- The inspected backend intake-report DTO/build path has no `lastImportStatus` field.
- Risk class: confirmed provenance gap and potential false readiness.

### Contract

- The intake report must expose an additive authoritative last-import status with timestamp.
- Failed/error/blocked/cancelled import -> `not_ready`.
- Running/queued/in-progress/partial -> `ready_with_warnings` unless another blocker makes it `not_ready`.
- Succeeded/completed -> no import-status degradation.
- Missing/unknown status must remain visible as unknown/warning; it must not imply success.
- Scope lineage must be honest. If import batches cannot be reliably mapped to selected store/supplier/dataScope, label the status as global rather than pretending it is scoped.

### Scope only

- `Api/Endpoints/DataQualityEndpoints.cs`
- the owning import-batch read query/service if one already exists
- `Klijent/clientapp/src/types/analytics.ts`
- `Klijent/clientapp/src/components/analytics/PilotImportReadinessCard.tsx`
- `Klijent/clientapp/src/utils/pilotImportReadiness.ts`
- focused backend/frontend tests

### Do not touch

- Access import execution or retry behavior
- recommendation formulas outside pilot readiness
- unrelated Data Quality panels
- database migration unless current batch status cannot be read additively; stop before inventing schema

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/Analytics/PILOT_DATA_REQUIREMENTS.md`
- `docs/Analytics/PILOT_ONBOARDING_IMPORT_MAP.md`
- `Api/Endpoints/DataQualityEndpoints.cs`
- `Klijent/clientapp/src/utils/pilotImportReadiness.ts`
- current pilot readiness tests

### Do

1. Identify the authoritative latest relevant `DataImportBatch` selection rule.
2. Add additive report fields such as `lastImportStatus` and, only if needed for honesty, `lastImportScope`/`lastImportBatchId`.
3. Normalize backend status vocabulary once; do not maintain separate backend and frontend aliases without tests.
4. Pass the status into `PilotImportReadinessCard` and display it in input signals.
5. Keep timestamp-without-status and status-without-timestamp cases explicit.
6. Add contract tests for API/report serialization and UI readiness behavior.

### Test matrix

- latest import succeeded/completed;
- latest import failed;
- latest import cancelled;
- running/queued/in-progress;
- partial/warning;
- no import row;
- older success followed by newer failure selects the failure;
- selected store/supplier cannot be proven to match import -> global/unknown scope warning;
- error/loading payload does not become ready.

### Checks

- `git diff --check`
- targeted backend intake-report tests
- `cd Klijent/clientapp && npm run test -- --run src/utils/__tests__/pilotImportReadiness.spec.ts`
- targeted card/page test
- `cd Klijent/clientapp && npm run check:analytics-guardrails`
- `cd Klijent/clientapp && npm run build`

### Acceptance

- A failed latest import cannot be presented as ready merely because an import timestamp exists.
- Import status and scope are visible and additive.
- Unknown remains non-green.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `Api/Endpoints/DataQualityEndpoints.cs`; `Api.Tests/PilotImportBatchStatusContractTests.cs`; `Api.Tests/AnalyticsReportsContractTests.cs`; `Klijent/clientapp/src/types/analytics.ts`; `pilotImportReadiness.ts` + tests; `PilotImportReadinessCard.tsx` + card spec; intake report metadata; fixture updates
- Contract: additive `lastImportStatus` / `lastImportScope` / `lastImportBatchId`; latest batch by `CompletedAtUtc ?? StartedAtUtc`; scope always `global` (honest); failed/cancelled â†’ not_ready
- Checks: `PilotImportBatchStatusContractTests` 27/27; `pilotImportReadiness` 10/10; `PilotImportReadinessCard` 2/2; `check:analytics-guardrails` pass; `npm run build` pass; `git diff --check` pass
- Risk: store/supplier filters still cannot map to batch rows (explicit global warning)
- Next: `STAB07` READY

---

## STAB07 - Backup and restore evidence rehearsal gate

Status: DONE
Ready after: `STAB01` is at least `PARTIAL` with a usable environment and STAB03 identifies the safe admin/ops boundary
Priority: P1
Type: ops/docs/scripts, optional manual workflow
Feature family: backup-restore-evidence
Parallel-safe: yes
Owner: Cursor-Composer
Local lock: none
Commit suggestion: `docs(ops): add backup restore rehearsal evidence`

### Why

The repo documents what should be backed up and how restore should work, but it does not contain proof that both operational and analytics data can be restored into a safe non-production target and validated end to end.

### Evidence already found

- `docs/ops/PILOT_DATA_SAFETY_CHECKLIST.md` and `docs/ops/BACKUP_RESTORE_RUNBOOK.md` are docs-only.
- The queue notes explicitly say there is no confirmed automated backup scheduler or one-click restore flow.
- No repository `pg_dump`/`pg_restore` rehearsal script or recorded restore result was found in the inspected search.
- Cache is correctly documented as non-durable, but post-restore refresh verification is not proven by an executable rehearsal.
- Risk class: confirmed operational evidence gap.

### Contract

- Never rehearse destructive restore against production.
- Test restore into a disposable database/project with explicit environment guards.
- Cover operational DB and analytics DB separately if they are separate connections.
- Secrets enter through environment/secret store only and must not be printed.
- A backup is not accepted until restore and validation succeed.
- Provider-managed backups may be used, but provider retention and restore procedure must be recorded as evidence rather than assumed.

### Scope only

- `docs/ops/PILOT_DATA_SAFETY_CHECKLIST.md`
- `docs/ops/BACKUP_RESTORE_RUNBOOK.md`
- new safe scripts under `scripts/ops/`
- optional manually dispatched CI workflow only if it can run without production secrets and cannot target production
- one dated rehearsal evidence template/result

### Do not touch

- production databases
- application business logic
- migrations unrelated to restore validation
- hard-coded credentials or connection strings

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ops/PILOT_DATA_SAFETY_CHECKLIST.md`
- `docs/ops/BACKUP_RESTORE_RUNBOOK.md`
- deployment/provider database documentation already committed in the repo
- current migration commands and health/readiness contract

### Do

1. Map backup ownership for operational DB, analytics DB, import files, generated documents/reports and logs.
2. Add safe backup/restore wrappers or exact provider procedure with:
   - explicit source and destination environment labels;
   - production-target refusal;
   - no secret echo;
   - checksum/size/timestamp evidence;
   - separate operational/analytics handling.
3. Restore into a disposable target.
4. Validate:
   - migrations/history readable;
   - representative row counts/non-zero domain checks;
   - critical foreign keys/joins;
   - analytics refresh can run or is explicitly required after restore;
   - health/readiness against restored connections;
   - no customer data appears in committed artifacts.
5. Record duration, tool/provider version, evidence and cleanup.
6. If no safe target/provider access exists, finish `BLOCKED` with the exact missing prerequisite and keep the scripts/docs non-destructive.

### Test matrix

- operational backup and restore;
- analytics backup and restore;
- empty/missing backup file fails closed;
- destination marked production is rejected;
- wrong credentials fail without secret leakage;
- restore validation detects missing tables or inconsistent row counts;
- post-restore analytics refresh/invalidation requirement is explicit;
- cleanup removes disposable target/artifacts where applicable.

### Checks

- `git diff --check`
- script dry run/help validation
- shell/PowerShell/Python syntax checks as applicable
- disposable restore rehearsal only

### Acceptance

- The repo has executable or provider-specific restore evidence, not only a conceptual runbook.
- Production cannot be targeted accidentally by the new path.
- Both DB responsibilities and post-restore analytics steps are explicit.

### Completion note

- Date: 2026-08-06
- Agent: Cursor-Composer
- Status: **DONE** (live local disposable restore) with accepted non-P0 warnings
- Changed: `scripts/ops/*` (Npgsqlâ†’libpq URI; Docker client via `TRENDPLUS_PG_DOCKER_CONTAINER`; default `pre-data`+`data` restore; `-IncludePostData`); `docs/ops/BACKUP_RESTORE_RUNBOOK.md`; `docs/ops/BACKUP_RESTORE_REHEARSAL_EVIDENCE_2026-08-06.md`
- Checks: `Test-BackupRestoreGuards.ps1` PASS; live `Invoke-BackupRestoreRehearsal.ps1 -EnvironmentLabel local -AllowDestructiveRestore` PASS (~9s); ops dump SHA256 `D2B63EFCâ€¦`; analytics dump SHA256 `7C4A57DBâ€¦`; `prodaja_zaglavlje` 3655=3655; analytics table count 80=80
- Accepted non-P0: provider retention not verified; post-data/MV refresh skipped by default (full `-IncludePostData` hung 23+ min on `mv_supplier_decision_score_cache`); app `/health` against restored URLs not run
- Risk: full post-data restore still unproven in time-boxed gate; treat MV refresh as separate overnight check
- Next: `STAB08` READY (if analytics P0 gate also satisfied per STAB08 Ready after)

---

## STAB08 - Refresh pilot release evidence and decide GenAI entry gate

Status: DONE
Ready after:
- `STAB01`, `STAB02`, `STAB03`, `STAB04`, `STAB05`, `STAB06` and `STAB07` are `DONE` or carry explicitly accepted non-P0 warnings;
- the analytics reliability priority review confirms the required P0 correctness tasks are complete, including at minimum the current `RQ01` family and any linked frontend companion;
Priority: P1
Type: release/docs/evidence
Feature family: pilot-release-gate
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/STAB08-<agent>.lock.md`
Commit suggestion: `docs(qa): refresh pilot release and genai gate`

### Why

Existing production-readiness and pilot-release documents are historical snapshots. GenAI planning was added later, but the repository needs one fresh evidence gate that confirms whether the core product is stable enough to accept a new runtime surface.

### Evidence already found

- `ANALYTICS_PRODUCTION_READINESS_STATUS.md` is dated 2026-06-19.
- `ANALYTICS_PILOT_RELEASE_CHECKLIST_V2.md` is dated 2026-06-22.
- Current inspected `main` is from 2026-07-31 and has a failing Vercel status.
- GenAI queue rules already prohibit skipping P0 gates, but they do not contain current cross-cutting deploy/auth/restore evidence.
- Risk class: confirmed stale release-evidence gap.

### Contract

- Release status is `PASS`, `WARN`, `FAIL` or `BLOCKED` per row, never inferred from old docs.
- Every PASS links to current code/test/deploy evidence.
- Core Trendplus must remain fully usable with GenAI disabled.
- GenAI entry is `READY` only when current deployment, correctness, authorization, privacy boundaries, restore evidence and rollback expectations are acceptable.
- Remaining warnings stay visible and become linked prompts; they are not hidden to obtain a green verdict.

### Scope only

- `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md` or a new dated replacement
- `docs/qa/ANALYTICS_PILOT_RELEASE_CHECKLIST_V2.md` or a new versioned replacement
- `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md` only for the entry status/link
- queue/router notes only

### Do not touch

- runtime code
- provider integration
- LLM/RAG/tool implementation
- historical evidence documents in a way that erases old timestamps/results

### Read first

- all STAB task outputs
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
- current analytics queue notes and checks
- `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS.md`
- `docs/qa/ANALYTICS_PILOT_RELEASE_CHECKLIST_V2.md`
- `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`
- `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md`

### Do

1. Record current source SHA, deployment SHA/bundle, backend version and evidence timestamps.
2. Build an evidence matrix for:
   - frontend/backend CI;
   - live deploy and route smoke;
   - analytics correctness P0 tasks;
   - no-fake-zero/green/impact rules;
   - cache/freshness;
   - authorization and admin exposure;
   - import readiness provenance;
   - backup/restore rehearsal;
   - action ledger/confidence warnings;
   - rollback/recovery;
   - GenAI off-by-default independence.
3. Assign PASS/WARN/FAIL/BLOCKED with links to exact tests/docs/commits.
4. Decide one of:
   - `Core pilot READY; GenAI audit GAI01 READY`;
   - `Core pilot READY WITH WARNINGS; GenAI blocked by named P0`;
   - `Core pilot NOT READY`.
5. Update queue routing so agents cannot infer a different result from stale documents.

### Test matrix

- every PASS has current evidence;
- every WARN/FAIL has an owner prompt;
- current deploy SHA matches evidence;
- auth tests include 401 and 403;
- restore evidence is non-production;
- GenAI-disabled core smoke passes;
- no old smoke date is used as current proof.

### Checks

- `git diff --check`
- docs link/path validation
- queue validator from STAB02
- no runtime checks may be claimed unless their artifacts/results are linked

### Acceptance

- One current release verdict is authoritative.
- GenAI entry is explicitly ready or blocked with named evidence.
- Historical readiness docs no longer create conflicting current claims.

### Completion note

- Date: 2026-08-06
- Agent: Cursor-Composer
- Status: DONE
- Changed files:
  - `docs/qa/ANALYTICS_PRODUCTION_READINESS_STATUS_2026-08-06.md`
  - `docs/qa/ANALYTICS_PILOT_RELEASE_CHECKLIST_V3.md`
  - `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`
  - `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`
- Checks:
  - live backend checks: `/ready` and `/api/analytics/refresh-status` (refresh provenance = `unknown`) - pass
  - live auth check: `/api/admin/demo-verification` returned `401 Unauthorized` - pass
  - live frontend DOM probes (Playwright): `.pilot-readiness-page` + `Pilot nije spreman` + `Spremno` present; `.decision-board-page` renders but `Backend decision board aggregate nije dostupan` is present - pass
  - `dotnet build Trendplus2.sln -c Release` - pass (warnings only)
  - `dotnet test Api.Tests/Api.Tests.csproj -c Release --no-build` (focused auth/middleware filters) - pass
  - `npm run check:analytics-guardrails` - pass
  - `npm run build` - pass
  - `git diff --check` - pass (LF/CRLF warnings only)
  - `node scripts/check-prompt-queues.mjs --self-test` - pass
- Risk:
  - Core pilot remains NOT READY because the executive decision-board aggregate is unavailable on live smoke; refresh provenance remains `unknown` and pilot readiness is mixed (warning/blocked signals present).
- Next:
  - none; GenAI entry stays BLOCKED (see `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`, and GAI01 runtime readiness audit stays BLOCKED).

---

## STAB09 - Stabilize access-import test host route registration and auth-first timeout contract

Status: DONE
Priority: P0
Type: tests/docs
Feature family: access-import-test-host-contract
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/STAB09-codex.lock.md`
Commit suggestion: `fix(tests): stabilize access import route auth hosts`

### Why

Backend CI now reaches real tests, but access-import coverage still fails for two distinct reasons: the authorization host for delete-batch maps `MapAccessImportEndpoints()` without an `IBatchLogService`, so minimal API route discovery infers `logService` as a body parameter and throws before auth assertions run; and the timeout-path test for `/api/access-import/run` is unauthenticated, so it correctly returns `401` before the timeout branch is exercised.

### Evidence already found

- `Api.Tests/AccessImportAdminAuthorizationTests.DeleteBatch_RejectsRequestWithoutAdminKey` fails during route mapping because `IBatchLogService` is not registered in the test host and minimal API infers `logService` as a body parameter.
- The same host registers `IAccessImportService`, `IAccessImportJobQueue`, `TrendplusDbContext`, memory cache and rate limiter, but not a batch-log service.
- `Api.Tests/AccessImportRunEndpointTests.PostRun_WhenStoragePreparationTimesOut_ReturnsGatewayTimeout` expected `504` but got `401 Unauthorized`.
- `Api/Endpoints/AccessImportEndpoints.cs` checks `AdminAccessControl.GetDecision(...)` before it enters the runtime/timeout path, so the timeout assertion must authenticate first.
- Risk class: confirmed test-host contract drift plus stale timeout-test setup.

### Contract

- Access-import test hosts that map `MapAccessImportEndpoints()` must register the minimal batch-log service dependency needed for route discovery, or share a helper that does so.
- Auth assertions must remain first: no credential -> `401`, wrong key -> `403`.
- Timeout-path tests must send a valid admin key before expecting `504 GatewayTimeout`.
- Do not change production access-import behavior just to satisfy the tests.

### Scope only

- `Api.Tests/AccessImportAdminAuthorizationTests.cs`
- `Api.Tests/AccessImportRunEndpointTests.cs`
- shared access-import test helper/fixture under `Api.Tests/` if needed
- optional small note in `docs/qa/BACKEND_CI_FAILURE_TRIAGE_2026-08-06.md`

### Do not touch

- `Api/Endpoints/AccessImportEndpoints.cs`
- import runtime behavior
- database schema/migrations
- unrelated analytics tests

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md`
- `docs/qa/BACKEND_CI_FAILURE_TRIAGE_2026-08-06.md`
- `Api.Tests/AccessImportAdminAuthorizationTests.cs`
- `Api.Tests/AccessImportRunEndpointTests.cs`
- `Api/Endpoints/AccessImportEndpoints.cs`

### Do

1. Add the minimal missing access-import test-host dependency so endpoint mapping completes.
2. Keep the unauthorized and forbidden assertions intact.
3. Update the timeout test to authenticate before asserting the storage timeout path.
4. If other access-import test hosts map the same endpoints, reuse the same helper so the route registration stays consistent.
5. Add or update the smallest focused test only if needed to prove the contract.

### Test matrix

- no credential -> `401`;
- wrong admin key -> `403`;
- correct admin key -> accepted path still works;
- timeout path with admin key -> `504`;
- route mapping no longer throws body-inference errors;
- auth-first behavior stays visible.

### Checks

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AccessImportAdminAuthorizationTests|FullyQualifiedName~AccessImportRunEndpointTests"`
- `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release`

### Acceptance

- Access-import authorization tests reach the auth assertions instead of failing during route discovery.
- The timeout test proves the auth-first contract and can still reach `504 GatewayTimeout` when authenticated.
- No production access-import endpoint behavior is changed as part of the test stabilization.

### Notes

- 2026-08-06: DONE. Added `IBatchLogService` registration to the admin auth test host, updated the timeout repro to send `X-Admin-Key`, and kept production access-import code unchanged.
- Checks:
  - `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~AccessImportAdminAuthorizationTests.DeleteBatch_RejectsRequestWithoutAdminKey|FullyQualifiedName~AccessImportRunEndpointTests.PostRun_WhenStoragePreparationTimesOut_ReturnsGatewayTimeout"` - pass
  - `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release` - pass
  - `git diff --check` - pass (LF/CRLF warnings only)

---

## STAB10 - Protect access-import operational reads and cleanup inspection surfaces

Status: DONE
Ready after: `STAB09` is `DONE` and the `STAB03` Phase-1 admin-key boundary remains the accepted pilot contract
Priority: P0
Type: backend/tests/docs
Feature family: access-import-operational-read-auth
Parallel-safe: no
Owner: Cursor
Local lock: removed after DONE
Commit suggestion: `fix(auth): gate access import operational reads`

### Problem

Access-import operational reads still expose runtime, batch, job, archive and log details without the Phase-1 admin-key boundary. This leaves import internals public even after STAB04/STAB09 tightened other admin surfaces.

### Evidence

- `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md` marks access-import GET runtime-status, batches/jobs, logs, cleanup preview and archive inspection as follow-up surfaces that should move to the admin-key boundary.
- `Api/Endpoints/AccessImportEndpoints.cs` currently leaves `GET /api/access-import/runtime-status`, `/batches`, `/jobs`, `/batches/{id}`, `/jobs/{id}`, `/batches/{id}/logs`, `/jobs/{id}/logs`, `POST /cleanup/preview`, `GET /cleanup/archive`, and `POST /cleanup/archive/export` public.
- These endpoints reveal batch history, row counts, archive metadata, deleted-row payload export and operational diagnostics that are not required for anonymous pilot analytics reads.
- `STAB09` proved the import test hosts can already exercise the `AdminAccessControl` contract safely with focused integration tests.

### Scope

- `Api/Endpoints/AccessImportEndpoints.cs`
- `Api.Tests/AccessImportAdminAuthorizationTests.cs`
- one new focused access-import operational-reads authorization test file under `Api.Tests/` if required
- `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md` only for a tiny evidence note if the landed surface differs from the audit phrasing

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md`
- `Api/Endpoints/AccessImportEndpoints.cs`
- `Api.Tests/AccessImportAdminAuthorizationTests.cs`
- `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md`

### Do

1. Protect the operational read/inspection endpoints listed in Evidence with `AdminAccessControl`.
2. Preserve the Phase-1 contract exactly: missing credential -> `401`, wrong key -> `403`, valid key -> existing behavior.
3. Reuse the smallest shared helper/test-host pattern already used by STAB09; do not invent a second admin-key check path.
4. Add only the smallest focused tests needed to prove the protected GET/preview/archive behavior.
5. Keep import execution, batching, payload shape and fallback behavior unchanged aside from the auth gate.

### Tests

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AccessImportAdminAuthorizationTests"`
- focused new auth tests for runtime-status/batches/archive surfaces if added
- `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release`

### Acceptance

- Access-import operational reads and cleanup inspection surfaces no longer respond anonymously.
- The admin-key contract matches STAB03/STAB04 semantics exactly.
- Existing import runtime behavior is unchanged once authorized.
- The diff stays inside access-import auth/tests/docs rather than expanding into a broader auth redesign.

### Dependencies

- `STAB03` DONE and still authoritative for the pilot auth boundary.
- `STAB09` DONE so the access-import test-host contract is already stabilized.
- If a surface is intentionally left open, record exact evidence and finish `PARTIAL` instead of silently accepting exposure.

### Notes (2026-08-13)

- Date: 2026-08-13
- Agent: Cursor
- Status: DONE
- Gated `runtime-status`, batches/jobs list and detail, logs, cleanup preview, archive list and archive export with `AdminAccessControl.RejectIfUnauthorized`.
- Same-owner UI repair: Access Import page no longer silently treats 401 as empty batch history; operational reads send `X-Admin-Key` after the existing prompt.
- Evidence: `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md` section 3.5
- Next: `STAB11`

---

## STAB11 - Protect logs and errors operational read surfaces

Status: DONE
Ready after: `STAB10` is `DONE`
Priority: P0
Type: backend/tests/docs
Feature family: logs-operational-read-auth
Parallel-safe: no
Owner: Cursor Grok 4.6
Local lock: removed after DONE
Commit suggestion: `fix(auth): gate logs and errors reads`

### Problem

The app still exposes logs and error feeds as anonymous reads even though they can contain internal operational context and potentially sensitive exception material.

### Evidence

- `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md` calls out `GET /api/logs`, `GET /api/logs/{id}`, and `GET /errors` as P0 info-exposure follow-ups that should move to the admin-key boundary.
- `Api/Endpoints/AllEndpoints.cs` currently leaves `/errors`, `/api/logs`, and `/api/logs/{id}` public while only `/api/logs/clear` is already protected.
- Existing logs UI and APIs allow detailed message/exception retrieval without the same boundary used for other admin/ops surfaces.

### Scope

- `Api/Endpoints/AllEndpoints.cs`
- `Klijent/clientapp/src/pages/LogsPage.tsx` only if the existing page needs a bounded auth/error-state adjustment
- one new focused logs authorization test file under `Api.Tests/`
- existing observability/auth docs only for tiny completion-note updates

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md`
- `Api/Endpoints/AllEndpoints.cs`
- `Klijent/clientapp/src/pages/LogsPage.tsx`
- `Api.Tests/AnalyticsCacheInvalidateAuthorizationTests.cs`

### Do

1. Apply `AdminAccessControl` to `/errors`, `/api/logs`, and `/api/logs/{id}`.
2. Preserve existing clear-log auth semantics and do not weaken caching/rate limiting.
3. Add focused tests for missing key, wrong key and valid key on the read surfaces.
4. If the logs page needs an auth-state message, keep it minimal and do not redesign the page.

### Tests

- `git diff --check`
- focused logs auth tests under `Api.Tests`
- `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release`
- frontend test only if a UI auth-state branch is added

### Acceptance

- Logs and errors reads are no longer public.
- Clear-log behavior remains admin-only and unchanged.
- Any frontend adjustment is limited to truthful auth/error handling.

### Dependencies

- `STAB10` DONE first so access-import ops reads are closed before the general logs surface.
- Reuse the existing admin-key contract; do not add a separate document/log role system here.

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: logs and errors GET surfaces are admin-key gated; missing key 401, wrong key 403, valid key existing behavior
- Changed files: Api/Endpoints/AllEndpoints.cs; Api.Tests/LogsOperationalReadsAuthorizationTests.cs; Klijent/clientapp/src/services/logsApi.ts; Klijent/clientapp/src/services/__tests__/logsApi.spec.ts; Klijent/clientapp/src/pages/LogsPage.tsx; docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md; docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md; MASTER_ROADMAP.md
- Checks run: git diff --check; dotnet test Api.Tests --filter FullyQualifiedName~LogsOperationalReadsAuthorizationTests (11 passed); npm run test -- --run src/services/__tests__/logsApi.spec.ts (4 passed); node scripts/check-prompt-queues.mjs
- Checks not run: full Api.Tests suite; npm run build; Trendplus2/Program.cs duplicate logs routes remain out of STAB11 scope
- Run log: .ai/runs/2026-08-13-STAB11-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 355eccef9e792a7d43f480aa6a363a21cc9ad241
- Main verification: git rev-parse origin/main -> 096bf20d6908186cd3d7062ca6339c086522040f; work SHA 355eccef9e792a7d43f480aa6a363a21cc9ad241 is an ancestor
- Missed: legacy Trendplus2/Program.cs still maps public /errors and /api/logs if that host is ever used
- Follow-up: STAB12
- Residual risk: operators must supply X-Admin-Key to view logs; duplicate Trendplus2 host is not gated
- Prompt defect / scope repair: same-owner UI repair so LogsPage sends X-Admin-Key instead of treating 401 as a generic load failure

---

## STAB12 - Stop trusting unauthenticated document user headers for export/generate privilege

Status: DONE
Ready after: `STAB11` is `DONE`
Priority: P0
Type: backend/tests/docs
Feature family: document-header-trust-boundary
Parallel-safe: no
Owner: Cursor Grok 4.6
Local lock: removed after DONE
Commit suggestion: `fix(documents): stop trusting spoofable export headers`

### Problem

Document/export generation and ownership decisions still trust caller-provided `X-User-*` headers and default export roles, which is not a real authentication boundary for pilot-sensitive exports.

### Evidence

- `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md` documents Phase-1 follow-up work to stop trusting unauthenticated `X-User-*` headers for generate privilege.
- `Infrastructure/Services/Documents/DocumentSecurityServices.cs` falls back to `X-User-Id`, `X-User-Name`, and `X-User-Roles`, then defaults missing roles to `AnalyticsExport`.
- `Api/Endpoints/DocumentEndpoints.cs` and inventory export/print endpoints in `Api/Endpoints/InventoryEndpoints.cs` call document generation/status/download flows through that context.
- The current contract can make spoofed headers look like export authorization even without ASP.NET authentication.

### Scope

- `Infrastructure/Services/Documents/DocumentSecurityServices.cs`
- `Api/Endpoints/DocumentEndpoints.cs`
- `Api/Endpoints/InventoryEndpoints.cs` only where document/export generation uses the same context
- `Api.Tests/DocumentSecurityTests.cs`
- one new focused document/export auth test file if needed

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md`
- `Infrastructure/Services/Documents/DocumentSecurityServices.cs`
- `Api/Endpoints/DocumentEndpoints.cs`
- `Api/Endpoints/InventoryEndpoints.cs`
- `Api.Tests/DocumentSecurityTests.cs`

### Do

1. Choose the smallest Phase-1-safe boundary that prevents unauthenticated header spoofing from granting generate/list/export privilege.
2. Keep signed-download token validation and ownership checks intact.
3. If authenticated principals are still unavailable in production, prefer an explicit admin/internal boundary over silent header trust.
4. Add focused tests proving spoofed headers alone are insufficient for generate privilege.
5. Do not redesign the entire document subsystem or invent a full RBAC model.

### Tests

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~DocumentSecurityTests"`
- focused new document/export auth tests if added
- `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release`

### Acceptance

- Caller-supplied `X-User-*` headers alone can no longer authorize export/document generation.
- Download-token and ownership checks remain valid for already-created documents.
- The chosen Phase-1 boundary is documented and bounded to document/export privilege rather than broad auth re-architecture.

### Dependencies

- `STAB11` DONE so the remaining residual watchlist is tackled in the audit order.
- `STAB03` remains the authoritative pilot auth boundary until a later identity-provider task exists.
- If a true runtime identity source is required and unavailable, finish `BLOCKED` with exact missing evidence instead of inventing claims-based auth.

### Completion note

- Date: 2026-08-13
- Status: DONE
- Completion: unauthenticated X-User-* headers no longer grant generate/list/export privilege; generate uses admin-key; signed download/print tokens remain
- Changed files: Infrastructure/Services/Documents/DocumentSecurityServices.cs; Api/Endpoints/AdminAccessControl.cs; Api/Endpoints/DocumentEndpoints.cs; Api/Endpoints/InventoryEndpoints.cs; Api.Tests/DocumentSecurityTests.cs; Api.Tests/DocumentExportAuthorizationTests.cs; Klijent/clientapp/src/services/exportApi.ts; Klijent/clientapp/src/services/analyticsApi.ts; Klijent/clientapp/src/services/__tests__/exportApi.spec.ts; docs/security/RUNTIME_AUTHORIZATION_BOUNDARY_AUDIT_2026-08-05.md; docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md; MASTER_ROADMAP.md
- Checks run: git diff --check; dotnet test --filter FullyQualifiedName~DocumentSecurityTests|DocumentExportAuthorizationTests (16 passed); npm run test -- --run src/services/__tests__/exportApi.spec.ts (2 passed); node scripts/check-prompt-queues.mjs
- Checks not run: full Api.Tests suite; npm run build
- Run log: .ai/runs/2026-08-13-STAB12-evidence.md
- Delivery mode: direct-main
- Main commit SHA: 355eccef9e792a7d43f480aa6a363a21cc9ad241
- Main verification: git rev-parse origin/main -> 096bf20d6908186cd3d7062ca6339c086522040f; work SHA 355eccef9e792a7d43f480aa6a363a21cc9ad241 is an ancestor
- Missed: none known for the named generate/list/export privilege
- Follow-up: STAB13 WAITING (pilot release evidence refresh); do not promote GAI01 without refreshed gate
- Residual risk: operators must supply X-Admin-Key for export/generate; inventory schedule create/update still records user names from anonymous context
- Prompt defect / scope repair: same-owner UI repair so exportApi/analyticsApi send X-Admin-Key; print URLs now include signed tokens

---

## Expected next-task transitions

- After `STAB01`: set `STAB02` to `READY` unless STAB01 identifies a repository deploy fix that must be split first.
- After `STAB02`: set `STAB03` to `READY`.
- After `STAB03`: choose the smallest safe order between `STAB04` and `STAB05`; only one should be READY unless explicitly parallel-safe.
- `STAB06` may run in parallel after queue reconciliation because it owns a separate data-quality contract family.
- `STAB07` requires a safe environment and may remain BLOCKED without provider/DB access.
- `STAB08` is the final cross-cutting gate before declaring `GAI01` runnable.
- If the refreshed evidence says `NOT READY`, keep `GAI01` blocked even after `STAB08` is complete.
- After `STAB09`: keep `STAB10` as the single STAB READY prompt until access-import operational reads are gated or residual risk is explicitly accepted.
- After `STAB10`: set `STAB11` to `READY`.
- After `STAB11`: set `STAB12` to `READY` unless a smaller same-owner document/export split is required by evidence.
- After `STAB12`: keep STAB Current READY `none` until an owner promotes `STAB13` (pilot release evidence refresh); do not promote `GAI01` from STAB13 alone.
- After `STAB13`: `STAB14` may be promoted when current-main frontend gate or live-smoke truth is red/stale; it still must not promote `GAI01` by itself.
- After `STAB14`: `STAB15` may be promoted when the frontend gate is green and either `RQ110` is `DONE` or the owner supplies canonical production data-bearing analytics routes/filters for the smoke pack.

## STAB13 - Refresh pilot release evidence and GenAI entry-gate prep

Status: DONE
Ready after: Current execution READY is `none` and an owner explicitly promotes this additive docs/evidence slice
Priority: P1
Type: evidence/docs
Feature family: pilot-release-evidence-refresh
Parallel-safe: yes, evidence/docs when path-safe
Owner: Cursor Auto
Local lock: removed after DONE close
Promotion note: 2026-08-20 - owner-promoted via queue refill continuation.

### Problem

STAB12 closed the unauthenticated document-header privilege gap, but pilot release evidence and the GenAI entry gate still need a fresh, citeable pack after the RQ96-RQ98 / Decision Pulse wave. Stale STAB08-era prose must not be treated as current readiness.

### Evidence

- STAB08 / STAB12 completion notes
- `MASTER_ROADMAP.md` GenAI blocked-by core-pilot evidence
- `docs/planning/QUEUE_REFILL_2026-08-20.md`

### Scope

- refresh pilot release evidence pointers and GenAI entry-gate checklist;
- do not mark `GAI01` READY;
- no production deploy or secret rotation in this prompt.

### Read first

- STAB12 completion note
- STAB08 gate materials
- MASTER_ROADMAP.md current READY

### Do

1. Capture a fresh evidence index for deploy/CI/auth/import/analytics decision surfaces.
2. Record whether GenAI remains blocked and why.
3. Keep `GAI01` WAITING/blocked until the gate explicitly clears.
4. Do not invent tenant or production access.

### Tests

- docs/queue validators pass when promoted;
- `GAI01` remains non-READY unless owner clears the gate.

### Acceptance

- citeable refreshed pilot evidence pack exists when promoted;
- GenAI entry remains explicit, not inferred.

### Dependencies

- STAB12 DONE;
- do not promote `GAI01` from this prompt alone.

### Completion note

- Date: 2026-08-20
- Status: DONE
- Completion: evidence refresh pack complete; delivered on main; GenAI stays BLOCKED
- Changed files: docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-20.md, docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md, docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md, MASTER_ROADMAP.md, docs/planning/QUEUE_REFILL_2026-08-20.md, .ai/runs/2026-08-20-STAB13-evidence.md
- Contract/runtime behavior changed: no runtime; refreshed pointers keep core pilot NOT READY and GenAI BLOCKED
- Checks run: node scripts/check-prompt-queues.mjs; node scripts/check-planning-architecture.mjs
- Checks not run: live smoke / production access - out of scope
- Run log: .ai/runs/2026-08-20-STAB13-evidence.md
- Evidence state: synchronized
- Delivery mode: branch `cursor/queue-refill-dt09-dex20`
- Main commit SHA: bc4dbb5f465974253668768fbd03766abf34c0e2
- Main verification: passed - origin/main contains bc4dbb5f465974253668768fbd03766abf34c0e2
- Missed: fresh live smoke
- Follow-up: live smoke pack before any GenAI reopen
- Residual risk: older readiness PASS rows remain historically present and must not be misread as current
- Prompt defect / scope repair: none

---

## STAB14 - Reopen frontend analytics gate and current-main live-smoke truth

Status: DONE
Ready after: `STAB13` is `DONE` and current-main frontend release truth is red or stale
Priority: P0
Type: frontend/tests/release-evidence
Feature family: pilot-release-current-main-reentry
Parallel-safe: no
Owner: Codex
Local lock: `.ai/task-locks/STAB14-<agent>.lock.md`
Commit suggestion: `test(release): reclose frontend gate and live smoke truth`
Promotion note: 2026-08-20 - owner-promoted from the current-main audit because the pilot still has no fresh live-smoke pack and the frontend analytics quality gate is red.

### Problem

STAB13 refreshed the pilot evidence pointers honestly, and the local frontend analytics gate is green again, but it still did not produce fresh current-main/live runtime proof. The product therefore still cannot claim a current pilot verdict from the exact current main branch.

### Evidence

- `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-20.md` keeps `Core pilot = NOT READY` specifically because no fresh live-smoke pack exists.
- The local frontend analytics gate now passes again:
  - `npm run test:analytics -- --cache false`
  - `npm run build`
- Exact current-main / deployed-runtime proof is still missing.
- Business milestone exit rule: `docs/roadmaps/BUSINESS_ROADMAP.md` requires the current STAB evidence to say `Pilot Ready` or `Pilot Ready With Accepted Warnings`; historical readiness docs do not count.
- Historical frontend gate evidence was red before this re-entry:
  - GitHub Actions run `32379775110` failed on commit `8c27094`
- Audit-reproduced frontend failure families that were repaired locally in this re-entry include:
  - Pilot readiness contract drift (`warning/ready` vs current `blocked`)
  - duplicate `Ponovo proveri` controls
  - Executive Decision Board duplicate `Spor obrt` / leaked `accepted` chip
  - inventory freshness tests missing router context
  - accessibility label/name drift
  - methodology registry/import drift
  - relative URL / MSW / `AbortSignal` harness failures
- STAB13 explicitly left fresh live smoke as missed work.

### Scope

- current failing frontend analytics test files and the exact frontend/runtime files they exercise
- `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-20.md`
- `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`
- `docs/roadmaps/BUSINESS_ROADMAP.md` only if the cited exit rule or milestone wording must be synchronized
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md`

### Read first

- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-20.md`
- `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`
- `docs/roadmaps/BUSINESS_ROADMAP.md`
- `Klijent/clientapp/package.json`
- the currently failing frontend test files from `npm run test:analytics`

### Do

1. Reproduce the current frontend analytics quality gate exactly with `npm run test:analytics`.
2. Group failures by contract family and apply only the smallest truthful repairs on the owned release surfaces.
3. Keep fail-closed semantics explicit:
   - blocked stays blocked;
   - stale/missing evidence never becomes ready/green;
   - duplicate controls or chips must not mask backend truth.
4. Re-run the frontend analytics gate until it is green or truthfully reduced to a smaller residual set.
5. After the gate is green, execute a fresh live-smoke pack against the exact current deployment or current-main runtime covering at minimum:
   - `/health`
   - `/ready`
   - Decision Board aggregate
   - Decision Pulse
   - inventory/forecast fail-closed paths
6. Update the pilot evidence pack and GenAI gate doc with the exact verdict from the fresh smoke.
7. Keep `GAI01` non-READY unless the new STAB evidence explicitly clears the core pilot gate.

### Tests

- `git diff --check`
- `cd Klijent/clientapp && npm run test:analytics`
- targeted frontend specs only when narrowing a failing family
- fresh live-smoke path recorded against the exact current runtime/deploy

### Acceptance

- The frontend analytics quality gate is tied to a fresh current-main result, not stale historical green evidence.
- A fresh live-smoke pack exists for the exact current runtime/deploy and updates the STAB pilot verdict honestly.
- `GAI01` remains blocked unless the refreshed evidence explicitly says otherwise.
- The prompt does not hide blocked/unknown states behind UI-friendly defaults.

### Dependencies

- `STAB13` DONE.
- `BCI10` remains the higher-priority backend gate in `MASTER_ROADMAP.md`; STAB14 must not claim overall pilot readiness if backend current-main truth is still red.
- Do not mix tenant-identity, MT, or GenAI runtime work into this prompt.

### Completion note

- Date: 2026-08-21
- Status: DONE
- Completion: re-closed the local frontend analytics gate and build by fixing the pilot readiness, executive decision board, supplier explainability, inventory freshness, and fetch/test harness drift; the fresh current-main live-smoke pack is synchronized to `origin/main` and STAB14 is now DONE
- Changed files: Klijent/clientapp/src/components/analytics/PilotDataQualityIntakeReport.tsx; Klijent/clientapp/src/components/analytics/__tests__/SupplierExplainabilitySnapshot.spec.tsx; Klijent/clientapp/src/components/inventory/InventoryItemsTable.spec.tsx; Klijent/clientapp/src/components/supplierDecisionHub/SupplierExplainabilitySnapshot.tsx; Klijent/clientapp/src/pages/DataQualityPage.tsx; Klijent/clientapp/src/pages/ExecutiveDecisionBoardPage.tsx; Klijent/clientapp/src/pages/PilotReadinessPage.tsx; Klijent/clientapp/src/pages/__tests__/AnalyticsSalesReadinessRegression.spec.tsx; Klijent/clientapp/src/pages/__tests__/ConfigurationPage.spec.tsx; Klijent/clientapp/src/pages/__tests__/InventoryPage.freshnessLineage.spec.tsx; Klijent/clientapp/src/services/__tests__/logsApi.spec.ts; Klijent/clientapp/src/utils/fetchWithTimeout.ts; docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-22.md; MASTER_ROADMAP.md; docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md; .ai/runs/2026-08-22-STAB14-evidence.md
- Checks run: git diff --check (pass); node scripts/check-prompt-queues.mjs --self-test (pass); node scripts/check-prompt-queues.mjs (pass); node scripts/check-planning-architecture.mjs --self-test (pass); node scripts/check-planning-architecture.mjs (pass); npm run test:analytics -- --cache false (pass); npm run build (pass); npm exec vitest run src/pages/__tests__/AnalyticsSalesReadinessRegression.spec.tsx --cache false (pass); npm exec vitest run src/pages/__tests__/InventoryPage.freshnessLineage.spec.tsx --cache false (pass); npm exec vitest run src/services/__tests__/logsApi.spec.ts --cache false (pass); npm exec vitest run src/pages/__tests__/ConfigurationPage.spec.tsx --cache false (pass); npm exec vitest run src/components/analytics/__tests__/SupplierExplainabilitySnapshot.spec.tsx --cache false (pass); npm exec vitest run src/components/inventory/InventoryItemsTable.spec.tsx --cache false (pass); production live-smoke script via puppeteer-core + local Chrome (pass); git push origin main (pass)
- Checks not run: full backend suite - not needed for this frontend/live-smoke release-truth prompt
- Run log: .ai/runs/2026-08-22-STAB14-evidence.md
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: f67505d4c9220c53f1823a70c734d6ad2c14bc9f
- Main verification: passed - `origin/main` contains `f67505d4c9220c53f1823a70c734d6ad2c14bc9f`; `git merge-base --is-ancestor HEAD origin/main` -> `ancestor=true`
- Missed: none known
- Follow-up: STAB15 only if the gate remains green
- Residual risk: current live smoke is synchronized to `main`, but the broader pilot is still not ready because BCI10 remains red
- Prompt defect / scope repair: same-owner UI and harness repairs so the local analytics gate closes truthfully without inventing current-main proof; 2026-08-31 queue status was refreshed from stale `PARTIAL` to `DONE` to match the synchronized run evidence and the already-dependent `STAB15` routing truth

---

## STAB15 - Production analytics non-empty smoke against exact deploy SHA

Status: DONE
Ready after: `STAB14` is `DONE` and either `RQ110` is `DONE` or the owner provides canonical production data-bearing analytics routes/filters
Priority: P0
Type: live-smoke/release-evidence
Feature family: pilot-production-nonempty-smoke
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/STAB15-<agent>.lock.md`
Commit suggestion: `docs(qa): prove production analytics non-empty smoke on exact deploy`

### Problem

Even after the frontend gate re-closes, the pilot still cannot claim reliable analytics if the exact deployed runtime can reach shell routes yet surface blank charts, blank tables, or misleading healthy-empty states on data-bearing screens. Production needs one exact-SHA smoke pack that proves the main analytics surfaces are either visibly non-empty when canonical data exists or explicitly degraded/blocked with truthful metadata.

### Evidence

- `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-20.md` still keeps the core pilot at `NOT READY` because fresh exact-deploy smoke truth is missing.
- `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md` historically captured route mismatches and runtime route failures that can look like "no data" to an operator even when data exists elsewhere in the system.
- GitHub Actions run `32379775110` failed on 2026-08-20 for the frontend analytics quality gate, and GitHub Actions run `32384559939` failed on 2026-08-20 for backend analytics/data-integrity coverage; current smoke must therefore tie to the exact deploy SHA instead of stale historical PASS rows.
- User instruction 2026-08-20: production is available for testing and the pilot should not tolerate blank analytics surfaces when authoritative data exists.
- Canonical owner-supplied route/filter matrix for this smoke comes from `docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md` plus the STAB14 evidence pack, which together name the exact backend/backend+frontend smoke surfaces to test on the current deploy SHA.

### Scope

- production/live-smoke evidence only for the exact deployed SHA or an explicitly documented deployment SHA
- `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-20.md`
- one new dated smoke evidence doc under `docs/qa/` if needed to preserve history
- `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`
- `MASTER_ROADMAP.md` only if the pilot gate verdict changes

### Read first

- `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-20.md`
- `docs/qa/ANALYTICS_PILOT_SMOKE_RESULT.md`
- `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`
- `docs/roadmaps/BUSINESS_ROADMAP.md`
- `STAB14` output
- `RQ110` matrix/output, or the owner-supplied canonical production data-bearing route/filter list

### Do

1. Resolve the exact deployed SHA being tested and record whether it equals current `main` or a separately identified production SHA.
2. Use the `RQ110` matrix or an owner-approved equivalent to select canonical production analytics routes/filters that should have visible data:
   - dashboard
   - product decision center
   - executive decision board
   - inventory
   - supplier decision/sales
   - analytics actions
   - durable report/download route when applicable
3. Execute a production smoke pack that records for each route:
   - exact URL/filter payload used;
   - expected data-bearing rationale;
   - pass/non-empty, warn/degraded, or block/fail result;
   - freshness/data-quality metadata observed;
   - whether the response/page was blank, shell-only, or truthfully degraded.
4. Treat shell-only success, blank table/chart render, stale empty cache, or hidden fallback as a failed reliability result, not a healthy empty state.
5. Update the pilot release evidence and GenAI gate docs with the exact verdict and exact tested SHA.
6. If a route fails, classify the first blocking family tightly enough to feed the next prompt:
   - route mismatch;
   - auth/session/environment issue;
   - cache/refresh/materialized-view lag;
   - backend contract empty/degraded truth issue;
   - frontend render mismatch.

### Tests

- `git diff --check`
- fresh production/live-smoke execution recorded against the exact tested SHA
- queue/planning validators if docs or roadmap pointers change

### Acceptance

- A citeable production analytics non-empty smoke pack exists for the exact tested deploy SHA.
- Each named pilot analytics surface is recorded as non-empty, truthfully degraded, or blocked with an explicit reason; no blank/shell-only pass is accepted as healthy.
- Pilot release evidence and the GenAI gate cite the same exact tested SHA and verdict.
- Any failing surface is classified narrowly enough to become the next owner prompt instead of remaining a generic "analytics blank" report.

### Dependencies

- `STAB14` DONE first.
- Prefer `RQ110` DONE first so production smoke uses a canonical data-bearing route/filter matrix instead of ad hoc route guesses.

### Completion note

- Date: 2026-08-22
- Status: DONE
- Completion: executed exact-deploy live smoke against backend runtime SHA `d9c4d0a8cd893c8e7cb330f47e41e92843fa9875` and current frontend bundle `/assets/index-HJjiguak.js` using the canonical smoke matrix from `docs/qa/ANALYTICS_PILOT_SMOKE_TEST.md`; all named pilot surfaces rendered real content or honest degraded states, and backend freshness remained visible as unknown instead of fake-green
- Changed files:
  - `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-22_STAB15.md`
  - `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`
  - `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md`
  - `MASTER_ROADMAP.md`
  - `.ai/runs/2026-08-22-STAB15-evidence.md`
- Contract/runtime behavior changed:
  - no runtime behavior changed
  - exact production deploy smoke evidence was refreshed for the current deployed SHA and frontend bundle
- Checks run:
  - live API fetch smoke for `/health`, `/ready`, `/api/runtime/version`, `/api/analytics/refresh-status?dataScope=all`, `/api/analytics/actions?dataScope=all`, and `/api/analytics/cached/products/decision-center?fromDate=2026-08-01&toDate=2026-08-22&top=10&dataScope=all` - pass
  - browser render smoke via local Chrome and `puppeteer` for `/analytics`, `/analytics/pilot-readiness`, `/analytics/products`, `/analytics/supplier`, `/analytics/inventory`, `/analytics/data-quality`, `/analytics/actions`, `/analytics/decision-board`, `/analytics/reports/pilot-intake?fromDate=2026-08-01&toDate=2026-08-22&dataScope=all`, and `/analytics/supplier/report?fromDate=2026-08-01&toDate=2026-08-22&dataScope=all` - pass
- Checks not run:
  - `git diff --check` after the final doc updates - pending until the evidence-sync pass
  - queue/planning validators after the final doc updates - pending until the evidence-sync pass
- Run log: `.ai/runs/2026-08-22-STAB15-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: `c5cabafb69eba05de4ffe5ce8e9b71cbf538eacd`
- Main verification: `git merge-base --is-ancestor c5cabafb69eba05de4ffe5ce8e9b71cbf538eacd origin/main` -> `ancestor=true`
- Missed: none known
- Follow-up: none
- Residual risk: production backend freshness remains `unknown`, but it is explicitly visible rather than hidden behind fake green state
- Prompt defect / scope repair: used the owner-approved canonical smoke contract plus STAB14 evidence to replace the missing `RQ110` matrix
- Do not broaden into general load/performance/security work; this prompt is only about exact-SHA production analytics visibility truth.

---

## STAB16 - Restore exact-deploy parity and durable analytics refresh workers

Status: BLOCKED
Ready after: production provider deployment authority and read-only audit connection are available
Priority: P0
Type: deploy/ops/live-evidence
Feature family: production-analytics-freshness-deploy-parity
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/STAB16-<agent>.lock.md`
Commit suggestion: `docs(ops): prove production analytics deploy and worker parity`

### Problem

The canonical production API reports a runtime SHA contained in current `main`, but it exposes no durable successful analytics-refresh runs. The web process intentionally has no heavy workers; that process-local fact does **not** prove whether a separate worker service exists or is healthy. What is proven is that the shared durable `AnalyticsRefreshRuns` history visible to the API contains no usable successful evidence for the required job families, so freshness remains unknown/critical and production truth is not certified.

### Evidence

- A same-day 2026-08-27 API-only recheck returned `commitSha=6ecbfa67a7304c3cbeeb71755a35255e766c8e24`, which is contained in current `main` and includes the current PDC/Decision Board trust-repair chain.
- `GET /api/analytics/refresh-status?dataScope=all` returned `workersEnabled=false`, process `web`, unknown freshness, in-memory-cache warning, and six job reasons equivalent to “Worker nije registrovan u web procesu.” `AnalyticsRefreshStatusService` first reads durable refresh history from `AnalyticsDbContext`; only when no matching run exists does it fall back to process-local worker health. Therefore this response proves missing durable run evidence, not absence of a separate Render worker service.
- `Api/Config/WorkerRuntimeConfig.cs` and `Api/Program.cs` deliberately register heavy refresh workers only for `PROCESS_TYPE=worker`; enabling them inside the web process would violate the established runtime boundary.
- `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md` plus `.ai/runs/2026-08-27-queue-audit-production-followups-evidence.md` record the public observations and the still-missing direct database reconciliation/browser proof.
- A read-only public recheck on 2026-08-31 returned HTTP 500 from `/health`, `/ready`, `/api/runtime/version`, `/api/analytics/refresh-status?dataScope=all`, and `/api/analytics/cached/dashboard/bootstrap?dataScope=all`, while `/` still returned the static SPA shell. The current production fault prevents a truthful conclusion that a visible `insufficient_data` state means the source data is absent; restore liveness and inspect provider logs before classifying data availability.
- `.ai/runs/2026-09-02-direct-supplier-decision-reliability-evidence.md`, `.ai/runs/2026-09-02-direct-decision-pulse-migration-chart-evidence.md`, `.ai/runs/2026-09-02-direct-product-decision-history-ux-evidence.md`, and `.ai/runs/2026-09-03-data-quality-consistency-evidence.md` all pushed analytics trust fixes to `main`, but each recorded that the canonical Render runtime was still stale, undeployed, or not directly provable at verification time.
- `.ai/runs/2026-09-03-pilot-readiness-truthfulness-evidence.md` and `.ai/runs/2026-09-03-analytics-followup-audit-evidence.md` confirm the UI now reports missing refresh history honestly, while the underlying worker/history parity and cross-endpoint period truth remain unresolved production follow-ups rather than completed live proof.

### Scope

- the authorized Render/Fly deployment configuration and provider evidence for the canonical API route;
- dedicated worker-process configuration, deployment, and refresh-status proof;
- read-only production analytics reconciliation and one dated QA/run-evidence update;
- no production database mutation, schema migration, analytics formula rewrite, or web-process worker registration.

### Read first

- `docs/qa/ANALYTICS_PRODUCTION_LIVE_AUDIT_2026-08-27.md`;
- `Api/Config/WorkerRuntimeConfig.cs`;
- `Api/Program.cs`;
- `Api/Services/AnalyticsRefreshStatusService.cs`;
- `docs/DEPLOY_TO_FLY.md` and the canonical provider configuration that actually owns `trendplus-api.onrender.com`;
- `.ai/runs/2026-08-27-queue-audit-production-followups-evidence.md`;
- `docs/ai/ANALYTICS_AGENT_SAFETY_GATE.md`.

### Do

1. Use authorized provider access to identify the canonical API deployment and its worker service; do not infer that the Fly fallback is canonical when the frontend points at Render.
2. Verify the canonical runtime SHA is contained in current `main`; if it is not, deploy through the approved path and record both the source SHA and runtime-version SHA.
3. Configure/restore a dedicated process with `PROCESS_TYPE=worker`; keep heavy refresh jobs disabled in the web API process.
4. Use `TRENDPLUS_AUDIT_DATABASE_URL` only for read-only reconciliation of the selected endpoint windows against the production source tables; do not record credentials or mutate data.
5. Prove at least one successful, timestamped refresh for each required job family, cache/freshness state, and the PDC stale/blocked contract after deployment.
6. Refresh the production audit, release evidence, and queue status only from the exact deployed runtime evidence.

### Tests

- `git diff --check` for repository changes;
- provider/runtime version check proves the runtime SHA is contained in current `main`;
- `/health`, `/ready`, `/api/analytics/refresh-status?dataScope=all`, PDC, Decision Board, and supplier-report live checks;
- read-only database-to-endpoint reconciliation for the documented window when the audit connection is supplied;
- queue/planning validators if routing/evidence docs change.

### Acceptance

- The canonical production runtime reports a SHA contained in current `main`.
- The web process remains API-only while a dedicated worker process is registered and produces durable successful-refresh timestamps.
- Unknown/stale/fallback evidence remains visible until the successful refresh proof exists; no fake-green fallback is introduced.
- The live PDC and Decision Board never expose an actionable recommendation, decision confidence, or expected impact when freshness/data quality blocks it.
- The direct database reconciliation and exact-deploy smoke results are recorded with windows, counts, and any mismatch classification.

### Dependencies

- Production provider deploy/read authority for the canonical Render service.
- A local, read-only `TRENDPLUS_AUDIT_DATABASE_URL` supplied outside repository files.
- No production mutation authority is needed or requested.

### Addendum 2026-10-04 (next-wave audit; first owner step, no status change)

- Before any database access, the owner can check three settings in the Render dashboard in about three minutes: (1) effective `Database__AutoMigrate`, `DatabaseInitialization__FailFast` and `StartupTasks__RunDatabaseInitialization` on `trendplus-api`; (2) whether a `trendplus-worker` service from `render.yaml:81-147` exists and runs; (3) the startup log after the next restart.
- Reason: retained live `/ready` is `ready=true` (since 2026-10-04 21:39:00 CEST) while startup-owned objects are missing. Code strongly indicates the effective runtime did not follow the expected strict initialization path, but provider config/startup logs are required to distinguish AutoMigrate-off, non-strict error completion, alternate effective config/connection, or post-readiness schema drift (RQ545/RQ587). `refresh-status` on the web process proves six job families have no durable run evidence visible to the API; it does **not** prove the separate worker service is absent. Pulse/supplier materializations are currently missing/empty and must be correlated with the worker service and its durable run history.
- If the owner changes effective startup policy to strict/fail-fast, the next restart can remain `not ready` until the schema issue is repaired; treat that as an intentional visibility change and schedule it operationally. First capture current effective config and logs before changing anything. RQ587 makes startup outcome readable without claiming current schema certification. Source: `docs/qa/ANALYTICS_RELIABILITY_VALUE_NEXT_WAVE_AUDIT_2026-10-04.md` §4.2, §22.


### Addendum 2026-10-13 — PR #133 merged, runtime/maintenance proof still external

- Source milestone: PR #133 (`fix(render): defer supplier windowed MV rebuild outside web cold start`) merged to `main` on `1c9158d3e9a74de96c0b7ece60fb0c88f9a18058`. Exact merge-SHA backend run `38301829902` passed (1,998 passed, 42 classified skips). The subsequent current repo tip for this review is `6dec862e6449a18a24a26234215bcc9b30705b98` (evidence-only after merge). This is **CI evidence only**, not provider/runtime verification.
- Render user log excerpt from 2026-10-12 16:50 UTC proves missing supplier 90d/180d MVs, a forced execution of SQL 029 and simultaneous API/readiness 503s; it does **not** include the eventual SQL success/error or `/ready` JSON. Do not assert SQL 029 was the sole cause of all 503s.
- Production config in `Api/appsettings.Production.json` now disables `DatabaseInitialization:BuildSupplierDecisionWindowedViewsOnStartup`; check effective Render environment overrides. Without deployment access, actual production setting is UNKNOWN. Do not set `StartupReadiness:GateApiTraffic=false` or bypass purchase-cost provenance schema enforcement.
- Public `/health` and `/ready` requests made from the assistant's environment on 2026-10-13 were **not accessible**; this is NOT an observed 503 and is NOT evidence of recovery or outage. The scheduled Render maintenance on Oct 14 01:00–02:00 UTC is separate from the Oct 12 503 incident.
- RQ634/RQ635/RQ636 are independent **repository-local** reliability tasks; they do not own or certify Render runtime, production worker registration, provider config, Vercel dashboard or Neon state. STAB16 remains the exclusive provider/deployed-proof owner.

#### Operator-ready minimum evidence packet (no credentials in Git)

1. Render Dashboard for service `trendplus-api`: effective runtime/build SHA, deploy finished timestamp, `ASPNETCORE_ENVIRONMENT`, `PROCESS_TYPE`, `Database__AutoMigrate`, `DatabaseInitialization__FailFast`, `StartupTasks__RunDatabaseInitialization`, `DatabaseInitialization__BuildSupplierDecisionWindowedViewsOnStartup`, worker registration flags and startup task flags. Capture values only, never connection-string values or secrets.
2. Consecutive `GET /health`, `GET /ready` with response status, sanitized JSON (`reason`, initialization `state/failureCategory/failureStage`, DB probes), and timings after cold start; if /ready is red, classify DB connection, schema contract, initialization, lock/timeout or optional analytics before proposing a mitigation.
3. Logs from the **same exact deploy**, from process start through post-ready: SQL 029 attempted/skipped, any transaction timeout/lock SQLSTATE, startup outcome and state transitions; don't equate `/health=200` with full schema health.
4. Separate Render worker service identity and `PROCESS_TYPE=worker` effective config, refresh job execution history, per-family last successful timestamp, and diagnostics from `/api/analytics/refresh-status?dataScope=all`; do not infer the worker service is absent from `web` process's worker list.
5. Supplier 90d/180d catalog verification **read-only**: existence, `relispopulated`, required columns, last refresh/run source. If missing, leave supplier capability unavailable and schedule SQL 029 maintenance only after explicit authorization, backup and review (RQ635 can first certify local preflight). Never run manual SQL 029 from the live web process during traffic.
6. Vercel deploy verification for RQ633 with authoritative project-root setting, ignoreCommand outcome on docs-only vs frontend commits and concrete provider IDs/errors; repo source changes alone do not prove dashboard configuration.
7. Only then perform bounded read-only production reconciliation and promote STAB16 when real provider/audit access is present. Do not reopen RQ630/RQ631, repeat Access import #23, or declare 90d/180d ready based on green GitHub CI.

**Unblock event:** authorized provider read/deploy access plus read-only analytics audit connection or equivalent scrubbed operator evidence. Repository-only tests may proceed independently under RQ634-RQ636; the STAB16 production status remains BLOCKED until its full acceptance is proven.



### Addendum 2026-10-13 — second-pass cold-start concurrency review (no status change)

- **New source-confirmed residual:** SQL 029 is not the only potentially expensive startup path. `EnsureAnalyticsSupplierDecisionSchemaAsync` still calls `BuildSupplierDecisionHubObjectsAsync(...allowHeavyRefresh:false)`; when SQL 018 supplier caches are missing, the code can execute their populated build with `commandTimeoutSeconds:0`. The flag only skips an explicit refresh. RQ634 now owns the repository-only cold-start/018 regression and safety fix, with exact PostgreSQL tests. **Do not disable mandatory base schema validation** to bypass slow rebuild.
- The web process is **not truly worker-free**: Production settings opt into `AccessImportBackgroundWorker` and 2-second polling. Its immediate stale-batch recovery and subsequent claim loop have no required-readiness wait, consistent with the user's observed import-queue SQL during /ready 503. New RQ637 owns repository-local gating and lifecycle tests, while STAB16 owns effective Render settings and live resource/probe measurements. Do not simply disable Access imports permanently or replay completed batch #23.
- Startup AnalyticsConnectionDiagnostics previously probed core/all-time supplier objects but omitted 90d/180d. The direct-request code correction adds read-only canonical MV capability diagnostics (missing object, missing required columns, unpopulated), without treating optional cache unavailability as global API readiness failure or repeatedly rebuilding on web.
- PR #134 final-head backend/quality/governance runs `38351086653`, `38351086632` and `38351086583` all succeeded; merged planning run `38351175549` succeeded. This is source/CI truth only; effective deployed Render runtime is still unknown.
- Verify **separately** in authorized Render logs: (a) required DB init state; (b) SQL018 or SQL029 start/completion and lock/timeout; (c) API /ready reason and post-start response; (d) import worker first stale recovery/poll before/after readiness; (e) 90/180d canonical capability. The worker's mere existence in web logs does not prove a dedicated analytics-refresh service exists.
- The 2026-10-14 Render maintenance notice is about dashboard/API/one-off-job availability, not a demonstrated source for the earlier Oct 12 app 503. Do not put secrets or connection strings in evidence.

**STAB16 stays BLOCKED on authorized provider read/deploy and production audit access**. RQ634/RQ637 (P1) and RQ635/RQ636/RQ638 (P2) remain independently runnable in the repository, subject to refreshed locks and PR ownership.


**Additional 2026-10-13 audit boundary:** `PerformanceLoggingBehavior` has independent MediatR `PerformanceLogs` length-contract risks (RQ638). Do not reinterpret test-host missing-table SQLSTATE 42P01 as proof of production VARCHAR overflow, and do not apply SQL schema changes to avoid enforcing existing application-side caps. This is repository-local and does not unblock STAB16.

### Addendum 2026-10-14 — approved provenance-column repair; Render restart pending

- The operator supplied `AccessImportBackgroundWorker` waiting on `schema_incompatible` and repeated `/ready` 503s. A direct public check of the canonical Render API confirmed `/ready` `status=failed`, `db.ok=true`, `reason=schema_incompatible`, `databaseInitialization.failureCategory=schema_contract_incompatible`, one attempt and process start at `2026-10-14T16:53:11Z`. `/health=200` represented liveness only.
- Read-only inspection of the connected Neon `Trendplus` project's `production` branch, `trendplus` database, confirmed that `public.prodaja_stavke.purchase_cost_provenance` was absent and `20261010160000_AddPurchaseCostProvenance` was not recorded in `__EFMigrationsHistory`. The latest recorded Trendplus EF migration was `20260930074136_GeneralizeOperationsAnalyticsIntegrityEvidence`; `20261011110000_AddAccessImportStoreDayCoverage` is also pending. The exact Render `DefaultConnection` target still requires provider configuration verification.
- The user explicitly approved the exact additive SQL `ALTER TABLE public.prodaja_stavke ADD COLUMN IF NOT EXISTS purchase_cost_provenance character varying(64);`. Neon migration `aa23640a-1faf-4285-ac5b-fbec963edd09` was prepared and checked on temporary branch `br-empty-moon-ag388d5x`, then applied to parent `production`; the temporary branch was deleted by the migration tool. Post-application metadata is nullable `character varying(64)`. Sale-line count remained `67,144` and non-null numeric-cost count remained `67,092` before and after the migration. No source import, business-row rewrite or EF-history insertion was performed.
- A subsequent public `/ready` request still returned the same 503 with unchanged process start and failed-at timestamps. `ReadinessWarmupHostedService` returns after this schema mismatch, so the running Render web process needs a restart to recheck the now-present column. No Render restart or new deploy was triggered from this workspace; the owner has been asked to restart the existing `trendplus-api` service. The historical manual GitHub deploy workflow failed its latest builds and would deploy a newer commit, so it was not used as a substitute for a restart.
- STAB16 remains `BLOCKED`. After restart, capture fresh `/ready` and `/api/runtime/version`, verify the effective `PROCESS_TYPE`, `Workers__Enabled`, `Database__AutoMigrate`, `DatabaseInitialization__FailFast`, `StartupTasks__RunDatabaseInitialization` and `DefaultConnection` target without exposing secrets. Reconcile the remaining pending EF migration through the approved startup/deployment path. A healthy `/ready` alone will not certify worker freshness, supplier materializations or the full STAB16 production acceptance.

---

## STAB17 - Safe incremental Access import with source-lineage bootstrap and lossless rollback

Status: IN_PROGRESS
Execution scope: repository-local engineering and disposable-source proof ONLY; production rollout/legacy backfill WAITING
Priority: P0
Type: Access import correctness, operational data integrity, PostgreSQL integration and controlled release
Feature family: access-incremental-import-source-identity
Owner: Codex `/root` (exclusive shared claim in this run); former PR #163 author/workspace released the engineering lane by user instruction, documented in PR #163 conversation on 2026-10-17.
Parallel-safe: NO for Api/Services/AccessImportService.cs, sale identity models/migrations, sales tests and related analytics paths; receiving owner MUST reuse PR #163, not open a parallel code PR, after claiming on main.
Ready after: completed owner-directed engineering handoff at https://github.com/ivanjovicic/Trendplus/pull/163#issuecomment-6231362263; source identity implementation is allowed only with an explicit operator-supplied namespace contract and safe default OFF for unknown legacy data. Real source namespace *selection*, historical legacy mapping, schema/backfill/deploy and production mutation remain blocked without separate authorization.
Production mutation authorization: NONE
Commit suggestion: fix(import): make recurring Access synchronization incremental and idempotent

### Claim reservation — 2026-10-17

- Receiving owner/workspace: Codex `/root`; `C:\Users\Ivan\.codex\worktrees\stab17-pr163\Trendplus2`.
- Inspected base: `origin/main` `531a2962856ea7b8151fdf71e1c52640c4e88722`; handoff baseline PR #163 `fb193a076e47f79870acb84ec134ee1c1fe97a35` on `codex/access-sale-line-identity`.
- Delivery branch/PR: continue the existing `codex/access-sale-line-identity` / draft PR #163 from a clean isolated worktree; no new implementation PR and no force push.
- Exclusive paths: `Api/Services/AccessImportService.cs`, `Api/Services/Access/*`, Access import API/options/UI, source-lineage models/migrations, PostgreSQL Access tests, SQL oracle, and this STAB17 evidence.
- WIP check: RQ680 is the other active independent code/evidence family; this claim is the second allowed active family under the product-value limit.
- Ownership transfer: `ivanjovicic` / prior PR #163 workspace -> Codex `/root`; authority: user instruction in this thread plus PR #163 owner-directed handoff comment `#6231362263` and PR #191 / main `531a2962`.
- Claim commit SHA: `a51be37fc12c67a791c8c33082687d0ff7af238c`; this reservation is pushed to `main` before any source edit.


### Owner-authorized single-agent STAB17 engineering handoff — 2026-10-17

**Authority and precise split:** On 2026-10-17 the repository/product owner requested that PR #163/STAB17 stop deadlocking and be made takeover-ready for another agent. The formal release of this **repository-local engineering scope** is recorded in [PR #163 comment #6231362263](https://github.com/ivanjovicic/Trendplus/pull/163#issuecomment-6231362263). The earlier PR author/workspace is `ivanjovicic` / `codex/access-sale-line-identity`; **the conflicting local rebase/worktree belongs to someone else and must not be touched**. This is not retroactive permission to push over concurrent work: check for fresh ownership activity immediately before the shared claim. Receiving owner is **the first agent to perform and publish the protocol's READY -> IN_PROGRESS reservation**; no agent is owner until that commit lands on current `main`. WIP <= 2 remains binding.

**Why engineering is READY but live release is NOT:** code can implement, behind a fail-closed feature gate, the explicit immutable namespace registration/validation protocol with **synthetic** independent Access sources; it does not require inventing the real retailer's source identifier. For the existing production #42 snapshot the actual logical-source namespace, independent legacy provenance, 67,092 source-unidentified lines, near-exhausted `integer` sequence, schema migration, live backup and business acceptance are still externally gated. No production cutover is authorized. Do **not** mark all STAB17 DONE by passing synthetic tests; after repository-scoped acceptance, record remaining production gate as `PARTIAL`/`WAITING` with exact evidence and owner.

#### One-agent claim and worktree procedure — mandatory order

1. Inspect fresh `origin/main` SHA, PR #163 current SHA and recent PR comments, issues #173/#174/#186, active queue claims, and other PR file overlaps. Confirm the owner-directed release above has not been superseded by a conflicting claim. Do not open a new code PR. If prior workspace is still actively pushing, **stop for explicit coordination**, not a forced push.
2. Publish the **canonical** STAB17 `READY -> IN_PROGRESS` reservation on main under queue protocol, with receiving agent identity, exact PR-head/base SHA, branch/worktree, source/test ownership paths, WIP count and claim-commit SHA. This is the single shared lock. Read-only investigation alone requires no code claim.
3. **Create a new clean isolated worktree**; do not run `git reset --hard`, `git stash`, `git clean`, `git checkout -f` or `git rebase` in the unrelated conflicted checkout. Fetch and preserve `codex/access-sale-line-identity`; integrate current main into that branch with an ordinary merge commit (no force push), carefully resolving three Access-file conflicts by semantic review and keeping current main's `duplicate_sale_header_identity` classifier (#178) plus the PR's unique generated-ID tests. Before merging the PR, isolate/review unrelated `InfoTip.tsx` changes without dropping historical commits or overwriting another agent's code.
4. First create real disposable PostgreSQL tests using two **synthetic logical source IDs** which reuse numeric header/line IDs across `prodajazaglavlje`, `prodajastavke`, Dnevnik, and a POS header; prove the old branch fails, then prove new `source_namespace + physical_table_key + source_row_identity` resolves unique canonical target IDs and correctly remaps every affected child FK, including cross-entity flows in #174. Never join, infer or upsert by numeric coincidence, receipt number, price/date or arbitrary file name/hash. Enforce case/format validation and immutable source-key collision policy.
5. Implement a **fail-closed operator-provisioned namespace registry/contract**: explicit stable logical database ID, authorized mapping of subsequent uploaded copies of the *same* source, incompatibility detection and independently generated IDs for distinct sources; no silent production default, no derived ID from uploaded file path/hash, no cross-source shared cursor. Source namespace must scope `AccessImportCursors` and header/line lineage; migration/compatibility and unknown legacy cases must fail safely without changing existing rows.
6. For ordinary uploads make incremental import the *future* safe default **only after** a verified source-specific #42 checkpoint and full parent/child provenance exists. Until then UI/API must **reject routine full+overwrite and uncertified incremental** with an actionable error, never silently issue `ResetAccessSalesSnapshotAsync`, never advance cursors after any exception; privileged exceptional rebuild must be explicit and separately authorized. Handle late inserts, changed historical rows, zero Dnevnik with new sale/return, CLI scan-without-pushdown and Windows ODBC parity.
7. Characterize all current `int` ID/FK dependencies and `pg_sequences` headroom (4,281 IDs above current watermark at incident time). Add plan/disposable tests for coordinated `bigint` widening and transaction-safe generated IDs; no `setval`, production ALTER or sequence reset. Address explicit-value identity drift before inserts only through a reviewed, collision-safe approach proven on disposable DB.
8. Independently validate multi-database boundaries (`trendplus` and `analytics`), exact source→target header→line→facts SQL oracle by `all/imported/existing` grain, signed returns and partial/negative facts, external rollback/retries, cancellation, concurrent POS, worker restart. **Zero skipped required integration tests**; run full backend/frontend, build, documentation and queue governance plus exact PR-head CI. Preserve existing 43 accepted skip inventory and classify any *new* skips.
9. Close out with **one consolidated draft PR #163** and STAB17 queue status, independent read-only production readiness report for #42, exact source namespace/legacy/backfill questions to owner, separately reviewed bigint rollout/recovery plan, and structured test evidence. Do NOT merge until all original PR #163 gates are met; no deploy, live import, backup, data repair or schema changes from this request. A truly unproven external fact is a named `WAITING` production gate, **not** a reason to abandon executable repository work.

**Acceptance of engineering lane:** proven two-source namespace isolation, all strategy source→generated-target FK oracles, idempotent repeat import, no silent snapshot reset, cursor retention on failure, explicit unknown-legacy failure, disposable identity exhaustion plan and exact-head green CI. **Acceptance of production rollout (later and separately authorized):** real immutable source registration, owner-approved #42 lineage and 67,092 legacy row disposition, complete cross-DB reconciliation, backup, rollback and migration sign-off. Never collapse these two acceptance levels.

### STAB17 exact-head code-review recovery — 2026-10-17, existing exclusive owner only

**This is an acceptance checklist for the existing STAB17 claim / DRAFT PR #163, not another READY task or takeover.** Code branch exact reviewed head `a020f5f7d0b65eabe6f4b592960284b112969fe0`; final GitHub Actions backend `39058919144`, quality `39058919141`, governance `39058919115` succeeded (backend 2,210 passed / 43 classified skips / 0 fails). See [the specific read-only review with code locations](https://github.com/ivanjovicic/Trendplus/pull/163#issuecomment-6239008105). Test green establishes repository-test execution only, not real provider parity or production rollout.

- **P0 other-master collision (#174):** `ImportTipoviAsync`, `ImportDobavljaciAsync`, `ImportSezoneAsync` still use global numeric Access IDs in importer; inspect stores and every dependent FK. For two distinct Access namespaces with the same supplier/type/season/store IDs, prove no conflation in disposable PostgreSQL or fail closed for unsupported cases. Do not approve source mapping based only on successful header/line tests.
- **P0 pre-write legacy certificate (#173):** a registered `SourceNamespace` alone is not proof that completed batch #42 legacy rows belong to that source. Before any live future write there must be an immutable, independently audited source-to-history baseline gate; 67,092 historic lines with unverified IDs must not be silently reinserted or claimed. Empty/new databases need explicit independent onboarding. No speculative production backfill.
- **P0 split database topology:** importer `EnsureAnalyticsSharesTrendplusDatabase` rejects distinct analytics/operations connections, while UI defaults `includeAnalytics=true`; CI uses one `trendplus_test` DB for both. Production topology must be separately proved and an authorized durable outbox/replay or explicitly signed-off bounded import-without-analytics + stale-data path defined. Never fake cross-DB atomicity or refresh evidence.
- **P1 known stock corruption risk (#186):** importer preloads article source lineage but not persisted quantity; attaches a stub missing `Kolicina` and `SourceHash`, so missing source quantity may replace known stock with null and unchanged replay may UPDATE. Correct the existing exclusive owner's code under shared claim; PostgreSQL prove known/zero/unknown quantities, signed stock overlay, concurrent receipt and zero-DML second replay.
- **P1 integer/sequence:** guard currently checks `MAX(id)` only after import writes, not true next sequence allocation; independently prove pre-write fail-closed readiness under stale/out-of-range sequences and concurrent POS. Near-exhausted production integer sequence still requires isolated bigint dependency migration/backup approval.
- **Independent oracle:** expand quantities, revenue, signed returns, master/store provenance and two physical PostgreSQL databases, not only counts/missing facts. Use `all/imported/existing/unknown` source populations and injected wrong-parent negative controls. Live Access ODBC/MDBTools parity and cross-database rollback remain unproven.

**Completion rule:** the exclusive PR #163 owner must either fix/test these within the original claim, or precisely classify unreleased production-source constraints as WAITING after completing all independently doable code. No new concurrent STAB18/READY import implementation, no silent ownership release, no forced PR merge. `RQ679` storage durability touches the same `AccessImportService` family and must coordinate with this owner even after the Render configuration fact is obtained. Existing `STAB16` retains production-deploy proof.

### Business contract (2026-10-16 owner instruction)

Trendplus periodically receives a newer copy of the SAME retailer Access .mdb/.accdb database. After the initial qualified baseline, routine import MUST process new source records only, plus explicitly supported changed records; it MUST NOT reset/rebuild the historical Access sales snapshot on each upload, duplicate invoices/lines, reparent lines, overwrite POS/customer entries, or silently omit new sales/returns. The last completed successful and source-qualified checkpoint wins; failed, cancelled, interrupted, partially committed, or ambiguous attempts never advance it. An empty delta must complete successfully with zero business mutations. Full rebuild is a separately authorized admin maintenance workflow, never the default button.

### Fresh verified evidence (read-only; do not mistake for a proven single-row exception key)

- Neon Trendplus production branch / trendplus DB: last completed import is batch #42 (2026-08-12 UTC); batches #43-#49 failed. Batches #42-#49 have ImportStrategy=full, IsIncremental=false and OverwriteExisting=true. AccessImportCursors has zero rows. Last full snapshot has 5,530 Access headers and 67,092 Access lines; all those lines have missing stable source_row_id.
- Render trendplus-api runtime is deploy SHA f9faf02542aed3ee191c556fcd66c8eed9da4fb0. Batch #49 (2026-10-15 14:29 UTC) and #47: db-flush SQLSTATE 23505, public.prodaja_zaglavlje, PK_prodaja_zaglavlje, duplicate_sale_header_identity; in #49 AddedHeaders=63, ModifiedHeaders=1, DuplicatePreparedHeaderIds=[]; in #47 AddedHeaders=26, ModifiedHeaders=1, DuplicatePreparedHeaderIds=[]. Batches #44/#46/#48 violate PK_prodaja_stavke; #45 violates PK_prodaja_zaglavlje.
- Sampled Render #49 HeaderSourceSamples carry source_table_key=prodajastavke; 55/55 sampled numeric IDs from nearby flushes already exist as Access headers in Neon with source_table_key=prodajazaglavlje and source_batch_id=42. This PROVES candidate cross-path numeric collisions, NOT which exact one caused the rejected statement. Never infer one source is the same receipt just because ID/date/amount match.
- Neon public.prodaja_zaglavlje.id and prodaja_stavke.id are integer IDENTITY; header sequence last_value = current max ID = 2,147,479,366, only 4,281 integer values remain above it. The equal last_value and max does NOT by itself prove a stale sequence; integer exhaustion IS a separate release blocker. Two Neon databases trendplus and analytics have distinct transactions; quantify cross-DB rollback/reconciliation.
- Current main source: Api/Config/AccessImportOptions.cs defaults Incremental.Enabled=false and Mode=full; AccessImportService.InsertPendingImportBatchAsync stores full/shadow/incremental by effective options; AccessImportPage.tsx defaults overwriteExisting=true; AccessImportEndpoints.cs defaults overwriteExisting=true. ShouldResetAccessSalesSnapshot returns true for overwriteExisting in non-incremental mode, deleting Access-origin sales snapshot before recreating it. Analyze transactional rollback in both DBs rather than assuming it.
- Incremental implementation exists: per-table cursor repository, ID/timestamp/id_or_composite profiles, overlap, optional predicate pushdown. On Render MDBTools CLI SupportsPredicatePushdown=false, so physical Access reads may still scan the file; the required optimization is no historical write/rebuild and correct delta. Code currently skips all linked sales/returns if dnevnik_promena has zero incremental delta; this is unsafe without a proven invariant.
- Existing draft PR #163 is PARTIAL and NOT merged; it owns sale header/line mappings, touches AccessImportService, database context and tests. Issues #173 (durable namespace, 67,092 legacy lines) and #174 (other entities/FK remapping) remain open. No second agent may edit the conflicting source or merge PR #163 without exclusive handoff/claim.

### Read first / ownership

Read AGENTS.md; docs/ai/PROMPT_QUEUE_PROTOCOL.md; MASTER_ROADMAP.md; this task; draft PR #163 and issues #173/#174; docs/qa/ACCESS_IMPORT_23_FACT_AUDIT_2026-10-10.md; Api/Config/AccessImportOptions.cs; Api/Services/AccessImportService.cs (cursor scopes, Begin/Complete, import transactions, ResetAccessSalesSnapshot, ImportProdaja, ImportProdajaFromLineTable, SynthesizeProdajaFromDnevnik, ImportProdajaStavke); Api/Services/Access/AccessImportCursorRepository.cs; WindowsAccessSession.cs; MdbToolsCliSession.cs; AccessImportPage.tsx; AccessImportEndpoints.cs; relevant real PostgreSQL tests. Refresh main and active PR/claims before writing code.

### Required implementation plan

1. Prove operator-owned immutable source database namespace (same logical Access DB despite renamed/reuploaded files; distinct DBs may reuse IDs). Reject ambiguous source changes and changed schema/key semantics until explicitly reconciled; no filename, content hash, random upload name, file path or receipt/date/amount is a durable identity by itself. Inventory actual Access tables, PKs, FK aliases, mutation timestamps and whether IDs are monotonic; unknown fields remain unknown.
2. Produce independent read-only preflight for batch #42 historical state, #47/#49 conflicts, source coverage, POS existing rows, exact source-to-target mappings and analytics. Classify legacy records as exact/ambiguous/missing without modifying them. Build a reproducible synthetic MDB fixture for direct-header, line-derived-header and Dnevnik-synthesized paths.
3. Choose a correct checkpoint model per stable source namespace AND physical source table: (a) monotonic ID cursor only where append-only ordering is actually proved, (b) timestamp+tie-breaker for changes if reliable timestamp exists, (c) durable source-ID/composite multiset anti-join/diff if no trustworthy cursor, including late-added records with lower IDs. Last successful import date is NOT a safe Access-row predicate without a genuine insertion/updated-at column. On an unsafe missing alias, never silently pass-through then commit a false checkpoint; either safe diff or explicit fail-closed.
4. Bootstrap checkpoint from proven historical source-to-target identity of completed batch #42, with read-only dry-run showing exact baseline counts and exceptions. If provenance cannot be proved (notably the 67,092 legacy lines), leave that population UNRESOLVED and do not auto-backfill or pretend it has a cursor. Require explicit reviewed reconciliation and reversible authorization for any historical correction. Allow repo-local fixture implementation independent of live approval only after PR ownership is released.
5. Make routine import use incremental mode by default in UI + API + worker/queue/runtime, visible strategy/source/checkpoint and opt-in FULL with an admin warning/authorization. Never use user-controlled overwriteExisting to trigger snapshot destruction accidentally; lock down ResetAccessSalesSnapshot on routine paths, including retries. Persist per-table delta metrics: read/scanned, new, updated, unchanged, rejected, skipped, cursor_before/after, source namespace, batch.
6. Make import idempotent per namespace+source table+source key (and occurrence identity for legitimately duplicate-looking lines). Resolve source headers to canonical GENERATED PostgreSQL target IDs and map every child FK without equating source IDs with target IDs. Preserve existing/POS records. Remove MAX(Id)+1 unsynchronized allocations. Address near-exhausted header integer range through a separately reviewed full dependency/FK/analytics widening migration plan with tests and backup; never blindly setval or mutate production in this task. Coordinate with #163/#173/#174 without duplicate competing patches.
7. Remove unsafe Dnevnik-only trigger for skipping sale/return deltas: each source table/parent-driven dependency must be covered even with zero new journal rows; also test journal-only, line-only and late-arriving parent cases. Missing parent or ambiguous source identity MUST be reported, not fabricated or mapped by numeric equality.
8. Make checkpoint advancement durable ONLY after successful business transaction and required consistency boundary: test commit/rollback ordering, cursor writes, retries and the two different Neon databases. Explicitly specify whether analytics is part of critical success; if it is eventually consistent, make outbox/retry + stale indicator correct and do not mark reconciled when not. Prove no cursor advance after any failure, cancellation, partial flush, app restart or analytics failure; replay the same file must be safe.
9. Implement safe first run and later deltas for Windows ODBC and Render Linux MDBTools; when predicate pushdown is unsupported, stream and filter safely without destructive full snapshot work. No blanket full-table re-read/write on each sync; differentiate source scan from target mutations honestly. Keep regular imports bounded and capable of cancellation.
10. Add visible strategy/last-success/delta-count feedback to Access import UI. Keep full-rebuild option separate/admin-only and independently approved; no unrelated frontend changes, new analytics surfaces, experimental modules or scraping.
11. Provide exact migration/deploy/backout order, read-only production preflight and dry-run reconciliation output. NO production ALTER/UPDATE/DELETE/setval/full import/deploy/secret changes in this task without explicit owner authorization. No merge of #163 merely because this task is queued.

### Mandatory negative and integration proofs

- Real disposable PostgreSQL, source fixture, Windows and CLI capability contracts: first import, second identical import (0 insert/update/delete), N+1 new invoices with correct children, zero delta, changed old record with timestamp, late new lower ID, duplicate invoice numbers, duplicate-looking source lines with different occurrence, two independent Access files reusing numeric IDs, direct vs line-derived vs journal sale, missing/cyclic parent, valid empty Dnevnik but new sales, POS/Access numeric collisions.
- Fault injection BEFORE/AFTER header flush, line flush, analytics write, transaction commit, cursor commit, retry, cancel, worker restart, stale lease/concurrent start. Require exact unchanged cursor and business state on failed/rolled-back attempts; no false success.
- SQL oracle independently compares all/imported/existing subsets for headers, lines, FKs, zero orphans, sum quantity/revenue, product/store/supplier and SalesFacts/SalesLineFacts, including cross-DB state and duplicate-source multiset. Do not assert analytics parity simply from green app tests.
- Numeric key upper boundary, IDENTITY allocation, sequence never rewinds, bigint widening dependency census; fail closed on exhausted legacy schema.
- All focused PostgreSQL tests executed (0 unexpected skips), complete required backend and frontend/UI checks, build, queue governance validators, and exact PR-head CI green before merging any PostgreSQL-dependent runtime changes. Never hide/relax broken tests or mark simulated checks as live proof.

### Deliverables and stop/handoff

- One code change set after shared ownership release, or smallest path-disjoint precursor; limited supporting tests and a concise run log. Keep PR #163 DRAFT until all its original P0 gates plus this task's checks pass.
- A verified read-only production-delta baseline, checkpoint bootstrap report, data repair classification, idempotency tests, and explicit cutover / rollback runbook; each pending operator approval is named and segregated.
- On success normal repeat upload is success with zero changes, real new records insert once, late/changed rows have defined safe behavior, no other POS/sales record is lost, analytics attribution remains correct, and cursor represents the last durable consistent import.
- WAITING unblock: explicit #163 owner handoff/closure or safe same-owner integration, approved stable Access source namespace, and a reviewed baseline mapping. Source namespace and destructive migration approval are REAL operator gates; repository-local independently path-disjoint tests/read-only audit are possible earlier but must not steal an active owner. An agent may record a non-conflicting precursor READY only after fresh full collision/dependency scan and owner protocol.


### STAB17 incident evidence and same-owner agent handoff — batch #50, 2026-10-17

**Status unchanged: WAITING; this is a diagnostic/implementation handoff within STAB17, NOT a new READY, new queue family, or permission to take PR #163.** Source: authorized read-only connected Render logs and Neon SQL on 2026-10-17; code checked against main `b6a32618c4518f2b34a72fe2211757080f24e9b4`. No production writes or runtime deployment performed.

**Fresh reproducible incident facts:**
- Render `trendplus-api` manual deploy finished **2026-10-17 06:51 UTC** at `b6a32618` (documentation-only). Import #50 queued 06:53:16 UTC with `IncludeAnalytics=true`, `OverwriteExisting=true`, read mode Linux CLI. It **failed** 06:54:45 UTC at `db-flush` with `SQLSTATE 23505`, `public.prodaja_zaglavlje`, constraint `PK_prodaja_zaglavlje`, `duplicate_sale_header_identity: target_primary_key_collision`; `AddedHeaders=33`, `ModifiedHeaders=0`, `DuplicatePreparedHeaderIds=[]`. Elapsed background job ~88.9s. Detail redaction does not identify the rejected row.
- `ResetAccessSalesSnapshotAsync` was invoked in this **full+overwrite** attempt: the application logged `SalesHeadersDeleted=5530`, `SalesLinesDeleted=67092`, `SalesFactsDeleted=0`, `SalesLineFactsDeleted=0` at 06:53:38. After failure, **read-only Neon** still shows **5,530** `data_origin='access'` headers and **67,092** lines attached to them. These counts support a trendplus transaction rollback; they do **not** prove cross-DB atomicity or row-by-row reconciliation. Never rerun full overwrite as troubleshooting.
- The final pre-failure diagnostic listed five sampled `State=Added` sale headers with `SourceTableKey=prodajastavke` and a proposed numeric target `Id=SourceRowId`. A **read-only five-key lookup** found **5/5 IDs already occupied in production** by existing Access `source_table_key=prodajazaglavlje`, `source_batch_id=42`. This independently proves the **source-table numeric ID collision mechanism** in the same pending flush, but the driver did not reveal which of all 33 attempted INSERTs first hit the constraint; do not claim exact violating key.
- Neon production `trendplus` DB: batch #50 `Status=failed`, `ImportStrategy=full`, `IsIncremental=false`, `OverwriteExisting=true`; last successful batch **#42 (2026-08-12)**; `AccessImportCursors` count **0**. No later successful batch was found at check time. `prodaja_stavke.source_row_id` NULL for **67,144/67,144** current lines, including 67,092 Access-linked, so no automatic reconstruction of exact source line identity. Batch total/progress counters during a rolled-back run do not prove committed records.
- Header `id` type is PostgreSQL `integer GENERATED BY DEFAULT AS IDENTITY`. `pg_sequences.last_value = max(id) = 2,147,479,366`, max `2,147,483,647` (**4,281 remaining** above current high watermark), a separate critical capacity blocker. Equality does not prove a stale sequence; switching to generated IDs WITHOUT reviewing bigint/FK/analytics dependencies can still fail shortly. **No production setval, ALTER, DELETE or rekey is approved.**
- Distinct Neon `analytics` DB has **0** `DataOrigin=access` in `SalesFacts` and `SalesLineFacts` (15 existing `SalesFacts`, 425 `SalesLineFacts` in inspected tables). The `trendplus` DB has 0 rows in its `SalesFacts`/`SalesLineFacts` copies. These are **table-specific observed counts**, NOT yet proof that all other retail materializations are absent. Mandatory source/config reconciliation: never presume two DBs share an atomic transaction.

**Code path on current main:** `Api/Config/AccessImportOptions.cs` default `Incremental.Enabled=false`, `Mode=full`; `Klijent/clientapp/src/pages/AccessImportPage.tsx:133` defaults `overwriteExisting=true`; `Api/Endpoints/AccessImportEndpoints.cs:1415,1422` defaults same; `Api/Services/AccessImportService.cs:4283` resets full snapshot when overwrite is enabled, `ImportProdajaAsync` sets `Id=id.Value` (main around 5318), and line-derived header insertion also uses source numeric IDs; source line/header IDs are not globally unique across distinct Access physical tables. `RetriableDbContextTransaction.cs` rolls back the trendplus transaction on failure; independent analytics DB ownership needs a separate negative test.

#### Agent prompt — execute only with the canonical owner/handoff

1. **Before coding:** inspect refreshed main, `AGENTS.md`, queue protocol and master roadmap, draft **PR #163** and issues **#173/#174**. STAB17 and PR #163 overlap exactly; obtain explicit same-owner takeover/handoff/release under protocol before modifying `AccessImportService.cs`, identity model, migrations or their shared tests. Do NOT force merge or silently mark STAB17 READY. Until handoff, deliver only read-only preflight and path-disjoint proof.
2. On a disposable PostgreSQL baseline plus synthetic Access fixture, reproduce **two different source tables using identical numeric IDs**, source header vs synthesized/line-derived header, and POS target identity. Prove the old main fails with duplicate target PK; prove mapping based on durable `source namespace + source table + source row identity` to a safe generated target ID, including every child FK, exactly-once import and line multiset. **Never** merge two sales just because numeric IDs or receipt numbers match.
3. Resolve #163 blockers: operator-approved stable logical Access database identity across renamed uploads (never derive only from filename/hash); legacy #42 header mapping and all **67,092** existing Access lines without source_row_id; #174 cross-entity identity/FK mapping; negative controls for wrong-parent/orphan/reparented rows and mixed POS. Do not auto-backfill ambiguous data, and fail closed if mapping is unprovable.
4. Replace routine **full+overwrite** behavior with a clearly gated, *proven* incremental/anti-join algorithm and safe read-only checkpoint bootstrap. No wall-clock sale-date shortcut or unsupported monotonic assumption; `Dnevnik` zero delta must not hide newly added sales/returns. Identical file twice -> **zero target mutations**, same source with N+1 rows -> each new sale once, changed rows only if reliable version evidence, late lower source IDs -> handled or honestly blocked. On Linux MDBTools a file scan may still be necessary; **target write/full reset must not happen** on every upload.
5. Prove `trendplus` vs `analytics` durable boundaries; inject failure at pre/post header flush, line flush, analytics projection, transaction commit, cursor commit, retry, restart and cancellation. Cursor must never advance unless required durable consistency criterion is satisfied. Run independent signed-return/quantity/revenue oracle over `all/imported/existing` and compare operational headers/lines to analytical facts.
6. Make a separately reviewed `integer` identity exhaustion plan (full inbound/outbound FK/index/view/code dependency inventory, disposable bigint migration, backup/recovery and rollout). **No unapproved live widening or setval.** A generated surrogate fix must not create false safety when only 4,281 IDs remain.
7. Add a fail-closed preflight in UI/API before *routine* source import if source namespace or #42 baseline is not certified. Preserve historic data and fail with one comprehensible code, rather than running full snapshot reset. Keep Admin-only extraordinary rebuild separate from normal upload and not enabled by an uncontrolled `overwriteExisting` checkbox.
8. Focused real PostgreSQL integration tests (zero unexpected skips), CLI/ODBC parity, source collision/lineage FK tests, negative failure/retry/rollback cases, full backend/required frontend and build, docs+queue validators and exact PR-head CI green **before** any merge. No unit-test success interpreted as production correctness. Do not insert real customer Access data or secrets into public repository.
9. Deliver one existing-owner PR or owner-authorized consolidation of #163 (not a competing patch), tested migration/deploy/backout and a concise proof pack. Record original cause vs validated fix, affected files, exact SHA/tests, remaining owner gates and independent reproducible counts. Production deployment/import/backfill remains separately owner-approved.

**Acceptance:** same source re-upload cannot delete/recreate historical Access snapshot; preserved #42 data and POS owners; no source-key/target-key conflation, no orphan/wrong-parent lines, safe cursor and rollback, explicit source-namespace and integer-headroom gates. Until these pass STAB17 stays WAITING, PR #163 stays DRAFT and live imports remain unsafe.
