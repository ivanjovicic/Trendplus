# KPI Methodology Coverage

This audit maps the main analytics KPIs to their methodology definitions and to the existing `KpiExplainButton` coverage in the UI.

Legend:
- `Has definition` means the KPI is documented in `analyticsMetricDefinitions.ts`.
- `Has UI explain button` means the screen already renders `KpiExplainButton` for the KPI card or row.
- `Gap` only lists real omissions, not intentional non-card badges.

## Coverage Table

| Screen | KPI | Has definition | Has UI explain button | Gap |
|---|---|---:|---:|---|
| AnalyticsDashboard | Prihod, Maržni doprinos, Prodate jedinice, Lager u riziku, Spremnost podataka | Yes | Yes | None. Main KPIs are covered through `ExecutiveKpiRow`. |
| ProductDecisionCenterPage | Prihod, Maržni doprinos, Prodate jedinice, Kapital u riziku, Procena izgubljene prodaje, Kapital u sporoj zalihi, Pokrivenost zalihe, Sell-through, Sigurnost preporuke, Pouzdanost signala | Yes | Yes | None. Card and inline KPI surfaces already expose explain buttons. |
| SupplierDecisionHubPage | Prihod, Maržni doprinos, Lager u riziku, Sigurnost preporuke, Pouzdanost signala, Zavisnost od nivelacija | Yes | Yes | None. Main scorecard and deep-dive cards already expose explain buttons. |
| InventoryPage | Lager u riziku, Kapital u sporoj zalihi, Rizik nestanka zalihe, Procena izgubljene prodaje, Pokrivenost zalihe, Sell-through | Yes | Yes | None. Inventory KPI cards already have explain buttons. |
| DataQualityPage | Spremnost podataka, Redovi bez nabavne cene, Artikli bez dobavljača, Prihod bez nabavne cene, Promet nepoznatog dobavljača, Blokirane preporuke, Ignorisani redovi | Yes | Yes | None. Data quality KPI cards already have explain buttons. |
| SupplierDecisionReport | Prihod, Maržni doprinos, Prodate jedinice, Lager u riziku, Sigurnost preporuke, Pouzdanost signala, Preporuke dozvoljene | Yes | Yes for KPI rows; No for `Preporuke dozvoljene` badge | `Preporuke dozvoljene` is rendered as a header badge, not a KPI card, so it does not get a button yet. The methodology definition now exists in `analyticsMetricDefinitions.ts`. |

## Definitions Added Or Confirmed

- `revenue` / `Prihod`
- `marginContribution` / `Maržni doprinos`
- `unitsSold` / `Prodate jedinice`
- `stockAtRisk` / `Lager u riziku`
- `slowStockCapital` / `Kapital u sporoj zalihi`
- `lostSalesEstimate` / `Procena izgubljene prodaje`
- `sellThrough` / `Sell-through`
- `confidencePct` / `Sigurnost preporuke`
- `reliabilityPct` / `Pouzdanost signala`
- `dataReadinessScore` / `Spremnost podataka`
- `recommendationAllowed` / `Preporuke dozvoljene`

## Notes

- No calculation logic was changed.
- No backend contract was changed.
- The dashboard main KPI row already uses `ExecutiveKpiRow`, so the explain buttons are inherited there rather than added per screen.
- If `Preporuke dozvoljene` is later promoted from badge to KPI card, it can reuse the new `recommendationAllowed` methodology definition without another registry change.
