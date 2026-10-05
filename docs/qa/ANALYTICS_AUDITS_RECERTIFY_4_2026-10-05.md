# Analytics audits adversarial recertify #4 — 2026-10-05

Base: `origin/main` `ded8e15e`  
Evidence: `.ai/runs/2026-10-05-analytics-audits-recertify-4-evidence.md`

## Verdict on audits after tip `ded8e15e`

| Item | Verdict | Notes |
|---|---|---|
| Recertify-1 UI (Insight N/D, IntelligenceSnapshot, unitsSold) | Still true | Grep + source re-read |
| Recertify-2 UI (Actions hint, Prosečna marža, nivelacija manual) | Still true | Grep + source re-read |
| Recertify-3 UI (Color PoP tip/export; Amazon/eBay/Google Loading/Run Sync/Cijena) | Mostly true | Amazon/eBay/Google pages OK; **TrendDashboard** sibling surface still English Loading/Refresh |
| Recertify-3 Amazon null price | **Incomplete** | eBay/Google used `Nije dostupno`; Amazon PriceLabel still `—` |
| Recertify-3 sync success toast | **Incomplete** | Still English `results — inserted/updated` on Amazon/eBay/Google |
| Recertify-3 evidence SHA after FF | **Incomplete** | Still said `HEAD == origin/main: NO` after tip landed as `ded8e15e` |
| UX trust collapse / sticky / reset-DQ | OK | Spec + code still present |
| Glossary Prihod/Promet / unitsSold | OK | Unchanged |
| RQ588 / Insight redesign | Not claimed / not done | Correct |

## Fixes in this pass
See evidence file.

## Queue truth (fresh, not claimed)
- RQ588: IN_PROGRESS elsewhere — not claimed
- No new READY prompts registered
- Insight Studio hierarchy remains RQ582 residual
