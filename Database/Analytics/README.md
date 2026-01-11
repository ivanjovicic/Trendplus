# Analytics Setup

## Problem
Analytics dashboard pokazuje greške prilikom u?itavanja podataka jer tabele `SalesFacts` i `SalesLineFacts` ne postoje u analytics bazi.

## Rešenje

### Opcija 1: Automatski (koristi EF migrations)

1. Instaliraj `dotnet-ef` tool globalno:
```bash
dotnet tool install --global dotnet-ef
```

2. Primeni migraciju na analytics bazu:
```bash
dotnet ef database update --context AnalyticsDbContext --project Infrastructure/Infrastructure.csproj --startup-project Trendplus2/Api.csproj --connection "YourAnalyticsConnectionString"
```

### Opcija 2: Ru?no (preporu?eno za brzo testiranje)

Pokreni SQL skriptu iz `Database/Analytics/001_CreateSalesFactTables.sql` direktno na analytics PostgreSQL bazu.

**Koraci:**

1. Otvori pgAdmin ili psql klijent
2. Konektuj se na analytics bazu (connection string iz `appsettings.json` ? `AnalyticsConnection`)
3. Pokreni celu skriptu iz `Database/Analytics/001_CreateSalesFactTables.sql`

**Alternativno preko psql:**
```bash
psql -h localhost -U your_user -d analytics_db -f Database/Analytics/001_CreateSalesFactTables.sql
```

## Provera

Nakon kreiranja tabela, proveri da li postoje:

```sql
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
AND table_name IN ('SalesFacts', 'SalesLineFacts');
```

Trebalo bi da vidiš obe tabele.

## Punjenje podataka

Nakon što su tabele kreirane, podatke puni **OutboxProcessorWorker** automatski:

1. Kad se kreira prodaja ? upisuje se `ProdajaKreirana` event u Outbox
2. OutboxProcessorWorker (background service) svake 30s:
   - ?ita neprocesirane evente iz Outbox-a
   - projektuje `ProdajaKreirana` u `SalesFacts` i `SalesLineFacts`
   - ozna?ava event kao processed

**Proveri Outbox:**
- Idi na `/outbox` u aplikaciji
- Vidi da li postoje `ProdajaKreirana` eventi
- Proveri da li su processed (IsProcessed = true)

**Ru?no testiranje punjenja:**

Ako želiš da napuniš analytics iz postoje?ih prodaja u write bazi, možeš:

1. Kreirati novu prodaju kroz UI (`/prodaja`)
2. Sa?ekati 30s da OutboxProcessorWorker procesira
3. Proveriti `SalesFacts` tabelu:
```sql
SELECT * FROM "SalesFacts" ORDER BY "SaleTimestampUtc" DESC LIMIT 10;
```

## Connection String

Proveri da li je `AnalyticsConnection` pravilno postavljen u `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=trendplus_write;...",
    "AnalyticsConnection": "Host=...;Database=trendplus_analytics;..."
  }
}
```

Ako nemaš odvojenu analytics bazu, možeš privremeno koristiti istu kao i write bazu (ali to nije preporu?eno za produkciju).
