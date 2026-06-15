# Daily Sales UX Audit

Datum: 2026-06-15  
Repo: ivanjovicic/Trendplus  
Scope: `Klijent/clientapp/src/pages/DailySalesStatsPage.tsx`, `Klijent/clientapp/src/services/dailySalesStatsApi.ts`, `Klijent/clientapp/src/pages/DailySalesStatsPage.css`

## Sažetak

`Prodaja po smeni` ima dosta korisnih analiza, ali trenutno više liči na analystski radni sto nego na ekran koji brzo vodi retail operatera do odgovora:

- šta se promenilo u odnosu na prethodni period,
- da li podacima može da se veruje,
- koji deo dana traži reakciju,
- i šta konkretno treba proveriti sledeće.

Glavni UX problem nije nedostatak signala, nego to što su:
- freshness i trust skriveni ili nepostojeći na vrhu ekrana,
- quality signal-i potisnuti iza collapsible panela,
- charts brojni i vizuelno jaki, ali bez jasnog prioriteta,
- a “šta dalje” preporuka ostaje implicitna.

## Šta ekran već radi dobro

- period filter i poređenje sa prethodnim opsegom su funkcionalno jasni
- postoji fallback za prazan period sa `Prikazi dostupne podatke`
- quality signali su bogati i poslovno korisni
- heuristički signal panel daje brz skraćeni pregled bez kopanja po tabeli
- tabela i print/export tok imaju realnu operativnu vrednost

## Glavni UX rizici

| Area | Trenutno stanje | Zašto zbunjuje | Prioritet | Safe follow-up |
|---|---|---|---|---|
| Freshness / refresh status | header pokazuje samo `Opseg`, bez `last refresh`, `generated at` ili statusa osvežavanja | korisnik ne zna da li gleda svež snapshot ili zastareo izveštaj | P1 | dodati trust/freshness header ili makar kompaktan generated/refresh strip ako backend to može da isporuči |
| Poverenje u podatke je sekundarno | `Kvalitet podataka` postoji, ali je sakriven iza collapse dugmeta i opisan kao “bitno samo ako planirate dublje analize pouzdanosti” | za retail odluku to deluje kao sporedna dijagnostika, iako direktno utiče na validnost smenskog i supplier zaključka | P1 | quality panel držati otvoren po default-u kada postoje warning/danger signali ili makar prikazati 2–3 top problema iznad fold-a |
| Nema jasnog operator CTA-a | ekran opisuje trendove i anomalije, ali ne kaže eksplicitno “proveri drugu smenu”, “proveri nepoznate dobavljače”, “otvori Data Quality”, “uporedi objekat” | korisnik mora sam da prevede analizu u akciju | P1 | dodati kratki “Šta proveriti sledeće” blok sa 2–4 linkabilna sledeća koraka |
| KPI traka je gusta | 7 KPI kartica odjednom mešaju revenue, items, day count, RSD/item, smene i supplier concentration | korisnik teško razlikuje šta je outcome, šta je context, a šta warning | P1 | grupisati KPI-je u `Rezultat`, `Smene`, `Zavisnost od dobavljača` ili smanjiti prvi red na 4 najvažnija |
| Poređenje sa prethodnim periodom nema business framing | compare kartice pokazuju delta, ali ne objašnjavaju da li je to dobro/loše za operaciju | `+8%` ili `-6%` nema poslovni kontekst bez kratkog tumačenja | P2 | dodati mikro-copy ispod kartica: rast/pad prometa, pad komada, pad cene po komadu, itd. |
| Charts su informativni, ali previše ravnopravni | trend, smenski miks, koncentracija dobavljača, obrazac po danu i anomalije imaju skoro isti vizuelni prioritet | operater ne zna gde prvo da gleda | P2 | jasno označiti primarni panel (`Trend + poređenje`), sekundarne (`Smenski miks`, `Kvalitet`) i exploratory panele |
| Table copy ima internal tone | badge `Check`, `Top dobavljača`, `Top 3 share`, `Top 5 share`, `N/A` i slični izrazi zvuče tehnički ili mešano | smanjuje poslovnu jasnoću i jezičku doslednost | P2 | zameniti copy sa `Proveri`, `Top dobavljači`, `Udeo top 3`, `Udeo top 5`, `Nije dostupno` |
| Quality upozorenja nisu dovoljno povezana sa ostatkom aplikacije | page prikazuje warninge, ali nema jasan prelaz na `Kvalitet podataka` ili drugi analytics ekran | korisnik vidi problem, ali nema ugrađen sledeći klik | P2 | dodati link ka `/analytics/data-quality` kada postoje mismatch, unknown supplier ili receipt problemi |

