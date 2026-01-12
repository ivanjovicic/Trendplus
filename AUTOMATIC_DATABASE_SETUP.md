# ?? Automatic Database Setup

Backend sada **automatski** izvršava sve SQL migracije pri pokretanju!

## ? Šta se automatski dešava:

### 1. **Trendplus Database (Write DB)**
- Izvršava EF Core migracije
- Ako nema Artikala, izvršava: `005_CreateArtikliAndTestData.sql`
  - Kreira 15 test artikala
  - Dodaje Velicina/Boja kolone
  - Kreira 5 DEMO prodaja
  - Kreira OutboxMessages

### 2. **Analytics Database (Read DB)**
- Izvršava EF Core migracije
- Ako nema SalesFacts tabelu, izvršava: `001_CreateSalesFactTables.sql`
- Ako nema ProductsDim tabelu, izvršava: `002_AddVelicinaBojaToProductsDim.sql`

## ?? Kako koristiti:

### **Lokalna PostgreSQL instalacija:**

```powershell
# 1. Kreiraj database-e (ako ih nemaš)
createdb trendplus
createdb analytics_db

# 2. Ažuriraj appsettings.json
# "ConnectionStrings": {
#   "DefaultConnection": "Host=localhost;Database=trendplus;Username=postgres;Password=yourpass",
#   "AnalyticsConnection": "Host=localhost;Database=analytics_db;Username=postgres;Password=yourpass"
# }

# 3. Pokreni backend - SVE SE DEŠAVA AUTOMATSKI!
cd Trendplus2
dotnet run
```

### **Neon (Cloud PostgreSQL):**

```powershell
# 1. Kreiraj database-e u Neon Console (trendplus i analytics_db)

# 2. Ažuriraj appsettings.json sa Neon connection string-ovima
# "ConnectionStrings": {
#   "DefaultConnection": "postgresql://user:pass@ep-xxx.neon.tech/trendplus?sslmode=require",
#   "AnalyticsConnection": "postgresql://user:pass@ep-xxx.neon.tech/analytics_db?sslmode=require"
# }

# 3. Pokreni backend - SVE SE DEŠAVA AUTOMATSKI!
cd Trendplus2
dotnet run
```

## ?? Provera da li radi:

Kada pokreneš `dotnet run`, trebao bi da vidiš logove:

```
=== DATABASE INITIALIZATION START ===
Initializing Trendplus database...
? Trendplus DB migrations applied
No Artikli found, running seed script...
? Executed SQL file: Database/Migrations/005_CreateArtikliAndTestData.sql
Initializing Analytics database...
? Analytics DB migrations applied
SalesFacts table not found, creating...
? Executed SQL file: Database/Analytics/001_CreateSalesFactTables.sql
ProductsDim table not found, creating...
? Executed SQL file: Database/Analytics/002_AddVelicinaBojaToProductsDim.sql
? Analytics DB initialized
=== DATABASE INITIALIZATION COMPLETE ===
```

## ?? Troubleshooting:

### Problem: "Database does not exist"
**Rešenje:** Kreiraj database-e ru?no:
```bash
# Lokalno
createdb trendplus
createdb analytics_db

# Neon
# Idi na https://console.neon.tech ? Databases ? New Database
```

### Problem: "Connection failed"
**Rešenje:** Proveri connection string-ove u `appsettings.json`

### Problem: "SQL file not found"
**Rešenje:** SQL fajlovi moraju biti kopirani u output folder.
Proverite da li su ozna?eni kao **Copy to Output Directory** u projektu.

## ?? Benefiti:

? **Nema ru?nog pokretanja SQL skripti**
? **Radi i lokalno i na Neon-u**
? **Idempotentno** - sigurno je pokrenuti više puta
? **Workers automatski procesiraju podatke**
? **Analytics dashboard radi odmah**

## ?? Alternativne metode (ako automatsko ne radi):

Ako iz nekog razloga automatska inicijalizacija ne radi, možeš koristiti:

1. **PowerShell skripte** (lokalno):
   - `.\run-sql-simple.ps1` - Brzo izvršavanje
   - `.\setup-manual.ps1` - Sa validacijom
   - `.\fix-analytics.ps1` - Kompletna setup

2. **Neon copy-paste** (cloud):
   - `.\neon-complete-setup.ps1` - Kopira SVE SQL u clipboard
   - Ctrl+V u Neon SQL Editor

---

**Autor:** GitHub Copilot  
**Verzija:** 1.0  
**Datum:** 2026-01-11
