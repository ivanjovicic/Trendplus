# Trendplus GenAI Copilot Roadmap

Updated: 2026-07-31
Status: planned; not approved for public or production use

## Purpose

Trendplus can become a strong applied-AI portfolio and product case by adding a grounded retail analytics copilot that follows the existing decision flow:

`question -> approved data/tool retrieval -> grounded explanation -> cited evidence -> human decision`

The copilot must extend the existing Analytics Decision OS. It must not replace backend decision logic, invent financial values, execute arbitrary SQL, or perform business writes without a separately approved workflow.

## Current repository reality

### Existing assets that reduce implementation risk

- Mature .NET analytics endpoints and decision contracts.
- Product, supplier, inventory, data-quality, action and decision-board surfaces.
- PostgreSQL and existing pgvector-oriented image similarity code.
- A Python/FastAPI image embedding service using CLIP or SigLIP.
- A .NET `PythonEmbeddingService` adapter.
- Redis/cache, Docker, structured logging, correlation IDs and CI coverage.
- Strong no-fake-zero, no-fake-green, nullable evidence and explainability standards.

### What does not yet exist

- A text/document RAG pipeline.
- A production LLM provider abstraction and provider/data policy.
- A dedicated AI gateway with authorization, data-scope enforcement and tool allowlists.
- Golden evaluation datasets and repeatable LLM regression tests.
- Prompt/version management, trace storage and cost budgets.
- Prompt-injection and indirect-injection controls.
- A safe agent/tool execution contract.
- A user-facing analytics copilot with citations.
- Proven tenant isolation for AI retrieval and tools.

The existing image embedding service is useful experience, but it is not evidence of a production RAG or agentic analytics system.

## Safety decision

### Safe to add now

- Architecture and product documentation.
- Threat modelling and data-boundary rules.
- Golden questions, expected sources and deterministic evaluation fixtures.
- Read-only tool contract design around existing analytics endpoints.
- An off-by-default local service skeleton with no public route and no production secrets.
- Offline experiments against synthetic or explicitly approved non-customer data.

### Safe only after explicit gates pass

- Third-party LLM calls with real business data.
- Persistent vector indexes containing customer or operational data.
- Public or pilot-facing chat UI.
- MCP or general-purpose tool exposure.
- Multi-step agents.

### Not safe in the first implementation wave

- Arbitrary model-generated SQL execution.
- Direct LLM access to the operational database.
- Frontend-to-provider calls or provider secrets in the browser.
- Automatic creation, closing or approval of analytics actions.
- Price, replenishment, markdown, import, cache, worker or admin writes.
- Silent fallback from unavailable evidence to a plausible answer.
- Reusing random mock embeddings in production paths.

## Target product scope

### Primary use case

A read-only **Trendplus Retail Analytics Copilot** that answers questions such as:

- Why did margin change in the selected period?
- Which products need review because of dead stock or OOS risk?
- Which suppliers have the strongest or weakest evidence-backed performance?
- What data-quality problems reduce confidence in this recommendation?
- Which existing analytics screen or report contains the supporting evidence?

### Required answer contract

Every successful answer should include:

- concise answer;
- requested and effective period;
- requested and effective data scope;
- cited Trendplus sources or tool calls;
- confidence/reliability state derived from backend evidence;
- data-quality and freshness warnings;
- explicit unknown/insufficient-data behavior;
- correlation/trace identifier;
- no action execution.

## Proposed architecture boundary

```text
React UI
  -> .NET AI Gateway
       - authentication and role check
       - store/data-scope enforcement
       - rate and cost limits
       - prompt/input validation
       - approved tool registry
       - audit/correlation IDs
  -> Python AI Orchestration Service
       - prompt templates and versions
       - retrieval/orchestration
       - provider adapter
       - evaluation hooks
       - no direct unrestricted database access
  -> Approved read-only sources
       - existing .NET analytics endpoints/services
       - approved methodology and help documents
       - separate vector index with source metadata
```

Rules:

- Backend remains the source of truth for financial metrics, decisions, confidence and reasons.
- The model summarizes and orchestrates; it does not redefine business formulas.
- Tools are explicit, typed, read-only and allowlisted.
- Retrieval records carry source ID, data scope, freshness, document version and authorization metadata.
- The existing image embedding service remains a separate capability until it passes its own security and reliability audit.

## Phased delivery

## Phase 0 — Readiness and quarantine

Goal: prove what AI-related code is active and prevent dormant prototypes from becoming accidental production dependencies.

Deliverables:

- inventory of `EmbeddingService`, .NET adapters, pgvector schema and runtime registration;
- confirmation whether mock or Python embedding implementation can run in each environment;
- explicit off-by-default configuration;
- provider/data policy;
- security and privacy review.

Exit gate:

- no random mock embedding can be selected in production;
- no unauthenticated AI endpoint is publicly exposed;
- secrets and provider configuration have a documented owner;
- permitted data classes are explicit.