## Period i poređenje

Pozitivno:
- korisnik vidi trenutni i prethodni opseg
- prethodni period je izračunat konzistentno
- `Prikazi dostupne podatke` je dobar recovery pattern za prazan period

Rizici:
- nema naglašene razlike između `requestedFrom/requestedTo` i stvarno dostupnog data prozora
- nema objašnjenja da poređenje nije seasonality-aware, nego prost prethodni opseg istog trajanja
- `RSD po komadu` je koristan, ali bez upozorenja da zavisi od vidljivih komada i filtera može delovati “apsolutnije” nego što jeste

## Kvalitet podataka

Ovo je najjača skrivena vrednost ekrana.

Dobro:
- unknown supplier, off-shift, duplicate receipts, mismatch i non-standard documents su konkretni i relevantni
- `dataHealthSummary` daje kompaktnu agregaciju problema

Glavni UX gap:
- quality layer je formulisan kao opcioni dijagnostički dodatak, a zapravo je trust gate za ostatak ekrana

Preporuka:
- kada postoji `danger` signal, quality deo treba da bude vidljiviji od koncentracije dobavljača ili weekday pattern-a

## Charts i informacijsko opterećenje

Trenutni redosled panela je logičan za analitičara, ali ne i za užurbanog operatera.

Najviše vrednosti za donošenje odluke imaju:
1. `Poređenje sa prethodnim periodom`
2. `Kvalitet podataka`
3. `Trend prihoda i komada`
4. `Heuristički signali i anomalije`

Sekundarni paneli:
- `Smenski miks po danima`
- `Koncentracija dobavljača`
- `Obrazac po danu u nedelji`

Današnji ekran im daje skoro isti vizuelni značaj, pa prvi pregled traje duže nego što bi trebalo.

## Copy i business clarity

Vidljivi copy je funkcionalan, ali nije potpuno dosledan:

- `Loading daily sales data` u spinner label-u je engleski
- `N/A` je tehnički, ne poslovni izraz
- `Check` badge je previše interni
- `Dijagnosticki sloj` i `heuristicki` zvuče više kao interni alat nego kao retail pomoć
- `Top 3 share`, `Top 5 share` i `Dobavljaca za 80%` nisu jezički ujednačeni sa ostatkom ekrana

Ovo nisu blockers, ali zajedno doprinose osećaju “power tool” UI-ja umesto prodajnog analytics ekrana.

## Predloženi mali polish backlog

### P1

1. Dodati kompaktan trust/freshness blok na vrh ekrana.
2. Učiniti quality signal-e vidljivim bez otvaranja kada postoji warning/danger.
3. Dodati mali `Šta proveriti sledeće` blok sa linkovima ili CTA-ovima.
4. Pregrupisati ili smanjiti prvi KPI red tako da ishod i upozorenja ne budu pomešani.

### P2

1. Ujednačiti copy na srpski poslovni jezik.
2. Dodati business framing za comparison kartice.
3. Vizuelno spustiti sekundarne chart panele ispod primarnih decision signala.
4. Dodati jasne prelaze na `Kvalitet podataka` kada quality signal nije dobar.

## Šta ne menjati u follow-up-u

- ne menjati metrics logiku
- ne dirati comparison calculation
- ne raditi broad redesign svih chart panela u jednom commit-u
- ne uklanjati tabelu ili print workflow

## Preporučen sledeći mali frontend task

Ako se radi mali polish posle ovog audita, najbezbedniji prvi commit bi bio:

1. otvoreniji quality/trust vrh,
2. copy cleanup (`N/A`, `Check`, mixed English),
3. mali `Šta proveriti sledeće` panel,
4. bez menjanja formula i bez reorganizacije svih chartova odjednom.
