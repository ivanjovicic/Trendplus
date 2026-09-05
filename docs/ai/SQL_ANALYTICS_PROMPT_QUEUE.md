# SQL Analytics Prompt Queue

Date: 2026-09-05
Repo: `ivanjovicic/Trendplus`
Current READY prompt: none

Use this queue with `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

Purpose: isolate SQL analytics work so Codex, Cursor and manual edits do not implement overlapping analytics features or risk-changing SQL semantics in the same prompt.

## Queue rules

1. Only the prompt marked `READY` may be started.
2. All `WAITING` prompts must stay untouched until their dependency is DONE or explicitly unblocked.
3. Create a local uncommitted lock before starting:
   - `.ai/task-locks/<task-id>-<agent>.lock.md`
4. Do not touch files outside `Scope only`.
5. Do not combine SQL semantics, frontend UX and deploy proof in one task.
6. If a SQL change needs DB evidence, mark `BLOCKED` or `PARTIAL` and record the missing evidence.
7. Queue status must be updated only after the task has checks and notes.

## Status summary

| Task | Status | Feature family | Purpose |
|---|---|---|---|
| Q69 | DONE | analytics-sql-trust | Audit current SQL semantics and add tests/spec before fixes |
| Q70 | DONE | nivelacija-sql-impact | Fix zero-baseline percent semantics after Q69 |
| Q71 | DONE | supplier-decision-sql-nullability | Guard supplier-decision SQL against fake zero/confidence |
| Q72 | DONE | supplier-sales-stats-performance | Review endpoint query plan and safe service split |
| Q73 | DONE | supplier-sales-stats-verification | Harden manual verification SQL script/runbook |
| Q74 | DONE | analytics-refresh-window-contracts | Lock refresh and windowed MV contracts in tests |
| Q75 | DONE | supplier-decision-windowed-readiness | Audit startup readiness for 90d/180d supplier decision MVs |
| Q76 | DONE | supplier-decision-query-parity | Compare precomputed and live supplier-decision SQL contracts |
| Q77 | DONE | supplier-decision-null-reader | Audit nullable reader/detail-query trust semantics |
| Q78 | DONE | analytics-backend-encoding | Extend encoding guardrail to backend analytics decision strings |
| Q79 | DONE | analytics-filter-fallback-meta | Add explicit meta/warnings to filter/list fallback paths |
| Q80 | DONE | lost-sales-source-confidence | Make lost-sales validation source/confidence explicit |
| Q81 | DONE | analytics-datascope-sql-consistency | Audit dataScope/store/supplier filtering across raw SQL helpers |
| Q82 | DONE | analytics-sql-observability | Standardize SQL timeout/cancellation/logging expectations |
| Q83 | PARTIAL | nivelacija-sql-nullability-and-baseline | Prove raw nivelacija SQL preserves missing coverage and revenue baseline semantics |

---

## Q69 - Analytics SQL trust semantic audit and tests

Status: DONE
Priority: P0
Type: docs/tests
Feature family: analytics-sql-trust
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q69-<agent>.lock.md`
Commit suggestion: `test(analytics): audit sql trust semantics`

### Why

The analytics SQL layer has several places where missing evidence, zero baseline or absent post-window rows can become ordinary numeric zero. Before production SQL changes, the repo needs a focused trust-semantic audit and regression tests that describe current behavior and the intended safer contract.

### Scope only

