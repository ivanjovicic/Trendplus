# Trendplus — posmatrana 20-minutna provera vrednosti za trgovca

**Status: NOT RUN.** Prazan obrazac nije dokaz korisnika, odluke, prihoda ni produkcijskog pilota. Povezano: [poslovne odluke](../product/TRENDPLUS_BUSINESS_PROGRESS_AND_FOCUS_2026-10-14.md), [postojeći runbook](ANALYTICS_PILOT_OPERATOR_RUNBOOK.md), [RQ601/RQ602 izvorni dokaz](../qa/RQ601_RQ602_HISTORICAL_VALIDATION_2026-10-10.md).

## Šta se odlučuje

Za jednog stvarnog trgovca obućom testirati tri poslovna posla: (1) pronaći i sravniti prodaju; (2) objasniti dobavljača i nepoznat trošak; (3) prepoznati zalihu/nivelaciju za proveru ili opravdanu blokadu. Test nije ocena lepote ekrana nego vremena, samostalnosti i korisnosti informacije u poređenju sa postojećim POS/Excel postupkom.

**Dozvoljeno:** sertifikovani istorijski izvor i vidljiv 'as of' datum, bez stvarne promene cena. **Zabranjeno:** predstavljati stare podatke kao aktuelne, izvođenje akcija u kupčevom sistemu, sintetička korisnička izjava ili uvećan ROI. Stvarna imena, lozinke, neanonimizovani računi i sirovi privatni fajlovi ne idu u javni GitHub.

## Scenarij, ukupno 20 minuta

| Minuti | Zadatak bez pomoći developera | Šta posmatrač beleži |
| --- | --- | --- |
| 0–2 | Izaberi prodavnicu, datum i izvor | Može li sam da prepozna istorijski izvorni horizont, dataScope i upozorenje o starosti? |
| 2–7 | Nađi promet i broj pari za isti period; izvezi | Da li razume povrate, jedinicu mere i ista ograničenja u izvozu? |
| 7–12 | Odaberi dobavljača o kome treba razgovarati | Ume li da razlikuje potvrđen trošak, procenu i nepoznatu maržu; ima li poslovni razlog? |
| 12–17 | Identifikuj robu/nivelaciju za proveru ili razlog da se odluka blokira | Ne naziva današnju zalihu istorijskom; vidi šta nedostaje za preporuku |
| 17–20 | Objasni sledeću odluku i poredi sa svojim POS/Excel procesom | Koji konkretan posao štedi? Šta bi bilo drugačije? Ko odobrava promenu i budžet? |

Rubrika za svaki od tri poslovna zadatka: **0** nije pronašao; **1** samo uz pomoć; **2** samostalno ali bez verifikacije; **3** samostalno sa proverom perioda/izvora ili izvoza. Posmatrač ne šapuće gde da klikne. Blokada zbog nedostupnog izvora ili nedovoljnog dokaza može biti **ispravno ponašanje proizvoda**, ali nije uspešno sprovedena finansijska akcija.

## Prazan obrazac za stvarni rezultat

- Datum/saglasnost/anonimizovana uloga: **NOT RUN**
- Demo okruženje/SHA, izvor ili batch, dataScope, prodavnica, efektivni period, poslednji izvorni datum: **NOT VERIFIED**
- Ocene za prodaju / dobavljača / zalihu (0–3): **NOT SCORED**
- Vreme do prve samostalne korisne informacije: **NOT MEASURED**
- Tri doslovne primedbe operatera (bez ličnih podataka): **NOT OBSERVED**
- Odluka ili eksplicitan razlog blokade; nosilac i sledeći stvarni korak: **NOT RECORDED**
- Šta operater već radi u POS/Excel i koliko je koraka izbegao: **NOT OBSERVED**
- Ko odlučuje o kupovini/pilotu, da li bi nastavio drugi sastanak, koji dokaz traži: **NOT ASKED**
- Stvarno izvršena promena, kontrola, izmereni finansijski rezultat: **NOT MEASURED**

**Predloženi go/no-go kriterijum (vlasnički, ne industrijski):** nastaviti na mali read-only pilot ako operater samostalno uradi bar 2 od 3 zadatka, prepozna jednu korisnu odluku ili stvarnu blokadu i pristane na nastavak sa autorizovanim izvornim podacima. Ako ne, otvoriti samo *jedan* konkretan P1 na najveći ponovljiv problem. Ako postojeći POS radi isto bez dodatnog napora, ne graditi još grafikona.

## Kratki promptovi za posebne, bezbedne poslove — nisu novi RQ

**OP-PREP (agent, repository-local):** "Proveri postojeći batch-23 izvorni audit i rute Daily Sales, Supplier, Inventory, Pre-Nivelacija, Decision Pulse. Pripremi jednu stranicu istorijskog scenarija sa istim izvorom/prodavnicom/periodom i izvozom. Bez novih ekrana ili fiksiranih lažnih vrednosti: svaka cifra mora imati dokaz ili oznaku NOT VERIFIED. Ne prijavljuj korisnički PASS ni profit."

**COST-INPUT (agent + vlasnik izvora):** "Koristi već navedenu listu 20 originalnih kalkulacija u RQ601/RQ602 dokumentu. Pripremi vlasniku upit za originalni dokument, identitet artikla, datum, valutu, nabavnu cenu po paru i troškove/takse koji utiču na trošak. Proveri jedino stvarno dostavljene originale; ne zaključuj skalu konverzije iz numeričkog odnosa niti postavljaj RQ601 na DONE bez dokaza. Ne kači originalne fakture u GitHub."

**PROVIDER-INPUT (agent + ovlašćeni operator):** "Napravi precizan spisak nedostajućih ovlašćenja iz STAB16/RQ448/RQ565: Render web/worker i deployed SHA, Vercel build, read-only Neon prozor, durable import/refresh, API→browser→real CSV/screenshot i izvorni SQL oracle. Ako pristup nije odobren, ostavi NOT AVAILABLE, prijavi samo vlasnika/blokadu. Bez tokena u logovima, deploya, izmene podataka ili produkcionog sertifikata iz sintetičkog RQ664 smoke testa."

## Komercijalna ponuda koja sme da se testira

Prvo ponuditi ograničen, datiran **read-only retail dijagnostički pregled** — promet, dobavljači, sporija roba, nepoznati troškovi i izvorni izvoz — bez tvrdnje o optimizaciji ili uzročno dokazanom ROI. Cena, spremnost za plaćanje i dužina pilota ostaju hipoteze do stvarnog razgovora. Tek uz svež i odobren izvor, dokumentovane nabavne cene i odobrenu izvršenu akciju aktivirati RQ592 merenje posmatranog ishoda.
