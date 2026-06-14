# Analytics Performance Budgets

Ovaj dokument definiše ciljne budžete performansi za pilot analytics tokove.
Budžeti služe za readiness procenu, demo pripremu i prioritizaciju optimizacija.
Ne menja SQL, cache kod ili refresh implementaciju.

## Target table

| Endpoint family | Warm target | Cold target | Cache expected | Risk | Notes |
|---|---|---|---|---|---|
| Dashboard / bootstrap | `<2s` | `<5s` | visok cache hit za agregate i bootstrap payload | executive početni ekran deluje nepoverljivo ako kasni | meriti payload trajanje, cache hit/miss, row count i correlationId |
| Product Decision Center | `<3s` | `<8s` | očekivan cache za read-heavy preglede i pomoćne lookup podatke | operater gubi tok rada ako odluka čeka predugo | meriti filter scope, row count, timeout i correlationId |
| Supplier scorecard | `<3s` | `<8s` | očekivan cache za scorecard izračune i report snapshot read | fallback i stale podaci mogu izgledati kao business signal | meriti dataset size, fallback signal, cache hit/miss i duration |
| Inventory analytics | `<3s` | `<8s` | očekivan cache za read modele i summary panele | spor inventory ekran blokira replenishment/OOS odluke | meriti row count, alert volume, timeout i correlationId |
| Data Quality | `<3s` | `<10s` | delimičan cache; intake i health mogu imati hladniji path | spor data quality ekran usporava onboarding i incident response | meriti section duration, import state, row count i cache miss rate |
| Pre/Post nivelacija | `<4s` | `<12s` | umereno očekivan cache, posebno za read-only analize | veći proračuni i istorijski opseg lako probijaju demo očekivanja | meriti period size, affected rows, timeout i correlationId |
| Reports | `<5s` cached | `<15s` | jak cache ili snapshot očekivan za dokument-style izlaz | prodajni/report flow deluje nestabilno ako export traje predugo | meriti render duration, snapshot hit/miss, payload size i correlationId |

## What to measure

- duration po endpoint family i po glavnom view-u
- cache `hit/miss` gde postoji cache sloj
- row count ili drugi signal volumena podataka
- timeout i failure rate
- `correlationId` za povezivanje sa backend logovima

## Demo rule

Pre demo-a treba uraditi warm cache i/ili refresh ako postoji rizik da je ključni analytics tok hladan ili stale.
Ako poslednji uspešan refresh nije svež ili je cache hladan, demo treba pripremiti unapred umesto oslanjanja na first-load ponašanje.

## Demo blockers

- dashboard cold path prelazi `5s`
- data quality cold path prelazi `10s`
- report cold path prelazi `15s`
- refresh status je stale ili critical pre demo toka
- ključni read modeli nemaju očekivani cache hit za drugi prolaz

## Top optimization candidates

- dashboard/bootstrap payload agregacija i smanjenje početnog round-trip broja
- product/supplier read modeli sa velikim row count-om i slabim cache hit-om
- data quality intake i health pozivi koji zavise od skupljih cold path-ova
- report snapshot strategija za dokument-style izlaze
- pre/post nivelacija upiti sa širokim periodom i većim istorijskim dataset-om

## Risk notes

- Budžeti su operativni ciljevi, ne garantovane SLO obaveze.
- Cold target promašaj pre demo-a znači da treba pripremiti refresh/cache unapred.
- Ako nema merenja `duration`, `row count` ili `correlationId`, incident i tuning ostaju spori i neprecizni.
