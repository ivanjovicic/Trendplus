# Trendplus GenAI Product Prompt Queue

Created: 2026-07-31
Repo: `ivanjovicic/Trendplus`
Status: dormant until the analytics reliability router and the stabilization/release/security queue have no unresolved P0 `READY`, `PARTIAL`, `BLOCKED` or `IN_PROGRESS` item.
Current gate verdict: BLOCKED by STAB08 refresh evidence on 2026-08-06.

## Goal

Build credible production-style Applied AI evidence without weakening the current Trendplus analytics product.

Target outcome:

- read-only retail analytics copilot;
- grounded in approved documents and typed backend tools;
- citations, evaluations, tracing and cost controls;
- no arbitrary SQL;
- no write actions in the first pilot;
- core Trendplus remains fully usable when AI is disabled.

## Queue rules

1. Read `AGENTS.md`, `.github/copilot-instructions.md`, `docs/ai/GENAI_COPILOT_ROADMAP.md`, `docs/security/GENAI_SECURITY_AND_DATA_BOUNDARIES.md` and `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`.
2. Confirm the stabilization queue and analytics reliability router have no unresolved P0 `READY`/`PARTIAL`/`BLOCKED`/`IN_PROGRESS` items.
3. Take only the first task with `Status: READY` (or the first unblocked `WAITING` task after promoting it to `READY`) whose dependencies are DONE.
4. Change it to `IN_PROGRESS` before implementation.
5. Use one task and one focused commit per session.
6. Finish as `DONE`, `PARTIAL` or `BLOCKED` with files, checks, risks and next step.
7. Never skip a P0 security/evaluation gate to start an LLM UI or agent.
8. Do not add a paid provider dependency unless the task explicitly allows it.
9. Do not use real customer data in development fixtures.
10. Stop if auth, scope, provider policy or source-of-truth ownership is unclear.
11. Use only protocol statuses: `READY`, `WAITING`, `IN_PROGRESS`, `BLOCKED`, `PARTIAL`, `DONE`, `OBSOLETE`. Never use `TODO` or `OPEN`.

## Global stop conditions

Stop and report instead of guessing when:

- a task requires arbitrary SQL or unrestricted database access;
- a model/provider secret would enter source, logs, browser or test fixtures;
- a public route would expose the Python service directly;
- store/tenant/user scope cannot be enforced before retrieval/tool execution;
- an AI response would hide stale, partial, unknown or error states;
- a write tool is proposed before a separate approval and audit design;
- more than 6-8 files must change without an explicit split plan;
- the AI feature becomes a dependency of core analytics startup or routes.

---

## GAI01  -  GenAI runtime and data-boundary readiness audit

Status: DONE
Priority: P0
Type: docs/audit
Token budget: medium
Commit suggestion: `docs(ai): audit genai runtime readiness`

### Why

The repository has an image embedding FastAPI service, .NET adapters, pgvector-oriented code and several Python services, but their active runtime and security posture are not yet proven. This must be understood before adding text RAG or agents.

### Scope only

- `EmbeddingService/*`
- `Infrastructure/Services/EmbeddingService.cs`
- `Application/Common/Interfaces/IEmbeddingService.cs`
- `Api/Program.cs` registration/configuration sections
- relevant appsettings/deploy files
- new `docs/qa/GENAI_RUNTIME_READINESS_AUDIT.md`

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Create an evidence-based GenAI runtime and data-boundary readiness audit.

Read first:
- AGENTS.md
- .github/copilot-instructions.md
- docs/ai/GENAI_COPILOT_ROADMAP.md
- docs/security/GENAI_SECURITY_AND_DATA_BOUNDARIES.md
- EmbeddingService/README.md
- EmbeddingService/app.py
- Infrastructure/Services/EmbeddingService.cs

Do:
1. Map every AI/ML/Python runtime, route, configuration key and deployment reference that could be confused with the planned GenAI copilot.
2. Determine whether MockEmbeddingService, PythonEmbeddingService and the FastAPI service are active, dormant, optional or unreachable in local, test and production environments.
3. Record authentication, CORS, request-size, timeout, concurrency, error-redaction, startup/model-download and secret-handling behavior.
4. Map current pgvector schema/use and prove whether it is image-only, text-ready or ambiguous.
5. Classify permitted data for offline prototype, internal alpha and customer pilot.
6. Create docs/qa/GENAI_RUNTIME_READINESS_AUDIT.md with PASS/WARN/BLOCKED rows and exact evidence paths.
7. Recommend the smallest next code task; do not implement it here.

