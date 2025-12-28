# ? Performance Tracking Feature

## ?? Opis

Dodata je funkcionalnost za tracking najdužih i najsporijih akcija u aplikaciji sa real-time dashboard-om.

## ? Funkcionalnosti

- **Automatsko logovanje** svih MediatR zahteva
- **Tracking sporih zahteva** (>1000ms) u bazu podataka
- **Real-time dashboard** sa statistikama
- **Filtriranje** po trajanju, datumu, broju rezultata
- **Color-coded indikatori** za razli?ite nivoe performansi
- **Exception tracking** za neuspešne zahteve

## ?? Šta se prati?

### Automatski se loguju:
- ? Svi zahtevi preko MediatR (Commands & Queries)
- ? Trajanje izvršenja u milisekundama
- ? Request data (JSON)
- ? Response data (JSON)
- ? Exception poruke (ako ima)
- ? Status (Success/Failed)

### Threshold-ovi:
- ?? **< 1000ms**: Normalno (ne loguje se u bazu)
- ?? **1000-3000ms**: Sporo (loguje se)
- ?? **3000-5000ms**: Vrlo sporo (loguje se)
- ?? **> 5000ms**: Kriti?no sporo (loguje se)

## ?? Dashboard Metrike

### Summary Cards:
1. **Total Requests** - Ukupan broj pra?enih zahteva
2. **Slow Requests** - Broj zahteva sporijih od 1s
3. **Failed Requests** - Broj neuspešnih zahteva
4. **Average Duration** - Prose?no trajanje
5. **Max Duration** - Najduže trajanje

### Tabela Najsporijih Zahteva:
- Sortirana po trajanju (najsporiji prvi)
- Prikazuje vreme, naziv zahteva, trajanje, status
- Exception details za neuspešne zahteve

## ?? Pristup

Dashboard je dostupan na:
- **URL**: `/performance`
- **Link u navigaciji**: "? Performance"
- **Link na po?etnoj stranici**

## ?? Backend Implementacija

### 1. PerformanceLoggingBehavior
MediatR Pipeline Behavior koji automatski prati sve zahteve:

```csharp
public class PerformanceLoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
```

**Funkcionalnost:**
- Meri vreme izvršenja svakog zahteva
- Loguje u Serilog (console/file) uvek
- Loguje u bazu samo ako:
  - Trajanje > 1000ms ILI
  - Zahtev nije uspeo (exception)

### 2. PerformanceLogs Tabela (Analytics DB)

```sql
CREATE TABLE "PerformanceLogs" (
    "Id" BIGSERIAL PRIMARY KEY,
    "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL,
    "RequestType" VARCHAR(200) NOT NULL,
    "RequestName" VARCHAR(500) NOT NULL,
    "DurationMs" BIGINT NOT NULL,
    "RequestData" VARCHAR(4000),
    "ResponseData" VARCHAR(4000),
    "ExceptionMessage" VARCHAR(2000),
    "IsSuccess" BOOLEAN NOT NULL
);
```

**Indexes:**
- `IX_PerformanceLogs_Timestamp`
- `IX_PerformanceLogs_DurationMs`
- `IX_PerformanceLogs_RequestName`

### 3. API Endpoint

**Endpoint**: `GET /api/performance`

**Query parametri:**
- `topCount` (default: 20) - Broj najsporijih zahteva
- `minDurationMs` (default: 1000) - Minimalno trajanje u ms
- `fromDate` (optional) - Od datuma
- `toDate` (optional) - Do datuma

**Response:**
```json
{
  "slowestRequests": [
    {
      "id": 1,
      "timestamp": "2025-12-28T00:00:00Z",
      "requestName": "CreateArtikalCommand",
      "durationMs": 3500,
      "isSuccess": true,
      "exceptionMessage": null
    }
  ],
  "summary": {
    "totalRequests": 150,
    "slowRequests": 12,
    "failedRequests": 2,
    "averageDurationMs": 450,
    "maxDurationMs": 5600
  }
}
```

## ?? Setup Uputstva

### 1. Kreiraj tabelu u Analytics bazi

Pokreni migration SQL:

