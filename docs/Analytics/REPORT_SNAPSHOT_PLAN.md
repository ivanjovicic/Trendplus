# REPORT SNAPSHOT PLAN

## Purpose
Supplier Decision Report i Pilot Intake Report treba da budu ponovo otvorivi i nakon refresh-a stranice, bez oslanjanja samo na browser preview state.

## 1) Trenutno stanje

### Supplier Decision Report
- Trenutno se oslanja na resolved table payload i browser state za prikaz report pogleda.
- Ako korisnik osveži stranicu ili podeli URL bez konteksta, prikaz može da izgubi ključne podatke.
- Postoje export/print tokovi, ali oni nisu pouzdan mehanizam za ponovno otvaranje istog report stanja.

### Pilot Intake Report
- Report može da se prikaže, exportuje i štampa.
- Nema durable snapshot identitet koji omogućava kasnije ponovno otvaranje istog sadržaja preko stabilnog `reportId`.
- URL i browser state nisu dovoljni kao dugoročni izvor istine.

## 2) Ciljno stanje
Svaki report treba da ima stabilan model koji može da se rekonstruiše i otvori kasnije:

- `reportId` (stabilan identifikator snapshot-a ili deterministički ključ)
- `generatedAtUtc`
- `period` (`fromDate`, `toDate`, eventualno `dataScope`)
- `lastRefresh` (analytics freshness kontekst)
- `dataQuality` (status + ključni count/signali)
- `warnings` (fallback, partial, stale, insufficient_data)
- `methodology` (kratko objašnjenje izvora i semantike)
- `sections/rows` (celokupan report payload za render)

Napomena: cilj je reproducibilnost prikaza, ne promena poslovne logike kalkulacija.

## 3) Predlog API pristupa

### Opcija A: Snapshot API (preporučeno za durable sharing)
- `POST /api/analytics/reports/snapshot`
  - Ulaz: report tip + filteri + opcioni metadata context
  - Izlaz: `reportId`, summary meta, eventualno odmah payload
- `GET /api/analytics/reports/snapshot/{id}`
  - Izlaz: kompletan report snapshot payload (meta + sekcije)

Prednosti:
- Jasan lifecycle report artefakta
- Lako deljenje i ponovno otvaranje
- Pogodno za audit trail

### Opcija B: Stabilni GET report endpoint-i (brži početak)
- Stabilni GET endpoint za Supplier report
- Stabilni GET endpoint za Pilot Intake report
- Report se rekonstruiše iz filtera i backend stanja, bez snapshot perzistencije

Prednosti:
- Manji inicijalni scope
- Brže za Phase 1

Ograničenje:
- Nije garantovana identičnost prikaza kroz vreme (promene podataka/refresh-a).

## 4) Skladištenje i retencija

### Skladištenje
- Primarno: DB tabela (npr. `AnalyticsReportSnapshot`) sa JSON payload kolonom.
- Alternativa: object/file storage za velike payload-e, uz DB metadata zapis.

Minimalna polja snapshot zapisa:
- `Id` / `ReportId`
- `ReportType` (`supplier_decision`, `pilot_intake`)
- `GeneratedAtUtc`
- `PeriodFromUtc`, `PeriodToUtc`
- `DataScope`
- `LastRefreshAtUtc`
- `DataQualityStatus`
- `WarningsJson`
- `MethodologyJson`
- `PayloadJson`
- `CreatedBy` (ako je dostupno)

### Retencija
- Početni predlog: 30-90 dana (konfigurisano).
- Cleanup job: periodično brisanje snapshot-a van retencionog prozora.
- Kasnije: posebna pravila za pilot/prod okruženja.

### Tenant/client izolacija (future-proof)
- Uvesti `ClientId/TenantId` kad multi-tenant model bude aktivan.
- Endpoint mora filtrirati po tenancy kontekstu (kasnija faza, eksplicitno planirana).

## 5) Faze implementacije

### Phase 1: Stable GET report endpoint (bez perzistencije)
- Definisati stabilan response contract za Supplier i Pilot Intake report.
- Frontend report stranice preći sa browser-only state na fetch-by-query.
- Obezbediti da refresh stranice ne razbije prikaz.

Deliverable:
- Report se može ponovo otvoriti preko URL parametara i backend GET endpoint-a.

### Phase 2: Snapshot persistence
- Dodati snapshot entitet/tabelu i API za create/get snapshot.
- Dodati `reportId` URL mode (`/analytics/reports/.../snapshot/{id}` ili query varijanta).
- U prikazu jasno označiti da je sadržaj snapshot (vreme generisanja + freshness).

Deliverable:
- Report je durable, deljiv i reproducibilan preko `reportId`.

### Phase 3: Share/Email manager
- Dodati flow za deljenje linka i eventualno email slanje snapshot izveštaja.
- Politike pristupa i opcionalna expiracija linkova.
- Audit polja (ko je kreirao, kada je otvoren).

Deliverable:
- Sales/demo i operativni tim mogu bezbedno deliti i ponovo otvarati isti report.

## 6) Rizici i mitigacije

### Stale report vs live data
Rizik:
- Snapshot može biti zastareo u odnosu na live dashboard.

Mitigacija:
- Uvek prikazati `generatedAtUtc`, `lastRefresh`, `dataQualityStatus` i warning badge.
- Dodati CTA: "Otvori live verziju" gde je relevantno.

### Dozvole i pristup
Rizik:
- Neautorizovan pristup deljenim report linkovima.

Mitigacija:
- Auth provera na GET snapshot endpoint-u.
- Kasnije: signed link ili token sa istekom za eksterno deljenje.

### Export i privatnost podataka
Rizik:
- Snapshot može sadržati osetljive poslovne podatke.

Mitigacija:
- Jasna data-classification pravila.
- Kontrola koje sekcije/kolone ulaze u snapshot i export.

### Veličina payload-a
Rizik:
- Veliki report može opteretiti DB/API.

Mitigacija:
- Ograničenje max rows po sekciji.
- Kompresija JSON payload-a ili object storage fallback.
- Paginacija/segmentacija za ekstremne slučajeve.

## Proposed next implementation prompt scope
Da sledeći Codex prompt ostane mali i konkretan:
- Implementirati samo Phase 1 za jedan report (Supplier Decision Report) sa stabilnim GET contract-om.
- Dodati minimalan FE fallback za refresh-safe load.
- Ne ulaziti još u snapshot persistence i share manager.
