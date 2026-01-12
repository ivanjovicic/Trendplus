# ?? Troubleshooting - 500 Error na Analytics Dashboard

## Problem
Frontend prikazuje grešku:
```
GET http://localhost:5174/api/analytics/sales/top-products?top=10 500 (Internal Server Error)
```

## Uzroci

### 1?? **Analytics tabele još ne postoje**
SQL skripte nisu pokrenute na analytics bazi.

**Rešenje**:
```bash
# Kreiraj Analytics tabele
psql -d analytics_db -f Database/Analytics/001_CreateSalesFactTables.sql
psql -d analytics_db -f Database/Analytics/002_AddVelicinaBojaToProductsDim.sql

# Dodaj kolone u write bazu
psql -d trendplus_db -f Database/Migrations/003_AddVelicinaBojaToArtikli.sql
```

### 2?? **Backend nije restartovan**
Izmene u kodu nisu u?itane.

**Rešenje**:
```bash
# Restart backend
cd Trendplus2
dotnet run
```

### 3?? **Nema podataka u SalesFacts tabeli**
OutboxProcessor još nije procesirao prodaje.

**Rešenje**:
```bash
# Kreiraj test podatke
psql -d trendplus_db -f Database/Migrations/004_SimpleTestData.sql

# Sa?ekaj 90 sekundi
# - 60s za SyncWorker (artikli)
# - 30s za OutboxProcessor (prodaje)
```

---

## ?? Complete Fix (copy-paste):

```bash
# 1. Zaustavi backend (Ctrl+C)

# 2. Kreiraj tabele (ako ve? nisu)
psql -d analytics_db -f Database/Analytics/001_CreateSalesFactTables.sql
psql -d analytics_db -f Database/Analytics/002_AddVelicinaBojaToProductsDim.sql
psql -d trendplus_db -f Database/Migrations/003_AddVelicinaBojaToArtikli.sql

# 3. Kreiraj test podatke
psql -d trendplus_db -f Database/Migrations/004_SimpleTestData.sql

# 4. Restart backend
cd Trendplus2
dotnet run

# 5. Sa?ekaj 90 sekundi

# 6. Osvezi frontend
# Ctrl + Shift + R (hard refresh)

# 7. Proveri /analytics dashboard
```

---

## ?? Verify Database State

### Write Database (trendplus_db):
```sql
-- Check Artikli
SELECT COUNT(*) as total, COUNT("Velicina") as sa_velicinom 
FROM "Artikli";

-- Check Sales
SELECT COUNT(*) FROM prodaja_zaglavlje WHERE broj_racuna LIKE 'DEMO-%';

-- Check Outbox
SELECT "IsProcessed", COUNT(*) 
FROM "OutboxMessages" 
WHERE "Payload"::jsonb->>'BrojRacuna' LIKE 'DEMO-%'
GROUP BY "IsProcessed";
```

### Analytics Database (analytics_db):
```sql
-- Check ProductsDim
SELECT COUNT(*) as total, COUNT("Velicina") as sa_velicinom 
FROM "ProductsDim";

-- Check SalesFacts
SELECT COUNT(*) FROM "SalesFacts";

-- Check SalesLineFacts
SELECT COUNT(*) FROM "SalesLineFacts";
```

---

## ? Expected Results After Fix:

### Backend Logs:
```
[Info] GetTopProducts query: Top=10
[Info] Sales count after filters: 5
[Info] Top products: 8 by revenue, 8 by units
```

### Frontend (/analytics):
- ? Health check: "? Analytics baza: 5 prodaja, 25 stavki, 152 proizvoda"
- ? Sales Summary prikazuje podatke
- ? Top Products tabele prikazuju artikle sa veli?inama i bojama
- ? Inventory Status prikazuje SKU count

---

## ?? Still Not Working?

### Check Backend Logs:
```bash
# Windows PowerShell
Get-Content -Path "logs/trendplus.log" -Tail 50 -Wait

# Or check Output window in Visual Studio
# View ? Output ? Show output from: Debug
```

### Check Frontend Network Tab:
1. F12 ? Network tab
2. Refresh page
3. Click on failed `/api/analytics/sales/top-products` request
4. Check **Response** tab for error details

### Manual Database Check:
```sql
-- Run on analytics_db
SELECT 
    table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_name IN ('SalesFacts', 'SalesLineFacts', 'ProductsDim');

-- Should return 3 rows
```

---

## ?? Recent Code Changes:

### ? Fixed:
- **GetTopProductsHandler.cs** - Added graceful handling for missing tables
- **analyticsApi.ts** - Already has proper encoding
- **AnalyticsDashboard.tsx** - Already handles empty data

### ? Build Successful:
All backend changes are compiled and ready.

---

## ?? Related Files:
- `Database/Migrations/DEBUG_CheckState.sql` - Quick database check script
- `Database/Migrations/QUICKSTART.md` - Complete setup guide
- `ANALYTICS_VELICINA_BOJA_FEATURE.md` - Feature documentation
