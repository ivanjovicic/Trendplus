# Pilot Data Safety Checklist

Ovaj checklist je za pilot operatera pre import-a, refresh-a, eksportovanja ili ručnog čišćenja podataka.
Ne tvrdi da postoji automatizacija koja još nije isporučena.

## Šta obuhvata

- operativni DB
- analytics DB
- import fajlove
- generisane reporte i snapshot-e
- logove i error zapise
- cache

## Pre pilot akcije

- Zabeleži datum, vlasnika i razlog akcije.
- Potvrdi koji je sistem na udaru: import, refresh, export, cleanup ili restore.
- Proveri poslednji uspešan refresh i poslednji status importa.
- Zabeleži period koji je pilot pokrio.
- Sačuvaj linkove ili identifikatore relevantnih reportova.
- Ako su prisutni sumnjivi signali, tretiraj stanje kao nebezbedno dok se ne potvrdi suprotno.

## Backup minimum

| Stavka | Šta čuvamo | Kada | Retencija | Pristup |
|---|---|---|---|---|
| Operativni DB | trenutni produkcioni/pilot podaci i veze između entiteta | pre rizičnog importa, većeg refresh-a ili ručnog cleanup-a | prema internom backup pravilniku; dok pilot traje zadržati bar poslednji dobar snapshot | samo administratori i operateri koji rade restore |
| Analytics DB | agregati, snapshoti, metadata i statusi osvežavanja | pre izmene pipeline-a, importa ili masovnog brisanja | isto kao operativni DB ili duže ako je needed for audit | ograničen pristup |
| Import fajlovi | originalni Access/CSV/XLS/XLSX fajlovi i staging kopije | pri svakom pilot unosu | do završetka pilota ili dok postoji potreba za poređenjem | samo tim koji radi import |
| Generisani reportovi / snapshot-i | PDF/CSV/print export i ključne snapshot verzije | posle svake validacije ili prebrisivanja | do zatvaranja pilota + period za audit | prodaja, operacije, admin |
| Logovi i error zapisi | worker, refresh, import i application logs | pre restore-a i pre čišćenja | prema log-retention politici; za pilot sačuvati incidentni interval | admin/ops |
| Cache | ne tretira se kao trajni backup | nema posebnu backup obavezu; može se obrisati i rekonstruisati | ne zadržavati kao izvor istine | admin/ops |

## Šta ne treba obećavati

- nema potvrđenog automatskog backup job-a za pilot
- nema potvrđenog one-click restore procesa
- nema potvrđene politike zadržavanja logova i snapshot-a na nivou aplikacije
- cache nije trajni izvor podataka i ne treba ga koristiti kao backup

## Minimalna pravila za pilot cleanup

- Ne briši logs i reportove pre nego što su sačuvani relevantni audit artefakti.
- Ne briši import fajlove dok se ne potvrdi da je restore ili ponovni import nepotreban.
- Pre ručnog čišćenja obavezno zabeleži šta je obrisano i zašto.
- Ako cleanup menja podatke koji se vide u dashboard-u, uradi novi refresh ili jasno označi da je stanje zastarelo.

## Otvoreni gapovi

- backup automatizacija nije deo ovog dokumenta i nije potvrđena u kodu
- restore koraci su manuelni i zavise od dostupnog DB alata ili provider snapshot-a
- zadržavanje reportova i import fajlova je organizaciona odluka, ne aplikacioni mehanizam
- log retention i cache policy nisu standardizovani kroz app konfiguraciju
