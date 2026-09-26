# AGENTS.md — Trendplus AI Agent Standard

Owner: `agent-system`

This is the repository-level rulebook for Codex, Copilot agent mode and other AI agents that inspect or change Trendplus.

Current code, focused tests and executable tooling override stale prose. When agent documents disagree, use the canonical owner table below instead of combining conflicting rules.

## 1. Mission and non-negotiable product outcome

Trendplus should become a reliable pilot/sales product for footwear/apparel retail.

Analytics must help a user understand:
- what is selling;
- where real margin exists;
- where dead stock is accumulating;
- which suppliers deserve focus;
- which data should not be trusted;
- which concrete action should be taken next.

Do not add another screen when an existing decision surface still fails to explain its period, freshness, data quality, fallback state or recommendation rationale.

## 2. Start here, then read narrowly

Do not preload every AI document.

Before editing:
1. classify the request;
2. identify one owning subsystem and the exact expected files;
3. inspect the target source and nearest focused test/guardrail;
4. read only the canonical guidance that can change the implementation decision;
5. define the narrowest credible completion proof.

Use these guides only when relevant:
- agent entrypoint: `docs/ai/AGENT_START_HERE.md`;
- architecture ownership: `docs/ai/ARCHITECTURE_BOUNDARIES.md`;
- validation selection: `docs/ai/VALIDATION_SELECTOR.md`;
- queue semantics: `docs/ai/PROMPT_QUEUE_PROTOCOL.md`;
- text/encoding safety: `docs/ai/ENCODING_AND_TEXT_SAFETY.md`;
- known failure patterns: `docs/ai/COMMON_FAILURES_AND_FIXES.md`;
- evidence/run-log standard: `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md` and `.ai/RUN_LOG_TEMPLATE.md`.

If a referenced canonical owner is missing, duplicated or contradictory, do not invent a replacement rule. Record the authority gap and use current code/tests plus the safest reversible same-owner behavior until the gap becomes material.

## 3. Request classification and autonomy

Treat questions as exceptional. Assume the user may be offline after assigning work.

Classify the request before touching queue state:
- **formal queue prompt assigned** → follow the queue protocol and claim it automatically;
- **`next` / `continue` / queue work** → use the canonical selector/router and claim the first safe candidate automatically;
- **direct repository task** → do not invent or steal a queue claim; record `Queue: direct-user-request` and continue in a scoped delivery path;
- **read-only review/audit** → inspect only unless repair is explicitly authorized;
- **existing active claim** → resume it, complete it, block it or explicitly hand it off.

A direct repository request authorizes normal, reversible repository-scoped work, including inspection, editing, focused tests, formatting, documentation updates, branch/worktree use, commits, pushes, PR creation and permitted merge.

Do not ask routine questions such as whether to inspect source, add a focused regression test, update the owning documentation, choose between two equivalent reversible implementations, commit, push, open a PR or verify `main`.

Ask only when the remaining choice has material product/business impact, tenant/privacy/security/secret implications, destructive data/schema consequences, production impact, external cost, licensing/legal consequences, irreversible effects outside the repository, or unresolved ownership that cannot be safely bounded.

When two safe same-owner choices remain, choose the smaller reversible one and record the assumption.

## 4. No-wandering execution model

Use this state model:

```text
CLASSIFY → CLAIM(if needed) → INTERPRET → PROVE OWNER → PATCH → VALIDATE → DELIVER → CLOSE/HANDOFF
```

Before the first edit, know:
- intended outcome;
- owning subsystem/source of truth;
- working hypothesis;
- expected changed files;
- focused proof;
- stop/handoff trigger.

Rules:
- do not refactor unrelated areas;
- prefer an existing helper/component/pattern over a new abstraction;
- do not add a second owner for an existing contract;
- do not expand into a second independent outcome silently;
- one invalidated implementation hypothesis permits one classified replacement; repeated unchanged failure stops the run;
- do not move from validation back into broad repository discovery without new evidence;
- do not patch product code to compensate for an environment, test-harness or CI failure;
- a small, reversible same-owner scope repair required by executable acceptance is allowed when recorded;
- a second subsystem or genuine owner boundary becomes a split/handoff, not hidden scope expansion.

