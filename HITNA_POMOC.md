# ?? HITNA POMO? - 500 Error na Analytics

## ? Najbrži fix (3 komande):

```powershell
# 1. Zaustavi backend (Ctrl+C u terminalu gde radi)

# 2. Restart backend sa novim kodom
cd Trendplus2
dotnet run

# 3. Sa?ekaj 90 sekundi, pa osveži browser (Ctrl+Shift+R)
```

---

## ?? Ili koristi automatski script:

```powershell
# Pokreni iz root direktorijuma projekta
.\fix-analytics.ps1
```

Skripta ?e automatski:
- ? Zaustaviti backend
- ? Kreirati tabele
- ? Dodati test podatke  
- ? Restartovati backend
- ? Sa?ekati da workers procesiraju
- ? Prikazati status

---

## ?? Ru?ni koraci (detaljno):

### 1?? Zaustavi backend

**Windows PowerShell**:
```powershell
Get-Process -Name "dotnet" | Where-Object { $_.MainWindowTitle -like "*Trendplus*" } | Stop-Process -Force
```

**Ili jednostavno**: `Ctrl+C` u terminalu gde radi backend

### 2?? Kreiraj tabele (ako ve? nisu)

```powershell
# Analytics baza
psql -d analytics_db -f Database/Analytics/001_CreateSalesFactTables.sql
psql -d analytics_db -f Database/Analytics/002_AddVelicinaBojaToProductsDim.sql

# Write baza
psql -d trendplus -f Database/Migrations/003_AddVelicinaBojaToArtikli.sql
```

### 3?? Dodaj test podatke (sa kreiran artikala)

```powershell
psql -d trendplus -f Database/Migrations/005_CreateArtikliAndTestData.sql
```

**Ova skripta ?e**:
- ? Kreirati 15 sample artikala (ako ih nemate)
- ? Dodati veli?ine i boje
- ? Kreirati 5 test prodaja
- ? Kreirati outbox events

### 4?? Restart backend SA NOVIM KODOM

```powershell
cd Trendplus2
dotnet clean
dotnet build
dotnet run
```

**VAŽNO**: Mora `dotnet build` da bi u?itao nove izmene!

### 5?? Sa?ekaj workers

```
? 60 sekundi - SyncWorker
? 30 sekundi - OutboxProcessor
??????????????????????????????
   90 sekundi ukupno
```

### 6?? Proveri browser

```
1. Idi na: http://localhost:8080/analytics
2. Hard refresh: Ctrl + Shift + R
3. Proveri da nema više 500 greške
```

---

## ?? Provera da li je sve OK:

### Backend logs:

Trebao bi da vidiš:
```
[Info] GetTopProducts query: Top=10
[Info] Sales count after filters: 5
[Info] Top products: X by revenue, Y by units
```

### Database check:

```sql
-- Analytics DB
SELECT COUNT(*) FROM "SalesFacts";  -- Should be 5
SELECT COUNT(*) FROM "SalesLineFacts";  -- Should be ~25
SELECT COUNT(*) FROM "ProductsDim" WHERE "Velicina" IS NOT NULL;  -- Should be 10

-- Write DB
SELECT "IsProcessed", COUNT(*) 
FROM "OutboxMessages" 
WHERE "Payload"::jsonb->>'BrojRacuna' LIKE 'DEMO-%'
GROUP BY "IsProcessed";
-- Should show: IsProcessed=true, COUNT=5
```

---

## ? Još uvek ne radi?

### Problem: Backend nije u?itao nove izmene

**Simptom**: I dalje 500 greška nakon restarta

**Rešenje**:
```powershell
cd Trendplus2
dotnet clean
dotnet build --no-incremental
dotnet run
```

### Problem: Tabele ne postoje

**Simptom**: "relation SalesFacts does not exist"

**Rešenje**: Pokreni SQL skripte (korak 2 iznad)

### Problem: Nema podataka

**Simptom**: Dashboard prazan ali nema greške

**Rešenje**:
```powershell
# Proveri outbox
psql -d trendplus -c "SELECT * FROM \"OutboxMessages\" WHERE \"Payload\"::jsonb->>'BrojRacuna' LIKE 'DEMO-%';"

# Ako nije processed, sa?ekaj još 30s
```

---

## ?? Povezani fajlovi:

- `fix-analytics.ps1` - Automatski script
- `TROUBLESHOOTING_500_ERROR.md` - Detaljno troubleshooting
- `QUICKSTART.md` - Quick start guide
- `TEST_DATA_README.md` - Test data dokumentacija

---

## ?? Hitna pomo?:

Ako ništa ne radi, proveri:

1. **Backend logs** - Da li je backend uopšte pokrenut?
2. **Database connection** - Da li backend može da se poveže na obe baze?
3. **Browser console** (F12) - Koja je ta?na greška?

---

**TL;DR**: **Restart backend** (`Ctrl+C` pa `dotnet run`)! To je 99% rešenje problema! ??
