# Inventory Decision Contract Audit

Date/time: 2026-06-19 15:48:41 +02:00
Verification HEAD: `ee23a61d43665630b163ddac43c29e49b09ed1c1`

## Purpose

Audit Q44 inventory decision confidence and nullable-impact behavior against the shared decision contract.

## Files reviewed

- `docs/Analytics/DECISION_CONFIDENCE_CONTRACT.md`
- `Klijent/clientapp/src/pages/InventoryPage.tsx`
- `Klijent/clientapp/src/components/inventory/inventoryUtils.ts`
- `Klijent/clientapp/src/components/inventory/types.ts`
- `Klijent/clientapp/src/components/inventory/InventoryItemsTable.tsx`
- `Klijent/clientapp/src/components/inventory/SKUDetailModal.tsx`
- `Klijent/clientapp/src/components/inventory/DecisionSummaryBar.tsx`
- `Klijent/clientapp/src/pages/__tests__/InventoryPage.signalActions.spec.ts`
- `Klijent/clientapp/src/components/inventory/InventoryItemsTable.spec.tsx`
- `Klijent/clientapp/src/pages/__tests__/InventoryPage.queueStatus.spec.tsx`

## Surface mapping

| Surface | Contract source | Missing-signal behavior | Classification | Notes |
| --- | --- | --- | --- | --- |
| Inventory list rows | `signalConfidencePct` | Signal confidence is nullable in row data and display helpers show `Nije dostupno` / `Nedovoljno podataka` instead of inventing a percentage | SAFE | List/table confidence is not shown as `0%` when backend omits it |
| Inventory detail modal | `signalConfidencePct`, `dataQualityStatus` | Detail copy stays descriptive and does not synthesize a confidence value | SAFE | The modal reads the row signal as-is |
| Inventory action queue spec | `expectedImpactRsd` | Missing cost/evidence now resolves to `null` instead of a fake zero | FIXED | This was the concrete fake-zero gap found in Q44 |
| Decision summary / workflow panels | `dataQualityWarning`, workflow counts | Panels stay descriptive and do not invent confidence | SAFE | These are operational counters, not confidence scores |

## Risk audit

- `signalConfidencePct` is already nullable in the inventory row contract and the visible UI honors that.
- The main regression risk was `expectedImpactRsd` falling back to a derived `0` when cost evidence was missing.
- That gap has been fixed by resolving expected impact from real evidence only, returning `null` when evidence is incomplete.
- Inventory still has a broader display-value follow-up: some operational value widgets use derived `estimatedValueAmount`, which is fine for current presentation but could be revisited later if the product wants full nullability parity on every value chip.

## Tests added

- `Klijent/clientapp/src/pages/__tests__/InventoryPage.signalActions.spec.ts`

## Checks

- `git diff --check` - pass, with repository line-ending warnings only
- `cd Klijent/clientapp && npm run check:analytics-guardrails` - pass
- `cd Klijent/clientapp && npm run test -- --run src/pages/__tests__/InventoryPage.signalActions.spec.ts` - pass
- `cd Klijent/clientapp && npm run build` - pass

## Outcome

Q44 is complete on the repo side: inventory confidence remains nullable, and action impact no longer fabricates a zero when the evidence is missing.

## Follow-up

- If we later want full nullability parity on every inventory value widget, the next pass should review `estimatedValueAmount` rendering separately from action impact contract fields.
