# ?? Hibridni Model - Prodaja u DnevnikPromena

## ?? Šta je ura?eno

Implementiran je **hibridni model** za pra?enje svih akcija u sistemu:

### 1?? **Operativne tabele** (detaljni podaci)
- `prodaja_zaglavlje` - zaglavlje prodaje
- `prodaja_stavke` - stavke prodaje (artikli, koli?ine, cene)

### 2?? **Audit log tabela** (centralni pregled)
- `DnevnikPromena` - hronološki prikaz svih akcija:
  - ? **Prodaja** (novo!)
  - ? **Nivelacija**
  - ?? Unos robe (može se dodati kasnije)

---

## ?? Implementacija

### **1. Automatsko logovanje prodaje**
U `ProdajaRepository.cs` dodato je:
```csharp
// After successful sale, log to DnevnikPromena
INSERT INTO "DnevnikPromena" 
    ("TipPromene", "Datum", "Iznos", "BrojRacuna", "Komentar")
SELECT 
    'Prodaja',
    CURRENT_TIMESTAMP,
    SUM(kolicina * cena),
    broj_racuna,
    'Prodaja - ' || broj_racuna || ' (' || nacin_placanja || ')'
FROM prodaja_zaglavlje + prodaja_stavke
```

### **2. Migracija postoje?ih podataka**
Kreirana je migracija: `20260110134237_MigrateSalesToDnevnikPromena.cs`
- Migrira **SVE postoje?e prodaje** u `DnevnikPromena`
- Spre?ava duplikate (proverava `BrojRacuna` + `Datum`)
- **Rollback mogu?**: `Down()` briše migrirane podatke

---

## ?? Kako primeniti promene

### **Korak 1: Primeni migraciju**
```bash
dotnet ef database update --project Infrastructure --startup-project Trendplus2 --context TrendplusDbContext
```

### **Korak 2: Restartuj backend**
```bash
# Zaustavi trenutnu aplikaciju (Ctrl+C)
dotnet run --project Trendplus2/Api.csproj
```

### **Korak 3: Testiraj**
1. Otvori frontend: `http://localhost:5173`
2. Idi na **"?? Dnevnik promena"**
3. Trebalo bi da vidiš:
   - Sve postoje?e prodaje
   - Sve nivelacije
   - Hronološki sortirane po datumu

---

## ? Prednosti hibridnog modela

| Aspekt | Prednost |
|--------|----------|
| **Pregled** | Jedna tabela za sve akcije |
| **Performanse** | Detaljni podaci u specijalizovanim tabelama |
| **Reporting** | Brzi izveštaji iz `DnevnikPromena` |
| **Analitika** | Duboke analize iz `prodaja_zaglavlje` |
| **Skalabilnost** | Lako dodavanje novih tipova (unos robe, korekcije) |

---

## ?? Slede?i koraci (opciono)

### **1. Dodaj unos robe u DnevnikPromena**
```sql
INSERT INTO "DnevnikPromena" 
    ("TipPromene", "Datum", "Iznos", "BrojRacuna", "DobavljacId", "Komentar")
VALUES 
    ('Unos robe', NOW(), ukupan_iznos, broj_racuna, dobavljac_id, komentar);
```

### **2. Dodaj storniranje prodaje**
```sql
INSERT INTO "DnevnikPromena" 
    ("TipPromene", "Datum", "Iznos", "BrojRacuna", "Komentar")
VALUES 
    ('Storno prodaje', NOW(), -iznos, broj_racuna, 'Stornirano: razlog');
```

### **3. Dodaj korekciju zaliha**
```sql
INSERT INTO "DnevnikPromena" 
    ("TipPromene", "Datum", "ArtikalId", "Komentar")
VALUES 
    ('Korekcija zaliha', NOW(), artikal_id, '+/-kolicina kom');
```

---

## ?? Struktura DnevnikPromena

| Kolona | Tip | Opis |
|--------|-----|------|
| `Id` | int | PK |
| `TipPromene` | string | "Prodaja", "Nivelacija", "Unos robe" |
| `Datum` | DateTime | Kada se desilo |
| `Iznos` | decimal | Ukupan iznos transakcije |
| `BrojRacuna` | string | Referenca na ra?un |
| `ArtikalId` | int? | Referenca na artikal (za nivelacije) |
| `DobavljacId` | int? | Referenca na dobavlja?a |
| `StaraProdajnaCena` | decimal? | Samo za nivelacije |
| `NovaProdajnaCena` | decimal? | Samo za nivelacije |
| `Komentar` | string | Opis akcije |
| `KorisnikIme` | string | Ko je uradio akciju |

---

## ?? Troubleshooting

### **Problem: Migracija ne radi**
```bash
# Proveri status migracija
dotnet ef migrations list --project Infrastructure --startup-project Trendplus2 --context TrendplusDbContext

# Rollback ako treba
dotnet ef database update <PreviousMigrationName> --project Infrastructure --startup-project Trendplus2 --context TrendplusDbContext
```

### **Problem: Duplirani podaci**
```sql
-- Proveri duplikate
SELECT "BrojRacuna", "Datum", COUNT(*)
FROM "DnevnikPromena"
WHERE "TipPromene" = 'Prodaja'
GROUP BY "BrojRacuna", "Datum"
HAVING COUNT(*) > 1;

-- Obriši duplikate (drži najnoviji)
DELETE FROM "DnevnikPromena" a
USING "DnevnikPromena" b
WHERE a."Id" < b."Id"
  AND a."TipPromene" = 'Prodaja'
  AND a."BrojRacuna" = b."BrojRacuna"
  AND a."Datum" = b."Datum";
```

---

## ?? Zaklju?ak

? **Implementirano:**
- Automatsko logovanje prodaje u `DnevnikPromena`
- Migracija postoje?ih podataka
- Hibridni model (operativne + audit tabele)

?? **Rezultat:**
- Stranica "Dnevnik promena" sada prikazuje **SVE akcije**
- Lako pra?enje istorije
- Brzi izveštaji
- Skalabilno rešenje

?? **Slede?i korak:** Restartuj backend i testiraj!
