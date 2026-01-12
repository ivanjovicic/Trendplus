# Test Data Scripts - Analytics Dashboard

Kreirane su 3 SQL skripte za dodavanje test podataka u sistem.

## ?? Dostupne skripte

### 1?? **004_QuickUpdate_VelicinaBoja.sql** ? (NAJBRŽE)

**Namena**: Samo ažurira postoje?e artikle sa veli?inama i bojama

**Kada koristiti**: 
- Ve? imate artikle u bazi
- Želite brzo da testirate Analytics sa postoje?im podacima
- NE kreira nove prodaje

**Pokretanje**:
```bash
psql -d trendplus_db -f Database/Migrations/004_QuickUpdate_VelicinaBoja.sql
```

**Šta radi**:
- Ažurira prvih 15 artikala sa:
  - Veli?inama: 40, 41, 42, 43, 44, 45
  - Bojama: Crna, Bela, Braon, Siva, Plava, Crvena
- Postavlja `UpdatedAt = NOW()` da triggeruje SyncWorker

**Rezultat**:
- Ažurirano 15 artikala
- Sa?ekaj 60s ? SyncWorker ?e ih sinhronizovati u Analytics bazu
- Kreira prodaju preko frontend-a `/prodaja`
- Sa?ekaj 30s ? OutboxProcessor ?e kreirati SalesFacts
- Proveri `/analytics` dashboard

---

### 2?? **004_SimpleTestData.sql** ?? (PREPORU?ENO)

**Namena**: Kompletna skripta - artikli + prodaje + outbox events

**Kada koristiti**:
- Želite kompletan test scenario
- Trebate i artikle i prodaje odjednom
- Želite da vidite Analytics dashboard sa podacima odmah

**Pokretanje**:
```bash
psql -d trendplus_db -f Database/Migrations/004_SimpleTestData.sql
```

**Šta radi**:
1. ? Ažurira 10 artikala sa veli?inama/bojama
2. ? Kreira 5 test prodaja (`DEMO-001` do `DEMO-005`)
3. ? Kreira Outbox events za svaku prodaju
4. ? Prikazuje summary svih kreiranih podataka

**Rezultat**:
- 10 artikala sa veli?inama/bojama
- 5 prodaja sa razli?itim artiklima
- Outbox eventi spremni za procesiranje
- Detaljne RAISE NOTICE poruke u output-u

**O?ekivani promet**: ~160,000 - 180,000 RSD

---

### 3?? **004_AddTestDataForAnalytics.sql** ?? (ADVANCED)

**Namena**: Napredna skripta sa više opcija i stored procedure pozivima

**Kada koristiti**:
- Imate `sp_prodaj_artikle_json` stored procedure
- Želite više kontrole nad test podacima
- Trebate custom test scenarios

**Pokretanje**:
```bash
psql -d trendplus_db -f Database/Migrations/004_AddTestDataForAnalytics.sql
```

**Šta radi**:
- Sve kao SimpleTestData
- Dodatno: Poziva stored procedure (ako postoji)
- Dodatno: Kreira više test prodaja (7 prodaja)
- Dodatno: Detaljnije verify sekcije

---

## ?? Quick Start - Preporu?eni workflow

### Scenario 1: Brzo testiranje (ve? imate artikle)

```bash
# Korak 1: Dodaj Velicina/Boja kolone
psql -d trendplus_db -f Database/Migrations/003_AddVelicinaBojaToArtikli.sql
psql -d analytics_db -f Database/Analytics/002_AddVelicinaBojaToProductsDim.sql

# Korak 2: Ažuriraj postoje?e artikle
psql -d trendplus_db -f Database/Migrations/004_QuickUpdate_VelicinaBoja.sql

# Korak 3: Sa?ekaj 60s za sync
# (SyncWorker radi svakih 60s)

# Korak 4: Kreira prodaju preko frontend-a
# http://localhost:8080/prodaja

# Korak 5: Sa?ekaj 30s za outbox processing

# Korak 6: Proveri Analytics
# http://localhost:8080/analytics
```

### Scenario 2: Kompletan test (sve odjednom)

```bash
# Korak 1: Dodaj Velicina/Boja kolone (ako ve? nisu dodate)
psql -d trendplus_db -f Database/Migrations/003_AddVelicinaBojaToArtikli.sql
psql -d analytics_db -f Database/Analytics/002_AddVelicinaBojaToProductsDim.sql

# Korak 2: Kreiraj sve test podatke
psql -d trendplus_db -f Database/Migrations/004_SimpleTestData.sql

# Korak 3: Sa?ekaj 60s za sync artikala

# Korak 4: Sa?ekaj 30s za outbox processing prodaja

# Korak 5: Proveri Analytics
# http://localhost:8080/analytics
```

---

## ?? Šta o?ekivati na Analytics Dashboard-u

Nakon što pokrenete skripte i sa?ekate sync:

