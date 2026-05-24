# AI Workflow and Token Budget

Ovaj dokument je za agente koji rade sa ograničenim kontekstom/tokenima.

## Osnovno pravilo

Bolje je završiti mali ispravan patch nego započeti veliki rewrite.

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
- vidiš nepovezan bug
- build/test environment ne radi
- ne znaš source of truth
- frontend i backend DTO se ne poklapaju
- postoji rizik migracije

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

Ne ponavljaj istu neuspešnu komandu.

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

## Final response

Ne preuveličavati.
Ako nešto nije provereno, reći:
- "Nisam mogao da pokrenem..."
- "Nisam potvrdio..."
- "Rizik ostaje..."