- `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
- optional new `Api.Tests/*SqlTrust*Tests.cs`
- no SQL runtime behavior changes

### Do not touch

- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`
- `Database/Migrations/018_AddSupplierDecisionHubViews.sql`
- `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql`
- frontend files
- deploy files

### Read first

- `.github/copilot-instructions.md`
- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`
- `docs/qa/ANALYTICS_SQL_SECOND_PASS_REVIEW.md`
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`
- `Database/Migrations/018_AddSupplierDecisionHubViews.sql`
- `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql`
- `scripts/check_supplier_sales_stats.sql`
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`

### Do

1. Add tests that lock the current high-risk SQL fragments:
   - zero-baseline percent behavior in `vw_vendor_sales_nivelacija`
   - `COALESCE(..., 0)` cost/post-signal behavior in supplier-decision views
   - 90d/180d duplicated formula fragments and output column expectations
   - refresh-list presence for `mv_supplier_decision_score_cache_90d` and `mv_supplier_decision_score_cache_180d`
2. Include second-pass findings from `docs/qa/ANALYTICS_SQL_SECOND_PASS_REVIEW.md` in the final notes.
3. Update `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md` with exact findings from the tests.
4. Mark which findings are safe to fix next and which need DB/EXPLAIN evidence.
5. Do not change runtime SQL yet.

### Checks

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "SupplierDecisionSchemaSqlTests|SqlTrust"`
- If `--no-build` fails due missing build artifacts, run `dotnet build Trendplus2.sln --no-restore --configuration Release` and then rerun the targeted tests.

### Acceptance

- SQL trust risks are documented with test-backed evidence.
- No production SQL behavior changes.
- Q70-Q82 can be refined from Q69 evidence.
- Queue entry is updated with changed files, checks, risk and next prompt.

### Notes

- 2026-08-04: DONE. Added contract tests for zero-baseline nivelacija percent semantics, explicit supplier-decision zero fallbacks, and duplicated 90d/180d score-cache columns. Updated the SQL audit with test-backed findings and a safe-next vs DB/EXPLAIN evidence split.
- Changed files:
  - `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
  - `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "SupplierDecisionSchemaSqlTests|SqlTrust"` - pass
  - `git diff --check` - pass
- Risk:
  - Runtime SQL semantics are unchanged; this prompt only locked current contracts with tests/docs.
- Next:
  - `Q70 - Nivelacija zero-baseline percent semantics`

---

## Q70 - Nivelacija zero-baseline percent semantics

Status: DONE
Ready after: Q69 DONE
Priority: P0
Type: SQL/tests
Feature family: nivelacija-sql-impact
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q70-<agent>.lock.md`
Commit suggestion: `fix(analytics): clarify nivelacija zero-baseline impact`

### Why

`vw_vendor_sales_nivelacija` currently represents zero-baseline changes as normal numeric percent values. That can make a no-baseline event look like a clean +100% uplift or 0% stable result.

### Scope only

- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`
- focused backend SQL contract tests
- optional docs update in `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`

### Do not touch

- supplier-decision scoring SQL
- frontend pages
- dashboard/report copy
- deploy proof docs

### Read first

- Q69 notes and tests
- `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`

### Do

1. Use Q69's chosen contract before changing SQL.
2. Prefer additive compatibility if existing columns are consumed elsewhere:
   - add explicit baseline flags/reason columns, or
   - make percent columns nullable only if downstream code is proven safe.
3. Preserve view recreation safety for Postgres column-order changes.
4. Add tests for:
   - pre=0/post>0
   - pre=0/post=0
   - normal pre>0 percent change
   - low-signal propagation

### Checks

- `git diff --check`
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "Nivelacija|SupplierDecisionSchemaSqlTests|SqlTrust"`

### Acceptance

- Zero-baseline events cannot be silently presented as ordinary percent change.
- Downstream compatibility is documented.
- No supplier-decision formula change is mixed into this task.

### Notes

- 2026-08-04: DONE. Added additive semantic baseline fields to `vw_vendor_sales_nivelacija`, extended SQL contract coverage for zero-baseline semantics and low-signal propagation, and documented the compatibility note in the SQL audit.
- Changed files:
  - `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`
  - `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
  - `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Trendplus2.sln --no-restore --configuration Release /p:UseSharedCompilation=false` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~SupplierDecisionSchemaSqlTests" /p:UseSharedCompilation=false` - pass
- Risk:
  - Legacy consumers still read the original percent columns, so the semantic fix is additive until downstream callers migrate.
- Next:
  - `Q71 - Supplier-decision SQL no-fake-zero/nullability guardrails`

---

## Q71 - Supplier-decision SQL no-fake-zero/nullability guardrails

Status: DONE
Ready after: Q69 DONE; Q70 DONE or explicitly not required
Priority: P0
Type: SQL/tests
Feature family: supplier-decision-sql-nullability
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q71-<agent>.lock.md`
Commit suggestion: `fix(analytics): harden supplier decision sql trust fields`

### Why

Supplier-decision views use `COALESCE(..., 0)` in many scoring inputs. Some are legitimate observed-zero defaults; others can hide missing evidence. Confidence and recommendation output must remain conservative when cost, post-window or stock evidence is missing.

### Scope only

- `Database/Migrations/018_AddSupplierDecisionHubViews.sql`
- `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql`
- focused SQL contract tests
- `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`

### Do not touch

- `Api/Endpoints/AllEndpoints.cs`
- frontend pages
- action ledger
- decision board aggregate work

### Do

1. Classify every risky zero fallback from Q69 as either observed zero or missing evidence.
2. Add explicit missing-evidence fields or confidence downgrades where needed.
3. Keep 90d and 180d window formulas in parity.
4. Do not rewrite the scoring model broadly.
5. Add focused tests for missing cost, missing post signal, stock proxy warning and one-supplier rank behavior.

### Checks

- `git diff --check`
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "SupplierDecision|SqlTrust"`

### Acceptance

- Missing evidence is visible or lowers confidence.
- Missing values are not silently converted into trusted zeros.
- 90d/180d/all-time SQL contracts stay aligned.

### Notes

- 2026-08-04: DONE. Added explicit post/did/cost coverage signals, nullable return-rate handling, evidence-quality status, and conservative `REVIEW_QUALITY` fallback across all supplier-decision SQL views, plus focused contract tests and audit notes.
- Changed files:
  - `Database/Migrations/018_AddSupplierDecisionHubViews.sql`
  - `Database/Migrations/029_AddSupplierDecisionWindowedViews.sql`
  - `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
  - `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release /p:UseSharedCompilation=false` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~SupplierDecisionSchemaSqlTests" /p:UseSharedCompilation=false` - pass
  - `git diff --check` - pass
- Risk:
  - The new conservative review path may surface more `REVIEW_QUALITY` outcomes until downstream users adapt to the explicit evidence flags.
- Next:
  - `Q72 - Supplier-sales-stats query plan and service split review`

---

## Q72 - Supplier-sales-stats query plan and service split review

Status: DONE
Ready after: Q71 DONE
Priority: P1
Type: backend/performance-review
Feature family: supplier-sales-stats-performance
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q72-<agent>.lock.md`
Commit suggestion: `docs(analytics): review supplier sales stats query plan`

### Why

`/api/analytics/supplier-sales-stats` combines EF queries, in-memory grouping, margin snapshots, previous-period comparison and nivelacija split logic. It may be acceptable for pilot windows, but broad date ranges need measured evidence before optimization.

### Scope only

- `Api/Endpoints/AllEndpoints.cs`
- optional extraction plan doc under `docs/qa/` or `docs/analytics/`
- focused tests only if a tiny service extraction is clearly safe

### Do not touch

- SQL materialized views
- supplier-decision scoring
- frontend UX
- cache policy unless measured evidence requires a follow-up prompt

### Do

1. Identify query count, row counts and in-memory aggregation points.
2. Add or document logging/measurement needed for broad ranges.
3. Decide whether the next safe step is:
   - docs-only plan,
   - service extraction preserving behavior,
   - or a DB-side aggregation proposal.
4. Keep response shape, cache key behavior and trust semantics unchanged.

### Checks

- `git diff --check`
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- targeted endpoint tests if code changes

### Acceptance

- Performance risk is evidence-based, not guessed.
- Any proposed rewrite is split into a later prompt.
- No response contract drift.

### Notes

- 2026-08-04: DONE. Added row-count telemetry to the supplier-sales-stats endpoint, documented the explicit DB materialization and in-memory aggregation points, and kept cache/response semantics unchanged.
- Changed files:
  - `Api/Endpoints/AllEndpoints.cs`
  - `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
  - `.ai/task-locks/Q72-codex.lock.md`
- Checks:
  - `dotnet build Trendplus2.sln --no-restore --configuration Release /p:UseSharedCompilation=false` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~AnalyticsSupplierSalesIntegrationTests" /p:UseSharedCompilation=false` - pass
  - `git diff --check` - pass
- Risk:
  - The new telemetry only measures broad-range pressure; it does not yet split the orchestration into a smaller service.
- Next:
  - `Q73 - Supplier-sales-stats manual SQL verification runbook`

---

## Q73 - Supplier-sales-stats manual SQL verification runbook

Status: DONE
Ready after: Q72 DONE
Priority: P1
Type: docs/sql-script
Feature family: supplier-sales-stats-verification
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q73-<agent>.lock.md`
Commit suggestion: `docs(analytics): harden supplier sales stats verification sql`

### Why

`scripts/check_supplier_sales_stats.sql` is useful, but it should clearly say it is diagnostic and list which endpoint behaviors it does not verify.

### Scope only

- `scripts/check_supplier_sales_stats.sql`
- optional `docs/qa/SUPPLIER_SALES_STATS_SQL_VERIFICATION.md`

### Do not touch

- endpoint code
- production SQL views
- frontend pages

### Do

1. Add parameter instructions and example scenarios.
2. Add EXPLAIN/ANALYZE guidance that is safe for Neon/psql usage.
3. Add a “does not verify” section:
   - active snapshot-cost path
   - cache metadata
   - frontend trust metadata
   - `dataScope` edge cases unless explicitly parameterized
4. Add fake-zero checks for margin and pre/post metrics.
5. Keep the script manually runnable.

### Checks

- `git diff --check`
- SQL syntax spot-check if local psql is available; otherwise document not run.

### Acceptance

- Operator can run the script without confusing it for the endpoint contract.
- Known gaps are explicit.
- No runtime code changed.

### Notes

- 2026-08-04: DONE. Added diagnostic-only guidance, `data_scope` parameterization, fake-zero reason columns, and a companion verification doc for manual SQL inspection.
- Changed files:
  - `scripts/check_supplier_sales_stats.sql`
  - `docs/qa/SUPPLIER_SALES_STATS_SQL_VERIFICATION.md`
  - `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
  - `psql -h 127.0.0.1 -w -d postgres -c "SELECT 1;"` - not run successfully; local server required authentication and no password was available
- Risk:
  - The script now surfaces more diagnostics, but it still does not prove the API endpoint's snapshot-cost or cache behavior.
- Next:
  - `Q74 - Analytics refresh and windowed MV contract tests`

---

## Q74 - Analytics refresh and windowed MV contract tests

Status: DONE
Ready after: Q73 DONE
Priority: P1
Type: tests
Feature family: analytics-refresh-window-contracts
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q74-<agent>.lock.md`
Commit suggestion: `test(analytics): lock windowed mv refresh contracts`

### Why

The 90d and 180d materialized views are present and included in refresh options, but the contract should be locked in tests so future queue work does not accidentally drop, duplicate or mis-order them.

### Scope only

- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
- `Infrastructure/Configuration/NightlyAnalyticsRefreshOptions.cs` only if test evidence proves config drift
- no SQL formula changes

### Do not touch

- worker scheduling behavior
- production deploy docs
- frontend pages

### Do

1. Add tests asserting default refresh list includes:
   - `mv_supplier_decision_score_cache_90d`
   - `mv_supplier_decision_score_cache_180d`
2. Assert the intended no-30d-MV behavior remains explicit unless a later prompt designs one.
3. Assert windowed SQL comments and endpoint fallback language stay aligned.
4. Keep this a contract-test task only.

### Checks

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "SupplierDecisionSchemaSqlTests"`

### Acceptance

- Windowed MV refresh and fallback assumptions are covered by tests.
- No formula or endpoint behavior changes.
- Queue notes identify the next safe SQL task.

### Notes

- 2026-08-04: DONE. Extended the supplier-decision schema SQL tests to lock the 90d/180d refresh list, the explicit no-30d MV behavior, and the window-comment plus endpoint fallback language alignment.
- Changed files:
  - `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
  - `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
  - `.ai/task-locks/Q74-codex.lock.md`
- Checks:
  - `dotnet build Api.Tests/Api.Tests.csproj --no-restore --configuration Release /p:UseSharedCompilation=false` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~SupplierDecisionSchemaSqlTests" /p:UseSharedCompilation=false` - pass
  - `git diff --check` - pass
- Risk:
  - The test coverage only locks the contract; it does not validate a live refresh job or runtime scheduler behavior.
- Next:
  - `Q75 - Supplier decision windowed MV startup readiness audit`

---

## Q75 - Supplier decision windowed MV startup readiness audit

Status: DONE
Ready after: Q69 DONE; Q74 DONE
Priority: P1
Type: backend/tests/docs
Feature family: supplier-decision-windowed-readiness
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q75-<agent>.lock.md`
Commit suggestion: `test(analytics): audit supplier decision windowed mv readiness`

### Why

Startup readiness and cache-count helpers currently focus on the all-time supplier decision cache objects, while the nightly refresh list also includes 90d and 180d decision-score MVs. Missing windowed MVs can therefore be invisible in startup readiness evidence.

### Scope only

- `Infrastructure/Seed/DatabaseInitializer.cs`
- `Infrastructure/Configuration/NightlyAnalyticsRefreshOptions.cs`
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
- `docs/qa/ANALYTICS_SQL_SECOND_PASS_REVIEW.md`

### Do not touch

- materialized-view formulas
- worker schedule/catch-up behavior
- frontend pages
- production deploy docs

### Do

1. Add tests that document which supplier-decision MVs startup readiness checks prove today.
2. Decide whether 90d/180d should be:
   - logged only,
   - checked as readiness warnings,
   - or included in build readiness.
3. Do not make startup perform heavy refresh by default.
4. If code changes are needed, keep them readiness/logging-only.

### Checks

- `git diff --check`
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "SupplierDecisionSchemaSqlTests|DatabaseInitializer"`

### Acceptance

- Windowed MV startup readiness is explicit.
- Missing 90d/180d objects cannot be silently treated as fully healthy without a documented decision.
- No SQL formula change is mixed in.

### Notes

- 2026-08-05: DONE. Startup readiness now logs windowed 90d/180d supplier-decision MVs separately while keeping all-time caches as the hard readiness gate; the contract test suite documents the explicit decision.
- Changed files:
  - `Infrastructure/Seed/DatabaseInitializer.cs`
  - `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
  - `docs/qa/ANALYTICS_SQL_SECOND_PASS_REVIEW.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Trendplus2.sln --no-restore --configuration Release /p:UseSharedCompilation=false` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~SupplierDecisionSchemaSqlTests" /p:UseSharedCompilation=false` - pass
  - `git diff --check` - pass
- Risk:
  - Windowed MVs are still not part of startup gating, so missing 90d/180d objects now warn instead of blocking startup.
- Next:
  - `Q76 - Supplier decision precomputed/live SQL parity matrix`

---

## Q76 - Supplier decision precomputed/live SQL parity matrix

Status: DONE
Ready after: Q69 DONE; Q71 DONE or explicitly not required
Priority: P0
Type: backend/tests/docs
Feature family: supplier-decision-query-parity
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q76-<agent>.lock.md`
Commit suggestion: `test(analytics): map supplier decision sql parity`

### Why

Supplier decision uses two query contracts: precomputed MV SQL and live CTE SQL. They must not drift on recommendation, confidence, period, filter, nullability or ranking semantics.

### Scope only

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
- optional new `docs/qa/SUPPLIER_DECISION_SQL_PARITY.md`

### Do not touch

- SQL migration files
- frontend pages
- decision board aggregate implementation
- ML training code

### Do

1. Create a parity matrix for precomputed vs live paths.
2. Cover:
   - requested/effective dataset
   - 30d -> 90d helper behavior
   - dataScope eligibility
   - store/category/gender/season filters
   - confidence and recommendation code behavior
   - null/zero field handling
3. Add string-level or unit tests where feasible.
4. Do not change formulas unless a tiny discrepancy is proven safe and isolated.

### Checks

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "SupplierDecision"`

### Acceptance

- Query path differences are intentional and documented.
- Any real parity gap becomes a new smaller prompt.
- No broad SQL rewrite.

### Notes

- 2026-08-05: DONE.
- Commit: not created in this pass.
- Changed files:
  - `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
  - `docs/qa/SUPPLIER_DECISION_SQL_PARITY.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName=Api.Tests.SupplierDecisionSchemaSqlTests.SupplierDecisionPrecomputedAndLiveSqlParityMatrixLocksIntentionalDifferences"` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~Api.Tests.SupplierDecisionSchemaSqlTests"` - pass
- Risk:
  - Precomputed and live supplier-decision SQL remain intentionally asymmetric for `dataScope` and article-level filters; any dedupe should stay a follow-up prompt.
- Next:
  - `Q77 - Supplier decision nullable reader and detail-query trust audit`

---

## Q77 - Supplier decision nullable reader and detail-query trust audit

Status: DONE
Ready after: Q69 DONE
Priority: P0
Type: backend/tests/docs
Feature family: supplier-decision-null-reader
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q77-<agent>.lock.md`
Commit suggestion: `docs(analytics): audit supplier decision nullable reads`

### Why

Supplier decision reader helpers currently convert `DBNull` to `0`, `0m` or empty string. That is safe for some display fields but risky for fields where null means unknown, not zero.

### Scope only

- `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
- supplier decision endpoint tests
- optional `docs/qa/SUPPLIER_DECISION_NULLABILITY_AUDIT.md`

### Do not touch

- migration SQL
- scoring formulas
- frontend display code

### Do

1. List every field read through `GetInt32`, `GetDecimal`, `GetString` in supplier decision paths.
2. Classify each field as:
   - observed zero is OK
   - null should remain nullable
   - empty string is acceptable
   - needs explicit unavailable flag
3. Add focused tests for the highest-risk fields if feasible.
4. Do not globally change helper behavior.

### Checks

- `git diff --check`
- targeted supplier decision tests if code changes

### Acceptance

- Nullability risks are documented field-by-field.
- Any required DTO change is split into a follow-up prompt.
- No fake-zero behavior is introduced.

### Notes

- 2026-08-05: DONE.
- Commit: not created in this pass.
- Changed files:
  - `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
  - `docs/qa/SUPPLIER_DECISION_NULLABILITY_AUDIT.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --configuration Release --filter "FullyQualifiedName~Api.Tests.SupplierDecisionSchemaSqlTests"` - pass
- Risk:
  - Identifier and recommendation-code nulls would still be risky if the schema ever loosens; they remain documented rather than globally masked.
- Next:
  - `Q78 - Backend encoding guardrail for analytics decision strings`

---

## Q78 - Backend encoding guardrail for analytics decision strings

Status: DONE
Ready after: Q69 DONE
Priority: P1
Type: tooling/backend-copy/tests
Feature family: analytics-backend-encoding
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q78-<agent>.lock.md`
Commit suggestion: `chore(analytics): extend backend encoding guardrail`

### Why

Supplier decision endpoint contains user-facing Serbian strings with mojibake in recommendation titles/reasons. The existing encoding work should cover backend analytics decision strings too.

### Scope only

- backend analytics endpoint/source files containing user-facing strings
- existing encoding script/tests if present
- `docs/ai/ENCODING_AND_TEXT_SAFETY.md`
- focused tests or script allowlist changes

### Do not touch

- recommendation codes
- SQL formulas
- frontend layouts
- unrelated legacy screens unless required by the encoding check

### Do

1. Extend the encoding/mojibake guardrail to backend analytics user-facing strings.
2. Fix visible mojibake in supplier decision recommendation titles/reasons.
3. Keep enum/code values unchanged.
4. Keep any allowlist explicit and small.

### Checks

- `git diff --check`
- `cd Klijent/clientapp && npm run check:encoding` if the script is frontend-owned
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- targeted tests if a backend test exists

### Acceptance

- Analytics backend copy no longer contains obvious mojibake in maintained surfaces.
- Guardrail catches future backend decision-string encoding regressions or documents why it cannot yet.
- No business logic changes.

### Notes

- 2026-08-05: DONE.
- Commit: not created in this pass.
- Changed files:
  - `Api/Endpoints/SupplierDecisionHubEndpoints.cs`
  - `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
  - `Klijent/clientapp/scripts/check-encoding.mjs`
  - `docs/ai/ENCODING_AND_TEXT_SAFETY.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `npm run check:encoding` - pass
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` - pass
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~Api.Tests.SupplierDecisionSchemaSqlTests"` - pass
  - `git diff --check` - pass, with existing LF/CRLF warnings only
- Risk:
  - `docs/qa/ANALYTICS_SQL_SECOND_PASS_REVIEW.md` stays allowlisted because it documents the historical mojibake issue instead of being a maintained copy surface.
- Next:
  - `Q79 - Dashboard filter/list fallback meta contract`

---

## Q79 - Dashboard filter/list fallback meta contract

Status: DONE
Ready after: Q69 DONE
Priority: P1
Type: backend/frontend-contract
Feature family: analytics-filter-fallback-meta
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q79-<agent>.lock.md`
Commit suggestion: `docs(analytics): define filter fallback meta contract`

### Why

Some dashboard endpoints return explicit `Meta` errors on database issues, while filter/list endpoints can return empty arrays on timeout/database failure. Empty filters can look like valid no-data instead of degraded analytics.

### Scope only

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- frontend filter consumers only if a tiny backward-compatible handling change is required
- optional `docs/qa/ANALYTICS_FILTER_FALLBACK_CONTRACT.md`

### Do not touch

- supplier-decision SQL formulas
- inventory/replenishment algorithms
- deployment docs

### Do

1. Audit filter/list endpoints that return empty collections on failure.
2. Decide backward-compatible contract:
   - preserve array and add header/meta elsewhere, or
   - wrap with meta only for new endpoint variants.
3. Document UI behavior for ancillary filter/list failures.
4. Add tests for no silent empty failure if code changes.

### Checks

- `git diff --check`
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- frontend tests only if UI handling changes

### Acceptance

- Ancillary filter query failure cannot be silently confused with a valid empty list.
- Existing consumers are not broken.

### Notes

- 2026-08-05: DONE.
- Commit: not created in this pass.
- Changed files:
  - `Api/Endpoints/CachedAnalyticsEndpoints.cs`
  - `Klijent/clientapp/src/services/analyticsApi.ts`
  - `Klijent/clientapp/src/pages/InventoryPage.tsx`
  - `Klijent/clientapp/src/pages/ProductDecisionCenterPage.tsx`
  - `Klijent/clientapp/src/pages/SupplierConsolidatedPage.tsx`
  - `Klijent/clientapp/src/pages/SupplierConsolidatedPage.css`
  - `Klijent/clientapp/src/services/__tests__/analyticsApi.contract.spec.ts`
  - `Klijent/clientapp/src/services/__tests__/supplierFilterFallbackMeta.spec.ts`
  - `docs/qa/ANALYTICS_FILTER_FALLBACK_CONTRACT.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `dotnet build Trendplus2.sln --no-restore --configuration Release` - pass
  - `npm run test -- --run src/services/__tests__/supplierFilterFallbackMeta.spec.ts` - pass
  - `git diff --check` - pass, with existing LF/CRLF warnings only
- Risk:
  - The shared `analyticsApi.contract.spec.ts` MSW-based suite still has an unrelated AbortSignal mismatch in this environment, so I used an isolated fetch-stub spec to verify the new fallback metadata contract.
- Next:
  - `Q80 - Lost-sales validation source/confidence contract`

---

## Q80 - Lost-sales validation source/confidence contract

Status: DONE
Ready after: Q69 DONE; RQ03 DONE (reuse API vocabulary)
Priority: P0
Type: backend/tests/docs
Feature family: lost-sales-source-confidence
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q80-<agent>.lock.md`
Commit suggestion: `docs(analytics): define lost sales source confidence`

### Why

Lost-sales validation can return `(0, 0)` when the view/connection is unavailable or when fallback evidence is sparse. For OOS/replenishment decisions, unavailable evidence must not look like clean zero lost sales.

### Scope only

- `Api/Endpoints/CachedAnalyticsEndpoints.cs`
- validation/lost-sales DTOs/tests if needed
- optional `docs/qa/LOST_SALES_VALIDATION_CONTRACT.md`

### Do not touch

- replenishment algorithm
- decision board ranking
- supplier decision SQL

### Do

1. Document the source hierarchy:
   - `vw_analytics_oos_lost_sales`
   - recent-sales/current-stock fallback
   - unavailable
2. Add source/confidence/status metadata if a small compatible DTO change exists.
3. Ensure unavailable is not presented as green zero.
4. Add tests for view unavailable, fallback used and true zero cases.

### Checks

- `git diff --check`
- `dotnet build Trendplus2.sln --no-restore --configuration Release`
- targeted validation/lost-sales tests if code changes

### Acceptance

- Lost-sales zero is distinguishable from unavailable/unknown.
- OOS/replenishment trust semantics stay conservative.

### Notes

- 2026-08-04: RQ03 landed the shared API vocabulary (`view`/`fallback`/`unavailable`/`true_zero`) and contract doc. Q80 should not invent a second model; remaining work is SQL-evidence/docs follow-up only if DB proof of the view path is still needed.
- 2026-08-05: DONE. Verified the endpoint already follows the shared source-confidence contract: unavailable returns `insufficient_data` with a null estimate, true zero stays `good`, and fallback zero remains `warning`. Confirmed by `Api.Tests/LostSalesValidationSourceStatusTests.cs` and the current `CachedAnalyticsEndpoints` implementation.
- Changed files:
  - `docs/qa/LOST_SALES_VALIDATION_CONTRACT.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~LostSalesValidationSourceStatusTests"` - pass
  - `git diff --check` - pass
- Risk:
  - The queue task was validation-only because the implementation already existed; no code path changed in this turn.
- Next:
  - `Q81 - Analytics dataScope/store/supplier SQL consistency audit`

---

## Q81 - Analytics dataScope/store/supplier SQL consistency audit

Status: DONE
Ready after: Q69 DONE
Priority: P1
Type: docs/tests
Feature family: analytics-datascope-sql-consistency
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q81-<agent>.lock.md`
Commit suggestion: `docs(analytics): audit sql filter consistency`

### Why

Analytics endpoints apply `dataScope`, store and supplier filters through different SQL paths. The same dashboard request can combine product, inventory, supplier, action and validation data with different filter semantics.

### Scope only

- analytics endpoint SQL builders/helpers
- `docs/qa/ANALYTICS_SQL_FILTER_CONSISTENCY_AUDIT.md`
- focused tests only if a tiny invariant is easy to lock

### Do not touch

- SQL formulas
- frontend routing
- action write security

### Do

1. Map how each raw SQL helper interprets:
   - `dataScope=all`
   - `dataScope=existing`
   - `dataScope=imported`
   - `storeId`
   - `supplierId`
2. Identify mismatches between supplier decision, dashboard cached endpoints, lost-sales and supplier-sales-stats.
3. Propose one follow-up prompt per mismatch.
4. Do not change behavior in this audit unless a typo-level bug is obvious and isolated.

### Checks

- `git diff --check`
- targeted tests only if code changes

### Acceptance

- Filter semantics are documented across raw SQL helpers.
- Mismatches are visible before any SQL rewrite.

### Notes

- 2026-08-05: DONE. Added a focused SQL filter consistency audit documenting the shared `dataScope` baseline and the current mismatches across Product Decision Center, lost-sales, inventory, supplier decision hub, and supplier-sales-stats paths.
- Changed files:
  - `docs/qa/ANALYTICS_SQL_FILTER_CONSISTENCY_AUDIT.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass
- Risk:
  - This prompt is audit-only; the helper contracts are now explicit, but the runtime cross-surface mismatches still exist until a follow-up prompt changes them.
- Next:
  - `Q82 - SQL timeout, cancellation and observability consistency audit`

---

## Q82 - SQL timeout, cancellation and observability consistency audit

Status: DONE
Ready after: Q69 DONE
Priority: P2
Type: docs/backend-observability
Feature family: analytics-sql-observability
Parallel-safe: no
Owner: unassigned
Local lock: `.ai/task-locks/Q82-<agent>.lock.md`
Commit suggestion: `docs(analytics): audit sql observability timeouts`

### Why

Analytics SQL paths use different timeout/cancellation/error-reporting approaches: 25s hard query timeout in supplier decision, endpoint-specific timeout CTS for filters, long worker MV refresh timeouts and different meta/fallback behavior.

### Scope only

- analytics endpoint timeout/cancellation paths
- `Workers/NightlyAnalyticsRefreshWorker.cs`
- optional `docs/qa/ANALYTICS_SQL_OBSERVABILITY_TIMEOUTS.md`
- tests only if existing helpers make this cheap

### Do not touch

- SQL formulas
- cache TTLs unless documented as a follow-up
- deployment workflows

### Do

1. Inventory command timeouts and cancellation behavior across analytics SQL paths.
2. Map user-visible response behavior:
   - explicit meta error
   - empty fallback
   - 503/problem
   - partial warning
3. Document logging/correlationId expectations.
4. Recommend small consistency fixes as separate prompts.

### Checks

- `git diff --check`
- docs-only unless code changes are explicitly tiny

### Acceptance

- Timeout and observability behavior is documented across SQL analytics paths.
- Future fixes can be prioritized without mixing with SQL formula changes.

### Notes

- 2026-08-05: DONE. Added an observability audit covering supplier decision hard SQL timeout handling, supplier-sales-stats cancellation/503 behavior, cached filter fallback semantics, board partial-meta behavior, and nightly worker timeout/correlation logging.
- Changed files:
  - `docs/qa/ANALYTICS_SQL_OBSERVABILITY_TIMEOUTS.md`
  - `docs/ai/SQL_ANALYTICS_PROMPT_QUEUE.md`
- Checks:
  - `git diff --check` - pass, with existing LF/CRLF warnings only
- Risk:
  - No runtime behavior changed; the audit only makes the current split timeout model explicit.
- Next:
  - none

---

## Q83 - Prove raw nivelacija SQL preserves nullability and revenue baseline semantics

Status: PARTIAL
Priority: P0
Type: SQL/backend/tests
Feature family: nivelacija-sql-nullability-and-baseline
Parallel-safe: no, this is the SQL owner for pre/post change semantics
Owner: Codex
Commit suggestion: `fix(sql): preserve nivelacija evidence states`

### Problem

The vendor nivelacija endpoint has compatibility SQL that coalesces missing pre/post quantity, revenue, coverage and change fields to zero. It also falls back from a revenue change column to a quantity change column when the revenue column is unavailable. That can make absent evidence look like a measured zero and can expose a quantity effect under a revenue metric name.

### Evidence

- `Api/Endpoints/AllEndpoints.cs:3227-3232` chooses `change_percent_revenue` when present and otherwise uses `change_percent_qty`.
- `Api/Endpoints/AllEndpoints.cs:3338-3346` and `:3401-3409` apply `COALESCE(..., 0)` to pre/post quantity, revenue, coverage and change fields.
- `Api/Endpoints/AllEndpoints.cs:3510-3517` reads missing numeric columns as zero and determines `HasSalesWindow` from those values, losing the distinction between missing relation data and a measured empty sales window.
- Existing Q69/Q70/Q71/Q76/Q77 work covered earlier SQL trust cases, but this raw endpoint compatibility branch is not yet proven with a missing-column/nullability matrix.

### Scope only

- `Api/Endpoints/AllEndpoints.cs` vendor-sales-nivelacija raw SQL and reader mapping
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`
- `Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql`
- focused SQL/backend tests and an optional SQL audit note

### Do not touch

- frontend pages/components
- recommendation thresholds or scenario/business interpretation
- cache/refresh worker ownership
- unrelated analytics formulas

### Read first

- `AGENTS.md`
- `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` section `RQ140`
- `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md`
- `docs/qa/ANALYTICS_SQL_SECOND_PASS_REVIEW.md`
- `Api/Endpoints/AllEndpoints.cs` vendor-sales-nivelacija handler
- `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`
- `Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql`

### Do

1. Define the SQL contract for each field: source column, unit, denominator, measured-zero condition and missing/unknown condition.
2. Stop using quantity percent as an implicit revenue percent. If compatibility requires a fallback, return an explicit source/status field and mark the result degraded; otherwise fail closed.
3. Preserve nullable coverage/change values through SQL and the reader. `HasSalesWindow` must distinguish a proven empty window from missing/unreadable relation data.
4. Prove whether absent pre/post rows, absent columns, view drift and no denominator should return empty, warning/degraded or error metadata; do not decide from numeric zero alone.
5. Add a bounded SQL audit note if the view and endpoint cannot be proven together without a live database; record the exact missing evidence rather than inferring it.

### Checks

- `git diff --check`
- focused SQL fragment/view contract tests for null, valid zero, missing denominator, missing column/view and revenue-vs-quantity fallback
- nearest `dotnet test` filter for changed SQL/backend tests
- `dotnet ef migrations list` for the affected context when a migration/view contract changes

### Acceptance

- Raw nivelacija SQL no longer hides missing coverage or change evidence as zero.
- Revenue and quantity effects are never silently interchangeable.
- Missing relation/column/migration and true empty sales window produce distinct tested states.
- SQL changes remain bounded to the SQL owner; frontend/business interpretation is handed back to `RQ140`.

### Dependencies

- `Q69`, `Q70`, `Q71`, `Q76` and `Q77` are historical prerequisites and must be reused.
- `RQ139` is independently promoted and supplies the shared numeric-state vocabulary; this SQL prompt must not wait for its runtime implementation or edit its files.
- `RQ140` consumes this SQL contract for cross-layer comparability.
- Live database/schema proof may remain `PARTIAL` or `BLOCKED` when the required runtime relation is unavailable; do not claim production proof from static SQL tests.

### Completion note

- Date: 2026-09-05
- Status: PARTIAL
- Completion: SQL/backend semantic contract, nullable reader mapping and focused regression coverage completed; live schema/refresh proof unavailable.
- Changed files: `Api/Endpoints/AllEndpoints.cs`, `Database/Analytics/014_CreateVendorSalesNivelacijaViews.sql`, `Database/Migrations/016_AnalyticsNivelacijaEnhancements.sql`, `Api/Models/VendorSalesNivelacijaModels.cs`, `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
- Checks run: focused backend tests 33/33 and wider mapped analytics tests 77/77; API build pass; solution build pass; `git diff --check` pass; EF migration enumeration/build pass.
- Checks not run: applied migration status and live view verification, blocked by Neon PostgreSQL `28P01 password authentication failed for user neondb_owner`.
- Run log: `.ai/runs/2026-09-05-analytics-trust-parity-evidence.md`
- Evidence state: synchronized
- Delivery mode: direct-main
- Main commit SHA: a84d8a42974e4228840ef07e3f0e9f5d03a4068c
- Main verification: current `main` and `origin/main` contain implementation commit a84d8a42974e4228840ef07e3f0e9f5d03a4068c as an ancestor; final branch SHA is recorded in the run log/delivery response.
- Missed: live schema and successful refresh proof.
- Follow-up: restore valid DB access and repeat migration/view/refresh verification; RQ140 remains the consumer for cross-layer comparability.
- Residual risk: production schema may still be missing the additive semantic columns until migration/view deployment is verified.
- Prompt defect / scope repair: the user-requested full pre/post/parity outcome required a bounded connected frontend contract in addition to the SQL owner scope; no unrelated analytics formulas were changed.
