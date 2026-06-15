# Inventory UX Audit

Datum: 2026-06-15  
Repo: ivanjovicic/Trendplus  
Scope: `Klijent/clientapp/src/pages/InventoryPage.tsx` i glavni inventory analytics paneli

## Sažetak

`Inventory` ekran je funkcionalno jak i već je bliže decision cockpit-u nego većina drugih analytics površina. Dobre vesti su:

- replenishment, OOS risk, transfer i dead stock već imaju namenski prostor,
- akcioni workflow je visoko pozicioniran,
- empty i error state-ovi uglavnom poštuju analytics guardrails,
- izvoz i scheduler su sklonjeni ispod fold-a.

Glavni UX problem nije što ekran nema signale, nego što i dalje meša tri nivoa rada:

- **odluka sada**,
- **signal / dijagnostika**,
- **operativni alati i dubinska analiza**.

Zbog toga operater relativno brzo vidi da “ima problema”, ali sporije dolazi do odgovora:
- šta treba dopuniti odmah,
- šta je transfer kandidat,
- šta je mrtva zaliha,
- i šta je samo sekundarni analitički pregled.

## Šta ekran već radi dobro

- `1. Odluke sada` i `2. Rizici i signali` daju dobar top-level decision okvir
- `DecisionSummaryBar` eksplicitno izdvaja `P1 Dopuni`, `P1 OOS`, `P2 Transfer`, `P2 Dead stock`
- `ActionWorkflowPanel` dolazi rano i podržava brzu odluku `Odobri / Odloži / Zatvori`
- `AnalyticsTrustHeader`, warning banner i `AnalyticsEmptyState` su već prisutni
- `Izvoz i scheduler` su spušteni u zasebnu sekciju i više ne dominiraju vrhom stranice

## Glavni UX rizici

| Area | Trenutno stanje | Zašto zbunjuje | Prioritet | Safe follow-up |
|---|---|---|---|---|
| Trust header copy | header i hero koriste `Inventory analytics`, `Decision cockpit`, `workflow`, `stock cover`, `Good sell-through SKU` | meša poslovni srpski i interni engleski jezik baš u najvidljivijem delu ekrana | P1 | ujednačiti top-level copy na srpski poslovni jezik |
| `Kvalitet` u `DecisionSummaryBar` je isključen | `dataQualityWarning={false}` drži quality karticu uvek kao `podaci OK` | korisnik dobija lažno miran signal iako page već zna za warning meta stanje | P0 | povezati karticu sa `showMetaWarning` / relevantnim inventory meta warning signalom |
| KPI signal strip iznad hero dela duplira deo odluke | `Stock cover risk`, `Low cover SKU`, `Slow stock SKU`, `Good sell-through SKU` sedi iznad glavnog hero + decision toka | korisnik prvo vidi tehničke KPI-je, pa tek onda sekciju `1. Odluke sada` | P1 | ili spustiti ovaj strip ispod decision bloka ili ga preimenovati kao “brzi signal” uz jasniju hijerarhiju |
| `Rizici i signali` je jak, ali bez jasnog CTA mosta | alerts, forecast i rebalance su dobri paneli, ali nisu uvek eksplicitno povezani sa `ActionWorkflowPanel` ili `Centralni red akcija` | korisnik vidi signal, ali nije uvek jasno da li sledeći klik ide na `Odobri`, `Uporedi lokacije` ili `Dodaj u akcije` | P1 | dodati jasan `sledeći korak` mikro-copy iznad signala ili u zaglavljima panela |
| `Detaljna analiza zaliha` je previše široka | KPI kartice, insight panels, priority panels, store comparison, size curve i tabela ulaze u isti nivo važnosti | dubinski sadržaj deluje kao drugi dashboard unutar istog ekrana | P1 | jasnije grupisati na `Prioriteti`, `Poređenje lokacija`, `Detalji artikala` |
| Tabela artikala dolazi kasno u toku | lista je važna za stvarni operativni rad, ali je posle više analitičkih panela | operater koji već zna da hoće da radi na artiklima mora dosta da skroluje | P2 | razmotriti anchor/link `Idi na listu artikala` iz vrha ili iz `Odluke sada` |
| Hero + filter + export zona je gusta | vrh ekrana sadrži trust header, methodology, signal strip, hero, filtere i mnogo export dugmadi | početni pogled je informativan, ali kognitivno težak | P2 | smanjiti broj simultano vidljivih control grupa i izdvojiti “operativne alate” od “decision summary” |
| Sekundarne operacije i dalje su vizuelno bučne | iako su scheduler/export spušteni, gornji filter panel i dalje prikazuje veliki broj akcija (`Print preview`, `CSV`, `Excel`, `PDF`, `Osveži`) | operator može steći utisak da je dokument/export jednako važan kao akcija na zalihi | P2 | ostaviti 1–2 najčešće akcije gore, ostalo gurnuti u collapsible tools panel |

