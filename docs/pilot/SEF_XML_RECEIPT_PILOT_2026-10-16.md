# Trendplus — SEF XML → pregled / priprema prijema robe (pilot)

Datum: 2026-10-16
Status: **OWNER-APPROVED ENGINEERING PILOT, CUSTOMER TEST NOT RUN**
Kanonska implementaciona queue: docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md (QDB11–QDB14)
UI cilj: /unos-robe (postojeća ruta); **nema novog prodajnog dashboarda**

## Problem koji zelimo da proverimo

Vlasnik maloprodaje obuce prima ulazne fakture preko drzavnog SEF portala i stavke robe rucno upisuje u originalni Access program. Istorijska Trendplus baza vec ima uvezene Access podatke o prijemima/kretanjima, ali ne dokazuje da imamo originalne iznose svih linija kalkulacija, pa RQ601 provenance ostaje PARTIAL. Aktuelni web UnosRobeForm -> ArtikliPage -> UnosArtikalaForm **nije kompletan prijemni ledger**: postojece stavke se samo loguju, a DnevnikPromena se samo ispisuje u konzolu. Ne koristiti taj submit kao nacin knjizenja pilota.

## Jedan poslovni cilj

Za istu ulaznu fakturu pokazati vlasniku: (a) koliko XML stavki se automatski cita, (b) koliko se **pouzdano** mapira na Trendplus katalog a koliko zahteva ljudsku odluku, (c) sta je fakturisano a sta zaista fizicki primljeno (unosi operator), (d) koliko je koraka/vremena potrebno za pripremu nacrta u odnosu na rucni Access unos.

**Nije cilj:** tvrditi da je Access vec automatski azuriran; automatski potvrditi stvarni prijem iz fakture; slati/menjati SEF dokument; utvrditi preplatu bez ugovora; zameniti Access ili dokazati spremnost SaaS-a sa jednim poznatim korisnikom.

## Faze i granice

| Faza | Queued | Demonstracija / izlaz | Granica |
| --- | --- | --- | --- |
| 1 | QDB11 DONE | Ucitaj jedan SEF XML, pregledaj broj/datume/dobavljaca/valutu/stavke | Read-only, bez persistentnih customer payload-a |
| 2 | QDB12 DONE | Po stavci exact/manual/ambiguous/unmatched i razlog; manual physical received qty | Ne menjaj master cost/stock, nesertifikovana Access kalkulacija = UNKNOWN |
| 3 | QDB13 DONE (engineering) | Na `/unos-robe` postoji odvojeni XML pregled/nacrt i NEKNJIŽENO CSV export; sinteticki Chromium fixture prolazi | Server auth, flag OFF po defaultu, bez zaliha writes; nema deployovanog owner URL-a niti stvarne fakture/operator testa |
| 4 | QDB14 WAITING | Opcioni commit u izolovanu TEST Trendplus bazu | Zahteva novu owner dozvolu, PostgreSQL dokaz, nikada Access write |

Redosled je serijski (unutar iste feature family). WIP i visi prioriteti iz MASTER_ROADMAP ostaju merodavni.

QDB13 local browser-fixture URL is ephemeral and is not a deployed demo: `http://127.0.0.1:54449/unos-robe` (tested with synthetic-only API interception; the server closed after the smoke test). The production API flag remains OFF by default and still requires server-side admin authorization. Real invoice/operator validation is NOT RUN pending the owner's explicit consent and approved environment.

## Priprema stvarnog operator testa

Obavezan izricit pristanak vlasnika za obradu poverljivih dokumenata. Nije potrebno deliti njegov SEF nalog, lozinku ni API kljuc.

Uz saglasnost uzeti 2–3 representative XML **ulazne** fakture: jednu jednostavnu, jednu sa vise velicina/sifara/artikala, i po mogucstvu jednu sa rabatom/posebnim jedinicama. Za vecu proveru kasnije 5–10. Dobiti po mogucnosti odgovarajuci Access prijem ili izvoz, sa datumom, lokacijom i stvarno primljenim kolicinama. Bez originalne prijemnice nemoj tvrditi da su količine fizicki stigle. Stvarni raw dokumenti ne ulaze u javni repo, PR, fixture, screenshot ili log; koristite synthetically modeled fixtures za CI. Real source se testira samo u ovlascenom okruzenju, uklanja nakon obrade.

