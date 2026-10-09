# RQ601 — sravnjenje razmere nabavne cene

Datum: 2026-10-09  
Status: **PARTIAL — dijagnostika i worksheet su spremni; vlasnički uzorak iz kalkulacija nije dostavljen**  
Izvor odluke: `docs/ai/ANALYTICS_OWNER_DECISIONS_RQ601_RQ603_2026-10-09.md`

## Zaključak za vlasnika

Postojeći istorijski dokaz je dovoljan da se master trošak tretira kao **sumnjiv i nesertifikovan**, ali nije dovoljan da se utvrdi da li je uzrok pogrešno polje, valuta, jedinica mere ili druga semantika izvora. Zato:

- vrednost zalihe u RSD ostaje **provisional / not certified**;
- nijedna nepoznata ili nedostajuća nabavna cena nije pretvorena u nulu;
- mapiranje importa i redosled izvora troška nisu menjani;
- konačni verdict `master scale correct` ili `master scale defective with cause` čeka 20 originalnih kalkulacionih stavki.

Poznati istorijski signal iz RQ576/re-audita: 3.566 komada zalihe je ranije prikazano kao 93.389 RSD, odnosno približno 26,19 RSD po komadu. Prodajne stavke istog skupa dobavljača imale su približno 1.200–4.700 RSD po komadu. Za artikal 21644 (`70126.1`, UnA plus) master trošak je bio 245 RSD, dok je dobavljački istorijski trošak bio približno 3.038 RSD po komadu; odnos je oko 8,1%, ispod postojećeg 25% praga za sumnjivu razmeru.

Ovo je dokaz anomalije koju treba proveriti, ne dokaz ispravnog alternativnog troška.

## Reproduktivni read-only izveštaj

Skripta: `tools/purchase-cost-scale-reconciliation.sql`.

Skripta se izvršava nad operativnom Trendplus PostgreSQL bazom i za svaki artikal vraća:

- master `NabavnaCena` i `NabavnaCenaDin`;
- poslednji pouzdan ulazni jedinični trošak iz `DnevnikPromena`;
- poslednji trošak prodajne stavke, odvojeno kao sirova vrednost i efektivna vrednost;
- poreklo troška prodajne stavke: `source_sale_line`, `master_nabavnacena_din_backfill`, `master_nabavnacena_backfill` ili `unknown`;
- prodajnu cenu, stanje, odnose između izvora i vrednost zalihe po svakoj osnovi;
- `master_scale_suspicious=true` kada je pozitivni master trošak ispod 25% pozitivnog troška prodajne stavke.

Upit nema `INSERT`, `UPDATE`, `DELETE`, `CREATE` niti produkcione upise. Sintetički Access ulazi sa iznosom nula ne smatraju se pouzdanim prijemom robe. Nedostajući trošak ostaje `NULL`.

## Kako napraviti uzorak od 20 artikala

1. Izvesti rezultat SQL skripte u CSV.
2. Uzeti redove redosledom iz upita (najveća poznata vrednost zalihe prva).
3. Izabrati 20 artikala iz najmanje tri dobavljača; najviše osam artikala jednog dobavljača.
4. Za svaki red pronaći originalnu kalkulacionu stavku za istu jedinicu mere, količinu i najbliži datum relevantnog prijema.
5. Popuniti samo kolonu `Kalkulacija nabavna cena RSD`; ne kopirati Trendplus vrednost kao referencu.
6. Izračunati grešku po artiklu i ukupnu ponderisanu grešku po količini. PASS je moguć tek kada je odobrena osnova unutar ±5% vlasničkog uzorka ili je rezidual pojedinačno objašnjen.

## Worksheet — vlasnik popunjava originalne kalkulacije

| # | Artikal ID | PLU | Dobavljač | Stanje | Master NC | Master NC Din | Poslednji ulaz | Prodajna stavka | Poreklo | Kalkulacija nabavna cena RSD | Dokaz (broj/datum kalkulacije) | Greška % | Napomena |
|---:|---:|---|---|---:|---:|---:|---:|---:|---|---:|---|---:|---|
| 1 | 21644 | 70126.1 | UnA plus | — | 245 | — | — | ≈3.038 dobavljački istorijski prosek | istorijski audit; tačna linija čeka SQL izvoz |  |  |  | poznati kandidat |
| 2 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 3 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 4 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 5 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 6 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 7 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 8 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 9 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 10 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 11 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 12 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 13 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 14 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 15 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 16 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 17 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 18 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 19 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |
| 20 |  |  |  |  |  |  |  |  |  |  |  |  | kandidat iz SQL izvoza |

## Sažetak koji se popunjava posle izvoza

| Mera | Trenutno stanje |
|---|---|
| Broj artikala u rezultatu | nije izmeren — nema odobrene istorijske DB konekcije u ovom run-u |
| Udeo prodajnih stavki sa source troškom | nije izmeren |
| Udeo prodajnih stavki sa master backfill-om | nije izmeren |
| Udeo artikala sa nepoznatim troškom | nije izmeren |
| Medijana `master / sale-line` po dobavljaču | nije izmerena |
| Vrednost zalihe po ulaznom trošku | nije izmerena |
| Vrednost zalihe po sale-line trošku | nije izmerena |
| Vlasnički uzorak | 0/20 popunjeno |
| Konačna ocena razmere | **UNRESOLVED — master trošak ostaje sumnjiv i nesertifikovan** |

## Fixture dokaz

`Api.Tests/PurchaseCostScaleReconciliationTests.cs` podiže izolovani PostgreSQL fixture sa:

- master troškom 245 RSD;
- pouzdanim ulaznim troškom 2.900 RSD;
- izvornim troškom prodajne stavke 3.000 RSD;
- zasebnim master-backfill redom;
- redom bez ijednog troška.

Test proverava poreklo svake vrednosti, sumnjivu razmeru, backfill oznaku i da nepoznati trošak/vrednost zalihe ostaju `NULL`.

## Sledeći tačan korak

Izvršiti SQL nad odobrenom istorijskom bazom, nalepiti 20 odabranih redova u worksheet i popuniti originalne kalkulacione troškove. Tek tada je dozvoljen verdict o razmeri i eventualna minimalna promena mapiranja/precedence-a sa pre/posle dokazom. Svež import nije uslov za ovu istorijsku proveru, ali jeste uslov za tvrdnju o trenutnom kapitalu zaliha.
