# Analytics UI/UX/design audit — 2026-10-05

Osnova: `origin/main` pre rada = `cd29c12d` (posle presentation pass-2 `90a47a54` + recertify).
Dokaz: `.ai/runs/2026-10-05-analytics-ui-ux-design-audit-evidence.md`.

## Cilj

Da vlasnik/prodavac za 5–10 s na svakom Analytics ekranu razume: šta se desilo, da li je dobro/loše, i šta dalje — bez developerskog jezika i bez scrollovanja kroz trust panel pre KPI-ja.

## Scope (pregledano)

- Stranice: Dashboard, Prodaja po dobavljačima, tip obuće, boja, Dnevna/smena, Inventory, Data Quality, Pre/posle nivelacije, Pre-nivelacija prioriteti, Supplier Footwear/Report/Decision Hub, Product Decision, Insight Studio, Actions, Pilot readiness, Global/external trends (surface check).
- Shared: `AnalyticsTrustHeader`, `AnalyticsControlBar`, `AnalyticsDataTable`, `ExecutiveKpiRow`, `MetricMethodologyPanel`, `analytics-system.css`.
- Testovi: trust/header/table/KPI + page reset/premium suite.

## Issues found → fixed

| Sev | Count found | Fixed now |
|---|---:|---:|
| P0 | 1 | 1 |
| P1 | 6 | 6 |
| P2 | 8 | 8 |
| P3 | 4 | 2 |

### P0

1. **Trust panel gura KPI ispod folda (650–1400px)** — `AnalyticsTrustHeader` sada podrazumevano **sklopi detalje**; always-visible: naslov, status, period+svežina strip, kritični banneri; detalji na „Prikaži detalje pouzdanosti“.

### P1

2. **„Reset filtera“** → **„Poništi filtere“** na sales/nivelacija ekranima (+ testovi).
3. **„Preporuka je gated“** → **„Preporuka nije dostupna“**.
4. **„Otvori Data Quality“** → **„Otvori kvalitet podataka“** (Executive KPI, methodology, Supplier sales empty actions).
5. **Široke tabele bez sticky first-col / scroll hint** — `AnalyticsDataTable` `responsivePilot` default **true**.
6. **Mobile filter density** — `responsiveFilterLayout` uključen na Shoe/Color/Supplier/Footwear/PreNivelacija/PrePost.

### P2

7. KPI `font-variant-numeric: tabular-nums` u `analytics-system.css`.
8. Insight Studio: Velocity × Margin → Velocity × Marža (Velocity ostaje product term / RQ582).
9. Dashboard: engleski margin leftover stringovi.
10. Trust: `n/a` → `Nije dostupno`; „Integritet Operacije“ → „Integritet operacija“.

## Screen-by-screen

| Screen | Status | Note |
|---|---|---|
| Dashboard | ACCEPTABLE | Trust collapse + compact; još gust overview |
| Prodaja po dobavljačima | ACCEPTABLE | Reset label + responsive filters + sticky table |
| Prodaja po vrsti obuće | ACCEPTABLE | isto |
| Prodaja po boji | ACCEPTABLE | isto |
| Dnevna / smena | ACCEPTABLE | reset label; sticky already |
| Inventory | ACCEPTABLE | već imao compact trust + responsive filters |
| Data Quality | ACCEPTABLE | presentation pass-2; UX polish shared table |
| Pre/posle nivelacije | ACCEPTABLE | responsive filters |
| Pre-nivelacija prioriteti | ACCEPTABLE | mobile summary bez lažnog „period“ u empty-state |
| Supplier Decision / Footwear | ACCEPTABLE | reset + table pilot |
| Product Decision | ACCEPTABLE | trust compact; insufficient signal već gated |
| Insight Studio | NEEDS FOLLOW-UP | RQ582 experimental; hijerarhija widgeta još teška |

## Responsive / a11y (šta je provereno)

- **Kod/static:** collapsible trust (aria-expanded/controls), focus-visible na toggle, table scroll region + hint default, tabular-nums.
- **Nije rađen** live browser prolaz na 1440/1024/768/390 u ovom tasku (merenja iz RESPONSIVE_REAUDIT_2026-10-04 i dalje važe kao residual kontekst).

## Residuals

1. Insight Studio product hierarchy / score semantics — RQ582 / product owner.
2. WAITING P-UI-31/35/36/38 responsive migracije — ne dirati kao queue claim; ovaj task je smanjio trust height i uključio postojeće pilot pattern-e.
3. Live visual/a11y sweep.
4. Canonical Prihod vs Promet drift na više površina — ne masovni rename bez PO (Prihod ostaje gde je već etabliran).

## Tests

Focused: TrustHeader 26, DataTable, ExecutiveKpi, Methodology, ControlBar, Footwear, Shoe premium, PreNivelacija 56, Color, Daily premium — zeleni u finalnoj rundi (vidi evidence).
`tsc -b` OK; `check:encoding` OK; `git diff --check` OK.

## Docs follow-up (2026-10-05)

Business terminology drift (Prihod vs Promet) and unavailable-label rules are locked in `docs/ai/ANALYTICS_BUSINESS_GLOSSARY.md` without inventing an owner rename.


## Recertify 2026-10-05

Re-checked claimed P0/P1 UX fixes against current tip: TrustHeader still defaults collapsed (`useState(false)`); `responsivePilot` default true; reset/DQ Serbian labels present. Ratings unchanged (Insight Studio remains **NEEDS FOLLOW-UP** / RQ582). Adjacent presentation gap in Insight signals panel addressed under presentation recertify, not as a new polish upgrade.

## Recertify-2 2026-10-05
Re-verified Trust collapse P0 still holds (`AnalyticsTrustHeader` details collapsed by default; critical stale/partial/gated cues above the fold). No UX redesign in this pass.