Do not:
- add an LLM provider
- add text RAG
- change runtime behavior
- claim production readiness

Acceptance:
- Active versus dormant AI code is explicit.
- Production selection and exposure risks are documented.
- Image and future text embedding boundaries are explicit.
- The next hardening task is small and evidence-based.

### Completion note

- Date: 2026-08-05
- Agent: Cursor-Composer
- Changed: `docs/qa/GENAI_RUNTIME_READINESS_AUDIT.md`, this queue
- Findings: mock embedding service is the default active path; Python/FastAPI image service is dormant unless config flips; production deploy files do not wire the Python service; image upload/search paths are exposed without service auth; pgvector contains image and text feature-vector tables but no approved text-RAG runtime
- Checks: `git diff --check` pass
- Risk: enabling `EmbeddingService:UseMock=false` without additional hardening would expose unauthenticated Python service calls and raw exception text
- Next: `GAI02`

Checks:
- git diff --check
```

---

## GAI02  -  Quarantine or harden the existing image embedding path

Status: DONE
> Completed: 2026-08-05. Hardened the image embedding boundary with explicit startup policy validation, mock-production fail-closed guards, bounded uploads, safe validation/errors, and timeout handling. Changed files: `Api/Config/EmbeddingServiceRuntimePolicy.cs`, `Api/Program.cs`, `Api.Tests/EmbeddingServiceRuntimePolicyTests.cs`, `Infrastructure/Services/EmbeddingService.cs`, `EmbeddingService/app.py`, `EmbeddingService/README.md`. Checks: `dotnet build Trendplus2.sln --configuration Release` pass; `dotnet test Api.Tests --configuration Release --filter FullyQualifiedName~EmbeddingServiceRuntimePolicyTests` pass; `python -m py_compile EmbeddingService/app.py` pass; targeted `git diff --check` clean, with only CRLF warnings.

Priority: P0
Type: backend/python security
Depends on: GAI01
Token budget: medium
Commit suggestion: `fix(ai): harden image embedding boundary`

### Why

Random mock vectors, unauthenticated service calls or unbounded uploads would undermine similarity results and create a misleading production AI claim.

### Scope only

- files proven by GAI01 to own image embedding configuration/runtime
- focused Python and/or .NET tests
- no text RAG files

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Apply the smallest safe hardening or quarantine plan from GAI01 to the existing image embedding path.

Do:
1. Make the production implementation choice explicit and fail closed.
2. Ensure random MockEmbeddingService cannot be selected in production.
3. Add configuration validation for model type, base URL and environment enable flag.
4. Add bounded upload size, bounded batch count, safe image validation and safe error messages where the Python service is active.
5. Add service-to-service authentication or keep the service unreachable outside approved local/private networking, according to the existing auth pattern.
6. Add timeout/cancellation and focused tests.
7. Update the image embedding README to state actual runtime behavior and limitations.

Do not:
- combine image and text vectors
- expose a new public route
- add a hosted LLM
- broaden into image-search redesign

Acceptance:
- Production cannot silently use random embeddings.
- Active endpoints have bounded input and safe errors.
- Configuration is explicit and off/fail-closed when incomplete.
- Tests prove the selected safety behavior.

Checks:
- relevant Python tests/lint if infrastructure exists
- dotnet build Trendplus2.sln --configuration Release
- focused .NET tests
- git diff --check
```

---

## GAI03  -  Retail Analytics Copilot PRD and product metrics

Status: WAITING
Priority: P0
Type: product/docs
Depends on: GAI01
Token budget: low
Commit suggestion: `docs(product): define analytics copilot prd`

### Scope only

- new `docs/product/RETAIL_ANALYTICS_COPILOT_PRD.md`
- optional small roadmap cross-link

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Define a narrow, measurable PRD for the first read-only Retail Analytics Copilot.

Do:
1. Define target user, top 5 jobs-to-be-done and explicit non-goals.
2. Select at most 3 MVP question flows from product, supplier, inventory, margin, data quality or methodology.
3. Define answer contract: source, period, scope, citations, confidence, freshness, warnings and unknown behavior.
4. Define product metrics with baseline method: task success, time-to-insight, acceptance/edit/rejection, repeat usage and support incidents.
5. Define feature-flag, kill-switch and rollout stages.
6. Define what would count as CV-quality evidence without inventing results.

