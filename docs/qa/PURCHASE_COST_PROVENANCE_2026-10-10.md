# RQ601 — poreklo nabavne cene i poverenje istorijske marže

Datum: 2026-10-10
Status: **PARTIAL — kodna zaštita je isporučena, ali 20 originalnih kalkulacionih dokaza i dalje nedostaje**
Scope: Access import #23, analitička marža, cost-quality signal i sale-line istorijski fallback

## Zaključak

Prethodni SQL je svaku pozitivnu vrednost `prodaja_stavke.nabavna_cena` prikazivao kao `source_sale_line`. To nije dokazivalo poreklo: vrednost je mogla biti naknadni master backfill ili stari red bez ikakvog metapodatka.

Kod sada čuva i prenosi eksplicitno poreklo:

| Vrednost | Značenje | Može biti istorijska marža? |
|---|---|---:|
| `source_sale_line` | pozitivna nabavna cena pročitana iz izvornog Access reda prodajne stavke | da, uz ugovor izvora |
| `confirmed_calculation` | vlasnički potvrđena kalkulaciona/ulazna cena | da |
| `master_nabavnacena_din_backfill` | približni backfill iz master `NabavnaCenaDin` | ne; procena |
| `master_nabavnacena_backfill` | približni backfill iz legacy master `NabavnaCena` | ne; procena |
| `unknown_provenance` | stari red, NULL/blank poreklo ili nedokaziv izvor | ne |

Stare numeričke vrednosti nisu prepisane niti obrisane. Ako im poreklo nije sačuvano, čitaju se kao `unknown_provenance`; eventualni trenutni master fallback ostaje eksplicitno procena. `Prenos ulaz` i `Povrat kupca` ostaju neautoritativni inbound signali, a samo `Ulaz robe` ima `inbound_receipt_signal` oznaku.

## Pre/posle

| Oblast | Pre | Posle |
|---|---|---|
| Import direktne sale-line cene | upisana samo numerička vrednost | upisana numerička vrednost + `source_sale_line` |
| Master backfill | `COALESCE(NabavnaCenaDin, NabavnaCena)` bez porekla | ista numerička vrednost + odgovarajući `*_backfill` label |
| Stari pozitivan red bez porekla | pogrešno tretiran kao istorijski source line | `unknown_provenance`; ne ulazi kao potvrđen sale-line trošak |
| NULL/nepoznat trošak | mogao se izgubiti u fallback interpretaciji | ostaje NULL/unknown; nikad nije 0 |
| Marža | pozitivna vrednost je automatski imala istorijski prioritet | istorijski prioritet imaju samo potvrđeni provenance labeli; inače se koristi procenjeni fallback ili nema troška |
| Inventory sale-line vrednost | neproveren sale-line trošak mogao je biti izvor | sale-line istorijski izvor je ograničen na potvrđeno poreklo; master snapshot semantika nije menjana |

## Promene potrošača

- `AnalyticsMarginPolicy` centralno proverava provenance za marže i označava fallback izvor.
- Supplier Sales, Shoe Type/Color, Daily/Detail, Insight Studio, Supplier Decision Hub i Vendor Sales/Nivelacija prosleđuju provenance.
- Cached Top Products/Product Decision SQL više ne koristi pozitivan sale-line broj bez potvrđenog porekla.
- `InventoryValuationSupport` više ne uzima neproveren sale-line broj kao istorijski kapitalni trošak.
- `AnalyticsDataQualityHealthService` ne smatra pozitivan, ali neproveren sale-line broj potvrđenim istorijskim troškom; može koristiti master samo kao eksplicitnu procenu.
- Operativni POS upis u `ProdajaRepository` sada čuva isti provenance ugovor, a idempotentni `DatabaseInitializer` bootstrap dodaje novu kolonu na starim bazama.
- Pre-nivelacija i inventory export koji rade nad master/snapshot cenom nisu preformulisani; njihove formule i postojeća semantika nisu menjane.

## Read-only dokaz batch-a #23

Izvršeno nad lokalnim Docker PostgreSQL `trendplus-postgres`, bazom `trendplus`, PostgreSQL 16.13, bez upisa:

- batch `23`: `completed`, `DataOrigin=access`, `TotalImported=237886`, `RowsWritten=243253`;
- Access zaglavlja: `5530`, Access stavke: `67092`;
- Access retail stavke: `66233`;
- trenutno pozitivna `nabavna_cena`: `65728`;
- nulta: `505`; negativna: `0`; NULL: `0`;
- prihod redova bez pozitivnog troška: `1.717.030,00 RSD`;
- ukupni retail prihod u toj proveri: `234.829.573,64 RSD`.

Lokalna poslovna baza još nema kolonu `prodaja_stavke.purchase_cost_provenance`; migracija zato nije primenjena nad njom. Zbog toga se iz trenutnog istorijskog stanja ne može dokazati nijedna populacija kao istorijski provenance (`0` dokazivih po dostupnoj šemi), iako numeričke vrednosti ostaju sačuvane. Referentne brojke iz prethodnog addenduma (`65.732 / 501 / 1.711.050 RSD`) razlikuju se od trenutnog read-only preseka za 4 reda i 5.980 RSD; rezultat nije veštački prilagođavan.

Ovo nije zahtev za masovni repair. Migracija je aditivna i namenjena budućem importu/deployment-u; ne menja stare numeričke vrednosti. Za postojeće redove bez dokaza ostaje potreban owner/source review.

## 20 originalnih kalkulacionih stavki

Worksheet i owner gate ostaju u `docs/qa/PURCHASE_COST_SCALE_RECONCILIATION_2026-10-09.md`. Popunjenost originalnim brojem/datumom kalkulacije je `0/20`. Nije izmišljena nijedna cena, jedinica mere, datum ili izvor. RQ601 zato ostaje `PARTIAL`.

## PostgreSQL i regresioni dokaz

Obavezni test koristi stvarni lokalni PostgreSQL na `127.0.0.1:5434`, kreira izolovanu privremenu bazu, izvršava SQL i briše samo tu bazu nakon testa:

- `PurchaseCostScaleReconciliationTests`: **1/1 pass**, `failed=0`, `skipped=0`;
- contract test: **1/1 pass**;
- završna objedinjena RQ601 regresija iz svežeg build-a: **176/176 pass**, `failed=0`, `skipped=0`, `notExecuted=0`;
- Release build: **0 errors**, postojeća upozorenja ostaju bez novih product error-a.

TRX dokazi:

- `F:\trendplus-rq601-build\trx-rq601-pg-final6\RQ601-pg-final6.trx`;
- `F:\trendplus-rq601-build\trx-rq601-final5\RQ601-final5.trx`;
- `F:\trendplus-rq601-build\trx-rq601-health\RQ601-health.trx` — 12/12 pass.

Fixture pokriva: direktni source-line trošak, master RSD/legacy backfill, stari broj bez provenance, potpuno NULL, inbound klasifikaciju i zabranu da unknown postane potvrđen trošak ili nula.

## Ograničenja i sledeći gate

- Nije pokrenut novi Access import.
- Nije menjana lokalna poslovna baza niti produkcija.
- Nije promenjena analitička formula niti je određena konačna master skala.
- Preostali owner gate: dostaviti i potvrditi 20 originalnih kalkulacionih stavki; zatim odvojeno odlučiti da li je potreban repair postojećih redova ili samo budući import metadata.
