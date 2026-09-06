# RQ159 evidence

Task ID: RQ159
Queue: docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md
Date: 2026-09-06
Agent/tool: local-session-ivan / Cursor
Delivery target: main
Working branch / PR: main
Main commit SHA: (set after push)
Main verification: pending push
Evidence state: synchronized

## What was done

Aligned DecisionSummaryBar with backend inventory balance semantics:

- Stopped subtracting OOS from low-stock (mutually exclusive backend predicates).
- Relabeled cards to current snapshot: "Trenutno OOS" and "Niska zaliha" (no fake 7d risk).
- Null counts → "Nije dostupno"; measured zero stays 0.

## Files changed

- `Klijent/clientapp/src/components/inventory/DecisionSummaryBar.tsx`
- `Klijent/clientapp/src/components/inventory/DecisionSummaryBar.spec.tsx`
- queue + lock + this evidence

## Validation run

```text
npm run test -- --run src/components/inventory/DecisionSummaryBar.spec.tsx → 5 passed
npm run typecheck → pass
```

## Validation not run

- Full frontend suite / live browser (STAB16)
- InventoryPage export parity for these counts (KPI cards already use balance fields directly)

## Documentation impact

Queue completion + run log.

## What was missed

None for scoped acceptance.

## Risks

Low: wording change may surprise operators who relied on "P1 OOS 7d" label; the old label was incorrect.

## Next

RQ160 READY (synthetic inventory health trend).