For large files, use targeted search and relevant slices. Do not load the whole repository or rewrite large files when a focused patch is sufficient.

## 5. Canonical workflow ownership

Keep mechanics in one owner. This root file states policy and links to mechanics; it should not duplicate selector, queue-lock or delivery algorithms.

| Area | Canonical owner |
|---|---|
| Repository entry/orientation | `docs/ai/AGENT_START_HERE.md` |
| Architecture and subsystem boundaries | `docs/ai/ARCHITECTURE_BOUNDARIES.md` |
| Queue selection, claim, statuses, lock/takeover and close semantics | `docs/ai/PROMPT_QUEUE_PROTOCOL.md` |
| Validation choice | `docs/ai/VALIDATION_SELECTOR.md` |
| Evidence/run-log contract | `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md` + `.ai/RUN_LOG_TEMPLATE.md` |
| Encoding/text safety | `docs/ai/ENCODING_AND_TEXT_SAFETY.md` |
| Repeated failure guidance | `docs/ai/COMMON_FAILURES_AND_FIXES.md` |

`MASTER_ROADMAP.md` resolves cross-program priority when the queue protocol requires it. `docs/ai/NEXT_PROMPT_QUEUE.md` is a historical ledger and is not an active router unless the queue protocol explicitly promotes it.

Do not introduce a new status, claim mechanism, local lock format or selector fallback in this file. Use exactly the statuses and ownership semantics defined by the queue protocol.

## 6. Queue work

Queue mechanics have one owner: `docs/ai/PROMPT_QUEUE_PROTOCOL.md`. Do not duplicate the selector, promotion, takeover or lock algorithm here.

For formal queue work:
- resolve the owner/program from `MASTER_ROADMAP.md`, then select/claim through the canonical protocol;
- treat `Current READY` as the primary/default pointer, not as a global mutex; execute one claimed prompt at a time **per agent/workspace**;
- when `Current READY` is `none`, run the protocol's **Idle recovery** before reporting no work: re-evaluate stale dependency/status truth, relevant `PARTIAL/BLOCKED/WAITING` prompts and recent run-log `What was missed` / `Risks` / `Next`, then promote and claim only a genuinely runnable candidate;
- after completing a prompt, re-enter selection/recovery when the user asked to continue/claim-and-execute instead of stopping only because the pointer returned to `none`;
- stop only for a genuine authority/gate/owner conflict or when the canonical router proves there is no safe repository-local action left.

A mechanical prompt/routing defect may be repaired without asking when the authoritative owner and acceptance are clear, the repair stays same-owner and evidence records it. Direct user work does not need to become a queue prompt before implementation.

## 7. Delivery truth

For file-changing work:

```text
local diff / local commit / pushed branch / open PR != Done
```

`Done` requires the repository's permitted delivery path plus honest proof that the delivered change is on the intended target branch. When the target is `main`, record the exact delivered SHA and verify current `main` contains it.

If delivery cannot be completed safely, preserve useful work using the permitted branch/PR path, record the blocker and use the canonical non-Done status. Do not claim success from local state alone.

Do not force-push, reset/clean unrelated user work, bypass branch protections, expose secrets, mutate production data or perform destructive actions outside the assigned scope unless an explicit authoritative workflow permits the exact action.

### 7.1 Main-first delivery (default)

Unless the task explicitly names another target branch, file-changing work must be **delivered on `main`**, not left on a feature branch or open PR.

Default completion path:

```text
implement → focused local validation → merge/push to main → verify origin/main contains implementation SHA → close with evidence
```

Rules:

