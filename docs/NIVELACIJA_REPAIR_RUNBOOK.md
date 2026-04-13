# Nivelacija Repair Runbook

`repair_nivelacije.ps1` standardizuje repair tok za imported `Nivelacija` redove iz Access-a.

## Sta radi

- radi pre-flight proveru preko `GET /admin/repair/nivelacije/preflight`
- pravi backup preko `pg_dump`
- pokrece `dryRun` preko `POST /admin/repair/nivelacije`
- cuva JSON plan lokalno
- trazi eksplicitnu potvrdu operatera
- pokrece live repair tek sa `confirm=true`

## Potrebno

- backend sa novim repair endpoint-om
- `ADMIN_API_KEY` ili prosledjen `-AdminKey`
- `pg_dump` u `PATH` ako se ne koristi `-SkipBackup`
- `TRENDPLUS_PG_URL` ili prosledjen `-PostgresUrl`

## Primer pokretanja

```powershell
./repair_nivelacije.ps1 \
  -ApiBaseUrl "https://trendplus-api.example.com" \
  -AdminKey "$env:ADMIN_API_KEY" \
  -PostgresUrl "$env:TRENDPLUS_PG_URL"
```

Sa eksplicitnim MDB fajlom:

```powershell
./repair_nivelacije.ps1 \
  -ApiBaseUrl "https://trendplus-api.example.com" \
  -AdminKey "$env:ADMIN_API_KEY" \
  -PostgresUrl "$env:TRENDPLUS_PG_URL" \
  -SourceFilePath "C:\data\Trend plus.mdb"
```

## Endpoint ugovor

### Preflight

`GET /admin/repair/nivelacije/preflight?sourceFilePath=...`

Vraca:

- resolved source file path
- required Postgres objects
- pronadjene Access tabele
- default threshold

### Dry run

`POST /admin/repair/nivelacije`

Body:

```json
{
  "dryRun": true,
  "confirm": false,
  "sourceFilePath": "C:\\data\\Trend plus.mdb",
  "maxRowsToModify": 10000
}
```

Vraca:

- `detectedIssues`
- `proposedFixes`
- `estimatedImpact`
- `verification`
- `auditId`

### Live repair

`POST /admin/repair/nivelacije`

Body:

```json
{
  "dryRun": false,
  "confirm": true,
  "sourceFilePath": "C:\\data\\Trend plus.mdb",
  "maxRowsToModify": 10000
}
```

Vraca:

- `fixedRows`
- `skippedRows`
- `auditId`
- `remainingIssuesAfterRepair`
- `verification`

## Safety mehanizmi

- live repair se ne izvrsava bez `confirm=true`
- threshold default je `10000` redova
- repair je idempotentan: drugi run ne menja vec uskladjene redove
- audit se upisuje u `nivelacija_repair_audit`
- live update ide u jednoj transakciji

## Rucno pokretanje

Rucni dry-run preko PowerShell-a:

```powershell
powershell.exe -ExecutionPolicy Bypass -File C:\path\to\repair_nivelacije.ps1 -ApiBaseUrl "https://trendplus-api.example.com" -AdminKey "%ADMIN_API_KEY%" -SkipBackup -DryRunOnly
```

GitHub Actions workflow za rucni dry-run je dodat u [.github/workflows/nivelacija-repair-dry-run.yml](../.github/workflows/nivelacija-repair-dry-run.yml).

Workflow radi sledece:

- pokrece `repair_nivelacije.ps1` u `-DryRunOnly` rezimu
- cuva JSON izlaz kao artifact
- upisuje kratak rezime u job summary
- failuje job ako dry-run vidi issue-je ili predjeni threshold

Potrebni GitHub repo secrets/variables:

- secret `ADMIN_API_KEY`
- variable `NIVELACIJA_REPAIR_API_URL`
- optional variable `NIVELACIJA_REPAIR_SOURCE_FILE_PATH`
- optional variable `NIVELACIJA_REPAIR_MAX_ROWS_TO_MODIFY`

Preporuka za koriscenje je:

- pokretanje samo kada zelis proveru stanja ili pre live repair-a
- live repair samo manuelno, nikad automatski