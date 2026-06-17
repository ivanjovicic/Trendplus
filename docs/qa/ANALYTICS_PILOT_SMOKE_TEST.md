# Analytics Pilot Smoke Test

Datum: 2026-06-17
Repo: `ivanjovicic/Trendplus`
Namena: ponovljiv manual smoke test za analytics pilot flow pre merge-a, demo-a ili customer pilot provere

## Kako koristiti ovaj dokument

- Ovaj smoke test je namenjen operatoru ili QA osobi koja nije developer.
- Test prolaz raditi redom, bez preskakanja ruta.
- Ako neka ruta nema dovoljno podataka za pun prikaz, to nije automatski fail.
- Fail je kada aplikacija krije problem, prikazuje lazno zdravo stanje ili blokira dalji rad.

## Pre pocetka

1. Otvoriti aplikaciju i prijaviti se u okruzenje za pilot/demo proveru.
2. Proveriti da je sidebar ucitan i da su analytics rute dostupne.
3. Ako je poznat period za test, koristiti isti period kroz sve analytics ekrane.
4. Ako postoje filteri po objektu/dobavljacu/scope-u, ne menjati ih bez razloga tokom prvog prolaza.

## Globalna trust pravila za sve rute

Na svakoj analytics ruti proveriti sledece:

- Nema fake `0 RSD`, `0 kom`, `0%` ako je API u gresci.
- `unknown` stanje ne sme izgledati zeleno ili kao "spremno".
- Zastarelo osvezenje mora biti vidljivo kada je dostupno.
- Parcijalni podaci moraju imati warning ton ili warning poruku.
- Ako postoji data quality warning, mora biti jasno vidljiv i ne sme liciti na healthy stanje.
- Ako postoji izvestaj, `generatedAt`, `lastRefreshAt`, freshness ili ekvivalent mora biti vidljiv kada backend to vraca.
- Error ili empty panel ne sme srusiti ostatak stranice.

## Pass / fail semantika

- `PASS`: ruta radi, trust signali su jasni, warning/empty/error stanja ne glume uspeh.
- `SOFT PASS`: ruta nema dovoljno podataka, ali to je jasno objasnjeno kroz empty/warning stanje.
- `FAIL`: ruta prikazuje lazno zeleno stanje, fake nule, nevidljiv stale/problem signal, pogresne linkove ili runtime gresku.

---

## 1. Dashboard

Ruta: `/analytics`

### Sta korisnik treba da vidi

- Glavni analytics pregled sa trust header-om ili ekvivalentnim freshness/data quality kontekstom.
- Kljuceve poslovne sekcije ili kartice koje vode ka proizvodima, dobavljacima, zalihama, data quality ili akcijama.
- Period i poslednje osvezenje, ako su dostupni.

### Warning / empty / error stanje ne sme da uradi

- Ne sme prikazati sve KPI kartice kao `0` ako API ili backend nisu dostupni.
- Ne sme sakriti stale ili partial stanje ako postoji warning.
- Ne sme zakljuciti da je stanje zdravo ako je data quality `warning`, `insufficient_data` ili `unknown`.

### Sta kliknuti

1. Klik na link ili CTA ka `Kvalitet podataka`, ako postoji.
2. Klik na link ili CTA ka `Pilot spremnost`, ako postoji.
3. Klik na bar jednu sekciju ka `Odluke o proizvodima`, `Pregled dobavljaca` ili `Zalihe i dopuna`.

### Sta je fail

- Prazan ekran, runtime error ili bela strana.
- Link vodi na pogresnu rutu ili ne radi.
- Trust/freshness signal nedostaje iako je ostatak stranice ocigledno analytics dashboard.

### Data trust checks

- Nema fake `0 RSD`.
- `unknown` nije zelen.
- Stale refresh je vidljiv kada postoji.
- Partial data izgleda kao warning, ne kao success.

---

## 2. Pilot Readiness

Ruta: `/analytics/pilot-readiness`

### Sta korisnik treba da vidi

- Checklistu pilot spremnosti sa vise stavki.
- Za svaku stavku: status, kratak razlog, sledeca akcija i link do korisnog ekrana.
- Vizuelnu razliku izmedju `ready`, `warning`, `blocked` i `unknown`.

### Warning / empty / error stanje ne sme da uradi

- `unknown` ne sme izgledati kao `ready`.
- Ako report ili API nisu dostupni, readiness ne sme lazno pozeleneti.
- Jedan neuspeli API poziv ne sme blokirati celu stranicu.

### Sta kliknuti

1. Klik na jednu `warning` ili `blocked` stavku i otvoriti njen sledeci ekran.
2. Klik na stavku `Izvestaji spremni` i otvoriti report link koji nudi.
3. Klik na stavku vezanu za data quality ili analytics refresh.

### Sta je fail

- Sve stavke su zelene bez objasnjenja, iako su podaci ocigledno nepotpuni ili nepoznati.
- CTA ne vodi ni na jedan koristan ekran.
- Cela readiness stranica padne zbog jednog neuspelnog izvora.

### Data trust checks

