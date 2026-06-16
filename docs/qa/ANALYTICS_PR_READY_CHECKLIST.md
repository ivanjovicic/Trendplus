# Analytics PR Ready Checklist

Datum: 2026-06-16
Repo: `ivanjovicic/Trendplus`

## Status Summary

- Current branch: `main`
- Latest local commit SHA: `45b14f8`
- Latest app-facing analytics stabilization commit before this docs update: `783adbc`
- Minimum expected SHA from task: `7fb6e04 or newer`
- Result: current branch is newer than the expected minimum

## PR #1 Decision

PR `#1` (`[codex] publish all changes`) should **not** be merged as-is.

Reason:

- it is still `draft`
- it is `mergeable=false`
- it points to stale head commit `6278af4`
- `main` has moved ahead to `45b14f8`
- Vercel comments on the PR show failed deployment state on the older branch history

Recommended action:

- close PR `#1`
- open a **new PR** from a fresh branch based on current `main`

## Included Feature Areas

Use the new PR for the latest analytics stabilization package already present on `main`.

Primary areas to include:

- analytics guardrail compliance and build stabilization
- action outcome summary endpoint and frontend summary surfaces
- trust header / refresh banner readiness messaging
- pilot readiness checklist page
- inventory decision summary quality-warning coverage
- analytics QA / deployment triage documentation

Recent relevant commits:

- `783adbc` `fix(analytics): satisfy guardrails`
- `0cddd32` `test(inventory): cover decision summary quality warning`
- `ecb6b22` `feat(analytics): add pilot readiness checklist`
- `b4d579f` `feat(analytics): polish trust surfaces and outcome specs`
- `2baa616` `feat(actions): add outcome summary endpoint`
- `55955fc` `feat(actions): add outcome summary frontend`
- `45b14f8` `fix(ci): triage vercel analytics deploy`

## Required Checks

Before opening the new PR:

```powershell
git fetch origin
git log --oneline -20
git diff --check
```

Frontend required checks:

```powershell
cd Klijent/clientapp
npm run check:analytics-guardrails
npm run build
```

Backend checks when backend analytics code is part of the PR:

```powershell
dotnet build
dotnet test
```

## Manual Smoke Checklist

Run or confirm the following screens after opening the clean PR:

- `/analytics`
  - trust header visible
  - no fake zero on error
  - freshness / refresh messaging visible
- `/analytics/actions`
  - outcome summary renders
  - summary buckets match action states
  - no broken filter interactions
- `/analytics/data-quality`
  - pilot readiness summary visible
  - links to refresh/data quality/pilot readiness work
- `/analytics/pilot-readiness`
  - checklist page loads
  - readiness status, links, and action counts render
- `/analytics/inventory`
  - decision summary warning state still behaves correctly
- `/admin/configuration?panel=workers`
  - worker/refresh links from analytics surfaces land on expected page

## Known Risks

- `docs/qa/ANALYTICS_PR_STATUS.md` was referenced by the task but is currently missing from the local repo
- PR `#1` contains stale branch history and old Vercel bot comments, so using it as the merge vehicle risks confusing review state
- Vercel project configuration must still point to `Klijent/clientapp`; otherwise deploy status may continue to fail even if code is healthy
- browser/manual smoke is still worth re-running after the new PR is opened because current evidence is strongest on build/test and targeted page coverage

## Open New PR Guidance

Preferred path:

1. create a fresh branch from current `main`
2. push that branch
3. open a new PR targeting `main`
4. copy only the relevant analytics stabilization summary, not the stale PR #1 narrative

Suggested PR framing:

- title should describe analytics stabilization / readiness work
- body should include:
  - period/trust/readiness surfaces
  - action outcome summary work
  - pilot readiness additions
  - required validation commands
  - Vercel root-directory note

## Go / No-Go

- Merge PR #1: **No-Go**
- Close PR #1 and open new PR from current main: **Go**