## Phase 1 — Product contract and evaluation-first design

Goal: define the user problem and tests before choosing frameworks.

Deliverables:

- narrow PRD and non-goals;
- 50–100 representative golden questions;
- expected tools/sources, expected warnings and refusal cases;
- product metrics: task success, time-to-insight, answer acceptance and repeat usage;
- engineering metrics: groundedness, numerical exactness, latency and cost.

Exit gate:

- each planned capability has an evaluation case;
- no production claim depends only on manual demos.

## Phase 2 — Read-only tool foundation

Goal: expose a small typed tool set without an LLM dependency.

Initial tools should wrap existing source-of-truth services, for example:

- analytics freshness and data-quality status;
- product decision summary;
- supplier decision summary;
- inventory risk summary;
- decision-board read model;
- methodology/source lookup.

Exit gate:

- authorization and data scope are tested server-side;
- no arbitrary URL, SQL or endpoint execution;
- deterministic tool contract tests pass;
- unknown/error/partial states remain visible.

## Phase 3 — Off-by-default AI service skeleton

Goal: create the operational shell without exposing product functionality.

Deliverables:

- Python/FastAPI service with configuration validation and health/readiness;
- provider adapter interface;
- request IDs, structured logs and timeouts;
- token/cost accounting hooks;
- Docker and local-only run instructions;
- CI tests that require no paid provider key.

Exit gate:

- disabled by default;
- no provider secret in source, logs, browser or test fixtures;
- startup does not download large models unexpectedly in the web process;
- failure cannot degrade core Trendplus analytics.

## Phase 4 — Grounded RAG over approved documents

Goal: prove retrieval and citations before operational data access.

Initial corpus:

- analytics methodology;
- KPI definitions;
- data-quality explanations;
- operator runbooks;
- approved product help content.

Do not index customer data in this phase.

Exit gate:

- citation accuracy and source authorization pass the evaluation gate;
- malicious/indirect prompt content is tested;
- documents can be re-indexed and deleted by source/version;
- retrieval failures produce unknown, not a fabricated answer.

## Phase 5 — Read-only analytics tool calling

Goal: combine document grounding with typed analytics tools.

Deliverables:

- model may choose only from the approved registry;
- tool results are structured and preserved in the trace;
- answer cites both tool and document evidence;
- no write tools;
- deterministic fallback when the model/provider is unavailable.

Exit gate:

- numerical exactness is measured against backend results;
- scope/auth isolation tests are 100% passing;
- prompt injection cannot call hidden or unapproved tools;
- latency and cost remain within the approved budget.

## Phase 6 — Internal copilot UI

Goal: validate usefulness with a small internal group.

Deliverables:

- feature-flagged route;
- read-only UX;
- visible citations, freshness, confidence and warnings;
- thumbs-up/down plus structured failure reason;
- trace ID available for support;
- no core analytics route dependency.

Exit gate:

- internal evaluation results are recorded;
- critical errors, data leakage and unauthorized tool calls are zero;
- the feature can be disabled without deploy or database repair.

## Phase 7 — Controlled pilot

Goal: gather measurable product evidence without granting autonomous authority.

Requirements:

- named pilot users and permitted data scopes;
- provider and retention approval;
- cost ceiling and rate limits;
- incident and kill-switch runbook;
- evaluation report against the current release candidate;
- explicit user messaging that answers are decision support, not guaranteed instructions.

## Framework guidance

Frameworks are implementation choices, not product proof.

A reasonable learning path for this repository is:

- Python, FastAPI, Pydantic and Pytest;
- one orchestration framework only after the plain contracts work;
- LangGraph or Semantic Kernel for explicit stateful flows;
- PostgreSQL/pgvector for a first controlled retrieval index;
- OpenTelemetry plus an LLM trace/evaluation product if approved;
- Azure AI/OpenAI or another provider only through a server-side adapter.

MCP should be evaluated late, after the same tools already have authorization, scope and audit guarantees. MCP must not be used as a shortcut around the gateway.

## CV-quality evidence target

The work becomes credible CV evidence when the repository can prove:

- a deployed, feature-flagged read-only AI product;
- typed RAG/tool architecture integrated with .NET and Python;
- a documented evaluation set and automated regression results;
- prompt-injection, auth and data-scope tests;
- tracing, latency and token/cost metrics;
- measured pilot product outcome such as reduced analysis time or increased task completion;
- honest limitations and rollback controls.

A framework list without these artifacts is not sufficient.

## Canonical companion documents

- `docs/security/GENAI_SECURITY_AND_DATA_BOUNDARIES.md`
- `docs/qa/GENAI_EVALUATION_AND_RELEASE_GATE.md`
- `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md`
- `docs/Analytics/ANALYTICS_DECISION_OS_ROADMAP.md`
- `docs/ai/ARCHITECTURE_BOUNDARIES.md`
