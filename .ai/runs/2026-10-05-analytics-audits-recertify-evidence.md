# Evidence — Analytics audits quality recertify 2026-10-05

- Queue: direct-user-request (review last presentation/UX/docs audits)
- Worktree: `/workspace/docs-audit` branch `audit-recertify-2026-10-05`
- Base: `origin/main` at start = `e12655fd`
- No Trendplus2 WT; no cloud agents; RQ588 not claimed

## What was wrong in prior audits
1. Presentation pass-2 claimed Insight `N/D` cleared, but `insightStudioTrustPresentation.ts` + its tests still used `N/D`.
2. Presentation/UX residual lists missed `IntelligenceSnapshotPanel` English/`n/a` (used by Insight Studio).
3. Docs audit correctly flagged the N/D residual but left DoD push checkbox unchecked after FF; evidence SHA still said pending.
4. Registry `unitsSold.interpretation` said "Meri promet po količini", colliding with Promet=`revenue` glossary rule.

## What was fixed
- Canonical unavailable in Insight trust helper + tests
- Serbianize IntelligenceSnapshotPanel titles/empty/unavailable
- Clarify unitsSold interpretation
- Guardrails in presentation pass2 + IntelligenceSnapshotPanel.spec
- Honest recertify notes on the three audit docs + glossary + evidence SHA sync
- Minor: WorkerControlFlag title Serbian; DailySales Data Health comment

## Validation
See commit message / agent report for vitest + validators.

## Bundle / tip
- Pre-FF tip of recertify commits: `6e64c1a5` (docs) after `b97eb0a3` (UI fix)
- Landed on origin/main after parent FF + evidence commit: **`1ccaf3eb`**
- Bundle used: `/workspace/out/analytics-audits-recertify.bundle`
- HEAD == origin/main after FF: YES (`1ccaf3eb`)
- Focused tests: 17/17 passed (insightStudioTrustPresentation, IntelligenceSnapshotPanel, analyticsPresentationPass2)
- Validators: check-prompt-queues OK; planning PASS; agent-instructions PASS; check:encoding OK; tsc -b OK; git diff --check OK

## Recertify-2 note
Second adversarial pass found this evidence file still claimed pre-FF tip/`NO` after main already advanced to `1ccaf3eb` — same class of SHA drift the first recertify criticized. Corrected here; product leftovers fixed in recertify-2 commit.
