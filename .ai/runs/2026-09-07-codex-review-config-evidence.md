Task ID: codex-review-config
Queue: direct-user-request
Date: 2026-09-07
Agent/tool: Codex
Delivery target: main
Working branch / PR: main / direct delivery
Main commit SHA: pending
Main verification: pending - implementation commit not yet delivered
Evidence state: pending

## What was done
- Audited the repository's GitHub workflows and Codex instruction surface for code review and review-fix behavior.
- Confirmed there was no repository `## Code Review Rules` section in the root `AGENTS.md`.
- Added concise Codex review rules for analytics trust boundaries, decision/period provenance and safe review fixes.
- Kept deterministic checks in existing CI workflows; no comment, review-posting or notification workflow was added.

## Files changed
- AGENTS.md
- .ai/runs/2026-09-07-codex-review-config-evidence.md

## Validation run
- `node scripts/check-agent-instructions.mjs` -> pass.
- `git diff --check` -> pass (Git emitted only the existing LF-to-CRLF normalization warning).
- Existing `.github/workflows/*.yml` audit -> pass for the intended no-comment/no-notification repository workflow posture; workflows use read-only permissions for review/test jobs and job summaries/artifacts where reporting is needed.

## Validation not run
- Frontend/backend runtime suites -> not run - documentation/governance-only change.
- `codex review --uncommitted` -> not completed - the CLI started and inspected the working tree but did not emit a final review result within the command window; no incomplete run was treated as passing proof.
- GitHub/Codex account-side review settings -> not verified - `gh auth status` reported no authenticated GitHub host and the Codex settings page was not signed in.

## Documentation impact
- Updated the canonical root `AGENTS.md` so Codex Cloud reviews and local Codex review/fix runs receive repository-specific rules.
- No separate review workflow was introduced because Codex Code Review, automatic review and `@codex fix` are account-side Codex/GitHub settings, not repository YAML configuration.

## What was missed
- Account-side enablement of Codex Cloud Code Review/Automatic reviews and permission to push fixes cannot be verified or changed from this unauthenticated environment.

## Risks
- The repository rules guide Codex but do not themselves enable Codex Cloud or suppress GitHub/Codex account notifications.
- Automatic fixes remain subject to Codex Cloud permissions and should still be checked by CI and branch protections.

## Next
- In Codex Settings, connect `ivanjovicic/Trendplus`, enable Code Review/Automatic reviews only if desired, and ensure the fix agent has push permission; leave account notifications disabled if that is the intended preference.
