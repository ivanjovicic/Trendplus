# Backup and Restore Runbook

Ovaj runbook pokriva ručni backup i restore tok za pilot situacije.
Ne pretpostavlja posebnu automatizaciju, scheduler ili self-service tooling.

## Kada ga koristiti

- loš import je upisao pogrešne podatke
- refresh je pokvario analytics stanje
- neko je obrisao ili promenio podatke koje treba vratiti
- treba vratiti stanje pre pilot incidenta
- potrebno je sačuvati dokazne artefakte pre resetovanja

## Pre nego što kreneš

- Zaustavi ili pauziraj worker/refresh tok ako dalji upisi mogu da pogoršaju stanje.
- Zabeleži vreme incidenta i poslednji poznati dobar trenutak.
- Sačuvaj trenutni DB state pre restore-a ako je moguće.
- Identifikuj da li treba vratiti operativni DB, analytics DB ili oba.
- Sačuvaj relevantne import fajlove, reportove i logove kao dokazni materijal.

## Backup tok

| Korak | Opis |
|---|---|
| 1 | Napravi backup operativnog DB pre svake rizične akcije. |
| 2 | Napravi backup analytics DB ako restore može da utiče na agregate, snapshot-e ili status osvežavanja. |
| 3 | Sačuvaj originalni import fajl i staging kopije. |
| 4 | Arhiviraj generisane reportove i snapshot-e koji su bitni za audit ili poređenje. |
| 5 | Izvuci logove iz intervala pre i posle incidenta. |

## Restore tok

1. Potvrdi da je problem stvaran i da nije samo stale cache ili kasni refresh.
2. Izaberi najnoviji dobar backup ili snapshot.
3. Zabeleži očekivani opseg restore-a pre promene bilo čega.
4. Restore-uj samo potreban sistem ako je moguće; ne vraćaj više nego što mora.
5. Nakon restore-a pokreni validaciju podataka.
6. Tek kada su podaci konzistentni, uključi refresh tokove i worker-e nazad.
7. Ako restore nije uspeo, zaustavi dalje pokušaje i sačuvaj logove greške.

## Validacija posle restore-a

- proveri da li se dashboard otvara bez error state-a
- proveri poslednji uspešan refresh i freshness status
- proveri sample brojeve za prodaju, dobavljače i akcione stavke
- proveri da li report export radi i da li odgovara očekivanom periodu
- proveri da li su import i action queue podaci konzistentni sa backup snapshot-om
- proveri da li postoje neočekivani `0` rezultati tamo gde bi trebalo da bude `unknown` ili error

## Post-restore refresh

- Pokreni refresh tek nakon što je restore potvrđen.
- Ako je refresh stari ili nepoznat, označi stanje kao warning dok se ne dobije novi uspešan run.
- Očisti cache samo ako je poznato da sadrži stare ili konfliktne vrednosti.
- Sačuvaj izlazne logove refresh-a i validacije kao deo incident record-a.

## Export opcije

- Pre restore-a eksportuj ključne reportove ako su potrebni za poređenje ili klijentsku komunikaciju.
- Ako je dostupan raw data export, sačuvaj i njega kao audit kopiju.
- Ne obećavaj automatski izvoz dok nije potvrđeno da konkretan path postoji u aplikaciji.
- Ako eksport nije dostupan, zabeleži to kao otvoreni gap umesto da ga simuliraš.

## Brisanje i cleanup

- Pilot cleanup radi tek posle eksplicitnog odobrenja.
- Sačuvaj logs/error records pre brisanja.
- Sačuvaj reportove i snapshot-e prema retention odluci.
- Import fajlove briši samo kada je jasno da više nisu potrebni za restore ili audit.
- Cache je dozvoljeno obrisati ako je deo recovery procedure i ako je potvrđeno da se može rekonstruisati.

## Otvoreni gapovi

- nema potvrđenog automatskog backup scheduler-a
- nema potvrđenog restore script-a za jedan klik
- nema standardizovanog retention modela za reportove i import fajlove
- nema aplikacionog mehanizma koji vodi operatera kroz ceo incident