- `unknown` nije zelen.
- Warning i blocked izgledaju ozbiljnije od ready stanja.
- Ako report fali, readiness to jasno kaze.
- Ako freshness ili last refresh postoje, vidljivi su na stranici ili kroz povezani ekran.

---

## 3. Product Decisions

Ruta: `/analytics/products`

### Sta korisnik treba da vidi

- Tabelu ili listu preporuka po proizvodu.
- Za redove sa preporukom: status, razlog, akciju i signal pouzdanosti kada postoji.
- Mesto za odlazak u centralne akcije ili dodavanje akcije, ako je funkcija dostupna.

### Warning / empty / error stanje ne sme da uradi

- Ne sme prikazati preporuku bez razloga.
- Ne sme prikazati healthy confidence/reliability ako je data quality warning ili insufficient.
- Ako nema podataka, mora postojati empty objasnjenje, ne mrtva tabela puna nula.

### Sta kliknuti

1. Otvoriti prvi dostupan `Zasto?`, details ili ekvivalent reason prikaz.
2. Kliknuti `Dodaj u akcije` ili ekvivalent, ako je dugme dostupno.
3. Otvoriti `Kvalitet podataka` link, ako je warning vidljiv.

### Sta je fail

- Preporuka postoji bez razloga ili reason-a.
- Klik na akciju ne radi ili vodi na pogresno mesto.
- Warning stanje izgleda isto kao healthy stanje.

### Data trust checks

- Nema fake `0 RSD`.
- Unknown / insufficient signal ne izgleda zeleno.
- Warning data quality je jasno oznacen.
- Ako postoji freshness ili generated time, vidljiv je.

---

## 4. Supplier

Ruta: `/analytics/supplier`

### Sta korisnik treba da vidi

- Pregled dobavljaca sa trust signalom, periodom i makar jednom sekcijom za odluku ili fokus.
- Ako postoji scorecard ili ranking, mora biti jasno da li je recommendation ili pomocni signal.
- Mesto za otvaranje supplier report-a, ako je dostupno.

### Warning / empty / error stanje ne sme da uradi

- Ne sme tiho fallback-ovati na drugi dataset bez warning-a.
- Ne sme prikazati finalnu preporuku ako je stanje samo signal/pomocni signal.
- Ne sme prikazati fake healthy margin/revenue nule na backend gresci.

### Sta kliknuti

1. Otvoriti supplier report ili report action, ako postoji.
2. Otvoriti data quality link kada postoji warning.
3. Otvoriti bar jedan supplier details ili drilldown element, ako je vidljiv.

### Sta je fail

- Supplier report link vodi na nepostojecu rutu.
- Dataset/fallback warning nedostaje kada je prikaz ogranicen.
- Stranica prikazuje finalnu preporuku bez objasnjenja.

### Data trust checks

- Nema fake `0 RSD`.
- Unknown ili fallback nije zelen.
- Stale/partial signal je vidljiv kada postoji.
- Ako je supplier report otvoren, generated/refresh info je vidljiv.

---

## 5. Inventory

Ruta: `/analytics/inventory`

### Sta korisnik treba da vidi

- Pregled zaliha, dopune, rizika ili rebalance preporuka.
- Jasno odvojene decision sekcije od pomocnih utility sekcija.
- Link ili CTA ka akcijama ili data quality kada je to relevantno.

### Warning / empty / error stanje ne sme da uradi

- Ne sme prikazati healthy stock signal ako je inventory meta warning/partial.
- Ne sme srusiti celu stranu ako jedan inventory panel nema podatke.
- Ne sme prikazati sve nule kao validan inventory pregled na backend gresci.

### Sta kliknuti

1. Otvoriti prvu dostupnu action/recommendation stavku.
2. Kliknuti data quality ili warning link ako postoji.
3. Otvoriti centralne akcije link ako je inventory odluka vec dodata ili moze da se doda.

### Sta je fail

- Inventory trust warning nije vidljiv, a panel izgleda healthy.
- Klik na preporuku ne radi.
- Jedan los panel blokira celu inventory stranicu.

### Data trust checks

- Nema fake `0 RSD`.
- Unknown nije zelen.
- Partial ili warning panel izgleda kao warning.
- Freshness/generation info je vidljiv kada backend to daje.

---

## 6. Data Quality

Ruta: `/analytics/data-quality`

### Sta korisnik treba da vidi

- Pregled kvaliteta podataka, problema i uticaja na analitiku.
- Link ili CTA ka pilot intake report-u ako je dostupan.
- Jasnu razliku izmedju healthy, warning i critical signala.

### Warning / empty / error stanje ne sme da uradi

- Ne sme tvrditi da je data quality healthy ako postoje warning/critical problemi.
- Ne sme prikazati "0 problema" ako se podaci nisu ucitali.
- Empty stanje mora objasniti zasto nema rezultata.

### Sta kliknuti

1. Otvoriti pilot intake report link ili CTA.
2. Otvoriti bar jedan issues/top offenders/tab prikaz.
3. Ako postoji refresh/workers link, otvoriti ga.

### Sta je fail

- Data quality stranica pokazuje healthy signal bez dokaza.
- Pilot intake report link ne radi.
- Error stanje izgleda kao empty ili healthy stanje.

