# AI Workflow and Token Budget

Updated: 2026-09-12

Ovaj dokument je za agente koji rade sa ograničenim kontekstom/tokenima.

## Osnovno pravilo

Bolje je završiti mali ispravan patch nego započeti veliki rewrite.

Podrazumevana topologija rada je **jedan glavni agent, bez subagenata**. Subagent je izuzetak za jasno ograničen audit, izolaciju velikog istraživačkog izlaza ili nezavisnu verifikaciju — nije podrazumevani način implementacije.

Cursor-specifična always-applied verzija ovog pravila nalazi se u `.cursor/rules/agent-execution-efficiency.mdc`.

## Direct-by-default / subagent pravila

Za normalan bounded task glavni agent radi direktno:

```text
razumi -> pročitaj tačne ownere -> izmeni -> focused proof -> popravi -> verifikuj -> isporuči
```

Ne pokreći subagenta za:
- rutinski repo search;
- čitanje tačnih fajlova;
- shell komande;
- queue selection/claim;
- običnu frontend/backend implementaciju;
- formatiranje;
- focused testove;
- `git status` / diff;
- dokumentaciju;
- poznat bug koji staje u mali scope.

Ne delegiraj samo zato što Auto/router nudi subagenta ili zato što task nije trivijalan.

Jedan subagent je opravdan samo kada ima **nezavisno i konačno pitanje/read set** i kada važi bar jedno:
- eksploracija bi proizvela veliki međurezultat koji bi nepotrebno zatrpao parent context;
- nezavisni verifier proverava već završenu implementaciju bez ponavljanja istog istraživanja;
- konačan audit ima zaseban scope, a parent može da nastavi drugi nepoklapajući posao.

Podrazumevano: **najviše jedan subagent u trenutku**.

Više paralelnih subagenata je dozvoljeno samo kada dodeljeni prompt eksplicitno definiše nezavisne, nepoklapajuće workstreamove i parent zabeleži zašto dodatni token/context trošak ima smisla.

Nikad ne koristiti nested/recursive subagente.

Pre delegiranja parent mora da zabeleži:

```text
Delegated question:
Exact scope/read set:
Expected result/artifact:
Why direct execution is worse:
Stop condition:
```

Ako ovo nije jasno u nekoliko redova, radi direktno.

Subagent **ne dobija novi budžet**. Njegova čitanja, pretrage, komande, output, vreme i vraćeni kontekst računaju se u isti task.

`Waiting for subagent` ne pauzira vreme zadatka. Ako parent može bezbedno da nastavi inspekciju, implementaciju, validaciju, dokumentaciju ili pripremu isporuke, treba da nastavi umesto da samo čeka.

Parent ne sme ponovo da radi isto istraživanje koje je subagent već završio osim ako trenutni code/test dokaz direktno protivreči rezultatu.

Ako subagent izađe iz scope-a, ponavlja isto pitanje ili ne donese novi dokaz, prekini delegiranje i vrati se na direct rad ili napravi split/handoff.

## Auto/model routing

Auto može da bira model, ali izbor rutera ne menja repo pravila.

- Ne menjaj model samo zato što je prethodni korak bio spor.
- Ne pokreći dodatne agente samo zato što je Auto procenio task kao kompleksan.
- Za bounded implementation task, ako Auto počne da pravi ponovljene `Waiting for subagent` cikluse ili prevelik context/token overhead, nastavi jednim eksplicitno izabranim coding modelom i zadrži isti execution packet/scope.
- Promena modela ne resetuje read/search/context budžet i ne opravdava ponovno čitanje već potvrđenog konteksta.

## Kada čitati manje

Ako task cilja jedan ekran:
- pročitaj taj page
- njegov service
- njegove types
- shared komponentu/helper koji koristi
- test ako postoji

Ne čitaj sve analytics stranice osim ako task traži audit.

## Kada stati

Stani ako:
- treba više od 8-10 fajlova bez jasnog razloga
- vidiš drugi nezavisan owner/subsystem; nepovezan bug samo zabeleži i ne širi scope
- obavezna provera ne može da se izvrši ni kroz bezbednu užu alternativu
- ne znaš source of truth
- frontend i backend DTO se ne poklapaju
- postoji rizik migracije
- delegiranje počinje da ponavlja već poznato istraživanje
- drugi subagent bi bio potreban samo zato što prvi nije dao dovoljan rezultat

Napiši:
```text
Potvrđeno:
- ...

Nejasno:
- ...

Najmanji sledeći korak:
- ...
```

## Komande

Izaberi proveru kroz `docs/ai/VALIDATION_SELECTOR.md`. Ne ponavljaj istu neuspešnu komandu bez promene uzroka.

Pre izmene poznatog buga, pokreni najbliži focused reproducer kada je praktično. Compile/setup/timeout ili nevezan baseline kvar klasifikuj kao test-harness/environment problem, ne kao dokaz runtime buga i ne popravljaj produkcioni kod da bi ga sakrio.

Ako `npm run build` padne:
1. pročitaj prvu TypeScript grešku
2. popravi nju
3. pokreni opet

Ako `dotnet test` pada zbog više projekata:
1. pokreni target test projekat ako znaš
2. inače `dotnet build`
3. prijavi ograničenje

## Search strategija

Prvo traži tačne termine:
- component name
- endpoint name
- DTO name
- route
- sourceKey
- recommendationAllowed

Ne traži generički `analytics` ako nije potrebno.

## Patch strategija

1. Minimalni patch
2. Compile
3. Guardrails
4. Test
5. Tek onda UX polish

## Progress visibility

Posle većeg koraka ili važnog tool rezultata napiši jednu kratku statusnu poruku:
- šta je potvrđeno/završeno;
- koji je sledeći korak.

Ne ispisuj svaku komandu i ne ostaj dugo u generičkom `Waiting for subagent` stanju kada postoji bezbedan direct sledeći korak.

## Evidence za delegiranje

Za non-trivial task zabeleži:

```text
Subagents used: <n>
Delegation reason: none | <jedna rečenica>
Duplicated research: no | <obrazloženje>
```

Više od jednog subagenta bez eksplicitnog parallel ownera je signal za scope/budget problem, ne razlog da se nastavi šire.

## Final response

Ne preuveličavati.
Ako nešto nije provereno, reći:
- "Nisam mogao da pokrenem..."
- "Nisam potvrdio..."
- "Rizik ostaje..."
