# Decision Alternatives Contract

Status: authoritative DEX20 docs-only contract  
Date: 2026-08-20  
Related:

- `docs/architecture/DECISION_EXPLAINABILITY_CROSS_FAMILY_READINESS.md`
- `docs/roadmaps/DECISION_INTELLIGENCE_ROADMAP.md` Alternative decisions
- Product Decision Center `ProductDecisionAlternativeRecommendationDto` (DEX07 baseline)
- `docs/planning/QUEUE_REFILL_2026-08-20.md`

## Purpose

Freeze alternatives as **first-class backend objects** (or explicit absence). A later UI or executive-board slice must not invent a ranked second-best, hide missing trade-offs as an empty-success list, or score alternatives in the client.

This contract is safe to ignore. Live ranking stays unchanged until a later prompt authorizes a new alternatives API or persistence.

## Non-goals

- no new alternatives endpoint
- no schema migration
- no LLM-generated alternatives
- no frontend-local ranking
- no inventory-forecast runtime or materializer work

## Source of truth

| Layer | Authority |
|---|---|
| Product Decision Center alternatives | Backend `AlternativeRecommendations` / Why-panel copy of the same list |
| Other families (inventory, supplier, executive) | Backend list or explicit `alternativesStatus=absent` |
| This file | Cross-family shape, absence, and no-fake rules |
| Frontend | Render and label only |

The selected recommendation remains the family's backend primary decision. Alternatives never outrank it locally.

## First-class alternative object

When present, each alternative must expose:

| Field | Meaning | Must not be |
|---|---|---|
| `rank` | Order below the selected decision (`1` = next-best among listed alternatives) | A client-computed score |
| `recommendationStatus` | Same vocabulary as the selected decision family | A free-text label used as status |
| `recommendationLabel` / `recommendedAction` | Operator-facing action | A second invented primary recommendation |
| `reason` / `reasonCodes` | Why this option exists | Parsed from Why prose |
| `whyLowerRanked` | Why it is not selected | Empty success |
| `confidenceLevel` and reliability/confidence fields when the family already has them | Trust of **this** alternative | Copied silently from the selected decision |
| `dataQualityStatus` | Evidence quality of this alternative | Healthy if missing |

Eligibility is implied by presence on the backend list. A row that is not eligible must be omitted, not listed with a fake rank.

## Absence vs empty success

| State | Meaning | UI/export |
|---|---|---|
| `present` | Backend sent one or more alternative objects | Render the list |
| `absent` | Family does not yet expose alternatives | No list; not “0 alternatives” as a KPI |
| `ineligible` | Selected decision is not allowed / fallback / insufficient | Do not synthesize alternatives |
| `unknown` | Payload missing the field | Treat as absent, not empty-success |

An empty JSON array is `absent` unless the backend also sets an explicit `alternativesStatus=present` with zero eligible options. Do not show a green “no better option” from a missing field.

## Family reuse

DEX07 Product Decision Center is the baseline. Other families may reuse this shape only when:

1. the backend owns the list;
2. missing alternatives stay `absent`;
3. `whyLowerRanked` (or equivalent) is backend text, not inferred from reason codes;
4. table, detail and export use the same objects.

Executive Decision Board must not invent alternatives from product cards. If a card has no alternative payload, the board shows absence.

## No-fake rules

1. Do not invent a second-best from prose, notes, or reason codes.
2. Do not rank in the frontend.
3. Do not treat missing alternatives as a successful empty list or `0` count KPI.
4. Do not generate alternatives with an LLM in this program.
5. Do not copy the selected decision's confidence onto an alternative unless the backend sent alternative-specific values.
6. `recommendationAllowed=false` does not authorize synthetic alternatives.

## Acceptance

- cross-family alternatives vs absence is citeable;
- PDC remaining the only runtime list until a later prompt;
- no runtime API or schema change in DEX20.
