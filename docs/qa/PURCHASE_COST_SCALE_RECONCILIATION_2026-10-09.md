# RQ601 — sravnjenje razmere nabavne cene

Datum: 2026-10-10 (read-only istorijska dopuna)
Status: **PARTIAL — stvarna lokalna baza je proverena; vlasnički uzorak iz originalnih kalkulacija nije dostavljen**
Izvor odluke: `docs/ai/ANALYTICS_OWNER_DECISIONS_RQ601_RQ603_2026-10-09.md`

## Zaključak za vlasnika

Postojeći istorijski dokaz je dovoljan da se master trošak tretira kao **sumnjiv i nesertifikovan**, ali nije dovoljan da se utvrdi da li je uzrok pogrešno polje, valuta, jedinica mere ili druga semantika izvora. Zato:

- vrednost zalihe u RSD ostaje **provisional / not certified**;
- nijedna nepoznata ili nedostajuća nabavna cena nije pretvorena u nulu;
- mapiranje importa i redosled izvora troška nisu menjani;
- konačni verdict `master scale correct` ili `master scale defective with cause` čeka 20 originalnih kalkulacionih stavki.

Read-only izvršavanje 2026-10-10 nad batch-om #23 potvrđuje 3.566 komada Access zalihe: 93.389 RSD po legacy `NabavnaCena` naspram 9.572.063 RSD po `NabavnaCenaDin`. Za artikal 21644 (`PLU=3854`, naziv `70126.1`, UnA plus) master legacy trošak je 245 RSD, a `NabavnaCenaDin`/sale-line osnova 2.875 RSD. To je odnos 8,52%, ispod praga 25% za sumnjivu razmeru.

Ovo je dokaz anomalije koju treba proveriti, ne dokaz ispravnog alternativnog troška.

## Reproduktivni read-only izveštaj

Skripta: `tools/purchase-cost-scale-reconciliation.sql`; izvršeno nad lokalnim Docker PostgreSQL `trendplus-postgres` / `trendplus`.

Skripta se izvršava nad operativnom Trendplus PostgreSQL bazom i za svaki artikal vraća:

- master `NabavnaCena` i `NabavnaCenaDin`;
- poslednji pouzdan ulazni jedinični trošak iz `DnevnikPromena`;
- poslednji trošak prodajne stavke, odvojeno kao sirova vrednost i efektivna vrednost;
- poreklo troška prodajne stavke: `source_sale_line`, `master_nabavnacena_din_backfill`, `master_nabavnacena_backfill` ili `unknown`;
- prodajnu cenu, stanje, odnose između izvora i vrednost zalihe po svakoj osnovi;
- `master_scale_suspicious=true` kada je pozitivni master trošak ispod 25% pozitivnog troška prodajne stavke.

