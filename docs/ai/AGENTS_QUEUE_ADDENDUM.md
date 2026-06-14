# AGENTS.md Addendum — Prompt Queue Workflow

Dodaj ovaj sadržaj na kraj postojećeg `AGENTS.md`.

## Prompt queue workflow

Ako postoji `docs/ai/NEXT_PROMPT_QUEUE.md`, agent mora da radi po queue pravilima.

### Pravila

1. Uzmi prvi task sa `Status: TODO`.
2. Ne preskači taskove bez eksplicitnog zahteva korisnika.
3. Ne radi više od jednog taska po sesiji/commitu.
4. Pre izmene postavi status na `IN_PROGRESS`.
5. Posle rada postavi `DONE`, `PARTIAL` ili `BLOCKED`.
6. Dodaj belešku u task:
   - datum
   - commit SHA ako postoji
   - promenjeni fajlovi
   - provere
   - rizik
   - sledeći korak
7. Ako je task `BLOCKED`, ne prelazi na sledeći task osim ako korisnik eksplicitno kaže.
8. Ako je task `PARTIAL`, sledeći task treba biti follow-up za partial gap, osim ako queue kaže drugačije.

### Stop rules za queue

Stani ako:
- task traži više od 6–8 fajlova
- build/test pada dva puta
- nema jasnog source-of-truth
- endpoint/security/cache pattern nije jasan
- potrebni su secrets ili produkcioni pristup
- postoji rizik od broad rewrite-a

### Finalni izveštaj

Agent mora da završi porukom:

```text
Queue task:
- Qxx title

Status:
- DONE/PARTIAL/BLOCKED

Promenjeno:
- ...

Provere:
- ...

Rizici:
- ...

Sledeće:
- Qyy title
```
