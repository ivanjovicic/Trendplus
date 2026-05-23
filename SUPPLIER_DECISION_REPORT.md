# Supplier Decision Report (Iteracija 1)

## Cilj
Omogucen je profesionalni Supplier Decision Report izvoz/print bez uvodjenja novog backend endpoint-a, kroz postojeci dokument API sloj (`/api/documents/generate`, `/api/documents/print-preview`).

## Gde je integrisano
- Stranica: `SupplierDecisionHubPage`
- Akcije: `Print izvestaj`, `Export Excel`, `Export PDF`
- UI hook: `AnalyticsTableToolbar.extraActions`

## Tehnicki tok
1. Frontend gradi sekcionisani payload kroz `buildSupplierDecisionReportPayload`.
2. Payload se salje na postojeci dokument stack sa `documentType = supplier-decision-report` i `templateName = analytics-table-default`.
3. Print koristi preview endpoint, PDF/Excel koriste generate endpoint + polling statusa za async slucajeve.

## Sadrzaj izvestaja
- Header: period, dobavljac, data scope, vreme generisanja, poslednji refresh
- KPI: prihod, marzni doprinos, broj dobavljaca, top-5 koncentracija
- Preporuke: raspodela statusa (pojacaj/zadrzi/oprez/smanji/nedovoljno)
- Top/Risk sekcije: top po prihodu, rizik zaliha, kandidati za pojacaj/smanji
- Data quality: coverage, ignored rows, missing supplier count
- Methodology + upozorenja (insufficient/partial/fallback)

## Napomene
- Iteracija 1 je namerno frontend-driven (Option B) da minimizuje arhitekturni rizik i reuse-uje postojecu infrastrukuru.
- Sledeca iteracija moze uvesti namenski backend report endpoint i XLSX multi-sheet layout ako bude potreban napredniji format.
