# ?? NEON DATABASE - Quick Start Guide

## ?? Tri Na?ina da Izvršite SQL na Neon-u

### **Opcija 1: Kompletna Setup (OBA Database-a) - PREPORU?ENO**

Ova opcija ?e kopirati SVE SQL upite sa instrukcijama za oba database-a.

1. **Kopiraj sve SQL upite:**
   ```powershell
   .\neon-complete-setup.ps1
   ```

2. **Otvori Neon Console:**
   - Idi na: https://console.neon.tech
   
3. **Prati instrukcije u komentarima:**
   - SQL koji si kopirao ima 3 sekcije (A, B, C)
   - Svaka sekcija kaže u koji database treba da odeš
   - Section A ? database: `trendplus`
   - Section B ? database: `analytics_db`
   - Section C ? database: `analytics_db`

---

### **Opcija 2: Posebno za svaki Database (Step-by-Step)**

#### **Korak 1: Setup Trendplus Database**

```powershell
.\neon-copy-sql.ps1
```

- Idi na Neon Console ? Select database: **trendplus**
- SQL Editor ? Paste ? Run

#### **Korak 2: Setup Analytics Database**

```powershell
.\neon-copy-analytics.ps1
```

- Idi na Neon Console ? Select database: **analytics_db**
- SQL Editor ? Paste ? Run

---

### **Opcija 3: Koristi psql sa Connection String-om**

1. **Dobavi Connection String-ove:**
   - Idi na: https://console.neon.tech
   - Selektuj svoj projekat
   - Klikni **Connection Details**
   - Kopiraj connection string-ove za oba database-a
   
   Primer:
   ```
   # Trendplus DB
   postgresql://alex:abc123@ep-cool-darkness-123456.us-east-2.aws.neon.tech/trendplus?sslmode=require
   
   # Analytics DB
   postgresql://alex:abc123@ep-cool-darkness-123456.us-east-2.aws.neon.tech/analytics_db?sslmode=require
   ```

2. **Edituj skriptu:**
   - Otvori `run-sql-neon.ps1`
   - Na?i liniju: `$NEON_CONNECTION_STRING = ""`
   - Nalepi svoj connection string izme?u navodnika

3. **Izvršite skriptu:**
   ```powershell
   .\run-sql-neon.ps1
   ```

---

## ??? Struktura Database-a

Trebaš **2 Neon database-a**:

| Database | Šta sadrži | Migracije |
|----------|-----------|-----------|
| **trendplus** | Write DB (Artikli, Prodaje, Outbox) | `Database/Migrations/005_*.sql` |
| **analytics_db** | Read DB (SalesFacts, ProductsDim) | `Database/Analytics/001_*.sql`, `002_*.sql` |

---

## ?? Šta SQL Skripte Rade

### **Trendplus DB (005_CreateArtikliAndTestData.sql)**
- ? Dodaje Velicina i Boja kolone u Artikli tabelu
- ? Kreira 15 test artikala sa veli?inama/bojama
- ? Kreira 5 DEMO prodaja
- ? Dodaje events u OutboxMessages za procesiranje

### **Analytics DB (001 + 002)**
- ? Kreira SalesFacts i SalesLineFacts tabele
- ? Kreira ProductsDim tabelu sa Velicina/Boja
- ? Dodaje indexe za brže upite

---

## ?? Konfiguracija Backend-a za Neon

Nakon što izvršiš SQL, ažuriraj `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "TrendplusDb": "postgresql://user:pass@ep-xxx.region.aws.neon.tech/trendplus?sslmode=require",
    "AnalyticsDb": "postgresql://user:pass@ep-xxx.region.aws.neon.tech/analytics_db?sslmode=require"
  }
}
```

**?? Tip:** Oba database-a mogu biti na istom Neon projektu, samo promeni ime baze na kraju.

---

## ?? Pokretanje Backend-a

```powershell
cd Trendplus2
dotnet run
```

?ekaj 90 sekundi da workers procesiraju podatke, zatim otvori:
http://localhost:8080/analytics

---

## ? Troubleshooting

### Problem: "psql not found"
**Rešenje:** Koristi **Opciju 1 ili 2** (copy-paste u Neon SQL Editor) - ne treba ti psql!

### Problem: "Connection failed"
**Rešenje:** 
- Proveri connection string
- Proveri da li database postoji u Neon
- Proveri whitelist IP adresa u Neon konzoli

### Problem: "Database does not exist"
**Rešenje:** Kreiraj database-e u Neon:
1. Idi na https://console.neon.tech
2. Selektuj projekat
3. **Databases** tab ? **New Database**
4. Kreiraj `trendplus` i `analytics_db`

### Problem: "500 Error na /api/analytics/sales/top-products"
**Rešenje:** 
- Proveri da li si pokrenuo **OBE migracije** (trendplus + analytics)
- Proveri da li postoje podaci: `SELECT COUNT(*) FROM "SalesFacts";`
- Proveri da li workers rade: ?ekaj 90 sekundi nakon pokretanja backend-a

---

## ?? Dodatne Skripte

| Skripta | Opis |
|---------|------|
| `neon-complete-setup.ps1` | **? PREPORU?ENO** - Sve SQL skripte sa instrukcijama |
| `neon-copy-sql.ps1` | Samo trendplus DB migracija |
| `neon-copy-analytics.ps1` | Samo analytics DB migracije |
| `run-sql-neon.ps1` | Izvršite SQL direktno sa psql (ako imaš instaliran) |
| `run-sql-simple.ps1` | Za lokalnu PostgreSQL instalaciju |
| `setup-manual.ps1` | Puna validacija i step-by-step setup (lokalno) |

---

## ?? Uspešno Završena Setup

### **Nakon Trendplus DB migracije:**
```
RAISE NOTICE: ? Created 15 Artikli with Velicina/Boja
RAISE NOTICE: ? Created 5 DEMO sales
RAISE NOTICE: ? Created 5 OutboxMessages
```

### **Nakon Analytics DB migracije:**
```
Table exists: SalesFacts
Table exists: SalesLineFacts
Table exists: ProductsDim
```

### **Nakon pokretanja backend-a (90s):**
- Otvori http://localhost:8080/analytics
- Dashboard prikazuje grafove sa prodajama
- Nema 500 grešaka! ??

