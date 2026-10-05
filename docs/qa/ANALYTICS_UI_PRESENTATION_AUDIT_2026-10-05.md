# Analytics UI presentation audit — 2026-10-05

Osnova (pass 1 tip): `8d0f1589` na `4bdb4400`.
Pass 2 osnova: `origin/main` `63486fb11536388f2a21255f0cf6ee3905708b0e`.
Dokaz: `.ai/runs/2026-10-05-analytics-ui-presentation-audit-evidence.md`.

## §152 — Definition of Done

| # | Criterion | Status |
|---|---|---|
| 1 | Fresh fetch + tip recorded | PASS (`63486fb1`) |
| 2 | Presentation domains reviewed (2nd full pass) | PASS |
| 3 | Residual AnalyticsDetails / Configuration / GlobalTrends fixed | PASS |
| 4 | Safe local UI fixes landed | PASS |
| 5 | Targeted tests (not brittle snapshots) | PASS |
| 6 | Encoding guardrail OK | PASS |
| 7 | Evidence + QA audit updated | PASS |
| 8 | Commit + PC bundle FF push | PASS (on `main` via presentation tip `8d0f1589` / pass-2 `90a47a54`; superseded by later UX tip) |
| 9 | No Trendplus2 WT / no cloud agents | PASS |
| 10 | Prefer fix over new RQ | PASS |

## Pass 2 — screens reviewed

Prodaja po dobavljačima; po vrsti obuće; po boji; Dnevna prodaja; Inventory/Bilans; Data Quality; Pre/posle nivelacije; Pre-nivelacija prioriteti; Supplier Footwear / Report / Decision Hub; Product Decision; Insight Studio (presentation-only; RQ582 experimental); Analytics Dashboard / Details / Actions; Global Trends + Amazon/eBay/Google trend empty states; Deichmann scraper; Configuration (admin health/copy); formatters / table state / export helpers.

## Pass 2 findings fixed

1. **AnalyticsDetails**: English risk/quality/action headings → Serbian; `N/A` → `ANALYTICS_UNAVAILABLE_LABEL`; diacritics (`Greške`, `Osveži`, `Marža`, `Prikaži`, `Kritično`); legacy jargon softened; basic `type="button"` / aria on key actions.
2. **GlobalTrends**: `formatTrendNumber` → `Nije dostupno` (test already expected this); Serbian toasts/empty labels; `Čizme` + categoryMap keys; Cyrillic mix `rasтуće` → `rastuće`; brand placeholder.
3. **Configuration**: health `N/A` → `Nije dostupno`; English error/toast/confirm → Serbian; Redis label; ping button aria-label.
4. **Inventory**: SizeCurve + SKU history `N/A` → canonical unavailable; `Dobavljač` diacritic.
5. **Data Quality / Dashboard**: English aria/empty/tableTitle and `(Data Health)` parenthetical cleaned.
6. **Insight Studio**: clear leftover English section/export titles; `N/D` → canonical unavailable; Croatian `Tjedna` → Serbian; no product rename of Velocity/OOS (RQ582).
7. **Pre/post / Footwear / external trends / Deichmann**: Reset → `Poništi filtere`; dataSource Serbian; empty states Serbian; Deichmann mojibake/BOM stripped and UTF-8 restored.
8. **Tests**: stale `N/A` expectations aligned; new `analyticsPresentationPass2.spec.ts` guardrail.

## Red-team (pass 2)

- Production `"N/A"` in analytics pages/components/inventory utils cleared (comment-only remains in constants).
- Mojibake scan on maintained analytics surfaces: OK (`npm run check:encoding`).
- Did not claim live visual a11y; code/static pass only for obvious missing names/aria.
- Did not invent RQ; Velocity kept.

## Residuals

Canonical business terms (Prihod/Promet, Nije dostupno, horizon, DUG/KOREKCIJA): `docs/ai/ANALYTICS_BUSINESS_GLOSSARY.md`.

- Insight Studio remains experimental (RQ582); deeper AI-sounding metrics/score semantics not redesigned.
- Some admin/diagnostic English identifiers (Redis, batch, scraper source codes) intentionally kept where domain/product names.
- Live visual/responsive/a11y sweep still not run in browser.
- Outbox/Logs Retry English is outside core Analytics sales surfaces (not changed this pass).

## Tests (pass 2 focused)

- `check:encoding` OK
- `tsc -b` OK (empty diagnostics)
- Vitest focused suites: **136 passed** (presentation pass2, AD period, Insight, Inventory null/forecast, indicator regression, Footwear, Daily/Shoe/Color premium, unavailable label, prePost toolbar)


## Recertify 2026-10-05

Quality review found **incomplete** pass-2 claim #6 (Insight `N/D`):
- `InsightStudioPage.tsx` used `ANALYTICS_UNAVAILABLE_LABEL` in several places, but `pages/insightStudioTrustPresentation.ts` still returned literal `N/D` (tests still expected `N/D`).
- Dashboard/Insight `IntelligenceSnapshotPanel` still showed `n/a` and English section titles (`Signals Snapshot`, `Demand Pulse`, …).

Both closed in the same recertify pass with guardrail extensions in `analyticsPresentationPass2.spec.ts`.
Do **not** treat pass-2 residual list as fully closed without this recertify.

## Recertify-2 2026-10-05
Adversarial re-check after tip `1ccaf3eb` found residual user-facing „N/A“ teaching text on Analytics Actions and `Avg marza` headers in Insight Studio (plus nivelacija FE manual). Closed in the same pass; trust/N/D/IntelligenceSnapshot claims from first recertify remain valid.
