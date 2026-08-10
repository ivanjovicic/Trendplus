# Prompt Implementation Audit — 2026-08-10

Repo: `ivanjovicic/Trendplus`
Scope: recent implementation/governance commits on `main`, checked against the prompt acceptance criteria and current routing.
Result: **PARTIAL / repaired routing** — several implementations are sound, but two backend-CI evidence obligations were never fully observed and two real backend assertion repairs were left WAITING with no runnable route.

## Executive summary

The recent work did not reveal a reason to revert runtime changes. The main defects were governance/evidence gaps:

1. `BCI04` correctly triaged the real backend suite after restore/build were fixed, but its remaining `RQ89` and `RQ90` repair prompts were left WAITING even after their dependencies were satisfied. That left `BCI01` PARTIAL with no direct runnable repair path.
2. `BCI02` implemented the intended coverage/artifact cascade behavior, but its own completion evidence says live GitHub Actions annotation shape was not verified after the workflow change.
3. `BCI03` implemented a backend `.slnf`, available JavaScript SDK pins and pin regression checking, but its prompt explicitly said to finish PARTIAL if Windows/Visual Studio compatibility could not be verified. Its evidence says Visual Studio IDE load was not tested.
4. The prompt-queue validator did not include `BACKEND_CI_REPAIR_PROMPT_QUEUE.md`, and its task-ID regex did not recognize `BCIxx`, so the newly added Planning Governance workflow could not actually validate BCI task routing/status integrity.
5. Premium UI P-UI-03 implementation does satisfy the row-count/truncation requirement; no runtime fix is needed there. Its historical completion note simply predates the later combined commit that carried the implementation.

## Commit-to-prompt review

| Commit | Intended work | Audit verdict | Evidence / remaining gap |
|---|---|---|---|
| `568f03c65891e96bf2c0f27592aeea96c2e58361` | BCI01 backend restore/build bootstrap | Correctly PARTIAL | Restore/build reach the backend project graph and the suite executes; real assertions remain red, so BCI01 must not be DONE. |
| `0f1743521628ae3fbc76361ffb19477901f35ec4` | BCI02 + BCI03 | Implementation useful; evidence incomplete | BCI02 log says live GHA annotation verification was not run. BCI03 log says Visual Studio IDE load was not run despite the original prompt's PARTIAL rule. |
| `ad1d86bfd15253c93f09a27b2c305342ea770332` | analytics workflow host/contracts and related prompt fixes | BCI bootstrap stayed unblocked; suite still red | Recorded GHA run reached restore/build success and test failure, which is the expected handoff into BCI04 root-cause repair. |
| `3ca8103243537098ae3ef5583f773d39b7915f3a` | shared dashboard controls/table surface including P-UI work | Acceptance spot-check passes | `AnalyticsDataTable` exposes row count/truncation support and dashboard call sites provide explicit top-limit copy where required. |
| `5db83e15b3326cd2ad62b68409d3173da203483b` | quarantine optional embeddings from startup | Consistent with current product gate | Optional AI no longer needs to crash production startup; disabled mode remains explicit rather than returning fake embedding output. No prompt contradiction found in this audit. |
| `0ba3e824cb39336f52b67848849ed61fda5a5708` / `34486dd8d755346d4ff134ece00acbde3c3d4d8f` | governance repair from this audit | Fixed | BCI IDs and both BCI queue files are now covered by `check-prompt-queues.mjs`; self-test includes a BCI Current READY/status mismatch case. |
| `83247129ecf5f5a9fb8e93b41d5370fd94492ace` | RQ89 routing repair | Fixed | RQ89 is the single current analytics-correctness READY task and includes exact focused checks plus BCI handoff. |
| `a17fa54c375a9a4023bf72beb778de1b3465b9e9` | RQ90 sequencing | Fixed | RQ90 remains WAITING after RQ89 for clean root-cause attribution, then requires full backend suite evidence. |

## BCI04 failure ownership reconciliation

BCI04 recorded four real failure families after bootstrap repair:

| Root-cause family | Owner | Current audit state |
|---|---|---|
| Access import test-host route/auth contract | STAB09 | DONE |
| Data Quality top-offender count/order | RQ77 | DONE |
| Data Quality top-offender dataScope SQL | RQ78 | DONE |
| Inventory list cached route/count | RQ89 | **READY** |
| Analytics actions canonical filter/search/paging | RQ90 | WAITING after RQ89 |

Therefore the current execution path is intentionally:

`RQ89 -> RQ90 -> complete backend suite -> GitHub Actions evidence -> BCI01 DONE decision`

