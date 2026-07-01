# Trendplus Prompt Batch Review Policy

Date: 2026-07-01
Repo: `ivanjovicic/Trendplus`
Status: prompt-system maintenance policy
Adapted from: AgentsWatch batch review rule and MathLearning prompt-evidence discipline

## Purpose

Prompt-system docs can become stale quickly when many queue/audit/rule files are updated. This policy prevents the AI workflow itself from becoming the next source of bugs and token waste.

## Trigger

Run a batch review after 3-5 important commits that change any of:

- prompt queues
- analytics reliability addendums
- AI/agent rules
- evidence standards
- stabilization/status docs
- queue routing/index docs
- prompt prep/hardening docs

Also run it when:

- two queue files disagree about next READY prompt;
- a prompt references a deleted/renamed file;
- a prompt is marked ready but still needs a contract decision;
- multiple new audit findings overlap older prompts;
- an agent reports token waste caused by stale docs.

## Batch review scope

Review only docs involved in the batch plus their routing/index docs.

Default read set:

1. `docs/ai/AGENT_START_HERE.md`
2. `docs/ai/PROMPT_QUEUE_PROTOCOL.md`
3. `docs/ai/ANALYTICS_RELIABILITY_PROMPT_PRIORITY_REVIEW.md`
4. changed queue/audit/rule docs from the last 3-5 prompt-system commits

Do not use batch review as an excuse for whole-repo analysis.

## Checks

A batch review must check:

- broken references or paths
- stale `Current READY prompt` declarations
- duplicate prompts for the same feature family
- prompts marked ready despite blocked gates
- missing validation/evidence requirements
- contradictions between `AGENT_START_HERE`, protocol, safety gate, priority review and queues
- old prompts superseded by newer refined prompts
- excessive token requirements caused by unnecessary read-first docs
- missing follow-up prompts for discovered issues

## Output format

```text
Batch window:
Docs reviewed:
Broken references:
Stale statuses:
Duplicate/overlap prompts:
Ready prompts downgraded:
Docs updated:
Prompts added:
Prompts not added:
Validation:
Residual risk:
Next READY prompt:
```

## Rule for too many issues

If review finds more than three unrelated issues, do not fix everything in one commit. Add focused follow-up prompts or a small prep document.

## Validation

Docs-only default:

```bash
git diff --check
```

If validation cannot run, say so and record residual risk. Do not claim docs are validated just because they were edited.
