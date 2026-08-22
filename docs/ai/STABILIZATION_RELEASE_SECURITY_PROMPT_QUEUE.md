# Trendplus Pilot Stabilization, Release and Security Prompt Queue

Created: 2026-08-04
Repo: `ivanjovicic/Trendplus`
Queue state: active cross-cutting queue; it supplements, and does not replace, the analytics reliability queues.
Current READY prompt: none (`STAB14` is DONE; see completion note)
Current gate verdict: `STAB13` evidence refresh pack is on main. Fresh current-main live-smoke proof has now been captured and synchronized in `docs/qa/PILOT_RELEASE_EVIDENCE_REFRESH_2026-08-22.md`; GenAI remains BLOCKED because the broader backend gate is still red.

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
5. One task per session and commit. Do not combine this queue with an analytics formula fix.

## Queue rules

- Follow `docs/ai/PROMPT_QUEUE_PROTOCOL.md` exactly.
- Use only: `READY`, `WAITING`, `IN_PROGRESS`, `BLOCKED`, `PARTIAL`, `DONE`, `OBSOLETE`.
- Create a local uncommitted lock under `.ai/task-locks/` before work.
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

Status: PARTIAL
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
- Status: PARTIAL
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
- Prompt defect / scope repair: same-owner UI and harness repairs so the local analytics gate closes truthfully without inventing current-main proof

---

## STAB15 - Production analytics non-empty smoke against exact deploy SHA

Status: WAITING
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
- Do not broaden into general load/performance/security work; this prompt is only about exact-SHA production analytics visibility truth.
