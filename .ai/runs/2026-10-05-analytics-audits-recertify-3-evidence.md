# Evidence — Analytics audits adversarial recertify #3 2026-10-05

- Queue: direct-user-request (third adversarial recertify after tip `ec03d814`)
- Worktree: `/workspace/docs-audit` branch `audit-recertify-3-2026-10-05`
- Base: `origin/main` = `ec03d814`
- No Trendplus2 WT; no cloud agents; RQ588 not claimed; Insight not redesigned (RQ582)

## What was wrong after recertify-2
1. `ColorSalesStatsPage` PoP InfoTip still taught users `N/A` while sibling ShoeType/Supplier tips already used `Nije dostupno`.
2. Color export/detail still exposed English `numerator` / `denominator` and `available` / `denominator nije pozitivan`.
3. Presentation claim that Global Trends empty states were Serbian was incomplete: Amazon/eBay/Google still had `No results…`, `Run Sync`, `Loading…`, `Price N/A` (eBay/Google), and Croatian `Cijena` sort labels.
4. Recertify-2 evidence still recorded `HEAD == origin/main: NO until parent FF` after tip already landed as `ec03d814`.

## What was verified OK (no invent)
- `insightStudioTrustPresentation.ts` uses `ANALYTICS_UNAVAILABLE_LABEL` (no `N/D`).
- `IntelligenceSnapshotPanel` Serbian titles + canonical unavailable.
- Actions outcome hint teaches `Nije dostupno` (not `N/A`).
- Insight average-margin headers = `Prosečna marža` (no `Avg marza`).
- Nivelacija FE manual null guidance = `Nije dostupno`.
- Trust header collapse P0 still covered by `AnalyticsTrustHeader.spec.tsx`.
- UX sticky identity / responsive filters still present.
- Glossary unitsSold = quantity ≠ Promet/Prihod.
- No READY queue work claimed; RQ588 untouched; no Insight redesign.

## Fixes
- Color PoP tip → `Nije dostupno ako…`
- Color share export/detail → Brojilac/Imenilac neto udela; `dostupno`; `imenilac nije pozitivan`
- eBay/Google null price → `Nije dostupno`
- Amazon/eBay/Google: Učitavanje…, Pokreni sinhronizaciju, Sinhronizacija…, Min/Max cena, Cena ↑/↓, Serbian type-empty copy
- Guardrails in `analyticsPresentationPass2.spec.ts`
- Docs/evidence SHA truth for this pass + note on recertify-2 tip `ec03d814`

## Validation
See commit / agent report.

## Bundle / tip
- Branch: `audit-recertify-3-2026-10-05`
- Commits on branch: `17a0fe0f` (UI fix) → `85fbb634` (docs/notes) → evidence tip commit (this file)
- Exact pre-FF tip SHA: recorded in `/workspace/out/analytics-audits-recertify-3-PUSH_INSTRUCTIONS.md` at bundle time (avoids self-referential amend loop)
- Requires origin/main: `ec03d814`
- Bundle: `/workspace/out/analytics-audits-recertify-3.bundle`
- HEAD == origin/main: NO until parent FF push
- Focused tests: pass2 10/10; trust/Insight/Snapshot 37/37; encoding OK; tsc -b OK; queue 709 OK; planning PASS; agent-instructions PASS; git diff --check OK

## Post-FF truth (recertify-4)
- Landed on origin/main after parent FF: **`ded8e15e`**
- HEAD == origin/main after FF: YES (`ded8e15e`)
- Note: earlier lines that said `HEAD == origin/main: NO until parent FF` were pre-push placeholders; corrected here after tip landed (same SHA-drift class as recertify-1/2).
