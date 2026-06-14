# Codex Queue Runner Instructions

Ovo je uputstvo za Codex kada treba da izvršava `docs/ai/NEXT_PROMPT_QUEUE.md`.

## Važno

Codex ne treba da pokušava da završi ceo queue odjednom. Radi jedan task po sesiji i napravi jedan mali commit.

## Start prompt za Codex

```text
Repo: ivanjovicic/Trendplus

Pre rada pročitaj:
- .github/copilot-instructions.md
- AGENTS.md
- docs/ai/NEXT_PROMPT_QUEUE.md

Zadatak:
Izvrši prvi task u docs/ai/NEXT_PROMPT_QUEUE.md koji ima Status: TODO.

Pravila:
1. Ne preskači redosled.
2. Ne radi više od jednog queue taska u ovoj sesiji.
3. Pre izmene promeni status taska u IN_PROGRESS.
4. Uradi minimalnu izmenu u scope-u taska.
5. Pokreni samo relevantne provere iz taska.
6. Ako provere zapnu, ne ponavljaj istu komandu više puta. Probaj uži check ili označi BLOCKED.
7. Posle rada ažuriraj status: DONE, PARTIAL ili BLOCKED.
8. U task belešku upiši šta je promenjeno, koje provere su pokrenute, rezultat i rizike.
9. Koristi commit message predložen u tasku.
10. Ne menjaj nepovezane fajlove.

Na kraju odgovori:
- completed task
- changed files
- checks
- risks
- next queue item
```

## Kada Codex treba da stane

Stani ako:
- task izlazi iz scope-a
- mora da menja više od 6–8 fajlova
- nema jasnog auth/cache/import pattern-a
- build/test pada zbog nepovezanog problema
- treba secret/environment koji nije dostupan
- postoji opasnost da se pokvari routing/lazy loading
- vidiš mojibake u novom tekstu

## Ručni nastavak

Posle svakog commita:
1. Otvori `docs/ai/NEXT_PROMPT_QUEUE.md`.
2. Nađi sledeći `Status: TODO`.
3. Pokreni start prompt iznad.
4. Ne vraćaj se na staru stavku osim ako je `PARTIAL` ili `BLOCKED`.
