# ?? QUICK START - Test Data za Analytics

## ? Najbrži na?in (3 komande):

```bash
# 1. Dodaj kolone (SAMO JEDNOM)
psql -d trendplus_db -f Database/Migrations/003_AddVelicinaBojaToArtikli.sql
psql -d analytics_db -f Database/Analytics/002_AddVelicinaBojaToProductsDim.sql

# 2. Dodaj test podatke (artikli + prodaje) - BEZ stored procedure!
psql -d trendplus_db -f Database/Migrations/004_SimpleTestData.sql

# 3. Sa?ekaj 90 sekundi, pa proveri:
# http://localhost:8080/analytics
```

---

## ?? VAŽNO - Izbor skripte:

### ? **004_SimpleTestData.sql** ? KORISTITE OVU!
- ? NE zahteva stored procedure
- ? Radi direktan INSERT u tabele
- ? Kreira 10 artikala + 5 prodaja
- ? Automatski kreira Outbox events

### ? **004_AddTestDataForAnalytics.sql** ? NE KORISTITE!
- ? Zahteva `sp_prodaj_artikle_json` stored procedure
- ? Stored procedure ne postoji u bazi
- ? Dobi?e ?ete grešku: "function does not exist"

---

## ?? Šta ?e se desiti:

### ?? Timeline:

```
0s    ? Pokrenuta skripta 004_SimpleTestData.sql
      ? ? 10 artikala ažurirano (Veli?ina + Boja)
      ? ? 5 prodaja kreirano (DEMO-001 do DEMO-005)
      ? ? Outbox eventi kreirani
      ?
60s   ? ?? SyncWorker - Artikli ? Analytics baza
      ? ? ProductsDim ažuriran
      ?
90s   ? ?? OutboxProcessor - Prodaje ? SalesFacts
      ? ? SalesFacts kreiran
      ?
      ? ?? GOTOVO! Proveri /analytics
```

---

## ?? O?ekivani rezultat na `/analytics`:

### ? Sales Summary
```
Ukupan promet:     165,000.00 RSD
Transakcije:       5
Prodate jedinice:  25
Prose?na korpa:    33,000.00 RSD
```

### ? Top proizvodi po prometu

| Artikal | **Veli?ina** | **Boja** | Promet | Kom |
|---------|--------------|----------|--------|-----|
| Nike... | 42 | Crna | 25,000 | 5 |
| Adidas... | 45 | Crna | 16,000 | 2 |

### ? Inventory Status
```
SKU count:      10
Total on hand:  (vaše trenutne koli?ine)
Low stock:      (artikli sa koli?inom < 2)
```

---

## ?? Quick verify:

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

---

## ?? Ako nešto ne radi:

**Greška: "function sp_prodaj_artikle_json does not exist"**
? Pogrešno ste pokrenuli `004_AddTestDataForAnalytics.sql`
? Koristite `004_SimpleTestData.sql` umesto toga!

**Artikli se ne vide u Analytics?**
? Sa?ekaj još 30s (SyncWorker interval = 60s)

**Prodaje se ne vide?**
? Proveri backend logs za OutboxProcessor

**Frontend prazan?**
? Hard refresh: `Ctrl + Shift + R`

---

## ?? Kompletna dokumentacija:

- `TEST_DATA_README.md` - Detaljno uputstvo
- `ANALYTICS_VELICINA_BOJA_FEATURE.md` - Feature dokumentacija
