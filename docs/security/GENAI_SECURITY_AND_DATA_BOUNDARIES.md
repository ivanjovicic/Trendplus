# GenAI Security and Data Boundaries

Updated: 2026-07-31
Status: mandatory design gate before Trendplus GenAI implementation

## Scope

This document defines the minimum security, privacy and operational boundaries for any Trendplus feature that uses:

- an LLM or hosted model provider;
- text/document embeddings;
- retrieval-augmented generation;
- agent or tool calling;
- MCP or a similar tool protocol;
- persistent prompts, conversations, traces or vector indexes.

It complements the existing analytics access-control, data-safety and architecture standards. It does not declare the current repository ready for production GenAI.

## Core rule

A model is an untrusted reasoning component.

It may propose a tool call or compose an explanation, but it must not decide authorization, data scope, financial truth, write permissions or secret access.

## Required trust boundaries

```text
Browser
  -> authenticated Trendplus backend / AI gateway
       -> approved provider adapter
       -> approved read-only tools
       -> approved retrieval index
```

Prohibited paths:

- browser -> model provider;
- browser -> Python AI service directly in production;
- model -> unrestricted database connection;
- model -> arbitrary URL fetch;
- model -> arbitrary SQL execution;
- model -> worker, import, cache, pricing or action-write endpoints;
- retrieval result -> automatic instruction execution.

## Data classification

Before any provider or vector index is enabled, every input must be classified.

| Data class | Examples | Initial policy |
| --- | --- | --- |
| Public product/help content | public methodology, public help text | may be used after source/version controls |
| Internal non-customer docs | runbooks, KPI definitions, architecture docs | local/internal evaluation only unless approved |
| Operational business data | sales, stock, margin, supplier performance | blocked from external providers until provider/data policy is approved |
| Customer or tenant data | imported customer datasets, store-level details | blocked until auth, tenant isolation, retention and deletion are proven |
| Personal or sensitive data | user identity, email, logs containing personal data | exclude or redact by default |
| Secrets | API keys, connection strings, tokens, admin keys | never send, embed, persist in prompts, or expose to tools |

Provider approval must document:

- which data classes may be sent;
- region and subprocessors;
- training/retention policy;
- request and log retention;
- deletion capability;
- incident contact;
- approved models and environments.

## Threat model

### Direct prompt injection

A user asks the model to ignore rules, reveal hidden prompts, call forbidden tools or expose data.

Controls:

- authorization and tool permissions outside the model;
- explicit allowlist;
- no secret-bearing system prompt;
- deterministic scope validation before and after tool selection;
- refusal and security evaluation cases.

### Indirect prompt injection

A retrieved document, product description, uploaded file or external page contains instructions for the model.

Controls:

- treat retrieved content as evidence, not instructions;
- source allowlist and provenance;
- content sanitization and suspicious-instruction flags;
- no external browsing in the first release;
- tool-call policy independent of retrieved text;
- evaluation corpus with malicious documents.

### Data exfiltration and cross-scope leakage

The model or retrieval layer exposes data outside the current user/store/tenant scope.

Controls:

- server-side user and scope resolution;
- scope included in every tool and retrieval request;
- vector records tagged with authorization metadata;
- filtering before similarity search, not after answer generation;
- separate indexes or enforced partitions when appropriate;
- 100% passing negative authorization tests.

### Excessive agency

The model performs or proposes high-impact operations as if already approved.

Controls:

- first release is read-only;
- no price, markdown, replenishment, import, worker, cache, config or action writes;
- future writes require explicit human confirmation, idempotency and audit design;
- generated text must not claim that a write succeeded.

### Arbitrary SQL or code execution

The model creates SQL/code that is executed against Trendplus.

Controls:

- prohibited in the initial architecture;
- use typed read-only tools over existing services;
- fixed parameters, validation and maximum result sizes;
- query budgets and timeouts;
- no shell, filesystem or unrestricted network tools.

### Cost and resource denial of service

Repeated or oversized requests cause provider cost, memory exhaustion or latency incidents.

Controls:

- user and global rate limits;
- token, request and daily cost ceilings;
- request/body/file-size limits;
- bounded retrieval count and context size;
- timeouts, cancellation and circuit breakers;
- concurrency limits;
- kill switch and provider disable flag.