Stvarni rezultat run-a: 12.428 artikala; poslednji inbound trošak postoji za 6.131 (49,33%), a poslednji efektivni sale-line trošak za 7.280 (58,58%). Na Access retail stavkama ima 66.233 redova; 65.732 imaju poznat trošak, 501 ostaje `NULL`. Poznata populacija nosi 233.118.523,64 RSD, a nepoznata 1.711.050,00 RSD.

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
| 1 | 17929 | 1 | Antilop | 23 | 57 | 7.040 | — | 7.040 | source_sale_line |  |  |  | M. Poluduboka cipela; Komision |
| 2 | 21434 | 3716 | Rieker | 12 | 39 | 4.600 | 10.490 | 4.600 | source_sale_line |  |  |  | Trend PLUS 1 |
| 3 | 21491 | 3763 | KGFASHION | 12 | 34 | 3.992 | 8.990 | 3.992 | source_sale_line |  |  |  | Trend PLUS 1 |
| 4 | 21626 | 3549 | UnA plus | 24 | 36 | 4.226 | — | 4.226 | source_sale_line |  |  |  | Trend PLUS 1 |
| 5 | 21460 | 3739 | Planika | 10 | 35 | 4.097 | 8.990 | 4.097 | source_sale_line |  |  |  | Trend PLUS 1 |
| 6 | 21496 | 3768 | Planika | 10 | 36 | 4.321 | 8.990 | 4.321 | source_sale_line |  |  |  | Trend PLUS 1 |
| 7 | 21642 | 1944 | UnA plus | 38 | 19 | 2.253 | — | 2.253 | source_sale_line |  |  |  | Trend PLUS 1 |
| 8 | 21481 | 3752 | Florida | 11 | 32 | 3.760 | 7.490 | 3.760 | source_sale_line |  |  |  | Trend PLUS 1 |
| 9 | 21650 | 3860 | UnA plus | 31 | 22 | 2.640 | — | 2.640 | source_sale_line |  |  |  | Trend PLUS 1 |
| 10 | 21477 | 3749 | Florida | 21 | 32 | 3.760 | — | 3.760 | source_sale_line |  |  |  | Trend PLUS 1 |
| 11 | 21480 | 3751 | Florida | 20 | 32 | 3.760 | — | 3.760 | source_sale_line |  |  |  | Trend PLUS 1 |
| 12 | 21479 | 3750 | Florida | 20 | 32 | 3.760 | — | 3.760 | source_sale_line |  |  |  | Trend PLUS 1 |
| 13 | 21473 | 3745 | Florida | 20 | 32 | 3.760 | — | 3.760 | source_sale_line |  |  |  | Trend PLUS 1 |
| 14 | 21478 | 3753 | Florida | 10 | 32 | 3.760 | 7.490 | 3.760 | source_sale_line |  |  |  | Trend PLUS 1 |
| 15 | 21602 | 3466 | Leon | 13 | 21 | 2.480 | 5.200 | 2.480 | source_sale_line |  |  |  | Trend PLUS 1 |
| 16 | 21627 | 3547 | UnA plus | 16 | 33 | 3.872 | — | 3.872 | source_sale_line |  |  |  | Trend PLUS 1 |
| 17 | 21621 | 3842 | KGFASHION | 12 | 41 | 4.990 | — | 4.990 | source_sale_line |  |  |  | Trend PLUS 1 |
| 18 | 21465 | 3742 | Moda Pele | 13 | 39 | 4.600 | — | 4.600 | source_sale_line |  |  |  | Trend PLUS 1 |
| 19 | 21208 | 3516 | Planika | 8 | 28 | 3.300 | 6.990 | 3.300 | source_sale_line |  |  |  | Trend PLUS 1 |
| 20 | 21644 | 3854 | UnA plus | 19 | 245 | 2.875 | — | 2.875 | source_sale_line |  |  |  | naziv 70126.1; Trend PLUS 1 |

## Sažetak koji se popunjava posle izvoza

| Mera | Trenutno stanje |
|---|---|
| Broj artikala u rezultatu | 12.428 |
| Udeo artikala sa poslednjim inbound troškom | 6.131/12.428 = 49,33% |
| Udeo artikala sa poslednjim sale-line troškom | 7.280/12.428 = 58,58% |
| Access retail stavke sa poznatim troškom | 65.732/66.233 = 99,24% |
| Access retail promet sa poznatim troškom | 233.118.523,64/234.829.573,64 = 99,27% |
| Medijana `legacy master / sale-line` | 0,8621%; 7.256/7.276 parova ispod 25% |
| Medijana `NC Din / sale-line` | 1,0000% |
| Vrednost Access zalihe po legacy NC | 93.389,00 RSD |
| Vrednost Access zalihe po NC Din | 9.572.063,00 RSD |
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

## Istorijska dopuna 2026-10-10

Detaljno sravnjenje batch-a #23, 20 kandidata i screen oracle nalazi se u `docs/qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md`. Nema dovoljno izvornog dokaza za zatvaranje RQ601: vlasnik mora dostaviti originalne kalkulacione stavke za gore navedene artikle. Do tada su obe skale prikazane kao odvojene scenarije, a ne kao potvrđena poslovna istina.

## Sledeći tačan korak

Izvršiti SQL nad odobrenom istorijskom bazom, nalepiti 20 odabranih redova u worksheet i popuniti originalne kalkulacione troškove. Tek tada je dozvoljen verdict o razmeri i eventualna minimalna promena mapiranja/precedence-a sa pre/posle dokazom. Svež import nije uslov za ovu istorijsku proveru, ali jeste uslov za tvrdnju o trenutnom kapitalu zaliha.
