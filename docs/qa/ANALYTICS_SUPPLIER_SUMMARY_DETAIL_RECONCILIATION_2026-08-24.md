# Analytics supplier summary/detail/export reconciliation — 2026-08-24

Scope: RQ112
Family: supplier-decision / sales

Authoritative seeded basis:

- period: `2026-04-01` through `2026-06-29`
- scope: `all`
- source rows:
  - Alpha — `EXPAND`, revenue `520000`, units `1400`
  - Beta — `EXPAND_SELECTIVELY`, revenue `410000`, units `1100`
  - Gamma — `PRICE_NEGOTIATE`, revenue `280000`, units `980`

Expected summary values:

- supplier count: `3`
- full-price revenue share: `0.64`
- full-price sell-through: `0.57`
- markdown revenue share: `0.36`
- pre-markdown margin pct: `0.31`
- capital at risk: `435000`
- top grow supplier: `Alpha`
- top risk supplier: `Gamma`

Expected detail/export values:

- `executive-summary` section keeps the same top-grow and capital-at-risk story as the summary response.
- `top-suppliers` section rows: Alpha, Beta.
- `risk-suppliers` section rows: Gamma.
- legacy `KPI` rows include `Prihod = 1210000`, `Prodate jedinice = 3480`, and `Kapital u riziku = 435000`.
- `report.Rows` and `report.Payload.Rows` are identical row-for-row.

Intentional denominator / coverage difference: none. The reconciliation proof uses the same authoritative dataset for summary, detail, chart-like sections, and export rows.

Proof location:

- `Api.Tests/AnalyticsReportsContractTests.cs`
- `Api.Tests/SupplierDecisionHubContractTests.cs`
