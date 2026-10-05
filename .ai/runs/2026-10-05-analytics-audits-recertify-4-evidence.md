# Evidence — Analytics audits adversarial recertify #4 2026-10-05

- Queue: direct-user-request (fourth adversarial recertify after tip `ded8e15e`)
- Worktree: `/workspace/docs-audit` branch `audit-recertify-4-2026-10-05`
- Base: `origin/main` = `ded8e15e`
- No Trendplus2 WT; no cloud agents; RQ588 not claimed; Insight not redesigned (RQ582)

## What was wrong after recertify-3
1. `TrendDashboard` (route `/trend-dashboard`) still showed English `Loading…` / `Refresh` / `refreshed`, English KPI labels (`Total items`, `Rising`, …), null price as `—`, and mojibake expand arrows (`â–²`/`â–¼`).
2. Amazon `PriceLabel` still used `—` for null price while eBay/Google already used `Nije dostupno` (claimed residual from recertify-3).
3. Amazon/eBay/Google sync success toasts still English: `results — inserted, updated`.
4. Recertify-3 evidence still recorded `HEAD == origin/main: NO until parent FF` after tip already landed as `ded8e15e`.

## What was verified OK (no invent)
- Color PoP tip + Brojilac/Imenilac export copy still Serbian.
- Amazon/eBay/Google: Učitavanje…, Pokreni sinhronizaciju, no `Price N/A` / `Run Sync` / `Cijena`.
- Insight trust helper = `ANALYTICS_UNAVAILABLE_LABEL` (no `N/D`).
- IntelligenceSnapshotPanel Serbian + unavailable.
- Actions outcome hint = Nije dostupno.
- Insight average-margin = Prosečna marža.
- Trust header collapse P0 still covered.
- Glossary unitsSold = quantity ≠ Promet/Prihod.
- No READY queue work claimed; RQ588 untouched; no Insight redesign.

## Fixes
- TrendDashboard: Učitavanje… / Osveži / osveženo; Serbian KPI labels; null price → ANALYTICS_UNAVAILABLE_LABEL; expand ▲/▼; title Rang lista trendova
- Amazon PriceLabel → ANALYTICS_UNAVAILABLE_LABEL
- Amazon/eBay/Google sync toast → `rezultata — uneto, ažurirano`
- TrendDashboardPage: Dashboard trendova; istorija (not historija)
- Guardrails in `analyticsPresentationPass2.spec.ts`
- Recertify-3 evidence post-FF SHA note (`ded8e15e`)
- This evidence + `docs/qa/ANALYTICS_AUDITS_RECERTIFY_4_2026-10-05.md`

## Bundle / tip
- Branch: `audit-recertify-4-2026-10-05`
- Exact pre-FF tip SHA: recorded in `/workspace/out/analytics-audits-recertify-4-PUSH_INSTRUCTIONS.md` at bundle time (avoids self-referential amend loop)
- Requires origin/main: `ded8e15e`
- Bundle: `/workspace/out/analytics-audits-recertify-4.bundle`
- HEAD == origin/main: NO until parent FF push

## Residuals (honest)
- TrendDashboard still has some English admin score-component labels (`Base`, `Cross-src`, …) and filter chip English (`rising`/`dropping` chips) — not claimed cleared.
- Category/name missing display still uses `—` (identity placeholder, not metric unavailable).
- Insight Studio hierarchy = RQ582.
- Prihod vs Promet unify = PO.
- Live a11y/viewport pass not run.
- RQ588 not claimed.
