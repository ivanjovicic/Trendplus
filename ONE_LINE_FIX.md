# ?? ONE-LINE FIX

## Copy-Paste ovo u PowerShell:

```powershell
.\run-sql-auto.ps1; cd Trendplus2; dotnet run
```

To je sve! Script ?e:
1. ? Prona?i PostgreSQL automatski
2. ? Pokrenuti SQL skriptu
3. ? Restart backend
4. ? Sa?ekajte 90 sekundi
5. ?? Osvežite browser: `http://localhost:8080/analytics`

---

## ?? Ako ne radi:

### Problem: "Database 'trendplus' does not exist"

**Rešenje**: Promenite ime baze

1. Edit `run-sql-auto.ps1`
2. Promenite liniju: `$DB = "trendplus"` ? `$DB = "vase_ime_baze"`
3. Pokrenite ponovo

### Problem: "Cannot find psql.exe"

**Rešenje**: Script ?e vas pitati za path

Unesite punu putanju, npr:
```
C:\Program Files\PostgreSQL\16\bin\psql.exe
```

### Problem: "Permission denied"

**Rešenje**: Edit skriptu i dodaj `-U postgres`:

```powershell
& $psqlPath -U postgres -d $DB -f "Database/Migrations/005_CreateArtikliAndTestData.sql"
```

---

## ?? Šta ?e se kreirati:

- ? 15 sample artikala (ako ih nema)
- ? 10 artikala sa veli?inama i bojama
- ? 5 test prodaja (DEMO-001 do DEMO-005)
- ? Outbox eventi za Analytics

---

## ?? Expected Result:

Nakon 90 sekundi na `/analytics` vide?ete:

```
? Analytics baza: 5 prodaja, 25 stavki, 15 proizvoda

Sales Summary:
  Ukupan promet: 117,600.00 RSD
  Transakcije: 5
  
Top proizvodi:
  | Artikal | Veli?ina | Boja | Promet | Kom |
  |---------|----------|------|--------|-----|
  | Nike... | 42       | Crna | ...    | ... |
```

---

**TL;DR**: `.\run-sql-auto.ps1; cd Trendplus2; dotnet run` ??
