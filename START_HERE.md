# ?? FINAL SOLUTION - Analytics Setup

## ?? **Najbrži na?in (ONE-LINE):**

```powershell
.\run-sql-auto.ps1
```

**Zašto ovaj?**
- ? Automatski pronalazi PostgreSQL
- ? Radi bez PATH-a
- ? Jasne poruke o greškama
- ? Prikazuje slede?e korake

---

## ?? **Sve dostupne opcije:**

| Script | Kada koristiti | Potrebno |
|--------|----------------|----------|
| **`run-sql-auto.ps1`** ? | **PREPORU?ENO** - Uvek | PostgreSQL instaliran |
| `run-sql-simple.ps1` | Ako imate `psql` u PATH | `psql` u PATH |
| `setup-manual.ps1` | Za detaljne provere | `psql` u PATH |

---

## ?? **Kompletni setup (step-by-step):**

### 1?? **Pokreni SQL skriptu**

```powershell
.\run-sql-auto.ps1
```

**Šta ?e kreirati:**
- ? 15 sample artikala (Nike, Adidas, Puma, itd.)
- ? 10 artikala sa veli?inama i bojama
- ? 5 test prodaja (DEMO-001 do DEMO-005)
- ? Outbox eventi

### 2?? **Restart backend**

```powershell
cd Trendplus2
dotnet clean
dotnet build
dotnet run
```

**Važno**: Mora `dotnet build` da u?ita nove izmene!

### 3?? **Sa?ekaj workers**

```
? 60s - SyncWorker (Artikli ? Analytics)
? 30s - OutboxProcessor (Prodaje ? SalesFacts)
???????????????????????????????????????????
   90s ukupno
```

### 4?? **Proveri frontend**

```
http://localhost:8080/analytics
Ctrl + Shift + R (hard refresh)
```

---

## ? **Expected Results:**

### Backend logs:
```
[Info] GetTopProducts query: Top=10
[Info] Sales count after filters: 5
[Info] Top products: 8 by revenue, 8 by units
```

### Frontend (`/analytics`):
```
? Analytics baza: 5 prodaja, 25 stavki, 15 proizvoda

Sales Summary:
  Ukupan promet: 117,600.00 RSD
  Transakcije: 5
  Prodate jedinice: 25
  
Top proizvodi po prometu:
  | Artikal          | Veli?ina | Boja  | Promet      | Kom |
  |------------------|----------|-------|-------------|-----|
  | Nike Air Max 90  | 42       | Crna  | 25,000 RSD  | 5   |
  | Jordan 1 Mid     | 45       | Crna  | 16,000 RSD  | 2   |
  | ...
```

---

## ?? **Troubleshooting:**

### ? "Database 'trendplus' does not exist"

**Fix 1**: Kreiraj bazu
```powershell
createdb trendplus
```

**Fix 2**: Promeni ime u skripti
```powershell
# Edit run-sql-auto.ps1, line 4:
$DB = "your_actual_db_name"
```

### ? "Cannot find psql.exe"

Script ?e vas pitati za path. Unesite:
```
C:\Program Files\PostgreSQL\16\bin\psql.exe
```

Ili dodaj u PATH:
```powershell
$env:Path += ";C:\Program Files\PostgreSQL\16\bin"
```

### ? "Permission denied"

Pokreni kao postgres user:
```powershell
# Edit run-sql-auto.ps1, add -U postgres:
& $psqlPath -U postgres -d $DB -f "..."
```

### ? Backend još pokazuje 500 error

**Problem**: Backend nije restartovan sa novim kodom

**Fix**:
```powershell
cd Trendplus2
dotnet clean
dotnet build
dotnet run
```

### ? Frontend prazan (nema greške)

**Problem**: Workers još nisu procesirali

**Fix**: Sa?ekaj još 30-60 sekundi

---

## ?? **Svi fajlovi u ovom fix-u:**

| File | Purpose |
|------|---------|
| **`run-sql-auto.ps1`** ? | Auto-detect PostgreSQL (NAJBOLJI) |
| `run-sql-simple.ps1` | Jednostavna verzija (zahteva PATH) |
| `setup-manual.ps1` | Sa detaljnim proverama |
| `005_CreateArtikliAndTestData.sql` | SQL skripta |
| `ONE_LINE_FIX.md` | Najbrži vodi? |
| `QUICK_FIX_README.md` | Detaljna dokumentacija |
| `HITNA_POMOC.md` | Troubleshooting guide |

---

## ?? **Dodatna dokumentacija:**

- **`ANALYTICS_VELICINA_BOJA_FEATURE.md`** - Feature dokumentacija
- **`TEST_DATA_README.md`** - Test data detalji
- **`TROUBLESHOOTING_500_ERROR.md`** - Backend troubleshooting
- **`QUICKSTART.md`** - Quick start guide

---

## ?? **Verify rezultate:**

### Write Database (trendplus):
```sql
-- Artikli
SELECT COUNT(*) as total, COUNT("Velicina") as sa_velicinom 
FROM "Artikli";
-- Expected: total=15, sa_velicinom=10

-- Prodaje
SELECT COUNT(*) FROM prodaja_zaglavlje WHERE broj_racuna LIKE 'DEMO-%';
-- Expected: 5

-- Outbox
SELECT "IsProcessed", COUNT(*) 
FROM "OutboxMessages" 
WHERE "Payload"::jsonb->>'BrojRacuna' LIKE 'DEMO-%'
GROUP BY "IsProcessed";
-- Expected: IsProcessed=true, COUNT=5 (after 90s)
```

### Analytics Database (analytics_db):
```sql
-- ProductsDim
SELECT COUNT(*) FROM "ProductsDim" WHERE "Velicina" IS NOT NULL;
-- Expected: 10 (after 60s)

-- SalesFacts
SELECT COUNT(*) FROM "SalesFacts";
-- Expected: 5 (after 90s)

-- SalesLineFacts
SELECT COUNT(*) FROM "SalesLineFacts";
-- Expected: ~25 (after 90s)
```

---

## ?? **TL;DR:**

```powershell
# Run this ONE command:
.\run-sql-auto.ps1

# Then restart backend:
cd Trendplus2
dotnet run

# Wait 90 seconds
# Refresh browser: http://localhost:8080/analytics
```

**Gotovo!** ??
