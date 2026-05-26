# KPI Methodology Audit

Date: 2026-05-26
Scope: core analytics screens i durable report prikazi.

## Coverage tabela

| Screen | KPI label | metricKey | Explain button | Methodology panel | Formatter | Notes |
|---|---|---|---|---|---|---|
| /analytics | Executive KPI strip (Prihod / Maržni doprinos / Prodate jedinice / Lager u riziku / Spremnost podataka) | revenue / marginContribution / unitsSold / stockAtRisk / dataReadinessScore | Yes | Yes | fmtRsd/fmtNumber + registry | Pokriveno kroz `ExecutiveKpiRow`; methodology panel koristi isti canonical metricKey skup. |
| /analytics | Dodatne KPI kartice (transakcije, promet po danu, dostupnost SKU, crvena zona, MA7/momentum, elastičnost, prosečna korpa) | N/A ili izvedeni signal | No (izuzetak) | No | legacy/detail formatter mix | Dokumentovan izuzetak: ove kartice nisu canonical recommendation KPI skup i zahtevaju poseban registry/backend dogovor ako postanu deo standardne methodology matrice. |
| /analytics/products | Za dopunu / pojačanje / sniženje / ne naručivati / proveriti podatke | replenishCount / boostCount / markdownCount / doNotOrderCount / fixDataCount | Yes | No | fmtNumber | Pokriveno u glavnom KPI bloku. |
| /analytics/products | Procena izgubljene prodaje / Kapital u sporoj zalihi | lostSalesEstimate / slowStockCapital | Yes | No | fmtRsd | Pokriveno u glavnom KPI bloku. |
| /analytics/supplier | Ukupan prihod / Udeo top 5 / Maržni doprinos / Kapital u riziku / Promena udela pune cene | revenue / topSupplierRevenueShare / marginContribution / stockAtRisk / fullPriceShareChange | Yes | No | fmtRsd/fmtPct/fmtSignedPct | Pokriveno u scorecard KPI bloku. |
| /analytics/supplier | Detail: markdown dependency / confidence / reliability | markdownDependency / confidencePct / reliabilityPct | Yes | No | fmtPct | Pokriveno u detail panelu. |
| /analytics/inventory | Signal strip: stockAtRisk / slowStockCapital / outOfStockRisk / lostSalesEstimate / sellThrough | canonical keys | Yes | No | fmtRsd/fmtPct | Pokriveno. |
| /analytics/inventory | Hero: Aktivni SKU / Stanje fonda | activeSkuShare / inventoryHealthScore | Yes | No | formatPercent + score prikaz | Explain dodat. |
| /analytics/data-quality | Spremnost podataka / Bez dobavljača / Bez nabavne cene / Blokirane preporuke / Ignorisani redovi | dataReadinessScore / missingSupplierCount / missingCostCount / blockedRecommendationsCount / ignoredRowsCount | Yes | Yes | fmtNumber/fmtPct | Pokriveno na issues KPI karticama i intake report sekcijama; metodologija je centralizovana kroz report panel. |
| Supplier Decision Report | Dinamički KPI redovi iz payload-a | findAnalyticsMetricKeyByLabel(...) + fallback na row key | Yes | Yes | payload format | Panel koristi centralni registry; payload methodology je prikazan kao dopunska napomena. |
| Pilot Intake Report | Readiness i quality KPI kartice | dataReadinessScore / missingSupplierCount / missingCostCount / revenueWithoutCost / blockedRecommendationsCount / ignoredRowsCount (+ durable mapped keys) | Yes | Yes | fmtNumber/fmtPct | Ako `methodology.metricKeys` postoji u durable payload-u, koristi se; u suprotnom fallback key lista. |

## Canonical metric keys provera

Potvrđeno prisutni i korišćeni (gde je primenjivo):
- revenue
- marginContribution
- unitsSold
- stockAtRisk
- slowStockCapital
- lostSalesEstimate
- dataReadinessScore
- missingCostCount
- missingSupplierCount
- sellThrough
- velocity
- confidencePct
- reliabilityPct
- markdownDependency
- outOfStockRisk

## Dodate/proverene dodatne definicije

Registry pokriva:
- revenueWithoutCost
- unknownSupplierRevenueShare
- stockUnits (alias na onHandUnits)
- lowStockCount
- blockedRecommendationsCount
- ignoredRowsCount
- grossMarginPct
- inventoryTurnover (indikativno, uz quality caveat)
- gmroi: nije uveden kao aktivna metrika; ostaje roadmap TODO dok backend ne isporuči stabilan contract

## Alias mapa

Potvrđeno:
- totalRevenue -> revenue
- soldUnits -> unitsSold
- lostSales -> lostSalesEstimate
- dataReadiness -> dataReadinessScore
- stockRiskCapital -> stockAtRisk

## Rollout zaključak

- Core KPI kartice na products/supplier/inventory/data-quality i report komponentama imaju explain pokrivenost.
- Dashboard je pokriven kroz postojeći `ExecutiveKpiRow` KPI strip i executive methodology panel sa canonical key set-om.
- Unknown metric fallback ostaje graceful kroz centralni registry i `MetricMethodologyPanel` fallback tekst.
- Report methodology je centralizovana kroz `MetricMethodologyPanel`; raw payload metodologija je tretirana kao dopunska napomena, ne kao canonical formula izvor.
