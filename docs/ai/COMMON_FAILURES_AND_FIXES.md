# Trendplus Common Failures and Fixes

## 1. Mojibake / pokvaren UTF-8

Pretraga:
```text
Ä
Å
â
�
DobavljaÄ
marÅ
osveÅ
uÄ
Å¡
Å¾
```

Fix:
- sačuvati fajl kao UTF-8
- popraviti srpske tekstove
- ne menjati logiku u istom commitu ako nije potrebno

---

## 2. Lokalni formatteri

Problem:
```ts
function formatCurrency() {}
function formatPercent() {}
```

Fix:
```ts
import { fmtRsd, fmtPct, fmtNumber } from "../utils/analyticsFormatters";
```

Provera:
```powershell
cd Klijent/clientapp
npm run check:analytics-guardrails
```

---

## 3. Fake zero

Problem: backend greška izgleda kao `0 RSD`.

Fix:
- backend: `AnalyticsResponseMetaFactory.Error(...)` ili Problem
- frontend: `AnalyticsErrorState`
- ne prikazivati KPI nule na error

---

## 4. Empty state bez objašnjenja

Loše:
```text
Nema podataka.
```

Dobro:
```text
Nema dovoljno podataka za izabrani period.

Mogući razlozi:
- nije bilo prodaje
- dobavljači nisu povezani
- nabavne cene nisu dostupne
- refresh nije završen
- period je preuzak
```

---

## 5. Supplier scorecard silent fallback

Problem: 30d prikazuje 180d bez oznake.

Fix:
- `usedFallback=true`
- `fallbackReason`
- `recommendationAllowed=false`
- UI: `Pomoćni signal`
- Trust Header: requested/effective dataset

---

## 6. Worker/refresh konfuzija

Problem: korisnik ne zna da li su podaci sveži.

Fix:
- `/api/analytics/refresh-status`
- `AnalyticsRefreshStatusBanner`
- web vs worker warning
- last success/failure

---

## 7. Missing MV/table

Problem: deploy bez migracije ruši analytics.

Fix:
- missing object -> controlled error
- no fake zero
- migration/initializer proveriti
- ErrorState u UI

---

## 8. Hardcoded theme colors

Problem:
```css
color: #22c55e;
background: rgb(...);
```

Fix:
```css
color: var(--success);
background: var(--surface-elevated);
border-color: var(--border-default);
```

---

## 9. Recharts problemi

Ponavljalo se:
- pogrešan legend order
- unsupported prop
- hardcoded color
- tooltip drift

Fix:
- explicit legend payload
- podržani Recharts props
- CSS variables
- shared tooltip style

---

## 10. Report preview expired

Ako report koristi browser state:
- jasno reci da je preview privremen
- ponudi ponovno generisanje
- ponuditi print/export za trajni dokument

Dugoročno:
- backend report endpoint

---

## 11. API URL u produkciji

Problem:
```text
Unexpected token <, <!doctype ...
```

Znači fetch gađa SPA, ne API.

Fix:
- koristiti `apiUrl()`/postojeći API helper
- ne hardkodovati relativne admin/API putanje ako deployment koristi odvojen backend

---

## 12. Action notes u Description

Problem: Description postaje audit log.

Fix:
- posebna notes/history tabela ako postoji
- Description ostaje opis akcije
- timeline prikazuje status history

---

## 13. Prevelik commit

Problem:
- backend
- frontend
- CSS
- migracija
- report
- worker
sve u jednom.

Fix:
- podeli na logične commitove
- svaki commit ima test/check

---

## 14. Token/agent drift

Problem: agent krene da menja previše fajlova.

Fix:
- stani posle 5-8 relevantnih fajlova
- sumiraj nalaze
- nastavi samo ako je potrebno
- ne radi masovni rewrite bez razloga

---

## 15. Search/build loop

Problem: ista komanda puca više puta.

Fix:
- ne ponavljaj identičnu komandu
- pročitaj error
- probaj uži command
- prijavi blokadu ako je environment problem

---

## 16. UI kaže "preporuka", a zapravo je signal

Problem: korisnik veruje nečemu što je samo helper signal.

Fix:
- `mode="signal"`
- label `Pomoćni signal`
- finalna preporuka samo ako `recommendationAllowed=true`

---

## 17. Data quality link nedostaje

Ako dataQualityStatus nije `good`, mora postojati put do:
```text
/analytics/data-quality
```

---

## 18. Refresh unknown prikazan kao fresh

Ako nema podataka o refresh-u:
- status `unknown`
- ne `fresh`
- UI kaže da vreme osveženja nije dostupno
