# ?? QUICK FIX - Analytics 500 Error

## ? **3 na?ina da rešite problem:**

---

## **Option 1: Ultra Simple** (copy-paste)

```powershell
# 1. Pokreni SQL skriptu
psql -d trendplus -f Database/Migrations/005_CreateArtikliAndTestData.sql

# 2. Restart backend
cd Trendplus2
dotnet run

# 3. Sa?ekaj 90s, refresh browser
```

**Potrebno**: PostgreSQL installed, `psql` in PATH, database `trendplus` postoji

---

## **Option 2: Auto-Detect PostgreSQL** (PREPORU?ENO)

```powershell
.\run-sql-auto.ps1
```

**Šta radi**:
- ? Automatski pronalazi PostgreSQL na sistemu
- ? Traži u common lokacijama (Program Files, itd.)
- ? Pita vas za path ako ne na?e
- ? Pokre?e SQL skriptu
- ? Prikazuje slede?e korake

**Potrebno**: PostgreSQL installed (bilo gde), database `trendplus` postoji

---

## **Option 3: Simple PowerShell** (samo SQL)

```powershell
.\run-sql-simple.ps1
```

**Ako dobijete grešku**:
- Promenite `$DB` varijablu u skripti na ime vaše baze

---

## ?? **Šta SQL skripta radi:**

`005_CreateArtikliAndTestData.sql` ?e:

1. ? **Kreirati 15 sample artikala** (ako ih nemate)
   - Nike, Adidas, Puma, Converse, New Balance, itd.
   - Sa cenama, koli?inama, dobavlja?em, sezonom

2. ? **Dodati Velicina i Boja** prvih 10 artikala
   - Veli?ine: 40, 41, 42, 43, 44, 45
   - Boje: Crna, Bela, Braon, Siva, Plava, Crvena

3. ? **Kreirati 5 test prodaja** (DEMO-001 do DEMO-005)
   - Razli?ite kombinacije artikala
   - Razli?iti na?ini pla?anja

4. ? **Kreirati Outbox events**
   - Za svaku prodaju
   - Spremni za OutboxProcessor

5. ? **Prikazati summary**
   - RAISE NOTICE poruke sa detaljima

---

## ?? **Troubleshooting:**

### Greška: "psql: command not found"

**Uzrok**: PostgreSQL nije u PATH-u

**Rešenje**:
```powershell
# Add to PATH (Windows)
$env:Path += ";C:\Program Files\PostgreSQL\16\bin"

# Or run from PostgreSQL directory
cd "C:\Program Files\PostgreSQL\16\bin"
.\psql -d trendplus -f "C:\path\to\Database\Migrations\005_CreateArtikliAndTestData.sql"
```

### Greška: "database 'trendplus' does not exist"

**Uzrok**: Baza ne postoji ili se zove druga?ije

**Rešenje 1**: Kreiraj bazu
```powershell
createdb trendplus
```

**Rešenje 2**: Promeni ime baze u skripti
```powershell
# Edit run-sql-simple.ps1
$DB = "your_actual_db_name"
```

### Greška: "permission denied"

**Uzrok**: Nemate permissions na bazi

**Rešenje**:
```sql
-- Run as postgres superuser
psql -U postgres -d trendplus -f Database/Migrations/005_CreateArtikliAndTestData.sql
```

### Greška: "relation 'Artikli' does not exist"

**Uzrok**: Glavne tabele nisu kreirane

**Rešenje**: Pokrenite EF migrations prvo
```powershell
cd Infrastructure
dotnet ef database update --context TrendplusDbContext
```

---

## ? **Verifikacija da je uspelo:**

```sql
-- Proveri artikle
SELECT "Id", "Naziv", "Velicina", "Boja" 
FROM "Artikli" 
WHERE "Velicina" IS NOT NULL 
LIMIT 5;

-- Proveri prodaje
SELECT * FROM prodaja_zaglavlje 
WHERE broj_racuna LIKE 'DEMO-%';

-- Proveri outbox
SELECT "IsProcessed", COUNT(*) 
FROM "OutboxMessages" 
WHERE "Payload"::jsonb->>'BrojRacuna' LIKE 'DEMO-%'
GROUP BY "IsProcessed";
```

**Trebalo bi da vidite**:
- 10+ artikala sa veli?inama
- 5 prodaja (DEMO-001 do DEMO-005)
- 5 outbox events (IsProcessed = false)

---

## ?? **Restart Backend:**

```powershell
# Stop backend (Ctrl+C if running)

# Restart with new code
cd Trendplus2
dotnet clean
dotnet build
dotnet run

# Wait 90 seconds for workers
```

---

## ?? **Expected Results:**

### After 60s (SyncWorker):
- `ProductsDim` u analytics bazi ima artikle sa veli?inama

### After 90s (OutboxProcessor):
- `SalesFacts` ima 5 prodaja
- `SalesLineFacts` ima ~25 stavki
- Outbox events: `IsProcessed = true`

### Frontend (`/analytics`):
```
? Analytics baza: 5 prodaja, 25 stavki, 15 proizvoda

Sales Summary:
  Ukupan promet: 117,600 RSD
  Transakcije: 5
  
Top proizvodi:
  | Artikal | Veli?ina | Boja | Promet | Kom |
```

---

## ?? **Files in this fix:**

| File | Purpose |
|------|---------|
| `005_CreateArtikliAndTestData.sql` | Main SQL script |
| `setup-manual.ps1` | PowerShell with checks |
| `run-sql-simple.ps1` | Simple SQL runner |
| `THIS_README.md` | This file |
| `HITNA_POMOC.md` | Detailed troubleshooting |

---

## ?? **Still not working?**

1. Check `HITNA_POMOC.md` for detailed troubleshooting
2. Check `TROUBLESHOOTING_500_ERROR.md` for backend issues
3. Verify database names match in connection strings

---

**TL;DR**: Pokrenite `.\run-sql-simple.ps1`, pa `dotnet run`, sa?ekajte 90s! ??
