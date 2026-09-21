# Operacije — audit nalaza i spremni queue promptovi (2026-09-21)

Queue owner: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`  
Queue: `direct-user-request`  
Scope: svih osam stavki pod menijem `Operacije`, njihove canonical/legacy redirect putanje i zajednički analytics error/URL/trust sloj.

## Površine pregledane u kodu

| Stavka menija | Ruta | Vlasnik |
|---|---|---|
| Zalihe i dopuna | `/analytics/inventory` | `InventoryPage` |
| Prodaja po dobavljačima | `/analytics/supplier-sales-stats` | redirect → Supplier `overview` |
| Prodaja po tipu obuće | `/analytics/shoe-type-sales-stats` | `ShoeTypeSalesStatsPage` |
| Prodaja po smeni i dobavljačima | `/analytics/daily-sales` | `DailySalesStatsPage` |
| Pre/Posle nivelacije | `/analytics/nivelacije-pre-post` | `ProdajaPrePostNivelacijePage` |
| Prodaja po boji artikla | `/analytics/color-sales-stats` | `ColorSalesStatsPage` |
| Prioriteti nivelacije | `/analytics/pre-nivelacija-prioriteti` | `PreNivelacijaPriorityPage` |
| Dobavljači i tipovi obuće | `/analytics/dobavljaci-tipovi-obuce` | redirect → Supplier `assortment` |

## Prioritetizovani backlog spreman za rad

Promptovi su već u canonical queue-u i ostaju `WAITING`; ovaj audit ih ne promoviše niti claim-uje.

### P1 — poverenje i zaštita operativnog toka

- `RQ301` — Inventory cockpit jezička konzistentnost.
- `RQ302` — smoke pokrivenost svih Operacije ruta i redirecta.
- `RQ303` — Daily Sales mixed QA/mismatch copy.
- `RQ304` — Color detail score label parity.
- `RQ368` — inline partial-failure poruke u Pre/Post prikazuju sirovu tehničku grešku.

### P2 — period, filteri, fallback i jasnoća toka

- `RQ305`–`RQ308` — Supplier alias IA, dijakritici, Shoe impact label i Inventory period/snapshot provenance.
- `RQ315`–`RQ323` — Pre-Nivelacija period/empty parity, URL/apply semantics, draft-vs-active period, store-load failure, Inventory bootstrap i stale secondary panels.
- `RQ325` — preostali engleski Operacije copy.

### P3 — navigacija, deljivi URL-ovi i test/polish dug

- `RQ309`–`RQ311` — sidebar ikone, production-route test fixture-i i guardrail cleanup.
- `RQ324`, `RQ326`–`RQ330` — Inventory detail error, Pre-Nivelacija/Daily sort URL, Pre/Post expansion/focus context i Shoe truncation label.

`RQ312`–`RQ358` koji su već `DONE` nisu ponovo otvoreni; audit ih tretira kao prethodno zatvorene nalaze, ne kao novi backlog.

Governance repair: queue tabela je imala zastarele `WAITING` redove za `RQ362`–`RQ364`, iako su completion notes i `MASTER_ROADMAP.md` već navodili `DONE`. Redovi su usklađeni; ovo nije novi runtime prompt.

## Novi potvrđeni nalaz

### RQ368 — inline Pre/Post partial-failure poruke ne koriste sigurnu mapu grešaka

**Prioritet:** P1  
**Dokaz:** `ProdajaPrePostNivelacijePage.tsx` pravi `previousError` iz `reason.message` (oko linije 610), a zatim ga direktno renderuje u `previousComparisonError` upozorenju (oko linije 1386). Isti obrazac postoji za `vendorLoadError` (oko linija 550 i 1393). Za razliku od glavnog `AnalyticsErrorState`, ove inline poruke zaobilaze `getSafeAnalyticsErrorMessage`.

**Rizik:** PostgreSQL/HTTP/stack detalji mogu postati korisnički tekst; korisnik dobija tehnički i potencijalno nestabilan opis umesto lokalizovane instrukcije, dok je delimični rezultat inače pravilno zadržan.

**Spremnost za rad:** Prompt `RQ368` sadrži Problem, Evidence, Scope, Read first, Do, Tests, Acceptance i Dependencies. Predviđen je frontend-only patch bez promene backend ugovora: zadržati current-period podatke i vendor listu, ali sanitizovati inline partial-failure poruke i dodati regresije za tehnički `Error`.

## Šta nije dokazano u ovom auditu

- Nije izvršen live browser smoke niti backend runtime test.
- Nisu ponovljene sve page-focused specifikacije; izvršeni su governance/evidence testovi i frontend analytics guardrails/typecheck.
- Ne uvodi se novi `READY` prompt: canonical queue i dalje ima `Current READY prompt: none`; owner mora eksplicitno promovisati jedan prompt pre implementacije.

## Validacija audita

- `npm run test:validation-evidence` — 4/4.
- `node scripts/check-prompt-queues.mjs` — pass.
- `node scripts/check-planning-architecture.mjs` — pass.
- `npm run check:analytics-guardrails` — pass, baseline-only sa 51 poznatim nalazom.
- `git diff --check` — pass pre dokumentacione izmene.
