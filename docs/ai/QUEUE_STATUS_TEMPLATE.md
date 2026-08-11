# Queue Status Template

Koristi ovaj šablon unutar trenutnog owner-queue taska ili planning zadatka koji vodi `docs/ai/PROMPT_QUEUE_PROTOCOL.md`.

```text
Status: READY | WAITING | IN_PROGRESS | DONE | PARTIAL | BLOCKED | OBSOLETE
Started:
Finished:
Commit:
Changed files:
Checks:
- dotnet build:
- dotnet test:
- npm run check:analytics-guardrails:
- npm run build:
Delivery mode:
Main commit SHA:
Main verification:
Notes:
- ...
Remaining:
- ...
```

Do not use `TODO` or `OPEN` for live queue entries, and do not treat `docs/ai/NEXT_PROMPT_QUEUE.md` as the live router.
