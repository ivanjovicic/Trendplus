# Codex Publish Safe Cherry-Pick Plan

Datum: 2026-06-16
Izvorna grana: `origin/codex/publish-all-changes`
Izvorni commit za bezbedne dodatke: `6278af4` (`feat: finalize analytics readiness and demo tooling`)

## Cilj

Ovaj plan namerno ne koristi merge cele `origin/codex/publish-all-changes` grane.

Razlog:
- `main` je ispred te grane za veliki broj novijih analytics promena
- branch sadrži regresivne izmene za pilot readiness route/nav i inventory trust test
- želimo samo branch-only fajlove koji ne prepisuju noviji `main`

## Safe As-Is kandidati

Ove fajlove je bezbedno prenositi kao nove dodatke, bez prepisivanja postojećih fajlova:

```text
scripts/demo-data/generate-demo-data.py
scripts/demo-data/generate-demo-data.ps1
seed/demo-data/analytics/AnalyticsActionItems.csv
seed/demo-data/analytics/AnalyticsRefreshRuns.csv
seed/demo-data/analytics/FootwearTypesDim.csv
seed/demo-data/analytics/InventoryMovementFacts.csv
seed/demo-data/analytics/InventoryRecommendations.csv
seed/demo-data/analytics/ProductsDim.csv
seed/demo-data/analytics/SalesFacts.csv
seed/demo-data/analytics/SalesLineFacts.csv
seed/demo-data/analytics/SeasonsDim.csv
seed/demo-data/analytics/StoresDim.csv
seed/demo-data/analytics/SuppliersDim.csv
seed/demo-data/operational/Artikli.csv
seed/demo-data/operational/DataImportBatches.csv
seed/demo-data/operational/DnevnikPromena.csv
seed/demo-data/operational/Dobavljaci.csv
seed/demo-data/operational/Sezone.csv
seed/demo-data/operational/TipoviObuce.csv
seed/demo-data/operational/prodaja_stavke.csv
seed/demo-data/operational/prodaja_zaglavlje.csv
seed/demo-data/support/data_quality_issues.csv
seed/demo-data/support/manifest.json
seed/demo-data/support/stock.csv
```

Zašto su ovi fajlovi safe:
- svi su branch-only `A` fajlovi, bez prepisivanja postojećeg sadržaja u `main`
- ne diraju rute, navigation, tests, analytics algoritme ili backend contract
- demo generator i seed paket su izolovani od runtime path-a dok ih eksplicitno ne koristimo

## Ne prenositi raw cherry-pick-om

Ove branch-only ili branch-delta fajlove ne treba uzimati automatski iz iste grane:

```text
.github/workflows/ci.yml
scripts/demo-data/load-demo-data.ps1
seed/demo-data/README.md
docs/Analytics/KPI_METHODOLOGY_COVERAGE.md
docs/demo/ANALYTICS_DEMO_MODE_PLAN.md
docs/ops/ANALYTICS_PERFORMANCE_BUDGETS.md
docs/ops/BACKUP_RESTORE_RUNBOOK.md
docs/ops/PILOT_DATA_SAFETY_CHECKLIST.md
```

Razlog za izuzimanje:
- `ci.yml` uvodi paralelan CI tok koji može da se preklopi sa postojećim analytics gate-ovima
- `load-demo-data.ps1` radi destruktivni `TRUNCATE` i zavisi od aktuelne lokalne DB šeme i container imena
- `seed/demo-data/README.md` sadrži lokalni path iz drugog okruženja i treba repo-specifičnu doradu
- docs delte prepisuju novije dokumente ili audit-e koje treba ručno uporediti sa današnjim `main`

## Tačan prenos

Preporučeni način je file-level restore sa grane, ne commit-level cherry-pick:

```powershell
git switch -c codex/import-safe-demo-seed

git restore --source origin/codex/publish-all-changes -- `
  scripts/demo-data/generate-demo-data.py `
  scripts/demo-data/generate-demo-data.ps1 `
  seed/demo-data/analytics/AnalyticsActionItems.csv `
  seed/demo-data/analytics/AnalyticsRefreshRuns.csv `
  seed/demo-data/analytics/FootwearTypesDim.csv `
  seed/demo-data/analytics/InventoryMovementFacts.csv `
  seed/demo-data/analytics/InventoryRecommendations.csv `
  seed/demo-data/analytics/ProductsDim.csv `
  seed/demo-data/analytics/SalesFacts.csv `
  seed/demo-data/analytics/SalesLineFacts.csv `
  seed/demo-data/analytics/SeasonsDim.csv `
  seed/demo-data/analytics/StoresDim.csv `
  seed/demo-data/analytics/SuppliersDim.csv `
  seed/demo-data/operational/Artikli.csv `
  seed/demo-data/operational/DataImportBatches.csv `
  seed/demo-data/operational/DnevnikPromena.csv `
  seed/demo-data/operational/Dobavljaci.csv `
  seed/demo-data/operational/Sezone.csv `
  seed/demo-data/operational/TipoviObuce.csv `
  seed/demo-data/operational/prodaja_stavke.csv `
  seed/demo-data/operational/prodaja_zaglavlje.csv `
  seed/demo-data/support/data_quality_issues.csv `
  seed/demo-data/support/manifest.json `
  seed/demo-data/support/stock.csv
```

## Minimalna validacija posle prenosa

```powershell
git diff --check
python .\scripts\demo-data\generate-demo-data.py --output-root "$env:TEMP\\trendplus-demo-check"
Get-Content .\seed\demo-data\support\manifest.json
```

Šta validiramo:
- nema whitespace ili patch problema
- generator može da izgeneriše dataset van repoa
- `manifest.json` ostaje čitljiv i brojke odgovaraju demo paketu

## Commit predlog

Ako se ovaj plan kasnije sprovede bez dodatnih prilagođavanja:

```text
feat(demo-data): import safe seed package from codex publish
```

Ako se pored safe paketa kasnije dorađuju loader ili docs, to treba odvojiti u poseban commit.
