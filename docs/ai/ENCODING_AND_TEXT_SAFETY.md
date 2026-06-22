# Encoding and Text Safety

## UTF-8 requirement

- All source files and docs should be UTF-8.
- Serbian Latin text must preserve `č ć š đ ž`.
- If a file contains user-facing Serbian text, verify the saved text is still readable after the patch.

## Mojibake detection list

Search for these patterns when touching Serbian UI or docs:

- `Ã„`
- `Ã…`
- `Ã¢`
- `ï¿½`
- `DobavljaÃ„`
- `marÃ…`
- `osveÃ…`
- `uÃ„`
- `Ã…Â¡`
- `Ã…Â¾`

## Correct examples

- `Dobavljač`
- `marža`
- `osvežavanje`
- `zalihe`
- `preporuka`
- `pouzdanost`

## Wrong examples

- `DobavljaÃ„`
- `marÃ…Â¾a`
- `osveÃ…Â¾avanje`

## Safe fix protocol

1. Fix text only.
2. Do not change business logic in the same commit unless the task explicitly requires it.
3. Run frontend guardrails and build checks.
4. Mention UTF-8 or encoding cleanup in the commit body when the change is dedicated text repair.

## Practical workflow

- Prefer targeted search before editing:
  - `rg -n "Ã„|Ã…|Ã¢|ï¿½|DobavljaÃ„|marÃ…|osveÃ…|uÃ„|Ã…Â¡|Ã…Â¾" docs Klijent/clientapp/src`
- Re-open the changed file after patching if the text is important user copy.
- If the cleanup is growing beyond text, split it into a dedicated follow-up commit.

## Optional tooling plan

The frontend guardrail suite now includes `npm run check:encoding`.

That script:

- scans maintained docs and frontend source
- fails with `file:line`
- avoids touching runtime logic
- stays separate from business-logic checks so encoding regressions are obvious

Future follow-up, if needed:

- wire `check:encoding` into broader CI workflows that already run docs or frontend checks
- add a repo-wide cleanup pass for intentionally excluded legacy surfaces if they become active again
