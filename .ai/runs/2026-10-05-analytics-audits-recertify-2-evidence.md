# Evidence — Analytics audits adversarial recertify #2 2026-10-05

- Queue: direct-user-request (second adversarial recertify after tip `1ccaf3eb`)
- Worktree: `/workspace/docs-audit` branch `audit-recertify-2-2026-10-05`
- Base: `origin/main` = `1ccaf3eb`
- No Trendplus2 WT; no cloud agents; RQ588 not claimed; Insight not redesigned (RQ582)

## What was wrong after first recertify
1. Recertify evidence still recorded pre-FF tip `6e64c1a5` / `HEAD == origin/main: NO` after FF already made tip `1ccaf3eb`.
2. Accuracy audit evidence/DoD still said `pending` though fix landed as `7aed4aad`.
3. Presentation claim “production N/A cleared” was incomplete: `AnalyticsActionsPage` help still taught users „N/A“ while values used `ANALYTICS_UNAVAILABLE_LABEL`.
4. Insight Studio still mixed `Avg marza` / `Avg marža` (English Avg + missing diacritic) in export/table headers.
5. Nivelacija frontend integration manual still instructed `"N/A"` for null metrics.

## What was verified OK (no invent)
- `insightStudioTrustPresentation.ts` uses `ANALYTICS_UNAVAILABLE_LABEL` (no `N/D`).
- `IntelligenceSnapshotPanel` Serbian titles + canonical unavailable.
- `unitsSold.interpretation` clarifies quantity ≠ Promet/Prihod; glossary matches.
- Trust header collapse P0 still covered by `AnalyticsTrustHeader.spec.tsx` (details collapsed by default; critical cues visible).
- UX sticky identity / responsive filters still present in code (`responsivePilot` / `responsiveFilterLayout`).
- No READY queue work claimed; RQ588 untouched; no Insight redesign.

## Fixes
- Actions outcome hint → „Nije dostupno“
- Insight average-margin headers → Prosečna marža [%]
- Integration manual null guidance → Nije dostupno
- Evidence/DoD SHA truth sync (recertify-1 + accuracy)
- Guardrails in `analyticsPresentationPass2.spec.ts`

## Validation
See commit / agent report.

## Bundle / tip
- UI fix commit: `6445a94e9dd848bac3bc297aca8e273b6fa1a625`
- Docs commit: (follows)
- Requires origin/main: `1ccaf3eb`
- Bundle: `/workspace/out/analytics-audits-recertify-2.bundle`
- HEAD == origin/main: NO until parent FF push
- Focused tests: 46/46 (presentation pass2, insight trust, IntelligenceSnapshot, TrustHeader, unavailable label)
- Validators: check-prompt-queues OK; planning PASS; agent-instructions PASS; check:encoding OK; tsc -b OK; git diff --check OK
