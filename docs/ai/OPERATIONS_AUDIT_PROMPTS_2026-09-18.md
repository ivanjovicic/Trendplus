# Operacije — audit nalaza i queue promptovi (2026-09-18)

Queue owner: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
Base: `origin/main` (post-`RQ300`)
**Ukupno WAITING promptova iz ovog audita: RQ301–RQ358 (58)**

---

## Mapa ekrana

| Meni | Ruta | Komponenta |
|---|---|---|
| Zalihe i dopuna | `/analytics/inventory` | `InventoryPage` |
| Prodaja po dobavljačima | `/analytics/supplier-sales-stats` | redirect → `/analytics/supplier?tab=overview` |
| Prodaja po tipu obuće | `/analytics/shoe-type-sales-stats` | `ShoeTypeSalesStatsPage` |
| Prodaja po smeni | `/analytics/daily-sales` | `DailySalesStatsPage` |
| Pre/Posle nivelacije | `/analytics/nivelacije-pre-post` | `ProdajaPrePostNivelacijePage` |
| Prodaja po boji | `/analytics/color-sales-stats` | `ColorSalesStatsPage` |
| Prioriteti nivelacije | `/analytics/pre-nivelacija-prioriteti` | `PreNivelacijaPriorityPage` |
| Dobavljači i tipovi obuće | `/analytics/dobavljaci-tipovi-obuce` | redirect → `/analytics/supplier?tab=assortment` |

---

## Već zatvoreno — ne duplirati

| Opseg | ID |
|---|---|
| Operacije audit 2026-09-15 | **RQ265–RQ300** |
| Round 1 audit 2026-09-18 | **RQ301–RQ311** (WAITING) |
| Round 2 audit 2026-09-18 | **RQ312–RQ330** (WAITING) |

---

## Round 1 — RQ301–RQ311 (copy, routing, guardrails)

| ID | P | Sažetak |
|---|---|---|
| RQ301 | P1 | Inventory engleski cockpit copy |
| RQ302 | P1 | 6/8 Operacije ruta van route smoke |
| RQ303 | P1 | Daily Sales „Check“ badge + mixed QA copy |
| RQ304 | P1 | Color „Decision score“ vs „Skor odluke“ |
| RQ305 | P2 | Supplier meni IA (tab alias) |
| RQ306 | P2 | Dijakritici pass |
| RQ307 | P2 | Shoe Type „Low signal“ |
| RQ308 | P2 | Inventory period null u trust header-u |
| RQ309 | P3 | Duplirane ShoppingBag ikone |
| RQ310 | P3 | Testovi na `/analitika/...` umesto `/analytics/...` |
| RQ311 | P3 | Guardrail score/reliability cleanup |

---

## Round 2 — RQ312–RQ330 (trust, URL, filter UX)

### P1 — trust / fake data

| ID | Ekran | Problem |
|---|---|---|
| **RQ312** | Inventory | Signal window zamrznut na mount |
| **RQ313** | Inventory | Insights failure + badge `?? 0` |
| **RQ314** | Pre/Post | Driver summary fake 0 RSD |

### P2 — URL, filter UX, silent failures

| ID | Ekran(i) | Problem |
|---|---|---|
| **RQ315–RQ323** | Pre-Nivelacija, Pre/Post, Color, Shoe, Inventory | Period, URL, filter apply, store load, stale paneli |
| **RQ325** | Color, Pre/Post, Inventory | Preostali engleski stringovi |

### P3 — polish

| ID | Ekran | Problem |
|---|---|---|
| **RQ324–RQ330** | Inventory, Pre-Nivelacija, Daily, Pre/Post, Shoe | Size-curve error, sort URL, expansion, truncation, focus context |

---

## Round 3 — RQ331–RQ358 (NOVO — dubinski trust, chart bug, refetch, supplier redirect)

### P1 — trust / fake data / frontend business logic

| ID | Ekran | Problem |
|---|---|---|
| **RQ331** | Inventory | Signal KPI kartice broje samo trenutnu stranicu (~50 redova), ne ceo filter |
| **RQ332** | Inventory | Off-page SKU detail otvara placeholder sa `kolicina: 0`, `nabavnaCena: 0` |
| **RQ333** | Pre/Post | Frontend računa `postSharePct` kad backend nema vrednost |
| **RQ334** | Supplier (redirect) | `prePostComparableArticleCount ?? 0` → lažni 0 artikala |

### P2 — stale data, race, silent failure, filter bias

