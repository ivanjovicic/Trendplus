# Analytics UI presentation audit — 2026-10-05

Osnova: `origin/main` `4bdb4400135a6c2a6029a54d2bb26713a97b8503`.
Dokaz: `.ai/runs/2026-10-05-analytics-ui-presentation-audit-evidence.md`.

Napomena: puni parent brief §0–154 nije bio u executor storage-u; audit pokriva Ivanove oblasti (terminologija, encoding, datumi/valuta/%, N/A vs 0, trust/empty/error, tooltips, charts, CSV, a11y osnove, red-team) uz §152/§153.

## §152 — Definition of Done

| # | Criterion | Status |
|---|---|---|
| 1 | Fresh fetch + tip recorded | PASS (`4bdb440`) |
| 2 | Presentation domains reviewed | PASS |
| 3 | Safe local UI fixes landed | PASS |
| 4 | Label↔metric correctness preferred over cosmetic EN | PASS (Velocity kept as product term) |
| 5 | Targeted tests (not brittle snapshots) | PASS |
| 6 | Encoding guardrail OK | PASS |
| 7 | Evidence + QA audit written | PASS |
| 8 | Commit + PC bundle FF push | pending at write |
| 9 | No Trendplus2 WT / no cloud agents | PASS |
| 10 | Prefer fix over new RQ | PASS |

## §153 — Final report

### Findings fixed
1. **Unavailable copy**: design system maps N/A → `Nije dostupno`. Centralized `ANALYTICS_UNAVAILABLE_LABEL`; defaults in `analyticsFormatters` + `analyticsTableState`; applied on Product Decision, Actions, Supplier Footwear.
2. **Insight Studio**: restored Serbian diacritics (Dobavljač, Marža, Trošak, Očekivani, će); Serbianized clear English headings (KPI Snapshot, Margin Pressure, Heatmap, At-risk, Smart Reorder Engine) without renaming Velocity product term.
3. **CSV encoding**: UTF-8 BOM on Dashboard client CSV and decision-timeline download (Excel-safe Serbian).
4. **Export CTA**: Product Decision `Export…` → `Izvoz…`.

### Red-team
- Searched mojibake (clean on maintained surfaces).
- Falsified N/A-as-unavailable vs measured 0 (formatters no longer default to English N/A).
- Did not invent RQ; P-UI-50 intent partially satisfied by shared unavailable label.

### Residuals
- AnalyticsDetails / Configuration / GlobalTrends still have some English or N/A.
- Full live visual pass and exhaustive a11y audit not run this delivery.

### Tests
- Encoding check OK; focused vitest suites green (SupplierFootwear 21/21 + formatter/table/export helpers).
