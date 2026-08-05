# Queue Status Template

Koristi ovaj šablon unutar svakog taska u `NEXT_PROMPT_QUEUE.md`.

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
Notes:
- ...
Remaining:
- ...
```

Do not use `TODO` or `OPEN` for live queue entries.