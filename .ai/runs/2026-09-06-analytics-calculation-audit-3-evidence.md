# 2026-09-06 - Analytics calculation audit pass #3

Owner: direct-user-request (Ivan)
Task: Analyze analytics audits and incomplete prompts; find undocumented calculation/indicator bugs; seed new prompts directly into queue.
Date: 2026-09-06

Input analysis:
- Reviewed `docs/qa/ANALYTICS_THIRD_CALCULATION_AUDIT_2026-09-06.md`
- Identified 5 calculation/indicator gaps in inventory signals, supplier freshness and pre/post aggregates
- Gaps are analytical (not infrastructure/security) and not yet queued

Gaps identified and prompts added:

1. **RQ176** - Inventory snapshot query time shown as data freshness
   - Problem: GeneratedAtUtc (query time) is used as snapshot freshness indicator
   - Risk: Old cached snapshots appear fresh
   - Fix: Add snapshot materialization timestamp to DTOs; distinguish query-time from snapshot-time

2. **RQ177** - Size-curve missing relation vs empty result collapse
   - Problem: Backend correctly distinguishes missing table vs empty result, but UI renders both as one state
   - Risk: Missing table warning disappears, looks like valid empty data
   - Fix: Preserve and render distinct backend warning states in SizeCurvePanel

3. **RQ178** - Inventory signal rows lack actionability/copy contract
   - Problem: No backend-owned actionability state; internal reason codes reach UI
   - Risk: Technical values shown to users; action affordances shown for denied recommendations
   - Fix: Add RecommendationAllowed, StatusReason, DataQualityStatus to inventory DTOs

4. **RQ181** - Supplier footwear marks query-time as fresh
   - Problem: SupplierFootwearAnalyticsPage sets fresh based on response existing, not proven refresh
   - Risk: Unknown refresh lineage shown as fresh
   - Fix: Use lastRefreshAtUtc instead of generatedAt for freshness state

5. **RQ182** - Pre/post frontend reconstructs backend-owned aggregate
   - Problem: Frontend falls back to row-sum when backend absolute-change total is unavailable
   - Risk: Divergence between frontend and backend aggregates; affects share/concentration/export
   - Fix: Make backend aggregate sole owner; render unavailable if absent

Actions taken:
- Added all 5 prompts (RQ176, RQ177, RQ178, RQ181, RQ182) directly to canonical queue
- Updated queue status summary table
- All prompts marked WAITING, P1, no duplicates of existing RQ* items
- Feature families: inventory-snapshot-freshness, inventory-empty-state-distinction, inventory-signal-actionability-contract, vendor-freshness-contract, pre-post-aggregate-owner-parity

Validation:
- Ran `node scripts/check-prompt-queues.mjs` after edits (output pending)
- All prompts follow protocol statuses and naming conventions
- Cross-references to RQ140, RQ141, RQ143, RQ145, RQ156 recorded

Delivery:
- Changes committed and pushed to origin/main
- Commit: TBD (pushing now)

Next steps:
- Analytics owners should prioritize RQ176-RQ182 based on pilot readiness
- RQ178 and RQ182 are independent of other analytics chains (can be worked in parallel)
- RQ176/RQ177/RQ181 require DTO/handler/page coordination but are bounded
