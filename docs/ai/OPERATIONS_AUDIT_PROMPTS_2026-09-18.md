# Operacije — audit nalaza i queue promptovi (2026-09-18)

Queue owner: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`
Base: `origin/main` (post-`RQ300`)
**Ukupno WAITING promptova iz ovog audita: RQ301–RQ330 (30)**

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
| Round 1 audit 2026-09-18 | **RQ301–RQ311** (WAITING, u queue) |

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

## Round 2 — RQ312–RQ330 (NOVO — trust, URL, filter UX, unapređenja)

### P1 — trust / fake data

| ID | Ekran | Problem |
|---|---|---|
| **RQ312** | Inventory | Signal window (`fromDate`/`toDate`) zamrznut na mount — stale API opseg |
| **RQ313** | Inventory | Insights failure ne briše stare podatke; badge `?? 0` → lažni „0 artikala 90+“ |
| **RQ314** | Pre/Post | Driver summary `trustedMetric ?? 0` → **fake 0 RSD** za winner/risk SKU |

### P2 — URL state, filter UX, silent failures

| ID | Ekran(i) | Problem / unapređenje |
|---|---|---|
| **RQ315** | Pre-Nivelacija | Trust header period uvek `null` |
| **RQ316** | Pre-Nivelacija | Period empty reason različit u header vs empty state |
| **RQ317** | Pre/Post | Focus filter nije u URL; reset na svaki load |
| **RQ318** | Color, Shoe, Pre/Post, Inventory | List filteri nisu URL-backed (faza po ekranu) |
| **RQ319** | Shoe vs Color/Pre/Post | Auto-apply vs „Primeni filtere“ — nekonzistentno |
| **RQ320** | Color, Pre/Post | Draft datumi ≠ trust header period pre Apply |
| **RQ321** | 5 ekrana | Store load failure → tihi prazan dropdown |
| **RQ322** | Inventory | Store bootstrap samo `console.error` |
| **RQ323** | Inventory | Secondary paneli zadržavaju stale podatke na partial failure |
| **RQ325** | Color, Pre/Post, Inventory | Preostali engleski stringovi (snapshot, alerts) |

### P3 — polish / shareability

| ID | Ekran | Problem / unapređenje |
|---|---|---|
| **RQ324** | Inventory | SKU detail size-curve greška = prazan panel |
| **RQ326** | Pre-Nivelacija | Sort nije u URL (RQ299 pokriva filter/focus/page) |
| **RQ327** | Daily Sales | Sort nije u URL |
| **RQ328** | Pre/Post | Expanded vendor se gubi na refetch |
| **RQ329** | Shoe Type | `truncationLabel` nikad ne trigeruje (dead UX) |
| **RQ330** | Pre/Post | Focus filter sakriva redove bez „N od M“ konteksta |

---

## Potencijalna unapređenja (product, ne bug)

| Tema | Prompt | Napomena |
|---|---|---|
| Deljivi linkovi sa filterima | RQ317, RQ318, RQ326, RQ327 | Poboljšava pilot demo i timsku saradnju |
| Konzistentan filter UX | RQ319, RQ320 | Smanjuje kognitivno opterećenje u Operacije grupi |
| Eksplicitna store-load degradacija | RQ321, RQ322 | Povezuje se sa RQ279 supplier stale pattern |
| Fokus + expansion UX | RQ328, RQ330 | Pre/Post dubinski pregled dobavljača |

---

## Preporučeni redosled promocije (round 1 + 2)

```text
Trust first:  RQ312 → RQ313 → RQ314
Quick wins:   RQ302 → RQ303 → RQ304 → RQ307
Localization: RQ301 → RQ306 → RQ325
URL/UX:       RQ317 → RQ318 → RQ326 → RQ327
Inventory:    RQ308 → RQ321 → RQ322 → RQ323 → RQ324
Polish:       RQ305 → RQ309 → RQ310 → RQ311 → RQ328–RQ330
```

---

## Validacija (round 2 audit)

| Check | Rezultat |
|---|---|
| `git pull origin main` | up to date |
| Operacije focused tests | **188/188 passed** |
| `check:encoding` | pass (prior run) |
| `check-prompt-queues.mjs` | pass (469 tasks) |
| Live browser/backend | not run |

---

## Kako pokrenuti

1. Queue owner promoviše jedan `WAITING` prompt (preporuka: **RQ312** ili **RQ302**).
2. Agent claim prema `PROMPT_QUEUE_PROTOCOL.md`.
3. Puni prompt tekst u `ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`.

**Current READY prompt: none**
