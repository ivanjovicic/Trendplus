# Trendplus Frontend / UX Standards

## Core components

Koristi:
- `AnalyticsTrustHeader`
- `AnalyticsRefreshStatusBanner`
- `AnalyticsErrorState`
- `AnalyticsEmptyState`
- `InfoTip`
- shared table/export components gde postoje

## Layout

Svaki analytics ekran:
1. Trust header
2. Filteri
3. KPI/summary
4. Decision/signal/report content
5. Table/chart
6. Methodology/help
7. Export/report

## Above the fold

Za executive/dashboard ekrane, iznad fold-a moraju biti:
- freshness/data context
- 4–5 ključnih KPI
- najvažnije odluke
- data quality warning ako postoji

Ne stavljati velike grafikone iznad decision panela.

## Empty/Error

Error:
- crvena/critical semantika
- retry
- correlation ID
- bez KPI nula

Empty:
- neutralno/warning
- razlozi
- akcije

Insufficient:
- ne prikazivati finalnu preporuku
- objasniti šta nedostaje

## Language

Korisnik nije developer.
Izbegavati:
- MV
- DTO
- SQL
- cache key
- first_markdown_date
- null
u UI tekstu, osim u admin/dev panelu.

Koristi:
- Podaci trenutno nisu dostupni
- Nema dovoljno podataka
- Proširite period
- Proverite kvalitet podataka
- Osvežavanje nije završeno

## Buttons

Dugme mora reći šta radi:
- `Dodaj u akcije`
- `U centralnim akcijama`
- `Print izveštaj`
- `Export Excel`
- `Pokušaj ponovo`
- `Otvori kvalitet podataka`

Izbegavati:
- `OK`
- `Submit`
- `Run`
- `Action`

## Tables

- jasni headeri
- tooltip za poslovno teške metrike
- sticky action column samo ako ne lomi mobile
- horizontal scroll u wrapperu tabele
- empty state u tabeli ako nema redova

## Charts

- chart nije zamena za odluku
- ako nema podataka, empty state
- boje kroz CSS vars
- legend order eksplicitan ako je poslovno bitan

## Accessibility

- status ne zavisi samo od boje
- aria-label za icon-only dugmad
- focus states vidljivi
- kontrast preko theme tokena
