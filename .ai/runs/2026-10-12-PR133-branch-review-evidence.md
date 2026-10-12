# PR133 branch review and merge evidence

Task ID: PR133-branch-review
Queue: direct-user-request
Date: 2026-10-12
Agent/tool: Codex
Delivery target: main
Working branch / PR: `fix/render-supplier-windowed-startup-20261012` / #133
Main commit SHA: `1c9158d3e9a74de96c0b7ece60fb0c88f9a18058`
Main verification: pass — refreshed `origin/main` is `1c9158d3e9a74de96c0b7ece60fb0c88f9a18058`, the squash-merge commit for PR #133.
Evidence state: synchronized
Ownership transfer: none

## What was done

- Reviewed local and remote branches not contained in `origin/main`, their direct diffs, associated PR states, and active worktree ownership.
- PR #133 was the only open PR and the only safe code integration candidate. Fixed its stale source-contract assertions after the startup-gate wrapper replaced direct calls; retained an explicit assertion that the wrapper still delegates to the repair helper.
- Pushed fix commit `aedd598331f5e0a7f32356d8ef23843549d240b0` to the PR branch and squash-merged PR #133. The merge commit is `1c9158d3e9a74de96c0b7ece60fb0c88f9a18058`.
- Left merged/closed historical branches and the separate active BCI17 worktree untouched. The closed RQ626 correction branch is superseded by the date repair already on main and contains a weaker stockout assertion.

## Files changed

- `Api.Tests/SupplierDecisionSchemaSqlTests.cs`
- `Api.Tests/SupplierDecisionWindowedStartupPolicyTests.cs`
- `Api/appsettings.Production.json`
- `Infrastructure/Seed/DatabaseInitializer.cs`
- `docs/qa/RENDER_SUPPLIER_MV_STARTUP_503_2026-10-12.md`
- `.ai/runs/2026-10-12-PR133-branch-review-evidence.md`

## Validation run

- `git diff --check` — pass.
- `dotnet test Api.Tests/Api.Tests.csproj --no-restore --filter 'FullyQualifiedName=Api.Tests.SupplierDecisionSchemaSqlTests.SupplierDecisionWindowedViewsAreVerifiedAndRepairedWhenStartupHistoryIsStale' --verbosity minimal` — pass, 1/1.
- `git merge-tree --write-tree origin/main origin/fix/render-supplier-windowed-startup-20261012` — pass, no conflicts.
- `gh pr merge 133 --squash --delete-branch` — pass; PR #133 merged at `1c9158d3e9a74de96c0b7ece60fb0c88f9a18058`.
- Fresh `git fetch origin` and `git rev-parse origin/main` — pass; current `origin/main` is the merge SHA above.
- Main Actions run `38301829902` was `in_progress` when inspected after merge.

## Validation not run

- Full local backend suite — not run; the targeted source-contract test passed and this repair changed one test contract.
- Production deployment, database mutation, or live readiness verification — not run; the PR documents these as separate operator work and no production access was used.

## Documentation impact

- The PR's operator runbook remains the canonical operational boundary; this run log records branch review, validation and delivery.

## What was missed

- Production recovery and materialized-view population remain unverified.
- Current-main Actions run `38301829902` had not completed at the last check.

## Risks

- The merged production setting defers the populated supplier materialized-view build; the supplier 90d/180d capability remains unavailable until controlled maintenance confirms both views are populated. The evidence does not claim that a deployment or database repair occurred.
- Main Actions run `38301829902` was still in progress after merge.

## Post-close routing recovery

- Not applicable — direct user request; no queue prompt was claimed or closed.

## Next

- No additional unmerged branch was identified as safe to integrate. Review the completed main Actions run and perform the documented controlled production maintenance separately when authorized.