Do not:
- choose framework before contracts
- include write actions
- include arbitrary free-form SQL
- promise autonomous decisions

Acceptance:
- MVP is narrow enough for one internal pilot.
- Success metrics and baseline method are measurable.
- Non-goals prevent feature creep.

Checks:
- git diff --check
```

---

## GAI04  -  Golden evaluation dataset and provider-free harness

Status: WAITING
Priority: P0
Type: tests/evaluation
Depends on: GAI03
Token budget: medium
Commit suggestion: `test(ai): add copilot golden evaluation harness`

### Scope only

- `tests` or a new focused GenAI evaluation directory
- synthetic/versioned fixtures
- no real customer data
- no paid provider call in required CI

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Create the first provider-free evaluation harness and golden case schema for the Retail Analytics Copilot.

Read first:
- docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md
- docs/product/RETAIL_ANALYTICS_COPILOT_PRD.md

Do:
1. Implement a machine-readable golden case schema.
2. Add at least 20 synthetic seed cases covering the selected MVP flows, insufficient data, stale data, dependency failure, unauthorized scope, direct injection and indirect injection.
3. Add validation that every case declares expected outcome, approved sources/tools and forbidden tools.
4. Add deterministic scoring helpers for outcome, exact numbers, warnings, citations and tool selection.
5. Add a CLI/test command that runs without provider credentials.
6. Document how the set grows to the internal/pilot thresholds.

Do not:
- call a paid model in required CI
- use production data
- use subjective LLM-as-judge as the only scorer

Acceptance:
- CI can validate fixture quality and deterministic scoring.
- Security and unknown-state cases exist before model integration.
- Results are reproducible.

Checks:
- focused test command
- git diff --check
```

---

## GAI05  -  Typed read-only analytics tool contracts

Status: WAITING
Priority: P0
Type: backend/contracts/tests
Depends on: GAI03, GAI04
Token budget: medium
Commit suggestion: `feat(ai): define read only analytics tools`

### Scope only

- new isolated .NET AI tool contract namespace
- adapters around existing services/endpoints
- focused contract/auth tests
- no LLM provider

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Implement a minimal typed read-only tool registry for the selected Copilot MVP flows without adding an LLM.

Do:
1. Define versioned tool descriptors with role, scope, input, output, max date range, result limit, timeout and read-only marker.
2. Wrap existing backend source-of-truth services; do not duplicate formulas.
3. Start with at most 3 tools selected by the PRD.
4. Preserve requested/effective period, data scope, freshness, data quality, confidence, reason codes, warnings and correlation ID.
5. Reject unknown tools and out-of-policy parameters deterministically.
6. Add authorization/scope, success, empty, warning and dependency-error tests.
7. Keep the registry internal; do not expose MCP or a public generic executor.

Do not:
- execute arbitrary SQL
- allow arbitrary endpoint URLs
- add writes
- let frontend define tool permissions

Acceptance:
- Tools can be invoked deterministically in tests without an LLM.
- Scope and role checks are server-side.
- Existing analytics remains the source of truth.

Checks:
- dotnet build Trendplus2.sln --configuration Release
- focused unit/integration tests
- git diff --check
```

---

## GAI06  -  Off-by-default Python AI orchestration service skeleton

Status: WAITING
Priority: P1
Type: python/platform
Depends on: GAI04, GAI05
Token budget: medium
Commit suggestion: `feat(ai): add disabled orchestration service skeleton`

### Scope only

- new dedicated text/GenAI service directory
- configuration, health/readiness, provider interface and tests
- no user-facing route
- no production provider required

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Add a separate off-by-default Python/FastAPI orchestration service skeleton for text GenAI.

Do:
1. Keep it separate from EmbeddingService image code.
2. Add typed settings with enable flag, environment validation and no secret defaults.
3. Add health and readiness that do not reveal secrets.
4. Add provider adapter interface plus a deterministic fake provider for tests only.
5. Add request ID propagation, structured safe logging, timeout, cancellation and bounded input/context settings.
6. Add Docker/local instructions and tests.
7. Ensure core Trendplus build/runtime does not depend on this service.

Do not:
- expose a public chat endpoint
- download a large model during core API startup
- add a real provider secret
- add RAG or tools yet

Acceptance:
- Service is disabled by default and testable without network/provider credentials.
- Missing configuration fails closed.
- Core analytics remains independent.

Checks:
- focused Python tests
- container/config validation if available
- git diff --check
```