- treat branch/PR work as transport only; opening or updating a PR is not completion;
- merge or fast-forward to `main` and push when repository policy allows it;
- do **not** wait for GitHub/Origin CI to finish before delivering to `main`;
- do **not** subscribe, poll or block closure on CI unless the assigned prompt explicitly makes a named check part of acceptance;
- record CI honestly in evidence as `queued`, `running`, `not inspected`, `green` or `red`; CI state is residual risk/follow-up, not a substitute for main SHA verification;
- use `PARTIAL` only when `main` cannot be updated safely (branch protection, merge conflict outside scope, missing permissions). Never use `PARTIAL` merely because CI is still running.

## 8. Validation discipline

Choose the narrowest proof through `docs/ai/VALIDATION_SELECTOR.md`.

Evidence order:

```text
reproducer/current-contract check
→ smallest changed-file/static check
→ nearest focused behavior + counterexample/regression test
→ mapped guardrail/documentation checks
→ wider build/test only for a named wider risk
```

Rules:
- add the smallest regression test that would fail before a runtime fix when practical;
- test failure/empty/stale/retry/fallback semantics where relevant, not only success;
- do not run full suites first by habit;
- do not repeat an unchanged failing or timed-out command without new evidence;
- classify failures as product, test, environment/tooling, prompt/contract or evidence failure before editing again;
- a skipped command is `not run` with a reason, never inferred as passing;
- CI that is queued has not proved anything yet;
- do not claim GitHub/remote validation without inspecting the relevant result;
- CI completion is not required before delivering to `main`; deliver after focused local proof unless a prompt explicitly blocks on a named remote check.

Typical commands are examples, not mandatory checklists:

Frontend:
```powershell
cd Klijent/clientapp
npm run check:analytics-guardrails
npm run build
npm run test -- --run <path-to-spec>
```

Backend:
```powershell
dotnet build
dotnet test
```

Analytics migration inspection when the change touches that surface:
```powershell
dotnet ef migrations list `
  --project .\Infrastructure\Infrastructure.csproj `
  --startup-project .\Api\Api.csproj `
  --context AnalyticsDbContext
