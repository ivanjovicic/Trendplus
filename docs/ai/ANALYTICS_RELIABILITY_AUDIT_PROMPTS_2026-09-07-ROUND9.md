# Analytics Reliability Audit Prompts - Round 9

Date: 2026-09-07
Repository: `C:\Users\Ivan\source\repos\Trendplus2`
Scope: production analytics surfaces only; standalone Trend, forecast, Shopify, vendor integrations and test-only functionality are excluded.
Queue mode: `direct-user-request`; no queue prompt was claimed and no existing lock was changed.

## Audit Result

This round found one additional concrete gap on `/analytics/inventory`. The two supplier findings from Round 8 were rechecked and remain open as `RQ249` and `RQ250`; they were not duplicated.

| Prompt | Surface | New gap | Primary owner |
|---|---|---|---|
| `RQ251` | `/analytics/inventory` workflow and scheduler panels | Raw workflow/action/scheduler status tokens are rendered directly to users despite tone helpers. | Inventory frontend status projection |

## Evidence Map

### RQ251: raw operational status labels

- `Klijent/clientapp/src/components/inventory/ActionWorkflowPanel.tsx:51-56` renders `item.actionType`, `item.status` and `item.priority` directly. Tone helpers only select CSS classes.
- `Klijent/clientapp/src/components/inventory/MailSchedulerPanel.tsx:106-109` appends `schedule.lastRunStatus` verbatim after the localized timestamp.
- `Klijent/clientapp/src/types/analytics.ts:1534-1608` permits open-string runtime values for these fields, so unknown/future tokens are possible.
- `Klijent/clientapp/src/components/inventory/inventoryUtils.ts:442-448` contains tone mapping but no user-facing label mapping.
- `Klijent/clientapp/src/components/inventory/ActionWorkflowPanel.spec.tsx:5-91` covers cost and quantity wording but not status/action-type/scheduler label safety.
- Git history checked: workflow rendering is from `d75d49e6`/`c65ab34a`; scheduler status rendering is from `c65ab34a`; no later analytics hardening added a safe display projection.

## Trust Matrix for the New Finding

| Stavka | Inventory workflow/scheduler status path |
|---|---|
| Potvrđeno | Raw `actionType`, workflow `status`, priority and `lastRunStatus` are visible in the Inventory analytics UI. |
| Traženi period | Parent Inventory filters exist, but operational status labels carry no own period proof. |
| Efektivni period | Not changed by the label defect; must remain from the parent analytics response. |
| Posmatrani period | Not applicable to status labels; no new period claim is allowed. |
| Data scope | Parent Inventory scope remains authoritative; status text must not expose technical scope tokens. |
| Vreme generisanja | Scheduler run timestamp is displayed separately; status must not imply freshness. |
| Poslednji uspešan refresh | Not proven by `lastRunStatus`; the label fix must not turn a run state into refresh proof. |
| Freshness status | Unknown unless supplied by the existing refresh contract. |
| Data quality status | Workflow status does not prove data quality or recommendation validity. |
| Empty/partial/error stanje | Empty workflow is localized, but unknown/partial operational states have no safe text contract. |
| Recommendation allowed | Owned by existing inventory actionability contract; `RQ251` does not unlock actions. |
| Razlog ograničenja | Existing status token can be technical or unknown; user needs a Serbian explanation, not the raw value. |

## Existing Owners Checked

- `RQ178` owns Inventory actionability and blocked-action semantics.
- `RQ196` owns schedule input validation.
- `RQ206` owns refresh-run success/failure semantics.
- `RQ145` owns broad parity; this prompt is only the concrete Inventory status-label boundary.
- `RQ249` and `RQ250` remain the Round 8 Supplier Decision Hub findings.

## Queue Change

- Added `RQ251` as `WAITING` in `docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md`.
- Preserved the existing `RQ169 READY` marker.
- No prompt was promoted, claimed or marked complete.
- No production code was changed.

## Residual Risk

This remains a static repository audit. Live database, schema/migration, refresh-run, browser-console and deployed-runtime proof remain unverified and are owned by existing queue prompts.
