# Prompt Queue Protocol

Updated: 2026-10-04
Repo: `ivanjovicic/Trendplus`

This protocol defines live prompt-queue governance. Cross-program routing lives in `MASTER_ROADMAP.md`; feature/product lifecycle lives in `docs/planning/FEATURE_LIFECYCLE.md`.

## Active queue families

Existing execution programs:

- `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md` + `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md` (`BCI`)
- `docs/ai/STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md` (`STAB`)
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` and SQL queue (`RQ` / `Q`)
- `docs/ai/ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md` (`P-UI`)
- `docs/ai/DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md` (`QDB`)
- `docs/ai/MULTITENANCY_PROMPT_QUEUE.md` (`MT`)
- `docs/ai/GENAI_PRODUCT_PROMPT_QUEUE.md` (`GAI`)

Future planning programs:

- `docs/ai/DECISION_INTELLIGENCE_PROMPT_QUEUE.md` (`DEX`, `RL`, `DT`)
- `docs/ai/PLATFORM_EVOLUTION_PROMPT_QUEUE.md` (`PERF`, `OBS`, `SEC`)

`docs/ai/NEXT_PROMPT_QUEUE.md` is a historical ledger and is not a live router.

## Canonical selection rule

1. Read `MASTER_ROADMAP.md`.
2. Resolve the owning program, its `Current READY` primary/default pointer, and the full set of READY candidates in that program.
3. Preserve the existing global priority before considering lower-priority programs.
4. Treat `P-UI` as a supplemental presentation lane: it may run only when path-safe and it must not displace BCI/STAB/RQ/QDB/MT/GAI priority or repair analytics correctness through frontend invention.
5. Start only a prompt whose status is READY, whose dependencies are satisfied, and whose feature-family/path/owner/gate collision checks are clear. Prefer the `Current READY` pointer for a simple `next` request, but do not serialize unrelated READY lanes behind it.
6. Do not resurrect a DONE/PARTIAL/WAITING prompt because an older addendum says it was once next.
7. A future planning READY (`DEX/RL/DT/PERF/OBS/SEC`) authorizes only its documented planning/contract scope. It does not authorize runtime implementation or outrank higher-priority gates.

## Idle recovery when there is no READY prompt

`Current READY prompt: none` is a routing state, not a reason to stop. For a user instruction such as `next`, `continue`, `claim`, or `claim and execute`, the agent must run this recovery sequence before reporting that no work is available.


### Zero-READY proof and dependency cascade (mandatory)

A statement such as **`Current READY prompt: none`**, **`no READY prompt`**, **`no safe successor`** or **`no safe claimable task`** is a positive claim that must be proved from the **current post-delivery `origin/main`**. It is never inherited from an older queue header, run log, completion note or previous agent. Treating an older **`none`** as **none is the last conclusion** without a fresh Zero-READY proof is invalid.

**A previous zero-READY conclusion becomes invalid immediately when any of these happens:**
- a prompt changes to `DONE`, `PARTIAL`, `BLOCKED` or `OBSOLETE`;
- a named dependency changes state or its evidence is synchronized;
- a new prompt, owner decision, oracle, fixture, baseline, deployment fact or run log lands;
- a branch/PR/commit that can satisfy a dependency reaches `main`;
- a blocker is reclassified from a start gate to final/deployed acceptance.

After any such event, the agent MUST recompute routing before writing `Next: none` or leaving the queue pointer at `none`.

#### Post-close dependency cascade

After delivering or closing a queue prompt, and **before** declaring no successor:

1. Refresh `origin/main` and use the resulting post-delivery SHA as the recovery base. Do not reuse the SHA from before the implementation/closure commit.
2. Search the owning program's **entire active queue set**, including every active addendum, for the completed/changed task ID and for any other dependency whose status changed in the run.
   - For Analytics/RQ this means all `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md` files plus the active SQL analytics queue, not only the addendum that contained the completed prompt.
3. Re-evaluate every dependent prompt's `Ready after`, `Dependencies`, status, owner and path collision against current code and synchronized evidence.
4. Then scan **all non-terminal prompts** (`WAITING`, `PARTIAL`, `BLOCKED`) in the owning program for stale/satisfied/circular/external-only blockers. This second pass is mandatory because dependencies can be indirect or described in prose rather than by exact task ID.
5. Only after dependency completeness is established, check active locks/branches/PRs/path collisions. A stale branch name is not a blocker; verify whether it contains unique current work.
6. If any prompt is dependency-complete, authorized and collision-safe, repair stale routing and promote it `WAITING -> READY` in the same recovery run. Do not leave it WAITING merely because an older completion note said `none`.
7. If the first candidate is still blocked, continue through other independent lanes in the same program, then the next eligible program.
8. If and only if no candidate is runnable, write a **Zero-READY proof** in the run evidence with:
   Treat **`none` as the last conclusion** only after steps 1–7 on the post-delivery SHA; **none is the last conclusion**, never inherited from an older header, run log or completion note.
   - recovery base `origin/main` SHA;
   - active queue/addendum files scanned;
   - every plausible non-terminal candidate checked;
   - blocker class for each;
   - whether the blocker is a true start gate or final acceptance only;
   - why no safe repo-local slice exists;
   - exact event that would unblock the next candidate.

If the agent cannot inspect the full active queue set or cannot verify current post-delivery `origin/main`, it MUST NOT claim that no READY work exists. Report the recovery as incomplete instead.

#### Explicitly prohibited shortcuts

The following are **not sufficient evidence** for a zero-READY conclusion:
- the queue header currently says `Current READY prompt: none`;
- the just-completed prompt's old `Next: none` line;
- the previous agent said there was no safe work;
- only the three or four prompts nearest the current one were checked;
- the highest-priority P0 is externally blocked;
- a production/browser/provider proof is missing when repo-local implementation/proof is independently safe;
- CI is queued/in progress;
- a historical branch/lock name exists without proof of an active conflicting owner.

For queue closure, none is the last conclusion, never the starting assumption (never inherit a prior `Current READY: none` without recomputing).

**Default bias: find safe progress, not a reason to refuse.** A blocker written in an old prompt is a claim to verify, not an eternal fact. The agent must distinguish:
- a true start gate from evidence needed only for final/deployed acceptance;
- external/provider/business authority from repository-local work that can proceed independently;
- a dependency that another task owns from evidence/artifacts this same prompt is supposed to create;
- a real owner collision from stale lock/branch/status metadata;
- an unsafe broad scope from a safe same-owner bounded slice that preserves the product contract.

A prerequisite is **circular** when it requires an artifact that the prompt itself owns creating (for example, requiring a performance baseline before the performance prompt whose first step is to produce that baseline). Repair that routing defect instead of treating it as a blocker.

### Mandatory blocker decomposition

Before leaving any candidate `WAITING`, `BLOCKED` or `PARTIAL` during idle recovery, classify every named blocker into one of these buckets:

| Blocker class | What to do |
|---|---|
| already satisfied / stale metadata | repair status/dependency text from current code, commits and synchronized evidence |
| same-prompt artifact (circular prerequisite) | move artifact creation into the prompt's executable steps; do not require it before promotion |
| repository-local proof | run or define the bounded proof (focused test, disposable DB measurement, static/runtime fixture, contract reconciliation) and promote when the start gate is then satisfied |
| external/provider/deployed evidence | keep it as residual/final acceptance unless the implementation is unsafe without it; do not block independent repo-local work merely because deployed verification is still missing |
| product/business/security/tenant decision | stop that decision-dependent slice; do not invent authority; search for another collision-safe prompt/slice |
| active owner/path collision | respect the owner; try another independent candidate/program |
| genuine missing owner/work item | de-duplicate first, then add the smallest prompt in the existing owner queue; promote immediately only if dependency-complete |

If a prompt mixes executable repo-local work with external final proof, the agent may **repair/narrow the prompt before claim** so the repo-local acceptance is explicit and the external proof remains a named residual/follow-up. This is permitted only when the owner, business semantics and safety boundaries do not change. Do not silently lower acceptance or mark deployed behavior verified.

### Recovery order

1. **Refresh routing truth.** Fetch current `origin/main` and record the exact recovery-base SHA. Read `MASTER_ROADMAP.md`, the owning queue header **and the entire active addendum set**, plus all current `READY` / `IN_PROGRESS` rows. After a task closure, this refresh MUST happen from the post-delivery SHA. Do not trust an older audit's “next” or “none” sentence.
2. **Finish already-started work and unfinished delivery first.**
   - Resume the agent/workspace's own active claim.
   - Inspect recent relevant run logs plus known task branch/PR state before selecting new work. If a valid implementation/proof exists on a branch or PR but `main` does not contain it, finish the permitted merge/push to `main`, resolve only in-scope conflicts, verify the delivered SHA, and synchronize evidence before moving on. Do not merge stale/unverified transport work blindly.
   - Do not steal a live claim from another owner.
   - If an `IN_PROGRESS` row is only stale metadata and current `main` plus its run log already prove delivery, reconcile it to the truthful terminal status before selecting new work.
   - A takeover is allowed only when current evidence proves the old claim is abandoned/stale and there is no active conflicting lock/branch/PR/owner. Record the takeover evidence.
3. **Re-evaluate non-DONE prompts instead of trusting old blockers.** First run the Post-close dependency cascade for every task/dependency that changed state in the current run; then inspect **all** `PARTIAL`, `BLOCKED` and `WAITING` candidates across the program's active queue/addenda in program priority, then task priority. Verify every named dependency against current code, commits and synchronized run evidence, then apply the Mandatory blocker decomposition above.
   - If a dependency is already satisfied, repair the stale dependency/status text.
   - Detect circular prerequisites: if the missing baseline/report/fixture/measurement is an output this prompt itself owns, make it step 1 of the prompt rather than a precondition.
   - Separate external final proof from safe repo-local work. Provider logs, production browser proof, exact-deployed SHA checks or remote CI may remain residual acceptance without blocking local implementation when the implementation can be proved safely on deterministic repository-owned fixtures.
   - If a blocker was repository-local and can be removed by bounded evidence work (for example a focused test, disposable PostgreSQL/Testcontainers measurement, status/evidence reconciliation, missing contract note or same-owner queue repair), do that work in the same run.
   - If the prompt is too broad but one same-owner slice is independently safe, rewrite the prompt before claim to make that slice explicit, preserve the unchanged business contract, and leave the external/owner-gated remainder as residual or follow-up.
   - If a `WAITING` prompt is now dependency-complete, collision-safe and authorized by its repaired/current scope, promote `WAITING -> READY` and claim it in the same run. This is an evidence-based promotion, not arbitrary auto-promotion.
4. **Read the latest relevant agent evidence.** For the candidate owner/family, inspect the newest applicable `.ai/runs/*-evidence.md` files and their `What was missed`, `Risks` and `Next` sections.
   - If a concrete unfinished same-owner acceptance item already has a prompt, use that prompt.
   - If a concrete repo-local follow-up is not queued, first prove it is not a duplicate, then add the smallest prompt to the existing owning queue.
   - A newly added prompt may be promoted immediately only when its dependencies are already satisfied and collision/gate checks are clear.
5. **Audit remaining queue truth.** Check non-DONE prompts for stale dependencies, delivered-but-not-closed work, obsolete duplicates, contradictory ownership, missing evidence links and prompts left `WAITING` only because of an old one-READY convention. Repair same-owner governance defects before declaring the queue empty.
6. **Try the next eligible program.** If the current program is genuinely exhausted or externally blocked, continue through the cross-program priority in `MASTER_ROADMAP.md`. Do not resurrect historical ledgers or lower-priority runtime work that bypasses a higher-priority gate.
7. **Use productive unblock work when runtime execution is impossible.** A queue-execution request authorizes bounded repository-local analysis/tests/docs that can remove a blocker or produce a well-scoped next prompt. Prefer work that changes readiness truth: a deterministic reproducer, fixture, baseline, contract proof, stale-gate repair, collision classification or missing owner prompt. Do not manufacture cosmetic busywork merely to avoid an empty queue.
8. **Try another safe lane before refusing.** If the highest-priority candidate still needs real external/owner authority, search the same program for a disjoint dependency-complete lane, then the next eligible program. A blocked P0 does not automatically forbid unrelated repository-local work unless the master roadmap says the gate is globally exclusive.
9. **Stop only after the router is truly exhausted.** “No prompt” by itself is not an acceptable final result. The agent may report **no safe claimable task** only after a post-delivery Zero-READY proof on current `origin/main` proves that every plausible remaining candidate requires unresolved external/business/security/tenant/production authority, unavailable secrets/provider access, or a genuine conflicting active owner. The final report and durable run evidence must include the recovery-base SHA, active queue/addendum files scanned, candidates checked, blocker class for each, why no repo-local slice is safe, and the exact event that would unblock the next action.

### Promotion preference during idle recovery

When several `WAITING` prompts become runnable, prefer:

1. P0/P1 correctness, data-integrity, security or release-truth work;
2. an explicit `Ready after` dependency that has just become DONE;
3. concrete `What was missed` / `Next` follow-up from recent synchronized evidence;
4. the smaller bounded task with clearer focused proof;
5. presentation/polish only after correctness work of the same owner is clear.

Do not promote two overlapping prompts merely to keep multiple agents busy. Independent prompts may both be READY only when the normal parallel-safety rules are satisfied.


### Anti-overblocking examples

- **Performance baseline:** if a performance prompt says "Ready after baseline exists" but its scope says to measure before/after, that baseline is a same-prompt artifact. Move it into step 1 and promote if the other dependencies are satisfied. Do not invent a speedup target before measuring.
- **Provider logs vs repo-local fix:** if provider logs are needed to name the exact production root cause, but current code already exposes a safely testable error contract/query-shape/cache defect, execute the repo-local prompt and leave provider/deployed root-cause proof under the STAB/operations owner.
- **Pending CI:** queued/in-progress CI is not a start blocker unless the prompt names that exact remote check as a gate. Classify already-red relevant CI, but do not refuse unrelated local work merely because CI has not finished.
- **Owner decision:** when a threshold/policy decision is genuinely owner-gated, do not invent it. Look for explainability, characterization, fixture, parity or unrelated-path work that does not choose the policy.
- **PARTIAL prompt:** do not resume a PARTIAL prompt blindly. Identify what acceptance remains; if the remainder is external but a separate same-owner repo-local follow-up is executable, repair/split routing and promote the executable owner rather than declaring the whole queue blocked.
- **RQ487 precedent (2026-10-02):** provider logs remained a STAB residual while the repository-local performance baseline/query-bounds work was promoted. The baseline was moved from a circular `Ready after` clause into the prompt's first step.

## Status model

Use these statuses exactly:

| Status | Meaning | Agent may start? |
|---|---|---|
| READY | Runnable, unclaimed prompt. A program may have multiple READY prompts when they are independently safe. | Yes, subject to master priority/dependencies/collision checks |
| WAITING | Valid prompt that is dependency-blocked, collision-prone, owner-gated or intentionally deferred. | No |
| IN_PROGRESS | Claimed by one owner/workspace. Multiple independent IN_PROGRESS prompts may coexist. | Only the claiming owner/workspace continues that prompt |
| BLOCKED | Missing dependency/decision/evidence that prevents safe progress. | No |
| PARTIAL | Useful work exists but acceptance/proof/delivery is incomplete. | No unless an explicit follow-up says so |
| DONE | Acceptance met with synchronized evidence and delivery truth. | No |
| OBSOLETE | Replaced by current evidence/prompt. | No |

`TODO`, `OPEN`, `COMPLETE`, `NEEDS_EVIDENCE_SYNC`, and other free-form live statuses are invalid.

Evidence synchronization is not a queue status. Use the separate evidence field defined by `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`:

```text
Evidence state: synchronized | pending | fallback <reason>
```

If implementation is useful but evidence/delivery verification is incomplete, use `PARTIAL`; use `BLOCKED` only when a real blocker prevents safe completion.

## READY invariants

- A program may have zero, one or multiple READY prompts. Multiple READY prompts are valid only when each is dependency-complete and the active set is collision-safe.
- `Current READY` is the **primary/default** routing pointer for deterministic `next` behavior. It is not a global mutex and does not make other READY tasks unclaimable.
- Zero READY/IN_PROGRESS is valid only when the owner queue/current-READY table and `MASTER_ROADMAP.md` explicitly declare `none` **and a current post-close Zero-READY proof has been performed after the latest dependency/evidence/status change**. An older `none` declaration is invalidated by any such change.
- Multiple programs and multiple independent feature families inside one program may be active concurrently; global program priority still comes from `MASTER_ROADMAP.md`.
- `Parallel-safe: no` makes that feature family/owned surface exclusive; it does **not** serialize unrelated feature families. Multiple READY/IN_PROGRESS tasks in the same feature family require `Parallel-safe: yes` on every active task in that family.
- Parallel-safe never means dependency, owner, release, security, tenant or production gates can be skipped.
- Current READY (or explicit `none`) must be declared near the queue top or in the queue's per-program current-READY table. When the primary task is IN_PROGRESS, the pointer may continue to name it while other independent READY tasks remain claimable.
- Dependent, overlapping or owner-gated prompts remain WAITING. Do not keep an otherwise independent prompt WAITING solely to satisfy a one-READY convention.
- A follow-up/evidence addendum belongs to the same program as its parent queue and shares the same collision domain.

## Required prompt sections

Every new live prompt must contain:

1. `Problem`
2. `Evidence`
3. `Scope`
4. `Read first`
5. `Do`
6. `Tests`
7. `Acceptance`
8. `Dependencies`

Existing legacy prompts may retain richer historical templates, but new prompt families must not omit these eight sections.

## Mechanical prompt conflicts

A stale file count, old "next READY" sentence, contradictory `Avoid paths` line or similar mechanical defect is not by itself a blocker when the owner program, current `READY` pointer and acceptance outcome are otherwise clear.

When this happens:

1. Keep the owner program, current `READY` pointer and acceptance stronger than stale prose.
2. Take the smallest same-owner repair needed to make the prompt executable.
3. Record the scope repair or prompt defect in the completion note/run evidence.
4. Do not use this exception to cross into another program, tenant authority, schema/API authority, secrets or production-only decisions.

## Local lock rule

Before runtime implementation of a READY prompt, create an uncommitted local lock:

```text
.ai/task-locks/<task-id>-<agent>.lock.md
```

Planning-only prompts may use a lock when several agents are active, but the lock must never be committed.

Suggested content:

```md
# Local task lock
Task: <id>
Agent: <agent>
Status: IN_PROGRESS
StartedAtUtc: <timestamp>
Branch: <branch>
Feature family: <family>
Exclusive area: <paths/contract>
```

## Claim workflow

1. Refresh current `main`/remote state.
2. Read `AGENTS.md`, `.github/copilot-instructions.md`, `docs/ai/AGENT_START_HERE.md`, `MASTER_ROADMAP.md`, this protocol and the target prompt.
3. Verify the queue still declares the selected task READY. The selected task may be the primary pointer or another READY candidate.
4. Verify dependencies and global priority.
5. Confirm no active READY/IN_PROGRESS task, lock, branch or PR owns a conflicting feature family/path where that evidence is available.
6. Create local lock for implementation work.
7. Work only inside Scope.
8. If extra scope crosses an owner/program boundary, stop as PARTIAL/BLOCKED and create a separate follow-up plan. A smallest same-owner mechanical repair allowed above is recorded and may continue.
9. Run exact tests/checks.
10. Record changed files, checks, remaining risk and next status.
11. Delete local lock before commit.
12. **Post-close routing recovery is mandatory.** After the implementation/closure reaches `main`, refresh `origin/main`, run the dependency cascade and either promote the next runnable prompt or record a full Zero-READY proof. Never copy the pre-claim `Next`/`none` state into the completion note.

## Main-first delivery

Unless a prompt explicitly names another target branch, file-changing queue work must be **delivered on `main`**.

Required close path:

```text
focused local proof → merge/push to main → verify origin/main SHA → synchronized evidence → DONE
```

Rules:

- branch/PR-only state is transport, not completion;
- do not wait for CI to finish before merging/pushing to `main`;
- do not subscribe or poll CI as a default close step;
- record CI honestly as residual risk or `Checks not run`; CI does not replace main SHA verification;
- use `PARTIAL` only when `main` cannot be updated safely, never merely because CI is queued/running.

Canonical owner for the full policy: `AGENTS.md` section 7 and `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`.

## Collision rules

Do not start **implementation** when:

- another active owner holds the same task/feature family;
- the prompt is not READY;
- the task overlaps a higher-priority exclusive path;
- a **true start dependency** is not DONE/accepted after blocker decomposition;
- runtime work is being inferred from a planning-only READY;
- the task would duplicate another queue's owner family;
- production/deploy/auth/tenant decisions are required to make the implementation safe and are absent.

These rules do not forbid the read-only/metadata recovery needed to determine whether a blocker is stale, circular, external-only or owner-gated. Repair routing truth first; then promote before implementation.

## Reliability rules

Prompts affecting analytics/decision/reporting values must specify the relevant contract facts:

- source of truth;
- unit (ratio vs percent unit, currency, quantity, etc.);
- numerator/denominator when relevant;
- true-zero behavior;
- missing/unknown behavior;
- no-baseline behavior;
- freshness/fallback behavior;
- affected API/UI/detail/chart/export/report/action surfaces;
- before/after compatibility when business meaning changes.

Missing evidence must not silently become zero, healthy, fresh, maintain, measured, confident or another trusted-looking default.

## Tenant/security rules

Tenant-sensitive prompts must specify:

- canonical tenant source and membership authority;
- missing/mismatched tenant behavior;
- DB/raw-SQL paths;
- cache keys/invalidation;
- jobs/outbox/imports;
- storage/documents/exports;
- two-tenant negative-test plan;
- dedicated-deployment compatibility.

Caller-provided tenant/store/source/path identity is not authorization by itself.

## Date/count/surface rules

- Date-only filters should normally use explicit whole-day half-open semantics unless a contract says otherwise.
- Returned/visible count must not be labelled total matching count without evidence.
- Table/detail/chart/export/report/action values must preserve the same semantics or explicitly document a conversion.

## Stop conditions

After Mandatory blocker decomposition, mark BLOCKED/PARTIAL rather than guessing when:

- business contract is unclear and no same-owner characterization/explainability slice avoids choosing that contract;
- source of truth is unclear and cannot be resolved from current code/tests/evidence;
- tenant/authorization source is unclear;
- evidence required to make the **current executable slice** safe cannot be produced, and no narrower truthful acceptance exists;
- fix needs unrelated files/programs and cannot be split into an owned follow-up;
- two queues define inconsistent ownership that current evidence cannot reconcile;
- a real dataset/provider decision is required **before implementation can be safe**, not merely for later deployed verification;
- implementation would hide unknown as zero/green/fresh/measured;
- performance/security/AI work would weaken correctness or release gates.

Do not use this section to re-create broad blockers already decomposed into external final proof plus independently safe repository-local work.

## Completion note

A completed or partially completed prompt records at minimum:

```md
### Completion note

- Date:
- Status: DONE | PARTIAL | BLOCKED
- Completion:
- Changed files:
- Contract/runtime behavior changed:
- Checks run:
- Checks not run:
- Run log:
- Evidence state: synchronized | pending | fallback <reason>
- Delivery mode:
- Main commit SHA:
- Main verification:
- Missed:
- Follow-up:
- Residual risk:
- Next:
- Prompt defect / scope repair:
```

Production/live smoke may be marked complete only from real current deployment evidence.

### Remote CI classification after main delivery

Main-first delivery does not require waiting for GitHub Actions, but an already-triggered relevant run must not be ignored when it is available before the final evidence is written.

- After the target commit is on `main`, inspect the relevant current-main workflow run if one was triggered for the changed paths and is discoverable.
- Record the run id/SHA and truthful state as `queued`, `in_progress`, `green` or `red`. Do not use `not inspected` merely because remote CI is not a named acceptance gate.
- A queued or in-progress run does not block `DONE` when the prompt's focused acceptance is otherwise satisfied; do not poll indefinitely or delay main delivery just to wait for CI.
- If the relevant current-main run is already red, inspect the failing job/step/log far enough to classify it as in-scope regression, pre-existing failure, unrelated concurrent change, or environment/tooling failure.
- A proven in-scope regression must be fixed (or the prompt must remain non-DONE) before final evidence. A pre-existing/unrelated red run may remain residual risk only with the concrete run id and classification recorded.
- Never infer remote validation from an older SHA when a newer relevant current-main run is the one exercising the delivered code.

All new or actively refreshed completion notes use the current evidence contract:

- `Run log:` is mandatory and points to durable `.ai/runs/...` evidence or `fallback <reason>`;
- `Evidence state:` is mandatory and is separate from queue status;
- `Delivery mode:`, `Main commit SHA:` and `Main verification:` are mandatory for file-changing work;
- older completion notes remain historical evidence and are not retroactively normalized unless a task is actively refreshing them.

For every non-trivial file-changing prompt run, also create a durable run log in `.ai/runs/<yyyy-mm-dd>-<task-id>-evidence.md` using `.ai/RUN_LOG_TEMPLATE.md`, or record an explicit fallback reason when a durable log could not be created safely.

Minimum durable run-log sections are owned by `.ai/RUN_LOG_TEMPLATE.md`; do not maintain a second copied section list here.

## Validation

Choose runtime/docs proof through `docs/ai/VALIDATION_SELECTOR.md`.

Run both governance layers when queue/planning governance is changed and the commands are available:

```text
node scripts/check-agent-instructions.mjs --self-test
node scripts/check-agent-instructions.mjs
node scripts/check-prompt-queues.mjs --self-test
node scripts/check-prompt-queues.mjs
node scripts/check-planning-architecture.mjs --self-test
node scripts/check-planning-architecture.mjs
```

`check-prompt-queues.mjs` validates the execution queues it inventories, including the BCI parent queue and BCI evidence addendum. `check-planning-architecture.mjs` validates the master roadmap, owner roadmap/queue symmetry and the DEX/RL/DT/PERF/OBS/SEC planning queues, including explicit zero-READY declarations.

## Commit hygiene

- One prompt per implementation commit unless the prompt explicitly allows a bounded docs consolidation.
- Documentation consolidation may use multiple logical commits by planning family.
- Do not commit `.ai/task-locks/*`.
- Preserve historical evidence; use OBSOLETE/archive/current-pointer language rather than deleting proof.
- If checks were not run, say so explicitly.