| ID | Ekran(i) | Problem |
|---|---|---|
| **RQ335** | Daily Sales | Previous-period fetch failure gutan — N/A delta bez upozorenja |
| **RQ336** | Daily Sales | Trend/shift chart koristi `sortedRows` umesto hronološkog reda — **bug sortiranja** |
| **RQ337** | Shoe, Color, Pre/Post | `getPresetRange("30d")` zamrznut na mount |
| **RQ338** | Pre/Post | Vendor load failure → tihi prazan dropdown |
| **RQ339** | Pre/Post | Focus filter reset na svaki reload (uključujući scope) |
| **RQ340** | Shoe Type | `showStaleError` mrtav kod — refetch uvek briše `data` |
| **RQ341** | Color | Isti problem — nema stale refetch puta |
| **RQ342** | 5 ekrana | Nema AbortController — race na brzim filter promenama |
| **RQ343** | Pre-Nivelacija | Expanded row se gubi na paginaciji |
| **RQ344** | Pre-Nivelacija | Filter dropdown opcije samo sa trenutne stranice |
| **RQ345** | Shoe, Color | Frontend share % recompute kad backend nema |
| **RQ346** | Shoe Type | `avgMarginPct` = frontend prosek redova |
| **RQ347** | Pre/Post | Winner/risk sort po raw `changeRevenue`, ne `trustedMetric` |
| **RQ348** | Supplier Footwear (redirect) | Toolbar metadata `?? 0` |

### P3 — polish, empty semantics, URL, localization

| ID | Ekran | Problem |
|---|---|---|
| **RQ349** | Color | `decisionScore` = zaokružen `confidencePct`, ne backend score |
| **RQ350** | Shoe, Color, Pre/Post | Table sort nije u URL |
| **RQ351** | Inventory | „Osveži“ = `window.location.reload()` |
| **RQ352** | Daily Sales | `filtered_out` kad je store selektovan i nema prodaje |
| **RQ353** | Inventory | Insight→detail path fake zero nabavna cena |
| **RQ354** | Supplier (redirect) | `brojDobavljaca ?? 0` u export metadata |
| **RQ355** | Color | Empty state = `insufficient_data` samo zbog quality notes |
| **RQ356** | Pre/Post | Engleske skraćenice na signal karticama (DID, OOS…) |
| **RQ357** | Inventory | Paginacija/search/compare nisu u URL |
| **RQ358** | Shoe Type | `brojTipovaObuce ?? 0` u export metadata |

---

## Potencijalna unapređenja (product)

| Tema | Prompt | Napomena |
|---|---|---|
| Autoritativni KPI agregati | RQ331, RQ346 | Cockpit brojevi moraju biti filter-wide ili jasno označeni |
| Chart/table decoupling | RQ336 | Kritičan UX bug — sort ne sme kvareti grafikon |
| Stale refetch standard | RQ340, RQ341, RQ342 | Operacije grupa treba isti partial-failure ugovor |
| Deljivi linkovi | RQ350, RQ357 | Pilot demo i timska saradnja |
| Supplier redirect trust | RQ334, RQ348, RQ354 | Operacije meni vodi na Supplier — trust mora biti konzistentan |

---

## Preporučeni redosled promocije (round 1 + 2 + 3)

```text
Trust first:     RQ312 → RQ313 → RQ314 → RQ331 → RQ332 → RQ334
Chart bug:       RQ336
Quick wins:      RQ302 → RQ303 → RQ304 → RQ307
Refetch/race:    RQ340 → RQ341 → RQ342 → RQ335
Localization:    RQ301 → RQ306 → RQ325 → RQ356
URL/UX:          RQ317 → RQ318 → RQ350 → RQ326 → RQ327 → RQ357
Inventory depth: RQ308 → RQ321 → RQ322 → RQ323 → RQ324 → RQ351 → RQ353
Pre-Nivelacija:  RQ315 → RQ344 → RQ343
Pre/Post depth:  RQ333 → RQ347 → RQ338 → RQ339 → RQ328 → RQ330
Polish:          RQ305 → RQ309 → RQ310 → RQ311 → RQ349 → RQ352 → RQ355 → RQ358
```

---

## Validacija (round 3 audit)

| Check | Rezultat |
|---|---|
| `git pull origin main` | up to date |
| Operacije focused tests | **188/188 passed** (prior rounds) |
| `check-prompt-queues.mjs` | pending pre-commit |
| Live browser/backend | not run |

---

## Kako pokrenuti

1. Queue owner promoviše jedan `WAITING` prompt (preporuka: **RQ331** ili **RQ336**).
2. Agent claim prema `PROMPT_QUEUE_PROTOCOL.md`.
3. Puni prompt tekst u `ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`.

**Current READY prompt: none**