```bash
psql -h <host> -U <user> -d analytics -f Database/Migrations/002_CreatePerformanceLogsTable.sql
```

Ili ru?no:

```sql
CREATE TABLE "PerformanceLogs" (
    "Id" BIGSERIAL PRIMARY KEY,
    "Timestamp" TIMESTAMP WITH TIME ZONE NOT NULL,
    "RequestType" VARCHAR(200) NOT NULL,
    "RequestName" VARCHAR(500) NOT NULL,
    "DurationMs" BIGINT NOT NULL,
    "RequestData" VARCHAR(4000),
    "ResponseData" VARCHAR(4000),
    "ExceptionMessage" VARCHAR(2000),
    "IsSuccess" BOOLEAN NOT NULL
);

CREATE INDEX "IX_PerformanceLogs_Timestamp" ON "PerformanceLogs" ("Timestamp");
CREATE INDEX "IX_PerformanceLogs_DurationMs" ON "PerformanceLogs" ("DurationMs");
CREATE INDEX "IX_PerformanceLogs_RequestName" ON "PerformanceLogs" ("RequestName");
```

### 2. Restartuj Backend

```bash
cd Trendplus2
dotnet run
```

### 3. Restartuj Frontend

```bash
cd Klijent/clientapp
npm run dev
```

### 4. Testiraj

Otvori u browseru:
```
http://localhost:5173/performance
```

## ?? UI Features

### Color Coding:
- ?? **Zeleno** (< 1s): Brzo
- ?? **Žuto** (1-3s): Sporo
- ?? **Narandžasto** (3-5s): Vrlo sporo
- ?? **Crveno** (> 5s): Kriti?no sporo

### Filteri:
- **Top Count**: Broj prikazanih rezultata (1-100)
- **Min Duration**: Minimalno trajanje u ms
- **Date Range**: Od/do datum filteri
- **Reset**: Resetuj sve filtere na default

## ?? Use Cases

### 1. Identifikacija sporih upita
- Prona?i najsporije API endpoints
- Optimizuj queries koji traju dugo

### 2. Monitoring production performansi
- Real-time pra?enje performansi
- Alerting za kriti?no spore zahteve

### 3. Debugging
- Vidi koje zahteve su failovali
- Proveri exception messages

### 4. Performance trend analysis
- Prati prose?no vreme izvršenja
- Identifikuj degradacije performansi

## ?? Važne Napomene

### Performance Impact:
- **Minimalan overhead** (<5ms per request)
- Logovanje u bazu **samo za spore/failed zahteve**
- Async operacije - ne blokira glavni thread

### Data Retention:
- **Preporuka**: Arhiviraj/obriši stare logove periodi?no
- Dodaj scheduled job za cleanup (npr. >30 dana)

### Monitoring:
- Prati veli?inu `PerformanceLogs` tabele
- Dodaj alert za broj failed requests
- Postavi threshold za average duration

## ?? Maintenance

### Cleanup starih logova:

```sql
-- Obriši logove starije od 30 dana
DELETE FROM "PerformanceLogs" 
WHERE "Timestamp" < NOW() - INTERVAL '30 days';

-- Arhiviraj u drugu tabelu pre brisanja
INSERT INTO "PerformanceLogsArchive" 
SELECT * FROM "PerformanceLogs" 
WHERE "Timestamp" < NOW() - INTERVAL '30 days';
```

### Optimizacija:

```sql
-- Vacuum tabelu povremeno
VACUUM ANALYZE "PerformanceLogs";

-- Rebuild indexes
REINDEX TABLE "PerformanceLogs";
```

## ?? Budu?i Development

Potencijalne nadogradnje:
- [ ] Export u CSV/Excel
- [ ] Email alerts za kriti?ne performanse
- [ ] Grafikon sa trendovima
- [ ] Pore?enje performansi po danu/sedmici
- [ ] Integration sa Application Insights
- [ ] Custom thresholds per request type

## ?? Zaklju?ak

Performance tracking feature omogu?ava:
- ? Proaktivno identifikovanje bottlenecks-a
- ? Real-time monitoring performansi
- ? Brzu dijagnozu sporih operacija
- ? Data-driven optimization decisions

**Happy Performance Monitoring! ?**
