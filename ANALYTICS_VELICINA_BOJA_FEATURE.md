# Analytics - Dodavanje Veli?ina i Boja polja

## ?? Pregled izmena

Dodati su **Veli?ina** i **Boja** atributi cipela u Analytics sistem, koji se prikazuju u Top Products tabelama.

## ??? Database Migracije

### 1. Write Database (Trendplus - glavna baza)

Pokrenite SQL skriptu:
```bash
psql -d trendplus_db -f Database/Migrations/003_AddVelicinaBojaToArtikli.sql
```

**Napomena**: Tabela `"Artikli"` koristi **PascalCase** sa navodnicima u PostgreSQL-u.

Ova skripta:
- Dodaje `"Velicina" VARCHAR(50)` kolonu u `"Artikli"` tabelu
- Dodaje `"Boja" VARCHAR(100)` kolonu u `"Artikli"` tabelu
- Kreira indekse za performanse (`"IX_Artikli_Velicina"`, `"IX_Artikli_Boja"`)

### 2. Analytics Database (Read model baza)

Pokrenite SQL skriptu:
```bash
psql -d analytics_db -f Database/Analytics/002_AddVelicinaBojaToProductsDim.sql
```

Ova skripta:
- Dodaje `"Velicina" VARCHAR(50)` kolonu u `"ProductsDim"` tabelu
- Dodaje `"Boja" VARCHAR(100)` kolonu u `"ProductsDim"` tabelu
- Kreira indekse za performanse

## ?? Backend izmene

### Domain modeli:

1. **`Domain/Model/Artikli.cs`** - Write model
   ```csharp
   public string? Velicina { get; set; }   // Veli?ina cipela
   public string? Boja { get; set; }       // Boja cipela
   ```

2. **`Domain/Model/ProductsDim.cs`** - Analytics read model
   ```csharp
   public string? Velicina { get; set; }   // Veli?ina cipela
   public string? Boja { get; set; }       // Boja cipela
   ```

### Workers:

**`Workers/SyncWorker.cs`**
```csharp
private static void MapToDim(Domain.Model.Artikli p, ProductsDim dim)
{
    // ...existing mappings...
    dim.Velicina = p.Velicina;  // Novo
    dim.Boja = p.Boja;          // Novo
    // ...existing mappings...
}
```

### Queries:

**`Application/Analytics/Queries/GetTopProducts/GetTopProductsQuery.cs`**
```csharp
public record TopProductDto(
    int ProductId,
    string ProductName,
    decimal TotalRevenue,
    int TotalUnits,
    string? Velicina = null,  // Novo
    string? Boja = null        // Novo
);
```

**`Application/Analytics/Queries/GetTopProducts/GetTopProductsHandler.cs`**
- GroupBy sada grupira po `(ProductId, ProductName, Velicina, Boja)`
- Razli?ite varijante istog proizvoda (npr. razli?ite veli?ine) se prikazuju odvojeno

## ?? Frontend izmene

### TypeScript Types:

**`Klijent/clientapp/src/types/analytics.ts`**
```typescript
export interface TopProduct {
  productId: number;
  productName: string;
  totalRevenue: number;
  totalUnits: number;
  velicina?: string | null;  // Novo
  boja?: string | null;      // Novo
}
```

### UI:

**`Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`**

Top Products tabele sada imaju kolone:
| Artikal | **Veli?ina** | **Boja** | Promet | Kom |
|---------|--------------|----------|--------|-----|

## ?? Kako radi

### Data Flow:

```
???????????????????????
?  Write DB           ?
?  "Artikli"          ?
?  - "Velicina"       ? ? PascalCase sa navodnicima
?  - "Boja"           ?
???????????????????????
           ?
           ? SyncWorker (svakih 60s)
           ? MapToDim() metoda
           ?
???????????????????????
?  Analytics DB       ?
?  "ProductsDim"      ?
?  - "Velicina"       ?
?  - "Boja"           ?
???????????????????????
           ?
           ? GetTopProductsQuery
           ? GROUP BY (ProductId, ProductName, Velicina, Boja)
           ?
???????????????????????
?   Frontend          ?
?   Analytics         ?
?   Dashboard         ?
?   - Tabele sa       ?
?     Veli?ina i Boja ?
???????????????????????
```

### Primeri prikaza:

**Top proizvodi po prometu:**
| Artikal          | Veli?ina | Boja   | Promet      | Kom |
|------------------|----------|--------|-------------|-----|
| Nike Air Max     | 42       | Crna   | 15,000 RSD  | 3   |
| Nike Air Max     | 43       | Bela   | 12,500 RSD  | 2   |
| Nike Air Max     | 42       | Bela   | 10,000 RSD  | 2   |
| Adidas Superstar | 41       | Braon  | 8,500 RSD   | 1   |

> **Napomena**: Isti proizvod razli?itih veli?ina/boja se prikazuje kao **zasebne stavke**.

