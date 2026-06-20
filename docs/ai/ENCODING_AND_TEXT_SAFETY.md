# Encoding and Text Safety

## UTF-8 requirement

- All source files and docs should be UTF-8.
- Serbian Latin text must preserve `č ć š đ ž`.
- If a file contains user-facing Serbian text, verify the saved text is still readable after the patch.

## Mojibake detection list

Search for these patterns when touching Serbian UI or docs:

- `Ä`
- `Å`
- `â`
- `�`
- `DobavljaÄ`
- `marÅ`
- `osveÅ`
- `uÄ`
- `Å¡`
- `Å¾`

## Correct examples

- `Dobavljač`
- `marža`
- `osvežavanje`
- `zalihe`
- `preporuka`
- `pouzdanost`

## Wrong examples

- `DobavljaÄ`
- `marÅ¾a`
- `osveÅ¾avanje`

## Safe fix protocol

1. Fix text only.
2. Do not change business logic in the same commit unless the task explicitly requires it.
3. Run frontend guardrails and build checks.
4. Mention UTF-8 or encoding cleanup in the commit body when the change is dedicated text repair.

## Practical workflow

- Prefer targeted search before editing:
  - `rg -n "Ä|Å|â|�|DobavljaÄ|marÅ|osveÅ|uÄ|Å¡|Å¾" docs Klijent/clientapp/src`
- Re-open the changed file after patching if the text is important user copy.
- If the cleanup is growing beyond text, split it into a dedicated follow-up commit.

## Optional tooling plan

The current frontend guardrail script checks business-logic drift, not encoding drift.

Future task:

`Add check:encoding script and wire it into check:analytics-guardrails or CI.`

That future script should:

- scan docs and frontend source
- fail with `file:line`
- avoid touching runtime logic
- be introduced in a small dedicated task