### Data trust checks

- Nema fake nula za broj problema.
- Unknown ili insufficient nije zelen.
- Refresh/freshness signal je vidljiv kada postoji.
- Warning i critical su jasno vidljivi.

---

## 7. Actions

Ruta: `/analytics/actions`

### Sta korisnik treba da vidi

- Listu centralnih akcija ili empty state koji objasnjava da akcija jos nema.
- Status, source, prioritet ili metadata za akcije kada postoje.
- Outcome summary ili report-like pregled, ako je dostupan.

### Warning / empty / error stanje ne sme da uradi

- Ne sme prikazati lazno da nema akcija ako je API u gresci.
- Ne sme izgubiti status metadata kada se otvori detalj.
- Ako summary nema dovoljno podataka, to mora biti warning ili empty, ne healthy signal.

### Sta kliknuti

1. Otvoriti prvu dostupnu akciju ili details panel.
2. Promeniti filter ili status filter, ako postoji.
3. Otvoriti link nazad ka izvornoj analytics ruti, ako postoji.

### Sta je fail

- Akcije postoje ali detalj ne moze da se otvori.
- Outcome summary izgleda spremno iako nema podataka ili je unknown.
- Klik na source ekran vodi na nepostojecu rutu.

### Data trust checks

- Nema fake `0` summary brojki na gresci.
- Unknown summary nije zelen.
- Warning/partial outcome signal je vidljiv.
- Ako postoji generated/freshness info, vidljiv je.

---

## 8. Pilot Intake Report

Ruta primer:
`/analytics/reports/pilot-intake?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all`

Legacy alias:
`/analytics/data-quality/pilot-intake-report`

### Sta korisnik treba da vidi

- Dokument-like report prikaz za pilot intake.
- Period, generated/generisano vreme, poslednje osvezenje i data quality kada su dostupni.
- Metodologiju, warning-e i glavne sekcije izvestaja.

### Warning / empty / error stanje ne sme da uradi

- Ne sme prikazati lazne redove ako report nije dostupan.
- Ako je preview istekao ili nema payload-a, mora prikazati empty ili error stanje.
- Ne sme izgledati kao healthy report ako je data quality warning ili insufficient.

### Sta kliknuti

1. Otvoriti `Otvori trajni report`, ako je link prisutan.
2. Otvoriti `Kvalitet podataka` ili ekvivalent link kada postoji.
3. Pokusati print/export akciju ako je vidljiva.

### Sta je fail

- Ruta se ne ucita preko direktnog URL-a.
- Report nema period ili trust metadata iako backend sekcije postoje.
- Empty/error stanje prikazuje fake tabelu.

### Data trust checks

- Nema fake `0 RSD`.
- Warning quality nije zelen.
- Generated/refresh info je vidljiv kada postoji.
- Stale ili partial stanje je vidljivo.

---

## 9. Supplier Decision Report

Ruta primer:
`/analytics/supplier/report?fromDate=2026-06-01&toDate=2026-06-30&dataScope=all`

### Sta korisnik treba da vidi

- Dokument-like report za supplier odluke.
- Supplier, period, generated time, last refresh i data quality kada su dostupni.
- Upozorenja, KPI, preporuke i metodologiju ili ekvivalentne sekcije.

### Warning / empty / error stanje ne sme da uradi

- Ne sme prikazati lazne report redove ako backend report nije dostupan.
- Ako postoji samo privremeni preview, to mora biti jasno obelezeno.
- Ne sme prikazati finalno healthy stanje ako je report fallback ili recommendation nije dozvoljen.

### Sta kliknuti

1. Otvoriti print/export akciju ako postoji.
2. Otvoriti `Kvalitet podataka` link ako je warning prikazan.
3. Ako postoji link nazad ka supplier ekranu ili trajnom report URL-u, otvoriti ga.

### Sta je fail

- Direktan URL ne radi ili se ne moze refresh-ovati.
- Report ne pokazuje warning kada je preview/fallback privremen.
- KPI ili preporuke izgledaju healthy iako je quality signal warning/unknown.

### Data trust checks

- Nema fake `0 RSD`.
- Unknown ili fallback nije zelen.
- Generated/refresh info je vidljiv kada postoji.
- Warning i ogranicenja su jasno vidljivi.

---

## Zavrsni smoke rezultat

Na kraju prolaza zapisati:

1. Datum i okruzenje.
2. Koje rute su `PASS`, `SOFT PASS` ili `FAIL`.
3. Screenshot ili kratak opis za svaki `FAIL`.
4. Da li je problem:
   - vizuelni trust problem
   - pogresna ruta/link
   - empty/error handling problem
   - stale/freshness signal problem
   - report problem

## Minimalni kriterijum za demo/merge prolaz

- Nijedna core ruta ne puca.
- Nijedna ruta ne prikazuje fake healthy stanje na unknown/error podacima.
- Pilot Readiness ne glumi spremnost kada su report ili data quality signali nepoznati.
- Report rute rade direktno preko URL-a.
- Warning, empty i error stanja su razumljiva operateru bez developerskog tumacenja.
