# SQL Analytics Prompt Queue

Date: 2026-06-28
Repo: `ivanjovicic/Trendplus`
Current READY prompt: Q69

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
| Q69 | READY | analytics-sql-trust | Audit current SQL semantics and add tests/spec before fixes |
| Q70 | WAITING | nivelacija-sql-impact | Fix zero-baseline percent semantics after Q69 |
| Q71 | WAITING | supplier-decision-sql-nullability | Guard supplier-decision SQL against fake zero/confidence |
| Q72 | WAITING | supplier-sales-stats-performance | Review endpoint query plan and safe service split |
| Q73 | WAITING | supplier-sales-stats-verification | Harden manual verification SQL script/runbook |
| Q74 | WAITING | analytics-refresh-window-contracts | Lock refresh and windowed MV contracts in tests |

---

## Q69 - Analytics SQL trust semantic audit and tests

Status: READY
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
2. Update `docs/qa/ANALYTICS_SQL_QUERY_AUDIT.md` with exact findings from the tests.
3. Mark which findings are safe to fix next and which need DB/EXPLAIN evidence.
4. Do not change runtime SQL yet.

### Checks

- `git diff --check`
- `dotnet test Api.Tests/Api.Tests.csproj --no-build --configuration Release --filter "SupplierDecisionSchemaSqlTests|SqlTrust"`
- If `--no-build` fails due missing build artifacts, run `dotnet build Trendplus2.sln --no-restore --configuration Release` and then rerun the targeted tests.

### Acceptance

- SQL trust risks are documented with test-backed evidence.
- No production SQL behavior changes.
- Q70-Q74 can be refined from Q69 evidence.
- Queue entry is updated with changed files, checks, risk and next prompt.

---

## Q70 - Nivelacija zero-baseline percent semantics

Status: WAITING
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

---

## Q71 - Supplier-decision SQL no-fake-zero/nullability guardrails

Status: WAITING
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

---

## Q72 - Supplier-sales-stats query plan and service split review

Status: WAITING
Ready after: Q69 DONE
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

---

## Q73 - Supplier-sales-stats manual SQL verification runbook

Status: WAITING
Ready after: Q69 DONE
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

---

## Q74 - Analytics refresh and windowed MV contract tests

Status: WAITING
Ready after: Q69 DONE
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