### Sales Summary
| Metrika | Vrednost |
|---------|----------|
| Ukupan promet | ~165,000 RSD |
| Transakcije | 5 |
| Prodate jedinice | 25 |
| Prose?na korpa | ~33,000 RSD |

### Top proizvodi po prometu
| Artikal | Veli?ina | Boja | Promet | Kom |
|---------|----------|------|--------|-----|
| Artikal 1 | 42 | Crna | 25,000 RSD | 5 |
| Artikal 9 | 45 | Crna | 16,000 RSD | 2 |
| ...

### Inventory Status
- Total SKU: 10-15 (zavisno od skripte)
- Total on hand: Trenutne koli?ine
- Low stock: Artikli sa koli?inom < 2

---

## ?? Timings

| Worker | Interval | Šta radi |
|--------|----------|----------|
| **SyncWorker** | 60s | Sinhronizuje artikle iz write ? analytics baze |
| **OutboxProcessor** | 30s | Procesira outbox events ? kreira SalesFacts |

**Preporu?eni workflow**:
1. Pokreni skriptu
2. ? Sa?ekaj 60s (sync artikala)
3. ? Sa?ekaj još 30s (procesiranje prodaja)
4. ?? Osveži `/analytics` u browser-u

---

## ?? Verify podatke

### Proveri artikle u write bazi:
```sql
SELECT "Id", "Naziv", "Velicina", "Boja", "ProdajnaCena"
FROM "Artikli"
WHERE "Velicina" IS NOT NULL
ORDER BY "Id";
```

### Proveri artikle u analytics bazi:
```sql
SELECT "ProductId", "ProductName", "Velicina", "Boja"
FROM "ProductsDim"
WHERE "Velicina" IS NOT NULL
ORDER BY "ProductId";
```

### Proveri prodaje:
```sql
SELECT 
    pz.broj_racuna,
    pz.datum_prodaje,
    COUNT(ps.id) as stavki,
    SUM(ps.kolicina * ps.cena) as iznos
FROM prodaja_zaglavlje pz
LEFT JOIN prodaja_stavke ps ON pz.id = ps.id_prodaja
WHERE pz.broj_racuna LIKE 'DEMO-%'
GROUP BY pz.id, pz.broj_racuna, pz.datum_prodaje
ORDER BY pz.datum_prodaje DESC;
```

### Proveri SalesFacts (analytics baza):
```sql
SELECT 
    "SaleId",
    "SaleTimestampUtc",
    "TotalAmount",
    "TotalUnits"
FROM "SalesFacts"
ORDER BY "SaleTimestampUtc" DESC
LIMIT 10;
```

### Proveri Outbox:
```sql
SELECT 
    "EventType",
    "Payload"::jsonb->>'BrojRacuna' as racun,
    "IsProcessed",
    "ProcessedAt",
    "RetryCount"
FROM "OutboxMessages"
WHERE "Payload"::jsonb->>'BrojRacuna' LIKE 'DEMO-%'
ORDER BY "CreatedAt" DESC;
```

---

## ?? Troubleshooting

### Problem: Artikli se ne pojavljuju u Analytics

**Uzrok**: SyncWorker nije sinhronizovao artikle

**Provera**:
```sql
-- Analytics baza
SELECT COUNT(*) FROM "ProductsDim" WHERE "Velicina" IS NOT NULL;
```

**Rešenje**:
1. Proveri backend logs za SyncWorker
2. Sa?ekaj 60s (interval sync-a)
3. Ru?no triggeru sync restartovanjem backend-a

### Problem: Prodaje nisu u Analytics

**Uzrok**: OutboxProcessor nije procesirao events

**Provera**:
```sql
-- Write baza
SELECT * FROM "OutboxMessages" 
WHERE "IsProcessed" = false 
AND "Payload"::jsonb->>'BrojRacuna' LIKE 'DEMO-%';
```

**Rešenje**:
1. Sa?ekaj 30s (interval processing-a)
2. Proveri backend logs za OutboxProcessor
3. Proveri da li ima grešaka u `"ErrorMessage"` koloni

### Problem: Frontend ne prikazuje podatke

**Uzrok**: Frontend cache ili API greška

**Rešenje**:
1. Hard refresh: `Ctrl + Shift + R`
2. Proveri browser console za greške
3. Proveri Network tab - da li API vra?a podatke
4. Proveri `/api/analytics/health` endpoint

---

## ?? Napomene

- Sve skripte su **idempotentne** - mogu se pokrenuti više puta
- Test prodaje koriste prefiks `DEMO-` ili `TEST-` za lako prepoznavanje
- Skripte koriste `DO $$` blokove za error handling
- `RAISE NOTICE` poruke prikazuju progress i rezultate

---

## ?? Povezani fajlovi

- `003_AddVelicinaBojaToArtikli.sql` - Write DB migration
- `002_AddVelicinaBojaToProductsDim.sql` - Analytics DB migration
- `ANALYTICS_VELICINA_BOJA_FEATURE.md` - Kompletna dokumentacija
