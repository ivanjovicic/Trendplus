# Trendplus Backend Standards

## AnalyticsResponseMeta

Core analytics endpointi treba da imaju meta contract.

```csharp
AnalyticsResponseMetaDto
AnalyticsResponseMetaFactory
```

Standard:
- `Success` za uspešne podatke
- `Empty` za uspešan query bez redova
- `Warning` za partial/stale/fallback
- `Error` za kontrolisane greške ako se ne koristi Problem

## No fake zero

Nikad:
```csharp
return Results.Ok(new { totalRevenue = 0, ... });
```
ako query nije uspeo.

Umesto toga:
```csharp
return Results.Problem(...);
```
ili DTO sa:
```csharp
Meta = AnalyticsResponseMetaFactory.Error(...)
```

## Error handling

- Catch specific DB exceptions gde ima smisla.
- Missing MV/table pretvoriti u user-friendly unavailable state.
- Timeout -> unavailable, ne empty.
- ErrorRecord insert mora biti safe.
- Trim dugačke poruke.
- CorrelationId gde postoji.

## Queries

- Ne raditi heavy query bez timeout guard-a.
- Ne fallback-ovati period bez metadata.
- Paginirati liste.
- Koristiti top/limit parametre.
- Logovati spor query gde postoji standard.

## Migrations

Za Analytics DB:
```powershell
dotnet ef migrations list `
  --project .\Infrastructure\Infrastructure.csproj `
  --startup-project .\Api\Api.csproj `
  --context AnalyticsDbContext
```

Ako dodaješ tabelu:
- DbSet
- OnModelCreating mapping
- indexes
- migration
- tests/smoke endpoint

## Workers

Heavy refresh ide u worker process.
Web process sme da prikaže status i eventualno admin manual trigger.

## Supplier Scorecard

Backend mora da štiti:
- no silent fallback
- recommendationAllowed
- empty vs error
- missing MV
- zero-revenue rows
- blank supplier names

## Product Decision

Backend helper treba da daje:
- recommendation status
- reason codes
- confidence
- reliability
- data quality
- margin/cost coverage

Frontend ne sme nadoknađivati ove vrednosti.