## ?? Deployment

### 1. Pokrenite SQL skripte (obe baze)
```bash
# Write database (VAŽNO: koristi "Artikli" sa navodnicima)
psql -d trendplus_db -f Database/Migrations/003_AddVelicinaBojaToArtikli.sql

# Analytics database
psql -d analytics_db -f Database/Analytics/002_AddVelicinaBojaToProductsDim.sql
```

### 2. Restart Backend
```bash
dotnet run --project Trendplus2
```

SyncWorker ?e automatski zapo?eti sinhronizaciju i mapirati nova polja.

### 3. Rebuild Frontend
```bash
cd Klijent/clientapp
npm run build
```

## ?? Testiranje

### 1. Dodaj test podatke

**SQL direktno u write bazu**:
```sql
-- Koristi "Artikli" sa velikim A i navodnicima!
UPDATE "Artikli" 
SET "Velicina" = '42', "Boja" = 'Crna' 
WHERE "Id" = 1;

UPDATE "Artikli" 
SET "Velicina" = '43', "Boja" = 'Bela' 
WHERE "Id" = 2;

UPDATE "Artikli" 
SET "Velicina" = '42', "Boja" = 'Bela' 
WHERE "Id" = 3;
```

### 2. Sa?ekaj sync (60 sekundi)

SyncWorker ?e automatski preneti podatke u analytics bazu.

### 3. Proveri analytics bazu:
```sql
SELECT "ProductName", "Velicina", "Boja" 
FROM "ProductsDim" 
WHERE "Velicina" IS NOT NULL 
LIMIT 10;
```

### 4. Proveri frontend

Idi na `http://localhost:8080/analytics` i pogledaj **Top Products** tabele - trebao bi da vidiš kolone za Veli?inu i Boju!

## ?? Važne napomene

### PostgreSQL Case Sensitivity

Ova baza koristi **PascalCase** sa **navodnicima** za imena tabela:
- ? `"Artikli"` - Ispravno
- ? `artikli` - Greška: "relation does not exist"
- ? `Artikli` (bez navodnika) - PostgreSQL konvertuje u lowercase

### Naming Convention

Razli?ite tabele koriste razli?ite konvencije:
- **PascalCase** (sa navodnicima): `"Artikli"`, `"Dobavljaci"`, `"TipoviObuce"`, `"Sezone"`
- **snake_case**: `prodaja_zaglavlje`, `prodaja_stavke`, `povracaj_zaglavlje`, `povracaj_stavke`

### Nullable polja

`Velicina` i `Boja` su **opciona** polja:
- Ako su `NULL`, frontend prikazuje `-`
- Grupiranje u Top Products radi i sa `NULL` vrednostima
- Indeksi rade sa `NULL` vrednostima

### Performance

- Dodati indeksi: `"IX_Artikli_Velicina"` i `"IX_Artikli_Boja"`
- Omogu?avaju brzo filtriranje po veli?ini/boji
- SyncWorker mapira samo izmenjene artikle (incremental sync)

## ?? Povezani fajlovi

### Database:
- `Database/Migrations/003_AddVelicinaBojaToArtikli.sql` ? **PascalCase sa navodnicima**
- `Database/Analytics/002_AddVelicinaBojaToProductsDim.sql`

### Backend:
- `Domain/Model/Artikli.cs`
- `Domain/Model/ProductsDim.cs`
- `Workers/SyncWorker.cs` - `MapToDim()` metoda
- `Application/Analytics/Queries/GetTopProducts/GetTopProductsQuery.cs`
- `Application/Analytics/Queries/GetTopProducts/GetTopProductsHandler.cs`

### Frontend:
- `Klijent/clientapp/src/types/analytics.ts`
- `Klijent/clientapp/src/pages/AnalyticsDashboard.tsx`

## ?? Troubleshooting

### Greška: "relation 'artikli' does not exist"

**Uzrok**: SQL skripta koristi lowercase `artikli` umesto `"Artikli"` sa navodnicima.

**Rešenje**: Koristite ažuriranu skriptu koja koristi `"Artikli"` (PascalCase sa navodnicima).

### Podaci se ne pojavljuju u analytics

**Provera**:
1. Da li su kolone dodate u **obe** baze?
2. Da li je SyncWorker aktivan? (proveri backend logs)
3. Da li su artikli ažurirani nakon dodavanja kolona? (SyncWorker ?ita samo izmenjene artikle)

**Rešenje**: Ažuriraj `UpdatedAt` timestamp:
```sql
UPDATE "Artikli" 
SET "UpdatedAt" = NOW() 
WHERE "Velicina" IS NOT NULL OR "Boja" IS NOT NULL;
```

### Frontend ne prikazuje kolone

**Provera**: Da li je frontend rebuild-ovan nakon izmena?

**Rešenje**:
```bash
cd Klijent/clientapp
npm run build
