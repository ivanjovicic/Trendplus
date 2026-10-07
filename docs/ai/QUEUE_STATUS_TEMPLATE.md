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
- Ownership transfer: none | <previous owner -> new owner; authority/evidence/date>
- ...
Post-close routing recovery (required after every terminal queue transition):
- Recovery base origin/main SHA:
- Completed/changed task IDs searched across active queues:
- Active owner queue/addendum files scanned:
- Newly satisfied dependencies:
- Promoted successor: <task id | none>
- Pre-claim/older `Next: none` reused: no

Zero-READY proof (required only when promoted successor = none):
- Candidate:
- Current status / priority:
- Named owner / active-claim check:
- Dependencies re-verified from current main:
- Blocker class: stale/satisfied | circular same-prompt artifact | repo-local proof | external/provider/deployed evidence | product/security/tenant authority | active owner/path collision | genuine missing owner
- True start gate or final acceptance only:
- Unblock action attempted:
- Result of unblock attempt:
- Safe repo-local slice available: yes/no + why:
- Meaningful disjoint split attempted: yes/no + result:
- Alternate collision-safe lane / next program checked:
- Unknown workspace edit evidence (paths/diff/owner, if applicable):
- Why no safe split exists:
- Exact unblock event if still blocked:
- Full active owner queue/addendum scan complete: yes/no
Remaining:
- ...
```

Do not use `TODO` or `OPEN` for live queue entries. `docs/ai/NEXT_PROMPT_QUEUE.md` is a historical ledger, not the live router.


When a queue is idle, do not fill this template with only "Current READY: none". Apply the **Mandatory no-READY action ladder**, Idle recovery, blocker decomposition and **Post-close dependency cascade** from `docs/ai/PROMPT_QUEUE_PROTOCOL.md`. Any terminal status/dependency/evidence change invalidates an older zero-READY conclusion. A final `none` requires a **Zero-READY proof** from the current post-delivery `origin/main`; if the full active owner queue/addendum set was not scanned, recovery is incomplete and "no READY" must not be claimed. A performance/test baseline owned by the prompt itself is not a prerequisite to promotion; external deployed proof may remain a residual when the repository-local slice is independently safe.
