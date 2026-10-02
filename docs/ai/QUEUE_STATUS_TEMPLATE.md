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
Recovery classification (required when no READY task is selected):
- Candidate:
- Blocker class: stale/satisfied | circular same-prompt artifact | repo-local proof | external/provider/deployed evidence | product/security/tenant authority | active owner/path collision | genuine missing owner
- True start gate or final acceptance only:
- Safe repo-local slice available: yes/no + why
- Alternate collision-safe lane checked:
- Exact unblock event if still blocked:
Remaining:
- ...
```

Do not use `TODO` or `OPEN` for live queue entries. `docs/ai/NEXT_PROMPT_QUEUE.md` is a historical ledger, not the live router.


When a queue is idle, do not fill this template with only "Current READY: none". Apply the Idle recovery and Mandatory blocker decomposition from `docs/ai/PROMPT_QUEUE_PROTOCOL.md`. A performance/test baseline owned by the prompt itself is not a prerequisite to promotion; external deployed proof may remain a residual when the repository-local slice is independently safe.