```

## Code Review Rules

### Analytics trust boundaries

- Flag changes that turn backend errors, unknown values, stale/partial data or successful empty results into fake zero-valued KPIs or an apparently successful state. Safe path: preserve the established meta contract and distinct frontend error, empty and degraded states.

### Decision and period provenance

- Flag changes that recreate final business recommendations in the frontend or mix requested period, effective/observed period, data scope, freshness or data quality. Safe path: keep decision semantics on the backend and render the authoritative metadata and reason on every affected decision surface.

### Review-fix discipline

- When fixing a confirmed review finding, keep the patch within the owning subsystem, add the smallest meaningful regression proof when practical, and do not weaken tenant, authorization, secret-handling or migration safety to make a review pass. If the finding is not proven, document the uncertainty instead of guessing.

Use non-watch test mode for agent runs.

## 9. Evidence and completion

Every non-trivial file-changing task needs a durable run log under:

```text
.ai/runs/<yyyy-mm-dd>-<task-id>-evidence.md
```

Use `.ai/RUN_LOG_TEMPLATE.md` and the current `docs/ai/AGENT_RUN_EVIDENCE_STANDARD.md`. Do not maintain a second dated completion schema in this file.

At minimum record:
- interpreted outcome and exact owner;
- files read and changed;
- assumptions, prompt defects and scope repairs;
- validation executed, failed, skipped and why;
- what was not completed;
- documentation impact;
- branch/PR/merge or direct-delivery evidence when applicable;
- exact target-branch/main verification;
- residual risks and next owner/step.

Final reports must be concise and truthful. Use the status vocabulary owned by the queue/delivery protocol; never invent a root-file-only status.

## 10. Core analytics invariants

These are product invariants. A narrower owning document or executable contract may define more detail, but agents must not violate these semantics silently.

### 10.1 No fake zero

A backend error or unknown value must never appear as a valid `0 RSD` KPI.

Backend: use the established error/meta contract such as `AnalyticsResponseMetaFactory.Error(...)` or `Results.Problem(...)`.

Frontend: render the established analytics error state and do not substitute zero-valued KPIs for failures.

### 10.2 Empty is not error

A successful empty dataset is a distinct state:
- success remains true;
- an empty reason is available;
- data quality reflects insufficient evidence when appropriate;
- the frontend renders the established empty state, not an error and not fake values.

### 10.3 Backend is decision source of truth

Decision semantics belong on the backend when the contract already owns them. Typical fields include:
- recommendation/status;
- confidence and/or reliability;
- reason codes;
- data quality status;
- decision score when part of the contract.

The frontend maps and explains these values; it should not silently recreate business scoring logic.

### 10.4 Formatting, theme and text safety

- Reuse shared formatters such as `fmtRsd`, `fmtPct`, `fmtNumber` and `fmtSignedPct` when present.
- Do not create local duplicate formatters for the same semantics.
- Use established theme tokens/CSS variables; do not hardcode replacement colors without a design-system reason.
- Preserve UTF-8 and Serbian diacritics. Fix confirmed mojibake only in the owned scope; do not turn an unrelated task into a repository-wide encoding rewrite.

## 11. Backend and API rules

Core analytics endpoints should use the established meta contract (`AnalyticsResponseMetaDto` or its current replacement) consistently:
- success with data → success;
- successful empty result → empty;
- fallback/partial/stale → warning/degraded state;
- true failure → error/problem response.

When compatibility requires preserving an existing response shape, prefer a backward-compatible optional meta extension rather than a breaking rewrite unless the owning contract explicitly says otherwise.

Error logging must not cause a second failure:
- cap/trim unsafe or oversized persisted summaries;
- include a correlation identifier where the existing infrastructure supports it;
- keep full technical details in the proper log sink and a safe summary in user/queryable storage.

## 12. Frontend decision-surface rules

A core analytics page should make trust visible where relevant:
- requested/effective period;
- freshness/refresh state;
- data quality;
- warning/fallback state;
- error vs empty distinction;
- methodology/help;
- explanation of recommendation/action;
- export/report only when useful to the decision.

Do not expose raw backend codes to users when an established mapping exists.

Routing guardrails:
- keep lazy/Suspense runtime routing intact when a smoke test can be fixed instead;
- do not remove compatibility routes without a replacement/redirect plan;
- do not change global theme defaults from an unrelated task;
- use `docs/Frontend/ROUTING_AND_SMOKE_TEST_STANDARDS.md` for detailed routing ownership.

## 13. Decision-specific invariants

### Supplier Scorecard
Must distinguish requested/effective period and dataset, avoid silent fallback, expose whether recommendation is allowed, warn on degraded/fallback evidence, avoid fake zero and explain empty/insufficient states.

### Product Decision
A real recommendation should expose the established equivalents of status, label/action, reason/reason codes, confidence/reliability and data quality. Do not present an actionable recommendation without an understandable “why”.

### Inventory
The primary surface should support inventory decisions such as replenishment, OOS risk, dead stock, transfer/rebalance and workflow/action handling. Export/scheduling must not displace the core decision surface.

### Reports
A report is a sales/decision artifact. Preserve period, freshness, data quality, methodology, warnings, printable presentation and graceful export failure handling.

## 14. Stop / handoff conditions

Stop implementation and record a precise blocker/handoff when:
- material source-of-truth, tenant authority or business contract remains unclear;
- the fix requires a second independent subsystem/owner that cannot be safely split;
- the same classified failure repeats without new evidence;
- required proof is unavailable and no narrower valid proof exists;
- secrets, production access, destructive schema/data operations or unresolved security/legal decisions are required;
- the canonical queue/router/delivery workflow has no safe action left.

Do not stop for a routine reversible engineering decision that the existing code, tests or canonical owner can resolve.