---

## GAI07  -  Approved-document RAG prototype with citations

Status: WAITING
Priority: P1
Type: python/retrieval/tests
Depends on: GAI04, GAI06
Token budget: medium/high
Commit suggestion: `feat(ai): add approved docs rag prototype`

### Scope only

- approved methodology/help docs only
- separate text vector namespace/schema/index
- ingestion, retrieval and tests
- no operational/customer data

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Build a local/internal RAG prototype over approved Trendplus methodology and help documents only.

Do:
1. Define an explicit source manifest/allowlist.
2. Store source ID, version, checksum, heading, chunk, scope and deletion state.
3. Use a separate text index/namespace from image embeddings.
4. Implement deterministic ingestion, re-index and delete-by-source/version.
5. Implement retrieval with bounded top-k and source citations.
6. Treat retrieved text as untrusted evidence and detect/test indirect instruction content.
7. Add retrieval tests against the golden dataset.
8. Produce insufficient_data when evidence is weak or missing.

Do not:
- index customer sales/stock/supplier data
- browse external websites
- mix image and text vector dimensions/tables
- claim answer correctness from similarity score alone

Acceptance:
- Approved sources are versioned and deletable.
- Retrieval and citation tests pass.
- Malicious document instructions cannot change tool/security policy.

Checks:
- focused retrieval tests
- migration/schema tests if database changes
- git diff --check
```

---

## GAI08  -  Read-only tool orchestration with grounded answer contract

Status: WAITING
Priority: P1
Type: integration/tests
Depends on: GAI05, GAI06, GAI07
Token budget: high
Commit suggestion: `feat(ai): orchestrate grounded read only answers`

### Scope only

- selected MVP tools
- approved document retrieval
- internal service-to-service path
- no frontend UI

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Integrate the approved document retriever and typed read-only analytics tools into a grounded answer pipeline.

Do:
1. Allow only the registered MVP tools.
2. Make the .NET gateway the authority for role/scope and tool approval.
3. Preserve all tool inputs, outputs, citations, warnings and trace IDs in a structured redacted trace.
4. Require the answer contract from the roadmap.
5. Add deterministic fallback for provider unavailable, tool unavailable, insufficient evidence and partial source failure.
6. Add prompt-injection tests proving hidden/forbidden tools cannot be called.
7. Run the golden evaluation suite and publish an evidence summary without inventing metrics.

Do not:
- add write tools
- add arbitrary SQL or URL tools
- expose a public generic tool executor
- hide source failure behind model prose

Acceptance:
- Answers are source-backed and cite their evidence.
- Numerical values match tool results under evaluation.
- Auth/scope and forbidden-tool cases pass 100%.

Checks:
- .NET contract/integration tests
- Python orchestration tests
- golden evaluation command
- git diff --check
```

---

## GAI09  -  LLM observability, privacy and cost budget

Status: WAITING
Priority: P1
Type: observability/ops/tests
Depends on: GAI08
Token budget: medium
Commit suggestion: `feat(ai): add trace and cost guardrails`

### Scope only

- structured trace schema
- OpenTelemetry integration/hooks
- redaction, retention and budgets
- no UI redesign

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Add privacy-safe observability and enforceable latency/token/cost budgets for the internal Copilot pipeline.

Do:
1. Define trace fields: user ID reference, role, scope, request ID, prompt version, provider/model version, tools, sources, latency, tokens, cost class and outcome.
2. Redact or omit raw sensitive prompt/tool content by default.
3. Add per-request and per-user rate/token/context limits.
4. Add timeout, cancellation, retry and circuit-breaker behavior.
5. Add a provider and feature kill switch.
6. Add tests for redaction, budget rejection, timeout and provider-disabled fallback.
7. Document retention and incident lookup.

Do not:
- log secrets or raw connection details
- make third-party trace capture mandatory
- let observability failure break core analytics

Acceptance:
- Every AI request has a trace ID and bounded resource use.
- Sensitive fields are not logged by default.
- The feature can be disabled immediately.

