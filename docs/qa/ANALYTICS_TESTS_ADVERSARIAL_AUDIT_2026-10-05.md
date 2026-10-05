# Analytics tests adversarial audit — 2026-10-05

Datum: 2026-10-05 (Europe/Belgrade).
Osnova: `origin/main` tip pri startu `8703a0b7`.
Način: box-only (`/workspace/docs-audit`); Trendplus2 WT nije diran; RQ588 nije diran; RQ582 Insight quarantine poštovan.
Dokaz: `.ai/runs/2026-10-05-analytics-tests-adversarial-audit-evidence.md`.

## Cilj

Testovi moraju dokazati poslovnu tačnost i hvatati regresije — ne samo da budu zeleni. Metod: tražiti način da Analytics vrati pogrešan broj dok postojeći testovi i dalje prolaze, zatim dodati test koji to zabranjuje.

## Scope pregleda

Pregledano (inventar + uzorkovanje sadržaja):

| Zona | Broj fajlova (približno) |
|---|---:|
| Backend `Api.Tests` Analytics / Sales / Inventory / Nivelacija / Decision / DQ / Oracle | ~120 |
| Frontend Vitest Analytics / Inventory / Supplier / Daily / Color / Nivelacija | ~110 |
| Fixtures / golden / oracle helpers | ~15 |
| Prior QA/test strategy / hardening docs | ~20 |

Ključni postojeći dokazi (zadržani, ne duplirani):

- `SupplierShoeTypeIndependentOracleIntegrationTests` + `SupplierShoeTypeRawFactOracle`
- `OperationsAnalyticsAllRoutesIntegrationTests` (RQ561 six-screen)
- `OperationsAnalyticsRawFactOracleIntegrationTests`
- `SupplierShoeTypeAdversarialGoldenManifestTests` (RQ446)
- `PreNivelacijaPriorityOracleIntegrationTests`, `SupplierScorecardOracleTests`, `AssortmentNivelacijaOracleTests`
- `DailySalesStatsServiceTests` (half-open, DUG/KOREKCIJA)
- `AnalyticsMarginPolicyTests`, `SalesReceiptPopulationPolicyTests`, `SupplierSharePolicyTests`

## Pronađeno — slabi / pogrešni testovi

| # | Problem | Zašto je opasno | Disposition |
|---|---|---|---|
| 1 | `AnalyticsSupplierSalesUnitTests.MarginWithNullCost_ShouldUseFallback` učio da `null` cost ⇒ marža = ceo promet | Zaključava fake-full-margin bug; kontradikcija sa `MarginAccumulator` / `AnalyticsMarginPolicy` | **FIXED** — test sada zahteva coverage 0 / contribution 0 |
| 2 | `AggregationInvariantTests` tautologije (`Assert.Equal(3460, 1500+1000+960)` bez SUT) | FE+BE mogu biti pogrešni, suite i dalje zelen | **FIXED** — vezano za `SupplierSharePolicy` + `MarginAccumulator` |
| 3 | `PctHelperTests` lokalni `Pct` sa +100% na zero baseline | Nije production contract; `ComputeTrendPct` vraća `null` | **FIXED** — zero previous ⇒ unavailable |
| 4 | Cross-screen equality nije eksplicitno vezana | RQ561 proverava iste brojeve odvojeno; jedan ekran može driftovati | **ADDED** `AnalyticsCrossScreenRevenueInvariantIntegrationTests` |
| 5 | Nema metamorphic suite (`[A,C)=[A,B)+[B,C)`, +100 RSD, other-store, full return) | Accuracy audit 2026-10-05 već označio kao residual | **ADDED** in-memory + FE mirror |
| 6 | Oracle manifest tvrdio inclusive period + Artikli join za Supplier/Shoe oracle | Doc drift vs `SupplierShoeTypeRawFactOracle` (half-open, no Artikli for totals) | **FIXED** docs |
| 7 | Guardrail baseline linija `IntelligenceSnapshotPanel:273` zastarela posle presentation fixa | `check:analytics-guardrails` pada na line drift | **FIXED** baseline → `:274` |
| 8 | `DecisionRecommendationEngineInputTests` i dalje tautološki bound checkovi | Lažna sigurnost | **residual** (nije production bug; cleanup kasnije) |
| 9 | Color nema zaseban deep endpoint-vs-oracle suite kao Supplier/Shoe | Delimično pokriveno RQ561 + novi cross-screen | **residual** — cross-screen pokriva totals; bucket-level Color oracle follow-up |
| 10 | Postgres/Docker nije dostupan na boxu | Cross-screen integration SKIP bez `TRENDPLUS_RUN_INTEGRATION_TESTS` | **documented**; CI/PC treba da ih pokrene |

## Ispravke / dodaci

### Novi oracle / metamorphic

- `Api.Tests/Analytics/CertifiedRetailLineOracle.cs` — in-memory certified retail population (half-open, DUG/KOREKCIJA, store, dataScope, signed qty).
- `Api.Tests/AnalyticsCertifiedRetailMetamorphicTests.cs` — hand-expected kit + metamorphic/boundary/null-cost/share/extreme.
- `Klijent/clientapp/src/utils/__tests__/certifiedRetailMetamorphic.spec.ts` — FE mirror istih invarijanti.
- `Api.Tests/AnalyticsCrossScreenRevenueInvariantIntegrationTests.cs` — Daily == Supplier == Shoe Type == Color.

### Canonical kit (hand-checkable)

2 prodavnice; više dobavljača/tipova/boja/dana; boundary `from` / `toExclusive`; sale; return; DUG/KOREKCIJA; missing cost; unknown dims; previous-only entity; negative margin; adjacent-day additivity.

Hand expected `[2026-09-01, 2026-09-02)` all stores: **promet 550 RSD**, units 6 (posle isključenja DUG/KOREKCIJA i `toExclusive`).

### Bug klase koje novi testovi hvataju

- Period non-additivity / wrong exclusive end
- Store leak
- DUG/KOREKCIJA uključeni u promet
- Null cost ⇒ fake 100% margin
- Non-positive share prikazan kao 0%
- Cross-screen promet drift
- Measured zero vs empty window conflation
- Synthesized +100% trend na missing previous

## Test rezultati (ovaj prolaz)

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Focused BE adversarial + related unit | 40 | 0 | 2 (cross-screen, no integration env) |
| FE metamorphic + unavailable + shift | 12 | 0 | 0 |
| `check:encoding` | OK | | |
| `check:analytics-guardrails` + `tsc -b` | OK | | |
| `git diff --check` | OK | | |
| Full Postgres RQ561 / independent oracle | not re-run (no Docker on box) | | |

## Residual

1. Pokrenuti `AnalyticsCrossScreenRevenueInvariantIntegrationTests` + RQ561 na PC/CI sa Docker + `TRENDPLUS_RUN_INTEGRATION_TESTS=true`.
2. Color bucket-level independent oracle (totals već u cross-screen).
3. Preostale tautologije u `DecisionRecommendationEngineInputTests` / lokalni `Pct` u `AllEndpoints` vs `ComputeTrendPct` (product razlika — ne dirati bez PO).
4. Live Jul window re-probe (UNPROVEN-RUNTIME) — van ovog box prolaza.
5. RQ588 IN_PROGRESS — nije diran; RQ582 Insight — nije redesignovan.

## Delivery

Commits na grani `analytics-tests-adversarial-2026-10-05`; bundle za parent FF na `main`.
