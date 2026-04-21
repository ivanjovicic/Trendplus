# mark-migration-safe.ps1

Kratka pomoć za `mark-migration-safe.ps1` — skripta koja bezbedno označi EF migraciju kao primenjenu
u tabeli `__EFMigrationsHistory` samo nakon verifikacije da schema u bazi odgovara očekivanjima
iz migration fajla.

Glavne osobine:
- Parsira tipične EF Core migration operacije: `CreateTable`, `AddColumn`, `CreateIndex`, `AddForeignKey`.
- Ako detektuje `migrationBuilder.Sql(...)` (raw SQL / data backfill) — abortira i traži manualnu proveru.
- Po defaultu radi u `--DryRun` modu (prikazuje koje provere bi se izvršile i SQL INSERT).

Primeri upotrebe

1) Dry run (preporučeno):

```powershell
.\tools\mark-migration-safe.ps1 -MigrationId 20260419113212_AddAnalyticsCostSnapshotTables -DbHost db.example -Database analytics -User postgres -DryRun
```

2) Izvršenje (bez suhog prikaza) — biće zatražena potvrda osim ako koristite `-AssumeYes`:

```powershell
.\tools\mark-migration-safe.ps1 -MigrationId 20260419113212_AddAnalyticsCostSnapshotTables -DbHost db.example -Database analytics -User postgres -Password 's3cr3t' -DryRun:$false
```

3) Non-interactive izvršenje (automatski potvrđuje):

```powershell
.\tools\mark-migration-safe.ps1 -MigrationId YourMigrationId -DbHost db -Database dbname -User user -Password pass -DryRun:$false -AssumeYes
```

If your provider requires custom SSL/connection parameters (like Neon), you can supply a
full libpq connection string via `-ConnString` instead of `-DbHost/-Port/-User/-Database`.

Example (Neon, using system trusted roots):

```powershell
.\tools\mark-migration-safe.ps1 -MigrationId YourMigrationId -ConnString "host=ep-still-unit-...neon.tech dbname=trendplus user=neondb_owner sslmode=verify-full sslrootcert=system channel_binding=require" -Password 'npg_...'
```

Napomene i preporuke
- Uvek počnite sa `-DryRun` i pregledajte sve detektovane provere.
- Ako migracija sadrži data backfill (raw SQL), ne koristite ovu skriptu — izvršite backfill
  i tek posle toga ubacite zapis u `__EFMigrationsHistory` ručno.
- Ako skripta ne može automatski odrediti `ProductVersion`, prosledi ga sa `-ProductVersion` ili
  upiši ručno kada se traži.
- Skripta koristi `psql` CLI; uveri se da je dostupan na hostu koji pokreće skriptu.

Sigurnost
- Ova skripta pokušava da bude konzervativna i nervozna — cilj je da NE sakrije neusaglašenosti.