Checks:
- focused tests
- build/lint for touched projects
- git diff --check
```

---

## GAI10  -  Feature-flagged internal Copilot UI

Status: WAITING
Priority: P2
Type: frontend/integration/tests
Depends on: GAI08, GAI09
Token budget: medium/high
Commit suggestion: `feat(ai): add internal analytics copilot ui`

### Scope only

- one internal feature-flagged route or panel
- selected MVP flows
- shared analytics trust/error/empty components
- no write controls

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Add a read-only internal Retail Analytics Copilot UI behind a disabled-by-default feature flag.

Do:
1. Reuse existing auth, route, trust, error, empty, formatter and response-meta patterns.
2. Show answer, requested/effective period, data scope, citations, confidence/reliability, freshness, data-quality warnings and trace ID.
3. Show honest provider/tool/retrieval errors without fake answer content.
4. Add structured thumbs-up/down and failure-reason feedback without storing unnecessary sensitive text.
5. Make decision-support wording explicit; no guaranteed recommendation copy.
6. Add route/page tests for disabled, unauthorized, success, insufficient, partial and error states.
7. Keep core analytics routes independent from the AI service.

Do not:
- add write buttons
- expose chain-of-thought
- call provider or Python service directly from browser
- replace existing analytics screens

Acceptance:
- Feature is invisible/off by default.
- Citations and warnings are prominent.
- AI outage does not degrade normal Trendplus analytics.

Checks:
- npm run check:analytics-guardrails
- npm run build
- focused frontend tests
- relevant backend tests
- git diff --check
```

---

## GAI11  -  Internal evaluation and release evidence

Status: WAITING
Priority: P1
Type: QA/product evidence
Depends on: GAI10
Token budget: medium
Commit suggestion: `docs(qa): record copilot internal evaluation`

### Scope only

- evaluation execution
- dated release report
- no broad feature work

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Run the complete internal GenAI evaluation gate and record an honest release verdict.

Do:
1. Pin commit, environment, provider/model, prompt, tool registry and corpus versions.
2. Run deterministic tool, retrieval, orchestration, security and UI tests.
3. Run the current golden set and report category-level results.
4. Record numerical exactness, citations, warnings, auth isolation, injection cases, latency, tokens and cost.
5. Run a small human task comparison with a documented baseline if suitable internal users are available; otherwise mark it not run.
6. Create a dated report using the canonical template.
7. Set verdict to NOT READY, INTERNAL ONLY or PILOT READY WITH WARNINGS based only on evidence.

Do not:
- change prompts repeatedly until only the test set passes without documenting the change
- hide failed cases in averages
- claim business impact without a baseline

Acceptance:
- Release verdict is reproducible and evidence-based.
- Zero-tolerance blockers are explicit.
- Remaining failures become narrow follow-up queue items.

Checks:
- all documented evaluation commands
- git diff --check
```

---

## GAI12  -  MCP and controlled customer-pilot readiness review

Status: WAITING
Priority: P2
Type: architecture/security review
Depends on: GAI11 with no zero-tolerance blocker
Token budget: medium
Commit suggestion: `docs(ai): review mcp and pilot readiness`

### Scope only

- docs/review
- no MCP server implementation
- no write tools

### Prompt

```text
Repo: ivanjovicic/Trendplus

Task:
Review whether the proven internal tool registry should be exposed through MCP and whether a controlled customer pilot is justified.

Do:
1. Compare direct internal gateway tools versus MCP for this repository.
2. Prove that auth, role, scope, audit, rate, cost and tool allowlist semantics would remain enforced outside the model.
3. Identify which tools, if any, are safe to expose read-only.
4. Define pilot user/scope, provider/data approval, retention, incident owner, cost ceiling and kill-switch proof.
5. Keep the verdict NOT READY unless GAI11 evidence and all P0 controls support the move.
6. Create follow-up implementation prompts only for approved, read-only scope.

Do not:
- use MCP as a shortcut around the .NET gateway
- expose admin, import, worker, cache, pricing or action writes
- implement autonomous agents

Acceptance:
- MCP decision is evidence-based, not framework-driven.
- Customer-pilot readiness has explicit approval and rollback gates.

Checks:
- git diff --check
```

## Expected CV evidence after completion

Only after the corresponding tasks and evaluation evidence are complete, the project may support statements such as:

- designed and delivered a feature-flagged read-only retail analytics copilot across .NET and Python;
- implemented typed tool calling and document RAG with source citations;
- built provider-free deterministic evaluation plus pinned model regression tests;
- enforced prompt-injection, authorization, scope, token/cost and observability controls;
- measured internal or pilot task outcomes against a documented baseline.

Do not add unmeasured percentages or production claims before GAI11 evidence exists.