### Scenario 15–20 minuta (NOT RUN)

1. **Pre**: posmatraj kako vlasnik inace obradi jednu fakturu u Accessu; izmeri ukupno vreme i rucne korake, greske/prisvojene sifre. Ne menjati njegov proizvodni Access.
2. **Preview**: vlasnik sam odabere XML, vidi zaglavlje i svaku liniju; proceni tacnost i cita izvorne detalje.
3. **Uparivanje**: potvrdi/prepravi predlozene artikle, posebno velicine i duple nazive; never auto-accept approximate matches.
4. **Prijem**: unese fizicki primljenu kolicinu i lokaciju za prikaz NACRTA; bez knjizenja. Ako nema fizickog dokaza, ostavi UNKNOWN.
5. **Izvoz**: preuzme lokalni NACRT i uporedi sa rucno evidentiranim Access dokumentom, objasni da li bi ga koristio.
6. **Vrednost**: zatraziti konkretno vreme/usku granu koja je olaksana; pitati za ucestalost faktura, trosak rucnog rada, greske i iskrenu spremnost za placeni pilot, ne sugestivnu pohvalu.

### Real operator evidence template

- Datum, saglasnost, anonimizovana uloga: NOT RUN
- Env URL, deploy SHA, role, feature flag, dokument format: NOT VERIFIED
- Broj XML faktura/stavki obradjenih: NOT MEASURED
- Exact/manual/ambiguous/unmatched po izvornom line ID: NOT MEASURED
- Matched Access original-kalkulacija i udeo finansijski proverljivih redova: NOT VERIFIED
- Trajanje originalnog Access unosa (min) / preview+dopuna (min): NOT MEASURED
- Broj izmisljenih/skipped/duplih stavki, valuta/PDV/UOM anomalije: NOT MEASURED
- 3 komentara vlasnika i sledeca odluka: NOT OBSERVED
- Spremnost za plativi pilot / cena / kanal prodaje: NOT ASKED
- Operativni PostgreSQL/Access upis: NOT AUTHORIZED
- Browser live vs synthetic CI: NOT RUN

### Go/no-go hipoteza

**GO za novu ogranicenu iteraciju**, ne za SaaS: vlasnik uspe da samostalno uradi preview i nacrt bez pogresnih ili precutanih stavki, vidljivo smanji rucni posao ili kaze koji tacno korak je ubrzan, i pristane na proveru sa vise originalnih dokumenata. Uslov za komercijalnu hipotezu: razgovor sa bar jos jednom nezavisnom firmom i konkretna spremnost na placeni pilot. Ako podaci u XML-u nisu dovoljno kvalitetni ili nema bezbednog nacina uvoza u Access, jasno pokazati pilot kao koristan eksport/nacrt, ne obecavati automatizaciju knjizenja.

## Preporuka za demo pristup

Owner trazi opciju *u samoj Trendplus aplikaciji*. QDB13 mora utvrditi autorizaciju i deploy uslove; nema bezuslovnog ukljucenja u javnoj produkciji. Ako je auth nedokazan, koristiti izolovano demo okruzenje i sinteticke XML podatke; realne fakture tek po autorizaciji. Za kriticni stari manual submit (UnosArtikalaForm) dodati jasno razdvajanje od pilot nacrta; ne otvarati ga iz XML wizard-a kao "gotov" upis.

## Reference u kodu i postojeci gate-ovi

- Klijent/clientapp/src/pages/UnosRobePage.tsx
- Klijent/clientapp/src/components/UnosRobeForm.tsx
- Klijent/clientapp/src/components/UnosArtikalaForm.tsx
- Klijent/clientapp/src/pages/ArtikliPage.tsx
- Api/Services/AccessImportService.cs (read-only inspect only)
- Domain/Model/DnevnikPromena.cs; Domain/Model/TipPromeneConstants.cs
- docs/qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md
- docs/product/RETAIL_PROFITGUARD_MARKET_FEASIBILITY_2026-10-15.md
- docs/ai/PROMPT_QUEUE_PROTOCOL.md

**Napomena:** Queue su implementacioni promptovi, ne isporucena funkcija. CI je samo razvojni dokaz, nikada stvarna korisnicka sesija ili dokaz finansijske ustede.
