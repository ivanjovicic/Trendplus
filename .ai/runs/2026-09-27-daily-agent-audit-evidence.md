Task: direct-user-request daily agent/commit audit
Date: 2026-09-27
Repository: ivanjovicic/Trendplus
Delivery target: main
Delivery mode: direct-main
Main implementation SHA before this evidence commit: e175de5e57e330b0574faf79c6044e8f6262a609
Evidence state: synchronized for findings below; current CI classified separately below

## Scope

- Audited strict 2026-09-27 commits plus the late 2026-09-26 agent session that directly led into RQ321/RQ322/RQ323/RQ316.
- Reviewed recent agent evidence, queue truth, open PRs, GitHub-visible branches and current GitHub Actions results.
- No Render deploy or production DB migration was executed by this audit.

## Findings and repairs

### Production automation was broader than the documented manual-release policy

- Fly production deploy still ran on every push to `main`, including docs-only queue commits.
- Production EF migration workflow still ran on matching `main` pushes using `secrets.DB_CONNECTION`.
- Fixed Fly deploy to `workflow_dispatch` only: `dbd5a5d4be7b1330c07973c3f9b81dd7dd59005c`.
- Fixed production DB migration to `workflow_dispatch` only and serialized manual migrations with non-cancelling concurrency: `389cca48408a78100d042164d132e0c66a418fe1`.
- Render remained manual-only: `render.yaml` uses `autoDeployTrigger: off`; the Render API deploy workflow is `workflow_dispatch` only.
- GitHub Actions history proved the last automatic Fly runs occurred before `dbd5a5d4`; later audit commits did not start Fly deploys.

### RQ321 missed a fail-closed URL store edge

- A `storeId` restored from the URL could remain authoritative after store discovery failed, even though the store selector became unavailable.
- The page now clears the unverified store scope, stale store-scoped current/previous rows and the URL query value on store discovery failure: `074e7b96ea703fe71f5c9da6087b90df1a77bf48`.
- Added regression coverage: `a92f2709954d0b2a62c12857a7e85c6c17b0f75e`.
- Updated the narrow reviewed analytics guardrail baseline for the intentional fail-closed clear: `877e3cdee0e51b1fd70a5905a0ccab1902d7801e`.

### Existing frontend CI failures were not inspected by prompt agents

- Previous RQ321/RQ322 runs were red while evidence said remote CI was not inspected.
- Fixed a Pilot readiness async test race: `c3172632b45d0cd0706c670b3331f9788da2438c`.
- Updated stale Shoe Type inclusive-end expectations to the current half-open date contract: `d4e382fb155ed5a338f41837b6c7d0a42b63b9a1`.
- The first new Daily URL-store regression used an obsolete response fixture; replaced it with the current validated DailySales contract: `72073d2e3241c6e03bee8564007f23a8d5719cd3`.
- Inventory tests now wait for the intentional store-bootstrap follow-up request before asserting conservative trust/interacting with secondary panels: `66628eb400f5869167785a8193ad7f47cd88de65`, `e175de5e57e330b0574faf79c6044e8f6262a609`.

### RQ323 still exposed fake workflow-derived zeros

- RQ323 correctly made the workflow pending count unavailable on workflow failure, but P2 Transfer and P2 Mrtva zaliha still fell back to zero from the same unavailable payload.
- Both counts now fail closed to `Nije dostupno`: `d30540284c3c82833fd2f1a62f383eba2620f8fc`.
- Added component regression: `421c3775496537ddc0ea37c5e6d6984c3b4ed9f0`.

### Long-running analytics workflow had no job bound

- Old run `36271811555` remained in the broad backend test step for hours on an obsolete SHA.
- Added 45-minute backend-suite and 30-minute RQ447-certification job bounds: `fdac63ba4c07b3f906096a653a0ef750430bd13f`.
- Existing workflow concurrency then cancelled superseded run `36271811555`.
- Replacement run `36275934187`: migrations/bootstrap/lifecycle smoke passed; dedicated RQ447 certification passed every gate and artifact upload; broad backend suite finished red with 20 failures across multiple independent backend families. Those failures are classified as a separate backend repair backlog, not silently green and not patched blindly in this cross-cutting audit.

### RQ447 evidence had contradictory terminal truth

- Queue/evidence contained a valid DONE certification followed by a stale historical PARTIAL paragraph.
- Marked the old PARTIAL revalidation explicitly superseded: `6ae59dd6383bdadde48252acf08c70033bc1860c`.
- Synchronized queue truth so the historical partial run cannot override DONE: `fe5ed464ad0dcc3a9cd3ecef67a2b7e052096293`.

### Agent process allowed known CI to remain unclassified

- Kept main-first delivery and the rule not to wait indefinitely for remote CI.
- Added canonical rule: when a relevant current-main Actions run is already triggered and discoverable before final evidence, record run id/SHA/state; a red run must be classified and an in-scope regression repaired before DONE.
- Protocol: `5a86b2272e797d1929005a13f5bc4b408611d814`.
- Root AGENTS policy: `7d73bc4a7897cacc0f6a64db708aa2e0d9753c77`.
- Copilot mirror: `de2a552e82e70581df71655cf7d7b4831596a32b`.

## Branch / PR audit

- Open PRs: none.
- Recent Cursor branches inspected were already fully contained in main (ahead 0) or stale.
- `cursor/operations-runtime-drift-guard-b591` is a stale divergent branch (roughly 202 commits behind main, one old large commit ahead). It was intentionally not merged because a blind merge would not be a safe local integration.
- RQ316/RQ321/RQ322/RQ323 task locks were absent after delivery.

## CI classification

- `Analytics Quality Gates` run `36275991928` on `421c3775` was inspected after failure: 7 failures in three frontend files were classified; the audit repaired the current-contract Daily fixture and Inventory bootstrap timing expectations instead of reverting fail-closed behavior.
- Final post-repair Quality Gate is the run triggered by `e175de5e57e330b0574faf79c6044e8f6262a609`; at evidence creation it was queued behind the immediately preceding superseded run.
- `Analytics Tests & Data Integrity` run `36275934187`: RQ447 certification green; broad backend suite red (1419 passed, 20 failed, 38 skipped). Failure families include cache identity/invalidation, SQL Server discovery/session, Access import FK, outbox concurrency and data-scope integration tests; this requires focused backend-owner work rather than a cross-subsystem blind patch.
- Planning Governance for the new CI-classification instruction was triggered; final current-main run state must be reported in the user-facing audit result.

## Residual risk / next owner

- Do not re-enable automatic Fly/Render production deploy or automatic production DB migration without an explicit release-policy change.
- Do not merge the stale divergent runtime-drift branch wholesale.
- If the final frontend Quality Gate is red, inspect the exact current-main failure before further code changes.
- The broad backend 20-test failure set should be handled as focused BCI/backend repair work with reproduction/classification per family.
