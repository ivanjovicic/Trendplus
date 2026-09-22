# Supplier + Shoe Type value/correctness audit

Task ID: supplier-shoe-type-value-correctness-audit
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-22
Agent/tool: ChatGPT GPT-5.6 Sol via GitHub connector
Delivery target: main
Working branch / PR: direct collision-safe main delivery
Main commit SHA: pending-this-commit
Main verification: pending-this-commit
Evidence state: synchronized

## Findings

- Supplier visible filtering currently narrows rows without guaranteeing that KPI/chart/export denominators narrow with it.
- Display population and recommendation reference cohort must be separate concepts; focusing one supplier must not turn the recommendation benchmark into self-comparison.
- Supplier and Shoe Type endpoints both use arithmetic means of row margin percentages as aggregate/recommendation baselines; this can materially bias decisions when row revenue weights differ.
- AnalyticsMarginPolicy already supplies the correct aggregate basis: margin contribution over revenue with usable cost.
- Supplier has no runtime Zod response validation. Shoe Type has a Zod boundary but decision-critical margin/cost/recommendation fields remain too permissive through passthrough.
- Supplier and Shoe Type total pre/post impact use broad summed pre/post values while comparable coverage/actionability is tracked separately; total impact must use the same comparable cohort or be explicitly labelled broader observation.
- Generic detail routes lag the row recommendation/trust contract; Shoe Type unknown identity is not independently resolvable by the numeric-only server detail path.

## Queue impact

- Strengthened RQ373-RQ375.
- Retained RQ376/RQ377 as the correct comparable-cohort/detail owners.
- Added RQ378 Supplier weighted margin/cost truth.
- Added RQ379 Supplier runtime validation.
- Added RQ380 Supplier comparable pre/post aggregate truth.
- Removed obsolete live wording that WAITING is required by a one-READY invariant.

## Value assessment

Revenue/units are already the strongest evidence on both screens. Margin and recommendation value are high but not yet trustworthy enough for blind action until weighted benchmark and cost provenance fixes land. Pre/post is useful descriptive evidence, not causal uplift. Detail/export are operationally valuable after parity prompts close.

## Validation

Planning/queue documentation only. Runtime application code was not changed in this audit. Planning Governance should validate the queue/roadmap after delivery.