Do not promote unrelated analytics work ahead of this sequence while the backend suite is still known red.

## BCI02 evidence gap

### What the implementation did correctly

- restore/build/test workflow steps have stable IDs;
- coverage absence after a failed/skipped test does not invent a second root failure;
- coverage absence after a successful test remains a real error;
- artifact absence warns rather than masking the primary fault;
- PostgreSQL health check uses the configured user/database.

### What was not proven

The BCI02 evidence log explicitly records that live GitHub Actions annotation shape after merge/push was not verified. That is an original acceptance/evidence gap, not a reason to undo the workflow logic.

### Resolution

Tracked by `BCI05` in `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md`. BCI05 runs after RQ89/RQ90 and owns final full-suite/GHA restore/build/test/coverage/artifact proof.

## BCI03 evidence/status gap

### What the implementation did correctly

- created `Trendplus2.Backend.slnf` as the canonical non-IDE backend path;
- replaced unavailable `Microsoft.VisualStudio.JavaScript.Sdk/1.0.3864779` pins with an available `1.0.3982316` pin;
- added `scripts/check-javascript-sdk-pins.mjs` and a quality-gate check;
- proved backend filter restore/build and `dotnet restore Trendplus2.sln --force`;
- proved POS npm build in that pass.

### Prompt mismatch

BCI03 said: if Windows/Visual Studio compatibility cannot be verified, finish PARTIAL rather than guess. The evidence log explicitly says Visual Studio IDE open was not run, yet the parent queue records BCI03 as DONE.

This audit treats the implementation as complete enough to keep, but **does not treat the original Windows/VS acceptance proof as closed**.

### Resolution

Tracked by `BCI06` in `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md`. It is deliberately P2 / WAITING so it cannot delay the P0 backend assertion sequence. It must use a real Windows/Visual Studio-capable environment or remain waiting/blocked.

## P-UI spot-check

P-UI-03 required the shared table surface to make top-N truncation explicit rather than presenting limited rows as the complete dataset. Current code exposes `rowCount` and optional `truncationLabel`, and the dashboard passes a `Prikazano top 10 od ... redova` label when the source has more rows than the visible top set.

Verdict: no repair prompt needed for P-UI-03 from this audit.

Historical completion notes that said a commit had not yet been created are retained as point-in-time evidence; later combined commits should be used for commit mapping rather than rewriting those historical notes.

## Governance validator defect and repair

Before this audit, `scripts/check-prompt-queues.mjs`:

- did not list `docs/ai/BACKEND_CI_REPAIR_PROMPT_QUEUE.md` in `ACTIVE_QUEUE_FILES`;
- did not include `BCI\d+` in `TASK_ID_PATTERN`.

Consequently, `.github/workflows/planning-governance.yml` could run successfully without validating BCI task IDs or its `Current READY` pointer.

Repair:

- add `BCIxx` to the task-ID grammar;
- add the parent BCI queue and evidence addendum to active queue validation;
- add a self-test fixture where `Current READY prompt: BCI99` points to `Status: WAITING` and must fail.

## Changes made by this audit

- `scripts/check-prompt-queues.mjs`
  - BCI grammar + BCI queue coverage + BCI self-test.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_INVENTORY_SIGNALS_ADDENDUM.md`
  - RQ89 promoted to READY with exact BCI04 evidence and checks.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_ACTION_OUTCOME_ADDENDUM.md`
  - RQ90 serialized after RQ89 with exact focused/full-suite handoff.
- `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
  - current BCI assertion-repair override added; stale 2026-08-05 runnable pointers replaced.
- `docs/ai/BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md`
  - BCI05 full suite/GHA proof.
  - BCI06 Windows/Visual Studio compatibility proof.
- `MASTER_ROADMAP.md`
  - canonical current route changed to RQ89 -> RQ90 -> backend full-suite/GHA proof.

## Runtime changes

None in this audit.

RQ89/RQ90 are correctness-sensitive backend changes. They were intentionally queued rather than implemented from repository metadata alone because their acceptance depends on reproducing focused tests and distinguishing route/service/test-fixture causes before changing production behavior.

## Validation required on current main

The governance commits should be considered fully closed only when the current-main Planning Governance workflow runs:

```text
node scripts/check-prompt-queues.mjs --self-test
node scripts/check-prompt-queues.mjs
node scripts/check-planning-architecture.mjs --self-test
node scripts/check-planning-architecture.mjs
```

If that workflow fails, repair the validator/queue contradiction before starting RQ89. Do not bypass the check.
