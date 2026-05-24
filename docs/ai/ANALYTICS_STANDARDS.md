# Trendplus Analytics Standards

## Product definition

Trendplus analytics je decision-support sistem za maloprodaju obuće/odeće.

Ne prodaje se "analytics", nego:
- šta pojačati
- šta zadržati
- šta proveriti
- šta smanjiti
- gde je marža
- gde je mrtav lager
- kojim podacima ne treba verovati

## Sales-ready definition

Ekran je sales-ready kada korisnik bez developera razume:
- šta ekran radi
- koji period gleda
- odakle dolaze podaci
- kada su osveženi
- koliko su pouzdani
- zašto je preporuka data
- šta treba da uradi

---

## Standardni analytics ekran

1. `AnalyticsTrustHeader`
2. Filteri
3. KPI kartice
4. Glavni decision/signal/report blok
5. Tabela/grafikon
6. "Kako čitati ovaj ekran?"
7. Metodologija
8. Export/print/report
9. Empty/error/insufficient state

---

## Recommendation / Signal / Report

### Recommendation

Sistem predlaže akciju.

Primer:
- Product Decision Center
- Inventory dopuna
- Pre-nivelacija prioriteti
- Executive dashboard actions

### Signal

Analitički signal, nije finalna preporuka.

Primer:
- Daily sales
- Shoe type/color sales
- Supplier scorecard ako je pomoćni signal
- Chart panels

### Report

Stanje, audit ili izveštaj.

Primer:
- Data Quality
- Action Queue
- Supplier Decision Report
- Pre/post nivelacija report

---

## Canonical vocabulary

Koristi dosledno:

| Termin | Značenje |
|---|---|
| Prihod | prodajna vrednost |
| Maržni doprinos | poslovni doprinos po marži |
| Lager u riziku | zaliha koja nosi rizik |
| Kapital u riziku | vrednost robe vezana u rizičnoj zalihi |
| Pouzdanost signala | reliability |
| Sigurnost preporuke | confidence |
| Kvalitet podataka | data quality |
| Nedovoljno podataka | nema dovoljno signala |
| Pomoćni signal | nije finalna preporuka |
| Preporuka sistema | sistem daje akciju |
| Analitički signal | indikator za proveru |
| Nivelacija | promena/sniženje cena u poslovnom jeziku aplikacije |

---

## Period semantics

Period je poslovna istina.

Obavezno:
- requested period
- effective dataset
- fallback warning
- recommendationAllowed
- empty/insufficient state

Zabranjeno:
- tihi fallback
- prikaz šireg perioda kao užeg
- `0` kao zamena za grešku
- all-time kao custom range bez oznake

---

## Refresh/freshness

Status:
- `fresh`: poslednji uspeh <= 24h
- `stale`: poslednji uspeh > 24h
- `critical`: >72h ili failure posle success-a
- `unknown`: nema podataka

Ne izmišljati fresh status.

---

## Data quality status

Canonical:
- `good`
- `warning`
- `critical`
- `insufficient_data`

Legacy:
- `fair` -> `warning`
- `poor` -> `critical`

Ako nije poznato, ne mapirati na good.

---

## Core UI components

Koristi:
- `AnalyticsTrustHeader`
- `AnalyticsRefreshStatusBanner`
- `AnalyticsErrorState`
- `AnalyticsEmptyState`
- shared formatters
- shared recommendation/data-quality labels

Ne praviti paralelne komponente osim ako se postojeće ne mogu proširiti.

---

## Backend meta contract

Core response treba da ima:

```ts
meta?: AnalyticsResponseMeta
```

Backend ekvivalent:
```csharp
AnalyticsResponseMetaDto
```

Stanja:
- Success
- Empty
- Warning
- Error

Frontend mora da koristi `meta` kada postoji.

---

## No fake zero matrix

| Situacija | Backend | Frontend |
|---|---|---|
| SQL timeout | Error/Problem | ErrorState |
| Missing MV/table | Error/Problem | ErrorState |
| Query uspeo bez redova | Empty meta | EmptyState |
| Nedovoljno signala | insufficient_data | Empty/Warning |
| Fallback dataset | Warning meta | Warning banner |
| Stale refresh | Warning/freshness | Refresh banner |

---

## Frontend boundary

Frontend ne računa finalne odluke. Backend ih vraća.

Dozvoljeno:
- UI label mapping
- CSS tone
- sort/filter
- display helpers

Nedozvoljeno:
- local recommendation engine
- weighted score
- local confidence
- local reasonCodes

---

## UX standards

### Headings

Naslov mora reći poslovnu svrhu, ne internu tehnologiju.

Dobro:
- `Odluke o proizvodima`
- `Zalihe i dopuna`
- `Kvalitet podataka`
- `Trendplus pregled`

Loše:
- `Analytics advanced`
- `Decision hub v2`
- `MV status`

### Empty state

Mora imati:
- naslov
- objašnjenje
- moguće razloge
- sledeće akcije

### Error state

Mora imati:
- korisničku poruku
- retry ako postoji
- correlation ID ako postoji
- predloge šta dalje

### Methodology

Za KPI/preporuke:
- kako se računa
- koji podaci ulaze
- kada se preporuka blokira
- šta smanjuje pouzdanost

---

## Performance

- Ne povlačiti ogromne liste ako treba samo summary.
- Koristiti pagination/top parametre.
- Cache mora imati TTL i invalidation posle importa/refresh-a.
- Ne raditi heavy query u render path-u frontenda.
- Ne dodavati `useEffect` koji loop-uje zbog nestabilnih dependency objekata.
- Ne refetch-ovati queue keys na svaki search keypress ako sourceKey ne zavisi od search-a.

---

## Accessibility / usability

- Dugmad moraju imati jasan tekst.
- Status ne sme zavisiti samo od boje.
- Tabele moraju imati horizontal scroll blizu tabele.
- Loading mora razlikovati prvi load i refresh.
- Error i success feedback ne sme nestati odmah pre nego što korisnik pročita.

---

## Reports

Report treba da bude:
- print-friendly
- razumljiv van aplikacije
- sa periodom/freshness/data quality
- sa methodology
- sa warnings
- sa fallback za export greške

---

## Definition of done for analytics task

- [ ] Trust/data context tačan
- [ ] Empty/error razdvojeni
- [ ] No fake zero
- [ ] Backend source of truth
- [ ] Shared formatters
- [ ] UTF-8 bez mojibake
- [ ] Theme tokens
- [ ] Build/check/test pokrenuti ili objašnjen razlog
