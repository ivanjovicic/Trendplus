## New prompt additions (2026-09-06)

The following prompt seeds were added as a focused supplement to `ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`. They should be reviewed and merged into the canonical queue file by the analytics/platform owners; created as separate prompts to avoid edit conflicts and allow quick review.

---

### RQ173 - Harden PythonEmbeddingService client usage and file safety

Status: WAITING
Priority: P1
Type: backend/security/tests
Feature family: embedding-service-client-hardening
Parallel-safe: yes
Owner: Platform

Commit suggestion: `fix(embeddings): validate embedding client base URL, file size and error mapping`

Problem:
`PythonEmbeddingService` assumes an HttpClient endpoint and reads image files from disk without explicit size checks or clear BaseAddress validation. Missing client BaseUrl or large/untrusted image paths can cause unreliable failures, unclear error mapping, or allow reading unexpected filesystem paths.

Scope:
- `Infrastructure/Services/EmbeddingService.cs` (PythonEmbeddingService.GetEmbeddingAsync)
- Startup registration and options for embedding client base URL
- Focused unit and integration tests for invalid/missing base URL, oversized files, and path-safety

Do:
1. Validate embedding client configuration at startup: require `EmbeddingService:BaseUrl` or fail-fast when `PythonEmbeddingService` is registered.
2. Add file-size and path-safety checks before reading image bytes; reject suspicious absolute/parent-traversal paths and enforce a configured max file size.
3. Map common HttpClient failures to clear, non-sensitive error messages and retry/timeout semantics.
4. Add CI integration test that runs a local embedding stub and exercises the client with valid and invalid inputs.

Acceptance:
- Client fails fast with clear diagnostic when BaseUrl is missing or invalid.
- Oversized or unsafe file paths are rejected with 4xx errors before attempting upload.

---

### RQ174 - Audit raw SQL call sites for parameterization and timeouts

Status: WAITING
Priority: P1
Type: backend/tests/security
Feature family: sql-parameterization-audit
Parallel-safe: no
Owner: Backend

Commit suggestion: `fix(sql): audit and parameterize raw SQL call sites; enforce command timeouts`

Problem:
The codebase contains many raw SQL call sites and Execute* calls. Some places assemble SQL dynamically or bind non-typed parameters (see embedding service) which risks SQL injection, incorrect parameter typing, and missing command timeouts that can hang workers.

Do:
1. Produce an inventory of raw SQL usages and flag any string-concatenated SQL or untyped parameters.
2. Replace unsafe concatenation with parameterized commands; prefer typed Npgsql parameters or ORM helpers.
3. Ensure every production-facing command sets an explicit CommandTimeout and is cancellable via CancellationToken.
4. Add integration tests that verify parameter binding correctness and that long-running queries respect timeouts.

Acceptance:
- No new unsafe SQL concatenation merged; CI inventory job runs and fails on new unsafe patterns.

---

### RQ175 - Improve Admin API-key handling: rotation and timing-safe compare

Status: WAITING
Priority: P2
Type: security/ops
Feature family: privileged-secrets-assurance
Parallel-safe: yes
Owner: Security

Commit suggestion: `fix(security): timing-safe admin key compare and rotation guidance`

Problem:
Admin API key (`ADMIN_API_KEY` / `Admin:ApiKey`) is compared using `string.Equals` and stored/checked from environment/config. This lacks explicit rotation guidance, audit trail, and could be improved with timing-safe comparisons. Operational documentation for rotation and emergency-access is incomplete.

Do:
1. Replace plain equality checks with a timing-safe constant-time comparison utility.
2. Add documentation and a small operator-runbook for rotating `ADMIN_API_KEY` safely and updating running services.
3. Add tests that validate the compare utility and fail on accidental logging of key values.

Acceptance:
- Admin comparisons use a timing-safe helper across codebase.
- Runbook committed under `docs/security/` describing rotation steps.