## Replenish / OOS / dead stock / transfer jasnoća

### Dobro

- `DecisionSummaryBar` dobro mapira četiri ključna problema
- `ActionWorkflowPanel` koristi razumljive akcione tipove: `dopuna`, `transfer`, `markdown`, `clearance`
- `DemandForecastPanel` i `RebalancingTable` daju konkretne signalne izvore za OOS i transfer

### Gde još škripi

- `P1 OOS 7d` i `P1 Dopuni` deluju blisko, ali nije sasvim jasno gde prestaje “signal” a gde počinje “akcija”
- `Dead stock` je vidljiv u summary baru, ali u dubinskim sekcijama nije dovoljno jasno izdvojen kao poseban tok odluke
- `Slow stock` i `dead stock / markdown` su korisniku bliski, ali vizuelno nisu dovoljno razdvojeni kao različite odluke

## Empty / stale / warning ponašanje

Ovo je jedna od jačih strana ekrana.

Dobro:
- fatalni error ne glumi nule
- empty state jasno razlikuje `insufficient_data`, `filtered_out` i `no_data`
- warning banner koristi meta message i vodi ka `Data Quality` / worker refresh statusu

Otvoreni UX gap:
- quality warning nije dovoljno propagiran u glavne decision kartice, pa top-level summary može delovati “zdravije” nego što jeste

## Da li export / scheduler potiskuju decision flow?

Ranije bi ovo bio veći problem; sada je stanje dosta bolje.

Dobro:
- zasebna sekcija `4. Izvoz i raspored izveštaja`
- `details` wrapper oko scheduler/export panela
- operativni tok odluke više nije potpuno podređen štampi i eksportu

Ipak:
- vršni filter panel i dalje ima mnogo dugmadi odjednom
- `Otvori centralni red akcija` je dobar CTA, ali je vizuelno u istoj ravni sa `CSV`, `PDF`, `Print preview`

Zaključak:
- export i scheduler više ne dominiraju stranicom, ali i dalje prave šum na vrhu

## Copy i business clarity

Najveći copy drift:

- `Inventory analytics`
- `Decision cockpit`
- `Stock cover risk`
- `Low cover SKU`
- `Slow stock SKU`
- `Good sell-through SKU`
- `workflow`

Ovaj miks slabi osećaj da je ekran namenjen prodajnom/operativnom korisniku. Funkcionalno je dobar, ali tonalno još uvek delom izgleda kao interni analytics alat.

## Predloženi mali polish backlog

### P0

1. Popraviti `DecisionSummaryBar` quality signal da ne prikazuje lažno `podaci OK` kada postoji inventory warning/meta problem.

### P1

1. Ujednačiti top-level copy na srpski poslovni jezik.
2. Jasnije razdvojiti `signal` od `akcije` u gornjem delu ekrana.
3. Učiniti `dead stock` / `markdown` tok vidljivijim kao posebnu odluku.
4. Dodati mali CTA most iz `Rizici i signali` ka `ActionWorkflowPanel` ili `Centralnom redu akcija`.
5. Pregrupisati `Detaljna analiza zaliha` u jasnije podsekcije.

### P2

1. Smanjiti broj simultano vidljivih export akcija u vršnom filter panelu.
2. Dodati anchor/link ka tabeli artikala za operatere koji rade direktno po SKU listi.
3. Vizuelno spustiti sekundarne analitičke panele u odnosu na glavni decision tok.

## Šta ne menjati u follow-up-u

- ne menjati inventory algoritme
- ne preuređivati celu stranicu u jednom velikom refactor-u
- ne uklanjati export/scheduler capability
- ne dirati shared analytics error/empty guardrails

## Preporučen prvi mali frontend commit

Najbezbedniji naredni inventory UX commit bio bi:

1. povezati `Kvalitet` karticu sa stvarnim warning stanjem,
2. očistiti top-level engleski copy,
3. dodati mali “sledeći korak” signal između `Rizici i signali` i `Odluke sada`,
4. bez menjanja algoritama i bez velikog layout refactor-a.
