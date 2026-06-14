# Analytics Demo Mode And Demo Dataset Plan

Ovaj dokument definiše kako Trendplus analytics demo treba da izgleda za prodajni ili pilot razgovor.
Ne uvodi seed, reset workflow ili UI toggle u ovom koraku.

## Demo story

Predloženi demo tok prati kratku, jasnu priču kroz 30-dnevni pilot:

1. Otvori Data Quality intake i pokaži da sistem prvo proverava koliko podacima može da veruje.
2. Pređi na Executive dashboard i objasni šta se prodaje, gde je marža i gde postoji rizik.
3. Otvori Supplier scorecard ili supplier analytics ekran i pokaži dobavljača koji traži akciju.
4. Otvori Inventory risk i pokaži OOS ili dead-stock signal.
5. Dodaj jednu akciju u Action Queue iz realnog analytics konteksta.
6. Otvori supplier report i pokaži dokument-style izlaz za razgovor sa kupcem.

## Demo dataset target

Dataset za demo treba da bude dovoljno mali za brzo razumevanje, ali dovoljno bogat da pokaže odluke:

- `50–200` proizvoda
- `5–10` dobavljača
- `90` ili `180` dana prodaje
- stanje zaliha
- nabavne cene
- markdown / nivelacija signali
- nekoliko namerno ubačenih data quality problema

## Required data elements

Minimalni demo dataset treba da pokrije:

- artikal identitet i osnovnu klasifikaciju
- supplier mapping
- prodajne redove i račune
- stock / availability stanje
- nabavnu cenu ili signal gde je cena nepoznata
- price change / markdown / nivelacija istoriju
- dovoljno signala za makar jednu supplier i jednu inventory odluku

## Intentional data quality issues

Demo treba namerno da sadrži nekoliko kontrolisanih problema, tako da Data Quality ekran ima vrednost:

- deo artikala bez dobavljača
- deo artikala bez nabavne cene
- manji broj redova sa slabim signalom ili nedovoljnim istorijskim pokrićem
- jedan primer import ili freshness upozorenja ako postoji bezbedan način da se prikaže

Problemi moraju biti jasno označeni kao demo artefakti, ne kao realan incident kupca.

## Demo rules

- Sve mora biti jasno obeleženo kao `Demo podaci`.
- Demo podaci ne smeju da se mešaju sa stvarnim podacima kupca.
- Reset/reseed ostaje budući implementacioni korak; za sada dataset treba održavati ručno ili kroz poseban demo deployment.
- Ako freshness ili cache nisu spremni, uraditi refresh/warm-up pre sastanka.

## 10-minute script

Predloženi tok za prezentaciju:

1. `00:00–01:30` Otvori Data Quality i pokaži readiness, freshness i glavne data gaps.
2. `01:30–03:00` Otvori Dashboard i pokaži executive pregled i trust kontekst.
3. `03:00–05:00` Otvori Supplier analytics i objasni dobavljača koji traži fokus.
4. `05:00–06:30` Otvori Inventory i pokaži OOS ili dead-stock rizik.
5. `06:30–08:00` Dodaj akciju u Action Queue iz konkretnog signala.
6. `08:00–10:00` Otvori i odštampaj/eksportuj report kao završni sales artefakt.

## Non-goals

- ne implementirati seed sada
- ne dodavati UI toggle sada
- ne oslanjati se na mešanje demo i customer dataset-a

## Open gaps

- nema potvrđenog automated reset/reseed flow-a
- nema zasebnog demo toggle-a u UI
- demo dataset ownership i update cadence treba definisati pre šire prodajne upotrebe
