# Backup and Restore Runbook

Ovaj runbook pokriva ručni backup i restore tok za pilot situacije, plus **rehearsal skripte** u `scripts/ops/`.
Ne pretpostavlja potvrđen produkcioni scheduler. Live restore se radi samo na disposable target.

## Executable rehearsal (STAB07)

| Script | Use |
|---|---|
| `scripts/ops/Test-BackupRestoreGuards.ps1` | Guard self-tests (no DB required) |
| `scripts/ops/Backup-PostgresDatabase.ps1` | `pg_dump` for `operational` or `analytics` |
| `scripts/ops/Restore-PostgresDatabase.ps1` | `pg_restore` into disposable DB (`-AllowDestructiveRestore`) |
| `scripts/ops/Invoke-BackupRestoreRehearsal.ps1` | Orchestrates both DB roles |

```powershell
# Guard self-test
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ops\Test-BackupRestoreGuards.ps1

# Dry-run (no pg_dump/pg_restore)
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ops\Invoke-BackupRestoreRehearsal.ps1 -EnvironmentLabel local -DryRun

# Live disposable rehearsal (requires env URLs; NEVER production)
# Prefer Docker client when host PG client major != server (e.g. host 18 vs container 16):
$env:TRENDPLUS_PG_DOCKER_CONTAINER = "trendplus-postgres"
$env:TRENDPLUS_OPS_REHEARSAL_SOURCE_URL = "<disposable-source>"
$env:TRENDPLUS_OPS_REHEARSAL_DEST_URL = "<disposable-dest>"
$env:TRENDPLUS_ANALYTICS_REHEARSAL_SOURCE_URL = "<disposable-analytics-source>"
$env:TRENDPLUS_ANALYTICS_REHEARSAL_DEST_URL = "<disposable-analytics-dest>"
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ops\Invoke-BackupRestoreRehearsal.ps1 -EnvironmentLabel local -AllowDestructiveRestore

# Optional full restore including indexes + materialized view refresh (can be very slow):
# ... -AllowDestructiveRestore -IncludePostData
```

Evidence template/result: `docs/ops/BACKUP_RESTORE_REHEARSAL_EVIDENCE_2026-08-06.md`.

### Safety contract

- Allowed labels: `local`, `rehearsal`, `disposable`, `staging-rehearsal`, `ci-rehearsal`.
- Production labels/targets are refused.
- Connection secrets are never printed (only host/database/port).
- Npgsql `Host=`/`Username=` strings are converted to libpq URIs for host tools.
- Restore requires `-AllowDestructiveRestore`.
- Default restore uses `pre-data`+`data` only; use `-IncludePostData` for indexes/MV refresh.
- Missing dump fails closed.
- Cache is not restored; analytics refresh/invalidation is required after restore.

## Kada ga koristiti

- loš import je upisao pogrešne podatke
- refresh je pokvario analytics stanje
- neko je obrisao ili promenio podatke koje treba vratiti
- treba vratiti stanje pre pilot incidenta
- potrebno je sačuvati dokazne artefakte pre resetovanja
- treba uraditi disposable rehearsal pre nego što se backup tretira kao dokazan

## Pre nego što kreneš

- Zaustavi ili pauziraj worker/refresh tok ako dalji upisi mogu da pogoršaju stanje.
- Zabeleži vreme incidenta i poslednji poznati dobar trenutak.
- Sačuvaj trenutni DB state pre restore-a ako je moguće.
- Identifikuj da li treba vratiti operativni DB, analytics DB ili oba.
- Sačuvaj relevantne import fajlove, reportove i logove kao dokazni materijal.
- Potvrdi da destination **nije** production (koristi rehearsal skripte ili provider disposable project).

## Backup tok

| Korak | Opis |
|---|---|
| 1 | Napravi backup operativnog DB pre svake rizične akcije (`-RoleName operational`). |
| 2 | Napravi backup analytics DB ako restore može da utiče na agregate, snapshot-e ili status osvežavanja (`-RoleName analytics`). |
| 3 | Sačuvaj originalni import fajl i staging kopije. |
| 4 | Arhiviraj generisane reportove i snapshot-e koji su bitni za audit ili poređenje. |
| 5 | Izvuci logove iz intervala pre i posle incidenta. |
| 6 | Zabeleži SHA256/size/timestamp dump fajla (skripta piše `.backup.meta.json`). |

## Restore tok

1. Potvrdi da je problem stvaran i da nije samo stale cache ili kasni refresh.
2. Izaberi najnoviji dobar backup ili snapshot.
3. Zabeleži očekivani opseg restore-a pre promene bilo čega.
4. Restore-uj samo potreban sistem ako je moguće; ne vraćaj više nego što mora.
5. Koristi disposable destination + `-AllowDestructiveRestore` (nikad production URL).
6. Nakon restore-a pokreni validaciju podataka.
7. Tek kada su podaci konzistentni, uključi refresh tokove i worker-e nazad.
8. Ako restore nije uspeo, zaustavi dalje pokušaje i sačuvaj logove greške.

## Validacija posle restore-a

- proveri da li se dashboard otvara bez error state-a
- proveri `/health` i `/ready` na restored connection-ima
- proveri poslednji uspešan refresh i freshness status
- proveri sample brojeve za prodaju, dobavljače i akcione stavke
- proveri da li report export radi i da li odgovara očekivanom periodu
- proveri da li su import i action queue podaci konzistentni sa backup snapshot-om
- proveri da li postoje neočekivani `0` rezultati tamo gde bi trebalo da bude `unknown` ili error
- proveri da migration/history ili schema presence nije prazan (rehearsal skripta radi basic check)

## Post-restore refresh

- Pokreni refresh tek nakon što je restore potvrđen.
- Ako je refresh stari ili nepoznat, označi stanje kao warning dok se ne dobije novi uspešan run.
- Očisti cache samo ako je poznato da sadrži stare ili konfliktne vrednosti.
- Sačuvaj izlazne logove refresh-a i validacije kao deo incident record-a.
- Cache nije deo DB restore-a i nikad nije source of truth.

## Export opcije

- Pre restore-a eksportuj ključne reportove ako su potrebni za poređenje ili klijentsku komunikaciju.
- Ako je dostupan raw data export, sačuvaj i njega kao audit kopiju.
- Ne obećavaj automatski izvoz dok nije potvrđeno da konkretan path postoji u aplikaciji.
- Ako eksport nije dostupan, zabeleži to kao otvoreni gap umesto da ga simuliraš.

## Brisanje i cleanup

- Pilot cleanup radi tek posle eksplicitnog odobrenja.
- Sačuvaj logs/error records pre brisanja.
- Sačuvaj reportove i snapshot-e prema retention odluci.
- Import fajlove briši samo kada je jasno da više nisu potrebni za restore ili audit.
- Cache je dozvoljeno obrisati ako je deo recovery procedure i ako je potvrđeno da se može rekonstruisati.
- Obriši lokalne `tmp/ops-rehearsal/*.dump` posle upisa evidence-a; ne commit-uj dump-ove.

## Otvoreni gapovi

- nema potvrđenog automatskog backup scheduler-a u produkciji
- live disposable restore rehearsal je **BLOCKED** dok operator ne obezbedi non-prod source/dest URL-ove (vidi evidence doc)
- nema standardizovanog retention modela za reportove i import fajlove
- nema aplikacionog mehanizma koji vodi operatera kroz ceo incident
- provider-managed backup retention mora se zapisati iz konzole (Render/Neon), ne pretpostavljati
