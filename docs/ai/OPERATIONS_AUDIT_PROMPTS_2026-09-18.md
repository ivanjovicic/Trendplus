# Operacije — audit nalaza i queue promptovi (2026-09-18)

Queue: `direct-user-request`  
Base: `origin/main` (post-`RQ300`, SHA verified at audit time)  
Canonical queue owner: `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md` → **RQ301–RQ311** (`WAITING`)

## Mapa ekrana (8 stavki menija Operacije)

| # | Meni | Ruta | Komponenta | Napomena |
|---|---|---|---|---|
| 1 | Zalihe i dopuna | `/analytics/inventory` | `InventoryPage.tsx` | Samostalan |
| 2 | Prodaja po dobavljačima | `/analytics/supplier-sales-stats` | `SupplierSalesStatsRedirect` | → `/analytics/supplier?tab=overview` |
| 3 | Prodaja po tipu obuće | `/analytics/shoe-type-sales-stats` | `ShoeTypeSalesStatsPage.tsx` | Samostalan |
| 4 | Prodaja po smeni i dobavljačima | `/analytics/daily-sales` | `DailySalesStatsPage.tsx` | Samostalan |
| 5 | Pre/Posle nivelacije | `/analytics/nivelacije-pre-post` | `ProdajaPrePostNivelacijePage.tsx` | Samostalan |
| 6 | Prodaja po boji artikla | `/analytics/color-sales-stats` | `ColorSalesStatsPage.tsx` | Samostalan |
| 7 | Prioriteti nivelacije | `/analytics/pre-nivelacija-prioriteti` | `PreNivelacijaPriorityPage.tsx` | Samostalan |
| 8 | Dobavljači i tipovi obuće | `/analytics/dobavljaci-tipovi-obuce` | `SupplierFootwearAnalyticsRedirect` | → `/analytics/supplier?tab=assortment` |

## Već zatvoreno — ne duplirati

**RQ265–RQ300** (Operacije audit 2026-09-15 + follow-up do 2026-09-18) pokriva m.in.:

- prazna stanja, freshness, heading hijerarhiju, legacy supplier redirecte
- scope reload (Daily, Inventory, Pre-Nivelacija)
- inventory KPI/search contract, total value, export window
- supplier embedded composition, filter scope/stale/previous-period
- shoe/color/daily/pre-post/pre-nivelacija numeriku, status identitet, URL state

Fokusirani Operacije testovi na `main`: **188/188 passed**.

## Novi queue promptovi (RQ301–RQ311)

| ID | P | Feature family | Kratak opis |
|---|---|---|---|
| **RQ301** | P1 | `operations-inventory-serbian-copy` | Inventory: engleski cockpit copy → srpski |
| **RQ302** | P1 | `operations-route-smoke` | 6/8 Operacije ruta van App smoke matrice |
| **RQ303** | P1 | `daily-sales-localization` | Daily Sales: engleski „Check“ badge + mixed QA copy |
| **RQ304** | P1 | `color-sales-detail-label-parity` | Color: „Decision score“ vs „Skor odluke“ |
| **RQ305** | P2 | `operations-supplier-ia-clarity` | Meni vs Supplier tab alias — IA zbunjenost |
| **RQ306** | P2 | `operations-diacritics-pass` | ASCII umesto dijakritika (`Greska`, `Pokrice`, …) |
| **RQ307** | P2 | `shoe-type-impact-label` | Shoe Type: engleski „Low signal“ |
| **RQ308** | P2 | `inventory-period-provenance` | Inventory trust header period uvek null |
| **RQ309** | P3 | `operations-nav-icons` | Tri ista ShoppingBag ikone u Operacije |
| **RQ310** | P3 | `operations-test-route-alignment` | Testovi mount-uju `/analitika/...` umesto `/analytics/...` |
| **RQ311** | P3 | `operations-guardrail-cleanup` | Guardrail violations za score/reliability |

Puni prompt tekst (Problem, Evidence, Scope, Do, Tests, Acceptance, Dependencies) je u **`ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`**.

## Detaljni nalazi po ekranu

### 1. Zalihe i dopuna (`InventoryPage`)

| Tip | Nalaz | Queue |
|---|---|---|
| UX/copy | Trust header „Inventory analytics“, KPI „Stock cover risk“, „Low cover SKU“, „Good sell-through SKU“ | RQ301 |
| UX/copy | Child paneli: „Ucitavam workflow…“, „Size curve“, „aging“, „poredjenje“, „stampu“ | RQ301, RQ306 |
| Trust | `periodFrom/To={null}` bez objašnjenja snapshot vs signal window | RQ308 |
| Komponente | `InventoryPriorityPanels`: „Artrikli“, „vrednoscu“ bez dijakritika | RQ306 |

### 2–8. Ostali Operacije ekrani

| Ekran | Nalaz | Queue |
|---|---|---|
| Supplier redirects (2 stavke) | Meni implicira samostalan izveštaj; aktivna nav → Odluke „Pregled dobavljača“ | RQ305 |
| Daily Sales | Badge „Check“, footnote „mismatch/total kolone/top+others“ | RQ303, RQ306 |
| Shoe Type | `Low signal` u nivelacija impact koloni; `Pokrice` bez dijakritika | RQ307, RQ306 |
| Color Sales | Detail „Decision score“ ≠ tabela „Skor odluke“; guardrail hit | RQ304, RQ311 |
| Pre/Post | „Pre nivo kolicina“ ASCII; guardrail reliability hit | RQ306, RQ311 |
| Pre-Nivelacija | Guardrail score/reliability hits (numeric robustness DONE u RQ300) | RQ311 |
| Svi (routing) | Nisu u `CORE_ANALYTICS_ROUTE_DEFINITIONS` osim inventory | RQ302 |
| Svi (testovi) | Page specs na `/analitika/...` list path | RQ310 |
| Nav | 3× ShoppingBag ikona u Operacije | RQ309 |

## Preporučeni redosled promocije

1. **RQ302** — jeftin, sprečava routing regresije  
2. **RQ303 + RQ304 + RQ307** — mali copy fixevi, visoka vidljivost  
3. **RQ301 + RQ306** — širi lokalizacioni pass  
4. **RQ305** — zahteva product izbor (badge vs standalone rute)  
5. **RQ308 → RQ309 → RQ310 → RQ311** — provenance, polish, test harness, guardrails  

## Validacija audita

| Check | Rezultat |
|---|---|
| `git fetch origin main && git pull` | up to date |
| Operacije focused tests (15 files) | **188/188 passed** |
| `npm run check:encoding` | pass |
| `npm run check:analytics-guardrails` | 5 Operacije-related violations (exit 0) |
| Live browser / backend | not run |

## Kako pokrenuti rad

```text
# Queue owner: promote one prompt
# Current READY prompt: none

# Agent: claim first promoted prompt per PROMPT_QUEUE_PROTOCOL.md
# Example after RQ302 promotion:
npm run test -- --run src/__tests__/AppAnalyticsRoutes.spec.tsx
```

Queue status: **RQ301–RQ311 = WAITING**, **Current READY prompt: none**.