### Sensitive logging and trace leakage

Prompts, tool results or traces persist sensitive data.

Controls:

- structured allowlisted trace fields;
- redaction before logs and third-party tracing;
- no secrets or raw connection details;
- configurable prompt/body capture disabled by default;
- retention and deletion policy;
- role-gated trace inspection.

### Hallucinated financial or operational facts

The model produces plausible but incorrect values or actions.

Controls:

- backend source-of-truth tools;
- citations and tool-result trace;
- exact numeric comparison in evaluation;
- unknown/insufficient-data states;
- no answer presented as authoritative when evidence failed;
- preserve existing confidence, freshness and data-quality metadata.

## Existing image embedding service audit

The repository currently contains `EmbeddingService/app.py` and a .NET embedding adapter. This is an image-similarity prototype, not a production text-RAG service.

Observed risks that must be verified and addressed before production use:

- FastAPI embedding endpoints do not show service-to-service authentication.
- Upload content is read into memory without an explicit application-level byte limit.
- Content type is trusted as part of validation; image decoding is the effective deeper validation.
- Error responses may include raw exception text.
- The model is downloaded/loaded during module startup.
- The selected model is currently a code constant rather than validated environment configuration.
- Batch size, image dimensions and concurrency are not explicitly bounded in the service.
- The .NET adapter reads the full image from a local path and does not show a service credential.
- A `MockEmbeddingService` returns random vectors and must never be selectable as a production similarity source.
- The README describes the adapter as automatic, while actual runtime registration and deployment status must be verified.

Required outcome of the first queue task:

- classify the service as active, dormant or experimental in every environment;
- make production selection explicit and fail closed;
- document or add authentication, limits, timeouts, safe errors and tests;
- keep image embeddings separate from the future text/document retrieval index.

## Mandatory P0 controls before real business data reaches an LLM

- [ ] Provider/data policy approved.
- [ ] Backend authentication and required role confirmed.
- [ ] Store/data/tenant scope enforced server-side.
- [ ] No browser provider credentials or direct provider calls.
- [ ] Approved read-only tool allowlist.
- [ ] No arbitrary SQL, URL, filesystem, shell or code execution.
- [ ] Prompt injection and indirect injection evaluation set.
- [ ] Vector metadata supports source, scope, version and deletion.
- [ ] Secrets and sensitive fields redacted from prompts/logs/traces.
- [ ] Rate, token, context, concurrency and cost limits.
- [ ] Timeout, cancellation, retry and circuit-breaker policy.
- [ ] Feature flag and immediate kill switch.
- [ ] Audit record with user, scope, tools, sources, model and trace ID.
- [ ] Retention and deletion procedure.
- [ ] Security and authorization tests pass in CI.

Any unchecked P0 item blocks a real-data pilot.

## Tool contract rules

Every tool must declare:

- stable name and version;
- business purpose;
- required role;
- allowed data scope;
- typed input schema;
- output schema;
- maximum date range and result size;
- timeout and cost class;
- freshness and data-quality metadata;
- whether it is read-only;
- audit fields;
- safe error contract.

The model may request a call. The gateway decides whether the call is permitted.

## Retrieval index rules

- Use a separate namespace/schema/index for text retrieval.
- Never mix image and text vectors only because both use pgvector.
- Store source ID, version, checksum, scope, created/updated time and deletion state.
- Chunking must preserve headings and source references.
- Index only approved sources.
- Deleting or revoking a source must remove or disable all related chunks.
- Retrieval filtering must occur before results are supplied to the model.
- A similarity score is not authorization or factual confidence.

## Conversation and memory rules

The first release should not provide durable personal memory.

For any stored conversation:

- document the purpose and retention;
- store the minimum necessary content;
- avoid secrets and sensitive raw payloads;
- bind access to the same user/scope rules;
- provide deletion;
- do not use conversations as an unreviewed training dataset.

## Incident and rollback requirements

The system must support:

- provider disable flag;
- route/feature disable flag;
- tool registry disable by tool;
- vector index reversion or rebuild by source version;
- trace lookup by correlation ID;
- cost spike alert;
- security incident log and owner;
- fallback to normal Trendplus analytics without the AI feature.

The core analytics product must remain usable when every GenAI component is disabled.
