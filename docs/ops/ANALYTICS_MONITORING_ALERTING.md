# Analytics Monitoring and Alerting Plan

Ovaj plan pokriva monitoring signale za pilot analytics i opisuje šta operater treba da uradi kada stanje postane rizično.
Ne uvodi email, Slack/Teams ili webhook integracije; to ostaje budući korak.

## Signali

| Signal | Severity | Šta znači | Response |
|---|---|---|---|
| Refresh critical | critical | Poslednje osvežavanje ili freshness status ukazuje da su podaci previše zastareli za pouzdano odlučivanje. | Proveri worker, proveri import, pokreni manual refresh, proveri logove. |
| Last success older than 72h | critical | Nije bilo uspešnog refresh-a duže od 72 sata. | Proveri worker, upozori tim, pokreni refresh ili recovery proceduru. |
| Failure after last success | critical | Postoji poslednji uspešan refresh, ali je noviji pokušaj pao. | Pregledaj grešku, proveri logs, popravi uzrok pre novog refresh-a. |
| Worker not running | critical | Worker proces nije aktivan ili nije dostupan. | Proveri deployment, startuj worker, zatim pokreni refresh. |
| Repeated endpoint errors | warning | Istovetne greške se ponavljaju na analytics endpoint-ima. | Pregledaj correlationId, proveri backend i API greške. |
| Stale cache warning | warning | Cache i dalje vraća stare rezultate ili refresh nije razbio zastarele vrednosti. | Očisti cache samo ako je bezbedno, potom ponovi refresh. |
| Import failed | critical | Pilot import nije uspeo ili je završio sa blokirajućim greškama. | Proveri import fajl, validaciju i logove; ne objavljuj odluke dok se ne potvrdi stanje. |

## Severity model

- `info` — signal postoji, ali ne blokira odluke; prati se i dokumentuje.
- `warning` — potrebno je brzo proveriti, ali dashboard još može da radi uz oprez.
- `critical` — odluke ne treba donositi bez ručne provere i svežeg refresh-a.

## Response actions

1. Proveri da li worker radi i da li je poslednji refresh uspeo.
2. Proveri poslednji import i da li je u pitanju import failure ili samo stale cache.
3. Pokreni manual refresh kada je poznat uzrok i kada je bezbedno.
4. Clear cache radi samo ako je potvrđeno da je bezbedno i da neće sakriti root cause.
5. Pregledaj logs, correlationId i posljednje greške pre bilo kakvog novog pokušaja.

## Future alert channels

- email
- Slack/Teams
- webhook

Ove kanale ne implementiramo dok ne postoji potvrđena infrastruktura i operativni vlasnik alarma.

## UI copy za kritično stanje

Kada je `dataFreshnessStatus = critical`, UI treba da prikaže ovu poruku:

> Podaci su kritično zastareli. Ne preporučuje se donošenje odluka bez provere osvežavanja.

Preporuka je da se poruka prikaže u refresh status banner-u i da bude vizuelno očigledna uz `Kritično` badge.

## Otvoreni gapovi

- nema potvrđenog alerting backend-a
- nema email/Slack/Teams delivery kanala
- nema definisanog incident owner-a po signalu
- nema automatskog escalation workflow-a
